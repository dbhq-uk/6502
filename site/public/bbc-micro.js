// The BBC Micro on its page: when the visitor presses Start, loads the machine
// (.NET WebAssembly) and its three ROMs, runs it at its own 2 MHz on the shared
// host (machine-host.js), draws its picture on the canvas, plays its sound once
// asked to, takes the keyboard while the screen has focus, and works its disc
// drive. An external module because the site's CSP allows no inline script.
//
// The page works without this file: the panel is drawn, every control is
// disabled, and the status line says the machine needs JavaScript.
//
// NOTHING DOWNLOADS UNTIL START. The machine is several megabytes, so the page
// loads none of it until the visitor asks: the Start button says how much it is.
//
// THE PICTURE. The machine draws into its framebuffer, 640 by 512 RGBA. Each
// frame, if the CRTC has finished a field since the last one, the host hands
// over a view of the framebuffer's own memory through the `picture` function
// registered below, and it is copied once, straight into the canvas's
// ImageData, and put on the canvas. No frame is copied twice and none is copied
// when nothing changed.
//
// THE KEYS. Keys are mapped by where they are, not by what they type
// (bbc-keys.js). They go to the machine only while the screen has focus, and
// Tab is never taken, so a keyboard user can always move on. A browser shortcut
// is left to the browser: with Alt or the Command key the machine gets nothing,
// and with Ctrl it gets the key but the browser keeps its shortcut, so Ctrl and
// R still reloads. The host holds each key long enough for the OS to see it
// (BbcKeyPresses), so a key a test presses and releases at once still counts.
// Focus leaving the screen lets go of every key held.
//
// THE ON-SCREEN KEYS are every key the machine has, as buttons, for touch and
// for the keys a PC keyboard does not press here. A tap presses the key and
// lets it go (the host holds it long enough to count). SHIFT and CTRL latch:
// a tap holds them down for the next key tapped, then lets them go, so one
// finger can type SHIFT and 2; a second tap lets go without typing.
//
// THE SOUND is off until the visitor turns it on, because browsers do not let a
// page start sound by itself. Then each frame's samples go to bbc-audio.js, an
// AudioWorklet, which plays them. Hiding the page suspends it and showing the
// page again starts it afresh.
//
// THE DISC. Drive 0 only. A .ssd or .dsd file dropped on the page, or chosen
// with Insert a disc, goes in; Make a blank disc puts in an empty one; Save disc
// downloads the disc as it now stands. The file is read in this tab and never
// sent anywhere.
//
// THE LIBRARY. A list of preset discs, each credited under it (the panel
// renders one credit a disc and this shows the chosen one; the list works as
// soon as the page does, so a visitor can read the credits before Start).
// Once the machine runs, Insert in drive 0 fetches the chosen image from this
// site, discs/<slug>.ssd beside the machine's files, and puts it in like a
// file; Insert and run does that and then SHIFT and BREAK, through the host's
// key queue (BbcHost.ShiftBreak), so the DFS runs the disc's !BOOT, and the
// screen is brought into view, since the keyboard is now the game's. A disc
// from the library is then a disc like any other: Save disc downloads it.
//
// LATEST WINS. Fetching a library disc, or reading a file, takes a moment, and
// the other disc controls stay usable meanwhile. Each insert takes a ticket;
// one that finishes after a later insert has started is dropped, so a file the
// visitor picks while a library disc downloads is not then replaced by it.
//
// For the site's browser check, once running the panel carries `panel.bbc`:
// `screenRow(n)` is mode 7 text row n read from screen memory, and `host` is
// the machine.
import { startMachine } from '/machine-host.js';
import { BBC_KEYS, PC_KEYS, STICKY } from '/bbc-keys.js';

// The sample rate the machine makes its sound at, and the rate the page asks
// the browser to play it at, so it is not resampled twice. The machine's default.
const SAMPLE_RATE = 48_000;
const DRIVE = 0;
const BLANK_TRACKS = 80;
// The largest disc image the drive takes: 80 tracks of ten 256-byte sectors, both sides.
const MOST_BYTES = 2 * 80 * 10 * 256;

for (const panel of document.querySelectorAll('[data-bbc]')) prepare(panel);

function prepare(panel) {
  const start = panel.querySelector('[data-bbc-start]');
  const status = panel.querySelector('[data-bbc-status]');
  const say = (text, state) => {
    status.textContent = text;
    panel.dataset.state = state;
  };
  library(panel);
  if (!panel.dataset.download) {
    say('This copy of the site was built without the BBC Micro\'s files, so it cannot run here.', 'missing');
    return;
  }
  start.disabled = false;
  say('Press Start to download the BBC Micro and run it.', 'ready');
  // A file dropped on the page goes to the drive once there is one (disc()
  // sets panel.putDisc). Before that it is refused here, rather than the browser
  // leaving the page to show it.
  document.addEventListener('dragover', (event) => {
    if ([...(event.dataTransfer?.types ?? [])].includes('Files')) event.preventDefault();
  });
  document.addEventListener('drop', (event) => {
    if (!event.dataTransfer?.files.length) return;
    event.preventDefault();
    if (panel.putDisc) panel.putDisc(event.dataTransfer.files[0]);
    // Before Start the visitor is told to press it; while the machine loads, to
    // wait; once it has failed, the failure stays on the status line.
    else if (panel.dataset.state === 'ready') say('Press Start first, then drop the disc on the page again.', 'ready');
    else if (panel.dataset.state === 'loading') say('The BBC Micro is still loading. Drop the disc on the page again once it runs.', 'loading');
  });
  start.addEventListener('click', () => run(panel, say), { once: true });
}

async function run(panel, say) {
  const start = panel.querySelector('[data-bbc-start]');
  const screen = panel.querySelector('[data-bbc-screen]');
  const canvas = panel.querySelector('[data-bbc-canvas]');
  const speed = panel.querySelector('[data-bbc-speed]');
  const base = panel.dataset.base;
  const clockMhz = Number(panel.dataset.clockMhz);
  start.disabled = true;

  say('Loading the BBC Micro: the emulator and its three ROMs.', 'loading');
  // The ROMs are fetched while the runtime loads, and handed over once it is up.
  const roms = Promise.all(panel.dataset.roms.split(' ').map((file) => bytes(`${base}${file}`)));
  roms.catch(() => {});

  const context = canvas.getContext('2d');
  let image = null;
  let pixels = null;
  let shown = -1;
  const sound = soundOutput(panel);

  const onFrame = (host) => {
    if (host.Frames() !== shown) {
      shown = host.Picture();
      context.putImageData(image, 0, 0);
      panel.dataset.frames = String(shown);
    }
    if (sound.on) host.Sound();
  };

  const started = await startMachine({
    panel,
    base,
    name: 'BBC Micro',
    assembly: 'Dbhq.Machines.BbcMicro.Wasm',
    hostClass: 'BbcHost',
    load: async (host, runtime) => {
      runtime.setModuleImports('bbc-micro', {
        picture: (view) => view.copyTo(pixels),
        sound: (view) => sound.play(view),
      });
      const [os, basic, dfs] = await roms;
      host.Load(os, basic, dfs, 7, SAMPLE_RATE);
      canvas.width = host.ScreenWidth();
      canvas.height = host.ScreenHeight();
      image = context.createImageData(canvas.width, canvas.height);
      pixels = new Uint8Array(image.data.buffer);
    },
    clockMhz,
    clockOf: "the BBC Micro's",
    onFrame,
    onPause: () => sound.pause(),
    onResume: () => sound.resume(),
    say,
    speedEl: speed,
  });
  if (!started) return;
  const bbc = started.host;
  sound.machine = bbc;
  start.hidden = true;

  keyboard(screen, bbc, panel);
  const keys = onScreenKeys(panel, bbc);
  // A SHIFT or CTRL latched on screen lets go when the PC keyboard takes over,
  // and at BREAK, so it never lands on a key typed long after.
  screen.addEventListener('focus', keys.letGo);
  for (const b of panel.querySelectorAll('[data-bbc-break]')) {
    b.disabled = false;
    b.addEventListener('click', () => {
      keys.letGo();
      bbc.Break();
    });
  }
  sound.wire();
  disc(panel, bbc, { screen, letGo: keys.letGo });

  panel.bbc = { host: bbc, screenRow: (row) => bbc.ScreenRow(row) };
  panel.dispatchEvent(new CustomEvent('bbc:ready'));
  say('Running. The screen has the keyboard: type into the BBC Micro, or press Tab to move on.', 'running');
  // Start was the visitor asking to use the machine, so the keyboard goes to it.
  screen.focus({ preventScroll: true });
}

// ---- The keyboard ----

// Exported for tests/bbc-micro.test.mjs, which plays key events into it.
export function keyboard(screen, bbc, panel) {
  screen.tabIndex = 0;
  // Each BBC key down, with the PC keys holding it: two PC keys can press one
  // BBC key (both Shifts), and it comes up only when the last of them does.
  const held = new Map();
  const down = (code) => {
    const key = BBC_KEYS[PC_KEYS[code]];
    const codes = held.get(key) ?? new Set();
    if (codes.size === 0) bbc.KeyDown(key);
    codes.add(code);
    held.set(key, codes);
  };
  const up = (code) => {
    const key = BBC_KEYS[PC_KEYS[code]];
    const codes = held.get(key);
    if (!codes?.delete(code) || codes.size > 0) return;
    held.delete(key);
    bbc.KeyUp(key);
  };
  const letGo = () => {
    for (const key of held.keys()) bbc.KeyUp(key);
    held.clear();
  };

  screen.addEventListener('keydown', (event) => {
    if (event.altKey || event.metaKey) return;
    if (event.code === 'Pause') {
      event.preventDefault();
      bbc.Break();
      return;
    }
    if (!(event.code in PC_KEYS)) return;
    if (!event.ctrlKey) event.preventDefault();
    if (event.repeat) return;
    // CAPS LOCK is a press, whatever the PC does with its own light: some
    // systems send its key up only at the next press.
    if (event.code === 'CapsLock') {
      bbc.KeyDown(BBC_KEYS.CapsLock);
      bbc.KeyUp(BBC_KEYS.CapsLock);
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

// Exported for tests/bbc-micro.test.mjs.
export function onScreenKeys(panel, bbc) {
  const latched = new Set();
  const buttons = [...panel.querySelectorAll('[data-bbc-key]')];
  const show = () => {
    for (const b of buttons) if (STICKY.includes(b.dataset.bbcKey)) b.setAttribute('aria-pressed', String(latched.has(b.dataset.bbcKey)));
  };
  for (const b of buttons) {
    b.disabled = false;
    b.addEventListener('click', () => {
      const name = b.dataset.bbcKey;
      if (STICKY.includes(name)) {
        if (latched.has(name)) latched.delete(name);
        else latched.add(name);
      } else {
        const held = [...latched];
        for (const m of held) bbc.KeyDown(BBC_KEYS[m]);
        bbc.KeyDown(BBC_KEYS[name]);
        bbc.KeyUp(BBC_KEYS[name]);
        for (const m of held.reverse()) bbc.KeyUp(BBC_KEYS[m]);
        latched.clear();
      }
      show();
    });
  }
  // Lets go of a latched SHIFT or CTRL without typing anything.
  const letGo = () => {
    latched.clear();
    show();
  };
  return { letGo };
}

// ---- The sound ----

function soundOutput(panel) {
  const button = panel.querySelector('[data-bbc-sound]');
  const line = panel.querySelector('[data-bbc-sound-status]');
  const out = {
    on: false,
    machine: null,
    context: null,
    node: null,
    async turnOn() {
      // Made inside the click, as browsers ask, at the machine's own rate.
      this.context ??= new AudioContext({ sampleRate: SAMPLE_RATE });
      if (!this.node) {
        await this.context.audioWorklet.addModule('/bbc-audio.js');
        this.node = new AudioWorkletNode(this.context, 'bbc-sound', { numberOfInputs: 0, outputChannelCount: [1] });
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

// ---- The disc drive ----

// The library's list: the credit under it follows the disc chosen.
function library(panel) {
  const list = panel.querySelector('[data-bbc-preset]');
  if (!list) return;
  const show = () => {
    for (const about of panel.querySelectorAll('[data-bbc-preset-disc]')) about.hidden = about.dataset.bbcPresetDisc !== list.value;
  };
  list.addEventListener('change', show);
  list.disabled = false;
  show();
}

// Exported for tests/bbc-micro.test.mjs, which drives it with a fake panel.
export function disc(panel, bbc, { screen, letGo }) {
  const insert = panel.querySelector('[data-bbc-insert]');
  const file = panel.querySelector('[data-bbc-file]');
  const blank = panel.querySelector('[data-bbc-blank]');
  const save = panel.querySelector('[data-bbc-save]');
  const eject = panel.querySelector('[data-bbc-eject]');
  const protect = panel.querySelector('[data-bbc-protect]');
  const line = panel.querySelector('[data-bbc-drive]');
  let name = null;
  // The ticket of the latest insert, eject or blank disc (LATEST WINS above).
  let latest = 0;

  const describe = (text) => {
    line.textContent = text;
    panel.dataset.disc = name ?? '';
    save.disabled = eject.disabled = name === null;
  };
  const put = async (chosen) => {
    const kind = /\.(ssd|dsd)$/i.exec(chosen?.name ?? '')?.[1]?.toLowerCase();
    if (!kind) {
      describe(`${chosen?.name ?? 'That'} is not a disc image this drive takes: it takes .ssd and .dsd files.`);
      return;
    }
    // Refused before it is read: a disc this drive takes is never larger.
    if (chosen.size > MOST_BYTES) {
      describe(`${chosen.name} is ${chosen.size.toLocaleString('en-GB')} bytes, more than a disc holds (${MOST_BYTES.toLocaleString('en-GB')} bytes, 80 tracks on both sides), so it was not read.`);
      return;
    }
    // A file refused above leaves any insert in progress alone; one that is read takes a ticket.
    const ticket = ++latest;
    try {
      const data = new Uint8Array(await chosen.arrayBuffer());
      if (ticket !== latest) return;
      const tracks = bbc.InsertDisc(DRIVE, data, kind === 'dsd', protect.checked);
      name = chosen.name;
      describe(`In drive 0: ${name}, ${tracks} tracks, ${kind === 'dsd' ? 'double' : 'single'} sided${protect.checked ? ', write-protected' : ''}.`);
    } catch (error) {
      describe(`${chosen.name} was not taken: ${error.message}`);
    }
  };

  insert.addEventListener('click', () => file.click());
  file.addEventListener('change', () => {
    if (file.files.length > 0) put(file.files[0]);
    file.value = '';
  });
  // A file dropped anywhere on the page goes in the drive (prepare() listens).
  panel.putDisc = put;
  blank.addEventListener('click', () => {
    latest++;
    bbc.BlankDisc(DRIVE, BLANK_TRACKS, false);
    bbc.SetDiscReadOnly(DRIVE, protect.checked);
    name = 'blank.ssd';
    describe(`In drive 0: a blank disc, ${BLANK_TRACKS} tracks, single sided${protect.checked ? ', write-protected' : ''}. Save disc keeps it as ${name}.`);
  });
  save.addEventListener('click', () => {
    const data = bbc.SaveDisc(DRIVE);
    if (!data) return;
    const url = URL.createObjectURL(new Blob([data], { type: 'application/octet-stream' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  });
  eject.addEventListener('click', () => {
    latest++;
    bbc.EjectDisc(DRIVE);
    name = null;
    describe('Drive 0 is empty.');
  });
  protect.addEventListener('change', () => {
    bbc.SetDiscReadOnly(DRIVE, protect.checked);
    if (name !== null) describe(`In drive 0: ${name}${protect.checked ? ', write-protected' : ', writable'}.`);
  });
  // The library: the chosen disc, fetched from this site when it is inserted.
  const list = panel.querySelector('[data-bbc-preset]');
  const run = panel.querySelector('[data-bbc-preset-run]');
  const only = panel.querySelector('[data-bbc-preset-insert]');
  const preset = async (andRun) => {
    // The disc chosen when the button was pressed, even if the list changes while it is fetched.
    const slug = list.value;
    const { title, licence } = panel.querySelector(`[data-bbc-preset-disc="${slug}"]`).dataset;
    const ticket = ++latest;
    run.disabled = only.disabled = true;
    try {
      const data = await bytes(`${panel.dataset.base}discs/${slug}.ssd`);
      // Something else went in while this was fetched: that stays.
      if (ticket !== latest) return;
      const tracks = bbc.InsertDisc(DRIVE, data, false, protect.checked);
      name = `${slug}.ssd`;
      const how = andRun ? 'started with SHIFT and BREAK.' : 'type *CAT to list it, or press Insert and run to start it.';
      describe(`In drive 0: ${title} (${licence}), ${tracks} tracks, single sided${protect.checked ? ', write-protected' : ''}; ${how}`);
      if (andRun) {
        letGo();
        bbc.ShiftBreak();
        // The disc is for playing: the keyboard goes to the machine, and the
        // screen comes into view, so nobody types into a game they cannot see.
        // 'nearest' leaves the page where it is when the screen is already in
        // view, and 'auto' follows the page's own scroll-behavior, which is
        // instant for a visitor who asks for reduced motion.
        screen.focus({ preventScroll: true });
        screen.scrollIntoView({ block: 'nearest', behavior: 'auto' });
      }
    } catch (error) {
      if (ticket === latest) describe(`${title} could not be put in: ${error.message}`);
    } finally {
      run.disabled = only.disabled = false;
    }
  };
  run.addEventListener('click', () => preset(true));
  only.addEventListener('click', () => preset(false));

  for (const control of [insert, blank, protect, run, only]) control.disabled = false;
  describe('Drive 0 is empty.');
}

async function bytes(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return new Uint8Array(await response.arrayBuffer());
}
