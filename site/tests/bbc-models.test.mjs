import test from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from '../src/lib/registry.mjs';

// The BBC Micro's two 3D models: their inputs and the measurements they rest
// on. The tools in tools/bbc-micro-model/ run offline, by hand; these tests
// read what they committed.

const TOOL = path.join(REPO_ROOT, 'tools', 'bbc-micro-model');
const sources = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'sources.json'), 'utf8')).sources;
const spike = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'spike.json'), 'utf8'));

const KINDS = ['scan', 'photograph', 'drawing', 'document', 'model'];
const FIELDS = ['id', 'kind', 'file', 'url', 'page', 'author', 'licence', 'fetched', 'bytes', 'width', 'height', 'sha256', 'committed', 'use'];

test('every input in the BBC models\' sources.json is fully described', () => {
  assert.ok(sources.length >= 7, `only ${sources.length} inputs`);
  assert.equal(new Set(sources.map((s) => s.id)).size, sources.length, 'an id is used twice');
  for (const s of sources) {
    for (const f of FIELDS) assert.ok(f in s, `${s.id} has no ${f}`);
    assert.ok(KINDS.includes(s.kind), `${s.id}: kind ${s.kind}`);
    assert.match(s.sha256, /^[0-9a-f]{64}$/, `${s.id}: sha256`);
    assert.ok(s.licence === null || (typeof s.licence === 'string' && s.licence.length > 0), `${s.id}: licence is a string or null`);
    assert.match(s.fetched, /^\d{4}-\d{2}-\d{2}$/, `${s.id}: fetched`);
    assert.ok(!Number.isNaN(Date.parse(s.fetched)), `${s.id}: fetched is not a date`);
    assert.ok(Number.isInteger(s.bytes) && s.bytes > 0, `${s.id}: bytes`);
    for (const d of ['width', 'height']) {
      if (s.kind === 'scan' || s.kind === 'photograph') assert.ok(Number.isInteger(s[d]) && s[d] > 0, `${s.id}: ${d}`);
      else assert.ok(s[d] === null || Number.isInteger(s[d]), `${s.id}: ${d}`);
    }
    for (const u of ['url', 'page']) assert.match(s[u], /^https?:\/\/\S+$/, `${s.id}: ${u}`);
    assert.ok(typeof s.author === 'string' && s.author.length > 0, `${s.id}: author`);
    assert.ok(typeof s.file === 'string' && s.file.length > 0 && !path.isAbsolute(s.file), `${s.id}: file`);
    assert.ok(typeof s.use === 'string' && s.use.length > 0, `${s.id}: use`);
    if (s.licence === null || s.kind === 'scan') assert.equal(s.committed, null, `${s.id} has no stated licence or is a scan, so it is never committed`);
    if (s.committed !== null) assert.ok(fs.existsSync(path.join(process.cwd(), 'src', 'assets', 'photos', s.committed)), `${s.id}: ${s.committed} is not in src/assets/photos`);
  }
});

test('the inputs\' hashes are the ones the research recorded in models.md', () => {
  const md = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'bbc-micro', 'facts', 'models.md'), 'utf8');
  const section = md.split(/^## Downloaded for the work$/m)[1];
  assert.ok(section, 'models.md has no "Downloaded for the work" section');
  const rows = [...section.matchAll(/^\| `([^`]+)` \| `([0-9a-f]{64})` \|$/gm)].map(([, file, sha]) => ({ file, sha }));
  assert.ok(rows.length >= 6, `only ${rows.length} rows read from the table`);
  for (const { file, sha } of rows) {
    const s = sources.find((x) => x.file === file);
    assert.ok(s, `${file} is in models.md but not in sources.json`);
    assert.equal(s.sha256, sha, `${file}: sources.json and models.md disagree`);
  }
  for (const id of ['O1', 'O2', 'O3', 'I1', 'I2', 'I5']) {
    const s = sources.find((x) => x.id === id);
    assert.ok(rows.some((r) => r.file === s.file), `${id} is not in models.md's table`);
  }
});

// Hashes every file under tools/, site/src/assets/, site/public/ and docs/ (a few
// tens of megabytes): an original copied anywhere a page or a tool could ship it fails.
test('no original input is committed, under tools/, site/src/assets/, site/public/ or docs/', () => {
  const originals = new Map(sources.map((s) => [s.sha256, s.id]));
  const skip = new Set(['node_modules', 'bin', 'obj', '__pycache__', '.pytest_cache']);
  let hashed = 0;
  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (skip.has(entry.name)) continue;
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.isFile()) {
        const sha = crypto.createHash('sha256').update(fs.readFileSync(full)).digest('hex');
        hashed += 1;
        assert.ok(!originals.has(sha), `${path.relative(REPO_ROOT, full)} is the original of ${originals.get(sha)}`);
      }
    }
  };
  walk(path.join(REPO_ROOT, 'tools'));
  walk(path.join(process.cwd(), 'src', 'assets'));
  walk(path.join(process.cwd(), 'public'));
  walk(path.join(REPO_ROOT, 'docs'));
  assert.ok(hashed > 20, `only ${hashed} files hashed: the walk is looking in the wrong place`);
});

// The plan's thresholds, set before the measurements
// (docs/superpowers/plans/2026-10-04-bbc-micro-models.md, Global Constraints).
const PASS = { scaleMedian: 0.10, scaleMax: 0.25, solderMedian: 0.15, solderP90: 0.30, solderMax: 0.60, keysMedian: 1.0, keysP90: 2.0, keysMax: 3.0, frontEdgePct: 1.5 };
const STOP = { scaleMedian: 0.20, scaleMax: 0.50, ratioPct: 1.5, solderMedian: 0.25, solderP90: 0.50, keysMedian: 1.5, keysOver3: 5, frontEdgePct: 2.5 };

function verdicts(s) {
  const e40 = s.scale.heldOut.fortyPinRowErrMm;
  const so = s.solder.heldOutMm;
  const k = s.keys.heldOutMm;
  const edge = Math.abs(s.keys.frontEdgeErrPct);
  const v = (ok, stop) => (stop ? 'STOP' : ok ? 'pass' : 'between pass and stop');
  return [
    v(e40.median <= PASS.scaleMedian && e40.max <= PASS.scaleMax, e40.median > STOP.scaleMedian || e40.max > STOP.scaleMax),
    v(true, 100 * Math.abs(s.scale.ratio - 1) > STOP.ratioPct),
    v(so.median <= PASS.solderMedian && so.p90 <= PASS.solderP90 && so.max <= PASS.solderMax && s.solder.holes >= 200, so.median > STOP.solderMedian || so.p90 > STOP.solderP90),
    v(k.median <= PASS.keysMedian && k.p90 <= PASS.keysP90 && k.max <= PASS.keysMax && s.keys.n >= 30, k.median > STOP.keysMedian || s.keys.over3mm > STOP.keysOver3),
    v(edge <= PASS.frontEdgePct, edge > STOP.frontEdgePct),
  ];
}

test('spike.json records the verdicts its figures give against the plan\'s thresholds', () => {
  assert.deepEqual(spike.verdicts.map((x) => x.verdict), verdicts(spike));
  assert.ok(spike.solder.holes >= 200, `only ${spike.solder.holes} holes matched`);
  assert.ok(spike.keys.n >= 30, `only ${spike.keys.n} keys`);
  assert.ok(spike.scale.footprints >= 12, `only ${spike.scale.footprints} footprints`);
});

// Task 0 crossed a STOP on 4 October 2026: O1's case front edge, on the
// key-plane registration, was 2.78 per cent short of 415 mm. The verdict stays
// recorded as measured. The plan was revised the same day, not the threshold:
// the edge is not in the key plane, so its raw width is recorded only, and task 8
// judges the width after a parallax correction against the same 1.5 and 2.5 per
// cent, judged on its interval with the camera's height measured independently
// of the 415 mm. The same revision made the solder side's largest error recorded,
// not a pass criterion, with an outlier rule fixed in advance (task 3).
test('spike.json passes the plan\'s rows as revised on 4 October 2026, and records the front edge raw', () => {
  const [scale, ratio, solder, keys, edge] = verdicts(spike);
  assert.equal(scale, 'pass', 'the scale, 40-pin rows held out');
  assert.notEqual(ratio, 'STOP', 'the scale, x against y');
  assert.equal(keys, 'pass', 'the keys, each held out');
  assert.notEqual(solder, 'STOP', 'the solder side');
  assert.ok(spike.solder.heldOutMm.median <= PASS.solderMedian && spike.solder.heldOutMm.p90 <= PASS.solderP90, 'the solder side\'s median and 90th percentile');
  // Recorded, not judged in task 0: a finite width, its error against 415 mm, and the verdict it got.
  assert.ok(Number.isFinite(spike.keys.frontEdgeMm) && spike.keys.frontEdgeMm > 300 && spike.keys.frontEdgeMm < 500);
  assert.ok(Math.abs(spike.keys.frontEdgeErrPct - (100 * (spike.keys.frontEdgeMm - 415)) / 415) < 0.001);
  assert.equal(spike.verdicts[4].verdict, edge, 'the front edge\'s recorded verdict is the one its figure gives');
  assert.match(spike.revision, /revised/);
  assert.match(spike.revision, /2026-10-04-bbc-micro-models\.md/);
  const plan = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'superpowers', 'plans', '2026-10-04-bbc-micro-models.md'), 'utf8');
  assert.match(plan, /O1's case front edge, raw, on the key-plane registration \(task 0\) \| recorded, not a stop/);
  assert.match(plan, /after correcting for parallax[^\n]*independent of the 415 mm[^\n]*\| the whole interval within 1\.5 per cent of 415 mm \| the whole interval outside 2\.5 per cent of 415 mm\. Otherwise inconclusive, which counts as not passed/);
  assert.match(plan, /the max is recorded, not a pass criterion/);
  // The key fit alone cannot give the camera's height: its jackknife error is recorded, and is large.
  assert.ok(spike.keys.parallax.jackknife.hMmSe > 0.25 * spike.keys.parallax.hMm);
});

test('the build and the tests never run the BBC model tools', () => {
  const pkg = fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8');
  assert.doesNotMatch(pkg, /bbc-micro-model/);
  assert.doesNotMatch(pkg, /python/);
});

// Task 2: the board's frame on the bare scan I1 (tools/bbc-micro-model/board_frame.py).
const frame = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'frame.json'), 'utf8'));
const FRAME_PASS = { yMedian: 0.10, yMax: 0.25, xMedian: 0.10, xMax: 0.25 };
const FRAME_STOP = { yMedian: 0.20, yMax: 0.50, ratioPct: 1.5, xMedian: 0.20, xMax: 0.50 };

// The x row as revised on 4 October 2026 by the controller, after task 2's
// figures were seen: rows across are scored only if at least 30 mm long (length
// alone), the median is judged against the same numbers, the largest is
// recorded. The verdict of the check as first written is kept, recorded only.
const X_SCORED_MIN_MM = 30;

function frameVerdicts(f) {
  const y = f.heldOut.rowLengthErrMm;
  const x = f.heldOutX.scaledErrMm;
  const xw = f.heldOutX.asWrittenScaledErrMm;
  const v = (ok, stop) => (stop ? 'STOP' : ok ? 'pass' : 'between pass and stop');
  return [
    v(y.median <= FRAME_PASS.yMedian && y.max <= FRAME_PASS.yMax, y.median > FRAME_STOP.yMedian || y.max > FRAME_STOP.yMax),
    v(true, f.ratioPct > FRAME_STOP.ratioPct),
    v(x.median <= FRAME_PASS.xMedian, x.median > FRAME_STOP.xMedian),
    v(xw.median <= FRAME_PASS.xMedian && xw.max <= FRAME_PASS.xMax, xw.median > FRAME_STOP.xMedian || xw.max > FRAME_STOP.xMax),
  ];
}

test('frame.json records the verdicts its figures give against the plan\'s scale thresholds, as written and as revised', () => {
  assert.deepEqual(frame.verdicts.map((x) => x.verdict), frameVerdicts(frame));
  assert.deepEqual(frame.verdicts.map((x) => x.judged), [true, true, true, false]);
  assert.ok(Math.abs(frame.ratio - frame.pxPerMm.x / frame.pxPerMm.y) < 1e-3);
  assert.ok(Math.abs(frame.ratioPct - 100 * Math.abs(frame.ratio - 1)) < 1e-2);
  // each row across is scaled to a 48.26 mm row; it is scored only if 30 mm or longer
  for (const r of frame.heldOutX.rows) {
    assert.ok(Math.abs(r.scaledErrMm - (r.errMm * 48.26) / (r.pitches * 2.54)) < 2e-3, r.row);
    assert.equal(r.scored, r.pitches * 2.54 >= X_SCORED_MIN_MM, r.row);
  }
  const scored = frame.heldOutX.rows.filter((r) => r.scored).map((r) => Math.abs(r.scaledErrMm));
  const all = frame.heldOutX.rows.map((r) => Math.abs(r.scaledErrMm));
  assert.equal(frame.heldOutX.scaledErrMm.n, scored.length);
  assert.ok(Math.abs(frame.heldOutX.scaledErrMm.max - Math.max(...scored)) < 1e-3);
  assert.ok(Math.abs(frame.heldOutX.asWrittenScaledErrMm.max - Math.max(...all)) < 1e-3);
  assert.equal(frame.heldOutX.asWrittenScaledErrMm.n, all.length);
  const largest = frame.heldOutX.rows.find((r) => r.row === frame.heldOutX.largest.row);
  assert.ok(largest && largest.scored && Math.abs(Math.abs(largest.scaledErrMm) - frame.heldOutX.scaledErrMm.max) < 1e-3, 'the largest is named');
  assert.match(frame.revision, /revised/);
  assert.match(frame.revision, /2026-10-04-bbc-micro-models\.md/);
  assert.ok(scored.length >= 5, 'at least five rows across scored');
  assert.ok(frame.heldOut.rowLengthErrMm.n >= 5, 'at least five 40-pin rows held out');
  assert.ok(frame.heldOut.footprints >= 10, `only ${frame.heldOut.footprints} footprints had a row held out`);
  // straightness is recorded, not a pass criterion: the rows at or over 0.05 mm are named
  assert.deepEqual(frame.straightness.over, frame.straightness.rows.filter((r) => r.rmsMm >= 0.05).map((r) => r.row));
});

// The plan's scale rows as revised on 4 October 2026: nothing judged is a STOP,
// y passes, and x's median over the scored rows is judged (between pass and
// stop is recorded, as the solder side). The check as first written crossed its
// STOP and stays recorded in frame.json.
test('frame.json passes the plan\'s scale rows as revised, and records the x row as first written', () => {
  const [y, ratio, x, asWritten] = frameVerdicts(frame);
  assert.equal(y, 'pass', 'the y scale, 40-pin rows held out');
  assert.notEqual(ratio, 'STOP', 'x against y');
  assert.notEqual(x, 'STOP', 'the x scale, rows across at least 30 mm held out');
  assert.equal(frame.verdicts[3].verdict, asWritten);
  const plan = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'superpowers', 'plans', '2026-10-04-bbc-micro-models.md'), 'utf8');
  assert.match(plan, /Revised on 4 Oct 2026 by the controller after task 2's figures were seen/);
  assert.match(plan, /scored only if it is at least 30 mm long/);
});

test('frame.json\'s x median over the scored rows passes outright', { todo: 'between pass and stop on 4 October 2026: see docs/journal/2026-10-04-the-bbc-micro-models.md, task 2' }, () => {
  assert.ok(frame.heldOutX.scaledErrMm.median <= FRAME_PASS.xMedian);
});

test('frame.json\'s outline is closed, square to its frame, and the board\'s size', () => {
  const o = frame.outline;
  assert.ok(o.length >= 5);
  assert.deepEqual(o[0], o[o.length - 1], 'the outline is closed');
  assert.deepEqual(o[0], [0, 0], 'it starts at the left rear corner, the origin');
  // Every edge runs across or down: the long ones square to the frame within
  // half a degree, the short walls of the front edge's slots within 15 degrees
  // (their ends are rounded, and a 5 mm wall fits a slant).
  for (let i = 1; i < o.length; i += 1) {
    const dx = Math.abs(o[i][0] - o[i - 1][0]);
    const dy = Math.abs(o[i][1] - o[i - 1][1]);
    const long = Math.max(dx, dy);
    const slant = (Math.atan2(Math.min(dx, dy), long) * 180) / Math.PI;
    assert.ok(slant < (long > 20 ? 0.5 : 15), `edge ${i} is ${slant.toFixed(2)} degrees off the frame`);
  }
  const xs = o.map((p) => p[0]);
  const ys = o.map((p) => p[1]);
  const w = Math.max(...xs) - Math.min(...xs);
  const d = Math.max(...ys) - Math.min(...ys);
  assert.ok(Math.abs(w - frame.board.widthMm) <= 0.5, `outline width ${w} against ${frame.board.widthMm}`);
  assert.ok(Math.abs(d - frame.board.depthMm) <= 0.5, `outline depth ${d} against ${frame.board.depthMm}`);
  // a sanity bound on the researcher's 309 by 229 mm, not a measurement
  assert.ok(Math.abs(frame.board.widthMm - 309) <= 5 && Math.abs(frame.board.depthMm - 229) <= 5);
});

test('frame.json\'s holes are inside the board, and its rectified copy is never committed', () => {
  assert.ok(frame.holes.length >= 4);
  for (const h of frame.holes) {
    assert.ok(h.x > 0 && h.x < frame.board.widthMm && h.y > 0 && h.y < frame.board.depthMm, JSON.stringify(h));
    assert.ok(h.d > 1.5 && h.d < 6, JSON.stringify(h));
  }
  assert.equal(frame.rectified.pxPerMm, 16);
  assert.ok(frame.rectified.file.startsWith('out/'));
  const ignore = fs.readFileSync(path.join(REPO_ROOT, '.gitignore'), 'utf8');
  assert.match(ignore, /^tools\/bbc-micro-model\/out\/$/m);
});

// Task 3: the solder side registered to the component side, and the pads,
// drills and footprints (tools/bbc-micro-model/board_register.py).
const registration = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'registration.json'), 'utf8'));
const SOLDER_PASS = { median: 0.15, p90: 0.30 };
const SOLDER_STOP = { median: 0.25, p90: 0.50 };
// The plan's outlier rule, fixed on 4 October 2026 before task 3.
const ROUND_MAX_AXIS = 1.25;
const ROUND_AREA = [0.6, 1.6];

// numpy's default percentile (linear between the closest ranks)
function percentile(values, q) {
  const v = [...values].sort((a, b) => a - b);
  const r = (q / 100) * (v.length - 1);
  const lo = Math.floor(r);
  const hi = Math.ceil(r);
  return v[lo] + (v[hi] - v[lo]) * (r - lo);
}
const summary = (v) => ({ median: percentile(v, 50), p90: percentile(v, 90), max: Math.max(...v) });

test('registration.json passes the plan\'s solder row on the figures without exclusion, and records the largest', () => {
  const s = registration.solder;
  assert.ok(s.holes >= 200, `only ${s.holes} holes matched`);
  assert.ok(['affine', 'cubic'].includes(s.model));
  assert.deepEqual(s.heldOutMm, s.fits[s.model].heldOutMm, 'the figures are the chosen model\'s');
  // the model with the lower held-out median; within 0.005 mm the affine
  const want = s.fits.cubic.heldOutMm.median < s.fits.affine.heldOutMm.median - 0.005 ? 'cubic' : 'affine';
  assert.equal(s.model, want);
  const h = s.heldOutMm;
  const verdict = h.median > SOLDER_STOP.median || h.p90 > SOLDER_STOP.p90 ? 'STOP' : h.median <= SOLDER_PASS.median && h.p90 <= SOLDER_PASS.p90 && s.holes >= 200 ? 'pass' : 'between pass and stop';
  assert.equal(s.verdict, verdict, 'the recorded verdict is the one the figures give');
  assert.equal(verdict, 'pass');
  // the largest is recorded, not judged
  assert.ok(Number.isFinite(h.max) && h.max >= h.p90);
  // the figures are the matched holes' own
  const rows = s.matched.rows;
  assert.equal(rows.length, s.holes);
  const col = s.matched.columns.indexOf('heldOutMm');
  const got = summary(rows.map((r) => r[col]));
  for (const k of ['median', 'p90', 'max']) assert.ok(Math.abs(got[k] - h[k]) < 1e-3, `${k}: ${got[k]} against ${h[k]}`);
});

test('the excluded holes are exactly those the roundness rule names, counted, with both sets of figures', () => {
  const s = registration.solder;
  const o = s.outliers;
  assert.match(o.rule, /1\.25/);
  assert.match(o.rule, /0\.6 to 1\.6/);
  assert.match(o.rule, /no residual/);
  const c = Object.fromEntries(s.matched.columns.map((k, i) => [k, i]));
  const rows = s.matched.rows;
  const median = (v) => percentile(v, 50);
  const med = { top: median(rows.map((r) => r[c.topArea])), bottom: median(rows.map((r) => r[c.bottomArea])) };
  const near = (x, lim) => Math.abs(x - lim) < 2e-4; // a value rounded to 4 places on the limit: either way
  const named = [];
  rows.forEach((r, i) => {
    const fails = [];
    let unsure = false;
    for (const [face, axis, area] of [['top', r[c.topAxis], r[c.topArea]], ['bottom', r[c.bottomAxis], r[c.bottomArea]]]) {
      if (near(axis, ROUND_MAX_AXIS)) unsure = true;
      if (axis > ROUND_MAX_AXIS) fails.push(face);
      const ratio = area / med[face];
      if (ratio < ROUND_AREA[0] || ratio > ROUND_AREA[1]) fails.push(face);
    }
    if (!unsure && fails.length) named.push(i);
    if (unsure) named.push(o.excluded.some((e) => e.hole === i) ? i : null);
  });
  assert.deepEqual(o.excluded.map((e) => e.hole), named.filter((i) => i !== null), 'the excluded holes are the ones the rule names');
  assert.equal(o.count, o.excluded.length);
  for (const e of o.excluded) assert.ok(e.why.length > 0 && e.why.every((w) => /^(top|bottom): (axis ratio \d\.\d\d over 1\.25|area \d+\.\d\d times)/.test(w)), JSON.stringify(e));
  // both sets of figures, each the matched holes' own
  const kept = rows.filter((_, i) => !o.excluded.some((e) => e.hole === i)).map((r) => r[c.heldOutMm]);
  const w = summary(kept);
  for (const k of ['median', 'p90', 'max']) {
    assert.ok(Number.isFinite(s.heldOutMm[k]));
    assert.ok(Math.abs(w[k] - o.heldOutMmWithExclusion[k]) < 1e-3, `with exclusion ${k}: ${w[k]} against ${o.heldOutMmWithExclusion[k]}`);
  }
});

// The plan's task 3 step 4, as revised after task 3's review (5 October 2026).
// Each drill's pad is placed at the drill, so "a pad within 0.2 mm" held by
// construction; what can fail is where each face put the hole.
const FACE_MM = 0.2;
const FILLED_OFF_SHARE_MAX = 0.05;

test('every drill in registration.json records both faces\' centres, sits at their mean, and has its faces within 0.2 mm as revised', () => {
  const { drills, pads, drillFaces } = registration;
  assert.ok(drills.length >= 200);
  const byDrill = new Map();
  for (const p of pads) if (p.drill !== null) byDrill.set(p.drill, [...(byDrill.get(p.drill) ?? []), p]);
  // registration.json rounds to 3 places, so a face this near the limit is either side of it
  const near = (v) => Math.abs(v - FACE_MM) <= 0.002;
  const off = (d) => Math.max(Math.hypot(d.top[0] - d.x, d.top[1] - d.y), Math.hypot(d.bottom[0] - d.x, d.bottom[1] - d.y));
  const count = { open: 0, filled: 0, filledOver: 0, filledNear: 0 };
  drills.forEach((d, i) => {
    assert.ok(Array.isArray(d.top) && Array.isArray(d.bottom), `drill ${i}: both faces' centres are recorded`);
    assert.ok(Math.abs((d.top[0] + d.bottom[0]) / 2 - d.x) <= 0.0011 && Math.abs((d.top[1] + d.bottom[1]) / 2 - d.y) <= 0.0011, `drill ${i} is not the mean of its faces`);
    assert.ok(Math.abs(Math.hypot(d.top[0] - d.bottom[0], d.top[1] - d.bottom[1]) - d.spreadMm) <= 0.003, `drill ${i}: spreadMm is not the faces' distance`);
    assert.ok(d.spreadMm <= 1.0, `drill ${i}: faces ${d.spreadMm} mm apart, over the 1.0 mm match gate`);
    assert.ok(d.d === null ? d.filled === true : d.d > 0.2 && d.d < 2, `drill ${i}: diameter ${d.d}`);
    // its pad, at the drill, on both faces (a check of the file's shape: it holds by construction)
    assert.ok((byDrill.get(i) ?? []).some((p) => p.face === 'both'), `drill ${i} has no pad`);
    const o = off(d);
    if (d.filled) {
      count.filled += 1;
      if (near(o)) count.filledNear += 1;
      else if (o > FACE_MM) count.filledOver += 1;
    } else {
      count.open += 1;
      assert.ok(o <= FACE_MM || near(o), `open drill ${i} at ${d.x}, ${d.y}: a face ${o.toFixed(3)} mm off`);
    }
  });
  assert.ok(count.open > 0 && count.filled > 0, JSON.stringify(count));
  // filled with solder, the solder may sit off the hole: the share further off is bounded, even with every near one counted over
  assert.ok((count.filledOver + count.filledNear) / count.filled <= FILLED_OFF_SHARE_MAX, JSON.stringify(count));
  // the file's report says the same
  assert.equal(drillFaces.limitMm, FACE_MM);
  assert.equal(drillFaces.filledShareMax, FILLED_OFF_SHARE_MAX);
  assert.equal(drillFaces.open.n, count.open);
  assert.equal(drillFaces.open.over, 0);
  assert.equal(drillFaces.filled.n, count.filled);
  assert.ok(drillFaces.filled.over >= count.filledOver && drillFaces.filled.over <= count.filledOver + count.filledNear, `${drillFaces.filled.over} filled drills over, against ${JSON.stringify(count)}`);
  assert.ok(Math.abs(drillFaces.filled.share - drillFaces.filled.over / drillFaces.filled.n) < 1e-3);
  for (const p of pads) {
    assert.ok(['round', 'square', 'oval'].includes(p.shape) && ['top', 'bottom', 'both'].includes(p.face));
    assert.ok(p.x > -1 && p.x < frame.board.widthMm + 1 && p.y > -1 && p.y < frame.board.depthMm + 1, JSON.stringify(p));
  }
});

test('registration.json\'s held-out figures by kind of hole are the matched holes\' own', () => {
  // This catches figures that disagree with the rows they were computed from. It does not
  // catch a hole given the wrong `kind`: that classification is the tool's, tested there.
  const s = registration.solder;
  const c = Object.fromEntries(s.matched.columns.map((k, i) => [k, i]));
  for (const [name, kind] of [['solder', 'blob'], ['ring', 'ring']]) {
    const v = s.matched.rows.filter((r) => r[c.kind] === kind).map((r) => r[c.heldOutMm]);
    const want = s.heldOutByKind[name];
    assert.equal(want.n, v.length, name);
    const got = summary(v);
    for (const k of ['median', 'p90', 'max']) assert.ok(Math.abs(got[k] - want[k]) < 1e-3, `${name} ${k}: ${got[k]} against ${want[k]}`);
  }
  for (const r of s.matched.rows) assert.ok(['blob', 'ring', 'mixed'].includes(r[c.kind]));
});

test('registration.json keeps the solder side\'s map: its model, parameters and the flip it was fitted after', () => {
  const s = registration.solder;
  assert.ok(['affine', 'cubic'].includes(s.model), `model ${s.model}`);
  assert.ok(s.params && typeof s.params === 'object' && Object.keys(s.params).length > 0, 'solder.params');
  if (s.model === 'cubic') assert.ok(Array.isArray(s.params.H) && s.params.H.length === 3 && s.params.H.every((r) => r.length === 3), 'the cubic\'s homography');
  const t = s.transform;
  assert.ok(t && t.source === 'I2' && Number.isInteger(t.widthPx) && t.widthPx > 0 && Number.isInteger(t.heightPx) && t.heightPx > 0, 'solder.transform');
  assert.ok(typeof t.mirror === 'string' && t.mirror.length > 0);
  // the parameters are unrounded (a map rounded to 4 places puts a drill 0.017 mm off)
  const flat = JSON.stringify(s.params).match(/-?\d+\.\d+/g);
  assert.ok(flat.some((n) => n.replace(/^-?\d+\./, '').length > 8), 'params are written unrounded');
});

test('every DIP footprint in registration.json has an even pin count from 14 to 40, a pad per pin and pin 1 first', () => {
  const { footprints, pads } = registration;
  const dips = footprints.filter((f) => f.kind === 'dip');
  assert.ok(dips.length >= 40, `only ${dips.length} DIPs`);
  for (const f of footprints) assert.ok(['dip', 'sip', 'axial', 'radial', 'connector', 'other'].includes(f.kind));
  for (const f of dips) {
    assert.ok(f.pins % 2 === 0 && f.pins >= 14 && f.pins <= 40, `${f.ref}: ${f.pins} pins`);
    assert.equal(f.pads.length, f.pins);
    assert.equal(new Set(f.pads).size, f.pins, 'a pad is one pin');
    const p1 = pads[f.pads[0]];
    assert.ok(Math.hypot(p1.x - f.pin1[0], p1.y - f.pin1[1]) < 1e-3, `${f.ref}: pin 1 is its first pad`);
    assert.ok(f.pin1From === 'print' || f.pin1From === 'square pad' || f.pin1From === 'marked by hand', `${f.ref}: pin 1 from ${f.pin1From}`);
    // pin N/2 is (N/2 - 1) x 2.54 mm along the row from pin 1, and pin N faces pin 1
    const half = pads[f.pads[f.pins / 2 - 1]];
    const last = pads[f.pads[f.pins - 1]];
    assert.ok(Math.abs(Math.hypot(half.x - p1.x, half.y - p1.y) - (f.pins / 2 - 1) * 2.54) < 0.6, `${f.ref}: row length`);
    assert.ok(Math.abs(Math.hypot(last.x - p1.x, last.y - p1.y) - f.rowSpacingMm) < 0.6, `${f.ref}: row spacing`);
    for (const i of f.pads) {
      const p = pads[i];
      assert.ok(p.x >= f.box[0] && p.x <= f.box[2] && p.y >= f.box[1] && p.y <= f.box[3], `${f.ref}: a pad outside its box`);
    }
  }
});

test('IC1 to IC7, IC51, IC52, IC69 and IC78 each have a DIP footprint, its reference marked by hand with a reason', () => {
  const marks = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'marks.json'), 'utf8')).footprints.marks;
  for (const ref of ['IC1', 'IC2', 'IC3', 'IC4', 'IC5', 'IC6', 'IC7', 'IC51', 'IC52', 'IC69', 'IC78']) {
    const f = registration.footprints.filter((x) => x.ref === ref);
    assert.equal(f.length, 1, `${ref}: ${f.length} footprints`);
    assert.equal(f[0].kind, 'dip', ref);
    const m = marks.find((x) => x.ref === ref);
    assert.ok(m && m.reason.length > 20 && m.crop && m.crop.box.length === 4, `${ref}: its mark, crop and reason`);
  }
  // every reference in the footprints was marked by hand, once
  const refs = registration.footprints.map((f) => f.ref).filter(Boolean);
  assert.equal(new Set(refs).size, refs.length, 'a reference twice');
  for (const r of refs) assert.ok(marks.some((m) => m.ref === r), `${r} has no mark`);
});

test('every DIP footprint has a reference read from the print, or says it is unread and why', () => {
  // the plan's task 3 step 4, as revised after task 3's review (5 October 2026)
  const marks = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'marks.json'), 'utf8')).footprints.marks;
  const dips = registration.footprints.filter((f) => f.kind === 'dip');
  for (const f of dips) {
    const where = `the ${f.pins}-pin DIP at ${f.box.map((v) => v.toFixed(1)).join(', ')}`;
    if (f.ref) {
      assert.ok(!f.unread, `${f.ref}: read and unread`);
      const m = marks.find((x) => x.ref === f.ref);
      assert.ok(m && m.reason.length > 20 && m.crop && m.crop.box.length === 4, `${f.ref}: its mark, crop and reason`);
    } else {
      assert.ok(typeof f.unread === 'string' && f.unread.length > 20, `${where} has neither a reference nor an unread reason`);
    }
  }
  const c = registration.footprintCounts;
  assert.equal(c.dipRefsRead, dips.filter((f) => f.ref).length);
  assert.equal(c.dipRefsUnread, dips.filter((f) => !f.ref && f.unread).length);
  assert.equal(c.dipsWithNeither, 0);
  // no mark went unused, and none disagreed with its footprint
  for (const m of registration.marks) {
    assert.equal(m.found, true, `the mark for ${m.ref} found no footprint`);
    for (const k of ['pins', 'pin1']) assert.ok(typeof m[k] !== 'string', `${m.ref}: ${m[k]}`);
  }
});

test('registration.json fits each mounting hole on its top rim and records the move from task 2\'s centre', () => {
  const holes = registration.holes.filter((h) => 'x' in h);
  assert.equal(holes.length, frame.holes.length, 'every hole of frame.json refitted');
  for (const h of holes) {
    assert.ok(Math.abs(h.shiftMm[0] - (h.x - h.task2.x)) < 1e-3 && Math.abs(h.shiftMm[1] - (h.y - h.task2.y)) < 1e-3);
    assert.ok(h.d >= h.task2.d - 0.05, 'the top rim is outside the lid seen through the hole');
    assert.ok(h.d > 2.5 && h.d < 5);
  }
  const shifts = holes.map((h) => h.shiftLenMm);
  assert.ok(Math.abs(registration.holeShiftMm.max - Math.max(...shifts)) < 1e-3);
});

// Task 4: the copper on both faces and the print, traced from the bare scans
// (tools/bbc-micro-model/board_trace.py), the plan's checks on them, and the track map.
const copper = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'copper.json'), 'utf8'));
const icTable = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'ic-table.json'), 'utf8'));
const BBC_MAP = path.join(process.cwd(), 'src', 'assets', 'tracks', 'bbc-micro-board.webp');
const BBC_MAP_BUDGET = 600_000;
const PLAN = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'superpowers', 'plans', '2026-10-04-bbc-micro-models.md'), 'utf8');
// The plan's table and task 4's note "Fixed on 13 October 2026", set before the checks were run
const COPPER_ROWS = { coverage: [0.10, 0.50], drills: 0.95, chips: 20, share: 0.90, i5Pass: 1.0, i5Stop: 2.0, iou: 0.50 };

function copperVerdicts(c) {
  const ok = (b) => (b ? 'pass' : 'fail');
  const n = c.nets;
  const med = c.againstI5.registration.heldOutMm.median;
  return {
    coverage: ok(['top', 'bottom'].every((f) => c.coverage[f] >= COPPER_ROWS.coverage[0] && c.coverage[f] <= COPPER_ROWS.coverage[1])),
    drillsInCopper: ok(['top', 'bottom'].every((f) => c.drillsInCopper[f] >= COPPER_ROWS.drills)),
    nets: ok(n.chips >= COPPER_ROWS.chips && !n.touching
      && ['gnd', 'vcc'].every((k) => n[k].pins > 0 && n[k].inLargest >= COPPER_ROWS.share * n[k].pins - 1e-9)),
    i5Registration: med <= COPPER_ROWS.i5Pass ? 'pass' : (med > COPPER_ROWS.i5Stop ? 'STOP' : 'between pass and stop'),
    againstI5: ok(c.againstI5.iou >= COPPER_ROWS.iou),
  };
}

test('the BBC Micro board\'s track map is committed, WebP, the size copper.json gives, inside its budget, its three channels apart', async () => {
  const sharp = (await import('sharp')).default;
  assert.ok(fs.existsSync(BBC_MAP), 'src/assets/tracks/bbc-micro-board.webp is missing');
  const bytes = fs.readFileSync(BBC_MAP);
  assert.ok(bytes.length <= BBC_MAP_BUDGET, `the track map is ${bytes.length} bytes, over its ${BBC_MAP_BUDGET} budget`);
  assert.equal(bytes.length, copper.map.bytes);
  assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), copper.map.sha256, 'copper.json\'s map.sha256 is not the committed map\'s');
  assert.equal((await sharp(bytes).metadata()).format, 'webp');
  // the highest of 8, 7 or 6 px/mm inside the budget, 8 as the plan's task 4 note records it was chosen before the run
  assert.equal(copper.mapPxPerMm, 8);
  const { data, info } = await sharp(bytes).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  assert.deepEqual([info.width, info.height], [copper.map.width, copper.map.height]);
  assert.equal(info.width, Math.round((copper.grid.width / copper.grid.pxPerMm) * copper.mapPxPerMm));
  assert.equal(info.height, Math.round((copper.grid.height / copper.grid.pxPerMm) * copper.mapPxPerMm));
  // edge to edge: the grid covers the outline's box, to within one of its pixels
  const xs = frame.outline.map((p) => p[0]);
  const ys = frame.outline.map((p) => p[1]);
  const px = 1 / copper.grid.pxPerMm;
  assert.ok(Math.abs(copper.grid.originMm[0] - Math.min(...xs)) < 1e-3 && Math.abs(copper.grid.originMm[1] - Math.min(...ys)) < 1e-3);
  assert.ok(Math.abs(copper.grid.width * px - (Math.max(...xs) - Math.min(...xs))) <= px, 'the grid is the outline\'s width');
  assert.ok(Math.abs(copper.grid.height * px - (Math.max(...ys) - Math.min(...ys))) <= px, 'the grid is the outline\'s depth');
  assert.deepEqual(copper.map.originMm, copper.grid.originMm);
  const on = [0, 0, 0];
  let between = 0;
  for (let i = 0; i < data.length; i += info.channels) {
    for (let k = 0; k < 3; k += 1) {
      if (data[i + k] > 127) on[k] += 1;
      if (data[i + k] > 8 && data[i + k] < 247) between += 1;
    }
  }
  const n = info.width * info.height;
  const [red, green, blue] = on.map((v) => v / n);
  assert.ok(red >= 0.10 && red <= 0.50, `red (the component side) covers ${(red * 100).toFixed(1)}%`);
  assert.ok(green >= 0.10 && green <= 0.50, `green (the solder side) covers ${(green * 100).toFixed(1)}%`);
  assert.ok(blue < 0.15, `blue (the print) covers ${(blue * 100).toFixed(1)}%`);
  assert.ok(between / (3 * n) < 0.001, 'the channels are not 255 or 0');
});

test('copper.json records the verdicts its figures give against the plan\'s rows, and its figures are its own', () => {
  assert.deepEqual(copper.verdicts, copperVerdicts(copper));
  for (const f of ['top', 'bottom', 'print']) assert.ok(copper.coverage[f] > 0 && copper.coverage[f] < 1, f);
  assert.equal(copper.drills.n, registration.drills.length, 'every drill task 3 made is checked');
  for (const f of ['top', 'bottom']) {
    const share = 1 - copper.drills.notInCopper[f].length / copper.drills.n;
    assert.ok(Math.abs(share - copper.drillsInCopper[f]) < 1e-3, `${f}: the share is the drills' own`);
  }
  // every IC of table 8.1 is either in the nets check or left out with a reason
  const refs = icTable.ics.map((i) => i.ref);
  assert.deepEqual([...copper.netsChips, ...copper.nets.excluded.map(([r]) => r)].sort(), [...refs].sort());
  for (const [ref, why] of copper.nets.excluded) assert.ok(typeof why === 'string' && why.length > 10, `${ref}: why it is left out`);
  assert.equal(copper.nets.chips, copper.netsChips.length);
  for (const k of ['gnd', 'vcc']) {
    assert.equal(copper.nets[k].pins, copper.netsChips.length, `${k}: one pin a chip`);
    assert.equal(copper.pinNets[k].length, copper.nets[k].pins);
    const ids = copper.pinNets[k].map(([, net]) => net).filter((x) => x !== null);
    const most = Math.max(0, ...ids.map((x) => ids.filter((y) => y === x).length));
    assert.equal(copper.nets[k].inLargest, most, `${k}: the largest net is the pins' own`);
  }
  const gnd = new Set(copper.pinNets.gnd.map(([, x]) => x).filter((x) => x !== null));
  assert.equal(copper.nets.touching, copper.pinNets.vcc.some(([, x]) => x !== null && gnd.has(x)), 'touching is the pins\' own');
  // I5's registration is on the pins marked by hand, every chip held out in turn
  const marks = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'marks.json'), 'utf8')).i5Pins.marks;
  const reg = copper.againstI5.registration;
  assert.equal(reg.points, marks.length);
  assert.equal(reg.chips, new Set(marks.map((m) => m.ref)).size);
  assert.equal(reg.folds, 'each chip held out in turn');
  const fps = new Map(registration.footprints.filter((f) => f.ref).map((f) => [f.ref, f]));
  for (const m of marks) {
    const f = fps.get(m.ref);
    assert.ok(f && f.kind === 'dip', `${m.ref}: an I5 mark on a chip task 3 found`);
    assert.ok(Number.isInteger(m.pin) && m.pin >= 1 && m.pin <= f.pins, `${m.ref} pin ${m.pin}`);
    assert.ok(m.crop && m.crop.box.length === 4, `${m.ref} pin ${m.pin}: its crop`);
  }
  assert.match(copper.thresholds.nets, /at least 20 ICs/);
});

test('copper.json passes the coverage, drills and I5 registration rows, and its nets and I5 rows stay the FAILs the plan records', () => {
  assert.equal(copper.verdicts.coverage, 'pass');
  assert.equal(copper.verdicts.drillsInCopper, 'pass');
  assert.equal(copper.verdicts.i5Registration, 'pass');
  // The plan's task 4 note, "Outcome, 13 October 2026": the nets check and the comparison with I5 failed on the one
  // run of the fixed method, and the map ships traced to look at. A run that changes either verdict must change that
  // note, the journal and every sentence that describes the copper, so both are pinned here with their figures.
  assert.equal(copper.verdicts.nets, 'fail');
  assert.deepEqual([copper.nets.chips, copper.nets.gnd, copper.nets.vcc, copper.nets.touching],
    [51, { pins: 51, inLargest: 39 }, { pins: 51, inLargest: 30 }, true]);
  assert.equal(copper.verdicts.againstI5, 'fail');
  assert.equal(copper.againstI5.iou.toFixed(3), '0.411');
  const outcome = PLAN.split('\n').find((l) => l.startsWith('- **Outcome, 13 October 2026 (the one run')) ?? '';
  assert.match(outcome, /\*\*The known nets FAILED\*\* \(51 ICs: GND 39 of 51 pins in the largest net, \+5V 30 of 51, and a GND pin and a \+5V pin in one net\)/);
  assert.match(outcome, /\*\*The component side against I5 FAILED\*\* \(intersection over union 0\.411 /);
  assert.match(outcome, /ships traced to look at, its connections not verified/);
});

test('ic-table.json is table 8.1, and gives each IC of the nets check its GND and +5V pins from a data sheet', () => {
  assert.equal(icTable.source, 'D1');
  assert.deepEqual(icTable.ics.map((i) => i.ref), Array.from({ length: 101 }, (_, k) => `IC${k + 1}`));
  // a few of table 8.1's rows, read again from the page images (PDF pages 66 and 67)
  const part = Object.fromEntries(icTable.ics.map((i) => [i.ref, i.part]));
  assert.deepEqual(['IC1', 'IC2', 'IC3', 'IC6', 'IC7', 'IC31', 'IC40', 'IC45', 'IC51', 'IC69', 'IC78', 'IC89'].map((r) => part[r]),
    ['6502A', '6845', '6522', '5C094', '2C199', '74LS34', '74S00', '74S139', '(27128)', '6522', '8271', '68B54']);
  for (const ic of icTable.ics) {
    assert.ok([66, 67].includes(ic.page) && ic.manualXY.length === 2, ic.ref);
    if ('excludedWhy' in ic) {
      assert.ok(!('gnd' in ic) && ic.excludedWhy.length > 10, `${ic.ref}: left out, with why`);
      continue;
    }
    for (const k of ['pins', 'package', 'gnd', 'vcc', 'pinoutSource']) assert.ok(k in ic, `${ic.ref}: ${k}`);
    assert.ok(Number.isInteger(ic.gnd) && Number.isInteger(ic.vcc) && ic.gnd !== ic.vcc, ic.ref);
    assert.ok(ic.gnd >= 1 && ic.gnd <= ic.pins && ic.vcc >= 1 && ic.vcc <= ic.pins, ic.ref);
    assert.equal(ic.package, `DIP-${ic.pins}`, ic.ref);
    assert.match(ic.pinoutSource, /https?:\/\/\S+\.pdf/, `${ic.ref}: its data sheet is named with its address`);
    // the data sheet is an input in sources.json with its SHA-256, at the address the pinout names
    const sheet = sources.find((x) => x.id === ic.pinoutSourceId);
    assert.ok(sheet && sheet.kind === 'document' && sheet.committed === null, `${ic.ref}: ${ic.pinoutSourceId} is not a data sheet in sources.json`);
    assert.ok(ic.pinoutSource.includes(`(${sheet.url})`), `${ic.ref}: the pinout's address is not ${sheet.id}'s`);
    assert.ok(sheet.use.includes(ic.ref), `${sheet.id}'s use does not name ${ic.ref}`);
    assert.match(ic.pinoutSource, new RegExp(`pin ${ic.gnd} (GND|VSS)`), `${ic.ref}: the data sheet's GND pin`);
    assert.match(ic.pinoutSource, new RegExp(`pin ${ic.vcc} (VCC|VDD)`), `${ic.ref}: the data sheet's +5V pin`);
    // a 74-series or 4000-series part on a corner pinout; the 6502 and the 6522s from Rockwell's sheets
    if (/^74|^7438$|^40\d\d$/.test(ic.part)) assert.deepEqual([ic.gnd, ic.vcc], [ic.pins / 2, ic.pins], ic.ref);
  }
  assert.equal(icTable.ics.filter((i) => 'gnd' in i).length, copper.netsChips.length);
  assert.deepEqual(['IC1', 'IC3', 'IC69'].map((r) => { const i = icTable.ics.find((x) => x.ref === r); return [i.gnd, i.vcc]; }), [[1, 8], [1, 20], [1, 20]]);
});

test('copper.json was written by the method fixed before its checks ran, with the hashes the journal records', () => {
  const journal = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'journal', '2026-10-04-the-bbc-micro-models.md'), 'utf8');
  const block = journal.split('### The method fixed, and the checks as fixed')[1]?.split(/\n#{2,3} /)[0] ?? '';
  const recorded = Object.fromEntries([...block.matchAll(/^([0-9a-f]{64}) (\S+)$/gm)].map(([, sha, f]) => [f, sha]));
  assert.ok(Object.keys(recorded).length >= 7, 'the journal records the method\'s hashes');
  assert.deepEqual(copper.methodFixed, recorded);
  assert.ok('tools/bbc-micro-model/board_trace.py' in recorded);
});

test('NOTICE.md and the photographs\' README name the BBC Micro board\'s track map, amb5l\'s scans, and that it is traced to look at', () => {
  const flat = (t) => t.replace(/\s+/g, ' ');
  const notice = fs.readFileSync(path.join(REPO_ROOT, 'NOTICE.md'), 'utf8');
  const section = flat(notice.split(/^## /m).find((s) => s.includes('bbc-micro-board.webp')) ?? '');
  assert.ok(section, 'NOTICE.md does not name site/src/assets/tracks/bbc-micro-board.webp');
  assert.match(section, /traced from amb5l's scans/);
  assert.match(section, /state no licence/);
  assert.match(section, /traced to look at: its connections are not verified/);
  assert.match(section, /stardot\.org\.uk\/forums\/viewtopic\.php\?t=23329/);
  const photos = fs.readFileSync(path.join(process.cwd(), 'src', 'assets', 'photos', 'README.md'), 'utf8');
  const ours = flat(photos.split(/^## /m).find((s) => s.includes('bbc-micro-board.webp')) ?? '');
  assert.match(ours, /traced from amb5l's scans/);
  assert.match(ours, /traced to look at: its connections are not verified/);
});

test('board_trace.py and common.py on disk are the method fixed before task 4\'s checks ran', () => {
  // The journal records the SHA-256 of the method's files, taken before the nets and I5 checks were first run.
  // Only the two scripts are pinned here: task 5 changes ic-table.json and registration.json on purpose. A change
  // to either script (a threshold such as NETS_MIN_SHARE or I5_IOU_MIN, say) means copper.json no longer comes from
  // the code in the repository, and needs a new check, fixed before it runs, and a journal entry.
  const journal = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'journal', '2026-10-04-the-bbc-micro-models.md'), 'utf8');
  const block = journal.split('### The method fixed, and the checks as fixed')[1]?.split(/\n#{2,3} /)[0] ?? '';
  const recorded = Object.fromEntries([...block.matchAll(/^([0-9a-f]{64}) (\S+)$/gm)].map(([, sha, f]) => [f, sha]));
  for (const f of ['tools/bbc-micro-model/board_trace.py', 'tools/bbc-micro-model/common.py']) {
    assert.ok(recorded[f], `the journal records no hash for ${f}`);
    const sha = crypto.createHash('sha256').update(fs.readFileSync(path.join(REPO_ROOT, f))).digest('hex');
    assert.equal(sha, recorded[f], `${f} is not the file the method was fixed with`);
  }
});

// --- Task 5: the parts, places and identities --------------------------------------------------

const parts = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'parts.json'), 'utf8'));
const marks = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'marks.json'), 'utf8'));
const PARTS_MJS = path.join(REPO_ROOT, 'site', 'src', 'models', 'bbc-micro-board-parts.mjs');
const JOURNAL = path.join(REPO_ROOT, 'docs', 'journal', '2026-10-04-the-bbc-micro-models.md');
const MODELS_MD = path.join(REPO_ROOT, 'docs', 'bbc-micro', 'facts', 'models.md');
// task 6's BbcChip, in its order, as the plan's task 5 note fixed the names
const BBC_CHIPS = ['crtc', 'acia', 'serialUla', 'videoUla', 'romLatch', 'systemVia', 'userVia', 'fdc8271', 'adc', 'sound'];
const byRef = (a, b) => Number(a.slice(2)) - Number(b.slice(2));

function task5Hashes() {
  const journal = fs.readFileSync(JOURNAL, 'utf8');
  const section = journal.split('## Task 5: the parts, places and identities')[1] ?? '';
  const block = section.split('### The method fixed')[1]?.split(/\n#{2,3} /)[0] ?? '';
  return Object.fromEntries([...block.matchAll(/^([0-9a-f]{64}) (\S+)$/gm)].map(([, sha, f]) => [f, sha]));
}

test('parts.json was written by the method fixed before task 5\'s checks ran, and board_parts.py on disk is that method', () => {
  const recorded = task5Hashes();
  assert.ok(Object.keys(recorded).length >= 7, 'the journal records task 5\'s method\'s hashes');
  assert.deepEqual(parts.methodFixed, recorded);
  // A change to board_parts.py (a threshold such as MANUAL_LIMIT_MM, or the identities' rule) means parts.json no longer
  // comes from the code in the repository, and needs a new check, fixed before it runs, and a journal entry.
  for (const f of ['tools/bbc-micro-model/board_parts.py', 'tools/bbc-micro-model/common.py']) {
    const sha = crypto.createHash('sha256').update(fs.readFileSync(path.join(REPO_ROOT, f))).digest('hex');
    assert.equal(sha, recorded[f], `${f} is not the file task 5's method was fixed with`);
  }
});

function footprintPads(ref) {
  const f = registration.footprints.find((x) => x.ref === ref && x.kind === 'dip');
  if (f) return f.pads.map((i) => [registration.pads[i].x, registration.pads[i].y]);
  const m = marks.parts.eightPin.marks.find((x) => x.ref === ref);
  if (!m) return null;
  // a pad task 3 did not find goes on its row's line, at its place in the pitch (board_parts.py's marked_footprint)
  const pts = m.pads.map((i) => (i === null ? null : [registration.pads[i].x, registration.pads[i].y]));
  const half = pts.length / 2;
  for (const row of [[...Array(half).keys()], [...Array(half).keys()].map((k) => k + half)]) {
    const known = row.map((i, k) => [k, pts[i]]).filter(([, p]) => p);
    for (const [k, i] of row.map((i, k) => [k, i])) {
      if (pts[i]) continue;
      const fit = (c) => { // least squares line through the row's known pads, by their place in the pitch
        const n = known.length, sx = known.reduce((s, [q]) => s + q, 0), sy = known.reduce((s, [, p]) => s + p[c], 0);
        const sxx = known.reduce((s, [q]) => s + q * q, 0), sxy = known.reduce((s, [q, p]) => s + q * p[c], 0);
        const b = (n * sxy - sx * sy) / (n * sxx - sx * sx);
        return (sy - b * sx) / n + b * k;
      };
      pts[i] = [fit(0), fit(1)];
    }
  }
  return pts;
}

test('every placed IC sits on its footprint: its pads\' centre, its turn from pin 1 to pin N/2, a pad for each pin', () => {
  const ics = parts.model.ics;
  assert.equal(ics.length, Object.keys(parts.places).length);
  for (const ic of ics) {
    const pads = footprintPads(ic.ref);
    assert.ok(pads, `${ic.ref} has no footprint`);
    assert.equal(pads.length, ic.pins, `${ic.ref}: its footprint has ${pads.length} pads, its part ${ic.pins} pins`);
    assert.equal(ic.pins, icTable.ics.find((x) => x.ref === ic.ref).pins, ic.ref);
    const cx = pads.reduce((s, p) => s + p[0], 0) / pads.length, cy = pads.reduce((s, p) => s + p[1], 0) / pads.length;
    assert.ok(Math.abs(cx - ic.x) < 0.002 && Math.abs(cy - ic.y) < 0.002, `${ic.ref} is not at its pads' centre`);
    const end = pads[ic.pins / 2 - 1];
    const turn = Math.atan2(end[1] - pads[0][1], end[0] - pads[0][0]) * 180 / Math.PI;
    assert.ok(Math.abs(turn - ic.rotation) < 0.002, `${ic.ref}: turned ${ic.rotation}, its pins say ${turn}`);
  }
  // every fitted IC is placed; the two not placed are not fitted and have no footprint
  const placed = new Set(ics.map((i) => i.ref));
  assert.deepEqual(icTable.ics.filter((i) => !placed.has(i.ref)).map((i) => [i.ref, i.fitted]), [['IC90', false], ['IC92', false]]);
  assert.equal(parts.verdicts.padCounts, 'pass');
});

function fittedRecord() {
  const md = fs.readFileSync(MODELS_MD, 'utf8');
  const section = md.split('\n## The fitted set\n')[1]?.split('\n## ')[0] ?? '';
  const rows = [...section.matchAll(/^\| (IC\d+) \| ([^|]+) \| (yes|no) \| [^|]+ \| ([^|]*) \| ([^|]*) \|$/gm)];
  return Object.fromEntries(rows.map(([, ref, part, fitted, sheila, paged]) =>
    [ref, { part: part.trim(), fitted: fitted === 'yes', sheila: sheila.trim() || null, paged: paged.trim() || null }]));
}

test('the fitted set, the counters and the paged ROMs are models.md\'s record, in ic-table.json, parts.json and the module alike', async () => {
  const record = fittedRecord();
  assert.deepEqual(Object.keys(record), icTable.ics.map((i) => i.ref), 'models.md\'s fitted set has a row for every IC, in order');
  for (const ic of icTable.ics) {
    assert.deepEqual([ic.part, ic.fitted, ic.paged ?? null], [record[ic.ref].part, record[ic.ref].fitted, record[ic.ref].paged], ic.ref);
    assert.ok(ic.fittedWhy && ic.fittedWhy.length > 20, `${ic.ref}: why it is fitted or not`);
  }
  const { ICS } = await import(PARTS_MJS);
  for (const ics of [parts.model.ics, ICS]) {
    for (const ic of ics) {
      const r = record[ic.ref];
      assert.deepEqual([ic.part, ic.fitted, ic.sheila, ic.paged ?? null], [r.part, r.fitted, r.sheila, r.paged], ic.ref);
    }
  }
  assert.deepEqual(parts.fitted.fitted, icTable.ics.filter((i) => i.fitted).map((i) => i.ref));
});

test('the fitted set is the plan\'s: the 8271 and the DFS fitted, Econet and speech not, BASIC and the DFS in their sockets', () => {
  const ic = Object.fromEntries(icTable.ics.map((i) => [i.ref, i]));
  assert.equal(ic.IC78.fitted, true);
  assert.equal(ic.IC78.part, '8271');
  assert.deepEqual([ic.IC88.fitted, ic.IC88.paged], [true, 'DFS']);
  assert.deepEqual([ic.IC52.fitted, ic.IC52.paged], [true, 'BASIC']);
  for (const r of ['IC89', 'IC98', 'IC99']) assert.equal(ic[r].fitted, false, r);
  // the disc interface's parts, as modification D lists them
  for (let n = 78; n <= 87; n++) assert.equal(ic[`IC${n}`].fitted, true, `IC${n}`);
});

test('the counters\' names are BbcChip\'s members, in its order, as the page reads the host\'s counts', () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro', 'BbcChip.cs'), 'utf8');
  const body = source.slice(source.indexOf('public enum BbcChip'), source.indexOf('}', source.indexOf('public enum BbcChip')));
  const members = [...body.matchAll(/^\s+(\w+),$/gm)].map((m) => m[1][0].toLowerCase() + m[1].slice(1));
  assert.deepEqual(members, BBC_CHIPS);
});

test('every counter is one of BbcChip\'s names and marks one IC', () => {
  const used = parts.model.ics.filter((i) => i.sheila !== null).map((i) => i.sheila);
  assert.deepEqual([...used].sort(), [...BBC_CHIPS].sort());
  assert.deepEqual(parts.sheila.names, BBC_CHIPS);
  for (const ic of parts.model.ics) if (ic.sheila) assert.equal(ic.fitted, true, `${ic.ref} is marked but not fitted`);
});

function corners(b) {
  const t = b.rotation * Math.PI / 180, u = [Math.cos(t) * b.l / 2, Math.sin(t) * b.l / 2], v = [-Math.sin(t) * b.w / 2, Math.cos(t) * b.w / 2];
  return [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([a, c]) => [b.x + a * u[0] + c * v[0], b.y + a * u[1] + c * v[1]]);
}

function insidePolygon(poly, [x, y]) {
  let c = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [xi, yi] = poly[i], [xj, yj] = poly[j];
    if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
  }
  return c;
}

function overlaps(a, b) {
  const A = corners(a), B = corners(b);
  for (const P of [A, B]) {
    for (let i = 0; i < 4; i++) {
      const e = [P[(i + 1) % 4][0] - P[i][0], P[(i + 1) % 4][1] - P[i][1]], n = [-e[1], e[0]];
      const pa = A.map((p) => p[0] * n[0] + p[1] * n[1]), pb = B.map((p) => p[0] * n[0] + p[1] * n[1]);
      if (Math.max(...pa) <= Math.min(...pb) + 1e-6 || Math.max(...pb) <= Math.min(...pa) + 1e-6) return false;
    }
  }
  return true;
}

test('every IC body and two-lead part lies inside the board\'s outline, and no two IC bodies overlap', () => {
  const outline = parts.model.board.outline;
  for (const ic of parts.model.ics) for (const c of corners(ic)) assert.ok(insidePolygon(outline, c), `${ic.ref} runs over the outline`);
  for (const q of parts.model.passives) {
    for (const c of corners({ x: q.x, y: q.y, l: q.length, w: q.diameter, rotation: q.rotation })) {
      assert.ok(insidePolygon(outline, c), `a two-lead part at (${q.x}, ${q.y}) runs over the outline`);
    }
  }
  const ics = parts.model.ics;
  for (let i = 0; i < ics.length; i++) for (let j = i + 1; j < ics.length; j++) assert.ok(!overlaps(ics[i], ics[j]), `${ics[i].ref} and ${ics[j].ref} overlap`);
  // what runs over the outline is the connectors at the rear and front edges and the modulator, as I5 shows them
  const over = [...parts.model.connectors, ...parts.model.others].filter((b) => corners(b).some((c) => !insidePolygon(outline, c))).map((b) => b.ref);
  assert.deepEqual(over, parts.overhang.refs);
  assert.ok(over.every((r) => /^(SK\d|PL(8|9|10|11|12)|modulator)$/.test(r)), over.join(' '));
  assert.deepEqual([parts.verdicts.inside, parts.verdicts.apart], ['pass', 'pass']);
});

// The best-fit similarity (a turn, one scale and a shift) taking src onto dst, by least squares.
function similarity(src, dst) {
  const n = src.length, ms = [0, 1].map((k) => src.reduce((s, p) => s + p[k], 0) / n), md = [0, 1].map((k) => dst.reduce((s, p) => s + p[k], 0) / n);
  let a = 0, b = 0, d = 0;
  for (let i = 0; i < n; i++) {
    const x = src[i][0] - ms[0], y = src[i][1] - ms[1], u = dst[i][0] - md[0], v = dst[i][1] - md[1];
    a += x * u + y * v; b += x * v - y * u; d += x * x + y * y;
  }
  a /= d; b /= d;
  return (p) => [a * (p[0] - ms[0]) - b * (p[1] - ms[1]) + md[0], b * (p[0] - ms[0]) + a * (p[1] - ms[1]) + md[1]];
}

test('the manual\'s places stay the FAIL the plan records: on pin N/2, the same five ICs over 10 mm', () => {
  // Recomputed here from the footprints and table 8.1: each placed IC's pin N/2 against the manual's place, Y flipped,
  // after the similarity fitted to all of them. A swap of two ICs in parts.json or in the table moves them past 10 mm.
  const refs = parts.model.ics.map((i) => i.ref).filter((r) => icTable.ics.find((x) => x.ref === r).manualXY);
  const pts = refs.map((r) => footprintPads(r)[parts.places[r].pins / 2 - 1]);
  const placed = refs.map((r) => { const ic = parts.model.ics.find((x) => x.ref === r); return [ic.x, ic.y]; });
  refs.forEach((r, k) => assert.ok(Math.hypot(...footprintPads(r).reduce((s, p) => [s[0] + p[0], s[1] + p[1]], [0, 0]).map((v, c) => v / parts.places[r].pins - placed[k][c])) < 0.002, r));
  const manual = refs.map((r) => { const [x, y] = icTable.ics.find((x) => x.ref === r).manualXY; return [x, -y]; });
  const f = similarity(manual, pts);
  const off = Object.fromEntries(refs.map((r, k) => [r, Math.hypot(f(manual[k])[0] - pts[k][0], f(manual[k])[1] - pts[k][1])]));
  for (const r of refs) assert.ok(Math.abs(off[r] - parts.manual.check.perPart[r].offMm) < 0.01, `${r}: ${off[r]} against parts.json's ${parts.manual.check.perPart[r].offMm}`);
  assert.equal(parts.manual.convention, 'pinHalf');
  assert.equal(parts.manual.settle.chosen, 'pinHalf');
  assert.deepEqual(parts.manual.settle.on, ['IC51', 'IC52', 'IC20', 'IC21']);
  const over = refs.filter((r) => off[r] > 10).sort(byRef);
  assert.deepEqual(over, ['IC39', 'IC42', 'IC74', 'IC75', 'IC94']);
  assert.equal(parts.verdicts.manual, 'fail');
  assert.equal(parts.manual.check.maxMm.toFixed(2), '97.22');
  const outcome = PLAN.split('\n').find((l) => l.startsWith('- **Outcome, 13 October 2026 (the one run, 20:23:10')) ?? '';
  assert.match(outcome, /\*\*The manual's places FAILED\*\* \(99 ICs on pin N\/2: largest 97\.22 mm, IC94, then IC74 18\.55, IC75 18\.34, IC42 11\.87 and IC39 10\.45; median 1\.44\)/);
});

test('the identities stay the FAIL the plan records, each reading marks.json\'s, and I6 is recorded, not judged', () => {
  for (const k of ['I5', 'I6']) {
    const read = Object.fromEntries(marks.parts.identities[k].marks.map((m) => [m.ref, m.reading]));
    assert.deepEqual(Object.keys(parts.identities[k]).sort(byRef), Object.keys(read).sort(byRef));
    for (const [r, v] of Object.entries(parts.identities[k])) assert.equal(v.reading, read[r], `${k} ${r}`);
  }
  const row = parts.identities.row;
  assert.deepEqual([row.read, row.populated, row.verdict], [80, 83, 'fail']);
  assert.deepEqual(row.disagree, ['IC5', 'IC31', 'IC61', 'IC62', 'IC63', 'IC64', 'IC65', 'IC66', 'IC67', 'IC68', 'IC77']);
  assert.equal(parts.verdicts.identities, 'fail');
  assert.ok(!('I6' in parts.verdicts) && !Object.keys(parts.verdicts).some((k) => /i6/i.test(k)));
  // the model keeps table 8.1's identities whatever the photographs read
  for (const ic of parts.model.ics) assert.equal(ic.part, icTable.ics.find((x) => x.ref === ic.ref).part, ic.ref);
  const outcome = PLAN.split('\n').find((l) => l.startsWith('- **Outcome, 13 October 2026 (the one run, 20:23:10')) ?? '';
  assert.match(outcome, /\*\*The identities FAILED\*\* \(80 read of the 83 ICs I5 shows populated; 11 disagree: IC5, IC31, IC61 to IC68 and IC77\)/);
  assert.match(outcome, /The places stay the scan's footprints and the identities table 8\.1's/);
});

test('task 5\'s I5 registration is task 4\'s, and passes its row', () => {
  assert.equal(parts.verdicts.i5Registration, 'pass');
  const a = parts.i5.registration, b = copper.againstI5.registration;
  assert.deepEqual([a.points, a.chips], [b.points, b.chips]);
  for (const k of ['median', 'p90', 'max']) assert.ok(Math.abs(a.heldOutMm[k] - b.heldOutMm[k]) < 0.001, k);
  assert.ok(a.heldOutMm.median <= 1.0);
});

test('the parts module is generated from parts.json, is not claimed by the page yet, and its heights are typical', async () => {
  const mod = await import(PARTS_MJS);
  const text = fs.readFileSync(PARTS_MJS, 'utf8');
  assert.match(text, /^\/\/ GENERATED by tools\/bbc-micro-model\/board_parts\.py/);
  assert.deepEqual(mod.BOARD, parts.model.board);
  assert.deepEqual(mod.ICS, parts.model.ics);
  assert.deepEqual(mod.CONNECTORS, parts.model.connectors);
  assert.deepEqual(mod.PASSIVES, parts.model.passives);
  assert.deepEqual(mod.OTHERS, parts.model.others);
  assert.deepEqual(mod.HEIGHTS_TYPICAL, parts.model.heights);
  for (const [k, h] of Object.entries(mod.HEIGHTS_TYPICAL)) assert.equal(h.measured, false, k);
  for (const c of mod.CONNECTORS) assert.ok(c.label && ['rear', 'underside', 'board'].includes(c.edge), c.ref);
  // only the page's entries are .js; the scene's data lands as .mjs until task 9b claims the models
  assert.ok(!fs.readdirSync(path.join(REPO_ROOT, 'site', 'src', 'models')).some((f) => /^bbc-micro.*\.js$/.test(f)));
  // and nothing on the page names it: no MODELS entry, and no machine's models in the registry
  const { MODELS } = await import('../src/models/models.mjs');
  const { loadRegistry } = await import('../src/lib/registry.mjs');
  assert.ok(!Object.keys(MODELS).some((k) => k.startsWith('bbc-micro-board')), 'a MODELS entry names the BBC board');
  const claimed = loadRegistry().machines.flatMap((m) => (m.models ?? []).map((x) => x.module));
  assert.ok(!claimed.some((k) => k.startsWith('bbc-micro-board')), 'a machine claims the BBC board');
});

test('task 5\'s seven method files and marks.json on disk are the ones the run read, by the hashes the journal records', () => {
  const recorded = task5Hashes();
  assert.equal(Object.keys(recorded).length, 7);
  const journal = fs.readFileSync(JOURNAL, 'utf8');
  // marks.json's hash was recorded after the run, in its own labelled line, outside the method's block
  const after = [...journal.matchAll(/^added after the run: ([0-9a-f]{64}) (\S+)$/gm)].map(([, sha, f]) => [f, sha]);
  assert.deepEqual(after.map(([f]) => f), ['tools/bbc-micro-model/data/marks.json']);
  for (const [f, sha] of [...Object.entries(recorded), ...after]) {
    const got = crypto.createHash('sha256').update(fs.readFileSync(path.join(REPO_ROOT, f))).digest('hex');
    assert.equal(got, sha, `${f} is not the file task 5's run read`);
  }
});

test('every description of the BBC board\'s parts says both checks failed and what the model keeps', () => {
  const flat = (t) => t.replace(/\s+/g, ' ');
  const read = (f) => flat(fs.readFileSync(path.join(REPO_ROOT, f), 'utf8'));
  const tool = read('tools/bbc-micro-model/README.md').split('| `board_parts.py` |')[1]?.split('\n')[0] ?? '';
  const data = read('tools/bbc-micro-model/data/README.md').split('| `parts.json` |')[1] ?? '';
  const md = read('docs/bbc-micro/facts/models.md').split('## The fitted set')[1]?.split(' ## ')[0] ?? '';
  const module = flat(fs.readFileSync(PARTS_MJS, 'utf8').split('\nexport ')[0]);
  for (const [where, text] of [['the tool README', tool], ['the data README', data], ['parts.json afterRun', flat(parts.afterRun.text)], ['the module', module], ['models.md', md]]) {
    assert.match(text, /(both held-out checks failed|the two checks on these parts failed)/i, `${where} does not say both checks failed`);
    assert.match(text, /table 8\.1's parts/, `${where} does not say the identities are table 8.1's`);
    assert.match(text, /not the board in I5/, `${where} does not say the fitted set is the emulated machine's`);
  }
  assert.match(md, /IC31 is marked 74LS74/);
  assert.match(md, /IC77 is marked 74LS00/);
  const notice = flat(fs.readFileSync(path.join(REPO_ROOT, 'NOTICE.md'), 'utf8').split(/^## /m).find((x) => x.includes('bbc-micro-board-parts.mjs')) ?? '');
  assert.match(notice, /8bs\.com\/see\/BBC_B_iss7\.jpg/);
  assert.match(notice, /states no licence/);
  assert.match(notice, /CC BY-SA 3\.0/);
  assert.match(notice, /Credit: photograph by Chris Whytehead, Chris's Acorns/);
  assert.match(notice, /No image from either photograph is committed/);
});

// --- task 7: the inside's scene, words and data, not on the page ---------------------------
//
// The re-plan of 13 October 2026: main's rules leave no green state in which the
// inside alone is claimed, so the scene, its words and its results file land as
// .mjs modules no MODELS entry lists and no machine claims, tested here in node
// (three.js builds a scene graph without a browser). Task 9b adds the thin .js
// entry, the MODELS entry and the claim together.

const layout = await import('../src/models/bbc-micro-board-layout.mjs');
const sceneModule = await import('../src/models/bbc-micro-board-scene.mjs');
const notes = await import('../src/models/bbc-micro-board-notes.mjs');
const THREE = await import('three');
const SCENE_MJS = path.join(process.cwd(), 'src', 'models', 'bbc-micro-board-scene.mjs');
const RESULTS = path.join(process.cwd(), 'src', 'data', 'bbc-micro-board-model.json');
const boardFigures = JSON.parse(fs.readFileSync(RESULTS, 'utf8'));
const TOKENS_CSS = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'tokens.css'), 'utf8');
const tokenHex = (name) => new RegExp(`--${name}:\\s*(#[0-9a-f]{6});`).exec(TOKENS_CSS)?.[1];
const fittedRefs = parts.model.ics.filter((i) => i.fitted).map((i) => i.ref).sort(byRef);

// The stage's token reader, from tokens.css instead of the page's computed style: the names asked for are kept.
function tokenReader() {
  const asked = new Set();
  const token = (name) => {
    asked.add(name);
    const hex = tokenHex(name);
    if (!hex) throw new Error(`tokens.css has no --${name}`);
    return new THREE.Color(hex);
  };
  return { token, asked };
}

function built() {
  const scene = new THREE.Scene();
  const { token, asked } = tokenReader();
  const board = sceneModule.buildBoard(scene, token);
  scene.updateMatrixWorld(true);
  return { scene, board, asked };
}

test('the board\'s counters are BbcChip\'s, and each moving counter marks its one IC; the paged ROM is BASIC in slot 15 and the DFS in 14, as the bus pages them', () => {
  assert.deepEqual(layout.COUNTED, BBC_CHIPS);
  for (const chip of BBC_CHIPS) {
    const rates = Object.fromEntries(BBC_CHIPS.map((c) => [c, { moved: c === chip, perSecond: c === chip ? 4 : 0 }]));
    assert.deepEqual(layout.marksFrom(rates), [Object.entries(parts.sheila.of).find(([, c]) => c === chip)[0]], chip);
  }
  assert.deepEqual(layout.marksFrom(null), []);
  const bus = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro', 'BbcBus.cs'), 'utf8');
  assert.match(bus, /15 => _basic,\s*14 => _dfs,/);
  assert.deepEqual(layout.PAGED_SLOTS, { 15: 'BASIC', 14: 'DFS' });
  assert.equal(layout.pagedRef(15), parts.model.ics.find((i) => i.paged === 'BASIC').ref);
  assert.equal(layout.pagedRef(14), parts.model.ics.find((i) => i.paged === 'DFS').ref);
  assert.deepEqual([layout.pagedRef(15), layout.pagedRef(14)], ['IC52', 'IC88']);
  for (const slot of [0, 1, 12, 13, null, undefined]) assert.equal(layout.pagedRef(slot), null, String(slot));
});

test('the scene draws a body for every fitted IC in the parts file and none for an unfitted one, each on a socket where the parts file has one', () => {
  const { scene, board } = built();
  const refs = new Map();
  scene.traverse((o) => { if (o.userData.ref) refs.set(`${o.userData.ref} ${o.userData.kind}`, o); });
  const bodies = [...refs.keys()].filter((k) => k.endsWith(' body')).map((k) => k.split(' ')[0]).sort(byRef);
  assert.deepEqual(bodies, fittedRefs);
  for (const ic of parts.model.ics.filter((i) => !i.fitted)) assert.ok(![...refs.keys()].some((k) => k.startsWith(`${ic.ref} `)), `${ic.ref} is not fitted but is drawn`);
  const sockets = [...refs.keys()].filter((k) => k.endsWith(' socket')).map((k) => k.split(' ')[0]).sort(byRef);
  assert.deepEqual(sockets, parts.model.ics.filter((i) => i.fitted && i.socketed).map((i) => i.ref).sort(byRef));
  for (const ic of parts.model.ics.filter((i) => i.fitted)) {
    const body = refs.get(`${ic.ref} body`);
    const p = new THREE.Vector3();
    body.getWorldPosition(p);
    assert.ok(Math.abs(p.x - (ic.x - parts.model.board.width / 2) / 10) < 1e-6 && Math.abs(p.z - (ic.y - parts.model.board.depth / 2) / 10) < 1e-6, `${ic.ref} is not at its place`);
    const box = new THREE.Box3().setFromObject(body);
    assert.ok(Math.abs(box.max.y - ic.height / 10) < 1e-6, `${ic.ref}'s top is not at its height`);
    assert.ok(box.min.y >= (ic.socketed ? parts.model.heights.socket.value / 10 - 1e-6 : 0), `${ic.ref} sinks into its socket or the board`);
  }
  assert.equal(board.chips.size, fittedRefs.length);
  // the connectors by reference, those underneath below the board and the others on it
  const T = parts.model.board.thickness / 10;
  for (const c of parts.model.connectors) {
    const o = refs.get(`${c.ref} connector`);
    assert.ok(o, `${c.ref} is not drawn`);
    const box = new THREE.Box3().setFromObject(o);
    if (c.edge === 'underside') assert.ok(box.max.y <= -T + 1e-6, `${c.ref} is not under the board`);
    else assert.ok(box.min.y >= -1e-6, `${c.ref} is not on the board`);
  }
  for (const o of parts.model.others) assert.ok(refs.get(`${o.ref} other`), `${o.ref} is not drawn`);
  // the two-lead parts, one instance each, by kind
  const counts = {};
  scene.traverse((o) => { if (o.isInstancedMesh && o.userData.passives) counts[o.userData.passives] = o.count; });
  assert.deepEqual(counts, parts.passives.byKind);
  // the board is its outline with its holes cut through: the top face's area is the outline's less the holes'
  const area = (pts) => Math.abs(pts.reduce((s, [x, y], i) => { const [u, v] = pts[(i + 1) % pts.length]; return s + x * v - u * y; }, 0)) / 2;
  const outline = parts.model.board.outline.slice(0, -1);
  const holes = parts.model.board.holes.map((h) => Array.from({ length: 28 }, (_, i) => [h.x + (h.d / 2) * Math.cos((i / 28) * 2 * Math.PI), h.y + (h.d / 2) * Math.sin((i / 28) * 2 * Math.PI)]));
  const want = (area(outline) - holes.reduce((s, h) => s + area(h), 0)) / 100;
  const g = board.board.geometry;
  const top = g.groups[0];
  const pos = g.getAttribute('position');
  let got = 0;
  for (let i = top.start; i < top.start + top.count; i += 3) {
    const a = new THREE.Vector3().fromBufferAttribute(pos, i), b = new THREE.Vector3().fromBufferAttribute(pos, i + 1), c = new THREE.Vector3().fromBufferAttribute(pos, i + 2);
    got += b.sub(a).cross(c.sub(a)).length() / 2;
  }
  assert.ok(Math.abs(got - want) / want < 1e-6, `the top face is ${got} cm2, not ${want}`);
  assert.equal(g.groups.length, 3);
  // the camera's bounds hold every part, with its margin
  const [x0, , z0, x1, , z1] = sceneModule.stageOptions().bounds;
  const all = new THREE.Box3().setFromObject(board.parts);
  assert.ok(all.min.x >= x0 && all.max.x <= x1 && all.min.z >= z0 && all.max.z <= z1, 'a part is outside the camera\'s bounds');
});

test('the access tint marks exactly the chips named, the paged frame exactly the paged ROM, and Show tracks and Show tracks only switch as the NES\'s do', () => {
  const { board } = built();
  board.mark(['IC3', 'IC78']);
  for (const [ref, c] of board.chips) assert.equal(c.tint.visible, ref === 'IC3' || ref === 'IC78', ref);
  board.mark([]);
  assert.ok([...board.chips.values()].every((c) => !c.tint.visible));
  board.page('IC88');
  for (const [ref, c] of board.chips) assert.equal(Boolean(c.frame?.visible), ref === 'IC88', ref);
  board.page(null);
  assert.ok([...board.chips.values()].every((c) => !c.frame?.visible));
  assert.ok(board.chips.get('IC52').frame && board.chips.get('IC88').frame && !board.chips.get('IC1').frame, 'only the paged ROMs have a frame');
  // the layers, with stand-in track materials
  const top = new THREE.MeshStandardMaterial(), bottom = new THREE.MeshStandardMaterial();
  const l = sceneModule.createLayers(board, { top, bottom });
  assert.deepEqual(l.state(), { tracks: true, partsShown: true, partsLevel: 1 });
  assert.equal(board.board.material[0], top);
  assert.equal(board.board.material[1], bottom);
  l.toggleOnly();
  assert.deepEqual(l.state().partsShown, false);
  l.step(0.1, false);
  assert.ok(l.state().partsLevel > 0 && l.state().partsLevel < 1, 'the parts fade, not vanish');
  l.step(1, false);
  assert.equal(board.parts.visible, false);
  l.toggleTracks();
  assert.deepEqual([l.state().tracks, l.state().partsShown], [false, true], 'tracks off brings the parts back');
  assert.equal(board.board.material[0], board.plain.top);
  l.step(0.01, true);
  assert.equal(l.state().partsLevel, 1, 'with reduced motion the parts come back at once');
  l.toggleOnly();
  assert.equal(l.state().tracks, true, 'tracks only puts the tracks on');
  // no maps: the board stays plain and the buttons do nothing
  const none = sceneModule.createLayers(built().board, { top: null, bottom: null });
  none.toggleTracks();
  assert.deepEqual(none.state(), { tracks: false, partsShown: true, partsLevel: 1 });
});

test('the board\'s colours are tokens of their own, the mark is --model-active and not the lime, and every token the scene reads is defined', () => {
  const { asked, board } = built();
  for (const name of ['model-bbc-pcb', 'model-bbc-pcb-under', 'model-bbc-socket', 'model-active']) assert.ok(asked.has(name), `the scene does not read --${name}`);
  // the board's plain faces, its sockets and the mark are those tokens' colours
  const hexOf = (m) => `#${m.color.getHexString()}`;
  assert.deepEqual([hexOf(board.plain.top), hexOf(board.plain.bottom), hexOf(board.chips.get('IC52').socket.material), hexOf(board.chips.get('IC1').tint.material)],
    ['model-bbc-pcb', 'model-bbc-pcb-under', 'model-bbc-socket', 'model-active'].map(tokenHex));
  // and the faces' colours, which the track map is turned into
  const reader = tokenReader();
  for (const face of ['top', 'bottom']) sceneModule.faceColours(face, reader.token);
  assert.deepEqual([...reader.asked].sort(), ['model-bbc-pcb', 'model-bbc-pcb-under', 'model-bbc-print', 'model-copper']);
  for (const name of [...asked, ...reader.asked]) assert.ok(tokenHex(name), `--${name}`);
  assert.notEqual(tokenHex('model-active'), tokenHex('lime'));
  for (const name of ['model-bbc-pcb', 'model-bbc-pcb-under', 'model-bbc-print', 'model-bbc-socket']) assert.notEqual(tokenHex(name), tokenHex('lime'), name);
  assert.doesNotMatch(fs.readFileSync(SCENE_MJS, 'utf8'), /emissive/, 'the tracks glow');
});

test('the track map is copper.json\'s, read as the component side\'s copper in red, the solder side\'s in green and the print in blue', () => {
  assert.equal(layout.TRACKS.src, '/models/bbc-micro-board-tracks.webp');
  assert.deepEqual([layout.TRACKS.width, layout.TRACKS.height, layout.TRACKS.pxPerMm], [copper.map.width, copper.map.height, copper.mapPxPerMm]);
  assert.deepEqual(layout.TRACKS.originMm, copper.map.originMm);
  const px = (r, g, b) => Uint8ClampedArray.from([r, g, b, 255]);
  const { texel } = sceneModule;
  assert.deepEqual([texel(px(255, 0, 0), 0, 'top'), texel(px(255, 0, 0), 0, 'bottom')], [{ copper: 1, print: 0 }, { copper: 0, print: 0 }]);
  assert.deepEqual([texel(px(0, 255, 0), 0, 'top'), texel(px(0, 255, 0), 0, 'bottom')], [{ copper: 0, print: 0 }, { copper: 1, print: 0 }]);
  assert.deepEqual([texel(px(0, 0, 255), 0, 'top'), texel(px(0, 0, 255), 0, 'bottom')], [{ copper: 0, print: 1 }, { copper: 0, print: 0 }]);
  // and turned into colour from the tokens: copper is --model-copper, print --model-bbc-print, bare lacquer each face's
  const { token } = tokenReader();
  const srgb = (name) => tokenHex(name).match(/[0-9a-f]{2}/g).map((h) => parseInt(h, 16));
  const near = (a, b) => a.every((v, i) => Math.abs(v - b[i]) <= 1);
  const top = sceneModule.faceColours('top', token), bottom = sceneModule.faceColours('bottom', token);
  assert.ok(near(sceneModule.shade({ copper: 0, print: 0 }, top).colour, srgb('model-bbc-pcb')));
  assert.ok(near(sceneModule.shade({ copper: 0, print: 0 }, bottom).colour, srgb('model-bbc-pcb-under')));
  assert.ok(near(sceneModule.shade({ copper: 1, print: 0 }, top).colour, srgb('model-copper')));
  assert.ok(near(sceneModule.shade({ copper: 1, print: 0 }, bottom).colour, srgb('model-copper')));
  assert.ok(near(sceneModule.shade({ copper: 1, print: 1 }, top).colour, srgb('model-bbc-print')), 'the print lies over the copper');
});

test('the legend lists every fitted IC once, in IC number order, with its part and role from ic-table.json, and its counter, its paged ROM or why it is never marked', () => {
  const rows = layout.chipLegend();
  assert.deepEqual(rows.map((r) => r.ref), fittedRefs);
  for (const row of rows) {
    const t = icTable.ics.find((i) => i.ref === row.ref);
    const ic = parts.model.ics.find((i) => i.ref === row.ref);
    assert.deepEqual([row.part, row.role], [t.part, t.role], row.ref);
    assert.deepEqual([row.sheila, row.paged], [ic.sheila, ic.paged ?? null], row.ref);
    if (row.sheila) assert.equal(row.mark, layout.COUNTED_WORDS[row.sheila], row.ref);
    else if (row.paged) assert.match(row.mark, /^paged: /, row.ref);
    else assert.match(row.mark, /^never: /, row.ref);
  }
  const mark = Object.fromEntries(rows.map((r) => [r.ref, r.mark]));
  for (const ref of ['IC1', 'IC51', ...Array.from({ length: 16 }, (_, i) => `IC${53 + i}`)]) assert.match(mark[ref], /^never: in use all the time/, ref);
  for (const chip of BBC_CHIPS) assert.match(layout.COUNTED_WORDS[chip], /^when the processor/, chip);
  assert.match(layout.COUNTED_WORDS.sound, /through the system VIA/);
});

test('the legend says in words what a mark means, why the CPU, RAM and OS ROM are never marked, what paged means, and which counted chips are not emulated', () => {
  const words = layout.LEGEND_WORDS.join(' ');
  assert.match(words, /read or written by the processor in the last quarter second/);
  assert.match(words, /not that the chip is working/);
  assert.match(words, /The processor, the RAM and the operating system's ROM are in use all the time/);
  assert.match(words, /so none of them is ever marked/);
  assert.match(words, /the paged ROM, BASIC or the DFS, is framed/);
  assert.match(words, /the 6850, the serial ULA and the uPD7002, which the machine does not emulate/);
  assert.match(words, /sound chip[^.]*through the system VIA/);
  assert.match(words, /heights of the parts are typical ones, not measured/);
  // each counted chip that the machine does not emulate is the one the legend names
  for (const [ref, part] of [['IC4', '6850'], ['IC73', 'uPD7002']]) assert.equal(icTable.ics.find((i) => i.ref === ref).part, part);
  assert.equal(parts.sheila.of.IC7, 'serialUla');
});

test('the caption starts "Model, not a photograph." and its sizes and counts come from the layout', () => {
  const about = layout.describe();
  const cm = (mm) => (mm / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  assert.match(about, /^Model, not a photograph\. /);
  assert.ok(about.includes(`${cm(parts.model.board.width)} by ${cm(parts.model.board.depth)} cm`));
  const fitted = parts.model.ics.filter((i) => i.fitted);
  assert.ok(about.includes(`the ${fitted.length} chips fitted`), 'the count of chips');
  assert.ok(about.includes(`${fitted.filter((i) => i.socketed).length} of them in sockets`), 'the count of sockets');
  assert.ok(about.includes(`${parts.model.connectors.length} connectors`));
  assert.ok(about.includes(`${parts.model.passives.length} resistors and capacitors`));
  for (const ref of ['IC1', 'IC78']) assert.ok(about.includes(parts.model.ics.find((i) => i.ref === ref).part), ref);
  assert.match(about, /to look at: the copper's connections are not verified, and the checks that would have shown them failed/);
  assert.match(about, /heights of the parts are typical ones, not measured/);
  // nothing typed: a part file with one chip fewer gives another caption (the layout reads the module)
  assert.ok(!/\b(88|32|13|258)\b/.test(fs.readFileSync(path.join(process.cwd(), 'src', 'models', 'bbc-micro-board-layout.mjs'), 'utf8').replace(/^\s*\/\/.*$/gm, '')), 'the layout types a count');
});

test('the results file is written from frame.json, registration.json, copper.json and parts.json, and agrees with them', () => {
  const f = boardFigures;
  const solder = registration.solder;
  assert.deepEqual(f.board, { widthMm: frame.board.widthMm, depthMm: frame.board.depthMm });
  assert.deepEqual(f.scale, {
    yMedianMm: frame.heldOut.rowLengthErrMm.median, yRows: frame.heldOut.rowLengthErrMm.n,
    xMedianMm: frame.heldOutX.scaledErrMm.median, xRows: frame.heldOutX.scaledErrMm.n, xLargestMm: frame.heldOutX.largest.scaledErrMm, xLargestRow: frame.heldOutX.largest.row,
    xyPct: frame.ratioPct, xVerdict: frame.verdicts.find((v) => /as revised/.test(v.measure)).verdict,
  });
  assert.deepEqual(f.solder, { holes: solder.holes, heldOutMedianMm: solder.heldOutMm.median, heldOutP90Mm: solder.heldOutMm.p90, verdict: solder.verdict });
  const n = copper.nets, i5 = copper.againstI5;
  assert.deepEqual(f.copper, {
    top: copper.coverage.top, bottom: copper.coverage.bottom, print: copper.coverage.print, drills: copper.drills.n, drillsTop: copper.drillsInCopper.top, drillsBottom: copper.drillsInCopper.bottom, mapPxPerMm: copper.mapPxPerMm,
    printMm2: copper.print.areaMm2, recoveredMm2: copper.print.recoveredMm2,
    nets: { chips: n.chips, gndPins: n.gnd.pins, gndInLargest: n.gnd.inLargest, vccPins: n.vcc.pins, vccInLargest: n.vcc.inLargest, touching: n.touching, verdict: copper.verdicts.nets },
    againstI5: { iou: i5.iou, comparedMm2: i5.comparedMm2, verdict: copper.verdicts.againstI5 },
    i5Registration: { chips: i5.registration.chips, points: i5.registration.points, heldOutMedianMm: i5.registration.heldOutMm.median, verdict: copper.verdicts.i5Registration },
  });
  const heights = Object.entries(parts.model.heights).filter(([k]) => k !== 'board').map(([, h]) => h);
  const over = Object.entries(parts.manual.check.perPart).filter(([, p]) => p.offMm > parts.manual.check.limitMm).map(([r]) => r).sort(byRef);
  assert.deepEqual(f.parts, {
    ics: parts.model.ics.length, fitted: parts.model.ics.filter((i) => i.fitted).length, socketed: parts.model.ics.filter((i) => i.fitted && i.socketed).length,
    connectors: parts.model.connectors.length, passives: parts.model.passives.length, axial: parts.passives.byKind.axial, radial: parts.passives.byKind.radial,
    manual: { maxMm: parts.manual.check.maxMm, medianMm: parts.manual.check.medianMm, limitMm: parts.manual.check.limitMm, over, verdict: parts.verdicts.manual },
    identities: { read: parts.identities.row.read, populated: parts.identities.row.populated, disagree: parts.identities.row.disagree.length, verdict: parts.verdicts.identities },
    heightsTypical: heights.filter((h) => !h.measured).length, heightsMeasured: heights.filter((h) => h.measured).length,
  });
  assert.deepEqual(over, ['IC39', 'IC42', 'IC74', 'IC75', 'IC94']);
  // every source the board used, by its address, as sources.json has it: the parts module's, then I6, read for markings
  assert.deepEqual(f.sources.map((x) => x.id), [...parts.model.sources, 'I6']);
  for (const x of f.sources) {
    const s = sources.find((y) => y.id === x.id);
    assert.deepEqual(x, { id: s.id, url: s.url, page: s.page, sha256: s.sha256, kind: s.kind, licence: s.licence, fetched: s.fetched });
  }
  assert.match(fs.readFileSync(path.join(TOOL, 'results.py'), 'utf8'), /bbc-micro-board-model\.json/);
});

test('the note is made() from the results file, every figure in it read from there, and it states the nets, I5 and both parts checks as failed', () => {
  const f = boardFigures;
  const { paragraphs, limits } = notes.made(f);
  const text = [...paragraphs, ...limits].join(' ');
  const mm = (v) => `${v.toLocaleString('en-GB', { maximumFractionDigits: 3 })} mm`;
  const pc = (v) => `${Math.round(v * 100)}%`;
  for (const v of [f.scale.yMedianMm, f.scale.xMedianMm, f.scale.xLargestMm, f.solder.heldOutMedianMm, f.solder.heldOutP90Mm, f.copper.i5Registration.heldOutMedianMm, f.parts.manual.maxMm, f.parts.manual.medianMm]) assert.ok(text.includes(mm(v)), `${mm(v)} is not in the note`);
  for (const v of [f.copper.top, f.copper.bottom]) assert.ok(text.includes(pc(v)), `${pc(v)} is not in the note`);
  assert.ok(text.includes(f.solder.holes.toLocaleString('en-GB')));
  assert.ok(text.includes(f.scale.xVerdict), 'the x scale\'s verdict');
  const sentence = notes.copperSentence(f);
  assert.ok(text.includes(sentence));
  const n = f.copper.nets;
  assert.ok(sentence.includes(`${n.gndInLargest} of ${n.gndPins} ground pins`) && sentence.includes(`${n.vccInLargest} of ${n.vccPins} +5V pins`));
  assert.ok(n.touching && /a ground pin and a \+5V pin came out in the same net/.test(sentence));
  assert.ok(sentence.includes(`${f.copper.nets.chips} chips`));
  assert.ok(sentence.includes(f.copper.againstI5.iou.toLocaleString('en-GB', { maximumFractionDigits: 3 })), 'the I5 comparison\'s figure');
  assert.match(sentence, /not a netlist/);
  assert.match(sentence, /failed[\s\S]*failed/, 'both the nets and the I5 comparison failed');
  assert.deepEqual([f.copper.nets.verdict, f.copper.againstI5.verdict], ['fail', 'fail']);
  assert.match(text, /optimistic, and not an independent check/);
  const partsWords = notes.partsSentence(f);
  assert.ok(text.includes(partsWords));
  assert.match(partsWords, /the two checks on these parts failed/i);
  assert.match(partsWords, /table 8\.1's parts/);
  assert.match(partsWords, /not the board in I5/);
  assert.ok(partsWords.includes(`${f.parts.identities.disagree} of the ${f.parts.identities.read}`));
  assert.ok(partsWords.includes(f.parts.manual.over.join(', ').replace(/, (IC\d+)$/, ' and $1')));
  assert.deepEqual([f.parts.manual.verdict, f.parts.identities.verdict], ['fail', 'fail']);
  // the figures are read, not typed: a changed results file changes the note
  const changed = structuredClone(f);
  changed.copper.nets.gndInLargest += 1;
  changed.parts.identities.disagree += 1;
  changed.solder.heldOutMedianMm += 0.001;
  assert.notEqual(notes.copperSentence(changed), sentence);
  assert.notEqual(notes.partsSentence(changed), partsWords);
  assert.notEqual(notes.made(changed).paragraphs.join(' '), paragraphs.join(' '));
});

test('every sentence the board model says about its copper says it is traced to look at and not verified, and the help says its checks failed', () => {
  const { paragraphs, limits } = notes.made(boardFigures);
  const blocks = [layout.describe(), layout.TRACKS_HELP, ...layout.LEGEND_WORDS, ...Object.values(layout.STATUS), ...paragraphs, ...limits];
  const code = fs.readFileSync(SCENE_MJS, 'utf8').replace(/^\s*\/\/.*$/gm, '');
  const said = [...code.matchAll(/'([A-Z][^'\n]*\.)'/g)].map((m) => m[1]);
  const sentences = [...blocks, ...said].flatMap((b) => b.split(/(?<=\.)\s+(?=[A-Z])/)).filter((x) => /copper/i.test(x));
  assert.ok(sentences.length >= 6, `only ${sentences.length} sentences about the copper`);
  for (const x of sentences) assert.ok(/to look at|not a netlist/.test(x) && /not verified|failed/.test(x), `a sentence about the copper does not say it is traced to look at and not verified: ${x}`);
  assert.match(layout.TRACKS_HELP, /traced to look at, with their connections not verified/);
  assert.match(layout.TRACKS_HELP, /failed/);
  for (const s of [layout.STATUS.tracksOn, layout.STATUS.partsHidden]) assert.match(s, /to look at[\s\S]*not verified/);
});

test('the scene, its words and its results land unclaimed: .mjs modules with no mount, no MODELS entry, no claim and nothing in the deploy list', async () => {
  const src = fs.readFileSync(SCENE_MJS, 'utf8');
  assert.equal(sceneModule.mount, undefined);
  assert.doesNotMatch(src, /export (async )?function mount/);
  assert.ok(fs.existsSync(RESULTS));
  assert.ok(!fs.readdirSync(path.join(process.cwd(), 'src', 'models')).some((f) => /^bbc-micro.*\.js$/.test(f)));
  const { MODELS } = await import('../src/models/models.mjs');
  assert.ok(!Object.keys(MODELS).some((k) => k.startsWith('bbc-micro')));
  const { loadRegistry } = await import('../src/lib/registry.mjs');
  assert.deepEqual(loadRegistry().machines.find((m) => m.id === 'bbc-micro').models ?? [], []);
  assert.doesNotMatch(fs.readFileSync(path.join(REPO_ROOT, '.github', 'workflows', 'deploy-site.yml'), 'utf8'), /bbc-micro-board/);
  // the scene reads the layout and the shared sampler's list, never the notes (Node only) or a copy of the sampler
  assert.match(src, /from '\.\/bbc-micro-board-layout\.mjs'/);
  assert.doesNotMatch(src, /bbc-micro-board-notes|accessRates|createSampler/);
  assert.doesNotMatch(fs.readFileSync(path.join(process.cwd(), 'src', 'models', 'bbc-micro-board-layout.mjs'), 'utf8'), /node:|accessRates =|function accessRates/);
});

// --- Task 8: the outside measured ------------------------------------------------------------

const keysJson = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'keys.json'), 'utf8'));
const caseJson = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'case.json'), 'utf8'));
const caseMarks = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'case-marks.json'), 'utf8'));
const caseParts = await import('../src/models/bbc-micro-case-parts.mjs');
const { BBC_KEYS } = await import('../public/bbc-keys.js');
const task8Outcome = () => PLAN.split('\n').find((l) => l.startsWith('- **Outcome, 13 October 2026 (the run, 22:08:29 UTC')) ?? '';

function task8Hashes() {
  const section = fs.readFileSync(JOURNAL, 'utf8').split('## Task 8: the outside measured')[1] ?? '';
  const block = section.split('### Task 8\'s method fixed, and its checks')[1]?.split(/\n#{2,3} /)[0] ?? '';
  const first = Object.fromEntries([...block.matchAll(/^([0-9a-f]{64}) (\S+)$/gm)].map(([, sha, f]) => [f, sha]));
  // the changes after the first and second runs, each a block of the files it changed, in order
  const later = (section.split('### The first run, and two changes after it')[1] ?? '').split(/\n## /)[0];
  for (const [, sha, f] of later.matchAll(/^([0-9a-f]{64}) (\S+)$/gm)) first[f] = sha;
  return first;
}

test('the outside was measured by the method fixed before its checks ran, changed only as the journal records, and the files on disk are that method', () => {
  const recorded = task8Hashes();
  assert.equal(Object.keys(recorded).length, 9);
  assert.deepEqual(caseJson.methodFixed, recorded);
  assert.deepEqual(keysJson.methodFixed, recorded);
  for (const [f, sha] of Object.entries(recorded)) {
    const got = crypto.createHash('sha256').update(fs.readFileSync(path.join(REPO_ROOT, f))).digest('hex');
    assert.equal(got, sha, `${f} is not the file task 8's last run read`);
  }
  // the scripts the checks run in are unchanged since the method was fixed
  const section = fs.readFileSync(JOURNAL, 'utf8').split('## Task 8: the outside measured')[1];
  for (const f of ['case_profile.py', 'case_register.py']) assert.match(section, new RegExp(`^${recorded[`tools/bbc-micro-model/${f}`]} tools/bbc-micro-model/${f.replace('.', '\\.')}$`, 'm'));
});

test('every machine key has exactly one K1 keycap, with BREAK and a second SHIFT, from the dbhq-uk fork', () => {
  const names = keysJson.keys.map((k) => k.name).sort();
  assert.deepEqual(names, [...Object.keys(BBC_KEYS), 'Break', 'Shift2'].sort());
  assert.equal(keysJson.mapping.verdict, 'pass');
  assert.equal(keysJson.k1.commit, '5ec53df566c77424caa6be827d0831da3916ecfa');
  const k1 = sources.find((s) => s.id === 'K1');
  for (const u of [k1.url, k1.page]) assert.match(u, /^https:\/\/github\.com\/dbhq-uk\/bbc-keyboard\/blob\/5ec53df566c77424caa6be827d0831da3916ecfa\//);
  assert.equal(keysJson.k1.sha256, k1.sha256);
  // cells on the 19.05 mm pitch: every centre a whole number of quarter pitches from ESCAPE's
  const esc = keysJson.keys.find((k) => k.name === 'Escape');
  for (const k of keysJson.keys) {
    for (const d of [k.x - esc.x, k.y - esc.y]) assert.ok(Math.abs(d / (19.05 / 8) - Math.round(d / (19.05 / 8))) < 0.01, `${k.name} is off the pitch`);
    assert.ok(Math.abs(k.w - k.units * 19.05) < 1e-6 && k.d === 19.05);
  }
});

test('keys.json passes the O1 row, records O3\'s and the raw front edge, and its verdicts are what its figures give', () => {
  const judge = (k) => (k.heldOutMm.median > 1.5 || k.over3mm > 5 ? 'STOP' : k.heldOutMm.median <= 1.0 && k.heldOutMm.p90 <= 2.0 && k.heldOutMm.max <= 3.0 && k.n >= 30 ? 'pass' : 'between pass and stop');
  assert.equal(judge(keysJson.o1), 'pass');
  assert.equal(keysJson.o1.verdict, judge(keysJson.o1));
  assert.equal(keysJson.o3.verdict, judge(keysJson.o3));
  assert.equal(keysJson.o3.n, 36);
  assert.deepEqual(keysJson.o1.heldOutMm, spike.keys.heldOutMm);
  assert.equal(keysJson.frontEdgeMm, spike.keys.frontEdgeMm);
  assert.equal(caseJson.frontEdge.rawMm, spike.keys.frontEdgeMm);
});

test('the front edge\'s parallax check is recorded as not measurable, with h\'s reason and d, and no corrected width', () => {
  const f = caseJson.frontEdge;
  assert.equal(f.verdict, 'not measurable');
  assert.equal(f.correctedMm, null);
  assert.equal(f.h.mm, null);
  assert.match(f.h.from, /K2[\s\S]*403[\s\S]*Exif[\s\S]*right-angle constraint/);
  assert.ok(f.d.mm > 0 && f.d.seMm > 0 && /O4 and O5/.test(f.d.from));
  assert.equal(f.rawSeMm, spike.keys.parallax.rawWidthSeMm);
  assert.match(task8Outcome(), /\*\*The front edge's parallax check is not measurable\*\* \(no h independent of the 415 mm\); d, the front edge's depth below the key plane, is 6\.18 mm with a standard error of 2\.80/);
  assert.equal(f.d.mm.toFixed(2), '6.18');
  assert.equal(f.d.seMm.toFixed(2), '2.80');
});

test('the side profile\'s check point is the key A, within 2.0 mm, and the profile and key plane come from the triangulation, not the fallback', () => {
  const c = caseJson.profileCheck;
  assert.equal(caseMarks.checkKey.key, 'A');
  const a = keysJson.keys.find((k) => k.name === 'A');
  assert.deepEqual(c.k1.slice(0, 2), [a.x, a.y]);
  const err = Math.hypot(c.triangulated[0] - a.x, c.triangulated[1] - a.y, c.triangulated[2]);
  assert.ok(Math.abs(err - c.errMm) < 0.001);
  assert.equal(c.verdict, err <= 2.0 ? 'pass' : 'fail');
  assert.equal(c.fallback, c.verdict !== 'pass');
  assert.equal(c.verdict, 'pass');
  assert.equal(caseParts.PROFILE.fallback, false);
  assert.ok(Math.abs(caseJson.keyPlaneZ - (73 + caseJson.frontEdge.d.mm)) < 1e-6);
  assert.match(task8Outcome(), /\*\*The side profile's check point passed:\*\* the key A, triangulated from O4 and O5, 0\.45 mm from K1's place, against 2\.0/);
  assert.equal(c.errMm.toFixed(2), '0.45');
});

test('the case is the Service Manual\'s, and the keys and lights lie inside the opening, the opening inside the case, no two keycaps overlapping', () => {
  assert.deepEqual([caseParts.CASE.width, caseParts.CASE.depth, caseParts.CASE.height], [415, 345, 73]);
  assert.deepEqual(caseJson.footprint, { widthMm: 415, depthMm: 345, source: 'D1' });
  const op = caseJson.recess.opening;
  const inside = (x, y) => op.every((p, i) => { const q = op[(i + 1) % op.length]; return (q[0] - p[0]) * (y - p[1]) - (q[1] - p[1]) * (x - p[0]) >= 0; });
  for (const p of op) assert.ok(p[0] > 0 && p[0] < 415 && p[1] > 0 && p[1] < 345);
  const keysIn = caseParts.KEYS.keys;
  for (const k of keysIn) {
    for (const [dx, dy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) assert.ok(inside(k.x + dx * k.w / 2, k.y + dy * k.d / 2), `${k.name}'s cell is not inside the opening`);
  }
  // keycaps overlap or not in K1's own frame, where their cells are square to the axes (in the case they are turned)
  for (const a of keysJson.keys) for (const b of keysJson.keys) {
    if (a === b) continue;
    const ox = Math.min(a.x + a.w / 2, b.x + b.w / 2) - Math.max(a.x - a.w / 2, b.x - b.w / 2);
    const oy = Math.min(a.y + a.d / 2, b.y + b.d / 2) - Math.max(a.y - a.d / 2, b.y - b.d / 2);
    assert.ok(ox <= 1e-3 || oy <= 1e-3, `${a.name} and ${b.name} overlap`);
  }
  const leds = caseParts.LEDS.leds;
  assert.deepEqual(leds.map((l) => l.name), ['cassette motor', 'caps lock', 'shift lock']);
  assert.deepEqual(caseMarks.o1.leds.order, ['cassette motor', 'caps lock', 'shift lock']);
  assert.ok(leds[0].x < leds[1].x && leds[1].x < leds[2].x, 'the lights are not in O1\'s order, left to right');
  for (const l of leds) assert.ok(inside(l.x, l.y) && l.z < 73 && l.z > 60, `${l.name}`);
});

test('the keys are K1\'s centres put in the case by the recorded turn and shift, their colours as read on O1', () => {
  const M = caseJson.k1ToCase.matrix;
  const byName = Object.fromEntries(keysJson.keys.map((k) => [k.name, k]));
  for (const k of caseParts.KEYS.keys) {
    const s = byName[k.name];
    // case.json keeps the motion to 4 places, so its turn is good to 0.00005, which is 0.02 mm at 400 mm
    assert.ok(Math.hypot(M[0][0] * s.x + M[0][1] * s.y + M[0][2] - k.x, M[1][0] * s.x + M[1][1] * s.y + M[1][2] - k.y) < 0.03, k.name);
    assert.ok(Math.abs(Math.hypot(M[0][0], M[1][0]) - 1) < 1e-4, 'the motion has a scale');
    assert.equal(k.colour, caseMarks.keyColours.red.includes(k.name) ? 'red' : caseMarks.keyColours.light.includes(k.name) ? 'light' : 'dark');
    assert.ok(Math.abs(k.z - caseJson.keyPlaneZ) < 0.001);
  }
  assert.deepEqual(caseMarks.keyColours.light, ['Left', 'Right', 'Up', 'Down', 'Copy']);
  assert.equal(keysJson.colours.agree, keysJson.keys.filter((k) => k.colour === k.colourClustered).length);
});

test('the rear ports are in O2\'s order, each scored one within 2.0 mm of its socket plus the board\'s offset, on the D shell\'s scale', () => {
  assert.deepEqual(caseJson.rearOrder, ['UHF out', 'video out', 'RGB', 'RS423', 'cassette', 'analogue in', 'econet']);
  const posts = caseMarks.o2.dsubPosts;
  assert.ok(Math.abs(caseJson.rearScale.pxPerMm - Math.hypot(posts.right[0] - posts.left[0], posts.right[1] - posts.left[1]) / 33.325) < 1e-4);
  const board = Object.fromEntries(parts.model.connectors.map((c) => [c.ref, c.x]));
  const scored = caseJson.rear.filter((p) => p.scored);
  assert.deepEqual(scored.map((p) => p.ref).sort(), ['SK1', 'SK3', 'SK4', 'SK5', 'SK6']);
  const offs = scored.map((p) => p.x - board[p.ref]).sort((a, b) => a - b);
  assert.ok(Math.abs(offs[2] - caseJson.boardInCase.x) < 0.001, 'the board\'s offset is not the median');
  for (const p of scored) {
    assert.ok(Math.abs(p.x - (board[p.ref] + caseJson.boardInCase.x) - p.errMm) < 0.001, p.ref);
    assert.ok(Math.abs(p.errMm) <= 2.0, `${p.ref} is ${p.errMm} mm off`);
  }
  assert.equal(caseJson.rearCheck.verdict, 'pass');
  assert.match(task8Outcome(), /\*\*The rear ports passed:\*\* 5 of 5 within 2\.0 mm of their sockets plus the board's offset, the worst 0\.78 mm \(SK1\)/);
  assert.equal(caseJson.rearCheck.worstMm.toFixed(2), '0.78');
  for (const p of caseJson.rear) assert.ok(p.label && p.x > 0 && p.x < 415 && p.z > 5 && p.z < 73, p.ref);
  for (const u of caseJson.underside) assert.ok(u.label && u.x - u.w / 2 > 0 && u.x + u.w / 2 < 415, u.ref);
  assert.ok(caseJson.undersidePanel.x0 >= 3 && caseJson.undersidePanel.x1 <= 412);
});

test('the case parts module is generated from case.json, and lands unclaimed', async () => {
  const near = (a, b, where) => {
    if (typeof a === 'number') return assert.ok(Math.abs(a - b) <= 0.0011, `${where}: ${a} against ${b}`);
    if (a && typeof a === 'object') { assert.deepEqual(Object.keys(a).sort(), Object.keys(b).sort(), where); for (const k of Object.keys(a)) near(a[k], b[k], `${where}.${k}`); return undefined; }
    return assert.equal(a, b, where);
  };
  near(caseParts.KEYS.keys, caseJson.keys, 'KEYS');
  near(caseParts.RECESS.opening, caseJson.recess.opening, 'RECESS');
  near(caseParts.LEDS.leds, caseJson.leds.map(({ name, x, y, z }) => ({ name, x, y, z })), 'LEDS');
  assert.deepEqual(caseParts.REAR.ports.map((p) => p.ref), caseJson.rear.map((p) => p.ref));
  assert.deepEqual(caseParts.UNDERSIDE.connectors.map((u) => u.label), ['tube', '1MHz BUS', 'user port', 'printer', 'disc drive']);
  for (const [name, v] of Object.entries(caseParts)) assert.ok(typeof v.note === 'string' && v.note.length > 0, `${name} has no note`);
  assert.match(fs.readFileSync(path.join(process.cwd(), 'src', 'models', 'bbc-micro-case-parts.mjs'), 'utf8'), /^\/\/ GENERATED by tools\/bbc-micro-model\/case_parts\.py/);
  const { MODELS } = await import('../src/models/models.mjs');
  assert.ok(!Object.keys(MODELS).some((k) => k.startsWith('bbc-micro')));
});

test('the outside\'s photographs are committed, credited and are the resized originals; O8 and I6 are not committed', async () => {
  const { loadRegistry } = await import('../src/lib/registry.mjs');
  const bbc = loadRegistry().machines.find((m) => m.id === 'bbc-micro');
  const files = { O1: 'bbc-micro-top.webp', O2: 'bbc-micro-rear.webp', O3: 'bbc-micro-keyboard.webp', O4: 'bbc-micro-front.webp', O5: 'bbc-micro-left.webp' };
  for (const [id, file] of Object.entries(files)) {
    const s = sources.find((x) => x.id === id);
    assert.equal(s.committed, file);
    assert.equal(s.licence, 'Public domain');
    const p = bbc.photos.find((x) => x.file === file);
    assert.ok(p, `${file} is not credited in the registry`);
    assert.equal(p.sourceUrl, s.page);
  }
  for (const id of ['O8-I', 'O8-H', 'I6', 'K1', 'P-TE_1757819']) assert.equal(sources.find((x) => x.id === id).committed, null, id);
  assert.equal(bbc.photos[0].file, 'bbc-micro.webp');
  assert.match(fs.readFileSync(path.join(REPO_ROOT, 'NOTICE.md'), 'utf8'), /## The BBC Micro's key centres, from Dominic Beesley's keyboard \(MIT\)[\s\S]*github\.com\/dbhq-uk\/bbc-keyboard[\s\S]*Copyright \(c\) 2023 dominicbeesley/);
});
