// The KIM-1 page, checked in a real browser: serves the built site (dist/) on
// 127.0.0.1 with the site's own response headers, opens /machines/kim-1/ in
// headless Chrome, types the "try it" program in by clicking the page's own
// keypad, and reads the result off the page's digits after every step. It
// fails on any console error, any failed request, any Content-Security-Policy
// violation, or any step whose digits are not what machines/kim-1/try-it.json
// says they will be. Then it reports how fast this browser runs the machine.
//
// Then the page's photograph and 3D model: the photograph must have loaded; the
// model's bundle must not have been fetched before the visitor scrolled to it;
// once loaded, its canvas must draw something other than the black canvas; its
// digits must show what the page's drawn digits show; pressing 1 on the page's
// keypad must put the model's 1 key down; and clicking the model's 2 key must
// press 2 on the machine.
//
//   node scripts/browser-check.mjs [--throttle N] [--measure seconds]
//
// --throttle N slows the browser's CPU N times (Chrome's own CPU throttling),
// to see how a slower computer would fare. The server binds to loopback, serves
// dist/ only, and is closed before the script exits.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';
import sharp from 'sharp';
import { loadTryIt, parseKeys } from '../src/lib/machines.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const site = path.resolve(here, '..');
const dist = path.join(site, 'dist');
const option = (name, fallback) => {
  const i = process.argv.indexOf(name);
  return i >= 0 ? Number(process.argv[i + 1]) : fallback;
};
const throttle = option('--throttle', 1);
const measureSeconds = option('--measure', 5);
const STEP_TIMEOUT_MS = 120_000;
const chrome = process.env.CHROME_PATH ?? '/usr/bin/google-chrome';

// The headers the edge sends on every route, from public/_headers' /* block,
// so the browser enforces the real CSP here and not a laxer one.
function edgeHeaders() {
  const lines = fs.readFileSync(path.join(site, 'public', '_headers'), 'utf8').split('\n');
  const headers = {};
  let inAll = false;
  for (const line of lines) {
    if (/^\S/.test(line) && !line.startsWith('#')) inAll = line.trim() === '/*';
    else if (inAll && /^\s+\S/.test(line)) {
      const [name, ...value] = line.trim().split(':');
      headers[name] = value.join(':').trim();
    }
  }
  if (!headers['Content-Security-Policy']) throw new Error('public/_headers has no Content-Security-Policy for /*');
  return headers;
}

const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.wasm': 'application/wasm', '.bin': 'application/octet-stream', '.svg': 'image/svg+xml',
  '.webp': 'image/webp', '.avif': 'image/avif', '.woff2': 'font/woff2', '.woff': 'font/woff', '.xml': 'application/xml', '.txt': 'text/plain',
};

const headers = edgeHeaders();
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  let file = path.join(dist, path.normalize(decodeURIComponent(url.pathname)));
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (!file.startsWith(dist) || !fs.existsSync(file)) {
    res.writeHead(404).end();
    return;
  }
  res.writeHead(200, { ...headers, 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
  fs.createReadStream(file).pipe(res);
});
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;

const matches = (expected, shown) => expected.length === shown.length && [...expected].every((c, i) => (c === 'x' ? /[0-9A-F]/.test(shown[i]) : c === shown[i]));
const asShown = (six) => `${six.slice(0, 4)} ${six.slice(4)}`;

const problems = [];
let browser;
try {
  // SwiftShader is Chrome's software WebGL, for machines with no GPU (a CI
  // runner): without the second flag Chrome no longer falls back to it.
  browser = await chromium.launch({ executablePath: chrome, headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  console.log(`browser ${browser.version()}${throttle > 1 ? `, CPU throttled ${throttle} times` : ''}`);
  const page = await browser.newPage();
  page.on('console', (m) => { if (m.type() === 'error') problems.push(`console error: ${m.text()}`); });
  page.on('pageerror', (e) => problems.push(`page error: ${e.message}`));
  page.on('requestfailed', (r) => problems.push(`request failed: ${r.url()} ${r.failure()?.errorText ?? ''}`));
  page.on('response', (r) => { if (r.status() >= 400) problems.push(`HTTP ${r.status()}: ${r.url()}`); });
  const requested = [];
  page.on('request', (r) => requested.push(new URL(r.url()).pathname));
  await page.exposeFunction('reportCspViolation', (v) => problems.push(`CSP violation: ${v}`));
  await page.addInitScript(() => {
    document.addEventListener('securitypolicyviolation', (e) => window.reportCspViolation(`${e.violatedDirective} blocked ${e.blockedURI || '(inline)'}`));
  });
  if (throttle > 1) {
    const cdp = await page.context().newCDPSession(page);
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: throttle });
  }

  const started = Date.now();
  await page.goto(`${origin}/machines/kim-1/`);
  const panel = page.locator('[data-kim1]');
  await page.waitForFunction(() => ['running', 'failed'].includes(document.querySelector('[data-kim1]')?.dataset.state), null, { timeout: STEP_TIMEOUT_MS });
  const state = await panel.getAttribute('data-state');
  console.log(`loaded in ${Date.now() - started} ms: ${await page.locator('[data-kim1-status]').innerText()}`);
  if (state !== 'running') throw new Error(`the machine did not start (state ${state})`);
  const before = await page.locator('[data-kim1-status]').innerText();
  // The analytics notice sits over the foot of the page; answering it keeps it off the keypad.
  const ok = page.locator('[data-analytics-on]');
  if (await ok.isVisible().catch(() => false)) await ok.click();

  const { steps } = loadTryIt('kim-1');
  for (const [i, step] of steps.entries()) {
    const t0 = Date.now();
    for (const key of parseKeys(step.keys)) await page.locator(`[data-key="${key}"]`).click();
    await page.waitForFunction(
      (expected) => {
        const p = document.querySelector('[data-kim1]');
        const six = p.dataset.display;
        if (p.dataset.pending !== '0' || six === undefined) return false;
        const shown = `${six.slice(0, 4)} ${six.slice(4)}`;
        return expected.length === shown.length && [...expected].every((c, k) => (c === 'x' ? /[0-9A-F]/.test(shown[k]) : c === shown[k]));
      },
      step.display,
      { timeout: STEP_TIMEOUT_MS, polling: 100 },
    ).catch(() => {});
    const shown = asShown((await panel.getAttribute('data-display')) ?? '      ');
    const said = await page.locator('[data-kim1-said]').innerText();
    const pass = matches(step.display, shown);
    console.log(`step ${i + 1} ${pass ? 'ok  ' : 'FAIL'} keys "${step.keys}" expected "${step.display}" shown "${shown}" announced "${said}" (${Date.now() - t0} ms)`);
    if (!pass) problems.push(`step ${i + 1}: expected "${step.display}", the page shows "${shown}"`);
  }

  const after = await page.locator('[data-kim1-status]').innerText();
  console.log(`status line: "${before}", then "${after}"`);
  if (after === before) problems.push('the status line never moved on from the reset prompt');

  // Speed: the page reports, once a second, how fast it is running the
  // machine and how fast this browser could run it flat out.
  const samples = [];
  for (let s = 0; s < measureSeconds; s++) {
    await page.waitForTimeout(1000);
    samples.push({ capacity: Number(await panel.getAttribute('data-capacity-mhz')), actual: Number(await panel.getAttribute('data-actual-mhz')) });
  }
  console.log(`speed, once a second for ${measureSeconds} s: ${samples.map((x) => `running ${x.actual.toFixed(2)} MHz, capacity ${x.capacity.toFixed(2)} MHz`).join('; ')}`);
  console.log(`page speed line: ${await page.locator('[data-kim1-speed]').innerText()}`);

  // The photograph of the original board.
  const photo = page.locator('[data-photo-img]');
  await photo.scrollIntoViewIfNeeded();
  await page.waitForFunction(() => { const i = document.querySelector('[data-photo-img]'); return i.complete && i.naturalWidth > 0; }, null, { timeout: STEP_TIMEOUT_MS }).catch(() => {});
  const img = await photo.evaluate((i) => ({ src: new URL(i.currentSrc).pathname, natural: `${i.naturalWidth}x${i.naturalHeight}`, shown: `${i.clientWidth}x${i.clientHeight}`, ok: i.complete && i.naturalWidth > 0 }));
  console.log(`photograph: ${img.ok ? 'loaded' : 'NOT LOADED'} ${img.src}, ${img.natural} pixels, shown at ${img.shown}`);
  if (!img.ok) problems.push('the photograph did not load');

  // The 3D model: nothing of it is fetched until the visitor reaches it.
  const early = requested.filter((r) => r.startsWith('/models/'));
  console.log(`model bundle requested before scrolling to it: ${early.length === 0 ? 'no' : early.join(', ')}`);
  if (early.length > 0) problems.push(`the model's bundle was loaded before the visitor reached it: ${early.join(', ')}`);
  const model = page.locator('[data-model]');
  const stage = page.locator('[data-model-stage]');
  const t1 = Date.now();
  await stage.scrollIntoViewIfNeeded();
  await page.waitForFunction(() => ['running', 'no-webgl', 'failed'].includes(document.querySelector('[data-model]')?.dataset.state), null, { timeout: STEP_TIMEOUT_MS });
  const modelState = await model.getAttribute('data-state');
  const loadedModel = requested.filter((r) => r.startsWith('/models/'));
  console.log(`model: ${modelState} in ${Date.now() - t1} ms after scrolling to it, fetched ${loadedModel.join(', ')}; status "${await page.locator('[data-model-status]').innerText()}"`);
  if (modelState !== 'running') throw new Error(`the 3D model did not start (state ${modelState})`);

  // Its canvas draws the board: count the pixels that are not the black canvas.
  await page.waitForTimeout(500);
  const shot = await page.locator('[data-model-canvas]').screenshot();
  const { data, info } = await sharp(shot).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  let lit = 0;
  for (let i = 0; i < data.length; i += info.channels) if (Math.max(data[i], data[i + 1], data[i + 2]) > 24) lit++;
  const share = lit / (info.width * info.height);
  console.log(`model canvas: ${info.width}x${info.height}, ${(share * 100).toFixed(1)}% of its pixels are not black`);
  if (share < 0.05) problems.push(`the model's canvas is blank (${(share * 100).toFixed(1)}% drawn)`);

  // Its digits show what the page's drawn digits show, after the walk above.
  const readBoth = () => page.evaluate(() => {
    const svg = [...document.querySelectorAll('[data-kim1] [data-digit]')].map((d) => [...d.querySelectorAll('[data-seg]')].reduce((n, p, s) => n | (p.classList.contains('on') ? 1 << s : 0), 0));
    const m = document.querySelector('[data-model]');
    return { page: svg.join(','), model: m.dataset.modelSegments, text: m.dataset.modelDisplay, said: document.querySelector('[data-kim1]').dataset.display };
  });
  await page.waitForFunction(() => {
    const svg = [...document.querySelectorAll('[data-kim1] [data-digit]')].map((d) => [...d.querySelectorAll('[data-seg]')].reduce((n, p, s) => n | (p.classList.contains('on') ? 1 << s : 0), 0));
    return svg.join(',') === document.querySelector('[data-model]').dataset.modelSegments;
  }, null, { timeout: 5000, polling: 100 }).catch(() => {});
  const both = await readBoth();
  const same = both.page === both.model;
  console.log(`digits ${same ? 'match' : 'DIFFER'}: page segments ${both.page} (${asShown(both.said ?? '      ')}), model segments ${both.model} ("${asShown(both.text ?? '      ')}")`);
  if (!same) problems.push(`the model's digits (${both.model}) are not the page's (${both.page})`);
  if (both.text !== both.said) problems.push(`the model reads "${both.text}" where the page says "${both.said}"`);

  // A key pressed on the page's keypad goes down on the model.
  const presses = async () => Number((await model.getAttribute('data-model-presses')) ?? 0);
  // The machine's display is read once it has settled, so each check waits for it to change.
  const changedFrom = async (was) => {
    await page.waitForFunction((w) => { const p = document.querySelector('[data-kim1]'); return p.dataset.pending === '0' && p.dataset.display !== w; }, was, { timeout: 10_000, polling: 100 }).catch(() => {});
    return panel.getAttribute('data-display');
  };
  // A typed digit lands at the end of the address (AD mode) or of the data (DA mode).
  const took = (six, d) => six?.[3] === d || six?.[5] === d;
  const before1Display = await panel.getAttribute('data-display');
  let before1 = await presses();
  await page.locator('[data-key="1"]').click();
  await page.waitForFunction((n) => { const m = document.querySelector('[data-model]'); return m.dataset.modelPressed === '1' && Number(m.dataset.modelPresses) > n; }, before1, { timeout: 5000 }).catch(() => {});
  const pressed1 = await model.getAttribute('data-model-pressed');
  console.log(`page key 1 clicked: model key down "${pressed1}", presses ${before1} then ${await presses()}`);
  if (pressed1 !== '1') problems.push(`pressing 1 on the page put down the model's "${pressed1}", not its 1`);

  // A key clicked on the model presses it on the machine: the machine's tap is what announces it.
  await stage.scrollIntoViewIfNeeded();
  await page.waitForTimeout(300);
  const point = await model.evaluate((m) => m.modelKeyPoint('2'));
  before1 = await presses();
  const before2 = await changedFrom(before1Display);
  const showing1 = took(before2, '1');
  await page.mouse.click(point.x, point.y);
  await page.waitForFunction((n) => { const m = document.querySelector('[data-model]'); return m.dataset.modelPressed === '2' && Number(m.dataset.modelPresses) > n; }, before1, { timeout: 5000 }).catch(() => {});
  const pressed2 = await model.getAttribute('data-model-pressed');
  const after2 = await changedFrom(before2);
  const took2 = took(after2, '2');
  console.log(`model key 2 clicked at ${Math.round(point.x)},${Math.round(point.y)}: model key down "${pressed2}"; the machine showed "${asShown(before2 ?? '      ')}" after the page's 1 and "${asShown(after2 ?? '      ')}" after the model's 2`);
  if (!showing1) problems.push(`the machine did not take the page's 1 (it shows "${before2}")`);
  if (pressed2 !== '2') problems.push(`clicking the model's 2 did not put its 2 down (last down "${pressed2}")`);
  if (!took2 || after2 === before2) problems.push(`clicking the model's 2 did not press 2 on the machine (it shows "${after2}")`);
} catch (error) {
  problems.push(error.message);
} finally {
  await browser?.close();
  server.close();
}

if (problems.length > 0) {
  console.error(`FAILED:\n- ${problems.join('\n- ')}`);
  process.exit(1);
}
console.log('no console errors, no failed requests, no CSP violations');
