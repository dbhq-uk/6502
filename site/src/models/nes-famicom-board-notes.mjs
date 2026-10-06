// How the NES board's model was made, for the note under it on the machine's
// page, one note for each console it draws. Every figure comes from `figures`,
// the committed src/data/nes-famicom-board-model.json, which
// tools/nes-model/results.py writes from the measurements (AGENTS.md rule 5:
// no figure typed by hand). The words are here; the numbers are not.
//
// Node only: the page builds this into its HTML, and the model's browser
// bundle never imports it.

import { ICS, OTHERS } from './nes-famicom-board-layout.mjs';

const WORDS = ['no', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten', 'eleven', 'twelve'];
const count = (n) => WORDS[n] ?? n.toLocaleString('en-GB');
const pc = (share) => `${Math.round(share * 100)}%`;
const pc1 = (share) => `${(share * 100).toLocaleString('en-GB', { maximumFractionDigits: 1 })}%`;
const mm = (v) => v.toLocaleString('en-GB', { maximumFractionDigits: 3 });
const cm = (v) => (v / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const part = (ref, region) => ICS.find((i) => i.ref === ref).parts[region];
const per = (v) => `${v.toLocaleString('en-GB', { maximumFractionDigits: 2 })}%`;

/** The sentence every note gives on the copper: what its nets check found, and that it is not a netlist. */
export function copperSentence(figures) {
  const n = figures.copper.nets;
  return `The copper is traced to look at, and it is not a netlist: its connections are not verified. The check that would have shown them, that the ground pins of the board's ${count(n.chips)} chips join in one net and their +5V pins in another, failed: ${count(n.gndInLargest)} of ${count(n.gndPins)} ground pins fell in the largest ground net and ${count(n.vccInLargest)} of ${count(n.vccPins)} +5V pins in the largest +5V net, and ${n.touching ? 'a ground pin and a +5V pin came out in the same net' : 'no ground pin shared a net with a +5V pin'}.`;
}

/** The note's paragraphs, then what the model does not show, for one console. */
export function made(figures, region) {
  const { board, scale, solder, copper, parts } = figures;
  const pal = region === 'pal';
  const paragraphs = [
    `The board is an NES-CPU-10, ${cm(board.widthMm)} by ${cm(board.depthMm)} cm, measured from flatbed scans of a bare board, both faces at ${board.dpi} dots an inch, from the OpenTendo project. The scale was taken from the pin rows of the chips, each row held out of the fit in turn: across the board, the median error over ${count(scale.xRows)} rows is ${mm(scale.xMedianMm)} mm on a 40-pin row's length; down it, the chips' row spacings are off by ${per(scale.yMedianPct)} at the median, over ${count(scale.yFootprints)} chips; and the two scales agree to ${per(scale.xyPct)}.`,
    `The solder side's scan was turned over and fitted to the component side on ${solder.holes.toLocaleString('en-GB')} holes found on both. On holes held out of the fit, the median error is ${mm(solder.heldOutMedianMm)} mm, and nine in ten are within ${mm(solder.heldOutP90Mm)} mm.`,
    `The copper of both faces and the printed legend were traced from the two scans into one map, ${copper.mapPxPerMm} pixels to the millimetre, to look at, with its connections not verified: the traced copper covers ${pc(copper.top)} of the component side and ${pc(copper.bottom)} of the solder side, and of the ${copper.drills.toLocaleString('en-GB')} drilled holes found on both scans, ${pc1(copper.drillsTop)} sit in it on the component side and ${pc1(copper.drillsBottom)} on the solder side. ${copperSentence(figures)}`,
    `Every chip and connector is placed on its footprint in the scan. Against OpenTendo's KiCad redrawing of the board, a cross-check from which nothing is drawn, the ${count(parts.ics)} chips' places agree to ${mm(parts.kicadMedianMm)} mm at the median and ${mm(parts.kicadMaxMm)} mm at the worst, after the best turn, scale and shift between the two.`,
    pal
      ? `The PAL console's parts, the ${part('U6', region)} CPU, the ${part('U5', region)} PPU, the ${part('U10', region)} lockout chip, the other chips, the crystal, ${OTHERS[region].find((o) => o.kind === 'crystal').part}, and the ${OTHERS[region].find((o) => o.kind === 'modulator').part} RF modulator, were read off a photograph of a populated PAL board, an NES-CPU-11, registered to the scan: each of its chips lies within ${mm(parts.palWorstMm)} mm of the scanned board's footprint of the same name. No PAL board was scanned bare, so the model draws the NES-CPU-10's copper, traced to look at and not verified, under the PAL parts; the check of the PAL board's layout against the scan, made before the model was built, found the two boards to be one layout within its limits.`
      : `The NTSC console's parts were read off Evan-Amos's photographs of a populated NES-CPU-07, an earlier revision of the same board, from above and from below: each of its chips sits within ${mm(parts.ntscWorstMm)} mm of the CPU-10's footprint of the same name. So the model is an NES-CPU-10 with an NTSC console's parts, as no one board was both scanned bare and photographed populated.`,
  ];
  const limits = [
    'The copper is traced to look at, not as a netlist: its connections are not verified, and in places the trace joins copper that is apart on the board or misses fine tracks and the hatched ground areas.',
    `The heights of the parts are typical ones for their packages, not measured, for all ${count(parts.heightsTypical)} kinds of part and connector the model draws.`,
    'Which controller port each buffer serves, U7 port 1 and U8 port 2, is by the board\'s print, checked against the nesdev wiki and the KiCad redrawing, not traced on the scan. U9\'s jobs are read from the redrawing\'s nets, not traced.',
    pal
      ? 'The PAL board is drawn as the scanned NTSC board, its copper traced to look at and not verified, with the PAL parts on it: no bare PAL board was scanned.'
      : 'The parts are a CPU-07\'s, on the CPU-10\'s footprints.',
    'The resistors, capacitors and other small parts are plain cylinders and boxes of typical sizes, without their bands or printing, and the connectors and the modulator are plain boxes.',
    ...(pal ? ['The PAL modulator and crystal are drawn where the PAL board\'s photograph shows them: the modulator\'s can a few millimetres to the right of the NTSC modulator\'s place, which may be the photograph\'s perspective rather than the board, and the crystal shorter than the NTSC one, its lower edge hidden in the photograph.'] : []),
    'The lockout chip is not emulated, so it is never marked.',
    `The track map, traced to look at, has ${copper.mapPxPerMm} pixels to the millimetre, so close up a track's edge is soft.`,
  ];
  return { paragraphs, limits };
}
