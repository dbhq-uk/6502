// The KIM-1 monitor ROM's pins, read from the one place they are written:
// tests/Dbhq.Cpu6502.TestSupport/Pins.cs, which the tests fetch them by. The
// build script and the site's tests read them here, so a pin changed there is
// changed everywhere and is never typed a second time.
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from './registry.mjs';

export const PINS_FILE = path.join(REPO_ROOT, 'tests', 'Dbhq.Cpu6502.TestSupport', 'Pins.cs');

/** The value of `public const string <name> = "...";` in Pins.cs. Throws when it is not there. */
export function pin(name, source = fs.readFileSync(PINS_FILE, 'utf8')) {
  const m = new RegExp(`public const string ${name} = "([^"]+)";`).exec(source);
  if (!m) throw new Error(`${path.relative(REPO_ROOT, PINS_FILE)} has no "${name}"`);
  return m[1];
}

/** The two monitor ROM files the KIM-1 page loads: their file names, pinned URLs and SHA-256 hashes. */
export function kim1Roms(source = fs.readFileSync(PINS_FILE, 'utf8')) {
  return ['002', '003'].map((chip) => ({
    chip: `6530-${chip}`,
    file: `6530-${chip}.bin`,
    url: pin(`Kim1Rom${chip}Url`, source),
    sha256: pin(`Kim1Rom${chip}Sha256`, source),
  }));
}
