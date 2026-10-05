import { SITE_ROOT } from './registry.mjs';
import { electronRoms } from './pins.mjs';
import { readRom } from './machines.mjs';
import { downloadBytes as machineDownloadBytes, issueUrl, megabytes } from './machine-page.mjs';
import { PC_LABELS, electronCharacters, legend, pcKeysFor } from '../../public/electron-keys.js';

// What the Electron's page says that is the Electron's own, kept out of the
// template so the tests can read the same values. The BBC Micro's page keeps
// its own in bbc-micro.mjs; what both say the same way is in machine-page.mjs.

export { issueUrl, megabytes };

/**
 * The parts of the Electron the emulation leaves out, each with the issue that
 * tracks it (the design, "What is left out", and tape.md s6; issues 57 to 60,
 * one a part, titled "Electron: <the part>", filed on 5 October 2026).
 */
export const NOT_MODELLED = [
  { part: 'the Plus 1 and its cartridge slots', issue: 57 },
  { part: 'the Plus 3 and its disc drive', issue: 58 },
  { part: 'the joystick and printer ports', issue: 59 },
  { part: '300 baud and non-standard tapes', issue: 60 },
];

/**
 * How many bytes the Start button downloads: every file the build put in
 * public/machines/electron/, the WebAssembly and the two ROMs, as built. Null
 * when the machine was not built into this copy of the site, which the page
 * says.
 */
export const downloadBytes = (root = SITE_ROOT) => machineDownloadBytes('electron', root);

/**
 * How long a tape takes, in the Electron's own time, as the machine's tests
 * measured it: the twelfth task's round trip, a one-line program saved by the
 * OS's own SAVE and loaded back by its LOAD, timed by the machine's cycle
 * counter at 2 MHz from RETURN to the motor going off. A dated record, quoted
 * from the journal entry that ran it with its command, not a figure about the
 * project as it stands: tests/electron.test.mjs reads that entry and fails if
 * these are not its figures, or not its cycle counts over two million.
 */
export const TAPE_TIMES = {
  measured: '5 October 2026',
  journal: '/journal/2026-10-04-the-electron-fact-sheets/',
  saveCycles: 21_513_995,
  loadCycles: 11_116_233,
};

/** Seconds of the Electron's time for a count of its 2 MHz cycles, to two places, the British way: 10.76. */
export const machineSeconds = (cycles) => (cycles / 2_000_000).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * The page's table of where the symbols are: every character the Electron
 * types that is not a letter, a digit or a space, with the Electron key that
 * types it and the PC key in the same place, both with SHIFT where it needs it.
 * Read from the OS ROM's own key tables (electronCharacters), so it is the
 * machine's, not a list typed in.
 */
export function symbolTable(os = readRom(electronRoms().find((r) => r.rom === 'os'))) {
  const rows = [];
  for (const [character, { key, shift }] of electronCharacters(os)) {
    if (/^[A-Z0-9 \r]$/.test(character)) continue;
    const codes = pcKeysFor(key);
    const label = codes.length > 0 ? PC_LABELS[codes[0]] : null;
    rows.push({
      character,
      electron: `${shift ? 'SHIFT and ' : ''}${legend(key)}`,
      pc: label === null || label === undefined ? null : `${shift ? 'Shift and ' : ''}${label}`,
    });
  }
  return rows.sort((a, b) => a.character.charCodeAt(0) - b.character.charCodeAt(0));
}
