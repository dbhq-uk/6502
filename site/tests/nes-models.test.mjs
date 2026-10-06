import test from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import { MIN, PASS, STOP, verdicts } from './nes-spike-verdicts.mjs';
import { page, visibleText, modelPanel } from './helpers.mjs';

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

// Task 4: the copper on both faces and the print, traced from the bare scans
// (tools/nes-model/board_trace.py), the plan's checks on them, and the track map.
const copper = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'copper.json'), 'utf8'));
const icTable = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'ic-table.json'), 'utf8'));
const NES_MAP = path.join(process.cwd(), 'src', 'assets', 'tracks', 'nes-famicom-board.webp');
const NES_MAP_BUDGET = 600_000;
// The plan's table, fixed before the measurements
const COPPER_ROWS = { coverage: [0.10, 0.50], drills: 0.95, chips: 8, share: 0.90 };

function copperVerdicts(c) {
  const ok = (b) => (b ? 'pass' : 'fail');
  const n = c.nets;
  return {
    coverage: ok(['top', 'bottom'].every((f) => c.coverage[f] >= COPPER_ROWS.coverage[0] && c.coverage[f] <= COPPER_ROWS.coverage[1])),
    drillsInCopper: ok(['top', 'bottom'].every((f) => c.drillsInCopper[f] >= COPPER_ROWS.drills)),
    nets: ok(n.chips >= COPPER_ROWS.chips && !n.touching
      && ['gnd', 'vcc'].every((k) => n[k].pins > 0 && n[k].inLargest >= COPPER_ROWS.share * n[k].pins - 1e-9)),
  };
}

test('the NES board\'s track map is committed, WebP, the size copper.json gives, inside its budget, its three channels apart', async () => {
  const sharp = (await import('sharp')).default;
  assert.ok(fs.existsSync(NES_MAP), 'src/assets/tracks/nes-famicom-board.webp is missing');
  const bytes = fs.readFileSync(NES_MAP);
  assert.ok(bytes.length <= NES_MAP_BUDGET, `the track map is ${bytes.length} bytes, over its ${NES_MAP_BUDGET} budget`);
  assert.equal(bytes.length, copper.map.bytes);
  assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), copper.map.sha256, 'copper.json\'s map.sha256 is not the committed map\'s');
  assert.equal((await sharp(bytes).metadata()).format, 'webp');
  assert.ok([10, 9, 8, 7, 6].includes(copper.mapPxPerMm));
  const { data, info } = await sharp(bytes).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  assert.deepEqual([info.width, info.height], [copper.map.width, copper.map.height]);
  // edge to edge in the board frame: the traced grid, which covers the outline's box, at the map's resolution
  assert.equal(info.width, Math.round((copper.grid.width / copper.grid.pxPerMm) * copper.mapPxPerMm));
  assert.equal(info.height, Math.round((copper.grid.height / copper.grid.pxPerMm) * copper.mapPxPerMm));
  assert.ok(Math.abs(copper.grid.width / copper.grid.pxPerMm - frame.board.widthMm) < 0.1, 'the grid is the board\'s width');
  assert.ok(Math.abs(copper.grid.height / copper.grid.pxPerMm - frame.board.depthMm) < 0.1, 'the grid is the board\'s depth');
  // red the component side, green the solder side, blue the print; each 255 or 0
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

test('copper.json records the verdicts its figures give against the plan\'s copper, drills and nets rows', () => {
  assert.deepEqual(copper.verdicts, copperVerdicts(copper));
  for (const f of ['top', 'bottom', 'print']) assert.ok(copper.coverage[f] > 0 && copper.coverage[f] < 1, f);
  assert.equal(copper.drills.n, registration.drills.length, 'every drill task 3 made is checked');
  for (const f of ['top', 'bottom']) {
    const share = 1 - copper.drills.notInCopper[f].length / copper.drills.n;
    assert.ok(Math.abs(share - copper.drillsInCopper[f]) < 1e-3, `${f}: the share is the drills' own`);
  }
  // every IC of the table is either in the nets check or left out with a reason
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
  assert.match(copper.thresholds.nets, /at least 8 ICs/);
});

test('copper.json passes the plan\'s coverage and drills rows, and its nets row stays the FAIL the plan\'s revision records', () => {
  assert.equal(copper.verdicts.coverage, 'pass');
  assert.equal(copper.verdicts.drillsInCopper, 'pass');
  // The plan's Known nets row, revised on 5 Oct 2026 after task 4's figures were seen (the controller's ruling after
  // review): task 4's result is FAIL, kept as measured (10 ICs: GND 1 of 10 pins in the largest net, +5V 2 of 10, a GND
  // and a +5V pin in one net), and the map is to look at, its connections not verified. A re-run that changes this
  // verdict must change that revision, the journal and every sentence that describes the copper, so it is pinned here.
  assert.equal(copper.verdicts.nets, 'fail');
  assert.deepEqual([copper.nets.chips, copper.nets.gnd, copper.nets.vcc, copper.nets.touching],
    [10, { pins: 10, inLargest: 1 }, { pins: 10, inLargest: 2 }, true]);
  const plan = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'superpowers', 'plans', '2026-10-05-nes-models.md'), 'utf8');
  const row = plan.split('\n').find((l) => l.startsWith('  | Known nets |')) ?? '';
  assert.match(row, /Revised on 5 Oct 2026, after task 4's figures were seen/);
  assert.match(row, /\*\*FAIL\*\* \(10 ICs: GND 1 of 10 pins in the largest net, \+5V 2 of 10/);
});

test('ic-table.json gives each IC its GND and +5V pins, each pinout with its source', () => {
  const PINS = { U1: 24, U2: 20, U3: 16, U4: 24, U5: 40, U6: 40, U7: 16, U8: 16, U9: 14, U10: 16 };
  assert.deepEqual(Object.fromEntries(icTable.ics.map((i) => [i.ref, i.pins])), PINS);
  for (const ic of icTable.ics) {
    for (const k of ['ref', 'role', 'pins', 'package', 'gnd', 'vcc', 'pinoutSource']) assert.ok(k in ic, `${ic.ref}: ${k}`);
    assert.ok(Number.isInteger(ic.gnd) && Number.isInteger(ic.vcc) && ic.gnd !== ic.vcc, ic.ref);
    assert.ok(ic.gnd >= 1 && ic.gnd <= ic.pins && ic.vcc >= 1 && ic.vcc <= ic.pins, ic.ref);
    assert.ok(ic.package.startsWith(`DIP-${ic.pins} `), ic.ref);
    assert.match(ic.pinoutSource, /https:\/\/\S+/, `${ic.ref}: its source is named with its address`);
    assert.match(ic.pinoutSource, new RegExp(`pin ${ic.gnd} `), `${ic.ref}: the source's GND pin`);
  }
  // the large chips from the nesdev wiki's pinout pages, the 6116 from its data sheet
  for (const [ref, page] of [['U6', 'CPU_pinout'], ['U5', 'PPU_pinout'], ['U10', 'CIC_lockout_chip_pinout']]) {
    assert.ok(icTable.ics.find((i) => i.ref === ref).pinoutSource.includes(`nesdev.org/wiki/${page}`), ref);
  }
  assert.deepEqual(['U6', 'U5'].map((r) => { const i = icTable.ics.find((x) => x.ref === r); return [i.gnd, i.vcc]; }), [[20, 40], [20, 40]]);
});

test('NOTICE.md and the photographs\' README name the NES track map, its TAPR Open Hardware License terms and OpenTendo', () => {
  const notice = fs.readFileSync(path.join(REPO_ROOT, 'NOTICE.md'), 'utf8');
  const flat = (t) => t.replace(/\s+/g, ' ');
  const section = flat(notice.split(/^## /m).find((s) => s.includes('nes-famicom-board.webp')) ?? '');
  assert.ok(section, 'NOTICE.md does not name site/src/assets/tracks/nes-famicom-board.webp');
  assert.match(section, /TAPR Open Hardware License/);
  assert.match(section, /OpenTendo/);
  assert.match(section, /traced from OpenTendo's scans/);
  assert.match(section, /dbhq-uk\/OpenTendo/);
  assert.match(section, /3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/);
  const photos = fs.readFileSync(path.join(process.cwd(), 'src', 'assets', 'photos', 'README.md'), 'utf8');
  const ours = flat(photos.split(/^## /m).find((s) => s.includes('nes-famicom-board.webp')) ?? '');
  assert.match(ours, /traced from OpenTendo's scans/);
  assert.match(ours, /TAPR Open Hardware License/);
});

// --- task 5: the parts, placed from the scan and named for both consoles -----------------------

const parts = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'parts.json'), 'utf8'));
const PARTS_MJS = path.join(process.cwd(), 'src', 'models', 'nes-famicom-board-parts.mjs');
// The counters the machine keeps, in the host's order (NesChip in C#). Task 7
// creates site/src/models/nes-famicom-access.mjs with this list as COUNTED; once
// it is there, the test below holds the two the same.
const COUNTED = ['ppu', 'apu', 'pad1', 'pad2'];
// Why an IC is never marked: the plan's list, and 'inverter' for U9, the hex
// inverter (the plan's task 5 interface, revised on 5 Oct 2026).
const ALWAYS = ['cpu', 'ram', 'decoder', 'latch', 'cartridge', 'lockout', 'inverter'];

function insidePolygon(poly, [x, y]) {
  let c = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [x1, y1] = poly[j];
    const [x2, y2] = poly[i];
    if ((y1 > y) !== (y2 > y) && x < ((x2 - x1) * (y - y1)) / (y2 - y1) + x1) c = !c;
  }
  return c;
}

function corners({ x, y, l, w, rotation }) {
  const t = (rotation * Math.PI) / 180;
  const u = [(Math.cos(t) * l) / 2, (Math.sin(t) * l) / 2];
  const v = [(-Math.sin(t) * w) / 2, (Math.cos(t) * w) / 2];
  return [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([a, b]) => [x + a * u[0] + b * v[0], y + a * u[1] + b * v[1]]);
}

function within(body, [px, py]) {
  const t = (body.rotation * Math.PI) / 180;
  const dx = px - body.x;
  const dy = py - body.y;
  return Math.abs(dx * Math.cos(t) + dy * Math.sin(t)) <= body.l / 2 && Math.abs(-dx * Math.sin(t) + dy * Math.cos(t)) <= body.w / 2;
}

function edgeDistance(poly, [x, y]) {
  let best = Infinity;
  for (let i = 0; i < poly.length; i++) {
    const [ax, ay] = poly[i];
    const [bx, by] = poly[(i + 1) % poly.length];
    const dx = bx - ax;
    const dy = by - ay;
    const t = Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy || 1)));
    best = Math.min(best, Math.hypot(x - ax - t * dx, y - ay - t * dy));
  }
  return best;
}

function overlaps(a, b) {
  const A = corners(a);
  const B = corners(b);
  for (const P of [A, B]) {
    for (let i = 0; i < 4; i++) {
      const e = [P[(i + 1) % 4][0] - P[i][0], P[(i + 1) % 4][1] - P[i][1]];
      const n = [-e[1], e[0]];
      const pa = A.map((p) => p[0] * n[0] + p[1] * n[1]);
      const pb = B.map((p) => p[0] * n[0] + p[1] * n[1]);
      if (Math.max(...pa) <= Math.min(...pb) + 1e-9 || Math.max(...pb) <= Math.min(...pa) + 1e-9) return false;
    }
  }
  return true;
}

// The similarity (turn, one scale, shift) taking `src` onto `dst` that fits best, by least squares.
function similarity(src, dst) {
  const n = src.length;
  const mean = (P) => [P.reduce((s, p) => s + p[0], 0) / n, P.reduce((s, p) => s + p[1], 0) / n];
  const [sx, sy] = mean(src);
  const [dx, dy] = mean(dst);
  let a = 0;
  let b = 0;
  let q = 0;
  for (let i = 0; i < n; i++) {
    const u = src[i][0] - sx;
    const v = src[i][1] - sy;
    const p = dst[i][0] - dx;
    const r = dst[i][1] - dy;
    a += u * p + v * r;
    b += u * r - v * p;
    q += u * u + v * v;
  }
  a /= q;
  b /= q;
  return (p) => [a * (p[0] - sx) - b * (p[1] - sy) + dx, b * (p[0] - sx) + a * (p[1] - sy) + dy];
}

const ics = parts.model.ics;
const footprintOf = (name) => registration.footprints.find((f) => f.ref === name);

test('every IC sits on a footprint whose pad count is its pins, and the model draws each IC in ic-table.json once', () => {
  assert.deepEqual(ics.map((i) => i.ref), icTable.ics.map((i) => i.ref));
  assert.deepEqual(parts.problems, []);
  for (const ic of ics) {
    const t = icTable.ics.find((i) => i.ref === ic.ref);
    const place = parts.places[ic.ref];
    const f = footprintOf(place.footprint);
    assert.ok(place.footprint === ic.ref || place.footprint.startsWith(`${ic.ref} (`), `${ic.ref} sits on ${place.footprint}`);
    assert.equal(ic.pins, t.pins, ic.ref);
    assert.equal(f.pads.length, ic.pins, `${ic.ref}: ${f.pads.length} pads on ${place.footprint}, ${ic.pins} pins`);
    assert.equal(place.padCount, f.pads.length, ic.ref);
    // its place is its pads' centre
    const c = f.pads.reduce((s, k) => [s[0] + registration.pads[k].x / f.pads.length, s[1] + registration.pads[k].y / f.pads.length], [0, 0]);
    assert.ok(Math.hypot(c[0] - ic.x, c[1] - ic.y) < 0.002, `${ic.ref}: its place is its pads' centre`);
  }
});

test('each console\'s part is the one ic-table.json gives, read off its own photograph, and the crystal\'s too', () => {
  for (const ic of ics) {
    const t = icTable.ics.find((i) => i.ref === ic.ref);
    assert.deepEqual(ic.parts, { ntsc: t.parts.ntsc.part, pal: t.parts.pal.part }, ic.ref);
    assert.deepEqual([t.parts.ntsc.seen, t.parts.pal.seen], ['I4', 'I3'], ic.ref);
    for (const r of ['ntsc', 'pal']) assert.match(t.parts[r].part, /^[0-9A-Z][0-9A-Z-]+$/, `${ic.ref} ${r}`);
  }
  // the consoles' CPUs, PPUs and lockout chips differ; the plan's Facts
  const part = (ref, r) => ics.find((i) => i.ref === ref).parts[r];
  assert.deepEqual(['U6', 'U5', 'U10'].map((r) => [part(r, 'ntsc'), part(r, 'pal')]),
    [['RP2A03G', 'RP2A07A'], ['RP2C02G-0', 'RP2C07-0'], ['3193A', '3195A']]);
  const x1 = icTable.crystals.find((c) => c.ref === 'X1');
  for (const r of ['ntsc', 'pal']) {
    const drawn = parts.model.others[r].find((o) => o.kind === 'crystal');
    assert.equal(drawn.part, x1.parts[r].part, r);
  }
  assert.deepEqual([x1.parts.ntsc.seen, x1.parts.pal.seen], ['I4', 'I3']);
});

test('every IC has a chip or an always, never both; every chip is one of COUNTED; U7 is pad1 and U8 pad2, by the print, checked in task 6', async () => {
  for (const ic of ics) {
    assert.ok((ic.chip === null) !== (ic.always === null), `${ic.ref}: chip ${ic.chip}, always ${ic.always}`);
    if (ic.chip !== null) assert.ok(COUNTED.includes(ic.chip), `${ic.ref}: ${ic.chip}`);
    if (ic.always !== null) assert.ok(ALWAYS.includes(ic.always), `${ic.ref}: ${ic.always}`);
  }
  const chip = Object.fromEntries(ics.map((i) => [i.ref, i.chip]));
  assert.deepEqual([chip.U6, chip.U5, chip.U7, chip.U8], ['apu', 'ppu', 'pad1', 'pad2']);
  assert.deepEqual(ics.filter((i) => i.chip).map((i) => i.chip).sort(), [...COUNTED].sort(), 'each counter marks one IC');
  for (const ref of ['U7', 'U8']) {
    const t = icTable.ics.find((i) => i.ref === ref);
    assert.equal(t.port.inferred, true, `${ref}: the port it serves is inferred: checked against the wiki and the redrawing in task 6, not traced on the scan`);
    assert.equal(t.port.value, ref === 'U7' ? 1 : 2);
  }
  assert.match(parts.chipInferred, /inferred/);
  const access = path.join(process.cwd(), 'src', 'models', 'nes-famicom-access.mjs');
  if (fs.existsSync(access)) assert.deepEqual((await import(access)).COUNTED, COUNTED);
});

test('every part is inside the board\'s outline; a body that runs past it is one the photographs show doing so, and is recorded', () => {
  const { outline } = parts.model.board;
  for (const ic of ics) for (const c of corners(ic)) assert.ok(insidePolygon(outline, c), `${ic.ref}: corner ${c}`);
  for (const p of parts.passives.parts) {
    assert.ok(insidePolygon(outline, [p.x, p.y]), `${p.ref}`);
    for (const l of p.leads) assert.ok(insidePolygon(outline, l), `${p.ref}: lead ${l}`);
  }
  const over = [];
  const held = [...parts.model.connectors.map((c) => [c, c.ref]), ...['ntsc', 'pal'].flatMap((r) => parts.model.others[r].map((o) => [o, `${o.ref} (${r})`]))];
  for (const [c, name] of held) {
    const f = footprintOf(c.ref);
    for (const k of f.pads) assert.ok(insidePolygon(outline, [registration.pads[k].x, registration.pads[k].y]), `${name}: its pad ${k}`);
    if (corners(c).some((p) => !insidePolygon(outline, p))) {
      over.push(name);
      // A body over the edge is anchored on its own pads: it covers every pad of its footprint, and its nearest pad is within
      // 15 mm of the edge. 15 mm was chosen after seeing the data (5 Oct 2026, task 5's review): the overhanging bodies' nearest
      // pads lie 1.7 (P1), 4.8 (P4), 4.9 (P5), 8.9 (P6) and 11.9 mm (P3, the modulator's pins) from the edge, and an IC's or
      // the expansion socket's 18 mm or more; so it holds what the photographs show and refuses a box slid off its pads.
      const pads = f.pads.map((k) => [registration.pads[k].x, registration.pads[k].y]);
      for (const q of pads) assert.ok(within(c, q), `${name} does not cover its pad at ${q}`);
      const near = Math.min(...pads.map((q) => edgeDistance(outline, q)));
      assert.ok(near <= 15, `${name}: its nearest pad is ${near.toFixed(2)} mm from the edge`);
    }
  }
  assert.deepEqual(over.sort(), [...parts.overhang.refs].sort());
});

test('no two IC bodies overlap', () => {
  for (let i = 0; i < ics.length; i++) for (let j = i + 1; j < ics.length; j++) assert.ok(!overlaps(ics[i], ics[j]), `${ics[i].ref} and ${ics[j].ref}`);
});

test('the ICs\' places against the KiCad redrawing\'s, after a best-fit similarity: none more than 3 mm off', () => {
  const refs = ics.map((i) => i.ref);
  const src = refs.map((r) => parts.kicad.places[r]);
  const dst = ics.map((i) => [i.x, i.y]);
  const S = similarity(src, dst);
  const off = Object.fromEntries(refs.map((r, k) => { const g = S(src[k]); return [r, Math.hypot(g[0] - dst[k][0], g[1] - dst[k][1])]; }));
  const worst = Math.max(...Object.values(off));
  assert.ok(worst <= 3.0, `${worst.toFixed(3)} mm`);
  assert.ok(Math.abs(worst - parts.kicad.maxMm) < 0.002, `recorded ${parts.kicad.maxMm}, recomputed ${worst.toFixed(4)}`);
  for (const r of refs) assert.ok(Math.abs(off[r] - parts.kicad.perPart[r].offMm) < 0.002, r);
  assert.equal(parts.kicad.verdict, 'pass');
  // each U is on its own redrawn footprint: the redrawing's value names the same part
  assert.equal(parts.kicad.limitMm, 3.0);
});

test('each part of the CPU-07 sits on the CPU-10 footprint of the same reference, and every check parts.json records passes', () => {
  for (const [ref, s] of Object.entries(parts.sitsOn.ics)) {
    // the CPU-07 (I4, I5) and the PAL board (I3) alike, each IC within the limit of the CPU-10 footprint of its name
    assert.ok(s.I4 <= parts.sitsOn.limitMm && s.I5 <= parts.sitsOn.limitMm && s.I3 <= parts.sitsOn.limitMm, `${ref}: ${s.I4}, ${s.I5}, ${s.I3}`);
    assert.equal(s.verdict, 'sits on it', ref);
  }
  assert.equal(parts.sitsOn.limitMm, 2.0);
  for (const [ref, s] of Object.entries(parts.sitsOn.others)) assert.equal(s.verdict, 'sits on it', ref);
  assert.deepEqual(parts.verdicts.map((v) => v.verdict), parts.verdicts.map(() => 'pass'));
  // the RAMs are on their 300 mil footprints: on the 600 mil ones they would be over 3 mm off on I4
  assert.ok(parts.photographs.I4.otherWidthMm.U1 > 3 && parts.photographs.I4.otherWidthMm.U4 > 3);
});

test('the board parts module is generated from parts.json, names its sources, and agrees with it', async () => {
  const text = fs.readFileSync(PARTS_MJS, 'utf8');
  assert.match(text, /^\/\/ GENERATED by tools\/nes-model\/board_parts\.py/);
  const head = text.split('\nexport ')[0];
  for (const s of parts.model.sources) assert.ok(head.includes(s), `the header names ${s}`);
  const m = await import(PARTS_MJS);
  const want = { BOARD: 'board', ICS: 'ics', CONNECTORS: 'connectors', PASSIVES: 'passives', OTHERS: 'others', HEIGHTS: 'heights' };
  assert.deepEqual(Object.keys(m).sort(), Object.keys(want).sort());
  for (const [name, key] of Object.entries(want)) assert.deepEqual(m[name], parts.model[key], name);
  assert.deepEqual(Object.keys(m.OTHERS).sort(), ['ntsc', 'pal']);
  assert.equal(m.BOARD.thickness, 1.6);
  assert.equal(m.HEIGHTS.board, 1.6);
  for (const [k, h] of Object.entries(m.HEIGHTS)) if (k !== 'board') assert.equal(typeof h.measured, 'boolean', k);
  for (const p of m.PASSIVES) assert.ok(['axial', 'radial', 'other'].includes(p.kind));
});

// --- task 8: the case, measured, for both consoles -------------------------------------------

const caseData = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'case.json'), 'utf8'));
const CASE_MJS = path.join(process.cwd(), 'src', 'models', 'nes-famicom-case-parts.mjs');
const CASE_EXPORTS = ['CASE', 'PROFILE', 'DOOR', 'VENTS', 'BUTTONS', 'LED', 'PORTS', 'REAR', 'LABELS', 'UNDERSIDE', 'FEET'];

test('case.json\'s size is the published figures, said not to be Nintendo\'s, and agrees with spike.json and its verdicts', () => {
  const c = caseData;
  assert.equal(c.footprint.widthMm, 254);
  assert.equal(c.footprint.depthMm, spike.case.patent.published.depthMm);
  assert.equal(c.heightMm, spike.case.patent.published.heightMm);
  assert.equal(c.footprint.depthFrom, 'published');
  assert.equal(c.heightFrom, 'published');
  assert.equal(c.footprint.notNintendos, true);
  assert.match(c.footprint.widthSource, /not Nintendo's/);
  const v = Object.fromEntries(spike.verdicts.map((x) => [x.check, x.verdict]));
  assert.equal(c.spike.caseDepth, v['case depth']);
  assert.equal(c.spike.caseHeight, v['case height']);
  assert.equal(c.spike.palFront, v['PAL front']);
  for (const k of ['caseDepth', 'caseHeight', 'palFront']) assert.notEqual(c.spike[k], 'STOP', k);
});

const onCase = (what, lo, hi, tol = 0.5) => {
  for (let i = 0; i < 3; i++) assert.ok(lo[i] >= -tol && hi[i] <= [254, 203.2, caseData.heightMm + caseData.feetMm][i] + tol, `${what}: ${lo} to ${hi}`);
};

test('every feature of the case lies on the case', () => {
  const c = caseData;
  const top = c.heightMm + c.feetMm;
  const front = (what, b) => onCase(what, [b.x, 203.2, b.z], [b.x + b.w, 203.2, b.z + b.h]);
  front('door', c.door.front);
  front('panel', c.panel);
  front('LED', c.led);
  for (const b of c.buttons) front(b.name, b);
  for (const p of c.ports) front(`port ${p.name}`, p);
  onCase('the door on the top', [c.door.top.x, c.door.top.y, top], [c.door.top.x + c.door.top.w, c.door.top.y + c.door.top.d, top]);
  for (const r of ['ntsc', 'pal']) {
    for (const x of c.rear[r]) {
      if (x.face === 'rear') onCase(x.label, [x.x, 0, x.z], [x.x + x.w, 0, x.z + x.h]);
      else onCase(x.label, [x.x - x.d / 2, x.y - x.d / 2, x.z - x.d / 2], [x.x, x.y + x.d / 2, x.z + x.d / 2]);
    }
    const u = c.underside[r];
    for (const f of u.feet) onCase('a foot', [f.x - f.d / 2, f.y - f.d / 2, 0], [f.x + f.d / 2, f.y + f.d / 2, 0]);
    for (const b of [u.cover, u.coverInner, ...u.panels, ...u.straps]) onCase('an underside part', [b.x, b.y, 0], [b.x + b.w, b.y + b.d, 0], 1.5);
    for (const l of c.labels[r]) {
      const [a, b, w, h] = l.box;
      if (l.face === 'bottom') onCase(l.words, [a, b, 0], [a + w, b + h, 0]);
      else onCase(l.words, [a, 0, b], [a + w, 0, b + h]);
    }
  }
  for (const v of c.vents) onCase(`vents, ${v.face}`, [v.x, v.y, 0], [v.x + v.w, v.y + v.d, 0]);
  for (const p of c.profile) assert.ok(p.inset >= 0 && p.inset < 127 && p.z >= 0 && p.z <= top + 0.01, JSON.stringify(p));
  const b = c.boardInCase;
  onCase('the board', [b.x, b.y - 119.445, b.z - 1.6], [b.x + 195.95, b.y, b.z]);
});

test('the buttons, LED and ports are in the order the photographs show, and the rear\'s connectors too', () => {
  const c = caseData;
  const [power, reset] = c.buttons;
  assert.deepEqual(c.buttons.map((b) => b.name), ['POWER', 'RESET']);
  assert.deepEqual(c.ports.map((p) => p.name), ['1', '2']);
  const xs = [c.led, power, reset, ...c.ports].map((f) => f.x);
  assert.deepEqual(xs, [...xs].sort((p, q) => p - q), 'left to right: the LED, POWER, RESET, port 1, port 2');
  assert.ok(c.led.x + c.led.w <= power.x && power.x + power.w <= reset.x && reset.x + reset.w <= c.ports[0].x && c.ports[0].x + c.ports[0].w <= c.ports[1].x);
  // seen from behind, left to right: AC ADAPTER, CH3-CH4, RF SWITCH, so x falls
  const rear = c.rear.ntsc.filter((x) => x.face === 'rear');
  assert.deepEqual(rear.map((x) => x.label), ['AC ADAPTER', 'CH3-CH4', 'RF SWITCH']);
  assert.ok(rear[0].x > rear[1].x && rear[1].x > rear[2].x);
  // the AV jacks: video nearer the rear than audio
  const side = Object.fromEntries(c.rear.ntsc.filter((x) => x.face === 'right').map((x) => [x.label, x]));
  assert.ok(side.VIDEO.y < side.AUDIO.y);
});

test('each rear connector\'s place on the board plus the board\'s offset is recorded against where O2-BR shows it, judged at 2 mm as measured', () => {
  const c = caseData;
  const b = c.boardInCase;
  const t = (b.turnDeg * Math.PI) / 180;
  assert.equal(c.rearCheck.limitMm, 2.0);
  let within = 0;
  let worst = 0;
  const checked = c.rear.ntsc.filter((x) => 'checkMm' in x);
  assert.equal(checked.length, 3);
  for (const x of checked) {
    const bx = x.board.x;
    const by = -x.board.y;
    const viaBoard = b.x + Math.cos(t) * bx - Math.sin(t) * by;
    assert.ok(Math.abs(viaBoard - x.boardPlusOffsetX) < 0.05, `${x.label}: ${viaBoard} against ${x.boardPlusOffsetX}`);
    const err = Math.abs(x.boardPlusOffsetX - x.centre.x);
    assert.ok(Math.abs(err - x.checkMm) < 0.02, x.label);
    assert.equal(x.within, err <= 2.0, x.label);
    within += x.within ? 1 : 0;
    worst = Math.max(worst, err);
    // the window's opening holds the connector the model draws
    assert.ok(x.centre.x >= x.x && x.centre.x <= x.x + x.w, x.label);
  }
  assert.equal(c.rearCheck.within, within);
  assert.equal(c.rearCheck.checked, checked.length);
  assert.ok(Math.abs(c.rearCheck.worstMm - worst) < 0.02);
  // where the check misses, case.json says what the model uses instead
  if (within < checked.length) assert.match(c.rearCheck.placedFrom, /O2-BR/);
  // Pinned as measured on 5 Oct 2026 and accepted as failed by the controller:
  // a re-run that moves these must change the plan's and the page's words too.
  assert.equal(c.rearCheck.within, 1);
  assert.equal(c.rearCheck.worstMm, 5.16);
  assert.deepEqual(checked.map((x) => [x.label, x.checkMm]), [['AC ADAPTER', 5.16], ['CH3-CH4', 3.5], ['RF SWITCH', 0.85]]);
  const plan = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'superpowers', 'plans', '2026-10-05-nes-models.md'), 'utf8');
  assert.ok(plan.includes('The rear connectors\' check failed as measured'));
  // the uncertainty the page will state: the places used against the patent, the board's miss
  const used = Math.max(...checked.map((x) => Math.abs(x.centre.x - x.patentX)));
  assert.equal(c.rearCheck.uncertaintyMm.placesUsed, Math.round(used * 10) / 10);
  assert.equal(c.rearCheck.uncertaintyMm.boardMiss, worst > 0 ? Math.round(worst * 100) / 100 : 0);
  assert.equal(c.rearCheck.words, `O2-BR and the patent's rear view agree within ${c.rearCheck.uncertaintyMm.placesUsed.toFixed(1)} mm; the board's places miss by up to ${worst.toFixed(1)} mm; the check failed as measured`);
  // what is not checked says so, here and in the module
  for (const r of ['ntsc', 'pal']) for (const x of c.rear[r]) {
    assert.equal(typeof x.checked, 'boolean', `${r} ${x.label}`);
    assert.equal(x.checked, r === 'ntsc' && x.face === 'rear', `${r} ${x.label}`);
    if (!x.checked) assert.ok(x.checkedWhy.length > 0, `${r} ${x.label}`);
  }
  assert.ok(c.model.REAR.note.endsWith(c.rearCheck.words));
});

test('the profile\'s held-out check passes within 2 mm, or the patent\'s fallback is used and flagged', () => {
  const p = caseData.profileCheck;
  assert.equal(p.limitMm, 2.0);
  assert.equal(p.passes, p.errMm <= p.limitMm);
  assert.equal(caseData.profileFrom, p.passes ? 'photographs' : 'patent');
  assert.equal(p.fallbackUsed, !p.passes);
  if (!p.passes) assert.deepEqual(caseData.profile, caseData.profilePatent);
  // the ends' spread at the base, which the held-out figure does not show (pinned, 5 Oct 2026)
  assert.deepEqual(p.endsMm, { min: 12.86, max: 17.71 });
  const each = caseData.profileEachEnd;
  const ends = [each['O2-FL'].left.inset, each['O2-FL'].right.inset, each['O2-BR'].caseRight.inset, each['O2-BR'].caseLeft.inset];
  assert.equal(p.endsMm.min, Math.min(...ends));
  assert.equal(p.endsMm.max, Math.max(...ends));
  assert.match(p.endsWhat, /good to about 2\.5 mm/);
  assert.match(caseData.model.PROFILE.note, /not that it is the true inset/);
  assert.ok(caseData.profilePatent.every((q) => q.z > caseData.feetMm), 'the patent profile\'s row at the base is left out');
  const zs = caseData.profile.map((q) => q.z);
  assert.equal(Math.max(...zs), Math.round((caseData.heightMm + caseData.feetMm) * 100) / 100);
});

test('the case\'s labels are words, and no label, export or module has an image, a path or a logo', async () => {
  for (const r of ['ntsc', 'pal']) {
    for (const l of caseData.labels[r]) {
      for (const k of Object.keys(l)) assert.ok(['words', 'box', 'face', 'from'].includes(k), `${r}: ${l.words} has ${k}`);
      assert.equal(typeof l.words, 'string');
      assert.ok(l.words.trim().length > 0);
      assert.doesNotMatch(l.words, /[<>{}\\]/);
      assert.equal(l.box.length, 4);
      for (const v of l.box) assert.equal(typeof v, 'number');
    }
  }
  const text = fs.readFileSync(CASE_MJS, 'utf8');
  assert.doesNotMatch(text, /<svg|<path|\bpath\s*:|"path"|\.png|\.webp|\.jpe?g|\.svg|data:image|logo/i);
  const m = await import(CASE_MJS);
  for (const r of ['ntsc', 'pal']) for (const l of m.LABELS[r]) assert.deepEqual(Object.keys(l).sort(), ['box', 'face', 'words']);
});

test('the PAL console\'s differences from the NTSC console are listed, and its words are its own', () => {
  const c = caseData;
  assert.ok(Array.isArray(c.palDifferences) && c.palDifferences.length >= 3);
  const words = (r) => c.labels[r].map((l) => l.words);
  assert.ok(words('pal').includes('EUROPEAN VERSION') && !words('ntsc').includes('EUROPEAN VERSION'));
  assert.ok(words('ntsc').includes('AC ADAPTER') && !words('pal').includes('AC ADAPTER'));
  assert.ok(words('pal').some((w) => /ANSCHLUSS ANTENNE/.test(w)));
  assert.ok(c.labels.pal.some((l) => l.face === 'bottom') && !c.labels.ntsc.some((l) => l.face === 'bottom'));
  assert.ok(c.palDifferences.some((d) => /EUROPEAN VERSION/.test(d)) && c.palDifferences.some((d) => /German/.test(d)));
  assert.equal(c.palFront.verdict, spike.verdicts.find((v) => v.check === 'PAL front').verdict);
});

test('whether POWER latches is said, and marked a guess when nothing shows it', () => {
  const p = caseData.powerLatch;
  assert.equal(typeof p.seen, 'boolean');
  if (!p.seen) assert.match(p.what, /\[guessing - verify\]/);
  for (const b of caseData.buttons) assert.ok(['typical', 'photographs'].includes(b.travelFrom), b.name);
});

test('the case parts module is generated from case.json, names its sources, and agrees with it', async () => {
  const text = fs.readFileSync(CASE_MJS, 'utf8');
  assert.match(text, /^\/\/ GENERATED by tools\/nes-model\/case_measure\.py/);
  const head = text.split('\nexport ')[0];
  for (const s of caseData.sources) assert.ok(head.includes(s), `the header names ${s}`);
  assert.doesNotMatch(text, /#[0-9a-fA-F]{3,8}\b|\b(rgb|hsl)a?\(/);
  const m = await import(CASE_MJS);
  assert.deepEqual(Object.keys(m).sort(), [...CASE_EXPORTS].sort());
  for (const name of CASE_EXPORTS) {
    assert.deepEqual(m[name], caseData.model[name], name);
    assert.equal(typeof m[name].note, 'string', `${name} has a source note`);
  }
  for (const name of ['REAR', 'LABELS', 'UNDERSIDE']) for (const r of ['ntsc', 'pal']) assert.ok(r in m[name], `${name}.${r}`);
  assert.equal(m.CASE.width, caseData.footprint.widthMm);
  assert.equal(m.CASE.depth, caseData.footprint.depthMm);
  assert.equal(m.CASE.height, caseData.heightMm);
  for (const s of caseData.sources) assert.ok(sources.some((x) => x.id === s), `${s} is in sources.json`);
});

// --- task 7: the inside model on the page --------------------------------------------------

const BOARD_ID = 'nes-famicom-board';
const { MODELS, wordsFor } = await import('../src/models/models.mjs');
const layout = await import('../src/models/nes-famicom-board-layout.mjs');
const notes = await import('../src/models/nes-famicom-board-notes.mjs');
const access = await import('../src/models/nes-famicom-access.mjs');
const { registry } = await import('../src/lib/data.mjs');
const boardEntry = MODELS[BOARD_ID];
const boardFigures = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src', 'data', `${BOARD_ID}-model.json`), 'utf8'));
const boardModule = fs.readFileSync(path.join(process.cwd(), 'src', 'models', `${BOARD_ID}.js`), 'utf8');
const nesRow = registry.machines.find((m) => m.id === 'nes');
const nesHtml = page('/machines/nes/')?.html ?? '';
// The board's part of the page: its view's tab panel since task 9 (helpers.mjs, modelPanel).
const boardSection = modelPanel(nesHtml, BOARD_ID);
const frameJson = frame;
const WORD = ['no', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten'];

test('the NES claims the inside model, and the entry is the plan\'s: the machine, the view, both consoles, the track map and the legend', () => {
  // Task 9 of the plan: case is back, with both views, the outside first (6 Oct 2026; task 7 had the inside alone).
  assert.deepEqual(nesRow.models, [{ view: 'outside', module: 'nes-famicom-case' }, { view: 'inside', module: BOARD_ID }]);
  assert.equal(nesRow.case, true);
  assert.equal(boardEntry.machine, 'nes');
  assert.equal(boardEntry.view, 'inside');
  assert.deepEqual(boardEntry.regions, ['ntsc', 'pal']);
  assert.equal(boardEntry.texture, layout.TRACKS.src);
  assert.equal(boardEntry.legend, layout.chipLegend);
  for (const r of boardEntry.regions) assert.equal(typeof boardEntry.made[r], 'function', r);
});

test('the regions the board model draws are the regions the NES page offers, both ways', () => {
  // The region control in the machine's panel (src/components/NesPanel.astro): input[name="nes-region"][data-nes-region].
  const controls = [...nesHtml.matchAll(/<input\b[^>]*\bname="nes-region"[^>]*>/g)].map((m) => m[0]);
  assert.ok(controls.length > 0, 'the NES page has no region control');
  assert.ok(controls.every((c) => /\bdata-nes-region\b/.test(c)));
  const values = controls.map((c) => /\bvalue="([^"]+)"/.exec(c)[1].toLowerCase());
  assert.deepEqual([...values].sort(), [...boardEntry.regions].sort());
  assert.deepEqual(values, boardEntry.regions, 'the first region is the one the page starts on');
});

test('the model draws every IC, connector, passive and both consoles\' crystal and modulator from the parts file', () => {
  assert.deepEqual(layout.ICS, parts.model.ics);
  assert.deepEqual(layout.CONNECTORS, parts.model.connectors);
  assert.deepEqual(layout.PASSIVES, parts.model.passives);
  assert.deepEqual(layout.OTHERS, parts.model.others);
  assert.match(boardModule, /for \(const ic of ICS\) \{/);
  assert.match(boardModule, /for \(const c of CONNECTORS\) \{/);
  assert.match(boardModule, /for \(const p of PASSIVES\) \{/);
  assert.match(boardModule, /for \(const region of REGIONS\) \{\s*const group = new Group\(\);\s*for \(const o of OTHERS\[region\]\)/);
  // The ICs are soldered in: drawn as bodies on their legs, with no sockets.
  assert.doesNotMatch(boardModule, /socket/i);
  // The board is its measured outline with its holes, not a box.
  assert.match(boardModule, /ring\(BOARD\.outline\)/);
  assert.match(boardModule, /BOARD\.holes\.map/);
});

test('the track map is copper.json\'s, read as the component side\'s copper in red, the solder side\'s in green and the print in blue', () => {
  assert.equal(layout.TRACKS.src, `/models/${BOARD_ID}-tracks.webp`);
  assert.deepEqual([layout.TRACKS.width, layout.TRACKS.height, layout.TRACKS.pxPerMm], [copper.map.width, copper.map.height, copper.mapPxPerMm]);
  assert.deepEqual(layout.TRACKS.originMm, copper.map.originMm);
  assert.match(boardModule, /top: \{ copperOf: \(d, i\) => d\[i\], printOf: \(d, i\) => d\[i \+ 2\], mask: token\('model-nes-pcb'\), copper: token\('model-copper'\), print: token\('model-nes-print'\) \}/);
  assert.match(boardModule, /bottom: \{ copperOf: \(d, i\) => d\[i \+ 1\], printOf: null, mask: token\('model-nes-pcb-under'\)/);
  assert.match(boardModule, /new MeshStandardMaterial\(\{ map: texture\(m\.colour, true\), roughnessMap: surface, metalnessMap: surface, roughness: 1, metalness: 1, bumpMap: texture\(m\.relief, false\), bumpScale: RELIEF \}\)/);
  assert.doesNotMatch(boardModule, /emissive/, 'the tracks glow');
});

test('the access mark and the board\'s colours are tokens of their own, and the mark is not the lime', () => {
  const tokens = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'tokens.css'), 'utf8');
  const value = (name) => new RegExp(`--${name}:\\s*(#[0-9a-f]{6});`).exec(tokens)?.[1];
  for (const name of ['model-nes-pcb', 'model-nes-pcb-under', 'model-nes-print', 'model-active']) assert.ok(value(name), `tokens.css has no --${name}`);
  assert.notEqual(value('model-active'), value('lime'));
  assert.match(boardModule, /token\('model-active'\)/);
});

test('the legend lists every IC once, in reference order, with both consoles\' parts from ic-table.json', () => {
  const refs = icTable.ics.map((i) => i.ref).sort((a, b) => Number(a.slice(1)) - Number(b.slice(1)));
  for (const region of boardEntry.regions) {
    const other = boardEntry.regions.find((r) => r !== region);
    const rows = layout.chipLegend(region);
    assert.deepEqual(rows.map((r) => r.ref), refs, region);
    for (const row of rows) {
      const t = icTable.ics.find((i) => i.ref === row.ref);
      assert.equal(row.part, t.parts[region].part, `${row.ref} ${region}`);
      assert.equal(row.otherPart, t.parts[other].part, `${row.ref} in the other console`);
      assert.equal(row.role, t.role);
      const ic = parts.model.ics.find((i) => i.ref === row.ref);
      assert.deepEqual([row.chip, row.always], [ic.chip, ic.always]);
      assert.ok(row.chip ? /^when the processor/.test(row.mark) : /^never: /.test(row.mark), `${row.ref}: ${row.mark}`);
    }
  }
  // On the page: one row a chip, each console's part in it, the first console's shown.
  const legend = /<div class="model-legend prose" data-model-legend>([\s\S]*?)<\/div>/.exec(boardSection)?.[1] ?? '';
  assert.ok(legend, 'the NES page has no chip legend');
  const rows = [...legend.matchAll(/<tr data-legend-ref="(U\d+)"[^>]*>([\s\S]*?)<\/tr>/g)];
  assert.deepEqual(rows.map((m) => m[1]), refs);
  for (const [, ref, cells] of rows) {
    for (const region of boardEntry.regions) {
      const part = icTable.ics.find((i) => i.ref === ref).parts[region].part;
      assert.ok(cells.includes(`<span data-model-region="${region}"${region === boardEntry.regions[0] ? '' : ' hidden'}>${part}</span>`), `${ref}: ${region}'s part`);
    }
    const chip = parts.model.ics.find((i) => i.ref === ref).chip;
    assert.equal(/data-legend-rate="(\w+)"/.exec(cells)?.[1] ?? null, chip, `${ref}: its rate cell`);
  }
  // Each counted chip has exactly one row to take its rate, in the machine's order.
  assert.deepEqual(access.COUNTED.map((c) => rows.filter(([, , cells]) => cells.includes(`data-legend-rate="${c}"`)).length), [1, 1, 1, 1]);
});

test('the legend says in words what a mark means, why some chips are never marked, where U9\'s jobs and U7\'s and U8\'s ports come from, and that the heights are typical', () => {
  const words = layout.LEGEND_WORDS.join(' ');
  assert.deepEqual(boardEntry.legendWords, layout.LEGEND_WORDS);
  assert.match(words, /read or written by the processor in the last quarter second/);
  assert.match(words, /not that the chip is working/);
  assert.match(words, /The CPU, the work RAM, the address decoder and the cartridge are in use all the time; the video RAM and the address latch are the PPU's own, in use whenever it draws; and the lockout chip is not emulated/);
  // The CPU's row is the one of them that is marked, for the registers on its own chip that ChipAccesses counts as Apu
  // ($4000 to $4015, the sprite copy's $4014 among them, and the writes to $4016 and $4017): fix round 1, 6 Oct 2026.
  assert.doesNotMatch(words, /Some chips are never marked\. The CPU/);
  assert.match(words, /all but the CPU are never marked\. The CPU's row is marked only when the processor uses the sound, sprite-copy and controller registers on its own chip \(\$4000 to \$4015, and the writes to \$4016 and \$4017\)/);
  const cs = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Nes', 'ChipAccesses.cs'), 'utf8');
  assert.match(cs, />= 0x4000 and <= 0x4015 => NesChip\.Apu/);
  assert.equal(layout.COUNTED_WORDS.apu, 'when the processor uses the sound, sprite-copy and controller registers on its own chip');
  assert.match(words, /By the redrawing's nets, U9, the hex inverter, inverts the PPU's address line A13 and the reset line and clocks the lockout chip, so it is in use all the time and is never marked/);
  assert.match(words, /OpenTendo's KiCad redrawing of the board, a cross-check, and are not traced on the scan/);
  assert.match(words, /U7 and U8 are the controller ports' buffers\. Which serves which port is by the board's print/);
  assert.match(words, /checked against the nesdev wiki and the KiCad redrawing, and is not traced on the scan/);
  assert.match(words, /The heights of the parts are typical ones, not measured/);
  // U7 is port 1 and U8 port 2 in the legend as in ic-table.json.
  for (const ref of ['U7', 'U8']) assert.match(words, new RegExp(`${ref} (is )?port ${icTable.ics.find((i) => i.ref === ref).port.value}`));
  // Every reason an IC is never marked has its words, and the page shows them all.
  for (const ic of parts.model.ics.filter((i) => i.always)) assert.ok(layout.NEVER[ic.always], ic.always);
  const text = visibleText(boardSection);
  for (const w of layout.LEGEND_WORDS) assert.ok(text.includes(visibleText(w)), `the page does not say: ${w}`);
});

test('each console\'s caption starts by saying it is a model of that console, and every figure in it is the layout\'s', () => {
  const cm = (mm) => (mm / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  const start = { ntsc: 'Model, not a photograph, of an NTSC NES-001, the front-loading console sold in North America', pal: 'Model, not a photograph, of a PAL NESE-001, the front-loading console sold in Europe' };
  for (const region of boardEntry.regions) {
    const about = wordsFor(boardEntry, region).about;
    assert.equal(about, layout.describe(region));
    assert.ok(about.startsWith(start[region]), `${region}: ${about.slice(0, 90)}`);
    assert.ok(about.includes(`${cm(parts.model.board.width)} by ${cm(parts.model.board.depth)} cm`));
    for (const ref of ['U6', 'U5', 'U10']) assert.ok(about.includes(parts.model.ics.find((i) => i.ref === ref).parts[region]), `${region}: ${ref}`);
    assert.ok(about.includes(parts.model.others[region].find((o) => o.kind === 'crystal').part));
    assert.match(about, /not one real board/);
    assert.match(about, /the heights of the parts are typical ones, not measured/);
    assert.match(about, /to look at: the copper's connections are not verified/);
  }
  // Each console's board: the NTSC parts on the scanned NES-CPU-10, the PAL parts read off an NES-CPU-11 and drawn on
  // the CPU-10's layout, which the PAL board was checked to share (fix round 1, 6 Oct 2026).
  assert.match(layout.describe('pal'), /drawn on an NES-CPU-10, whose layout the PAL console's NES-CPU-11 was checked to share/);
  assert.doesNotMatch(layout.describe('ntsc'), /CPU-11/);
  assert.match(layout.describe('ntsc'), /three\.js, an NES-CPU-10, /);
  // Every height the parts file gives is typical, so the caption may say all of them are.
  assert.ok(Object.entries(parts.model.heights).every(([k, h]) => k === 'board' || h.measured === false));
});

test('the results file is written from frame.json, registration.json, copper.json and parts.json, and agrees with them', () => {
  const f = boardFigures;
  assert.deepEqual(f.board, { widthMm: frameJson.board.widthMm, depthMm: frameJson.board.depthMm, dpi: frameJson.statedDpi });
  assert.deepEqual(f.scale, { xMedianMm: frameJson.heldOutX.scaledErrMm.median, xRows: frameJson.heldOutX.n, yMedianPct: frameJson.heldOutY.errPct.median, yFootprints: frameJson.heldOutY.footprints.length, xyPct: frameJson.ratioPct, verdicts: frameJson.verdicts.map((v) => v.verdict) });
  assert.deepEqual(f.solder, { holes: registration.solder.holes, heldOutMedianMm: registration.solder.heldOutMm.median, heldOutP90Mm: registration.solder.heldOutMm.p90, verdict: registration.solder.verdict });
  assert.deepEqual(f.copper, {
    top: copper.coverage.top, bottom: copper.coverage.bottom, drillsTop: copper.drillsInCopper.top, drillsBottom: copper.drillsInCopper.bottom, drills: copper.drills.n, mapPxPerMm: copper.mapPxPerMm,
    nets: { chips: copper.nets.chips, gndPins: copper.nets.gnd.pins, gndInLargest: copper.nets.gnd.inLargest, vccPins: copper.nets.vcc.pins, vccInLargest: copper.nets.vcc.inLargest, touching: copper.nets.touching, verdict: copper.verdicts.nets },
  });
  const sits = Object.values(parts.sitsOn.ics);
  const heights = Object.entries(parts.model.heights).filter(([k]) => k !== 'board').map(([, h]) => h);
  assert.deepEqual(f.parts, {
    ics: parts.model.ics.length, passives: parts.model.passives.length, kicadMaxMm: parts.kicad.maxMm, kicadMedianMm: parts.kicad.medianMm,
    ntscWorstMm: Math.max(...sits.map((x) => Math.max(x.I4, x.I5))), palWorstMm: Math.max(...sits.map((x) => x.I3)), sitsOnLimitMm: parts.sitsOn.limitMm,
    heightsTypical: heights.filter((h) => !h.measured).length, heightsMeasured: heights.filter((h) => h.measured).length,
  });
  assert.deepEqual(f.sources.map((x) => x.id), [...parts.model.sources, 'I2']);
  for (const x of f.sources) {
    const s = sources.find((y) => y.id === x.id);
    assert.deepEqual(x, { id: s.id, url: s.url, page: s.page, sha256: s.sha256, kind: s.kind });
  }
  // results.py writes it; no URL is in the tool's code but in comments.
  assert.match(fs.readFileSync(path.join(TOOL, 'results.py'), 'utf8'), /nes-famicom-board-model\.json/);
});

test('each console\'s note is made() from the results file, word for word on the page, and states the nets check\'s failure as copper.json has it', () => {
  const text = visibleText(boardSection);
  const notesOnPage = [...boardSection.matchAll(/<div class="model-made prose" data-model-made data-model-region="([a-z]+)"[^>]*>([\s\S]*?)<\/div>/g)];
  assert.deepEqual(notesOnPage.map((m) => m[1]), boardEntry.regions);
  for (const [, region, note] of notesOnPage) {
    const { paragraphs, limits } = notes.made(boardFigures, region);
    assert.deepEqual(boardEntry.made[region](boardFigures), { paragraphs, limits });
    const t = visibleText(note);
    for (const p of [...paragraphs, ...limits]) assert.ok(t.includes(visibleText(p)), `${region}: the page does not say: ${p}`);
  }
  // The nets result, from copper.json itself, and that the copper is not a netlist.
  const n = copper.nets;
  const sentence = notes.copperSentence(boardFigures);
  assert.ok(sentence.includes(`${WORD[n.gnd.inLargest]} of ${WORD[n.gnd.pins]} ground pins fell in the largest ground net`));
  assert.ok(sentence.includes(`${WORD[n.vcc.inLargest]} of ${WORD[n.vcc.pins]} +5V pins in the largest +5V net`));
  assert.equal(copper.verdicts.nets, 'fail');
  assert.match(sentence, /failed/);
  assert.match(sentence, /not a netlist/);
  assert.ok(n.touching && /a ground pin and a \+5V pin came out in the same net/.test(sentence));
  assert.ok(text.includes(visibleText(sentence)));
  // A figure the note gives is one the results file holds.
  const pct = (v) => `${Math.round(v * 100)}%`;
  for (const v of [boardFigures.copper.top, boardFigures.copper.bottom]) assert.ok(text.includes(pct(v)), `${pct(v)} is not on the page`);
  for (const v of [boardFigures.solder.heldOutMedianMm, boardFigures.solder.heldOutP90Mm, boardFigures.parts.kicadMaxMm, boardFigures.parts.ntscWorstMm, boardFigures.parts.palWorstMm]) assert.ok(text.includes(`${v.toLocaleString('en-GB', { maximumFractionDigits: 3 })} mm`), `${v} mm is not on the page`);
});

test('every sentence on the page about the copper says it is traced to look at and its connections are not verified', () => {
  // The caption, the help, the legend's words, the notes and their limits: the words the section shows.
  const blocks = [
    ...[...boardSection.matchAll(/<p class="figure-caption model-about"[^>]*>([\s\S]*?)<\/p>/g)].map((m) => m[1]),
    /<p class="model-help"[^>]*>([\s\S]*?)<\/p>/.exec(boardSection)?.[1] ?? '',
    ...[...boardSection.matchAll(/<(?:p|li)>([\s\S]*?)<\/(?:p|li)>/g)].map((m) => m[1]),
  ].map(visibleText);
  const sentences = blocks.flatMap((b) => b.split(/(?<=\.)\s+(?=[A-Z])/)).filter((x) => /copper/i.test(x));
  assert.ok(sentences.length >= 6, `only ${sentences.length} sentences about the copper were found`);
  for (const x of sentences) assert.ok(/to look at|not a netlist/.test(x) && /not verified|failed/.test(x), `a sentence about the copper does not say it is traced to look at and not verified: ${x}`);
  // So do the status lines the model writes about the tracks.
  const code = boardModule.replace(/^\s*\/\/.*$/gm, '');
  // Its sentences: a capital to a full stop, in quotes on one line.
  const said = [...code.matchAll(/'([A-Z][^'\n]*\.)'/g)].map((m) => m[1]).filter((x) => /copper/i.test(x));
  assert.ok(said.length > 0, 'the model says nothing about the copper on its status line');
  for (const x of said) assert.match(x, /to look at[\s\S]*not verified/, x);
});

test('every source the board used is credited in the registry by its address, and every reference the NES has is one the board used', () => {
  const refs = nesRow.references ?? [];
  const board = boardFigures.sources;
  const of = (r) => board.find((x) => r.sourceUrl === x.url || r.sourceUrl === x.page);
  for (const r of refs) {
    const x = of(r);
    assert.ok(x, `the reference ${r.sourceUrl} is not a source of the board`);
    const s = sources.find((y) => y.id === x.id);
    assert.equal(r.sha256, s.sha256, `${x.id}: the SHA-256 is not the original's`);
    assert.equal(r.licence, s.licence, `${x.id}: the licence is not as sources.json records it`);
    assert.equal(r.fetched, s.fetched, `${x.id}: fetched`);
    // OpenTendo's are credited at the fork this project read them from, and name OpenTendo.
    if (x.url.includes('/dbhq-uk/OpenTendo/')) {
      assert.equal(r.sourceUrl, x.url);
      assert.match(`${r.title} ${r.author}`, /OpenTendo/);
    }
  }
  assert.deepEqual(board.map((x) => refs.filter((r) => of(r) === x).length), board.map(() => 1), 'a source of the board is credited not once');
  // Which buffer serves which port is by the board's print, checked against the wiki and the redrawing, never read
  // from the redrawing (the controller's ruling; fix round 1, 6 Oct 2026).
  assert.match('U9\'s jobs and which buffer serves which controller port read from its nets', /(buffer|port)[^.;,]*read from/, 'the check would not catch the old words');
  for (const r of refs) assert.doesNotMatch(r.used, /(buffer|port)[^.;,]*read from|read from[^.;,]*(buffer|port)/, `${r.title}: ${r.used}`);
  // On the page, the note credits exactly those, in the registry's order, and not the photograph at the head of the page.
  for (const [, , note] of boardSection.matchAll(/<div class="model-made prose" data-model-made data-model-region="([a-z]+)"[^>]*>([\s\S]*?)<\/div>/g)) {
    const items = [...note.matchAll(/<li data-model-source>([\s\S]*?)<\/li>/g)].map((m) => m[1]);
    assert.equal(items.length, refs.length);
    refs.forEach((r, i) => {
      assert.ok(items[i].includes(`href="${r.sourceUrl}"`), `credit ${i + 1} does not link ${r.sourceUrl}`);
      assert.ok(visibleText(items[i]).includes(`by ${r.author}`));
    });
    assert.ok(!note.includes(nesRow.photos[0].sourceUrl), 'the photograph at the head of the page is credited as a source of the board');
  }
});

test('the model reads the machine through panel.nes, takes a fresh baseline on nes:start and nes:region, and swaps every console\'s words together', () => {
  assert.match(boardModule, /document\.querySelector\('\[data-nes\]'\)/);
  // The region, upper case as the host gives it, lower-cased in one place.
  assert.equal([...boardModule.matchAll(/toLowerCase\(\)/g)].length, 1);
  assert.match(boardModule, /const regionNow = \(\) => nes\(\)\?\.region\?\.\(\)\?\.toLowerCase\(\) \?\? REGIONS\[0\];/);
  assert.match(boardModule, /createSampler\(\{ counts: \(\) => nes\(\)\?\.accessCounts\?\.\(\) \?\? null, now: \(\) => performance\.now\(\) \}\)/);
  assert.match(boardModule, /panel\?\.addEventListener\('nes:start', \(\) => \{\s*running = true;\s*setRegion\(regionNow\(\)\);\s*restart\(\);/);
  assert.match(boardModule, /panel\?\.addEventListener\('nes:region', \(e\) => \{\s*setRegion\(e\.detail\?\.region\);\s*restart\(\);/);
  assert.match(boardModule, /const restart = \(\) => \{\s*sampler\.reset\(\);\s*show\(null\);/);
  assert.match(boardModule, /setInterval\(\(\) => \{\s*if \(!running\) return;\s*show\(sampler\.sample\(\)\);\s*\}, SAMPLE_MS\);/);
  assert.match(boardModule, /const SAMPLE_MS = 250;/);
  // The caption, the note and the legend's parts switch together, the names stay hidden, and the stage's name and description follow.
  assert.match(boardModule, /for \(const el of root\.querySelectorAll\('\[data-model-region\]'\)\) if \(!el\.hasAttribute\('data-model-label'\)\) el\.hidden = el\.dataset\.modelRegion !== region;/);
  assert.match(boardModule, /stage\.setAttribute\('aria-label', name\)/);
  // The caption's own id, which carries the view's suffix in a tab panel (task 9): found by its console, not built.
  assert.match(boardModule, /const about = root\.querySelector\(`\.model-about\[data-model-region="\$\{region\}"\]`\);\s*if \(about\) stage\.setAttribute\('aria-describedby', about\.id\);/);
  // The test hooks the browser check reads.
  for (const hook of ['root.dataset.modelRegion', 'root.dataset.modelAccessed', 'root.dataset.modelRates', 'root.dataset.modelTracks', 'root.dataset.modelParts', 'root.modelChipPoint = ', 'root.modelBoardPoint = (x, y, below = false) =>']) assert.ok(boardModule.includes(hook), hook);
  // Pointing at a chip names it and marks its row.
  assert.match(boardModule, /row\.toggleAttribute\('data-pointed', r === ref\)/);
  assert.match(boardModule, /row\.toggleAttribute\('data-accessed', refs\.includes\(ref\)\)/);
});

// --- task 9: the outside model, and both models as views ------------------------------------

const CASE_ID = 'nes-famicom-case';
const caseEntry = MODELS[CASE_ID];
const caseLayout = await import('../src/models/nes-famicom-case-layout.mjs');
const caseNotes = await import('../src/models/nes-famicom-case-notes.mjs');
const caseFigures = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src', 'data', `${CASE_ID}-model.json`), 'utf8'));
const caseModule = fs.readFileSync(path.join(process.cwd(), 'src', 'models', `${CASE_ID}.js`), 'utf8');
const caseSection = modelPanel(nesHtml, CASE_ID);
const caseText = visibleText(caseSection);
const mm2 = (v) => v.toLocaleString('en-GB', { maximumFractionDigits: 2 });

test('the outside model\'s entry is the plan\'s: the NES, the outside view, both consoles, each caption naming its console first', () => {
  assert.equal(caseEntry.machine, 'nes');
  assert.equal(caseEntry.view, 'outside');
  assert.deepEqual(caseEntry.regions, ['ntsc', 'pal']);
  assert.ok(caseSection.startsWith('<div class="model-panel" role="tabpanel" id="model-panel-outside"'), 'the outside is not the first view\'s panel');
  const start = { ntsc: 'Model, not a photograph, of an NTSC NES-001, the front-loading console sold in North America', pal: 'Model, not a photograph, of a PAL NESE-001, the front-loading console sold in Europe' };
  for (const r of caseEntry.regions) {
    const { about, label } = wordsFor(caseEntry, r);
    assert.equal(about, caseLayout.describe(r));
    assert.ok(about.startsWith(start[r]), `${r}: ${about.slice(0, 90)}`);
    assert.match(about, /the published size, which is not Nintendo's own/);
    assert.match(label, new RegExp(`${r === 'pal' ? 'a PAL NESE-001' : 'an NTSC NES-001'}\\. Click POWER`));
    assert.equal(typeof caseEntry.made[r], 'function');
  }
  assert.match(caseLayout.describe('pal'), /own words, read from photographs of a PAL console: EUROPEAN VERSION, /);
  assert.match(caseLayout.describe('pal'), /yellowed; the model draws it as made/);
});

test('the regions the outside model draws are the regions the NES page offers, both ways', () => {
  const values = [...nesHtml.matchAll(/<input\b[^>]*\bname="nes-region"[^>]*>/g)].map((m) => /\bvalue="([^"]+)"/.exec(m[0])[1].toLowerCase());
  assert.deepEqual(values, caseEntry.regions, 'the first region is the one the page starts on');
  assert.deepEqual(caseLayout.REGIONS, caseEntry.regions);
});

test('the case\'s sizes are case.json\'s, and its results file is case.json\'s figures and its sources', () => {
  assert.deepEqual([caseLayout.CASE.width, caseLayout.CASE.depth, caseLayout.CASE.height], [caseData.footprint.widthMm, caseData.footprint.depthMm, caseData.heightMm]);
  const f = caseFigures;
  assert.deepEqual([f.size.widthMm, f.size.depthMm, f.size.heightMm, f.size.from, f.size.notNintendos], [caseData.footprint.widthMm, caseData.footprint.depthMm, caseData.heightMm, caseData.heightFrom, true]);
  assert.match(caseData.footprint.widthSource, new RegExp(`good to about ${f.size.goodToPct} per cent`));
  assert.deepEqual(f.profile.endsMm, caseData.profileCheck.endsMm);
  assert.equal(f.profile.heldOutMm, caseData.profileCheck.errMm);
  assert.equal(f.profile.insetMm, caseLayout.PROFILE.points.at(-1).inset);
  assert.match(caseData.profileCheck.endsWhat, new RegExp(`good to about ${f.profile.goodToMm} mm`));
  assert.deepEqual([f.rear.placesUsedMm, f.rear.boardMissMm, f.rear.within, f.rear.checked, f.rear.worstMm, f.rear.words], [caseData.rearCheck.uncertaintyMm.placesUsed, caseData.rearCheck.uncertaintyMm.boardMiss, caseData.rearCheck.within, caseData.rearCheck.checked, caseData.rearCheck.worstMm, caseData.rearCheck.words]);
  assert.deepEqual([f.rear.placesUsedMm, f.rear.boardMissMm], [1.3, 5.16], 'the rear check is not as measured on 5 October 2026');
  assert.equal(f.boardInCase.uncertaintyMm, caseData.boardInCase.uncertaintyMm);
  assert.equal(f.boardInCase.sharedMoulding, true);
  assert.deepEqual([f.buttons.travelMm, f.buttons.travelFrom], [caseData.buttons[0].travelMm, 'typical']);
  assert.equal(f.powerLatchSeen, caseData.powerLatch.seen);
  for (const id of ['O4', 'O10']) assert.equal(sources.find((s) => s.id === id).focalLengthMm, f.palLensMm, id);
  // O9's own XMP, in case.json, is the same camera at the same focal length.
  assert.equal(caseData.boardInCase.o9.xmp['exif:FocalLength'], `${f.palLensMm * 10}/10`);
  assert.deepEqual(f.sources.map((x) => x.id), caseData.sources);
  for (const x of f.sources) {
    const s = sources.find((y) => y.id === x.id);
    assert.deepEqual(x, { id: s.id, url: s.url, page: s.page, sha256: s.sha256, kind: s.kind });
  }
  assert.match(fs.readFileSync(path.join(TOOL, 'results.py'), 'utf8'), /nes-famicom-case-model\.json/);
});

test('every feature of the case lies on it: on its faces, inside its footprint and its height', () => {
  const { CASE, DOOR, BUTTONS, LED, PORTS, REAR, LABELS, UNDERSIDE, FEET, VENTS } = caseLayout;
  const top = CASE.height + CASE.feet;
  const across = (x, w, what) => assert.ok(x >= 0 && x + w <= CASE.width, `${what}: x ${x} to ${x + w} is off the case`);
  const up = (z, h, what) => assert.ok(z >= CASE.feet - 1e-9 && z + h <= top + 1e-9, `${what}: z ${z} to ${z + h} is off the case`);
  const along = (y, d, what) => assert.ok(y >= 0 && y + d <= CASE.depth + 1e-9, `${what}: y ${y} to ${y + d} is off the case`);
  for (const [what, f] of [['the door', DOOR.front], ['the LED', LED], ...BUTTONS.list.map((b) => [b.name, b]), ...PORTS.list.map((p) => [`port ${p.name}`, p])]) { across(f.x, f.w, what); up(f.z, f.h, what); }
  across(DOOR.top.x, DOOR.top.w, 'the door\'s top'); along(DOOR.top.y, DOOR.top.d, 'the door\'s top');
  for (const v of VENTS.list) { across(v.x, v.w, 'a vent'); along(v.y, v.d, 'a vent'); }
  for (const r of caseLayout.REGIONS) {
    for (const c of REAR[r]) {
      if (c.face === 'rear') { across(c.x, c.w, c.label); up(c.z, c.h, c.label); } else { across(c.x, 0, c.label); up(c.z, 0, c.label); along(c.y, 0, c.label); }
    }
    for (const l of LABELS[r]) {
      const [x, a, w, b] = l.box;
      across(x, w, l.words);
      if (l.face === 'bottom') along(a, b, l.words); else up(a, b, l.words);
    }
    const u = UNDERSIDE[r];
    for (const p of [u.cover, u.coverInner, ...u.panels]) { across(p.x, p.w, 'an underside panel'); along(p.y, p.d, 'an underside panel'); }
  }
  for (const f of FEET.list) { across(f.x - f.d / 2, f.d, 'a foot'); along(f.y - f.d / 2, f.d, 'a foot'); }
  // The buttons are on the case's front face, and the front words on the door or the front.
  for (const b of BUTTONS.list) assert.equal(b.y, CASE.depth);
});

test('the case\'s words are words: no module of the NES\'s models names a logo, loads an image but its own drawn words and the track map, or holds an SVG path', () => {
  const dir = path.join(process.cwd(), 'src', 'models');
  for (const f of fs.readdirSync(dir).filter((x) => x.startsWith('nes-famicom-'))) {
    const src = fs.readFileSync(path.join(dir, f), 'utf8');
    assert.doesNotMatch(src, /logo/i, `${f} names a logo`);
    assert.doesNotMatch(src, /<path\b|Path2D|SVGLoader|\bd="M|\.svg\b/i, `${f} holds an SVG path`);
    const images = [...src.matchAll(/new Image\(|TextureLoader|ImageLoader|ImageBitmapLoader|fetch\(/g)].map((m) => m[0]);
    if (f === 'nes-famicom-board.js') assert.deepEqual(images, ['new Image('], 'the board loads an image other than its track map');
    else assert.deepEqual(images, [], `${f} loads an image`);
  }
  // The case's words are drawn from text, in the site's own type, after it has loaded, into one canvas.
  assert.equal([...caseModule.matchAll(/document\.createElement\('canvas'\)/g)].length, 1, 'the case draws its words into more than one canvas');
  assert.match(caseModule, /document\.fonts\?\.load\(f\)/);
  assert.match(caseModule, /css\.getPropertyValue\('--font-head'\)/);
  assert.match(caseModule, /g\.fillText\(line, 0, 0\)/);
  for (const r of caseLayout.REGIONS) for (const l of caseLayout.LABELS[r]) assert.ok(typeof l.words === 'string' && /\w/.test(l.words), `${r}: a label is not words`);
});

test('the outside model follows the machine: the light and POWER from panel.nes.running(), RESET down on nes:reset, the region at mount and on nes:region', () => {
  assert.equal(caseLayout.PRESS_MS, 140);
  assert.match(caseModule, /const running = \(\) => nes\(\)\?\.running\?\.\(\) === true;/);
  // The region the page has when the view mounts, not when the page loaded (Review Focus 3).
  assert.match(caseModule, /const regionNow = \(\) => nes\(\)\?\.region\?\.\(\)\?\.toLowerCase\(\) \?\? REGIONS\[0\];\s*setRegion\(REGIONS\.includes\(regionNow\(\)\) \? regionNow\(\) : REGIONS\[0\]\);/);
  assert.match(caseModule, /panel\?\.addEventListener\('nes:region', \(e\) => setRegion\(e\.detail\?\.region\)\);/);
  assert.match(caseModule, /panel\?\.addEventListener\('nes:reset', \(\) => \{/);
  assert.match(caseModule, /setTimeout\(\(\) => \{ reset\.position\.z = reset\.userData\.out;[^}]*\}, PRESS_MS\)/);
  assert.match(caseModule, /every\(\(\) => show\(running\(\)\)\);/);
  assert.match(caseModule, /led\.material = on \? M\.ledOn : M\.ledOff;/);
  assert.match(caseModule, /ledOn: new MeshBasicMaterial\(\{ color: token\('model-led'\)/);
  assert.match(caseModule, /ledOff: standard\('model-led-off'/);
  assert.match(caseModule, /in: out - S\(b\.travelMm\)/);
  for (const hook of ['root.dataset.modelRegion', 'root.dataset.modelLed', 'root.dataset.modelPower', 'root.dataset.modelPresses', 'root.modelButtonPoint = ']) assert.ok(caseModule.includes(hook), hook);
  // The camera may go under the case: the stage's full orbit, and the bounds the case plus two centimetres.
  assert.match(caseModule, /const margin = 2;/);
  assert.match(caseModule, /createStage\(root, \{ view: \[[^\]]*\], bounds, minDistance: 3, maxDistance: 150 \}\)/);
  // Its colours, the case as made in greys, are tokens of its own, and none is the lime.
  const tokens = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'tokens.css'), 'utf8');
  const value = (name) => new RegExp(`--${name}:\\s*(#[0-9a-f]{6});`).exec(tokens)?.[1];
  for (const name of ['model-nes-case', 'model-nes-case-dark', 'model-nes-band', 'model-nes-button', 'model-nes-ink', 'model-nes-sticker']) {
    assert.ok(value(name), `tokens.css has no --${name}`);
    assert.notEqual(value(name), value('lime'));
  }
  for (const grey of ['model-nes-case', 'model-nes-case-dark']) {
    const [r, g, b] = value(grey).slice(1).match(/../g).map((h) => parseInt(h, 16));
    assert.ok(Math.max(r, g, b) - Math.min(r, g, b) <= 8, `--${grey} is not a grey: the case is drawn as made, not yellowed`);
  }
});

/** A made-up machine panel: Start, its state, and panel.nes as public/nes.js gives it before and after Start. */
function fakePanel({ running = false, startDisabled = false, state = 'ready' } = {}) {
  const calls = { start: 0, reset: 0 };
  const start = { disabled: startDisabled, hidden: running, click: () => { calls.start++; } };
  const panel = {
    dataset: { state },
    querySelector: (s) => (s === '[data-nes-start]' ? start : null),
    nes: { running: () => running, region: () => 'NTSC', reset: () => { calls.reset++; } },
  };
  return { panel, calls };
}

test('a click on the model\'s buttons before Start: POWER does what Start does, RESET does nothing and the status says the machine is not running (Review Focus 4)', () => {
  const { STATUS, press } = caseLayout;
  let p = fakePanel();
  assert.equal(press('POWER', p.panel), STATUS.starting);
  assert.deepEqual(p.calls, { start: 1, reset: 0 }, 'POWER did not press Start');
  p = fakePanel();
  assert.equal(press('RESET', p.panel), STATUS.notRunning);
  assert.deepEqual(p.calls, { start: 0, reset: 0 }, 'RESET did something before Start');
  assert.match(STATUS.notRunning, /not running/);
  // While Start's download runs, and on a page that cannot start the machine, POWER says so and presses nothing.
  p = fakePanel({ startDisabled: true, state: 'loading' });
  assert.equal(press('POWER', p.panel), STATUS.loading);
  p = fakePanel({ startDisabled: true, state: 'missing' });
  assert.equal(press('POWER', p.panel), STATUS.cannot);
  assert.deepEqual(p.calls, { start: 0, reset: 0 });
  // With no machine panel on the page at all, nothing is called and nothing throws.
  assert.equal(press('RESET', null), STATUS.notRunning);
  assert.equal(press('POWER', null), STATUS.cannot);
});

test('a click on the model\'s buttons once the machine runs: RESET resets it through panel.nes.reset(), POWER changes nothing and says there is no power-off', () => {
  const { STATUS, press } = caseLayout;
  const p = fakePanel({ running: true });
  assert.equal(press('RESET', p.panel), STATUS.reset);
  assert.equal(press('POWER', p.panel), STATUS.noPowerOff);
  assert.deepEqual(p.calls, { start: 0, reset: 1 });
  assert.match(STATUS.noPowerOff, /no power-off/);
  // The model's click goes through press(), with the page's panel.
  assert.match(caseModule, /status\.textContent = press\(o\.userData\.button, panel\)/);
  // POWER presses the page's own Start button, the one public/nes.js wires to run().
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'nes.js'), 'utf8');
  assert.match(driver, /start\.addEventListener\('click', \(\) => run\(panel, say\), \{ once: true \}\);/);
  assert.match(fs.readFileSync(path.join(process.cwd(), 'src', 'models', 'nes-famicom-case-layout.mjs'), 'utf8'), /panel\?\.querySelector\('\[data-nes-start\]'\)/);
});

test('the outside\'s note states every uncertainty plainly, with its figures read from the results file, and the page shows it word for word', () => {
  const f = caseFigures;
  const notes = [...caseSection.matchAll(/<div class="model-made prose" data-model-made data-model-region="([a-z]+)"[^>]*>([\s\S]*?)<\/div>/g)];
  assert.deepEqual(notes.map((m) => m[1]), caseEntry.regions);
  for (const [, region, note] of notes) {
    const { paragraphs, limits } = caseNotes.made(f, region);
    assert.deepEqual(caseEntry.made[region](f), { paragraphs, limits });
    const t = visibleText(note);
    for (const x of [...paragraphs, ...limits]) assert.ok(t.includes(visibleText(x)), `${region}: the page does not say: ${x}`);
    const all = [...paragraphs, ...limits].join(' ');
    // The size: published, not Nintendo's, good to about 3 per cent.
    assert.ok(all.includes(`published ${mm2(f.size.widthMm)} by ${mm2(f.size.depthMm)} mm and ${mm2(f.size.heightMm)} mm high`), 'the published size');
    assert.match(all, /none of them Nintendo's/);
    assert.ok(all.includes(`good to about ${mm2(f.size.goodToPct)} per cent`));
    // The profile: an average, good to about 2.5 mm, of ends from 12.86 to 17.71 mm.
    assert.ok(all.includes(`range from ${mm2(f.profile.endsMm.min)} to ${mm2(f.profile.endsMm.max)} mm, so the inset is an average good to about ${mm2(f.profile.goodToMm)} mm`));
    // The rear: placed from a photograph that agrees with the patent within 1.3 mm; the board's places missed by up to 5.16; failed.
    assert.ok(all.includes(caseNotes.rearSentence(f)));
    assert.ok(all.includes(`within ${mm2(f.rear.placesUsedMm)} mm`) && all.includes(`missed by up to ${mm2(f.rear.boardMissMm)} mm`));
    assert.match(all, /the check failed as measured/);
    // The board's place: about 2 mm, assuming one moulding.
    assert.ok(all.includes(`good to about ${mm2(f.boardInCase.uncertaintyMm)} mm, and assumes that the PAL and NTSC cases share one moulding`));
    // The buttons' travel, typical; POWER's latch, not known.
    assert.ok(all.includes(`The buttons' travel, ${mm2(f.buttons.travelMm)} mm, is a typical one, not measured`));
    assert.match(all, /Whether POWER latches in on a real console is not known/);
    // The jacks on the side and the PAL rear: not checked against the board.
    assert.match(all, /The video and audio jacks on the side are not checked against the board, and neither is the PAL console's rear/);
    // The PAL words: a 20 mm lens, strong perspective.
    assert.ok(all.includes(`taken with a ${mm2(f.palLensMm)} mm lens, with strong perspective`));
    // The lockout chip is not emulated, so the light never blinks.
    assert.match(all, /lockout chip is not emulated, so the power light never blinks/);
  }
  // The caption says the size is not Nintendo's; the help, under the model, what its buttons do and that the light never blinks.
  assert.match(caseText, /the published size, which is not Nintendo's own/);
  assert.ok(caseText.includes(caseLayout.HELP));
  assert.match(caseLayout.HELP, /The page has no power-off/);
  assert.match(caseLayout.HELP, /the light never blinks/);
});
