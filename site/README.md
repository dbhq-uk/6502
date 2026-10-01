# 6502.dbhq.uk

The site for this repository: a journal of the work, the table of every 6502
machine, and the count of the ones that run. Astro, static, deployed to
Cloudflare Pages.

```sh
cd site
npm ci
npm run results     # runs the whole test suite and writes src/data/results.json
npm run dev         # http://127.0.0.1:4333/
npm test            # builds the site, then checks it
```

Needs Node 22.22 or later (`engines` in `package.json`). `dev` and `preview` serve
on `127.0.0.1`. To reach them from another machine, set `SITE_HOST` to the address
to bind, for example `SITE_HOST=0.0.0.0 npm run dev`. Nothing here is tied to one
network, and no private address is committed.

`npm run results` takes a few minutes and downloads about 5 GB of test data the
first time. `src/data/results.json` is generated and never committed. CI
produces it from the same test run that gates the merge.

## Where everything comes from

Nothing on the site is typed twice. The repository is the source.

| On the site | Comes from |
|---|---|
| The journal | `docs/journal/*.md`, read at build time. Each entry has `title`, `date`, `summary` and `order` in its front matter, all required; `order` is the position within a day, and a tie on both is broken by file name |
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
class. Its page at `/machines/<id>/` is generated from the registry by
`src/pages/machines/[id].astro`, and the machines table then links to it; there is
no page to write. Its emulator is a separate piece of work.

## The checks

`npm test` runs these, in `tests/`:

| File | Holds |
|---|---|
| `registry.test.mjs` | The registry is valid, agrees with the family document, and a running machine has a passing acceptance test |
| `results.test.mjs` | The test results are read correctly, are publishable, and the real results file passes |
| `measurements.test.mjs` | The benchmark output is parsed correctly, and a machine is called physical only when `systemd-detect-virt` ran and said `none` (a missing tool is "a machine of unknown type") |
| `figures.test.mjs` | The figures are computed from the registry, the results and the measurements, a missing suite (or no Harte suite) stops the build instead of publishing 0, and the target verdict never rounds up |
| `figures-on-pages.test.mjs` | Every number on the home and status pages is a generated figure, the pages say honestly whether the speed target is met and that the figures are one collection of runs, and the results file names a full commit |
| `honest-pages.test.mjs` | The pages claim nothing they cannot back up: no machine appears to run, no page says the reference data came from a real chip, no test cadence is asserted, every generated image used as a background is captioned |
| `machines.test.mjs` | The machines and chips tables match the registry; only a running machine is linked (rules tested on a made-up registry, since none runs yet); the filters are hidden until the script runs; the sort comparison puts blanks last; the family page renders the repository document |
| `journal.test.mjs` | Every journal entry has its front matter, is built once with its own title, and is listed newest first; a link to another entry is a site link and a link to any other file goes to GitHub |
| `site.test.mjs` | Every page has a title, description and canonical link (the 404 has none and is `noindex`), no mention of the dropped port goal, one `h1` and its landmarks; no dashes or forbidden names; British English; no inline script; every internal link resolves; every image in `src/assets/imagery/` is captioned and alt-texted as an illustration; the lime fills one element and its other uses are named |
| `analytics.test.mjs` | GA4 uses the estate's one ID, is denied by default, loads only after consent, and the CSP allows only what it needs |
| `design.test.mjs` | Contrast from the real tokens, on the black canvas, the cards and the worst pixel of the traces texture; no raw colours in the stylesheet; no shadows but the navigation bar's |
| `html.test.mjs` | Every built page is valid HTML |
| `build.test.mjs` | The pages, the sitemap, `robots.txt` and the 404 page were built, and the sitemap lists exactly the pages that exist; the deploy checks every script the pages load; both workflows hold the same test floor; no local path or private address is published; this table lists every test file |

CI reads the number of passing tests back and fails below a floor, in `validate.yml`
and `deploy-site.yml`. The floor is the same number in both and must be raised when
the suite grows.

`DESIGN.md` records the look, including the fork from the DBHQ brand, and how
each image was made.

## Analytics and Search Console

GA4 uses the estate's one measurement ID (`G-3H3NFGSX85`) with no data stream of
its own, denied by default, and loaded only on `6502.dbhq.uk` after the visitor
accepts. `public/analytics.js` and `public/consent.js` began as the estate's, from
terraken, and have since changed here: the choice is remembered for this site only
(`localStorage` belongs to one origin), and the footer's Cookie choice button asks
again and puts every Consent Mode signal back to denied. They are external files
because the site's CSP allows no inline script.
The Search Console property for the host sits under the `sc-domain:dbhq.uk`
roll-up. The estate's own record of both is in the private company repository.
