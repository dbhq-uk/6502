// The NES's buttons, and which keys and gamepad buttons press them. Data only,
// so the page script (nes.js), the panel's markup (NesPanel.astro, which says
// which key is which) and the site's tests all read the same table.
//
// BUTTONS is the machine's own bit order, src/Dbhq.Machines.Nes/Controller.cs:
// bit 0 A, 1 B, 2 Select, 3 Start, 4 Up, 5 Down, 6 Left, 7 Right, the mask
// NesHost.SetButtons takes. tests/nes-panel.test.mjs checks this copy against
// the C#, so the two cannot drift.
//
// KEYS maps a browser's KeyboardEvent.code, which names a key by where it is on
// the keyboard and not by what it types, to a button of controller 1: the arrow
// keys for the d-pad, Z for B and X for A (B left of A, as on the pad), Enter
// for Start and the right Shift for Select. Tab, Escape and the function keys
// are not here: they are the browser's and the keyboard user's.
//
// GAMEPAD_BUTTONS maps a gamepad's buttons in the browser's standard layout, by
// index: the bottom face button (0) is A and the left one (2) is B, so B sits
// left of A as on the NES pad; Select and Start are the two middle buttons (8
// and 9); the d-pad is 12 to 15.

export const BUTTONS = { A: 0x01, B: 0x02, Select: 0x04, Start: 0x08, Up: 0x10, Down: 0x20, Left: 0x40, Right: 0x80 };

export const KEYS = {
  ArrowUp: 'Up',
  ArrowDown: 'Down',
  ArrowLeft: 'Left',
  ArrowRight: 'Right',
  KeyZ: 'B',
  KeyX: 'A',
  Enter: 'Start',
  ShiftRight: 'Select',
};

/** What each key in KEYS is called on the page. */
export const KEY_NAMES = {
  ArrowUp: 'Up arrow',
  ArrowDown: 'Down arrow',
  ArrowLeft: 'Left arrow',
  ArrowRight: 'Right arrow',
  KeyZ: 'Z',
  KeyX: 'X',
  Enter: 'Enter',
  ShiftRight: 'right Shift',
};

export const GAMEPAD_BUTTONS = { 0: 'A', 2: 'B', 8: 'Select', 9: 'Start', 12: 'Up', 13: 'Down', 14: 'Left', 15: 'Right' };
