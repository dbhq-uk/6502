// The KIM-1 page, checked in a real browser: serves the built site (dist/) on
// 127.0.0.1 with the site's own response headers, opens /machines/kim-1/ in
// headless Chrome, types the "try it" program in by clicking the page's own
// keypad, and reads the result off the page's digits after every step. It
// fails on any console error, any failed request, any Content-Security-Policy
// violation, or any step whose digits are not what machines/kim-1/try-it.json
// says they will be. Then it reports how fast this browser runs the machine.
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
  '.webp': 'image/webp', '.woff2': 'font/woff2', '.woff': 'font/woff', '.xml': 'application/xml', '.txt': 'text/plain',
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
  browser = await chromium.launch({ executablePath: chrome, headless: true });
  console.log(`browser ${browser.version()}${throttle > 1 ? `, CPU throttled ${throttle} times` : ''}`);
  const page = await browser.newPage();
  page.on('console', (m) => { if (m.type() === 'error') problems.push(`console error: ${m.text()}`); });
  page.on('pageerror', (e) => problems.push(`page error: ${e.message}`));
  page.on('requestfailed', (r) => problems.push(`request failed: ${r.url()} ${r.failure()?.errorText ?? ''}`));
  page.on('response', (r) => { if (r.status() >= 400) problems.push(`HTTP ${r.status()}: ${r.url()}`); });
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
