import test from 'node:test';
import assert from 'node:assert/strict';
import { page, visibleText } from './helpers.mjs';
import fs from 'node:fs';
import { loadRegistry, validateRegistry, counts } from '../src/lib/registry.mjs';
import { figures } from '../src/lib/figures.mjs';
import { runningMachines, machineHref, acceptanceSummary } from '../src/lib/machines.mjs';
import { compareCells } from '../public/table-sort.js';

test('the machines table has one row for every machine in the registry', () => {
  const registry = loadRegistry();
  const rows = (page('/machines/').html.match(/<tr data-status=/g) ?? []).length;
  assert.equal(rows, registry.machines.length);
  const text = visibleText(page('/machines/').html);
  for (const m of registry.machines) assert.ok(text.includes(m.name), `${m.name} is not in the table`);
});

test('the chips table has one row for every chip in the registry', () => {
  const chips = loadRegistry().chips;
  const html = page('/chips/').html;
  const body = html.slice(html.indexOf('<tbody>'), html.indexOf('</tbody>'));
  assert.equal((body.match(/<tr>/g) ?? []).length, chips.length);
});

test('a running machine links to its page and only a running machine does', () => {
  const html = page('/machines/').html;
  const links = [...html.matchAll(/<a href="\/machines\/([a-z0-9-]+)\/"/g)].map((m) => m[1]);
  const running = loadRegistry().machines.filter((m) => m.status === 'running').map((m) => m.id);
  assert.deepEqual(links.sort(), running.sort());
  for (const id of running) assert.ok(page(`/machines/${id}/`), `${id} has no page`);
});

test('the table can be filtered and sorted without the page being rebuilt: the script is loaded and the hooks are there', () => {
  const html = page('/machines/').html;
  assert.ok(html.includes('src="/machines-table.js"'));
  assert.ok(html.includes('data-sortable') && html.includes('data-filterable') && html.includes('data-count'));
  for (const f of ['status', 'category', 'core']) assert.ok(html.includes(`data-filter="${f}"`), `no ${f} filter`);
});

test('the family page renders the repository document, headings and tables included', () => {
  const html = page('/family/').html;
  assert.ok(html.includes('<table'), 'the family document has tables');
  assert.match(visibleText(html), /Everything that ran on a 6502/);
});

test('without JavaScript the filters are not shown, and the table is complete', () => {
  const html = page('/machines/').html;
  assert.match(html, /<div[^>]*\bdata-filters\b[^>]*\bhidden\b[^>]*>/, 'the filter group is not hidden in the built page');
  assert.ok(html.includes('data-sortable'), 'the table is missing');
  // Every select is inside the hidden group, so none is visible and inert.
  const group = html.slice(html.indexOf('data-filters'), html.indexOf('<div class="tablewrap"'));
  assert.equal((group.match(/<select\b/g) ?? []).length, 3);
  assert.ok(!/<select\b/.test(html.slice(html.indexOf('<div class="tablewrap"'))), 'a select outside the hidden group');
});

test('the script shows the filter group once it is running', () => {
  const source = fs.readFileSync(new URL('../public/machines-table.js', import.meta.url), 'utf8');
  assert.match(source, /querySelector\("\[data-filters\]"\)/);
  assert.match(source, /\.hidden = false|removeAttribute\("hidden"\)/);
  // The CSS must let the attribute win over .filters { display: flex }.
  const css = fs.readFileSync(new URL('../src/styles/global.css', import.meta.url), 'utf8');
  assert.match(css, /\.filters\[hidden\]\s*\{\s*display:\s*none/);
});

test('the machine count is a polite live region, so a screen reader hears a filter change', () => {
  assert.match(page('/machines/').html, /<p[^>]*\bdata-count\b[^>]*\baria-live="polite"/);
});

test('the sort script imports its comparison from a same-origin file that the build ships', () => {
  const source = fs.readFileSync(new URL('../public/machines-table.js', import.meta.url), 'utf8');
  assert.match(source, /^import \{ compareCells \} from "\/table-sort\.js";$/m);
  assert.ok(fs.existsSync(new URL('../dist/table-sort.js', import.meta.url)), 'table-sort.js was not built into dist');
});

test('a blank value sorts last in both directions, so sorting by year never puts the blanks first', () => {
  const years = ['1976', '', '1981', '', '1975', '2019'];
  const sort = (ascending) => [...years].sort((a, b) => compareCells(a, b, ascending));
  assert.deepEqual(sort(true), ['1975', '1976', '1981', '2019', '', '']);
  assert.deepEqual(sort(false), ['2019', '1981', '1976', '1975', '', '']);
  assert.equal(compareCells('', '', true), 0);
  assert.equal(compareCells('', '', false), 0);
  assert.equal(compareCells('', '1975', true), 1);
  assert.equal(compareCells('', '1975', false), 1);
  assert.equal(compareCells('1975', '', true), -1);
  assert.equal(compareCells('1975', '', false), -1);
  // Numbers compare as numbers, not as text: 9 comes before 10.
  assert.deepEqual(['10', '9', '100'].sort((a, b) => compareCells(a, b, true)), ['9', '10', '100']);
  // Text compares as text, and descending reverses it.
  assert.deepEqual(['Zeta', 'alpha', 'Beta'].sort((a, b) => compareCells(a, b, true)), ['alpha', 'Beta', 'Zeta']);
  assert.deepEqual(['Zeta', 'alpha', 'Beta'].sort((a, b) => compareCells(a, b, false)), ['Zeta', 'Beta', 'alpha']);
});

// The registry and results below are made up. Nothing here reads or writes machines/registry.json.
const photo = { file: 'kim-1.webp', author: 'A. Photographer', sourceUrl: 'https://example.org/kim-1', licence: null, date: '1977', alt: 'A single-board computer seen from above, keypad at the bottom right.' };
const machine = (over = {}) => ({ id: 'kim-1', name: 'KIM-1', year: 1976, category: 'single-board', cpu: '6502', core: 'nmos', status: 'running', acceptance: 'Kim1AcceptanceTests', photo, ...over });
const planned = machine({ id: 'bbc-micro', name: 'BBC Micro', year: 1981, category: 'computer', status: 'planned', acceptance: null });
const registryOf = (...machines) => ({ machines, chips: [] });
const suite = (over = {}) => ({ passed: 3, failed: 0, skipped: 0, ...over });
const resultsOf = (suites) => ({ total: { passed: 6, failed: 0, skipped: 0 }, suites: { XHarte: suite(), DormannTests: suite(), TransistorModelTests: suite(), ...suites } });
const measurements = { modes: { native: { runs: [{ mhz: 1 }] }, interpreter: { runs: [{ mhz: 1 }] }, aot: { runs: [{ mhz: 1 }] } } };

test('a running machine with a passing acceptance suite is valid and is counted as implemented', () => {
  const registry = registryOf(machine(), planned);
  const results = resultsOf({ Kim1AcceptanceTests: suite() });
  assert.deepEqual(validateRegistry(registry, results), []);
  assert.equal(counts(registry).running, 1);
  const f = figures({ registry, results, measurements });
  assert.equal(f.machinesImplemented, 1);
  assert.equal(f.machinesInScope, 2);
});

test('a running machine whose acceptance suite is missing or not passing fails validation, and a planned machine needs none', () => {
  const registry = registryOf(machine(), planned);
  assert.match(validateRegistry(registry, resultsOf({})).join(), /"Kim1AcceptanceTests" is not in the test results/);
  assert.match(validateRegistry(registry, resultsOf({ Kim1AcceptanceTests: suite({ failed: 1 }) })).join(), /must pass with nothing failed or skipped/);
  assert.match(validateRegistry(registry, resultsOf({ Kim1AcceptanceTests: suite({ passed: 0 }) })).join(), /must pass/);
  assert.match(validateRegistry(registry, resultsOf({ Kim1AcceptanceTests: suite({ skipped: 1 }) })).join(), /must pass/);
  assert.deepEqual(validateRegistry(registryOf(planned), resultsOf({})), []);
});

test('the machines page links only a running machine, and only a running machine has a page', () => {
  const statuses = { running: machine(), 'in-progress': machine({ id: 'a', status: 'in-progress' }), planned, 'out-of-scope': machine({ id: 'b', status: 'out-of-scope', core: 'none' }) };
  assert.equal(machineHref(statuses.running), '/machines/kim-1/');
  for (const status of ['in-progress', 'planned', 'out-of-scope']) assert.equal(machineHref(statuses[status]), null, `${status} must not be linked`);
  const registry = registryOf(...Object.values(statuses));
  assert.deepEqual(runningMachines(registry).map((m) => m.id), ['kim-1']);
  // The page's paths and the page's links are the same rule.
  assert.deepEqual(registry.machines.filter((m) => machineHref(m)).map((m) => m.id), runningMachines(registry).map((m) => m.id));
});

test("a machine's own page states its acceptance test and the count from the results", () => {
  const results = resultsOf({ Kim1AcceptanceTests: suite({ passed: 1234 }) });
  assert.equal(acceptanceSummary(machine(), results), 'Acceptance test Kim1AcceptanceTests: 1234 passing.');
  assert.equal(acceptanceSummary(machine(), results, (n) => n.toLocaleString('en-GB')), 'Acceptance test Kim1AcceptanceTests: 1,234 passing.');
  assert.throws(() => acceptanceSummary(machine(), resultsOf({})), /Kim1AcceptanceTests/);
});

test('every running machine in the real registry has a built page that states its acceptance test', () => {
  for (const m of runningMachines(loadRegistry())) {
    const built = page(`/machines/${m.id}/`);
    assert.ok(built, `${m.id} has no page`);
    assert.ok(visibleText(built.html).includes(m.acceptance), `${m.id}'s page does not name ${m.acceptance}`);
  }
});
