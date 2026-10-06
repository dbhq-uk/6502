// The NES's sound, played: an AudioWorklet processor that plays the samples
// nes.js posts it, a frame's worth at a time, each a Float32Array at the
// AudioContext's rate (the page asks for the rate the machine makes them at,
// so nothing is resampled twice). The machine's samples have already been
// through the console's own filters (SampleBuffer: high-passes at 90 and 440
// Hz, a low-pass at 14 kHz), so they sit round zero and are played as they
// are. Loaded by audioWorklet.addModule, which the CSP's script-src 'self'
// covers. A worklet is a module, so the rules below are exported too, for
// tests/nes-panel.test.mjs to run without a browser.
//
// THE RING NEVER GROWS (the plan's Review Focus 3). The samples wait in a ring
// of MOST seconds, allocated once. The machine makes sound as fast as the page
// runs it, which is real time, and the audio hardware takes it at its own
// steady pace, so the two drift and the frames arrive in bursts. Playing starts
// once PRIME seconds are queued, so a late frame does not starve it at once.
// When more than MOST would be queued (a busy main thread, then a burst), the
// oldest are dropped down to PRIME, so the sound never lags the picture by more
// than that and the ring holds no more than it was made with. The machine's own
// buffer does the same on its side: a hidden tab, or sound turned off, drops
// its oldest samples and allocates nothing (SampleBuffer.Dropped).
//
// RUNNING DRY. When the ring empties the output falls from the last sample's
// level to silence over a few milliseconds, rather than jumping to zero, which
// would click. Then it waits for PRIME seconds again before it plays.
//
// A message { flush: true } empties the ring, for when the sound is turned on
// or the page comes back from being hidden.

export const PRIME = 0.04;
export const MOST = 0.15;
// Each sample of a dry spell keeps this share of the last: about 4 ms to fall by e at 48 kHz.
const FADE = 0.995;

/**
 * What arrives: with `queued` samples waiting and `incoming` more, how many of
 * the oldest to drop, and how many are then queued. Under `most` nothing goes;
 * over it, the oldest go until `prime` are left.
 */
export function admit(queued, incoming, prime, most) {
  const total = queued + incoming;
  if (total <= most) return { drop: 0, queued: total };
  return { drop: total - prime, queued: prime };
}

/**
 * What one block of output takes: `wanted` samples, with `queued` waiting.
 * Nothing until `prime` are queued; then as many as there are, up to `wanted`.
 * A block that finds fewer than it wants has run dry, and playing stops until
 * `prime` are queued again.
 */
export function take(queued, wanted, playing, prime) {
  const starts = playing || queued >= prime;
  if (!starts) return { taken: 0, playing: false };
  const taken = Math.min(queued, wanted);
  return { taken, playing: taken === wanted };
}

/** The ring: MOST seconds of samples at `rate`, allocated once. */
export class SampleRing {
  constructor(rate) {
    this.capacity = Math.round(MOST * rate);
    this.prime = Math.round(PRIME * rate);
    this.samples = new Float32Array(this.capacity);
    this.start = 0;
    this.queued = 0;
    this.playing = false;
    this.level = 0;
  }

  /** Queues `chunk`, dropping the oldest samples (queued first, then the chunk's own) as admit says. */
  push(chunk) {
    const { drop } = admit(this.queued, chunk.length, this.prime, this.capacity);
    const fromQueue = Math.min(drop, this.queued);
    this.start = (this.start + fromQueue) % this.capacity;
    this.queued -= fromQueue;
    let from = drop - fromQueue;
    while (from < chunk.length) {
      const at = (this.start + this.queued) % this.capacity;
      const n = Math.min(chunk.length - from, this.capacity - at);
      this.samples.set(chunk.subarray(from, from + n), at);
      this.queued += n;
      from += n;
    }
  }

  /** Fills `out` with the next samples, or with the fade when there are not enough. Returns how many came from the ring. */
  pull(out) {
    const { taken, playing } = take(this.queued, out.length, this.playing, this.prime);
    for (let i = 0; i < taken; i++) {
      out[i] = this.samples[this.start];
      this.start = (this.start + 1) % this.capacity;
    }
    this.queued -= taken;
    if (taken > 0) this.level = out[taken - 1];
    for (let i = taken; i < out.length; i++) {
      this.level *= FADE;
      out[i] = this.level;
    }
    this.playing = playing;
    return taken;
  }

  flush() {
    this.start = 0;
    this.queued = 0;
    this.playing = false;
  }
}

// Only in an AudioWorkletGlobalScope: a test imports the rules above and nothing more.
if (typeof registerProcessor === 'function') {
  registerProcessor('nes-sound', class extends AudioWorkletProcessor {
    constructor() {
      super();
      this.ring = new SampleRing(sampleRate);
      this.port.onmessage = (event) => {
        if (event.data instanceof Float32Array) this.ring.push(event.data);
        else if (event.data?.flush) this.ring.flush();
      };
    }

    process(inputs, outputs) {
      this.ring.pull(outputs[0][0]);
      return true;
    }
  });
}
