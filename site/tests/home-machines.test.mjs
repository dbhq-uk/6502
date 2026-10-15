import test from 'node:test';
import assert from 'node:assert/strict';
import { page } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';
import { machineHref, runningMachines } from '../src/lib/machines.mjs';

// The home page's machines section lists every machine that runs, as a list a
// screen reader announces, and links no other machine: only a running machine
// has a page.

const registry = loadRegistry();
const html = page('/').html;
const at = html.indexOf('id="machines"');
const section = at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
const links = [...section.matchAll(/<a href="(\/machines\/[^"/]+\/)"/g)].map((m) => m[1]);

test('the home page links the page of every running machine, from the registry', () => {
  assert.ok(section, 'the home page has no machines section');
  for (const m of runningMachines(registry)) {
    assert.ok(links.includes(machineHref(m)), `the home page does not link ${m.name} (${machineHref(m)})`);
  }
});

test('the home page links no machine that does not run, and no machine twice', () => {
  const running = new Set(runningMachines(registry).map(machineHref));
  for (const href of links) assert.ok(running.has(href), `the home page links ${href}, which is not a running machine`);
  assert.equal(new Set(links).size, links.length, 'a machine is linked twice');
  assert.equal(links.length, running.size);
});

test('the running machines are a list, one item each', () => {
  const list = /<ul\b[^>]*>([\s\S]*?)<\/ul>/.exec(section)?.[1] ?? '';
  assert.ok(list, 'the machines are not in a <ul>');
  const items = list.match(/<li\b/g) ?? [];
  assert.equal(items.length, runningMachines(registry).length);
  assert.doesNotMatch(section, /class="chip/, 'a "Running" chip is back: the heading and the sentence already say it');
});
