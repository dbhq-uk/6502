import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { loadRegistry, validateRegistry, modelProblems, referenceProblems, counts, REPO_ROOT, PHOTOS_DIR, VIEWS, MODELS_DIR, DATA_DIR } from '../src/lib/registry.mjs';
import { parseFamilyDoc } from './family-doc.mjs';

const machine = (over = {}) => ({ id: 'kim-1', name: 'KIM-1', year: 1976, category: 'single-board', cpu: '6502', core: 'nmos', status: 'planned', acceptance: null, ...over });
const registry = (...machines) => ({ machines, chips: [] });
const results = (suites) => ({ suites });
// A made-up photograph, complete. Its file is checked against a stand-in for the disk.
const photo = (over = {}) => ({ file: 'board.webp', author: 'A. Photographer', sourceUrl: 'https://example.org/board', licence: 'CC BY 4.0', date: '1977-05', fetched: '2026-10-02', used: 'the tracks on the top face', alt: 'A single-board computer seen from above, keypad at the bottom right.', ...over });
const drawing = (over = {}) => ({ title: 'a replica of the board', author: 'A. Drafter', sourceUrl: 'https://example.org/replica', licence: 'CC BY-NC 4.0', fetched: '2026-10-02', used: 'the places of the parts', ...over });
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
  const running = registry(machine({ status: 'running', acceptance: 'Kim1AcceptanceTests', photos: [photo({ file: 'kim-1.webp' })] }));
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

test('a running machine must have at least one photograph of the original, and every one must name its author and link its source', () => {
  const running = (over) => registry(machine({ status: 'running', acceptance: 'Kim1AcceptanceTests', ...over }));
  const check = (photos) => validateRegistry(running({ photos }), results(passing), onDisk).join('\n');
  assert.deepEqual(validateRegistry(running({ photos: [photo()] }), results(passing), onDisk), []);
  assert.match(validateRegistry(running({}), results(passing), onDisk).join(), /must have at least one photograph of the original/);
  assert.match(check([]), /must have at least one photograph of the original/);
  // The old single photo field is refused, so a machine cannot keep it by mistake.
  assert.match(validateRegistry(running({ photo: photo(), photos: [photo()] }), results(passing), onDisk).join(), /photo is now photos/);
  // Every photograph in the list is checked, not only the first.
  const second = (over) => [photo(), photo({ file: 'board.webp', ...over })];
  assert.match(check([photo(), photo({ author: '' })]), /photos\[1\]: must name its author/);
  assert.match(check([photo(), photo({ author: undefined })]), /photos\[1\]: must name its author/);
  assert.match(check([photo(), photo({ sourceUrl: undefined })]), /photos\[1\]: must link its source/);
  assert.match(check([photo(), photo({ sourceUrl: 'ftp://example.org/board' })]), /photos\[1\]: must link its source/);
  assert.match(check(second({})), /a photograph is listed twice/);
  // A source with no https is linked as it is: the credit is what matters.
  assert.deepEqual(validateRegistry(running({ photos: [photo({ sourceUrl: 'http://example.org/board' })] }), results(passing), onDisk), []);
  // A machine not yet running needs no photograph, but one it has is checked all the same.
  assert.deepEqual(validateRegistry(registry(machine()), null, onDisk), []);
  assert.match(validateRegistry(registry(machine({ photos: [photo({ author: ' ' })] })), null, onDisk).join(), /must name its author/);
});

test('a photograph\'s file must be in the photos folder, its licence stated as the source states it or null, and its dates, use and alt text well formed', () => {
  const check = (over) => validateRegistry(registry(machine({ photos: [photo(over)] })), null, onDisk).join('\n');
  assert.match(check({ file: 'missing.webp' }), /is not in site\/src\/assets\/photos/);
  assert.match(check({ file: '../imagery/hero-die.webp' }), /must be an image file name/);
  const noLicence = photo();
  delete noLicence.licence;
  assert.match(validateRegistry(registry(machine({ photos: [noLicence] })), null, onDisk).join(), /licence must be the licence as the source states it, or null/);
  assert.equal(check({ licence: null }), '', 'a source that states no licence is recorded as null, and the page says so');
  assert.match(check({ licence: null, licenceUrl: 'https://example.org/l' }), /licenceUrl is set but no licence is named/);
  assert.match(check({ licenceUrl: 'ftp://example.org/l' }), /licenceUrl must be an https link/);
  assert.match(check({ date: '24 August 2010' }), /date must be when it was taken/);
  assert.match(check({ fetched: undefined }), /fetched must be the day it was downloaded/);
  assert.match(check({ fetched: '2026-10' }), /fetched must be the day it was downloaded/);
  assert.match(check({ used: '' }), /used must say what was traced or measured from it/);
  assert.match(check({ alt: 'A board' }), /alt must describe the photograph/);
  assert.match(check({ alt: 'Illustration of a single-board computer seen from above' }), /calls the photograph an illustration/);
  assert.match(check({ caption: '' }), /caption, when given, must not be empty/);
});

test('a drawing the model was measured from is credited like a photograph: what it is, its author, its source, its licence, when it was fetched and what it was used for', () => {
  const check = (over) => validateRegistry(registry(machine({ drawings: [drawing(over)] })), null, onDisk).join('\n');
  assert.equal(check({}), '');
  assert.match(check({ title: '' }), /drawings\[0\]: must say what it is/);
  assert.match(check({ author: undefined }), /drawings\[0\]: must name its author/);
  assert.match(check({ sourceUrl: 'example.org' }), /drawings\[0\]: must link its source/);
  assert.match(check({ fetched: 'today' }), /drawings\[0\]: fetched must be/);
  assert.match(check({ used: undefined }), /drawings\[0\]: used must say/);
  assert.match(validateRegistry(registry(machine({ drawings: {} })), null, onDisk).join(), /drawings must be a list/);
});

test('every running machine in the real registry has its photographs on disk, and every photograph on disk belongs to a machine', () => {
  const reg = loadRegistry();
  const running = reg.machines.filter((m) => m.status === 'running');
  assert.ok(running.length > 0);
  for (const m of running) {
    assert.ok(m.photos?.length >= 1, `${m.id} has no photograph`);
    for (const p of m.photos) assert.ok(fs.existsSync(path.join(PHOTOS_DIR, p.file)), `${m.id}'s photograph ${p.file} is not on disk`);
  }
  const used = new Set(reg.machines.flatMap((m) => (m.photos ?? []).map((p) => p.file)));
  const files = fs.readdirSync(PHOTOS_DIR).filter((f) => /\.(webp|jpe?g|png|avif)$/.test(f));
  assert.deepEqual(files.filter((f) => !used.has(f)), [], 'a photograph in src/assets/photos/ that no machine names');
  // A photograph is never a generated image: the two folders share no file.
  const generated = fs.readdirSync(path.join(PHOTOS_DIR, '..', 'imagery'));
  assert.deepEqual(files.filter((f) => generated.includes(f)), []);
});

test('every photograph in the folder is written up in its README: its author, its source and the SHA-256 of the file as committed, which matches', () => {
  const reg = loadRegistry();
  const readme = fs.readFileSync(path.join(PHOTOS_DIR, 'README.md'), 'utf8');
  for (const p of reg.machines.flatMap((m) => m.photos ?? [])) {
    const at = readme.indexOf(`## ${p.file}\n`);
    assert.ok(at >= 0, `src/assets/photos/README.md has no section for ${p.file}`);
    const section = readme.slice(at, readme.indexOf('\n## ', at + 1) >>> 0 || undefined);
    assert.ok(section.includes(p.sourceUrl), `the README does not give ${p.file}'s source`);
    assert.ok(section.includes(p.author.split(',')[0].replace(' & ', ' &amp; ')) || section.includes(p.author.split(',')[0]), `the README does not name ${p.file}'s author`);
    const sha = crypto.createHash('sha256').update(fs.readFileSync(path.join(PHOTOS_DIR, p.file))).digest('hex');
    assert.match(section, new RegExp(`This copy\\b[^\\n]*SHA-256 \`${sha}\``), `the README's SHA-256 for ${p.file} is not the committed file's (${sha})`);
  }
});

// The models a machine has: `case` and `models`, both optional (design, "The
// registry's models field"). A made-up module is present or absent through an
// injected modelFiles, as a photograph is through photoExists.
const built = (over = {}) => ({ modelFiles: () => ({ module: true, exportsMount: true, results: true, ...over }), ...onDisk });
// A machine that claims a model states its case (6 Oct 2026), so a made-up claim has none unless a test says otherwise.
const claims = (models, over = {}) => registry(machine({ case: false, models, ...over }));
const check = (reg, files = built()) => validateRegistry(reg, null, files).join('\n');

test('a machine with no models and no case is valid, running or not: models never hold up a machine counting', () => {
  // The BBC Micro today: running, cased, and claiming no model.
  const bbc = machine({ id: 'bbc-micro', category: 'computer', status: 'running', acceptance: 'BbcAcceptanceTests', photos: [photo()] });
  assert.deepEqual(validateRegistry(registry(bbc), results({ BbcAcceptanceTests: { passed: 1, failed: 0, skipped: 0 } }), built()), []);
  assert.equal(check(registry(machine())), '');
  // A cased machine may state its case before it claims a model.
  assert.equal(check(registry(machine({ case: true }))), '');
  assert.equal(check(registry(machine({ case: false }))), '');
});

test('the real registry: the KIM-1 has no case and one model, its board, and the BBC Micro claims none yet', () => {
  const reg = loadRegistry();
  const kim = reg.machines.find((m) => m.id === 'kim-1');
  const bbc = reg.machines.find((m) => m.id === 'bbc-micro');
  assert.equal(kim.case, false);
  assert.deepEqual(kim.models, [{ view: 'board', module: 'kim-1' }]);
  assert.equal(bbc.models, undefined);
  assert.equal(bbc.case, undefined);
  // Read from the disk, as the build reads it: the KIM-1's module and results file are there.
  assert.deepEqual(modelProblems(kim, 'machine kim-1'), []);
  assert.deepEqual(VIEWS, ['board', 'outside', 'inside']);
  assert.equal(MODELS_DIR, path.join(process.cwd(), 'src', 'models'));
  assert.equal(DATA_DIR, path.join(process.cwd(), 'src', 'data'));
});

test('models, when given, is a non-empty list of views and modules', () => {
  assert.match(check(claims([])), /models, when given, must be a non-empty list/);
  assert.match(check(claims({ view: 'board', module: 'kim-1' })), /models, when given, must be a non-empty list/);
  assert.match(check(claims(['kim-1'])), /models\[0\]: a model must be an object/);
  assert.equal(check(claims([{ view: 'board', module: 'kim-1' }])), '');
});

test('a model\'s view is one of board, outside and inside', () => {
  assert.match(check(claims([{ view: 'top', module: 'kim-1' }])), /models\[0\]: view "top" is not one of board, outside, inside/);
  assert.match(check(claims([{ module: 'kim-1' }])), /models\[0\]: view "undefined" is not one of/);
});

test('a machine claims each view once', () => {
  assert.match(check(claims([{ view: 'board', module: 'kim-1' }, { view: 'board', module: 'kim-1-other' }])), /the view board is claimed twice/);
});

test('a module is claimed once in the whole registry', () => {
  const two = registry(machine({ models: [{ view: 'board', module: 'kim-1' }] }), machine({ id: 'kim-1', name: 'KIM-1 again', models: [{ view: 'board', module: 'kim-1' }] }));
  assert.match(check(two), /module kim-1 is claimed twice in the registry/);
  assert.match(check(claims([{ view: 'outside', module: 'kim-1' }, { view: 'inside', module: 'kim-1' }], { case: true })), /module kim-1 is claimed twice in the registry/);
});

test('a module is named for its machine: the machine\'s id, or the id and a hyphen', () => {
  assert.match(check(claims([{ view: 'board', module: 'aim-65' }])), /models\[0\]: module "aim-65" must be the machine's id, kim-1, or start with kim-1-/);
  assert.match(check(claims([{ view: 'board', module: 'kim-10' }])), /module "kim-10" must be the machine's id/);
  assert.match(check(claims([{ view: 'board', module: '../kim-1' }])), /module "\.\.\/kim-1" must be the machine's id/);
  assert.match(check(claims([{ view: 'board' }])), /module "undefined" must be the machine's id/);
  assert.equal(check(claims([{ view: 'board', module: 'kim-1-board' }])), '');
});

test('a claimed model must be built: its module is there and exports mount(root), and its results file is there', () => {
  const one = claims([{ view: 'board', module: 'kim-1' }]);
  assert.match(check(one, built({ module: false, exportsMount: false })), /models\[0\]: site\/src\/models\/kim-1\.js is missing/);
  assert.match(check(one, built({ exportsMount: false })), /models\[0\]: site\/src\/models\/kim-1\.js does not export mount\(root\)/);
  assert.match(check(one, built({ results: false })), /models\[0\]: site\/src\/data\/kim-1-model\.json is missing: a model's results file says how it was made/);
  // The default reads the disk: a made-up module is not there.
  assert.match(validateRegistry(claims([{ view: 'board', module: 'kim-1-nothing' }]), null, onDisk).join('\n'), /site\/src\/models\/kim-1-nothing\.js is missing/);
});

test('case, when given, is true or false', () => {
  assert.match(check(registry(machine({ case: 'yes' }))), /case, when given, must be true or false/);
  assert.match(check(registry(machine({ case: null }))), /case, when given, must be true or false/);
});

test('a machine with no case has no outside or inside, and a machine with a case has no bare board', () => {
  assert.match(check(claims([{ view: 'outside', module: 'kim-1-case' }], { case: false })), /has no case, so it cannot claim the outside view/);
  assert.match(check(claims([{ view: 'inside', module: 'kim-1-board' }], { case: false })), /has no case, so it cannot claim the inside view/);
  assert.match(check(claims([{ view: 'board', module: 'kim-1' }], { case: true })), /has a case, so it claims outside and inside, not board/);
  assert.equal(check(claims([{ view: 'board', module: 'kim-1' }], { case: false })), '');
});

test('a machine with a case that claims a model claims both, the outside and the inside', () => {
  const both = [{ view: 'outside', module: 'kim-1-case' }, { view: 'inside', module: 'kim-1-board' }];
  assert.equal(check(claims(both, { case: true })), '');
  assert.match(check(claims([both[1]], { case: true })), /has a case and claims a model, so it must claim both outside and inside/);
  assert.match(check(claims([both[0]], { case: true })), /must claim both outside and inside/);
});

test('a machine that claims a model states case: a claim with no case fails, and the other rules still hold', () => {
  const both = [{ view: 'outside', module: 'kim-1-case' }, { view: 'inside', module: 'kim-1-board' }];
  const none = (models) => registry(machine({ models }));
  const rule = /a machine that claims a model must state case, true or false/;
  // The NES as task 7 left it: the inside alone, and no case. Until task 9 that passed; now it fails.
  assert.match(check(none([both[1]])), rule);
  assert.match(check(none(both)), rule);
  assert.match(check(none([{ view: 'board', module: 'kim-1' }])), rule);
  // Stated either way, the claim passes the new rule, and the old rules judge it as before.
  assert.equal(check(claims([{ view: 'board', module: 'kim-1' }], { case: false })), '');
  assert.equal(check(claims(both, { case: true })), '');
  assert.doesNotMatch(check(claims([both[1]], { case: true })), rule);
  assert.match(check(claims([both[1]], { case: true })), /must claim both outside and inside/);
  // A machine that claims no model need not state its case.
  assert.equal(check(registry(machine())), '');
  // The real registry: the KIM-1 states no case and claims its board; the NES states its case and claims both views.
  const reg = loadRegistry();
  for (const id of ['kim-1', 'nes']) assert.deepEqual(modelProblems(reg.machines.find((m) => m.id === id), `machine ${id}`), [], id);
  for (const m of reg.machines) if (m.models) assert.equal(typeof m.case, 'boolean', `${m.id} claims a model and does not state case`);
});

test('a reference the model was measured from, kept out of the repository, is credited like a drawing and names the SHA-256 of what was measured', () => {
  const reference = (over = {}) => ({ title: 'a flatbed scan of the bare board, component side', author: 'A. Scanner', sourceUrl: 'https://example.org/scan', licence: null, fetched: '2026-10-04', sha256: 'a'.repeat(64), used: 'the places of the holes and the copper', ...over });
  const one = (over) => check(registry(machine({ references: [reference(over)] })));
  assert.equal(one({}), '');
  assert.match(one({ title: ' ' }), /references\[0\]: must say what it is/);
  assert.match(one({ author: undefined }), /references\[0\]: must name its author/);
  assert.match(one({ sourceUrl: 'example.org' }), /references\[0\]: must link its source/);
  assert.match(one({ fetched: 'today' }), /references\[0\]: fetched must be/);
  assert.match(one({ used: '' }), /references\[0\]: used must say/);
  assert.match(one({ sha256: undefined }), /references\[0\]: sha256 must be the SHA-256 of the original, 64 hex digits/);
  assert.match(one({ sha256: 'A'.repeat(64) }), /references\[0\]: sha256 must be/);
  assert.match(one({ sha256: 'a'.repeat(63) }), /references\[0\]: sha256 must be/);
  assert.match(check(registry(machine({ references: {} }))), /references must be a list/);
  assert.match(check(registry(machine({ references: ['a scan'] }))), /references\[0\]: a reference must be an object/);
  assert.deepEqual(referenceProblems(reference(), 'r'), []);
});
