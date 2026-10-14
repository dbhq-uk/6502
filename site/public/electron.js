// The Electron on its page: when the visitor presses Start, loads the machine
// (.NET WebAssembly) and its two ROMs, runs it at its own pace on the shared host
// (machine-host.js), draws its picture on the canvas, plays its sound once asked
// to, takes the keyboard while the screen has focus, and works its cassette
// recorder. An external module because the site's CSP allows no inline script.
// The BBC Micro's page (bbc-micro.js) is the pattern, part for part.
//
// The page works without this file: the panel is drawn, every control is
// disabled, and the status line says the machine needs JavaScript.
//
// NOTHING DOWNLOADS UNTIL START. The machine is several megabytes, so the page
// loads none of it until the visitor asks: the Start button says how much it is.
//
// THE PICTURE. The ULA draws into a framebuffer of 640 by 256 RGBA pixels. Each
// frame, if it has finished a frame since the last one, the host hands over a
// view of the framebuffer's own memory through the `picture` function
// registered below, and it is copied once, straight into the canvas's
// ImageData, and put on the canvas. The stylesheet shows it four by three.
//
// THE KEYS. Keys are mapped by where they are, not by what they type
// (electron-keys.js). They go to the machine only while the screen has focus,
// and Tab is never taken, so a keyboard user can always move on. A browser
// shortcut is left to the browser: with Alt or the Command key the machine gets
// nothing, and with Ctrl it gets the key but the browser keeps its shortcut. The
// host holds each key long enough for the OS to see it (ElectronKeyPresses), so
// a key pressed and released at once still counts. Focus leaving the screen
// lets go of every key held. Pause is BREAK, as is the Break button.
//
// THE ON-SCREEN KEYS are every key the machine has, as buttons. A tap presses
// the key and lets it go. SHIFT, CTRL and CAPS LK FUNC latch for the next key
// tapped (electron-keys.js, STICKY); a second tap of CAPS LK FUNC presses it
// alone, which turns CAPS LOCK on or off.
//
// THE SOUND is off until the visitor turns it on, because browsers do not let a
// page start sound by itself. Then each frame's samples go to electron-audio.js,
// an AudioWorklet, which plays them. Hiding the page suspends it.
//
// THE TAPE. A .uef file dropped on the page, or chosen with Insert a tape, goes
// in the recorder, rewound; Blank tape puts in an empty one and presses record,
// so a SAVE is recorded; Rewind winds back to the start, and a recording then
// plays; Save tape downloads the tape as a .uef file and leaves it in, rewound.
// The file is read in this tab and never sent anywhere. A tape plays at the
// machine's own pace, as a cassette does: nothing is sped up. A file the
// machine's reader refuses is described in the reader's own words
// (ElectronHost.InsertTape returns them) and nothing else changes: the tape that
// was in stays in. The line under the controls says whether the motor is on and
// how many seconds of tape have gone by, read from the machine.
//
// LATEST WINS. Reading a file takes a moment, and the other controls stay
// usable meanwhile. Each insert takes a ticket; one that finishes after a later
// one has started is dropped.
//
// For the site's browser check, once running the panel carries
// `panel.electron`: `screenRow(n)` is mode 6 text row n read from screen memory,
// and `host` is the machine.
import { startMachine } from '/machine-host.js';
import { ELECTRON_KEYS, PC_KEYS, STICKY } from '/electron-keys.js';

// The sample rate the machine makes its sound at, and the rate the page asks
// the browser to play it at, so it is not resampled twice.
const SAMPLE_RATE = 48_000;
// The largest file the recorder reads: UefReader.MaxInflatedBytes, 4 MiB, the
// most a UEF may be (tests/electron.test.mjs holds the two equal). A larger file
// is refused before it is read.
export const MOST_BYTES = 4 * 1024 * 1024;
const MOST_MIB = `${MOST_BYTES / (1024 * 1024)} MiB`;
const NOT_RUNNING = 'A tape dropped on the page was not taken, because the Electron is not running.';

for (const panel of document.querySelectorAll('[data-electron]')) prepare(panel);

function prepare(panel) {
  const start = panel.querySelector('[data-electron-start]');
  const status = panel.querySelector('[data-electron-status]');
  const say = (text, state) => {
    status.textContent = text;
    panel.dataset.state = state;
  };
  if (!panel.dataset.download) {
    say('This copy of the site was built without the Electron\'s files, so it cannot run here.', 'missing');
    return;
  }
  start.disabled = false;
  say('Press Start to download the Electron and run it.', 'ready');
  // A file dropped on the page goes to the recorder once there is one (tape()
  // sets panel.putTape). Before that it is refused here, rather than the browser
  // leaving the page to show it.
  document.addEventListener('dragover', (event) => {
    if ([...(event.dataTransfer?.types ?? [])].includes('Files')) event.preventDefault();
  });
  document.addEventListener('drop', (event) => {
    if (!event.dataTransfer?.files.length) return;
    event.preventDefault();
    const { files } = event.dataTransfer;
    if (panel.putTape) panel.putTape(files[0], othersDropped(files));
    else if (panel.dataset.state === 'ready') say('Press Start first, then drop the tape on the page again.', 'ready');
    else if (panel.dataset.state === 'loading') say('The Electron is still loading. Drop the tape on the page again once it runs.', 'loading');
    // The failure stays on the status line, and the drop is not lost without a word.
    else if (panel.dataset.state === 'failed' && !status.textContent.endsWith(NOT_RUNNING)) status.textContent += ` ${NOT_RUNNING}`;
  });
  start.addEventListener('click', () => run(panel, say), { once: true });
}

// One tape at a time: with several files dropped, the first is tried and the tape line says so.
// Exported for tests/electron.test.mjs.
export const othersDropped = (files) => (files.length > 1 ? `Only the first of the ${files.length} files dropped, ${files[0].name}, was tried: drop one tape at a time.` : '');

async function run(panel, say) {
  const start = panel.querySelector('[data-electron-start]');
  const screen = panel.querySelector('[data-electron-screen]');
  const canvas = panel.querySelector('[data-electron-canvas]');
  const speed = panel.querySelector('[data-electron-speed]');
  const base = panel.dataset.base;
  const clockMhz = Number(panel.dataset.clockMhz);
  start.disabled = true;

  say('Loading the Electron: the emulator and its two ROMs.', 'loading');
  // The ROMs are fetched while the runtime loads, and handed over once it is up.
  const roms = Promise.all(panel.dataset.roms.split(' ').map((file) => bytes(`${base}${file}`)));
  roms.catch(() => {});

  const context = canvas.getContext('2d');
  const image = context.createImageData(canvas.width, canvas.height);
  const pixels = new Uint8Array(image.data.buffer);
  let shown = -1;
  const sound = soundOutput(panel);
  let recorder = null;

  const onFrame = (host, cycles, now) => {
    if (host.Frames() !== shown) {
      shown = host.Picture();
      context.putImageData(image, 0, 0);
      panel.dataset.frames = String(shown);
    }
    if (sound.on) host.SoundSamples();
    recorder?.tick(now);
  };

  const started = await startMachine({
    panel,
    base,
    name: 'Electron',
    assembly: 'Dbhq.Machines.Electron.Wasm',
    hostClass: 'ElectronHost',
    load: async (host, runtime) => {
      runtime.setModuleImports('electron', {
        picture: (view) => view.copyTo(pixels),
        sound: (view) => sound.play(view),
      });
      const [os, basic] = await roms;
      host.Load(os, basic, SAMPLE_RATE);
    },
    clockMhz,
    clockOf: "the Electron's",
    onFrame,
    onPause: () => sound.pause(),
    onResume: () => sound.resume(),
    say,
    speedEl: speed,
  });
  if (!started) return;
  const electron = started.host;
  sound.machine = electron;
  start.hidden = true;

  keyboard(screen, electron, panel);
  const keys = onScreenKeys(panel, electron);
  // A key latched on screen lets go when the PC keyboard takes over, and at
  // BREAK, so it never lands on a key typed long after.
  screen.addEventListener('focus', keys.letGo);
  for (const b of panel.querySelectorAll('[data-electron-break]')) {
    b.disabled = false;
    b.addEventListener('click', () => {
      keys.letGo();
      electron.Break();
    });
  }
  sound.wire();
  recorder = tape(panel, electron);

  panel.electron = { host: electron, screenRow: (row) => electron.ScreenRow(row) };
  panel.dispatchEvent(new CustomEvent('electron:ready'));
  say('Running. The screen has the keyboard: type into the Electron, or press Tab to move on.', 'running');
  // Start was the visitor asking to use the machine, so the keyboard goes to it.
  screen.focus({ preventScroll: true });
}

// ---- The keyboard ----

// Exported for tests/electron.test.mjs, which plays key events into it.
export function keyboard(screen, electron, panel) {
  screen.tabIndex = 0;
  // Each Electron key down, with the PC keys holding it: two PC keys can press
  // one Electron key (both Shifts, Backspace and Delete), and it comes up only
  // when the last of them does.
  const held = new Map();
  const down = (code) => {
    const key = ELECTRON_KEYS[PC_KEYS[code]];
    const codes = held.get(key) ?? new Set();
    if (codes.size === 0) electron.PressKey(key);
    codes.add(code);
    held.set(key, codes);
  };
  const up = (code) => {
    const key = ELECTRON_KEYS[PC_KEYS[code]];
    const codes = held.get(key);
    if (!codes?.delete(code) || codes.size > 0) return;
    held.delete(key);
    electron.ReleaseKey(key);
  };
  const letGo = () => {
    for (const key of held.keys()) electron.ReleaseKey(key);
    held.clear();
  };

  screen.addEventListener('keydown', (event) => {
    if (event.altKey || event.metaKey) return;
    if (event.code === 'Pause') {
      event.preventDefault();
      electron.Break();
      return;
    }
    if (!(event.code in PC_KEYS)) return;
    if (!event.ctrlKey) event.preventDefault();
    if (event.repeat) return;
    // CAPS LK is a press, whatever the PC does with its own light: some systems
    // send its key up only at the next press, which would hold FUNC down.
    if (event.code === 'CapsLock') {
      electron.PressKey(ELECTRON_KEYS.CapsLock);
      electron.ReleaseKey(ELECTRON_KEYS.CapsLock);
      return;
    }
    down(event.code);
  });
  screen.addEventListener('keyup', (event) => {
    if (event.code in PC_KEYS && event.code !== 'CapsLock') up(event.code);
  });
  screen.addEventListener('focus', () => { panel.dataset.typing = 'true'; });
  screen.addEventListener('blur', () => {
    delete panel.dataset.typing;
    letGo();
  });
  document.addEventListener('visibilitychange', () => { if (document.hidden) letGo(); });
}

// Exported for tests/electron.test.mjs.
export function onScreenKeys(panel, electron) {
  const latched = new Set();
  const buttons = [...panel.querySelectorAll('[data-electron-key]')];
  const show = () => {
    for (const b of buttons) if (STICKY.includes(b.dataset.electronKey)) b.setAttribute('aria-pressed', String(latched.has(b.dataset.electronKey)));
  };
  const tap = (name) => {
    electron.PressKey(ELECTRON_KEYS[name]);
    electron.ReleaseKey(ELECTRON_KEYS[name]);
  };
  for (const b of buttons) {
    b.disabled = false;
    b.addEventListener('click', () => {
      const name = b.dataset.electronKey;
      if (STICKY.includes(name)) {
        if (!latched.has(name)) latched.add(name);
        else {
          latched.delete(name);
          // CAPS LK FUNC tapped twice is CAPS LK pressed alone: the lock turns over.
          if (name === 'CapsLock') tap(name);
        }
      } else {
        const held = [...latched];
        for (const m of held) electron.PressKey(ELECTRON_KEYS[m]);
        tap(name);
        for (const m of held.reverse()) electron.ReleaseKey(ELECTRON_KEYS[m]);
        latched.clear();
      }
      show();
    });
  }
  // Lets go of a latched key without typing anything.
  const letGo = () => {
    latched.clear();
    show();
  };
  return { letGo };
}

// ---- The sound ----

function soundOutput(panel) {
  const button = panel.querySelector('[data-electron-sound]');
  const line = panel.querySelector('[data-electron-sound-status]');
  const out = {
    on: false,
    machine: null,
    context: null,
    node: null,
    async turnOn() {
      // Made inside the click, as browsers ask, at the machine's own rate.
      this.context ??= new AudioContext({ sampleRate: SAMPLE_RATE });
      if (!this.node) {
        await this.context.audioWorklet.addModule('/electron-audio.js');
        this.node = new AudioWorkletNode(this.context, 'electron-sound', { numberOfInputs: 0, outputChannelCount: [1] });
        this.node.connect(this.context.destination);
      }
      this.fresh();
      await this.context.resume();
      this.on = true;
    },
    async turnOff() {
      this.on = false;
      await this.context?.suspend();
    },
    // What was made while the sound was off, or the page hidden, is not played late.
    fresh() {
      this.machine.DropSound();
      this.node.port.postMessage({ flush: true });
    },
    play(view) {
      const samples = new Float32Array(view.byteLength / 4);
      view.copyTo(new Uint8Array(samples.buffer));
      this.node.port.postMessage(samples, [samples.buffer]);
    },
    pause() {
      if (this.on) this.context.suspend();
    },
    resume() {
      if (!this.on) return;
      this.fresh();
      this.context.resume();
    },
    wire() {
      button.disabled = false;
      button.addEventListener('click', async () => {
        button.disabled = true;
        try {
          if (this.on) await this.turnOff();
          else await this.turnOn();
        } catch (error) {
          this.on = false;
          line.textContent = `Sound could not start: ${error.message}`;
          panel.dataset.sound = 'failed';
          button.disabled = false;
          return;
        }
        button.textContent = this.on ? 'Turn sound off' : 'Turn sound on';
        button.setAttribute('aria-pressed', String(this.on));
        line.textContent = this.on ? 'Sound is on.' : 'Sound is off.';
        panel.dataset.sound = this.on ? 'on' : 'off';
        button.disabled = false;
      });
    },
  };
  return out;
}

// ---- The cassette recorder ----

// One decimal place, the British way: 10.8.
const tenths = (seconds) => seconds.toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

// Exported for tests/electron.test.mjs, which drives it with a fake panel and a fake machine.
// Returns { tick(now) }, which the page calls every frame to keep the motor line true.
export function tape(panel, electron) {
  const insert = panel.querySelector('[data-electron-insert]');
  const file = panel.querySelector('[data-electron-file]');
  const blank = panel.querySelector('[data-electron-blank]');
  const rewind = panel.querySelector('[data-electron-rewind]');
  const save = panel.querySelector('[data-electron-save]');
  const line = panel.querySelector('[data-electron-tape]');
  const motorLine = panel.querySelector('[data-electron-motor]');
  // The tape's name, or null with none in; and whether it is a blank tape on record.
  let name = null;
  let recording = false;
  // The ticket of the latest insert, blank tape or save (LATEST WINS above).
  let latest = 0;

  const describeLine = (text) => {
    line.textContent = text;
    panel.dataset.tape = name ?? '';
    save.disabled = rewind.disabled = name === null;
  };
  const put = async (chosen, others = '') => {
    // What the visitor did with several files at once goes after whatever this one comes to.
    const describe = (text) => describeLine(others ? `${text} ${others}` : text);
    if (!/\.uef$/i.test(chosen?.name ?? '')) {
      describe(`${chosen?.name ?? 'That'} is not a tape this recorder takes: it takes .uef tape images.`);
      return;
    }
    // Refused before it is read: a tape the reader takes is never larger.
    if (chosen.size > MOST_BYTES) {
      describe(`${chosen.name} is ${chosen.size.toLocaleString('en-GB')} bytes, more than the ${MOST_MIB} a tape may be here, so it was not read.`);
      return;
    }
    const ticket = ++latest;
    let data;
    try {
      data = new Uint8Array(await chosen.arrayBuffer());
    } catch (error) {
      if (ticket === latest) describe(`${chosen.name} could not be read: ${error.message}`);
      return;
    }
    if (ticket !== latest) return;
    // The machine's reader decides. A tape it refuses changes nothing: its reason is shown as it gives it.
    const refused = electron.InsertTape(data);
    if (refused !== '') {
      describe(`${chosen.name} was not put in. ${refused}`);
      return;
    }
    name = chosen.name;
    recording = false;
    describe(`In the recorder: ${name}, rewound. Type LOAD "" or CHAIN "" and press RETURN to load the first program on it.`);
  };

  const describe = describeLine;
  insert.addEventListener('click', () => file.click());
  file.addEventListener('change', () => {
    if (file.files.length > 0) put(file.files[0]);
    file.value = '';
  });
  // A file dropped anywhere on the page goes in the recorder (prepare() listens).
  panel.putTape = put;
  blank.addEventListener('click', () => {
    latest++;
    electron.StartRecording();
    name = 'tape.uef';
    recording = true;
    describe('In the recorder: a blank tape, on record. Type SAVE "NAME" and press RETURN, then RETURN again at RECORD then RETURN. Save tape keeps it as tape.uef.');
  });
  rewind.addEventListener('click', () => {
    electron.Rewind();
    const was = recording;
    recording = false;
    describe(was ? `Rewound: the recording on ${name} now plays. Type LOAD "" and press RETURN to load it back.` : `Rewound: ${name} plays from the start.`);
  });
  save.addEventListener('click', () => {
    // The OS is still writing a SAVE: taking the tape out now would keep half of it.
    if (recording && electron.MotorOn()) {
      describe('The Electron is still saving to the tape: wait for the prompt to come back, then press Save tape.');
      return;
    }
    latest++;
    const data = electron.EjectTape();
    if (!data) {
      // Nothing on it. A blank tape goes back on record, so the visitor's next SAVE still
      // records; a tape file with nothing on it is simply out.
      if (recording) electron.StartRecording();
      else name = null;
      describe(recording ? 'There is nothing on the tape to save yet: it is still on record.' : 'There was nothing on that tape, so the recorder is empty.');
      return;
    }
    const url = URL.createObjectURL(new Blob([data], { type: 'application/octet-stream' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    // The tape stays in, rewound: the bytes just saved go back in, read by the same reader as any file.
    const refused = electron.InsertTape(data);
    recording = false;
    describe(refused === ''
      ? `Saved as ${name}, and left in the recorder, rewound. Type LOAD "" and press RETURN to load it back.`
      : `Saved as ${name}. It could not go back in: ${refused}`);
  });

  for (const control of [insert, blank]) control.disabled = false;
  describe('No tape in the recorder.');

  // The motor line, from the machine: on or off, and the seconds of tape gone by.
  let motor = null;
  let shownAt = -Infinity;
  const tick = (now) => {
    const on = electron.MotorOn();
    if (on === motor && (!on || now - shownAt < 250)) return;
    const seconds = tenths(electron.TapeSeconds());
    motorLine.textContent = on
      ? `Motor on: ${seconds} seconds of tape${recording ? ' recorded' : ' played'}.`
      : motor === null ? 'Motor off.' : `Motor off, after ${seconds} seconds of tape${recording ? ' recorded' : ' played'}.`;
    panel.dataset.motor = on ? 'on' : 'off';
    motor = on;
    shownAt = now;
  };
  return { tick };
}

async function bytes(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return new Uint8Array(await response.arrayBuffer());
}
