import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

// public/machine-host.js, the part of a machine page that every machine shares:
// it loads the machine's .NET WebAssembly, runs it in step with real time, and
// says how fast the browser runs it. It is run here, against a fake .NET
// runtime and a fake machine, in a fake browser whose clock and animation
// frames the test turns by hand. A regex over the source would prove a phrase
// is there; this proves the loop reaches it.
//
// The fake machine runs at a set speed: Run(n) moves the fake clock on by the
// time n cycles take at that speed, so the time the page spends inside Run is
// exactly what a browser that fast would spend. It also runs a few cycles past
// the budget, as a real machine finishing its last instruction does, so the
// test can tell the cycles it ran from the cycles it was asked for.

const { startMachine, MAX_FRAME_MS, ROOM_TIMES, speedRoom, speedSentence } = await import(pathToFileURL(path.join(process.cwd(), 'public', 'machine-host.js')).href);

const OVERRUN = 3;

// A fake .NET runtime, at <base>_framework/dotnet.js, as the published
// machine has it. It defers to whatever the running test put in
// globalThis.fakeDotnet, so one module serves every test.
const runtimeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'machine-host-'));
fs.mkdirSync(path.join(runtimeDir, 'with', '_framework'), { recursive: true });
fs.mkdirSync(path.join(runtimeDir, 'without'), { recursive: true });
fs.writeFileSync(path.join(runtimeDir, 'with', '_framework', 'dotnet.js'), 'export const dotnet = { create: () => globalThis.fakeDotnet.create() };\n');
const BASE = `${pathToFileURL(path.join(runtimeDir, 'with')).href}/`;
const NO_RUNTIME = `${pathToFileURL(path.join(runtimeDir, 'without')).href}/`;
test.after(() => fs.rmSync(runtimeDir, { recursive: true, force: true }));

/** A browser just big enough for the host: a clock, animation frames and the page's visibility, all turned by hand. */
function fakeBrowser(t) {
  const saved = ['performance', 'requestAnimationFrame', 'cancelAnimationFrame', 'document'].map((k) => [k, Object.getOwnPropertyDescriptor(globalThis, k)]);
  t.after(() => {
    for (const [k, d] of saved) {
      if (d) Object.defineProperty(globalThis, k, d);
      else delete globalThis[k];
    }
    delete globalThis.fakeDotnet;
  });
  let now = 5000;
  let nextId = 1;
  const frames = new Map();
  const listeners = new Map();
  const document = {
    hidden: false,
    addEventListener: (type, fn) => listeners.set(fn, type),
    removeEventListener: (type, fn) => listeners.delete(fn),
  };
  const define = (k, value) => Object.defineProperty(globalThis, k, { value, configurable: true, writable: true });
  define('performance', { now: () => now });
  define('requestAnimationFrame', (cb) => { frames.set(nextId, cb); return nextId++; });
  define('cancelAnimationFrame', (id) => frames.delete(id));
  define('document', document);
  const visibility = (hidden) => {
    document.hidden = hidden;
    for (const [fn, type] of [...listeners]) if (type === 'visibilitychange') fn();
  };
  return {
    advance: (ms) => { now += ms; },
    now: () => now,
    pending: () => frames.size,
    /** One animation frame, `gap` ms of real time after the last thing that happened. */
    frame(gap = 16) {
      now += gap;
      const due = [...frames.values()];
      frames.clear();
      for (const cb of due) cb(now);
      return due.length;
    },
    hide: () => visibility(true),
    show: () => visibility(false),
  };
}

/** A fake machine running at `capacityMhz` in this browser (a test may change `machine.capacityMhz`). */
function fakeMachine(browser, capacityMhz) {
  const machine = {
    capacityMhz,
    runs: [],
    loaded: 0,
    cycles: 0,
    Load() { machine.loaded++; },
    Cycles: () => machine.cycles,
    Run(n) {
      machine.runs.push(n);
      const ran = n + OVERRUN;
      browser.advance(ran / (machine.capacityMhz * 1000));
      machine.cycles += ran;
      return machine.cycles;
    },
  };
  return machine;
}

/** Starts a machine on the host with a fake runtime that exports `exports` from `assembly`. */
async function start(browser, { exports, assembly = 'Fake.Wasm', base = BASE, ...options }) {
  const asked = [];
  globalThis.fakeDotnet = {
    create: async () => ({
      getAssemblyExports: async (name) => {
        asked.push(name);
        if (name !== assembly) throw new Error(`no assembly ${name}`);
        return exports;
      },
    }),
  };
  const panel = { dataset: {} };
  const speedEl = { textContent: '' };
  const said = [];
  const frames = [];
  const result = await startMachine({
    panel,
    base,
    name: 'KIM-1',
    assembly,
    hostClass: 'FakeHost',
    clockMhz: 1,
    load: (host) => host.Load(),
    onFrame: (host, cycles, now) => frames.push({ host, cycles, now }),
    say: (text, state) => said.push([text, state]),
    speedEl,
    ...options,
  });
  return { result, panel, speedEl, said, frames, asked };
}

/** Runs frames `gap` ms apart until the page has reported its speed once. */
function untilReport(browser, panel, gap = 16) {
  for (let i = 0; i < 10_000 && panel.dataset.capacityMhz === undefined; i++) browser.frame(gap);
  assert.ok(panel.dataset.capacityMhz !== undefined, 'the page never reported its speed');
}

test('it loads the runtime from the base the page names, asks for the assembly, loads the machine class, and only then runs it', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const { result, asked, frames } = await start(browser, { exports: { FakeHost: machine } });
  assert.equal(result.host, machine);
  assert.equal(typeof result.stop, 'function');
  assert.deepEqual(asked, ['Fake.Wasm']);
  assert.equal(machine.loaded, 1);
  // Nothing has run yet: the loop waits for the first animation frame.
  assert.deepEqual(machine.runs, []);
  assert.equal(frames.length, 0);
  assert.equal(browser.pending(), 1);
});

test('each frame runs the real time since the last one at the clock, as a whole number of cycles, never less than one', async (t) => {
  const browser = fakeBrowser(t);
  // A machine so fast that running it takes no measurable time.
  const machine = fakeMachine(browser, 1e9);
  await start(browser, { exports: { FakeHost: machine } });
  browser.frame(16);
  assert.equal(machine.runs[0], 16_000, 'sixteen milliseconds at 1 MHz is 16,000 cycles');
  browser.frame(2.4);
  assert.equal(machine.runs[1], 2_400);
  browser.frame(0);
  assert.equal(machine.runs[2], 1, 'a frame with no time since the last still runs one cycle');
  assert.ok(machine.runs.every(Number.isInteger));
});

test('a frame after a stall runs at most a tenth of a second of machine time, at 1 MHz and at 2 MHz', async (t) => {
  assert.equal(MAX_FRAME_MS, 100);
  for (const clockMhz of [1, 2]) {
    const browser = fakeBrowser(t);
    const machine = fakeMachine(browser, 50);
    await start(browser, { exports: { FakeHost: machine }, clockMhz });
    browser.frame(5000);
    assert.equal(machine.runs[0], 100 * clockMhz * 1000, `a five-second stall at ${clockMhz} MHz`);
    assert.ok(Number.isInteger(machine.runs[0]));
  }
  // The BBC Micro's budget, 200,000 cycles, is a whole number well inside an int: JSExport passes it as one.
  assert.ok(100 * 2 * 1000 < 2 ** 31);
});

test('onFrame gets the machine, the cycles it really ran this frame (not the budget), and the frame time', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 50);
  const { frames } = await start(browser, { exports: { FakeHost: machine }, clockMhz: 2 });
  browser.frame(16);
  browser.frame(16);
  assert.equal(frames.length, 2);
  for (const [i, f] of frames.entries()) {
    assert.equal(f.host, machine);
    assert.equal(f.cycles, machine.runs[i] + OVERRUN);
  }
  assert.equal(frames[0].cycles + frames[1].cycles, machine.cycles);
  assert.ok(frames[1].now > frames[0].now);
});

test('a browser two or more times as fast as the clock is told so, with its capacity and actual speed on the panel', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const { panel, speedEl } = await start(browser, { exports: { FakeHost: machine } });
  untilReport(browser, panel);
  assert.equal(panel.dataset.capacityMhz, '5.00');
  assert.equal(panel.dataset.actualMhz, '1.00');
  assert.equal(speedEl.textContent, "Running at the board's own 1 MHz. This browser could run it about 5 times as fast.");
});

test('the multiple is rounded down and written the British way', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 2500.9);
  const { panel, speedEl } = await start(browser, { exports: { FakeHost: machine }, clockMhz: 2 });
  untilReport(browser, panel);
  assert.equal(speedEl.textContent, "Running at the board's own 2 MHz. This browser could run it about 1,250 times as fast.");
});

test('a browser between one and two times as fast is running with little to spare', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 1.5);
  const { panel, speedEl } = await start(browser, { exports: { FakeHost: machine } });
  untilReport(browser, panel);
  assert.equal(panel.dataset.capacityMhz, '1.50');
  assert.equal(speedEl.textContent, "Running at the board's own 1 MHz, with little to spare in this browser.");
});

test('a browser slower than the clock says how fast it is really running, and that it cannot keep up', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 0.5);
  const { panel, speedEl } = await start(browser, { exports: { FakeHost: machine } });
  // No idle time between frames: the browser is flat out, so each frame's real
  // time is the last frame's work, and the cap holds it to a tenth of a second.
  // The first second includes the climb to that cap from a standing start, so
  // the second second is the one read: every frame is then a tenth of a second
  // of machine time that took a fifth of a second to run.
  untilReport(browser, panel, 0);
  delete panel.dataset.capacityMhz;
  untilReport(browser, panel, 0);
  assert.equal(panel.dataset.capacityMhz, '0.50');
  assert.equal(panel.dataset.actualMhz, '0.50');
  assert.ok(machine.runs.slice(-5).every((n) => n === 100_000), 'the budget is not at its cap');
  assert.equal(speedEl.textContent, `Running at ${panel.dataset.actualMhz} MHz, slower than the board's 1 MHz: this browser cannot keep up.`);
});

test('the sentences can name whose clock it is', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 1.5);
  const { panel, speedEl } = await start(browser, { exports: { FakeHost: machine }, clockMhz: 2, clockOf: "the BBC Micro's" });
  untilReport(browser, panel, 0);
  assert.match(speedEl.textContent, /^Running at [\d.]+ MHz, slower than the BBC Micro's 2 MHz: this browser cannot keep up\.$/);
});

test('the speed is reported about once a second, from that second alone', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const { panel } = await start(browser, { exports: { FakeHost: machine } });
  untilReport(browser, panel);
  delete panel.dataset.capacityMhz;
  let frames = 0;
  while (panel.dataset.capacityMhz === undefined) { browser.frame(16); frames++; }
  // About fifty frames of twenty milliseconds each (sixteen idle and four running).
  assert.ok(frames >= 45 && frames <= 55, `${frames} frames between reports`);
});

test('without the runtime the page says the machine could not start, and nothing runs', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const { result, said, panel } = await start(browser, { exports: { FakeHost: machine }, base: NO_RUNTIME, name: 'BBC Micro' });
  assert.equal(result, null);
  assert.equal(said.length, 1);
  assert.match(said[0][0], /^The BBC Micro could not start: \S/);
  assert.equal(said[0][1], 'failed');
  assert.equal(machine.loaded, 0);
  assert.equal(browser.pending(), 0);
  assert.equal(panel.dataset.capacityMhz, undefined);
});

test('a load that fails, or an assembly without the machine class, fails the same way, with the reason', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const failing = await start(browser, { exports: { FakeHost: machine }, load: () => { throw new Error('6530-002.bin answered 404'); } });
  assert.equal(failing.result, null);
  assert.deepEqual(failing.said, [['The KIM-1 could not start: 6530-002.bin answered 404', 'failed']]);
  const missing = await start(browser, { exports: { OtherHost: machine } });
  assert.equal(missing.result, null);
  assert.deepEqual(missing.said, [['The KIM-1 could not start: Fake.Wasm exports no FakeHost', 'failed']]);
  const asynchronous = await start(browser, { exports: { FakeHost: machine }, load: async () => { throw new Error('late'); } });
  assert.deepEqual(asynchronous.said, [['The KIM-1 could not start: late', 'failed']]);
  assert.equal(browser.pending(), 0);
  assert.deepEqual(machine.runs, []);
});

test('stop() ends the loop: no frame is waiting, and nothing runs again, even when the page is shown again', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const { result, frames } = await start(browser, { exports: { FakeHost: machine } });
  browser.frame(16);
  result.stop();
  assert.equal(browser.pending(), 0);
  assert.equal(browser.frame(16), 0);
  browser.hide();
  browser.show();
  assert.equal(browser.pending(), 0);
  assert.equal(machine.runs.length, 1);
  assert.equal(frames.length, 1);
});

test('a hidden page pauses the machine, and when it is shown again the machine carries on from then, without the time it was away', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const { panel, speedEl } = await start(browser, { exports: { FakeHost: machine } });
  browser.frame(16);
  browser.hide();
  assert.equal(browser.pending(), 0, 'a frame is still waiting while the page is hidden');
  browser.advance(60_000);
  assert.equal(browser.frame(16), 0);
  browser.show();
  assert.equal(browser.pending(), 1);
  browser.frame(16);
  assert.equal(machine.runs.at(-1), 16_000, 'the first frame back ran the time away, not the time since it was shown');
  // The minute away does not count against the browser's speed.
  untilReport(browser, panel);
  assert.equal(panel.dataset.actualMhz, '1.00');
  assert.match(speedEl.textContent, /about 5 times as fast/);
});

test('the first reading after the page is shown again covers only the frames after it was shown', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 0.5);
  const { panel } = await start(browser, { exports: { FakeHost: machine } });
  // A slow part of a second, never reported, then hidden; shown again on a browser ten times faster.
  for (let i = 0; i < 3; i++) browser.frame(16);
  assert.equal(panel.dataset.capacityMhz, undefined, 'the slow part was long enough to be reported');
  browser.hide();
  machine.capacityMhz = 5;
  browser.show();
  untilReport(browser, panel);
  assert.equal(panel.dataset.capacityMhz, '5.00', 'the reading mixed in frames from before the page was hidden');
});

test('a page that starts hidden waits until it is shown', async (t) => {
  const browser = fakeBrowser(t);
  globalThis.document.hidden = true;
  const machine = fakeMachine(browser, 5);
  const { result } = await start(browser, { exports: { FakeHost: machine } });
  assert.ok(result);
  assert.equal(browser.pending(), 0);
  browser.advance(10_000);
  browser.show();
  browser.frame(16);
  assert.deepEqual(machine.runs, [16_000]);
});

test('load is given the machine class and the .NET runtime, so a machine can register the functions it hands its picture and sound to', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const given = [];
  await start(browser, { exports: { FakeHost: machine }, load: (host, runtime) => given.push([host, runtime]) });
  assert.equal(given.length, 1);
  assert.equal(given[0][0], machine);
  assert.equal(typeof given[0][1]?.getAssemblyExports, 'function', 'the second argument is not the runtime');
});

test('onPause is called when the page is hidden and onResume when it is shown again, once each, and neither when the machine first starts', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const calls = [];
  const { result } = await start(browser, { exports: { FakeHost: machine }, onPause: () => calls.push('pause'), onResume: () => calls.push('resume') });
  browser.frame(16);
  assert.deepEqual(calls, [], 'starting the machine is not a resume');
  browser.hide();
  browser.hide();
  assert.deepEqual(calls, ['pause']);
  browser.show();
  browser.show();
  assert.deepEqual(calls, ['pause', 'resume']);
  // stop() pauses for good: one more pause, and showing the page again resumes nothing.
  result.stop();
  browser.hide();
  browser.show();
  assert.deepEqual(calls, ['pause', 'resume', 'pause']);
});

test('a page that starts hidden is not resumed when it is first shown: it was never paused', async (t) => {
  const browser = fakeBrowser(t);
  globalThis.document.hidden = true;
  const machine = fakeMachine(browser, 5);
  const calls = [];
  await start(browser, { exports: { FakeHost: machine }, onPause: () => calls.push('pause'), onResume: () => calls.push('resume') });
  browser.show();
  browser.frame(16);
  assert.deepEqual(calls, []);
  assert.deepEqual(machine.runs, [16_000]);
});

// ---- What the NES needs: a clock that changes with the region, and a hold for its test hook ----

test('the clock can be a function, read each frame, so a machine whose clock changes (the NES, by region) runs and reports at the new one', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 1e9);
  let mhz = 1;
  const { panel, speedEl } = await start(browser, { exports: { FakeHost: machine }, clockMhz: () => mhz, clockOf: "the NES's" });
  browser.frame(16);
  assert.equal(machine.runs.at(-1), 16_000);
  mhz = 2;
  browser.frame(16);
  assert.equal(machine.runs.at(-1), 32_000, 'the frame after the change did not run at the new clock');
  machine.capacityMhz = 5;
  untilReport(browser, panel);
  assert.equal(speedEl.textContent, "Running at the NES's own 2 MHz. This browser could run it about 2 times as fast.");
});

test('hold() stops running the machine until it is let go, and a hidden page shown again does not let it go', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const calls = [];
  const { result } = await start(browser, { exports: { FakeHost: machine }, onPause: () => calls.push('pause'), onResume: () => calls.push('resume') });
  browser.frame(16);
  result.hold(true);
  assert.equal(browser.pending(), 0);
  assert.deepEqual(calls, ['pause']);
  browser.hide();
  browser.show();
  assert.equal(browser.pending(), 0, 'showing the page let go of the hold');
  assert.equal(machine.runs.length, 1);
  browser.advance(5000);
  result.hold(false);
  assert.deepEqual(calls, ['pause', 'resume']);
  browser.frame(16);
  assert.equal(machine.runs.at(-1), 16_000, 'the time held was run');
  // Let go while the page is hidden, it waits for the page to be shown.
  result.hold(true);
  browser.hide();
  result.hold(false);
  assert.equal(browser.pending(), 0);
  browser.show();
  assert.equal(browser.pending(), 1);
  // After stop(), hold does nothing.
  result.stop();
  result.hold(false);
  assert.equal(browser.pending(), 0);
});

test('cycles the machine runs outside the loop while held are not counted as the next frame\'s, nor in the speed', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const { result, frames, panel } = await start(browser, { exports: { FakeHost: machine } });
  browser.frame(16);
  result.hold(true);
  // The NES's stepTo: millions of cycles run by the page itself, not by the loop, taking no loop time.
  machine.cycles += 3_600_000;
  result.hold(false);
  browser.frame(16);
  assert.equal(frames.at(-1).cycles, machine.runs.at(-1) + OVERRUN, 'the first frame after the hold counted the cycles run while held');
  untilReport(browser, panel);
  assert.equal(panel.dataset.capacityMhz, '5.00', 'the cycles run while held inflated the speed');
});

// ---- The speed line keeps its height (issue 80) ----

// How many lines a sentence takes on a line `width` characters wide, in a
// monospaced face (the speed lines are set in one), breaking only at spaces and
// filling each line as far as it goes, as a browser does with this text.
const linesAt = (sentence, width) => {
  let lines = 1;
  let used = 0;
  for (const word of sentence.split(' ')) {
    const need = used === 0 ? word.length : used + 1 + word.length;
    if (need > width && used > 0) {
      lines++;
      used = word.length;
    } else used = need;
  }
  return lines;
};

// Every width of figure each kind of sentence can have: the multiple from 2 to
// the room's, at each number of digits, and a browser behind the clock from a
// standstill to just under it.
const sentencesOn = (clockMhz, clockOf) => {
  const capacities = [2, 9, 10, 99, 100, 999, 1000, 9999].map((m) => (m + 0.5) * clockMhz);
  return [
    ...capacities.map((c) => speedSentence(c, clockMhz, clockMhz, clockOf)),
    speedSentence(1.5 * clockMhz, clockMhz, clockMhz, clockOf),
    ...[0, 0.5, 0.99].map((f) => speedSentence(f * clockMhz, f * clockMhz, clockMhz, clockOf)),
  ];
};
const CLOCKS = [[1, "the board's"], [2, "the BBC Micro's"], [2, "the Electron's"], [1.789773, "the NES's"], [1.662607, "the NES's"]];

/** Where in a line of `width` characters a sentence is taller than the tallest of the room, or null. */
function tallerThanTheRoom(room, clockMhz, clockOf) {
  for (let width = 8; width <= 200; width++) {
    const tallest = Math.max(...room.map((s) => linesAt(s, width)));
    for (const s of sentencesOn(clockMhz, clockOf)) if (linesAt(s, width) > tallest) return `${width} characters: "${s}"`;
  }
  return null;
}

test('the speed sentences are the ones the host writes, figure for figure', () => {
  assert.equal(speedSentence(5, 1, 1), "Running at the board's own 1 MHz. This browser could run it about 5 times as fast.");
  assert.equal(speedSentence(1.5, 1, 1), "Running at the board's own 1 MHz, with little to spare in this browser.");
  assert.equal(speedSentence(0.5, 0.5, 1, "the BBC Micro's"), "Running at 0.50 MHz, slower than the BBC Micro's 1 MHz: this browser cannot keep up.");
});

test('on a line of any width, no sentence wraps to more lines than the tallest of the room, on every machine\'s clock', () => {
  for (const [clockMhz, clockOf] of CLOCKS) assert.equal(tallerThanTheRoom(speedRoom(clockMhz, clockOf), clockMhz, clockOf), null, clockOf);
  // The check can fail: a room made for a multiple of 2 is too short, and so, on the BBC Micro's clock, is one without the sentence of a browser behind it.
  const [spare, little, behind] = speedRoom(1, "the board's");
  assert.notEqual(tallerThanTheRoom([spare.replace('9,999', '2'), little, behind], 1, "the board's"), null);
  const bbc = speedRoom(2, "the BBC Micro's");
  assert.notEqual(tallerThanTheRoom(bbc.slice(0, 2), 2, "the BBC Micro's"), null);
});

test('the room takes in a reading wider than it was made for', () => {
  assert.equal(ROOM_TIMES, 9999);
  const [spare, , behind] = speedRoom(1, "the board's", { times: 12_345, actual: 10.5 });
  assert.match(spare, /about 12,345 times/);
  assert.match(behind, /^Running at 10\.50 MHz/);
  assert.match(speedRoom(1, "the board's", { times: 3, actual: 0.4 })[0], /about 9,999 times/);
});

/** Just enough of a document for the host's speed line: elements with children, classes, attributes and text. */
function fakeDocument() {
  const doc = {};
  const element = (tag) => ({
    tag,
    ownerDocument: doc,
    parentNode: null,
    children: [],
    className: '',
    attributes: {},
    textContent: '',
    setAttribute(name, value) { this.attributes[name] = value; },
    append(...kids) {
      for (const k of kids) {
        if (k.parentNode) k.parentNode.children.splice(k.parentNode.children.indexOf(k), 1);
        k.parentNode = this;
        this.children.push(k);
      }
    },
    replaceWith(other) {
      const parent = this.parentNode;
      parent.children[parent.children.indexOf(this)] = other;
      other.parentNode = parent;
      this.parentNode = null;
    },
    replaceChildren(...kids) {
      for (const k of this.children) k.parentNode = null;
      this.children = [];
      this.append(...kids);
    },
  });
  doc.createElement = element;
  return doc;
}

test('once started, the speed element sits in a line with a hidden room of the longest sentences, in its own classes, before the first sentence', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const doc = fakeDocument();
  const panelEl = doc.createElement('div');
  const speedEl = doc.createElement('p');
  speedEl.className = 'kim1-speed';
  panelEl.append(speedEl);
  const { panel } = await start(browser, { exports: { FakeHost: machine }, speedEl });
  const [line] = panelEl.children;
  assert.equal(line.className, 'speed-line');
  const [first, room] = line.children;
  assert.equal(first, speedEl);
  assert.equal(room.className, 'speed-room');
  assert.equal(room.attributes['aria-hidden'], 'true');
  assert.equal(speedEl.textContent, '', 'a sentence was written before the first second was measured');
  assert.deepEqual(room.children.map((p) => [p.tag, p.className, p.textContent]), speedRoom(1, "the board's").map((s) => ['p', 'kim1-speed', s]));
  // A reading inside the room leaves it as it was; one wider takes it in.
  const before = [...room.children];
  untilReport(browser, panel);
  assert.equal(speedEl.textContent, "Running at the board's own 1 MHz. This browser could run it about 5 times as fast.");
  assert.deepEqual(room.children, before);
  machine.capacityMhz = 20_000;
  delete panel.dataset.capacityMhz;
  untilReport(browser, panel);
  assert.match(room.children[0].textContent, /about 1\d,\d{3} times|about 20,000 times/);
});

test('a machine that cannot start leaves the speed element where it was', async (t) => {
  const browser = fakeBrowser(t);
  const machine = fakeMachine(browser, 5);
  const doc = fakeDocument();
  const panelEl = doc.createElement('div');
  const speedEl = doc.createElement('p');
  panelEl.append(speedEl);
  await start(browser, { exports: { FakeHost: machine }, base: NO_RUNTIME, speedEl });
  assert.deepEqual(panelEl.children, [speedEl]);
});
