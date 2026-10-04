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
