// The BBC Micro's keys, and which key on a PC keyboard presses each one. Data
// only, so the page script (bbc-micro.js), the page's build and the site's tests
// all read the same table.
//
// BBC_KEYS is the machine's own list, src/Dbhq.Machines.BbcMicro/BbcKey.cs:
// each key's internal number, column plus sixteen times row (fact sheet
// docs/bbc-micro/facts/via.md section 3(b)). The host's KeyDown and KeyUp take
// that number. tests/bbc-page.test.mjs checks this copy against the C# enum, so
// the two cannot drift.
//
// PC_KEYS maps a browser's KeyboardEvent.code, which names a key by where it
// is on the keyboard and not by what it types, to a BBC key. The rule is the
// key in the same place, so a typist's fingers land where they would on the
// machine, and the symbols are where the BBC has them, not where the PC does:
// = is SHIFT and -, * is SHIFT and :, and the BBC's : is where a PC has '.
// Where the two keyboards differ in shape, a key goes to the PC key that does
// its job: DELETE on Backspace (and Delete), RETURN on Enter, COPY on End, the
// cursor keys on the arrows, and ESCAPE on Esc. Where the BBC has more keys
// than the row has room for, the spare BBC key goes to a PC key the row does
// not use: _ on the key left of 1 (Backquote). ] is on the key at the right-hand
// end of the A row, which the browser calls Backslash (# on a UK keyboard, \ on
// a US one), and \ on the extra key left of Z that UK keyboards have and US
// ones do not.
//
// NOT_ON_A_PC lists every BBC key no PC key presses here, and why. The test
// requires every key in BBC_KEYS to be in one table or the other.
//
// BREAK is not a key in the matrix (it is the reset line), so it is not here:
// the page has a Break button, and the PC's Pause key presses it too.

export const BBC_KEYS = {
  Shift: 0x00, Ctrl: 0x01,
  Q: 0x10, D3: 0x11, D4: 0x12, D5: 0x13, F4: 0x14, D8: 0x15, F7: 0x16, Minus: 0x17, Caret: 0x18, Left: 0x19,
  F0: 0x20, W: 0x21, E: 0x22, T: 0x23, D7: 0x24, I: 0x25, D9: 0x26, D0: 0x27, Underscore: 0x28, Down: 0x29,
  D1: 0x30, D2: 0x31, D: 0x32, R: 0x33, D6: 0x34, U: 0x35, O: 0x36, P: 0x37, LeftBracket: 0x38, Up: 0x39,
  CapsLock: 0x40, A: 0x41, X: 0x42, F: 0x43, Y: 0x44, J: 0x45, K: 0x46, At: 0x47, Colon: 0x48, Return: 0x49,
  ShiftLock: 0x50, S: 0x51, C: 0x52, G: 0x53, H: 0x54, N: 0x55, L: 0x56, Semicolon: 0x57, RightBracket: 0x58, Delete: 0x59,
  Tab: 0x60, Z: 0x61, Space: 0x62, V: 0x63, B: 0x64, M: 0x65, Comma: 0x66, FullStop: 0x67, Slash: 0x68, Copy: 0x69,
  Escape: 0x70, F1: 0x71, F2: 0x72, F3: 0x73, F5: 0x74, F6: 0x75, F8: 0x76, F9: 0x77, Backslash: 0x78, Right: 0x79,
};

const letters = Object.fromEntries([...'ABCDEFGHIJKLMNOPQRSTUVWXYZ'].map((c) => [`Key${c}`, c]));
const digits = Object.fromEntries([...'0123456789'].map((d) => [`Digit${d}`, `D${d}`]));

export const PC_KEYS = {
  ...letters,
  ...digits,
  Escape: 'Escape',
  Backquote: 'Underscore',
  Minus: 'Minus',
  Equal: 'Caret',
  Backspace: 'Delete',
  Delete: 'Delete',
  BracketLeft: 'At',
  BracketRight: 'LeftBracket',
  Backslash: 'RightBracket',
  IntlBackslash: 'Backslash',
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

const functionKeys = 'the browser keeps its function keys for itself (F5 reloads the page), so the page leaves them alone';
export const NOT_ON_A_PC = {
  Tab: 'Tab moves on from the machine, so the page never keeps hold of the keyboard',
  ShiftLock: 'no key is left over for it on a PC keyboard',
  ...Object.fromEntries(['F0', 'F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7', 'F8', 'F9'].map((f) => [f, functionKeys])),
};

// What is printed on each PC key the page uses for a symbol, for the page's
// table of where the symbols are. A UK keyboard prints some of them elsewhere;
// the key is the same either way, because it is the key's place that counts.
export const PC_LABELS = {
  ...Object.fromEntries(Object.entries(letters).map(([code, c]) => [code, c])),
  ...Object.fromEntries([...'0123456789'].map((d) => [`Digit${d}`, d])),
  Backquote: '` (left of 1)', Minus: '-', Equal: '=', BracketLeft: '[', BracketRight: ']',
  Backslash: '# or \\ (beside Enter)', IntlBackslash: '\\ (left of Z)',
  Semicolon: ';', Quote: "'", Comma: ',', Period: '.', Slash: '/', Space: 'Space',
};

/** The BBC key's name for an internal number, or undefined. */
export const bbcKeyName = (number) => Object.keys(BBC_KEYS).find((k) => BBC_KEYS[k] === number);

/** The PC keys (KeyboardEvent.code) that press a BBC key, in the order PC_KEYS lists them. */
export const pcKeysFor = (name) => Object.keys(PC_KEYS).filter((code) => PC_KEYS[code] === name);

/**
 * What each character the BBC types needs, from the OS ROM's own key table, as
 * BbcSession.Keys in the tests makes it: for each key in rows 1 to 7, the byte
 * at $F03B + 16 (row - 1) + column of the OS is what it types unshifted
 * (via.md section 3(b)). Letters are stored small and type capitals, CAPS LOCK
 * being on from power on. SHIFT on a code from $21 to $3F, except 0, flips bit
 * 4, so " is SHIFT and 2. `os` is the 16 KB OS ROM, which starts at $C000.
 * Returns a Map from the character to { key, shift }.
 */
export function bbcCharacters(os) {
  const characters = new Map();
  for (const [key, number] of Object.entries(BBC_KEYS)) {
    const row = number >> 4;
    const column = number & 0x0f;
    if (row === 0) continue;
    const code = os[0x303b + 16 * (row - 1) + column];
    if (code >= 0x61 && code <= 0x7a) characters.set(String.fromCharCode(code - 0x20), { key, shift: false });
    else if (code === 0x0d || (code >= 0x20 && code <= 0x5f)) {
      characters.set(code === 0x0d ? '\r' : String.fromCharCode(code), { key, shift: false });
      if (code >= 0x21 && code <= 0x3f && code !== 0x30) characters.set(String.fromCharCode(code ^ 0x10), { key, shift: true });
    }
  }
  return characters;
}

// ---- The keys on the page, for touch and for anyone ----
//
// LEGENDS is what the BBC Micro's own keyboard prints on each key (the names in
// via.md section 3(b): SHIFT LOCK, f0, the cursor keys as arrows), so the page
// names a key the way the machine does.
export const LEGENDS = {
  ...Object.fromEntries([...'ABCDEFGHIJKLMNOPQRSTUVWXYZ'].map((c) => [c, c])),
  ...Object.fromEntries([...'0123456789'].map((d) => [`D${d}`, d])),
  ...Object.fromEntries([...'0123456789'].map((d) => [`F${d}`, `f${d}`])),
  Shift: 'SHIFT', Ctrl: 'CTRL', CapsLock: 'CAPS LOCK', ShiftLock: 'SHIFT LOCK', Tab: 'TAB', Escape: 'ESCAPE',
  Return: 'RETURN', Delete: 'DELETE', Copy: 'COPY', Space: 'SPACE',
  Left: '←', Right: '→', Up: '↑', Down: '↓',
  Minus: '-', Caret: '^', Backslash: '\\', At: '@', LeftBracket: '[', Underscore: '_',
  Semicolon: ';', Colon: ':', RightBracket: ']', Comma: ',', FullStop: '.', Slash: '/',
};

/** The legend on a BBC key, by its name in BBC_KEYS. */
export const legend = (key) => LEGENDS[key];

// A key's name in words, for a screen reader, where its legend is a symbol.
const SPOKEN = {
  Left: 'cursor left', Right: 'cursor right', Up: 'cursor up', Down: 'cursor down',
  Minus: 'minus', Caret: 'caret', Backslash: 'backslash', At: 'at', LeftBracket: 'left bracket', Underscore: 'underscore',
  Semicolon: 'semicolon', Colon: 'colon', RightBracket: 'right bracket', Comma: 'comma', FullStop: 'full stop', Slash: 'slash',
};

/** What a screen reader should call a key: its legend, or the symbol in words. */
export const spokenName = (key) => SPOKEN[key] ?? LEGENDS[key];

/**
 * The on-screen keys, in the machine's own layout, row by row: the red
 * function keys, then the four rows of the main keyboard and the space bar,
 * then the block of cursor keys with COPY and DELETE that sits at the right on
 * the machine. Every key in BBC_KEYS is here exactly once (a test holds it).
 * BREAK is not a key in the matrix; it is the page's Break button.
 */
export const ON_SCREEN_ROWS = [
  ['F0', 'F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7', 'F8', 'F9'],
  ['Escape', 'D1', 'D2', 'D3', 'D4', 'D5', 'D6', 'D7', 'D8', 'D9', 'D0', 'Minus', 'Caret', 'Backslash'],
  ['Tab', 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', 'At', 'LeftBracket', 'Underscore'],
  ['CapsLock', 'Ctrl', 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', 'Semicolon', 'Colon', 'RightBracket', 'Return'],
  ['ShiftLock', 'Shift', 'Z', 'X', 'C', 'V', 'B', 'N', 'M', 'Comma', 'FullStop', 'Slash'],
  ['Space'],
  ['Left', 'Up', 'Down', 'Right', 'Copy', 'Delete'],
];

/**
 * SHIFT and CTRL on screen latch: a tap holds the key for the next key tapped,
 * then lets go, so one finger can type SHIFT and 2. A second tap lets go.
 */
export const STICKY = ['Shift', 'Ctrl'];
