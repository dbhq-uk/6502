// The BBC Micro section of the browser check (scripts/browser-check.mjs calls
// it after the KIM-1's, in the same browser, with the same watch for console
// errors, failed requests and CSP violations).
//
// The registry still says the BBC Micro is planned, so the site in dist/ has no
// page for it. This builds the site again into a temporary folder with
// REGISTRY_PREVIEW set to tests/fixtures/bbc-micro-running.json, the registry
// as it will be once the machine is switched on, and serves that build with the
// site's own headers. Nothing it builds is kept.
//
// Then, on /machines/bbc-micro/: nothing of the machine is fetched before
// Start is pressed, and a disc dropped then is refused with a message rather
// than the browser leaving the page; Start downloads it and it boots to BASIC's prompt (read from
// screen memory through the page's test hook, and the canvas must be drawing);
// PRINT 6*7 and Return, pressed as real key events through the page's own
// keyboard handling, by where the keys are on the BBC (* is SHIFT and the key
// where a PC has '), shows 42 and lights more of the canvas; the headroom line
// appears; Tab moves focus off the machine; PRINT "A" tapped on the on-screen
// keys alone, SHIFT latched for each quote, prints A; the sound button turns sound on and off without an error; a disc
// image put in through the file input lists its catalogue with *CAT; Save disc
// downloads the same bytes; and Break restarts the machine.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const site = path.resolve(here, '..');
const PREVIEW = 'tests/fixtures/bbc-micro-running.json';
const TIMEOUT = 120_000;

/**
 * A 40-track single-sided DFS disc with the title TESTDISC and one file,
 * $.HELLO, 256 bytes at sector 2, laid out as docs/bbc-micro/facts/disc.md
 * section 7 gives the catalogue: names in sector 0, then for each file in
 * sector 1 its load and execution addresses, length and start sector.
 */
export function testDisc() {
  const disc = new Uint8Array(40 * 10 * 256);
  const text = (at, s) => [...s].forEach((c, i) => { disc[at + i] = c.charCodeAt(0); });
  text(0, 'TESTDISC');
  text(8, 'HELLO  ');
  disc[15] = '$'.charCodeAt(0);
  disc[256 + 5] = 8; // one file
  disc[256 + 6] = 0x01; // 400 sectors: bits 9 and 8
  disc[256 + 7] = 0x90; //              bits 7 to 0
  disc[256 + 8 + 4] = 0x00; // length 256: low byte
  disc[256 + 8 + 5] = 0x01; //             middle byte
  disc[256 + 8 + 7] = 2; // start sector
  text(512, 'HELLO FROM THE BROWSER CHECK');
  return disc;
}

export async function checkBbcMicro({ browser, watch, problems, serve }) {
  const out = fs.mkdtempSync(path.join(os.tmpdir(), 'bbc-check-'));
  const built = spawnSync(process.execPath, [path.join('node_modules', 'astro', 'bin', 'astro.mjs'), 'build', '--outDir', out], { cwd: site, env: { ...process.env, REGISTRY_PREVIEW: PREVIEW }, encoding: 'utf8' });
  if (built.status !== 0) {
    problems.push(`bbc: the preview build failed: ${built.stderr.slice(-500)}`);
    return;
  }
  const server = serve(out);
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  try {
    await run({ browser, watch, problems, origin });
  } finally {
    server.close();
    fs.rmSync(out, { recursive: true, force: true });
  }
}

async function run({ browser, watch, problems, origin }) {
  const page = await browser.newPage();
  await watch(page, 'bbc: ');
  const fetched = [];
  page.on('response', async (r) => {
    const url = new URL(r.url());
    // The machine's files, not the page itself, which lives at the folder's own address.
    if (url.pathname.startsWith('/machines/bbc-micro/') && url.pathname !== '/machines/bbc-micro/') fetched.push({ path: url.pathname, bytes: (await r.body().catch(() => Buffer.alloc(0))).length });
  });

  await page.goto(`${origin}/machines/bbc-micro/`);
  const panel = page.locator('[data-bbc]');
  await page.waitForFunction(() => document.querySelector('[data-bbc]')?.dataset.state === 'ready', null, { timeout: TIMEOUT }).catch(() => {});
  await page.waitForTimeout(1500);
  const startText = await page.locator('[data-bbc-start]').innerText();
  console.log(`bbc: page opened, state "${await panel.getAttribute('data-state')}", status "${await page.locator('[data-bbc-status]').innerText()}", button "${startText}"`);
  if ((await panel.getAttribute('data-state')) !== 'ready') problems.push('bbc: the page is not ready to start');
  if (fetched.length > 0) problems.push(`bbc: the machine was fetched before Start was pressed: ${fetched.map((f) => f.path).join(', ')}`);
  const ok = page.locator('[data-analytics-on]');
  if (await ok.isVisible().catch(() => false)) await ok.click();

  // A disc dropped before Start: refused, and the page stays.
  const dropped = await page.evaluate(() => {
    const data = new DataTransfer();
    data.items.add(new File([new Uint8Array(256)], 'early.ssd'));
    const event = new DragEvent('drop', { dataTransfer: data, cancelable: true, bubbles: true });
    document.body.dispatchEvent(event);
    return { prevented: event.defaultPrevented, status: document.querySelector('[data-bbc-status]').textContent };
  });
  console.log(`bbc: a disc dropped before Start: default prevented ${dropped.prevented}, status "${dropped.status}"`);
  if (!dropped.prevented || !/Press Start first/.test(dropped.status)) problems.push(`bbc: a disc dropped before Start gave ${JSON.stringify(dropped)}`);

  // Start: the download, then the boot to BASIC's prompt.
  const t0 = Date.now();
  await page.locator('[data-bbc-start]').click();
  await page.waitForFunction(() => ['running', 'failed'].includes(document.querySelector('[data-bbc]')?.dataset.state), null, { timeout: TIMEOUT });
  const state = await panel.getAttribute('data-state');
  console.log(`bbc: ${state} in ${Date.now() - t0} ms: "${await page.locator('[data-bbc-status]').innerText()}"`);
  if (state !== 'running') throw new Error(`bbc: the machine did not start (state ${state})`);
  const bytes = fetched.reduce((n, f) => n + f.bytes, 0);
  console.log(`bbc: Start fetched ${fetched.length} files, ${bytes} bytes; the button said "${startText}"`);

  const rows = () => page.evaluate(() => Array.from({ length: 25 }, (_, r) => document.querySelector('[data-bbc]').bbc.screenRow(r).trimEnd()));
  // Polled from here, not inside the page: the page's CSP rightly refuses new Function.
  const waitRows = async (test, label) => {
    const until = Date.now() + TIMEOUT;
    while (Date.now() < until) {
      if (test(await rows())) return true;
      await page.waitForTimeout(200);
    }
    problems.push(`bbc: ${label}; the screen shows ${JSON.stringify(await rows())}`);
    return false;
  };
  const t1 = Date.now();
  await waitRows((r) => r[1] === 'BBC Computer 32K' && r.includes('BASIC') && r[r.findLastIndex((x) => x !== '')] === '>', 'it did not boot to the prompt');
  console.log(`bbc: the prompt after ${Date.now() - t1} ms: ${JSON.stringify((await rows()).slice(0, 8))}`);

  // The canvas draws the picture: some of it is not black, and fields keep coming.
  const drawn = () => page.evaluate(() => {
    const c = document.querySelector('[data-bbc-canvas]');
    const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
    let lit = 0;
    for (let i = 0; i < d.length; i += 4) if (d[i] + d[i + 1] + d[i + 2] > 0) lit++;
    return { lit, size: `${c.width}x${c.height}`, shown: `${c.clientWidth}x${c.clientHeight}`, frames: Number(document.querySelector('[data-bbc]').dataset.frames) };
  });
  const a = await drawn();
  await page.waitForTimeout(1000);
  const b = await drawn();
  console.log(`bbc: canvas ${a.size}, shown at ${a.shown}, ${a.lit} pixels lit; fields ${a.frames} then ${b.frames} a second later`);
  if (a.lit < 1000) problems.push(`bbc: the canvas is blank (${a.lit} pixels lit)`);
  if (!(b.frames > a.frames + 20)) problems.push(`bbc: the picture is not being redrawn (fields ${a.frames} then ${b.frames})`);
  const [w, h] = a.shown.split('x').map(Number);
  if (Math.abs(w / h - 4 / 3) > 0.02) problems.push(`bbc: the screen is shown ${a.shown}, not four by three`);

  // The picture's handover, timed: a frame's copy into the canvas's pixels.
  const handover = await page.evaluate(() => {
    const host = document.querySelector('[data-bbc]').bbc.host;
    const t = performance.now();
    for (let i = 0; i < 50; i++) host.Picture();
    return (performance.now() - t) / 50;
  });
  console.log(`bbc: handing over one picture, 640 by 512, takes ${handover.toFixed(3)} ms`);

  // The keyboard: Start gave the screen focus. PRINT 6*7, by the BBC's places.
  const focused = await page.evaluate(() => document.activeElement?.hasAttribute('data-bbc-screen') ?? false);
  console.log(`bbc: the screen has focus after Start: ${focused}; typing ${await panel.getAttribute('data-typing')}`);
  if (!focused) problems.push('bbc: Start did not give the screen the keyboard');
  const type = async (keys) => { for (const k of keys) await page.keyboard.press(k); };
  const litBefore = (await drawn()).lit;
  await type(['KeyP', 'KeyR', 'KeyI', 'KeyN', 'KeyT', 'Space', 'Digit6', 'Shift+Quote', 'Digit7', 'Enter']);
  if (await waitRows((r) => r.indexOf('>PRINT 6*7') >= 0 && r[r.indexOf('>PRINT 6*7') + 1].trim() === '42', 'PRINT 6*7 did not show 42')) {
    const r = await rows();
    const i = r.indexOf('>PRINT 6*7');
    console.log(`bbc: typed PRINT 6*7 and Return: the screen shows "${r[i]}", then "${r[i + 1].trim()}", then "${r[i + 2]}"`);
  }
  // The picture shows it too: the text added lights more of the canvas (the answer itself is
  // read off the picture by BbcAcceptanceTests; here the screen memory is read, and the canvas
  // must have changed with it).
  await page.waitForTimeout(300);
  const litAfter = (await drawn()).lit;
  console.log(`bbc: lit pixels on the canvas ${litBefore} before typing, ${litAfter} after`);
  if (!(litAfter > litBefore)) problems.push(`bbc: the canvas did not change when the text did (${litBefore} then ${litAfter} lit pixels)`);

  // The headroom line, once a second.
  await page.waitForFunction(() => document.querySelector('[data-bbc-speed]')?.textContent !== '', null, { timeout: 10_000 }).catch(() => {});
  const speed = await page.locator('[data-bbc-speed]').innerText();
  console.log(`bbc: speed line "${speed}"; running ${await panel.getAttribute('data-actual-mhz')} MHz, capacity ${await panel.getAttribute('data-capacity-mhz')} MHz`);
  if (!/^Running at (the BBC Micro's own 2 MHz|[\d.]+ MHz, slower than the BBC Micro's 2 MHz)/.test(speed)) problems.push(`bbc: the headroom line is "${speed}"`);

  // Tab moves on: the page never keeps the keyboard.
  await page.keyboard.press('Tab');
  const left = await page.evaluate(() => !(document.activeElement?.hasAttribute('data-bbc-screen') ?? false));
  console.log(`bbc: Tab moves focus off the screen: ${left}; typing ${await panel.getAttribute('data-typing')}`);
  if (!left) problems.push('bbc: Tab did not move focus off the screen');

  // The on-screen keys alone: PRINT "A", SHIFT latched for each quote (SHIFT and 2 is ").
  const tapKey = (key) => page.locator(`[data-bbc-key="${key}"]`).click();
  for (const k of ['P', 'R', 'I', 'N', 'T', 'Space']) await tapKey(k);
  await tapKey('Shift');
  const latched = await page.locator('[data-bbc-key="Shift"]').getAttribute('aria-pressed');
  await tapKey('D2');
  const unlatched = await page.locator('[data-bbc-key="Shift"]').getAttribute('aria-pressed');
  await tapKey('A');
  await tapKey('Shift');
  await tapKey('D2');
  await tapKey('Return');
  console.log(`bbc: on-screen SHIFT pressed "${latched}", after the next key "${unlatched}"`);
  if (latched !== 'true' || unlatched !== 'false') problems.push(`bbc: the on-screen SHIFT did not latch for one key (${latched}, then ${unlatched})`);
  if (await waitRows((r) => r.indexOf('>PRINT "A"') >= 0 && r[r.indexOf('>PRINT "A"') + 1].trim() === 'A', 'PRINT "A" on the on-screen keys did not print A')) {
    const r = await rows();
    const i = r.indexOf('>PRINT "A"');
    console.log(`bbc: tapped PRINT "A" and RETURN on the on-screen keys: the screen shows "${r[i]}", then "${r[i + 1].trim()}"`);
  }

  // Sound on, then off: headless Chrome may play nothing, but nothing may fail.
  const sound = page.locator('[data-bbc-sound]');
  await sound.click();
  await page.waitForFunction(() => ['on', 'failed'].includes(document.querySelector('[data-bbc]').dataset.sound), null, { timeout: 10_000 }).catch(() => {});
  const on = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-bbc-sound-status]').innerText() };
  await page.waitForTimeout(500);
  await sound.click();
  await page.waitForFunction(() => document.querySelector('[data-bbc]').dataset.sound === 'off', null, { timeout: 10_000 }).catch(() => {});
  const off = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-bbc-sound-status]').innerText() };
  console.log(`bbc: sound on: ${JSON.stringify(on)}; off again: ${JSON.stringify(off)}`);
  if (on.state !== 'on' || on.button !== 'Turn sound off' || on.line !== 'Sound is on.') problems.push(`bbc: turning the sound on gave ${JSON.stringify(on)}`);
  if (off.state !== 'off' || off.button !== 'Turn sound on' || off.line !== 'Sound is off.') problems.push(`bbc: turning the sound off gave ${JSON.stringify(off)}`);

  // A disc through the file input, its catalogue with *CAT.
  const disc = Buffer.from(testDisc());
  await page.locator('[data-bbc-file]').setInputFiles({ name: 'test.ssd', mimeType: 'application/octet-stream', buffer: disc });
  await page.waitForFunction(() => document.querySelector('[data-bbc]').dataset.disc === 'test.ssd', null, { timeout: 10_000 }).catch(() => {});
  const drive = await page.locator('[data-bbc-drive]').innerText();
  console.log(`bbc: disc inserted: "${drive}"`);
  if (!drive.startsWith('In drive 0: test.ssd, 40 tracks, single sided')) problems.push(`bbc: the drive says "${drive}"`);
  await page.locator('[data-bbc-screen]').click();
  await type(['Shift+Quote', 'KeyC', 'KeyA', 'KeyT', 'Enter']);
  const listed = (r) => r.slice(Math.max(0, r.lastIndexOf('>*CAT')));
  if (await waitRows((r) => r.lastIndexOf('>*CAT') >= 0 && listed(r).some((x) => x.startsWith('TESTDISC')) && listed(r).some((x) => x.trim() === 'HELLO'), '*CAT did not list the disc')) {
    const r = await rows();
    console.log(`bbc: *CAT lists ${JSON.stringify(r.slice(r.lastIndexOf('>*CAT')).filter((x) => x !== ''))}`);
  }

  // Save disc downloads the disc as it stands: *CAT wrote nothing, so the same bytes.
  const [download] = await Promise.all([page.waitForEvent('download', { timeout: 10_000 }), page.locator('[data-bbc-save]').click()]);
  const saved = fs.readFileSync(await download.path());
  console.log(`bbc: Save disc downloaded ${download.suggestedFilename()}, ${saved.length} bytes, the same as went in: ${saved.equals(disc)}`);
  if (download.suggestedFilename() !== 'test.ssd' || !saved.equals(disc)) problems.push('bbc: Save disc did not download the disc that went in');

  // Break restarts the machine: the banner after a BREAK has no memory size.
  await page.locator('[data-bbc-break]').click();
  await waitRows((r) => r.includes('BBC Computer') && !r.includes('>*CAT'), 'Break did not restart the machine');
  console.log(`bbc: after Break: ${JSON.stringify((await rows()).filter((x) => x !== ''))}`);

  // Every image on the page loads, from this site.
  const images = await page.evaluate(async () => {
    const all = [...document.images];
    for (const i of all) { i.loading = 'eager'; i.scrollIntoView(); }
    await new Promise((r) => setTimeout(r, 1500));
    return all.map((i) => ({ src: new URL(i.currentSrc || i.src).pathname, ok: i.complete && i.naturalWidth > 0, local: new URL(i.currentSrc || i.src).origin === location.origin }));
  });
  console.log(`bbc: images ${images.map((i) => `${i.src} ${i.ok ? 'loaded' : 'NOT LOADED'}`).join('; ')}`);
  for (const i of images) if (!i.ok || !i.local) problems.push(`bbc: the image ${i.src} did not load from this site`);
  await page.close();
}
