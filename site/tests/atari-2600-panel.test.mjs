import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { register } from 'node:module';
import { pathToFileURL } from 'node:url';
import { REPO_ROOT } from '../src/lib/registry.mjs';

// The Atari 2600 panel's own parts, without a browser: the key, gamepad and
// switch maps against the machine's bits, the keyboard and the switches played
// with made-up events, the picture's window and shape in each region against
// Region.cs and region.md, the PAL suggestion on made-up line counts, the size
// refusal, the sound's offset taken out, and the shared ring's drop rule. Then
// the panel's markup, rendered, against what the script looks for; the host's
// exports against what the script calls; and the page script run as the
// browser runs it, in a fake browser, from Start to a region change. Task 14's
// page test checks the built page, and its browser check runs the panel in a
// real browser.

const PUBLIC = path.join(process.cwd(), 'public');
const SRC = path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Atari2600');

// The page scripts import each other by their site paths, /machine-host.js;
// here those resolve to the files in public/.
register(`data:text/javascript,${encodeURIComponent(`
  export async function resolve(specifier, context, next) {
    if (/^\\/[\\w-]+\\.js$/.test(specifier)) return next(${JSON.stringify(pathToFileURL(PUBLIC).href)} + specifier, context);
    return next(specifier, context);
  }`)}`);

const vcs = await import(pathToFileURL(path.join(PUBLIC, 'atari-2600.js')).href);
const ring = await import(pathToFileURL(path.join(PUBLIC, 'sample-worklet.js')).href);
const nesAudio = await import(pathToFileURL(path.join(PUBLIC, 'nes-audio.js')).href);
const picker = await import(pathToFileURL(path.join(PUBLIC, 'cartridge-picker.js')).href);
const pads = await import(pathToFileURL(path.join(PUBLIC, 'gamepads.js')).href);
const regionLine = await import(pathToFileURL(path.join(PUBLIC, 'region-line.js')).href);
const soundOut = await import(pathToFileURL(path.join(PUBLIC, 'sound-output.js')).href);
const {
  STICK, SWITCHES, HELD_SWITCHES, STICK_KEYS, SWITCH_KEYS, KEY_NAMES, GAMEPAD_STICK, GAMEPAD_SWITCHES, WIDTH, WINDOWS, SUGGEST_FRAMES,
  keyState, keyboard, gamepadStick, gamepadSwitches, joystickMasks, switchMask, toggleSwitch, switchLabel,
  pictureShape, showShape, median, suggestsPal, linesLine, suggestionLine, loadCartridge, cartridgeLine, badFileLine, dcBlocker,
} = vcs;

const source = (file) => fs.readFileSync(path.join(SRC, file), 'utf8');
const driver = fs.readFileSync(path.join(PUBLIC, 'atari-2600.js'), 'utf8');

/** A C# enum's members and values, read from its declaration in `file`. */
const csEnum = (file, name) => {
  const body = new RegExp(`public enum ${name}\\s*\\{([^}]*)\\}`).exec(source(file))?.[1];
  assert.ok(body, `${file} no longer declares enum ${name} in the shape this test reads`);
  return Object.fromEntries([...body.matchAll(/(\w+) = (\d+),/g)].filter((m) => m[1] !== 'None').map((m) => [m[1], Number(m[2])]));
};

// ---- The bits, the keys and the gamepads ----

test('the page\'s joystick and switch bits are the machine\'s Joystick and ConsoleSwitches', () => {
  assert.deepEqual(STICK, csEnum('Controllers.cs', 'Joystick'));
  assert.deepEqual(SWITCHES, csEnum('Controllers.cs', 'ConsoleSwitches'));
  assert.deepEqual(HELD_SWITCHES, ['Reset', 'Select']);
});

test('the key map: the arrows and Space are joystick 1, W A S D and F joystick 2, R and T hold Reset and Select, C 1 and 2 flip the others', () => {
  const sticks = Object.fromEntries(Object.entries(STICK_KEYS).map(([code, k]) => [code, `${k.stick}:${k.input}`]));
  assert.deepEqual(sticks, {
    ArrowUp: '0:Up', ArrowDown: '0:Down', ArrowLeft: '0:Left', ArrowRight: '0:Right', Space: '0:Fire',
    KeyW: '1:Up', KeyS: '1:Down', KeyA: '1:Left', KeyD: '1:Right', KeyF: '1:Fire',
  });
  assert.deepEqual(SWITCH_KEYS, { KeyR: 'Reset', KeyT: 'Select', KeyC: 'BlackAndWhite', Digit1: 'LeftDifficultyA', Digit2: 'RightDifficultyA' });
  for (const k of Object.values(STICK_KEYS)) assert.equal(typeof STICK[k.input], 'number');
  for (const s of Object.values(SWITCH_KEYS)) assert.equal(typeof SWITCHES[s], 'number');
  // The panel names each key from the same table, each once.
  assert.deepEqual(Object.keys(KEY_NAMES).sort(), [...Object.keys(STICK_KEYS), ...Object.keys(SWITCH_KEYS)].sort());
});

test('the keys a browser or a keyboard user needs are never taken: Tab, Escape, the function keys, Alt and the Command key', () => {
  for (const code of ['Tab', 'Escape', 'AltLeft', 'AltRight', 'MetaLeft', 'MetaRight', 'ContextMenu', ...Array.from({ length: 12 }, (_, i) => `F${i + 1}`)]) {
    assert.ok(!(code in STICK_KEYS) && !(code in SWITCH_KEYS), `${code} is mapped to the machine`);
  }
});

test('keys held are each joystick\'s inputs and the held switches; every input at once is every bit at once', () => {
  assert.deepEqual(keyState([]), { sticks: [0, 0], switches: 0 });
  assert.deepEqual(keyState(['ArrowUp', 'ArrowDown']), { sticks: [STICK.Up | STICK.Down, 0], switches: 0 });
  assert.deepEqual(keyState(['KeyF', 'KeyA']), { sticks: [0, STICK.Fire | STICK.Left], switches: 0 });
  assert.deepEqual(keyState(['KeyR', 'KeyT']), { sticks: [0, 0], switches: SWITCHES.Reset | SWITCHES.Select });
  // A switch that stays is flipped by its key's press, not held by it.
  assert.deepEqual(keyState(['KeyC', 'Digit1', 'Digit2']), { sticks: [0, 0], switches: 0 });
  assert.deepEqual(keyState(Object.keys(STICK_KEYS)), { sticks: [31, 31], switches: 0 });
});

test('the gamepad map: the d-pad and the left stick past half way move, button 0 fires, 9 is Reset and 8 Select', () => {
  assert.deepEqual(GAMEPAD_STICK, { 0: 'Fire', 12: 'Up', 13: 'Down', 14: 'Left', 15: 'Right' });
  assert.deepEqual(GAMEPAD_SWITCHES, { 8: 'Select', 9: 'Reset' });
  const pad = (pressed = [], axes = [0, 0]) => ({ axes, buttons: Array.from({ length: 17 }, (_, i) => ({ pressed: pressed.includes(i) })) });
  assert.equal(gamepadStick(pad([0])), STICK.Fire);
  assert.equal(gamepadStick(pad([12])), STICK.Up);
  assert.equal(gamepadStick(pad([13])), STICK.Down);
  assert.equal(gamepadStick(pad([14])), STICK.Left);
  assert.equal(gamepadStick(pad([15])), STICK.Right);
  assert.equal(gamepadStick(pad([], [-0.9, 0])), STICK.Left);
  assert.equal(gamepadStick(pad([], [0.9, 0])), STICK.Right);
  assert.equal(gamepadStick(pad([], [0, -0.9])), STICK.Up);
  assert.equal(gamepadStick(pad([], [0, 0.9])), STICK.Down);
  // Half way is not past half way; a pad with no axes is read by its buttons.
  assert.equal(gamepadStick(pad([], [0.5, -0.5])), 0);
  assert.equal(gamepadStick({ buttons: [{ pressed: true }] }), STICK.Fire);
  assert.equal(gamepadSwitches(pad([9])), SWITCHES.Reset);
  assert.equal(gamepadSwitches(pad([8])), SWITCHES.Select);
  // Buttons the maps do not name do nothing; everything at once is every bit (Review Focus 4).
  assert.equal(gamepadStick(pad([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 16])), 0);
  assert.equal(gamepadStick(pad([0, 12, 13, 14, 15], [1, 1])), 31);
  assert.equal(gamepadSwitches(pad([8, 9])), SWITCHES.Reset | SWITCHES.Select);
  assert.deepEqual(pads.stickDirections(pad([], [-1, -1])), ['Up', 'Left']);
});

test('gamepads go by index: the first joins joystick 1 with its keys and the touch pad, the second joystick 2 with its keys', () => {
  const pad = (...pressed) => ({ buttons: Array.from({ length: 16 }, (_, i) => ({ pressed: pressed.includes(i) })) });
  const none = { keys: [0, 0], touch: 0 };
  assert.deepEqual(joystickMasks(none, []), [0, 0]);
  assert.deepEqual(joystickMasks({ keys: [STICK.Up, STICK.Left], touch: STICK.Fire }, [pad(15), pad(0)]), [STICK.Up | STICK.Fire | STICK.Right, STICK.Left | STICK.Fire]);
  // A pad unplugged leaves a hole in the browser's list; a third pad is not a joystick here.
  assert.deepEqual(joystickMasks(none, [null, pad(14), pad(0)]), [0, STICK.Left]);
});

// ---- The switches ----

test('the switches that stay flip on each press and stay; Reset and Select are never flipped, only held', () => {
  let toggled = 0;
  toggled = toggleSwitch(toggled, 'BlackAndWhite');
  assert.equal(toggled, SWITCHES.BlackAndWhite);
  toggled = toggleSwitch(toggled, 'LeftDifficultyA');
  toggled = toggleSwitch(toggled, 'RightDifficultyA');
  assert.equal(toggled, SWITCHES.BlackAndWhite | SWITCHES.LeftDifficultyA | SWITCHES.RightDifficultyA);
  toggled = toggleSwitch(toggled, 'BlackAndWhite');
  assert.equal(toggled, SWITCHES.LeftDifficultyA | SWITCHES.RightDifficultyA);
  assert.equal(toggleSwitch(0, 'Reset'), 0);
  assert.equal(toggleSwitch(0, 'Select'), 0);
  assert.equal(toggleSwitch(0, 'Power'), 0);
});

test('the switches sent are the ones that stay and the held ones from every source, all at once included', () => {
  const pad = (...pressed) => ({ buttons: Array.from({ length: 16 }, (_, i) => ({ pressed: pressed.includes(i) })) });
  const at = (o) => ({ toggled: 0, keySwitches: 0, buttons: 0, ...o });
  assert.equal(switchMask(at({}), []), 0);
  assert.equal(switchMask(at({ toggled: SWITCHES.BlackAndWhite }), []), SWITCHES.BlackAndWhite);
  assert.equal(switchMask(at({ keySwitches: SWITCHES.Reset }), []), SWITCHES.Reset);
  assert.equal(switchMask(at({ buttons: SWITCHES.Select }), []), SWITCHES.Select);
  assert.equal(switchMask(at({}), [null, pad(9)]), SWITCHES.Reset);
  assert.equal(switchMask(at({ toggled: 28, keySwitches: 1, buttons: 2 }), [pad(8, 9)]), 31);
});

test('each switch\'s button says where it is', () => {
  assert.equal(switchLabel('BlackAndWhite', 0), 'Colour');
  assert.equal(switchLabel('BlackAndWhite', SWITCHES.BlackAndWhite), 'Black and white');
  assert.equal(switchLabel('LeftDifficultyA', 0), 'Left difficulty B');
  assert.equal(switchLabel('LeftDifficultyA', SWITCHES.LeftDifficultyA), 'Left difficulty A');
  assert.equal(switchLabel('RightDifficultyA', SWITCHES.RightDifficultyA), 'Right difficulty A');
  assert.equal(switchLabel('RightDifficultyA', SWITCHES.LeftDifficultyA), 'Right difficulty B');
});

/** The screen as a made-up element, and a way to fire key events at it. */
function wiredKeys() {
  const on = {};
  const screen = { blurred: 0, addEventListener: (type, fn) => { on[type] = fn; }, blur() { this.blurred++; } };
  const held = [];
  const toggles = [];
  keyboard(screen, { onHeld: (codes) => held.push(keyState(codes)), onToggle: (name) => toggles.push(name) });
  const fire = (type, code, mods = {}) => {
    const event = { code, repeat: false, ctrlKey: false, altKey: false, metaKey: false, ...mods, prevented: false, preventDefault() { this.prevented = true; } };
    on[type](event);
    return event;
  };
  return { on, held, toggles, fire, screen };
}

test('a key held is its input held, the page takes it from the browser, and opposite directions go together as pressed', () => {
  const { held, fire } = wiredKeys();
  assert.equal(fire('keydown', 'ArrowLeft').prevented, true);
  fire('keydown', 'ArrowRight');
  fire('keydown', 'KeyW');
  fire('keydown', 'KeyS');
  assert.deepEqual(held.at(-1).sticks, [STICK.Left | STICK.Right, STICK.Up | STICK.Down]);
  assert.equal(fire('keydown', 'Space').prevented, true, 'Space would scroll the page');
  fire('keyup', 'ArrowLeft');
  assert.deepEqual(held.at(-1).sticks, [STICK.Right | STICK.Fire, STICK.Up | STICK.Down]);
});

test('R and T hold Reset and Select while down; C, 1 and 2 flip their switch once a press, not on repeats nor on letting go', () => {
  const { held, toggles, fire } = wiredKeys();
  fire('keydown', 'KeyR');
  assert.equal(held.at(-1).switches, SWITCHES.Reset);
  fire('keyup', 'KeyR');
  assert.equal(held.at(-1).switches, 0);
  fire('keydown', 'KeyC');
  fire('keydown', 'KeyC', { repeat: true });
  fire('keyup', 'KeyC');
  fire('keydown', 'Digit1');
  fire('keyup', 'Digit1');
  fire('keydown', 'Digit2');
  assert.deepEqual(toggles, ['BlackAndWhite', 'LeftDifficultyA', 'RightDifficultyA']);
  assert.ok(toggles.every((t) => !HELD_SWITCHES.includes(t)));
});

test('Tab, Alt and the Command key are left to the browser; Escape lets go of the screen; focus leaving lets go of every key held', () => {
  const { held, toggles, fire, on, screen } = wiredKeys();
  assert.equal(fire('keydown', 'Tab').prevented, false);
  assert.equal(fire('keydown', 'KeyC', { altKey: true }).prevented, false);
  assert.equal(fire('keydown', 'ArrowUp', { metaKey: true }).prevented, false);
  assert.deepEqual(held, []);
  assert.deepEqual(toggles, []);
  fire('keydown', 'Escape');
  assert.equal(screen.blurred, 1);
  fire('keydown', 'ArrowUp');
  fire('keydown', 'KeyR');
  on.blur();
  assert.deepEqual(held.at(-1), { sticks: [0, 0], switches: 0 });
});

// ---- The picture ----

/** Region.cs's two regions, in order NTSC then PAL, as the numbers it passes to its constructor. */
const regionNumbers = () => {
  const text = source('Region.cs');
  const read = (name) => [...text.matchAll(new RegExp(`${name}: (\\d+),`, 'g'))].map((m) => Number(m[1]));
  const [first, lines, across, down, nominal] = ['firstShownLine', 'shownLines', 'pixelWidth', 'pixelHeight', 'nominalLines'].map(read);
  assert.deepEqual([first, lines, across, down, nominal].map((a) => a.length), [2, 2, 2, 2, 2], 'Region.cs no longer gives its numbers in the shape this test reads');
  return Object.fromEntries(['NTSC', 'PAL'].map((r, i) => [r, { first: first[i], lines: lines[i], pixel: [across[i], down[i]], nominal: nominal[i] }]));
};

test('each region\'s window, pixel shape and nominal lines are Region.cs\'s, and the width is FrameBuffer.Width', () => {
  assert.deepEqual(WINDOWS, regionNumbers());
  assert.match(source('FrameBuffer.cs'), new RegExp(`public const int Width = ${WIDTH};`));
});

test('the picture is shown in the region\'s pixel shape, within 1 per cent of region.md\'s: 12:7 on NTSC, 2.079 on PAL', () => {
  // region.md section 4 and its worked example 3: NTSC 274.3 wide for 228 tall, PAL 332.6 for 264.
  const sheet = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'atari-2600', 'facts', 'region.md'), 'utf8');
  assert.match(sheet, /NTSC 24\/7 \/ 2 = \*\*12:7\*\*/);
  assert.match(sheet, /PAL 4\.1586 \/ 2 = \*\*2\.079\*\*/);
  const within = (actual, expected) => Math.abs(actual - expected) <= expected * 0.01;
  for (const [region, pixel, lines] of [['NTSC', 12 / 7, 228], ['PAL', 2.079, 264]]) {
    const { width, height } = pictureShape(region);
    assert.equal(height, lines);
    assert.ok(within(width / WIDTH, pixel), `${region}'s pixel is ${width / WIDTH} wide`);
    const canvas = { width: 0, height: 0, style: {}, dataset: {} };
    showShape(canvas, region);
    assert.equal(canvas.width, WIDTH);
    assert.equal(canvas.height, lines, 'the canvas is not the region\'s window');
    const m = /^([\d.]+) \/ ([\d.]+)$/.exec(canvas.style.aspectRatio);
    assert.ok(within(Number(m[1]) / Number(m[2]), (WIDTH * pixel) / lines));
    assert.equal(canvas.dataset.region, region);
  }
});

// ---- The PAL suggestion (the plan's "Fixed before running", item 13) ----

test('PAL is suggested when the median of the last thirty frames is nearer PAL\'s lines than NTSC\'s, and only on NTSC', () => {
  assert.equal(SUGGEST_FRAMES, 30);
  const frames = (n, lines) => Array(n).fill(lines);
  assert.equal(suggestsPal(frames(30, 312), 'NTSC'), true);
  assert.equal(suggestsPal(frames(30, 262), 'NTSC'), false);
  assert.equal(suggestsPal(frames(30, 312), 'PAL'), false, 'the page suggested PAL on PAL');
  assert.equal(suggestsPal(frames(29, 312), 'NTSC'), false, 'twenty-nine frames is not thirty');
  // The median, not the mean: a few odd frames do not move it.
  assert.equal(suggestsPal([...frames(27, 312), 400, 400, 48], 'NTSC'), true);
  assert.equal(suggestsPal([...frames(27, 262), 400, 400, 400], 'NTSC'), false);
  // Sixteen NTSC frames and fourteen of the line limit: the mean is nearer PAL, the median is NTSC's.
  assert.equal(suggestsPal([...frames(16, 262), ...frames(14, 400)], 'NTSC'), false);
  // Only the last thirty count.
  assert.equal(suggestsPal([...frames(100, 262), ...frames(30, 312)], 'NTSC'), true);
  assert.equal(suggestsPal([...frames(100, 312), ...frames(30, 262)], 'NTSC'), false);
  // Exactly half way between is nearer neither.
  assert.equal(suggestsPal(frames(30, 287), 'NTSC'), false);
  assert.equal(suggestsPal(frames(30, 288), 'NTSC'), true);
  assert.equal(median([3, 1, 2]), 2);
  assert.equal(median([4, 1, 2, 3]), 2.5);
  // The page says the lines and the suggestion; it never changes the region by itself.
  assert.equal(linesLine(400), 'The last frame had 400 lines. An NTSC game makes 262 a frame and a PAL game 312.');
  assert.match(suggestionLine(true), /try PAL\. The page does not change the region by itself\.$/);
  assert.equal(suggestionLine(false), '');
});

// ---- A file that is not a cartridge this machine models (Review Focus 2) ----

const fakeFile = (name, size) => ({ name, size, reads: 0, async arrayBuffer() { this.reads++; return new ArrayBuffer(size); } });

test('a file over the limit the host gives is refused before it is read, and the page never types the limit', async () => {
  const big = fakeFile('ten-megabytes.bin', 10 * 1024 * 1024);
  const result = await picker.readRom(big, 32768);
  assert.equal(result.ok, false);
  assert.equal(big.reads, 0, 'the file was read before it was refused');
  assert.equal(result.line, 'ten-megabytes.bin is 10,485,760 bytes, more than the 32,768 bytes of the largest file this machine loads, so it was not read.');
  assert.match(driver, /readRom\(file, host\.MaxRomBytes\(\)\)/);
  assert.doesNotMatch(driver, /32768|32_768|32 \* 1024/);
});

test('a file no board takes shows the cartridge\'s own sentence, and the machine carries on as it was', () => {
  const sentence = 'The file is 3,072 bytes, which no modelled board has; the modelled sizes are 2,048, 4,096, 8,192, 12,288, 16,384 and 32,768 bytes.';
  const host = { machine: 'before', Load() { throw new Error(sentence); } };
  const result = loadCartridge(host, new Uint8Array(3072), 'NTSC', 48_000);
  assert.deepEqual(result, { ok: false, message: sentence });
  assert.equal(host.machine, 'before');
  assert.equal(badFileLine('odd.bin', result, true), `odd.bin was not loaded. ${sentence} The machine carries on as it was.`);
  assert.equal(badFileLine('odd.bin', result, false), `odd.bin was not loaded. ${sentence} Choose another file.`);
  // An error that is not a sentence still gives a line, never an exception.
  const odd = loadCartridge({ Load: () => { throw 'a string'; } }, new Uint8Array(1), 'NTSC', 48_000);
  assert.equal(odd.ok, false);
  assert.ok(odd.message.length > 0);
});

test('a cartridge put in is named with the host\'s sentence, which says the board it was run as', () => {
  const calls = [];
  const host = { Load: (data, region, rate) => { calls.push([data.length, region, rate]); return 'The board is F8 with the Superchip, chosen from the file.'; } };
  const result = loadCartridge(host, new Uint8Array(8192), 'PAL', 48_000);
  assert.deepEqual(result, { ok: true, sentence: 'The board is F8 with the Superchip, chosen from the file.' });
  assert.deepEqual(calls, [[8192, 'PAL', 48_000]]);
  assert.equal(cartridgeLine('game.a26', result.sentence), 'game.a26 is in. The board is F8 with the Superchip, chosen from the file.');
});

// ---- The sound ----

test('the sound\'s steady offset is taken out: a held level falls to silence, and a tone comes through', () => {
  const RATE = 48_000;
  const block = dcBlocker(RATE);
  const held = new Float32Array(RATE).fill(0.6);
  block(held);
  assert.ok(Math.abs(held.at(-1)) < 1e-3, `a held level is still ${held.at(-1)} after a second`);
  // A 440 Hz square wave between 0 and 0.6 comes out round zero, about as big as it went in.
  const tone = dcBlocker(RATE);
  const wave = Float32Array.from({ length: RATE }, (_, i) => (Math.floor((i * 440 * 2) / RATE) % 2 === 0 ? 0.6 : 0));
  tone(wave);
  const last = wave.subarray(RATE / 2);
  const mean = last.reduce((s, v) => s + v, 0) / last.length;
  const peak = Math.max(...last) - Math.min(...last);
  assert.ok(Math.abs(mean) < 0.01, `the tone sits at ${mean}`);
  assert.ok(peak > 0.55, `the tone lost its size: ${peak}`);
  // The state carries from one frame's samples to the next: two halves are one whole.
  const whole = Float32Array.from({ length: 2000 }, (_, i) => (i % 100 < 50 ? 1 : 0));
  const halves = whole.slice();
  dcBlocker(RATE)(whole);
  const split = dcBlocker(RATE);
  split(halves.subarray(0, 1000));
  split(halves.subarray(1000));
  assert.deepEqual([...halves], [...whole]);
});

test('the offset does not thump when the sound starts: the first sample seeds the high-pass, and fresh() starts it again (task 13\'s review)', () => {
  const RATE = 48_000;
  // Sound turned on with the volume left at a held level: silence, from the very first sample.
  const block = dcBlocker(RATE);
  const first = new Float32Array(64).fill(0.6);
  block(first);
  assert.deepEqual([...first], new Array(64).fill(0), 'the first samples of a held level are not silent');
  // A step after that still comes through, and decays.
  const step = new Float32Array(4).fill(0.9);
  block(step);
  assert.ok(step[0] > 0.29, `the step came through as ${step[0]}`);
  // Started again (the page's fresh()), at another held level: silent again from its first sample.
  block.reset();
  const again = new Float32Array(16).fill(0.2);
  block(again);
  assert.deepEqual([...again], new Array(16).fill(0), 'the high-pass carried its old state across a fresh start');
  // The shared sound output starts the page's transform again when it starts afresh.
  let resets = 0;
  const transform = Object.assign(() => {}, { reset: () => { resets++; } });
  const out = soundOut.soundOutput({ transform });
  out.machine = { DropSound: () => 0 };
  out.fresh();
  assert.equal(resets, 1);
  // A transform with no reset, and no transform, are fine.
  Object.assign(soundOut.soundOutput({ transform: () => {} }), { machine: { DropSound: () => 0 } }).fresh();
  Object.assign(soundOut.soundOutput({}), { machine: { DropSound: () => 0 } }).fresh();
});

test('the shared sound button posts each frame\'s samples through the page\'s transform, in their own buffer', () => {
  const posted = [];
  const out = soundOut.soundOutput({ transform: (samples) => { for (let i = 0; i < samples.length; i++) samples[i] -= 0.5; } });
  out.node = { port: { postMessage: (data, transfer) => posted.push([data, transfer]) } };
  const bytes = new Uint8Array(new Float32Array([0.5, 1, 0]).buffer);
  out.play({ byteLength: bytes.length, copyTo: (target) => target.set(bytes) });
  assert.equal(posted.length, 1);
  assert.deepEqual([...posted[0][0]], [0, 0.5, -0.5]);
  assert.deepEqual(posted[0][1], [posted[0][0].buffer], 'the samples were not handed over');
  // Without a transform, the NES's way, they go as they came.
  const plain = soundOut.soundOutput({});
  plain.node = { port: { postMessage: (data) => posted.push([data]) } };
  plain.play({ byteLength: bytes.length, copyTo: (target) => target.set(bytes) });
  assert.deepEqual([...posted[1][0]], [0.5, 1, 0]);
});

test('the shared region control: a change the page refuses puts the radios back, one it carries out leaves them', () => {
  const radios = ['NTSC', 'PAL'].map((value) => Object.assign(new EventTarget(), { value, checked: value === 'NTSC', disabled: true }));
  let current = 'NTSC';
  let answer = false;
  const asked = [];
  regionLine.wireRegion(radios, () => current, (value) => { asked.push(value); if (answer) current = value; return answer; });
  assert.ok(radios.every((r) => !r.disabled));
  const choose = (i) => { radios.forEach((r, k) => { r.checked = k === i; }); radios[i].dispatchEvent(new Event('change')); };
  choose(1);
  assert.deepEqual(asked, ['PAL']);
  assert.deepEqual(radios.map((r) => r.checked), [true, false], 'a refused change left PAL checked');
  answer = true;
  choose(1);
  assert.deepEqual(radios.map((r) => r.checked), [false, true]);
  // Choosing the region already running asks nothing.
  choose(1);
  assert.deepEqual(asked, ['PAL', 'PAL']);
});

test('the shared ring drops the oldest samples at its most and keeps the newest, for both consoles alike', () => {
  const RATE = 48_000;
  // The NES's worklet hands out the shared ring's own rules, unchanged.
  for (const name of ['PRIME', 'MOST', 'admit', 'take', 'SampleRing']) assert.equal(nesAudio[name], ring[name], `nes-audio.js's ${name} is not the shared one`);
  const r = new ring.SampleRing(RATE);
  const most = Math.round(ring.MOST * RATE);
  let next = 0;
  for (let i = 0; i < 100; i++) {
    r.push(Float32Array.from({ length: 800 }, () => next++));
    assert.ok(r.queued <= most, `the ring holds ${r.queued}`);
  }
  const out = new Float32Array(128);
  const first = next - r.queued;
  r.pull(out);
  assert.equal(out[0], first);
  assert.deepEqual(ring.admit(most, 1, 10, most), { drop: most + 1 - 10, queued: 10 });
  // The Atari's page plays the ring through the processor sample-worklet.js registers itself.
  assert.match(fs.readFileSync(path.join(PUBLIC, 'sample-worklet.js'), 'utf8'), /^registerRing\('sample-ring'\);$/m);
  assert.match(driver, /module: '\/sample-worklet\.js',\s*processor: 'sample-ring',/);
  assert.match(fs.readFileSync(path.join(PUBLIC, 'nes-audio.js'), 'utf8'), /^registerRing\('nes-sound'\);$/m);
});

// ---- The host and the markup the script relies on ----

const host = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Atari2600.Wasm', 'Program.cs'), 'utf8');

test('every host call the page makes is a JSExport of Atari2600Host, and the limit is the library\'s', () => {
  const exported = new Set([...host.matchAll(/\[JSExport\]\s+public static \w+(?:\[\])? (\w+)\(/g)].map((m) => m[1]));
  const called = new Set([...driver.matchAll(/\bhost\.(\w+)\(/g)].map((m) => m[1]));
  assert.ok(called.size >= 10, `the page calls only ${[...called].join(', ')}`);
  for (const name of called) assert.ok(exported.has(name), `the page calls host.${name}, which Atari2600Host does not export`);
  // The plan's interface, and the bench's calls (bench/atari-2600-speed/main.js), are all there.
  for (const name of ['Load', 'MaxRomBytes', 'Run', 'RunFrames', 'Cycles', 'Frames', 'Lines', 'Region', 'SetRegion', 'CpuHz', 'NominalLines', 'SetJoystick', 'SetSwitches', 'PowerCycle', 'Picture', 'Sound', 'DropSound']) {
    assert.ok(exported.has(name), `Atari2600Host does not export ${name}`);
  }
  assert.match(host, /public static int MaxRomBytes\(\) => Cartridge\.MaxFileSize;/);
  // The picture and the sound are handed to the functions the page registers, in the module it names.
  assert.match(host, /\[JSImport\("picture", "atari2600"\)\]/);
  assert.match(host, /\[JSImport\("sound", "atari2600"\)\]/);
  assert.match(driver, /setModuleImports\('atari2600', \{\s*picture:/);
  assert.match(driver, /assembly: 'Dbhq\.Machines\.Atari2600\.Wasm',\s*hostClass: 'Atari2600Host',/);
  assert.match(driver, /clockMhz = host\.CpuHz\(\) \/ 1e6;/);
});

/** The panel rendered to HTML through Astro's container, as the site builds it. */
async function renderPanel(props) {
  const { experimental_AstroContainer: AstroContainer } = await import('astro/container');
  const { getViteConfig } = await import('astro/config');
  const { createServer } = await import('vite');
  const config = await getViteConfig({ server: { middlewareMode: true, hmr: false, ws: false }, appType: 'custom', logLevel: 'silent', optimizeDeps: { noDiscovery: true, include: [] } })({ command: 'serve', mode: 'test' });
  const vite = await createServer(config);
  try {
    const component = await vite.ssrLoadModule('/src/components/Atari2600Panel.astro');
    const container = await AstroContainer.create();
    return await container.renderToString(component.default, { props });
  } finally {
    await vite.close();
  }
}

const markup = await renderPanel({ base: '/machines/atari-2600/', rom: { file: '2048.bin', title: '2048 2600' }, download: 3_100_000 });
const text = markup.replace(/<[^>]+>/g, ' ').replace(/&#39;/g, "'").replace(/&amp;/g, '&').replace(/\s+/g, ' ');

test('the panel has every element the script looks for, and every control is disabled until the script runs', () => {
  const selectors = new Set([...driver.matchAll(/'\[(data-vcs[\w-]*)(?:\]|\])/g)].map((m) => m[1]));
  assert.ok(selectors.size >= 20, `the script looks for only ${selectors.size} elements`);
  for (const attribute of selectors) assert.match(markup, new RegExp(`\\b${attribute}\\b`), `the panel has no ${attribute}`);
  assert.match(markup, /<div class="vcs" data-vcs data-base="\/machines\/atari-2600\/" data-rom="2048\.bin" data-rom-title="2048 2600" data-download="3100000" data-state="static">/);
  for (const m of markup.matchAll(/<(button|input)\b[^>]*>/g)) {
    if (/type="file"/.test(m[0])) continue;
    assert.match(m[0], /\bdisabled\b/, `${m[0]} is not disabled before the script runs`);
  }
  // Two radios, NTSC checked; the five switches; joystick 1's five buttons.
  assert.deepEqual([...markup.matchAll(/value="(\w+)" data-vcs-region/g)].map((m) => m[1]), ['NTSC', 'PAL']);
  assert.deepEqual([...markup.matchAll(/data-vcs-hold="(\w+)"/g)].map((m) => m[1]), HELD_SWITCHES);
  assert.deepEqual([...markup.matchAll(/data-vcs-toggle="(\w+)"/g)].map((m) => m[1]).sort(), Object.keys(SWITCHES).filter((s) => !HELD_SWITCHES.includes(s)).sort());
  assert.deepEqual([...markup.matchAll(/data-vcs-button="(\w+)"/g)].map((m) => m[1]).sort(), Object.keys(STICK).sort());
});

test('the screen is a canvas 160 wide and NTSC\'s window tall, and the panel says it models the picture and is not a CRT', () => {
  assert.match(markup, new RegExp(`<canvas data-vcs-canvas width="${WIDTH}" height="${WINDOWS.NTSC.lines}"></canvas>`));
  assert.match(text, /It models the picture and is not a CRT/);
  assert.match(text, /A frame shorter or longer than a television's is shown as it is/);
});

test('the panel names each key from the script\'s tables, and says which board a file runs as, linking the board issue', () => {
  assert.match(text, /joystick 1 is Up arrow, Down arrow, Left arrow, Right arrow to move and Space to fire, and joystick 2 is W, S, A, D and F\./);
  assert.match(text, /R holds Reset and T holds Select; C flips the colour switch, 1 the left difficulty and 2 the right\./);
  assert.match(text, /Reset \(R\)/);
  assert.match(text, /Select \(T\)/);
  assert.match(markup, /href="https:\/\/github\.com\/dbhq-uk\/6502\/issues\/126">issue 126<\/a>/);
  assert.match(text, /the board is chosen by the file's size, and the line above says which/);
  assert.match(text, /Start puts 2048 2600 in\./);
  assert.match(text, /Sound is off\./);
});

test('the Start button gives the download\'s size from its props, and the panel types no size', () => {
  assert.match(markup, /data-vcs-start disabled>Start the Atari 2600, a download of up to [\d.]+ MB<\/button>/);
  const component = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'Atari2600Panel.astro'), 'utf8');
  assert.doesNotMatch(component, /\d+(\.\d+)? MB/);
});

// ---- The page: the script run as the browser runs it ----
//
// In a fake browser just big enough for it, as tests/nes-panel.test.mjs runs
// the NES's: a panel of made-up elements, a fake .NET runtime at
// <base>_framework/dotnet.js that hands over a made-up Atari2600Host, and a
// fetch that gives the bundled cartridge. The run loop's animation frames are
// never turned: nothing here needs the machine to run, only the page's wiring.

const pageDir = fs.mkdtempSync(path.join(os.tmpdir(), 'vcs-page-'));
fs.mkdirSync(path.join(pageDir, '_framework'), { recursive: true });
fs.writeFileSync(path.join(pageDir, '_framework', 'dotnet.js'), 'export const dotnet = { create: () => globalThis.fakeVcsDotnet.create() };\n');
const PAGE_BASE = `${pathToFileURL(pageDir).href}/`;
test.after(() => fs.rmSync(pageDir, { recursive: true, force: true }));

class FakeElement extends EventTarget {
  constructor(props = {}) {
    super();
    Object.assign(this, { dataset: {}, style: {}, disabled: true, hidden: false, textContent: '' }, props);
  }
  focus() {}
  blur() {}
  click() { this.dispatchEvent(new Event('click')); }
  setAttribute(name, value) { this[name] = value; }
  setPointerCapture() {}
}

/** A made-up Atari2600Host, recording each call. */
function fakeHost({ refuse = null } = {}) {
  const host = {
    calls: [],
    region: 'NTSC',
    frames: 0,
    Load(data, region, rate) {
      host.calls.push(['Load', data.length, region, rate]);
      if (refuse) throw new Error(refuse);
      host.region = region;
      return 'The board is 2K, chosen from the file.';
    },
    MaxRomBytes: () => 32768,
    Run: () => 0,
    RunFrames(n) { host.calls.push(['RunFrames', n]); host.frames = n; return n; },
    Cycles: () => 0,
    Frames: () => host.frames,
    Lines: () => 262,
    Region: () => host.region,
    SetRegion(region) { host.calls.push(['SetRegion', region]); host.region = region; },
    CpuHz: () => (host.region === 'PAL' ? 1_182_298 : 1_193_182),
    NominalLines: () => 262,
    SetJoystick(stick, state) { host.calls.push(['SetJoystick', stick, state]); },
    SetSwitches(state) { host.calls.push(['SetSwitches', state]); },
    PowerCycle() { host.calls.push(['PowerCycle']); host.frames = 0; },
    Picture: () => host.frames,
    Sound: () => 0,
    DropSound: () => 0,
  };
  return host;
}

let pages = 0;

async function until(done, ms = 10_000) {
  const end = Date.now() + ms;
  while (!done() && Date.now() < end) await new Promise((resolve) => setTimeout(resolve, 2));
}

async function vcsPage(t, host) {
  const saved = ['document', 'window', 'fetch', 'requestAnimationFrame', 'cancelAnimationFrame'].map((k) => [k, Object.getOwnPropertyDescriptor(globalThis, k)]);
  t.after(() => {
    for (const [k, d] of saved) {
      if (d) Object.defineProperty(globalThis, k, d);
      else delete globalThis[k];
    }
    delete globalThis.fakeVcsDotnet;
  });
  const radios = ['NTSC', 'PAL'].map((value) => new FakeElement({ value, checked: value === 'NTSC' }));
  const toggles = ['BlackAndWhite', 'LeftDifficultyA', 'RightDifficultyA'].map((name) => new FakeElement({ dataset: { vcsToggle: name } }));
  const holds = ['Reset', 'Select'].map((name) => new FakeElement({ dataset: { vcsHold: name } }));
  const stick = Object.keys(STICK).map((name) => new FakeElement({ dataset: { vcsButton: name } }));
  const lists = { '[data-vcs-region]': radios, '[data-vcs-toggle]': toggles, '[data-vcs-hold]': holds, '[data-vcs-button]': stick };
  const elements = new Map();
  const panel = new FakeElement({ dataset: { download: '3.1 MB', base: PAGE_BASE, rom: '2048.bin', romTitle: '2048 2600' } });
  panel.querySelectorAll = (selector) => lists[selector] ?? [];
  panel.querySelector = (selector) => {
    if (selector === '[data-vcs-region]:checked') return radios.find((r) => r.checked) ?? null;
    if (!elements.has(selector)) {
      elements.set(selector, new FakeElement(selector === '[data-vcs-canvas]'
        ? { width: 160, height: 228, getContext: () => ({ createImageData: (w, h) => ({ width: w, height: h, data: new Uint8ClampedArray(w * h * 4) }), putImageData() {} }) }
        : {}));
    }
    return elements.get(selector);
  };
  const events = [];
  panel.addEventListener('vcs:ready', () => events.push('vcs:ready'));
  const define = (k, value) => Object.defineProperty(globalThis, k, { value, configurable: true, writable: true });
  define('document', { hidden: false, querySelectorAll: (s) => (s === '[data-vcs]' ? [panel] : []), addEventListener() {}, removeEventListener() {} });
  define('window', { addEventListener() {} });
  define('fetch', async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(2048) }));
  define('requestAnimationFrame', () => 1);
  define('cancelAnimationFrame', () => {});
  globalThis.fakeVcsDotnet = {
    create: async () => ({ getAssemblyExports: async () => ({ Atari2600Host: host }), setModuleImports() {} }),
  };
  await import(`${pathToFileURL(path.join(PUBLIC, 'atari-2600.js')).href}?page=${++pages}`);
  const start = async () => {
    panel.querySelector('[data-vcs-start]').click();
    await until(() => panel.dataset.state === 'running');
    assert.equal(panel.dataset.state, 'running', `the page did not start: ${panel.querySelector('[data-vcs-status]').textContent}`);
  };
  return { panel, radios, toggles, holds, stick, events, start, el: (selector) => panel.querySelector(selector) };
}

test('before Start nothing is asked of a host; Start loads the bundled cartridge in NTSC and says which board it runs as', async (t) => {
  const host = fakeHost();
  const { panel, events, start, el } = await vcsPage(t, host);
  assert.equal(panel.vcs.running(), false);
  assert.equal(panel.vcs.region(), 'NTSC');
  assert.deepEqual(host.calls, []);
  await start();
  assert.equal(panel.vcs.running(), true);
  assert.deepEqual(events, ['vcs:ready']);
  assert.deepEqual(host.calls[0], ['Load', 2048, 'NTSC', 48_000]);
  assert.equal(el('[data-vcs-cartridge]').textContent, '2048 2600 is in. The board is 2K, chosen from the file.');
  // The machine is told the inputs at rest once it has a cartridge.
  assert.deepEqual(host.calls.filter((c) => c[0] !== 'Load').slice(0, 3), [['SetJoystick', 0, 0], ['SetJoystick', 1, 0], ['SetSwitches', 0]]);
});

test('the region switches at once with no restart: SetRegion, no Load and no power cycle, and the canvas takes the new window', async (t) => {
  const host = fakeHost();
  const { panel, radios, start, el } = await vcsPage(t, host);
  await start();
  host.calls.length = 0;
  radios[0].checked = false;
  radios[1].checked = true;
  radios[1].dispatchEvent(new Event('change'));
  assert.deepEqual(host.calls, [['SetRegion', 'PAL']]);
  assert.equal(panel.vcs.region(), 'PAL');
  assert.equal(el('[data-vcs-canvas]').height, WINDOWS.PAL.lines);
  assert.match(el('[data-vcs-region-line]').textContent, /^Now PAL: the colours and the clock changed at once, and the game carries on without a restart\.$/);
  radios[1].checked = false;
  radios[0].checked = true;
  radios[0].dispatchEvent(new Event('change'));
  assert.deepEqual(host.calls, [['SetRegion', 'PAL'], ['SetRegion', 'NTSC']]);
  assert.equal(el('[data-vcs-canvas]').height, WINDOWS.NTSC.lines);
});

test('the switch buttons: the ones that stay flip and say where they are; Reset is held while pressed', async (t) => {
  const host = fakeHost();
  const { toggles, holds, start, el } = await vcsPage(t, host);
  await start();
  host.calls.length = 0;
  toggles[0].click();
  assert.deepEqual(host.calls, [['SetSwitches', SWITCHES.BlackAndWhite]]);
  assert.equal(toggles[0].textContent, 'Black and white');
  assert.equal(toggles[0]['aria-pressed'], 'true');
  holds[0].dispatchEvent(Object.assign(new Event('pointerdown'), { pointerId: 1 }));
  assert.deepEqual(host.calls.at(-1), ['SetSwitches', SWITCHES.BlackAndWhite | SWITCHES.Reset]);
  assert.equal(holds[0]['aria-pressed'], 'true');
  holds[0].dispatchEvent(new Event('pointerup'));
  assert.deepEqual(host.calls.at(-1), ['SetSwitches', SWITCHES.BlackAndWhite]);
  toggles[0].click();
  assert.deepEqual(host.calls.at(-1), ['SetSwitches', 0]);
  assert.equal(toggles[0].textContent, 'Colour');
  // The keyboard flips the same switch, and holds Reset through its key.
  const screen = el('[data-vcs-screen]');
  const key = (type, code) => screen.dispatchEvent(Object.assign(new Event(type), { code, repeat: false }));
  key('keydown', 'Digit1');
  assert.deepEqual(host.calls.at(-1), ['SetSwitches', SWITCHES.LeftDifficultyA]);
  key('keydown', 'KeyR');
  assert.deepEqual(host.calls.at(-1), ['SetSwitches', SWITCHES.LeftDifficultyA | SWITCHES.Reset]);
  key('keyup', 'KeyR');
  assert.deepEqual(host.calls.at(-1), ['SetSwitches', SWITCHES.LeftDifficultyA]);
});

test('every direction and both fire buttons held at once reach the machine as written (Review Focus 4)', async (t) => {
  const host = fakeHost();
  const { stick, start, el } = await vcsPage(t, host);
  await start();
  const screen = el('[data-vcs-screen]');
  for (const code of Object.keys(STICK_KEYS)) screen.dispatchEvent(Object.assign(new Event('keydown'), { code, repeat: false }));
  for (const b of stick) b.dispatchEvent(Object.assign(new Event('pointerdown'), { pointerId: 1 }));
  const sets = host.calls.filter((c) => c[0] === 'SetJoystick');
  assert.deepEqual(sets.filter((c) => c[1] === 0).at(-1), ['SetJoystick', 0, 31]);
  assert.deepEqual(sets.filter((c) => c[1] === 1).at(-1), ['SetJoystick', 1, 31]);
});

test('a refused file leaves the machine as it was, and an oversized one is not read', async (t) => {
  const host = fakeHost();
  const { start, el } = await vcsPage(t, host);
  await start();
  const loads = host.calls.filter((c) => c[0] === 'Load').length;
  const file = el('[data-vcs-file]');
  const big = fakeFile('huge.bin', 64 * 1024);
  file.files = [big];
  file.dispatchEvent(new Event('change'));
  await until(() => el('[data-vcs-cartridge]').textContent.startsWith('huge.bin'));
  assert.equal(el('[data-vcs-cartridge]').textContent, 'huge.bin is 65,536 bytes, more than the 32,768 bytes of the largest file this machine loads, so it was not read. The machine carries on as it was.');
  assert.equal(big.reads, 0);
  host.Load = () => { throw new Error('The file is 3,072 bytes, which no modelled board has.'); };
  file.files = [fakeFile('odd.bin', 3072)];
  file.dispatchEvent(new Event('change'));
  await until(() => el('[data-vcs-cartridge]').textContent.startsWith('odd.bin'));
  assert.equal(el('[data-vcs-cartridge]').textContent, 'odd.bin was not loaded. The file is 3,072 bytes, which no modelled board has. The machine carries on as it was.');
  assert.equal(host.calls.filter((c) => c[0] === 'Load').length, loads);
});

test('stepTo lets go of the joysticks and of Reset and Select, keeps the switches that stay, powers off and on, and runs exactly n frames', async (t) => {
  const host = fakeHost();
  const { panel, toggles, holds, start, el } = await vcsPage(t, host);
  await start();
  toggles[0].click();
  // Held when the check calls it: joystick 1 left and fire on the keys, joystick 2 up, and Reset on its button.
  const screen = el('[data-vcs-screen]');
  for (const code of ['ArrowLeft', 'Space', 'KeyW']) screen.dispatchEvent(Object.assign(new Event('keydown'), { code, repeat: false }));
  holds[0].dispatchEvent(Object.assign(new Event('pointerdown'), { pointerId: 1 }));
  assert.deepEqual(host.calls.filter((c) => c[0] === 'SetJoystick').slice(-2).map((c) => c[1]).sort(), [0, 1]);
  assert.deepEqual(host.calls.at(-1), ['SetSwitches', SWITCHES.BlackAndWhite | SWITCHES.Reset]);
  host.calls.length = 0;
  assert.equal(panel.vcs.stepTo(100), 100);
  // Both joysticks and Reset let go, the colour switch kept at black and white, all before the power cycle.
  const before = host.calls.slice(0, host.calls.findIndex((c) => c[0] === 'PowerCycle'));
  assert.deepEqual(before.filter((c) => c[0] === 'SetJoystick').sort(), [['SetJoystick', 0, 0], ['SetJoystick', 1, 0]]);
  assert.deepEqual(before.filter((c) => c[0] === 'SetSwitches'), [['SetSwitches', SWITCHES.BlackAndWhite]]);
  assert.deepEqual(host.calls.filter((c) => c[0] === 'PowerCycle' || c[0] === 'RunFrames'), [['PowerCycle'], ['RunFrames', 100]]);
  assert.ok(!host.calls.some((c) => c[0] === 'SetSwitches' && c[1] !== SWITCHES.BlackAndWhite));
  assert.equal(panel.dataset.frames, '100');
});
