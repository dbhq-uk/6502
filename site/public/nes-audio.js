// The NES's sound, played: an AudioWorklet processor, 'nes-sound', that plays
// the samples nes.js posts it from the shared ring in sample-worklet.js, where
// the ring, its drop rule and its fade now live (moved there unchanged when the
// Atari 2600 became the second console to need them, the Atari 2600 plan's
// task 13). The machine's samples have already been through the console's own
// filters (SampleBuffer: high-passes at 90 and 440 Hz, a low-pass at 14 kHz),
// so they sit round zero and are played as they are. Loaded by
// audioWorklet.addModule, which the CSP's script-src 'self' covers, and which
// follows its import of /sample-worklet.js. The rules are exported from here
// as before, for tests/nes-panel.test.mjs to run without a browser.

import { registerRing } from '/sample-worklet.js';

export { PRIME, MOST, admit, take, SampleRing } from '/sample-worklet.js';

registerRing('nes-sound');
