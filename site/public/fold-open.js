// Opens a closed <details> that a link points at.
//
// A machine page keeps its reference in closed <details> (src/components/Fold.astro).
// A link such as "#rom" lands on the closed fold, because the browser does not
// open one for a fragment. This opens the fold the fragment names, or every
// fold round the element it names, when the page loads and when the fragment
// changes, then scrolls to it, because the browser scrolled while it was shut.
// It is an external module because the site's CSP has no 'unsafe-inline' in
// script-src. Without JavaScript the folds stay closed and a visitor opens them.

/** Opens the folds the fragment leads into. True when it opened any. */
export function openFoldsFor(hash, doc) {
  let id;
  try { id = decodeURIComponent(hash.replace(/^#/, "")); } catch { return false; }
  const target = id ? doc.getElementById(id) : null;
  if (!target) return false;
  let opened = false;
  for (let fold = target.closest("details"); fold; fold = fold.parentElement?.closest("details") ?? null) {
    if (!fold.open) { fold.open = true; opened = true; }
  }
  if (opened) target.scrollIntoView();
  return opened;
}

if (typeof document !== "undefined") {
  const run = () => openFoldsFor(location.hash, document);
  run();
  addEventListener("hashchange", run);
}
