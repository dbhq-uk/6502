import test from 'node:test';
import assert from 'node:assert/strict';
import { MODELS, wordsFor, regionProblems, regionViews, shownFor } from '../src/models/models.mjs';
import { page, visibleText, modelPanel } from './helpers.mjs';

const made = () => 'note';
const both = {
  machine: 'nes-famicom', view: 'inside', regions: ['ntsc', 'pal'],
  label: { ntsc: 'NTSC label', pal: 'PAL label' },
  about: { ntsc: 'NTSC caption', pal: 'PAL caption' },
  made: { ntsc: made, pal: made },
};

test('a model with one console has one set of words, whatever region is asked for by default', () => {
  const kim = MODELS['kim-1'];
  assert.deepEqual(wordsFor(kim), { label: kim.label, about: kim.about, made: kim.made });
});

test('a model that draws two consoles gives each its own words, the first region by default', () => {
  assert.deepEqual(wordsFor(both), { label: 'NTSC label', about: 'NTSC caption', made });
  assert.equal(wordsFor(both, 'pal').about, 'PAL caption');
  assert.throws(() => wordsFor(both, 'dendy'), /draws ntsc and pal, not dendy/);
});

test('a model\'s regions are checked: listed once, each with a label, a caption and a note, and nothing else keyed', () => {
  for (const [module, entry] of Object.entries(MODELS)) assert.deepEqual(regionProblems(module, entry), [], module);
  assert.deepEqual(regionProblems('m', both), []);
  assert.deepEqual(regionProblems('m', { ...both, regions: [] }), ['m: regions, when given, must be a non-empty list']);
  assert.deepEqual(regionProblems('m', { ...both, regions: ['ntsc', 'ntsc'], label: { ntsc: 'a' }, about: { ntsc: 'b' }, made: { ntsc: made } }), ['m: a region is listed twice']);
  assert.deepEqual(regionProblems('m', { ...both, about: { ntsc: 'NTSC caption' } }), ['m: no about for pal']);
  assert.deepEqual(regionProblems('m', { ...both, made: { ntsc: made } }), ['m: made has no note for pal']);
  assert.deepEqual(regionProblems('m', { ...both, label: { ...both.label, dendy: 'x' } }), ['m: label has dendy, which is not in regions']);
  assert.deepEqual(regionProblems('m', { ...both, regions: ['NTSC', 'pal'], label: { NTSC: 'a', pal: 'b' }, about: { NTSC: 'a', pal: 'b' }, made: { NTSC: made, pal: made } }), ['m: region "NTSC" must be a lower-case word']);
});
test('the page draws every console\'s words, the first shown and the rest hidden, and a one-console model as it always was', () => {
  const views = regionViews(both);
  assert.deepEqual(views.map((v) => [v.region, v.hidden, v.suffix, v.label, v.about]), [
    ['ntsc', false, '-ntsc', 'NTSC label', 'NTSC caption'],
    ['pal', true, '-pal', 'PAL label', 'PAL caption'],
  ]);
  assert.ok(views.every((v) => v.made === made));
  const kim = MODELS['kim-1'];
  assert.deepEqual(regionViews(kim), [{ region: null, hidden: false, suffix: '', label: kim.label, about: kim.about, made: kim.made }]);
});

test('the model component reads its words through the regions, never from the entry\'s fields', async () => {
  const fs = await import('node:fs');
  const src = fs.readFileSync(new URL('../src/components/ModelPanel.astro', import.meta.url), 'utf8');
  assert.match(src, /regionViews\(model\)/);
  assert.doesNotMatch(src, /model\.(label|about|made)\b/);
});

// The regions as the built page has them, on the one page whose models draw two
// consoles: the NES's (this replaced a test of the component's source, 6 Oct 2026,
// after the review of task 1 found no test rendered the regions at all). Since
// task 9 each model is a view in its own tab panel, so its ids carry the view
// as well as the console (model-about-inside-pal).
test('on the NES\'s page each console has one caption, one note and one hidden name in each model, only the first console\'s shown, every id once', () => {
  const entries = Object.entries(MODELS).filter(([, e]) => e.regions?.length > 1);
  assert.ok(entries.length > 0, 'no model draws two consoles');
  for (const [module, entry] of entries) {
    const html = page(`/machines/${entry.machine}/`)?.html ?? '';
    const section = modelPanel(html, module);
    assert.ok(section, `/machines/${entry.machine}/ has no model ${module}`);
    const tabbed = section.startsWith('<div class="model-panel"');
    const ids = (what, r) => `${what}${tabbed ? `-${entry.view}` : ''}-${r}`;
    const tags = (re) => [...section.matchAll(re)].map((m) => m[0]);
    const regionOf = (tag) => /data-model-region="([a-z]+)"/.exec(tag)?.[1];
    const hidden = (tag) => /\shidden(?=[\s>])/.test(tag);
    const captions = tags(/<p class="figure-caption model-about"[^>]*>/g);
    const notes = tags(/<div class="model-made prose"[^>]*>/g);
    const names = tags(/<span [^>]*data-model-label[^>]*>/g);
    for (const [what, list] of [['caption', captions], ['note', notes], ['name', names]]) {
      assert.deepEqual(list.map(regionOf), entry.regions, `${module}: one ${what} for each console, in the entry's order`);
    }
    assert.deepEqual(captions.map(hidden), entry.regions.map((_, i) => i > 0), `${module}: only the first console's caption is shown`);
    assert.deepEqual(notes.map(hidden), entry.regions.map((_, i) => i > 0), `${module}: only the first console's note is shown`);
    assert.ok(names.every(hidden), 'a console\'s accessible name is shown as text');
    // Each console's words are its own, and the stage is named and described by the first's.
    entry.regions.forEach((r, i) => {
      const words = wordsFor(entry, r);
      assert.ok(section.includes(`id="${ids('model-about', r)}"`), `no caption with the id ${ids('model-about', r)}`);
      assert.ok(section.includes(`id="${ids('model-made', r)}"`), `no note with the id ${ids('model-made', r)}`);
      assert.ok(visibleText(section).includes(visibleText(words.about)), `${r}'s caption is not its own`);
      assert.equal(visibleText(names[i] + section.slice(section.indexOf(names[i]) + names[i].length).split('</span>')[0]), words.label);
    });
    const stage = /<section class="model-stage"[^>]*>/.exec(section)?.[0] ?? '';
    assert.ok(stage.includes(`aria-describedby="${ids('model-about', entry.regions[0])}"`));
    assert.ok(stage.includes(`aria-label="${wordsFor(entry).label.replaceAll("'", '&#39;')}"`) || stage.includes(`aria-label="${wordsFor(entry).label}"`));
    // Every id on the page is used once.
    const all = [...html.matchAll(/\sid="([^"]+)"/g)].map((m) => m[1]);
    assert.deepEqual(all.filter((id, i) => all.indexOf(id) !== i), [], 'an id is used twice on the page');
    // Whatever else is marked for a console (the legend's parts) is marked for each, the first shown.
    const marked = tags(/<[a-z]+ [^>]*data-model-region="[a-z]+"[^>]*>/g).filter((t) => !/data-model-label|model-about|model-made/.test(t));
    for (const r of entry.regions) assert.equal(marked.filter((t) => regionOf(t) === r).length, marked.length / entry.regions.length, `${r} has not as many marked parts as the others`);
    assert.ok(marked.every((t) => hidden(t) === (regionOf(t) !== entry.regions[0])), 'a part marked for a console other than the first is shown, or the first\'s is hidden');
  }
});

// Review Focus 5 of the NES models plan: a model that draws the NTSC console only, on a page set to PAL.
test('a model that does not draw the page\'s console shows its own first console, labelled as that, and says there is no model of the page\'s and why', () => {
  const ntscOnly = { ...both, regions: ['ntsc'], label: { ntsc: 'NTSC label' }, about: { ntsc: 'NTSC caption' }, made: { ntsc: made }, missing: { pal: 'the PAL board could not be shown to share the NTSC board\'s layout' } };
  assert.deepEqual(regionProblems('m', { ...ntscOnly, missing: undefined }), []);
  const shown = shownFor(ntscOnly, 'pal');
  assert.equal(shown.region, 'ntsc');
  assert.equal(shown.label, 'NTSC label');
  assert.equal(shown.missing, 'There is no model of the PAL console, because the PAL board could not be shown to share the NTSC board\'s layout, so this is the NTSC console\'s.');
  assert.match(shownFor({ ...ntscOnly, missing: undefined }, 'pal').missing, /no model of the PAL console, because none was built/);
  // The console the page names, when the model draws it, with nothing to say; and a one-console model, whatever the page says.
  assert.deepEqual(shownFor(both, 'pal'), { region: 'pal', label: 'PAL label', missing: null });
  assert.deepEqual(shownFor(ntscOnly, 'ntsc'), { region: 'ntsc', label: 'NTSC label', missing: null });
  assert.deepEqual(shownFor(MODELS['kim-1'], 'pal'), { region: null, label: MODELS['kim-1'].label, missing: null });
  // Both NES models draw both consoles, so neither page console is ever missing.
  for (const [module, e] of Object.entries(MODELS).filter(([, x]) => x.regions)) for (const r of e.regions) assert.equal(shownFor(e, r).missing, null, `${module}: ${r}`);
});

// Review Focus 5, on the page: a model's region path (followRegion, which both NES modules call from their setRegion)
// driven on a stand-in for a model's panel that draws the NTSC console alone, with the page set to PAL (fix round 1 of
// task 9, 6 Oct 2026: the helper alone was tested before, and no module called it).
class Node_ {
  constructor(tag, attrs = {}, text = '') { this.tag = tag; this.attrs = { ...attrs }; this.textContent = text; this.children = []; this.dataset = {}; for (const [k, v] of Object.entries(attrs)) if (k.startsWith('data-')) this.dataset[k.slice(5).replace(/-([a-z])/g, (_, c) => c.toUpperCase())] = v; }
  add(...c) { this.children.push(...c); return this; }
  get hidden() { return 'hidden' in this.attrs; }
  set hidden(on) { if (on) this.attrs.hidden = ''; else delete this.attrs.hidden; }
  get id() { return this.attrs.id ?? ''; }
  hasAttribute(n) { return n in this.attrs; }
  getAttribute(n) { return this.attrs[n] ?? null; }
  setAttribute(n, v) { this.attrs[n] = String(v); }
  /** Compound selectors of classes and attributes, as followRegion uses them: .a[b][c="d"]. */
  matches(sel) {
    const parts = sel.match(/\.[\w-]+|\[[\w-]+(?:="[^"]*")?\]/g);
    if (!parts || parts.join('') !== sel) throw new Error(`the stand-in does not know ${sel}`);
    return parts.every((p) => {
      if (p.startsWith('.')) return (this.attrs.class ?? '').split(' ').includes(p.slice(1));
      const [, n, v] = /^\[([\w-]+)(?:="([^"]*)")?\]$/.exec(p);
      return n in this.attrs && (v === undefined || this.attrs[n] === v);
    });
  }
  *all() { for (const c of this.children) { yield c; yield* c.all(); } }
  querySelectorAll(sel) { return [...this.all()].filter((e) => e.matches(sel)); }
  querySelector(sel) { return this.querySelectorAll(sel)[0] ?? null; }
}

test('a model that does not draw the page\'s console, on the page: its first console stays drawn and named, and the status line says there is no model of the page\'s', async () => {
  const { followRegion } = await import('../src/models/regions.mjs');
  const stage = new Node_('section', { 'data-model-stage': '' }).add(new Node_('span', { 'data-model-region': 'ntsc', 'data-model-label': '', hidden: '' }, 'NTSC label'));
  const status = new Node_('p', { 'data-model-status': '' }, 'The model is running.');
  const caption = new Node_('p', { class: 'figure-caption model-about', id: 'model-about-outside-ntsc', 'data-model-region': 'ntsc' }, 'NTSC caption');
  const root = new Node_('div', { 'data-model': 'm' }).add(stage, status, caption);
  // Mounting with the page on PAL: the NTSC console is drawn, named and described, and the line says why.
  assert.equal(followRegion(root, ['ntsc'], 'pal', null), 'ntsc');
  assert.equal(root.dataset.modelRegion, 'ntsc');
  assert.equal(stage.getAttribute('aria-label'), 'NTSC label');
  assert.equal(stage.getAttribute('aria-describedby'), 'model-about-outside-ntsc');
  assert.equal(caption.hidden, false);
  assert.match(status.textContent, /^There is no model of the PAL console, because none was built, so this is the NTSC console's\.$/);
  assert.equal(root.dataset.modelMissing, 'pal');
  // Back to NTSC: the sentence's flag goes, and nothing else moves.
  assert.equal(followRegion(root, ['ntsc'], 'ntsc', 'ntsc'), 'ntsc');
  assert.equal(root.dataset.modelMissing, undefined);
  // An event with no region changes nothing.
  assert.equal(followRegion(root, ['ntsc'], undefined, 'ntsc'), 'ntsc');
  // Both NES modules go through it, in their setRegion, and do not write over its sentence when they start.
  const fs = await import('node:fs');
  for (const f of ['nes-famicom-case.js', 'nes-famicom-board.js']) {
    const src = fs.readFileSync(new URL(`../src/models/${f}`, import.meta.url), 'utf8');
    assert.match(src, /import \{ followRegion \} from '\.\/regions\.mjs';/, f);
    assert.match(src, /region = followRegion\(root, REGIONS, next, region\);/, f);
    assert.match(src, /if \(!root\.dataset\.modelMissing\) status\.textContent = /, f);
  }
});
