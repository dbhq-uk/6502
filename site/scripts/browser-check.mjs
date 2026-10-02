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
// Then the model's controls, as a visitor uses them: a left-drag turns it and a
// right-drag pans; with the model unfocused the wheel scrolls the page and the
// model does not zoom; after a click on it the wheel zooms to the cursor and
// the page does not scroll; ctrl and the wheel zooms unfocused; Escape lets go
// and the wheel scrolls the page again; the camera goes under the board and the
// view from there is drawn; panning far stays inside the board's bounds;
// the distance limits hold; a double click on empty space resets the view; the
// keyboard turns, pans, zooms and resets. Then the copper tracks: on to start
// with, a part-free stretch of the board has more colour variance with them
// than without, Show tracks takes them off and Show tracks only fades the parts
// out (read off the scene through the model's test hook) and leaves the track
// pixels showing, both from the keyboard too. Then in an emulated phone: one finger
// scrolls the page until a tap focuses the model, after which one finger turns
// it and two fingers pinch-zoom, and the canvas is touch-action none only while
// it has focus.
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
  // Every page this script opens is watched for the same three things.
  const requested = [];
  const watch = async (p, label = '') => {
    p.on('console', (m) => { if (m.type() === 'error') problems.push(`${label}console error: ${m.text()}`); });
    p.on('pageerror', (e) => problems.push(`${label}page error: ${e.message}`));
    p.on('requestfailed', (r) => problems.push(`${label}request failed: ${r.url()} ${r.failure()?.errorText ?? ''}`));
    p.on('response', (r) => { if (r.status() >= 400) problems.push(`${label}HTTP ${r.status()}: ${r.url()}`); });
    p.on('request', (r) => { if (!label) requested.push(new URL(r.url()).pathname); });
    await p.exposeFunction('reportCspViolation', (v) => problems.push(`${label}CSP violation: ${v}`));
    await p.addInitScript(() => {
      document.addEventListener('securitypolicyviolation', (e) => window.reportCspViolation(`${e.violatedDirective} blocked ${e.blockedURI || '(inline)'}`));
    });
  };
  const page = await browser.newPage();
  await watch(page);
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

  // ---- The model's controls ----
  const canvas = page.locator('[data-model-canvas]');
  const view = () => model.evaluate((m) => m.modelView());
  // The camera eases (and software WebGL draws slowly), so a reading is taken
  // once the camera has got to where it was heading.
  const settle = async () => {
    for (let i = 0; i < 100; i++) {
      const v = await view();
      if (!v.moving) return v;
      await page.waitForTimeout(150);
    }
    throw new Error('the camera never came to rest');
  };
  // The page scrolls smoothly, so a scroll is waited out before anything is measured or aimed.
  // Three readings the same, 120 ms apart: on a loaded machine software WebGL can
  // hold a frame back longer than one gap, and two equal readings mid-scroll passed.
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
  const bring = async () => { await stage.evaluate((el) => el.scrollIntoView({ block: 'center', behavior: 'instant' })); await stillScrolling(page); };
  const rect = async () => canvas.boundingBox();
  // Points on the canvas, as fractions of it. The top right corner is the black
  // around the board at the starting view and whenever the board is small.
  const at = async (fx, fy) => { const b = await rect(); return { x: b.x + b.width * fx, y: b.y + b.height * fy }; };
  const drag = async (from, to, button = 'left', shift = false) => {
    await page.mouse.move(from.x, from.y);
    if (shift) await page.keyboard.down('Shift');
    await page.mouse.down({ button });
    await page.mouse.move(to.x, to.y, { steps: 5 });
    await page.mouse.up({ button });
    if (shift) await page.keyboard.up('Shift');
  };
  const near = (a, b, eps = 1e-3) => Math.abs(a - b) < eps;
  const sameTarget = (a, b, eps = 1e-3) => a.every((v, i) => near(v, b[i], eps));
  const wheelOver = async (point, dy, { ctrl = false } = {}) => {
    await page.mouse.move(point.x, point.y);
    if (ctrl) await page.keyboard.down('Control');
    await page.mouse.wheel(0, dy);
    if (ctrl) await page.keyboard.up('Control');
  };
  const f3 = (n) => n.toFixed(3);

  await bring();
  const empty = await at(0.96, 0.06);
  await page.evaluate(() => document.activeElement?.blur());
  const start = await settle();
  console.log(`controls: start view azimuth ${f3(start.azimuth)}, polar ${f3(start.polar)}, distance ${f3(start.distance)}, target ${start.target.map(f3).join(',')}; bounds ${start.bounds.map(f3).join(',')}; focused ${start.focused}`);
  if (start.focused) problems.push('the model has focus beforeTurn anyone has touched it');
  const touchAction = () => canvas.evaluate((c) => c.style.touchAction);
  const ring = () => stage.evaluate((el) => ({ active: el.hasAttribute('data-active'), outline: getComputedStyle(el).outlineStyle, width: getComputedStyle(el).outlineWidth }));
  const hint = await page.locator('.model-hint').evaluate((h) => ({ text: h.innerText, shown: getComputedStyle(h).display !== 'none' }));
  console.log(`hint on the model while unfocused: "${hint.text}", shown ${hint.shown}; touch-action ${await touchAction()}`);
  if (!hint.shown || hint.text !== 'Click the model, then scroll to zoom') problems.push(`the hint on the model is "${hint.text}" (shown ${hint.shown})`);

  // The wheel, with the model unfocused, scrolls the page and does not zoom.
  const scroll0 = await page.evaluate(() => scrollY);
  await wheelOver(await at(0.5, 0.5), 300);
  await page.waitForFunction((y) => scrollY !== y, scroll0, { timeout: 5000 }).catch(() => {});
  const scroll1 = await stillScrolling(page);
  await bring();
  const afterScroll = await settle();
  console.log(`wheel over the unfocused model: scrollY ${scroll0} to ${scroll1}; distance ${f3(start.distance)} to ${f3(afterScroll.distance)}`);
  if (scroll1 === scroll0) problems.push('the wheel over the unfocused model did not scroll the page');
  if (!near(afterScroll.distance, start.distance)) problems.push(`the wheel zoomed the unfocused model (${f3(start.distance)} to ${f3(afterScroll.distance)})`);

  // Ctrl and the wheel zooms even when unfocused, and the page does not scroll.
  await bring();
  const scrollC0 = await page.evaluate(() => scrollY);
  await wheelOver(await at(0.5, 0.5), -300, { ctrl: true });
  const afterCtrl = await settle();
  const scrollC1 = await page.evaluate(() => scrollY);
  console.log(`ctrl and the wheel over the unfocused model: distance ${f3(afterScroll.distance)} to ${f3(afterCtrl.distance)}; scrollY ${scrollC0} to ${scrollC1}`);
  if (!(afterCtrl.distance < afterScroll.distance - 1)) problems.push('ctrl and the wheel did not zoom the model');
  if (scrollC1 !== scrollC0) problems.push('ctrl and the wheel scrolled the page');

  // A click on the model gives it the wheel, and shows the focus ring.
  await bring();
  await page.mouse.click(empty.x, empty.y);
  const focused = await view();
  const focusedRing = await ring();
  console.log(`after a click on empty space: focused ${focused.focused}, ring ${JSON.stringify(focusedRing)}, touch-action ${await touchAction()}`);
  if (!focused.focused || !focusedRing.active || focusedRing.outline !== 'solid') problems.push(`a click on the model did not focus it with a ring (${JSON.stringify(focusedRing)})`);
  if ((await touchAction()) !== 'none') problems.push(`a focused model's canvas is touch-action ${await touchAction()}, not none`);
  if (await page.locator('.model-hint').evaluate((h) => getComputedStyle(h).display) !== 'none') problems.push('the hint is still shown on the focused model');

  // Zoom to the cursor: the key under the cursor stays under it, and the page does not scroll.
  await page.keyboard.press('Home');
  const home = await settle();
  const key5 = await model.evaluate((m) => m.modelKeyPoint('5'));
  const scrollW0 = await page.evaluate(() => scrollY);
  await wheelOver(key5, -300);
  const zoomed = await settle();
  const key5After = await model.evaluate((m) => m.modelKeyPoint('5'));
  const scrollW1 = await page.evaluate(() => scrollY);
  const slip = Math.hypot(key5After.x - key5.x, key5After.y - key5.y);
  console.log(`wheel over the focused model, cursor on the 5 key at ${Math.round(key5.x)},${Math.round(key5.y)}: distance ${f3(home.distance)} to ${f3(zoomed.distance)}; the key is now at ${Math.round(key5After.x)},${Math.round(key5After.y)}, ${slip.toFixed(1)} px from the cursor; scrollY ${scrollW0} to ${scrollW1}`);
  if (!(zoomed.distance < home.distance - 1)) problems.push('the wheel did not zoom the focused model');
  if (scrollW1 !== scrollW0) problems.push('the wheel scrolled the page while the model had focus');
  if (slip > 25) problems.push(`the wheel did not zoom to the cursor (the key under it moved ${slip.toFixed(1)} px)`);

  // Escape lets go, and the wheel scrolls the page again.
  await page.keyboard.press('Escape');
  const released = await view();
  console.log(`Escape: focused ${released.focused}, ring ${JSON.stringify(await ring())}, touch-action ${await touchAction()}`);
  if (released.focused || (await ring()).active || (await touchAction()) !== 'pan-y pinch-zoom') problems.push('Escape did not let go of the model');
  await bring();
  const scrollE0 = await page.evaluate(() => scrollY);
  await wheelOver(await at(0.5, 0.5), 300);
  await page.waitForFunction((y) => scrollY !== y, scrollE0, { timeout: 5000 }).catch(() => {});
  const scrollE1 = await stillScrolling(page);
  console.log(`wheel after Escape: scrollY ${scrollE0} to ${scrollE1}`);
  if (scrollE1 === scrollE0) problems.push('the wheel did not scroll the page after Escape');

  // A left-drag turns it, a right-drag pans it, shift and a left-drag pans too.
  await bring();
  await page.mouse.click(empty.x, empty.y);
  await page.keyboard.press('Home');
  const beforeTurn = await settle();
  await drag(await at(0.9, 0.1), await at(0.6, 0.2));
  const turned = await settle();
  console.log(`left-drag 233 px left and 46 px down: azimuth ${f3(beforeTurn.azimuth)} to ${f3(turned.azimuth)}, polar ${f3(beforeTurn.polar)} to ${f3(turned.polar)}; target unchanged ${sameTarget(beforeTurn.target, turned.target)}`);
  if (near(turned.azimuth, beforeTurn.azimuth, 0.05)) problems.push('a left-drag did not turn the model');
  if (!sameTarget(beforeTurn.target, turned.target)) problems.push('a left-drag moved the target');
  await page.keyboard.press('Home');
  await settle();
  const pre = await view();
  await drag(await at(0.9, 0.1), await at(0.6, 0.3), 'right');
  const panned = await settle();
  console.log(`right-drag: target ${pre.target.map(f3).join(',')} to ${panned.target.map(f3).join(',')}; azimuth unchanged ${near(pre.azimuth, panned.azimuth)}, distance unchanged ${near(pre.distance, panned.distance)}`);
  if (sameTarget(pre.target, panned.target, 0.05)) problems.push('a right-drag did not pan the model');
  if (!near(pre.azimuth, panned.azimuth) || !near(pre.distance, panned.distance)) problems.push('a right-drag turned or zoomed the model');
  await page.keyboard.press('Home');
  await settle();
  await drag(await at(0.9, 0.1), await at(0.6, 0.3), 'left', true);
  const shifted = await settle();
  console.log(`shift and left-drag: target ${pre.target.map(f3).join(',')} to ${shifted.target.map(f3).join(',')}; azimuth unchanged ${near(pre.azimuth, shifted.azimuth)}`);
  if (sameTarget(pre.target, shifted.target, 0.05) || !near(pre.azimuth, shifted.azimuth)) problems.push('shift and a left-drag did not pan the model');

  // Panning far stays inside the board's bounds.
  await page.keyboard.press('Home');
  await settle();
  for (const [dx, dy] of [[-0.5, 0], [-0.5, 0], [0, -0.4], [0, -0.4], [0.5, 0], [0.5, 0], [0.5, 0], [0, 0.4], [0, 0.4]]) {
    const from = await at(0.5 - dx / 2, 0.5 - dy / 2);
    const to = await at(0.5 + dx / 2, 0.5 + dy / 2);
    await drag(from, to, 'right');
  }
  const far = await settle();
  const [lo, hi] = [far.bounds.slice(0, 3), far.bounds.slice(3)];
  const inside = far.target.every((v, i) => v >= lo[i] - 1e-6 && v <= hi[i] + 1e-6);
  const touching = far.target.some((v, i) => near(v, lo[i], 0.01) || near(v, hi[i], 0.01));
  console.log(`nine right-drags, far and back and forth: target ${far.target.map(f3).join(',')} within ${lo.map(f3).join(',')} to ${hi.map(f3).join(',')}: ${inside}; it reached an edge: ${touching}`);
  if (!inside) problems.push(`panning took the target out of its bounds (${far.target.join(',')})`);
  if (!touching) problems.push('the pan test never reached the bounds, so it proved nothing');

  // The camera goes under the board, and what it sees there is drawn.
  await page.keyboard.press('Home');
  await settle();
  const measure = async () => {
    const { data, info } = await sharp(await canvas.screenshot()).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    let n = 0;
    let sum = 0;
    for (let i = 0; i < data.length; i += info.channels) {
      const v = Math.max(data[i], data[i + 1], data[i + 2]);
      if (v > 24) { n++; sum += v; }
    }
    return { lit: n / (info.width * info.height), mean: n ? sum / n : 0 };
  };
  const top = await measure();
  let under = await view();
  const step = under.polar;
  await drag(await at(0.9, 0.1), await at(0.9, 0.9));
  under = await settle();
  const sign = under.polar > step ? 'down' : 'up';
  for (let i = 0; i < 4 && under.polar < Math.PI / 2 + 0.6; i++) {
    await drag(await at(0.9, sign === 'down' ? 0.1 : 0.9), await at(0.9, sign === 'down' ? 0.9 : 0.1));
    under = await settle();
  }
  const below = await measure();
  console.log(`under the board: polar ${f3(step)} to ${f3(under.polar)} (pi/2 is ${f3(Math.PI / 2)}, pi is ${f3(Math.PI)}); ${(below.lit * 100).toFixed(1)}% of the canvas is not black, mean brightness ${below.mean.toFixed(0)} of 255 (from above: ${(top.lit * 100).toFixed(1)}%, ${top.mean.toFixed(0)})`);
  if (!(under.polar > Math.PI / 2)) problems.push(`the camera could not go under the board (polar ${f3(under.polar)})`);
  if (below.lit < 0.05) problems.push(`the view from under the board is blank (${(below.lit * 100).toFixed(1)}% drawn)`);
  if (below.mean < 40) problems.push(`the underside is nearly black (mean brightness ${below.mean.toFixed(0)})`);
  for (let i = 0; i < 2; i++) await drag(await at(0.9, sign === 'down' ? 0.1 : 0.9), await at(0.9, sign === 'down' ? 0.9 : 0.1));
  const flat = await settle();
  console.log(`dragged on to the stop: polar ${f3(flat.polar)}, never past pi (${f3(Math.PI)})`);
  if (flat.polar > Math.PI + 1e-6 || flat.polar < Math.PI - 0.2) problems.push(`the full orbit stops at polar ${f3(flat.polar)}, not at pi`);

  // The distance limits: close enough for a chip, far enough for the whole board small.
  await page.keyboard.press('Home');
  await settle();
  for (let i = 0; i < 3; i++) await wheelOver(await at(0.5, 0.5), -2000);
  const closest = await settle();
  const closeShot = await measure();
  for (let i = 0; i < 3; i++) await wheelOver(await at(0.5, 0.5), 3000);
  const farthest = await settle();
  const farShot = await measure();
  console.log(`the wheel to its limits: closest ${f3(closest.distance)} (${(closeShot.lit * 100).toFixed(1)}% of the canvas drawn), farthest ${f3(farthest.distance)} (${(farShot.lit * 100).toFixed(1)}% drawn)`);
  if (closest.distance > 3 + 1e-3 || closest.distance < 3 - 1e-3) problems.push(`the closest the wheel goes is ${f3(closest.distance)}, not 3`);
  if (farthest.distance < 150 - 1e-3 || farthest.distance > 150 + 1e-3) problems.push(`the farthest the wheel goes is ${f3(farthest.distance)}, not 150`);
  if (farShot.lit < 0.005 || farShot.lit > 0.2) problems.push(`the whole board, small, should be a few percent of the canvas (${(farShot.lit * 100).toFixed(1)}%)`);
  if (closeShot.lit < 0.5) problems.push(`up close the model should fill the canvas (${(closeShot.lit * 100).toFixed(1)}% drawn)`);

  // A double click on empty space resets the view (the board is small here, so the corner is empty).
  const corner = await at(0.96, 0.06);
  await page.mouse.dblclick(corner.x, corner.y);
  const reset = await settle();
  const backAtStart = near(reset.azimuth, start.azimuth) && near(reset.polar, start.polar) && near(reset.distance, start.distance) && sameTarget(reset.target, start.target);
  console.log(`double click on empty space: distance ${f3(farthest.distance)} to ${f3(reset.distance)}, polar ${f3(reset.polar)}, back at the start view: ${backAtStart}`);
  if (!backAtStart) problems.push('a double click on empty space did not reset the view');

  // The keyboard, with the model focused.
  await page.keyboard.press('Home');
  const k0 = await settle();
  await page.keyboard.press('ArrowLeft');
  const kLeft = await settle();
  await page.keyboard.press('ArrowDown');
  const kDown = await settle();
  await page.keyboard.press('Home');
  await settle();
  await page.keyboard.press('Shift+ArrowLeft');
  const kPan = await settle();
  await page.keyboard.press('Shift+ArrowUp');
  const kPanUp = await settle();
  await page.keyboard.press('Home');
  await settle();
  await page.keyboard.press('+');
  const kIn = await settle();
  await page.keyboard.press('-');
  await page.keyboard.press('-');
  const kOut = await settle();
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('Home');
  const kHome = await settle();
  console.log(`keyboard: ArrowLeft azimuth ${f3(k0.azimuth)} to ${f3(kLeft.azimuth)}; ArrowDown polar ${f3(kLeft.polar)} to ${f3(kDown.polar)}; Shift+ArrowLeft target x ${f3(k0.target[0])} to ${f3(kPan.target[0])}; Shift+ArrowUp target ${kPan.target.map(f3).join(',')} to ${kPanUp.target.map(f3).join(',')}; + distance ${f3(k0.distance)} to ${f3(kIn.distance)}; then - twice to ${f3(kOut.distance)}; Home back at the start: ${near(kHome.azimuth, start.azimuth) && near(kHome.distance, start.distance)}`);
  if (near(kLeft.azimuth, k0.azimuth, 0.05)) problems.push('ArrowLeft did not turn the model');
  if (near(kDown.polar, kLeft.polar, 0.05)) problems.push('ArrowDown did not tilt the model');
  if (near(kPan.target[0], k0.target[0], 0.05)) problems.push('Shift and ArrowLeft did not pan the model');
  if (sameTarget(kPan.target, kPanUp.target, 0.05)) problems.push('Shift and ArrowUp did not pan the model');
  if (!(kIn.distance < k0.distance - 1) || !(kOut.distance > kIn.distance + 1)) problems.push('plus and minus did not zoom the model');
  if (!(near(kHome.azimuth, start.azimuth) && near(kHome.polar, start.polar) && near(kHome.distance, start.distance) && sameTarget(kHome.target, start.target))) problems.push('Home did not reset the view');
  await page.keyboard.press('Escape');
  if ((await view()).focused) problems.push('Escape did not let go of the model');

  // The reset button, still there.
  await stage.focus();
  await page.keyboard.press('ArrowLeft');
  await settle();
  await page.locator('[data-model-reset]').click();
  const viaButton = await settle();
  console.log(`reset button: back at the start view: ${near(viaButton.azimuth, start.azimuth) && near(viaButton.distance, start.distance)}`);

  // ---- The copper tracks ----
  const layers = () => model.evaluate((m) => m.modelLayers());
  const tracksButton = page.locator('[data-toggle-tracks]');
  const onlyButton = page.locator('[data-toggle-tracks-only]');
  const pressed = async () => `${await tracksButton.getAttribute('aria-pressed')},${await onlyButton.getAttribute('aria-pressed')}`;
  await page.evaluate(() => document.activeElement?.blur());
  await bring();
  await settle();
  // A part-free stretch of the board: the bus of tracks down its left side,
  // 10 to 30 mm in and 115 to 165 mm down, as a box on the screen.
  const corners = await model.evaluate((m) => [[10, 115], [30, 115], [10, 165], [30, 165]].map(([x, y]) => m.modelBoardPoint(x, y)));
  const box = await rect();
  const clip = {
    x: Math.min(...corners.map((c) => c.x)) - box.x, y: Math.min(...corners.map((c) => c.y)) - box.y,
    width: Math.max(...corners.map((c) => c.x)) - Math.min(...corners.map((c) => c.x)), height: Math.max(...corners.map((c) => c.y)) - Math.min(...corners.map((c) => c.y)),
  };
  // Colour variance of the stretch (the sum of the three channels' variances),
  // and the share of the whole canvas that is copper-coloured: red at least 0.6
  // of green, and both well over blue. The tracks are, as drawn here, and so are
  // the gold contacts; the green mask (red about 0.15 of green) and the grey
  // and black parts are not.
  const surfaceOf = async () => {
    const { data, info } = await sharp(await canvas.screenshot()).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    let copper = 0;
    const sum = [0, 0, 0], sq = [0, 0, 0];
    let n = 0;
    const [x0, y0, x1, y1] = [Math.round(clip.x), Math.round(clip.y), Math.round(clip.x + clip.width), Math.round(clip.y + clip.height)];
    for (let y = 0; y < info.height; y++) {
      for (let x = 0; x < info.width; x++) {
        const i = (y * info.width + x) * info.channels;
        const [r, g, b] = [data[i], data[i + 1], data[i + 2]];
        if (r >= 0.6 * g && r - b > 25 && g - b > 15) copper++;
        if (x >= x0 && x < x1 && y >= y0 && y < y1) { [r, g, b].forEach((v, c) => { sum[c] += v; sq[c] += v * v; }); n++; }
      }
    }
    const variance = sq.reduce((a, q, c) => a + q / n - (sum[c] / n) ** 2, 0);
    return { variance, copper: copper / (info.width * info.height), n };
  };
  const waitParts = (level) => page.waitForFunction((l) => document.querySelector('[data-model]').modelLayers().partsLevel === l, level, { timeout: 10_000 }).catch(() => {});
  const l0 = await layers();
  const p0 = await pressed();
  const withTracks = await surfaceOf();
  console.log(`tracks at the start: ${JSON.stringify(l0)}, buttons pressed ${p0}, section data-model-tracks "${await model.getAttribute('data-model-tracks')}"`);
  if (!l0.tracksLoaded || !l0.tracks || p0 !== 'true,false') problems.push(`the tracks are not on at the start (${JSON.stringify(l0)}, pressed ${p0})`);
  // Show tracks, from the keyboard: Tab to it and press Enter.
  await tracksButton.focus();
  await page.keyboard.press('Enter');
  await page.waitForTimeout(400);
  const l1 = await layers();
  const p1 = await pressed();
  const without = await surfaceOf();
  console.log(`Show tracks, Enter: ${JSON.stringify(l1)}, pressed ${p1}; status "${await page.locator('[data-model-status]').innerText()}"`);
  console.log(`the board's left-hand bus, ${Math.round(clip.width)}x${Math.round(clip.height)} px (${withTracks.n} pixels): colour variance ${withTracks.variance.toFixed(1)} with the tracks, ${without.variance.toFixed(1)} without; copper-coloured pixels on the canvas ${(withTracks.copper * 100).toFixed(2)}% with, ${(without.copper * 100).toFixed(2)}% without`);
  if (l1.tracks || p1 !== 'false,false') problems.push(`Show tracks did not take the tracks off (${JSON.stringify(l1)}, pressed ${p1})`);
  if (!(withTracks.variance > without.variance * 1.5)) problems.push(`the tracks do not show: colour variance ${withTracks.variance.toFixed(1)} with them against ${without.variance.toFixed(1)} without`);
  if (!(withTracks.copper > without.copper + 0.03)) problems.push(`the tracks add too few copper-coloured pixels (${(withTracks.copper * 100).toFixed(2)}% against ${(without.copper * 100).toFixed(2)}%)`);
  // Space puts them back.
  await page.keyboard.press('Space');
  await page.waitForTimeout(400);
  const p2 = await pressed();
  if (!(await layers()).tracks || p2 !== 'true,false') problems.push(`Space on Show tracks did not put the tracks back (pressed ${p2})`);
  // Show tracks only: the parts fade out and go, the tracks stay.
  await onlyButton.click();
  await waitParts(0);
  const l3 = await layers();
  const p3 = await pressed();
  const only = await surfaceOf();
  console.log(`Show tracks only: ${JSON.stringify(l3)}, pressed ${p3}, data-model-parts "${await model.getAttribute('data-model-parts')}"; copper-coloured pixels ${(only.copper * 100).toFixed(2)}% (parts shown: ${(withTracks.copper * 100).toFixed(2)}%); the bus's colour variance ${only.variance.toFixed(1)}`);
  if (l3.partsVisible || l3.partsLevel !== 0 || !l3.tracks || p3 !== 'true,true') problems.push(`Show tracks only did not hide the parts and keep the tracks (${JSON.stringify(l3)}, pressed ${p3})`);
  if (!(only.copper > without.copper + 0.03) || !(only.variance > without.variance * 1.5)) problems.push('with the parts hidden the tracks do not show');
  // A click where a key was presses nothing now.
  const before5 = Number((await model.getAttribute('data-model-presses')) ?? 0);
  const ghost = await model.evaluate((m) => m.modelKeyPoint('5'));
  await page.mouse.click(ghost.x, ghost.y);
  await page.waitForTimeout(400);
  const after5 = Number((await model.getAttribute('data-model-presses')) ?? 0);
  console.log(`a click where the hidden 5 key was: presses ${before5} then ${after5}`);
  if (after5 !== before5) problems.push('a hidden key could still be clicked');
  // Show tracks while tracks only is on: the tracks go and the parts come back.
  await tracksButton.click();
  await waitParts(1);
  const l4 = await layers();
  const p4 = await pressed();
  console.log(`Show tracks with the parts hidden: ${JSON.stringify(l4)}, pressed ${p4}`);
  if (l4.tracks || !l4.partsVisible || l4.partsLevel !== 1 || p4 !== 'false,false') problems.push(`turning the tracks off did not bring the parts back (${JSON.stringify(l4)}, pressed ${p4})`);
  await tracksButton.click();
  if ((await pressed()) !== 'true,false' || !(await layers()).tracks) problems.push('the tracks did not come back on');

  // A phone: one finger scrolls the page until a tap focuses the model.
  const phone = await browser.newContext({ viewport: { width: 390, height: 780 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });
  const mobile = await phone.newPage();
  await watch(mobile, 'phone: ');
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
  const pinch = async (centre, from, to, steps = 6) => {
    const pair = (r) => touchPoints({ x: centre.x - r, y: centre.y }, { x: centre.x + r, y: centre.y });
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: pair(from) });
    for (let i = 1; i <= steps; i++) {
      await mobile.waitForTimeout(16);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: pair(from + ((to - from) * i) / steps) });
    }
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  };
  const mat = async (fx, fy) => { const b = await mobile.locator('[data-model-canvas]').boundingBox(); return { x: b.x + b.width * fx, y: b.y + b.height * fy }; };
  await mbring();
  const m0 = await msettle();
  const mhint = await mobile.locator('.model-hint').evaluate((h) => ({ text: h.innerText, shown: getComputedStyle(h).display !== 'none' }));
  console.log(`phone, unfocused: touch-action "${await mtouch()}", hint "${mhint.text}", shown ${mhint.shown}`);
  if ((await mtouch()) !== 'pan-y pinch-zoom') problems.push(`phone: an unfocused canvas is touch-action "${await mtouch()}"`);
  if (!mhint.shown || mhint.text !== 'Tap the model, then drag to turn and pinch to zoom') problems.push(`phone: the hint is "${mhint.text}" (shown ${mhint.shown})`);
  const ms0 = await mobile.evaluate(() => scrollY);
  await finger(await mat(0.8, 0.8), await mat(0.8, 0.3));
  const ms1 = await stillScrolling(mobile);
  await mbring();
  const m1 = await msettle();
  console.log(`phone, one finger swiped up on the unfocused model: scrollY ${ms0} to ${ms1}; azimuth ${f3(m0.azimuth)} to ${f3(m1.azimuth)}, polar ${f3(m0.polar)} to ${f3(m1.polar)}`);
  if (ms1 <= ms0) problems.push('phone: a finger on the unfocused model did not scroll the page');
  if (!near(m1.azimuth, m0.azimuth, 0.01) || !near(m1.polar, m0.polar, 0.01)) problems.push('phone: a finger on the unfocused model turned it');
  await mbring();
  const tap = await mat(0.96, 0.06);
  await mobile.touchscreen.tap(tap.x, tap.y);
  const m2 = await msettle();
  console.log(`phone, after a tap on the model: focused ${m2.focused}, touch-action "${await mtouch()}"`);
  if (!m2.focused || (await mtouch()) !== 'none') problems.push('phone: a tap did not focus the model and set touch-action none');
  await mbring();
  const ms2 = await mobile.evaluate(() => scrollY);
  await finger(await mat(0.8, 0.3), await mat(0.3, 0.45));
  const m3 = await msettle();
  const ms3 = await mobile.evaluate(() => scrollY);
  console.log(`phone, one finger dragged on the focused model: azimuth ${f3(m2.azimuth)} to ${f3(m3.azimuth)}, polar ${f3(m2.polar)} to ${f3(m3.polar)}; scrollY ${ms2} to ${ms3}`);
  if (near(m3.azimuth, m2.azimuth, 0.05)) problems.push('phone: a finger on the focused model did not turn it');
  if (ms3 !== ms2) problems.push('phone: a finger on the focused model scrolled the page');
  await pinch(await mat(0.5, 0.5), 30, 120);
  const m4 = await msettle();
  console.log(`phone, two fingers spread on the focused model: distance ${f3(m3.distance)} to ${f3(m4.distance)}; scrollY ${ms3} to ${await mobile.evaluate(() => scrollY)}`);
  if (!(m4.distance < m3.distance - 1)) problems.push('phone: two fingers spreading did not zoom the focused model in');
  await mobile.evaluate(() => document.activeElement.blur());
  console.log(`phone, after it loses focus: touch-action "${await mtouch()}"`);
  if ((await mtouch()) !== 'pan-y pinch-zoom') problems.push('phone: touch-action was not restored when the model lost focus');
  await phone.close();
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
