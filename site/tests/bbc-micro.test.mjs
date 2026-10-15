import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import vm from 'node:vm';
import { register } from 'node:module';
import { pathToFileURL } from 'node:url';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import { loadTryIt } from '../src/lib/machines.mjs';
import { readRom } from '../src/lib/machines.mjs';
import { bbcRoms } from '../src/lib/pins.mjs';
import { NOT_MODELLED, megabytes, symbolTable } from '../src/lib/bbc-micro.mjs';
import { BBC_KEYS, PC_KEYS, NOT_ON_A_PC, ON_SCREEN_ROWS, STICKY, LEGENDS, bbcCharacters, spokenName } from '../public/bbc-keys.js';

// The BBC Micro page's own parts, without a browser: the key table against the
// machine's, the page script's key handling played with made-up key events,
// the sound worklet fed made-up samples, and the data the page is built from.
// tests/bbc-page.test.mjs checks the built page; scripts/browser-check.mjs runs
// it in a real browser.

const PUBLIC = path.join(process.cwd(), 'public');

// The page scripts import each other by their site paths, /machine-host.js and
// /bbc-keys.js; here those resolve to the files in public/.
register(`data:text/javascript,${encodeURIComponent(`
  export async function resolve(specifier, context, next) {
    if (/^\\/[\\w-]+\\.js$/.test(specifier)) return next(${JSON.stringify(pathToFileURL(PUBLIC).href)} + specifier, context);
    return next(specifier, context);
  }`)}`);

// ---- The key table ----

/** BbcKey.cs's members and values, read from the source. */
const enumKeys = () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro', 'BbcKey.cs'), 'utf8');
  const body = source.slice(source.indexOf('public enum BbcKey'));
  return Object.fromEntries([...body.matchAll(/(\w+) = 0x([0-9A-F]{2})/g)].map((m) => [m[1], parseInt(m[2], 16)]));
};

test('the page\'s key list is the machine\'s BbcKey, name for name and number for number', () => {
  const keys = enumKeys();
  assert.equal(Object.keys(keys).length, 72, 'BbcKey.cs was not read: it has 72 keys');
  assert.deepEqual(BBC_KEYS, keys);
});

test('every BBC key is pressed by a PC key or listed as not on a PC keyboard with a reason, never both', () => {
  const pressed = new Set(Object.values(PC_KEYS));
  for (const name of Object.values(PC_KEYS)) assert.ok(name in BBC_KEYS, `PC_KEYS names ${name}, which is not a BBC key`);
  for (const name of Object.keys(BBC_KEYS)) {
    const missing = name in NOT_ON_A_PC;
    assert.notEqual(pressed.has(name), missing, `${name} is ${missing ? 'both pressed and listed as missing' : 'neither pressed nor listed as missing'}`);
    if (missing) assert.ok(NOT_ON_A_PC[name].length > 20, `${name} has no reason`);
  }
  for (const name of Object.keys(NOT_ON_A_PC)) assert.ok(name in BBC_KEYS, `NOT_ON_A_PC names ${name}, which is not a BBC key`);
});

test('the keys a browser or a keyboard user needs are never taken: Tab, the function keys, Alt and the Command key', () => {
  for (const code of ['Tab', 'AltLeft', 'AltRight', 'MetaLeft', 'MetaRight', 'ContextMenu', ...Array.from({ length: 12 }, (_, i) => `F${i + 1}`)]) {
    assert.ok(!(code in PC_KEYS), `${code} is mapped to the machine`);
  }
});

test('keys go by place, not by legend: the BBC\'s colon is where a PC has its apostrophe, and its caret where a PC has =', () => {
  // The BBC's A row ends ; : ] and its number row - ^ (via.md section 3(b), and the keyboard's legends).
  assert.equal(PC_KEYS.Semicolon, 'Semicolon');
  assert.equal(PC_KEYS.Quote, 'Colon');
  assert.equal(PC_KEYS.Minus, 'Minus');
  assert.equal(PC_KEYS.Equal, 'Caret');
  assert.equal(PC_KEYS.BracketLeft, 'At');
  assert.equal(PC_KEYS.KeyQ, 'Q');
  assert.equal(PC_KEYS.Digit0, 'D0');
});

test('every key the machine has is on the on-screen keys exactly once, and has its legend', () => {
  const onScreen = ON_SCREEN_ROWS.flat();
  assert.deepEqual([...onScreen].sort(), Object.keys(enumKeys()).sort(), 'the on-screen keys are not BbcKey, each once');
  assert.deepEqual(Object.keys(LEGENDS).sort(), Object.keys(enumKeys()).sort(), 'a key has no legend, or a legend has no key');
  // The machine's legends, not the code's names (via.md section 3(b)).
  assert.equal(LEGENDS.ShiftLock, 'SHIFT LOCK');
  assert.equal(LEGENDS.Tab, 'TAB');
  assert.equal(LEGENDS.F0, 'f0');
  assert.equal(LEGENDS.D7, '7');
  assert.equal(spokenName('Left'), 'cursor left');
  assert.equal(spokenName('Q'), 'Q');
  assert.deepEqual(STICKY, ['Shift', 'Ctrl']);
});

test('the on-screen rows run left to right as the Model B\'s own keyboard does, as the page\'s photograph shows it', () => {
  // Read off site/src/assets/photos/bbc-micro.webp, not off the table: a sort-order check above
  // would pass with two keys swapped. The right SHIFT is left out (each key is on screen once),
  // and the cursor keys, DELETE and COPY are kept together in the last row.
  assert.deepEqual(ON_SCREEN_ROWS[0], ['F0', 'F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7', 'F8', 'F9']);
  assert.deepEqual(ON_SCREEN_ROWS[1], ['Escape', 'D1', 'D2', 'D3', 'D4', 'D5', 'D6', 'D7', 'D8', 'D9', 'D0', 'Minus', 'Caret', 'Backslash']);
  assert.deepEqual(ON_SCREEN_ROWS[2], ['Tab', 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', 'At', 'LeftBracket', 'Underscore']);
  assert.deepEqual(ON_SCREEN_ROWS[3], ['CapsLock', 'Ctrl', 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', 'Semicolon', 'Colon', 'RightBracket', 'Return']);
  assert.deepEqual(ON_SCREEN_ROWS[4], ['ShiftLock', 'Shift', 'Z', 'X', 'C', 'V', 'B', 'N', 'M', 'Comma', 'FullStop', 'Slash']);
  assert.deepEqual(ON_SCREEN_ROWS[5], ['Space']);
});

// ---- The characters, from the OS ROM ----

const os = readRom(bbcRoms().find((r) => r.rom === 'os'));

test('the characters each key types come from the OS ROM, and agree with the keyboard\'s legends', () => {
  const chars = bbcCharacters(os);
  // The same pairs BasicTests.TheTypingTableIsTheKeyboardsLegends checks on the C# side.
  assert.deepEqual(chars.get('"'), { key: 'D2', shift: true });
  assert.deepEqual(chars.get('*'), { key: 'Colon', shift: true });
  assert.deepEqual(chars.get('+'), { key: 'Semicolon', shift: true });
  assert.deepEqual(chars.get('='), { key: 'Minus', shift: true });
  assert.deepEqual(chars.get('?'), { key: 'Slash', shift: true });
  assert.deepEqual(chars.get('&'), { key: 'D6', shift: true });
  assert.deepEqual(chars.get('.'), { key: 'FullStop', shift: false });
  assert.deepEqual(chars.get('@'), { key: 'At', shift: false });
  assert.deepEqual(chars.get('P'), { key: 'P', shift: false });
  assert.equal(chars.has('p'), false);
});

test('the symbol table says where each symbol is on a PC keyboard, from the key map', () => {
  const rows = new Map(symbolTable(os).map((r) => [r.character, r]));
  assert.deepEqual(rows.get('*'), { character: '*', bbc: 'SHIFT and :', pc: "Shift and '" });
  assert.deepEqual(rows.get('='), { character: '=', bbc: 'SHIFT and -', pc: 'Shift and -' });
  assert.deepEqual(rows.get(':'), { character: ':', bbc: ':', pc: "'" });
  assert.deepEqual(rows.get('"'), { character: '"', bbc: 'SHIFT and 2', pc: 'Shift and 2' });
  for (const r of rows.values()) assert.doesNotMatch(r.character, /^[A-Z0-9 ]$/, 'letters, digits and space need no table');
  // Every symbol in the try-it program is in the table.
  for (const c of loadTryIt('bbc-micro').lines.join('')) if (!/[A-Z0-9 ]/.test(c)) assert.ok(rows.has(c), `the table does not say where ${c} is`);
});

// ---- The page script's keys ----

// The page script, imported with no panels on the page: it wires nothing until it finds one.
const savedDocument = Object.getOwnPropertyDescriptor(globalThis, 'document');
const listeners = [];
Object.defineProperty(globalThis, 'document', { configurable: true, writable: true, value: { querySelectorAll: () => [], hidden: false, addEventListener: (t, f) => listeners.push([t, f]) } });
const { keyboard, onScreenKeys, disc, machineState, lights } = await import(pathToFileURL(path.join(PUBLIC, 'bbc-micro.js')).href);
test.after(() => {
  if (savedDocument) Object.defineProperty(globalThis, 'document', savedDocument);
  else delete globalThis.document;
});

/** The `bbc:key` events a made-up panel was sent, each as [name, action]. */
const keyEvents = (events) => events.filter((e) => e.type === 'bbc:key').map((e) => [e.detail.name, e.detail.action]);

/** The screen, a machine that records its keys, and a way to fire key events at the screen. */
function wired() {
  const on = {};
  const screen = { addEventListener: (type, fn) => { on[type] = fn; } };
  const calls = [];
  const bbc = { KeyDown: (k) => calls.push(['down', k]), KeyUp: (k) => calls.push(['up', k]), Break: () => calls.push(['break']) };
  const events = [];
  const panel = { dataset: {}, dispatchEvent: (e) => events.push(e) };
  keyboard(screen, bbc, panel);
  const fire = (type, code, mods = {}) => {
    const event = { code, key: 'x', repeat: false, ctrlKey: false, altKey: false, metaKey: false, ...mods, prevented: false, preventDefault() { this.prevented = true; } };
    on[type](event);
    return event;
  };
  return { screen, calls, panel, fire, on, events };
}

test('a key goes down and up by its place: event.code, not event.key, picks the BBC key', () => {
  const { calls, fire } = wired();
  const down = fire('keydown', 'Quote', { key: '"' });
  fire('keyup', 'Quote', { key: '"' });
  assert.deepEqual(calls, [['down', BBC_KEYS.Colon], ['up', BBC_KEYS.Colon]]);
  assert.equal(down.prevented, true, 'a key the machine takes should not also scroll or type in the page');
});

test('Tab is left to the browser, and so is anything with Alt or the Command key', () => {
  const { calls, fire } = wired();
  assert.equal(fire('keydown', 'Tab').prevented, false);
  assert.equal(fire('keydown', 'KeyR', { altKey: true }).prevented, false);
  assert.equal(fire('keydown', 'KeyR', { metaKey: true }).prevented, false);
  assert.deepEqual(calls, []);
});

test('with Ctrl the machine gets the key, but the browser keeps its shortcut: Ctrl and R still reloads', () => {
  const { calls, fire } = wired();
  assert.equal(fire('keydown', 'ControlLeft', { ctrlKey: true }).prevented, false);
  assert.equal(fire('keydown', 'KeyR', { ctrlKey: true }).prevented, false);
  assert.deepEqual(calls, [['down', BBC_KEYS.Ctrl], ['down', BBC_KEYS.R]]);
});

test('a key held down repeats nothing: the OS makes its own repeats', () => {
  const { calls, fire } = wired();
  fire('keydown', 'KeyA');
  fire('keydown', 'KeyA', { repeat: true });
  fire('keydown', 'KeyA', { repeat: true });
  assert.deepEqual(calls, [['down', BBC_KEYS.A]]);
});

test('two PC keys on one BBC key: it comes up only when the last of them does', () => {
  const { calls, fire } = wired();
  fire('keydown', 'ShiftLeft');
  fire('keydown', 'ShiftRight');
  fire('keyup', 'ShiftLeft');
  assert.deepEqual(calls, [['down', BBC_KEYS.Shift]]);
  fire('keyup', 'ShiftRight');
  assert.deepEqual(calls, [['down', BBC_KEYS.Shift], ['up', BBC_KEYS.Shift]]);
});

test('focus leaving the screen lets go of every key held, so none sticks', () => {
  const { calls, fire, on, panel } = wired();
  on.focus();
  assert.equal(panel.dataset.typing, 'true');
  fire('keydown', 'KeyA');
  fire('keydown', 'ShiftLeft');
  on.blur();
  assert.equal(panel.dataset.typing, undefined);
  assert.deepEqual(calls.filter((c) => c[0] === 'up').map((c) => c[1]).sort(), [BBC_KEYS.A, BBC_KEYS.Shift].sort());
});

test('CAPS LOCK is one press for each key down, whatever the PC sends after', () => {
  const { calls, fire } = wired();
  fire('keydown', 'CapsLock');
  fire('keyup', 'CapsLock');
  assert.deepEqual(calls, [['down', BBC_KEYS.CapsLock], ['up', BBC_KEYS.CapsLock]]);
});

test('the Pause key is BREAK', () => {
  const { calls, fire } = wired();
  assert.equal(fire('keydown', 'Pause').prevented, true);
  assert.deepEqual(calls, [['break']]);
});

// ---- The on-screen keys ----

/** The panel's on-screen keys as made-up buttons, a machine that records its keys, and a tap. */
function onScreen() {
  const buttons = ON_SCREEN_ROWS.flat().map((key) => {
    const b = { dataset: { bbcKey: key }, disabled: true, attrs: {}, on: {} };
    b.addEventListener = (type, fn) => { b.on[type] = fn; };
    b.setAttribute = (name, value) => { b.attrs[name] = value; };
    return b;
  });
  const events = [];
  const panel = { querySelectorAll: () => buttons, dispatchEvent: (e) => events.push(e) };
  const calls = [];
  const bbc = { KeyDown: (k) => calls.push(['down', k]), KeyUp: (k) => calls.push(['up', k]), Break: () => calls.push(['break']) };
  const keys = onScreenKeys(panel, bbc);
  const button = (key) => buttons.find((b) => b.dataset.bbcKey === key);
  return { calls, button, tap: (key) => button(key).on.click(), buttons, keys, events, panel, bbc };
}

test('an on-screen key is a press and a release, and every key is enabled once the machine runs', () => {
  const { calls, tap, buttons } = onScreen();
  assert.ok(buttons.every((b) => !b.disabled));
  tap('Tab');
  tap('F0');
  assert.deepEqual(calls, [['down', BBC_KEYS.Tab], ['up', BBC_KEYS.Tab], ['down', BBC_KEYS.F0], ['up', BBC_KEYS.F0]]);
});

test('SHIFT and CTRL latch for the next key, show it, then let go; a second tap lets go without typing', () => {
  const { calls, tap, button } = onScreen();
  tap('Shift');
  assert.equal(button('Shift').attrs['aria-pressed'], 'true');
  assert.deepEqual(calls, [], 'SHIFT alone pressed nothing yet');
  tap('D2');
  assert.deepEqual(calls, [['down', BBC_KEYS.Shift], ['down', BBC_KEYS.D2], ['up', BBC_KEYS.D2], ['up', BBC_KEYS.Shift]]);
  assert.equal(button('Shift').attrs['aria-pressed'], 'false', 'SHIFT stayed latched after the key');
  calls.length = 0;
  tap('Ctrl');
  tap('Shift');
  tap('Ctrl');
  assert.equal(button('Ctrl').attrs['aria-pressed'], 'false');
  assert.equal(button('Shift').attrs['aria-pressed'], 'true');
  tap('A');
  assert.deepEqual(calls, [['down', BBC_KEYS.Shift], ['down', BBC_KEYS.A], ['up', BBC_KEYS.A], ['up', BBC_KEYS.Shift]]);
});

test('a latched SHIFT or CTRL lets go without typing when the page lets go of it (the screen taking the keyboard, or BREAK)', () => {
  const { calls, tap, button, keys } = onScreen();
  tap('Shift');
  tap('Ctrl');
  keys.letGo();
  assert.equal(button('Shift').attrs['aria-pressed'], 'false');
  assert.equal(button('Ctrl').attrs['aria-pressed'], 'false');
  assert.deepEqual(calls, [], 'letting go typed something');
  tap('A');
  assert.deepEqual(calls, [['down', BBC_KEYS.A], ['up', BBC_KEYS.A]], 'the latch was still held for the next key');
});

// ---- What the page tells a model: the keys, the lights and the chips ----

test('the PC keyboard tells the page each BBC key going down and up, by name, as bbc:key events', () => {
  const { fire, on, events } = wired();
  fire('keydown', 'KeyA');
  fire('keyup', 'KeyA');
  fire('keydown', 'ShiftLeft');
  fire('keydown', 'ShiftRight');
  fire('keyup', 'ShiftLeft');
  fire('keyup', 'ShiftRight');
  fire('keydown', 'Quote');
  on.blur();
  assert.ok(events.every((e) => e instanceof CustomEvent), 'an event was not a CustomEvent');
  assert.deepEqual(keyEvents(events), [
    ['A', 'down'], ['A', 'up'],
    ['Shift', 'down'], ['Shift', 'up'],
    ['Colon', 'down'], ['Colon', 'up'],
  ]);
});

test('CAPS LOCK and the Pause key are taps: CAPS LOCK is one press, and Pause is BREAK', () => {
  const { fire, events } = wired();
  fire('keydown', 'CapsLock');
  fire('keyup', 'CapsLock');
  fire('keydown', 'Pause');
  assert.deepEqual(keyEvents(events), [['CapsLock', 'tap'], ['Break', 'tap']]);
});

test('an on-screen key is a tap, and a latched SHIFT is latch-on, then latch-off once the key it shifts is tapped', () => {
  const { tap, events } = onScreen();
  tap('Q');
  tap('Shift');
  tap('D2');
  tap('Ctrl');
  tap('Ctrl');
  assert.deepEqual(keyEvents(events), [
    ['Q', 'tap'],
    ['Shift', 'latch-on'], ['D2', 'tap'], ['Shift', 'latch-off'],
    ['Ctrl', 'latch-on'], ['Ctrl', 'latch-off'],
  ]);
});

test('letting go of a latched key on screen tells the page it is let go', () => {
  const { tap, keys, events } = onScreen();
  tap('Shift');
  tap('Ctrl');
  keys.letGo();
  assert.deepEqual(keyEvents(events), [['Shift', 'latch-on'], ['Ctrl', 'latch-on'], ['Shift', 'latch-off'], ['Ctrl', 'latch-off']]);
});

test('panel.bbc.tap(name) does exactly what a click on that key does, a latched SHIFT held round it', () => {
  const clicked = onScreen();
  clicked.tap('Shift');
  clicked.tap('A');
  const tapped = onScreen();
  const state = machineState(tapped.bbc, tapped.keys);
  state.tap('Shift');
  state.tap('A');
  assert.deepEqual(tapped.calls, [['down', BBC_KEYS.Shift], ['down', BBC_KEYS.A], ['up', BBC_KEYS.A], ['up', BBC_KEYS.Shift]]);
  assert.deepEqual(tapped.calls, clicked.calls);
  assert.deepEqual(keyEvents(tapped.events), keyEvents(clicked.events));
  assert.equal(tapped.button('Shift').attrs['aria-pressed'], 'false', 'the on-screen SHIFT still shows latched');
  assert.throws(() => state.tap('Owl'), /no key called Owl/);
});

test('panel.bbc.tap(\'Break\') presses BREAK as the Break button does: a latched key let go, then the reset', () => {
  const { tap, events, calls, bbc, keys } = onScreen();
  tap('Shift');
  machineState(bbc, keys).tap('Break');
  assert.deepEqual(calls, [['break']], 'the latched SHIFT was pressed, or BREAK was not');
  assert.deepEqual(keyEvents(events), [['Shift', 'latch-on'], ['Shift', 'latch-off'], ['Break', 'tap']]);
});

test('leds() reads the host\'s three bits: the cassette motor, CAPS LOCK and SHIFT LOCK', () => {
  assert.deepEqual(lights(0), { motor: false, caps: false, shiftLock: false });
  assert.deepEqual(lights(1), { motor: true, caps: false, shiftLock: false });
  assert.deepEqual(lights(2), { motor: false, caps: true, shiftLock: false });
  assert.deepEqual(lights(4), { motor: false, caps: false, shiftLock: true });
  assert.deepEqual(lights(7), { motor: true, caps: true, shiftLock: true });
  let bits = 6;
  const state = machineState({ Leds: () => bits }, onScreen().keys);
  assert.deepEqual(state.leds(), { motor: false, caps: true, shiftLock: true });
  bits = 1;
  assert.deepEqual(state.leds(), { motor: true, caps: false, shiftLock: false }, 'leds() is not read afresh each time');
});

// Added in task 10 of the models plan (14 October 2026), for a mutation that survived: BbcLeds' two lock values
// swapped in C# passed every test, the C# tests reading the lights by name and the page's tests by made-up bits.
test('leds() decodes the bits the host sends: BbcLeds\' values in BbcChip.cs, which BbcHost.Leds() returns as they are', () => {
  const chip = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro', 'BbcChip.cs'), 'utf8');
  const body = chip.slice(chip.indexOf('public enum BbcLeds'));
  const value = (name) => Number(body.match(new RegExp(`^\\s+${name} = (\\d+),`, 'm'))?.[1]);
  const off = { motor: false, caps: false, shiftLock: false };
  assert.deepEqual(lights(value('CassetteMotor')), { ...off, motor: true });
  assert.deepEqual(lights(value('CapsLock')), { ...off, caps: true });
  assert.deepEqual(lights(value('ShiftLock')), { ...off, shiftLock: true });
  const host = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro.Wasm', 'Program.cs'), 'utf8');
  assert.match(host, /public static int Leds\(\) => \(int\)Machine\.Bus\.Leds;/);
});

test('accessCounts() and romSlot() are the host\'s, read afresh each time', () => {
  let counts = new Int32Array([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  let slot = 15;
  const state = machineState({ AccessCounts: () => counts, RomSlot: () => slot }, onScreen().keys);
  assert.deepEqual([...state.accessCounts()], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  assert.equal(state.romSlot(), 15);
  counts = new Int32Array([-1, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
  slot = 14;
  assert.equal(state.accessCounts()[0], -1);
  assert.equal(state.romSlot(), 14);
});

// ---- The sound worklet ----

/** bbc-audio.js in a made-up AudioWorkletGlobalScope at 48 kHz: returns a processor, a way to post to it and a way to pull frames. */
function worklet() {
  let Processor;
  const scope = {
    sampleRate: 48_000,
    AudioWorkletProcessor: class { constructor() { this.port = { onmessage: null }; } },
    registerProcessor: (name, cls) => { assert.equal(name, 'bbc-sound'); Processor = cls; },
    Float32Array,
  };
  vm.runInNewContext(fs.readFileSync(path.join(PUBLIC, 'bbc-audio.js'), 'utf8'), scope);
  const p = new Processor();
  // Samples from this realm are not the worklet realm's Float32Array, as a posted one would be: rebuild them there.
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

test('the worklet stays silent until enough sound is queued, then plays it with its steady level taken off', () => {
  const { post, pull } = worklet();
  assert.deepEqual(pull(1), new Array(128).fill(0), 'nothing queued is silence');
  // A square wave, 0 and 1 in turn every 24 samples, short of the 40 ms the worklet waits for.
  const square = (n) => Float32Array.from({ length: n }, (_, i) => ((i / 24) & 1 ? 1 : 0));
  post(square(ms(20)));
  assert.ok(pull(2).every((v) => Math.abs(v) < 1e-6), 'it played before enough was queued');
  post(square(ms(40)));
  const played = pull(8);
  assert.ok(Math.max(...played) > 0.2 && Math.min(...played) < -0.2, 'the square wave did not come out round zero');
});

test('running dry gives silence that fades rather than clicks, and then it waits to fill up again', () => {
  const { post, pull } = worklet();
  post(new Float32Array(ms(50)).fill(0.5));
  pull(Math.ceil(ms(50) / 128) + 40);
  const tail = pull(4);
  assert.ok(tail.every((v) => Math.abs(v) < 0.01), 'after running dry the output is not silent');
  post(new Float32Array(ms(10)).fill(1));
  assert.ok(pull(2).every((v) => Math.abs(v) < 0.01), 'after running dry it played before refilling');
});

test('a queue longer than the most allowed is cut back to the newest, so the sound never lags far behind', () => {
  const { p, post } = worklet();
  post(new Float32Array(ms(100)));
  post(new Float32Array(ms(100)));
  assert.ok(p.queued <= ms(150), `the queue holds ${p.queued} samples`);
  assert.equal(p.queued, ms(40));
});

test('flush empties the queue', () => {
  const { p, post } = worklet();
  post(new Float32Array(ms(60)));
  post({ flush: true });
  assert.equal(p.queued, 0);
});

// ---- The data the page is built from ----

test('the six parts left out are the ones the design names, each with its own issue', () => {
  assert.deepEqual(NOT_MODELLED.map((m) => m.issue), [33, 34, 35, 36, 37, 38]);
  const parts = NOT_MODELLED.map((m) => m.part).join(' / ');
  for (const part of ['6850 serial port', 'analogue-to-digital converter', 'joystick', 'Tube', 'cassette', 'printer port', '1 MHz bus']) assert.ok(parts.includes(part), `${part} is not listed`);
});

test('the try-it program is BASIC lines and what they print, and a broken file is refused', () => {
  const program = loadTryIt('bbc-micro');
  assert.ok(program.lines.length > 0 && program.shows.length > 0);
  assert.equal(program.lines.at(-1), 'RUN');
  const root = fs.mkdtempSync(path.join(process.cwd(), '.tmp-try-'));
  try {
    fs.mkdirSync(path.join(root, 'machines', 'bbc-micro'), { recursive: true });
    fs.writeFileSync(path.join(root, 'machines', 'bbc-micro', 'try-it.json'), JSON.stringify({ lines: ['10 PRINT "é"'], shows: ['x'] }));
    assert.throws(() => loadTryIt('bbc-micro', root), /not a line of plain printable text/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('megabytes are one decimal place, the British way', () => {
  assert.equal(megabytes(12_345_678), '12.3');
  assert.equal(megabytes(1_000_000), '1.0');
  assert.equal(megabytes(1_234_567_890), '1,234.6');
});

// ---- The page script's disc drive ----

/**
 * The disc drive's controls as plain objects, a machine that records what goes in the drive,
 * and a fetch that answers only when the test says so, so a library disc can be caught in
 * the middle of its download.
 */
function drive() {
  const element = (extra = {}) => {
    const on = {};
    return { disabled: true, hidden: false, textContent: '', dataset: {}, checked: false, value: '', files: [], on, addEventListener: (type, fn) => { on[type] = fn; }, click() { on.click?.(); }, ...extra };
  };
  const els = {
    '[data-bbc-insert]': element(), '[data-bbc-file]': element(), '[data-bbc-blank]': element(), '[data-bbc-save]': element(),
    '[data-bbc-eject]': element(), '[data-bbc-protect]': element(), '[data-bbc-drive]': element(),
    '[data-bbc-preset]': element({ value: 'onslaught' }), '[data-bbc-preset-run]': element(), '[data-bbc-preset-insert]': element(),
    '[data-bbc-preset-disc="onslaught"]': element({ dataset: { title: 'Onslaught', licence: 'MIT' } }),
  };
  const panel = { dataset: { base: '/machines/bbc-micro/' }, querySelector: (q) => els[q] };
  const inserted = [];
  const calls = [];
  const bbc = {
    InsertDisc: (drive, data, dsd, readOnly) => { inserted.push([...data]); return 40; },
    BlankDisc: () => inserted.push('blank'),
    SetDiscReadOnly: () => {}, EjectDisc: () => inserted.push('eject'), ShiftBreak: () => calls.push('shift-break'),
  };
  const screen = { focus: (o) => calls.push(['focus', o]), scrollIntoView: (o) => calls.push(['scroll', o]) };
  const fetches = [];
  globalThis.fetch = (url) => new Promise((resolve) => fetches.push({ url, answer: (bytes) => resolve({ ok: true, arrayBuffer: async () => new Uint8Array(bytes).buffer }) }));
  disc(panel, bbc, { screen, letGo: () => calls.push('let-go') });
  const file = (name, bytes) => ({ name, size: bytes.length, arrayBuffer: async () => new Uint8Array(bytes).buffer });
  const settle = () => new Promise((r) => setTimeout(r, 0));
  return { els, inserted, calls, fetches, file, settle, panel };
}

test('Insert and run puts the library disc in, starts it with SHIFT and BREAK, and brings the screen into view', async () => {
  const { els, inserted, calls, fetches, settle, panel } = drive();
  els['[data-bbc-preset-run]'].click();
  assert.equal(fetches.length, 1);
  assert.equal(fetches[0].url, '/machines/bbc-micro/discs/onslaught.ssd');
  fetches[0].answer([1, 2, 3]);
  await settle();
  assert.deepEqual(inserted, [[1, 2, 3]]);
  assert.equal(panel.dataset.disc, 'onslaught.ssd');
  assert.match(els['[data-bbc-drive]'].textContent, /^In drive 0: Onslaught \(MIT\), 40 tracks/);
  // Focus without a jump, then into view only as far as needed, at the page's own scroll speed.
  assert.deepEqual(calls, ['let-go', 'shift-break', ['focus', { preventScroll: true }], ['scroll', { block: 'nearest', behavior: 'auto' }]]);
});

test('Insert in drive 0 puts the library disc in and starts nothing', async () => {
  const { els, inserted, calls, fetches, settle } = drive();
  els['[data-bbc-preset-insert]'].click();
  fetches[0].answer([9]);
  await settle();
  assert.deepEqual(inserted, [[9]]);
  assert.deepEqual(calls, []);
  assert.match(els['[data-bbc-drive]'].textContent, /type \*CAT to list it/);
});

test('a file chosen while a library disc downloads stays in the drive: the download that finishes later is dropped', async () => {
  const { els, inserted, fetches, file, settle, panel } = drive();
  els['[data-bbc-preset-run]'].click();
  // The visitor picks a file of their own before the library disc arrives.
  els['[data-bbc-file]'].files = [file('mine.ssd', [7, 7])];
  els['[data-bbc-file]'].on.change();
  await settle();
  fetches[0].answer([1, 2, 3]);
  await settle();
  assert.deepEqual(inserted, [[7, 7]], 'the library disc replaced the visitor\'s file');
  assert.equal(panel.dataset.disc, 'mine.ssd');
  assert.match(els['[data-bbc-drive]'].textContent, /^In drive 0: mine\.ssd/);
});

test('a blank disc made while a library disc downloads stays in the drive, and a refused file cancels nothing', async () => {
  const { els, inserted, fetches, file, settle, panel } = drive();
  els['[data-bbc-preset-run]'].click();
  els['[data-bbc-blank]'].click();
  fetches[0].answer([1]);
  await settle();
  assert.deepEqual(inserted, ['blank']);
  assert.equal(panel.dataset.disc, 'blank.ssd');
  // A file the drive refuses (not a disc image) does not stop a library disc already on its way.
  els['[data-bbc-preset-run]'].click();
  els['[data-bbc-file]'].files = [file('notes.txt', [0])];
  els['[data-bbc-file]'].on.change();
  fetches[1].answer([5]);
  await settle();
  assert.deepEqual(inserted, ['blank', [5]]);
  assert.equal(panel.dataset.disc, 'onslaught.ssd');
});

test('a file read that fails after a later insert has started says nothing: the later disc keeps the status line', async () => {
  const { els, inserted, file, settle, panel } = drive();
  let fail;
  const slow = { name: 'slow.ssd', size: 2, arrayBuffer: () => new Promise((_, reject) => { fail = reject; }) };
  els['[data-bbc-file]'].files = [slow];
  els['[data-bbc-file]'].on.change();
  els['[data-bbc-file]'].files = [file('fast.ssd', [3, 3])];
  els['[data-bbc-file]'].on.change();
  await settle();
  fail(new Error('the read failed'));
  await settle();
  assert.deepEqual(inserted, [[3, 3]]);
  assert.equal(panel.dataset.disc, 'fast.ssd');
  assert.match(els['[data-bbc-drive]'].textContent, /^In drive 0: fast\.ssd/);
});

// ---- The page: Start, panel.bbc and its events, for the models ----
//
// The page script run as the browser runs it, from its own `prepare` to the
// machine running, in a fake browser just big enough for it, as
// nes-panel.test.mjs runs the NES's: made-up elements, a fake .NET runtime at
// <base>_framework/dotnet.js that hands over a made-up BbcHost, and a fetch
// that gives three ROMs. The run loop's animation frames are never turned.

const pageDir = fs.mkdtempSync(path.join(tmpdir(), 'bbc-page-'));
fs.mkdirSync(path.join(pageDir, '_framework'), { recursive: true });
fs.writeFileSync(path.join(pageDir, '_framework', 'dotnet.js'), 'export const dotnet = { create: () => globalThis.fakeBbcDotnet.create() };\n');
const PAGE_BASE = `${pathToFileURL(pageDir).href}/`;
test.after(() => fs.rmSync(pageDir, { recursive: true, force: true }));

/** A made-up element: listeners, a dataset, and click() dispatching a click. */
class FakeElement extends EventTarget {
  constructor(props = {}) {
    super();
    Object.assign(this, { dataset: {}, style: {}, disabled: true, hidden: false, textContent: '' }, props);
  }
  focus() {}
  blur() {}
  click() { this.dispatchEvent(new Event('click')); }
  setAttribute(name, value) { this[name] = value; }
}

/** Waits until `done()` is true, for at most `ms` of real time (as nes-panel.test.mjs, issue 91). */
async function until(done, ms = 10_000) {
  const end = Date.now() + ms;
  while (!done() && Date.now() < end) await new Promise((resolve) => setTimeout(resolve, 2));
}

let pages = 0;

/** The BBC Micro's page with one panel, one Break button and the on-screen A key, and `host` as its machine. */
async function bbcPage(t, host) {
  const saved = ['document', 'window', 'fetch', 'requestAnimationFrame', 'cancelAnimationFrame', 'ResizeObserver'].map((k) => [k, Object.getOwnPropertyDescriptor(globalThis, k)]);
  t.after(() => {
    for (const [k, d] of saved) {
      if (d) Object.defineProperty(globalThis, k, d);
      else delete globalThis[k];
    }
    delete globalThis.fakeBbcDotnet;
  });
  const elements = new Map();
  const breakButton = new FakeElement();
  const keyA = new FakeElement({ dataset: { bbcKey: 'A' } });
  const panel = new FakeElement({ dataset: { download: '3.1 MB', base: PAGE_BASE, clockMhz: '2', roms: 'os.rom basic.rom dfs.rom' } });
  // The site header, as tall as the test says, and the stylesheet's custom properties on the panel.
  const header = { height: 130, getBoundingClientRect() { return { height: this.height }; } };
  panel.style = { setProperty(name, value) { this[name] = value; } };
  const watchers = [];
  panel.querySelectorAll = (selector) => (selector === '[data-bbc-break]' ? [breakButton] : selector === '[data-bbc-key]' ? [keyA] : []);
  panel.querySelector = (selector) => {
    if (!elements.has(selector)) {
      elements.set(selector, new FakeElement(selector === '[data-bbc-canvas]'
        ? { getContext: () => ({ createImageData: (w, h) => ({ data: new Uint8ClampedArray(w * h * 4) }), putImageData() {} }) }
        : {}));
    }
    return elements.get(selector);
  };
  const events = [];
  for (const type of ['bbc:ready', 'bbc:start', 'bbc:key']) {
    panel.addEventListener(type, (event) => events.push([type, event.detail ?? null, Boolean(panel.bbc)]));
  }
  const define = (k, value) => Object.defineProperty(globalThis, k, { value, configurable: true, writable: true });
  define('document', { hidden: false, querySelector: (s) => (s === '.top' ? header : null), querySelectorAll: (s) => (s === '[data-bbc]' ? [panel] : []), addEventListener() {}, removeEventListener() {} });
  define('window', { addEventListener() {} });
  define('ResizeObserver', class { constructor(callback) { watchers.push(callback); } observe() {} });
  define('fetch', async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(16) }));
  define('requestAnimationFrame', () => 1);
  define('cancelAnimationFrame', () => {});
  globalThis.fakeBbcDotnet = {
    create: async () => ({ getAssemblyExports: async () => ({ BbcHost: host }), setModuleImports() {} }),
  };
  await import(`${pathToFileURL(path.join(PUBLIC, 'bbc-micro.js')).href}?page=${++pages}`);
  const start = async () => {
    panel.querySelector('[data-bbc-start]').click();
    await until(() => panel.dataset.state === 'running');
    assert.equal(panel.dataset.state, 'running', `the page did not start: ${panel.querySelector('[data-bbc-status]').textContent}`);
  };
  return { panel, events, start, breakButton, keyA, header, resize: () => watchers.forEach((w) => w()) };
}

/** A BbcHost for the whole page: what Start and the panel's controls call, recorded. */
function pageHost() {
  const calls = [];
  return {
    calls,
    Load: (...args) => calls.push(['Load', args.length]),
    Cycles: () => 0, Run: () => 0, Frames: () => 0, Picture: () => 0, Sound: () => 0, DropSound: () => 0,
    ScreenWidth: () => 640, ScreenHeight: () => 512, ScreenRow: (row) => `row ${row}`,
    KeyDown: (k) => calls.push(['down', k]), KeyUp: (k) => calls.push(['up', k]), Break: () => calls.push(['break']),
    Leds: () => 2, AccessCounts: () => new Int32Array([5, 0, 0, 7, 9, 11, 3, 0, 0, 1]), RomSlot: () => 15,
  };
}

test('Start runs the machine and then sends bbc:ready and bbc:start, with panel.bbc in place for both', async (t) => {
  const host = pageHost();
  const { panel, events, start } = await bbcPage(t, host);
  assert.equal(panel.bbc, undefined, 'panel.bbc was there before Start');
  await start();
  assert.deepEqual(events.map((e) => [e[0], e[2]]), [['bbc:ready', true], ['bbc:start', true]]);
  assert.equal(panel.bbc.host, host);
  assert.equal(panel.bbc.screenRow(3), 'row 3');
  assert.deepEqual(panel.bbc.leds(), { motor: false, caps: true, shiftLock: false });
  assert.deepEqual([...panel.bbc.accessCounts()], [5, 0, 0, 7, 9, 11, 3, 0, 0, 1]);
  assert.equal(panel.bbc.romSlot(), 15);
});

test('on the running page the on-screen keys, the Break button and panel.bbc.tap all tell the page as bbc:key', async (t) => {
  const host = pageHost();
  const { panel, events, start, breakButton, keyA } = await bbcPage(t, host);
  await start();
  events.length = 0;
  keyA.click();
  panel.bbc.tap('A');
  breakButton.click();
  panel.bbc.tap('Break');
  assert.deepEqual(events.map((e) => [e[0], e[1]]), [
    ['bbc:key', { name: 'A', action: 'tap' }], ['bbc:key', { name: 'A', action: 'tap' }],
    ['bbc:key', { name: 'Break', action: 'tap' }], ['bbc:key', { name: 'Break', action: 'tap' }],
  ]);
  assert.deepEqual(host.calls.filter((c) => c[0] !== 'Load'), [
    ['down', BBC_KEYS.A], ['up', BBC_KEYS.A], ['down', BBC_KEYS.A], ['up', BBC_KEYS.A], ['break'], ['break'],
  ]);
});

test('the page gives the stylesheet the site header\'s height as --bbc-pin, from the start and again when the header changes, so the pinned screen sits just under it', async (t) => {
  const { panel, header, resize } = await bbcPage(t, pageHost());
  assert.equal(panel.style['--bbc-pin'], '130px');
  // A tablet's header is one line: the links no longer wrap.
  header.height = 96.5;
  resize();
  assert.equal(panel.style['--bbc-pin'], '97px');
});
