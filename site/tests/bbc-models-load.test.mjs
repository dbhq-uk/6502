// Each of the BBC Micro models' modules loads, one test each, with its main
// exports. bbc-models.test.mjs imports these modules at its top level, and when
// one of them fails to load (a syntax error, say), node's runner reports the
// tests after the import as neither passed nor failed, and that file's summary
// says 0 failed: only the suite's floor would catch it. Here a module that does
// not load fails a test named after it. Added after task 9a's review (13
// October 2026).
import test from 'node:test';
import assert from 'node:assert/strict';

const MODULES = {
  'bbc-micro-board-parts.mjs': ['BOARD', 'ICS', 'CONNECTORS', 'PASSIVES', 'OTHERS', 'HEIGHTS_TYPICAL'],
  'bbc-micro-board-layout.mjs': ['BOARD', 'FITTED', 'COUNTED', 'TRACKS', 'chipLegend', 'marksFrom', 'pagedRef', 'pointWords', 'describe'],
  'bbc-micro-board-scene.mjs': ['buildBoard', 'stageOptions', 'createLayers', 'trackMaps', 'trackMaterials', 'canvasPrinter'],
  'bbc-micro-board-notes.mjs': ['made', 'copperSentence', 'partsSentence'],
  'bbc-micro-case-parts.mjs': ['CASE', 'RECESS', 'PROFILE', 'KEYS', 'LEDS', 'BADGE', 'GRILLE', 'REAR', 'UNDERSIDE', 'FEET'],
  'bbc-micro-case-layout.mjs': ['CASE', 'KEYS', 'legendOf', 'capsOf', 'createKeyState', 'litLeds', 'press', 'pointWords', 'describe'],
  'bbc-micro-case-scene.mjs': ['buildCase', 'stageOptions', 'atlasItems', 'layoutAtlas', 'drawAtlas'],
  'bbc-micro-case-notes.mjs': ['made', 'sizeSentence'],
};

for (const [file, names] of Object.entries(MODULES)) {
  test(`src/models/${file} loads, with its main exports`, async () => {
    const m = await import(`../src/models/${file}`);
    for (const name of names) assert.ok(m[name] !== undefined && m[name] !== null, `${file} does not export ${name}`);
  });
}
