import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { page, visibleText, modelPanel, DIST } from './helpers.mjs';

// The BBC Micro's two 3D models on its page (task 9b of the BBC Micro models
// plan, 13 October 2026): the two thin entries, their MODELS entries, the
// registry's claim of both views with "case": true, the credits each view
// gives, and the words that must reach the page.
//
// Every module is imported inside a test, never at the top of this file: when a
// module imported at the top of a test file fails to load, node's runner reports
// the tests after it as neither passed nor failed, and the file's summary says 0
// failed (task 9a's journal, "Mistakes"). Here a module that fails to import
// fails its test, loudly.

const ROOT = path.resolve(process.cwd(), '..');
const MODELS_DIR = path.join(process.cwd(), 'src', 'models');
const read = (p) => fs.readFileSync(p, 'utf8');
const json = (p) => JSON.parse(read(p));
const BOARD = 'bbc-micro-board';
const CASE = 'bbc-micro-case';
const boardFigures = json(path.join(process.cwd(), 'src', 'data', `${BOARD}-model.json`));
const caseFigures = json(path.join(process.cwd(), 'src', 'data', `${CASE}-model.json`));
const sources = json(path.join(ROOT, 'tools', 'bbc-micro-model', 'data', 'sources.json')).sources;
const registry = json(path.join(ROOT, 'machines', 'registry.json'));
const bbc = registry.machines.find((m) => m.id === 'bbc-micro');
const html = page('/machines/bbc-micro/')?.html ?? '';
const flat = (s) => s.replace(/\s+/g, ' ').trim();
// The licences the credits give for sources that state one: the registry's short form, and the terms that
// sources.json records each source stating, which the short form must not go beyond.
const CHRIS = 'CC BY-SA 3.0, as the site\'s home page states (an older paragraph on the same page asks to be asked first)';
const LICENCES = {
  D1: { short: 'Copyright Acorn Computers Limited 1985: may not be adapted or reproduced without Acorn\'s prior written approval: facts read from it, nothing copied', terms: [/^Copyright ACORN Computers Limited 1985\./, /may be adapted or reproduced/, /prior written approval/] },
  K1: { short: 'MIT', terms: [/^MIT License/] },
  I6: { short: CHRIS, terms: [/CC-BY-SA 3\.0/, /please ask me first/] },
  'O8-I': { short: CHRIS, terms: [/CC-BY-SA 3\.0/, /please ask me first/] },
};

test('the two entries and every module they bundle import in Node and export what the page uses; a module that fails to import fails here', async () => {
  for (const file of [`${BOARD}.js`, `${CASE}.js`]) {
    const mod = await import(path.join(MODELS_DIR, file));
    assert.equal(typeof mod.mount, 'function', `${file} does not export mount(root)`);
    assert.deepEqual(Object.keys(mod), ['mount'], `${file} exports more than mount(root): the drawing belongs in its scene module`);
  }
  const want = {
    [`${BOARD}-scene.mjs`]: ['buildBoard', 'stageOptions', 'trackMaps', 'trackMaterials', 'createLayers', 'canvasPrinter', 'X', 'Z'],
    [`${BOARD}-layout.mjs`]: ['BOARD', 'FITTED', 'COUNTED', 'TRACKS', 'STATUS', 'marksFrom', 'pagedRef', 'pointWords', 'chipLegend', 'describe', 'LABEL', 'LEGEND_WORDS', 'TRACKS_HELP'],
    [`${BOARD}-notes.mjs`]: ['made'],
    [`${CASE}-scene.mjs`]: ['buildCase', 'stageOptions', 'atlasItems', 'drawAtlas'],
    [`${CASE}-layout.mjs`]: ['createKeyState', 'litLeds', 'ledWords', 'press', 'pointWords', 'STATUS', 'LABEL', 'HELP', 'describe'],
    [`${CASE}-notes.mjs`]: ['made'],
    'access.mjs': ['createSampler'],
    'models.mjs': ['MODELS'],
  };
  for (const [file, names] of Object.entries(want)) {
    const mod = await import(path.join(MODELS_DIR, file));
    for (const name of names) assert.notEqual(mod[name], undefined, `${file} does not export ${name}`);
  }
});

test('the BBC Micro states its case and claims both views at once, outside then inside, and MODELS has both entries as the plan gives them', async () => {
  const { MODELS } = await import('../src/models/models.mjs');
  const boardLayout = await import('../src/models/bbc-micro-board-layout.mjs');
  const caseLayout = await import('../src/models/bbc-micro-case-layout.mjs');
  assert.equal(bbc.case, true);
  assert.deepEqual(bbc.models, [{ view: 'outside', module: CASE }, { view: 'inside', module: BOARD }]);
  const c = MODELS[CASE];
  assert.deepEqual([c.machine, c.view, c.label, c.about, c.help], ['bbc-micro', 'outside', caseLayout.LABEL, caseLayout.describe(), caseLayout.HELP]);
  assert.equal(typeof c.made, 'function');
  assert.equal(c.texture, undefined);
  const b = MODELS[BOARD];
  assert.deepEqual([b.machine, b.view, b.label, b.about, b.texture, b.tracksHelp, b.legendWords], ['bbc-micro', 'inside', boardLayout.LABEL, boardLayout.describe(), boardLayout.TRACKS.src, boardLayout.TRACKS_HELP, boardLayout.LEGEND_WORDS]);
  assert.equal(typeof b.made, 'function');
  // The legend is chipLegend's rows, each counted chip named by its counter as the shared legend table reads it (`chip`).
  const rows = b.legend();
  assert.deepEqual(rows.map(({ chip, ...rest }) => rest), boardLayout.chipLegend());
  for (const r of rows) assert.equal(r.chip, r.sheila, r.ref);
  // On the page: the views' section, Outside first and shown, Inside hidden, each a panel of its own model.
  assert.match(html, /<section class="model" aria-labelledby="model" data-model-section hidden>/);
  assert.ok(modelPanel(html, CASE).startsWith('<div class="model-panel" role="tabpanel" id="model-panel-outside"'), 'the outside is not the first panel');
  assert.match(modelPanel(html, BOARD), /^<div class="model-panel" role="tabpanel" id="model-panel-inside"[^>]*\shidden[\s>]/);
  // The head photograph no longer says the BBC Micro has no model.
  assert.doesNotMatch(bbc.photos[0].used, /no 3D model/);
});

test('each view credits exactly the sources its results file lists, each once, as sources.json records them, and never the photograph at the head of the page', () => {
  const credited = [...bbc.photos, ...(bbc.drawings ?? []), ...(bbc.references ?? [])];
  for (const [module, figures] of [[CASE, caseFigures], [BOARD, boardFigures]]) {
    const panel = modelPanel(html, module);
    assert.ok(panel, `the page has no ${module} panel`);
    const items = [...panel.matchAll(/<li data-model-source>([\s\S]*?)<\/li>/g)].map((m) => m[1]);
    const own = figures.sources;
    assert.equal(items.length, own.length, `${module}: ${items.length} credits for ${own.length} sources`);
    for (const s of own) {
      const at = credited.filter((r) => r.sourceUrl === s.url || r.sourceUrl === s.page);
      assert.equal(at.length, 1, `${module}: ${s.id} is credited ${at.length} times in the registry`);
      const r = at[0];
      const item = items.find((i) => i.includes(`href="${r.sourceUrl.replace(/&/g, '&amp;')}"`));
      assert.ok(item, `${module}: no credit links ${s.id} (${r.sourceUrl})`);
      const text = visibleText(item).replace(/\s+([.,:])/g, '$1');
      assert.ok(text.includes(`by ${s.author}`), `${module}: ${s.id}'s credit does not name ${s.author}: ${text}`);
      // sources.json records a source that states no licence as null or as "none stated"; the page says "no licence stated".
      if (s.licence === null || /^none stated/i.test(s.licence)) assert.match(text, /no licence stated\.$/, `${module}: ${s.id}`);
      else if (r.file) assert.equal(r.licence, s.licence, `${module}: ${s.id}, a photograph on the page, is not credited with its licence as sources.json records it`);
      else {
        // Said short on the page, in full here: the registry's words, and the terms sources.json records the source stating.
        assert.equal(r.licence, LICENCES[s.id]?.short, `${module}: ${s.id}'s licence is "${r.licence}"`);
        for (const term of LICENCES[s.id].terms) assert.match(s.licence, term, `${module}: sources.json does not record ${s.id} stating ${term}`);
        assert.ok(text.endsWith(`${r.licence}.`), `${module}: ${s.id}'s credit does not end with its licence: ${text}`);
      }
      // A reference is not committed, so it names the SHA-256 of the original measured, and when it was fetched.
      if (r.sha256 !== undefined || !r.file) {
        assert.equal(r.sha256, s.sha256, `${s.id}: the SHA-256 is not the original's`);
        assert.equal(r.fetched, s.fetched, `${s.id}: fetched`);
      }
    }
    assert.ok(!panel.includes(`href="${bbc.photos[0].sourceUrl}"`), `${module} credits the photograph at the head of the page`);
  }
  // Every reference the BBC Micro has is a source of one view or the other: nothing credited that was not used.
  const used = [...boardFigures.sources, ...caseFigures.sources].flatMap((s) => [s.url, s.page]);
  for (const r of bbc.references ?? []) assert.ok(used.includes(r.sourceUrl), `the reference ${r.sourceUrl} is not a source of either view`);
  // Chris's Acorns' photographs carry its byline and its licence as the site states it; K1 is read from the dbhq-uk fork.
  for (const id of ['I6', 'O8-I']) {
    const r = bbc.references.find((x) => x.sourceUrl === sources.find((s) => s.id === id).url);
    assert.ok(r, `${id} is not a reference`);
    assert.match(r.author, /^Chris Whytehead \(Chris's Acorns\)$/);
    assert.match(r.licence, /^CC BY-SA 3\.0/);
    assert.match(r.licence, /ask/, `${id}: the older sentence on the same site asking to be asked first is not said`);
    // The byline links the site, as its licence asks; and on the page the author's name is that link.
    assert.equal(r.authorUrl, 'http://chrisacorns.computinghistory.org.uk/');
    for (const module of [BOARD, CASE]) {
      const item = [...modelPanel(html, module).matchAll(/<li data-model-source>([\s\S]*?)<\/li>/g)].map((m) => m[1]).find((i) => i.includes(`href="${r.sourceUrl}"`));
      if (item) assert.ok(item.includes(`by <a href="${r.authorUrl}">${r.author.replace(/'/g, '&#39;')}</a>`), `${id}'s credit does not link Chris's Acorns: ${item}`);
    }
  }
  assert.match(bbc.references.find((x) => x.sha256 === sources.find((s) => s.id === 'K1').sha256).sourceUrl, /^https:\/\/github\.com\/dbhq-uk\/bbc-keyboard\//);
});

test('the words that must reach the page do: each caption, note and limit word for word, the copper traced to look at with its failed checks, and the case\'s size the manual\'s with the measures that disagree and the front edge not measurable', async () => {
  const boardLayout = await import('../src/models/bbc-micro-board-layout.mjs');
  const boardNotes = await import('../src/models/bbc-micro-board-notes.mjs');
  const caseLayout = await import('../src/models/bbc-micro-case-layout.mjs');
  const caseNotes = await import('../src/models/bbc-micro-case-notes.mjs');
  const inside = flat(visibleText(modelPanel(html, BOARD)));
  const outside = flat(visibleText(modelPanel(html, CASE)));
  for (const [text, about, note, own] of [[inside, boardLayout.describe(), boardNotes.made(boardFigures), [boardLayout.TRACKS_HELP, ...boardLayout.LEGEND_WORDS]], [outside, caseLayout.describe(), caseNotes.made(caseFigures), [caseLayout.HELP]]]) {
    for (const s of [about, ...note.paragraphs, ...note.limits, ...own]) assert.ok(text.includes(flat(s)), `the page does not say: ${s.slice(0, 120)}`);
  }
  // Every limit, by its key phrase, in the notes as made() writes them and on the page (review of task 9b): a limit
  // reworded to say less fails here even when the page is built from it.
  const LIMITS = {
    outside: [[/the Service Manual's, not measured/, /measures point different ways/, /that is not resolved/], [/front edge's check could not be measured/], [/^These are typical, not measured:/], [/tapered blocks with flat tops, not sculpted/], [/shifted symbols[^.]*are not drawn/, /space bar is blank/, /not in Acorn's or the BBC's lettering/], [/owl[^.]*is not drawn/], [/not measured are not drawn/], [/copper is traced to look at, with its connections not verified/]],
    inside: [[/traced to look at, not as a netlist: its connections are not verified/], [/print hides the copper under it/, /drawn as print only/], [/typical ones for their packages, not measured/], [/plain cylinders and boxes of typical sizes/], [/are not drawn\.$/], [/traced to look at, has \d+ pixels to the millimetre/]],
  };
  for (const [view, note, text] of [['outside', caseNotes.made(caseFigures), outside], ['inside', boardNotes.made(boardFigures), inside]]) {
    assert.equal(note.limits.length, LIMITS[view].length, `${view}: a limit added or dropped`);
    note.limits.forEach((limit, i) => {
      for (const phrase of LIMITS[view][i]) {
        assert.match(limit, phrase, `${view}'s limit ${i + 1} does not say ${phrase}: ${limit}`);
        assert.match(text, new RegExp(phrase.source.replace(/^\^|\$$/g, '')), `${view}'s page does not say ${phrase}`);
      }
    });
  }
  // The note says the x scale's verdict in a visitor's words, not the plan's (review of task 9b).
  assert.doesNotMatch(inside, /the plan judges/);
  assert.ok(inside.includes(boardNotes.VERDICT_WORDS[boardFigures.scale.xVerdict]), 'the x scale\'s verdict is not in a visitor\'s words');
  // The inside: its copper traced to look at, its connections not verified, beside the two checks that failed (the nets, and I5).
  assert.match(inside, /traced to look at[^.]*not verified/);
  assert.match(inside, /ground pins of \d+ chips[^.]*failed/);
  assert.match(inside, /photograph of a populated board[^.]*overlap by [\d.]+ of their union, and that check failed too/);
  // The outside: the size is the manual's, the photographs' measures disagree with it, unresolved, and the front edge was not measurable.
  assert.ok(outside.includes(flat(caseNotes.sizeSentence(caseFigures))), 'the size sentence is not on the page');
  assert.match(outside, /Service Manual's/);
  assert.match(outside, /not resolved/);
  assert.match(outside, /front edge's check is not measurable/i);
  assert.match(outside, /traced to look at/);
  // The legend table: every fitted chip once, a counted chip's rate cell named by its counter.
  const rows = [...modelPanel(html, BOARD).matchAll(/<tr data-legend-ref="([^"]+)"( data-legend-chip="([^"]+)")?/g)].map((m) => [m[1], m[3] ?? null]);
  assert.deepEqual(rows, boardLayout.chipLegend().map((r) => [r.ref, r.sheila ?? null]));
  for (const chip of boardLayout.COUNTED) assert.match(modelPanel(html, BOARD), new RegExp(`<td data-legend-rate="${chip}">not running</td>`), chip);
});

test('the inside\'s entry is thin and follows the machine: the stage, the scene, panel.bbc\'s counters sampled every quarter second from a fresh baseline on bbc:start, the paged ROM framed, and the test hooks', () => {
  const src = read(path.join(MODELS_DIR, `${BOARD}.js`));
  assert.match(src, /const s = createStage\(root, stageOptions\(\)\);/);
  assert.match(src, /buildBoard\(scene, token, \{ printer: canvasPrinter\(token, mono\) \}\)/);
  assert.match(src, /await trackMaps\(TRACKS\.src, token\)/);
  assert.match(src, /createLayers\(board, /);
  assert.match(src, /document\.querySelector\('\[data-bbc\]'\)/);
  assert.match(src, /createSampler\(\{ counted: COUNTED, counts: \(\) => bbc\(\)\?\.accessCounts\?\.\(\) \?\? null, now: \(\) => performance\.now\(\) \}\)/);
  assert.match(src, /panel\?\.addEventListener\('bbc:start', \(\) => \{\s*running = true;\s*status\.textContent = STATUS\.running;\s*restart\(\);/);
  assert.match(src, /const restart = \(\) => \{\s*sampler\.reset\(\);\s*show\(null\);/);
  assert.match(src, /setInterval\(\(\) => \{\s*if \(!running\) return;\s*show\(sampler\.sample\(\)\);\s*\}, SAMPLE_MS\);/);
  assert.match(src, /const SAMPLE_MS = 250;/);
  assert.match(src, /marksFrom\(rates\)/);
  assert.match(src, /pagedRef\(bbc\(\)\?\.romSlot\?\.\(\)\)/);
  assert.match(src, /pointWords\(ref, \{ marked, paged \}\)/);
  assert.match(src, /every\(\(dt\) => layers\.step\(dt, reduced\)\)/);
  for (const hook of ['root.dataset.modelAccessed', 'root.dataset.modelPaged', 'root.dataset.modelRates', 'root.dataset.modelTracks', 'root.dataset.modelParts', 'root.modelChipPoint = ', 'root.modelBoardPoint = (x, y, below = false) =>', 'root.modelLayers = ']) assert.ok(src.includes(hook), hook);
  assert.match(src, /row\.toggleAttribute\('data-accessed', marked\.includes\(ref\)\)/);
  assert.match(src, /row\.toggleAttribute\('data-pointed', r === ref\)/);
  // Thin: no geometry of its own, and every word from the layout.
  assert.doesNotMatch(src, /new (BoxGeometry|PlaneGeometry|CylinderGeometry|BufferGeometry|MeshStandardMaterial)\(/);
  assert.doesNotMatch(src.replace(/^\s*\/\/.*$/gm, ''), /textContent = '/);
});

test('the outside\'s entry is thin and follows the machine: its keys from bbc:key on the page\'s clock, its lights from panel.bbc.leds() each frame, a click through press(), and the test hooks', () => {
  const src = read(path.join(MODELS_DIR, `${CASE}.js`));
  assert.match(src, /const s = createStage\(root, stageOptions\(\)\);/);
  assert.match(src, /await drawAtlas\(atlasItems\(\), token, family\)/);
  assert.match(src, /buildCase\(scene, token, \{ atlas \}\)/);
  assert.match(src, /document\.querySelector\('\[data-bbc\]'\)/);
  assert.match(src, /const keys = createKeyState\(\);/);
  assert.match(src, /panel\?\.addEventListener\('bbc:key', \(e\) => \{\s*if \(!keys\.on\(e\.detail, performance\.now\(\)\)\) return;/);
  assert.match(src, /panel\?\.addEventListener\('bbc:start', \(\) => \{\s*keys\.reset\(\);/);
  assert.match(src, /every\(\(dt, now\) => \{/);
  assert.match(src, /const down = keys\.down\(now\);/);
  assert.match(src, /model\.setKeys\(down\)/);
  assert.match(src, /const lit = litLeds\(bbc\(\)\?\.leds\?\.\(\) \?\? null\);/);
  assert.match(src, /model\.setLeds\(lit\)/);
  assert.match(src, /status\.textContent = press\(o\.userData\.key, panel\)/);
  // A click's line stands: the pointing that the same mouse movement queued is dropped, and the part clicked counts
  // as pointed, so the hover's name for it does not replace what the click said (CI round 1, 13 October 2026).
  assert.match(src, /s\.onClick\(\(\) => model\.pickable, \(hit\) => \{\s*if \(pending\) \{ cancelAnimationFrame\(pending\); pending = null; \}\s*pointed = pointWords\(partOf\(hit\) \?\? \{\}\);/);
  for (const hook of ['root.dataset.modelPressed', 'root.dataset.modelPresses', 'root.dataset.modelDown', 'root.dataset.modelLeds', 'root.modelKeyPoint = ']) assert.ok(src.includes(hook), hook);
  assert.doesNotMatch(src, /new (BoxGeometry|PlaneGeometry|CylinderGeometry|BufferGeometry|MeshStandardMaterial)\(/);
  assert.doesNotMatch(src.replace(/^\s*\/\/.*$/gm, ''), /textContent = '/);
});

test('nothing of either model is in the page\'s first load, and each later file is inside its budget: the bundles 200,000 bytes gzipped, the track map 600,000 bytes', async () => {
  const zlib = await import('node:zlib');
  const { modelSrc, MODELS } = await import('../src/models/models.mjs');
  const scripts = [...html.matchAll(/<script\b[^>]*\bsrc="([^"]+)"/g)].map((m) => m[1]);
  assert.deepEqual(scripts.filter((s) => s.startsWith('/models/') || /three/.test(s)), []);
  assert.doesNotMatch(html, /<link\b[^>]*rel="modulepreload"/);
  const later = [...html.matchAll(/data-model-(?:src|texture)="(\/[^"]+)"/g)].map((m) => m[1]);
  assert.deepEqual(later, [modelSrc(CASE), modelSrc(BOARD), MODELS[BOARD].texture]);
  for (const id of [CASE, BOARD]) assert.ok(zlib.gzipSync(fs.readFileSync(path.join(DIST, modelSrc(id))), { level: 9 }).length <= 200_000, id);
  assert.ok(fs.statSync(path.join(DIST, MODELS[BOARD].texture)).size <= 600_000, 'the track map');
});

test('the browser check runs the BBC Micro\'s models after its machine, with the NES\'s settled() imported, not copied, and the other machines\' sections as they were', async () => {
  const main = read(path.join(process.cwd(), 'scripts', 'browser-check.mjs'));
  assert.match(main, /import \{ checkBbcMicro, checkBbcModels \} from '\.\/browser-check-bbc\.mjs';/);
  assert.match(main, /await checkBbcMicro\(\{ browser, watch, problems, origin \}\);\s*await checkBbcModels\(\{ browser, watch, problems, origin \}\);/);
  const check = read(path.join(process.cwd(), 'scripts', 'browser-check-bbc.mjs'));
  assert.match(check, /import \{ settled \} from '\.\/browser-check-nes\.mjs';/);
  assert.doesNotMatch(check, /const settled = /);
  assert.match(check, /export async function checkBbcModels\(/);
  // What it checks, by the problems it can report.
  // The *DUMP step judges only what *DUMP shows, on a file long enough to keep the DFS paged for seconds.
  assert.match(check, /await page\.evaluate\(\(\) => window\.bbcInside\.paged\.clear\(\)\);\s*await type\(\['Shift\+Quote', 'KeyD'/);
  const { dumpDisc, testDisc } = await import('../scripts/browser-check-bbc.mjs');
  const d = dumpDisc();
  assert.equal(d[256 + 8 + 5], 8, 'the file is not 2 KB');
  assert.equal(d[256 + 8 + 4], 0);
  assert.equal(d[256 + 8 + 7], 2);
  assert.deepEqual([...d.slice(0, 512)].map((b, i) => (i === 256 + 8 + 5 ? 1 : b)), [...testDisc().slice(0, 512)], 'the catalogue is not the test disc\'s but for the length');
  for (const what of ['before the visitor reached it', 'fetched the inside', 'canvas is blank', 'caps lock', 'cassette motor', 'did not put A down', 'did not type C', 'under the case', 'Right on the Outside tab', 'IC78', 'the DFS', 'reduced motion', 'Tab never left']) assert.ok(check.includes(what), what);
});
