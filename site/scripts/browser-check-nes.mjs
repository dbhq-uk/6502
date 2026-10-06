// The NES section of the browser check (scripts/browser-check.mjs calls it
// after the BBC Micro's, in the same browser, with the same watch for console
// errors, failed requests and CSP violations).
//
// It runs on the KIM-1's server, which serves dist/ with the site's own headers.
//
// On /machines/nes/: nothing of the machine is fetched before Start is pressed,
// and a file dropped then is refused with a message rather than the browser
// leaving the page. Start downloads it and puts the bundled game in. Then, for
// each region, the page's test hook runs the machine from power on to exactly
// the frame machines/nes/expected-frames.json records (read here, not copied),
// and the canvas's bytes must hash to the recorded SHA-256 and must not be
// blank; switching the region control says so in the line under it, and the
// same frame must match again. Then the loop runs on its own: Start pressed
// through the page's keyboard handling (Enter on the focused screen) must change
// the picture, the counters the board model reads (panel.nes.accessCounts())
// must be four numbers with the PPU's moving, the headroom line must appear,
// and the sound turns on and off.
// Then the board's 3D model, with the game running: nothing under /models/ was
// fetched before the visitor scrolled to it, and scrolling loads its bundle and
// track map; its canvas draws; pointing at the PPU names it on the status line
// and lights its legend row; the PPU (U5) and controller port 1's buffer (U7)
// are marked at least once in two seconds of looking every 50 ms, and their
// legend rates are above zero; switching the region to PAL switches the model
// to the PAL console (its region, its accessible name and description, the
// PPU's part in the legend) with nothing more fetched, and the rates show
// nothing for a sample, then come back; switching back, the same; Show tracks
// and Show tracks only work; and from below, the board shows its solder side's
// copper where the map has it.
// Then the cartridge picker: a tiny NROM test cartridge, built here from bytes
// (never a game), goes in and the screen turns its colour; a file of text and a
// file over the size limit are each refused with a plain sentence, and the
// machine carries on as it was. Every image on the page loads, from this site.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';
import { BOARD, TRACKS } from '../src/models/nes-famicom-board-layout.mjs';

const TIMEOUT = 120_000;
const here = path.dirname(fileURLToPath(import.meta.url));
const EXPECTED = path.resolve(here, '..', '..', 'machines', 'nes', 'expected-frames.json');

/**
 * A 16 KB NROM cartridge with no character ROM (so 8 KB of CHR RAM) that waits
 * for two VBlanks, writes one colour to the PPU's backdrop entry ($3F00) with
 * rendering off, points the PPU's address back at $0000 and loops. The screen is
 * then that one colour everywhere. The header says nothing of the region.
 */
export function testCartridge(colour = 0x21) {
  const header = [0x4e, 0x45, 0x53, 0x1a, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
  const prg = new Uint8Array(16 * 1024).fill(0xea);
  const code = [
    0x78, // SEI
    0xd8, // CLD
    0xa2, 0xff, 0x9a, // LDX #$FF; TXS
    0xa9, 0x00, 0x8d, 0x00, 0x20, // LDA #0; STA $2000 (no NMI)
    0x8d, 0x01, 0x20, // STA $2001 (rendering off)
    0x2c, 0x02, 0x20, 0x10, 0xfb, // wait: BIT $2002; BPL wait
    0x2c, 0x02, 0x20, 0x10, 0xfb, // and again
    0xa9, 0x3f, 0x8d, 0x06, 0x20, // LDA #$3F; STA $2006
    0xa9, 0x00, 0x8d, 0x06, 0x20, // LDA #$00; STA $2006
    0xa9, colour, 0x8d, 0x07, 0x20, // LDA #colour; STA $2007
    0xa9, 0x00, 0x8d, 0x06, 0x20, 0x8d, 0x06, 0x20, // LDA #0; STA $2006; STA $2006
  ];
  const loop = 0xc000 + code.length;
  code.push(0x4c, loop & 0xff, loop >> 8); // loop: JMP loop
  prg.set(code, 0);
  prg.set([0x40], 0x3ff0); // RTI, for NMI and IRQ
  prg.set([0xf0, 0xff, 0x00, 0xc0, 0xf0, 0xff], 0x3ffa); // NMI $FFF0, RESET $C000, IRQ $FFF0
  return Buffer.concat([Buffer.from(header), Buffer.from(prg)]);
}

export async function checkNes({ browser, watch, problems, origin }) {
  const expected = JSON.parse(fs.readFileSync(EXPECTED, 'utf8'));
  const page = await browser.newPage();
  await watch(page, 'nes: ');
  const fetched = [];
  const requested = [];
  page.on('request', (r) => requested.push(new URL(r.url()).pathname));
  page.on('response', async (r) => {
    const url = new URL(r.url());
    if (url.pathname.startsWith('/machines/nes/') && url.pathname !== '/machines/nes/') fetched.push({ path: url.pathname, bytes: (await r.body().catch(() => Buffer.alloc(0))).length });
  });

  await page.goto(`${origin}/machines/nes/`);
  const panel = page.locator('[data-nes]');
  await page.waitForFunction(() => document.querySelector('[data-nes]')?.dataset.state === 'ready', null, { timeout: TIMEOUT }).catch(() => {});
  await page.waitForTimeout(1500);
  const startText = await page.locator('[data-nes-start]').innerText();
  console.log(`nes: page opened, state "${await panel.getAttribute('data-state')}", status "${await page.locator('[data-nes-status]').innerText()}", button "${startText}"`);
  if ((await panel.getAttribute('data-state')) !== 'ready') problems.push('nes: the page is not ready to start');
  if (fetched.length > 0) problems.push(`nes: the machine was fetched before Start was pressed: ${fetched.map((f) => f.path).join(', ')}`);
  const ok = page.locator('[data-analytics-on]');
  if (await ok.isVisible().catch(() => false)) await ok.click();

  // A file dropped before Start: refused, and the page stays.
  const dropped = await page.evaluate(() => {
    const data = new DataTransfer();
    data.items.add(new File([new Uint8Array(16)], 'early.nes'));
    const event = new DragEvent('drop', { dataTransfer: data, cancelable: true, bubbles: true });
    document.body.dispatchEvent(event);
    return { prevented: event.defaultPrevented, status: document.querySelector('[data-nes-status]').textContent };
  });
  console.log(`nes: a file dropped before Start: default prevented ${dropped.prevented}, status "${dropped.status}"`);
  if (!dropped.prevented || !/Press Start first/.test(dropped.status)) problems.push(`nes: a file dropped before Start gave ${JSON.stringify(dropped)}`);

  // Start: the download, the bundled game, and the machine running.
  const t0 = Date.now();
  await page.locator('[data-nes-start]').click();
  await page.waitForFunction(() => ['running', 'failed', 'waiting'].includes(document.querySelector('[data-nes]')?.dataset.state), null, { timeout: TIMEOUT });
  const state = await panel.getAttribute('data-state');
  console.log(`nes: ${state} in ${Date.now() - t0} ms: "${await page.locator('[data-nes-status]').innerText()}"`);
  if (state !== 'running') throw new Error(`nes: the machine did not start with its game (state ${state})`);
  const bytes = fetched.reduce((n, f) => n + f.bytes, 0);
  console.log(`nes: Start fetched ${fetched.length} files, ${bytes} bytes; the button said "${startText}"`);
  const cartridge = await panel.getAttribute('data-cartridge');
  const line = () => page.locator('[data-nes-region-line]').innerText();
  console.log(`nes: cartridge "${cartridge}"; "${await page.locator('[data-nes-cartridge]').innerText()}"; region ${await panel.getAttribute('data-region')}: "${await line()}"`);
  if (cartridge !== 'Lan Master') problems.push(`nes: Start put in "${cartridge}", not Lan Master`);

  // The canvas: its bytes' SHA-256, how many colours, and the size it is shown at.
  const canvas = () => page.evaluate(async () => {
    const c = document.querySelector('[data-nes-canvas]');
    const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
    const hash = [...new Uint8Array(await crypto.subtle.digest('SHA-256', d))].map((b) => b.toString(16).padStart(2, '0')).join('');
    const colours = new Set();
    for (let i = 0; i < d.length; i += 4) colours.add((d[i] << 16) | (d[i + 1] << 8) | d[i + 2]);
    return { hash, colours: colours.size, pixels: Array.from(d), shown: `${c.clientWidth}x${c.clientHeight}`, frames: Number(document.querySelector('[data-nes]').dataset.frames) };
  });
  const differing = (a, b) => {
    let n = 0;
    for (let i = 0; i < a.length; i += 4) if (a[i] !== b[i] || a[i + 1] !== b[i + 1] || a[i + 2] !== b[i + 2]) n++;
    return n;
  };

  // Each region, from power on to the recorded frame, through the page's test hook.
  // The canvas is cleared first, to transparent black, which the machine's
  // picture never is (every pixel it paints is opaque), so a stepTo that runs
  // but does not paint leaves a blank canvas and fails, rather than passing on
  // the last frame painted (the title is nearly still, and the two regions'
  // recorded hashes are the same).
  const frameIn = async (region) => {
    const n = await page.evaluate((frames) => {
      const c = document.querySelector('[data-nes-canvas]');
      c.getContext('2d').clearRect(0, 0, c.width, c.height);
      return document.querySelector('[data-nes]').nes.stepTo(frames);
    }, expected.frames);
    const got = await canvas();
    const want = expected.hashes[region];
    console.log(`nes: ${region}, ${n} frames from power on: canvas ${got.shown}, ${got.colours} colours, SHA-256 ${got.hash} ${got.hash === want ? 'matches' : 'DOES NOT MATCH'} the recorded ${want}`);
    if (n !== expected.frames) problems.push(`nes: ${region}: stepTo ran ${n} frames, not ${expected.frames}`);
    if (got.colours < 2) problems.push(`nes: ${region}: the canvas is blank at frame ${n}`);
    if (got.hash !== want) problems.push(`nes: ${region}: the frame on the canvas is not the recorded one`);
    return got;
  };
  const region = await page.evaluate(() => document.querySelector('[data-nes]').nes.region());
  const first = await frameIn(region);
  const [w, h] = first.shown.split('x').map(Number);
  console.log(`nes: shown at ${(w / h).toFixed(3)}:1 in ${region}`);

  const choose = async (value) => {
    const before = await line();
    await page.locator(`[data-nes-region][value="${value}"]`).check();
    await page.waitForFunction((v) => document.querySelector('[data-nes]').dataset.region === v, value, { timeout: 10_000 }).catch(() => {});
    const after = await line();
    console.log(`nes: region control to ${value}: "${after}"`);
    if (after === before || !after.startsWith('The region changed, so the machine restarted.') || !after.includes(`Running as ${value}`)) problems.push(`nes: switching to ${value}, the line went from "${before}" to "${after}"`);
  };
  const other = region === 'NTSC' ? 'PAL' : 'NTSC';
  await choose(other);
  await frameIn(other);
  await choose(region);
  const title = await frameIn(region);

  // Let the loop run again, then press Start through the page's own keyboard handling.
  await page.evaluate(() => document.querySelector('[data-nes]').nes.play());
  await page.locator('[data-nes-screen]').focus();
  await page.waitForTimeout(1500);
  const waiting = await canvas();
  await page.keyboard.down('Enter');
  await page.waitForTimeout(150);
  await page.keyboard.up('Enter');
  await page.waitForTimeout(2000);
  const pressed = await canvas();
  const byItself = differing(title.pixels, waiting.pixels);
  const byKey = differing(waiting.pixels, pressed.pixels);
  console.log(`nes: frames ${waiting.frames} then ${pressed.frames}; pixels changed while waiting ${byItself}, after Start on the keyboard ${byKey} of ${256 * 240}`);
  if (!(pressed.frames > waiting.frames + 30)) problems.push(`nes: the picture is not being redrawn (frames ${waiting.frames} then ${pressed.frames})`);
  if (!(byKey > 5000 && byKey > 4 * byItself)) problems.push(`nes: pressing Start on the keyboard did not change the picture (${byKey} pixels, ${byItself} by itself)`);

  // The counters the board model reads: four numbers from the machine itself, the PPU's moving
  // while the game runs, and the page says it runs.
  const counts = await page.evaluate(async () => {
    const nes = document.querySelector('[data-nes]').nes;
    const first = nes.accessCounts();
    await new Promise((resolve) => setTimeout(resolve, 500));
    const second = nes.accessCounts();
    // JSExport hands an int[] over as an Int32Array, a copy: signed 32-bit, as the counters wrap.
    return { kind: second?.constructor?.name, first: Array.from(first ?? []), second: Array.from(second ?? []), running: nes.running(), region: nes.region() };
  });
  const ppuMoved = (counts.second[0] - counts.first[0]) | 0;
  console.log(`nes: accessCounts, an ${counts.kind}, ${JSON.stringify(counts.first)} then ${JSON.stringify(counts.second)} half a second later; running() ${counts.running}, region() ${counts.region}`);
  if (counts.kind !== 'Int32Array' || counts.second.length !== 4) problems.push(`nes: accessCounts() gave an ${counts.kind} of ${JSON.stringify(counts.second)}, not four signed 32-bit counters`);
  if (!(ppuMoved > 0)) problems.push('nes: the PPU\'s access count did not move while the game ran');
  if (counts.running !== true) problems.push('nes: running() is not true while the machine runs');

  // The headroom line, once a second, from a fresh second after play().
  await page.waitForTimeout(2500);
  const speed = await page.locator('[data-nes-speed]').innerText();
  console.log(`nes: speed line "${speed}"; running ${await panel.getAttribute('data-actual-mhz')} MHz, capacity ${await panel.getAttribute('data-capacity-mhz')} MHz`);
  if (!/^Running at (the NES's own [\d.]+ MHz|[\d.]+ MHz, slower than the NES's [\d.]+ MHz)/.test(speed)) problems.push(`nes: the headroom line is "${speed}"`);

  // Sound on, then off: headless Chrome may play nothing, but nothing may fail.
  const sound = page.locator('[data-nes-sound]');
  await sound.click();
  await page.waitForFunction(() => ['on', 'failed'].includes(document.querySelector('[data-nes]').dataset.sound), null, { timeout: 10_000 }).catch(() => {});
  const on = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-nes-sound-status]').innerText() };
  await page.waitForTimeout(500);
  await sound.click();
  await page.waitForFunction(() => document.querySelector('[data-nes]').dataset.sound === 'off', null, { timeout: 10_000 }).catch(() => {});
  const off = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-nes-sound-status]').innerText() };
  console.log(`nes: sound on: ${JSON.stringify(on)}; off again: ${JSON.stringify(off)}`);
  if (on.state !== 'on' || on.button !== 'Turn sound off' || on.line !== 'Sound is on.') problems.push(`nes: turning the sound on gave ${JSON.stringify(on)}`);
  if (off.state !== 'off' || off.button !== 'Turn sound on' || off.line !== 'Sound is off.') problems.push(`nes: turning the sound off gave ${JSON.stringify(off)}`);

  await checkBoardModel({ page, problems, requested, choose, region, other });

  // The picker: a test cartridge built here, and the screen turns its one colour.
  await page.locator('[data-nes-file]').setInputFiles({ name: 'test.nes', mimeType: 'application/octet-stream', buffer: testCartridge() });
  await page.waitForFunction(() => document.querySelector('[data-nes]').dataset.cartridge === 'test.nes', null, { timeout: 10_000 }).catch(() => {});
  await page.waitForTimeout(1000);
  const loaded = await canvas();
  console.log(`nes: test cartridge: "${await page.locator('[data-nes-cartridge]').innerText()}"; "${await line()}"; ${loaded.colours} colour(s) on the canvas`);
  if ((await panel.getAttribute('data-cartridge')) !== 'test.nes') problems.push('nes: the test cartridge did not go in through the picker');
  if (loaded.colours !== 1 || loaded.hash === pressed.hash) problems.push(`nes: the test cartridge did not turn the screen one colour (${loaded.colours} colours)`);

  // Files the machine cannot take: each refused with a plain sentence, and the machine carries on.
  const refuse = async (file, why) => {
    const framesBefore = Number(await panel.getAttribute('data-frames'));
    await page.locator('[data-nes-file]').setInputFiles(file);
    await page.waitForFunction((name) => document.querySelector('[data-nes-cartridge]').textContent.startsWith(name), file.name, { timeout: 10_000 }).catch(() => {});
    await page.waitForTimeout(1000);
    const said = await page.locator('[data-nes-cartridge]').innerText();
    const still = await panel.getAttribute('data-cartridge');
    const framesAfter = Number(await panel.getAttribute('data-frames'));
    console.log(`nes: ${file.name}, ${file.buffer.length} bytes: "${said}"; the cartridge is still ${still}; frames ${framesBefore} then ${framesAfter}`);
    if (!why.test(said) || !said.endsWith('The machine carries on as it was.')) problems.push(`nes: ${file.name} gave "${said}"`);
    if (still !== 'test.nes' || !(framesAfter > framesBefore)) problems.push(`nes: after ${file.name} the machine did not carry on as it was`);
  };
  await refuse({ name: 'notes.nes', mimeType: 'text/plain', buffer: Buffer.from('These are notes, not a cartridge.\n') }, /^notes\.nes was not loaded\. .+/);
  const limit = await page.evaluate(() => document.querySelector('[data-nes]').nes.host.MaxRomBytes());
  await refuse({ name: 'huge.nes', mimeType: 'application/octet-stream', buffer: Buffer.alloc(limit + 1) }, /^huge\.nes is [\d,]+ bytes, more than the [\d,]+ bytes of the largest file this machine loads, so it was not read\./);

  // Every image on the page loads, from this site.
  const images = await page.evaluate(async () => {
    const all = [...document.images];
    for (const i of all) { i.loading = 'eager'; i.scrollIntoView(); }
    await new Promise((r) => setTimeout(r, 1500));
    return all.map((i) => ({ src: new URL(i.currentSrc || i.src).pathname, ok: i.complete && i.naturalWidth > 0, local: new URL(i.currentSrc || i.src).origin === location.origin }));
  });
  console.log(`nes: images ${images.map((i) => `${i.src} ${i.ok ? 'loaded' : 'NOT LOADED'}`).join('; ')}`);
  for (const i of images) if (!i.ok || !i.local) problems.push(`nes: the image ${i.src} did not load from this site`);
  console.log(`nes: the recorded frames are from ${path.relative(path.resolve(here, '..', '..'), EXPECTED)}, sha256 of the file ${crypto.createHash('sha256').update(fs.readFileSync(EXPECTED)).digest('hex')}`);
  await page.close();
}

/** Whether a board point in mm is inside the board's measured outline. */
function onBoard(x, y) {
  const poly = BOARD.outline;
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [x1, y1] = poly[j];
    const [x2, y2] = poly[i];
    if ((y1 > y) !== (y2 > y) && x < ((x2 - x1) * (y - y1)) / (y2 - y1) + x1) inside = !inside;
  }
  return inside;
}

/**
 * The NES board's 3D model, on the page with the game running (plan task 7,
 * step 5). `choose` switches the page's region control and checks its line.
 */
async function checkBoardModel({ page, problems, requested, choose, region, other }) {
  const model = page.locator('[data-model]');
  const stage = page.locator('[data-model-stage]');
  const status = () => page.locator('[data-model-status]').innerText();
  const models = () => requested.filter((r) => r.startsWith('/models/'));
  const shot = async () => page.screenshot({ clip: await page.locator('[data-model-canvas]').boundingBox(), timeout: TIMEOUT });
  const settle = async () => {
    await page.waitForFunction(() => !document.querySelector('[data-model]').modelView().moving, null, { timeout: 30_000 }).catch(() => {});
    await page.waitForTimeout(300);
    return model.evaluate((m) => m.modelView());
  };

  // Nothing of the model until the visitor reaches it; then its bundle and its map.
  const early = models();
  console.log(`nes: model files requested before scrolling to the model: ${early.length === 0 ? 'none' : early.join(', ')}`);
  if (early.length > 0) problems.push(`nes: the board model was fetched before the visitor reached it: ${early.join(', ')}`);
  const t0 = Date.now();
  await stage.scrollIntoViewIfNeeded();
  await page.waitForFunction(() => ['running', 'no-webgl', 'failed'].includes(document.querySelector('[data-model]')?.dataset.state), null, { timeout: TIMEOUT });
  const state = await model.getAttribute('data-state');
  const loaded = models();
  console.log(`nes: model ${state} in ${Date.now() - t0} ms after scrolling to it, fetched ${loaded.join(', ')}; status "${await status()}"`);
  if (state !== 'running') throw new Error(`nes: the board model did not start (state ${state})`);
  for (const f of ['/models/nes-famicom-board.js', TRACKS.src]) if (!loaded.includes(f)) problems.push(`nes: scrolling to the model did not fetch ${f}`);
  await page.waitForFunction(() => document.querySelector('[data-model]').modelLayers?.().tracksLoaded !== undefined, null, { timeout: TIMEOUT });

  // The canvas draws the board: the share of its pixels that are not the black canvas.
  await settle();
  {
    const { data, info } = await sharp(await shot()).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    let lit = 0;
    for (let i = 0; i < data.length; i += info.channels) if (data[i] + data[i + 1] + data[i + 2] > 30) lit++;
    const share = lit / (info.width * info.height);
    console.log(`nes: model canvas ${info.width}x${info.height}, ${(share * 100).toFixed(1)}% of its pixels drawn; region ${await model.getAttribute('data-model-region')}, tracks ${await model.getAttribute('data-model-tracks')}`);
    if (share < 0.05) problems.push(`nes: the board model's canvas is blank (${(share * 100).toFixed(1)}% drawn)`);
  }

  // Pointing at the PPU names it on the status line and lights its row.
  const ppuAt = await model.evaluate((m) => m.modelChipPoint('U5'));
  await page.mouse.move(ppuAt.x, ppuAt.y);
  await page.waitForFunction(() => document.querySelector('[data-legend-ref="U5"]')?.hasAttribute('data-pointed'), null, { timeout: 10_000 }).catch(() => {});
  const named = await status();
  const pointedRows = await page.$$eval('[data-legend-ref][data-pointed]', (rows) => rows.map((r) => r.dataset.legendRef));
  console.log(`nes: pointing at U5 at ${Math.round(ppuAt.x)},${Math.round(ppuAt.y)}: status "${named}"; rows lit ${pointedRows.join(', ') || 'none'}`);
  if (!named.startsWith('U5, the picture processing unit') || pointedRows.join() !== 'U5') problems.push(`nes: pointing at the PPU did not name it and light its row ("${named}", rows ${pointedRows.join(', ')})`);
  await page.mouse.move(5, 5);

  // Marks and rates: two seconds, looked at every 50 ms.
  const rateOf = (chip) => page.locator(`[data-legend-rate="${chip}"]`).innerText().then((t) => Number(t.replace(/,/g, '')));
  const look = async (ms) => {
    const seen = new Set();
    const rates = { ppu: 0, pad1: 0 };
    let accessedRows = new Set();
    for (const end = Date.now() + ms; Date.now() < end;) {
      for (const ref of ((await model.getAttribute('data-model-accessed')) ?? '').split(' ').filter(Boolean)) seen.add(ref);
      for (const r of await page.$$eval('[data-legend-ref][data-accessed]', (rows) => rows.map((x) => x.dataset.legendRef))) accessedRows.add(r);
      for (const chip of Object.keys(rates)) rates[chip] = Math.max(rates[chip], (await rateOf(chip)) || 0);
      await page.waitForTimeout(50);
    }
    return { seen: [...seen].sort(), rows: [...accessedRows].sort(), rates };
  };
  const ntsc = await look(2000);
  console.log(`nes: in 2 s on ${region}, chips marked ${ntsc.seen.join(' ') || 'none'}, legend rows marked ${ntsc.rows.join(' ') || 'none'}; highest legend rates: U5 ${ntsc.rates.ppu} and U7 ${ntsc.rates.pad1} a second`);
  for (const ref of ['U5', 'U7']) {
    if (!ntsc.seen.includes(ref)) problems.push(`nes: ${ref} was never marked in 2 s with the game running`);
    if (!ntsc.rows.includes(ref)) problems.push(`nes: ${ref}'s legend row was never marked in 2 s`);
  }
  if (!(ntsc.rates.ppu > 0 && ntsc.rates.pad1 > 0)) problems.push(`nes: the legend's rates for U5 and U7 did not rise above zero (${JSON.stringify(ntsc.rates)})`);

  // The region: the model follows it, with nothing more fetched, and the rates start again.
  const switchTo = async (value) => {
    const before = models().length;
    await page.evaluate(() => {
      const m = document.querySelector('[data-model]');
      window.nesRates = [];
      window.nesRatesWatch?.disconnect();
      window.nesRatesWatch = new MutationObserver(() => window.nesRates.push({ at: performance.now(), rates: m.dataset.modelRates }));
      window.nesRatesWatch.observe(m, { attributes: true, attributeFilter: ['data-model-rates'] });
      document.querySelector('[data-nes]').addEventListener('nes:region', () => window.nesRates.push({ at: performance.now(), event: 'nes:region' }), { once: true });
    });
    await choose(value);
    const want = value.toLowerCase();
    await page.waitForFunction((r) => document.querySelector('[data-model]').dataset.modelRegion === r, want, { timeout: 30_000 }).catch(() => {});
    await page.waitForTimeout(2000);
    const got = {
      region: await model.getAttribute('data-model-region'),
      label: await stage.getAttribute('aria-label'),
      describedby: await stage.getAttribute('aria-describedby'),
      ppu: (await page.locator('[data-legend-ref="U5"] td').first().innerText()).trim(),
      caption: await page.locator(`#model-about-${want}`).isVisible(),
      fetched: models().slice(before),
      sequence: await page.evaluate(() => window.nesRates),
    };
    const after = got.sequence.slice(got.sequence.findIndex((e) => e.event === 'nes:region') + 1).filter((e) => e.rates !== undefined);
    const firstEmpty = after.findIndex((e) => e.rates === 'null');
    const back = firstEmpty >= 0 ? after.slice(firstEmpty).find((e) => e.rates !== 'null') : null;
    console.log(`nes: region to ${value}: model region ${got.region}, its caption shown ${got.caption}, described by ${got.describedby}, named "${got.label}"; U5's part in the legend ${got.ppu}; fetched since ${got.fetched.length === 0 ? 'nothing' : got.fetched.join(', ')}; the rates after nes:region: ${after.map((e) => (e.rates === 'null' ? 'none' : JSON.parse(e.rates).ppu.perSecond)).join(', ')}`);
    if (got.region !== want || got.describedby !== `model-about-${want}` || !got.caption) problems.push(`nes: switching to ${value}, the model's region, caption or description did not follow (${JSON.stringify({ region: got.region, describedby: got.describedby, caption: got.caption })})`);
    if (!got.label?.includes(`${value} console`)) problems.push(`nes: switching to ${value}, the model's name did not follow ("${got.label}")`);
    if (got.fetched.length > 0) problems.push(`nes: switching to ${value} fetched ${got.fetched.join(', ')}`);
    if (firstEmpty < 0 || !back) problems.push(`nes: switching to ${value}, the rates did not show nothing for a sample and then come back`);
    return got;
  };
  const pal = await switchTo(other);
  const wantPpu = other === 'PAL' ? 'RP2C07-0' : 'RP2C02G-0';
  if (pal.ppu !== wantPpu) problems.push(`nes: with ${other} chosen, the PPU's part in the legend is ${pal.ppu}, not ${wantPpu}`);
  // On a busy machine the page runs the game in bursts, and a quarter second can pass with no frame run at all,
  // so the new machine is given up to five seconds to touch the PPU.
  const palLook = await look(5000);
  console.log(`nes: in 5 s on ${other}, chips marked ${palLook.seen.join(' ') || 'none'}; highest legend rates: U5 ${palLook.rates.ppu}, U7 ${palLook.rates.pad1}`);
  if (!palLook.seen.includes('U5') || !(palLook.rates.ppu > 0)) problems.push(`nes: on ${other} the PPU was not marked in 5 s, or its rate did not come back`);
  const back = await switchTo(region);
  if (back.ppu !== (region === 'PAL' ? 'RP2C07-0' : 'RP2C02G-0')) problems.push(`nes: back on ${region}, the PPU's part in the legend is ${back.ppu}`);

  // Show tracks and Show tracks only.
  await stage.scrollIntoViewIfNeeded();
  const tracks = page.locator('[data-toggle-tracks]');
  const only = page.locator('[data-toggle-tracks-only]');
  const layers = () => model.evaluate((m) => m.modelLayers());
  const l0 = await layers();
  await tracks.click();
  const l1 = await layers();
  const t1 = await model.getAttribute('data-model-tracks');
  await tracks.click();
  await only.click();
  await page.waitForFunction(() => document.querySelector('[data-model]').modelLayers().partsLevel === 0, null, { timeout: 10_000 }).catch(() => {});
  const l2 = await layers();
  const p2 = await model.getAttribute('data-model-parts');
  await only.click();
  await page.waitForFunction(() => document.querySelector('[data-model]').modelLayers().partsLevel === 1, null, { timeout: 10_000 }).catch(() => {});
  const l3 = await layers();
  console.log(`nes: tracks at the start ${JSON.stringify({ loaded: l0.tracksLoaded, tracks: l0.tracks, underside: l0.underside })}; Show tracks: ${t1}, ${JSON.stringify({ tracks: l1.tracks, underside: l1.underside })}; Show tracks only: parts ${p2}, ${JSON.stringify({ visible: l2.partsVisible, tracks: l2.tracks })}; and back: ${JSON.stringify({ visible: l3.partsVisible, level: l3.partsLevel, tracks: l3.tracks })}`);
  if (!l0.tracksLoaded || !l0.tracks || !l0.underside) problems.push('nes: the board model\'s tracks are not on both faces at the start');
  if (l1.tracks || l1.underside || t1 !== 'off') problems.push('nes: Show tracks did not take the tracks off');
  if (l2.partsVisible || !l2.tracks || p2 !== 'hidden') problems.push('nes: Show tracks only did not hide the parts and keep the tracks');
  if (!l3.partsVisible || l3.partsLevel !== 1 || !l3.tracks) problems.push('nes: the parts did not come back');

  // From below: where the map has the solder side's copper, the board shows it.
  await stage.focus();
  await page.keyboard.press('Home');
  await settle();
  for (let i = 0; i < 14; i++) await page.keyboard.press('ArrowDown');
  const below = await settle();
  const map = path.resolve(here, '..', 'dist', TRACKS.src.slice(1));
  const green = await sharp(map).extractChannel(1).raw().toBuffer();
  const { width: MW, height: MH } = await sharp(map).metadata();
  const mmOf = (u, v) => [TRACKS.originMm[0] + (u + 0.5) / TRACKS.pxPerMm, TRACKS.originMm[1] + (v + 0.5) / TRACKS.pxPerMm];
  // Copper solid for 0.4 mm round the point, bare board for a millimetre; on the board, and clear of the cartridge connector, which stands below the edge.
  const solid = (cu, cv, want) => {
    const r = Math.round(TRACKS.pxPerMm * (want ? 0.4 : 1));
    for (let v = cv - r; v <= cv + r; v++) for (let u = cu - r; u <= cu + r; u++) {
      if (u < 0 || v < 0 || u >= MW || v >= MH) return false;
      if (want ? green[v * MW + u] < 200 : green[v * MW + u] > 30) return false;
    }
    return true;
  };
  let seed = 7;
  const random = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  const pick = (want) => {
    const out = [];
    for (let tries = 0; out.length < 300 && tries < 300_000; tries++) {
      const u = Math.floor(random() * MW), v = Math.floor(random() * MH);
      const [x, y] = mmOf(u, v);
      if (y < 100 && onBoard(x, y) && onBoard(x + 1.5, y) && onBoard(x - 1.5, y) && onBoard(x, y + 1.5) && onBoard(x, y - 1.5) && solid(u, v, want)) out.push([x, y]);
    }
    return out;
  };
  const sample = async (pts) => {
    const box = await page.locator('[data-model-canvas]').boundingBox();
    const screen = await model.evaluate((m, list) => list.map(([x, y]) => m.modelBoardPoint(x, y, true)), pts);
    const { data, info } = await sharp(await shot()).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const sum = [0, 0, 0];
    let n = 0;
    for (const p of screen) {
      const x = Math.round(p.x - box.x), y = Math.round(p.y - box.y);
      if (x < 0 || y < 0 || x >= info.width || y >= info.height) continue;
      const i = (y * info.width + x) * info.channels;
      for (let c = 0; c < 3; c++) sum[c] += data[i + c];
      n++;
    }
    return { mean: sum.map((v) => v / Math.max(n, 1)), n };
  };
  const apart = (a, b) => Math.hypot(...a.mean.map((v, c) => v - b.mean[c]));
  const copperPts = pick(true), barePts = pick(false);
  const mirror = (pts) => pts.map(([x, y]) => [BOARD.width - x, y]);
  const onCopper = await sample(copperPts), onBare = await sample(barePts);
  const mCopper = await sample(mirror(copperPts)), mBare = await sample(mirror(barePts));
  const sep = apart(onCopper, onBare), control = apart(mCopper, mBare);
  const rgb = (m) => m.mean.map((v) => v.toFixed(0)).join(',');
  console.log(`nes: under the board, polar ${below.polar.toFixed(3)}: the map's solder-side copper at ${onCopper.n} points is ${rgb(onCopper)}, its bare board at ${onBare.n} points ${rgb(onBare)}, ${sep.toFixed(1)} apart; the same points mirrored side to side ${control.toFixed(1)} apart`);
  if (!(below.polar > Math.PI - 0.05)) problems.push(`nes: the camera did not get under the board (polar ${below.polar.toFixed(3)})`);
  if (!(onCopper.n > 200 && onBare.n > 200)) problems.push(`nes: too few underside points were on the screen (${onCopper.n} and ${onBare.n})`);
  if (!(sep > 25)) problems.push(`nes: from below, the solder side's copper does not show (${sep.toFixed(1)} apart)`);
  if (!(sep > 2 * control)) problems.push(`nes: the solder side's tracks are not where the map puts them: ${sep.toFixed(1)} apart, against ${control.toFixed(1)} mirrored`);
  await page.keyboard.press('Home');
  await settle();
  await page.evaluate(() => document.activeElement?.blur());
}
