// The gamepads a console's page shares: the poll, read by index in the
// browser's list, the standard layout's buttons by index, and the line that
// says which gamepad is which. Moved out of nes.js, unchanged in what it does,
// when the Atari 2600 became the second console to need it (the Atari 2600
// plan's task 13). Each page's own table says which button does what.
//
// A pad the browser does not know the layout of is read the same way and may
// have its buttons elsewhere. Nothing is filtered: every button held is passed
// on, opposite directions together included.

/** The browser's gamepads now, by index, with a hole (null) where one was unplugged. */
export const connected = () => globalThis.navigator?.getGamepads?.() ?? [];

/**
 * The bits a gamepad holds: for each [index, name] of `map`, the button at
 * that index in the standard layout, if pressed, holds bits[name].
 */
export function gamepadBits(gamepad, map, bits) {
  let mask = 0;
  for (const [index, name] of Object.entries(map)) {
    if (gamepad?.buttons?.[index]?.pressed) mask |= bits[name];
  }
  return mask;
}

/**
 * The directions the left stick holds, read in the standard layout (axis 0
 * across, negative left; axis 1 down, negative up): a direction is held when
 * the stick is past `share` of the way to it.
 */
export function stickDirections(gamepad, share = 0.5) {
  const [x = 0, y = 0] = gamepad?.axes ?? [];
  const held = [];
  if (y < -share) held.push('Up');
  if (y > share) held.push('Down');
  if (x < -share) held.push('Left');
  if (x > share) held.push('Right');
  return held;
}

/**
 * Keeps `line` saying which gamepad is which: the first two in the browser's
 * list are `noun` 1 and `noun` 2 ("controller", "joystick").
 */
export function watchPads(line, noun) {
  const describe = () => {
    const found = [...connected()].slice(0, 2);
    const named = found.map((g, i) => (g ? `${noun} ${i + 1} is ${g.id}` : null)).filter(Boolean);
    line.textContent = named.length > 0
      ? `Gamepads: ${named.join('; ')}.`
      : `No gamepad yet: plug one in and press a button on it, and the first is ${noun} 1, the second ${noun} 2.`;
  };
  window.addEventListener('gamepadconnected', describe);
  window.addEventListener('gamepaddisconnected', describe);
  describe();
}
