import test from 'node:test';
import assert from 'node:assert/strict';
import { accessRates, COUNTED } from '../src/models/nes-famicom-access.mjs';

// The NES board model's marks and rates, from the machine's access counters
// (panel.nes.accessCounts(), NesHost.AccessCounts): plan task 7, step 1, and
// its Review Focus 1 and 2.

test('a chip whose count moved is marked, with its accesses per second', () => {
  const r = accessRates([10, 0, 5, 5], [70, 0, 7, 5], 0.25);
  assert.deepEqual(r.ppu, { moved: true, perSecond: 240 });
  assert.deepEqual(r.apu, { moved: false, perSecond: 0 });
  assert.deepEqual(r.pad1, { moved: true, perSecond: 8 });
  assert.deepEqual(r.pad2, { moved: false, perSecond: 0 });
});

test('a counter that wraps past 2^31 and past 2^32 still gives the right difference', () => {
  // The host's int[]: 2^31 - 2 then -2^31 + 2 (four on, past the sign), and -1 then 3 (four on, past zero).
  const r = accessRates([2 ** 31 - 2, -1, 0, 0], [-(2 ** 31) + 2, 3, 0, 0], 1);
  assert.equal(r.ppu.perSecond, 4);
  assert.equal(r.apu.perSecond, 4);
});

test('the counters as the page hands them, an Int32Array copy, give the same differences, across the wrap too', () => {
  // panel.nes.accessCounts() is an Int32Array (JSExport hands an int[] over so): the same values as above.
  const before = Int32Array.from([2 ** 31 - 2, -1, 7, 0]);
  const after = Int32Array.from([-(2 ** 31) + 2, 3, 9, 0]);
  const r = accessRates(before, after, 0.5);
  assert.deepEqual(r, { ppu: { moved: true, perSecond: 8 }, apu: { moved: true, perSecond: 8 }, pad1: { moved: true, perSecond: 4 }, pad2: { moved: false, perSecond: 0 } });
});

test('a snapshot of the wrong length, or no time between them, is refused', () => {
  assert.throws(() => accessRates([0, 0, 0], [0, 0, 0], 1), /expected 4 counters/);
  assert.throws(() => accessRates([0, 0, 0, 0], [0, 0, 0, 0], 0), /more than 0/);
  assert.equal(COUNTED.length, 4);
});

test('the sampler divides by the time it measured, not the time it was asked to wait', async () => {
  const { createSampler } = await import('../src/models/nes-famicom-access.mjs');
  let t = 0;
  let c = [0, 0, 0, 0];
  const s = createSampler({ counts: () => c, now: () => t });
  assert.equal(s.sample(), null, 'the first sample has nothing to compare with');
  t += 250; c = [100, 0, 0, 0];
  assert.equal(s.sample().ppu.perSecond, 400);
  t += 1000; c = [200, 0, 0, 0];         // a timer that fired four times late
  assert.equal(s.sample().ppu.perSecond, 100);
});

test('after a reset the sampler takes a fresh baseline, so a new machine\'s counters never show as billions', async () => {
  const { createSampler } = await import('../src/models/nes-famicom-access.mjs');
  let t = 0;
  let c = [5000, 50, 10, 10];
  const s = createSampler({ counts: () => c, now: () => t });
  s.sample();
  t += 250;
  s.reset();                              // nes:region: a new machine, counting from zero
  c = [3, 0, 1, 0];
  assert.equal(s.sample(), null, 'no rate across two machines');
  t += 250; c = [43, 0, 3, 0];
  assert.deepEqual(s.sample().ppu, { moved: true, perSecond: 160 });
  assert.equal(s.sample(), null, 'two samples at one instant give no rate');
});

test('the sampler takes the page\'s Int32Array, and a machine with no counters yet (null, before Start) gives no rate and no error', async () => {
  const { createSampler } = await import('../src/models/nes-famicom-access.mjs');
  let t = 0;
  let c = null;
  const s = createSampler({ counts: () => c, now: () => t });
  assert.equal(s.sample(), null, 'no counters, no rate');
  t += 250; c = Int32Array.from([10, 0, 0, 0]);
  assert.equal(s.sample(), null, 'the first counters are a baseline, not a rate against nothing');
  t += 250; c = Int32Array.from([30, 0, 0, 0]);
  assert.equal(s.sample().ppu.perSecond, 80);
});

test('a count that goes down is a new machine: no rate, and the new counts are the baseline, never billions a second', async () => {
  // Fix round 1 of task 7, 6 Oct 2026: the page announces every new machine (nes:start, nes:region), but should one
  // ever come without, its counters start again from zero and the difference is negative.
  assert.equal(accessRates([100, 50, 0, 0], [40, 60, 0, 0], 0.25), null, 'a negative difference gives no rate');
  const { createSampler } = await import('../src/models/nes-famicom-access.mjs');
  let t = 0;
  let c = [90000, 4000, 300, 0];
  const s = createSampler({ counts: () => c, now: () => t });
  s.sample();
  t += 250; c = [10, 2, 1, 0];              // a new machine with no reset
  assert.equal(s.sample(), null, 'no rate across two machines');
  t += 250; c = [110, 2, 3, 0];
  assert.deepEqual(s.sample(), { ppu: { moved: true, perSecond: 400 }, apu: { moved: false, perSecond: 0 }, pad1: { moved: true, perSecond: 8 }, pad2: { moved: false, perSecond: 0 } });
});

test('the difference is taken as a signed 32-bit number, (b - a) | 0, as the page\'s script says a reader takes it', async () => {
  const fs = await import('node:fs');
  const src = fs.readFileSync(new URL('../src/models/nes-famicom-access.mjs', import.meta.url), 'utf8');
  assert.match(src, /\(after\[i\] - before\[i\]\) \| 0/);
  assert.doesNotMatch(src, />>> 0/);
  assert.match(fs.readFileSync(new URL('../public/nes.js', import.meta.url), 'utf8'), /\(b - a\) \| 0/);
});
