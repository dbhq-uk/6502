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
