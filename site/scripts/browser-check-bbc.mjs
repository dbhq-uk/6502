// The BBC Micro section of the browser check (scripts/browser-check.mjs calls
// it after the KIM-1's, in the same browser, with the same watch for console
// errors, failed requests and CSP violations).
//
// It runs on the KIM-1's server, which serves dist/ with the site's own headers.
//
// On /machines/bbc-micro/: nothing of the machine is fetched before
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
//
// Then the library of preset discs: Start fetched none of them; the default's
// credit shows under the list; with the page scrolled so the screen is out of
// view, Insert and run fetches it from this site, puts it in drive 0, starts it
// with SHIFT and BREAK and brings the screen into view, and the machine leaves BASIC's
// mode 7 for the disc's own screen, in colour; choosing another disc shows its
// credit instead; Insert in drive 0 puts a disc in without starting it, and
// *CAT lists it; Insert and run starts a second disc over the first and its
// title shows; and the visitor's own file still goes in through the file input
// after them, and *CAT lists it.
import fs from 'node:fs';
import sharp from 'sharp';
import { settled } from './browser-check-nes.mjs';
import { BOARD as BBC_BOARD, CONNECTORS as BBC_CONNECTORS, TRACKS as BBC_TRACKS, pagedRef } from '../src/models/bbc-micro-board-layout.mjs';

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

export async function checkBbcMicro({ browser, watch, problems, origin }) {
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

  // The library of preset discs. Start fetched none of them.
  const discsAtStart = fetched.filter((f) => f.path.includes('/discs/')).map((f) => f.path);
  if (discsAtStart.length > 0) problems.push(`bbc: a preset disc was fetched before it was inserted: ${discsAtStart.join(', ')}`);
  const list = page.locator('[data-bbc-preset]');
  const credit = async (slug) => ({ shown: await page.locator(`[data-bbc-preset-disc="${slug}"]`).isVisible(), text: await page.locator(`[data-bbc-preset-disc="${slug}"]`).innerText() });
  const first = await list.inputValue();
  const firstCredit = await credit(first);
  console.log(`bbc: the library offers ${await list.locator('option').count()} discs; chosen "${first}", its credit ${firstCredit.shown ? 'shown' : 'NOT SHOWN'}: ${JSON.stringify(firstCredit.text)}`);
  if (!firstCredit.shown) problems.push(`bbc: the chosen disc's credit is not shown`);
  const colours = () => page.evaluate(() => {
    const c = document.querySelector('[data-bbc-canvas]');
    const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
    const seen = new Set();
    for (let i = 0; i < d.length; i += 4) seen.add((d[i] << 16) | (d[i + 1] << 8) | d[i + 2]);
    return seen.size;
  });
  const mode = () => page.evaluate(() => document.querySelector('[data-bbc]').bbc.host.Peek(0x0355));
  const waitFor = async (test, label, limit = TIMEOUT) => {
    const until = Date.now() + limit;
    while (Date.now() < until) {
      if (await test()) return true;
      await page.waitForTimeout(250);
    }
    problems.push(`bbc: ${label}`);
    return false;
  };
  const inDrive = (name) => page.waitForFunction((n) => document.querySelector('[data-bbc]').dataset.disc === n, name, { timeout: 30_000 }).then(() => true, () => false);

  // The page scrolled down to the disc drive, so the screen is out of view above: Insert and
  // run must bring it back, or the visitor types into a game they cannot see.
  const canvasBox = () => page.evaluate(() => {
    const r = document.querySelector('[data-bbc-canvas]').getBoundingClientRect();
    return { top: Math.round(r.top), bottom: Math.round(r.bottom), height: innerHeight };
  });
  await page.evaluate(() => {
    const button = document.querySelector('[data-bbc-preset-run]');
    window.scrollTo({ top: button.getBoundingClientRect().top + scrollY - 120, behavior: 'instant' });
  });
  const away = await canvasBox();
  if (!(away.bottom < 0)) problems.push(`bbc: the check could not scroll the screen out of view (${JSON.stringify(away)})`);
  const t2 = Date.now();
  await page.locator('[data-bbc-preset-run]').click();
  let seen = await canvasBox();
  const inView = (b) => b.top >= 0 && b.bottom <= b.height;
  for (let i = 0; i < 20 && !inView(seen); i++) {
    await page.waitForTimeout(150);
    seen = await canvasBox();
  }
  console.log(`bbc: the screen before Insert and run ${JSON.stringify(away)}, after ${JSON.stringify(seen)}: ${inView(seen) ? 'in view' : 'NOT IN VIEW'}`);
  if (!inView(seen)) problems.push(`bbc: after Insert and run the screen is not in view (${JSON.stringify(seen)})`);
  if (!(await inDrive(`${first}.ssd`))) problems.push(`bbc: Insert and run did not put ${first} in drive 0`);
  console.log(`bbc: Insert and run: "${await page.locator('[data-bbc-drive]').innerText()}"; the screen has focus: ${await page.evaluate(() => document.activeElement?.hasAttribute('data-bbc-screen') ?? false)}`);
  if (await waitFor(async () => (await mode()) !== 7 && (await colours()) > 2, `${first} did not leave BASIC's mode 7 for a screen of its own`)) {
    console.log(`bbc: ${first} is running ${Date.now() - t2} ms after Insert and run: mode ${await mode()}, ${await colours()} colours on the canvas`);
  }
  const fromSite = fetched.filter((f) => f.path.includes('/discs/'));
  console.log(`bbc: discs fetched: ${fromSite.map((f) => `${f.path} ${f.bytes} bytes`).join(', ')}`);
  if (!fromSite.some((f) => f.path === `/machines/bbc-micro/discs/${first}.ssd` && f.bytes > 0)) problems.push(`bbc: ${first} was not fetched from this site`);

  // Another disc's credit, then Insert in drive 0, which starts nothing: *CAT lists it.
  await list.selectOption('onslaught');
  const before = await credit(first);
  const onslaught = await credit('onslaught');
  console.log(`bbc: chose onslaught: its credit ${onslaught.shown ? 'shown' : 'NOT SHOWN'}, ${first}'s ${before.shown ? 'STILL SHOWN' : 'hidden'}: ${JSON.stringify(onslaught.text)}`);
  if (!onslaught.shown || before.shown || !/Matt Godbolt/.test(onslaught.text) || !/MIT/.test(onslaught.text)) problems.push('bbc: choosing a disc did not show its credit alone');
  await page.locator('[data-bbc-break]').click();
  await waitRows((r) => r[r.findLastIndex((x) => x !== '')] === '>', 'Break did not return to the prompt after the disc');
  await page.locator('[data-bbc-preset-insert]').click();
  if (!(await inDrive('onslaught.ssd'))) problems.push('bbc: Insert in drive 0 did not put onslaught in');
  const insertedOnly = await page.locator('[data-bbc-drive]').innerText();
  console.log(`bbc: Insert in drive 0: "${insertedOnly}"`);
  if (!/type \*CAT/.test(insertedOnly)) problems.push(`bbc: Insert in drive 0 says "${insertedOnly}"`);
  await page.locator('[data-bbc-screen]').click();
  await type(['Shift+Quote', 'KeyC', 'KeyA', 'KeyT', 'Enter']);
  if (await waitRows((r) => r.lastIndexOf('>*CAT') >= 0 && r.slice(r.lastIndexOf('>*CAT')).some((x) => x.startsWith('ONSLAUGHT')), '*CAT did not list the inserted disc')) {
    const r = await rows();
    console.log(`bbc: *CAT lists ${JSON.stringify(r.slice(r.lastIndexOf('>*CAT')).filter((x) => x !== '').slice(0, 4))}`);
  }

  // A second disc started over the first: its title shows.
  await list.selectOption('caterpillar');
  await page.locator('[data-bbc-preset-run]').click();
  if (!(await inDrive('caterpillar.ssd'))) problems.push('bbc: Insert and run did not put caterpillar in');
  if (await waitRows((r) => r.some((x) => x.includes('Guide the caterpillar')), 'caterpillar did not start')) {
    console.log(`bbc: caterpillar started over onslaught: ${JSON.stringify((await rows()).filter((x) => x.trim() !== '').slice(2, 5))}`);
  }

  // The visitor's own file still goes in after a preset, and lists.
  await page.locator('[data-bbc-file]').setInputFiles({ name: 'test.ssd', mimeType: 'application/octet-stream', buffer: disc });
  if (!(await inDrive('test.ssd'))) problems.push('bbc: the file input did not insert a file after a preset');
  const own = await page.locator('[data-bbc-drive]').innerText();
  console.log(`bbc: own disc after the presets: "${own}"`);
  if (!own.startsWith('In drive 0: test.ssd, 40 tracks, single sided')) problems.push(`bbc: the drive says "${own}"`);
  await page.locator('[data-bbc-break]').click();
  await waitRows((r) => r[r.findLastIndex((x) => x !== '')] === '>', 'Break did not return to the prompt after caterpillar');
  await page.locator('[data-bbc-screen]').click();
  await type(['Shift+Quote', 'KeyC', 'KeyA', 'KeyT', 'Enter']);
  if (await waitRows((r) => r.lastIndexOf('>*CAT') >= 0 && r.slice(r.lastIndexOf('>*CAT')).some((x) => x.startsWith('TESTDISC')), '*CAT did not list the own disc after the presets')) {
    console.log('bbc: *CAT lists TESTDISC again');
  }

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

// ---- The BBC Micro's two 3D models (task 9b of its models plan) ----
//
// scripts/browser-check.mjs calls this after checkBbcMicro, in the same browser,
// on a page of its own, so that what the page fetches is counted from the start.
//
// Before Start: nothing under /models/ was fetched before the visitor scrolled
// to the models; scrolling fetches the outside's bundle and not the inside's; it
// draws, and its camera comes to rest; no light is lit; a click on its C key
// presses nothing, fetches nothing of the machine and says on the status line
// that the machine is not running. Then Start, and the boot to BASIC: CAPS LOCK
// lights on the model; the on-screen CAPS LOCK puts it out and lights it again;
// *MOTOR 1 typed lights the cassette motor's light and *MOTOR 0 puts it out;
// the on-screen A puts A down on the model; a real key event on the screen puts
// that key down while it is held; a click on the model's C types C on the
// screen and adds one press; from below (polar pi) the case is drawn.
// Then the tabs: Right on the Outside tab selects Inside, which only then
// fetches its bundle and its track map, runs and draws; pointing at IC78 names
// it and marks its row; with a disc of its own in the drive, *CAT marks IC78
// and its legend row, and *DUMP of a 2 KB file frames the DFS as the paged ROM,
// each looked at every 50 ms for up to ten seconds (a timeout for the disc to
// be read, not a judged figure); Show tracks
// and Show tracks only work; and from below, the board shows its solder side's
// copper where the map has it. Left returns to Outside with its camera where it
// was, Right to Inside with its own, and Tab goes from the selected tab into
// its stage and on out of the section. Last, in a second browser context that
// asks for reduced motion, both views load and switch with no error.
//
// The camera's rest is the NES check's settled(), imported, not copied.

const OUTSIDE = '#model-panel-outside';
const INSIDE = '#model-panel-inside';

/**
 * The test disc with its one file, $.HELLO, made 2 KB long (eight sectors from sector 2), so that *DUMP HELLO prints
 * for seconds with the DFS paged in.
 */
export function dumpDisc() {
  const disc = testDisc();
  disc[256 + 8 + 4] = 0x00; // length 2048: low byte
  disc[256 + 8 + 5] = 0x08; //              middle byte
  const text = 'HELLO FROM THE BROWSER CHECK. ';
  for (let i = 0; i < 2048; i++) disc[512 + i] = text.charCodeAt(i % text.length);
  return disc;
}

export async function checkBbcModels({ browser, watch, problems, origin }) {
  const say = (s) => console.log(`bbc models: ${s}`);
  const problem = (s) => problems.push(`bbc models: ${s}`);
  // settled() reports a camera that never came to rest as the NES's; here it is said as the BBC Micro's.
  const rest = async (p, root) => {
    const own = [];
    const view = await settled(p, root, own);
    for (const x of own) problem(x.replace(/^nes: /, ''));
    // What the camera was doing when it did not rest, for the next reading of the log (CI round 3, 13 October 2026).
    if (!view) say(`${root} did not rest: ${JSON.stringify(await p.locator(root).evaluate((m) => ({ view: m.modelView(), shown: (() => { const r = m.querySelector('[data-model-stage]').getBoundingClientRect(); return { top: Math.round(r.top), bottom: Math.round(r.bottom), height: innerHeight }; })() })))}`);
    return view;
  };
  const page = await browser.newPage();
  await watch(page, 'bbc models: ');
  const requested = [];
  page.on('request', (r) => requested.push(new URL(r.url()).pathname));
  const models = () => requested.filter((r) => r.startsWith('/models/'));
  const machineFiles = () => requested.filter((r) => r.startsWith('/machines/bbc-micro/') && r !== '/machines/bbc-micro/');

  await page.goto(`${origin}/machines/bbc-micro/`);
  await page.waitForFunction(() => document.querySelector('[data-bbc]')?.dataset.state === 'ready', null, { timeout: TIMEOUT }).catch(() => {});
  await page.waitForTimeout(1500);
  const ok = page.locator('[data-analytics-on]');
  if (await ok.isVisible().catch(() => false)) await ok.click();
  const outside = page.locator(OUTSIDE);
  const state = () => outside.evaluate((m) => ({ state: m.dataset.state, leds: m.dataset.modelLeds, down: m.dataset.modelDown ?? '', presses: Number(m.dataset.modelPresses), pressed: m.dataset.modelPressed ?? null, status: m.querySelector('[data-model-status]').textContent }));
  const shot = async (root) => {
    const { data, info } = await sharp(await page.screenshot({ clip: await page.locator(`${root} [data-model-canvas]`).boundingBox(), timeout: TIMEOUT })).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    return { data, info };
  };
  const drawn = async (root) => {
    const { data, info } = await shot(root);
    let lit = 0;
    for (let i = 0; i < data.length; i += info.channels) if (data[i] + data[i + 1] + data[i + 2] > 30) lit++;
    return { share: lit / (info.width * info.height), size: `${info.width}x${info.height}` };
  };
  const rows = () => page.evaluate(() => Array.from({ length: 25 }, (_, r) => document.querySelector('[data-bbc]').bbc.screenRow(r).trimEnd()));
  const lastRow = async () => { const r = await rows(); return r[r.findLastIndex((x) => x !== '')] ?? ''; };
  const until = async (test, ms = 10_000) => {
    const end = Date.now() + ms;
    while (Date.now() < end) {
      if (await test()) return true;
      await page.waitForTimeout(100);
    }
    return false;
  };
  const type = async (keys) => { for (const k of keys) await page.keyboard.press(k); };
  const leds = async () => (await state()).leds ?? '';
  const keyPoint = (name) => outside.evaluate((m, n) => m.modelKeyPoint(n), name);

  // ---- The outside, before Start ----
  const early = models();
  say(`model files requested before scrolling to the models: ${early.length === 0 ? 'none' : early.join(', ')}`);
  if (early.length > 0) problem(`a model was fetched before the visitor reached it: ${early.join(', ')}`);
  const t0 = Date.now();
  await page.locator(`${OUTSIDE} [data-model-stage]`).scrollIntoViewIfNeeded();
  await page.waitForFunction(() => ['running', 'no-webgl', 'failed'].includes(document.getElementById('model-panel-outside')?.dataset.state), null, { timeout: TIMEOUT });
  const before = await state();
  const loaded = models();
  say(`outside model ${before.state} in ${Date.now() - t0} ms after scrolling to it, fetched ${loaded.join(', ')}; lights ${before.leds}; status "${before.status}"`);
  if (before.state !== 'running') throw new Error(`bbc models: the outside model did not start (state ${before.state})`);
  if (!loaded.includes('/models/bbc-micro-case.js')) problem('scrolling to the models did not fetch the outside\'s bundle');
  if (loaded.some((f) => f.startsWith('/models/bbc-micro-board'))) problem(`scrolling to the models fetched the inside's files too: ${loaded.join(', ')}`);
  if (await page.locator(INSIDE).isVisible()) problem('the inside\'s panel is shown before its tab is chosen');
  await rest(page, OUTSIDE);
  const d0 = await drawn(OUTSIDE);
  say(`outside canvas ${d0.size}, ${(d0.share * 100).toFixed(1)}% drawn`);
  if (d0.share < 0.05) problem(`the outside model's canvas is blank (${(d0.share * 100).toFixed(1)}% drawn)`);
  if (before.leds !== 'none') problem(`before Start the model's lights are "${before.leds}", not none`);
  // A click on the model's C before Start presses nothing and says why.
  const fetchedBefore = machineFiles().length;
  const c0 = await keyPoint('C');
  await page.mouse.click(c0.x, c0.y);
  await page.waitForTimeout(500);
  const clicked = await state();
  const panelState = await page.locator('[data-bbc]').getAttribute('data-state');
  const fetchedSince = machineFiles().slice(fetchedBefore);
  say(`a click on the model's C at ${Math.round(c0.x)},${Math.round(c0.y)} before Start: the machine is ${panelState}, fetched ${fetchedSince.length === 0 ? 'nothing' : fetchedSince.join(', ')}; presses ${clicked.presses}; status "${clicked.status}"`);
  if (panelState !== 'ready' || fetchedSince.length > 0 || clicked.presses !== 0) problem(`a click on C before Start did something (machine ${panelState}, fetched ${fetchedSince.join(', ')}, presses ${clicked.presses})`);
  if (!/not running/.test(clicked.status)) problem(`a click on C before Start did not say the machine is not running ("${clicked.status}")`);

  // ---- Start, and the machine's lights and keys on the model ----
  await page.locator('[data-bbc-start]').click();
  await page.waitForFunction(() => ['running', 'failed'].includes(document.querySelector('[data-bbc]')?.dataset.state), null, { timeout: TIMEOUT });
  if ((await page.locator('[data-bbc]').getAttribute('data-state')) !== 'running') throw new Error('bbc models: the machine did not start');
  const booted = await until(async () => { const r = await rows(); return r.includes('BASIC') && (await lastRow()) === '>'; }, TIMEOUT);
  if (!booted) problem(`the machine did not boot to the prompt; the screen shows ${JSON.stringify(await rows())}`);
  const capsLit = await until(async () => (await leds()).includes('caps lock'));
  const started = await state();
  say(`after the boot: lights "${started.leds}"; status "${started.status}"`);
  if (!capsLit) problem(`after the boot the model's caps lock light is not lit ("${started.leds}")`);
  if (!/so is the machine/.test(started.status)) problem(`after Start the outside's status line says "${started.status}"`);
  const capsKey = page.locator('[data-bbc-key="CapsLock"]');
  await capsKey.click();
  const capsOut = await until(async () => !(await leds()).includes('caps lock'));
  const between = await leds();
  await capsKey.click();
  const capsBack = await until(async () => (await leds()).includes('caps lock'));
  say(`the on-screen CAPS LOCK: the lights "${between}", then again "${await leds()}"`);
  if (!capsOut || !capsBack) problem(`the on-screen CAPS LOCK did not put the caps lock light out and light it again (${capsOut}, ${capsBack})`);
  await page.locator('[data-bbc-screen]').click();
  await type(['Shift+Quote', 'KeyM', 'KeyO', 'KeyT', 'KeyO', 'KeyR', 'Space', 'Digit1', 'Enter']);
  const motorOn = await until(async () => (await leds()).includes('cassette motor'));
  const withMotor = await leds();
  await type(['Shift+Quote', 'KeyM', 'KeyO', 'KeyT', 'KeyO', 'KeyR', 'Space', 'Digit0', 'Enter']);
  const motorOff = await until(async () => !(await leds()).includes('cassette motor'));
  say(`*MOTOR 1: the lights "${withMotor}"; *MOTOR 0: "${await leds()}"`);
  if (!motorOn) problem(`*MOTOR 1 did not light the model's cassette motor light ("${withMotor}")`);
  if (!motorOff) problem('*MOTOR 0 did not put the cassette motor light out');

  // Every change of the keys down, as it happens.
  await outside.evaluate((m) => {
    window.bbcDown = [];
    new MutationObserver(() => window.bbcDown.push(m.dataset.modelDown)).observe(m, { attributes: true, attributeFilter: ['data-model-down'] });
  });
  const downs = () => page.evaluate(() => window.bbcDown.splice(0));
  const p0 = (await state()).presses;
  await page.locator('[data-bbc-key="A"]').click();
  await page.waitForTimeout(600);
  const tapped = await downs();
  const p1 = (await state()).presses;
  say(`the on-screen A: the keys down on the model went ${JSON.stringify(tapped)}; presses ${p0} then ${p1}`);
  if (!tapped.some((d) => d.split(' ').includes('A')) || tapped.at(-1) !== '') problem(`the on-screen A did not put A down on the model and let it go (${JSON.stringify(tapped)})`);
  if (p1 !== p0 + 1) problem(`the on-screen A added ${p1 - p0} presses, not one`);
  await page.locator('[data-bbc-screen]').focus();
  await page.keyboard.down('KeyZ');
  await page.waitForTimeout(300);
  const held = (await state()).down;
  await page.keyboard.up('KeyZ');
  await page.waitForTimeout(300);
  const let_ = (await state()).down;
  say(`a real Z held on the screen: the keys down "${held}"; let go: "${let_}"`);
  if (!held.split(' ').includes('Z') || let_.split(' ').includes('Z')) problem(`a real key event did not put Z down on the model while it was held ("${held}", then "${let_}")`);
  await page.keyboard.press('Enter');
  await until(async () => (await lastRow()) === '>');
  // A click on the model's C types C on the machine, once.
  await page.locator(`${OUTSIDE} [data-model-stage]`).scrollIntoViewIfNeeded();
  await rest(page, OUTSIDE);
  const p2 = (await state()).presses;
  const c1 = await keyPoint('C');
  await page.mouse.click(c1.x, c1.y);
  const typedC = await until(async () => (await lastRow()) === '>C', 5000);
  const afterC = await state();
  say(`a click on the model's C with the machine running: the screen's last line "${await lastRow()}"; presses ${p2} then ${afterC.presses}; status "${afterC.status}"`);
  if (!typedC || afterC.presses !== p2 + 1) problem(`a click on the model's C did not type C on the machine once (line "${await lastRow()}", presses ${p2} then ${afterC.presses})`);
  await page.locator('[data-bbc-screen]').focus();
  await page.keyboard.press('Enter');
  await until(async () => (await lastRow()) === '>');
  // From below: the camera goes under the case, and the underside is drawn. The stage is brought on screen first:
  // it moves its camera only while it is on screen.
  await page.locator(`${OUTSIDE} [data-model-stage]`).scrollIntoViewIfNeeded();
  await page.locator(`${OUTSIDE} [data-model-stage]`).focus();
  await page.keyboard.press('Home');
  await rest(page, OUTSIDE);
  for (let i = 0; i < 14; i++) await page.keyboard.press('ArrowDown');
  const below = await rest(page, OUTSIDE);
  if (below) {
    const d = await drawn(OUTSIDE);
    say(`under the case, polar ${below.polar.toFixed(3)}: ${(d.share * 100).toFixed(1)}% of the canvas drawn`);
    if (!(below.polar > Math.PI - 0.05)) problem(`the camera did not get under the case (polar ${below.polar.toFixed(3)})`);
    if (d.share < 0.05) problem(`from below, the case's canvas is blank (${(d.share * 100).toFixed(1)}%)`);
  }
  await page.keyboard.press('Home');
  const outsideView = await rest(page, OUTSIDE);

  // ---- The tabs: Right selects Inside, which only now fetches its files ----
  const board = () => requested.filter((r) => r.startsWith('/models/bbc-micro-board'));
  if (board().length > 0) problem(`the inside was fetched before its tab was chosen: ${board().join(', ')}`);
  await page.locator('#model-tab-outside').focus();
  await page.keyboard.press('ArrowRight');
  await page.waitForFunction(() => ['running', 'no-webgl', 'failed'].includes(document.getElementById('model-panel-inside')?.dataset.state), null, { timeout: TIMEOUT });
  const inside = page.locator(INSIDE);
  const insideState = await inside.getAttribute('data-state');
  const right = await page.evaluate(() => ({ focused: document.activeElement?.id, selected: [...document.querySelectorAll('[role="tab"]')].filter((t) => t.getAttribute('aria-selected') === 'true').map((t) => t.id).join(), outside: !document.getElementById('model-panel-outside').hidden, inside: !document.getElementById('model-panel-inside').hidden }));
  say(`Right on the Outside tab: focus ${right.focused}, selected ${right.selected}, outside shown ${right.outside}, inside shown ${right.inside}; the inside ${insideState}, fetched ${board().join(', ')}`);
  if (insideState !== 'running') throw new Error(`bbc models: the inside model did not start (state ${insideState})`);
  if (right.focused !== 'model-tab-inside' || right.selected !== 'model-tab-inside' || right.outside || !right.inside) problem(`Right on the Outside tab did not move to Inside, select it and show its panel alone (${JSON.stringify(right)})`);
  for (const f of ['/models/bbc-micro-board.js', BBC_TRACKS.src]) if (!board().includes(f)) problem(`choosing Inside did not fetch ${f}`);
  await page.waitForFunction(() => document.getElementById('model-panel-inside').modelLayers?.().tracksLoaded !== undefined, null, { timeout: TIMEOUT });
  await inside.locator('[data-model-stage]').scrollIntoViewIfNeeded();
  await rest(page, INSIDE);
  const d1 = await drawn(INSIDE);
  say(`inside canvas ${d1.size}, ${(d1.share * 100).toFixed(1)}% drawn; tracks ${await inside.getAttribute('data-model-tracks')}; status "${await inside.locator('[data-model-status]').innerText()}"`);
  if (d1.share < 0.05) problem(`the inside model's canvas is blank (${(d1.share * 100).toFixed(1)}% drawn)`);

  // Pointing at IC78 names it on the status line and marks its row.
  const at78 = await inside.evaluate((m) => m.modelChipPoint('IC78'));
  await page.mouse.move(at78.x, at78.y);
  await page.waitForFunction(() => document.querySelector('[data-legend-ref="IC78"]')?.hasAttribute('data-pointed'), null, { timeout: 10_000 }).catch(() => {});
  const named = await inside.locator('[data-model-status]').innerText();
  const pointedRows = await page.$$eval('[data-legend-ref][data-pointed]', (r) => r.map((x) => x.dataset.legendRef));
  say(`pointing at IC78 at ${Math.round(at78.x)},${Math.round(at78.y)}: status "${named}"; rows ${pointedRows.join(', ') || 'none'}`);
  if (!named.startsWith('IC78, the disc controller') || pointedRows.join() !== 'IC78') problem(`pointing at IC78 did not name it and mark its row alone ("${named}", rows ${pointedRows.join(', ')})`);
  await page.mouse.move(5, 5);

  // With a disc of its own in the drive: *CAT marks IC78 and its row, and *DUMP frames the DFS as the paged ROM, each
  // at least once. The DFS is the paged ROM only while it works, and the model reads the ROM latch every quarter
  // second: a *CAT is over in a fraction of a second, so the frame is looked for while *DUMP prints a file of 2 KB,
  // which keeps the DFS paged for seconds (CI round 3, 13 October 2026: three *CATs never showed it framed).
  const dfs = pagedRef(14);
  await page.locator('[data-bbc-file]').setInputFiles({ name: 'dump.ssd', mimeType: 'application/octet-stream', buffer: Buffer.from(dumpDisc()) });
  await page.waitForFunction(() => document.querySelector('[data-bbc]').dataset.disc === 'dump.ssd', null, { timeout: 10_000 }).catch(() => {});
  await inside.evaluate((m) => {
    window.bbcInside = { accessed: new Set(), paged: new Set(), rows: new Set() };
    const look = () => {
      for (const r of (m.dataset.modelAccessed ?? '').split(' ').filter(Boolean)) window.bbcInside.accessed.add(r);
      if (m.dataset.modelPaged) window.bbcInside.paged.add(m.dataset.modelPaged);
      for (const row of m.querySelectorAll('[data-legend-ref][data-accessed]')) window.bbcInside.rows.add(row.dataset.legendRef);
    };
    new MutationObserver(look).observe(m, { attributes: true, subtree: true, attributeFilter: ['data-model-accessed', 'data-model-paged', 'data-accessed'] });
  });
  const seen = () => page.evaluate(() => ({ accessed: [...window.bbcInside.accessed].sort(), paged: [...window.bbcInside.paged].sort(), rows: [...window.bbcInside.rows].sort() }));
  const LOOK_MS = 10_000;
  const lookFor = async (test) => {
    const t = Date.now();
    let marks = await seen();
    while (Date.now() - t < LOOK_MS && !test(marks)) {
      await page.waitForTimeout(50);
      marks = await seen();
    }
    return marks;
  };
  await page.locator('[data-bbc-screen]').focus();
  await type(['Shift+Quote', 'KeyC', 'KeyA', 'KeyT', 'Enter']);
  const cat = await lookFor((x) => x.accessed.includes('IC78') && x.rows.includes('IC78'));
  const rate = await page.locator('[data-legend-rate="fdc8271"]').innerText();
  say(`*CAT: chips marked ${cat.accessed.join(' ') || 'none'}; rows marked ${cat.rows.join(' ') || 'none'}; ROMs framed ${cat.paged.join(' ') || 'none'}; the 8271's rate then "${rate}"`);
  if (!cat.accessed.includes('IC78') || !cat.rows.includes('IC78')) problem(`*CAT never marked IC78, the 8271, or its row in ${LOOK_MS / 1000} s`);
  await until(async () => (await lastRow()) === '>');
  // The frames seen so far are forgotten, so that what is judged is what *DUMP shows.
  await page.evaluate(() => window.bbcInside.paged.clear());
  await type(['Shift+Quote', 'KeyD', 'KeyU', 'KeyM', 'KeyP', 'Space', 'KeyH', 'KeyE', 'KeyL', 'KeyL', 'KeyO', 'Enter']);
  const dump = await lookFor((x) => x.paged.includes(dfs));
  say(`*DUMP HELLO: ROMs framed ${dump.paged.join(' ') || 'none'} (the DFS is ${dfs}); the screen's last line "${await lastRow()}"`);
  if (!dump.paged.includes(dfs)) problem(`*DUMP never framed ${dfs}, the DFS, as the paged ROM in ${LOOK_MS / 1000} s`);
  if (!dump.paged.includes(pagedRef(15))) problem(`the paged ROM was never ${pagedRef(15)}, BASIC, while the machine ran`);
  await page.keyboard.press('Escape');
  await until(async () => (await lastRow()) === '>', 30_000);

  // Show tracks and Show tracks only.
  await inside.locator('[data-model-stage]').scrollIntoViewIfNeeded();
  const layers = () => inside.evaluate((m) => m.modelLayers());
  const tracks = inside.locator('[data-toggle-tracks]');
  const only = inside.locator('[data-toggle-tracks-only]');
  const l0 = await layers();
  await tracks.click();
  const l1 = await layers();
  const t1 = await inside.getAttribute('data-model-tracks');
  await tracks.click();
  await only.click();
  await page.waitForFunction(() => document.getElementById('model-panel-inside').modelLayers().partsLevel === 0, null, { timeout: 10_000 }).catch(() => {});
  const l2 = await layers();
  const p2s = await inside.getAttribute('data-model-parts');
  await only.click();
  await page.waitForFunction(() => document.getElementById('model-panel-inside').modelLayers().partsLevel === 1, null, { timeout: 10_000 }).catch(() => {});
  const l3 = await layers();
  say(`tracks at the start ${JSON.stringify({ loaded: l0.tracksLoaded, tracks: l0.tracks, underside: l0.underside })}; Show tracks: ${t1}, ${JSON.stringify({ tracks: l1.tracks, underside: l1.underside })}; Show tracks only: parts ${p2s}, ${JSON.stringify({ visible: l2.partsVisible, tracks: l2.tracks })}; and back: ${JSON.stringify({ visible: l3.partsVisible, level: l3.partsLevel, tracks: l3.tracks })}`);
  if (!l0.tracksLoaded || !l0.tracks || !l0.underside) problem('the board model\'s tracks are not on both faces at the start');
  if (l1.tracks || l1.underside || t1 !== 'off') problem('Show tracks did not take the tracks off');
  if (l2.partsVisible || !l2.tracks || p2s !== 'hidden') problem('Show tracks only did not hide the parts and keep the tracks');
  if (!l3.partsVisible || l3.partsLevel !== 1 || !l3.tracks) problem('the parts did not come back');

  // From below: where the map has the solder side's copper, the board shows it.
  await inside.locator('[data-model-stage]').focus();
  await page.keyboard.press('Home');
  await rest(page, INSIDE);
  for (let i = 0; i < 14; i++) await page.keyboard.press('ArrowDown');
  const under = await rest(page, INSIDE);
  if (under) {
    const map = new URL(`../dist${BBC_TRACKS.src}`, import.meta.url);
    const green = await sharp(map.pathname).extractChannel(1).raw().toBuffer();
    const { width: MW, height: MH } = await sharp(map.pathname).metadata();
    const mmOf = (u, v) => [BBC_TRACKS.originMm[0] + (u + 0.5) / BBC_TRACKS.pxPerMm, BBC_TRACKS.originMm[1] + (v + 0.5) / BBC_TRACKS.pxPerMm];
    const solid = (cu, cv, want) => {
      const r = Math.round(BBC_TRACKS.pxPerMm * (want ? 0.4 : 1));
      for (let v = cv - r; v <= cv + r; v++) for (let u = cu - r; u <= cu + r; u++) {
        if (u < 0 || v < 0 || u >= MW || v >= MH) return false;
        if (want ? green[v * MW + u] < 200 : green[v * MW + u] > 30) return false;
      }
      return true;
    };
    // Clear of the ribbon connectors that hang under the board, by 3 mm, and well inside the outline.
    const hanging = BBC_CONNECTORS.filter((c) => c.edge === 'underside').map((c) => ({ x: c.x, y: c.y, r: Math.max(c.l, c.w) / 2 + 3 }));
    const clear = (x, y) => hanging.every((h) => Math.abs(x - h.x) > h.r || Math.abs(y - h.y) > h.r);
    let seed = 7;
    const random = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
    const pick = (want) => {
      const out = [];
      for (let tries_ = 0; out.length < 300 && tries_ < 300_000; tries_++) {
        const u = Math.floor(random() * MW), v = Math.floor(random() * MH);
        const [x, y] = mmOf(u, v);
        if ([[0, 0], [1.5, 0], [-1.5, 0], [0, 1.5], [0, -1.5]].every(([a, b]) => onBoard(x + a, y + b)) && clear(x, y) && solid(u, v, want)) out.push([x, y]);
      }
      return out;
    };
    const sample = async (pts) => {
      const box = await page.locator(`${INSIDE} [data-model-canvas]`).boundingBox();
      const screen = await inside.evaluate((m, list) => list.map(([x, y]) => m.modelBoardPoint(x, y, true)), pts);
      const { data, info } = await shot(INSIDE);
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
    const mirror = (pts) => pts.map(([x, y]) => [BBC_BOARD.width - x, y]);
    const onCopper = await sample(copperPts), onBare = await sample(barePts);
    const mCopper = await sample(mirror(copperPts)), mBare = await sample(mirror(barePts));
    const sep = apart(onCopper, onBare), control = apart(mCopper, mBare);
    const rgb = (m) => m.mean.map((v) => v.toFixed(0)).join(',');
    say(`under the board, polar ${under.polar.toFixed(3)}: the map's solder-side copper at ${onCopper.n} points is ${rgb(onCopper)}, its bare board at ${onBare.n} points ${rgb(onBare)}, ${sep.toFixed(1)} apart; the same points mirrored side to side ${control.toFixed(1)} apart`);
    if (!(under.polar > Math.PI - 0.05)) problem(`the camera did not get under the board (polar ${under.polar.toFixed(3)})`);
    if (!(onCopper.n > 200 && onBare.n > 200)) problem(`too few underside points were on the screen (${onCopper.n} and ${onBare.n})`);
    if (!(sep > 25)) problem(`from below, the solder side's copper does not show (${sep.toFixed(1)} apart)`);
    if (!(sep > 2 * control)) problem(`the solder side's tracks are not where the map puts them: ${sep.toFixed(1)} apart, against ${control.toFixed(1)} mirrored`);
  }
  await page.keyboard.press('Home');

  // ---- The tabs from the keyboard: each view keeps its camera, and Tab leaves the section ----
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowUp');
  const insideView = await rest(page, INSIDE);
  await page.locator('#model-tab-inside').focus();
  await page.keyboard.press('ArrowLeft');
  await page.waitForTimeout(500);
  const left = await page.evaluate(() => ({ focused: document.activeElement?.id, selected: [...document.querySelectorAll('[role="tab"]')].filter((t) => t.getAttribute('aria-selected') === 'true').map((t) => t.id).join(), outside: !document.getElementById('model-panel-outside').hidden, inside: !document.getElementById('model-panel-inside').hidden }));
  const outsideAgain = await rest(page, OUTSIDE);
  await page.keyboard.press('ArrowRight');
  await page.waitForTimeout(500);
  const insideAgain = await rest(page, INSIDE);
  await page.keyboard.press('ArrowLeft');
  await rest(page, OUTSIDE);
  const same = (a, b) => Math.abs(a.polar - b.polar) + Math.abs(a.azimuth - b.azimuth) + Math.abs(a.distance - b.distance) < 1e-3;
  const compared = [outsideView, outsideAgain, insideView, insideAgain].every(Boolean);
  say(`Left from Inside: focus ${left.focused}, selected ${left.selected}, outside shown ${left.outside}, inside shown ${left.inside}; ${compared ? `the outside's camera kept ${same(outsideView, outsideAgain)}, the inside's ${same(insideView, insideAgain)}` : 'the cameras not compared: one never came to rest'}`);
  if (left.focused !== 'model-tab-outside' || left.selected !== 'model-tab-outside' || !left.outside || left.inside) problem(`Left on the Inside tab did not move to Outside and select it (${JSON.stringify(left)})`);
  if (compared && !same(outsideView, outsideAgain)) problem('Left did not return to Outside with its camera where it was');
  if (compared && !same(insideView, insideAgain)) problem('the inside did not keep its camera when switched away and back');
  const path_ = [];
  for (let i = 0; i < 20; i++) {
    await page.keyboard.press('Tab');
    const where = await page.evaluate(() => ({ id: document.activeElement?.id || document.activeElement?.tagName, stage: document.activeElement?.hasAttribute('data-model-stage') ?? false, inSection: Boolean(document.activeElement?.closest('[data-model-section]')) }));
    path_.push(where);
    if (!where.inSection) break;
  }
  say(`Tab from the Outside tab: ${path_.map((w) => (w.stage ? 'the stage' : w.inSection ? w.id : `${w.id}, out of the section`)).join(', ')}`);
  if (!path_[0]?.stage) problem('Tab from the selected tab did not go to its panel\'s stage');
  if (path_.at(-1).inSection) problem('Tab never left the models\' section');
  await page.close();

  // ---- Reduced motion: both views load and switch, with no error ----
  const context = await browser.newContext({ reducedMotion: 'reduce' });
  const calm = await context.newPage();
  await watch(calm, 'bbc models (reduced motion): ');
  await calm.goto(`${origin}/machines/bbc-micro/`);
  const reduced = await calm.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches);
  await calm.locator(`${OUTSIDE} [data-model-stage]`).scrollIntoViewIfNeeded();
  await calm.waitForFunction(() => ['running', 'no-webgl', 'failed'].includes(document.getElementById('model-panel-outside')?.dataset.state), null, { timeout: TIMEOUT });
  await calm.locator('#model-tab-inside').click();
  await calm.waitForFunction(() => ['running', 'no-webgl', 'failed'].includes(document.getElementById('model-panel-inside')?.dataset.state), null, { timeout: TIMEOUT });
  await calm.locator('#model-tab-inside').focus();
  await calm.keyboard.press('ArrowLeft');
  await calm.waitForTimeout(300);
  const states = await calm.evaluate(() => ['outside', 'inside'].map((v) => document.getElementById(`model-panel-${v}`).dataset.state));
  const selected = await calm.evaluate(() => document.querySelector('[role="tab"][aria-selected="true"]').id);
  say(`reduced motion (${reduced ? 'asked for' : 'NOT ASKED FOR'}): the views ${states.join(' and ')}, switched back to ${selected}`);
  if (!reduced || states.some((x) => x !== 'running') || selected !== 'model-tab-outside') problem(`with reduced motion the views did not load and switch (${states.join(', ')}, ${selected})`);
  await context.close();
}

/** Whether a board point in mm is inside the BBC Micro board's measured outline. */
function onBoard(x, y) {
  const poly = BBC_BOARD.outline;
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [x1, y1] = poly[j];
    const [x2, y2] = poly[i];
    if ((y1 > y) !== (y2 > y) && x < ((x2 - x1) * (y - y1)) / (y2 - y1) + x1) inside = !inside;
  }
  return inside;
}
