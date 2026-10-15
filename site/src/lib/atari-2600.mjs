// What the Atari 2600's page says that is the Atari 2600's own, kept out of the
// template so the tests can read the same values.
import { STICK_KEYS, SWITCH_KEYS, KEY_NAMES } from '../../public/atari-2600-keys.js';

/**
 * The parts of the Atari 2600 the emulation leaves out, each with the issue
 * that tracks it (the plan's Global Constraints; issues 106 to 108 and 124 to
 * 128, filed on 14 October 2026, one a part, titled "Atari 2600: <the part>").
 * In the order the registry's notes give them.
 */
export const NOT_MODELLED = [
  { part: 'SECAM, the third television colour system beside NTSC and PAL, and its console', issue: 106 },
  { part: 'the paddles', issue: 108 },
  { part: 'the keypads', issue: 124 },
  { part: 'the driving controller', issue: 125 },
  { part: 'other peripherals: the trak-ball, the light gun, AtariVox and SaveKey', issue: 127 },
  { part: 'the analogue picture, such as PAL\'s colour loss on frames with an odd number of lines', issue: 128 },
  { part: 'choosing the board from the image itself, not only from its size', issue: 126 },
  { part: 'the cartridge boards beyond the ones modelled, listed below', issue: 107 },
];

/**
 * The cartridge boards that are not modelled, one issue each (issues 131 to 155,
 * filed on 14 October 2026, titled "Atari 2600 board: <name>"), in the order the
 * design lists them.
 */
export const BOARDS = [
  ['E0', 131], ['3F', 132], ['E7', 133], ['DPC', 134], ['FE', 135], ['3E', 136], ['3E+', 137], ['F0', 138],
  ['UA', 139], ['EF', 140], ['BF', 141], ['DF', 142], ['SB', 143], ['CV', 144], ['0840', 145], ['X07', 146],
  ['MDM', 147], ['WD', 148], ['WF8', 149], ['FC', 150], ['JANE', 151], ['03E0', 152], ['0FA0', 153],
  ['AR, the Supercharger', 154], ['the ARM boards, DPC+, CDF and CDFJ', 155],
].map(([board, issue]) => ({ board, issue }));

/**
 * The other Atari 2600 issues the page links: the speed pass (the design plans
 * it after the machine counts), the 3D models (their own pull request), and the
 * suite's real-console PAL captures, which are not compared yet.
 */
export const ELSEWHERE = { speed: 130, models: 129, captures: 162 };

/**
 * What each control a try-it file may name is on the page: a joystick's input
 * or a console switch, by the names public/atari-2600-keys.js gives them. The
 * left fire button is joystick 1's and the right joystick 2's, as the left and
 * right controller ports are.
 */
export const CONTROL_INPUTS = {
  'Joystick': { stick: 0, inputs: ['Up', 'Down', 'Left', 'Right'] },
  'Left fire button': { stick: 0, inputs: ['Fire'] },
  'Right fire button': { stick: 1, inputs: ['Fire'] },
  'GAME RESET': { switch: 'Reset' },
  'GAME SELECT': { switch: 'Select' },
  'Colour switch': { switch: 'BlackAndWhite' },
  'Left difficulty switch': { switch: 'LeftDifficultyA' },
  'Right difficulty switch': { switch: 'RightDifficultyA' },
};

/** "a, b and c". */
const list = (names) => (names.length < 2 ? names.join('') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`);

/** The keys that work a control, from the map the page script uses: "Up arrow, Down arrow, Left arrow and Right arrow", "Space", "T". */
export function controlKeys(button) {
  const control = CONTROL_INPUTS[button];
  if (!control) throw new Error(`"${button}" is not one of the Atari 2600's controls`);
  const codes = control.switch
    ? Object.keys(SWITCH_KEYS).filter((code) => SWITCH_KEYS[code] === control.switch)
    : control.inputs.flatMap((input) => Object.keys(STICK_KEYS).filter((code) => STICK_KEYS[code].stick === control.stick && STICK_KEYS[code].input === input));
  return list(codes.map((code) => KEY_NAMES[code]));
}
