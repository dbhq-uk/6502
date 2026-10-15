// The Atari 2600 on its page: when the visitor presses Start, loads the machine
// (.NET WebAssembly) and its cartridge, runs it at its region's own clock on
// the shared host (machine-host.js), draws its picture on the canvas, plays its
// sound once asked to, and takes its two joysticks and its console switches
// from the keyboard, from gamepads and from the buttons on the panel. An
// external module because the site's CSP allows no inline script.
//
// The page works without this file: the panel is drawn, every control is
// disabled, and the status line says the machine needs JavaScript.
//
// NOTHING DOWNLOADS UNTIL START. The machine is megabytes, so the page loads
// none of it until the visitor asks: the Start button says how much it is,
// from the built files.
//
// SHARED PARTS, with the NES's page since the Atari 2600 plan's task 13: the
// cartridge slot (cartridge-picker.js), the region radios (region-line.js),
// the gamepad poll (gamepads.js), the held buttons (touch-pad.js), the sound
// button (sound-output.js) and the worklet's ring (sample-worklet.js).
//
// THE CARTRIDGE. Start loads the panel's own cartridge, data-rom under
// data-base, the bundled homebrew, fetched while the runtime loads. Without
// one, or if it cannot be fetched, the machine waits for the visitor's: a
// file chosen with the button or dropped on the page, read in this tab and
// never sent anywhere. A file larger than the largest the machine loads is
// refused before it is read, by its size, with the limit the machine gives
// (Atari2600Host.MaxRomBytes, the library's Cartridge.MaxFileSize), never a
// figure typed here. A file no modelled board takes is refused with the
// cartridge's own sentence, and the machine running carries on as it was:
// Load replaces the machine only once the new cartridge has been read. The
// line under the slot says which board the file was run as, which is chosen
// by its size (the host's sentence).
//
// THE REGION starts at NTSC, as a 2600 cartridge has no header to say. A change
// switches the palette and the clock at once, with no restart, as region.md
// section 5 found nothing else a program can see differ (Atari2600Host.
// SetRegion). The run loop's clock follows: it is the region's own, from
// Atari2600Host.CpuHz, read after every change. The page never changes the
// region by itself: under the control it shows the lines the last frame had,
// and suggests PAL when the median of the last SUGGEST_FRAMES frames' lines is
// nearer PAL's nominal count than NTSC's and the region is NTSC (the plan's
// "Fixed before running", item 13).
//
// THE PICTURE is the region's window of the frame (FrameBuffer.ToRgba), 160
// pixels by the window's lines, handed over each time a frame finishes as a
// view of the host's own memory, through the `picture` function registered
// below, and copied once into the canvas's ImageData. A short frame shows
// black below its last line and a long one is cut at the window, as it is: it
// is not stretched. The canvas is shown in the region's pixel shape (WINDOWS,
// from region.md section 4). It models the picture, not a television.
//
// THE JOYSTICKS AND THE SWITCHES. Joystick 1 is the arrow keys and Space,
// joystick 2 W, A, S, D and F, while the screen has focus (STICK_KEYS, by
// event.code, so by place and not by what the key types); joystick 1 is also
// the panel's touch pad; gamepads go by their index, the first to joystick 1
// and the second to joystick 2. Reset and Select are held while their key, their
// button on the panel or their gamepad button is down; the colour switch and
// the two difficulty switches flip on each press of their key or button and
// stay. Every source is joined and sent as it is: up and down together, every
// direction and both fire buttons at once, reach the machine as pressed (the
// plan's Review Focus 4). Tab is never taken and Escape lets go of the screen.
// Focus leaving the screen, or the page being hidden, lets go of every key held,
// and the switches that stay stay.
//
// THE SOUND is off until the visitor turns it on. The TIA's samples run from 0
// to 1, so a held level is a steady offset (the task 10 journal), which
// dcBlocker takes out before they are posted, so silence is 0 however the
// volume was left.
//
// FOR THE SITE'S BROWSER CHECK, the panel carries `panel.vcs` from the moment
// the page is ready: running() and region() before Start, and once running
// host, the machine (Atari2600Host); stepTo(n), which holds the run loop, lets
// go of both joysticks and of Reset and Select (the switches that stay stay as
// the panel shows them), switches the machine off and on, runs it until
// exactly n frames have completed, and paints that frame, so the canvas can be
// compared with machines/atari-2600/expected-frames.json at exactly n frames,
// which matters because the bundled game's title changes every frame; and
// play(), which lets go of the loop again. The panel dispatches vcs:ready once
// running.
import { startMachine } from '/machine-host.js';
import { bytes, dropTarget, firstCartridge, messageOf, picker, readRom } from '/cartridge-picker.js';
import { connected, gamepadBits, stickDirections, watchPads } from '/gamepads.js';
import { checkRegion, wireRegion } from '/region-line.js';
import { soundOutput } from '/sound-output.js';
import { holdButtons } from '/touch-pad.js';
import { GAMEPAD_STICK, GAMEPAD_SWITCHES, HELD_SWITCHES, KEY_NAMES, STICK, STICK_KEYS, SWITCHES, SWITCH_KEYS, WIDTH, WINDOWS } from '/atari-2600-keys.js';

export { GAMEPAD_STICK, GAMEPAD_SWITCHES, HELD_SWITCHES, KEY_NAMES, STICK, STICK_KEYS, SWITCHES, SWITCH_KEYS, WIDTH, WINDOWS };

// The sample rate the machine makes its sound at, and the rate the page asks
// the browser to play it at, so it is not resampled twice. The machine's default.
const SAMPLE_RATE = 48_000;

/** The frames whose lines the PAL suggestion takes the median of (the plan's item 13). */
export const SUGGEST_FRAMES = 30;

/** The corner of dcBlocker's high-pass, in hertz: below hearing, so it takes out the offset and leaves the tones. */
export const DC_CORNER_HZ = 20;

// ---- The pure parts, exported for tests/atari-2600-panel.test.mjs ----

/** The bits of a set of names in `bits` (STICK or SWITCHES). */
export const maskOf = (names, bits) => {
  let mask = 0;
  for (const name of names) mask |= bits[name];
  return mask;
};

/**
 * What a set of keys held (event.code values) holds: each joystick's inputs,
 * and the switches that are held while their key is down. A key for a switch
 * that stays holds nothing here: its press flips it (keyboard's onToggle).
 */
export function keyState(codes) {
  const sticks = [0, 0];
  let switches = 0;
  for (const code of codes) {
    const key = STICK_KEYS[code];
    if (key) sticks[key.stick] |= STICK[key.input];
    const name = SWITCH_KEYS[code];
    if (name && HELD_SWITCHES.includes(name)) switches |= SWITCHES[name];
  }
  return { sticks, switches };
}

/** A gamepad's joystick: the d-pad and the bottom face button, and the left stick past half way. */
export function gamepadStick(gamepad) {
  return gamepadBits(gamepad, GAMEPAD_STICK, STICK) | maskOf(stickDirections(gamepad), STICK);
}

/** The switches a gamepad holds: Select and Reset, on the two middle buttons. */
export function gamepadSwitches(gamepad) {
  return gamepadBits(gamepad, GAMEPAD_SWITCHES, SWITCHES);
}

/**
 * The two joysticks: joystick 1 is its keys, the touch pad and the first
 * gamepad joined; joystick 2 is its keys and the second gamepad. Nothing is
 * filtered.
 */
export function joystickMasks({ keys, touch }, gamepads) {
  return [keys[0] | touch | gamepadStick(gamepads[0]), keys[1] | gamepadStick(gamepads[1])];
}

/**
 * The console switches as sent: the ones that stay, as `toggled` has them, and
 * Reset and Select while any source holds them (the keys, the panel's buttons,
 * either gamepad).
 */
export function switchMask({ toggled, keySwitches, buttons }, gamepads) {
  return toggled | keySwitches | buttons | gamepadSwitches(gamepads[0]) | gamepadSwitches(gamepads[1]);
}

/** `toggled` with the switch `name` flipped; a switch that springs back is not flipped. */
export function toggleSwitch(toggled, name) {
  if (!(name in SWITCHES) || HELD_SWITCHES.includes(name)) return toggled;
  return toggled ^ SWITCHES[name];
}

/** What a switch's button says, in the position `mask` puts it. */
export function switchLabel(name, mask) {
  const on = (mask & SWITCHES[name]) !== 0;
  switch (name) {
    case 'BlackAndWhite': return on ? 'Black and white' : 'Colour';
    case 'LeftDifficultyA': return `Left difficulty ${on ? 'A' : 'B'}`;
    case 'RightDifficultyA': return `Right difficulty ${on ? 'A' : 'B'}`;
    default: return name;
  }
}

/** The picture's displayed size, in square pixels, for a region: 160 pixels wide in its pixel shape, its window's lines tall. */
export function pictureShape(region) {
  const { lines, pixel: [across, down] } = WINDOWS[region] ?? WINDOWS.NTSC;
  return { width: (WIDTH * across) / down, height: lines };
}

/** Sizes the canvas to a region's window and shows it in the region's pixel shape. */
export function showShape(canvas, region) {
  const { width, height } = pictureShape(region);
  canvas.width = WIDTH;
  canvas.height = height;
  canvas.style.aspectRatio = `${width} / ${height}`;
  canvas.dataset.region = region;
}

/** The median of a list of numbers: the middle one, or the mean of the middle two. */
export function median(values) {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = sorted.length >> 1;
  return sorted.length % 2 === 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
}

/**
 * The PAL suggestion (the plan's item 13): with the lines of the frames so far,
 * true when the region is NTSC, at least SUGGEST_FRAMES frames have been seen,
 * and the median of the last SUGGEST_FRAMES is nearer PAL's nominal lines than
 * NTSC's. A median exactly between the two is not nearer either.
 */
export function suggestsPal(lines, region) {
  if (region !== 'NTSC' || lines.length < SUGGEST_FRAMES) return false;
  const m = median(lines.slice(-SUGGEST_FRAMES));
  return Math.abs(m - WINDOWS.PAL.nominal) < Math.abs(m - WINDOWS.NTSC.nominal);
}

/** The line under the region control that gives the last frame's lines. */
export const linesLine = (lines) =>
  `The last frame had ${lines} lines. An NTSC game makes ${WINDOWS.NTSC.nominal} a frame and a PAL game ${WINDOWS.PAL.nominal}.`;

/** The suggestion, or nothing. */
export const suggestionLine = (on) =>
  (on ? 'This game makes frames of PAL\'s length, so it may be a PAL game: try PAL. The page does not change the region by itself.' : '');

/** The line for a change of region. */
export const regionChangeLine = (region) =>
  `Now ${region}: the colours and the clock changed at once, and the game carries on without a restart.`;

/**
 * Puts a cartridge in: Load builds a machine in `region` and powers it on, and
 * replaces the one running only once the file has been read, so a refused file
 * leaves it as it was. Returns { ok, sentence } with the host's sentence naming
 * the board, or { ok: false, message } with the cartridge's, never throwing.
 */
export function loadCartridge(host, data, region, sampleRate) {
  try {
    return { ok: true, sentence: host.Load(data, region, sampleRate) };
  } catch (error) {
    return { ok: false, message: messageOf(error) };
  }
}

/** The line for a cartridge put in, with the host's sentence naming its board. */
export const cartridgeLine = (name, sentence) => `${name} is in. ${sentence}`;

/** The line for a file that was refused; `running` says whether a machine carries on. */
export const badFileLine = (name, result, running) =>
  `${name} was not loaded. ${result.message} ${running ? 'The machine carries on as it was.' : 'Choose another file.'}`;

/**
 * A high-pass that takes out a steady offset: a function that changes a
 * Float32Array of samples at `rate` in place, keeping its state from one call
 * to the next. y[n] = x[n] - x[n-1] + a y[n-1], with a set by the corner.
 */
export function dcBlocker(rate, cornerHz = DC_CORNER_HZ) {
  const a = Math.exp((-2 * Math.PI * cornerHz) / rate);
  let x1 = 0;
  let y1 = 0;
  return (samples) => {
    for (let i = 0; i < samples.length; i++) {
      const x = samples[i];
      y1 = x - x1 + a * y1;
      x1 = x;
      samples[i] = y1;
    }
  };
}

// ---- The keyboard ----

/**
 * The keys on the screen: onHeld(codes) with the Set of keys held whenever it
 * changes, and onToggle(name) when a key for a switch that stays is pressed
 * (not on its repeats, nor when it is let go). Tab is left to the browser, as
 * is anything with Alt, Ctrl or the Command key; Escape lets go of the screen.
 */
export function keyboard(screen, { onHeld, onToggle }) {
  const held = new Set();
  const letGo = () => {
    if (held.size === 0) return;
    held.clear();
    onHeld(held);
  };
  screen.addEventListener('keydown', (event) => {
    if (event.code === 'Escape') {
      screen.blur();
      return;
    }
    const known = event.code in STICK_KEYS || event.code in SWITCH_KEYS;
    if (event.altKey || event.metaKey || event.ctrlKey || !known) return;
    event.preventDefault();
    if (event.repeat || held.has(event.code)) return;
    held.add(event.code);
    const name = SWITCH_KEYS[event.code];
    if (name && !HELD_SWITCHES.includes(name)) onToggle(name);
    onHeld(held);
  });
  screen.addEventListener('keyup', (event) => {
    if (held.delete(event.code)) onHeld(held);
  });
  screen.addEventListener('blur', letGo);
  globalThis.document?.addEventListener('visibilitychange', () => { if (globalThis.document.hidden) letGo(); });
}

// ---- The page ----

for (const panel of globalThis.document?.querySelectorAll('[data-vcs]') ?? []) prepare(panel);

function prepare(panel) {
  const start = panel.querySelector('[data-vcs-start]');
  const status = panel.querySelector('[data-vcs-status]');
  const say = (text, state) => {
    status.textContent = text;
    panel.dataset.state = state;
  };
  // What a check may ask before Start; run() gives each its running meaning.
  panel.vcs = {
    running: () => false,
    region: () => panel.querySelector('[data-vcs-region]:checked').value,
  };
  if (!panel.dataset.download) {
    say('This copy of the site was built without the Atari 2600\'s files, so it cannot run here.', 'missing');
    return;
  }
  start.disabled = false;
  say('Press Start to download the Atari 2600 and run it.', 'ready');
  // A file dropped on the page goes in once the machine is up (run() sets
  // panel.putRom). Before that it is refused, rather than the browser leaving
  // the page to show it.
  dropTarget(panel, say, 'Atari 2600');
  start.addEventListener('click', () => run(panel, say), { once: true });
}

async function run(panel, say) {
  const start = panel.querySelector('[data-vcs-start]');
  const screen = panel.querySelector('[data-vcs-screen]');
  const canvas = panel.querySelector('[data-vcs-canvas]');
  const speed = panel.querySelector('[data-vcs-speed]');
  const cartLine = panel.querySelector('[data-vcs-cartridge]');
  const regionLine = panel.querySelector('[data-vcs-region-line]');
  const linesEl = panel.querySelector('[data-vcs-lines]');
  const suggestEl = panel.querySelector('[data-vcs-suggest]');
  const radios = [...panel.querySelectorAll('[data-vcs-region]')];
  const toggles = [...panel.querySelectorAll('[data-vcs-toggle]')];
  const holds = [...panel.querySelectorAll('[data-vcs-hold]')];
  const base = panel.dataset.base;
  const bundledFile = panel.dataset.rom;
  const bundledName = panel.dataset.romTitle || bundledFile;
  start.disabled = true;

  say('Loading the Atari 2600: the emulator and its cartridge.', 'loading');
  // The bundled cartridge is fetched while the runtime loads; if it cannot be, the visitor picks one.
  const bundled = bundledFile ? bytes(`${base}${bundledFile}`).catch(() => null) : Promise.resolve(null);

  const context = canvas.getContext('2d');
  let image = null;
  let pixels = null;
  const sound = soundOutput({
    panel,
    button: panel.querySelector('[data-vcs-sound]'),
    line: panel.querySelector('[data-vcs-sound-status]'),
    module: '/sample-worklet.js',
    processor: 'sample-ring',
    rate: SAMPLE_RATE,
    transform: dcBlocker(SAMPLE_RATE),
  });
  // What each source holds; sent is what the machine was last given, so only a change is sent.
  const input = { keys: [0, 0], touch: 0, keySwitches: 0, buttons: 0, toggled: 0, sent: [-1, -1, -1] };
  const lineCounts = [];
  let shown = -1;
  let shownLines = -1;
  let suggesting = false;
  let clockMhz = 0;
  let host = null;
  let rom = null;
  let region = panel.vcs.region();
  let running = false;
  let cartridgeArrived = null;

  // The canvas and its pixels follow the region's window; the next paint fills them.
  const size = () => {
    showShape(canvas, region);
    image = context.createImageData(WIDTH, WINDOWS[region].lines);
    pixels = new Uint8Array(image.data.buffer);
    shown = -1;
  };
  const showSwitches = (mask) => {
    for (const b of toggles) {
      b.textContent = switchLabel(b.dataset.vcsToggle, mask);
      b.setAttribute('aria-pressed', String((mask & SWITCHES[b.dataset.vcsToggle]) !== 0));
    }
    for (const b of holds) b.setAttribute('aria-pressed', String((mask & SWITCHES[b.dataset.vcsHold]) !== 0));
    panel.dataset.switches = String(mask);
  };
  const send = (sticks, switches) => {
    for (const stick of [0, 1]) {
      if (sticks[stick] !== input.sent[stick]) {
        host.SetJoystick(stick, sticks[stick]);
        input.sent[stick] = sticks[stick];
      }
    }
    if (switches !== input.sent[2]) {
      host.SetSwitches(switches);
      input.sent[2] = switches;
      showSwitches(switches);
    }
  };
  const sendInputs = () => {
    if (!host || rom === null) return;
    const pads = connected();
    send(joystickMasks(input, pads), switchMask(input, pads));
  };
  const paint = () => {
    shown = host.Picture();
    context.putImageData(image, 0, 0);
    panel.dataset.frames = String(shown);
    const lines = host.Lines();
    lineCounts.push(lines);
    if (lineCounts.length > SUGGEST_FRAMES) lineCounts.shift();
    if (lines !== shownLines) {
      shownLines = lines;
      linesEl.textContent = linesLine(lines);
      panel.dataset.lines = String(lines);
    }
    const now = suggestsPal(lineCounts, region);
    if (now !== suggesting) {
      suggesting = now;
      suggestEl.textContent = suggestionLine(now);
    }
  };
  const onFrame = () => {
    sendInputs();
    if (host.Frames() !== shown) paint();
    if (sound.on) host.Sound();
  };

  // A machine has just been built or switched on: the clock and the inputs follow it, and the lines start again.
  const fresh = () => {
    clockMhz = host.CpuHz() / 1e6;
    input.sent = [-1, -1, -1];
    lineCounts.length = 0;
    shown = -1;
    if (sound.on) sound.fresh();
  };
  // A cartridge's bytes, from the bundle or the visitor: put in, or refused with the machine as it was.
  const use = (data, name) => {
    const result = loadCartridge(host, data, region, SAMPLE_RATE);
    if (!result.ok) {
      cartLine.textContent = badFileLine(name, result, running || rom !== null);
      return false;
    }
    rom = data;
    fresh();
    sendInputs();
    cartLine.textContent = cartridgeLine(name, result.sentence);
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

  size();
  const started = await startMachine({
    panel,
    base,
    name: 'Atari 2600',
    assembly: 'Dbhq.Machines.Atari2600.Wasm',
    hostClass: 'Atari2600Host',
    load: async (vcs, runtime) => {
      runtime.setModuleImports('atari2600', {
        picture: (view) => view.copyTo(pixels),
        sound: (view) => sound.play(view),
      });
      host = vcs;
      await firstCartridge(bundled, (data) => use(data, bundledName), () => {
        picker(panel.querySelector('[data-vcs-choose]'), panel.querySelector('[data-vcs-file]'), put);
        // A file dropped anywhere on the page goes in too (prepare() listens).
        panel.putRom = put;
      }, () => {
        say('Choose a cartridge file, or drop one on the page: the Atari 2600 starts once it has a cartridge.', 'waiting');
        return new Promise((resolve) => { cartridgeArrived = resolve; });
      });
    },
    clockMhz: () => clockMhz,
    clockOf: "the Atari 2600's",
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

  keyboard(screen, {
    onHeld: (codes) => {
      const { sticks, switches } = keyState(codes);
      input.keys = sticks;
      input.keySwitches = switches;
      sendInputs();
    },
    onToggle: (name) => { input.toggled = toggleSwitch(input.toggled, name); },
  });
  holdButtons(panel.querySelectorAll('[data-vcs-button]'), (b) => b.dataset.vcsButton, (held) => {
    input.touch = maskOf(held, STICK);
    sendInputs();
  });
  holdButtons(holds, (b) => b.dataset.vcsHold, (held) => {
    input.buttons = maskOf(held, SWITCHES);
    sendInputs();
  });
  for (const b of toggles) {
    b.disabled = false;
    b.addEventListener('click', () => {
      input.toggled = toggleSwitch(input.toggled, b.dataset.vcsToggle);
      sendInputs();
    });
  }
  screen.tabIndex = 0;
  screen.addEventListener('focus', () => { panel.dataset.typing = 'true'; });
  screen.addEventListener('blur', () => { delete panel.dataset.typing; });
  watchPads(panel.querySelector('[data-vcs-pads]'), 'joystick');

  wireRegion(radios, () => region, (value) => {
    host.SetRegion(value);
    region = host.Region();
    clockMhz = host.CpuHz() / 1e6;
    size();
    checkRegion(radios, region);
    panel.dataset.region = region;
    regionLine.textContent = regionChangeLine(region);
    suggesting = suggestsPal(lineCounts, region);
    suggestEl.textContent = suggestionLine(suggesting);
    return true;
  });
  const powerLine = panel.querySelector('[data-vcs-power-line]');
  const power = panel.querySelector('[data-vcs-power]');
  power.disabled = false;
  power.addEventListener('click', () => {
    host.PowerCycle();
    fresh();
    powerLine.textContent = 'Switched off and on: the game started again from the beginning, and the switches stayed where they were.';
  });
  sound.wire();

  Object.assign(panel.vcs, {
    host,
    stepTo(frames) {
      started.hold(true);
      send([0, 0], input.toggled);
      host.PowerCycle();
      host.RunFrames(frames);
      paint();
      return host.Frames();
    },
    play: () => started.hold(false),
    region: () => region,
    running: () => running,
  });
  panel.dataset.region = region;
  panel.dispatchEvent(new CustomEvent('vcs:ready'));
  say('Running. The screen has the keyboard: play with the keys below, or press Tab to move on.', 'running');
  // Start was the visitor asking to play, so the keyboard goes to the machine.
  screen.focus({ preventScroll: true });
}
