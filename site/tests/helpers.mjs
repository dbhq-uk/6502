// Shared by the tests: the built site, read as files.
import fs from 'node:fs';
import path from 'node:path';

export const DIST = path.join(process.cwd(), 'dist');

/** Every built page as { url, file, html }, for example url "/status/". */
export function pages() {
  const out = [];
  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith('.html')) {
        const rel = path.relative(DIST, full).replaceAll('\\', '/');
        const url = rel === '404.html' ? '/404.html' : '/' + rel.replace(/index\.html$/, '');
        out.push({ url, file: full, html: fs.readFileSync(full, 'utf8') });
      }
    }
  };
  walk(DIST);
  return out;
}

/** The text a reader sees: no scripts, styles, code or tags. */
export function visibleText(html) {
  return html
    .replace(/<(script|style|pre|code)\b[\s\S]*?<\/\1>/g, ' ')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n)))
    .replace(/&middot;/g, '·')
    .replace(/\s+/g, ' ')
    .trim();
}

export const page = (url) => pages().find((p) => p.url === url);
