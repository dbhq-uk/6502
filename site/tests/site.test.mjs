import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { pages, visibleText, DIST } from './helpers.mjs';

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

test('every internal link and asset resolves to a built file', () => {
  const missing = [];
  for (const p of all) {
    for (const m of p.html.matchAll(/\b(?:href|src)="([^"#?]+)[^"]*"/g)) {
      const target = m[1];
      if (!target.startsWith('/') || target.startsWith('//')) continue;
      const file = target.endsWith('/') ? path.join(DIST, target, 'index.html') : path.join(DIST, target);
      if (!fs.existsSync(file)) missing.push(`${p.url} -> ${target}`);
    }
  }
  assert.deepEqual(missing, []);
});

test('no page names a commercial game as the goal, or carries a non-affiliation line', () => {
  assert.ok(all.length > 0);
  for (const p of all) assert.ok(!/mega ?man|capcom/i.test(visibleText(p.html)), `${p.url} still mentions the dropped port goal`);
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

test('every image has alt text', () => {
  for (const p of all) {
    for (const m of p.html.matchAll(/<img\b[^>]*>/g)) assert.ok(/\balt="[^"]+"/.test(m[0]), `${p.url}: an image has no alt text`);
  }
});

// The list of generated images is the folder they live in, not an attribute a
// page has to remember to add: a new file there is checked without an edit here.
// An image can be shown as an <img> (the hero) or as a CSS background (the other
// two, checked in honest-pages.test.mjs); either way it must be used somewhere.
const IMAGERY = path.join(process.cwd(), 'src', 'assets', 'imagery');
const generated = fs.readdirSync(IMAGERY).map((f) => f.replace(/\.[a-z0-9]+$/, ''));

test('every generated image is captioned as an illustration, and has alt text saying so, wherever it is shown', () => {
  assert.ok(generated.length >= 3, 'the imagery folder holds fewer generated images than the site uses');
  const css = all.map((p) => p.html).join('\n');
  for (const name of generated) {
    let shown = 0;
    for (const p of all) {
      for (const m of p.html.matchAll(/<img\b[^>]*>/g)) {
        if (!new RegExp(`/${name}\\.[\\w-]+\\.(?:webp|png|jpe?g|avif)`).test(m[0])) continue;
        shown++;
        assert.match(m[0], /\balt="Illustration/, `${p.url}: the generated image ${name} has no alt text calling it an illustration`);
        const fig = p.html.slice(p.html.lastIndexOf('<figure', m.index), p.html.indexOf('</figure>', m.index));
        assert.ok(/<figcaption[^>]*>[^<]*Illustration/.test(fig), `${p.url}: the generated image ${name} is not captioned as an illustration`);
      }
    }
    if (shown === 0) assert.ok(new RegExp(`url\\([^)]*/${name}\\.`).test(css), `the generated image ${name} is in src/assets/imagery/ but no page shows it`);
  }
});

test('an image marked data-generated is one of the generated images, so the mark cannot be added to a photograph', () => {
  for (const p of all) {
    for (const m of p.html.matchAll(/<img\b[^>]*\bdata-generated\b[^>]*>/g)) assert.ok(generated.some((name) => m[0].includes(`/${name}.`)), `${p.url}: data-generated on an image that is not in the imagery folder`);
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

// The lime has two uses and only two. As a FILL it is the button's, once per
// page: that is the accent. As TEXT it is the syntax colour of the terminal
// window's "Passed" labels (.win .k), which is a code colour and not a second
// accent, and so does not count against the one fill. The focus rings are lime
// too, as an interaction state. Any other rule that takes the lime fails here,
// so a third use has to be argued for in this test.
const LIME_RULES = new Set(['.btn.pill', '.btn.pill:hover', '.win .k', ':focus-visible', '.data th button:focus-visible']);

test('the lime accent fills at most one element on any page, and its other uses are named', () => {
  for (const p of all) assert.ok((p.html.match(/class="[^"]*\bpill\b/g) ?? []).length <= 1, `${p.url} uses the lime more than once`);
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
  const users = [...css.matchAll(/([^{}]+)\{([^}]*var\(--lime\)[^}]*)\}/g)].map((m) => m[1].trim().replace(/\s+/g, ' '));
  assert.deepEqual(users.filter((u) => !LIME_RULES.has(u)), [], 'a rule uses the lime that this test does not name');
});

test('lime text is only the terminal window\'s syntax colour: every .k is inside a .win', () => {
  for (const p of all) {
    for (const m of p.html.matchAll(/<span class="k">/g)) {
      const window = p.html.lastIndexOf('<div class="win">', m.index);
      assert.ok(window >= 0 && p.html.indexOf('</pre>', window) > m.index, `${p.url} has lime text outside a terminal window`);
    }
  }
});

test('the 404 page has no canonical address and is not indexed; every other page has a canonical and is', () => {
  const missing = all.find((p) => p.url === '/404.html');
  assert.ok(missing, 'no 404 page');
  assert.doesNotMatch(missing.html, /rel="canonical"/);
  assert.doesNotMatch(missing.html, /property="og:url"/);
  assert.match(missing.html, /<meta name="robots" content="noindex"/);
  for (const p of all.filter((x) => x.url !== '/404.html')) assert.doesNotMatch(p.html, /<meta name="robots"/, `${p.url} is marked noindex`);
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
