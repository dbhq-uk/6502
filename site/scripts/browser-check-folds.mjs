// A link into a closed fold opens it (scripts/browser-check.mjs calls this for
// each machine's page, in the same browser, watched the same way).
//
// On /machines/<id>/ the reference sits in closed <details> and public/fold-open.js
// opens the one a link points at. For each fold named, in a fresh page: the page
// is opened at the fragment and the fold must be open when it has loaded; then
// the fragment is changed to the next fold and that must open; then the page is
// opened with no fragment, the visible link to the fold is clicked, as a visitor
// does, and the fold must open and be on the screen. A fold that is open after
// the load with no fragment fails too: the page must not start open.

/** Checks that each fold in `folds` opens for a fragment, for a changed fragment and for a click on a link to it. */
export async function checkFoldLinks({ browser, watch, problems, origin, machine, folds, links = {} }) {
  const label = `${machine}: `;
  const page = await browser.newPage();
  await watch(page, label);
  const isOpen = (id) => page.evaluate((x) => document.getElementById(x)?.open === true, id);
  const exists = (id) => page.evaluate((x) => document.getElementById(x)?.localName === 'details', id);
  const settle = (id) => page.waitForFunction((x) => document.getElementById(x)?.open === true, id, { timeout: 5000 }).then(() => true, () => false);
  for (const id of folds) {
    await page.goto(`${origin}/machines/${machine}/`);
    if (!(await exists(id))) { problems.push(`${label}there is no fold #${id}`); continue; }
    if (await isOpen(id)) problems.push(`${label}the fold #${id} is open when the page loads with no fragment`);
    await page.goto(`${origin}/machines/${machine}/#${id}`);
    await page.reload();
    if (!(await settle(id))) problems.push(`${label}the page opened at #${id} has the fold shut`);
  }
  // A changed fragment, on a page already loaded.
  await page.goto(`${origin}/machines/${machine}/`);
  for (const id of folds) {
    await page.evaluate((x) => { location.hash = `#${x}`; }, id);
    if (!(await settle(id))) problems.push(`${label}changing the fragment to #${id} left the fold shut`);
  }
  // A click on the link a visitor sees.
  for (const [id, selector] of Object.entries(links)) {
    await page.goto(`${origin}/machines/${machine}/`);
    await page.locator(selector).first().click();
    if (!(await settle(id))) problems.push(`${label}clicking ${selector} left the fold #${id} shut`);
    // The page may scroll smoothly, so give it a moment to arrive.
    const inView = await page.waitForFunction((x) => { const r = document.getElementById(x).getBoundingClientRect(); return r.top < innerHeight && r.bottom > 0; }, id, { timeout: 5000 }).then(() => true, () => false);
    if (!inView) problems.push(`${label}clicking ${selector} opened #${id} but left it off the screen`);
  }
  console.log(`${label}folds ${folds.join(', ')} open for a fragment, a changed fragment${Object.keys(links).length ? ' and a click' : ''}`);
  await page.close();
}
