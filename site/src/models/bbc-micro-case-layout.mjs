// The BBC Micro Model B's case, for its outside 3D model: its layout, and the
// words the page says about it (its caption, its help, its status lines), and
// how its keys follow the machine's.
//
// The figures are measured, not typed: tools/bbc-micro-model/ measured
// photographs of the machine and an open keyboard drawing, and case_parts.py
// wrote ./bbc-micro-case-parts.mjs, which this module hands on to the model and
// the page. The journal for 4 October 2026 (its task 8 section, 13 October) has
// every figure. The case's size is the Service Manual's; the outside's own
// measures point different ways, two narrower in the keyboard drawing's units
// and one wider on the rear's D socket scale, which is not resolved, and the
// parts module's notes and the model's note say so.
//
// A key's legend is LEGENDS[name] from public/bbc-keys.js, the on-screen keys'
// own table, so the model's legends cannot drift from the page's. Millimetres
// in the case frame: from the case's left rear corner at table level, x to the
// right, y towards the front, z up. A browser module: no Node imports, because
// the model's bundle includes it.

import * as P from './bbc-micro-case-parts.mjs';
import { BBC_KEYS, LEGENDS } from '../../public/bbc-keys.js';

export const { CASE, RECESS, PROFILE, KEYS, LEDS, BADGE, GRILLE, REAR, UNDERSIDE, FEET } = P;

/**
 * The keycaps the case has beyond the machine's matrix keys: BREAK, which is
 * the reset line and not in the matrix, and a second SHIFT, at the right of
 * the bottom letter row, on the same matrix place as the first.
 */
export const BREAK = 'Break';
export const SECOND_SHIFT = 'Shift2';

/** The word on BREAK: the page's Break button's, in capitals as the keyboard prints its words. */
const BREAK_LEGEND = BREAK.toUpperCase();

/** The key on the machine a keycap presses: the second SHIFT is SHIFT; every other keycap is its own key. */
export function machineKey(name) {
  return name === SECOND_SHIFT ? 'Shift' : name;
}

/** The keycaps on the case that a key of the machine, by its name in BBC_KEYS or BREAK, puts down: SHIFT puts both down. */
export function capsOf(name) {
  return KEYS.keys.filter((k) => machineKey(k.name) === name).map((k) => k.name);
}

/**
 * What the model prints on a keycap: LEGENDS[name], the on-screen key's; the
 * first SHIFT's on the second; BREAK's word on BREAK. The space bar is blank,
 * as O1 shows it, though the on-screen key says SPACE.
 */
export function legendOf(name) {
  if (name === BREAK) return BREAK_LEGEND;
  const key = machineKey(name);
  if (!(key in BBC_KEYS)) throw new Error(`the case has no key ${name}`);
  return key === 'Space' ? '' : LEGENDS[key];
}

/** How long a tapped key stays down on the model, in milliseconds: as long as the NES's RESET does. */
export const TAP_MS = 140;

const ACTIONS = ['down', 'up', 'tap', 'latch-on', 'latch-off'];

/**
 * Which keys are down on the model, from the page's bbc:key events (public/
 * bbc-micro.js, THE EVENTS): `down` holds a key until `up`, `tap` holds it for
 * `tapMs`, `latch-on` holds it until `latch-off`, and a key is down while
 * anything holds it. Time is passed in (milliseconds, as performance.now gives),
 * so a test runs it on a fake clock. `on(detail, at)` takes one event and says
 * whether it was one the case has; `down(at)` gives the keycaps down then, in
 * KEYS order, SHIFT's two together; `presses` counts the events that start a
 * hold (down, tap, latch-on), and `last` names the key the last of them
 * pressed. `reset()`, for a new machine, lets go of everything and starts
 * `presses` and `last` again.
 */
export function createKeyState({ tapMs = TAP_MS } = {}) {
  const held = new Set();
  const latched = new Set();
  const tapped = new Map();
  const state = {
    presses: 0,
    last: null,
    on({ name, action } = {}, at) {
      if (!ACTIONS.includes(action) || capsOf(name).length === 0) return false;
      if (action === 'down') held.add(name);
      else if (action === 'up') held.delete(name);
      else if (action === 'tap') tapped.set(name, at + tapMs);
      else if (action === 'latch-on') latched.add(name);
      else latched.delete(name);
      if (action === 'down' || action === 'tap' || action === 'latch-on') {
        state.presses += 1;
        state.last = name;
      }
      return true;
    },
    down(at) {
      const keys = new Set([...held, ...latched, ...[...tapped].filter(([, until]) => until > at).map(([name]) => name)]);
      return KEYS.keys.map((k) => k.name).filter((cap) => keys.has(machineKey(cap)));
    },
    reset() {
      held.clear();
      latched.clear();
      tapped.clear();
      state.presses = 0;
      state.last = null;
    },
  };
  return state;
}

/** Each light of panel.bbc.leds() by the name the case prints under it (LEDS, O1's words). */
export const LED_OF = { motor: LEDS.leds[0].name, caps: LEDS.leds[1].name, shiftLock: LEDS.leds[2].name };

/** The lights lit, by name, in LEDS order, from panel.bbc.leds() (null before Start: none). */
export function litLeds(leds) {
  if (!leds) return [];
  return LEDS.leds.map((l) => l.name).filter((name) => Object.entries(LED_OF).some(([k, n]) => n === name && leds[k]));
}

/** The lit lights in words, for a test hook: their names, or none. */
export const ledWords = (lit) => (lit.length ? lit.join(', ') : 'none');

/** The model section's accessible name. */
export const LABEL = '3D model of the outside of the BBC Micro. Click a key on it to press that key on the machine.';

/** What the model's keys and lights do, said under it. */
export const HELP = 'A key clicked on the model is pressed on the machine above, exactly as the on-screen key is: SHIFT and CTRL hold for the next key, as they do there. A key pressed on the machine, from the PC keyboard, the on-screen keys or the model, goes down on the model. The three lights are the machine\'s: CAPS LOCK and SHIFT LOCK as its operating system sets them, and the cassette motor\'s while the motor\'s relay is on.';

/** What the model's status line says. */
export const STATUS = {
  idle: 'The model is running. The machine above is not running: press Start, and its keys and lights show here.',
  running: 'The model is running, and so is the machine: a key goes down on the model as it is pressed, and the lights are the machine\'s.',
  notRunning: 'The machine above is not running, so the key did nothing: press Start first.',
  pressed: (name) => `${legendOf(name) || 'The space bar'} pressed on the machine, as its on-screen key does.`,
};

/**
 * A click on a keycap, on the page whose machine panel is `panel` (public/
 * bbc-micro.js's [data-bbc], with panel.bbc once the machine runs): presses
 * the key through panel.bbc.tap, the second SHIFT as SHIFT and BREAK as the
 * Break button; before Start it presses nothing. Returns what the status line
 * says.
 */
export function press(name, panel) {
  if (capsOf(machineKey(name)).length === 0) throw new Error(`the case has no key ${name}`);
  const bbc = panel?.bbc;
  if (!bbc) return STATUS.notRunning;
  bbc.tap(machineKey(name));
  return STATUS.pressed(name);
}

/** The status line for what the pointer is over: a keycap, a port on the rear or a connector underneath (an object's userData). */
export function pointWords({ key, port } = {}) {
  if (key) return `${legendOf(key) || 'The space bar'}: click to press it on the machine.`;
  const rear = REAR.ports.find((p) => p.ref === port);
  if (rear) return `${rear.label}, ${rear.ref}, on the rear.`;
  const under = UNDERSIDE.connectors.find((u) => u.ref === port);
  if (under) return `${under.label}, ${under.ref}, underneath.`;
  return null;
}

const cm = (mm) => (mm / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const list = (xs) => (xs.length < 2 ? xs.join('') : `${xs.slice(0, -1).join(', ')} and ${xs.at(-1)}`);

/** The model's text alternative and caption, with every size and count read from the layout above. */
export function describe() {
  const machine = KEYS.keys.filter((k) => k.name !== BREAK && k.name !== SECOND_SHIFT).length;
  return `Model, not a photograph. A drawing in three.js of the outside of the BBC Micro Model B, ${cm(CASE.width)} by ${cm(CASE.depth)} cm and ${cm(CASE.height)} cm high: the Service Manual's size, though the photographs' measures of it point different ways, two narrower and one wider, which is not resolved. On it are its ${KEYS.keys.length} keys, the machine's ${machine}, BREAK and a second SHIFT, each with its on-screen key's legend; the ${list(LEDS.leds.map((l) => l.name))} lights; the badge strip's words; the rear's ${REAR.ports.length} ports, its vents and its mains panel; and underneath, the connectors for the ${list(UNDERSIDE.connectors.map((u) => u.label).reverse())}. The keys, the lights and the opening round them were placed from photographs and an open keyboard drawing; the note below says how well each is known. While the machine above runs, a key pressed on it goes down on the model, and the lights are lit as the machine's are.`;
}
