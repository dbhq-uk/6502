// The KIM-1 board's layout for its 3D model.
//
// The figures are measured, not read off a grid, by the scripts in
// tools/kim1-model/, from five photographs of two Rev B boards and Eduardo
// Casino's KiCad replica of the Rev D board (the photographs' README in
// site/src/assets/photos/ says where each came from, and the journal for
// 2 October 2026 has every figure and its residual). Those scripts write
// ./kim-1-parts.mjs; this module gives it the shapes the model and the page use.
//
// - Every photograph is registered to the replica: a homography from four
//   hand-marked corners, refined by matching the photograph's light copper to
//   the replica's, then a smooth correction for the lens. Residual error, on
//   blocks held out of the fit: a tenth to a fifth of a millimetre.
// - The parts soldered through the board are placed by their pads, from the
//   replica. The keypad, its keys, the display window, the crystal's can, the
//   name and the red wire are measured on the photographs.
// - Heights are triangulated between two photographs of the same board taken
//   from different places, to about a third of a millimetre. Where no height
//   was measured (the resistors, capacitors and diodes, the board itself) the
//   part's own size is used, and HEIGHT_SOURCES says which.
//
// Coordinates are millimetres from the top left corner of the board's body as
// the component side shows it: x to the right, y down. The tabs are at
// negative x. A browser module: no Node imports, because the model's bundle
// includes it.

import * as P from './kim-1-parts.mjs';

export const PITCH = 3.9624; // 0.156 inch, the KIM-1's 44-way connectors
export const CONTACTS_PER_TAB = 22;

/** The board's body. 1.6 mm thick: the replica's stack-up and the usual 1/16 inch; not measured. */
export const BOARD = { width: P.BODY.width, depth: P.BODY.depth, thickness: 1.6 };

/** The two edge-connector tabs, down the left edge: from y0 to y1, standing `out` mm proud, with their first contact's centre. */
export const TABS = P.TABS.map((t, i) => ({ ...t, first: P.TAB_CONTACTS[i].first }));

const LABELS = { U1: '6502', U2: '6530', U3: '6530' };
/** Every integrated circuit: `kind` is ceramic, socketed, memory or plastic, as the Musée Bolo's board has them. */
export const CHIPS = P.CHIPS.map((c) => ({ ...c, id: c.ref, label: LABELS[c.ref] ?? null }));
/** Resistors, capacitors and diodes lying flat between their two pads. */
export const AXIAL = P.AXIAL;
export const TRANSISTORS = P.TRANSISTORS;
export const TRIMMER = P.OTHER.find((o) => o.kind === 'trimmer');
export const DISC = P.OTHER.find((o) => o.kind === 'disc');

const box = ([x0, y0, x1, y1]) => ({ x: (x0 + x1) / 2, y: (y0 + y1) / 2, width: x1 - x0, depth: y1 - y0 });
export const CRYSTAL = { ...box(P.CRYSTAL.box), height: P.CRYSTAL.height, leads: P.CRYSTAL.leads };
/** The KIM-1 name, its letters' box, on a plate a millimetre larger all round. */
export const NAME = { ...box([P.NAME[0] - 1, P.NAME[1] - 1, P.NAME[2] + 1, P.NAME[3] + 1]), text: 'KIM-1' };
/** The four corner holes, and the holes the keypad is fixed through. */
export const HOLES = P.HOLES;
export const KEYPAD_HOLES = P.KEYPAD_HOLES;
/** The loose red wire across the left of the Musée Bolo's board, a point every 8 mm along it. */
export const WIRE = P.WIRE;

/**
 * The display: a dark window over six digits, four and then two. The digits'
 * centres are the six LED packages' (MAN72A) from the replica; a digit is
 * 7.6 mm tall (0.3 inch, the MAN72A's datasheet) and drawn 4.6 mm wide.
 */
export const DISPLAY = { ...box(P.DISPLAY_WINDOW.box), height: P.DISPLAY_WINDOW.height, digits: P.DIGITS.map(([x]) => x), digitY: P.DIGITS[0][1], digitWidth: 4.6, digitDepth: 7.6 };

/** The keypad, rows top to bottom as the photograph and the page's keypad have them. SST is the slide switch. */
export const KEY_ROWS = [
  ['GO', 'ST', 'RS', 'SST'],
  ['AD', 'DA', 'PC', '+'],
  ['C', 'D', 'E', 'F'],
  ['8', '9', 'A', 'B'],
  ['4', '5', '6', '7'],
  ['0', '1', '2', '3'],
];
/**
 * The keypad: its own small circuit board, a black bezel on it with a well for
 * each key, and the keys. `key` is a key cap's side, 9.1 mm.
 */
export const KEYPAD = {
  ...box(P.KEYPAD.bezel),
  board: box(P.KEYPAD.board),
  boardHeight: P.KEYPAD.boardHeight,
  bezelHeight: P.KEYPAD.bezelHeight,
  keyHeight: P.KEYPAD.keyHeight,
  columns: P.KEYPAD.columns,
  rows: P.KEYPAD.rows,
  key: 9.1,
};

/** Every key on the model with its centre, the SST switch left out. */
export const KEYS = KEY_ROWS.flatMap((row, r) => row.map((name, c) => ({ name, x: KEYPAD.columns[c], y: KEYPAD.rows[r] }))).filter((k) => k.name !== 'SST');
export const SST = { x: KEYPAD.columns[3], y: KEYPAD.rows[0] };

/** Heights above the board's top face, in mm: measured, by triangulation, unless HEIGHT_SOURCES says otherwise. */
export const HEIGHTS = P.HEIGHTS;
export const HEIGHT_SOURCES = {
  measured: ['the keypad, its circuit board, its bezel and its keys', 'the display window', 'the crystal', 'a ceramic 40-pin chip (U1), the socketed one (U2), a memory chip (U12) and a logic chip (U13)', 'a transistor (Q1)', 'the trimmer', 'two capacitors (C16 and C5)'],
  notMeasured: ['the board, 1.6 mm', 'U3, taken as U1, the same package', 'every other chip and transistor, taken as the one of its kind that was measured', 'how thick a chip\'s body is below its measured top, from typical package sizes', 'the other resistors, capacitors and diodes, from their footprints', 'the red wire, drawn 1 mm above the board'],
};

/**
 * The copper of both faces, in one map (site/src/assets/tracks/kim-1.webp,
 * copied to `src` by scripts/build-models.mjs): red is the top face from the
 * photographs, green the top face from the replica where no photograph shows
 * the board, and blue the underside from its photograph. 8 pixels to the
 * millimetre, the board's body edge to edge.
 */
export const TRACKS = { src: '/models/kim-1-tracks.webp', width: 1600, height: 2184 };

/**
 * Seven segments in a digit's cell, a to g, each as [x, y, length, horizontal]
 * in units of the cell (0 to 1 across, 0 to 1 down). Bit 0 of what the machine
 * reports is segment a, round to bit 6, segment g, as on the page's drawn digits.
 */
export const SEGMENTS = [
  [0.5, 0.0, 0.7, true],
  [1.0, 0.25, 0.42, false],
  [1.0, 0.75, 0.42, false],
  [0.5, 1.0, 0.7, true],
  [0.0, 0.75, 0.42, false],
  [0.0, 0.25, 0.42, false],
  [0.5, 0.5, 0.7, true],
];

/** The usual seven-segment shapes, the same table as Kim1Display.Decode: '?' for any other pattern, a space when dark. */
const SHAPES = { 0x00: ' ', 0x3f: '0', 0x06: '1', 0x5b: '2', 0x4f: '3', 0x66: '4', 0x6d: '5', 0x7d: '6', 0x07: '7', 0x7f: '8', 0x6f: '9', 0x77: 'A', 0x7c: 'B', 0x39: 'C', 0x5e: 'D', 0x79: 'E', 0x71: 'F' };
export const decode = (segments) => SHAPES[segments & 0x7f] ?? '?';

const WORDS = ['no', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten'];
const count = (n) => WORDS[n] ?? n.toLocaleString('en-GB');
const cm = (mm) => (mm / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const of = (kind) => AXIAL.filter((a) => a.kind === kind).length;

/** The model's text alternative and caption, with every size and count read from the layout above. */
export function describe() {
  const logic = CHIPS.filter((c) => c.kind === 'plastic').length;
  const memory = CHIPS.filter((c) => c.kind === 'memory').length;
  return `Model, not a photograph. A drawing of the KIM-1 board in three.js, measured from the photographs below: the green board, ${cm(BOARD.depth)} by ${cm(BOARD.width)} cm, with the copper tracks of both faces traced from photographs of two Rev B boards, and, under the parts, where no photograph shows the board, taken from a replica of its layout; ${count(CONTACTS_PER_TAB)} gold contacts on each of its ${count(TABS.length)} edge-connector tabs; the 6502 and the two 6530s, ${count(memory)} memory chips and ${count(logic)} logic chips; ${count(of('resistor'))} resistors, ${count(of('capacitor') + (DISC ? 1 : 0))} capacitors, ${count(of('diode'))} diodes, ${count(TRANSISTORS.length)} transistors, the trimmer, the crystal and the loose red wire this board carries; the ${count(DISPLAY.digits.length)} red LED digits, which light with what the running machine's display shows; and the ${count(KEYS.length)} keys and the SST switch, which press the machine's own keys when clicked, and go down when a key is pressed on the keypad above. The parts' places come from the replica's pads and the photographs, and most of their heights were measured by triangulation between two photographs; the rest are the parts' own sizes.`;
}
