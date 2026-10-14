// THROWAWAY, never merged: runs only the KIM-1's phone step of the browser
// check (scripts/browser-check.mjs, "A phone: one finger scrolls the page until
// a tap focuses the model") N times on one build, each in a fresh phone, with
// the step's own checks unchanged, and counts the runs in which "a finger on the
// focused model scrolled the page" (issue 80). Each run also logs the load
// average, the page's height and the speed line before and after the drag, and
// the speed line's height for each kind of sentence.
//
//   node scripts/stress-kim1-phone.mjs --runs 25 --label old
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const here = path.dirname(fileURLToPath(import.meta.url));
const site = path.resolve(here, '..');
const dist = path.join(site, 'dist');
const arg = (name, fallback) => { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : fallback; };
const runs = Number(arg('--runs', 25));
const label = arg('--label', '?');
const STEP_TIMEOUT_MS = 120_000;

function edgeHeaders() {
  const lines = fs.readFileSync(path.join(site, 'public', '_headers'), 'utf8').split('\n');
  const headers = {};
  let inAll = false;
  for (const line of lines) {
    if (/^\S/.test(line) && !line.startsWith('#')) inAll = line.trim() === '/*';
    else if (inAll && /^\s+\S/.test(line)) { const [name, ...value] = line.trim().split(':'); headers[name] = value.join(':').trim(); }
  }
  return headers;
}
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.bin': 'application/octet-stream', '.svg': 'image/svg+xml', '.webp': 'image/webp', '.avif': 'image/avif', '.woff2': 'font/woff2', '.woff': 'font/woff', '.xml': 'application/xml', '.txt': 'text/plain' };
const headers = edgeHeaders();
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  let file = path.join(dist, path.normalize(decodeURIComponent(url.pathname)));
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (!file.startsWith(dist) || !fs.existsSync(file)) { res.writeHead(404).end(); return; }
  res.writeHead(200, { ...headers, 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
  fs.createReadStream(file).pipe(res);
});
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const load = () => fs.readFileSync('/proc/loadavg', 'utf8').split(' ').slice(0, 3).join(' ');

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH ?? '/usr/bin/google-chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
console.log(`browser ${browser.version()}; ${runs} runs; page ${label}; load ${load()}`);

const near = (a, b, eps = 1e-3) => Math.abs(a - b) < eps;
const f3 = (n) => n.toFixed(3);
const stillScrolling = async (p) => {
  let last = -1;
  let same = 0;
  for (let i = 0; i < 60; i++) {
    const y = await p.evaluate(() => scrollY);
    same = y === last ? same + 1 : 0;
    if (same >= 2) return y;
    last = y;
    await p.waitForTimeout(120);
  }
  return last;
};

// The phone step, as scripts/browser-check.mjs has it on main, to the drag on the focused model.
async function phoneStep(problems) {
  const phone = await browser.newContext({ viewport: { width: 390, height: 780 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });
  try {
    const mobile = await phone.newPage();
    mobile.on('console', (m) => { if (m.type() === 'error') problems.push(`phone: console error: ${m.text()}`); });
    mobile.on('pageerror', (e) => problems.push(`phone: page error: ${e.message}`));
    const cdp = await phone.newCDPSession(mobile);
    await mobile.goto(`${origin}/machines/kim-1/`);
    const mok = mobile.locator('[data-analytics-on]');
    if (await mok.isVisible().catch(() => false)) await mok.click();
    const mstage = mobile.locator('[data-model-stage]');
    await mstage.scrollIntoViewIfNeeded();
    await mobile.waitForFunction(() => document.querySelector('[data-model]')?.dataset.state === 'running', null, { timeout: STEP_TIMEOUT_MS });
    await mobile.waitForTimeout(500);
    const mview = () => mobile.locator('[data-model]').evaluate((m) => m.modelView());
    const msettle = async () => {
      for (let i = 0; i < 100; i++) {
        const v = await mview();
        if (!v.moving) return v;
        await mobile.waitForTimeout(150);
      }
      throw new Error('phone: the camera never came to rest');
    };
    const mbring = async () => { await mstage.evaluate((el) => el.scrollIntoView({ block: 'center', behavior: 'instant' })); await stillScrolling(mobile); };
    const mtouch = () => mobile.locator('[data-model-canvas]').evaluate((c) => c.style.touchAction);
    const touchPoints = (...pts) => pts.map((p, id) => ({ x: p.x, y: p.y, id }));
    const finger = async (from, to, steps = 6) => {
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: touchPoints(from) });
      for (let i = 1; i <= steps; i++) {
        await mobile.waitForTimeout(16);
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: touchPoints({ x: from.x + ((to.x - from.x) * i) / steps, y: from.y + ((to.y - from.y) * i) / steps }) });
      }
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    };
    const mat = async (fx, fy) => { const b = await mobile.locator('[data-model-canvas]').boundingBox(); return { x: b.x + b.width * fx, y: b.y + b.height * fy }; };
    // Logged only: the speed line's height for each kind of sentence (the fixed page's own module writes them).
    await mobile.waitForFunction(() => ['running', 'failed'].includes(document.querySelector('[data-kim1]')?.dataset.state), null, { timeout: STEP_TIMEOUT_MS });
    const line = await mobile.locator('[data-kim1-speed]').evaluate((el) => {
      const box = el.closest('.speed-line') ?? el;
      const said = el.textContent;
      const heights = ["Running at the board's own 1 MHz, with little to spare in this browser.", "Running at the board's own 1 MHz. This browser could run it about 9 times as fast.", "Running at the board's own 1 MHz. This browser could run it about 10 times as fast.", 'Running at 0.50 MHz, slower than the board\'s 1 MHz: this browser cannot keep up.'].map((s) => { el.textContent = s; return box.getBoundingClientRect().height.toFixed(2); });
      el.textContent = said;
      return `${box === el ? 'no line' : 'in its line'}, ${heights.join('/')} px`;
    });
    await mbring();
    const m0 = await msettle();
    if ((await mtouch()) !== 'pan-y pinch-zoom') problems.push(`phone: an unfocused canvas is touch-action "${await mtouch()}"`);
    const ms0 = await mobile.evaluate(() => scrollY);
    await finger(await mat(0.8, 0.8), await mat(0.8, 0.3));
    const ms1 = await stillScrolling(mobile);
    await mbring();
    const m1 = await msettle();
    if (ms1 <= ms0) problems.push('phone: a finger on the unfocused model did not scroll the page');
    if (!near(m1.azimuth, m0.azimuth, 0.01) || !near(m1.polar, m0.polar, 0.01)) problems.push('phone: a finger on the unfocused model turned it');
    await mbring();
    const tap = await mat(0.96, 0.06);
    await mobile.touchscreen.tap(tap.x, tap.y);
    const m2 = await msettle();
    if (!m2.focused || (await mtouch()) !== 'none') problems.push('phone: a tap did not focus the model and set touch-action none');
    await mbring();
    const mpage = () => mobile.evaluate(() => ({ y: scrollY, height: document.documentElement.scrollHeight, speed: document.querySelector('[data-kim1-speed]')?.textContent }));
    const mp2 = await mpage();
    const ms2 = mp2.y;
    await finger(await mat(0.8, 0.3), await mat(0.3, 0.45));
    const m3 = await msettle();
    const mp3 = await mpage();
    const ms3 = mp3.y;
    if (near(m3.azimuth, m2.azimuth, 0.05)) problems.push('phone: a finger on the focused model did not turn it');
    if (ms3 !== ms2) problems.push('phone: a finger on the focused model scrolled the page');
    return `speed line ${line}; drag: azimuth ${f3(m2.azimuth)} to ${f3(m3.azimuth)}; scrollY ${ms2} to ${ms3}; page ${mp2.height} to ${mp3.height} px; "${mp2.speed}" to "${mp3.speed}"`;
  } finally {
    await phone.close();
  }
}

const tally = { runs: 0, clean: 0, scrolled: 0, other: 0 };
for (let i = 1; i <= runs; i++) {
  const problems = [];
  const t = Date.now();
  const before = load();
  let said = '';
  try {
    said = await phoneStep(problems);
  } catch (error) {
    problems.push(`thrown: ${error.message}`);
  }
  tally.runs++;
  const scrolled = problems.some((p) => /a finger on the focused model scrolled the page/.test(p));
  if (problems.length === 0) tally.clean++;
  if (scrolled) tally.scrolled++;
  if (problems.some((p) => !/a finger on the focused model scrolled the page/.test(p))) tally.other++;
  console.log(`STRESS run ${i} of ${runs}, page ${label}, load ${before}: ${Math.round((Date.now() - t) / 1000)} s; ${said}; ${problems.length === 0 ? 'clean' : problems.join(' | ')}`);
}
console.log(`STRESS TALLY page ${label}, load at the end ${load()}: ${JSON.stringify(tally)} (runs, clean, runs in which the finger on the focused model "scrolled the page", runs with any other problem)`);
await browser.close();
server.close();
