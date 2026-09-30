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
- `node site/scripts/seed-registry.mjs` printed `56 machines and 30 chips written`. `jq -r '.machines[].status' machines/registry.json | sort | uniq -c` on the resulting file gave 51 planned and 5 out of scope, and no other status, so none running or in progress.

### What surprised

- `npm run build` with no pages exits cleanly: `0 page(s) built`, with a warning from the sitemap integration that no pages were found. So the `pretest` build step passes on an empty site rather than failing.

## Task 2: The test results

Every figure on the site has to come from a test run, so this task turns a .NET test run into one file the site reads.

### What was built

- **`site/scripts/make-results.mjs`.** Reads the `.trx` files `dotnet test` writes. It keys each result by its test class and counts passed, failed and skipped per suite and in total, with the commit, the run id and the date. A result whose class it cannot find is an error rather than a silent gap. With no arguments it runs the whole .NET suite first; given a folder it only reads it.
- **`site/src/lib/results.mjs`.** Loads `site/src/data/results.json` and says whether it may be published: it ran something, nothing failed, nothing was skipped, and the Dormann tests ran.
- **`site/tests/results.test.mjs`.** Five tests: parsing, counting, the missing-class error, the publish rules, and a check that the real results file passes them.
- **`TestResults/` in the root `.gitignore`.** It was not ignored, and the `.trx` files must not be committed. `site/src/data/results.json` was already ignored by `site/.gitignore`, so it is not committed either: it is made from a run and rebuilt each time.

### Decisions

- **Skipped counts as not publishable, not as a soft pass.** The Dormann tests skip rather than fail where their assembler cannot run, so a run that skipped them would otherwise look clean. The publish rule therefore refuses any skip and also requires at least one Dormann test to have passed.
- **The results file is never committed.** A committed copy could be stale the moment code changed, and the site would keep quoting it. The cost is that a fresh checkout has to run the suite (or take the CI artifact) before the site tests that read it can pass.

### What the tests showed

- Before the two library files existed, `node --test tests/results.test.mjs` in `site/` failed with `ERR_MODULE_NOT_FOUND` for `site/scripts/make-results.mjs`: 0 passed, 1 failed (the file could not load).
- With both files written but no results file yet, the same command ran 5 tests: 4 passed and 1 failed, and the one failure was `the real results are publishable`, with the message that `results.json` is missing.
- `dotnet test --configuration Release --logger "trx;LogFileName=results.trx" --results-directory TestResults` at the repository root ended with `Passed!  - Failed: 0, Passed: 1480, Skipped: 0, Total: 1480, Duration: 1 m 9 s`. Timed with `time`, the whole command took 1 m 26 s.
- `node scripts/make-results.mjs ../TestResults` in `site/` printed `1480 passed, 0 failed, 0 skipped, in 13 suites`. It read that run's `.trx` file rather than running the suite again. `npm run results` with no argument runs the suite itself, so running it after the `dotnet test` above would have done the work twice.
- `node --test tests/*.test.mjs` in `site/` then ran 12 tests (5 new, 7 from task 1) and all 12 passed.

### What surprised

- The results file has 254 passed for `Wdc65C02Harte` and 256 for each of the other four Harte variants. That is not a gap: the WDC variant leaves out `$CB` (WAI) and `$DB` (STP), whose Harte files are empty, and `WaitAndStopTests` covers them instead. `docs/journal/2026-09-30-building-the-core.md` records the same 254 and the reason. The result is that the per-suite Harte counts on the site cannot be summed as 4 x 256 and must be read from the file.
- `npm run results` and the plan's step 4 overlap: the script runs `dotnet test` itself when given no folder, so the plan's two commands in a row run the suite twice.
