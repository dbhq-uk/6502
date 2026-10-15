// The sound button and the page's side of the sound worklet, which a console's
// page shares. Moved out of nes.js, unchanged in what it does, when the Atari
// 2600 became the second console to need it (the Atari 2600 plan's task 13).
// Kept apart from the worklet's own module (sample-worklet.js), which runs in
// the audio thread and has no page to talk to.
//
// THE SOUND is off until the visitor turns it on, because browsers do not let a
// page start sound by itself. Then each frame's samples go to the worklet,
// which plays them from a ring that never grows. While sound is off, or the
// page is hidden, the machine's own buffer drops its oldest samples rather
// than growing.
//
// soundOutput({ panel, button, line, module, processor, rate, transform })
//   panel      the machine's element, whose data-sound says on, off or failed
//   button     the button that turns it on and off; line, the line that says which
//   module     the worklet's module, for audioWorklet.addModule; processor, its name
//   rate       the sample rate the machine makes its sound at, which the page asks
//              the browser to play it at, so it is not resampled twice
//   transform  optional: called on each frame's samples, a Float32Array, before
//              they are posted, to change them in place
// It gives an object whose `machine` the page sets to the host once it runs:
// the host's Sound() hands the page a view of the samples, which play(view)
// posts, and its DropSound() throws away what was made while sound was off.

import { messageOf } from '/cartridge-picker.js';

export function soundOutput({ panel, button, line, module, processor, rate, transform }) {
  return {
    on: false,
    machine: null,
    context: null,
    node: null,
    async turnOn() {
      // Made inside the click, as browsers ask, at the machine's own rate.
      this.context ??= new AudioContext({ sampleRate: rate });
      if (!this.node) {
        await this.context.audioWorklet.addModule(module);
        this.node = new AudioWorkletNode(this.context, processor, { numberOfInputs: 0, outputChannelCount: [1] });
        this.node.connect(this.context.destination);
      }
      this.fresh();
      await this.context.resume();
      this.on = true;
    },
    async turnOff() {
      this.on = false;
      await this.context?.suspend();
    },
    // What was made while the sound was off, or the page hidden, is not played late.
    fresh() {
      this.machine.DropSound();
      this.node?.port.postMessage({ flush: true });
    },
    play(view) {
      const samples = new Float32Array(view.byteLength / 4);
      view.copyTo(new Uint8Array(samples.buffer));
      transform?.(samples);
      this.node.port.postMessage(samples, [samples.buffer]);
    },
    pause() {
      if (this.on) this.context.suspend();
    },
    resume() {
      if (!this.on) return;
      this.fresh();
      this.context.resume();
    },
    wire() {
      button.disabled = false;
      button.addEventListener('click', async () => {
        button.disabled = true;
        try {
          if (this.on) await this.turnOff();
          else await this.turnOn();
        } catch (error) {
          this.on = false;
          line.textContent = `Sound could not start, so it is off: ${messageOf(error)}`;
          panel.dataset.sound = 'failed';
          button.disabled = false;
          return;
        }
        button.textContent = this.on ? 'Turn sound off' : 'Turn sound on';
        button.setAttribute('aria-pressed', String(this.on));
        line.textContent = this.on ? 'Sound is on.' : 'Sound is off.';
        panel.dataset.sound = this.on ? 'on' : 'off';
        button.disabled = false;
      });
    },
  };
}
