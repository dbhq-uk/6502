// The Electron section of the browser check (scripts/browser-check.mjs calls it
// after the BBC Micro's, in the same browser, with the same watch for console
// errors, failed requests and CSP violations).
//
// The registry still says the Electron is planned, so the site in dist/ has no
// page for it. This builds the site again into a temporary folder with
// REGISTRY_PREVIEW set to tests/fixtures/electron-running.json, the registry as
// it will be once the machine is switched on, and serves that build with the
// site's own headers, as the BBC Micro's section did before it counted. Nothing
// it builds is kept.
//
// Then, on /machines/electron/: nothing of the machine is fetched before Start
// is pressed, and a tape dropped then is refused with a message rather than the
// browser leaving the page; Start downloads it and it boots to BASIC's prompt
// (read from screen memory through the page's test hook, and the canvas must be
// drawing, four by three); PRINT 6*7 and Return, pressed as real key events
// through the page's own keyboard handling, by where the keys are on the
// Electron (* is SHIFT and the key where a PC has '), shows 42 and lights more of
// the canvas; the headroom line appears; Tab moves focus off the machine; PRINT
// "A" tapped on the on-screen keys alone, SHIFT latched for each quote, prints A;
// the sound turns on and off without an error.
//
// Then the tape. Each damaged or unsupported file in BAD_TAPES, built here from
// bytes, goes in through the file input and is refused in the machine's reader's
// own words, with the machine still running. Then a program is typed, saved with
// the OS's own SAVE onto a blank tape, Save tape downloads it, the program is
// cleared with NEW, the downloaded file is dropped on the page, LOAD "" loads it
// back and RUN prints 42. How long the SAVE and the LOAD took, in real time and
// in tape seconds read from the machine, is printed. Break restarts the machine,
// and every image on the page loads from this site.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import zlib from 'node:zlib';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const site = path.resolve(here, '..');
const PREVIEW = 'tests/fixtures/electron-running.json';
const TIMEOUT = 120_000;

/** A UEF chunk: its id and length, little endian, then its bytes (tape.md s1). */
export const chunk = (id, ...data) => [id & 0xff, id >> 8, data.length & 0xff, (data.length >> 8) & 0xff, 0, 0, ...data];

/** A UEF file of version `major`.`minor` holding the chunks given: "UEF File!", a zero, the minor and the major version (tape.md s1). */
export const uef = ({ minor = 10, major = 0 } = {}, ...chunks) => Uint8Array.from([...'UEF File!'].map((c) => c.charCodeAt(0)).concat([0, minor, major], ...chunks));

/**
 * Damaged and unsupported tapes, each made here from bytes, with what the
 * Electron's reader (UefReader, in src/Dbhq.Machines.Electron/Tape/) says of it.
 * `says` is the reader's whole sentence, or `starts` its opening where the rest
 * is .NET's own words. `source` holds the parts of the sentence written in
 * UefReader.cs, which tests/electron.test.mjs finds there, so these cannot drift
 * from the reader; the browser check puts each into the real machine.
 */
export const BAD_TAPES = [
  { name: 'empty.uef', bytes: () => new Uint8Array(0), says: 'The file is empty.', source: ['The file is empty.'] },
  {
    // A good tape, gzipped, with the last four bytes of its trailer cut off.
    name: 'cut.uef',
    bytes: () => {
      const whole = zlib.gzipSync(uef({}, chunk(0x0110, 0xe8, 0x03), chunk(0x0100, 0x2a, 0x54, 0x45)));
      return Uint8Array.from(whole.subarray(0, whole.length - 4));
    },
    starts: 'The tape is cut short or damaged:',
    source: ['The tape is cut short or damaged: its gzip stream'],
  },
  {
    // Chunk &0117, the data encoding, set to 300 baud (&012C).
    name: 'slow.uef',
    bytes: () => uef({}, chunk(0x0117, 0x2c, 0x01)),
    says: "Chunk &0117 at offset 12 sets the data encoding to 300, and only 1200 baud is loaded (the Electron's ULA is a fixed 1200 baud receiver).",
    source: ['sets the data encoding to {baud}, and only 1200 baud is loaded (the Electron\'s ULA is a fixed 1200 baud receiver)', 'Chunk &{id:X4} at offset {at} {why}.'],
  },
  {
    name: 'version-1.uef',
    bytes: () => uef({ minor: 0, major: 1 }, chunk(0x0100, 0x01)),
    says: 'The UEF has major version 1: this reader loads version 0, whose chunks mean what the specification says (the minor version is 0).',
    source: ['The UEF has major version {major}: this reader loads version 0, whose chunks mean what the specification says (the minor version is {bytes[10]}).'],
  },
];

export async function checkElectron({ browser, watch, problems, serve }) {
  const out = fs.mkdtempSync(path.join(os.tmpdir(), 'electron-check-'));
  const built = spawnSync(process.execPath, [path.join('node_modules', 'astro', 'bin', 'astro.mjs'), 'build', '--outDir', out], { cwd: site, env: { ...process.env, REGISTRY_PREVIEW: PREVIEW }, encoding: 'utf8' });
  if (built.status !== 0) {
    problems.push(`electron: the preview build failed: ${built.stderr.slice(-500)}`);
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
  await watch(page, 'electron: ');
  const fetched = [];
  page.on('response', async (r) => {
    const url = new URL(r.url());
    if (url.pathname.startsWith('/machines/electron/') && url.pathname !== '/machines/electron/') fetched.push({ path: url.pathname, bytes: (await r.body().catch(() => Buffer.alloc(0))).length });
  });

  await page.goto(`${origin}/machines/electron/`);
  const panel = page.locator('[data-electron]');
  await page.waitForFunction(() => document.querySelector('[data-electron]')?.dataset.state === 'ready', null, { timeout: TIMEOUT }).catch(() => {});
  await page.waitForTimeout(1500);
  const startText = await page.locator('[data-electron-start]').innerText();
  console.log(`electron: page opened, state "${await panel.getAttribute('data-state')}", status "${await page.locator('[data-electron-status]').innerText()}", button "${startText}"`);
  if ((await panel.getAttribute('data-state')) !== 'ready') problems.push('electron: the page is not ready to start');
  if (fetched.length > 0) problems.push(`electron: the machine was fetched before Start was pressed: ${fetched.map((f) => f.path).join(', ')}`);
  const ok = page.locator('[data-analytics-on]');
  if (await ok.isVisible().catch(() => false)) await ok.click();

  // A tape dropped on the page, as a file from the visitor's own computer would be.
  const drop = (name, bytes) => page.evaluate(({ name: n, bytes: b }) => {
    const data = new DataTransfer();
    data.items.add(new File([new Uint8Array(b)], n));
    const event = new DragEvent('drop', { dataTransfer: data, cancelable: true, bubbles: true });
    document.body.dispatchEvent(event);
    return { prevented: event.defaultPrevented, status: document.querySelector('[data-electron-status]').textContent };
  }, { name, bytes: [...bytes] });

  // A tape dropped before Start: refused, and the page stays.
  const early = await drop('early.uef', uef({}, chunk(0x0100, 1)));
  console.log(`electron: a tape dropped before Start: default prevented ${early.prevented}, status "${early.status}"`);
  if (!early.prevented || !/Press Start first/.test(early.status)) problems.push(`electron: a tape dropped before Start gave ${JSON.stringify(early)}`);

  // Start: the download, then the boot to BASIC's prompt.
  const t0 = Date.now();
  await page.locator('[data-electron-start]').click();
  await page.waitForFunction(() => ['running', 'failed'].includes(document.querySelector('[data-electron]')?.dataset.state), null, { timeout: TIMEOUT });
  const state = await panel.getAttribute('data-state');
  console.log(`electron: ${state} in ${Date.now() - t0} ms: "${await page.locator('[data-electron-status]').innerText()}"`);
  if (state !== 'running') throw new Error(`electron: the machine did not start (state ${state})`);
  const bytes = fetched.reduce((n, f) => n + f.bytes, 0);
  console.log(`electron: Start fetched ${fetched.length} files, ${bytes} bytes; the button said "${startText}"`);

  // Mode 6 text, read from screen memory. A cell that matches no glyph (the cursor) is a space here.
  const rows = () => page.evaluate(() => Array.from({ length: 25 }, (_, r) => document.querySelector('[data-electron]').electron.screenRow(r).replace(/\uFFFD/g, ' ').trimEnd()));
  const prompt = (r) => r[r.findLastIndex((x) => x !== '')] === '>';
  const waitRows = async (test, label, limit = TIMEOUT) => {
    const until = Date.now() + limit;
    while (Date.now() < until) {
      if (test(await rows())) return true;
      await page.waitForTimeout(200);
    }
    problems.push(`electron: ${label}; the screen shows ${JSON.stringify(await rows())}`);
    return false;
  };
  const after = (r, line) => r[r.lastIndexOf(line) + 1]?.trim();
  const t1 = Date.now();
  await waitRows((r) => r[1] === 'Acorn Electron' && r[3] === 'BASIC' && prompt(r), 'it did not boot to the prompt');
  console.log(`electron: the prompt after ${Date.now() - t1} ms: ${JSON.stringify((await rows()).slice(0, 6))}`);

  // The canvas draws the picture: some of it is not black, frames keep coming, and it is shown four by three.
  const drawn = () => page.evaluate(() => {
    const c = document.querySelector('[data-electron-canvas]');
    const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
    let lit = 0;
    for (let i = 0; i < d.length; i += 4) if (d[i] + d[i + 1] + d[i + 2] > 0) lit++;
    return { lit, size: `${c.width}x${c.height}`, shown: `${c.clientWidth}x${c.clientHeight}`, frames: Number(document.querySelector('[data-electron]').dataset.frames) };
  });
  const a = await drawn();
  await page.waitForTimeout(1000);
  const b = await drawn();
  console.log(`electron: canvas ${a.size}, shown at ${a.shown}, ${a.lit} pixels lit; frames ${a.frames} then ${b.frames} a second later`);
  if (a.lit < 500) problems.push(`electron: the canvas is blank (${a.lit} pixels lit)`);
  if (!(b.frames > a.frames + 20)) problems.push(`electron: the picture is not being redrawn (frames ${a.frames} then ${b.frames})`);
  const [w, h] = a.shown.split('x').map(Number);
  if (Math.abs(w / h - 4 / 3) > 0.02) problems.push(`electron: the screen is shown ${a.shown}, not four by three`);
  const handover = await page.evaluate(() => {
    const host = document.querySelector('[data-electron]').electron.host;
    const t = performance.now();
    for (let i = 0; i < 50; i++) host.Picture();
    return (performance.now() - t) / 50;
  });
  console.log(`electron: handing over one picture, 640 by 256, takes ${handover.toFixed(3)} ms`);

  // The keyboard: Start gave the screen focus. PRINT 6*7, by the Electron's places.
  const focused = await page.evaluate(() => document.activeElement?.hasAttribute('data-electron-screen') ?? false);
  console.log(`electron: the screen has focus after Start: ${focused}; typing ${await panel.getAttribute('data-typing')}`);
  if (!focused) problems.push('electron: Start did not give the screen the keyboard');
  const type = async (keys) => { for (const k of keys) await page.keyboard.press(k); };
  const word = (text) => [...text].map((c) => (c === ' ' ? 'Space' : c === '"' ? 'Shift+Digit2' : c === '*' ? 'Shift+Quote' : /\d/.test(c) ? `Digit${c}` : `Key${c}`));
  const litBefore = (await drawn()).lit;
  await type([...word('PRINT 6*7'), 'Enter']);
  if (await waitRows((r) => r.includes('>PRINT 6*7') && after(r, '>PRINT 6*7') === '42', 'PRINT 6*7 did not show 42')) {
    const r = await rows();
    console.log(`electron: typed PRINT 6*7 and Return: the screen shows "${r[r.lastIndexOf('>PRINT 6*7')]}", then "${after(r, '>PRINT 6*7')}"`);
  }
  await page.waitForTimeout(300);
  const litAfter = (await drawn()).lit;
  console.log(`electron: lit pixels on the canvas ${litBefore} before typing, ${litAfter} after`);
  if (!(litAfter > litBefore)) problems.push(`electron: the canvas did not change when the text did (${litBefore} then ${litAfter} lit pixels)`);

  // The headroom line, once a second.
  await page.waitForFunction(() => document.querySelector('[data-electron-speed]')?.textContent !== '', null, { timeout: 10_000 }).catch(() => {});
  const speed = await page.locator('[data-electron-speed]').innerText();
  console.log(`electron: speed line "${speed}"; running ${await panel.getAttribute('data-actual-mhz')} MHz, capacity ${await panel.getAttribute('data-capacity-mhz')} MHz`);
  if (!/^Running at (the Electron's own 2 MHz|[\d.]+ MHz, slower than the Electron's 2 MHz)/.test(speed)) problems.push(`electron: the headroom line is "${speed}"`);

  // Tab moves on: the page never keeps the keyboard.
  await page.keyboard.press('Tab');
  const left = await page.evaluate(() => !(document.activeElement?.hasAttribute('data-electron-screen') ?? false));
  console.log(`electron: Tab moves focus off the screen: ${left}`);
  if (!left) problems.push('electron: Tab did not move focus off the screen');

  // The on-screen keys alone: PRINT "A", SHIFT latched for each quote (SHIFT and 2 is ").
  const tapKey = (key) => page.locator(`[data-electron-key="${key}"]`).click();
  for (const k of ['P', 'R', 'I', 'N', 'T', 'Space']) await tapKey(k);
  await tapKey('Shift');
  const latched = await page.locator('[data-electron-key="Shift"]').getAttribute('aria-pressed');
  await tapKey('D2');
  const unlatched = await page.locator('[data-electron-key="Shift"]').getAttribute('aria-pressed');
  await tapKey('A');
  await tapKey('Shift');
  await tapKey('D2');
  await tapKey('Return');
  console.log(`electron: on-screen SHIFT pressed "${latched}", after the next key "${unlatched}"`);
  if (latched !== 'true' || unlatched !== 'false') problems.push(`electron: the on-screen SHIFT did not latch for one key (${latched}, then ${unlatched})`);
  if (await waitRows((r) => r.includes('>PRINT "A"') && after(r, '>PRINT "A"') === 'A', 'PRINT "A" on the on-screen keys did not print A')) {
    console.log('electron: tapped PRINT "A" and RETURN on the on-screen keys: it printed A');
  }

  // Sound on, then off: headless Chrome may play nothing, but nothing may fail.
  const sound = page.locator('[data-electron-sound]');
  await sound.click();
  await page.waitForFunction(() => ['on', 'failed'].includes(document.querySelector('[data-electron]').dataset.sound), null, { timeout: 10_000 }).catch(() => {});
  const on = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-electron-sound-status]').innerText() };
  await page.waitForTimeout(500);
  await sound.click();
  await page.waitForFunction(() => document.querySelector('[data-electron]').dataset.sound === 'off', null, { timeout: 10_000 }).catch(() => {});
  const off = { state: await panel.getAttribute('data-sound'), button: await sound.innerText(), line: await page.locator('[data-electron-sound-status]').innerText() };
  console.log(`electron: sound on: ${JSON.stringify(on)}; off again: ${JSON.stringify(off)}`);
  if (on.state !== 'on' || on.button !== 'Turn sound off' || on.line !== 'Sound is on.') problems.push(`electron: turning the sound on gave ${JSON.stringify(on)}`);
  if (off.state !== 'off' || off.button !== 'Turn sound on' || off.line !== 'Sound is off.') problems.push(`electron: turning the sound off gave ${JSON.stringify(off)}`);

  // The damaged and unsupported tapes: each refused in the reader's own words, and the machine runs on.
  const tapeLine = () => page.locator('[data-electron-tape]').innerText();
  for (const bad of BAD_TAPES) {
    const before = Number(await panel.getAttribute('data-frames'));
    await page.locator('[data-electron-file]').setInputFiles({ name: bad.name, mimeType: 'application/octet-stream', buffer: Buffer.from(bad.bytes()) });
    await page.waitForFunction((n) => document.querySelector('[data-electron-tape]').textContent.startsWith(`${n} was not put in.`), bad.name, { timeout: 10_000 }).catch(() => {});
    const said = await tapeLine();
    const reason = said.slice(`${bad.name} was not put in. `.length);
    await page.waitForTimeout(300);
    const later = Number(await panel.getAttribute('data-frames'));
    console.log(`electron: ${bad.name} (${bad.bytes().length} bytes): "${said}"; tape in: "${await panel.getAttribute('data-tape')}"; frames ${before} then ${later}`);
    const right = bad.says ? reason === bad.says : reason.startsWith(bad.starts);
    if (!said.startsWith(`${bad.name} was not put in. `) || !right) problems.push(`electron: ${bad.name} was described as "${said}", not in the reader's words`);
    if ((await panel.getAttribute('data-tape')) !== '') problems.push(`electron: ${bad.name} left a tape in the recorder`);
    if (!(later > before)) problems.push(`electron: the machine stopped after ${bad.name}`);
  }

  // A program, saved with the OS's own SAVE onto a blank tape.
  await page.locator('[data-electron-screen]').click();
  await type([...word('10 PRINT 6*7'), 'Enter']);
  await page.locator('[data-electron-blank]').click();
  console.log(`electron: Blank tape: "${await tapeLine()}"`);
  await page.locator('[data-electron-screen]').click();
  await type([...word('SAVE "T"'), 'Enter']);
  if (await waitRows((r) => r.includes('RECORD then RETURN'), 'SAVE did not ask for RECORD then RETURN', 30_000)) {
    const t2 = Date.now();
    await type(['Enter']);
    const motor = () => panel.getAttribute('data-motor');
    await page.waitForFunction(() => document.querySelector('[data-electron]').dataset.motor === 'on', null, { timeout: 30_000 }).catch(() => {});
    const ran = await motor();
    await page.waitForFunction(() => document.querySelector('[data-electron]').dataset.motor === 'off', null, { timeout: TIMEOUT }).catch(() => {});
    await waitRows((r) => prompt(r), 'the prompt did not come back after SAVE');
    const motorLine = await page.locator('[data-electron-motor]').innerText();
    console.log(`electron: SAVE took ${((Date.now() - t2) / 1000).toFixed(1)} s of real time from RETURN; the motor was ${ran} then ${await motor()}: "${motorLine}"`);
    if (ran !== 'on') problems.push('electron: the motor never came on for SAVE');
  }
  const [download] = await Promise.all([page.waitForEvent('download', { timeout: 10_000 }), page.locator('[data-electron-save]').click()]);
  const saved = fs.readFileSync(await download.path());
  console.log(`electron: Save tape downloaded ${download.suggestedFilename()}, ${saved.length} bytes, gzip ${saved[0] === 0x1f && saved[1] === 0x8b}; "${await tapeLine()}"`);
  if (download.suggestedFilename() !== 'tape.uef' || saved.length < 20) problems.push('electron: Save tape did not download the recording');
  const plain = (() => { try { return zlib.gunzipSync(saved); } catch { return Buffer.alloc(0); } })();
  if (plain.subarray(0, 9).toString('latin1') !== 'UEF File!') problems.push('electron: the saved tape is not a gzipped UEF');

  // Cleared, then the saved file dropped on the page and loaded back.
  await page.locator('[data-electron-screen]').click();
  await type([...word('NEW'), 'Enter']);
  const put = await drop('saved.uef', saved);
  await page.waitForFunction(() => document.querySelector('[data-electron]').dataset.tape === 'saved.uef', null, { timeout: 10_000 }).catch(() => {});
  console.log(`electron: dropped the saved tape: prevented ${put.prevented}; "${await tapeLine()}"`);
  if ((await panel.getAttribute('data-tape')) !== 'saved.uef') problems.push('electron: the saved tape did not go in when dropped');
  await page.locator('[data-electron-screen]').click();
  const t3 = Date.now();
  await type([...word('LOAD ""'), 'Enter']);
  await page.waitForFunction(() => document.querySelector('[data-electron]').dataset.motor === 'on', null, { timeout: 30_000 }).catch(() => {});
  await page.waitForFunction(() => document.querySelector('[data-electron]').dataset.motor === 'off', null, { timeout: TIMEOUT }).catch(() => {});
  if (await waitRows((r) => r.lastIndexOf('>LOAD ""') >= 0 && r.slice(r.lastIndexOf('>LOAD ""')).includes('Loading') && prompt(r), 'LOAD "" did not load the saved tape')) {
    const r = await rows();
    console.log(`electron: LOAD took ${((Date.now() - t3) / 1000).toFixed(1)} s of real time; "${await page.locator('[data-electron-motor]').innerText()}"; the screen shows ${JSON.stringify(r.slice(r.lastIndexOf('>LOAD ""')).filter((x) => x !== ''))}`);
  }
  await type([...word('RUN'), 'Enter']);
  if (await waitRows((r) => r.includes('>RUN') && after(r, '>RUN') === '42', 'RUN after LOAD did not print 42')) {
    console.log('electron: RUN after the LOAD printed 42');
  }
  const lost = await page.evaluate(() => document.querySelector('[data-electron]').electron.host.LostBytes());
  console.log(`electron: bytes the OS did not read in time on that load: ${lost}`);
  if (lost !== 0) problems.push(`electron: the load lost ${lost} bytes`);

  // Break restarts the machine: the banner comes back and the screen is cleared.
  await page.locator('[data-electron-break]').click();
  await waitRows((r) => r.includes('Acorn Electron') && !r.includes('>RUN') && prompt(r), 'Break did not restart the machine');
  console.log(`electron: after Break: ${JSON.stringify((await rows()).filter((x) => x !== ''))}`);

  // Every image on the page loads, from this site.
  const images = await page.evaluate(async () => {
    const all = [...document.images];
    for (const i of all) { i.loading = 'eager'; i.scrollIntoView(); }
    await new Promise((r) => setTimeout(r, 1500));
    return all.map((i) => ({ src: new URL(i.currentSrc || i.src).pathname, ok: i.complete && i.naturalWidth > 0, local: new URL(i.currentSrc || i.src).origin === location.origin }));
  });
  console.log(`electron: images ${images.map((i) => `${i.src} ${i.ok ? 'loaded' : 'NOT LOADED'}`).join('; ')}`);
  for (const i of images) if (!i.ok || !i.local) problems.push(`electron: the image ${i.src} did not load from this site`);
  await page.close();
}
