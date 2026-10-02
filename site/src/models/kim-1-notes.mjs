// How the KIM-1's model was made, for the note under it on the machine's page.
// Every figure comes from `figures`, the committed src/data/kim-1-model.json,
// which tools/kim1-model/results.py writes from the measurements (AGENTS.md
// rule 5: no figure typed by hand). The words are here; the numbers are not.
//
// Node only: the page builds this into its HTML, and the model's browser
// bundle never imports it.

const WORDS = ['no', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten'];
const count = (n) => WORDS[n] ?? String(n);
const pc = (share) => `${Math.round(share * 100)}%`;
const mm = (v) => v.toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 2 });
const range = (values, f) => {
  const lo = Math.min(...values), hi = Math.max(...values);
  return f(lo) === f(hi) ? f(lo) : `${f(lo)} to ${f(hi)}`;
};

/** What each measured part is called in a sentence. */
const PART = {
  'dip40-ceramic': 'white ceramic 40-pin chips',
  'dip40-plastic': 'socketed 6530',
  dip16: 'memory chips',
  dip14: 'logic chips',
  display: 'display window',
  'keypad-bezel': 'keypad',
  key: 'keys',
  crystal: 'crystal',
};

/** The note's paragraphs, then what the model does not show. */
export function made(figures) {
  const { registration, tracks, heights, layout } = figures;
  const held = Object.values(registration).map((r) => r.heldOutMedianMm);
  const loo = Object.values(tracks.leaveOneOut);
  const worst = heights.before.worst;
  const paragraphs = [
    `It is cross-referenced from ${count(Object.keys(registration).length)} photographs of two original Rev B boards, the Musée Bolo's and another on Hans Otten's site, and from a replica of the board's layout in KiCad, traced by its author from another photograph, scaled by its chips' 0.1 inch pin pitch, and built and run. Every photograph was registered to the replica: four corners marked by hand, refined by matching the photograph's copper to the replica's, then a smooth correction for the lens. On parts of the board held out of that fit, the median error is ${range(held, mm)} mm.`,
    `The top face's tracks are a vote between three photographs: the Musée Bolo's board from each end, and the other board, so a track one of them hides is usually seen by another. Checked against each of the three in turn, left out of the vote, the tracks and that photograph's own tracing agree on ${range(loo.map((e) => e.after), pc)} of their copper (intersection over union); the map traced from the one photograph this model had before agreed on ${range(loo.map((e) => e.before), pc)}, even against the photograph it came from, because it traced the tracks wider and sat a little off where the registration puts them. Against the replica, which is of a later revision and so cannot match everywhere, the match went from ${pc(tracks.top.before)} to ${pc(tracks.top.after)}. Under the chips, the display and the keypad, where no photograph shows the board, ${pc(tracks.top.fromReplicaShare)} of the top face, the copper is the replica's.`,
    `The underside's tracks are traced from one photograph, of the other board's solder side, which shows ${pc(tracks.bottom.seenShare)} of it; they match the replica's underside on ${pc(tracks.bottom.after)}. Before, the underside was plain.`,
    `Heights were triangulated between two photographs of the Musée Bolo's board taken from different places, with one camera fitted to both. A point on the board itself comes out ${mm(heights.checkMm)} mm above it. The typical heights the model used before were off by ${mm(heights.before.meanOffMm)} mm on average, and by ${mm(worst.off)} mm for the ${PART[worst.part] ?? worst.part}.`,
    `The parts soldered through the board stand on the replica's pads. Checked against two photographs, a chip's body is found ${mm(layout.chipBodyOffsetMm.median)} mm from where the replica puts it at the median, ${mm(layout.chipBodyOffsetMm.p90)} mm at the 90th percentile; that includes the parallax of a chip's top, which stands above the board the photographs are registered on. The keys sit on a grid found from their legends, ${mm(layout.keyGridResidualMm.median)} mm from it at the median.`,
  ];
  const limits = [
    'The underside is one photograph of a different board from the one the rest of the model follows, and only the replica checks it.',
    'Under the chips, the display and the keypad the copper is the replica\'s, which is of Rev D, not the photographed Rev B.',
    `Where the photographs disagree, ${pc(tracks.top.disagreeShare)} of what two or more of them see, the vote decides, and the photograph outvoted may be the one that is right.`,
    'One part of each kind was measured for height, and the others of its kind take that height. Of the resistors, capacitors and diodes, two capacitors were measured; the rest take their sizes from their footprints, not from a photograph.',
    'Resistors, capacitors and diodes are plain cylinders without their bands or printing, and the transistors are round where the real ones are flat on one side.',
    'The red wire follows the path one photograph shows, drawn lying just above the board.',
    `The track map has ${tracks.mapPxPerMm} pixels to the millimetre, so close up a track's edge is soft.`,
  ];
  return { paragraphs, limits };
}
