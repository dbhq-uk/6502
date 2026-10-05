import test from 'node:test';
import assert from 'node:assert/strict';
import { MODELS, wordsFor, regionProblems, regionViews } from '../src/models/models.mjs';

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
  const src = fs.readFileSync(new URL('../src/components/MachineModel.astro', import.meta.url), 'utf8');
  assert.match(src, /regionViews\(model\)/);
  assert.doesNotMatch(src, /model\.(label|about|made)\b/);
  assert.match(src, /data-model-region=\{v\.region\} hidden=\{v\.hidden\}/);
});
