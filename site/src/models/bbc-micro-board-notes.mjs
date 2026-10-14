// How the BBC Micro board's model was made, for the note under it on the
// machine's page. Every figure comes from `figures`, the committed
// src/data/bbc-micro-board-model.json, which tools/bbc-micro-model/results.py
// writes from the measurements (AGENTS.md rule 5: no figure typed by hand). The
// words are here; the numbers are not.
//
// Node only: the page builds this into its HTML, and the model's browser
// bundle never imports it.

const mm = (v) => `${v.toLocaleString('en-GB', { maximumFractionDigits: 3 })} mm`;
const pc = (share) => `${Math.round(share * 100)}%`;
const pc1 = (share) => `${(share * 100).toLocaleString('en-GB', { maximumFractionDigits: 1 })}%`;
const per = (v) => `${v.toLocaleString('en-GB', { maximumFractionDigits: 2 })}%`;
const cm = (v) => (v / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const n = (v) => v.toLocaleString('en-GB');
const list = (refs) => (refs.length < 2 ? refs.join('') : `${refs.slice(0, -1).join(', ')} and ${refs[refs.length - 1]}`);

/**
 * A scale check's verdict in a visitor's words: the check was set before the
 * figure was measured, with a figure it passes at and a worse one at which it
 * would have stopped the work.
 */
export const VERDICT_WORDS = {
  pass: 'which passes the check set for it before it was measured',
  'between pass and stop': 'which falls short of the figure its check, set before it was measured, passes at, but not as far as the figure at which it would have stopped the work',
  stop: 'which is past the figure at which its check, set before it was measured, stops the work',
};

/**
 * The sentence every note gives on the copper: that it is not a netlist, what
 * its nets check found, and what the comparison with a photograph of a
 * populated board found, both failed.
 */
export function copperSentence(figures) {
  const { nets, againstI5: i5 } = figures.copper;
  return `The copper is traced to look at, and it is not a netlist: its connections are not verified. The check that would have shown them, that the ground pins of ${nets.chips} chips join in one net and their +5V pins in another, failed: ${nets.gndInLargest} of ${nets.gndPins} ground pins fell in the largest ground net and ${nets.vccInLargest} of ${nets.vccPins} +5V pins in the largest +5V net, and ${nets.touching ? 'a ground pin and a +5V pin came out in the same net' : 'no ground pin shared a net with a +5V pin'}. Nor does the component side's copper, traced to look at, agree with the copper a photograph of a populated board shows: over the ${n(Math.round(i5.comparedMm2))} square millimetres compared, the two overlap by ${i5.iou.toLocaleString('en-GB', { maximumFractionDigits: 3 })} of their union, and that check failed too.`;
}

/** The sentence every note gives on the parts: both checks on them failed, and what the model keeps. */
export function partsSentence(figures) {
  const { manual, identities } = figures.parts;
  return `The two checks on these parts failed. Against the Service Manual's approximate places, after the best turn, scale and shift between the two, ${list(manual.over)} are more than ${mm(manual.limitMm)} off, the worst ${mm(manual.maxMm)}, with the median ${mm(manual.medianMm)}. And of the chips' markings read on a photograph of a populated board, ${identities.disagree} of the ${identities.read} read disagree with the manual. So the places stay the scan's footprints, the chips are named with table 8.1's parts, and the chips fitted are those of the machine the page emulates, with the disc interface and without Econet or speech, not the board in I5, the photograph the markings were read on.`;
}

/** The note's paragraphs, then what the model does not show. */
export function made(figures) {
  const { board, scale, solder, copper, parts } = figures;
  const i5 = copper.i5Registration;
  const paragraphs = [
    `The board is an Issue 7, ${cm(board.widthMm)} by ${cm(board.depthMm)} cm, measured from flatbed scans of a bare board, both faces, shared by amb5l. The scale was taken from the pin rows of the chips and the connectors, each row held out of the fit in turn: down the board, the median error over ${scale.yRows} rows of 40-pin chips is ${mm(scale.yMedianMm)}; across it, over the ${scale.xRows} rows long enough to be scored, each scaled to a 40-pin row's length, the median is ${mm(scale.xMedianMm)}, ${VERDICT_WORDS[scale.xVerdict]}, and the largest ${mm(scale.xLargestMm)}, on ${scale.xLargestRow}; the two scales agree to ${per(scale.xyPct)}.`,
    `The solder side's scan was turned over and fitted to the component side on ${n(solder.holes)} holes found on both. On holes held out of the fit, the median error is ${mm(solder.heldOutMedianMm)}, and nine in ten are within ${mm(solder.heldOutP90Mm)}.`,
    `The copper of both faces and the printed legend were traced from the two scans into one map, ${copper.mapPxPerMm} pixels to the millimetre, to look at, with its connections not verified: the traced copper covers ${pc(copper.top)} of the component side and ${pc(copper.bottom)} of the solder side, and of the ${n(copper.drills)} drilled holes, ${pc1(copper.drillsTop)} sit in it on the component side and ${pc1(copper.drillsBottom)} on the solder side, which every hole with a pad must. ${copperSentence(figures)} The photograph was registered to the scan on ${i5.points} chip pins of ${i5.chips} chips, each chip held out in turn, to ${mm(i5.heldOutMedianMm)} at the median; that figure is optimistic, and not an independent check, because the pins were chosen where they fitted well.`,
    `Every chip is placed on its footprint in the scan, and named from table 8.1 of Acorn's Service Manual: ${parts.ics} chips have a place there, and the model draws the ${parts.fitted} fitted, ${parts.socketed} of them in sockets the manual names. ${partsSentence(figures)}`,
  ];
  const limits = [
    'The copper is traced to look at, not as a netlist: its connections are not verified, and the trace joins copper that is apart on the board in places and breaks copper that is joined in others.',
    `The print hides the copper under it: of ${n(Math.round(copper.printMm2))} square millimetres of print, the copper, traced to look at and not verified, was recovered under ${n(Math.round(copper.recoveredMm2))}, where a track runs on in line either side of it, and the rest is drawn as print only.`,
    `The heights of the parts are typical ones for their packages, not measured, for all ${parts.heightsTypical} kinds of part and connector the model draws: no photograph of a populated Issue 7 board from the side was found.`,
    `The ${parts.passives} resistors and capacitors, ${parts.axial} lying down and ${parts.radial} standing, are read from the print between pairs of pads, and drawn as plain cylinders and boxes of typical sizes, without their bands or printing; the connectors, the modulator, the crystal and the relay are plain boxes.`,
    'The chips this machine does not have, the Econet and speech parts, the ones table 8.1 lists as not fitted and the empty sockets for more paged ROMs, are not drawn.',
    `The track map, traced to look at, has ${copper.mapPxPerMm} pixels to the millimetre, so close up a track's edge is soft.`,
  ];
  return { paragraphs, limits };
}
