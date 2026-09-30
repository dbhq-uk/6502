import test from 'node:test';
import assert from 'node:assert/strict';
import { page, visibleText } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';

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
