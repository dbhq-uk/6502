import { SITE_ROOT } from './registry.mjs';
import { bbcRoms } from './pins.mjs';
import { machineDownloadBytes, readRom } from './machines.mjs';
import { DISCS_FOLDER } from './bbc-discs.mjs';
import { PC_LABELS, bbcCharacters, legend, pcKeysFor } from '../../public/bbc-keys.js';

// What the BBC Micro's page says that is the BBC Micro's own, kept out of the
// template so the tests can read the same values.

/**
 * The parts of the Model B the emulation leaves out, each with the issue that
 * tracks it (the design, "What is left out"; issues 33 to 38, one a part, titled
 * "BBC Micro: <the part>").
 */
export const NOT_MODELLED = [
  { part: 'the 6850 serial port', issue: 33 },
  { part: 'the analogue-to-digital converter and joystick port', issue: 34 },
  { part: 'the Tube and the second processor', issue: 35 },
  { part: 'the cassette interface', issue: 36 },
  { part: 'the printer port', issue: 37 },
  { part: 'the 1 MHz bus', issue: 38 },
];

export const issueUrl = (n) => `https://github.com/dbhq-uk/6502/issues/${n}`;

/**
 * How many bytes the Start button downloads: every file the build put in
 * public/machines/bbc-micro/, the WebAssembly and the ROMs, as built. The
 * preset discs in discs/ are not counted: Start fetches none of them, and each
 * is fetched only when the visitor inserts it. Null when the machine was not
 * built into this copy of the site, which the page says. The rule is every
 * machine's, machineDownloadBytes in src/lib/machines.mjs.
 */
export const downloadBytes = (root = SITE_ROOT) => machineDownloadBytes('bbc-micro', root, [DISCS_FOLDER]);

/** Megabytes, one decimal place, the British way: 12.3. */
export const megabytes = (bytes) => (bytes / 1e6).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });


/**
 * The page's table of where the symbols are: every character the BBC types
 * that is not a letter, a digit or a space, with the BBC key that types it and
 * the PC key in the same place, both with SHIFT where it needs it. Read from the
 * OS ROM's own key table (bbcCharacters), so it is the machine's, not a list
 * typed in. A symbol whose key has no PC key here says so.
 */
export function symbolTable(os = readRom(bbcRoms().find((r) => r.rom === 'os'))) {
  const rows = [];
  for (const [character, { key, shift }] of bbcCharacters(os)) {
    if (/^[A-Z0-9 \r]$/.test(character)) continue;
    const codes = pcKeysFor(key);
    const label = codes.length > 0 ? PC_LABELS[codes[0]] : null;
    rows.push({
      character,
      bbc: `${shift ? 'SHIFT and ' : ''}${legend(key)}`,
      pc: label === null || label === undefined ? null : `${shift ? 'Shift and ' : ''}${label}`,
    });
  }
  return rows.sort((a, b) => a.character.charCodeAt(0) - b.character.charCodeAt(0));
}
