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
});
