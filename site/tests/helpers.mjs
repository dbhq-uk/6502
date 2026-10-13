// Shared by the tests: the built site, read as files.
import fs from 'node:fs';
import path from 'node:path';

// The built site.
export const DIST = path.resolve(process.cwd(), 'dist');

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

/**
 * One model's part of a machine page, by its module: the whole model section
 * for a machine with one model, or that view's tab panel for a machine with a
 * case (src/components/MachineModel.astro). '' when the page has no such model.
 */
export function modelPanel(html, module) {
  const tab = html.search(new RegExp(`<div class="model-panel" role="tabpanel"[^>]*\\bdata-model="${module}"`));
  if (tab >= 0) {
    const rest = html.slice(tab + 1);
    const end = Math.min(...[rest.search(/<div class="model-panel" role="tabpanel"/), rest.indexOf('<p class="model-help" id="model-help">')].filter((i) => i >= 0));
    return html.slice(tab, tab + 1 + end);
  }
  const at = html.search(new RegExp(`<section class="model"[^>]*\\bdata-model="${module}"`));
  return at < 0 ? '' : html.slice(at, html.indexOf('<section class="photos"', at));
}
