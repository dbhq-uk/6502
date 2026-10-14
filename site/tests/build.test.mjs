import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { page, pages, DIST } from './helpers.mjs';
import { readFloor, floorProblems } from './floor.mjs';

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

test('the test floor is one number in tests/floor.json, and both workflows read the count back and compare it with that', () => {
  assert.ok(readFloor() > 0);
  assert.match(validate, /count\(\) \{/);
  assert.match(deploy, /count\(\) \{/);
  assert.deepEqual(floorProblems('validate.yml', validate), []);
  assert.deepEqual(floorProblems('deploy-site.yml', deploy), []);
});

// The check above can only be trusted if it can fail. Each case breaks a scratch copy
// of a workflow in one way, and the check must say so; a bad floor file must throw.
test('neither workflow holds a floor of its own, and the check shows it by failing on broken copies', () => {
  const read = 'FLOOR=$(jq -r .floor tests/floor.json)';
  const compare = '[ "${PASS:-0}" -ge "$FLOOR" ]';
  for (const [name, text] of [['validate.yml', validate], ['deploy-site.yml', deploy]]) {
    assert.ok(text.includes(read) && text.includes(compare), `${name} does not read the floor and compare with it`);
    const broken = {
      'a typed number in the comparison': text.replace(compare, '[ "${PASS:-0}" -ge 641 ]'),
      'a typed number for the floor': text.replace(read, 'FLOOR=641'),
      'a floor read from another file': text.replace(read, read.replace('floor.json', 'other.json')),
      'the comparison removed': text.replace(compare, 'true'),
      'a second comparison with a number': text.replace(compare, `${compare}\n          [ "\${PASS:-0}" -ge 641 ]`),
    };
    for (const [how, copy] of Object.entries(broken)) {
      assert.notEqual(copy, text, `${how}: the scratch copy was not changed`);
      assert.notDeepEqual(floorProblems(name, copy), [], `${name} with ${how} was not caught`);
    }
  }
  const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'floor-'));
  try {
    for (const bad of ['{"floor": "641"}', '{"floor": 0}', '{"floor": 6.5}', '{}']) {
      const file = path.join(scratch, 'floor.json');
      fs.writeFileSync(file, bad);
      assert.throws(() => readFloor(file), /positive whole number/, `${bad} was accepted as a floor`);
    }
  } finally {
    fs.rmSync(scratch, { recursive: true, force: true });
  }
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
