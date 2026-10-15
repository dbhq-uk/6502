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
// SHARED PARTS. The cartridge slot (cartridge-picker.js), the region radios
// (region-line.js), the gamepad poll (gamepads.js), the on-screen buttons
// (touch-pad.js), the sound button (sound-output.js) and the worklet's ring
// (sample-worklet.js, through nes-audio.js) were moved out of this file and
// nes-audio.js, unchanged in what they do, when the Atari 2600 became the
// second console to need them (the Atari 2600 plan's task 13). This file keeps
// the NES's own: its buttons and keys, its cartridge and region rules, its
// picture, and its page. What its tests import from here, it still exports.
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
// FOR THE MODELS AND THE SITE'S BROWSER CHECK, the panel carries `panel.nes`
// from the moment the page is ready, before Start, so a model loaded early can
// ask it whether the machine runs and which region the page has:
//   running()      false until Start has the machine running, then true. The
//                  page never stops it: it has no power-off, and Power and
//                  Reset restart the game, not the page. A hidden tab pauses
//                  the run loop, and stepTo holds it, but the machine stays
//                  switched on, so neither makes running() false.
//   region()       the region the page has now, "NTSC" or "PAL": the region
//                  control's before Start, the machine's (NesHost.Region)
//                  after. Upper case, as the host gives it; a model takes
//                  region().toLowerCase().
//   accessCounts() the CPU's accesses to each chip since the machine's power
//                  on, NesHost.AccessCounts: four wrapping counters as signed
//                  32-bit ints, in NesChip order Ppu, Apu, Pad1, Pad2, in an
//                  Int32Array (a copy: JSExport hands an int[] over so). A
//                  reader takes differences, wrapped: (b - a) | 0. null
//                  before Start.
//   reset()        does exactly what the Reset button does. Nothing before Start.
// and once running:
//   host           the machine (NesHost).
//   stepTo(n)      holds the run loop, lets go of every button, switches the
//                  machine off and on, runs it until exactly n frames have
//                  completed (NesHost.RunFrames), and paints that frame on the
//                  canvas, so the canvas's bytes can be hashed and compared
//                  with machines/nes/expected-frames.json at exactly n frames.
//                  Returns the frames completed. The loop stays held.
//   play()         lets go of the loop again.
//
// THE EVENTS, each a CustomEvent on the panel:
//   nes:ready      once running, as panel.nes gains host, stepTo and play.
//   nes:start      a machine is running with its counters from zero: after
//                  Start, after Power and stepTo (both switch it off and on),
//                  after a new cartridge goes in while it runs, and after a
//                  change of region (after its nes:region). A model takes a
//                  fresh baseline for accessCounts() on it, and reads
//                  region() again: the cartridge Start puts in may name the
//                  other region from the one the control showed before, and
//                  no nes:region is sent for that.
//   nes:reset      after every reset, the Reset button's and reset()'s alike.
//                  The counters carry on.
//   nes:region     detail { region: 'ntsc' | 'pal' }, lower case: after a
//                  change of region has restarted the machine and it runs,
//                  from the control or a new cartridge whose file names the
//                  other region. nes:start follows it.
import { startMachine } from '/machine-host.js';
import { BUTTONS, KEYS, GAMEPAD_BUTTONS } from '/nes-keys.js';
import { bytes, dropTarget, firstCartridge, messageOf, picker, readRom, sizeRefusal } from '/cartridge-picker.js';
import { connected, gamepadBits, watchPads } from '/gamepads.js';
import { checkRegion, wireRegion } from '/region-line.js';
import { soundOutput } from '/sound-output.js';
import { holdButtons } from '/touch-pad.js';

// The buttons, the keys and the gamepad's buttons: nes-keys.js, shared with the panel's markup.
export { BUTTONS, KEYS, GAMEPAD_BUTTONS };
// The cartridge slot's rules, shared with the Atari 2600's page since its task 13: cartridge-picker.js.
export { firstCartridge, readRom, sizeRefusal };

// The sample rate the machine makes its sound at, and the rate the page asks
// the browser to play it at, so it is not resampled twice. The machine's default.
const SAMPLE_RATE = 48_000;

// The picture's size in pixels, FrameBuffer.Width and Height, and each region's
// pixel shape, width over height, from ppu.md 10: 8:7 on NTSC, and on PAL the
// shape that makes 256 pixels as wide as 355 square ones (about 1.3862:1).
const WIDTH = 256;
const HEIGHT = 240;
const PIXEL = { NTSC: [8, 7], PAL: [355, 256] };

// ---- The pure parts, exported for tests/nes-panel.test.mjs ----

/** The buttons held by a set of button names. */
const maskOf = (names) => {
  let mask = 0;
  for (const name of names) mask |= BUTTONS[name];
  return mask;
};

/** The buttons a gamepad holds, read through GAMEPAD_BUTTONS. */
export function gamepadMask(gamepad) {
  return gamepadBits(gamepad, GAMEPAD_BUTTONS, BUTTONS);
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

/** The line for a cartridge put in; the line under the region control is the host's sentence. */
export const cartridgeLine = (name) => `${name} is in.`;

/** Dispatches one of the panel's events, `nes:<name>`, with its detail if it has one. */
export const announce = (panel, name, detail) =>
  panel.dispatchEvent(new CustomEvent(`nes:${name}`, detail === undefined ? undefined : { detail }));

/** Announces a region the machine now runs in: the one place the page's "NTSC" or "PAL" is lower-cased. */
export const announceRegion = (panel, region) => announce(panel, 'region', { region: region.toLowerCase() });

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
  holdButtons(panel.querySelectorAll('[data-nes-button]'), (b) => b.dataset.nesButton, (held) => onMask(maskOf(held)));
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
  // What a model may ask before Start; run() gives each its running meaning.
  panel.nes = {
    running: () => false,
    region: () => panel.querySelector('[data-nes-region]:checked').value,
    accessCounts: () => null,
    reset: () => {},
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
  dropTarget(panel, say, 'NES');
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
  const sound = soundOutput({
    panel,
    button: panel.querySelector('[data-nes-sound]'),
    line: panel.querySelector('[data-nes-sound-status]'),
    module: '/nes-audio.js',
    processor: 'nes-sound',
    rate: SAMPLE_RATE,
  });
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
    sendButtons(padMasks(input, connected()));
    if (host.Frames() !== shown) paint();
    if (sound.on) host.Sound();
  };

  // A machine has just been built or switched on: the clock, the picture's shape and the region control follow it.
  const loaded = (result) => {
    region = result.region;
    clockMhz = host.CpuHz() / 1e6;
    showShape(canvas, region);
    checkRegion(radios, region);
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
    const before = region;
    rom = data;
    loaded(result);
    cartLine.textContent = cartridgeLine(name);
    regionLine.textContent = result.sentence;
    panel.dataset.cartridge = name;
    cartridgeArrived?.();
    cartridgeArrived = null;
    // A new machine while one runs: its counters start from zero, and its region may be the other.
    if (running) {
      if (region !== before) announceRegion(panel, region);
      announce(panel, 'start');
    }
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
      await firstCartridge(bundled, (data) => use(data, bundledName), () => {
        picker(panel.querySelector('[data-nes-choose]'), panel.querySelector('[data-nes-file]'), put);
        // A file dropped anywhere on the page goes in too (prepare() listens).
        panel.putRom = put;
      }, () => {
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
  watchPads(panel.querySelector('[data-nes-pads]'), 'controller');

  wireRegion(radios, () => region, (value) => {
    if (rom === null) return true;
    const result = loadCartridge(host, rom, value, SAMPLE_RATE);
    if (!result.ok) {
      regionLine.textContent = `The region could not change: ${result.message}`;
      return false;
    }
    loaded(result);
    regionLine.textContent = regionChangeLine(result);
    // A new machine: the region first, so a model redraws, then its counters from zero.
    announceRegion(panel, region);
    announce(panel, 'start');
    return true;
  });
  const line = panel.querySelector('[data-nes-power-line]');
  const reset = panel.querySelector('[data-nes-reset]');
  const power = panel.querySelector('[data-nes-power]');
  reset.disabled = power.disabled = false;
  const resetMachine = () => {
    host.Reset();
    line.textContent = 'Reset: the game started again, and the cartridge\'s RAM was kept.';
    announce(panel, 'reset');
  };
  reset.addEventListener('click', resetMachine);
  power.addEventListener('click', () => {
    host.PowerCycle();
    shown = -1;
    line.textContent = 'Switched off and on: the game started again, and the cartridge\'s RAM, a saved game included, was cleared.';
    announce(panel, 'start');
  });
  sound.wire();

  Object.assign(panel.nes, {
    host,
    stepTo(frames) {
      started.hold(true);
      sendButtons([0, 0]);
      host.PowerCycle();
      host.RunFrames(frames);
      paint();
      announce(panel, 'start');
      return host.Frames();
    },
    play: () => started.hold(false),
    region: () => region,
    running: () => running,
    accessCounts: () => host.AccessCounts(),
    reset: resetMachine,
  });
  announce(panel, 'ready');
  announce(panel, 'start');
  say('Running. The screen has the keyboard: play with the keys below, or press Tab to move on.', 'running');
  // Start was the visitor asking to play, so the keyboard goes to the machine.
  screen.focus({ preventScroll: true });
}
