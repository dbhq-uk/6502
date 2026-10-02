import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from '../src/lib/registry.mjs';

// AGENTS.md rule 3: the tests and the build never depend on a third party's
// server. Every file they fetch comes from a repository in the dbhq-uk
// organisation (a fork or a mirror we hold), pinned to a commit and a hash.
// This reads the places that fetch and fails on any other host.

const SCANNED = [
  ['tests', /\.(cs|sh|py|mjs)$/],
  ['tools', /\.(cs|sh|py|mjs)$/],
  ['bench', /\.(cs|sh|py|mjs)$/],
  ['src', /\.(cs|sh|py|mjs)$/],
  ['site/scripts', /\.(mjs|sh)$/],
];
const SKIP = new Set(['node_modules', 'bin', 'obj', 'publish', 'dist', '.astro', '.testdata']);
const ALLOWED = [
  /^https:\/\/raw\.githubusercontent\.com\/dbhq-uk\//,
  /^https:\/\/api\.github\.com\/repos\/dbhq-uk\//,
  /^https:\/\/github\.com\/dbhq-uk\//,
  /^https?:\/\/(127\.0\.0\.1|localhost)([:/]|$)/,
  /^https:\/\/6502\.dbhq\.uk\b/,
  /^https?:\/\/(www\.)?w3\.org\//, // XML namespaces, never fetched
];

function* walk(dir, pattern) {
  const full = path.join(REPO_ROOT, dir);
  if (!fs.existsSync(full)) return;
  for (const entry of fs.readdirSync(full, { withFileTypes: true })) {
    if (SKIP.has(entry.name)) continue;
    const rel = path.join(dir, entry.name);
    if (entry.isDirectory()) yield* walk(rel, pattern);
    else if (pattern.test(entry.name)) yield rel;
  }
}

const isComment = (line) => /^\s*(\/\/|#|\*|\/\*)/.test(line);

test('every file the tests and the build fetch comes from a dbhq-uk repository', () => {
  const found = [];
  const foreign = [];
  for (const [dir, pattern] of SCANNED) {
    for (const file of walk(dir, pattern)) {
      fs.readFileSync(path.join(REPO_ROOT, file), 'utf8').split('\n').forEach((line, i) => {
        if (isComment(line)) return;
        for (const m of line.matchAll(/https?:\/\/[^\s"'`)<>]+/g)) {
          found.push(m[0]);
          if (!ALLOWED.some((re) => re.test(m[0]))) foreign.push(`${file}:${i + 1} ${m[0]}`);
        }
      });
    }
  }
  assert.ok(found.length >= 5, `only ${found.length} URLs found: the scan is looking in the wrong places`);
  assert.deepEqual(foreign, [], 'these fetch from a host we do not control: mirror the source under dbhq-uk and pin it');
});

test('the mirrors named in Pins.cs are pinned to a full commit, not a branch', () => {
  const pins = fs.readFileSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Cpu6502.TestSupport', 'Pins.cs'), 'utf8');
  const commits = [...pins.matchAll(/(\w+Commit) = "([0-9a-f]+)"/g)];
  assert.ok(commits.length >= 4, 'the pinned commits were not found');
  for (const [, name, sha] of commits) assert.match(sha, /^[0-9a-f]{40}$/, `${name} is not a full 40-character commit`);
});
