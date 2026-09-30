// Two changes to every markdown file the site renders from the repository.
//
// 1. The first level-one heading is dropped. The page prints its own title from
//    the front matter, and a second heading would repeat it.
// 2. A link written for the repository (a relative path to another file) is
//    pointed at that file on GitHub. A path like ../superpowers/specs/x.md is
//    correct in the repository and a dead link on the site, and the site's
//    link check would rightly fail on it.
import path from 'node:path';

const REPO = 'https://github.com/dbhq-uk/6502/blob/main/';

export function rewriteHref(href, sourceDir) {
  if (!href || /^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('#') || href.startsWith('/')) return href;
  const [file, fragment] = href.split('#');
  const resolved = path.posix.normalize(path.posix.join(sourceDir, file));
  return REPO + resolved + (fragment ? '#' + fragment : '');
}

export function rehypeJournal() {
  return (tree, file) => {
    // file.path is absolute; the repository root is the folder above site/.
    const abs = (file.path ?? '').replaceAll('\\', '/');
    const marker = abs.lastIndexOf('/docs/');
    const sourceDir = marker >= 0 ? path.posix.dirname(abs.slice(marker + 1)) : 'docs';
    let droppedTitle = false;
    const visit = (node, parent, index) => {
      if (!droppedTitle && node.type === 'element' && node.tagName === 'h1' && parent) {
        droppedTitle = true;
        parent.children.splice(index, 1);
        return index - 1;
      }
      if (node.type === 'element' && node.tagName === 'a' && node.properties?.href) {
        node.properties.href = rewriteHref(String(node.properties.href), sourceDir);
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
