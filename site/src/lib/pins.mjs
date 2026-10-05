// The machines' ROM pins, read from the one place they are written:
// tests/Dbhq.Cpu6502.TestSupport/Pins.cs, which the tests read them by. The
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

/** The two monitor ROM files the KIM-1 page loads: their file names, repository paths, GitHub links and SHA-256 hashes. */
export function kim1Roms(source = fs.readFileSync(PINS_FILE, 'utf8')) {
  return ['002', '003'].map((chip) => ({
    chip: `6530-${chip}`,
    file: `6530-${chip}.bin`,
    path: pin(`Kim1Rom${chip}Path`, source),
    url: `https://github.com/dbhq-uk/6502/blob/main/${pin(`Kim1Rom${chip}Path`, source)}`,
    sha256: pin(`Kim1Rom${chip}Sha256`, source),
  }));
}

/**
 * The BBC Micro's three 16 KB ROMs, in the order BbcHost.Load takes them: the
 * operating system, BASIC and the Disc Filing System. Each with its name in the
 * Pins.cs constants (`rom`), what it is (`name`, as roms/README.md says), the
 * file name the page loads it by (the name it has
 * in roms/bbc-micro/), its repository path, GitHub link and SHA-256.
 */
export function bbcRoms(source = fs.readFileSync(PINS_FILE, 'utf8')) {
  const names = { Os: 'MOS 1.20, the operating system', Basic: 'BBC BASIC 2', Dfs: 'DFS 1.20, the Disc Filing System' };
  return ['Os', 'Basic', 'Dfs'].map((rom) => {
    const relative = pin(`Bbc${rom}Path`, source);
    return {
      rom: rom.toLowerCase(),
      name: names[rom],
      file: path.posix.basename(relative),
      path: relative,
      url: `https://github.com/dbhq-uk/6502/blob/main/${relative}`,
      sha256: pin(`Bbc${rom}Sha256`, source),
    };
  });
}

/**
 * The Electron's two 16 KB ROMs, in the order ElectronHost.Load takes them: the
 * operating system, from roms/electron/, and BASIC, which is the BBC Micro's
 * file in roms/bbc-micro/, kept once (roms/README.md). The same fields as
 * bbcRoms; BASIC's pins are the BBC Micro's, as Pins.cs says.
 */
export function electronRoms(source = fs.readFileSync(PINS_FILE, 'utf8')) {
  return [
    { rom: 'os', name: 'OS 1.00, the operating system', paths: ['ElectronOsPath', 'ElectronOsSha256'] },
    { rom: 'basic', name: 'BBC BASIC 2, the BBC Micro\'s file', paths: ['BbcBasicPath', 'BbcBasicSha256'] },
  ].map(({ rom, name, paths: [at, hash] }) => {
    const relative = pin(at, source);
    return {
      rom,
      name,
      file: path.posix.basename(relative),
      path: relative,
      url: `https://github.com/dbhq-uk/6502/blob/main/${relative}`,
      sha256: pin(hash, source),
    };
  });
}
