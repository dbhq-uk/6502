import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { pages, visibleText, DIST } from './helpers.mjs';
import { CAPCOM } from '../src/lib/site.mjs';

// Rules that hold for every page the site builds, whichever pages exist yet.
const all = pages();
const strip = (s) => s.replace(/<[^>]+>/g, '').replace(/&amp;/g, '&').replace(/\s+/g, ' ').trim();

test('every page has a title, a description and an https canonical link on the site', () => {
  for (const p of all.filter((x) => x.url !== '/404.html')) {
    assert.ok(/<title>[^<]+<\/title>/.test(p.html), `${p.url} has no title`);
    assert.ok(/<meta name="description" content="[^"]{20,}"/.test(p.html), `${p.url} has no description`);
    assert.ok(p.html.includes(`<link rel="canonical" href="https://6502.dbhq.uk${p.url}"`), `${p.url} has no canonical link`);
  }
});

test('every page carries the Capcom non-affiliation line word for word', () => {
  for (const p of all) assert.ok(visibleText(p.html).includes(CAPCOM), `${p.url} is missing the Capcom line`);
});

// The patterns are built from character codes. Writing a dash or a project name
// here would make this very file fail the dash rule, or name what must not be named.
const DASHES = new RegExp(`[${String.fromCharCode(0x2013, 0x2014)}]`);
const NEVER_NAMED = new RegExp(
  [[97, 108, 108, 101, 121, 99, 97, 116], [114, 111, 99, 107, 102, 111, 114, 100]].map((c) => String.fromCharCode(...c)).join('|'),
  'i',
);

test('no en or em dash anywhere in the built site, and none of the names this project must not mention', () => {
  for (const p of all) {
    assert.ok(!DASHES.test(p.html), `${p.url} has an en or em dash`);
    assert.ok(!NEVER_NAMED.test(p.html), `${p.url} names a project it must not`);
  }
});

test('no HTML comment is served: internal notes stay in the source', () => {
  for (const p of all) assert.ok(!/<!--/.test(p.html), `${p.url} serves an HTML comment`);
});

test('the copy is in British English', () => {
  const banned = /\b(color|colors|organize|organizes|organized|behavior|centered|analyze|recognize|optimize|catalog)\b/i;
  for (const p of all) {
    const m = banned.exec(visibleText(p.html));
    assert.equal(m, null, `${p.url} uses "${m?.[0]}"`);
  }
});

test('every image has alt text, and every generated image is captioned as an illustration', () => {
  for (const p of all) {
    for (const m of p.html.matchAll(/<img\b[^>]*>/g)) {
      assert.ok(/\balt="[^"]+"/.test(m[0]), `${p.url}: an image has no alt text`);
      if (/data-generated/.test(m[0])) {
        const fig = p.html.slice(p.html.lastIndexOf('<figure', m.index), p.html.indexOf('</figure>', m.index));
        assert.ok(/<figcaption[^>]*>[^<]*Illustration/.test(fig), `${p.url}: a generated image is not captioned as an illustration`);
      }
    }
  }
});

test('there is no inline script: the CSP allows none', () => {
  for (const p of all) {
    for (const m of p.html.matchAll(/<script\b([^>]*)>/g)) {
      if (/type="application\/ld\+json"/.test(m[1])) continue;
      assert.ok(/\bsrc="/.test(m[1]), `${p.url} has an inline script`);
    }
  }
});

test('the lime accent fills at most one element on any page', () => {
  for (const p of all) assert.ok((p.html.match(/class="[^"]*\bpill\b/g) ?? []).length <= 1, `${p.url} uses the lime more than once`);
});

test('at least one page was built', () => { assert.ok(all.length > 0); });

test('no heading ends in a full stop: a heading is a label, not a sentence', () => {
  const bad = [];
  for (const p of pages()) {
    for (const m of p.html.matchAll(/<h([1-6])\b[^>]*>([\s\S]*?)<\/h\1>/g)) {
      const text = strip(m[2]);
      if (/\.$/.test(text) && !/\.\.\.$/.test(text)) bad.push(`${p.url}: ${text}`);
    }
  }
  assert.deepEqual(bad, []);
});

test('every page has exactly one h1, and headings do not skip a level', () => {
  for (const p of pages()) {
    const levels = [...p.html.matchAll(/<h([1-6])\b/g)].map((m) => Number(m[1]));
    assert.equal(levels.filter((l) => l === 1).length, 1, `${p.url} needs exactly one h1`);
    let prev = 1;
    for (const level of levels) {
      assert.ok(level <= prev + 1, `${p.url} jumps from h${prev} to h${level} (headings: ${levels.join(',')})`);
      prev = level;
    }
  }
});

test('every page has a main landmark and a skip link, and its language is British English', () => {
  for (const p of pages()) {
    assert.ok(/<main\b[^>]*id="main"/.test(p.html), `${p.url} has no main landmark`);
    assert.ok(/class="skip" href="#main"/.test(p.html), `${p.url} has no skip link`);
    assert.ok(/<html lang="en-GB"/.test(p.html), `${p.url} is not lang en-GB`);
  }
});
