// Serves a published copy of the NES WebAssembly app on 127.0.0.1 with this folder's page, and
// runs it in a real headless Chrome, once per browser launch, each launch fresh so it starts from
// a cold runtime. Prints the lines the page prints, and the browser version.
//
//   node run-in-browser.mjs <published-folder> [launches] [timed-cycles] [boot-cycles] [timed-runs] [region]
//       region: 0 NTSC (default) or 1 PAL
//
// The ROM is SNOW, from the fork dbhq-uk/nes-test-roms at the commit pinned in
// tests/Dbhq.Cpu6502.TestSupport/Pins.cs. It is downloaded once into .testdata/ (the same place
// NesTestRoms.Read keeps it), checked against its SHA-256 pin every time it is read, and served
// at /rom. It is never copied into the published app and never committed. A ROM that does not
// match its pin stops the run.
//
// With ROM=homebrew in the environment, the ROM is the bundled homebrew, Lan Master, read from
// roms/nes/ (Pins.NesHomebrewPath) and checked against its SHA-256 pin (Pins.NesHomebrewSha256), in
// place of SNOW: the program the thread-time bench's lan-ntsc and lan-pal workloads run, here
// at its title after the boot cycles (added in task 3 of the scanline renderer work).
//
// With PROFILE=<n> in the environment, each launch is also recorded with the DevTools protocol's
// sampling profiler (200 microsecond interval), and the n functions with the most self time are
// printed with their share of all the samples: of the whole page, boot included, or with
// THREAD_TIME (below) of the driver's runs alone. Publish with
// -p:WasmNativeStrip=false so the WebAssembly keeps its function names.
//
// With PROFILE_OUT=<path> as well, each launch's whole profile is written to <path>.<launch>.json
// (the protocol's Profile object: nodes, samples and timeDeltas), for lazy-chips/profile-groups.mjs.
//
// With THREAD_TIME=<n> in the environment, after the page's own runs the driver makes n more runs
// of the timed cycles itself, each read with the DevTools protocol's ThreadTime metric (the CPU
// time of the page's main thread) before and after, and prints each as
// `thread <i> cycles=<n> thread_ms=<ms> mhz=<x> times_real=<x>`. On a shared machine the main
// thread's CPU time moves less with the load than the wall-clock time does.
//
// Local measurement only: the server binds to loopback and serves one folder.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const [dir, launchesArg = '1', cyclesArg = '1790000', bootArg = '5000000', runsArg = '5', regionArg = '0'] = process.argv.slice(2);
if (!dir) {
  console.error('usage: node run-in-browser.mjs <published-folder> [launches] [timed-cycles] [boot-cycles] [timed-runs] [region]');
  process.exit(2);
}
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const published = path.resolve(dir, fs.existsSync(path.join(dir, 'wwwroot')) ? 'wwwroot' : '.');
const chrome = process.env.CHROME_PATH ?? '/usr/bin/google-chrome';

// The pins, read from the one place they are written, so a pin is never typed a second time.
const pins = fs.readFileSync(path.join(repo, 'tests', 'Dbhq.Cpu6502.TestSupport', 'Pins.cs'), 'utf8');
const romName = 'other/snow.nes';
const commit = /public const string NesTestRomsCommit = "([0-9a-f]{40})";/.exec(pins)?.[1];
const want = new RegExp(`\\["${romName}"\\] = "([0-9a-f]{64})"`).exec(pins)?.[1];
if (!commit || !want) throw new Error(`Pins.cs has no commit or no hash for ${romName}`);

const sha256 = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');
const local = path.join(repo, '.testdata', 'nes-test-roms', commit, ...romName.split('/'));
let rom = fs.existsSync(local) ? fs.readFileSync(local) : null;
const homebrew = process.env.ROM === 'homebrew';
if (homebrew) {
  const relative = /public const string NesHomebrewPath = "([^"]+)";/.exec(pins)?.[1];
  const pinned = /public const string NesHomebrewSha256 = "([0-9a-f]{64})";/.exec(pins)?.[1];
  if (!relative || !pinned) throw new Error('Pins.cs has no path or no hash for the homebrew');
  rom = fs.readFileSync(path.join(repo, ...relative.split('/')));
  if (sha256(rom) !== pinned) throw new Error(`${relative} does not match its pinned hash ${pinned}`);
  console.log(`rom: ${relative}, ${rom.length} bytes, sha256 ${sha256(rom)}, hash checked`);
} else if (!rom || sha256(rom) !== want) {
  const url = `https://raw.githubusercontent.com/dbhq-uk/nes-test-roms/${commit}/${romName}`;
  const response = await fetch(url);
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${url}`);
  rom = Buffer.from(await response.arrayBuffer());
  if (sha256(rom) !== want) throw new Error(`${url} does not match its pinned hash ${want}`);
  fs.mkdirSync(path.dirname(local), { recursive: true });
  fs.writeFileSync(local, rom);
}
if (!homebrew) console.log(`rom: ${romName} at ${commit.slice(0, 12)}, ${rom.length} bytes, sha256 ${sha256(rom)}, hash checked`);

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
    const profileTop = Number(process.env.PROFILE ?? 0);
    const threadRuns = Number(process.env.THREAD_TIME ?? 0);
    const cdp = profileTop > 0 ? await page.context().newCDPSession(page) : null;
    const startProfile = async () => {
      await cdp.send('Profiler.enable');
      await cdp.send('Profiler.setSamplingInterval', { interval: 200 });
      await cdp.send('Profiler.start');
    };
    if (cdp && threadRuns === 0) await startProfile();
    try {
      await page.goto(`${base}?cycles=${cyclesArg}&boot=${bootArg}&runs=${runsArg}&region=${regionArg}`);
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
    if (cdp && threadRuns > 0) await startProfile();
    if (threadRuns > 0) await threadTimes(page, launch, threadRuns, Number(cyclesArg));
    if (cdp) {
      const { profile } = await cdp.send('Profiler.stop');
      if (process.env.PROFILE_OUT) fs.writeFileSync(`${process.env.PROFILE_OUT}.${launch}.json`, JSON.stringify(profile));
      printProfile(launch, profile, profileTop);
    }
    await browser.close();
  }
} finally {
  server.close();
}

// Self time by function name, summed over every node of that name, as a share of all samples.
function printProfile(launch, profile, top) {
  const byId = new Map(profile.nodes.map(node => [node.id, node]));
  const self = new Map();
  let total = 0;
  profile.samples.forEach((id, i) => {
    const dt = profile.timeDeltas[i] ?? 0;
    const name = byId.get(id).callFrame.functionName || '(anonymous)';
    self.set(name, (self.get(name) ?? 0) + dt);
    total += dt;
  });
  const rows = [...self].sort((a, b) => b[1] - a[1]).slice(0, top);
  for (const [name, t] of rows) console.log(`launch ${launch} profile ${(100 * t / total).toFixed(1)}% ${name}`);
}

// More timed runs, driven from here, each measured in the main thread's CPU time.
async function threadTimes(page, launch, runs, cycles) {
  const session = await page.context().newCDPSession(page);
  await session.send('Performance.enable', { timeDomain: 'threadTicks' });
  await page.evaluate(async () => {
    const runtime = globalThis.getDotnetRuntime(0);
    globalThis.benchNes = (await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName)).NesHost;
  });
  const cpuHz = await page.evaluate(() => globalThis.benchNes.CpuHz());
  const threadMs = async () => (await session.send('Performance.getMetrics')).metrics.find(m => m.name === 'ThreadTime').value * 1000;
  for (let i = 1; i <= runs; i++) {
    const before = await threadMs();
    const ran = await page.evaluate(n => { const c = globalThis.benchNes.Cycles(); globalThis.benchNes.Run(n); return globalThis.benchNes.Cycles() - c; }, cycles);
    const ms = await threadMs() - before;
    const perSecond = ran / (ms / 1000);
    console.log(`launch ${launch} thread ${i} cycles=${ran} thread_ms=${ms.toFixed(3)} mhz=${(perSecond / 1e6).toFixed(3)} times_real=${(perSecond / cpuHz).toFixed(2)}`);
  }
}
