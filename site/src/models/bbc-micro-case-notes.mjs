// How the BBC Micro case's model was made, for the note under it on the
// machine's page. Every figure comes from `figures`, the committed
// src/data/bbc-micro-case-model.json, which tools/bbc-micro-model/results.py
// writes from data/keys.json and data/case.json (AGENTS.md rule 5: no figure
// typed by hand). The words are here; the numbers are not. Each uncertainty the
// measuring found is said in plain words: above all, that the case's size is
// the Service Manual's and the photographs' measures disagree with it.
//
// Node only: the page builds this into its HTML, and the model's browser
// bundle never imports it.

const mm = (v) => `${v.toLocaleString('en-GB', { maximumFractionDigits: 2 })} mm`;
const times = (v) => v.toLocaleString('en-GB', { maximumFractionDigits: 3 });
const pc = (ratio) => ((ratio - 1) * 100).toLocaleString('en-GB', { maximumFractionDigits: 1 });

/** What each typical value in the results file is, in words. */
const TYPICAL = {
  boardY: 'the board\'s place front to back in the case',
  boardZ: 'the board\'s height in the case',
  cornerRadius: 'the radius of the case\'s corners',
  feet: 'the feet\'s height',
  keyAboveTop: 'how far the key tops stand above the case top',
  panelBelowKeys: 'how far the panel lies below the key tops',
  portSize: 'the ports\' sizes',
  undersideMargin: 'the underside panel round its connectors',
  wall: 'the case\'s wall',
};

const list = (xs) => (xs.length < 2 ? xs.join('') : `${xs.slice(0, -1).join(', ')} and ${xs.at(-1)}`);

/** The sentence on the case's size: the Service Manual's, and the three measures that disagree with it, not resolved. */
export function sizeSentence(f) {
  const { size: s, width: w } = f;
  return `The case's size is the Service Manual's, ${mm(s.widthMm)} wide, ${mm(s.depthMm)} deep and ${mm(s.heightMm)} high with its feet, which the manual gives in its section on the Model B+ and is taken here to hold for the Model B. Three measures on the photographs disagree with it: on the top photograph, registered to that size on the case top's four corners, the keys read ${times(w.caseTopScale)} times the size of the keyboard drawing they come from; the opening's two front corners, placed from two other photographs, imply ${times(w.cornersScale)}; and on the rear photograph, on the scale of the 15-way D socket's two posts, the case is ${mm(w.rearMm)} wide, ${times(w.rearRatio)} times the manual's width. The first two share the drawing and the top photograph, so they are not independent of each other. Either the case is wider than the manual says, or the drawing and the socket both read small, or something common to how the case's corners and ends were marked: this is not resolved, and nothing was rescaled to fit it.`;
}

/** The note's paragraphs, then what the model does not show. */
export function made(f) {
  const { keys: k, o1, o3, frontEdge: fe, profileCheck: pr, rear: r } = f;
  const heights = pr.fallback
    ? `The side profile's check failed (the key A landed ${mm(pr.errMm)} from its place, against ${mm(pr.limitMm)}), so the heights of the key tops and the panel are typical ones, not measured.`
    : `The key A, left out of the camera's fit, lands ${mm(pr.errMm)} from its place on the drawing, within the ${mm(pr.limitMm)} the check allowed, so the triangulation passed; that says the two cameras agree where the legends are, not at the case's edges.`;
  const paragraphs = [
    `${sizeSentence(f)} The model keeps the manual's size and the keys at the drawing's size, and the rear's ports at their places on the D socket's scale.`,
    `The ${k.keycaps} keycaps are those of an open keyboard drawing (Dominic Beesley's, MIT licensed), each matched to one of the machine's ${k.machineKeys} keys, BREAK or the second SHIFT, with none missing and none left over. On the top photograph, each key held out of the fit in turn, the drawing's key centres land within ${mm(o1.medianMm)} at the median and ${mm(o1.maxMm)} at the most, over ${o1.n} keys; on a photograph of the keyboard, over ${o3.n} legends, within ${mm(o3.medianMm)} and ${mm(o3.maxMm)}. Each key's colour was read by eye on the top photograph: ${k.red} red, ${k.light} a brown lighter than the black and ${k.dark} black; a clustering of measured colours agreed on ${k.coloursAgree} of the ${k.keycaps}, and was not used.`,
    `The keys, the lights and the opening's front were placed by triangulating two photographs with one camera fitted to both. The key tops stand ${mm(fe.dMm)} above the case top, with a standard error of ${mm(fe.dSeMm)}, and the lights and the panel round the keys a little below the top. ${heights}`,
    `The front edge's check is ${fe.verdict}. The case's front edge reads ${mm(fe.rawMm)} wide on the top photograph, measured in the plane of the keys, which it does not lie in; corrected for the camera's height above the keys, it would have checked the manual's width on its own, but no measure of that height independent of the width could be had, so the check was dropped before any corrected width was worked out.`,
    `The rear's ports were placed on the rear photograph, on the D socket's scale, and checked against their sockets on the board: ${r.within} of the ${r.n} that could be checked lie within ${mm(r.limitMm)} of their socket's place plus the board's offset in the case, the worst ${mm(r.worstMm)} (${r.worstRef}), so the check passed. It tests the D socket's scale across the rear, not the offset itself: the board's left edge, ${mm(r.boardOffsetMm)} from the case's left end, is good to a few millimetres. The video socket, which is on the case, and the Econet socket, which the machine on this page does not have, were placed from the photograph alone.`,
  ];
  const limits = [
    `The size is the Service Manual's, not measured, and the photographs' three measures read the case ${pc(f.width.low)} to ${pc(f.width.high)} per cent wider; that is not resolved.`,
    `The front edge's check could not be measured: no camera height independent of the case's width was found.`,
    `These are typical, not measured: ${list(f.typical.map((t) => TYPICAL[t] ?? t))}; and the feet's places, the keys' travel and the keycaps' shape.`,
    'The keycaps are tapered blocks with flat tops, not sculpted row by row: the keycap model that would have given their profile could not be fetched.',
    'Each keycap carries its on-screen key\'s legend, one each: the shifted symbols the machine\'s keycaps also print are not drawn, and the space bar is blank, as on the machine. The legends, the badge strip\'s words and the labels are drawn in this site\'s own type, not in Acorn\'s or the BBC\'s lettering.',
    'The badge strip carries its words only: the BBC\'s owl beside them is not drawn.',
    'Parts of the case that were not measured are not drawn: the dark panel round the rear\'s ports, the recess left of the keys, the screws and the labels underneath.',
    'The inside of the case is the other view: the main board, whose copper is traced to look at, with its connections not verified.',
  ];
  return { paragraphs, limits };
}
