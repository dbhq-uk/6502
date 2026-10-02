import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { page, visibleText, DIST } from './helpers.mjs';
import { KIM1_KEYS } from '../src/lib/machines.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import { MODELS, modelSrc } from '../src/models/models.mjs';
import { BOARD, TABS, CONTACTS_PER_TAB, PITCH, CHIPS, RAM, LOGIC, CRYSTAL, NAME, HOLES, DISPLAY, KEYPAD, KEY_ROWS, KEYS, SST, decode, describe } from '../src/models/kim-1-layout.mjs';

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
