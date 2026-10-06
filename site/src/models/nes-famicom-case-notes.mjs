// How the NES case's model was made, for the note under it on the machine's
// page, one note for each console it draws. Every figure comes from `figures`,
// the committed src/data/nes-famicom-case-model.json, which
// tools/nes-model/results.py writes from tools/nes-model/data/case.json
// (AGENTS.md rule 5: no figure typed by hand). The words are here; the numbers
// are not. Each uncertainty the measuring found is said in plain words.
//
// Node only: the page builds this into its HTML, and the model's browser
// bundle never imports it.

const WORDS = ['no', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten'];
const count = (n) => WORDS[n] ?? n.toLocaleString('en-GB');
const mm = (v) => v.toLocaleString('en-GB', { maximumFractionDigits: 2 });
const mm1 = (v) => v.toLocaleString('en-GB', { maximumFractionDigits: 1 });
const pc = (v) => Math.abs(v).toLocaleString('en-GB', { maximumFractionDigits: 2 });

/** The sentence on the case's size: published, not Nintendo's, and how well it is known. */
export function sizeSentence(f) {
  const s = f.size;
  return `The case's size is the published ${mm(s.widthMm)} by ${mm(s.depthMm)} mm and ${mm(s.heightMm)} mm high, from more than one published source, none of them Nintendo's, so it is good to about ${mm(s.goodToPct)} per cent.`;
}

/** The sentence on the rear connectors: where they were placed from, and the check that failed. */
export function rearSentence(f) {
  const r = f.rear;
  return `The rear connectors are placed where a photograph of a real console shows them, which agrees with the design patent's rear view within ${mm(r.placesUsedMm)} mm. Their places worked out from the board, as a check, missed by up to ${mm(r.boardMissMm)} mm, and ${count(r.within)} of the ${count(r.checked)} ${r.within === 1 ? 'was' : 'were'} within the ${mm(r.limitMm)} mm limit: the check failed as measured, and the model keeps the photograph's places.`;
}

/** The note's paragraphs, then what the model does not show, for one console. */
export function made(f, region) {
  const pal = region === 'pal';
  const p = f.profile;
  const paragraphs = [
    `${sizeSentence(f)} Before the model was built, the proportions of the design patent's drawings were checked against it, and the depth and the height ${f.spike.depth === 'pass' && f.spike.height === 'pass' ? 'passed' : 'did not both pass'}, on limits widened after the drawings were measured, so that the check catches a gross error of scale and not a small one (two earlier checks, on photographs and then on the drawings with tighter limits, stopped, and stay on record); on a photograph of a console taken apart, the case's depth to its width comes out ${pc(f.widthCrossCheckPct)} per cent ${f.widthCrossCheckPct < 0 ? 'under' : 'over'} the published figures'.`,
    `The front, the top and the rear were measured on photographs of an NTSC console from its corners, each face straightened out on its own corners: the cartridge door, the vents, the buttons, the power light, the controller ports and the rear's window. The underside, its expansion cover and the feet are from the design patent's view from below. The bottom shell's ends lean in below a break: by ${mm(p.insetMm)} mm at the base, the average of the two ends on one photograph. The four ends read on two photographs range from ${mm(p.endsMm.min)} to ${mm(p.endsMm.max)} mm, so the inset is an average good to about ${mm(p.goodToMm)} mm; on the second photograph the same averaging comes within ${mm(p.heldOutMm)} mm of it (the limit was ${mm(p.limitMm)} mm), which shows the method repeats, not that the average is the true inset.`,
    `${rearSentence(f)} The board's place in the case, which that check used, is good to about ${mm(f.boardInCase.uncertaintyMm)} mm, and ${f.boardInCase.sharedMoulding ? 'assumes that the PAL and NTSC cases share one moulding, as it was measured in a PAL case' : 'was measured in this console\'s case'}.`,
    pal
      ? `The PAL console's front words, its rear and its two labels underneath were read from photographs of a PAL console taken apart. The front and the rear were taken with a ${mm(f.palLensMm)} mm lens, so with strong perspective, and they are placed as the NTSC console's are. The plastic of the console photographed has yellowed; the model draws it as made, in the NTSC console's greys.`
      : 'The NTSC console has no labels underneath in the model: no photograph of an NTSC console\'s underside was used.',
  ];
  const limits = [
    `The size is the published one, not Nintendo's, good to about ${mm(f.size.goodToPct)} per cent; the corners are drawn square, where the case's are rounded.`,
    `The bottom shell's lean is an average, good to about ${mm(p.goodToMm)} mm.`,
    `The rear connectors agree with the patent within ${mm(f.rear.placesUsedMm)} mm, but the check against the board failed, by up to ${mm(f.rear.boardMissMm)} mm.`,
    `The buttons' travel, ${mm(f.buttons.travelMm)} mm, is a ${f.buttons.travelFrom} one, not measured.`,
    `Whether POWER latches in on a real console is not known${f.powerLatchSeen ? '' : ': no photograph shows it pressed'}. The model shows it in while the machine runs, as the design chose.`,
    `The video and audio jacks on the side${f.rear.avChecked ? ' are' : ' are not'} checked against the board, and ${f.rear.palChecked ? 'the PAL console\'s rear is' : 'neither is the PAL console\'s rear'}.`,
    `The PAL console's front words and rear were read from photographs taken with a ${mm(f.palLensMm)} mm lens, with strong perspective.`,
    'The lockout chip is not emulated, so the power light never blinks as a real console\'s does when it refuses a cartridge.',
    `The rear's window is drawn flat on the rear face; on the console it is set in, by about ${mm1(f.rear.windowSetBackMm)} mm.`,
    'The underside\'s ribs are drawn as lines.',
    'The stage\'s light, the same for every model on this site, tints the greys a little green.',
    'The cartridge door does not open, and the inside of the case is the other view: the board.',
    ...(pal ? ['The PAL console\'s rear words are printed on three lines, NETZGERAT without its umlaut as the case has it; the model draws each on one line, squeezed into the box the NTSC console\'s words take.'] : []),
  ];
  return { paragraphs, limits };
}
