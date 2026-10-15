// The Atari 2600's page tables: the inputs' bits, which keys and gamepad
// buttons work them, and each region's picture window. Data only, so the page
// script (atari-2600.js), the panel's markup (Atari2600Panel.astro, which says
// which key is which and sizes the canvas) and the site's tests all read the
// same tables. tests/atari-2600-panel.test.mjs checks each copy of a machine
// figure here against the C# it comes from, so the two cannot drift.
//
// STICK is the machine's Joystick, src/Dbhq.Machines.Atari2600/Controllers.cs:
// Up 1, Down 2, Left 4, Right 8, Fire 16, the state Atari2600Host.SetJoystick
// takes. SWITCHES is its ConsoleSwitches: Reset 1, Select 2, BlackAndWhite 4,
// LeftDifficultyA 8, RightDifficultyA 16, the state SetSwitches takes. No
// switch flag set is the console at rest: colour, both difficulties at B.
//
// STICK_KEYS maps a browser's KeyboardEvent.code, which names a key by where
// it is on the keyboard and not by what it types, to a joystick's input:
// joystick 1 on the arrow keys with Space to fire, joystick 2 on W, A, S and D
// with F to fire. SWITCH_KEYS maps the console switches: R for Reset and T for
// Select, held while the key is down as the console's spring back; C for the
// colour switch, 1 and 2 for the left and right difficulty, which each press
// flips and which stay where they are put. Tab, Escape and the function keys
// are not here: they are the browser's and the keyboard user's.
//
// GAMEPAD_STICK maps a gamepad's buttons in the browser's standard layout, by
// index, to its joystick: the bottom face button (0) fires and the d-pad (12
// to 15) moves; the left stick moves it too, past half way (gamepads.js).
// GAMEPAD_SWITCHES: the two middle buttons, 8 for Select and 9 for Reset, the
// places of Select and Start on most pads.
//
// WINDOWS is each region's picture window and pixel shape, from
// docs/atari-2600/facts/region.md sections 3 and 4 as Region.cs holds them:
// the first line shown and the lines shown, the pixel's width to its height,
// and the lines a frame of that region's games has, which the PAL suggestion
// compares with.

export const STICK = { Up: 1, Down: 2, Left: 4, Right: 8, Fire: 16 };

export const SWITCHES = { Reset: 1, Select: 2, BlackAndWhite: 4, LeftDifficultyA: 8, RightDifficultyA: 16 };

/** The switches that spring back: held only while pressed. The others stay where they are put. */
export const HELD_SWITCHES = ['Reset', 'Select'];

export const STICK_KEYS = {
  ArrowUp: { stick: 0, input: 'Up' },
  ArrowDown: { stick: 0, input: 'Down' },
  ArrowLeft: { stick: 0, input: 'Left' },
  ArrowRight: { stick: 0, input: 'Right' },
  Space: { stick: 0, input: 'Fire' },
  KeyW: { stick: 1, input: 'Up' },
  KeyS: { stick: 1, input: 'Down' },
  KeyA: { stick: 1, input: 'Left' },
  KeyD: { stick: 1, input: 'Right' },
  KeyF: { stick: 1, input: 'Fire' },
};

export const SWITCH_KEYS = {
  KeyR: 'Reset',
  KeyT: 'Select',
  KeyC: 'BlackAndWhite',
  Digit1: 'LeftDifficultyA',
  Digit2: 'RightDifficultyA',
};

/** What each key in STICK_KEYS and SWITCH_KEYS is called on the page. */
export const KEY_NAMES = {
  ArrowUp: 'Up arrow',
  ArrowDown: 'Down arrow',
  ArrowLeft: 'Left arrow',
  ArrowRight: 'Right arrow',
  Space: 'Space',
  KeyW: 'W',
  KeyS: 'S',
  KeyA: 'A',
  KeyD: 'D',
  KeyF: 'F',
  KeyR: 'R',
  KeyT: 'T',
  KeyC: 'C',
  Digit1: '1',
  Digit2: '2',
};

export const GAMEPAD_STICK = { 0: 'Fire', 12: 'Up', 13: 'Down', 14: 'Left', 15: 'Right' };

export const GAMEPAD_SWITCHES = { 8: 'Select', 9: 'Reset' };

/** Pixels in a line, FrameBuffer.Width. */
export const WIDTH = 160;

export const WINDOWS = {
  NTSC: { first: 19, lines: 228, pixel: [12, 7], nominal: 262 },
  PAL: { first: 27, lines: 264, pixel: [52, 25], nominal: 312 },
};
