import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { accessRates, createSampler } from '../src/models/access.mjs';
import * as nes from '../src/models/nes-famicom-access.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';

// The access sampler every board model shares (site/src/models/access.mjs):
// the NES's, lifted out of nes-famicom-access.mjs by task 7 of the BBC Micro
// models plan (re-plan of 13 October 2026), taking the list of counted chips.
// The NES binds its own list and keeps its exports, and its tests
// (nes-famicom-access.test.mjs) run unedited; these hold the shared code and
// the BBC Micro's use of it.

// BbcChip's members, in its order, read from the C#: the host hands the counts in this order.
const bbcChip = (() => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro', 'BbcChip.cs'), 'utf8');
  const body = source.slice(source.indexOf('public enum BbcChip'), source.indexOf('}', source.indexOf('public enum BbcChip')));
  return [...body.matchAll(/^\s+(\w+),$/gm)].map((m) => m[1][0].toLowerCase() + m[1].slice(1));
})();

test('the shared rates take the list of counted chips: the BBC Micro\'s ten, in BbcChip\'s order', () => {
  assert.equal(bbcChip.length, 10);
  const before = bbcChip.map(() => 0);
  const after = bbcChip.map((_, i) => i * 3);
  const r = accessRates(bbcChip, before, after, 0.25);
  assert.deepEqual(Object.keys(r), bbcChip, 'the rates are keyed by the counted list, in its order');
  bbcChip.forEach((chip, i) => assert.deepEqual(r[chip], { moved: i > 0, perSecond: i * 12 }, chip));
  assert.throws(() => accessRates(bbcChip, before.slice(1), after, 1), /expected 10 counters, got 9 and 10/);
  assert.throws(() => accessRates(bbcChip, before, after, 0), /more than 0/);
});

test('the shared rates take the difference as a signed 32-bit number, across a wrap, and a count that fell gives no rate', () => {
  const two = ['a', 'b'];
  assert.deepEqual(accessRates(two, Int32Array.from([2 ** 31 - 2, -1]), Int32Array.from([-(2 ** 31) + 2, 3]), 1), { a: { moved: true, perSecond: 4 }, b: { moved: true, perSecond: 4 } });
  assert.equal(accessRates(two, [100, 0], [40, 0], 1), null);
  const src = fs.readFileSync(path.join(process.cwd(), 'src', 'models', 'access.mjs'), 'utf8');
  assert.match(src, /\(after\[i\] - before\[i\]\) \| 0/);
  assert.doesNotMatch(src, />>> 0/);
});

test('the shared sampler takes the counted list, divides by the time it measured, and takes a fresh baseline after a reset or a fall', () => {
  let t = 0;
  let c = null;
  const s = createSampler({ counted: bbcChip, counts: () => c, now: () => t });
  assert.equal(s.sample(), null, 'no counters before Start');
  c = bbcChip.map(() => 0);
  assert.equal(s.sample(), null, 'the first counters are a baseline');
  t += 1000; c = bbcChip.map((_, i) => (i === 7 ? 50 : 0));   // 7 is the 8271
  const r = s.sample();
  assert.deepEqual(r.fdc8271, { moved: true, perSecond: 50 });
  assert.equal(Object.values(r).filter((x) => x.moved).length, 1);
  s.reset();
  t += 250; c = bbcChip.map(() => 1);
  assert.equal(s.sample(), null, 'no rate across two machines');
  t += 250; c = bbcChip.map(() => 0);
  assert.equal(s.sample(), null, 'a count that fell is a new machine');
  assert.throws(() => createSampler({ counts: () => c, now: () => t }), /counted/);
});

test('the NES\'s sampler is the shared one with its own list bound: the same results for the same counts, and no arithmetic of its own', () => {
  assert.deepEqual(nes.COUNTED, ['ppu', 'apu', 'pad1', 'pad2']);
  const cases = [
    [[10, 0, 5, 5], [70, 0, 7, 5], 0.25],
    [[2 ** 31 - 2, -1, 0, 0], [-(2 ** 31) + 2, 3, 0, 0], 1],
    [[100, 50, 0, 0], [40, 60, 0, 0], 0.25],
  ];
  for (const [a, b, s] of cases) assert.deepEqual(nes.accessRates(a, b, s), accessRates(nes.COUNTED, a, b, s));
  let t = 0;
  let c = [0, 0, 0, 0];
  const mine = nes.createSampler({ counts: () => c, now: () => t });
  const shared = createSampler({ counted: nes.COUNTED, counts: () => c, now: () => t });
  for (const next of [[0, 0, 0, 0], [40, 2, 1, 0], [90, 2, 1, 1], [3, 0, 0, 0], [13, 1, 0, 0]]) {
    t += 250; c = next;
    assert.deepEqual(mine.sample(), shared.sample());
  }
  const src = fs.readFileSync(path.join(process.cwd(), 'src', 'models', 'nes-famicom-access.mjs'), 'utf8');
  assert.match(src, /from '\.\/access\.mjs'/);
  // Its only code is the binding: no difference, rounding or loop of its own (its comments may name them).
  const code = src.replace(/^\s*(\/\/|\*|\/\*\*).*$/gm, '');
  assert.doesNotMatch(code, /Math\.round|\| 0|for \(|\.map\(/);
});
