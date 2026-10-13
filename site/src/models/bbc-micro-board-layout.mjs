// The BBC Micro Model B's main board, Issue 7, for its inside 3D model: its
// layout, and the words the page says about it (its caption, its chip legend,
// its status lines).
//
// The figures are measured, not typed: tools/bbc-micro-model/ measured the
// bare Issue 7 scans (amb5l's) and a photograph of a populated board (8BS's),
// and board_parts.py wrote ./bbc-micro-board-parts.mjs, which this module gives
// the shapes the model and the page use. The journal for 4 October 2026 (and
// its later sections, to 13 October) has every figure.
//
// Places are the scan's footprints and the parts are table 8.1's of Acorn's
// Service Manual; the two checks on them (the manual's places, the markings on
// a populated board) failed, and the fitted set is the machine the page
// emulates, the disc interface fitted and no Econet or speech, not the board in
// the photograph. Coordinates are millimetres from the board's left rear corner
// as the component side is seen with the rear connectors at the far edge: x to
// the right, y towards the front. A browser module: no Node imports, because
// the model's bundle includes it.

import * as P from './bbc-micro-board-parts.mjs';

export const { BOARD, ICS, CONNECTORS, PASSIVES, OTHERS } = P;

/** Each kind's typical height, said so: `measured` is false for all of them. */
export const HEIGHTS = P.HEIGHTS_TYPICAL;

/** The ICs a Model B with the disc interface has fitted, the ones the model draws. */
export const FITTED = ICS.filter((ic) => ic.fitted);

/**
 * The chips the machine counts, in the host's order (BbcChip in C#,
 * panel.bbc.accessCounts()): the list ./access.mjs's sampler takes. Each is an
 * IC's `sheila` in the parts file.
 */
export const COUNTED = ['crtc', 'acia', 'serialUla', 'videoUla', 'romLatch', 'systemVia', 'userVia', 'fdc8271', 'adc', 'sound'];

/** The paged ROM in each slot the machine fills (BbcBus.PagedRom): panel.bbc.romSlot() names the slot. */
export const PAGED_SLOTS = { 15: 'BASIC', 14: 'DFS' };

/**
 * The copper of both faces and the print, in one map
 * (site/src/assets/tracks/bbc-micro-board.webp, copied to `src` by
 * scripts/build-models.mjs): red is the component side's copper, green the
 * solder side's, blue the printed legend, each 255 or 0, in the board frame.
 * Pixel (u, v)'s centre is board millimetre originMm + ((u + 0.5) / pxPerMm,
 * (v + 0.5) / pxPerMm), as tools/bbc-micro-model/data/copper.json gives it (a
 * test holds the two the same). Traced to look at: its connections are not
 * verified, and the checks that would have shown them failed.
 */
export const TRACKS = { src: '/models/bbc-micro-board-tracks.webp', width: 2480, height: 1837, pxPerMm: 8, originMm: [0, -0.0492] };

const number = (ref) => Number(ref.slice(2));
const byRef = (a, b) => number(a.ref) - number(b.ref);

/** The IC holding the paged ROM in `slot`, or null for a slot the machine leaves empty. */
export function pagedRef(slot) {
  const rom = PAGED_SLOTS[slot];
  return rom ? FITTED.find((ic) => ic.paged === rom)?.ref ?? null : null;
}

/** The ICs to mark for a sample of the sampler (null: none), in IC number order. */
export function marksFrom(rates) {
  if (!rates) return [];
  return FITTED.filter((ic) => ic.sheila && rates[ic.sheila]?.moved).sort(byRef).map((ic) => ic.ref);
}

/** What a legend row's mark column says for a chip the machine counts. */
export const COUNTED_WORDS = {
  crtc: 'when the processor reads or writes it',
  acia: 'when the processor reads or writes it',
  serialUla: 'when the processor reads or writes it',
  videoUla: 'when the processor reads or writes it',
  romLatch: 'when the processor reads or writes it, as it does to choose a paged ROM',
  systemVia: 'when the processor reads or writes it',
  userVia: 'when the processor reads or writes it',
  fdc8271: 'when the processor reads or writes it',
  adc: 'when the processor reads or writes it',
  sound: 'when the processor writes a byte to it through the system VIA',
};

/** Why an IC the machine does not count is never marked, by kind. */
export const NEVER = {
  cpu: 'in use all the time: it is the processor',
  ram: 'in use all the time: the processor and the screen read it',
  os: 'in use all the time: the processor runs the operating system from it',
  other: 'the machine counts only the chips the processor reaches at their own addresses',
};

/** Why an IC is never marked: its kind in NEVER. The RAM is the 4816s, table 8.1's IC53 to IC68. */
export function neverOf(ic) {
  if (ic.ref === 'IC1') return 'cpu';
  if (ic.ref === 'IC51') return 'os';
  if (ic.part === '4816') return 'ram';
  return 'other';
}

/**
 * The chip legend: every fitted IC once, in IC number order, with its part
 * and role (table 8.1's, through the parts file), the counter that marks it
 * (`sheila`, one of COUNTED) or the ROM it holds (`paged`), and `mark`, what
 * its row says about when it is marked.
 */
export function chipLegend() {
  return [...FITTED].sort(byRef).map((ic) => ({
    ref: ic.ref, part: ic.part, role: ic.role, sheila: ic.sheila, paged: ic.paged ?? null,
    mark: ic.sheila ? COUNTED_WORDS[ic.sheila] : ic.paged ? `paged: framed while ${ic.paged === 'DFS' ? 'the DFS' : ic.paged} is the paged ROM` : `never: ${NEVER[neverOf(ic)]}`,
  }));
}

/** The words under the legend: what a mark means, why some chips are never marked, what paged means, and what is typical. */
export const LEGEND_WORDS = [
  'A mark on a chip, on the model and in its row here, means the chip was read or written by the processor in the last quarter second, while the machine above runs. A mark says the processor talked to the chip, not that the chip is working: the video circuits draw every picture whether they are marked or not.',
  'The processor, the RAM and the operating system\'s ROM are in use all the time, so none of them is ever marked. Nor are the other chips the machine does not count: it counts only the chips the processor reaches at their own addresses in the &FE00 page, and the sound chip, which the processor writes a byte at a time through the system VIA.',
  'Of the paged ROMs, only one is in the memory map at a time: the paged ROM, BASIC or the DFS, is framed for as long as it is the one chosen.',
  `The machine counts the ${ICS.find((i) => i.sheila === 'acia').part} and the ${ICS.find((i) => i.sheila === 'adc').part}, which it does not emulate, and the serial ULA, which it emulates only in part: it keeps the control byte, which drives the cassette relay. It counts all three whenever the processor reaches their addresses, so they are marked like the others.`,
  'The copper on the board is traced to look at from the scans, and its connections are not verified: the checks that would have shown them failed. The heights of the parts are typical ones, not measured.',
];

/** What the two track buttons do, said for this board, whose copper is traced to look at. */
export const TRACKS_HELP = 'Show tracks puts the copper tracks on the board or takes them off, traced to look at, with their connections not verified, as the checks that would have shown them failed, and Show tracks only fades the parts out so the tracks can be followed.';

/** The model section's accessible name. */
export const LABEL = '3D model of the BBC Micro\'s main board, Issue 7. Point at a chip to name it.';

/** The status line, as the model and the machine run. */
export const STATUS = {
  idle: 'The model is running. The machine above is not running, so no chip is marked: press Start, and the chips the processor reads and writes are marked here.',
  running: 'The model is running, and so is the machine: a marked chip was read or written by the processor in the last quarter second, and the framed ROM is the paged one.',
  tracksOn: 'The copper tracks are on the board, traced to look at: their connections are not verified.',
  tracksOff: 'The tracks are off: the board is plain.',
  partsHidden: 'The parts are hidden, so only the board and its copper show, traced to look at: their connections are not verified.',
  partsShown: 'The parts are back on the board.',
};

/** The status line for a chip pointed at: its reference, role and part, and when it is marked. */
export function pointWords(ref, { marked = [], paged = null } = {}) {
  const ic = FITTED.find((i) => i.ref === ref);
  if (!ic) return null;
  const head = `${ref}, ${ic.role}: ${ic.part}.`;
  if (ic.sheila) return `${head}${marked.includes(ref) ? ' Marked: the processor read or wrote it in the last quarter second.' : ''}`;
  if (ic.paged) return `${head}${paged === ref ? ` Framed: ${ic.paged === 'DFS' ? 'the DFS' : ic.paged} is the paged ROM.` : ''}`;
  return `${head} It is never marked: its row in the legend says why.`;
}

const cm = (mm) => (mm / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const part = (ref) => ICS.find((i) => i.ref === ref).part;

/** The model's text alternative and caption, with every size, count and part read from the layout above. */
export function describe() {
  const typical = Object.entries(HEIGHTS).filter(([k, h]) => k !== 'board' && !h.measured).length;
  const heights = typical === Object.keys(HEIGHTS).length - 1 ? 'the heights of the parts are typical ones, not measured' : 'some heights of the parts are typical ones, not measured';
  const sockets = FITTED.filter((i) => i.socketed).length;
  return `Model, not a photograph. A drawing in three.js of the BBC Micro Model B's main board, Issue 7, ${cm(BOARD.width)} by ${cm(BOARD.depth)} cm, with the copper of both faces and the printed legend traced from flatbed scans of a bare board, to look at: the copper's connections are not verified, and the checks that would have shown them failed. On it are the ${FITTED.length} chips fitted to a Model B with the disc interface, ${sockets} of them in sockets: the processor, a ${part('IC1')}, the ${part('IC78')} disc controller, the operating system's ROM, BASIC and the DFS among them; ${CONNECTORS.length} connectors; ${PASSIVES.length} resistors and capacitors; the crystal, the modulator and the cassette motor's relay. The places are the scan's footprints and the parts are the Service Manual's; ${heights}. While the machine above runs, a chip the processor read or wrote in the last quarter second is marked, and the paged ROM is framed.`;
}
