// Which console a model shows when the page names a region, and the page's
// words for it: one place for both NES models and for models.mjs. A browser
// module with no Node imports, so the models' bundles can import it (models.mjs
// cannot be, as it pulls in the notes, which are built into the page only).

/**
 * The console a model draws when the page's region is `region` (lower case):
 * the region's own if it is one of `regions`; otherwise the first, and a
 * sentence saying there is no model of the page's console, and why (Review
 * Focus 5 of the NES models plan). `why` defaults to none having been built.
 */
export function shownRegion(regions, region, why = 'none was built') {
  if (regions.includes(region)) return { region, missing: null };
  const first = regions[0];
  return { region: first, missing: `There is no model of the ${String(region).toUpperCase()} console, because ${why}, so this is the ${first.toUpperCase()} console's.` };
}

/**
 * The same for a model's entry in MODELS: its console, its accessible name for
 * that console, and the sentence. A model with no `regions` draws one console
 * whatever the page says. An entry may say why a console is missing in
 * `missing[region]`.
 */
export function shownFor(entry, region) {
  if (!entry.regions) return { region: null, label: entry.label, missing: null };
  const s = shownRegion(entry.regions, region, entry.missing?.[region]);
  return { region: s.region, label: entry.label[s.region], missing: s.missing };
}

/**
 * A model's page words follow the page's region: on the model's root, the
 * caption, the note and anything else marked with data-model-region (but the
 * accessible names, which stay hidden) show for the console drawn, and the
 * stage takes that console's name and caption. A region the model does not
 * draw keeps its first console, and the status line says so. Returns the
 * console now drawn; `current` is the one drawn before, null at mount. A
 * region that is not a word (no detail on an event) changes nothing.
 */
export function followRegion(root, regions, next, current) {
  if (typeof next !== 'string') return current;
  const { region, missing } = shownRegion(regions, next);
  if (missing) {
    root.querySelector('[data-model-status]').textContent = missing;
    root.dataset.modelMissing = next;
  } else delete root.dataset.modelMissing;
  if (region === current) return current;
  const stage = root.querySelector('[data-model-stage]');
  root.dataset.modelRegion = region;
  for (const el of root.querySelectorAll('[data-model-region]')) if (!el.hasAttribute('data-model-label')) el.hidden = el.dataset.modelRegion !== region;
  const name = root.querySelector(`[data-model-label][data-model-region="${region}"]`)?.textContent;
  if (name) stage.setAttribute('aria-label', name);
  // The caption's id carries the view's suffix when the model is one of a case's views (model-about-inside-pal).
  const about = root.querySelector(`.model-about[data-model-region="${region}"]`);
  if (about) stage.setAttribute('aria-describedby', about.id);
  return region;
}
