import test from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import { MIN, PASS, STOP, verdicts } from './nes-spike-verdicts.mjs';

// The NES's two 3D models, each drawn for the NTSC and the PAL console: their
// inputs and the measurements they rest on. The tools in tools/nes-model/ run
// offline, by hand; these tests read what they committed.

const TOOL = path.join(REPO_ROOT, 'tools', 'nes-model');
const sources = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'sources.json'), 'utf8')).sources;
const spike = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'spike.json'), 'utf8'));

const KINDS = ['scan', 'photograph', 'drawing', 'document', 'model'];
const FIELDS = ['id', 'kind', 'file', 'url', 'page', 'author', 'licence', 'fetched', 'bytes', 'width', 'height', 'sha256', 'committed', 'use'];

test('every input in the NES models\' sources.json is fully described', () => {
  assert.ok(sources.length >= 13, `only ${sources.length} inputs`);
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

test('OpenTendo\'s inputs are read from the dbhq-uk fork at the pinned commit', () => {
  const PIN = '3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009';
  for (const id of ['I1-front', 'I1-back', 'I2']) {
    const s = sources.find((x) => x.id === id);
    assert.ok(s, `${id} is not in sources.json`);
    assert.ok(s.url.startsWith(`https://github.com/dbhq-uk/OpenTendo/raw/${PIN}/`), `${id}: ${s.url} is not the fork at the pinned commit`);
  }
});

test('the inputs\' hashes are the ones the research recorded in models.md', () => {
  const md = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'nes', 'facts', 'models.md'), 'utf8');
  const section = md.split(/^## Downloaded for the work$/m)[1];
  assert.ok(section, 'models.md has no "Downloaded for the work" section');
  const rows = [...section.matchAll(/^\| (\S+) \| `([^`]+)` \| (\d+) \| `([0-9a-f]{64})` \|$/gm)].map(([, id, file, bytes, sha]) => ({ id, file, bytes: Number(bytes), sha }));
  assert.ok(rows.length >= 13, `only ${rows.length} rows read from the table`);
  for (const r of rows) {
    const s = sources.find((x) => x.id === r.id);
    assert.ok(s, `${r.id} is in models.md but not in sources.json`);
    assert.deepEqual([s.file, s.bytes, s.sha256], [r.file, r.bytes, r.sha], `${r.id}: sources.json and models.md disagree`);
  }
  for (const s of sources) assert.ok(rows.some((r) => r.id === s.id), `${s.id} is not in models.md's table`);
});

// Hashes every file under tools/, site/src/assets/, site/public/ and docs/: an
// original copied anywhere a page or a tool could ship it fails.
test('no original NES input is committed, under tools/, site/src/assets/, site/public/ or docs/', () => {
  const originals = new Map(sources.map((s) => [s.sha256, s.id]));
  const skip = new Set(['node_modules', 'bin', 'obj', '__pycache__', '.pytest_cache', 'out']);
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

test('the site never runs the NES model tools', () => {
  const pkg = fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8');
  assert.doesNotMatch(pkg, /nes-model|python/);
});

test('spike.json records the verdicts its figures give against the plan\'s thresholds', () => {
  assert.deepEqual(spike.verdicts, verdicts(spike));
});

test('the PAL front is judged on O4 against O2-FL\'s front, on the ratios O4 shows; the patent\'s front is recorded', () => {
  const r = spike.palFront.ratios;
  assert.deepEqual(spike.palFront.ratioErrPct, r.filter((x) => x.O4 !== null).map((x) => x['O4AgainstO2-FLPct']));
  for (const x of r) assert.ok('patentFig3' in x && 'O4AgainstPatentPct' in x, `${x.ratio}: the patent's figure is not recorded`);
});

// Task 2: the board's frame on the bare scan I1-front (tools/nes-model/board_frame.py).
const frame = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'frame.json'), 'utf8'));

// The plan's scale rows (x, y, and x against y), the same numbers task 0's
// verdicts are judged on.
function frameVerdicts(f) {
  const v = (ok, stop) => (stop ? 'STOP' : ok ? 'pass' : 'between pass and stop');
  const x = f.heldOutX;
  const y = f.heldOutY;
  const ratioPct = 100 * Math.abs(f.pxPerMm.x / f.pxPerMm.y - 1);
  return [
    { check: 'scale x', verdict: v(x.scaledErrMm.median <= PASS.xMedian && x.n >= MIN.xRows, x.scaledErrMm.median > STOP.xMedian) },
    { check: 'scale y', verdict: v(y.errPct.median <= PASS.yMedianPct && y.footprints.length >= MIN.yFootprints, y.errPct.median > STOP.yMedianPct) },
    { check: 'x against y', verdict: v(true, ratioPct > STOP.ratioPct) },
  ];
}

const median = (a) => {
  const s = [...a].sort((p, q) => p - q);
  return s.length % 2 ? s[(s.length - 1) / 2] : (s[s.length / 2 - 1] + s[s.length / 2]) / 2;
};

test('frame.json records the verdicts its figures give, and its figures are the ones its rows give', () => {
  assert.deepEqual(frame.verdicts, frameVerdicts(frame));
  assert.ok(Math.abs(frame.ratio - frame.pxPerMm.x / frame.pxPerMm.y) < 1e-3);
  assert.ok(Math.abs(frame.ratioPct - 100 * Math.abs(frame.ratio - 1)) < 1e-2);
  assert.equal(frame.statedDpi, 300);
  // each row across is scaled to a 48.26 mm row, and scored only if 30 mm or longer
  for (const r of frame.heldOutX.rows) {
    assert.ok(Math.abs(r.scaledErrMm - (r.errMm * 48.26) / (r.pitches * 2.54)) < 2e-3, r.row);
    assert.equal(r.scored, r.pitches * 2.54 >= 30, r.row);
  }
  const scored = frame.heldOutX.rows.filter((r) => r.scored);
  assert.equal(frame.heldOutX.n, scored.length);
  assert.ok(Math.abs(frame.heldOutX.scaledErrMm.median - median(scored.map((r) => Math.abs(r.scaledErrMm)))) < 2e-3);
  assert.ok(Math.abs(frame.heldOutX.scaledErrMm.max - Math.max(...scored.map((r) => Math.abs(r.scaledErrMm)))) < 2e-3);
  const largest = scored.find((r) => r.row === frame.heldOutX.largestRow);
  assert.ok(largest && Math.abs(Math.abs(largest.scaledErrMm) - frame.heldOutX.scaledErrMm.max) < 2e-3, 'the largest row is named');
  assert.ok(!frame.heldOutX.rows.some((r) => r.row.startsWith('P1')), 'the edge fingers (2.50 mm) are not in the x scale');
  // y: each footprint's error a per cent of its own spacing; the two sets apart
  const fp = frame.heldOutY.footprints;
  assert.ok(Math.abs(frame.heldOutY.errPct.median - median(fp.map((f) => Math.abs(f.errPct)))) < 2e-3);
  assert.ok(Math.abs(frame.heldOutY.errPct.max - Math.max(...fp.map((f) => Math.abs(f.errPct)))) < 2e-3);
  for (const [key, spacing] of [['errPct600', 15.24], ['errPct300', 7.62]]) {
    const set = fp.filter((f) => f.spacingMm === spacing).map((f) => Math.abs(f.errPct));
    assert.equal(frame.heldOutY[key].n, set.length, key);
    assert.ok(Math.abs(frame.heldOutY[key].median - median(set)) < 2e-3, key);
  }
  // straightness is recorded, not judged: the rows at or over 0.05 mm are named
  assert.deepEqual(frame.straightness.over, frame.straightness.rows.filter((r) => r.rmsMm >= 0.05).map((r) => r.row));
});

test('frame.json passes the plan\'s x, y and x against y rows', () => {
  assert.deepEqual(frame.verdicts.map((v) => v.verdict), ['pass', 'pass', 'pass']);
});

test('frame.json\'s outline is closed, starts at the origin, and is the board\'s size, within 2 mm of the KiCad redrawing', () => {
  const o = frame.outline;
  assert.ok(o.length >= 5);
  assert.deepEqual(o[0], o[o.length - 1], 'the outline is closed');
  assert.deepEqual(o[0], [0, 0], 'it starts at the board\'s top left corner, the origin');
  const xs = o.map((p) => p[0]);
  const ys = o.map((p) => p[1]);
  const w = Math.max(...xs) - Math.min(...xs);
  const d = Math.max(...ys) - Math.min(...ys);
  assert.ok(Math.abs(w - frame.board.widthMm) <= 0.5, `outline width ${w} against ${frame.board.widthMm}`);
  assert.ok(Math.abs(d - frame.board.depthMm) <= 0.5, `outline depth ${d} against ${frame.board.depthMm}`);
  // a sanity bound: the KiCad file is a redrawing, compared and never drawn
  assert.ok(Math.abs(frame.kicad.widthMm - 196.252) < 1e-3 && Math.abs(frame.kicad.depthMm - 118.7) < 1e-3, 'the KiCad outline as read in task 0');
  assert.ok(Math.abs(frame.board.widthMm - frame.kicad.widthMm) <= 2.0, `width ${frame.board.widthMm} against KiCad ${frame.kicad.widthMm}`);
  assert.ok(Math.abs(frame.board.depthMm - frame.kicad.depthMm) <= 2.0, `depth ${frame.board.depthMm} against KiCad ${frame.kicad.depthMm}`);
  // the corners are on the outline, and every notch's circle crosses its edge
  for (const c of frame.corners) assert.ok(o.some((p) => Math.hypot(p[0] - c[0], p[1] - c[1]) < 1e-3), `corner ${c} is not on the outline`);
  assert.equal(frame.notches.length, 3);
});

test('frame.json\'s holes are inside the board, and its rectified copy is never committed', () => {
  assert.ok(frame.holes.length >= 4);
  for (const h of frame.holes) {
    assert.ok(h.x > 0 && h.x < frame.board.widthMm && h.y > 0 && h.y < frame.board.depthMm, JSON.stringify(h));
    assert.ok(h.d > 1.4 && h.d < 6, JSON.stringify(h));
  }
  assert.equal(frame.rectified.pxPerMm, 12);
  assert.ok(frame.rectified.file.startsWith('out/'));
  const ignore = fs.readFileSync(path.join(REPO_ROOT, '.gitignore'), 'utf8');
  assert.match(ignore, /^tools\/nes-model\/out\/$/m);
});

// Task 3: the solder side registered to the component side, and the pads,
// drills and footprints (tools/nes-model/board_register.py).
const registration = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'registration.json'), 'utf8'));
const footprintMarks = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'marks.json'), 'utf8')).footprints.marks;
// The plan's outlier rule, fixed before any measurement.
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

test('registration.json passes the plan\'s solder row on the figures without exclusion, over at least 150 holes, and records the largest', () => {
  const s = registration.solder;
  assert.ok(s.holes >= MIN.solderHoles, `only ${s.holes} holes matched`);
  assert.deepEqual(s.thresholds.pass, { median: PASS.solderMedian, p90: PASS.solderP90 });
  assert.deepEqual(s.thresholds.stop, { median: STOP.solderMedian, p90: STOP.solderP90 });
  assert.ok(['affine', 'cubic'].includes(s.model));
  assert.deepEqual(s.heldOutMm, s.fits[s.model].heldOutMm, 'the figures are the chosen model\'s');
  // the model with the lower held-out median; within 0.005 mm the affine
  const want = s.fits.cubic.heldOutMm.median < s.fits.affine.heldOutMm.median - 0.005 ? 'cubic' : 'affine';
  assert.equal(s.model, want);
  const h = s.heldOutMm;
  const verdict = h.median > STOP.solderMedian || h.p90 > STOP.solderP90 ? 'STOP'
    : h.median <= PASS.solderMedian && h.p90 <= PASS.solderP90 && s.holes >= MIN.solderHoles ? 'pass' : 'between pass and stop';
  assert.equal(s.verdict, verdict, 'the recorded verdict is the one the figures give');
  assert.equal(verdict, 'pass');
  assert.ok(Number.isFinite(h.max) && h.max >= h.p90, 'the largest is recorded');
  // the figures are the matched holes' own
  const rows = s.matched.rows;
  assert.equal(rows.length, s.holes);
  const col = s.matched.columns.indexOf('heldOutMm');
  const got = summary(rows.map((r) => r[col]));
  for (const k of ['median', 'p90', 'max']) assert.ok(Math.abs(got[k] - h[k]) < 1e-3, `${k}: ${got[k]} against ${h[k]}`);
});

test('the excluded holes are exactly those the roundness rule names, with the figures both with and without them', () => {
  const s = registration.solder;
  const o = s.outliers;
  assert.match(o.rule, /1\.25/);
  assert.match(o.rule, /0\.6 to 1\.6/);
  assert.match(o.rule, /no residual/);
  const c = Object.fromEntries(s.matched.columns.map((k, i) => [k, i]));
  const rows = s.matched.rows;
  const med = { top: percentile(rows.map((r) => r[c.topArea]), 50), bottom: percentile(rows.map((r) => r[c.bottomArea]), 50) };
  const near = (x, lim) => Math.abs(x - lim) < 2e-3; // rounded to 3 places on the limit: either way
  const named = [];
  rows.forEach((r, i) => {
    let fails = false;
    let unsure = false;
    for (const [face, axis, area] of [['top', r[c.topAxis], r[c.topArea]], ['bottom', r[c.bottomAxis], r[c.bottomArea]]]) {
      if (near(axis, ROUND_MAX_AXIS)) unsure = true;
      if (axis > ROUND_MAX_AXIS) fails = true;
      const ratio = area / med[face];
      if (near(ratio, ROUND_AREA[0]) || near(ratio, ROUND_AREA[1])) unsure = true;
      if (ratio < ROUND_AREA[0] || ratio > ROUND_AREA[1]) fails = true;
    }
    if (unsure) named.push(o.excluded.some((e) => e.hole === i) ? i : null);
    else if (fails) named.push(i);
  });
  assert.deepEqual(o.excluded.map((e) => e.hole), named.filter((i) => i !== null), 'the excluded holes are the ones the rule names');
  assert.equal(o.count, o.excluded.length);
  for (const e of o.excluded) assert.ok(e.why.length > 0 && e.why.every((w) => /^(top|bottom): (axis ratio \d+\.\d\d over 1\.25|area \d+\.\d\d times)/.test(w)), JSON.stringify(e));
  const kept = rows.filter((_, i) => !o.excluded.some((e) => e.hole === i)).map((r) => r[c.heldOutMm]);
  const w = summary(kept);
  for (const k of ['median', 'p90', 'max']) assert.ok(Math.abs(w[k] - o.heldOutMmWithExclusion[k]) < 1e-3, `with exclusion ${k}`);
});

test('every drill in registration.json has a pad on both faces within 0.2 mm', () => {
  const { drills, pads } = registration;
  assert.ok(drills.length >= MIN.solderHoles);
  // A drill is made only where its two faces agree within 0.4 mm, so its pads are within 0.2 mm by construction:
  // what can fail is the count. Every matched hole is a drill or is recorded as not drilled, and not drilled stays rare.
  const notDrilled = registration.notDrilled.pairs.length;
  assert.equal(drills.length + notDrilled, registration.solder.holes, 'every matched hole is a drill or recorded as not drilled');
  assert.ok(notDrilled <= 0.1 * registration.solder.holes, `${notDrilled} of ${registration.solder.holes} matched holes not drilled`);
  const byDrill = new Map();
  for (const p of pads) if (p.drill !== null) byDrill.set(p.drill, [...(byDrill.get(p.drill) ?? []), p]);
  drills.forEach((d, i) => {
    const mine = byDrill.get(i) ?? [];
    for (const face of ['top', 'bottom']) {
      assert.ok(mine.some((p) => p.face === face && Math.hypot(p.x - d.x, p.y - d.y) <= 0.2), `drill ${i} at ${d.x}, ${d.y} has no ${face} pad within 0.2 mm`);
    }
    assert.ok(d.spreadMm >= 0 && d.spreadMm <= 0.4, `drill ${i}: its faces ${d.spreadMm} mm apart`);
    // a diameter is what the open rims show: a via part filled with solder shows less than its drill (the smallest, 0.19 mm on
    // 5 Oct 2026, is one such via, a dome on one face and a dimple on the other), so the floor is 0.15 mm
    assert.ok(d.d === null ? d.filled === true : d.d > 0.15 && d.d < 2.5, `drill ${i}: diameter ${d.d}`);
  });
  for (const p of pads) {
    assert.ok(['round', 'square', 'oval', 'rect'].includes(p.shape) && ['top', 'bottom', 'both'].includes(p.face), JSON.stringify(p));
    assert.ok(p.x > -1 && p.x < frame.board.widthMm + 1 && p.y > -1 && p.y < frame.board.depthMm + 1, JSON.stringify(p));
  }
});

test('U1 to U10 each have a DIP footprint with the pin count the plan\'s Facts give, pin 1 first, its reference marked by hand', () => {
  const { footprints, pads } = registration;
  const PINS = { U1: 24, U2: 20, U3: 16, U4: 24, U5: 40, U6: 40, U7: 16, U8: 16, U9: 14, U10: 16 };
  for (const [ref, pins] of Object.entries(PINS)) {
    const f = footprints.filter((x) => x.ref === ref);
    assert.equal(f.length, 1, `${ref}: ${f.length} footprints`);
    assert.equal(f[0].kind, 'dip', ref);
    assert.equal(f[0].pins, pins, ref);
    const m = footprintMarks.find((x) => x.ref === ref);
    assert.ok(m && m.reason.length > 20 && m.crop && m.crop.box.length === 4, `${ref}: its mark, crop and reason`);
  }
  for (const f of footprints) assert.ok(['dip', 'connector', 'edge', 'crystal', 'axial', 'radial', 'other'].includes(f.kind), f.kind);
  for (const f of footprints.filter((x) => x.kind === 'dip')) {
    assert.equal(f.pads.length, f.pins);
    assert.equal(new Set(f.pads).size, f.pins, 'a pad is one pin');
    const p1 = pads[f.pads[0]];
    assert.ok(Math.hypot(p1.x - f.pin1[0], p1.y - f.pin1[1]) < 1e-3, `${f.ref}: pin 1 is its first pad`);
    if (f.ref) assert.ok(['print', 'square pad', 'marked by hand'].includes(f.pin1From), `${f.ref}: pin 1 from ${f.pin1From}`);
    // a hand mark's agreement with the grouping is said only where the grouping had a pin 1 of its own
    if ('pin1AgreesWithGrouping' in f) assert.equal(f.pin1AgreesWithGrouping === null, f.pin1FromGrouping === null, `${f.ref}: agreement`);
    // pin N/2 is (N/2 - 1) x 2.54 mm along the row from pin 1, and pin N faces pin 1
    const half = pads[f.pads[f.pins / 2 - 1]];
    const last = pads[f.pads[f.pins - 1]];
    assert.ok(Math.abs(Math.hypot(half.x - p1.x, half.y - p1.y) - (f.pins / 2 - 1) * 2.54) < 0.6, `${f.ref}: row length`);
    assert.ok(Math.abs(Math.hypot(last.x - p1.x, last.y - p1.y) - f.rowSpacingMm) < 0.6, `${f.ref}: row spacing`);
  }
  // every reference was marked by hand, once
  const refs = footprints.map((f) => f.ref).filter(Boolean);
  assert.equal(new Set(refs).size, refs.length, 'a reference twice');
  for (const r of refs) assert.ok(footprintMarks.some((m) => m.ref === r), `${r} has no mark`);
});

test('P1 has 72 fingers across both faces, 36 on each, on the 2.50 mm pitch', () => {
  const { footprints, pads } = registration;
  const f = footprints.filter((x) => x.ref === 'P1');
  assert.equal(f.length, 1);
  assert.equal(f[0].kind, 'edge');
  assert.equal(f[0].pins, 72);
  assert.equal(f[0].pads.length, 72);
  for (const face of ['top', 'bottom']) {
    const xs = f[0].pads.map((i) => pads[i]).filter((p) => p.face === face).map((p) => p.x).sort((a, b) => a - b);
    assert.equal(xs.length, 36, face);
    const inner = xs.slice(2, -1).map((x, i) => x - xs[i + 1]);
    assert.ok(Math.abs(inner.reduce((a, b) => a + b, 0) / inner.length - 2.5) < 0.02, `${face}: the inner fingers' pitch`);
    // and every gap between neighbouring inner fingers, one by one (the end fingers are wider, widened outwards)
    inner.forEach((g, i) => assert.ok(Math.abs(g - 2.5) < 0.15, `${face}: fingers ${i + 2} and ${i + 3} are ${g} mm apart`));
  }
});
