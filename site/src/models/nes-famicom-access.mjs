// What the NES board model marks, and the rates its legend shows, from the
// machine's access counters (panel.nes.accessCounts(), the host's
// NesHost.AccessCounts, ChipAccesses in C#). A browser module with no imports:
// the model's bundle includes it, and the site's tests run it in Node.

/** The chips the machine counts, in the host's order (NesChip in C#). */
export const COUNTED = ['ppu', 'apu', 'pad1', 'pad2'];

/**
 * What changed between two snapshots of the machine's access counters, taken
 * `seconds` apart: for each counted chip, whether its count moved and its
 * accesses per second, rounded to a whole number. The host hands the counters
 * as signed 32-bit numbers that wrap (an Int32Array on the page), so the
 * difference is taken modulo 2^32, which is right across a wrap as long as
 * fewer than 2^32 accesses fall between the two. A snapshot of another length
 * is an error: the host and this list have drifted apart.
 */
export function accessRates(before, after, seconds) {
  if (before.length !== COUNTED.length || after.length !== COUNTED.length) throw new Error(`expected ${COUNTED.length} counters, got ${before.length} and ${after.length}`);
  if (!(seconds > 0)) throw new Error(`seconds must be more than 0, not ${seconds}`);
  return Object.fromEntries(COUNTED.map((chip, i) => {
    const d = (after[i] - before[i]) >>> 0;
    return [chip, { moved: d > 0, perSecond: Math.round(d / seconds) }];
  }));
}

/**
 * Samples the machine's counters for the board model. Each `sample()` reads
 * `counts()` and the clock `now()` (milliseconds, as performance.now gives) and
 * returns `accessRates` against the last sample, divided by the time measured
 * between the two, not the time the sampler was asked to wait: a timer that
 * fires late (a background tab, a busy machine) still gives true rates. The
 * first sample, and the first after `reset()`, returns null: the model calls
 * `reset()` when the machine starts or is replaced (a region change builds a
 * new machine whose counters start again from zero), so a difference is never
 * taken across two machines. Two samples at the same instant also give null,
 * and so does a machine with no counters yet (`counts()` null, before Start),
 * which leaves no baseline behind it.
 */
export function createSampler({ counts, now }) {
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
      return accessRates(prev.counts, snap.counts, (snap.at - prev.at) / 1000);
    },
    reset() {
      last = null;
    },
  };
}
