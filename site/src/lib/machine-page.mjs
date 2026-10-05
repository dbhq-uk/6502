import fs from 'node:fs';
import path from 'node:path';
import { SITE_ROOT } from './registry.mjs';

// What a machine page with a Start button says about its download, and how it
// links an issue: the same for every machine that has one (the BBC Micro's and
// the Electron's), kept here so neither page types its own.

export const issueUrl = (n) => `https://github.com/dbhq-uk/6502/issues/${n}`;

/**
 * How many bytes a machine page's Start button downloads: every file the build
 * put in public/machines/<id>/, the WebAssembly and the ROMs, as built (the edge
 * may compress them on the way, so this is the most it can be). A folder named
 * in `skip`, at the top of the machine's folder, is not counted: the BBC Micro's
 * preset discs, which Start does not fetch. Null when the machine was not built
 * into this copy of the site, which the page says.
 */
export function downloadBytes(id, root = SITE_ROOT, skip = []) {
  const dir = path.join(root, 'public', 'machines', id);
  if (!fs.existsSync(path.join(dir, '_framework'))) return null;
  let total = 0;
  const walk = (d) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      if (d === dir && skip.includes(e.name)) continue;
      const full = path.join(d, e.name);
      if (e.isDirectory()) walk(full);
      else total += fs.statSync(full).size;
    }
  };
  walk(dir);
  return total;
}

/** Megabytes, one decimal place, the British way: 12.3. */
export const megabytes = (bytes) => (bytes / 1e6).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
