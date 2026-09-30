import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { pages, page } from './helpers.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';

const dir = path.join(REPO_ROOT, 'docs', 'journal');
const files = fs.readdirSync(dir).filter((f) => /^2.*\.md$/.test(f));

test('every journal entry has front matter: a title, a date and a summary', () => {
  for (const f of files) {
    const text = fs.readFileSync(path.join(dir, f), 'utf8');
    const front = /^---\n([\s\S]*?)\n---\n/.exec(text)?.[1] ?? '';
    for (const key of ['title', 'date', 'summary']) assert.match(front, new RegExp(`^${key}: .+`, 'm'), `${f} has no ${key}`);
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

test('links written for the repository point at it on GitHub', () => {
  for (const p of pages().filter((x) => x.url.startsWith('/journal/2'))) {
    assert.ok(!/href="\.\.?\//.test(p.html), `${p.url} still has a relative repository link`);
    assert.ok(!/href="(?!https?:)[^"]+\.md"/.test(p.html), `${p.url} links to a markdown file that is not on the site`);
  }
});

test('an entry prints its title once, from its front matter', () => {
  for (const p of pages().filter((x) => x.url.startsWith('/journal/2'))) {
    assert.equal((p.html.match(/<h1\b/g) ?? []).length, 1, `${p.url} has more than one h1`);
  }
});
