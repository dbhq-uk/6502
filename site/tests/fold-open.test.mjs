import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { page } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';
import { runningMachines } from '../src/lib/machines.mjs';

// A link to a part of a machine page that is inside a closed <details> must
// open it (the review of PR 180: every "#rom" link landed on a shut fold). The
// browser does not open a <details> for a fragment, so public/fold-open.js does,
// on load and when the fragment changes. It is an external module, because the
// site's CSP has no 'unsafe-inline' in script-src. Here it runs on a small
// stand-in for the document; the browser checks run it in Chrome.

const { openFoldsFor } = await import('../public/fold-open.js');

/** A stand-in element: its parent, whether it is a <details>, and the nearest <details> from it up, itself included. */
function el(name, parentElement = null) {
  const e = { localName: name, parentElement, open: false, scrolled: 0, scrollIntoView() { this.scrolled += 1; } };
  e.closest = (selector) => { assert.equal(selector, 'details'); for (let x = e; x; x = x.parentElement) if (x.localName === 'details') return x; return null; };
  return e;
}
const doc = (byId) => ({ getElementById: (id) => byId[id] ?? null });

test('a fragment that names a closed details opens it', () => {
  const rom = el('details');
  assert.equal(openFoldsFor('#rom', doc({ rom })), true);
  assert.equal(rom.open, true);
});

test('a fragment that names something inside a closed details opens every details round it, outermost too', () => {
  const outer = el('details');
  const inner = el('details', el('div', outer));
  const target = el('h3', el('p', inner));
  assert.equal(openFoldsFor('#deep', doc({ deep: target })), true);
  assert.equal(inner.open && outer.open, true);
});

test('a fragment that names something outside any details, or nothing, or no fragment at all, opens nothing and moves nothing', () => {
  const some = el('details');
  const heading = el('h2', el('section'));
  assert.equal(openFoldsFor('#keyboard', doc({ keyboard: heading, rom: some })), false);
  assert.equal(openFoldsFor('#missing', doc({ rom: some })), false);
  assert.equal(openFoldsFor('', doc({ rom: some })), false);
  assert.equal(openFoldsFor('#', doc({ rom: some })), false);
  assert.equal(some.open, false);
  assert.equal(heading.scrolled, 0);
});

test('a details that is already open is left alone and the page is not scrolled again', () => {
  const rom = el('details');
  rom.open = true;
  assert.equal(openFoldsFor('#rom', doc({ rom })), false);
  assert.equal(rom.scrolled, 0);
});

test('opening scrolls the target into view, because the browser scrolled to it while it was shut', () => {
  const rom = el('details');
  openFoldsFor('#rom', doc({ rom }));
  assert.equal(rom.scrolled, 1);
});

test('a fragment with percent-escapes is decoded, and a malformed one is ignored', () => {
  const fold = el('details');
  assert.equal(openFoldsFor('#a%20b', doc({ 'a b': fold })), true);
  assert.equal(openFoldsFor('#%E0%A4%A', doc({})), false);
});

test('the script runs on load and on hashchange', () => {
  const source = fs.readFileSync(new URL('../public/fold-open.js', import.meta.url), 'utf8');
  assert.match(source, /addEventListener\(\s*["']hashchange["']/);
  assert.match(source, /location\.hash/);
});

test('every machine page loads the script, and the build ships it (the deploy check list is derived from the pages, in build.test.mjs)', () => {
  for (const m of runningMachines(loadRegistry())) {
    assert.match(page(`/machines/${m.id}/`).html, /<script\b[^>]*\bsrc="\/fold-open\.js"/, `/machines/${m.id}/ does not load fold-open.js`);
  }
  assert.ok(fs.existsSync(path.join(process.cwd(), 'dist', 'fold-open.js')), 'fold-open.js was not built into dist');
});

test('every "#fragment" link on a machine page leads to an element on the page, and one that is inside a fold is opened by the script', () => {
  for (const m of runningMachines(loadRegistry())) {
    const html = page(`/machines/${m.id}/`).html;
    const ids = new Set([...html.matchAll(/\sid="([^"]+)"/g)].map((x) => x[1]));
    for (const [, id] of html.matchAll(/<a\b[^>]*\shref="#([^"]+)"/g)) assert.ok(ids.has(id), `/machines/${m.id}/: a link to #${id} leads nowhere`);
  }
});

test('the BBC Micro and the Electron link the typing tip to the symbols table, which is a fold the link opens, not to a heading above it', () => {
  for (const id of ['bbc-micro', 'electron']) {
    const html = page(`/machines/${id}/`).html;
    assert.match(html, /<a href="#symbols">/, `${id}: the keyboard link does not lead to the symbols`);
    assert.match(html, /<details\b[^>]*\bid="symbols"/, `${id}: the symbols are not a fold with that id`);
  }
});
