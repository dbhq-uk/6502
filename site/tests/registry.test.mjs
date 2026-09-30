import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { loadRegistry, validateRegistry, counts, REPO_ROOT } from '../src/lib/registry.mjs';
import { parseFamilyDoc } from './family-doc.mjs';

const machine = (over = {}) => ({ id: 'kim-1', name: 'KIM-1', year: 1976, category: 'single-board', cpu: '6502', core: 'nmos', status: 'planned', acceptance: null, ...over });
const registry = (...machines) => ({ machines, chips: [] });
const results = (suites) => ({ suites });

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
  const running = registry(machine({ status: 'running', acceptance: 'Kim1AcceptanceTests' }));
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
