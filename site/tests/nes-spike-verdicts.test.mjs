import test from 'node:test';
import assert from 'node:assert/strict';
import { verdicts } from './nes-spike-verdicts.mjs';

const good = {
  scale: { x: { rows: 6, scaledErrMm: { median: 0.08, max: 0.4 } }, y: { footprints: 10, errPct: { median: 0.3 } }, ratio: 1.004 },
  solder: { holes: 400, heldOutMm: { median: 0.12, p90: 0.3, max: 0.9 } },
  palLayout: { parts: 13, heldOutMm: { median: 0.6, max: 1.5 }, unmatched: [] },
  case: { patent: { depthToWidthErrPct: -0.8, heightToWidthErrPct: 1.1 } },
  palFront: { ratioErrPct: [0.5, -1.2] },
};
const names = (s) => verdicts(s).map((x) => x.verdict);
const tweak = (path, value) => { const s = structuredClone(good); let o = s; path.slice(0, -1).forEach((k) => { o = o[k]; }); o[path.at(-1)] = value; return s; };

test('figures inside every pass limit pass every check; the largest x row error is recorded, not judged', () => {
  assert.deepEqual(names(good), Array(8).fill('pass'));
});

test('each check stops on its own figure, and only that check', () => {
  const cases = [
    [['scale', 'x', 'scaledErrMm', 'median'], 0.26, 0],
    [['scale', 'y', 'errPct', 'median'], 1.1, 1],
    [['scale', 'ratio'], 1.016, 2],
    [['solder', 'heldOutMm', 'p90'], 0.61, 3],
    [['palLayout', 'heldOutMm', 'max'], 3.1, 4],
    [['palLayout', 'unmatched'], ['U9'], 4],
    [['case', 'patent', 'depthToWidthErrPct'], -8.1, 5],
    [['case', 'patent', 'depthToWidthErrPct'], 8.1, 5],
    [['case', 'patent', 'heightToWidthErrPct'], 8.1, 6],
    [['case', 'patent', 'heightToWidthErrPct'], -8.1, 6],
    [['palFront', 'ratioErrPct'], [0.5, 4.5], 7],
    [['palFront', 'ratioErrPct'], [-4.1], 7],
  ];
  for (const [p, value, i] of cases) {
    const got = names(tweak(p, value));
    assert.equal(got[i], 'STOP', `${p.join('.')} = ${JSON.stringify(value)}`);
    assert.equal(got.filter((x) => x === 'STOP').length, 1, `${p.join('.')} stops another check too`);
  }
});

test('between the limits is neither a pass nor a stop, and too few points is never a pass', () => {
  assert.equal(names(tweak(['scale', 'x', 'scaledErrMm', 'median'], 0.2))[0], 'between pass and stop');
  assert.equal(names(tweak(['solder', 'holes'], 149))[3], 'between pass and stop');
  assert.equal(names(tweak(['palLayout', 'parts'], 9))[4], 'between pass and stop');
  assert.equal(names(tweak(['case', 'patent', 'depthToWidthErrPct'], -5.1))[5], 'between pass and stop');
  assert.equal(names(tweak(['case', 'patent', 'depthToWidthErrPct'], -4.9))[5], 'pass');
  assert.equal(names(tweak(['case', 'patent', 'heightToWidthErrPct'], 5.1))[6], 'between pass and stop');
  assert.equal(names(tweak(['case', 'patent', 'heightToWidthErrPct'], 4.9))[6], 'pass');
  assert.equal(names(tweak(['case', 'patent', 'depthToWidthErrPct'], 7.9))[5], 'between pass and stop');
  assert.equal(names(tweak(['case', 'patent', 'heightToWidthErrPct'], -7.9))[6], 'between pass and stop');
});

test('the PAL front passes only on at least two measured ratios, each within the limit', () => {
  assert.equal(names(tweak(['palFront', 'ratioErrPct'], [0.5]))[7], 'between pass and stop');
  assert.equal(names(tweak(['palFront', 'ratioErrPct'], []))[7], 'between pass and stop');
  assert.equal(names(tweak(['palFront', 'ratioErrPct'], [0.5, -1.9]))[7], 'pass');
  assert.equal(names(tweak(['palFront', 'ratioErrPct'], [0.5, 2.1]))[7], 'between pass and stop');
});
