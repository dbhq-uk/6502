import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { register } from 'node:module';
import { pathToFileURL } from 'node:url';
import { REPO_ROOT, applyPreview, loadRegistry } from '../src/lib/registry.mjs';
import { loadTryIt, readRom } from '../src/lib/machines.mjs';
import { electronRoms } from '../src/lib/pins.mjs';
import { NOT_MODELLED, TAPE_TIMES, machineSeconds, symbolTable } from '../src/lib/electron.mjs';
import { ELECTRON_KEYS, PC_KEYS, NOT_ON_A_PC, ON_SCREEN_ROWS, STICKY, LEGENDS, electronCharacters, spokenName } from '../public/electron-keys.js';
import { BAD_TAPES } from '../scripts/browser-check-electron.mjs';

// The Electron page's own parts, without a browser: the key table against the
// machine's, the page script's key handling played with made-up key events,
// its cassette recorder driven with a made-up machine, the sound worklet fed
// made-up samples, and the data the page is built from. tests/electron-page.test.mjs
// checks the built page; scripts/browser-check-electron.mjs runs it in a real
// browser, where the tapes below go into the real reader. tests/bbc-micro.test.mjs
// is the pattern.

const PUBLIC = path.join(process.cwd(), 'public');

// The page scripts import each other by their site paths, /machine-host.js and
// /electron-keys.js; here those resolve to the files in public/.
register(`data:text/javascript,${encodeURIComponent(`
  export async function resolve(specifier, context, next) {
    if (/^\\/[\\w-]+\\.js$/.test(specifier)) return next(${JSON.stringify(pathToFileURL(PUBLIC).href)} + specifier, context);
    return next(specifier, context);
  }`)}`);

// ---- The key table ----

/** ElectronKey.cs's members and values, read from the source. */
const enumKeys = () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Electron', 'ElectronKey.cs'), 'utf8');
  const body = source.slice(source.indexOf('public enum ElectronKey'));
  return Object.fromEntries([...body.matchAll(/(\w+) = 0x([0-9A-F]{2})/g)].map((m) => [m[1], parseInt(m[2], 16)]));
};

test('the page\'s key list is the machine\'s ElectronKey, name for name and number for number', () => {
  const keys = enumKeys();
  // ula.md s7b: 14 columns of 4 bits, less the two positions not connected.
  assert.equal(Object.keys(keys).length, 54, 'ElectronKey.cs was not read: it has 54 keys');
  assert.deepEqual(ELECTRON_KEYS, keys);
});

test('every Electron key is pressed by a PC key or listed as not on a PC keyboard with a reason, never both', () => {
  const pressed = new Set(Object.values(PC_KEYS));
  for (const name of Object.values(PC_KEYS)) assert.ok(name in ELECTRON_KEYS, `PC_KEYS names ${name}, which is not an Electron key`);
  for (const name of Object.keys(ELECTRON_KEYS)) {
    const missing = name in NOT_ON_A_PC;
    assert.notEqual(pressed.has(name), missing, `${name} is ${missing ? 'both pressed and listed as missing' : 'neither pressed nor listed as missing'}`);
  }
  // The Electron has fewer keys than a PC: every one is on the PC keyboard.
  assert.deepEqual(NOT_ON_A_PC, {});
});

test('the keys a browser or a keyboard user needs are never taken: Tab, the function keys, Alt and the Command key', () => {
  for (const code of ['Tab', 'AltLeft', 'AltRight', 'MetaLeft', 'MetaRight', 'ContextMenu', ...Array.from({ length: 12 }, (_, i) => `F${i + 1}`)]) {
    assert.ok(!(code in PC_KEYS), `${code} is mapped to the machine`);
  }
});

test('keys go by place, not by legend: the colon is where a PC has its apostrophe, and = is SHIFT and -, so the PC\'s = presses nothing', () => {
  assert.equal(PC_KEYS.Semicolon, 'Semicolon');
  assert.equal(PC_KEYS.Quote, 'Colon');
  assert.equal(PC_KEYS.Minus, 'Minus');
  assert.equal(PC_KEYS.Equal, undefined);
  assert.equal(PC_KEYS.KeyQ, 'Q');
  assert.equal(PC_KEYS.Digit0, 'D0');
  assert.equal(PC_KEYS.CapsLock, 'CapsLock');
  assert.equal(PC_KEYS.End, 'Copy');
});

test('every key the machine has is on the on-screen keys exactly once, and has its legend', () => {
  const onScreen = ON_SCREEN_ROWS.flat();
  assert.deepEqual([...onScreen].sort(), Object.keys(enumKeys()).sort(), 'the on-screen keys are not ElectronKey, each once');
  assert.deepEqual(Object.keys(LEGENDS).sort(), Object.keys(enumKeys()).sort(), 'a key has no legend, or a legend has no key');
  assert.equal(LEGENDS.CapsLock, 'CAPS LK FUNC');
  assert.equal(spokenName('CapsLock'), 'caps lock and func');
  assert.equal(spokenName('Up'), 'cursor up');
  assert.equal(spokenName('Q'), 'Q');
  assert.deepEqual(STICKY, ['Shift', 'Ctrl', 'CapsLock']);
});

test('the on-screen rows follow the matrix: each of the four rows is one bit of the columns from 12 down to 0, after its column 13 key', () => {
  // ula.md s7b: column 12 holds 1, Q, A and Z on bits 0 to 3, column 3 holds 0, P, ; and /, and
  // so on. Read from the key numbers, not typed: a row is column 13's key on its bit, then every
  // connected position on that bit from column 12 down to column 0. Row 4 (bit 3) ends at column 1,
  // as SPACE, column 0 bit 3, is the bar below; and row 3 (bit 2) skips column 0, not connected.
  const byPlace = Object.fromEntries(Object.entries(ELECTRON_KEYS).map(([k, n]) => [n, k]));
  for (let bit = 0; bit < 4; bit++) {
    const expected = [byPlace[13 | (bit << 4)]];
    for (let column = 12; column >= 0; column--) {
      const key = byPlace[column | (bit << 4)];
      if (key && key !== 'Space') expected.push(key);
    }
    assert.deepEqual(ON_SCREEN_ROWS[bit], expected, `row ${bit + 1}`);
  }
  assert.deepEqual(ON_SCREEN_ROWS[4], ['Space']);
});

// ---- The characters, from the OS ROM ----

const os = readRom(electronRoms().find((r) => r.rom === 'os'));

test('the characters each key types come from the OS ROM\'s key and SHIFT tables, as the machine\'s own tests read them', () => {
  const chars = electronCharacters(os);
  // The pairs ElectronSession.Keys holds, which ElectronKeyboardTests checks against the same tables.
  assert.deepEqual(chars.get('"'), { key: 'D2', shift: true });
  assert.deepEqual(chars.get('@'), { key: 'D0', shift: true });
  assert.deepEqual(chars.get('*'), { key: 'Colon', shift: true });
  assert.deepEqual(chars.get('+'), { key: 'Semicolon', shift: true });
  assert.deepEqual(chars.get('='), { key: 'Minus', shift: true });
  assert.deepEqual(chars.get('?'), { key: 'Slash', shift: true });
  assert.deepEqual(chars.get('<'), { key: 'Comma', shift: true });
  assert.deepEqual(chars.get('.'), { key: 'FullStop', shift: false });
  assert.deepEqual(chars.get('P'), { key: 'P', shift: false });
  assert.deepEqual(chars.get('\r'), { key: 'Return', shift: false });
  assert.equal(chars.has('p'), false);
  // The cursor keys and COPY hold codes ($21 to $25) the key handler acts on rather than types,
  // so no character comes from one of them: ! is SHIFT and 1.
  for (const { key } of chars.values()) assert.ok(!['Left', 'Right', 'Up', 'Down', 'Copy'].includes(key), `${key} types a character`);
  assert.deepEqual(chars.get('!'), { key: 'D1', shift: true });
});

test('the symbol table says where each symbol is on a PC keyboard, from the key map', () => {
  const rows = new Map(symbolTable(os).map((r) => [r.character, r]));
  assert.deepEqual(rows.get('*'), { character: '*', electron: 'SHIFT and :', pc: "Shift and '" });
  assert.deepEqual(rows.get('='), { character: '=', electron: 'SHIFT and -', pc: 'Shift and -' });
  assert.deepEqual(rows.get(':'), { character: ':', electron: ':', pc: "'" });
  assert.deepEqual(rows.get('"'), { character: '"', electron: 'SHIFT and 2', pc: 'Shift and 2' });
  for (const r of rows.values()) assert.doesNotMatch(r.character, /^[A-Z0-9 ]$/, 'letters, digits and space need no table');
  for (const c of loadTryIt('electron').lines.join('')) if (!/[A-Z0-9 ]/.test(c)) assert.ok(rows.has(c), `the table does not say where ${c} is`);
});

// ---- The page script's keys ----

const savedDocument = Object.getOwnPropertyDescriptor(globalThis, 'document');
Object.defineProperty(globalThis, 'document', { configurable: true, writable: true, value: { querySelectorAll: () => [], hidden: false, addEventListener: () => {}, createElement: () => ({ click() {} }) } });
const { keyboard, onScreenKeys, tape, MOST_BYTES, othersDropped } = await import(pathToFileURL(path.join(PUBLIC, 'electron.js')).href);
// The recorder's Save tape test replaces these two; they are put back when the file is done.
const { createObjectURL, revokeObjectURL } = URL;
test.after(() => {
  URL.createObjectURL = createObjectURL;
  URL.revokeObjectURL = revokeObjectURL;
  if (savedDocument) Object.defineProperty(globalThis, 'document', savedDocument);
  else delete globalThis.document;
});

/** The screen, a machine that records its keys, and a way to fire key events at the screen. */
function wired() {
  const on = {};
  const screen = { addEventListener: (type, fn) => { on[type] = fn; } };
  const calls = [];
  const electron = { PressKey: (k) => calls.push(['down', k]), ReleaseKey: (k) => calls.push(['up', k]), Break: () => calls.push(['break']) };
  const panel = { dataset: {} };
  keyboard(screen, electron, panel);
  const fire = (type, code, mods = {}) => {
    const event = { code, key: 'x', repeat: false, ctrlKey: false, altKey: false, metaKey: false, ...mods, prevented: false, preventDefault() { this.prevented = true; } };
    on[type](event);
    return event;
  };
  return { calls, panel, fire, on };
}

test('a key goes down and up by its place: event.code, not event.key, picks the Electron key', () => {
  const { calls, fire } = wired();
  const down = fire('keydown', 'Quote', { key: '"' });
  fire('keyup', 'Quote', { key: '"' });
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.Colon], ['up', ELECTRON_KEYS.Colon]]);
  assert.equal(down.prevented, true, 'a key the machine takes should not also scroll or type in the page');
});

test('Tab is left to the browser, and so is anything with Alt or the Command key, and a key the Electron does not have', () => {
  const { calls, fire } = wired();
  assert.equal(fire('keydown', 'Tab').prevented, false);
  assert.equal(fire('keydown', 'KeyR', { altKey: true }).prevented, false);
  assert.equal(fire('keydown', 'KeyR', { metaKey: true }).prevented, false);
  assert.equal(fire('keydown', 'Equal').prevented, false);
  assert.deepEqual(calls, []);
});

test('with Ctrl the machine gets the key, but the browser keeps its shortcut', () => {
  const { calls, fire } = wired();
  assert.equal(fire('keydown', 'ControlLeft', { ctrlKey: true }).prevented, false);
  assert.equal(fire('keydown', 'KeyR', { ctrlKey: true }).prevented, false);
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.Ctrl], ['down', ELECTRON_KEYS.R]]);
});

test('a key held down repeats nothing, two PC keys on one Electron key let it up only with the last, and focus leaving lets go of all', () => {
  const { calls, fire, on, panel } = wired();
  fire('keydown', 'KeyA');
  fire('keydown', 'KeyA', { repeat: true });
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.A]]);
  calls.length = 0;
  fire('keydown', 'Backspace');
  fire('keydown', 'Delete');
  fire('keyup', 'Backspace');
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.Delete]]);
  on.focus();
  assert.equal(panel.dataset.typing, 'true');
  on.blur();
  assert.equal(panel.dataset.typing, undefined);
  assert.deepEqual(calls.filter((c) => c[0] === 'up').map((c) => c[1]).sort(), [ELECTRON_KEYS.A, ELECTRON_KEYS.Delete].sort());
});

test('Caps Lock is one press of CAPS LK FUNC for each key down, so FUNC is never left held; Pause is BREAK', () => {
  const { calls, fire } = wired();
  fire('keydown', 'CapsLock');
  fire('keyup', 'CapsLock');
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.CapsLock], ['up', ELECTRON_KEYS.CapsLock]]);
  calls.length = 0;
  assert.equal(fire('keydown', 'Pause').prevented, true);
  assert.deepEqual(calls, [['break']]);
});

// ---- The on-screen keys ----

function onScreen() {
  const buttons = ON_SCREEN_ROWS.flat().map((key) => {
    const b = { dataset: { electronKey: key }, disabled: true, attrs: {}, on: {} };
    b.addEventListener = (type, fn) => { b.on[type] = fn; };
    b.setAttribute = (name, value) => { b.attrs[name] = value; };
    return b;
  });
  const calls = [];
  const electron = { PressKey: (k) => calls.push(['down', k]), ReleaseKey: (k) => calls.push(['up', k]) };
  const keys = onScreenKeys({ querySelectorAll: () => buttons }, electron);
  const button = (key) => buttons.find((b) => b.dataset.electronKey === key);
  return { calls, button, tap: (key) => button(key).on.click(), buttons, keys };
}

test('an on-screen key is a press and a release, and every key is enabled once the machine runs', () => {
  const { calls, tap, buttons } = onScreen();
  assert.ok(buttons.every((b) => !b.disabled));
  tap('Copy');
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.Copy], ['up', ELECTRON_KEYS.Copy]]);
});

test('SHIFT latches for the next key and lets go; a second tap of SHIFT lets go without typing', () => {
  const { calls, tap, button } = onScreen();
  tap('Shift');
  assert.equal(button('Shift').attrs['aria-pressed'], 'true');
  assert.deepEqual(calls, []);
  tap('D2');
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.Shift], ['down', ELECTRON_KEYS.D2], ['up', ELECTRON_KEYS.D2], ['up', ELECTRON_KEYS.Shift]]);
  assert.equal(button('Shift').attrs['aria-pressed'], 'false');
  calls.length = 0;
  tap('Shift');
  tap('Shift');
  assert.deepEqual(calls, []);
});

test('CAPS LK FUNC latches as FUNC for the next key, and tapped twice is pressed alone, which turns CAPS LOCK over', () => {
  const { calls, tap, button } = onScreen();
  tap('CapsLock');
  assert.equal(button('CapsLock').attrs['aria-pressed'], 'true');
  tap('K');
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.CapsLock], ['down', ELECTRON_KEYS.K], ['up', ELECTRON_KEYS.K], ['up', ELECTRON_KEYS.CapsLock]]);
  calls.length = 0;
  tap('CapsLock');
  tap('CapsLock');
  assert.deepEqual(calls, [['down', ELECTRON_KEYS.CapsLock], ['up', ELECTRON_KEYS.CapsLock]]);
  assert.equal(button('CapsLock').attrs['aria-pressed'], 'false');
});

test('a latched key lets go without typing when the page lets go of it', () => {
  const { calls, tap, button, keys } = onScreen();
  tap('Shift');
  tap('CapsLock');
  keys.letGo();
  assert.equal(button('Shift').attrs['aria-pressed'], 'false');
  assert.equal(button('CapsLock').attrs['aria-pressed'], 'false');
  assert.deepEqual(calls, []);
});

// ---- The cassette recorder ----

/**
 * The recorder's controls as plain objects, and a made-up machine whose InsertTape answers as
 * the reader would: an empty string for a tape it takes, its sentence for one it refuses. It
 * records every call, so a test can see that a refused tape changed nothing.
 */
function recorder(reasons = new Map()) {
  const element = () => {
    const on = {};
    return { disabled: true, textContent: '', files: [], value: '', on, addEventListener: (type, fn) => { on[type] = fn; }, click() { on.click?.(); } };
  };
  const els = Object.fromEntries(['insert', 'file', 'blank', 'rewind', 'save', 'tape', 'motor'].map((n) => [`[data-electron-${n}]`, element()]));
  const panel = { dataset: {}, querySelector: (q) => els[q] };
  const calls = [];
  let ejected = null;
  // The motor and the tape counter, as the test sets them.
  const state = { motor: false, seconds: 0 };
  const electron = {
    InsertTape: (data) => { calls.push(['insert', data.length]); return reasons.get(data.length) ?? ''; },
    StartRecording: () => calls.push(['record']),
    Rewind: () => calls.push(['rewind']),
    EjectTape: () => { calls.push(['eject']); return ejected; },
    MotorOn: () => state.motor,
    TapeSeconds: () => state.seconds,
  };
  const recorder = tape(panel, electron);
  const file = (name, bytes) => ({ name, size: bytes.length, arrayBuffer: async () => Uint8Array.from(bytes).buffer });
  const choose = async (f) => {
    els['[data-electron-file]'].files = [f];
    els['[data-electron-file]'].on.change();
    await new Promise((r) => setTimeout(r, 0));
  };
  return { els, panel, calls, file, choose, recorder, state, eject: (b) => { ejected = b; }, line: () => els['[data-electron-tape]'].textContent };
}

test('the bad tapes the browser check uses are the reader\'s own cases, worded as UefReader.cs words them', () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Electron', 'Tape', 'UefReader.cs'), 'utf8');
  assert.deepEqual(BAD_TAPES.map((t) => t.name), ['empty.uef', 'cut.uef', 'slow.uef', 'not-a-tape.uef', 'version-1.uef']);
  for (const t of BAD_TAPES) for (const part of t.source) assert.ok(source.includes(part), `UefReader.cs does not say "${part}"`);
  // The files are what they say: empty, gzip cut short, &0117 at 300 (&012C), not a UEF though
  // named one, and major version 1.
  const [empty, cut, slow, notTape, version] = BAD_TAPES.map((t) => t.bytes());
  assert.equal(empty.length, 0);
  assert.deepEqual([...cut.subarray(0, 2)], [0x1f, 0x8b]);
  assert.deepEqual([...slow.subarray(12)], [0x17, 0x01, 2, 0, 0, 0, 0x2c, 0x01]);
  assert.ok(notTape.length >= 12, 'too short to reach the header check');
  assert.notEqual(String.fromCharCode(...notTape.subarray(0, 9)), 'UEF File!');
  assert.deepEqual([...version.subarray(9, 12)], [0, 0, 1]);
});

test('a damaged or unsupported tape is shown in the reader\'s own words, changes nothing, and logs no error', async () => {
  // The made-up reader gives each bad tape's sentence, told apart by the file's length.
  const reasons = new Map(BAD_TAPES.map((t) => [t.bytes().length, t.says ?? `${t.starts} its gzip stream does not end with the CRC and length of what it inflates to.`]));
  assert.equal(reasons.size, BAD_TAPES.length, 'two bad tapes have the same length');
  const { panel, calls, file, choose, line } = recorder(reasons);
  const errors = [];
  const saved = console.error;
  console.error = (...args) => errors.push(args);
  try {
    // A good tape first, so a refused one can be seen to leave it in.
    const good = [1, 2, 3, 4, 5, 6, 7];
    assert.ok(!reasons.has(good.length));
    await choose(file('good.uef', good));
    assert.equal(panel.dataset.tape, 'good.uef');
    calls.length = 0;
    for (const t of BAD_TAPES) {
      const bytes = t.bytes();
      await choose(file(t.name, [...bytes]));
      assert.equal(line(), `${t.name} was not put in. ${reasons.get(bytes.length)}`);
      assert.equal(panel.dataset.tape, 'good.uef', `${t.name} took the good tape out`);
    }
  } finally {
    console.error = saved;
  }
  // The reader was asked, once a file, and nothing else was done to the machine.
  assert.deepEqual(calls, BAD_TAPES.map((t) => ['insert', t.bytes().length]));
  assert.deepEqual(errors, []);
});

test('a file that is not a .uef, or is larger than the reader takes, is refused before it is read', async () => {
  const { calls, file, choose, line } = recorder();
  await choose(file('game.ssd', [1]));
  assert.match(line(), /^game\.ssd is not a tape this recorder takes: it takes \.uef tape images\.$/);
  await choose({ name: 'huge.uef', size: MOST_BYTES + 1, arrayBuffer: async () => { throw new Error('read'); } });
  assert.match(line(), /^huge\.uef is 4,194,305 bytes, more than the 4 MiB a tape may be here, so it was not read\.$/);
  assert.deepEqual(calls, []);
});

test('several files dropped at once: the first is tried, and the tape line says the others were left', async () => {
  assert.equal(othersDropped([{ name: 'a.uef' }]), '');
  const others = othersDropped([{ name: 'a.uef' }, { name: 'b.uef' }, { name: 'c.uef' }]);
  assert.equal(others, 'Only the first of the 3 files dropped, a.uef, was tried: drop one tape at a time.');
  const { panel, file, line } = recorder();
  await panel.putTape(file('a.uef', [1, 2, 3]), others);
  assert.equal(panel.dataset.tape, 'a.uef');
  assert.ok(line().endsWith(` ${others}`), line());
});

test('the page\'s largest tape is the reader\'s own limit', () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Electron', 'Tape', 'UefReader.cs'), 'utf8');
  assert.match(source, /public const int MaxInflatedBytes = 4 \* 1024 \* 1024;/);
  assert.equal(MOST_BYTES, 4 * 1024 * 1024);
});

test('Blank tape records, Rewind plays it, and Save tape downloads it and puts it back in, rewound', async () => {
  const { els, panel, calls, eject, line } = recorder();
  els['[data-electron-blank]'].click();
  assert.deepEqual(calls, [['record']]);
  assert.equal(panel.dataset.tape, 'tape.uef');
  assert.equal(els['[data-electron-save]'].disabled, false);
  assert.match(line(), /on record\. Type SAVE "NAME"/);
  // Nothing recorded yet: Save tape says so and the blank tape stays on record.
  els['[data-electron-save]'].click();
  assert.deepEqual(calls.slice(1), [['eject'], ['record']]);
  assert.match(line(), /nothing on the tape to save yet/);
  calls.length = 0;
  const made = [];
  URL.createObjectURL = () => 'blob:x';
  URL.revokeObjectURL = () => {};
  globalThis.document.createElement = () => { const a = { click() { made.push(a.download); } }; return a; };
  eject(new Uint8Array([0x1f, 0x8b, 1, 2]));
  els['[data-electron-save]'].click();
  assert.deepEqual(made, ['tape.uef']);
  assert.deepEqual(calls, [['eject'], ['insert', 4]]);
  assert.match(line(), /^Saved as tape\.uef, and left in the recorder, rewound\./);
  calls.length = 0;
  els['[data-electron-rewind]'].click();
  assert.deepEqual(calls, [['rewind']]);
});

test('Save tape while the OS is still writing a SAVE is refused with a sentence, and nothing is taken out', () => {
  const { els, calls, state, eject, line } = recorder();
  els['[data-electron-blank]'].click();
  calls.length = 0;
  state.motor = true;
  eject(new Uint8Array([0x1f, 0x8b, 1, 2]));
  els['[data-electron-save]'].click();
  assert.deepEqual(calls, [], 'the half-written tape was taken out');
  assert.equal(line(), 'The Electron is still saving to the tape: wait for the prompt to come back, then press Save tape.');
});

test('the motor line follows the machine: off, then on with the seconds of tape, then off after them', () => {
  const { els, panel, state, recorder: r } = recorder();
  const motor = () => els['[data-electron-motor]'].textContent;
  r.tick(0);
  assert.equal(motor(), 'Motor off.');
  assert.equal(panel.dataset.motor, 'off');
  state.motor = true;
  state.seconds = 2.34;
  r.tick(100);
  assert.equal(motor(), 'Motor on: 2.3 seconds of tape played.');
  assert.equal(panel.dataset.motor, 'on');
  // Under a quarter of a second later the line is left as it is; after, it moves on.
  state.seconds = 2.5;
  r.tick(200);
  assert.equal(motor(), 'Motor on: 2.3 seconds of tape played.');
  r.tick(400);
  assert.equal(motor(), 'Motor on: 2.5 seconds of tape played.');
  state.motor = false;
  state.seconds = 5.56;
  r.tick(420);
  assert.equal(motor(), 'Motor off, after 5.6 seconds of tape played.');
  assert.equal(panel.dataset.motor, 'off');
});

// ---- The sound worklet ----

function worklet() {
  let Processor;
  const scope = {
    sampleRate: 48_000,
    AudioWorkletProcessor: class { constructor() { this.port = { onmessage: null }; } },
    registerProcessor: (name, cls) => { assert.equal(name, 'electron-sound'); Processor = cls; },
    Float32Array,
  };
  vm.runInNewContext(fs.readFileSync(path.join(PUBLIC, 'electron-audio.js'), 'utf8'), scope);
  const p = new Processor();
  const post = (data) => p.port.onmessage({ data: data instanceof Float32Array ? new scope.Float32Array(data) : data });
  const pull = (quanta = 1) => {
    const out = [];
    for (let i = 0; i < quanta; i++) {
      const block = new Float32Array(128);
      assert.equal(p.process([], [[block]]), true);
      out.push(...block);
    }
    return out;
  };
  return { p, post, pull };
}

const ms = (n) => Math.round(48 * n);

test('the worklet stays silent until enough sound is queued, then plays the samples as they come, at half gain', () => {
  const { post, pull } = worklet();
  assert.deepEqual(pull(1), new Array(128).fill(0));
  // The Electron's samples are already round zero (UlaSound): a tone of plus and minus a half.
  const square = (n) => Float32Array.from({ length: n }, (_, i) => ((i / 24) & 1 ? 0.5 : -0.5));
  post(square(ms(20)));
  assert.ok(pull(2).every((v) => v === 0), 'it played before enough was queued');
  post(square(ms(40)));
  const played = pull(8);
  assert.equal(Math.max(...played), 0.25);
  assert.equal(Math.min(...played), -0.25);
});

test('running dry fades to silence rather than clicking, then waits to fill again; a long queue is cut back; flush empties it', () => {
  const { p, post, pull } = worklet();
  post(new Float32Array(ms(50)).fill(0.5));
  pull(Math.ceil(ms(50) / 128) + 40);
  assert.ok(pull(4).every((v) => Math.abs(v) < 0.01), 'after running dry the output is not silent');
  post(new Float32Array(ms(10)).fill(1));
  assert.ok(pull(2).every((v) => Math.abs(v) < 0.01), 'after running dry it played before refilling');
  post(new Float32Array(ms(100)));
  post(new Float32Array(ms(100)));
  assert.equal(p.queued, ms(40));
  post({ flush: true });
  assert.equal(p.queued, 0);
});

// ---- The data the page is built from ----

test('the four parts left out are the ones the design and tape.md s6 name, each with its own issue', () => {
  assert.deepEqual(NOT_MODELLED.map((m) => m.issue), [57, 58, 59, 60]);
  const parts = NOT_MODELLED.map((m) => m.part).join(' / ');
  for (const part of ['Plus 1', 'cartridge', 'Plus 3', 'disc', 'joystick', 'printer', '300 baud', 'non-standard tapes']) assert.ok(parts.includes(part), `${part} is not listed`);
});

test('the try-it program is the BBC Micro\'s, line for line, as the page says it is', () => {
  const electron = loadTryIt('electron');
  const bbc = loadTryIt('bbc-micro');
  assert.deepEqual(electron.lines, bbc.lines);
  assert.deepEqual(electron.shows, bbc.shows);
});

test('the try-it program is BASIC lines and what they print, read as the BBC Micro\'s is', () => {
  const program = loadTryIt('electron');
  assert.ok(program.lines.length > 0 && program.shows.length > 0);
  assert.equal(program.lines.at(-1), 'RUN');
  const root = fs.mkdtempSync(path.join(process.cwd(), '.tmp-try-'));
  try {
    fs.mkdirSync(path.join(root, 'machines', 'electron'), { recursive: true });
    fs.writeFileSync(path.join(root, 'machines', 'electron', 'try-it.json'), JSON.stringify({ lines: ['10 PRINT "é"'], shows: ['x'] }));
    assert.throws(() => loadTryIt('electron', root), /not a line of plain printable text/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('how long a tape takes is the journal\'s dated measurement, its cycles over the 2 MHz clock, never a figure typed twice', () => {
  const journal = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'journal', '2026-10-04-the-electron-fact-sheets.md'), 'utf8');
  const fmt = (n) => n.toLocaleString('en-GB');
  assert.ok(journal.includes(`**${fmt(TAPE_TIMES.saveCycles)} cycles, ${machineSeconds(TAPE_TIMES.saveCycles)} s**`), 'the journal does not record the SAVE as the page says');
  assert.ok(journal.includes(`**${fmt(TAPE_TIMES.loadCycles)} cycles, ${machineSeconds(TAPE_TIMES.loadCycles)} s**`), 'the journal does not record the LOAD as the page says');
  assert.ok(journal.includes('run on 5 October'), 'the measurement is not dated in the journal');
  assert.equal(TAPE_TIMES.measured, '5 October 2026');
  assert.equal(TAPE_TIMES.journal, '/journal/2026-10-04-the-electron-fact-sheets/');
  assert.equal(machineSeconds(21_513_995), '10.76');
});

// ---- The registry, planned, and its preview ----

test('a preview lays its fields over the registry\'s for the machines it names, and cannot add one', () => {
  const registry = { machines: [{ id: 'a', status: 'planned', name: 'A' }, { id: 'b', status: 'planned' }], chips: [] };
  const seen = applyPreview(registry, { machines: { a: { status: 'running', notes: 'n' } } });
  assert.deepEqual(seen.machines[0], { id: 'a', status: 'running', name: 'A', notes: 'n' });
  assert.equal(seen.machines[1], registry.machines[1]);
  assert.equal(registry.machines[0].status, 'planned', 'the registry itself was changed');
  assert.throws(() => applyPreview(registry, { machines: { c: {} } }), /not in the registry/);
});

test('the real registry says the Electron is planned, with its acceptance test, rights and notes already; the preview switches it on and gives its clock', () => {
  const plain = loadRegistry(undefined, '');
  const record = plain.machines.find((m) => m.id === 'electron');
  assert.equal(record.status, 'planned');
  assert.equal(record.acceptance, 'ElectronAcceptanceTests');
  assert.ok(fs.existsSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.Electron.Tests', 'ElectronAcceptanceTests.cs')));
  assert.match(record.rights, /documented, not cleared/);
  assert.match(record.notes, /^The keyboard/);
  const previewed = loadRegistry(undefined, 'tests/fixtures/electron-running.json');
  const electron = previewed.machines.find((m) => m.id === 'electron');
  assert.equal(electron.status, 'running');
  assert.equal(electron.cpu, '6502, 2 MHz');
  assert.equal(electron.rights, record.rights, 'the preview changes the rights: they are the registry\'s');
  assert.deepEqual(previewed.machines.filter((m) => m.id !== 'electron'), plain.machines.filter((m) => m.id !== 'electron'));
});
