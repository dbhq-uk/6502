// The KIM-1 board's layout for its 3D model, measured on the photograph on its
// page (site/src/assets/photos/README.md says where that came from).
//
// How it was measured, on the full-size source (3792 by 4675 pixels), on
// 2 October 2026, by scripts/measure-kim1-photo.mjs; the journal entry for
// that day has its output:
//
// - The scale comes from the edge contacts. Each tab has 22 gold contacts,
//   counted, at the 0.156 inch (3.96 mm) pitch of the KIM-1's two 44-way
//   connectors. Their centres are 59.98 pixels apart on the upper tab and
//   60.26 on the lower, so 15.17 pixels to the millimetre.
// - The board is where the photograph stops being black: 3,030 by 4,139
//   pixels, so 199.7 by 272.8 mm, with the two tabs standing 10.0 mm proud of
//   its left edge. On that scale the three 40-pin chips measure 50.8 to
//   52.1 mm long and the keys sit 12.5 to 13.0 mm apart, against 52 mm and
//   half an inch (12.7 mm) on paper, which is a check on the scale.
// - Everything else was read off a grid laid over the photograph to about a
//   millimetre. Heights are not in a photograph taken from above; they are the
//   usual ones for the parts (a 1.6 mm board, a 40-pin package about 4 mm high),
//   and the model is honest about that in its caption.
//
// Coordinates are millimetres from the top left corner of the board's body as
// the photograph shows it: x to the right, y down the photograph. The tabs are
// at negative x. A browser module: no Node imports, because the model's bundle
// includes it.

export const PX_PER_MM = 15.17;
export const PITCH = 3.9624;

export const BOARD = { width: 199.7, depth: 272.8, thickness: 1.6 };

/** The two edge-connector tabs, down the left edge: from y0 to y1, standing `out` mm proud. */
export const TABS = [
  { y0: 11.1, y1: 101.8, out: 10.0, first: 14.9 },
  { y0: 170.2, y1: 261.1, out: 10.0, first: 174.0 },
];
export const CONTACTS_PER_TAB = 22;

/** The 40-pin chips: the 6502 at the top, then the two 6530s, U3 and U2, as the photograph labels them. */
export const CHIPS = [
  { id: 'U1', label: '6502', x: 52.3, y: 40.3, pins: 40 },
  { id: 'U3', label: '6530', x: 52.8, y: 77.8, pins: 40 },
  { id: 'U2', label: '6530', x: 52.2, y: 110.3, pins: 40 },
];

/** The column of eight 1 KB memory chips (6102s), 16 pins each, end to end down the board. */
export const RAM = Array.from({ length: 8 }, (_, i) => ({ x: 111.4, y: 12.3 + i * 12.45, pins: 16 }));

/** Smaller logic chips, read off the photograph more roughly: 14 or 8 pins, standing along y. */
export const LOGIC = [
  { x: 136.3, y: 49.5, pins: 14 },
  { x: 136.3, y: 74.3, pins: 14 },
  { x: 148.0, y: 97.4, pins: 14 },
  { x: 103.0, y: 158.6, pins: 14 },
  { x: 37.9, y: 184.2, pins: 14 },
  { x: 62.1, y: 179.0, pins: 14 },
  { x: 62.1, y: 199.8, pins: 14 },
  { x: 103.0, y: 199.3, pins: 14 },
  { x: 38.6, y: 225.3, pins: 14 },
  { x: 74.3, y: 228.4, pins: 8 },
];

export const CRYSTAL = { x: 145.8, y: 16.5, width: 18, depth: 16 };
export const NAME = { x: 170.8, y: 38.3, width: 35.2, depth: 7.8, text: 'KIM-1' };
export const HOLES = [[6.9, 7.1], [191.8, 6.6], [191.8, 264.9], [6.9, 264.9]];

/**
 * The display: six digits behind one dark window, four and then two, with a
 * wider gap between them, as the board groups address and data. `digits` are
 * the centres along x.
 */
export const DISPLAY = { x: 155.8, y: 148.4, width: 67.7, depth: 19.5, digits: [128.3, 138.3, 148.3, 158.3, 173.3, 183.3], digitWidth: 6.2, digitDepth: 10.4 };

/** The keypad, rows top to bottom as the photograph and the page's keypad have them. SST is the slide switch. */
export const KEY_ROWS = [
  ['GO', 'ST', 'RS', 'SST'],
  ['AD', 'DA', 'PC', '+'],
  ['C', 'D', 'E', 'F'],
  ['8', '9', 'A', 'B'],
  ['4', '5', '6', '7'],
  ['0', '1', '2', '3'],
];
export const KEYPAD = { x: 157.8, y: 217.0, width: 60.7, depth: 86.5, columns: [138.0, 150.6, 163.6, 176.4], rows: [185.7, 198.2, 211.1, 223.8, 236.6, 249.5], key: 9.1 };

/** Every key on the model with its centre, the SST switch left out. */
export const KEYS = KEY_ROWS.flatMap((row, r) => row.map((name, c) => ({ name, x: KEYPAD.columns[c], y: KEYPAD.rows[r] }))).filter((k) => k.name !== 'SST');
export const SST = { x: KEYPAD.columns[3], y: KEYPAD.rows[0] };

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

/** The model's text alternative and caption, with every size and count read from the layout above. */
export function describe() {
  return `Model, not a photograph. A drawing of the KIM-1 board in three.js, measured from the photograph above: the green board, ${cm(BOARD.depth)} by ${cm(BOARD.width)} cm, with ${count(CONTACTS_PER_TAB)} gold contacts on each of its ${count(TABS.length)} edge-connector tabs; the 6502 and the two 6530s; ${count(RAM.length)} memory chips; the ${count(DISPLAY.digits.length)} red LED digits, which light with what the running machine's display shows; and the ${count(KEYS.length)} keys and the SST switch, which press the machine's own keys when clicked, and go down when a key is pressed on the keypad above. The sizes and places come from the photograph; the heights of the parts are typical ones, because a photograph taken from above does not show them.`;
}
