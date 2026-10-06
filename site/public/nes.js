// The NES on its page: when the visitor presses Start, loads the machine (.NET
// WebAssembly) and its cartridge, runs it at its region's own clock on the
// shared host (machine-host.js), draws its picture on the canvas, plays its
// sound once asked to, and takes its two controllers from the keyboard, from
// gamepads and from the on-screen pad. An external module because the site's
// CSP allows no inline script.
//
// The page works without this file: the panel is drawn, every control is
// disabled, and the status line says the machine needs JavaScript.
//
// NOTHING DOWNLOADS UNTIL START. The machine is megabytes, so the page loads
// none of it until the visitor asks: the Start button says how much it is,
// from the built files (the page gives the figure, as the BBC Micro's does).
//
// THE CARTRIDGE. Start loads the panel's own cartridge, data-rom under
// data-base, the bundled homebrew, fetched while the runtime loads. Without
// one, or if it cannot be fetched, the machine waits for the visitor's: a .nes
// file chosen with the button or dropped on the page. The file is read in this
// tab and never sent anywhere. A file larger than the largest the machine
// loads is refused before it is read, by its size, with the limit the machine
// gives (NesHost.MaxRomBytes, the library's Cartridge.MaxFileSize), never a
// figure typed here. A file the machine cannot run (not a NES file, cut short,
// an unmodelled board, the Dendy) is refused with the cartridge's own
// sentence, and the machine running carries on as it was: RegionOf checks the
// file before Load is asked, and Load replaces the machine only once the new
// one is built. The picker opens only once the bundled cartridge has been
// tried, so a file the visitor chooses is never replaced by it.
//
// THE REGION. Each new cartridge starts in the region its header names, or
// NTSC when it does not say, and the line under the control says which and why
// (the sentence is the machine's, NesHost.Load, and so is the region the
// control shows, NesHost.Region). Changing the control restarts
// the machine in that region and says so. The run loop's clock follows: it is
// the region's own, from NesHost.CpuHz, read after every load.
//
// THE PICTURE. The PPU draws 256 by 240 pixels. Each frame, if it has
// finished a frame since the last one, the host hands over a view of the frame
// buffer's own memory through the `picture` function registered below, and it
// is copied once, straight into the canvas's ImageData. The canvas is shown in
// the region's pixel shape (docs/nes/facts/ppu.md 10, from the nesdev wiki's
// Overscan page): 8:7 on NTSC, so 256 pixels are as wide as 292.6 square ones,
// and about 1.386:1 on PAL, so they are as wide as 355. It models the picture,
// not a television.
//
// THE CONTROLLERS. Controller 1 is the keyboard while the screen has focus
// (KEYS, by event.code, so by place and not by what the key types), the
// on-screen pad, and the first gamepad; controller 2 is the second gamepad.
// Gamepads are read by their index in the browser's list, in the standard
// layout (GAMEPAD_BUTTONS); a pad the browser does not know the layout of is
// read the same way and may have its buttons elsewhere. Every source's buttons
// are joined and sent as they are: Left and Right together, or Up and Down,
// reach the machine as pressed (the plan's Review Focus 4), as the machine's
// Controller takes any mask. Tab is never taken and Escape lets go of the
// screen, so a keyboard user can always move on. Focus leaving the screen, or
// the page being hidden, lets go of every key.
//
// THE SOUND is off until the visitor turns it on, because browsers do not let
// a page start sound by itself. Then each frame's samples go to nes-audio.js,
// an AudioWorklet, which plays them from a ring that never grows. While sound
// is off, or the page is hidden, the machine's own buffer drops its oldest
// samples rather than growing (SampleBuffer).
//
// RESET AND POWER. Reset is the console's button: the game starts again and
// the cartridge's RAM is kept. Power switches it off and on, which clears that
// RAM, a battery-backed save included. Saving is not built, so reloading the
// page loses it too; the panel says so.
//
// FOR THE SITE'S BROWSER CHECK, once running the panel carries `panel.nes`:
//   host           the machine (NesHost).
//   stepTo(n)      holds the run loop, lets go of every button, switches the
//                  machine off and on, runs it until exactly n frames have
//                  completed (NesHost.RunFrames), and paints that frame on the
//                  canvas, so the canvas's bytes can be hashed and compared
//                  with machines/nes/expected-frames.json at exactly n frames.
//                  Returns the frames completed. The loop stays held.
//   play()         lets go of the loop again.
//   region()       the region running, "NTSC" or "PAL".
import { startMachine } from '/machine-host.js';
import { BUTTONS, KEYS, GAMEPAD_BUTTONS } from '/nes-keys.js';

// The buttons, the keys and the gamepad's buttons: nes-keys.js, shared with the panel's markup.
export { BUTTONS, KEYS, GAMEPAD_BUTTONS };

// The sample rate the machine makes its sound at, and the rate the page asks
// the browser to play it at, so it is not resampled twice. The machine's default.
const SAMPLE_RATE = 48_000;

// The picture's size in pixels, FrameBuffer.Width and Height, and each region's
// pixel shape, width over height, from ppu.md 10: 8:7 on NTSC, and on PAL the
// shape that makes 256 pixels as wide as 355 square ones (about 1.3862:1).
const WIDTH = 256;
const HEIGHT = 240;
const PIXEL = { NTSC: [8, 7], PAL: [355, 256] };

const fmt = (n) => n.toLocaleString('en-GB');
const messageOf = (error) => (typeof error?.message === 'string' && error.message) || String(error) || 'it could not be read';

// ---- The pure parts, exported for tests/nes-panel.test.mjs ----

/** The buttons held by a set of button names. */
const maskOf = (names) => {
  let mask = 0;
  for (const name of names) mask |= BUTTONS[name];
  return mask;
};

/** The buttons a gamepad holds, read through GAMEPAD_BUTTONS. */
export function gamepadMask(gamepad) {
  let mask = 0;
  for (const [index, name] of Object.entries(GAMEPAD_BUTTONS)) {
    if (gamepad?.buttons?.[index]?.pressed) mask |= BUTTONS[name];
  }
  return mask;
}

/**
 * The two controllers' masks: controller 1 is the keys, the on-screen pad and
 * the first gamepad joined; controller 2 is the second gamepad. Nothing is
 * filtered.
 */
export function padMasks({ keys, touch }, gamepads) {
  return [keys | touch | gamepadMask(gamepads[0]), gamepadMask(gamepads[1])];
}

/**
 * Puts a cartridge in: asks the host to check the file (RegionOf refuses one
 * it cannot run, before anything changes), then loads it in `region` ("" for
 * the header's, else NTSC, which is the host's rule) and reads back the region
 * it chose, so the page never works one out. Returns { ok, region, sentence },
 * or { ok: false, message } with the cartridge's sentence, never throwing.
 */
export function loadCartridge(host, bytes, region, sampleRate) {
  try {
    host.RegionOf(bytes);
    const sentence = host.Load(bytes, region, sampleRate);
    return { ok: true, region: host.Region(), sentence };
  } catch (error) {
    return { ok: false, message: messageOf(error) };
  }
}

/**
 * The first cartridge: the bundled one, `bundled` (a promise of its bytes, or
 * of null), is tried with `use` before `openPicker` opens the picker, so a
 * file the visitor chooses is never replaced by it. Without one, or if it will
 * not load, the picker opens and `waitForFile` waits for the visitor's.
 */
export async function firstCartridge(bundled, use, openPicker, waitForFile) {
  const data = await bundled;
  const loaded = Boolean(data) && use(data);
  openPicker();
  if (!loaded) await waitForFile();
}

/** The line for a cartridge put in; the line under the region control is the host's sentence. */
export const cartridgeLine = (name) => `${name} is in.`;

/** The line for a change of region. */
export const regionChangeLine = (result) => `The region changed, so the machine restarted. ${result.sentence}`;

/** The line for a file that was refused; `running` says whether a machine carries on. */
export const badFileLine = (name, result, running) =>
  `${name} was not loaded. ${result.message} ${running ? 'The machine carries on as it was.' : 'Choose another file.'}`;

/** The picture's displayed size, in square pixels, for a region. */
export function pictureShape(region) {
  const [across, down] = PIXEL[region] ?? PIXEL.NTSC;
  return { width: (WIDTH * across) / down, height: HEIGHT };
}

/** Shows the canvas in a region's pixel shape. */
export function showShape(canvas, region) {
  const { width, height } = pictureShape(region);
  canvas.style.aspectRatio = `${width} / ${height}`;
  canvas.dataset.region = region;
}

/** The line refusing a file over `limit` bytes, or null when it fits. */
export function sizeRefusal(file, limit) {
  if (file.size <= limit) return null;
  return `${file.name} is ${fmt(file.size)} bytes, more than the ${fmt(limit)} bytes of the largest file this machine loads, so it was not read.`;
}

/** Reads a chosen file, unless it is over `limit` bytes: { ok, bytes } or { ok: false, line }. */
export async function readRom(file, limit) {
  const refusal = sizeRefusal(file, limit);
  if (refusal) return { ok: false, line: refusal };
  try {
    return { ok: true, bytes: new Uint8Array(await file.arrayBuffer()) };
  } catch (error) {
    return { ok: false, line: `${file.name} could not be read: ${messageOf(error)}` };
  }
}

// ---- The keyboard and the on-screen pad ----

/**
 * Controller 1's keys on the screen: calls onMask(mask) whenever the keys held
 * change. Tab is left to the browser, as is anything with Alt, Ctrl or the
 * Command key; Escape lets go of the screen.
 */
export function keyboard(screen, onMask) {
  const held = new Set();
  const send = () => onMask(maskOf([...held].map((code) => KEYS[code])));
  const letGo = () => {
    if (held.size === 0) return;
    held.clear();
    send();
  };
  screen.addEventListener('keydown', (event) => {
    if (event.code === 'Escape') {
      screen.blur();
      return;
    }
    if (event.altKey || event.metaKey || event.ctrlKey || !(event.code in KEYS)) return;
    event.preventDefault();
    if (event.repeat || held.has(event.code)) return;
    held.add(event.code);
    send();
  });
  screen.addEventListener('keyup', (event) => {
    if (held.delete(event.code)) send();
  });
  screen.addEventListener('blur', letGo);
  globalThis.document?.addEventListener('visibilitychange', () => { if (globalThis.document.hidden) letGo(); });
}

/**
 * The on-screen pad: each button is held while a finger, or the mouse, is on
 * it, several at once included, and calls onMask(mask) as they change. A
 * button pressed from the keyboard (Enter or Space on it) is held for a moment,
 * long enough for a game to read it.
 */
export function touchPad(panel, onMask) {
  const held = new Set();
  const send = () => onMask(maskOf(held));
  for (const b of panel.querySelectorAll('[data-nes-button]')) {
    const name = b.dataset.nesButton;
    const release = () => {
      if (held.delete(name)) send();
    };
    b.disabled = false;
    b.addEventListener('pointerdown', (event) => {
      event.preventDefault();
      // Captured, so the button hears its finger lift even off its edge; a pointer the browser
      // no longer tracks cannot be, and the button is held all the same.
      try {
        b.setPointerCapture?.(event.pointerId);
      } catch {
        // Nothing to capture.
      }
      held.add(name);
      send();
    });
    b.addEventListener('pointerup', release);
    b.addEventListener('pointercancel', release);
    b.addEventListener('lostpointercapture', release);
    b.addEventListener('contextmenu', (event) => event.preventDefault());
    b.addEventListener('click', (event) => {
      // detail 0 is a click from the keyboard; a pointer's was handled above.
      if (event.detail !== 0) return;
      held.add(name);
      send();
      setTimeout(release, 150);
    });
  }
}

// ---- The page ----

for (const panel of globalThis.document?.querySelectorAll('[data-nes]') ?? []) prepare(panel);

function prepare(panel) {
  const start = panel.querySelector('[data-nes-start]');
  const status = panel.querySelector('[data-nes-status]');
  const say = (text, state) => {
    status.textContent = text;
    panel.dataset.state = state;
  };
  if (!panel.dataset.download) {
    say('This copy of the site was built without the NES\'s files, so it cannot run here.', 'missing');
    return;
  }
  start.disabled = false;
  say('Press Start to download the NES and run it.', 'ready');
  // A file dropped on the page goes in once the machine is up (run() sets
  // panel.putRom). Before that it is refused here, rather than the browser
  // leaving the page to show it.
  document.addEventListener('dragover', (event) => {
    if ([...(event.dataTransfer?.types ?? [])].includes('Files')) event.preventDefault();
  });
  document.addEventListener('drop', (event) => {
    if (!event.dataTransfer?.files.length) return;
    event.preventDefault();
    if (panel.putRom) panel.putRom(event.dataTransfer.files[0]);
    else if (panel.dataset.state === 'ready') say('Press Start first, then drop the file on the page again.', 'ready');
    else if (panel.dataset.state === 'loading') say('The NES is still loading. Drop the file on the page again once it runs.', 'loading');
  });
  start.addEventListener('click', () => run(panel, say), { once: true });
}

async function run(panel, say) {
  const start = panel.querySelector('[data-nes-start]');
  const screen = panel.querySelector('[data-nes-screen]');
  const canvas = panel.querySelector('[data-nes-canvas]');
  const speed = panel.querySelector('[data-nes-speed]');
  const cartLine = panel.querySelector('[data-nes-cartridge]');
  const regionLine = panel.querySelector('[data-nes-region-line]');
  const radios = [...panel.querySelectorAll('[data-nes-region]')];
  const base = panel.dataset.base;
  const bundledFile = panel.dataset.rom;
  const bundledName = panel.dataset.romTitle || bundledFile;
  start.disabled = true;

  say('Loading the NES: the emulator and its cartridge.', 'loading');
  // The bundled cartridge is fetched while the runtime loads; if it cannot be, the visitor picks one.
  const bundled = bundledFile ? bytes(`${base}${bundledFile}`).catch(() => null) : Promise.resolve(null);

  const context = canvas.getContext('2d');
  const image = context.createImageData(WIDTH, HEIGHT);
  const pixels = new Uint8Array(image.data.buffer);
  const sound = soundOutput(panel);
  const input = { keys: 0, touch: 0, sent: [-1, -1] };
  let shown = -1;
  let clockMhz = 0;
  let host = null;
  let rom = null;
  let region = 'NTSC';
  let running = false;
  let cartridgeArrived = null;

  const paint = () => {
    shown = host.Picture();
    context.putImageData(image, 0, 0);
    panel.dataset.frames = String(shown);
  };
  const sendButtons = (masks) => {
    for (const pad of [0, 1]) {
      if (masks[pad] !== input.sent[pad]) {
        host.SetButtons(pad, masks[pad]);
        input.sent[pad] = masks[pad];
      }
    }
  };
  const onFrame = () => {
    sendButtons(padMasks(input, navigator.getGamepads?.() ?? []));
    if (host.Frames() !== shown) paint();
    if (sound.on) host.Sound();
  };

  // A machine has just been built or switched on: the clock, the picture's shape and the region control follow it.
  const loaded = (result) => {
    region = result.region;
    clockMhz = host.CpuHz() / 1e6;
    showShape(canvas, region);
    for (const r of radios) r.checked = r.value === region;
    panel.dataset.region = region;
    input.sent = [-1, -1];
    shown = -1;
    if (sound.on) sound.fresh();
  };
  // A cartridge's bytes, from the bundle or the visitor: put in, or refused with the machine as it was.
  const use = (data, name) => {
    const result = loadCartridge(host, data, '', SAMPLE_RATE);
    if (!result.ok) {
      cartLine.textContent = badFileLine(name, result, running || rom !== null);
      return false;
    }
    rom = data;
    loaded(result);
    cartLine.textContent = cartridgeLine(name);
    regionLine.textContent = result.sentence;
    panel.dataset.cartridge = name;
    cartridgeArrived?.();
    cartridgeArrived = null;
    return true;
  };
  const put = async (file) => {
    const read = await readRom(file, host.MaxRomBytes());
    if (!read.ok) {
      cartLine.textContent = `${read.line} ${rom !== null ? 'The machine carries on as it was.' : 'Choose another file.'}`;
      return;
    }
    use(read.bytes, file.name);
  };

  const started = await startMachine({
    panel,
    base,
    name: 'NES',
    assembly: 'Dbhq.Machines.Nes.Wasm',
    hostClass: 'NesHost',
    load: async (nes, runtime) => {
      runtime.setModuleImports('nes', {
        picture: (view) => view.copyTo(pixels),
        sound: (view) => sound.play(view),
      });
      host = nes;
      await firstCartridge(bundled, (data) => use(data, bundledName), () => picker(panel, put), () => {
        say('Choose a .nes file, or drop one on the page: the NES starts once it has a cartridge.', 'waiting');
        return new Promise((resolve) => { cartridgeArrived = resolve; });
      });
    },
    clockMhz: () => clockMhz,
    clockOf: "the NES's",
    onFrame,
    onPause: () => sound.pause(),
    onResume: () => sound.resume(),
    say,
    speedEl: speed,
  });
  if (!started) return;
  running = true;
  sound.machine = host;
  start.hidden = true;

  keyboard(screen, (mask) => { input.keys = mask; });
  touchPad(panel, (mask) => { input.touch = mask; });
  screen.tabIndex = 0;
  screen.addEventListener('focus', () => { panel.dataset.typing = 'true'; });
  screen.addEventListener('blur', () => { delete panel.dataset.typing; });
  pads(panel);

  for (const r of radios) {
    r.disabled = false;
    r.addEventListener('change', () => {
      if (!r.checked || r.value === region || rom === null) return;
      const result = loadCartridge(host, rom, r.value, SAMPLE_RATE);
      if (!result.ok) {
        regionLine.textContent = `The region could not change: ${result.message}`;
        for (const other of radios) other.checked = other.value === region;
        return;
      }
      loaded(result);
      regionLine.textContent = regionChangeLine(result);
    });
  }
  const line = panel.querySelector('[data-nes-power-line]');
  const reset = panel.querySelector('[data-nes-reset]');
  const power = panel.querySelector('[data-nes-power]');
  reset.disabled = power.disabled = false;
  reset.addEventListener('click', () => {
    host.Reset();
    line.textContent = 'Reset: the game started again, and the cartridge\'s RAM was kept.';
  });
  power.addEventListener('click', () => {
    host.PowerCycle();
    shown = -1;
    line.textContent = 'Switched off and on: the game started again, and the cartridge\'s RAM, a saved game included, was cleared.';
  });
  sound.wire();

  panel.nes = {
    host,
    stepTo(frames) {
      started.hold(true);
      sendButtons([0, 0]);
      host.PowerCycle();
      host.RunFrames(frames);
      paint();
      return host.Frames();
    },
    play: () => started.hold(false),
    region: () => region,
  };
  panel.dispatchEvent(new CustomEvent('nes:ready'));
  say('Running. The screen has the keyboard: play with the keys below, or press Tab to move on.', 'running');
  // Start was the visitor asking to play, so the keyboard goes to the machine.
  screen.focus({ preventScroll: true });
}

// ---- The cartridge picker ----

function picker(panel, put) {
  const choose = panel.querySelector('[data-nes-choose]');
  const file = panel.querySelector('[data-nes-file]');
  choose.disabled = false;
  choose.addEventListener('click', () => file.click());
  file.addEventListener('change', () => {
    if (file.files.length > 0) put(file.files[0]);
    file.value = '';
  });
  // A file dropped anywhere on the page goes in too (prepare() listens).
  panel.putRom = put;
}

// ---- Gamepads ----

function pads(panel) {
  const line = panel.querySelector('[data-nes-pads]');
  const describe = () => {
    const found = [...(navigator.getGamepads?.() ?? [])].slice(0, 2);
    const named = found.map((g, i) => (g ? `controller ${i + 1} is ${g.id}` : null)).filter(Boolean);
    line.textContent = named.length > 0
      ? `Gamepads: ${named.join('; ')}.`
      : 'No gamepad yet: plug one in and press a button on it, and the first is controller 1, the second controller 2.';
  };
  window.addEventListener('gamepadconnected', describe);
  window.addEventListener('gamepaddisconnected', describe);
  describe();
}

// ---- The sound ----

function soundOutput(panel) {
  const button = panel.querySelector('[data-nes-sound]');
  const line = panel.querySelector('[data-nes-sound-status]');
  return {
    on: false,
    machine: null,
    context: null,
    node: null,
    async turnOn() {
      // Made inside the click, as browsers ask, at the machine's own rate.
      this.context ??= new AudioContext({ sampleRate: SAMPLE_RATE });
      if (!this.node) {
        await this.context.audioWorklet.addModule('/nes-audio.js');
        this.node = new AudioWorkletNode(this.context, 'nes-sound', { numberOfInputs: 0, outputChannelCount: [1] });
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
      this.node?.port.postMessage({ flush: true });
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
          line.textContent = `Sound could not start, so it is off: ${messageOf(error)}`;
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
}

async function bytes(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return new Uint8Array(await response.arrayBuffer());
}
