// What the NES board model marks, and the rates its legend shows, from the
// machine's access counters (panel.nes.accessCounts(), the host's
// NesHost.AccessCounts, ChipAccesses in C#). A browser module: the model's
// bundle includes it, and the site's tests run it in Node.
//
// The sampling is ./access.mjs, which every board model shares (lifted out of
// this file by task 7 of the BBC Micro models plan, 13 October 2026, with no
// change in what it does); this file binds the NES's list of counted chips to
// it and keeps the exports the NES's model and tests have always used. There
// the difference between two snapshots is taken as a signed 32-bit number,
// (after[i] - before[i]) | 0, as public/nes.js says a reader takes it.

import { accessRates as ratesOf, createSampler as samplerOf } from './access.mjs';

/** The chips the machine counts, in the host's order (NesChip in C#). */
export const COUNTED = ['ppu', 'apu', 'pad1', 'pad2'];

/** accessRates in ./access.mjs, for the NES's chips: `before` and `after` are four counters, `seconds` apart. */
export const accessRates = (before, after, seconds) => ratesOf(COUNTED, before, after, seconds);

/** createSampler in ./access.mjs, for the NES's chips: `counts()` gives the four counters, `now()` the time in ms. */
export const createSampler = ({ counts, now }) => samplerOf({ counted: COUNTED, counts, now });
