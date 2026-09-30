// Two changes to every markdown file the site renders from the repository.
//
// 1. The first level-one heading is dropped. The page prints its own title from
//    the front matter, and a second heading would repeat it.
// 2. A link written for the repository (a relative path to another file) is
//    pointed at that file on GitHub. A path like ../superpowers/specs/x.md is
//    correct in the repository and a dead link on the site, and the site's
//    link check would rightly fail on it.
//    A link to another journal entry is the exception: that entry is a page on
//    this site, so it points at the page and keeps its fragment.
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from './registry.mjs';

const REPO = 'https://github.com/dbhq-uk/6502/blob/main/';

/** The ids of the journal entries the site builds: the same files the content collection's glob (2*.md) reads. */
export function journalEntryIds(dir = path.join(REPO_ROOT, 'docs', 'journal')) {
  return new Set(fs.readdirSync(dir).filter((f) => /^2.*\.md$/.test(f)).map((f) => f.replace(/\.md$/, '')));
}

/** `entries` is the set of built journal entry ids; a link to one of them becomes that page's address. */
export function rewriteHref(href, sourceDir, entries = new Set()) {
  if (!href || /^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('#') || href.startsWith('/')) return href;
  const [file, fragment] = href.split('#');
  const resolved = path.posix.normalize(path.posix.join(sourceDir, file));
  const suffix = fragment ? '#' + fragment : '';
  const entry = /^docs\/journal\/([^/]+)\.md$/.exec(resolved);
  if (entry && entries.has(entry[1])) return `/journal/${entry[1]}/${suffix}`;
  return REPO + resolved + suffix;
}

export function rehypeJournal() {
  return (tree, file) => {
    // file.path is absolute; the repository root is the folder above site/.
    const abs = (file.path ?? '').replaceAll('\\', '/');
    const marker = abs.lastIndexOf('/docs/');
    const sourceDir = marker >= 0 ? path.posix.dirname(abs.slice(marker + 1)) : 'docs';
    const entries = journalEntryIds();
    let droppedTitle = false;
    const visit = (node, parent, index) => {
      if (!droppedTitle && node.type === 'element' && node.tagName === 'h1' && parent) {
        droppedTitle = true;
        parent.children.splice(index, 1);
        return index - 1;
      }
      if (node.type === 'element' && node.tagName === 'a' && node.properties?.href) {
        node.properties.href = rewriteHref(String(node.properties.href), sourceDir, entries);
      }
      for (let i = 0; i < (node.children?.length ?? 0); i++) {
        const next = visit(node.children[i], node, i);
        if (typeof next === 'number') i = next;
      }
      return undefined;
    };
    visit(tree, null, 0);
  };
}
