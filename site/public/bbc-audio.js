// The BBC Micro's sound, played: an AudioWorklet processor that plays the
// samples bbc-micro.js posts it, a frame's worth at a time, each a Float32Array
// of samples from 0 to 1 at the AudioContext's rate (the page asks for the same
// rate the machine makes them at, so nothing is resampled twice). Loaded by
// audioWorklet.addModule, which the CSP's script-src 'self' covers.
//
// THE QUEUE. The machine makes sound as fast as the page runs it, which is real
// time, and the audio hardware takes it at its own steady pace, so the two
// drift a little and the page's frames arrive in uneven bursts. Playing starts
// once PRIME seconds are queued, so a late frame does not starve it at once.
// If more than MOST seconds pile up (a busy main thread, then a burst), the
// oldest are dropped down to PRIME again, so the sound never lags the picture by
// more than that.
//
// RUNNING DRY. When the queue empties the output holds the last sample's level,
// which the filter below turns into silence within a few milliseconds, rather
// than jumping to zero, which would click. Then it waits for PRIME seconds
// again before it plays.
//
// THE FILTER. The machine's samples run from 0 to 1, so a silent chip sits at a
// steady level, not at zero. A one-pole high-pass filter takes that level off,
// as the coupling capacitor on a real output does: y = x - x' + R y', with R
// close to 1, which passes everything above a few hertz. The gain keeps four
// channels at full volume inside the range the output takes.
//
// A message { flush: true } empties the queue, for when the sound is turned on
// or the page comes back from being hidden.

const PRIME = 0.04;
const MOST = 0.15;
const R = 0.995;
const GAIN = 0.5;

class BbcSound extends AudioWorkletProcessor {
  constructor() {
    super();
    this.chunks = [];
    this.offset = 0;
    this.queued = 0;
    this.playing = false;
    // The filter starts at the first sample's level, so the first sound does not click in.
    this.x = null;
    this.y = 0;
    this.port.onmessage = (event) => {
      if (event.data instanceof Float32Array) this.add(event.data);
      else if (event.data?.flush) this.flush();
    };
  }

  add(chunk) {
    if (chunk.length === 0) return;
    this.x ??= chunk[0];
    this.chunks.push(chunk);
    this.queued += chunk.length;
    if (this.queued > MOST * sampleRate) {
      let drop = this.queued - Math.round(PRIME * sampleRate);
      while (drop > 0 && this.chunks.length > 0) {
        const left = this.chunks[0].length - this.offset;
        const n = Math.min(left, drop);
        this.offset += n;
        this.queued -= n;
        drop -= n;
        if (this.offset === this.chunks[0].length) {
          this.chunks.shift();
          this.offset = 0;
        }
      }
    }
  }

  flush() {
    this.chunks = [];
    this.offset = 0;
    this.queued = 0;
    this.playing = false;
  }

  process(inputs, outputs) {
    const out = outputs[0][0];
    if (this.x === null) {
      out.fill(0);
      return true;
    }
    if (!this.playing && this.queued >= PRIME * sampleRate) this.playing = true;
    for (let i = 0; i < out.length; i++) {
      let x = this.x;
      if (this.playing && this.queued > 0) {
        const chunk = this.chunks[0];
        x = chunk[this.offset++];
        this.queued--;
        if (this.offset === chunk.length) {
          this.chunks.shift();
          this.offset = 0;
        }
      } else {
        this.playing = false;
      }
      this.y = x - this.x + R * this.y;
      this.x = x;
      out[i] = GAIN * this.y;
    }
    return true;
  }
}

registerProcessor('bbc-sound', BbcSound);
