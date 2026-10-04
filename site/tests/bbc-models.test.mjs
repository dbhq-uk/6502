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

test('every drill in registration.json has a pad on both faces within 0.2 mm', () => {
  const { drills, pads } = registration;
  assert.ok(drills.length >= 200);
  const byDrill = new Map();
  for (const p of pads) if (p.drill !== null) byDrill.set(p.drill, [...(byDrill.get(p.drill) ?? []), p]);
  drills.forEach((d, i) => {
    const mine = byDrill.get(i) ?? [];
    const near = (p) => Math.hypot(p.x - d.x, p.y - d.y) <= 0.2;
    const both = mine.some((p) => p.face === 'both' && near(p));
    const top = mine.some((p) => p.face === 'top' && near(p));
    const bottom = mine.some((p) => p.face === 'bottom' && near(p));
    assert.ok(both || (top && bottom), `drill ${i} at ${d.x}, ${d.y} has no pad on both faces within 0.2 mm`);
    // the drill is the mean of where each face put the hole; how far apart they were is recorded
    assert.ok(Number.isFinite(d.spreadMm) && d.spreadMm >= 0 && d.spreadMm <= 1.0, `drill ${i}: faces ${d.spreadMm} mm apart, over the 1.0 mm match gate`);
    assert.ok(d.d === null ? d.filled === true : d.d > 0.2 && d.d < 2, `drill ${i}: diameter ${d.d}`);
  });
  for (const p of pads) {
    assert.ok(['round', 'square', 'oval'].includes(p.shape) && ['top', 'bottom', 'both'].includes(p.face));
    assert.ok(p.x > -1 && p.x < frame.board.widthMm + 1 && p.y > -1 && p.y < frame.board.depthMm + 1, JSON.stringify(p));
  }
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
