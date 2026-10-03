// Serves a published copy of the BBC Micro WebAssembly app on 127.0.0.1 with this
// folder's page, and runs it in a real headless Chrome, once per browser launch, each
// launch fresh so it starts from a cold runtime. Prints the lines the page prints, and
// the browser version.
//
//   node run-in-browser.mjs <published-folder> [launches] [timed-cycles] [boot-cycles] [timed-runs] [mode] [screen]
//
// The three ROMs are read from roms/bbc-micro/ and checked against the SHA-256 pinned in
// tests/Dbhq.Cpu6502.TestSupport/Pins.cs before the server starts; a ROM that does not
// match stops the run. They are served at /roms/, never copied into the published app.
//
// Local measurement only: the server binds to loopback and serves one folder.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const [dir, launchesArg = '1', cyclesArg = '2000000', bootArg = '6000000', runsArg = '5', modeArg = '7', screenArg = 'boot'] = process.argv.slice(2);
if (!dir) {
  console.error('usage: node run-in-browser.mjs <published-folder> [launches] [timed-cycles] [boot-cycles] [timed-runs] [mode] [screen]');
  process.exit(2);
}
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const published = path.resolve(dir, fs.existsSync(path.join(dir, 'wwwroot')) ? 'wwwroot' : '.');
const chrome = process.env.CHROME_PATH ?? '/usr/bin/google-chrome';

// The pins, read from the one place they are written, so a pin is never typed a second time.
const pins = fs.readFileSync(path.join(repo, 'tests', 'Dbhq.Cpu6502.TestSupport', 'Pins.cs'), 'utf8');
const pin = (name) => {
  const m = new RegExp(`public const string ${name} = "([^"]+)";`).exec(pins);
  if (!m) throw new Error(`Pins.cs has no "${name}"`);
  return m[1];
};
const roms = new Map();
for (const [name, key] of [['os', 'BbcOs'], ['basic', 'BbcBasic'], ['dfs', 'BbcDfs']]) {
  const relative = pin(`${key}Path`);
  const want = pin(`${key}Sha256`);
  const bytes = fs.readFileSync(path.join(repo, relative));
  const got = crypto.createHash('sha256').update(bytes).digest('hex');
  if (got !== want) throw new Error(`${relative} does not match its pinned hash ${want} (got ${got})`);
  console.log(`${name}: ${relative}, ${bytes.length} bytes, sha256 ${got}, hash checked`);
  roms.set(name, bytes);
}

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
  if (name.startsWith('/roms/')) {
    const rom = roms.get(name.slice('/roms/'.length));
    if (rom) return send(res, 'application/octet-stream', rom);
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
      await page.goto(`${base}?cycles=${cyclesArg}&boot=${bootArg}&runs=${runsArg}&mode=${modeArg}&screen=${screenArg}`);
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
