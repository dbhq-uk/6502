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

## Task 3: The speed measurements

The Status page will print speeds, and rule 5 says a speed comes from a measurement rather than from prose. So the speeds are one dated file, written by a script and read by the site.

### What was built

- **`bench/collect-measurements.mjs`.** Builds and runs the native speed tool, publishes the browser tool twice (interpreter, and ahead of time), runs each in Chrome and writes `site/src/data/measurements.json`. It reads only the `measured` lines, never the warm-up, and throws unless every mode has exactly the requested number of measured runs. It records the date, the machine, the browser and .NET versions, and a plain description of the workload.
- **`site/src/lib/measurements.mjs`.** Loads the file and gives the best run of a mode in MHz.
- **`site/tests/measurements.test.mjs`.** Three tests: only measured runs are read, the browser and machine are described in words, and the best run is the highest.
- **`site/src/data/measurements.json`.** Committed, unlike `results.json`. A speed is a record of one machine on one day, and the file says so; it is not a claim that stays true as the code changes.

### How the file was made

The file was not produced in this task. `bench/collect-measurements.mjs` was run earlier the same day in a scratch copy of the repository, on this machine, and the output was copied in. Its `collected` field says `2026-09-30`. The collector was not run again here because it takes about ten minutes and the copy is from the same script and the same day. The script in this repository was written from the plan and checked only with `node --check`, which reported no error. Its live path (the builds, the browser runs) has not been run from this checkout.

### What the tests showed

- Before `bench/collect-measurements.mjs` existed, `node --test tests/measurements.test.mjs` in `site/` failed with `ERR_MODULE_NOT_FOUND` for that file: 0 passed, 1 failed (the file could not load).
- With the three files written, `node --test tests/*.test.mjs` in `site/` ran 15 tests (3 new, 12 from tasks 1 and 2) and all 15 passed.

### What the measurements say

Read from `site/src/data/measurements.json` with a short `node -e` script. The machine is `a KVM virtual machine, DO-Premium-AMD, 8 cores`, with Chrome 153.0.8010.47 and .NET 10.0.400. Each mode has 5 measured runs of 100000001 cycles.

| Mode | Best run | Worst run |
| --- | --- | --- |
| Native | 107.986 MHz | 103.172 MHz |
| Browser, ahead of time | 48.85 MHz | 29.137 MHz |
| Browser, interpreter | 4.965 MHz | 1.948 MHz |

- **The design target was 25 times a 2 MHz machine, which is 50 MHz. The best ahead-of-time run is 48.85 MHz, which is 24.4 times (48.85 / 2 = 24.425).** That is below the target, by a small margin. It is not rounded up. Native clears it easily: its best run is 107.986 MHz, 54.0 times.
- The ahead-of-time runs are not steady: 47.125, 48.85, 42.357, 29.137 and 38.027 MHz, in run order. The best is the top of a wide spread, and the worst run is 29.137 MHz (14.6 times). Any page that quotes one figure should say it is the best of five and show the spread.
- The interpreter runs climb from 1.948 MHz to 4.965 MHz over the five runs (best 2.5 times). The first run, 1.948 MHz, is under 40 percent of the best, 4.965 MHz (1.948 / 4.965 = 0.392). That fits a JIT-less interpreter warming caches, but the file does not say why, so this is a guess and not a finding.

### What surprised

- Ahead-of-time is 24.4 times, not 25 or more. The target was set before this measurement existed, so the site cannot claim it is met.
- The spread inside one mode, on one idle-as-far-as-we-know virtual machine, is larger than the gap between the best ahead-of-time run and the target.

## Task 4: The figures

Every number a page shows now comes through one module. A page that needs a count or a speed takes it from there, and never types it.

### What was built

- **`site/src/lib/figures.mjs`.** Computes every figure from three inputs: the registry, the test results and the dated measurements. It counts machines by status, sums the Harte cases over the suites whose name ends in `Harte`, and gives each mode's best speed as a multiple of a 2 MHz machine. `fmt` and `fmt1` format numbers the British way. The only typed numbers are two constants that belong to the design: the speed target (25) and the reference clock (2 MHz).
- **`site/src/lib/data.mjs`.** Loads the registry, the results and the measurements once, refuses to build if the registry or the results fail their checks, and exports the computed figures as `fig`.
- **`site/tests/figures.test.mjs`.** Four tests: the figures from made-up inputs, the number formats, that the real figures can be computed, and that the real registry is valid against the real results.

### Decisions

- **The Harte figure is a sum over the suites, not a typed 4 x 256.** The variant count is the number of suites whose name ends in `Harte`, so a sixth variant would change both figures without an edit here. The alternative was to list the variants by name, which would have been a second place to keep in step.
- **`data.mjs` throws rather than warns.** A page built from figures the checks reject would publish a claim nobody can stand behind, so the build stops.

### What the tests showed

- Before `figures.mjs` existed, `node --test tests/figures.test.mjs` in `site/` failed with `ERR_MODULE_NOT_FOUND` for `site/src/lib/figures.mjs`: 0 passed, 1 failed (the file could not load).
- With the three files written, `node --test tests/*.test.mjs` in `site/` ran 19 tests (4 new, 15 from tasks 1 to 3) and all 19 passed.
- `node -e "import('./src/lib/data.mjs').then(m=>console.log(JSON.stringify(m.fig)))"` in `site/` printed the real figures. Machines implemented 0, in scope 51, in progress 0. Variants 5. Tests passing 1480. Harte tests 1278. Dormann builds 12. Interrupt runs 150. Speed as a multiple of 2 MHz: 24.425 ahead of time, 2.4825 interpreter, 53.993 native.

### What surprised

- The Harte figure is 1278, not 1280, and the reason is the one recorded under task 2: four variants have 256 passing cases and the WDC variant has 254 (1278 = 4 x 256 + 254). Summing from the file gets it right where a typed 5 x 256 would not.
- The results file in this working tree was made from a run at commit `a8de702`, read from the `commit` field of `site/src/data/results.json`, which is earlier than the current commit. The task 4 changes touch no .NET code, so the counts are unchanged, but a published site must be built from a results file made at the commit it ships.

## Task 5: The shell

The look, the page layout, the analytics and consent files, and the first two pages (About and the 404 page). Every later page is built inside this layout.

### What was built

- **The look.** `site/src/styles/tokens.css` holds every colour, font and radius as a named token, and `global.css` uses them by name. It is the phosphor terminal decided on 30 September: black canvas, pale-green text, one lime accent.
- **The layout and its parts.** `site/src/layouts/Base.astro` with a header, a footer that carries the Capcom non-affiliation line word for word, a `Card` for figures and a native `<dialog>` for the consent choice.
- **Analytics, consent-gated.** `site/public/analytics.js` sets all four Consent Mode v2 signals to denied and loads the Google tag only from inside `__dbhqEnableGA()`, which runs only on the live host and only after a visitor accepts. `consent.js` owns the prompt. Both are same-origin files because the Content-Security-Policy in `site/public/_headers` has no `unsafe-inline` for scripts. The measurement ID is the estate's one, not a new data stream.
- **`machines-table.js`, `robots.txt`, `favicon.svg`.** The first adds sortable headers and filters to the tables that arrive in later tasks.
- **Two pages.** `about.astro` and `404.astro`.
- **Four test files, and a helper.** `site.test.mjs` (rules for every built page), `analytics.test.mjs`, `design.test.mjs` (contrast ratios read off the tokens) and `html.test.mjs` (every built page through `html-validate`).
- **New packages.** `@fontsource-variable/inter`, `@fontsource-variable/inter-tight` and `@fontsource/fira-mono` (self-hosted fonts, so the CSP needs no font host) and, as a dev dependency, `html-validate`. The versions in `site/package.json` after the install are `^5.3.0` for the three fonts and `^11.16.1` for `html-validate`.

### Decisions

- **The tests were written before the code they test.** The site tests import `site/src/lib/site.mjs`, which did not exist, so they could fail first.
- **Nothing is measured off the live host.** The tag loads only when `location.hostname` is `6502.dbhq.uk`, so a local preview or a Pages branch build never calls Google. The deploy stays switched off; nothing here touches Cloudflare or the estate's analytics settings.
- **Decline comes first, and is the same size as Accept.** A test holds the order.

### What the tests showed

- Before the code existed, `npm test` in `site/` ran 23 tests: 20 passed, and 3 test files failed to load (`site.test.mjs` with `ERR_MODULE_NOT_FOUND` for `site/src/lib/site.mjs`, and `analytics.test.mjs` and `design.test.mjs` because their inputs did not exist yet).
- With every file written from the brief, `npm test` in `site/` built the site and ran 43 tests, and all 43 passed: the 19 from tasks 1 to 4, and 24 new (11 in `site.test.mjs`, 6 in `analytics.test.mjs`, 6 in `design.test.mjs`, 1 in `html.test.mjs`).
- `find dist -name '*.html'` in `site/` lists two built pages, `dist/404.html` and `dist/about/index.html`. The rules above are therefore checked against two pages so far, and get stronger as pages arrive.
- No file was changed from the brief.

### What surprised

- Before any page existed, the build still succeeded and the HTML validity test passed, because it had no pages to check. A rule that runs over "every built page" says nothing about a site with none, which is why `site.test.mjs` has a test that fails when no page was built.
- `npm install` printed a warning that `esbuild@0.28.2` has an install script not yet covered by `allowScripts`. The install and build worked without it, and nothing was approved.

### Task 5 review: what it found and what changed

The review agreed the code matched the plan. It then found three defects that the plan itself had mandated, and four smaller ones. All are fixed in one follow-up commit.

**What the review found, and what was changed**

- **Contrast tests said more than they checked.** The test titled "text meets AA on the card, window and nav surfaces it is actually set on" only checked three colours. A throwaway node script that computes WCAG contrast from the tokens showed the real failures: `--sage-40` on `--iron` is 3.97 to 1 (used for the consent note and the terminal window's title), and `--deep` on the navigation bar is 3.88 to 1 once the 85 percent translucency over black is counted (3.62 on `--veil` alone). Both are under 4.5. **The fix changes the use, not the palette:** the consent note, the window title and the brand's "by dbhq" now use `--fern` (5.82 on iron, 5.44 on the blended nav bar, same script). `--sage-40` and `--deep` are now used only on the black canvas, where they pass (4.69 and 4.91). The `--deep` comment in `tokens.css` said it "holds", which was true only on black, and now says so.
- **The design test now covers every colour rule.** It reads every rule in `global.css` that sets a text colour, looks up the surface it sits on in a table, and computes the ratio from the real tokens, with the nav bar's colour computed from its own `rgba`. A new colour rule with no entry in the table fails the suite, and so does a table entry for a rule that no longer exists. The nav bar's blend is over black; scrolled over lighter content it can differ, and the test comment says so.
- **No navigation below 900px.** `.links` was `display: none` under 900px with nothing to replace it, so a phone had no way to reach any page. The header now wraps: the links drop to a second row under the brand, with no script, and the GitHub button stays. A test fails if a `display: none` returns on the links, the brand or the header, at any width.
- **No way to withdraw consent.** Once a reader answered, the prompt never came back. The footer now has a `Cookie choice` button (a real `<button>`). `consent.js` wires it (the CSP allows no inline handler): it removes the `dbhq-consent` key, calls a new `__dbhqRevokeGA()` in `analytics.js` that puts all four Consent Mode signals back to denied, and reopens the dialog with `showModal()`.
- **A bug found while writing that.** Accepting again in the same page load would have done nothing, because `__dbhqEnableGA()` returned early once the tag had loaded, so consent would stay denied. It now sends the granted update first when the tag is already there. A test holds that order.
- **Enter granted consent.** Accept had `autofocus`, so a bare Enter on opening the prompt accepted. Initial focus is on the dialog heading now (`tabindex="-1"`, `autofocus`), and Decline still comes first.
- **A comment that was untrue.** `consent.js` said Escape returns the prompt "next visit". It returns on the next page load, since nothing is stored. The comment says that.
- **Internal notes were being served.** The HTML comments in `Consent.astro` and `Base.astro` went out in every page, and `analytics.js` is copied as it is and named another project, the analytics property and a path in another repository. The comments are now frontmatter comments, which Astro does not emit, and the `analytics.js` header describes what the file does without those names. Tests fail on any HTML comment in built output and on those names in `analytics.js`.
- **The `_headers` comment.** It called `style-src 'unsafe-inline'` "the only other relaxation", but `img-src` also allows `data:`. Both are named now.
- **The 404 page.** The analytics and consent-prompt checks skipped it. They now cover it, because it is built from the same layout and loads the same scripts.

**What the tests showed**

- `npm test` in `site/` ran 53 tests and all 53 passed: the 43 from before plus 10 new.
- With the source under `src/` and `public/` put back to the first version and the new tests kept, the same command ran 53 tests with 43 passing and 10 failing, one for each finding above. The tests fail without the fixes.
- `grep -c "Modal\|DESIGN.md" dist/about/index.html` printed 0, so the design notes in the stylesheet's comments are not in the served page either.

**What it says about the plan**

The plan mandated the contrast wording, the hidden navigation, the missing withdrawal control and Accept-first focus. Each was a defect in its own right, not a slip in copying the plan.

**Not done, on purpose:** withdrawing stops measurement and cookie writing, but does not delete a `_ga` cookie already set. Deleting one on the shared parent domain is a decision about the whole estate, so it is left alone.

## Task 6: The journal

Every entry in `docs/journal/` is now a page at `/journal/<name>/`, and `/journal/` lists them, newest first. The site reads the repository's own folder at build time, so writing an entry is the same act as publishing it.

### What was built

- **Front matter on the five older entries.** Each got a title, a date, a summary and an `order` (the position within a day), and nothing else in them changed. The entry for this site already had front matter from task 1.
- **A content collection.** `site/src/content.config.ts` reads `../docs/journal/2*.md` and checks each entry's front matter against a schema.
- **Two pages.** `journal/index.astro` (the list) and `journal/[id].astro` (one entry).
- **A small markdown plugin.** `site/src/lib/rehype-journal.mjs` drops each entry's first level-one heading, because the page prints the title from the front matter, and rewrites a relative link such as `../superpowers/specs/x.md` to the same file on GitHub. Both are needed: the entries are written for the repository, where those links work, and on the site they would be dead.
- **`journal.test.mjs`.** Four tests: front matter is complete and the date matches the file name; every entry is built and listed; no relative repository link survives; each entry has exactly one `h1`.
- **A package.** `@astrojs/markdown-remark`, added to `site/package.json` as a dependency (`^7.3.1`).

### A separate fix, found by the task 5 re-review

`Consent.astro` told readers "The choice is remembered across dbhq.uk and its sites, so this is asked once", and comments in it and in `analytics.js` said the `dbhq-consent` key is shared across `*.dbhq.uk`. That is untrue: `localStorage` belongs to one origin, so `6502.dbhq.uk` cannot read what a reader chose on `dbhq.uk`. The reader is asked once on each site. The copy now says "remembered in this browser, for this site only", and both comments say why. No test asserted the old wording. It went in its own commit (`71f08b1`) before this task's. The plan document `docs/superpowers/plans/2026-09-30-site-v1.md` still carries the old wording, because it is a record of what was planned; it is not served.

### What the tests showed

- With the four new tests and no front matter, no plugin and no pages, `npm test` in `site/` ran 57 tests: 55 passed and 2 failed. The front matter test failed on `2026-09-29-deciding-what-to-build.md has no title`, and the built-pages test failed because `/journal/` did not exist. The other two new tests passed because they loop over journal pages, and with none they check nothing. That is the same emptiness as task 5 found, and it is why the built-pages test is the one that carries the weight.
- With everything written from the brief, `npm test` in `site/` ran 57 tests and all 57 passed: the 53 from before plus the 4 new. The brief's count of 47 was made before task 5's review added 10 tests and includes these 4, so the same arithmetic gives 57.
- `find dist -name '*.html' | wc -l` in `site/` printed 9: the 404 page, About, the journal list and six entries. `grep -o "<h1[^>]*>[^<]*" dist/journal/2026-09-30-building-the-core/index.html` printed one heading, `Building the core`, from the front matter.
- `grep -o 'href="https://github.com/dbhq-uk/6502/blob[^"]*"' dist/journal/*/index.html` shows the rewritten links, for example `docs/the-6502-family.md#who-made-it` in `checking-the-ground`.

### What surprised

- `npm install @astrojs/markdown-remark` printed the same `esbuild` install-script warning as before. Nothing was approved.
- The build printed no warnings.

## Task 7: The machines, the chips and the family

Three pages now answer "what ran on a 6502 and what does the core run": `/machines/`, `/chips/` and `/family/`. Each is generated from a file the repository already holds, so none carries a count typed by hand.

### What was built

- **`/machines/`.** One row for every machine in `machines/registry.json`, oldest first, with three filters (status, category, core) and a count line. It is a complete table with no script: `machines-table.js`, written in task 5, only adds sortable headers and makes the filters work. Without it, the filters do nothing and the table still reads in full. A machine links to its own page only when its status is `running`.
- **`/machines/[id]/`.** One page for each running machine, showing the acceptance suite's pass count from `results.json`. The registry has no running machine, so this route builds no page yet.
- **`/chips/`.** One row for every chip in the registry, with sortable headers and a count line.
- **`/family/`.** The repository's own `docs/the-6502-family.md`, rendered as it is. It is a second collection, `docs`, in `site/src/content.config.ts`, which also lists `known-differences.md`. Nothing was copied, so the page cannot drift from the document. The rehype plugin from task 6 drops the document's own first heading, so the page has one `h1`.
- **`machines.test.mjs`.** Five tests: a row for every machine, a row for every chip, links only for running machines, the sort and filter hooks and the script tag, and the family page holding a table and its opening sentence.

### What was different from the brief

`site/src/content.config.ts` already existed, because task 6 created it for the journal. This task added the `docs` collection to it rather than creating the file, and the journal collection is unchanged.

### What the tests showed

- With `machines.test.mjs` written and no page, `npm test` in `site/` ran 62 tests: 57 passed and 5 failed. All five failed with `Cannot read properties of undefined (reading 'html')`, because `page('/machines/')` and the others found no built page.
- With the pages written, the same command ran 62 tests and all 62 passed: the 57 from before plus the 5 new.
- `find dist -name '*.html' | wc -l` in `site/` printed 12: the 9 from task 6 plus machines, chips and family.
- Counting in the built HTML: `grep -o '<tr data-status' dist/machines/index.html | wc -l` printed 56, and `grep -o '<tr>' dist/chips/index.html | wc -l` printed 31, which is 30 chip rows and the header row. A `node -e` over `machines/registry.json` printed 56 machines, 30 chips and 0 running, so the tables and the registry agree.
- `npm run build` printed no warnings or errors.

### What surprised

- The new pages needed no new CSS. `.filters`, `.tablewrap`, `table.data`, `.status` and `.count` were already in `global.css` from task 5, so the contrast test, which fails on a colour rule with no surface, had nothing to place and did not change.

## Task 8: The home page, the status page and the imagery

The site now has a front door and a page that shows the state of the work. Both read every figure from `results.json`, `measurements.json` and the registry, and the three generated images are in.

### What was built

- **`/` (home).** A headline, the hero image with its "Illustration" caption, four figure cards, a terminal-style card of test results, the first three machines from the registry and the three newest journal entries.
- **`/status/`.** Six figure cards, the test suites in a table, the speed table with the design target beside it, and `known-differences.md` rendered underneath.
- **Three images.** `hero-die`, `waveform` and `traces` were converted from the PNG originals with `cwebp` (quality 88, 90 and 82). `ls -l` showed 322,732, 17,098 and 94,902 bytes against originals of 2,055,376, 421,970 and 1,096,527. Only the WebP files are committed. The originals stay in `/home/devops/dbhq-previews/6502/imagery/final/`.
- **`imagery.css`** holds the two background images as custom properties, and `Base.astro` imports it before `global.css`. `Base.astro` already existed from task 5, so this task added one import line to it rather than creating it.
- **`DESIGN.md`** records the fork of the brand, the imagery prompts, model, quality and cost. The cost and model were checked against `~/.dbhq/imager/history.jsonl` with a short Python script: the four low-quality drafts billed $0.017955 in all and the three finals $0.147563, which are the $0.018 and $0.148 written there. The model is `gpt-image-2.5-flare` on every row. The brief and the history agree. Two lines in the brief's text pointed at files that do not exist (`tests/pages.test.mjs` and a site README), so they now say `tests/site.test.mjs` and "the figures and the content".
- **`figures-on-pages.test.mjs`.** Three tests: every number on both pages is a generated figure or a named literal, both pages name the commit their results came from, and the home page shows the machines count.
- **The internal-link test** moved into `site.test.mjs`, as the brief says: every `href` and `src` that starts with `/` must resolve to a built file.

### What the tests showed

- With `figures-on-pages.test.mjs` written and no pages, `node --test tests/figures-on-pages.test.mjs` in `site/` ran 3 tests and all 3 failed with `Cannot read properties of undefined (reading 'html')`, because `page('/')` and `page('/status/')` found nothing.
- With everything in place, `npm test` in `site/` ran 66 tests and all 66 passed: the 62 before this task, plus the 3 in `figures-on-pages.test.mjs` and the 1 link test. The brief expected 56 from 52 with the same 4 tests, and 66 is that plus the 10 that the task 5 review added and the 5 that the task 6 review added.
- `npm run build` printed `14 page(s) built`: the 12 from task 7, the home page and the status page.
- To check the tests catch a wrong multiple, `{fmt1(fig.speedAot)} times` on the home page was replaced with a typed `25.0 times` and `npm test` failed with `/ shows numbers that are not generated figures: 25.0`. The change was reverted and the suite went back to 66 passing. **The guard has a limit:** a typed `25` or a typed `24.4` would pass, because both are values the test allows. It catches a number that is not in the data, not one that is in the data and in the wrong place.

### The speed target is not met, and the pages do not say it is

The design target is 25 times a 2 MHz machine. The best ahead-of-time run in `measurements.json` is 48.85 MHz, which is 24.4 times, so the target is not met. The home page shows `24.4 times` and says nothing about the target. The status page says "The design's target is 25 times" and, in the table below it, shows `24.4` for the ahead-of-time build. The brief's wording was read against that and left as it is: it states the target and the measurement, and claims neither that the target is met nor that 24.4 is 25. `fmt1` gives 24.4 from 24.425, which rounds down.

### Two defects and one check

- **A missing space on the status page.** The brief's template put a line break between `{longDate(...)}` and `on`, and Astro dropped the whitespace, so the built page read "Measured on 30 September 2026on a KVM virtual machine". `grep -o "2026.\{0,8\}on a KVM" dist/status/index.html` showed `2026on a KVM`. An explicit `{' '}` after the date fixed it, and the same grep then showed `2026 on a KVM`.
- **The waveform lowered text contrast on narrow screens.** The design test only reads colour rules, so it cannot see text over an image. To check, a Playwright script loaded the built home page at five widths, made all text in the section transparent, took a full-page screenshot and measured the brightest background pixel under every text box. At 901, 1280 and 1920 pixels the brightest pixel was black, so body text (`--sage-60`) measured 8.29 to 1. Below the 900 pixel breakpoint the two columns stack, the text runs across the whole width and the waveform lines sit under it: at 390 pixels the worst background had luminance 0.1473 and `--sage-60` measured 2.10 to 1, and at 768 pixels 1.73 to 1. That fails 4.5 to 1. `global.css` now sets `.proof::before` to opacity 0.18 below 900 pixels. Measured again the same way, `--sage-60` is 5.89 to 1 at 390 pixels and 5.55 to 1 at 768, and the heading and the eyebrow are higher. Desktop is unchanged at opacity 0.5.
- **The traces texture.** It sits under a 0.55 black overlay, which the CSS does have (`.tex::before`). The same script measured the worst background at 0.0133 luminance at 1280 pixels, and `--sage-60` is 6.54 to 1 on it, the eyebrow colour 8.12 and the link colour `--fern` 5.44. The only text in `.note` on that section is a link in `--fern`. `--sage-40` would measure 3.71 there and fail, so plain `.note` text must not be placed over that texture. The other text in the section sits on opaque cards.
- **The hero image** carries no text. The headline is above it and the caption below it, on the black canvas, so the image cannot lower any text contrast.

No entry was added to the contrast table in `design.test.mjs` and nothing in it was weakened: `.hero`, `.proof` and `.tex` set no text colour, so the test had nothing new to place.

### What surprised

- The results the pages show are from commit `a8de702`, which is the commit `results.json` names, not the head of the branch. `results.json` is git-ignored and was generated at 15:37 today. It is a true statement of where the figures came from.
- The home page says "0 of 51" machines implemented and links "All 56 machines in the family". Both come from the registry: 51 are in scope and 56 are listed.

### Task 8 review: what it found and what changed

The review agreed the code matched the brief and then found seven places where the pages, or the tests that guard them, said more than they could back up. Each was fixed in one follow-up commit.

- **The target had no subject.** The status page said "The design's target is 25 times" and then showed 54.0, 2.5 and 24.4 in a table, so a reader could not tell what the target applied to or whether anything met it. The design (`docs/superpowers/specs/2026-09-29-6502-design.md`, the speed item in the core's test list) says the target is "at least 25 times real speed for the core alone", and does not say which build. The page now says exactly that, then gives the verdict for the native run (`54.0 times: met`) and for the browser build compiled ahead of time (`24.4 times: not yet met`). The verdict comes from `targetVerdict` in `site/src/lib/figures.mjs`, so no page types "met". The home page card says the same in one line: "met natively, not yet met in the browser". The design, and the wording of it, are unchanged.
- **The figures guard was weaker than its claim.** It stripped the Speed paragraph, every note and the terminal card before checking numbers, allowed digits such as 0 and 2 from several unrelated sources, and carried a `const status = url === '/status/' ? html : html;` that did nothing. It now checks all the page text, including the terminal card and the notes, against a set built from the data only; it decodes `&#39;` first, because the apostrophe in "NES's" had read as the number 39 once the stripping stopped. The one document that is rendered whole on the status page is still exempt. New tests: each headline card equals the expected string computed from the figures; each row of the speed table equals `fmt1` of the best run and of best divided by 2; the number 25 appears exactly once per page, next to a "met" or "not yet met"; and when the browser build is under the target, the pages must say "not yet met" and no sentence near "target" may say reached, exceeds, achieved or hit.
- **The hero image was lazy.** It is above the fold, so it is now `loading="eager"` with `fetchpriority="high"`, and a test holds both.
- **The home page read as though machines exist.** It showed "0 of 51" beside "Each runs in the browser and passes an automated test". The card now says "A machine counts only once it runs in the browser and passes an automated test.", the machines heading is "The machines this core is for", and the hero says "the machines it will run".
- **The card claimed a test cadence the page could not verify.** It said the tests run on each push. `.github/workflows/validate.yml` runs `dotnet test` on pushes to `main` and on pull requests, not on each push, and the commit on the pages is not the branch head. The card now says "From the latest test run, on the commit shown below", and a test fails any page that makes the cadence claim.
- **The background images had no caption.** `waveform` and `traces` are CSS backgrounds, so the existing caption test, which looks at `<img>` tags, could not see them. The home page now carries "Illustration: the textured backgrounds behind the proof and machines sections are generated, not measurements". A new test reads `imagery.css`, finds the class that uses each image in `global.css`, and fails any built page that uses that class without such a caption.
- **`DESIGN.md` said the PNG originals were "1 to 2 MB each".** `ls -l` on the three showed 421,970, 1,096,527 and 2,055,376 bytes, so it now says "0.4 to 2 MB".

`npm test` in `site/` ran 75 tests and all 75 passed: the 66 before, plus 4 new tests in `figures-on-pages.test.mjs`, 1 in `figures.test.mjs` and 4 in the new `honest-pages.test.mjs`. Three mutations were tried and each failed the suite as it should: deleting the caption line, adding "it has reached the target" to the status page, and removing the eager attributes from the hero. Each was reverted.

### Task 8 re-review: the 2 MHz reference is the site's, not the design's

The re-review found the target fix only partly done. The status page said "The design's target is 25 times a 2 MHz machine", and the home card put the target beside "A 2 MHz machine". The design (`docs/superpowers/specs/2026-09-29-6502-design.md`, the speed item) says only "at least 25 times real speed for the core alone". The 2 MHz is this site's own reference, the BBC Micro's clock, and the met and not yet met verdicts depend on it, so the pages had put a choice of the site's into the design's mouth.

- **The wording.** The status page now quotes the design as it words it: "at least 25 times real speed, for the core alone", and says the design does not say which build. It then says the site measures against a 2 MHz machine, the BBC Micro's clock, gives the two verdicts against that reference, and says plainly that the reference is the site's choice and the verdicts assume it. The clocks come from the repository, not from memory: `grep -n "MHz" machines/registry.json` and `docs/the-6502-family.md` both give the KIM-1 as `6502, 1 MHz` and the BBC Micro as `6502, 2 MHz`. The page reads the KIM-1's clock out of the registry, so it says the KIM-1 runs at 1 MHz and every multiple against it would be 2 times as large, and neither number is typed. The home card title is now "Against a 2 MHz machine, in the browser", and its text quotes the design first and gives the verdicts in a separate sentence that names the site's reference.
- **The tests.** A new test in `figures-on-pages.test.mjs` splits each page into sentences and fails if any sentence holds both "target" and "MHz", and requires the sentence with "target is" to say "at least", "real speed" and "for the core alone". The once-per-page 25 rule, the verdict test and the exact-value tests were unchanged and pass.
- **A mistake of mine, found by the suite.** The fix-round journal text above quoted the banned phrase for the test cadence, and the built journal page then failed the very test that bans it. `npm test` had been run before that journal section was written, so the commit before this one was pushed with that test failing. The quote is reworded, and this commit is green.

## Task 9: CI, deploy and the docs

The site now has checks in CI and a deploy workflow that is switched off. Nothing has been deployed.

### What was built

- **`validate.yml`.** The `test` job now writes a TRX file, turns it into `results.json` with `site/scripts/make-results.mjs`, and uploads it as an artifact. A new `site` job downloads that artifact into `site/src/data`, runs `npm ci` and `npm test`. The site's figures are read from `results.json` with the working directory `site`, and the artifact puts the file at `site/src/data/results.json`, so the "real results are publishable" test finds it where it looks.
- **`deploy-site.yml`.** On a push to `main` that touches the site, the docs, the registry, the core or the tests, it runs the whole test suite, builds the site, deploys to Cloudflare Pages, purges the cache and checks that every built page, the machine-readable files and the edge headers are serving. It runs only when the repository variable `SITE_DEPLOY` is `true`. That variable is not set, no secret has been created and nothing outside the repository has been touched.
- **`site/tests/build.test.mjs`.** Three tests: the eight pages the brief names were built, a sitemap and a `robots.txt` exist and the sitemap holds no 404, and a third that the brief did not have (below).
- **`site/README.md`.**

### Choices and changes from the brief

- **Node 22.22, not 22.** `site/node_modules/html-validate/package.json` says `"node": "^22.22.0 || >= 24.8.0"` and `site/node_modules/astro/package.json` says `">=22.12.0"`. The brief's `node-version: '22'` would satisfy both only if the runner resolved it to a recent 22. `actions/setup-node` uses a Node already in the runner's tool cache when one matches the range, and a cached 22.x older than 22.22 would match `22`. So the workflows say `22.22`, which a cached 22.21 does not match. `site/package.json` has no `engines` field, so nothing else states the floor.
- **The test-count floor is 79, not 51.** The brief's own comment says to raise it when the suite grows. 51 would let 28 tests disappear unseen.
- **A sitemap test derived from the build.** The brief's sitemap test names one page by hand and looks for no 404. It would pass with a sitemap missing every other page. The new test reads the built pages and the sitemap and requires the two lists to be equal, with the 404 excluded, and fails if fewer than eight pages were found so that an empty build cannot pass it.
- **The README's table of checks was rewritten.** The brief's table named `pages.test.mjs` and `content.test.mjs`, which do not exist on this branch. The table now lists the thirteen test files that do, each with what it holds.
- **Actions.** Every action is pinned by commit SHA. The three the brief adds (`setup-node`, `upload-artifact`, `download-artifact`) were each looked up with `gh api repos/actions/<name>/commits/<sha>`, which found the commit, and `git/matching-refs/tags`, which showed the tags v4.4.0, v4.6.2 and v4.3.0 pointing at them. The three already in `validate.yml` were checked the same way.

### What the checks showed

- Before the sitemap test could pass, `node --test tests/build.test.mjs` run from an empty directory failed with `ENOENT: no such file or directory, scandir '/tmp/emptycwd/dist'`, so the tests do read the build. With the real build, all 3 passed.
- To check the new test is not vacuous, the `about` entry was deleted from `dist/sitemap-0.xml` and `node --test tests/build.test.mjs` failed the sitemap test with a deep-equal assertion (2 passed, 1 failed). The file was restored and all 3 passed.
- `python3 -c "import yaml; ..."` parsed both workflows: `validate.yml` has the jobs `test`, `site` and `dashes`, and `deploy-site.yml` has `deploy`, with `if: vars.SITE_DEPLOY == 'true'`. `actionlint` 1.7.12, downloaded to `/tmp` for the check and not added to the repository, printed nothing on both files, with exit code 0. `shellcheck` is installed here, so actionlint checked the shell in the `run` steps too.
- The deploy workflow reads the pass and fail counts out of the log. The log came from Node 24 on this machine, which prints `ℹ pass 79`, and Node 22 in CI prints `# pass 79` when the output is piped. The two `grep` lines in the workflow were run against a captured log in each format, under `LC_ALL=C` and `LC_ALL=C.UTF-8`, and read 79 and 0 in all four cases. The page list the deploy checks is derived from `dist`: `find` gave 13 addresses, the same 13 that are in the sitemap.
- `npm test` in `site/` ran 79 tests and all 79 passed. The brief expected 58 from 56, and after the reviews of the earlier tasks the suite was at 76, so 78 was expected. The extra one is the derived sitemap test.

### Things that cannot be checked here

- Neither workflow has run. `validate.yml`'s new jobs run for the first time on the pull request, and the deploy workflow is skipped until `SITE_DEPLOY` is set. The Cloudflare project, the secrets and the steps that follow the deploy have never been exercised.
- `npx wrangler` in the deploy step is not pinned: `site/package.json` does not list `wrangler`, so `npx` fetches whichever version is latest on the day. That is the brief's design. It is worth pinning before the switch is turned on.

## Where it stands

Every figure here is from a command run on 30 September 2026.

- **Pages.** `npm test` in `site/` runs the build first, and the log says `14 page(s) built`: the 13 pages that carry an `index.html` (`find dist -name index.html`) and the 404 page.
- **Tests.** `npm test` in `site/` ran 79 tests, 79 passed, 0 failed, 0 skipped.
- **Deploy.** Nothing is deployed. `deploy-site.yml` is switched off by design: it runs only when the repository variable `SITE_DEPLOY` is `true`, and it is not set. No secret was created and no Cloudflare, Terraform or Search Console change was made.
- **Workflows.** Both parse as YAML and `actionlint` is clean, but neither has run.
