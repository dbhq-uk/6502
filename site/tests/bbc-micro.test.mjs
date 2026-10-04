import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { register } from 'node:module';
import { pathToFileURL } from 'node:url';
import { REPO_ROOT, applyPreview, loadRegistry } from '../src/lib/registry.mjs';
import { loadTryIt } from '../src/lib/machines.mjs';
import { readRom } from '../src/lib/machines.mjs';
import { bbcRoms } from '../src/lib/pins.mjs';
import { NOT_MODELLED, megabytes, symbolTable } from '../src/lib/bbc-micro.mjs';
import { BBC_KEYS, PC_KEYS, NOT_ON_A_PC, bbcCharacters } from '../public/bbc-keys.js';

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
const { keyboard } = await import(pathToFileURL(path.join(PUBLIC, 'bbc-micro.js')).href);
test.after(() => {
  if (savedDocument) Object.defineProperty(globalThis, 'document', savedDocument);
  else delete globalThis.document;
});

/** The screen, a machine that records its keys, and a way to fire key events at the screen. */
function wired() {
  const on = {};
  const screen = { addEventListener: (type, fn) => { on[type] = fn; } };
  const calls = [];
  const bbc = { KeyDown: (k) => calls.push(['down', k]), KeyUp: (k) => calls.push(['up', k]), Break: () => calls.push(['break']) };
  const panel = { dataset: {} };
  keyboard(screen, bbc, panel);
  const fire = (type, code, mods = {}) => {
    const event = { code, key: 'x', repeat: false, ctrlKey: false, altKey: false, metaKey: false, ...mods, prevented: false, preventDefault() { this.prevented = true; } };
    on[type](event);
    return event;
  };
  return { screen, calls, panel, fire, on };
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

test('a preview lays its fields over the registry\'s for the machines it names, and cannot add one', () => {
  const registry = { machines: [{ id: 'a', status: 'planned', name: 'A' }, { id: 'b', status: 'planned' }], chips: [] };
  const seen = applyPreview(registry, { machines: { a: { status: 'running', notes: 'n' } } });
  assert.deepEqual(seen.machines[0], { id: 'a', status: 'running', name: 'A', notes: 'n' });
  assert.equal(seen.machines[1], registry.machines[1]);
  assert.equal(registry.machines[0].status, 'planned', 'the registry itself was changed');
  assert.throws(() => applyPreview(registry, { machines: { c: {} } }), /not in the registry/);
});

test('the real registry is read as it is unless a preview is asked for, and the BBC Micro\'s preview only switches it on', () => {
  const plain = loadRegistry(undefined, '');
  assert.equal(plain.machines.find((m) => m.id === 'bbc-micro').status, 'planned');
  const previewed = loadRegistry(undefined, 'tests/fixtures/bbc-micro-running.json');
  const bbc = previewed.machines.find((m) => m.id === 'bbc-micro');
  assert.equal(bbc.status, 'running');
  assert.equal(bbc.acceptance, 'BbcAcceptanceTests');
  assert.ok(fs.existsSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.BbcMicro.Tests', 'BbcAcceptanceTests.cs')));
  // Every other machine is the registry's own.
  assert.deepEqual(previewed.machines.filter((m) => m.id !== 'bbc-micro'), plain.machines.filter((m) => m.id !== 'bbc-micro'));
});
