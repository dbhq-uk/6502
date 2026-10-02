// The KIM-1 on its page: loads the machine (.NET WebAssembly) and MOS
// Technology's monitor ROM, runs it at the board's own clock, draws the six
// digits and plays the keypad into it. An external module because the site's
// CSP allows no inline script.
//
// The page works without this file: the keypad is drawn but disabled, and the
// status line says that the KIM-1 needs JavaScript.
//
// Timing. Each animation frame runs as many machine cycles as real time has
// passed since the last one, at the clock the page states (1 MHz for the
// KIM-1), up to a tenth of a second's worth: after a stall, or with the tab in
// the background, the machine pauses rather than racing to catch up.
//
// The digits. The monitor lights one digit at a time and moves on; the machine
// keeps what each digit last showed for 20 ms of machine time, as the eye
// does, so a digit the monitor is scanning stays lit and one it has stopped
// scanning goes dark. This file only draws what the machine reports.
//
// The keys. A click queues one tap. The machine plays taps at a person's pace,
// each held 40 ms and rested 40 ms in machine time, so a click is never too
// short for the monitor to see, and clicks faster than it reads are kept in
// order (src/Dbhq.Machines.Kim1/Kim1Keystrokes.cs).
//
// The 3D model. Once the machine is running, the panel carries `panel.kim1`:
// `tap(key)` presses a key exactly as a click on the keypad does, and
// `segments(digit)` is what this file last drew for that digit, so the model
// lights the same segments as the drawn display. Every tap, from a click, a
// keyboard or the model, is announced on the panel as a `kim1:key` event, so the
// model's key goes down whichever way it was pressed. The panel says
// `kim1:ready` when the machine has started.

const MAX_FRAME_MS = 100;
const SETTLE_MS = 400;

for (const panel of document.querySelectorAll('[data-kim1]')) start(panel);

async function start(panel) {
  const status = panel.querySelector('[data-kim1-status]');
  const speed = panel.querySelector('[data-kim1-speed]');
  const said = panel.querySelector('[data-kim1-said]');
  const keys = [...panel.querySelectorAll('[data-key]')];
  const sst = panel.querySelector('[data-kim1-sst]');
  const digits = [...panel.querySelectorAll('[data-digit]')].map((d) => [...d.querySelectorAll('[data-seg]')]);
  const base = panel.dataset.base;
  const clockMhz = Number(panel.dataset.clockMhz);

  const say = (text, state) => {
    status.textContent = text;
    panel.dataset.state = state;
  };

  let kim;
  try {
    say('Loading the KIM-1: the emulator and the monitor ROM.', 'loading');
    const [{ dotnet }, rom002, rom003] = await Promise.all([
      import(`${base}_framework/dotnet.js`),
      bytes(`${base}6530-002.bin`),
      bytes(`${base}6530-003.bin`),
    ]);
    const runtime = await dotnet.create();
    const exports = await runtime.getAssemblyExports('Dbhq.Machines.Kim1.Wasm');
    kim = exports.Kim1Host;
    kim.Load(rom002, rom003);
  } catch (error) {
    say(`The KIM-1 could not start: ${error.message}`, 'failed');
    return;
  }

  const shown = digits.map(() => -1);
  const tap = (key) => {
    kim.Tap(key);
    panel.dispatchEvent(new CustomEvent('kim1:key', { detail: { key } }));
  };

  for (const key of keys) {
    key.disabled = false;
    key.addEventListener('click', () => tap(key.dataset.key));
  }
  sst.disabled = false;
  sst.addEventListener('change', () => kim.SetSingleStep(sst.checked));
  // With focus anywhere on the machine, the hex keys on a keyboard press the
  // keypad's hex keys. Nothing else is taken over, so Tab, Enter and Space
  // keep their usual meanings.
  panel.addEventListener('keydown', (event) => {
    if (event.ctrlKey || event.metaKey || event.altKey || event.repeat) return;
    if (!/^[0-9a-f]$/i.test(event.key) || event.target === sst) return;
    event.preventDefault();
    tap(event.key.toUpperCase());
  });

  panel.kim1 = { tap, segments: (digit) => shown[digit] };
  panel.dispatchEvent(new CustomEvent('kim1:ready'));
  say('Running. Press RS to start the monitor.', 'running');

  let last = performance.now();
  let cycles = kim.Cycles();
  let busyMs = 0;
  let busyCycles = 0;
  let wallMs = 0;
  let wallCycles = 0;
  let text = null;
  let textSince = 0;
  let announced = null;

  const frame = (now) => {
    const real = now - last;
    const elapsed = Math.min(real, MAX_FRAME_MS);
    last = now;
    const want = Math.max(1, Math.round(elapsed * clockMhz * 1000));
    const before = performance.now();
    const after = kim.Run(want);
    busyMs += performance.now() - before;
    busyCycles += after - cycles;
    wallMs += real;
    wallCycles += after - cycles;
    cycles = after;

    for (let d = 0; d < digits.length; d++) {
      const segments = kim.Segments(d);
      if (segments === shown[d]) continue;
      shown[d] = segments;
      digits[d].forEach((path, s) => path.classList.toggle('on', (segments & (1 << s)) !== 0));
    }

    // The screen-reader summary changes only once the digits have held still
    // for a moment, so it reads out a result and not every key on the way.
    const now6 = kim.Display();
    if (now6 !== text) {
      text = now6;
      textSince = now;
    } else if (now - textSince >= SETTLE_MS && text !== announced) {
      announced = text;
      said.textContent = text.trim() === '' ? 'Display dark' : `Display ${text.slice(0, 4)} ${text.slice(4)}`;
      panel.dataset.display = text;
      if (panel.dataset.state === 'running' && text.trim() !== '') say('Running. Type the program below, or your own.', 'monitor');
    }

    panel.dataset.pending = String(kim.Pending());

    if (wallMs >= 1000) {
      report(busyCycles / busyMs / 1000, wallCycles / wallMs / 1000);
      busyMs = busyCycles = wallMs = wallCycles = 0;
    }
    requestAnimationFrame(frame);
  };

  // capacity: how fast this browser runs the machine flat out, in MHz, from
  // the time spent inside Run. actual: how fast it is running it, against the
  // real time that passed, so a browser that cannot keep up reports less than
  // the board's clock.
  const report = (capacity, actual) => {
    panel.dataset.capacityMhz = capacity.toFixed(2);
    panel.dataset.actualMhz = actual.toFixed(2);
    const times = Math.floor(capacity / clockMhz);
    speed.textContent = times >= 2
      ? `Running at the board's own ${fmt(clockMhz)} MHz. This browser could run it about ${fmt(times)} times as fast.`
      : times === 1
        ? `Running at the board's own ${fmt(clockMhz)} MHz, with little to spare in this browser.`
        : `Running at ${actual.toFixed(2)} MHz, slower than the board's ${fmt(clockMhz)} MHz: this browser cannot keep up.`;
  };

  requestAnimationFrame(frame);
}

const fmt = (n) => n.toLocaleString('en-GB');

async function bytes(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return new Uint8Array(await response.arrayBuffer());
}
