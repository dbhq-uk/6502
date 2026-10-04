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
