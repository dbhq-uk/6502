// The plan's task 0 thresholds, set before the measurements
// (docs/superpowers/plans/2026-10-05-nes-models.md, Global Constraints).
// Revised 5 October 2026 after task 0's figures were seen (Dan): the case's
// depth and height are judged on the design patent's orthographic views
// (case.patent), the height with the depth's limits; the row on the two
// photographs' heights agreeing is dropped; the PAL front is O4 against the
// patent's front view and passes only on at least two measured ratios. No
// limit moved. The first verdicts are kept in spike.json's revision.
export const PASS = { xMedian: 0.15, yMedianPct: 0.5, solderMedian: 0.20, solderP90: 0.40, palMedian: 1.0, palMax: 2.0, depthPct: 1.5, heightPct: 1.5, palFrontPct: 2.0 };
export const STOP = { xMedian: 0.25, yMedianPct: 1.0, ratioPct: 1.5, solderMedian: 0.30, solderP90: 0.60, palAny: 3.0, depthPct: 3.0, heightPct: 3.0, palFrontPct: 4.0 };
export const MIN = { xRows: 4, yFootprints: 8, solderHoles: 150, palParts: 10, palFrontRatios: 2 };

/** Each check's verdict from spike.json's figures: 'pass', 'between pass and stop' or 'STOP', in the table's order. */
export function verdicts(s) {
  const v = (ok, stop) => (stop ? 'STOP' : ok ? 'pass' : 'between pass and stop');
  const so = s.solder.heldOutMm;
  const pal = s.palLayout;
  const depth = Math.abs(s.case.patent.depthToWidthErrPct);
  const height = Math.abs(s.case.patent.heightToWidthErrPct);
  const ratios = s.palFront.ratioErrPct;
  const front = ratios.length ? Math.max(...ratios.map(Math.abs)) : 0;
  return [
    { check: 'scale x', verdict: v(s.scale.x.scaledErrMm.median <= PASS.xMedian && s.scale.x.rows >= MIN.xRows, s.scale.x.scaledErrMm.median > STOP.xMedian) },
    { check: 'scale y', verdict: v(s.scale.y.errPct.median <= PASS.yMedianPct && s.scale.y.footprints >= MIN.yFootprints, s.scale.y.errPct.median > STOP.yMedianPct) },
    { check: 'x against y', verdict: v(true, 100 * Math.abs(s.scale.ratio - 1) > STOP.ratioPct) },
    { check: 'solder side', verdict: v(so.median <= PASS.solderMedian && so.p90 <= PASS.solderP90 && s.solder.holes >= MIN.solderHoles, so.median > STOP.solderMedian || so.p90 > STOP.solderP90) },
    { check: 'PAL layout', verdict: v(pal.heldOutMm.median <= PASS.palMedian && pal.heldOutMm.max <= PASS.palMax && pal.parts >= MIN.palParts, pal.heldOutMm.max > STOP.palAny || pal.unmatched.length > 0) },
    { check: 'case depth', verdict: v(depth <= PASS.depthPct, depth > STOP.depthPct) },
    { check: 'case height', verdict: v(height <= PASS.heightPct, height > STOP.heightPct) },
    { check: 'PAL front', verdict: v(front <= PASS.palFrontPct && ratios.length >= MIN.palFrontRatios, front > STOP.palFrontPct) },
  ];
}
