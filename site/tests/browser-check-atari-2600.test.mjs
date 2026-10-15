import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { differing, sameInOtherColours } from '../scripts/browser-check-atari-2600.mjs';
import { PINS_FILE } from '../src/lib/pins.mjs';

// The Atari 2600 browser check's comparisons, on made-up pictures: the region
// switch must give the same picture in other colours, read on the rows both
// windows show, and the play checks count the pixels that changed.

/** A picture `width` wide from rows of colour numbers, each number drawn as its own RGB. */
const picture = (rows, rgb = (n) => [n * 10, n * 20, n * 30]) => Uint8Array.from(rows.flatMap((row) => row.flatMap((n) => [...rgb(n), 255])));

test('the same picture with its colours renamed one for one is the same picture, and says how many colours changed', () => {
  const a = picture([[1, 2, 2], [3, 1, 0]]);
  const b = picture([[1, 2, 2], [3, 1, 0]], (n) => (n === 0 ? [0, 0, 0] : [n * 11, n * 21, n * 31]));
  const got = sameInOtherColours(a, b, { width: 3, rows: 2, fromA: 0, fromB: 0 });
  assert.deepEqual(got, { consistent: true, colours: 4, changed: 3, broken: null });
  assert.equal(sameInOtherColours(a, a, { width: 3, rows: 2, fromA: 0, fromB: 0 }).changed, 0);
});

test('one pixel out of step, either way round, breaks it, and the rows are matched from their own first rows', () => {
  const a = picture([[1, 2], [2, 1]]);
  assert.deepEqual(sameInOtherColours(a, picture([[1, 2], [2, 2]]), { width: 2, rows: 2, fromA: 0, fromB: 0 }).broken, { x: 1, y: 1 });
  // Two colours of a drawn as one of b is not a renaming.
  assert.equal(sameInOtherColours(picture([[1, 2]]), picture([[3, 3]]), { width: 2, rows: 1, fromA: 0, fromB: 0 }).consistent, false);
  // b has one row more at its top: from row 1 of b it is a's picture.
  const tall = picture([[5, 5], [1, 2], [2, 1]]);
  assert.equal(sameInOtherColours(a, tall, { width: 2, rows: 2, fromA: 0, fromB: 0 }).consistent, false);
  assert.equal(sameInOtherColours(a, tall, { width: 2, rows: 2, fromA: 0, fromB: 1 }).consistent, true);
});

test('differing counts the pixels whose colour changed, and not their alpha', () => {
  const a = picture([[1, 2, 3]]);
  const b = picture([[1, 4, 3]]);
  assert.equal(differing(a, b), 1);
  const c = Uint8Array.from(a);
  c[3] = 0;
  assert.equal(differing(a, c), 0);
});

test('the check fetches its test cartridge only from the suite\'s fork, at the pinned commit, through the pinned manifest', () => {
  const source = fs.readFileSync(path.join(process.cwd(), 'scripts', 'browser-check-atari-2600.mjs'), 'utf8');
  assert.match(source, /`https:\/\/raw\.githubusercontent\.com\/dbhq-uk\/missingno\/\$\{commit\}\/\$\{TEST_ROM\}`/);
  assert.match(source, /const commit = pin\('Atari2600BinariesCommit'\);/);
  assert.match(source, /pin\('Atari2600SuiteManifestSha256'\)/);
  assert.match(source, /if \(gitBlob\(rom\) !== entry\[0\]\) throw new Error/);
  const manifest = fs.readFileSync(path.join(path.dirname(PINS_FILE), 'atari-2600-suite.manifest'), 'utf8');
  assert.match(manifest, /^[0-9a-f]{40} missingno crates\/systems\/vcs\/tests\/accuracy\/roms\/harness\/calibration_ntsc\.a26$/m);
});
