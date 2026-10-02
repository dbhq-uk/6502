import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { loadRegistry, validateRegistry, counts, REPO_ROOT, PHOTOS_DIR } from '../src/lib/registry.mjs';
import { parseFamilyDoc } from './family-doc.mjs';

const machine = (over = {}) => ({ id: 'kim-1', name: 'KIM-1', year: 1976, category: 'single-board', cpu: '6502', core: 'nmos', status: 'planned', acceptance: null, ...over });
const registry = (...machines) => ({ machines, chips: [] });
const results = (suites) => ({ suites });
// A made-up photograph, complete. Its file is checked against a stand-in for the disk.
const photo = (over = {}) => ({ file: 'board.webp', author: 'A. Photographer', sourceUrl: 'https://example.org/board', licence: 'CC BY 4.0', date: '1977-05', alt: 'A single-board computer seen from above, keypad at the bottom right.', ...over });
const onDisk = { photoExists: (f) => f === 'board.webp' };
const passing = { Kim1AcceptanceTests: { passed: 3, failed: 0, skipped: 0 } };

test('the real registry is valid', () => {
  assert.deepEqual(validateRegistry(loadRegistry()), []);
});

test('the registry and the family document name the same machines and chips', () => {
  const doc = parseFamilyDoc(fs.readFileSync(path.join(REPO_ROOT, 'docs', 'the-6502-family.md'), 'utf8'));
  assert.ok(doc.machines.length > 0 && doc.chips.length > 0, 'the family document was read but held no tables');
  const reg = loadRegistry();
  const machines = new Set(reg.machines.map((m) => m.name));
  const chips = new Set(reg.chips.map((c) => c.name));
  assert.deepEqual(doc.machines.filter((n) => !machines.has(n)), [], 'machines in the document but not in the registry');
  assert.deepEqual(doc.chips.filter((n) => !chips.has(n)), [], 'chips in the document but not in the registry');
  assert.deepEqual([...machines].filter((n) => !doc.machines.includes(n)), [], 'machines in the registry but not in the document');
  assert.deepEqual([...chips].filter((n) => !doc.chips.includes(n)), [], 'chips in the registry but not in the document');
});

test('a running machine must name an acceptance test', () => {
  const errors = validateRegistry(registry(machine({ status: 'running' })));
  assert.match(errors.join(), /must name its acceptance test/);
});

test('a running machine needs a passing acceptance suite in the results', () => {
  const running = registry(machine({ status: 'running', acceptance: 'Kim1AcceptanceTests', photo: photo({ file: 'kim-1.webp' }) }));
  assert.match(validateRegistry(running, results({})).join(), /is not in the test results/);
  assert.match(validateRegistry(running, results({ Kim1AcceptanceTests: { passed: 0, failed: 0, skipped: 0 } })).join(), /must pass/);
  assert.match(validateRegistry(running, results({ Kim1AcceptanceTests: { passed: 3, failed: 1, skipped: 0 } })).join(), /must pass/);
  assert.match(validateRegistry(running, results({ Kim1AcceptanceTests: { passed: 3, failed: 0, skipped: 1 } })).join(), /must pass/);
  assert.deepEqual(validateRegistry(running, results({ Kim1AcceptanceTests: { passed: 3, failed: 0, skipped: 0 } })), []);
});

test('a machine is out of scope exactly when no core variant runs it', () => {
  assert.match(validateRegistry(registry(machine({ core: 'none' }))).join(), /out of scope exactly when/);
  assert.match(validateRegistry(registry(machine({ status: 'out-of-scope' }))).join(), /out of scope exactly when/);
  assert.deepEqual(validateRegistry(registry(machine({ core: 'none', status: 'out-of-scope' }))), []);
});

test('ids are unique and well formed, and status, category and core are from the lists', () => {
  const errors = validateRegistry(registry(machine(), machine(), machine({ id: 'Bad_ID', status: 'done', category: 'toy', core: 'z80' }))).join('\n');
  assert.match(errors, /duplicate id/);
  assert.match(errors, /lower-case words joined by hyphens/);
  assert.match(errors, /status "done"/);
  assert.match(errors, /category "toy"/);
  assert.match(errors, /core "z80"/);
});

test('counts separates the machines in scope from those out of it', () => {
  const c = counts(registry(machine({ id: 'a', status: 'running', acceptance: 'x' }), machine({ id: 'b' }), machine({ id: 'c', status: 'in-progress' }), machine({ id: 'd', core: 'none', status: 'out-of-scope' })));
  assert.deepEqual(c, { running: 1, inProgress: 1, planned: 1, outOfScope: 1, inScope: 3 });
});

test('a running machine must have a photograph of the original, and it must name its author and link its source', () => {
  const running = (over) => registry(machine({ status: 'running', acceptance: 'Kim1AcceptanceTests', ...over }));
  assert.deepEqual(validateRegistry(running({ photo: photo() }), results(passing), onDisk), []);
  assert.match(validateRegistry(running({}), results(passing), onDisk).join(), /a running machine must have a photograph of the original/);
  assert.match(validateRegistry(running({ photo: null }), results(passing), onDisk).join(), /a running machine must have a photograph of the original/);
  assert.match(validateRegistry(running({ photo: photo({ author: '' }) }), results(passing), onDisk).join(), /must name its author/);
  assert.match(validateRegistry(running({ photo: photo({ author: undefined }) }), results(passing), onDisk).join(), /must name its author/);
  assert.match(validateRegistry(running({ photo: photo({ sourceUrl: undefined }) }), results(passing), onDisk).join(), /must link its source/);
  assert.match(validateRegistry(running({ photo: photo({ sourceUrl: 'http://example.org/board' }) }), results(passing), onDisk).join(), /must link its source/);
  // A machine not yet running needs no photograph, but one it has is checked all the same.
  assert.deepEqual(validateRegistry(registry(machine()), null, onDisk), []);
  assert.match(validateRegistry(registry(machine({ photo: photo({ author: ' ' }) })), null, onDisk).join(), /must name its author/);
});

test('a photograph\'s file must be in the photos folder, its licence stated as the source states it or null, and its date and alt text well formed', () => {
  const check = (over) => validateRegistry(registry(machine({ photo: photo(over) })), null, onDisk).join('\n');
  assert.match(check({ file: 'missing.webp' }), /is not in site\/src\/assets\/photos/);
  assert.match(check({ file: '../imagery/hero-die.webp' }), /must be an image file name/);
  const noLicence = photo();
  delete noLicence.licence;
  assert.match(validateRegistry(registry(machine({ photo: noLicence })), null, onDisk).join(), /photo\.licence must be the licence as the source states it, or null/);
  assert.equal(check({ licence: null }), '', 'a source that states no licence is recorded as null, and the page says so');
  assert.match(check({ licence: null, licenceUrl: 'https://example.org/l' }), /licenceUrl is set but no licence is named/);
  assert.match(check({ licenceUrl: 'ftp://example.org/l' }), /licenceUrl must be an https link/);
  assert.match(check({ date: '24 August 2010' }), /photo\.date must be when it was taken/);
  assert.match(check({ alt: 'A board' }), /photo\.alt must describe the photograph/);
  assert.match(check({ alt: 'Illustration of a single-board computer seen from above' }), /calls the photograph an illustration/);
  assert.match(check({ caption: '' }), /photo\.caption, when given, must not be empty/);
});

test('every running machine in the real registry has a photograph on disk, and every photograph on disk belongs to a machine', () => {
  const reg = loadRegistry();
  const running = reg.machines.filter((m) => m.status === 'running');
  assert.ok(running.length > 0);
  for (const m of running) {
    assert.ok(m.photo, `${m.id} has no photograph`);
    assert.ok(fs.existsSync(path.join(PHOTOS_DIR, m.photo.file)), `${m.id}'s photograph ${m.photo.file} is not on disk`);
  }
  const used = new Set(reg.machines.filter((m) => m.photo).map((m) => m.photo.file));
  const files = fs.readdirSync(PHOTOS_DIR).filter((f) => /\.(webp|jpe?g|png|avif)$/.test(f));
  assert.deepEqual(files.filter((f) => !used.has(f)), [], 'a photograph in src/assets/photos/ that no machine names');
  // A photograph is never a generated image: the two folders share no file.
  const generated = fs.readdirSync(path.join(PHOTOS_DIR, '..', 'imagery'));
  assert.deepEqual(files.filter((f) => generated.includes(f)), []);
  // Each is written up where it came from, by file name, with its source.
  const readme = fs.readFileSync(path.join(PHOTOS_DIR, 'README.md'), 'utf8');
  for (const m of reg.machines.filter((x) => x.photo)) {
    assert.ok(readme.includes(`## ${m.photo.file}`), `src/assets/photos/README.md has no section for ${m.photo.file}`);
    assert.ok(readme.includes(m.photo.sourceUrl), `src/assets/photos/README.md does not give ${m.photo.file}'s source`);
  }
});
