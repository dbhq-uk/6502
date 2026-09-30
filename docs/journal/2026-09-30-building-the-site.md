---
title: "Building the site"
date: 2026-09-30
summary: "The Astro site for 6502.dbhq.uk is built one task at a time, starting with a registry of machines that a test keeps in step with the family document."
order: 5
---

# 30 September 2026: building the site

## Task 1: The skeleton and the registry

The site is an Astro project in `site/`. This task laid the skeleton and the machine registry that later pages read.

### What was built

- **The Astro project.** `site/package.json`, `astro.config.mjs` (`trailingSlash: 'always'`, stylesheets inlined, the sitemap integration) and `tsconfig.json`. `npm install astro@^7.0.6 @astrojs/sitemap@^3.7.3` resolved to Astro 7.3.5 and `@astrojs/sitemap` 3.7.4 (read from `site/package.json` after the install).
- **The registry.** `machines/registry.json` lists every machine and chip in `docs/the-6502-family.md`, with a status, a core variant and, for a running machine, the name of the acceptance test that proves it. It is the source of truth from now on.
- **`site/src/lib/registry.mjs`.** Loads the registry, validates it and counts machines by status. A machine is out of scope exactly when no core variant runs it. A machine may only be marked `running` if its acceptance suite is in the test results and passes, with nothing failed or skipped.
- **A one-off seed script,** `site/scripts/seed-registry.mjs`, which built the registry from the family document's tables. It refuses to run again once the registry exists, because a re-run would overwrite the hand-kept fields.

### Decisions

- **The registry is generated once, then hand-kept, rather than parsed from the family document on every build.** Status and acceptance are facts the document does not carry. The cost is two places to edit, so a test reads the document and fails when the two name different machines or chips.
- **The repository root comes from the working directory, not the module's location.** The build bundles pages into `dist/`, so a path taken from a module's own location would point into the bundle.

### What the tests showed

`node --test tests/*.test.mjs` in `site/`:

- Before `registry.mjs` existed, the run failed with `ERR_MODULE_NOT_FOUND` for `site/src/lib/registry.mjs`: 0 passed, 1 failed (the file could not load).
- After it and the seed script were written, it ran 7 tests and all 7 passed.
- `node site/scripts/seed-registry.mjs` printed `56 machines and 30 chips written`. A count by status of the resulting file gave 51 planned and 5 out of scope, and none running or in progress.

### What surprised

- `npm run build` with no pages exits cleanly: `0 page(s) built`, with a warning from the sitemap integration that no pages were found. So the `pretest` build step passes on an empty site rather than failing.
