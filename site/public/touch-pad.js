// On-screen buttons that are held, which a console's page shares: the NES's
// controller and the Atari 2600's joystick and its Reset and Select switches.
// Moved out of nes.js, unchanged in what it does, when the Atari 2600 became
// the second console to need it (the Atari 2600 plan's task 13).

/**
 * Each of `buttons` is held while a finger, or the mouse, is on it, several at
 * once included, and onHeld(names) is called with the Set of the names held
 * (nameOf(button)) as they change. A button pressed from the keyboard (Enter or
 * Space on it) is held for a moment, long enough for a game to read it. Each
 * button is enabled.
 */
export function holdButtons(buttons, nameOf, onHeld) {
  const held = new Set();
  const send = () => onHeld(held);
  for (const b of buttons) {
    const name = nameOf(b);
    const release = () => {
      if (held.delete(name)) send();
    };
    b.disabled = false;
    b.addEventListener('pointerdown', (event) => {
      event.preventDefault();
      // Captured, so the button hears its finger lift even off its edge; a pointer the browser
      // no longer tracks cannot be, and the button is held all the same.
      try {
        b.setPointerCapture?.(event.pointerId);
      } catch {
        // Nothing to capture.
      }
      held.add(name);
      send();
    });
    b.addEventListener('pointerup', release);
    b.addEventListener('pointercancel', release);
    b.addEventListener('lostpointercapture', release);
    b.addEventListener('contextmenu', (event) => event.preventDefault());
    b.addEventListener('click', (event) => {
      // detail 0 is a click from the keyboard; a pointer's was handled above.
      if (event.detail !== 0) return;
      held.add(name);
      send();
      setTimeout(release, 150);
    });
  }
}
