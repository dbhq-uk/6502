// The NES main board's layout for its inside 3D model, and the words the page
// says about it: its caption for each console and its chip legend.
//
// The figures are measured, not typed: tools/nes-model/ measured the bare
// NES-CPU-10 scans (OpenTendo's, read from the dbhq-uk fork) and the
// photographs of populated boards, and board_parts.py wrote
// ./nes-famicom-board-parts.mjs, which this module gives the shapes the model
// and the page use. The journal for 5 and 6 October 2026 has every figure.
//
// One board for both consoles. The places are the NES-CPU-10 scan's
// footprints; the NTSC console's parts were read off photographs of a
// populated NES-CPU-07 and the PAL console's off a photograph of an NES-CPU-11,
// each checked to sit on the CPU-10's footprints. So the model is an NES-CPU-10
// with each console's parts, not a photograph of one real board.
//
// Coordinates are millimetres from the board's top left corner as the
// component side lies with the edge fingers at the bottom: x to the right, y
// down. A browser module: no Node imports, because the model's bundle
// includes it.

import * as P from './nes-famicom-board-parts.mjs';

export const { BOARD, ICS, CONNECTORS, PASSIVES, OTHERS, HEIGHTS } = P;

/** The consoles the model draws, the first shown until the page names another. */
export const REGIONS = ['ntsc', 'pal'];

/** Each console, as the caption names it. */
export const CONSOLES = {
  // `board` is the board the caption says the console's parts are drawn on: the NTSC parts are on the scanned
  // NES-CPU-10's own layout; the PAL parts were read off an NES-CPU-11, whose chips' places task 0 checked against the scan's
  // (the connectors and the copper were not compared; reworded in the final fix wave, 6 Oct 2026).
  ntsc: { name: 'an NTSC NES-001', sold: 'North America', short: 'NTSC', a: 'an NTSC console', board: 'an NES-CPU-10' },
  pal: { name: 'a PAL NESE-001', sold: 'Europe', short: 'PAL', a: 'a PAL console', board: 'drawn on an NES-CPU-10, whose chips\' places the PAL console\'s NES-CPU-11 was checked to share' },
};

/**
 * The copper of both faces and the print, in one map
 * (site/src/assets/tracks/nes-famicom-board.webp, copied to `src` by
 * scripts/build-models.mjs): red is the component side's copper, green the
 * solder side's, blue the printed legend, each 255 or 0, in the board frame.
 * Pixel (u, v)'s centre is board millimetre originMm + ((u + 0.5) / pxPerMm,
 * (v + 0.5) / pxPerMm), as tools/nes-model/data/copper.json gives it (a test
 * holds the two the same). Traced to look at: its connections are not
 * verified.
 */
export const TRACKS = { src: '/models/nes-famicom-board-tracks.webp', width: 1959, height: 1194, pxPerMm: 10, originMm: [-0.1465, -0.1119] };

/** The chips by reference number, U1 to U10. */
const byRef = (a, b) => Number(a.ref.slice(1)) - Number(b.ref.slice(1));

/**
 * The chip legend for one console: every IC once, in reference order, with
 * its role, its part in this console and in the other, the counter that marks
 * it (`chip`, one of the machine's NesChip, lower case) or why it is never
 * marked (`always`), and `mark`, what its row says about when it is marked.
 */
export function chipLegend(region) {
  if (!REGIONS.includes(region)) throw new Error(`the board model draws ${REGIONS.join(' and ')}, not ${region}`);
  const other = REGIONS.find((r) => r !== region);
  return [...ICS].sort(byRef).map((ic) => ({
    ref: ic.ref, role: ic.role, part: ic.parts[region], otherPart: ic.parts[other], chip: ic.chip, always: ic.always,
    mark: ic.chip ? COUNTED_WORDS[ic.chip] : `never: ${neverWords(ic)}`,
  }));
}

/** The short word in a legend row for a chip that is never marked: why. */
export const NEVER = {
  cpu: 'in use all the time',
  ram: 'in use all the time',
  decoder: 'in use all the time',
  latch: 'the PPU\'s own',
  cartridge: 'in use all the time',
  lockout: 'not emulated',
  inverter: 'in use all the time',
};

/** What a legend row's mark column says for a chip the machine counts. */
export const COUNTED_WORDS = {
  apu: 'when the processor uses the sound, sprite-copy and controller registers on its own chip',
  ppu: 'when the processor reads or writes it',
  pad1: 'when the processor reads controller port 1',
  pad2: 'when the processor reads controller port 2',
};

/** The video RAM is the PPU's own, as the latch is: its row says so, where the work RAM's says it is in use all the time. */
export const neverWords = (row) => (row.always === 'ram' && /video/.test(row.role) ? 'the PPU\'s own' : NEVER[row.always]);

/**
 * The words under the legend: what a mark means, why some chips are never
 * marked, where U9's jobs and U7's and U8's ports come from, and that the
 * heights are typical. The page shows them once, for both consoles.
 */
export const LEGEND_WORDS = [
  'A mark on a chip, on the model and in its row here, means the chip was read or written by the processor in the last quarter second. The rate beside it is how many times a second, over that quarter second, while the machine above runs. A mark says the processor talked to the chip, not that the chip is working: the PPU draws every picture whether it is marked or not.',
  'The CPU, the work RAM, the address decoder and the cartridge are in use all the time; the video RAM and the address latch are the PPU\'s own, in use whenever it draws; and the lockout chip is not emulated. So none of them is marked for being in use, and all but the CPU are never marked. The CPU\'s row is marked only when the processor uses the sound, sprite-copy and controller registers on its own chip ($4000 to $4015, and the writes to $4016 and $4017), which the machine counts apart.',
  'By the redrawing\'s nets, U9, the hex inverter, inverts the PPU\'s address line A13 and the reset line and clocks the lockout chip, so it is in use all the time and is never marked. Those jobs are read from OpenTendo\'s KiCad redrawing of the board, a cross-check, and are not traced on the scan.',
  'U7 and U8 are the controller ports\' buffers. Which serves which port is by the board\'s print, 40H368(CI) and 40H368(CII): U7 is port 1, read at $4016, and U8 port 2, read at $4017. That was checked against the nesdev wiki and the KiCad redrawing, and is not traced on the scan.',
  'The copper on the board is traced to look at from the scans, and its connections are not verified, so the model cannot show which chip a track joins. The heights of the parts are typical ones, not measured.',
];

/** What the two track buttons do, said for this board, whose copper is traced to look at. */
export const TRACKS_HELP = 'Show tracks puts the copper tracks on the board or takes them off, traced to look at, with their connections not verified, and Show tracks only fades the parts out so the tracks can be followed.';

/** The model section's accessible name, for each console. */
export const LABELS = {
  ntsc: '3D model of the NES\'s main board, with an NTSC console\'s chips. Point at a chip to name it.',
  pal: '3D model of the NES\'s main board, with a PAL console\'s chips. Point at a chip to name it.',
};

const WORDS = ['no', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten', 'eleven', 'twelve'];
const count = (n) => WORDS[n] ?? n.toLocaleString('en-GB');
const cm = (mm) => (mm / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const part = (ref, region) => ICS.find((i) => i.ref === ref).parts[region];
const connector = (ref) => CONNECTORS.find((c) => c.ref === ref).label;
const other = (region, kind) => OTHERS[region].find((o) => o.kind === kind);

/** The model's text alternative and caption for one console, with every size, count and part read from the layout above. */
export function describe(region) {
  const c = CONSOLES[region];
  if (!c) throw new Error(`the board model draws ${REGIONS.join(' and ')}, not ${region}`);
  const typical = Object.entries(HEIGHTS).filter(([k, h]) => k !== 'board' && !h.measured).length;
  const heights = typical === Object.keys(HEIGHTS).length - 1 ? 'the heights of the parts are typical ones, not measured' : 'some heights of the parts are typical ones, not measured';
  return `Model, not a photograph, of ${c.name}, the front-loading console sold in ${c.sold}: a drawing of its main board in three.js, ${c.board}, ${cm(BOARD.width)} by ${cm(BOARD.depth)} cm, with the copper of both faces and the printed legend traced from flatbed scans of a bare board, to look at: the copper's connections are not verified. On it are the ${count(ICS.length)} chips, with this console's parts: the CPU, ${part('U6', region)}, the PPU, ${part('U5', region)}, and the lockout chip, ${part('U10', region)}; ${connector('P1')}, ${connector('P2')}, the two controller ports' headers and ${connector('P6')}; ${count(PASSIVES.length)} resistors, capacitors and other small parts; the crystal, ${other(region, 'crystal').part}, and the RF modulator. The places are the scan's; the parts were read off photographs of populated boards and placed on the scan's footprints, so this is the scanned board with ${c.a}'s parts, not one real board; ${heights}. While the machine above runs, a chip the processor read or wrote in the last quarter second is marked.`;
}
