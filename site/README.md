# 6502.dbhq.uk

The site for this repository: a journal of the work, the table of every 6502
machine, and the count of the ones that run. Astro, static, deployed to
Cloudflare Pages.

```sh
cd site
npm ci
npm run results     # runs the whole test suite and writes src/data/results.json
npm run dev         # http://100.115.72.85:4333/
npm test            # builds the site, then checks it
```

`npm run results` takes a few minutes and downloads about 5 GB of test data the
first time. `src/data/results.json` is generated and never committed. CI
produces it from the same test run that gates the merge.

## Where everything comes from

Nothing on the site is typed twice. The repository is the source.

| On the site | Comes from |
|---|---|
| The journal | `docs/journal/*.md`, read at build time. Each entry has `title`, `date`, `summary` and an optional `order` in its front matter |
| The family page | `docs/the-6502-family.md`, rendered unaltered |
| Known differences on the Status page | `docs/known-differences.md`, rendered unaltered |
| The machines and chips tables, and the count | `machines/registry.json` |
| Tests passing, variants, suites | `src/data/results.json`, made from the test run |
| The speed figures | `src/data/measurements.json`, made by `bench/collect-measurements.mjs` and committed as a dated record |

**A machine counts as implemented only when it runs in the browser and passes an
automated test in CI.** In the registry that is `status: "running"` with an
`acceptance` field naming a test suite, and the build fails if that suite is not
in the results, or did not pass with nothing skipped.

## Adding a machine

Add it to `machines/registry.json` and to `docs/the-6502-family.md`; a test fails
if the two disagree. It starts as `planned`. When its own spec is built and its
acceptance test passes, set `status` to `running` and `acceptance` to the test
class, and give it a page under `src/pages/machines/`.

## The checks

`npm test` runs these, in `tests/`:

| File | Holds |
|---|---|
| `registry.test.mjs` | The registry is valid, agrees with the family document, and a running machine has a passing acceptance test |
| `results.test.mjs` | The test results are read correctly, are publishable, and the real results file passes |
| `measurements.test.mjs` | The benchmark output is parsed correctly |
| `figures.test.mjs` | The figures are computed from the registry, the results and the measurements, and the target verdict never rounds up |
| `figures-on-pages.test.mjs` | Every number on the home and status pages is a generated figure, and the pages say honestly whether the speed target is met |
| `honest-pages.test.mjs` | The pages claim nothing they cannot back up: no machine appears to run, no test cadence is asserted, every generated image is captioned |
| `machines.test.mjs` | The machines and chips tables match the registry, and the family page renders the repository document |
| `journal.test.mjs` | Every journal entry has its front matter, is built and is listed |
| `site.test.mjs` | Every page has a title, description and canonical link, the Capcom line, one `h1` and its landmarks; no dashes or forbidden names; British English; no inline script; every internal link resolves; the lime is used once |
| `analytics.test.mjs` | GA4 uses the estate's one ID, is denied by default, loads only after consent, and the CSP allows only what it needs |
| `design.test.mjs` | Contrast from the real tokens, no raw colours in the stylesheet, no shadows but the navigation bar's |
| `html.test.mjs` | Every built page is valid HTML |
| `build.test.mjs` | The pages, the sitemap, `robots.txt` and the 404 page were built, and the sitemap lists exactly the pages that exist |

`DESIGN.md` records the look, including the fork from the DBHQ brand, and how
each image was made.

## Analytics and Search Console

GA4 uses the estate's one measurement ID (`G-3H3NFGSX85`) with no data stream of
its own, denied by default, and loaded only on `6502.dbhq.uk` after the visitor
accepts. `public/analytics.js` and `public/consent.js` are the estate's, from
terraken, and are external files because the site's CSP allows no inline script.
The Search Console property for the host sits under the `sc-domain:dbhq.uk`
roll-up. The estate's own record of both is in the private company repository.
