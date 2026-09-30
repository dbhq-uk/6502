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

// The post-deploy check fetches the files a page needs. It is derived from what
// the pages load and what those scripts import, so a script added later cannot be
// left out of the list (table-sort.js was, once: machines-table.js imports it).
const root = path.resolve(process.cwd(), '..');
const deploy = fs.readFileSync(path.join(root, '.github', 'workflows', 'deploy-site.yml'), 'utf8');
const validate = fs.readFileSync(path.join(root, '.github', 'workflows', 'validate.yml'), 'utf8');

test('the deploy checks that every script a page loads, and every script those import, is serving', () => {
  const loaded = new Set();
  for (const p of pages()) for (const m of p.html.matchAll(/<script\b[^>]*\bsrc="(\/[^"]+\.js)"/g)) loaded.add(m[1].slice(1));
  assert.ok(loaded.size >= 3, 'no scripts were found in the built pages');
  for (const f of [...loaded]) {
    const source = fs.readFileSync(path.join(process.cwd(), 'public', f), 'utf8');
    for (const m of source.matchAll(/^import\b[^;]*from\s+["'](\/[^"']+\.js)["']/gm)) loaded.add(m[1].slice(1));
  }
  const listed = (/for f in ([^;]*); do/.exec(deploy)?.[1] ?? '').split(/\s+/);
  for (const f of loaded) assert.ok(listed.includes(f), `deploy-site.yml does not check that ${f} is serving`);
});

test('the test floor is the same number in both workflows, and both read the count back', () => {
  const floor = (text) => Number(/"\$\{PASS:-0\}" -ge (\d+)/.exec(text)?.[1]);
  assert.ok(floor(deploy) > 0, 'deploy-site.yml has no test floor');
  assert.equal(floor(validate), floor(deploy), 'validate.yml and deploy-site.yml have different test floors');
  assert.match(validate, /count\(\) \{/);
  assert.match(deploy, /Raise the number when the suite grows/);
  assert.match(validate, /Raise the number when the suite grows/);
});

test('no local path and no private address is published in the docs the site renders, its README or its manifest', () => {
  const files = [
    ...fs.readdirSync(path.join(root, 'docs', 'journal')).map((f) => path.join(root, 'docs', 'journal', f)),
    path.join(root, 'docs', 'the-6502-family.md'), path.join(root, 'docs', 'known-differences.md'),
    path.join(root, 'docs', 'superpowers', 'plans', '2026-09-30-site-v1.md'),
    path.join(process.cwd(), 'README.md'), path.join(process.cwd(), 'DESIGN.md'), path.join(process.cwd(), 'package.json'),
  ];
  const local = /\/home\/[a-z]|100\.\d+\.\d+\.\d+|~\/\.[a-z]/;
  for (const f of files) assert.doesNotMatch(fs.readFileSync(f, 'utf8'), local, `${path.relative(root, f)} names a local path or a private address`);
});

test('the dev and preview scripts take their host from SITE_HOST, and the site names its Node floor', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8'));
  for (const name of ['dev', 'preview']) assert.match(pkg.scripts[name], /--host "\$\{SITE_HOST:-127\.0\.0\.1\}"/, `${name} does not read SITE_HOST`);
  assert.equal(pkg.engines?.node, '>=22.22');
  assert.match(fs.readFileSync(path.join(process.cwd(), 'README.md'), 'utf8'), /SITE_HOST/);
});

test('the README lists every test file, and no file it lists is missing', () => {
  const readme = fs.readFileSync(path.join(process.cwd(), 'README.md'), 'utf8');
  const onDisk = fs.readdirSync(path.join(process.cwd(), 'tests')).filter((f) => f.endsWith('.test.mjs'));
  for (const f of onDisk) assert.ok(readme.includes(`\`${f}\``), `README.md does not list ${f}`);
  for (const m of readme.matchAll(/^\| `([\w-]+\.test\.mjs)` \|/gm)) assert.ok(onDisk.includes(m[1]), `README.md lists ${m[1]}, which does not exist`);
});
