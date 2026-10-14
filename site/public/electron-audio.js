// The Electron's sound, played: an AudioWorklet processor that plays the
// samples electron.js posts it, a frame's worth at a time, each a Float32Array
// at the AudioContext's rate (the page asks for the same rate the machine makes
// them at, so nothing is resampled twice). Loaded by audioWorklet.addModule,
// which the CSP's script-src 'self' covers.
//
// The queue is the BBC Micro's (bbc-audio.js): playing starts once PRIME seconds
// are queued, so a late frame does not starve it at once, and if more than MOST
// seconds pile up the oldest are dropped down to PRIME again, so the sound never
// lags the picture by more than that.
//
// NO FILTER. The BBC Micro's chip gives samples from 0 to 1, so its worklet
// takes the steady level off. The Electron's one-bit output is already centred:
// silence is exactly 0 and a tone swings about a half either side of it
// (UlaSound, whose own high-pass does what a coupling capacitor does), so these
// samples are played as they come, at GAIN.
//
// RUNNING DRY. When the queue empties the output does not jump to zero, which
// would click in the middle of a tone: it falls from the last level to silence
// by DECAY a sample, a few milliseconds. Then it waits for PRIME seconds again
// before it plays.
//
// A message { flush: true } empties the queue, for when the sound is turned on
// or the page comes back from being hidden.

const PRIME = 0.04;
const MOST = 0.15;
const DECAY = 0.995;
const GAIN = 0.5;

class ElectronSound extends AudioWorkletProcessor {
  constructor() {
    super();
    this.chunks = [];
    this.offset = 0;
    this.queued = 0;
    this.playing = false;
    this.level = 0;
    this.port.onmessage = (event) => {
      if (event.data instanceof Float32Array) this.add(event.data);
      else if (event.data?.flush) this.flush();
    };
  }

  add(chunk) {
    if (chunk.length === 0) return;
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
    if (!this.playing && this.queued >= PRIME * sampleRate) this.playing = true;
    for (let i = 0; i < out.length; i++) {
      if (this.playing && this.queued > 0) {
        const chunk = this.chunks[0];
        this.level = chunk[this.offset++];
        this.queued--;
        if (this.offset === chunk.length) {
          this.chunks.shift();
          this.offset = 0;
        }
      } else {
        this.playing = false;
        this.level *= DECAY;
      }
      out[i] = GAIN * this.level;
    }
    return true;
  }
}

registerProcessor('electron-sound', ElectronSound);
