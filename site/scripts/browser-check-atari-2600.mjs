// The Atari 2600 section of the browser check (scripts/browser-check.mjs calls
// it after the Electron's, in the same browser, with the same watch for console
// errors, failed requests and CSP violations). It runs on the KIM-1's server,
// which serves dist/ with the site's own headers.
//
// On /machines/atari-2600/: nothing of the machine is fetched before Start is
// pressed, and a file dropped then is refused with a message rather than the
// browser leaving the page. Start downloads the machine and puts the bundled
// game, 2048 2600, in. Then the recorded frames: the bundled game's title
// changes its colours every frame, so for each region the page's test hook,
// panel.vcs.stepTo(n), lets go of every input, keeps the switches the panel
// shows, switches the machine off and on and runs exactly the frame count
// machines/atari-2600/expected-frames.json records, with the colour switch set
// through the page's own button to the position the file records for that
// region (read here, not copied); the canvas's RGBA bytes must hash to the
// recorded `rgba` value (the plan's "Fixed before running", item 14), and must
// not be blank. Between the two, the region control is switched with the loop
// held: the frame count must not move (no restart), the line under the control
// must say so, and the same frame run in the other region, the switches as
// they were, must be the same picture in that region's colours: every colour
// on the rows both windows show maps to one colour in the other, the same at
// every pixel, and not every colour is the same.
//
// Then the loop runs on its own. Through the page's keyboard handling, on the
// focused screen: the left fire button (Space) starts a game, and the picture
// becomes the board, which changes far more than the title does by itself;
// joystick 1 left (the left arrow) moves its tiles, which changes the picture
// more than twice as much as the board changes by itself; and Reset, held
// through the panel's button, changes it again. The headroom line appears, and
// the sound turns on and off. Then a test cartridge from the suite's fork,
// harness/calibration, fetched at the pinned commit and checked against the
// git blob hash the pinned manifest gives (AGENTS.md rule 3), goes in through
// the picker and changes the picture; a file of text and a file over the size
// limit are each refused with a plain sentence, the machine carrying on. Every
// image on the page loads, from this site.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { pin, PINS_FILE } from '../src/lib/pins.mjs';
import { WINDOWS, WIDTH } from '../public/atari-2600-keys.js';

const TIMEOUT = 120_000;
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const EXPECTED = path.join(repo, 'machines', 'atari-2600', 'expected-frames.json');
const MANIFEST = path.join(path.dirname(PINS_FILE), 'atari-2600-suite.manifest');
const TEST_ROM = 'crates/systems/vcs/tests/accuracy/roms/harness/calibration_ntsc.a26';

const sha256 = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');
const gitBlob = (bytes) => crypto.createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');

/**
 * The suite's harness/calibration for NTSC, from the fork dbhq-uk/missingno at
 * the commit Pins.cs pins, checked against the git blob hash in the suite's
 * manifest, whose own SHA-256 Pins.cs pins, as Atari2600Suite.Read checks it.
 * Kept in .testdata/ where Atari2600Suite.Read keeps it, so a run that has it
 * does not fetch it again. Never committed and never published.
 */
export async function testRom() {
  const manifest = fs.readFileSync(MANIFEST);
  if (sha256(manifest) !== pin('Atari2600SuiteManifestSha256')) throw new Error('atari-2600-suite.manifest does not match Pins.Atari2600SuiteManifestSha256');
  const entry = manifest.toString('utf8').split('\n').map((l) => l.split(' ')).find(([, fork, p]) => fork === 'missingno' && p === TEST_ROM);
  if (!entry) throw new Error(`the manifest has no ${TEST_ROM} in missingno`);
  const commit = pin('Atari2600BinariesCommit');
  const local = path.join(repo, '.testdata', 'atari-2600', 'missingno', commit, ...TEST_ROM.split('/'));
  let rom = fs.existsSync(local) ? fs.readFileSync(local) : null;
  if (!rom || gitBlob(rom) !== entry[0]) {
    const url = `https://raw.githubusercontent.com/dbhq-uk/missingno/${commit}/${TEST_ROM}`;
    const response = await fetch(url);
    if (!response.ok) throw new Error(`HTTP ${response.status}: ${url}`);
    rom = Buffer.from(await response.arrayBuffer());
    if (gitBlob(rom) !== entry[0]) throw new Error(`${url} does not match its git blob hash ${entry[0]}`);
    fs.mkdirSync(path.dirname(local), { recursive: true });
    fs.writeFileSync(local, rom);
  }
  return { name: path.posix.basename(TEST_ROM), bytes: rom, blob: entry[0], commit };
}

/**
 * Whether two pictures are the same picture in other colours: on the rows they
 * share (`rowsA` of a from `fromA`, the same count of b from `fromB`, `width`
 * pixels each, RGBA), every colour of a maps to one colour of b, the same at
 * every pixel, and the reverse. Gives the colours of a, how many map to a
 * colour other than themselves, and the first pixel that breaks the mapping.
 * Exported for its test.
 */
export function sameInOtherColours(a, b, { width, rows, fromA, fromB }) {
  const forward = new Map();
  const back = new Map();
  let broken = null;
  for (let y = 0; y < rows && !broken; y++) {
    for (let x = 0; x < width; x++) {
      const i = ((fromA + y) * width + x) * 4;
      const j = ((fromB + y) * width + x) * 4;
      const ca = (a[i] << 16) | (a[i + 1] << 8) | a[i + 2];
      const cb = (b[j] << 16) | (b[j + 1] << 8) | b[j + 2];
      if ((forward.has(ca) && forward.get(ca) !== cb) || (back.has(cb) && back.get(cb) !== ca)) {
        broken = { x, y };
        break;
      }
      forward.set(ca, cb);
      back.set(cb, ca);
    }
  }
  const changed = [...forward].filter(([ca, cb]) => ca !== cb).length;
  return { consistent: broken === null, colours: forward.size, changed, broken };
}

/** The number of pixels whose colour differs between two RGBA pictures of the same size. */
export const differing = (a, b) => {
  let n = 0;
  for (let i = 0; i < a.length; i += 4) if (a[i] !== b[i] || a[i + 1] !== b[i + 1] || a[i + 2] !== b[i + 2]) n++;
  return n;
};

export async function checkAtari2600({ browser, watch, problems, origin }) {
  const expected = JSON.parse(fs.readFileSync(EXPECTED, 'utf8'));
  const page = await browser.newPage();
  await watch(page, 'atari-2600: ');
  const fetched = [];
  page.on('response', async (r) => {
    const url = new URL(r.url());
    if (url.pathname.startsWith('/machines/atari-2600/') && url.pathname !== '/machines/atari-2600/') fetched.push({ path: url.pathname, bytes: (await r.body().catch(() => Buffer.alloc(0))).length });
  });

  await page.goto(`${origin}/machines/atari-2600/`);
  const panel = page.locator('[data-vcs]');
  await page.waitForFunction(() => document.querySelector('[data-vcs]')?.dataset.state === 'ready', null, { timeout: TIMEOUT }).catch(() => {});
  await page.waitForTimeout(1500);
  const startText = await page.locator('[data-vcs-start]').innerText();
  console.log(`atari-2600: page opened, state "${await panel.getAttribute('data-state')}", status "${await page.locator('[data-vcs-status]').innerText()}", button "${startText}"`);
  if ((await panel.getAttribute('data-state')) !== 'ready') problems.push('atari-2600: the page is not ready to start');
  if (fetched.length > 0) problems.push(`atari-2600: the machine was fetched before Start was pressed: ${fetched.map((f) => f.path).join(', ')}`);
  const ok = page.locator('[data-analytics-on]');
  if (await ok.isVisible().catch(() => false)) await ok.click();

  // A file dropped before Start: refused, and the page stays.
  const dropped = await page.evaluate(() => {
    const data = new DataTransfer();
    data.items.add(new File([new Uint8Array(2048)], 'early.a26'));
    const event = new DragEvent('drop', { dataTransfer: data, cancelable: true, bubbles: true });
    document.body.dispatchEvent(event);
    return { prevented: event.defaultPrevented, status: document.querySelector('[data-vcs-status]').textContent };
  });
  console.log(`atari-2600: a file dropped before Start: default prevented ${dropped.prevented}, status "${dropped.status}"`);
  if (!dropped.prevented || !/Press Start first/.test(dropped.status)) problems.push(`atari-2600: a file dropped before Start gave ${JSON.stringify(dropped)}`);
  if (fetched.length > 0) problems.push('atari-2600: a file dropped before Start fetched the machine');

  // Start: the download, the bundled game, and the machine running.
  const t0 = Date.now();
  await page.locator('[data-vcs-start]').click();
  await page.waitForFunction(() => ['running', 'failed', 'waiting'].includes(document.querySelector('[data-vcs]')?.dataset.state), null, { timeout: TIMEOUT });
  const state = await panel.getAttribute('data-state');
  console.log(`atari-2600: ${state} in ${Date.now() - t0} ms: "${await page.locator('[data-vcs-status]').innerText()}"`);
  if (state !== 'running') throw new Error(`atari-2600: the machine did not start with its game (state ${state})`);
  console.log(`atari-2600: Start fetched ${fetched.length} files, ${fetched.reduce((n, f) => n + f.bytes, 0)} bytes; the button said "${startText}"`);
  const cartridge = await panel.getAttribute('data-cartridge');
  console.log(`atari-2600: cartridge "${cartridge}"; "${await page.locator('[data-vcs-cartridge]').innerText()}"`);
  if (cartridge !== '2048 2600') problems.push(`atari-2600: Start put in "${cartridge}", not 2048 2600`);

  // The canvas: its bytes' SHA-256, how many colours, its size, and the size it is shown at.
  const canvas = () => page.evaluate(async () => {
    const c = document.querySelector('[data-vcs-canvas]');
    const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
    const hash = [...new Uint8Array(await crypto.subtle.digest('SHA-256', d))].map((b) => b.toString(16).padStart(2, '0')).join('');
    const colours = new Set();
    for (let i = 0; i < d.length; i += 4) colours.add((d[i] << 16) | (d[i + 1] << 8) | d[i + 2]);
    return { hash, colours: colours.size, pixels: Array.from(d), size: `${c.width}x${c.height}`, shown: `${c.clientWidth}x${c.clientHeight}`, frames: Number(document.querySelector('[data-vcs]').dataset.frames) };
  });
  const region = () => page.evaluate(() => document.querySelector('[data-vcs]').vcs.region());
  const colourSwitch = () => page.locator('[data-vcs-toggle="BlackAndWhite"]').getAttribute('aria-pressed');
  // The colour switch, through the page's own button, to the position the file names.
  const setColourSwitch = async (position) => {
    const want = position === 'black and white' ? 'true' : 'false';
    if ((await colourSwitch()) !== want) await page.locator('[data-vcs-toggle="BlackAndWhite"]').click();
    const now = await colourSwitch();
    if (now !== want) problems.push(`atari-2600: the colour switch did not go to ${position} (aria-pressed ${now})`);
  };
  // The region, through the page's own radios.
  const choose = async (value) => {
    await page.locator(`[data-vcs-region][value="${value}"]`).check();
    await page.waitForFunction((v) => document.querySelector('[data-vcs]').dataset.region === v, value, { timeout: 10_000 }).catch(() => {});
    return page.locator('[data-vcs-region-line]').innerText();
  };
  // Power on to exactly n frames through the hook, the canvas cleared first to transparent black, which the
  // machine's picture never is (every pixel it paints is opaque), so a stepTo that does not paint fails.
  const stepTo = (frames) => page.evaluate((n) => {
    const c = document.querySelector('[data-vcs-canvas]');
    c.getContext('2d').clearRect(0, 0, c.width, c.height);
    return document.querySelector('[data-vcs]').vcs.stepTo(n);
  }, frames);
  const recorded = async (name) => {
    if ((await region()) !== name) await choose(name);
    await setColourSwitch(expected.switches[name]);
    const n = await stepTo(expected.frames[name]);
    const got = await canvas();
    const want = expected.rgba[name];
    console.log(`atari-2600: ${name}, colour switch at ${expected.switches[name]}, ${n} frames from power on: canvas ${got.size} shown at ${got.shown}, ${got.colours} colours, SHA-256 ${got.hash} ${got.hash === want ? 'matches' : 'DOES NOT MATCH'} the recorded ${want}`);
    if (n !== expected.frames[name]) problems.push(`atari-2600: ${name}: stepTo ran ${n} frames, not ${expected.frames[name]}`);
    if (got.size !== `${WIDTH}x${WINDOWS[name].lines}`) problems.push(`atari-2600: ${name}: the canvas is ${got.size}, not the region's window`);
    if (got.colours < 2) problems.push(`atari-2600: ${name}: the canvas is blank at frame ${n}`);
    if (got.hash !== want) problems.push(`atari-2600: ${name}: the frame on the canvas is not the recorded one`);
    return got;
  };

  // NTSC first, as the machine starts.
  const first = await region();
  if (first !== 'NTSC') problems.push(`atari-2600: the machine started as ${first}, not NTSC`);
  const ntsc = await recorded('NTSC');

  // The region switched with the loop held: no restart, and the line says so; then the same frame in PAL's colours.
  const framesBefore = await page.evaluate(() => document.querySelector('[data-vcs]').vcs.host.Frames());
  const line = await choose('PAL');
  const framesAfter = await page.evaluate(() => document.querySelector('[data-vcs]').vcs.host.Frames());
  console.log(`atari-2600: region control to PAL with the loop held: frames ${framesBefore} then ${framesAfter}; "${line}"`);
  if (framesAfter !== framesBefore) problems.push(`atari-2600: switching the region restarted the machine (frames ${framesBefore} then ${framesAfter})`);
  if (!/^Now PAL: the colours and the clock changed at once, and the game carries on without a restart\./.test(line)) problems.push(`atari-2600: switching the region said "${line}"`);
  if ((await region()) !== 'PAL') problems.push('atari-2600: the region control did not switch the machine to PAL');
  const n = await stepTo(expected.frames.NTSC);
  const palColours = await canvas();
  const shared = { width: WIDTH, rows: Math.min(WINDOWS.NTSC.first + WINDOWS.NTSC.lines, WINDOWS.PAL.first + WINDOWS.PAL.lines) - Math.max(WINDOWS.NTSC.first, WINDOWS.PAL.first) };
  const lowest = Math.max(WINDOWS.NTSC.first, WINDOWS.PAL.first);
  const same = sameInOtherColours(ntsc.pixels, palColours.pixels, { ...shared, fromA: lowest - WINDOWS.NTSC.first, fromB: lowest - WINDOWS.PAL.first });
  console.log(`atari-2600: the same ${n} frames on PAL, the switches as on NTSC: canvas ${palColours.size}; on the ${shared.rows} rows both windows show, ${same.colours} colours, each one colour in the other ${same.consistent ? 'at every pixel' : `EXCEPT at ${JSON.stringify(same.broken)}`}, ${same.changed} of them changed`);
  if (!same.consistent || same.colours < 2) problems.push(`atari-2600: on PAL the same frame is not the same picture (${JSON.stringify(same)})`);
  if (same.changed < 1) problems.push('atari-2600: switching the region did not change the colours');

  await recorded('PAL');
  await recorded('NTSC');

  // Let the loop run again, and play through the page: the screen has the keyboard.
  await page.evaluate(() => document.querySelector('[data-vcs]').vcs.play());
  await page.locator('[data-vcs-screen]').focus();
  const look = async (ms) => {
    await page.waitForTimeout(ms);
    return canvas();
  };
  const press = async (key, ms = 200) => {
    await page.keyboard.down(key);
    await page.waitForTimeout(ms);
    await page.keyboard.up(key);
  };
  const title = await look(1000);
  const titleLater = await look(500);
  await press('Space');
  const board = await look(1500);
  const boardLater = await look(500);
  await press('ArrowLeft');
  const moved = await look(1500);
  const pixels = WIDTH * WINDOWS.NTSC.lines;
  const byItself = { title: differing(title.pixels, titleLater.pixels), board: differing(board.pixels, boardLater.pixels) };
  const byFire = differing(titleLater.pixels, board.pixels);
  const byStick = differing(boardLater.pixels, moved.pixels);
  console.log(`atari-2600: frames ${title.frames}, ${titleLater.frames}, ${board.frames}, ${moved.frames}; of ${pixels} pixels, the title changed ${byItself.title} by itself in half a second, fire (Space) changed ${byFire}; the board changed ${byItself.board} by itself in half a second, joystick 1 left (the left arrow) changed ${byStick}`);
  if (!(moved.frames > title.frames + 60)) problems.push(`atari-2600: the picture is not being redrawn (frames ${title.frames} then ${moved.frames})`);
  if (!(byFire > 2 * byItself.title && byFire > 500)) problems.push(`atari-2600: the fire button on the keyboard did not start a game (${byFire} pixels changed, ${byItself.title} by itself)`);
  if (!(byStick > 0 && byStick > 2 * byItself.board)) problems.push(`atari-2600: joystick 1 on the keyboard did not move the tiles (${byStick} pixels changed, ${byItself.board} by itself)`);
  // Reset, held through the panel's button for a moment, as on the console.
  const reset = page.locator('[data-vcs-hold="Reset"]');
  await reset.scrollIntoViewIfNeeded();
  const resetBox = await reset.boundingBox();
  await page.mouse.move(resetBox.x + resetBox.width / 2, resetBox.y + resetBox.height / 2);
  await page.mouse.down();
  await page.waitForTimeout(200);
  const held = { pressed: await reset.getAttribute('aria-pressed'), switches: await panel.getAttribute('data-switches') };
  await page.mouse.up();
  const afterReset = await look(1500);
  const afterResetLater = await look(500);
  const byReset = differing(moved.pixels, afterReset.pixels);
  console.log(`atari-2600: Reset held on the panel (aria-pressed ${held.pressed}, switches ${held.switches}), then let go: ${byReset} pixels changed; then ${differing(afterReset.pixels, afterResetLater.pixels)} by itself in half a second; released, aria-pressed ${await reset.getAttribute('aria-pressed')}`);
  if (held.pressed !== 'true' || (Number(held.switches) & 1) !== 1) problems.push(`atari-2600: the panel's Reset was not held while pressed (${JSON.stringify(held)})`);
  if ((await reset.getAttribute('aria-pressed')) !== 'false') problems.push('atari-2600: the panel\'s Reset did not spring back');
  if (!(byReset > 0)) problems.push('atari-2600: Reset through the panel did not change the picture');

  // The headroom line, once a second.
  await page.waitForTimeout(2500);
  const speed = await page.locator('[data-vcs-speed]').innerText();
  console.log(`atari-2600: speed line "${speed}"; running ${await panel.getAttribute('data-actual-mhz')} MHz, capacity ${await panel.getAttribute('data-capacity-mhz')} MHz`);
  if (!/^Running at (the Atari 2600's own [\d.]+ MHz|[\d.]+ MHz, slower than the Atari 2600's [\d.]+ MHz)/.test(speed)) problems.push(`atari-2600: the headroom line is "${speed}"`);

  // Sound on, then off: headless Chrome may play nothing, but nothing may fail.
  const sound = page.locator('[data-vcs-sound]');
  await sound.click();
  await page.waitForFunction(() => ['on', 'failed'].includes(document.querySelector('[data-vcs]').dataset.sound), null, { timeout: 10_000 }).catch(() => {});
  const on = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-vcs-sound-status]').innerText() };
  await page.waitForTimeout(500);
  await sound.click();
  await page.waitForFunction(() => document.querySelector('[data-vcs]').dataset.sound === 'off', null, { timeout: 10_000 }).catch(() => {});
  const off = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-vcs-sound-status]').innerText() };
  console.log(`atari-2600: sound on: ${JSON.stringify(on)}; off again: ${JSON.stringify(off)}`);
  if (on.state !== 'on' || on.button !== 'Turn sound off' || on.line !== 'Sound is on.') problems.push(`atari-2600: turning the sound on gave ${JSON.stringify(on)}`);
  if (off.state !== 'off' || off.button !== 'Turn sound on' || off.line !== 'Sound is off.') problems.push(`atari-2600: turning the sound off gave ${JSON.stringify(off)}`);

  // The picker: a test cartridge from the suite's fork, pinned and hash-checked, changes the picture.
  const rom = await testRom();
  console.log(`atari-2600: test cartridge ${rom.name}, ${rom.bytes.length} bytes, from dbhq-uk/missingno at ${rom.commit.slice(0, 12)}, git blob ${rom.blob}, hash checked`);
  const playing = await canvas();
  await page.locator('[data-vcs-file]').setInputFiles({ name: rom.name, mimeType: 'application/octet-stream', buffer: rom.bytes });
  await page.waitForFunction((name) => document.querySelector('[data-vcs]').dataset.cartridge === name, rom.name, { timeout: 10_000 }).catch(() => {});
  const loaded = await look(1500);
  const cartLine = await page.locator('[data-vcs-cartridge]').innerText();
  console.log(`atari-2600: "${cartLine}"; ${loaded.colours} colours on the canvas, ${differing(playing.pixels, loaded.pixels)} pixels changed`);
  if ((await panel.getAttribute('data-cartridge')) !== rom.name) problems.push('atari-2600: the test cartridge did not go in through the picker');
  if (!cartLine.startsWith(`${rom.name} is in. The board is `)) problems.push(`atari-2600: the line under the picker said "${cartLine}"`);
  if (loaded.colours < 2 || loaded.hash === playing.hash) problems.push(`atari-2600: the test cartridge did not draw its picture (${loaded.colours} colours)`);

  // Files the machine cannot take: each refused with a plain sentence, and the machine carries on.
  const refuse = async (file, why) => {
    const framesBefore2 = Number(await panel.getAttribute('data-frames'));
    await page.locator('[data-vcs-file]').setInputFiles(file);
    await page.waitForFunction((name) => document.querySelector('[data-vcs-cartridge]').textContent.startsWith(name), file.name, { timeout: 10_000 }).catch(() => {});
    await page.waitForTimeout(1000);
    const said = await page.locator('[data-vcs-cartridge]').innerText();
    const still = await panel.getAttribute('data-cartridge');
    const framesAfter2 = Number(await panel.getAttribute('data-frames'));
    console.log(`atari-2600: ${file.name}, ${file.buffer.length} bytes: "${said}"; the cartridge is still ${still}; frames ${framesBefore2} then ${framesAfter2}`);
    if (!why.test(said) || !said.endsWith('The machine carries on as it was.')) problems.push(`atari-2600: ${file.name} gave "${said}"`);
    if (still !== rom.name || !(framesAfter2 > framesBefore2)) problems.push(`atari-2600: after ${file.name} the machine did not carry on as it was`);
  };
  await refuse({ name: 'notes.a26', mimeType: 'text/plain', buffer: Buffer.from('These are notes, not a cartridge.\n') }, /^notes\.a26 was not loaded\. .+/);
  const limit = await page.evaluate(() => document.querySelector('[data-vcs]').vcs.host.MaxRomBytes());
  await refuse({ name: 'huge.a26', mimeType: 'application/octet-stream', buffer: Buffer.alloc(limit + 1) }, /^huge\.a26 is [\d,]+ bytes, more than the [\d,]+ bytes of the largest file this machine loads, so it was not read\./);

  // Every image on the page loads, from this site.
  const images = await page.evaluate(async () => {
    const all = [...document.images];
    for (const i of all) { i.loading = 'eager'; i.scrollIntoView(); }
    await new Promise((r) => setTimeout(r, 1500));
    return all.map((i) => ({ src: new URL(i.currentSrc || i.src).pathname, ok: i.complete && i.naturalWidth > 0, local: new URL(i.currentSrc || i.src).origin === location.origin }));
  });
  console.log(`atari-2600: images ${images.map((i) => `${i.src} ${i.ok ? 'loaded' : 'NOT LOADED'}`).join('; ')}`);
  for (const i of images) if (!i.ok || !i.local) problems.push(`atari-2600: the image ${i.src} did not load from this site`);
  if (images.length < 2) problems.push(`atari-2600: the page shows ${images.length} images, not its photographs`);
  console.log(`atari-2600: the recorded frames are from ${path.relative(repo, EXPECTED)}, sha256 of the file ${sha256(fs.readFileSync(EXPECTED))}`);
  await page.close();
}
