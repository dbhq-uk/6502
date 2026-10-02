import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { page, visibleText, DIST } from './helpers.mjs';
import { KIM1_KEYS } from '../src/lib/machines.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import { MODELS, CONTROLS, CONTROLS_DESCRIPTION, TRACK_BUTTONS, modelSrc } from '../src/models/models.mjs';
import { BOARD, TABS, CONTACTS_PER_TAB, PITCH, CHIPS, RAM, LOGIC, CRYSTAL, NAME, HOLES, DISPLAY, KEYPAD, KEY_ROWS, KEYS, SST, PHOTO, PX_PER_MM, TRACKS, decode, describe } from '../src/models/kim-1-layout.mjs';
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
  return at < 0 ? '' : html.slice(at, html.indexOf('<section aria-labelledby="rom"', at));
})();

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
  for (const c of CHIPS) inside(c, 26, 7, c.id);
  for (const [i, c] of RAM.entries()) inside(c, 10, 3.2, `memory chip ${i + 1}`);
  for (const [i, c] of LOGIC.entries()) inside(c, 3.2, 10, `logic chip ${i + 1}`);
  inside(CRYSTAL, CRYSTAL.width / 2, CRYSTAL.depth / 2, 'the crystal');
  inside(NAME, NAME.width / 2, NAME.depth / 2, 'the name');
  for (const [x, y] of HOLES) inside({ x, y }, 1.6, 1.6, 'a hole');
  inside(DISPLAY, DISPLAY.width / 2, DISPLAY.depth / 2, 'the display');
  inside(KEYPAD, KEYPAD.width / 2, KEYPAD.depth / 2, 'the keypad');
  for (const k of KEYS) {
    assert.ok(Math.abs(k.x - KEYPAD.x) + KEYPAD.key / 2 <= KEYPAD.width / 2 && Math.abs(k.y - KEYPAD.y) + KEYPAD.key / 2 <= KEYPAD.depth / 2, `key ${k.name} is off the keypad`);
  }
  for (const a of KEYS) for (const b of KEYS) if (a !== b) assert.ok(Math.abs(a.x - b.x) >= KEYPAD.key || Math.abs(a.y - b.y) >= KEYPAD.key, `keys ${a.name} and ${b.name} overlap`);
  for (const d of DISPLAY.digits) assert.ok(Math.abs(d - DISPLAY.x) + DISPLAY.digitWidth / 2 <= DISPLAY.width / 2, `a digit at ${d} is outside the display`);
  assert.equal(DISPLAY.digits.length, 6);
});

test('the edge contacts are the scale: 22 a tab at 0.156 inch, all of them on their tab', () => {
  assert.equal(CONTACTS_PER_TAB, 22);
  assert.ok(Math.abs(PITCH - 0.156 * 25.4) < 1e-9);
  for (const t of TABS) {
    const last = t.first + (CONTACTS_PER_TAB - 1) * PITCH;
    assert.ok(t.first - 1.2 >= t.y0 && last + 1.2 <= t.y1, `the contacts from ${t.first} to ${last.toFixed(1)} do not fit the tab from ${t.y0} to ${t.y1}`);
  }
  // The 6502 and both 6530s are 40-pin packages, and the 6502 is the top one.
  assert.deepEqual(CHIPS.map((c) => [c.label, c.pins]), [['6502', 40], ['6530', 40], ['6530', 40]]);
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

test('the track map is committed, greyscale, the size the model expects, inside its budget, and built beside the model\'s bundle', async () => {
  assert.ok(fs.existsSync(trackFile), 'src/assets/tracks/kim-1.webp is missing');
  const bytes = fs.readFileSync(trackFile);
  assert.ok(bytes.length <= TRACK_BUDGET, `the track map is ${bytes.length} bytes, over its ${TRACK_BUDGET} budget`);
  const { data, info } = await sharp(bytes).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  assert.equal(info.format, 'raw');
  assert.deepEqual([info.width, info.height], [TRACKS.width, TRACKS.height]);
  assert.equal((await sharp(bytes).metadata()).format, 'webp');
  // Greyscale, white for copper: every pixel has its three channels equal, and
  // copper covers a real share of the board, neither none nor most of it.
  let grey = true;
  let copper = 0;
  for (let i = 0; i < data.length; i += info.channels) {
    if (data[i] !== data[i + 1] || data[i] !== data[i + 2]) grey = false;
    if (data[i] > 127) copper++;
  }
  assert.ok(grey, 'the track map is not greyscale');
  const share = copper / (info.width * info.height);
  assert.ok(share > 0.05 && share < 0.4, `copper covers ${(share * 100).toFixed(1)}% of the map`);
  // It covers the board's body edge to edge, the same box the photograph maps to.
  assert.ok(Math.abs((PHOTO.body[2] - PHOTO.body[0]) - BOARD.width * PX_PER_MM) < 1);
  assert.ok(Math.abs((PHOTO.body[3] - PHOTO.body[1]) - BOARD.depth * PX_PER_MM) < 1);
  // Built into dist beside the bundle, byte for byte, where the model asks for it.
  assert.equal(MODELS['kim-1'].texture, TRACKS.src);
  assert.equal(TRACKS.src, '/models/kim-1-tracks.webp');
  const built = path.join(DIST, TRACKS.src);
  assert.ok(fs.existsSync(built), `${TRACKS.src} was not built into dist`);
  assert.ok(fs.readFileSync(built).equals(bytes), `${TRACKS.src} is not the committed map`);
});

test('the track map is loaded with the model, never with the page, and the deploy checks it is serving', () => {
  const model = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  assert.match(model, /await trackMaps\(TRACKS\.src, token\('model-pcb'\), token\('model-copper'\)\)/);
  assert.match(model, /img\.src = src;\s*await img\.decode\(\);/, 'the map is not loaded as a same-origin image');
  assert.match(section, new RegExp(`data-model-texture="${TRACKS.src}"`));
  // Nothing on the page itself names it: no img, link or preload.
  assert.doesNotMatch(html.replace(/data-model-texture="[^"]*"/, ''), /kim-1-tracks/);
  const deploy = fs.readFileSync(path.join(REPO_ROOT, '.github', 'workflows', 'deploy-site.yml'), 'utf8');
  const listed = (/for f in ([^;]*); do/.exec(deploy)?.[1] ?? '').split(/\s+/);
  for (const m of Object.values(MODELS)) if (m.texture) assert.ok(listed.includes(m.texture.slice(1)), `deploy-site.yml does not check ${m.texture}`);
  // The build copies it; nothing in the build or the tests makes it.
  const pkg = fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8');
  assert.doesNotMatch(pkg, /make-board-tracks/);
  assert.match(fs.readFileSync(path.join(process.cwd(), 'scripts', 'build-models.mjs'), 'utf8'), /fs\.copyFileSync\(tracks, `public\$\{MODELS\[id\]\.texture\}`\)/);
});

test('the top face takes the tracks as colour, shine and relief, from tokens, with no glow, and the underside gets none', () => {
  const model = fs.readFileSync(path.join(models, 'kim-1.js'), 'utf8');
  const tokens = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'tokens.css'), 'utf8');
  assert.match(tokens, /--model-copper: #[0-9a-f]{6};/);
  assert.match(model, /new MeshStandardMaterial\(\{ map: texture\(maps\.colour, true\), roughnessMap: surface, metalnessMap: surface, roughness: 1, metalness: 1, bumpMap: texture\(maps\.relief, false\), bumpScale: RELIEF \}\)/);
  assert.doesNotMatch(model, /emissive/, 'the tracks glow');
  // The top face is the third of a box's six; the underside, the fourth, stays the plain solder side.
  assert.match(model, /board\.material\[2\] = tracksOn \? top : M\.board;/);
  assert.doesNotMatch(model, /material\[3\] =/);
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
  assert.match(model, /root\.modelBoardPoint = \(x, y\) =>/);
});

test('the tracks are credited as traced from the photograph, under its licence, on the page and in the photographs\' README', () => {
  const photo = registry.machines.find((m) => m.id === 'kim-1').photo;
  const credit = /<p class="figure-caption model-credit" data-model-credit>([\s\S]*?)<\/p>/.exec(section)?.[1] ?? '';
  assert.equal(visibleText(credit).replace(/\s+([.,:])/g, '$1'), `Tracks traced from the photograph above, by ${photo.author}, and shared under its licence: ${photo.licence}.`);
  assert.ok(credit.includes(`<a href="${photo.licenceUrl}">`), 'the licence is not linked');
  assert.match(describe(), /copper tracks of its top face traced from that photograph/);
  assert.match(describe(), /underside, which the photograph does not show, is left plain/);
  const readme = fs.readFileSync(path.join(process.cwd(), 'src', 'assets', 'photos', 'README.md'), 'utf8');
  assert.match(readme, /## The track map, src\/assets\/tracks\/kim-1\.webp/);
  assert.match(readme, /share-alike/i);
  assert.match(readme, /node scripts\/make-board-tracks\.mjs/);
});

test('the script that traced the map clears every part the model draws, and the parts it lists by hand lie on the board', async () => {
  const { HIDDEN, THRESHOLDS, modelled } = await import('../scripts/make-board-tracks.mjs');
  const boxes = modelled();
  // Three 40-pin chips, eight memory chips, the logic chips, the crystal, the display and the keypad.
  assert.equal(boxes.length, CHIPS.length + RAM.length + LOGIC.length + 3);
  for (const [x0, y0, x1, y1] of [...boxes, ...HIDDEN]) {
    assert.ok(x0 < x1 && y0 < y1, `the box ${x0}, ${y0}, ${x1}, ${y1} is empty`);
    assert.ok(x0 >= 0 && y0 >= 0 && x1 <= BOARD.width + 1e-9 && y1 <= BOARD.depth + 1e-9, `the box ${x0}, ${y0}, ${x1}, ${y1} is off the board`);
  }
  // The tracks' hue band leaves out the parts' colours, as measured: cream capacitors at 85 to 89 degrees, resistors 50 to 65, gold 74.
  assert.ok(THRESHOLDS.hue[0] > 89 && THRESHOLDS.weak.hue[0] > 89);
});
