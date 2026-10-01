// The chip page's diagram: what is drawn and where. Pure data, no three.js, so
// the site's tests can check it in Node.
//
// It is a DIAGRAM, not a trace of the die. The layout follows the 6502's
// floorplan in outline (the instruction decoder across the top, the control
// logic below it, the registers and the arithmetic unit along the bottom), but
// nothing here is measured from a die photograph, and the pads are drawn in the
// order of the package's pins rather than where they sit on the silicon.
// Units are arbitrary. x runs left to right, z top to bottom, y is height.

export const DIE = { width: 40, depth: 44, thickness: 1.2 };

// The 40 pins of the package, in order. A pad is lit by the trace when it names
// a bus line (A0 to A15, D0 to D7), R/W, SYNC or the clock.
const PIN_NAMES = [
  'VSS', 'RDY', 'PHI1', 'IRQ', 'NC', 'NMI', 'SYNC', 'VCC', 'A0', 'A1',
  'A2', 'A3', 'A4', 'A5', 'A6', 'A7', 'A8', 'A9', 'A10', 'A11',
  'VSS', 'A12', 'A13', 'A14', 'A15', 'D7', 'D6', 'D5', 'D4', 'D3',
  'D2', 'D1', 'D0', 'RW', 'NC', 'NC', 'PHI0', 'SO', 'PHI2', 'RES',
];

/** Display text for a pad name: the package's own spelling. */
export const padLabel = (name) => ({ PHI0: 'φ0', PHI1: 'φ1', PHI2: 'φ2', RW: 'R/W' })[name] ?? name;

const SLOT = (j) => -15.75 + j * 3.5;
const EDGE_X = DIE.width / 2 - 1.5;
const EDGE_Z = DIE.depth / 2 - 1.5;

/** Pins 1 to 10 down the left side, 11 to 20 along the bottom, 21 to 30 up the right, 31 to 40 back along the top. */
export const PADS = PIN_NAMES.map((name, i) => {
  const pin = i + 1;
  const j = i % 10;
  const side = ['left', 'bottom', 'right', 'top'][Math.floor(i / 10)];
  const at = {
    left: [-EDGE_X, SLOT(j)],
    bottom: [SLOT(j), EDGE_Z],
    right: [EDGE_X, -SLOT(j)],
    top: [-SLOT(j), -EDGE_Z],
  }[side];
  return { pin, name, side, x: at[0], z: at[1] };
});

export const pad = (name) => PADS.find((p) => p.name === name);

// The datapath along the bottom: one column per register, bit-sliced, bit 7 at
// the top of each column. The address bus registers and the data latch are
// what the chip drives onto, and reads from, its pins.
const DATAPATH = { left: -16.5, right: 15, top: -2.6, rowPitch: 2.1 };
export const ROWS = 8;
export const rowZ = (bit) => DATAPATH.top + 1 + (7 - bit) * DATAPATH.rowPitch;
export const DATAPATH_BOTTOM = rowZ(0) + 1;

const COLUMN_IDS = [
  { id: 'ABH', label: 'ABH', reg: 'abh', title: 'Address bus, high byte' },
  { id: 'ABL', label: 'ABL', reg: 'abl', title: 'Address bus, low byte' },
  { id: 'Y', label: 'Y', reg: 'y', title: 'Index register Y' },
  { id: 'X', label: 'X', reg: 'x', title: 'Index register X' },
  { id: 'S', label: 'S', reg: 's', title: 'Stack pointer' },
  { id: 'ALU', label: 'ALU', reg: null, title: 'Arithmetic and logic unit' },
  { id: 'A', label: 'A', reg: 'a', title: 'Accumulator' },
  { id: 'PCH', label: 'PCH', reg: 'pch', title: 'Program counter, high byte' },
  { id: 'PCL', label: 'PCL', reg: 'pcl', title: 'Program counter, low byte' },
  { id: 'DL', label: 'DL', reg: 'dl', title: 'Data latch' },
];
const slot = (DATAPATH.right - DATAPATH.left) / COLUMN_IDS.length;
export const COLUMNS = COLUMN_IDS.map((c, i) => ({ ...c, x: DATAPATH.left + slot * (i + 0.5), width: slot - 0.5 }));
export const column = (id) => COLUMNS.find((c) => c.id === id);

// The blocks above the datapath. Heights are for the eye, not the process.
export const BLOCKS = [
  { id: 'decoder', label: 'Instruction decoder', x: 0, z: -14.75, width: 28, depth: 4.5, height: 0.7 },
  { id: 'timing', label: 'Timing', x: -13.25, z: -8, width: 5.5, depth: 6, height: 0.9 },
  { id: 'control', label: 'Control logic', x: -1, z: -8, width: 18, depth: 6, height: 1.1 },
  { id: 'flags', label: 'Status flags', x: 11.6, z: -8, width: 5.6, depth: 6, height: 0.8 },
];

/** The status register's eight bits, bit 7 first, as the datasheet names them. */
export const FLAGS = ['N', 'V', '-', 'B', 'D', 'I', 'Z', 'C'];

// Wires are lists of [x, z] points on the die's surface, from the chip's inside
// to its pad. Each address and data line is its own wire, so the trace can
// light it bit by bit.
const LANE = 0.2;

function addressWire(bit) {
  const col = column(bit >= 8 ? 'ABH' : 'ABL');
  const b = bit % 8;
  const sx = col.x - col.width / 2 + 0.2 + b * ((col.width - 0.4) / 7);
  const laneZ = DATAPATH_BOTTOM + 0.5 + (15 - bit) * LANE;
  const p = pad(`A${bit}`);
  const start = [sx, DATAPATH_BOTTOM];
  if (p.side === 'bottom') return [start, [sx, laneZ], [p.x, laneZ], [p.x, p.z - 1]];
  const laneX = p.side === 'left' ? -EDGE_X + 1.3 + bit * LANE : EDGE_X - 1.3 - (bit - 12) * LANE;
  return [start, [sx, laneZ], [laneX, laneZ], [laneX, p.z], [p.x + (p.side === 'left' ? 1 : -1), p.z]];
}

function dataWire(bit) {
  const col = column('DL');
  const z = rowZ(bit);
  const p = pad(`D${bit}`);
  const laneX = DATAPATH.right + 0.35 + (7 - bit) * LANE;
  const start = [col.x + col.width / 2, z];
  if (p.side === 'right') return [start, [laneX, z], [laneX, p.z], [p.x - 1, p.z]];
  const laneZ = -EDGE_Z + 1.25 + bit * LANE;
  return [start, [laneX, z], [laneX, laneZ], [p.x, laneZ], [p.x, p.z + 1]];
}

/** Short stubs from the control pads to the block that uses them. */
function stub(name, to) {
  const p = pad(name);
  const inward = { left: [1, 0], right: [-1, 0], top: [0, 1], bottom: [0, -1] }[p.side];
  const from = [p.x + inward[0], p.z + inward[1]];
  return [from, [from[0] + inward[0] * to, from[1] + inward[1] * to]];
}

export const WIRES = [
  ...Array.from({ length: 16 }, (_, bit) => ({ id: `A${bit}`, bus: 'address', bit, points: addressWire(bit) })),
  ...Array.from({ length: 8 }, (_, bit) => ({ id: `D${bit}`, bus: 'data', bit, points: dataWire(bit) })),
  { id: 'RW', bus: 'control', points: stub('RW', 1.6) },
  { id: 'SYNC', bus: 'control', points: stub('SYNC', 0.6) },
  { id: 'PHI0', bus: 'control', points: stub('PHI0', 1.6) },
  { id: 'IRQ', bus: 'control', points: stub('IRQ', 0.6) },
  { id: 'NMI', bus: 'control', points: stub('NMI', 0.6) },
  { id: 'RES', bus: 'control', points: stub('RES', 1.6) },
];

// The tour. Each stop is a camera position, the point it looks at, and the focus
// region [x, z, width, depth] the brackets frame, and the
// words shown beside it. The page renders the words as HTML, so they read
// without JavaScript and the tests can check them.
export const STOPS = [
  {
    id: 'overview',
    focus: [0, 0, 42, 46],
    title: 'The whole chip',
    camera: [0, 47, 47],
    target: [0, 0, 4],
    text: 'The 6502 in outline: the instruction decoder across the top, the control logic under it, and the registers and the arithmetic unit along the bottom, with a pad for each of the package’s 40 pins around the edge. Press play to watch this project’s core run a program through it, one bus cycle at a time.',
  },
  {
    id: 'pins',
    focus: [0, 0, 42, 46],
    title: 'The pins',
    camera: [-10, 22, 46],
    target: [0, 0, 16],
    text: 'Every cycle the 6502 puts an address on its 16 address pins and moves one byte over its 8 data pins, in or out. A lit address pad is a 1 on that line. R/W says which way the byte goes, and SYNC lights on the cycle that fetches an instruction.',
  },
  {
    id: 'decoder',
    focus: [0, -14.75, 30, 6.5],
    title: 'The instruction decoder',
    camera: [0, 24, 10],
    target: [0, 0, -12],
    text: 'On the real chip this is a grid of transistors that turns the opcode byte into the signals each step of an instruction needs. Here it lights on the cycle the opcode is fetched.',
  },
  {
    id: 'control',
    focus: [-6, -8, 26, 8],
    title: 'The control logic',
    camera: [-8, 22, 12],
    target: [-2, 0, -7],
    text: 'The logic that sequences each instruction cycle by cycle, with the timing block that splits the clock into two phases. It pulses on every cycle of the trace.',
  },
  {
    id: 'registers',
    focus: [-0.75, 5.75, 34, 18],
    title: 'The registers',
    camera: [-10, 26, 28],
    target: [-2, 0, 5],
    text: 'Each column is a register, one cell per bit, bit 7 at the top. A cell is lit for a 1. When an instruction changes a register, its column flashes. The registers show the core’s state when each instruction finishes, not the chip’s internal timing within it.',
  },
  {
    id: 'alu',
    focus: [0.8, 5.75, 5, 18],
    title: 'The arithmetic unit',
    camera: [8, 24, 26],
    target: [1, 0, 5],
    text: 'The ALU adds, subtracts and does the logic operations. It lights while an instruction that computes a result is in progress, such as ADC, AND or INC. On the real chip it also works out indexed addresses and branch targets, which this view does not show.',
  },
  {
    id: 'flags',
    focus: [11.6, -8, 7.5, 8],
    title: 'The status flags',
    camera: [14, 22, 8],
    target: [11, 0, -7],
    text: 'Eight bits, N V - B D I Z C, set by the results of instructions: negative, overflow, break, decimal, interrupt disable, zero and carry. The program here turns decimal mode on and off on every lap.',
  },
  {
    id: 'buses',
    focus: [0, 0, 42, 46],
    title: 'The buses',
    camera: [32, 26, 18],
    target: [6, 0, 6],
    text: 'Wires from the address bus registers fan out to the address pins, and the data latch connects to the data pins. Each cycle a pulse runs along the wires that changed, inward for a read and outward for a write.',
  },
];
