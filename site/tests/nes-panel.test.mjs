import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { register } from 'node:module';
import { pathToFileURL } from 'node:url';
import { REPO_ROOT } from '../src/lib/registry.mjs';

// The NES panel's own parts, without a browser: the keyboard and gamepad maps
// against the machine's button bits, the page script's key, pad and touch
// handling played with made-up events, the region lines and the bad-file line
// with a made-up host, the picture's shape in each region, the size limit, and
// the sound worklet's bounded ring. Task 15's page test checks the built page,
// and its browser check runs the panel in a real browser.

const PUBLIC = path.join(process.cwd(), 'public');

// The page scripts import each other by their site paths, /machine-host.js;
// here those resolve to the files in public/.
register(`data:text/javascript,${encodeURIComponent(`
  export async function resolve(specifier, context, next) {
    if (/^\\/[\\w-]+\\.js$/.test(specifier)) return next(${JSON.stringify(pathToFileURL(PUBLIC).href)} + specifier, context);
    return next(specifier, context);
  }`)}`);

const nes = await import(pathToFileURL(path.join(PUBLIC, 'nes.js')).href);
const { KEY_NAMES } = await import(pathToFileURL(path.join(PUBLIC, 'nes-keys.js')).href);
const audio = await import(pathToFileURL(path.join(PUBLIC, 'nes-audio.js')).href);
const { BUTTONS, KEYS, GAMEPAD_BUTTONS, keyboard, touchPad, gamepadMask, padMasks, startingRegion, loadCartridge, cartridgeLine, regionChangeLine, badFileLine, pictureShape, showShape, sizeRefusal, readRom } = nes;

// ---- The buttons, the keys and the pads ----

/** The machine's own bit order, read from Controller.cs: "bit 0 A, 1 B, ...". */
const machineBits = () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Nes', 'Controller.cs'), 'utf8');
  const order = /bit 0 A, 1 B, 2 Select, 3 Start, 4 Up, 5 Down, 6 Left, 7 Right/.exec(source)?.[0];
  assert.ok(order, 'Controller.cs no longer states its bit order in the words this test reads');
  return Object.fromEntries([...order.matchAll(/(\d) (\w+)/g)].map((m) => [m[2], 1 << Number(m[1])]));
};

test('the page\'s button bits are the machine\'s, A in bit 0 to Right in bit 7', () => {
  assert.deepEqual(BUTTONS, machineBits());
});

test('the key map: each event.code to its button, by place, as the brief lists them', () => {
  assert.deepEqual(KEYS, {
    ArrowUp: 'Up',
    ArrowDown: 'Down',
    ArrowLeft: 'Left',
    ArrowRight: 'Right',
    KeyZ: 'B',
    KeyX: 'A',
    Enter: 'Start',
    ShiftRight: 'Select',
  });
  for (const [code, button] of Object.entries(KEYS)) assert.equal(typeof BUTTONS[button], 'number', `${code} names ${button}, which is not a button`);
  // The panel says which key is which from the same table, each key named once.
  assert.deepEqual(Object.keys(KEY_NAMES).sort(), Object.keys(KEYS).sort());
});

test('the keys a browser or a keyboard user needs are never taken: Tab, Escape, the function keys, Alt and the Command key', () => {
  for (const code of ['Tab', 'Escape', 'AltLeft', 'AltRight', 'MetaLeft', 'MetaRight', 'ContextMenu', ...Array.from({ length: 12 }, (_, i) => `F${i + 1}`)]) {
    assert.ok(!(code in KEYS), `${code} is mapped to the machine`);
  }
});

test('the gamepad map: standard buttons 0 and 2 are A and B, 8 and 9 Select and Start, 12 to 15 the d-pad', () => {
  assert.deepEqual(GAMEPAD_BUTTONS, { 0: 'A', 2: 'B', 8: 'Select', 9: 'Start', 12: 'Up', 13: 'Down', 14: 'Left', 15: 'Right' });
  const pad = (...pressed) => ({ buttons: Array.from({ length: 17 }, (_, i) => ({ pressed: pressed.includes(i) })) });
  assert.equal(gamepadMask(pad(0)), BUTTONS.A);
  assert.equal(gamepadMask(pad(2)), BUTTONS.B);
  assert.equal(gamepadMask(pad(8)), BUTTONS.Select);
  assert.equal(gamepadMask(pad(9)), BUTTONS.Start);
  assert.equal(gamepadMask(pad(12)), BUTTONS.Up);
  assert.equal(gamepadMask(pad(13)), BUTTONS.Down);
  assert.equal(gamepadMask(pad(14)), BUTTONS.Left);
  assert.equal(gamepadMask(pad(15)), BUTTONS.Right);
  // Buttons the map does not name do nothing; a pad with fewer buttons is read as far as it goes.
  assert.equal(gamepadMask(pad(1, 3, 4, 5, 6, 7, 10, 11, 16)), 0);
  assert.equal(gamepadMask({ buttons: [{ pressed: true }] }), BUTTONS.A);
  // Every button at once is every bit at once: the page filters nothing (Review Focus 4).
  assert.equal(gamepadMask(pad(0, 2, 8, 9, 12, 13, 14, 15)), 0xFF);
});

test('pads go by their index: the first is controller 1 with the keys and the touch pad, the second is controller 2', () => {
  const pad = (...pressed) => ({ buttons: Array.from({ length: 16 }, (_, i) => ({ pressed: pressed.includes(i) })) });
  assert.deepEqual(padMasks({ keys: 0, touch: 0 }, []), [0, 0]);
  assert.deepEqual(padMasks({ keys: BUTTONS.Up, touch: BUTTONS.A }, [pad(9), pad(0)]), [BUTTONS.Up | BUTTONS.A | BUTTONS.Start, BUTTONS.A]);
  // A pad unplugged leaves a hole in the browser's list; a third pad is not a controller here.
  assert.deepEqual(padMasks({ keys: 0, touch: 0 }, [null, pad(14), pad(0)]), [0, BUTTONS.Left]);
});

/** The screen as a made-up element, and a way to fire key events at it. */
function wiredKeys() {
  const on = {};
  const screen = { blurred: 0, addEventListener: (type, fn) => { on[type] = fn; }, blur() { this.blurred++; } };
  const masks = [];
  keyboard(screen, (mask) => masks.push(mask));
  const fire = (type, code, mods = {}) => {
    const event = { code, repeat: false, ctrlKey: false, altKey: false, metaKey: false, ...mods, prevented: false, preventDefault() { this.prevented = true; } };
    on[type](event);
    return event;
  };
  return { screen, on, masks, fire };
}

test('a key held is its button held, and the page takes the key from the browser so the arrows do not scroll', () => {
  const { masks, fire } = wiredKeys();
  assert.equal(fire('keydown', 'ArrowLeft').prevented, true);
  assert.equal(masks.at(-1), BUTTONS.Left);
  fire('keydown', 'KeyX');
  assert.equal(masks.at(-1), BUTTONS.Left | BUTTONS.A);
  fire('keyup', 'ArrowLeft');
  assert.equal(masks.at(-1), BUTTONS.A);
});

test('Left and Right together, and Up and Down, go to the machine as pressed: the page does not filter them', () => {
  const { masks, fire } = wiredKeys();
  fire('keydown', 'ArrowLeft');
  fire('keydown', 'ArrowRight');
  assert.equal(masks.at(-1), 0xC0);
  fire('keydown', 'ArrowUp');
  fire('keydown', 'ArrowDown');
  assert.equal(masks.at(-1), 0xF0);
});

test('Tab is left to the browser, and so is anything with Alt or the Command key; Escape lets go of the screen', () => {
  const { masks, fire, screen } = wiredKeys();
  assert.equal(fire('keydown', 'Tab').prevented, false);
  assert.equal(fire('keydown', 'KeyX', { altKey: true }).prevented, false);
  assert.equal(fire('keydown', 'KeyX', { metaKey: true }).prevented, false);
  assert.deepEqual(masks, []);
  assert.equal(fire('keydown', 'Escape').prevented, false);
  assert.equal(screen.blurred, 1, 'Escape did not let go of the screen');
});

test('a held key\'s repeats change nothing, and focus leaving the screen lets go of every key', () => {
  const { masks, fire, on } = wiredKeys();
  fire('keydown', 'Enter');
  fire('keydown', 'Enter', { repeat: true });
  assert.deepEqual(masks, [BUTTONS.Start]);
  fire('keydown', 'ShiftRight');
  on.blur();
  assert.equal(masks.at(-1), 0);
});

/** The on-screen pad's buttons as made-up elements. */
function wiredTouch() {
  const buttons = Object.keys(BUTTONS).map((name) => {
    const b = { dataset: { nesButton: name }, disabled: true, on: {}, attrs: {} };
    b.addEventListener = (type, fn) => { b.on[type] = fn; };
    b.setAttribute = (n, v) => { b.attrs[n] = v; };
    b.setPointerCapture = () => {};
    return b;
  });
  const panel = { querySelectorAll: () => buttons };
  const masks = [];
  touchPad(panel, (mask) => masks.push(mask));
  const button = (name) => buttons.find((b) => b.dataset.nesButton === name);
  const event = () => ({ pointerId: 1, preventDefault() {} });
  return { buttons, masks, button, down: (n) => button(n).on.pointerdown(event()), up: (n) => button(n).on.pointerup(event()), cancel: (n) => button(n).on.pointercancel(event()) };
}

test('the on-screen pad: a button is held while a finger is on it, two at once included, and enabled once the machine runs', () => {
  const { buttons, masks, down, up, cancel } = wiredTouch();
  assert.ok(buttons.every((b) => !b.disabled));
  down('Left');
  down('Right');
  assert.equal(masks.at(-1), 0xC0);
  up('Left');
  assert.equal(masks.at(-1), BUTTONS.Right);
  cancel('Right');
  assert.equal(masks.at(-1), 0);
});

// ---- The region, and a file that is not a cartridge ----

test('the region control starts from the header: PAL where it says PAL, NTSC where it says NTSC or nothing', () => {
  assert.equal(startingRegion('PAL'), 'PAL');
  assert.equal(startingRegion('NTSC'), 'NTSC');
  assert.equal(startingRegion(''), 'NTSC');
});

/** A made-up NesHost: RegionOf and Load as the C# has them (NesLoaderTests), and a record of what was loaded. */
function fakeHost({ header = '', refuse = null, refuseBoard = null } = {}) {
  const host = {
    loaded: 'the machine before',
    calls: [],
    RegionOf(bytes) {
      host.calls.push(['RegionOf', bytes.length]);
      if (refuse) throw new Error(refuse);
      if (refuseBoard) throw new Error(refuseBoard);
      return header;
    },
    Load(bytes, region, rate) {
      host.calls.push(['Load', region, rate]);
      if (refuse) throw new Error(refuse);
      if (refuseBoard) throw new Error(refuseBoard);
      const chosen = region || header || 'NTSC';
      host.loaded = chosen;
      if (!region) return header ? `Running as ${chosen}: the file says ${chosen}.` : 'Running as NTSC: the file does not say, so NTSC.';
      if (!header) return `Running as ${chosen}, as chosen here: the file does not say which.`;
      return header === chosen ? `Running as ${chosen}, as chosen here, which is what the file says.` : `Running as ${chosen}, as chosen here, though the file says ${header}, so it may run at the wrong speed.`;
    },
  };
  return host;
}

const ROM = new Uint8Array(40_976);

test('a file that says PAL runs as PAL with no choice made, and the region line is the host\'s sentence', () => {
  const host = fakeHost({ header: 'PAL' });
  const result = loadCartridge(host, ROM, '', 48_000);
  assert.deepEqual(result, { ok: true, region: 'PAL', sentence: 'Running as PAL: the file says PAL.' });
  assert.deepEqual(host.calls, [['RegionOf', ROM.length], ['Load', '', 48_000]]);
  // The line under the region control is the host's sentence as it stands; the cartridge's line names the file.
  assert.equal(cartridgeLine('snow.nes'), 'snow.nes is in.');
});

test('a file that does not say runs as NTSC, and the region line says the file does not say', () => {
  const host = fakeHost();
  const result = loadCartridge(host, ROM, '', 48_000);
  assert.deepEqual(result, { ok: true, region: 'NTSC', sentence: 'Running as NTSC: the file does not say, so NTSC.' });
});

test('changing the region against the file restarts the machine in that region, and the line says so and why', () => {
  const host = fakeHost({ header: 'PAL' });
  const result = loadCartridge(host, ROM, 'NTSC', 48_000);
  assert.deepEqual(host.calls.at(-1), ['Load', 'NTSC', 48_000]);
  assert.equal(result.region, 'NTSC');
  assert.equal(regionChangeLine(result), 'The region changed, so the machine restarted. Running as NTSC, as chosen here, though the file says PAL, so it may run at the wrong speed.');
});

test('a bad file shows the cartridge\'s own sentence, and the machine stays as it was: Load is never asked', () => {
  const host = fakeHost({ refuse: 'The file does not start with the iNES signature, so it is not a NES cartridge.' });
  const result = loadCartridge(host, ROM, '', 48_000);
  assert.deepEqual(result, { ok: false, message: 'The file does not start with the iNES signature, so it is not a NES cartridge.' });
  assert.deepEqual(host.calls.map((c) => c[0]), ['RegionOf']);
  assert.equal(host.loaded, 'the machine before');
  assert.equal(badFileLine('notes.txt', result, true), 'notes.txt was not loaded. The file does not start with the iNES signature, so it is not a NES cartridge. The machine carries on as it was.');
  assert.equal(badFileLine('notes.txt', result, false), 'notes.txt was not loaded. The file does not start with the iNES signature, so it is not a NES cartridge. Choose another file.');
});

test('a board the machine does not model is refused the same way, by RegionOf, before Load', () => {
  const host = fakeHost({ refuseBoard: 'This cartridge uses mapper 5, which this machine does not model; it models mappers: 0, 1, 2, 3, 4, 7.' });
  const result = loadCartridge(host, ROM, 'PAL', 48_000);
  assert.equal(result.ok, false);
  assert.match(result.message, /mapper 5/);
  assert.equal(host.loaded, 'the machine before');
});

test('an error that is not a sentence still gives a line, never an exception', () => {
  const host = { RegionOf: () => { throw 'a string'; }, Load: () => assert.fail('Load was asked') };
  const result = loadCartridge(host, ROM, '', 48_000);
  assert.equal(result.ok, false);
  assert.equal(typeof result.message, 'string');
  assert.ok(result.message.length > 0);
});

// ---- The picture's shape ----

/** A canvas's displayed width over its height, from the aspect-ratio the page set on it. */
const displayed = (canvas) => {
  const m = /^([\d.]+) \/ ([\d.]+)$/.exec(canvas.style.aspectRatio);
  assert.ok(m, `the canvas's aspect-ratio is ${canvas.style.aspectRatio}`);
  return Number(m[1]) / Number(m[2]);
};
const within = (actual, expected, share) => Math.abs(actual - expected) <= expected * share;

test('the picture is 256 by 240 pixels shown in the region\'s pixel shape: 8:7 on NTSC, 355 by 240 on PAL', () => {
  assert.deepEqual(pictureShape('NTSC'), { width: 256 * 8 / 7, height: 240 });
  assert.ok(within(pictureShape('PAL').width / pictureShape('PAL').height, 355 / 240, 0.01));
  const canvas = { width: 256, height: 240, style: {}, dataset: {} };
  showShape(canvas, 'NTSC');
  assert.ok(within(displayed(canvas), (256 * 8) / 7 / 240, 0.01), `NTSC shows ${displayed(canvas)}`);
  showShape(canvas, 'PAL');
  assert.ok(within(displayed(canvas), 355 / 240, 0.01), `PAL shows ${displayed(canvas)}`);
  // Changing the region changes it, and back again.
  const pal = displayed(canvas);
  showShape(canvas, 'NTSC');
  assert.notEqual(displayed(canvas), pal);
  assert.equal(canvas.dataset.region, 'NTSC');
});

// ---- The size limit ----

/** A made-up File: its name and size, and an arrayBuffer that records whether it was read. */
const fakeFile = (name, size) => ({ name, size, reads: 0, async arrayBuffer() { this.reads++; return new ArrayBuffer(size); } });

test('a file over the cartridge\'s limit is refused before it is read, with the limit the host gives', async () => {
  const limit = 1234;
  const big = fakeFile('huge.nes', limit + 1);
  assert.match(sizeRefusal(big, limit), /^huge\.nes is 1,235 bytes, more than the 1,234 bytes of the largest file this machine loads, so it was not read\.$/);
  const result = await readRom(big, limit);
  assert.equal(result.ok, false);
  assert.equal(big.reads, 0, 'the file was read before it was refused');
  const fits = fakeFile('ok.nes', limit);
  assert.equal(sizeRefusal(fits, limit), null);
  const read = await readRom(fits, limit);
  assert.equal(read.ok, true);
  assert.equal(read.bytes.length, limit);
  assert.equal(fits.reads, 1);
});

test('the page never types the limit: it asks the host for MaxRomBytes', () => {
  const source = fs.readFileSync(path.join(PUBLIC, 'nes.js'), 'utf8');
  assert.match(source, /\.MaxRomBytes\(\)/);
  assert.doesNotMatch(source, /4194304|4_194_304|4 \* 1024 \* 1024|1024 \* 1024/);
});

// ---- The sound worklet's ring ----

const RATE = 48_000;
const ms = (n) => Math.round((RATE * n) / 1000);

test('admit: what arrives is kept while the ring is under its most; past it, the oldest go, down to the prime', () => {
  const { PRIME, MOST } = audio;
  const prime = Math.round(PRIME * RATE);
  const most = Math.round(MOST * RATE);
  assert.deepEqual(audio.admit(0, 100, prime, most), { drop: 0, queued: 100 });
  assert.deepEqual(audio.admit(most - 100, 100, prime, most), { drop: 0, queued: most });
  assert.deepEqual(audio.admit(most, 1, prime, most), { drop: most + 1 - prime, queued: prime });
  // A burst far larger than the ring still leaves only the prime's worth.
  assert.deepEqual(audio.admit(10, most * 10, prime, most), { drop: 10 + most * 10 - prime, queued: prime });
});

test('take: silent until the prime is queued, then it plays; running dry stops it until the prime is queued again', () => {
  const prime = 100;
  assert.deepEqual(audio.take(99, 128, false, prime), { taken: 0, playing: false });
  assert.deepEqual(audio.take(100, 128, false, prime), { taken: 100, playing: false });
  assert.deepEqual(audio.take(500, 128, false, prime), { taken: 128, playing: true });
  assert.deepEqual(audio.take(50, 128, true, prime), { taken: 50, playing: false });
  assert.deepEqual(audio.take(0, 128, false, prime), { taken: 0, playing: false });
});

test('the ring never holds more than its most, whatever arrives unread, and keeps the newest samples', () => {
  const ring = new audio.SampleRing(RATE);
  const most = Math.round(audio.MOST * RATE);
  assert.equal(ring.capacity, most);
  let next = 0;
  for (let i = 0; i < 100; i++) {
    const chunk = Float32Array.from({ length: ms(30) }, () => next++);
    ring.push(chunk);
    assert.ok(ring.queued <= most, `the ring holds ${ring.queued} samples`);
  }
  // Ten times the ring went in unread; what is left is the newest, in order.
  assert.ok(ring.queued >= Math.round(audio.PRIME * RATE));
  const first = next - ring.queued;
  const out = new Float32Array(128);
  assert.equal(ring.pull(out), 128);
  assert.equal(out[0], first);
  assert.equal(out[127], first + 127);
});

test('the ring plays what it was given in order, then fades to silence when it runs dry rather than clicking', () => {
  const ring = new audio.SampleRing(RATE);
  ring.push(new Float32Array(ms(50)).fill(0.5));
  const out = new Float32Array(128);
  let played = 0;
  while (ring.queued > 0) played += ring.pull(out);
  assert.equal(played, ms(50));
  // Dry: the output starts at the last level and falls away, never jumping to 0.
  const tail = new Float32Array(128 * 40);
  for (let i = 0; i < 40; i++) {
    ring.pull(out);
    tail.set(out, i * 128);
  }
  assert.ok(tail[0] > 0.4, `the first dry sample is ${tail[0]}`);
  assert.ok(Math.abs(tail.at(-1)) < 0.01, `the output did not fade: ${tail.at(-1)}`);
  for (let i = 1; i < tail.length; i++) assert.ok(tail[i] <= tail[i - 1] + 1e-9, 'the fade is not smooth');
  ring.flush();
  assert.equal(ring.queued, 0);
});
