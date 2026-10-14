// Serves a published copy of the Atari 2600 WebAssembly app on 127.0.0.1 with this folder's page,
// and runs it in a real headless Chrome, once per browser launch, each launch fresh so it starts
// from a cold runtime. Prints the lines the page prints, and the browser version.
//
//   node run-in-browser.mjs <published-folder> [launches] [region] [timed-runs] [settle-frames]
//       region: NTSC (default) or PAL
//
// The program is the region's build of the suite's harness/calibration (rule.md, item 10 of the
// plan's "Fixed before running"), from the fork dbhq-uk/missingno at the commit pinned in
// tests/Dbhq.Cpu6502.TestSupport/Pins.cs (Atari2600BinariesCommit). The suite's manifest is
// checked against its SHA-256 in Pins.cs (Atari2600SuiteManifestSha256), and the file against its
// git blob hash in the manifest, every time it is read, as Atari2600Suite.Read does. It is kept
// in .testdata/ where Atari2600Suite.Read keeps it, served at /rom, and never copied into the
// published app or committed (AGENTS.md rule 3). A file that does not match stops the run.
//
// Local measurement only: the server binds to loopback and serves one folder.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const [dir, launchesArg = '1', regionArg = 'NTSC', runsArg = '5', settleArg = '60'] = process.argv.slice(2);
if (!dir || (regionArg !== 'NTSC' && regionArg !== 'PAL')) {
  console.error('usage: node run-in-browser.mjs <published-folder> [launches] [NTSC|PAL] [timed-runs] [settle-frames]');
  process.exit(2);
}
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const published = path.resolve(dir, fs.existsSync(path.join(dir, 'wwwroot')) ? 'wwwroot' : '.');
const chrome = process.env.CHROME_PATH ?? '/usr/bin/google-chrome';

// The pins, read from the one place they are written, so a pin is never typed a second time.
const support = path.join(repo, 'tests', 'Dbhq.Cpu6502.TestSupport');
const pins = fs.readFileSync(path.join(support, 'Pins.cs'), 'utf8');
const pin = (name, pattern) => {
  const m = new RegExp(`public const string ${name} = "(${pattern})";`).exec(pins);
  if (!m) throw new Error(`Pins.cs has no ${name}`);
  return m[1];
};
const commit = pin('Atari2600BinariesCommit', '[0-9a-f]{40}');
const manifestSha256 = pin('Atari2600SuiteManifestSha256', '[0-9a-f]{64}');

const sha256 = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');
const gitBlob = (bytes) => crypto.createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');

const manifest = fs.readFileSync(path.join(support, 'atari-2600-suite.manifest'));
if (sha256(manifest) !== manifestSha256) throw new Error(`atari-2600-suite.manifest does not match Pins.Atari2600SuiteManifestSha256 ${manifestSha256}`);
const romPath = `crates/systems/vcs/tests/accuracy/roms/harness/calibration_${regionArg.toLowerCase()}.a26`;
const entry = manifest.toString('utf8').split('\n').map(l => l.split(' ')).find(([, fork, p]) => fork === 'missingno' && p === romPath);
if (!entry) throw new Error(`the manifest has no ${romPath} in missingno`);
const want = entry[0];

const local = path.join(repo, '.testdata', 'atari-2600', 'missingno', commit, ...romPath.split('/'));
let rom = fs.existsSync(local) ? fs.readFileSync(local) : null;
if (!rom || gitBlob(rom) !== want) {
  const url = `https://raw.githubusercontent.com/dbhq-uk/missingno/${commit}/${romPath}`;
  const response = await fetch(url);
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${url}`);
  rom = Buffer.from(await response.arrayBuffer());
  if (gitBlob(rom) !== want) throw new Error(`${url} does not match its git blob hash ${want}`);
  fs.mkdirSync(path.dirname(local), { recursive: true });
  fs.writeFileSync(local, rom);
}
console.log(`rom: ${romPath} at ${commit.slice(0, 12)}, ${rom.length} bytes, git blob ${gitBlob(rom)}, hash checked`);

const types = {
  '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript',
  '.json': 'application/json', '.wasm': 'application/wasm', '.dll': 'application/octet-stream',
  '.dat': 'application/octet-stream', '.pdb': 'application/octet-stream', '.blat': 'application/octet-stream',
};
const send = (res, type, body) => {
  res.writeHead(200, { 'Content-Type': type, 'Cache-Control': 'no-store' });
  res.end(body);
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  const name = path.normalize(decodeURIComponent(url.pathname)).replace(/^(\.\.[/\\])+/, '');
  if (name === '/rom') {
    return send(res, 'application/octet-stream', rom);
  } else if (name === '/' || name === '/index.html') {
    return send(res, 'text/html', fs.readFileSync(path.join(here, 'index.html')));
  } else if (name === '/main.js') {
    return send(res, 'text/javascript', fs.readFileSync(path.join(here, 'main.js')));
  } else {
    const file = path.join(published, name);
    if (file.startsWith(published) && fs.existsSync(file) && fs.statSync(file).isFile()) {
      return send(res, types[path.extname(file)] ?? 'application/octet-stream', fs.readFileSync(file));
    }
  }
  res.writeHead(404).end();
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const base = `http://127.0.0.1:${server.address().port}/`;

try {
  for (let launch = 1; launch <= Number(launchesArg); launch++) {
    const browser = await chromium.launch({ executablePath: chrome, headless: true });
    if (launch === 1) console.log('browser ' + browser.version());
    const page = await browser.newPage();
    // A page that cannot load the runtime must fail the run at once, not hang
    // until the run's own timeout.
    let fail;
    const failed = new Promise((_, reject) => { fail = reject; });
    failed.catch(() => {});
    page.on('pageerror', error => fail(new Error('page error: ' + error.message)));
    page.on('requestfailed', request => fail(new Error(`request failed: ${request.url()}`)));
    page.on('response', response => {
      if (response.status() >= 400) fail(new Error(`HTTP ${response.status()}: ${response.url()}`));
    });
    try {
      await page.goto(`${base}?region=${regionArg}&runs=${runsArg}&settle=${settleArg}`);
      await Promise.race([
        page.waitForFunction(() => document.body.dataset.done === 'true', null, { timeout: 30 * 60 * 1000 }),
        failed,
      ]);
    } catch (error) {
      await browser.close();
      throw error;
    }
    const text = await page.locator('#log').innerText();
    for (const line of text.split('\n')) console.log(`launch ${launch} ${line}`);
    await browser.close();
  }
} finally {
  server.close();
}
