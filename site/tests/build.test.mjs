import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { page, pages, DIST } from './helpers.mjs';

test('the expected pages were built', () => {
  for (const url of ['/', '/journal/', '/machines/', '/chips/', '/family/', '/status/', '/about/', '/404.html']) {
    assert.ok(page(url), `${url} was not built`);
  }
});

test('a sitemap, robots.txt and the 404 page were built', () => {
  assert.ok(fs.existsSync(path.join(DIST, 'sitemap-index.xml')));
  const sitemap = fs.readFileSync(path.join(DIST, 'sitemap-0.xml'), 'utf8');
  assert.match(sitemap, /https:\/\/6502\.dbhq\.uk\/status\//);
  assert.match(fs.readFileSync(path.join(DIST, '404.html'), 'utf8'), /<h1[^>]*>\s*Page not found\s*<\/h1>/);
  assert.match(fs.readFileSync(path.join(DIST, 'robots.txt'), 'utf8'), /Sitemap: https:\/\/6502\.dbhq\.uk\/sitemap-index\.xml/);
});

// The two checks above name pages by hand. This one is derived from the build:
// the sitemap lists every built page except the 404, and nothing else, so a page
// added or dropped cannot leave the sitemap behind.
test('the sitemap lists exactly the pages that were built', () => {
  const sitemap = fs.readFileSync(path.join(DIST, 'sitemap-0.xml'), 'utf8');
  const listed = [...sitemap.matchAll(/<loc>https:\/\/6502\.dbhq\.uk([^<]*)<\/loc>/g)].map((m) => m[1]).sort();
  const built = pages().map((p) => p.url).filter((u) => u !== '/404.html').sort();
  assert.ok(built.length >= 8, `only ${built.length} pages were found in dist, so the comparison proves nothing`);
  assert.deepEqual(listed, built);
});
