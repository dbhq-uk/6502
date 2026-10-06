import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { page, visibleText, DIST } from './helpers.mjs';
import { KIM1_KEYS } from '../src/lib/machines.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import { MODELS, CONTROLS, CONTROLS_DESCRIPTION, TRACK_BUTTONS, modelSrc } from '../src/models/models.mjs';
import { modelsOf } from '../src/models/models.mjs';
import { BOARD, TABS, CONTACTS_PER_TAB, PITCH, CHIPS, AXIAL, TRANSISTORS, TRIMMER, CRYSTAL, NAME, HOLES, KEYPAD_HOLES, WIRE, DISPLAY, KEYPAD, KEY_ROWS, KEYS, SST, HEIGHTS, TRACKS, decode, describe } from '../src/models/kim-1-layout.mjs';
import { made } from '../src/models/kim-1-notes.mjs';
import { registry } from '../src/lib/data.mjs';

// sharp ships with Astro (its image service), so the track map can be measured without a new dependency.
const sharp = (await import('sharp')).default;

// The machines' 3D models: the KIM-1's board, the stage every model shares,
// and how the page loads them. The model is checked here as data and as built
// markup; scripts/browser-check.mjs checks it running, in a real browser.

const models = path.join(process.cwd(), 'src', 'models');
const html = page('/machines/kim-1/')?.html ?? '';
const section = (() => {
  const at = html.indexOf('<section class="model"');
  return at < 0 ? '' : html.slice(at, html.indexOf('<section class="photos"', at));
})();
// What the model was measured from, and how well: the committed results of tools/kim1-model/.
const figures = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src', 'data', 'kim-1-model.json'), 'utf8'));
const kim = registry.machines.find((m) => m.id === 'kim-1');

test('the model\'s keypad is the machine\'s: every key once, in the panel\'s rows, with the SST switch where the board has it', () => {
  assert.deepEqual(KEYS.map((k) => k.name).sort(), [...KIM1_KEYS].sort());
  assert.equal(KEYS.length, 23);
  assert.deepEqual(KEY_ROWS.flat().filter((k) => k !== 'SST'), KEYS.map((k) => k.name));
  assert.deepEqual([SST.x, SST.y], [KEYPAD.columns[3], KEYPAD.rows[0]]);
  // The panel draws its keypad from the same rows, so the two cannot drift apart.
  const panel = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'Kim1Panel.astro'), 'utf8');
  assert.match(panel, /import \{ KEY_ROWS as rows \} from '\.\.\/models\/kim-1-layout\.mjs'/);
});

test('every part of the model lies on the board, and no two keys overlap', () => {
  const inside = ({ x, y }, halfW = 0, halfD = 0, what = '') => {
    assert.ok(x - halfW >= 0 && x + halfW <= BOARD.width && y - halfD >= 0 && y + halfD <= BOARD.depth, `${what} at ${x}, ${y} is off the board`);
  };
  const DIP = { 40: [26, 7], 16: [9.7, 3.2], 14: [9.5, 3.2], 8: [4.8, 3.2] };
  for (const c of CHIPS) inside(c, ...(c.alongX ? DIP[c.pins] : [...DIP[c.pins]].reverse()), c.id);
  for (const a of AXIAL) { inside({ x: a.x0, y: a.y0 }, 0, 0, a.ref); inside({ x: a.x1, y: a.y1 }, 0, 0, a.ref); }
  for (const t of TRANSISTORS) inside(t, 2.4, 2.4, t.ref);
  inside(TRIMMER, 5, 5, 'the trimmer');
  inside(CRYSTAL, CRYSTAL.width / 2, CRYSTAL.depth / 2, 'the crystal');
  inside(NAME, NAME.width / 2, NAME.depth / 2, 'the name');
  for (const [x, y] of [...HOLES, ...KEYPAD_HOLES]) inside({ x, y }, 1.6, 1.6, 'a hole');
  for (const [x, y] of WIRE) inside({ x, y }, 0, 0, 'the red wire');
  inside(DISPLAY, DISPLAY.width / 2, DISPLAY.depth / 2, 'the display');
  inside(KEYPAD, KEYPAD.width / 2, KEYPAD.depth / 2, 'the keypad');
  for (const k of KEYS) {
    assert.ok(Math.abs(k.x - KEYPAD.x) + KEYPAD.key / 2 <= KEYPAD.width / 2 && Math.abs(k.y - KEYPAD.y) + KEYPAD.key / 2 <= KEYPAD.depth / 2, `key ${k.name} is off the keypad`);
  }
  for (const a of KEYS) for (const b of KEYS) if (a !== b) assert.ok(Math.abs(a.x - b.x) >= KEYPAD.key || Math.abs(a.y - b.y) >= KEYPAD.key, `keys ${a.name} and ${b.name} overlap`);
  for (const d of DISPLAY.digits) assert.ok(Math.abs(d - DISPLAY.x) + DISPLAY.digitWidth / 2 <= DISPLAY.width / 2, `a digit at ${d} is outside the display`);
  assert.equal(DISPLAY.digits.length, 6);
});

test('the parts are the replica\'s and the heights are measured: the model draws every chip, resistor, capacitor, diode and transistor, and its heights are the triangulated ones', () => {
  const parts = JSON.parse(fs.readFileSync(path.join(REPO_ROOT, 'tools', 'kim1-model', 'data', 'kicad-parts.json'), 'utf8')).parts;
  const refs = (re) => parts.filter((p) => re.test(p.footprint)).map((p) => p.ref).sort();
  assert.deepEqual(CHIPS.map((c) => c.ref).sort(), refs(/^DIP-/));
  assert.deepEqual(AXIAL.map((a) => a.ref).sort(), refs(/^(R_|C_Axial|CP_Axial|D_DO)/));
  assert.deepEqual(TRANSISTORS.map((t) => t.ref).sort(), refs(/^TO-92$/));
  // Each height the model uses is a triangulated point in the analysis's results, not a typical value.
  const measured = new Map(figures.heights.points.map((p) => [p.part, p.height]));
  assert.equal(HEIGHTS.ceramic, measured.get('dip40-ceramic'));
  assert.equal(HEIGHTS.socketed, measured.get('dip40-plastic'));
  assert.equal(HEIGHTS.plastic, measured.get('dip14'));
  assert.equal(HEIGHTS.transistor, measured.get('to92'));
  assert.equal(HEIGHTS.trimmer, measured.get('trimmer'));
  assert.equal(CRYSTAL.height, measured.get('crystal'));
  assert.equal(DISPLAY.height, measured.get('display'));
  assert.equal(KEYPAD.bezelHeight, measured.get('keypad-bezel'));
  assert.equal(KEYPAD.keyHeight, measured.get('key'));
  const memory = figures.heights.points.filter((p) => p.part === 'dip16').map((p) => p.height);
  assert.ok(Math.abs(HEIGHTS.memory - memory.reduce((a, b) => a + b) / memory.length) < 0.006);
  // The check point on the board itself comes out at the board, within its range.
  assert.ok(figures.heights.checkRange[0] <= 0 && figures.heights.checkRange[1] >= 0, 'the check point on the board is not at height 0 within its range');
});

test('the edge contacts are the scale: 22 a tab at 0.156 inch, all of them on their tab', () => {
  assert.equal(CONTACTS_PER_TAB, 22);
  assert.ok(Math.abs(PITCH - 0.156 * 25.4) < 1e-9);
  for (const t of TABS) {
    const last = t.first + (CONTACTS_PER_TAB - 1) * PITCH;
    assert.ok(t.first - 1.2 >= t.y0 && last + 1.2 <= t.y1, `the contacts from ${t.first} to ${last.toFixed(1)} do not fit the tab from ${t.y0} to ${t.y1}`);
  }
  // The 6502 and both 6530s are 40-pin packages, and the 6502 is the top one.
  const labelled = CHIPS.filter((c) => c.label);
  assert.deepEqual(labelled.map((c) => [c.label, c.pins]), [['6502', 40], ['6530', 40], ['6530', 40]]);
  assert.ok(labelled[0].y < Math.min(...labelled.slice(1).map((c) => c.y)), 'the 6502 is not the top one');
});

test('the model reads the digits with the machine\'s own table, Kim1Display.Decode', () => {
  const cs = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Kim1', 'Kim1Display.cs'), 'utf8');
  const pairs = [...cs.matchAll(/0x([0-9A-F]{2}) => '(.)'/g)].map((m) => [parseInt(m[1], 16), m[2]]);
  assert.ok(pairs.length >= 17, 'the decode table was not found in Kim1Display.cs');
  for (const [segments, char] of pairs) assert.equal(decode(segments), char, `0x${segments.toString(16)}`);
  assert.equal(decode(0x01), '?');
});

test('the model and its stage hold no colour of their own: every colour is a token the stylesheet defines', () => {
  const tokens = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'tokens.css'), 'utf8');
  for (const f of fs.readdirSync(models).filter((x) => /\.m?js$/.test(x))) {
    const src = fs.readFileSync(path.join(models, f), 'utf8');
    assert.deepEqual(src.match(/#[0-9a-fA-F]{3,8}\b|0x[0-9a-fA-F]{6}\b/g) ?? [], [], `${f} holds a colour`);
    for (const m of src.matchAll(/(?:token|standard|hex)\('([\w-]+)'/g)) assert.match(tokens, new RegExp(`--${m[1]}:`), `${f} uses --${m[1]}, which tokens.css does not define`);
    for (const m of src.matchAll(/colour: '([\w-]+)'/g)) assert.match(tokens, new RegExp(`--${m[1]}:`), `${f} prints in --${m[1]}, which tokens.css does not define`);
  }
});

test('every model in the map has its module and a built bundle, and every model module in the folder is in the map', () => {
  const ids = Object.keys(MODELS);
  assert.ok(ids.includes('kim-1'));
  for (const id of ids) {
    assert.ok(fs.existsSync(path.join(models, `${id}.js`)), `src/models/${id}.js is missing`);
    assert.ok(fs.existsSync(path.join(DIST, modelSrc(id))), `${modelSrc(id)} was not built into dist`);
    assert.match(fs.readFileSync(path.join(models, `${id}.js`), 'utf8'), /export async function mount\(root\)/, `src/models/${id}.js does not export mount(root)`);
  }
  // A model module is <id>.js; the shared stage and the layouts are .mjs.
  const modules = fs.readdirSync(models).filter((f) => f.endsWith('.js')).map((f) => f.replace(/\.js$/, ''));
  assert.deepEqual(modules.filter((m) => !ids.includes(m)), [], 'a model module that src/models/models.mjs does not list');
});

// The budget, set before the model was built (journal, 2 October 2026): the
// model must not cost more than the chip page's own bundle, and none of it is
// part of the page's first load.
const BUDGET_GZIP = 200_000;

test('each model bundle is inside its budget, and carries the licences of what it bundles', () => {
  for (const id of Object.keys(MODELS)) {
    const js = fs.readFileSync(path.join(DIST, modelSrc(id)));
    const gz = zlib.gzipSync(js, { level: 9 }).length;
    assert.ok(gz <= BUDGET_GZIP, `${modelSrc(id)} is ${gz} bytes gzipped, over the ${BUDGET_GZIP} budget`);
    assert.match(js.toString(), /Copyright 2010-\d{4} Three\.js Authors/);
    assert.match(js.toString(), /SPDX-License-Identifier: MIT/);
  }
});

test('the model is loaded lazily: the page loads only the small loader, never the model or three.js', () => {
  const scripts = [...html.matchAll(/<script\b[^>]*\bsrc="([^"]+)"/g)].map((m) => m[1]);
  assert.ok(scripts.includes('/model-loader.js'), 'the page does not load the model loader');
  assert.deepEqual(scripts.filter((s) => s.startsWith('/models/') || /three|chip\.js/.test(s)), [], 'the model is in the page\'s first load');
  assert.doesNotMatch(html, /<link\b[^>]*rel="modulepreload"/);
  const loader = fs.readFileSync(path.join(process.cwd(), 'public', 'model-loader.js'), 'utf8');
  assert.doesNotMatch(loader, /^import\b/m, 'the loader imports something when the page opens');
  assert.match(loader, /await import\(root\.dataset\.modelSrc\)/);
  assert.match(loader, /new IntersectionObserver\(/);
  assert.match(loader, /button\.addEventListener\('click', load\)/);
  assert.ok(fs.statSync(path.join(DIST, 'model-loader.js')).size < 4000, 'the loader is no longer small');
  assert.match(section, new RegExp(`data-model-src="${modelSrc('kim-1')}"`));
});

test('the model section is hidden without JavaScript, has an accessible name, a reset button, a load button and its label', () => {
  assert.ok(section, 'the page has no model section');
  assert.match(section, /^<section class="model" aria-labelledby="model"[^>]*\bhidden\b/, 'the section is not hidden in the markup');
  assert.match(section, /<h2 id="model">A model of the board<\/h2>/);
  const stage = /<section class="model-stage"[^>]*>/.exec(section)?.[0] ?? '';
  assert.match(stage, /tabindex="0"/, 'the model cannot take focus, so the arrow keys cannot reach it');
  assert.ok(stage.includes(`aria-label="${MODELS['kim-1'].label}"`), 'the model has no accessible name');
  assert.match(stage, /aria-describedby="model-about"/);
  assert.match(section, /<span class="model-tag" aria-hidden="true">Model, not a photograph<\/span>/);
  assert.match(section, /<button type="button" class="btn small" data-model-reset>Reset the view<\/button>/);
  assert.match(section, /<button type="button" class="btn small model-load" data-model-load>Load the 3D model<\/button>/);
  assert.match(section, /role="status" data-model-status/);
  assert.doesNotMatch(section, /class="[^"]*\bpill\b/, 'the model spends the page\'s lime');
});

// The controls. The behaviour is checked in a real browser by
// scripts/browser-check.mjs; these hold the markup and the wiring it relies on.
test('the controls are told to the visitor in real text, under the model and on it, and to a screen reader in its aria-description', () => {
  const stage = /<section class="model-stage"[^>]*>/.exec(section)?.[0] ?? '';
  // One set of sentences, so the visible text and the description cannot disagree.
  const help = /<p class="model-help" id="model-help">([\s\S]*?)<\/p>/.exec(section)?.[1] ?? '';
  assert.equal(visibleText(help), `${CONTROLS.pointer} ${CONTROLS.touch} ${CONTROLS.keys} ${CONTROLS.tracks}`);
  assert.ok(stage.includes(`aria-description="${CONTROLS_DESCRIPTION}"`), 'the model has no aria-description of its keys');
  assert.ok(CONTROLS_DESCRIPTION.includes(CONTROLS.keys));
  // Every key the model answers to is named in both places.
  for (const word of ['arrow keys turn', 'Shift and the arrow keys pan', 'plus and minus zoom', 'Home resets the view', 'Escape lets go']) {
    assert.ok(visibleText(help).includes(word), `the visible hint does not say "${word}"`);
    assert.ok(CONTROLS_DESCRIPTION.includes(word), `the aria-description does not say "${word}"`);
  }
  for (const word of ['Drag to turn', 'right-drag or Shift-drag to pan', 'Click the model, then scroll to zoom', 'Double-click empty space to reset', 'tap the model first']) assert.ok(visibleText(help).includes(word), `the visible hint does not say "${word}"`);
  // The hint on the canvas is text, not an image, and a label, so it has no full stop.
  assert.match(section, /<span class="model-hint" aria-hidden="true"><span class="hint-pointer">Click the model, then scroll to zoom<\/span><span class="hint-touch">Tap the model, then drag to turn and pinch to zoom<\/span><\/span>/);
  for (const label of [CONTROLS.hint, CONTROLS.hintTouch]) assert.doesNotMatch(label, /\.$/);
  assert.doesNotMatch(section, /<img\b/, 'the model section holds an image');
  // The accessible name is short; the controls are the description.
  assert.ok(MODELS['kim-1'].label.length < 100);
});

test('focus decides who has the wheel and the finger: the wheel zooms only with focus or ctrl or cmd, touch-action is pan-y until a tap focuses the model, Escape lets go', () => {
  const stage = fs.readFileSync(path.join(models, 'stage.mjs'), 'utf8');
  // camera-controls' own wheel is off, ours checks focus first and only then stops the page scrolling.
  assert.match(stage, /controls\.mouseButtons\.wheel = CameraControls\.ACTION\.NONE/);
  assert.match(stage, /if \(document\.activeElement !== stage && !e\.ctrlKey && !e\.metaKey\) return;\s*e\.preventDefault\(\);/);
  assert.match(stage, /addEventListener\('wheel'[\s\S]*?\{ passive: false \}/);
  // The canvas scrolls the page until focused, and only then is touch-action none.
  assert.match(stage, /canvas\.style\.touchAction = active \? 'none' : 'pan-y pinch-zoom'/);
  assert.match(stage, /stage\.addEventListener\('focus', \(\) => setActive\(true\)\)/);
  assert.match(stage, /stage\.addEventListener\('blur', \(\) => setActive\(false\)\)/);
  assert.match(stage, /one: ACTION\.NONE, two: ACTION\.NONE, three: ACTION\.NONE/);
  assert.match(stage, /one: ACTION\.TOUCH_ROTATE, two: ACTION\.TOUCH_DOLLY_TRUCK/);
  // A finger takes the focus on a tap; the mouse on going down.
  assert.match(stage, /if \(e\.pointerType !== 'mouse'\) stage\.focus\(/);
  assert.match(stage, /Escape: \(\) => stage\.blur\(\)/);
  assert.match(stage, /Home: \(\) => reset\(\)/);
  assert.match(stage, /e\.shiftKey \? controls\.truck/);
  // The stylesheet shows the ring while focused, and no longer pins the canvas to touch-action none.
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8');
  assert.match(css, /\.model-stage\[data-active\] \{ outline: 2px solid var\(--lime\); \}/);
  assert.doesNotMatch(css, /\.model-stage canvas \{[^}]*touch-action/);
  // The hint goes once the model has focus.
  assert.match(css, /\.model-stage\[data-active\] \.model-hint \{ display: none; \}/);
});

test('the camera goes all the way round, may get close to one chip and far from the whole board, and the target is held inside the board\'s bounds', () => {
  const stage = fs.readFileSync(path.join(models, 'stage.mjs'), 'utf8');
  const kim1 = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  assert.match(stage, /maxPolarAngle = Math\.PI \}/, 'the camera is clamped short of a full orbit');
  assert.doesNotMatch(stage, /0\.48/);
  assert.match(stage, /controls\.setBoundary\(new Box3\(/);
  assert.match(stage, /minDistance = 3, maxDistance = 150/);
  assert.match(kim1, /createStage\(root, \{ view: \[[^\]]*\], bounds, minDistance: 3, maxDistance: 150 \}\)/);
  assert.match(kim1, /const bounds = \[X\(-Math\.max\(\.\.\.TABS\.map\(\(t\) => t\.out\)\)\) - margin, /);
  // Close enough that a 40-pin chip (52 mm) is wider than the view, on a phone held upright: 36 degrees of view, 390 by 340.
  const visibleWidth = (distance, aspect) => 2 * distance * Math.tan((36 / 2) * Math.PI / 180) * aspect;
  assert.ok(visibleWidth(3, 390 / 340) < 5.2, 'at the closest, a chip does not fill the view');
  // Far enough that the board (27.3 cm) is well inside it.
  assert.ok(visibleWidth(150, 1) > 27.3 * 3, 'at the farthest, the board is not small');
  // The camera's far plane reaches the board from the farthest the camera goes.
  assert.match(stage, /new PerspectiveCamera\(36, 1, 0\.1, 400\)/);
});

test('the underside is lit and has its own material, so a view from below is not a black void', () => {
  const stage = fs.readFileSync(path.join(models, 'stage.mjs'), 'utf8');
  const kim1 = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  const tokens = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'tokens.css'), 'utf8');
  assert.match(stage, /new HemisphereLight\(/);
  assert.match(stage, /under\.position\.set\([^)]*-\d+, /, 'the second fill is not below the board');
  assert.match(tokens, /--model-pcb-under:/);
  assert.match(kim1, /under: standard\('model-pcb-under'/);
  // A box's faces are +x, -x, +y, -y, +z, -z: the fourth, the underside, takes the new material.
  assert.match(kim1, /const faces = \[M\.board, M\.board, M\.board, M\.under, M\.board, M\.board\]/);
  assert.match(kim1, /BoxGeometry\(S\(BOARD\.width\), H\.board, S\(BOARD\.depth\)\), faces\)/);
  assert.match(kim1, /pads/, 'the legs end in no pads on the underside');
});

test('a double click or a double tap on empty space resets the view, and a click is still told from a drag', () => {
  const stage = fs.readFileSync(path.join(models, 'stage.mjs'), 'utf8');
  assert.match(stage, /if \(pick\(e, scene\.children\)\) \{ lastTap = null; return; \}/);
  assert.match(stage, /now - lastTap\.at < 350/);
  assert.match(stage, /\n      reset\(\);/);
  // Pressing a key is still a click that moved under five pixels.
  assert.match(stage, /Math\.hypot\(e\.clientX - down\[0\], e\.clientY - down\[1\]\) > 5/);
  assert.match(fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8'), /s\.onClick\(\(\) => pickable/);
  assert.match(section, /<button type="button" class="btn small" data-model-reset>Reset the view<\/button>/);
});

test('the model\'s text alternative is its caption, says it is a model and not a photograph, and its figures come from the layout', () => {
  const about = /<p class="figure-caption model-about" id="model-about">([\s\S]*?)<\/p>/.exec(section)?.[1] ?? '';
  assert.equal(visibleText(about), describe());
  assert.match(describe(), /^Model, not a photograph\./);
  assert.ok(describe().includes(`${(BOARD.depth / 10).toFixed(1)} by ${(BOARD.width / 10).toFixed(1)} cm`));
  assert.ok(describe().includes(`the ${KEYS.length} keys`));
});

test('the model takes the machine\'s state from the panel: the driver exposes it and announces every tap', () => {
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'kim-1.js'), 'utf8');
  assert.match(driver, /panel\.kim1 = \{ tap, segments: \(digit\) => shown\[digit\] \}/);
  assert.match(driver, /new CustomEvent\('kim1:key', \{ detail: \{ key \} \}\)/);
  assert.match(driver, /new CustomEvent\('kim1:ready'\)/);
  // Every tap goes through the one function that announces it.
  assert.doesNotMatch(driver.replace(/const tap = \(key\) => \{\s*kim\.Tap\(key\);/, ''), /kim\.Tap\(/);
  const model = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  assert.match(model, /machine\.segments\(d\)/);
  assert.match(model, /machine\.tap\(hit\.userData\.key\)/);
  assert.match(model, /addEventListener\('kim1:key'/);
});

test('the model never turns on its own, and with reduced motion its moves land at once', () => {
  const stage = fs.readFileSync(path.join(models, 'stage.mjs'), 'utf8');
  assert.doesNotMatch(stage, /autoRotate|controls\.rotate\(dt/, 'the model turns by itself');
  assert.match(stage, /prefers-reduced-motion: reduce/);
  assert.match(stage, /controls\.smoothTime = reduced \? 0/);
  for (const key of ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown']) assert.ok(stage.includes(`${key}:`), `the ${key} key does not turn the model`);
  assert.match(stage, /\[data-model-reset\]/);
});

test('the deploy checks that the model loader and every model bundle are serving', () => {
  const deploy = fs.readFileSync(path.join(REPO_ROOT, '.github', 'workflows', 'deploy-site.yml'), 'utf8');
  const listed = (/for f in ([^;]*); do/.exec(deploy)?.[1] ?? '').split(/\s+/);
  assert.ok(listed.includes('model-loader.js'));
  for (const id of Object.keys(MODELS)) assert.ok(listed.includes(modelSrc(id).slice(1)), `deploy-site.yml does not check ${modelSrc(id)}`);
});

// The copper tracks. The behaviour is checked in a real browser by
// scripts/browser-check.mjs; these hold the asset, its credit, the markup and the wiring.
const trackFile = path.join(process.cwd(), 'src', 'assets', 'tracks', 'kim-1.webp');
const TRACK_BUDGET = 400_000;

test('the track map is committed, the size the model expects, inside its budget, its three channels apart, and built beside the model\'s bundle', async () => {
  assert.ok(fs.existsSync(trackFile), 'src/assets/tracks/kim-1.webp is missing');
  const bytes = fs.readFileSync(trackFile);
  assert.ok(bytes.length <= TRACK_BUDGET, `the track map is ${bytes.length} bytes, over its ${TRACK_BUDGET} budget`);
  assert.equal((await sharp(bytes).metadata()).format, 'webp');
  const { data, info } = await sharp(bytes).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  // It covers the board's body edge to edge, at the resolution the analysis made it.
  assert.deepEqual([info.width, info.height], [TRACKS.width, TRACKS.height]);
  assert.deepEqual([TRACKS.width, TRACKS.height], [BOARD.width * figures.tracks.mapPxPerMm, BOARD.depth * figures.tracks.mapPxPerMm]);
  // Red is the top face from the photographs, green the top face from the
  // replica where no photograph shows the board, blue the underside. Red and
  // green never both claim a pixel, and each face has a real share of copper.
  let top = 0, fill = 0, both = 0, under = 0;
  for (let i = 0; i < data.length; i += info.channels) {
    const r = data[i] > 127, g = data[i + 1] > 127, b = data[i + 2] > 127;
    if (r || g) top++;
    if (g) fill++;
    if (r && g) both++;
    if (b) under++;
  }
  const n = info.width * info.height;
  assert.ok(top / n > 0.05 && top / n < 0.4, `copper covers ${(top / n * 100).toFixed(1)}% of the top face`);
  assert.ok(under / n > 0.05 && under / n < 0.4, `copper covers ${(under / n * 100).toFixed(1)}% of the underside`);
  assert.ok(fill > 0 && fill < top / 2, 'the replica fills more of the top face than the photographs do');
  assert.ok(both / n < 0.001, `the photographs and the replica both claim ${(both / n * 100).toFixed(2)}% of the top face`);
  // Built into dist beside the bundle, byte for byte, where the model asks for it.
  assert.equal(MODELS['kim-1'].texture, TRACKS.src);
  assert.equal(TRACKS.src, '/models/kim-1-tracks.webp');
  const built = path.join(DIST, TRACKS.src);
  assert.ok(fs.existsSync(built), `${TRACKS.src} was not built into dist`);
  assert.ok(fs.readFileSync(built).equals(bytes), `${TRACKS.src} is not the committed map`);
});

test('the track map is loaded with the model, never with the page, and the deploy checks it is serving', () => {
  const model = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  assert.match(model, /await trackMaps\(TRACKS\.src, \{/);
  assert.match(model, /img\.src = src;\s*await img\.decode\(\);/, 'the map is not loaded as a same-origin image');
  assert.match(section, new RegExp(`data-model-texture="${TRACKS.src}"`));
  // Nothing on the page itself names it: no img, link or preload.
  assert.doesNotMatch(html.replace(/data-model-texture="[^"]*"/, ''), /kim-1-tracks/);
  const deploy = fs.readFileSync(path.join(REPO_ROOT, '.github', 'workflows', 'deploy-site.yml'), 'utf8');
  const listed = (/for f in ([^;]*); do/.exec(deploy)?.[1] ?? '').split(/\s+/);
  for (const m of Object.values(MODELS)) if (m.texture) assert.ok(listed.includes(m.texture.slice(1)), `deploy-site.yml does not check ${m.texture}`);
  // The build copies it; nothing in the build or the tests makes it or runs the analysis.
  const pkg = fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8');
  assert.doesNotMatch(pkg, /kim1-model|make-board-tracks|python/);
  assert.match(fs.readFileSync(path.join(process.cwd(), 'scripts', 'build-models.mjs'), 'utf8'), /fs\.copyFileSync\(tracks, `public\$\{MODELS\[id\]\.texture\}`\)/);
});

test('every model with a track map has it committed, inside the budget its plan set, and built beside its bundle byte for byte, where its entry says', () => {
  // The KIM-1's map's budget is the test above's; the NES board's, set before it was traced, is 600,000 bytes (its plan's budgets).
  const BUDGETS = { 'kim-1': TRACK_BUDGET, 'nes-famicom-board': 600_000 };
  const textured = Object.entries(MODELS).filter(([, m]) => m.texture);
  assert.ok(textured.length >= 2, 'fewer than two models have a track map');
  for (const [id, m] of textured) {
    const committed = path.join(process.cwd(), 'src', 'assets', 'tracks', `${id}.webp`);
    assert.ok(fs.existsSync(committed), `src/assets/tracks/${id}.webp is missing`);
    const bytes = fs.readFileSync(committed);
    assert.ok(id in BUDGETS, `${id}'s track map has no budget here`);
    assert.ok(bytes.length <= BUDGETS[id], `${id}'s track map is ${bytes.length} bytes, over its ${BUDGETS[id]} budget`);
    assert.equal(m.texture, `/models/${id}-tracks.webp`);
    const built = path.join(DIST, m.texture);
    assert.ok(fs.existsSync(built), `${m.texture} was not built into dist`);
    assert.ok(fs.readFileSync(built).equals(bytes), `${m.texture} is not the committed map`);
    // The page names it for the loader, and nothing else on the page loads it.
    const machinePage = page(`/machines/${m.machine}/`)?.html ?? '';
    assert.match(machinePage, new RegExp(`data-model-texture="${m.texture}"`));
    assert.doesNotMatch(machinePage.replace(/data-model-texture="[^"]*"/, ''), new RegExp(`${id}-tracks`));
  }
});

test('both faces take their tracks as colour, shine and relief, from tokens, with no glow: the top from red or green, the underside from blue', () => {
  const model = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  const tokens = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'tokens.css'), 'utf8');
  assert.match(tokens, /--model-copper: #[0-9a-f]{6};/);
  assert.match(model, /new MeshStandardMaterial\(\{ map: texture\(m\.colour, true\), roughnessMap: surface, metalnessMap: surface, roughness: 1, metalness: 1, bumpMap: texture\(m\.relief, false\), bumpScale: RELIEF \}\)/);
  assert.doesNotMatch(model, /emissive/, 'the tracks glow');
  assert.match(model, /top: \{ cover: \(d, i\) => Math\.max\(d\[i\], d\[i \+ 1\]\), mask: token\('model-pcb'\), copper: token\('model-copper'\), flip: false \}/);
  assert.match(model, /bottom: \{ cover: \(d, i\) => d\[i \+ 2\], mask: token\('model-pcb-under'\), copper: token\('model-copper'\), flip: true \}/);
  // A box's faces are +x, -x, +y, -y, +z, -z: the third is the top, the fourth the underside.
  assert.match(model, /board\.material\[2\] = tracksOn \? top_ : M\.board;/);
  assert.match(model, /board\.material\[3\] = tracksOn \? bottom_ : M\.under;/);
  // The tabs have their own list of faces, so the map goes on the board alone.
  assert.match(model, /BoxGeometry\(S\(t\.out\), H\.board, S\(t\.y1 - t\.y0\)\), \[\.\.\.faces\]\)/);
});

test('Show tracks and Show tracks only are real toggle buttons, labelled, in a named group, pressed state in aria-pressed, shown once the model runs', () => {
  const group = /<div class="model-toggles" role="group" aria-label="Tracks">([\s\S]*?)<\/div>/.exec(section)?.[1] ?? '';
  assert.ok(group, 'no group of track buttons');
  assert.match(group, new RegExp(`<button type="button" class="btn small model-toggle" data-toggle-tracks aria-pressed="true">${TRACK_BUTTONS.tracks}</button>`), 'Show tracks is not a button pressed to start with');
  assert.match(group, new RegExp(`<button type="button" class="btn small model-toggle" data-toggle-tracks-only aria-pressed="false">${TRACK_BUTTONS.only}</button>`), 'Show tracks only is not a button, unpressed to start with');
  for (const label of Object.values(TRACK_BUTTONS)) assert.doesNotMatch(label, /\.$/);
  assert.doesNotMatch(group, /pill/, 'the buttons spend the page\'s lime');
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8');
  assert.match(css, /\[data-model\]:not\(\[data-state="running"\]\) \.model-toggles \{ display: none; \}/);
  assert.match(css, /\.model-toggle\[aria-pressed="true"\]::before \{ background: currentColor; \}/, 'the pressed state is told by colour alone');
  // The wiring: each button flips its state and says so in aria-pressed; tracks only
  // brings the tracks on, and tracks off brings the parts back.
  const model = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  assert.match(model, /tracksButton\?\.setAttribute\('aria-pressed', String\(tracksOn\)\)/);
  assert.match(model, /onlyButton\?\.setAttribute\('aria-pressed', String\(!partsShown\)\)/);
  assert.match(model, /tracksOn = !tracksOn;\s*\/\/[^\n]*\n\s*if \(!tracksOn\) partsShown = true;/);
  assert.match(model, /partsShown = !partsShown;\s*if \(!partsShown\) tracksOn = true;/);
  // The parts fade, then go; with reduced motion at once. A hidden part cannot be clicked.
  assert.match(model, /partsLevel = reduced \? target : /);
  assert.match(model, /parts\.visible = level > 0;/);
  assert.match(fs.readFileSync(path.join(models, 'stage.mjs'), 'utf8'), /\.find\(\(hit\) => shown\(hit\.object\)\)/);
  assert.match(model, /root\.modelLayers = \(\) => \(\{/);
  assert.match(model, /root\.modelBoardPoint = \(x, y, below = false\) =>/);
});

test('the model says how it was made, with its figures read from the committed results, and credits every photograph and drawing with what it took from each', () => {
  const note = /<div class="model-made prose" data-model-made>([\s\S]*?)<\/div>/.exec(section)?.[1] ?? '';
  assert.ok(note, 'the model has no note on how it was made');
  assert.match(note, /<h3 id="model-made">How the model was made<\/h3>/);
  // The paragraphs and the limits are the ones made() writes from src/data/kim-1-model.json, word for word.
  const { paragraphs, limits } = made(figures);
  const text = visibleText(note).replace(/\s+/g, ' ');
  for (const p of [...paragraphs, ...limits]) assert.ok(text.includes(p.replace(/\s+/g, ' ')), `the page does not say: ${p}`);
  // The figures on the page are the results file's: the before and after of the tracks, and the heights.
  const pc = (v) => `${Math.round(v * 100)}%`;
  for (const v of [figures.tracks.top.before, figures.tracks.top.after, figures.tracks.bottom.after]) assert.ok(text.includes(pc(v)), `${pc(v)} is not on the page`);
  assert.ok(Object.values(figures.tracks.leaveOneOut).every((e) => e.after > e.before), 'the fused tracks do not beat the old map against every photograph left out');
  // Every photograph and drawing, in the registry's order, with what was taken from it, its author, its source and its licence.
  const items = [...note.matchAll(/<li data-model-source>([\s\S]*?)<\/li>/g)].map((m) => m[1]);
  const sources = [...kim.photos, ...(kim.drawings ?? [])];
  assert.equal(items.length, sources.length);
  sources.forEach((src, i) => {
    const t = visibleText(items[i]).replace(/\s+([.,:])/g, '$1');
    assert.ok(t.toLowerCase().startsWith(src.used.toLowerCase()), `credit ${i + 1} does not say what was taken: ${t}`);
    assert.ok(t.includes(`by ${src.author}`), `credit ${i + 1} does not name ${src.author}`);
    assert.ok(items[i].includes(`href="${src.sourceUrl}"`), `credit ${i + 1} does not link its source`);
    assert.ok(t.endsWith(`${src.licence === null ? 'no licence stated' : src.licence}.`), `credit ${i + 1} does not give its licence as stated`);
    if (src.licenceUrl) assert.ok(items[i].includes(`href="${src.licenceUrl}"`), `credit ${i + 1} does not link its licence`);
  });
  assert.match(describe(), /copper tracks of both faces traced from photographs/);
});

test('the analysis behind the model is committed: its sources agree with the registry, and its results file is what the page reads', () => {
  const tools = path.join(REPO_ROOT, 'tools', 'kim1-model');
  const sources = JSON.parse(fs.readFileSync(path.join(tools, 'data', 'sources.json'), 'utf8')).sources;
  const photos = sources.filter((s) => s.kind === 'photograph');
  // Every photograph in the analysis is a photograph on the page, and the other way round.
  assert.deepEqual(photos.map((s) => s.file).sort(), kim.photos.map((p) => p.file).sort());
  for (const s of photos) {
    const p = kim.photos.find((x) => x.file === s.file);
    const original = s.original.url.split('/').pop();
    assert.ok(fs.readFileSync(path.join(process.cwd(), 'src', 'assets', 'photos', 'README.md'), 'utf8').includes(s.original.sha256), `the photographs' README does not give ${original}'s SHA-256`);
    assert.ok(p.sourceUrl, `${s.file} has no source in the registry`);
  }
  assert.equal(sources.filter((s) => s.kind === 'drawing').length, (kim.drawings ?? []).length);
  // The results file is made from the analysis's own data, so the two agree.
  const agreement = JSON.parse(fs.readFileSync(path.join(tools, 'data', 'agreement.json'), 'utf8'));
  assert.equal(figures.tracks.top.after, agreement.top.after.replica);
  assert.equal(figures.tracks.top.before, agreement.top.before.replica);
  const registration = JSON.parse(fs.readFileSync(path.join(tools, 'data', 'registration.json'), 'utf8'));
  for (const [id, r] of Object.entries(registration)) assert.equal(figures.registration[id].heldOutMedianMm, r.residualMm.correctedHeldOut.median);
  // Each registration is good to well under a millimetre on blocks held out of its fit.
  for (const [id, r] of Object.entries(figures.registration)) assert.ok(r.heldOutMedianMm < 0.5, `${id} is registered to ${r.heldOutMedianMm} mm`);
  // The analysis runs offline, from the full-size originals; nothing in the build or the tests runs it.
  assert.ok(fs.existsSync(path.join(tools, 'README.md')));
  assert.doesNotMatch(fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8'), /kim1-model/);
});

// The registry says which models a machine has, and MODELS holds what each one
// is (design, "A model is built", rule 2): the two agree both ways.
test('every model in the map is claimed by exactly one machine, with the same view', () => {
  for (const [module, entry] of Object.entries(MODELS)) {
    const by = registry.machines.flatMap((m) => modelsOf(m).filter((c) => c.module === module).map((c) => ({ machine: m.id, view: c.view })));
    assert.equal(by.length, 1, `${module} is claimed by ${by.length} machines in the registry`);
    assert.deepEqual(by[0], { machine: entry.machine, view: entry.view }, `${module}'s entry in src/models/models.mjs is not the registry's claim`);
  }
});

test('every model a machine claims is in the map, with that machine and that view', () => {
  for (const m of registry.machines) {
    for (const c of modelsOf(m)) {
      assert.ok(c.module in MODELS, `${m.id} claims ${c.module}, which src/models/models.mjs does not list`);
      assert.equal(MODELS[c.module].machine, m.id, `${c.module} is listed for ${MODELS[c.module].machine}, not ${m.id}`);
      assert.equal(MODELS[c.module].view, c.view, `${c.module} is listed as the ${MODELS[c.module].view} view, not ${c.view}`);
    }
  }
  assert.deepEqual(modelsOf({ id: 'x' }), []);
});

test('a machine\'s page shows the model section exactly when the machine claims a model, with that model\'s module', () => {
  const running = registry.machines.filter((m) => m.status === 'running');
  assert.ok(running.some((m) => modelsOf(m).length > 0) && running.some((m) => modelsOf(m).length === 0), 'the real registry no longer has a running machine with a model and one without');
  for (const m of running) {
    const built = page(`/machines/${m.id}/`)?.html ?? '';
    assert.ok(built, `/machines/${m.id}/ was not built`);
    const sections = [...built.matchAll(/<section class="model"[^>]*\bdata-model="([^"]+)"/g)].map((x) => x[1]);
    assert.deepEqual(sections, modelsOf(m).map((c) => c.module), `${m.id}'s page shows models ${sections.join(', ') || 'none'}`);
    assert.equal(/src="\/model-loader\.js"/.test(built), modelsOf(m).length > 0, `${m.id}'s page loads the model loader exactly when it has a model`);
  }
});
