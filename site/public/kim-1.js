// The KIM-1 on its page: loads the machine (.NET WebAssembly) and MOS
// Technology's monitor ROM, runs it at the board's own clock, draws the six
// digits and plays the keypad into it. An external module because the site's
// CSP allows no inline script.
//
// The page works without this file: the keypad is drawn but disabled, and the
// status line says that the KIM-1 needs JavaScript.
//
// Timing, and the sentence about this browser's speed, are machine-host.js's,
// shared with every machine page: each animation frame runs as many machine
// cycles as real time has passed since the last one, at the clock the page
// states (1 MHz for the KIM-1), up to a tenth of a second's worth, and a hidden
// page is paused. This file keeps only what is the KIM-1's own.
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
//
// The teletype. The machine models the terminal on the board's 20 mA loops
// (src/Dbhq.Machines.Kim1/Kim1Teletype.cs): the TTY switch, the terminal's
// speed, the keys sent to it as bits and the characters it decodes from the
// bits the monitor sends. Each frame this file takes what was printed and puts
// it on the paper (teletype-paper.js), a row to a line in a log. The field types
// at the teletype only while it has focus, in capitals, as a Teletype Model 33
// does; Enter is CR and Backspace or Delete is RUB OUT. Tab is never taken.
// panel.dataset.ttyIdle is "1" once nothing is waiting to be sent and the
// monitor has stopped printing.
import { startMachine } from '/machine-host.js';
import { Paper } from '/teletype-paper.js';

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
  const mode = panel.querySelector('[data-kim1-mode]');
  const baud = panel.querySelector('[data-kim1-baud]');
  const clear = panel.querySelector('[data-kim1-clear]');
  const paperEl = panel.querySelector('[data-kim1-paper]');
  const typing = panel.querySelector('[data-kim1-type]');
  const sends = [...panel.querySelectorAll('[data-kim1-send]')];
  const tty = panel.querySelector('[data-kim1-tty]');
  const paper = new Paper();

  const say = (text, state) => {
    status.textContent = text;
    panel.dataset.state = state;
  };

  say('Loading the KIM-1: the emulator and the monitor ROM.', 'loading');
  // The ROM is fetched while the runtime loads, and handed over once it is up.
  // The catch only marks it handled for the case where the runtime fails
  // first and the ROM is never awaited; load still sees the failure.
  const rom = Promise.all([bytes(`${base}6530-002.bin`), bytes(`${base}6530-003.bin`)]);
  rom.catch(() => {});

  const shown = digits.map(() => -1);
  let text = null;
  let textSince = 0;
  let announced = null;

  const onFrame = (kim, cycles, now) => {
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
    const printed = kim.TeletypePrinted();
    if (printed) draw(paper.print(printed));
    panel.dataset.ttyIdle = kim.TeletypeIdle() ? '1' : '0';
  };

  // The paper's rows follow its lines: torn-off lines leave the top, changed
  // and new ones are written, and the log keeps to its foot unless the reader
  // has scrolled up.
  const draw = ({ from, torn }) => {
    const atFoot = paperEl.scrollHeight - paperEl.scrollTop - paperEl.clientHeight < 24;
    for (let i = 0; i < torn && paperEl.firstChild; i++) paperEl.firstChild.remove();
    const rows = paperEl.children;
    for (let i = from; i < paper.lines.length; i++) {
      const row = rows[i] ?? paperEl.appendChild(document.createElement('div'));
      if (row.textContent !== paper.lines[i]) row.textContent = paper.lines[i];
    }
    panel.dataset.paper = paper.text();
    if (atFoot) paperEl.scrollTop = paperEl.scrollHeight;
  };

  const started = await startMachine({
    panel,
    base,
    name: 'KIM-1',
    assembly: 'Dbhq.Machines.Kim1.Wasm',
    hostClass: 'Kim1Host',
    load: async (host) => {
      const [rom002, rom003] = await rom;
      host.Load(rom002, rom003);
    },
    clockMhz,
    onFrame,
    say,
    speedEl: speed,
  });
  if (!started) return;
  const kim = started.host;

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
  // With focus anywhere on the machine but the teletype, the hex keys on a
  // keyboard press the keypad's hex keys. Nothing else is taken over, so Tab,
  // Enter and Space keep their usual meanings.
  panel.addEventListener('keydown', (event) => {
    if (event.ctrlKey || event.metaKey || event.altKey || event.repeat) return;
    if (!/^[0-9a-f]$/i.test(event.key) || event.target === sst || tty.contains(event.target)) return;
    event.preventDefault();
    tap(event.key.toUpperCase());
  });

  // The teletype. Characters go as the machine's queue sends them, a key at a
  // time when the line is quiet, so typing ahead of the monitor loses nothing.
  const send = (text) => kim.TeletypeSend(text);
  for (const el of [mode, baud, clear, typing, ...sends]) el.disabled = false;
  mode.addEventListener('change', () => {
    kim.SetTeletypeMode(mode.checked);
    say(mode.checked ? 'TTY switch on. Press RS, then RUB OUT, and the monitor prints KIM on the teletype.' : 'TTY switch off. The monitor goes back to the keypad and the digits.', 'teletype');
  });
  baud.addEventListener('change', () => {
    kim.SetTeletypeBaud(Number(baud.value));
    say(`The teletype now runs at ${baud.value} baud. Press RS, then RUB OUT, so the monitor measures the new speed.`, 'teletype');
  });
  clear.addEventListener('click', () => {
    paper.clear();
    paperEl.replaceChildren();
    panel.dataset.paper = '';
  });
  for (const b of sends) b.addEventListener('click', () => send({ RUBOUT: '\u007f', LF: '\n' }[b.dataset.kim1Send]));
  // What is typed in the field goes to the teletype, in capitals, and leaves
  // the field: it is a keyboard, not a place to edit a line.
  typing.addEventListener('input', () => {
    const text = typing.value.toUpperCase().replace(/[^ -~]/g, '');
    typing.value = '';
    if (text) send(text);
  });
  typing.addEventListener('keydown', (event) => {
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    const key = { Enter: '\r', Backspace: '\u007f', Delete: '\u007f' }[event.key];
    if (!key) return;
    event.preventDefault();
    send(key);
  });

  // startMachine resolves before its first frame, so all of this is in place
  // before the machine first runs, as it was when the loop lived here.
  panel.kim1 = { tap, segments: (digit) => shown[digit] };
  panel.dispatchEvent(new CustomEvent('kim1:ready'));
  say('Running. Press RS to start the monitor.', 'running');
}

async function bytes(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return new Uint8Array(await response.arrayBuffer());
}
