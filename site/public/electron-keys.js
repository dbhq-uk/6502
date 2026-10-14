// The Electron's keys, and which key on a PC keyboard presses each one. Data
// only, so the page script (electron.js), the page's build and the site's tests
// all read the same table. The BBC Micro's page has its own (bbc-keys.js): the
// two keyboards share a shape and little else.
//
// ELECTRON_KEYS is the machine's own list, src/Dbhq.Machines.Electron/ElectronKey.cs:
// each key's place in the matrix, the column (0 to 13) plus sixteen times the
// bit (0 to 3), fact sheet docs/electron/facts/ula.md section 7b. The host's
// PressKey and ReleaseKey take that number. tests/electron.test.mjs checks this
// copy against the C# enum, so the two cannot drift.
//
// PC_KEYS maps a browser's KeyboardEvent.code, which names a key by where it is
// on the keyboard and not by what it types, to an Electron key. The rule is the
// BBC Micro page's: the key in the same place, so a typist's fingers land where
// they would on the machine, and the symbols are where the Electron has them:
// = is SHIFT and -, * is SHIFT and :, and the Electron's : is where a PC has '.
// Where the two keyboards differ in shape, a key goes to the PC key that does
// its job: DELETE on Backspace (and Delete), RETURN on Enter, COPY on End, the
// cursor keys on the arrows, ESCAPE on Esc, and CAPS LK on Caps Lock. The
// Electron has fewer keys than a PC, so every one of them is pressed by a PC key
// and none is left over (NOT_ON_A_PC is empty; the test still requires every key
// to be in one table or the other).
//
// CAPS LK and FUNC are one key on the Electron (ula.md s7b): pressed alone it
// turns CAPS LOCK on or off, and held with another key it makes that key a
// function key or a BASIC keyword. A PC's Caps Lock is a press and nothing more
// (some systems send its key up only at the next press), so from the PC keyboard
// it toggles CAPS LOCK; FUNC with another key is on the on-screen keys, where it
// latches like SHIFT and CTRL.
//
// BREAK is not a key in the matrix (it is the reset line, s7c), so it is not
// here: the page has a Break button, and the PC's Pause key presses it too, as on
// the BBC Micro's page.

export const ELECTRON_KEYS = {
  Right: 0x00, Copy: 0x10, Space: 0x30,
  Left: 0x01, Down: 0x11, Return: 0x21, Delete: 0x31,
  Minus: 0x02, Up: 0x12, Colon: 0x22,
  D0: 0x03, P: 0x13, Semicolon: 0x23, Slash: 0x33,
  D9: 0x04, O: 0x14, L: 0x24, FullStop: 0x34,
  D8: 0x05, I: 0x15, K: 0x25, Comma: 0x35,
  D7: 0x06, U: 0x16, J: 0x26, M: 0x36,
  D6: 0x07, Y: 0x17, H: 0x27, N: 0x37,
  D5: 0x08, T: 0x18, G: 0x28, B: 0x38,
  D4: 0x09, R: 0x19, F: 0x29, V: 0x39,
  D3: 0x0A, E: 0x1A, D: 0x2A, C: 0x3A,
  D2: 0x0B, W: 0x1B, S: 0x2B, X: 0x3B,
  D1: 0x0C, Q: 0x1C, A: 0x2C, Z: 0x3C,
  Escape: 0x0D, CapsLock: 0x1D, Ctrl: 0x2D, Shift: 0x3D,
};

const letters = Object.fromEntries([...'ABCDEFGHIJKLMNOPQRSTUVWXYZ'].map((c) => [`Key${c}`, c]));
const digits = Object.fromEntries([...'0123456789'].map((d) => [`Digit${d}`, `D${d}`]));

export const PC_KEYS = {
  ...letters,
  ...digits,
  Escape: 'Escape',
  Minus: 'Minus',
  Backspace: 'Delete',
  Delete: 'Delete',
  CapsLock: 'CapsLock',
  ControlLeft: 'Ctrl',
  ControlRight: 'Ctrl',
  Semicolon: 'Semicolon',
  Quote: 'Colon',
  Enter: 'Return',
  NumpadEnter: 'Return',
  ShiftLeft: 'Shift',
  ShiftRight: 'Shift',
  Comma: 'Comma',
  Period: 'FullStop',
  Slash: 'Slash',
  Space: 'Space',
  ArrowLeft: 'Left',
  ArrowRight: 'Right',
  ArrowUp: 'Up',
  ArrowDown: 'Down',
  End: 'Copy',
};

export const NOT_ON_A_PC = {};

// What is printed on each PC key the page uses for a symbol, for the page's
// table of where the symbols are. A UK keyboard prints some of them elsewhere;
// the key is the same either way, because it is the key's place that counts.
export const PC_LABELS = {
  ...Object.fromEntries(Object.entries(letters).map(([code, c]) => [code, c])),
  ...Object.fromEntries([...'0123456789'].map((d) => [`Digit${d}`, d])),
  Minus: '-', Semicolon: ';', Quote: "'", Comma: ',', Period: '.', Slash: '/', Space: 'Space',
};

/** The PC keys (KeyboardEvent.code) that press an Electron key, in the order PC_KEYS lists them. */
export const pcKeysFor = (name) => Object.keys(PC_KEYS).filter((code) => PC_KEYS[code] === name);

/**
 * The keys that type a character rather than move the cursor or copy: the four
 * cursor keys and COPY hold small codes in the OS's key table ($21 to $25) that
 * the key handler acts on rather than types, so they are left out of the table
 * of characters.
 */
const EDITING = ['Left', 'Right', 'Up', 'Down', 'Copy'];

/**
 * What each character the Electron types needs, from the OS ROM's own tables,
 * as ElectronKeyboardTests.TheSessionTypesWhatTheOsKeyTablesSay reads them: the
 * unshifted code of the key at column c, bit b is the byte at $EDD3 + 4c + b
 * (ula.md s7b), and the code with SHIFT is the byte at $EFB7 plus the unshifted
 * code. Letters are stored small and type capitals, CAPS LOCK being on from
 * power on (s7c). Column 13 (ESCAPE, CAPS LK, CTRL and SHIFT) types nothing of
 * its own. `os` is the 16 KB OS ROM, which starts at $C000. Returns a Map from
 * the character to { key, shift }; RETURN is '\r'.
 */
export function electronCharacters(os) {
  const characters = new Map();
  const add = (character, entry) => { if (!characters.has(character)) characters.set(character, entry); };
  for (const [key, number] of Object.entries(ELECTRON_KEYS)) {
    const column = number & 0x0f;
    const bit = number >> 4;
    if (column === 13 || EDITING.includes(key)) continue;
    const code = os[0xedd3 - 0xc000 + 4 * column + bit];
    if (code >= 0x61 && code <= 0x7a) add(String.fromCharCode(code - 0x20), { key, shift: false });
    else if (code === 0x0d || (code >= 0x20 && code <= 0x5f)) add(code === 0x0d ? '\r' : String.fromCharCode(code), { key, shift: false });
    else continue;
    const shifted = os[0xefb7 - 0xc000 + code];
    if (shifted >= 0x21 && shifted <= 0x7e && !(shifted >= 0x41 && shifted <= 0x5a)) add(String.fromCharCode(shifted), { key, shift: true });
  }
  return characters;
}

// ---- The keys on the page, for touch and for anyone ----
//
// LEGENDS is what the Electron prints on each key, so the page names a key the
// way the machine does: the cursor keys as arrows, and CAPS LK and FUNC as the
// one key they are.
export const LEGENDS = {
  ...Object.fromEntries([...'ABCDEFGHIJKLMNOPQRSTUVWXYZ'].map((c) => [c, c])),
  ...Object.fromEntries([...'0123456789'].map((d) => [`D${d}`, d])),
  Shift: 'SHIFT', Ctrl: 'CTRL', CapsLock: 'CAPS LK FUNC', Escape: 'ESCAPE',
  Return: 'RETURN', Delete: 'DELETE', Copy: 'COPY', Space: 'SPACE',
  Left: '←', Right: '→', Up: '↑', Down: '↓',
  Minus: '-', Semicolon: ';', Colon: ':', Comma: ',', FullStop: '.', Slash: '/',
};

/** The legend on an Electron key, by its name in ELECTRON_KEYS. */
export const legend = (key) => LEGENDS[key];

// A key's name in words, for a screen reader, where its legend is a symbol or
// two words that run together.
const SPOKEN = {
  Left: 'cursor left', Right: 'cursor right', Up: 'cursor up', Down: 'cursor down',
  Minus: 'minus', Semicolon: 'semicolon', Colon: 'colon', Comma: 'comma', FullStop: 'full stop', Slash: 'slash',
  CapsLock: 'caps lock and func',
};

/** What a screen reader should call a key: its legend, or the symbol in words. */
export const spokenName = (key) => SPOKEN[key] ?? LEGENDS[key];

/**
 * The on-screen keys, row by row, in the Electron's layout. The rows follow the
 * matrix: each column of ula.md s7b holds a key from each row in turn (column 12
 * is 1, Q, A and Z), so the column that holds -, the cursor-up key and : puts
 * cursor up after P and : after ;, and the columns of the cursor keys, COPY,
 * RETURN and DELETE close the rows. ESCAPE, CAPS LK and FUNC, CTRL and SHIFT,
 * column 13, start the four rows, and SPACE is the bar below. That the rows
 * run this way is read from the matrix, not from a photograph of the keyboard:
 * the photograph comes with the page's record, and is checked against it then.
 * Every key in ELECTRON_KEYS is here exactly once (a test holds it). BREAK is
 * not a key in the matrix; it is the page's Break button.
 */
export const ON_SCREEN_ROWS = [
  ['Escape', 'D1', 'D2', 'D3', 'D4', 'D5', 'D6', 'D7', 'D8', 'D9', 'D0', 'Minus', 'Left', 'Right'],
  ['CapsLock', 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', 'Up', 'Down', 'Copy'],
  ['Ctrl', 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', 'Semicolon', 'Colon', 'Return'],
  ['Shift', 'Z', 'X', 'C', 'V', 'B', 'N', 'M', 'Comma', 'FullStop', 'Slash', 'Delete'],
  ['Space'],
];

/**
 * SHIFT, CTRL and CAPS LK FUNC on screen latch: a tap holds the key for the
 * next key tapped, then lets go, so one finger can type SHIFT and 2, or FUNC
 * and a key. A second tap of SHIFT or CTRL lets go without typing; a second tap
 * of CAPS LK FUNC presses it alone, which is how the machine turns CAPS LOCK on
 * or off.
 */
export const STICKY = ['Shift', 'Ctrl', 'CapsLock'];
