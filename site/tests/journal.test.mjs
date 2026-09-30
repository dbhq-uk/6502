import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { pages, page, visibleText } from './helpers.mjs';
import { newestFirst } from '../src/lib/site.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import { rewriteHref, journalEntryIds } from '../src/lib/rehype-journal.mjs';

const dir = path.join(REPO_ROOT, 'docs', 'journal');
const files = fs.readdirSync(dir).filter((f) => /^2.*\.md$/.test(f));

test('every journal entry has front matter: a title, a date and a summary', () => {
  for (const f of files) {
    const text = fs.readFileSync(path.join(dir, f), 'utf8');
    const front = /^---\n([\s\S]*?)\n---\n/.exec(text)?.[1] ?? '';
    for (const key of ['title', 'date', 'summary', 'order']) assert.match(front, new RegExp(`^${key}: .+`, 'm'), `${f} has no ${key}`);
    assert.match(front, /^order: \d+$/m, `${f} order must be a whole number`);
    assert.match(front, /^date: \d{4}-\d{2}-\d{2}$/m, `${f} date must be YYYY-MM-DD`);
    assert.ok(f.startsWith(/^date: (\S+)/m.exec(front)?.[1] ?? 'no date'), `${f} does not start with its own date`);
  }
});

test('every journal entry is built as a page, and listed on the journal page', () => {
  assert.ok(files.length > 0);
  const index = page('/journal/').html;
  for (const f of files) {
    const id = f.replace(/\.md$/, '');
    assert.ok(page(`/journal/${id}/`), `${id} was not built`);
    assert.ok(index.includes(`href="/journal/${id}/"`), `${id} is not listed`);
  }
});

const entryPages = () => {
  const built = pages().filter((x) => x.url.startsWith('/journal/2'));
  // An empty list would let every loop below pass without checking anything.
  assert.equal(built.length, files.length, 'the number of built journal pages is not the number of entry files');
  return built;
};

const titleOf = (file) => {
  const front = /^---\n([\s\S]*?)\n---\n/.exec(fs.readFileSync(path.join(dir, file), 'utf8'))[1];
  return /^title: (.+)$/m.exec(front)[1].trim().replace(/^"(.*)"$/, '$1');
};

test('links written for the repository point at it on GitHub', () => {
  for (const p of entryPages()) {
    assert.ok(!/href="\.\.?\//.test(p.html), `${p.url} still has a relative repository link`);
    assert.ok(!/href="(?!https?:)[^"]+\.md"/.test(p.html), `${p.url} links to a markdown file that is not on the site`);
  }
});

test('an entry prints its title once, and it is the title in its front matter', () => {
  for (const f of files) {
    const id = f.replace(/\.md$/, '');
    const p = entryPages().find((x) => x.url === `/journal/${id}/`);
    assert.ok(p, `${id} was not built`);
    const h1s = [...p.html.matchAll(/<h1\b[^>]*>([\s\S]*?)<\/h1>/g)];
    assert.equal(h1s.length, 1, `${p.url} has more than one h1`);
    assert.equal(visibleText(h1s[0][1]), titleOf(f), `${p.url}: the h1 is not the front matter title`);
  }
});

test('the home page shows the three newest entries, in the journal\'s own order', () => {
  const real = files.map((f) => {
    const front = fs.readFileSync(path.join(dir, f), 'utf8');
    return { id: f.replace(/\.md$/, ''), data: { date: new Date(/^date: (\S+)/m.exec(front)[1]), order: Number(/^order: (\d+)/m.exec(front)[1]) } };
  });
  const expected = real.sort(newestFirst).slice(0, 3).map((e) => e.id);
  const html = page('/').html;
  const listed = [...html.slice(html.indexOf('From the journal')).matchAll(/href="\/journal\/(2[^"/]+)\/"/g)].map((m) => m[1]);
  assert.deepEqual(listed, expected);
});

test('the journal index lists the entries newest first, and equal dates and orders are broken by file name', () => {
  const entry = (id, date, order) => ({ id, data: { date: new Date(date), order } });
  const sorted = (list) => list.sort(newestFirst).map((e) => e.id);
  const a = entry('2026-09-30-a', '2026-09-30', 1);
  const b = entry('2026-09-30-b', '2026-09-30', 1);
  const c = entry('2026-09-30-c', '2026-09-30', 2);
  const d = entry('2026-09-29-z', '2026-09-29', 9);
  assert.deepEqual(sorted([a, b, c, d]), ['2026-09-30-c', '2026-09-30-b', '2026-09-30-a', '2026-09-29-z']);
  assert.deepEqual(sorted([d, b, a, c]), sorted([c, a, d, b]), 'the order depends on the order the entries arrive in');
  // The real index agrees with the same rule applied to the front matter.
  const real = files.map((f) => {
    const front = fs.readFileSync(path.join(dir, f), 'utf8');
    return entry(f.replace(/\.md$/, ''), /^date: (\S+)/m.exec(front)[1], Number(/^order: (\d+)/m.exec(front)[1]));
  });
  const listed = [...page('/journal/').html.matchAll(/href="\/journal\/(2[^"/]+)\/"/g)].map((m) => m[1]);
  assert.deepEqual(listed, sorted(real));
});

test('a link to another journal entry becomes a link to its page, and other repository files still go to GitHub', () => {
  const entries = new Set(['2026-09-30-building-the-core']);
  const from = 'docs/journal';
  assert.equal(rewriteHref('2026-09-30-building-the-core.md', from, entries), '/journal/2026-09-30-building-the-core/');
  assert.equal(rewriteHref('2026-09-30-building-the-core.md#task-1', from, entries), '/journal/2026-09-30-building-the-core/#task-1');
  assert.equal(rewriteHref('./2026-09-30-building-the-core.md', from, entries), '/journal/2026-09-30-building-the-core/');
  // From a document outside the journal folder, the same file is reached by its longer path.
  assert.equal(rewriteHref('journal/2026-09-30-building-the-core.md', 'docs', entries), '/journal/2026-09-30-building-the-core/');
  // Not a built entry: the README is not in the collection, and neither is a name that does not exist.
  assert.equal(rewriteHref('README.md', from, entries), 'https://github.com/dbhq-uk/6502/blob/main/docs/journal/README.md');
  assert.equal(rewriteHref('2026-01-01-no-such-entry.md', from, entries), 'https://github.com/dbhq-uk/6502/blob/main/docs/journal/2026-01-01-no-such-entry.md');
  // Other files keep the GitHub rewrite, fragment included.
  assert.equal(rewriteHref('../known-differences.md#x', from, entries), 'https://github.com/dbhq-uk/6502/blob/main/docs/known-differences.md#x');
  assert.equal(rewriteHref('../../bench/Dbhq.Cpu6502.WasmBench/README.md', from, entries), 'https://github.com/dbhq-uk/6502/blob/main/bench/Dbhq.Cpu6502.WasmBench/README.md');
  // Absolute URLs, site paths and bare fragments are left alone.
  assert.equal(rewriteHref('https://example.com/a.md', from, entries), 'https://example.com/a.md');
  assert.equal(rewriteHref('/journal/', from, entries), '/journal/');
  assert.equal(rewriteHref('#top', from, entries), '#top');
});

test('the set of entries the link rewrite knows is exactly the set of entry files', () => {
  assert.deepEqual([...journalEntryIds()].sort(), files.map((f) => f.replace(/\.md$/, '')).sort());
});

test('in the built journal, a link to a sibling entry points at its page on the site', () => {
  const from = page('/journal/2026-09-30-the-browser-speed-check/').html;
  assert.ok(from.includes('href="/journal/2026-09-30-building-the-core/"'), 'the link to the core entry is not a site link');
  assert.ok(!from.includes('github.com/dbhq-uk/6502/blob/main/docs/journal/2026-09-30-building-the-core.md'), 'the link still leaves the site');
});
