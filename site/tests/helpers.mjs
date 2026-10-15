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
  return at < 0 ? '' : html.slice(at, html.indexOf('<section class="machine-more"', at));
}

/**
 * One part of a machine page, by its id: a section, from its
 * aria-labelledby="<id>" to the section's end, or a part of the reference at
 * the foot of the page, a <details id="<id>"> (src/components/Fold.astro), to
 * the end of the details. '' when the page has no such part.
 */
export function pagePart(html, id) {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  if (at >= 0) return html.slice(at, html.indexOf('</section>', at));
  const fold = html.search(new RegExp(`<details\\b[^>]*\\bid="${id}"`));
  return fold < 0 ? '' : html.slice(fold, html.indexOf('</details>', fold) + '</details>'.length);
}

/** A reference part's label, the text of its <summary>, and whether it is open when the page loads: '' and false when it is not a <details>. */
export function foldOf(part) {
  const tag = /^<details\b[^>]*>/.exec(part)?.[0];
  if (!tag) return { summary: '', open: false };
  return { summary: visibleText(/<summary\b[^>]*>([\s\S]*?)<\/summary>/.exec(part)?.[1] ?? ''), open: /\sopen(?:[\s=>])/.test(tag) };
}
