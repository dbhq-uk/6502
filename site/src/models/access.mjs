// What a board model marks, and the rates its legend shows, from a machine's
// access counters: the processor's reads and writes of each chip it reaches by
// address, which the machine's host hands over as one signed 32-bit number a
// chip, in the machine's own order. Shared by the NES's board model (through
// ./nes-famicom-access.mjs, which binds the NES's list) and the BBC Micro's
// (its list is COUNTED in ./bbc-micro-board-layout.mjs, BbcChip's order). A
// browser module with no imports: the models' bundles include it, and the
// site's tests run it in Node.

/**
 * What changed between two snapshots of the counters, taken `seconds` apart:
 * for each chip in `counted`, whether its count moved and its accesses per
 * second, rounded to a whole number, keyed by the chip's name in `counted`'s
 * order. The host hands the counters as signed 32-bit numbers that wrap (an
 * Int32Array on the page), so the difference is taken as a signed 32-bit
 * number, (b - a) | 0: right across a wrap while fewer than 2^31 accesses fall
 * between the two. A difference below zero is a new machine, whose counters
 * started again from zero, and gives null: no rate across two machines. A
 * snapshot of another length is an error: the host and the list have drifted
 * apart.
 */
export function accessRates(counted, before, after, seconds) {
  if (before.length !== counted.length || after.length !== counted.length) throw new Error(`expected ${counted.length} counters, got ${before.length} and ${after.length}`);
  if (!(seconds > 0)) throw new Error(`seconds must be more than 0, not ${seconds}`);
  const d = counted.map((_, i) => (after[i] - before[i]) | 0);
  if (d.some((v) => v < 0)) return null;
  return Object.fromEntries(counted.map((chip, i) => [chip, { moved: d[i] > 0, perSecond: Math.round(d[i] / seconds) }]));
}

/**
 * Samples a machine's counters for a board model. Each `sample()` reads
 * `counts()` and the clock `now()` (milliseconds, as performance.now gives) and
 * returns `accessRates` for `counted` against the last sample, divided by the
 * time measured between the two, not the time the sampler was asked to wait: a
 * timer that fires late (a background tab, a busy machine) still gives true
 * rates. The first sample, and the first after `reset()`, returns null: the
 * model calls `reset()` when the machine starts or is replaced, so a difference
 * is never taken across two machines. A count that went down with no
 * `reset()`, a new machine the page did not announce, gives null too, and its
 * counts are the new baseline. Two samples at the same instant also give null,
 * and so does a machine with no counters yet (`counts()` null, before Start),
 * which leaves no baseline behind it.
 */
export function createSampler({ counted, counts, now }) {
  if (!Array.isArray(counted) || counted.length === 0) throw new Error('a sampler needs the list of counted chips, in the host\'s order');
  let last = null;
  return {
    sample() {
      const got = counts();
      if (got == null) {
        last = null;
        return null;
      }
      const snap = { counts: Array.from(got), at: now() };
      const prev = last;
      last = snap;
      if (!prev || snap.at <= prev.at) return null;
      return accessRates(counted, prev.counts, snap.counts, (snap.at - prev.at) / 1000);
    },
    reset() {
      last = null;
    },
  };
}
