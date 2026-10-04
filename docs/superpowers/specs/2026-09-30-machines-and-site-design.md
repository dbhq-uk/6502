# Stage 2: the machines programme and the site

Written 30 September 2026. This is the design for everything after the core:
how machines are built and counted, and the site at 6502.dbhq.uk that shows
them. It changes the mission in [the first design](2026-09-29-6502-design.md),
and where the two disagree, this one governs.

## The mission

**Implement as many 6502-family machines as possible, in the open, each one
proven.** The core is the foundation. The machines are the product. The site is
where the count, and the proof behind it, is shown.

The earlier design named three machines: the KIM-1, the BBC Micro and the NES.
They are now the first three entries of a much longer list, not the whole
plan. The verified port of a commercial NES game that the first design named as the
end goal was dropped on 1 October 2026, and nothing on the site refers to it.

## What "implemented" means

The site's headline figure is "machines implemented", so the definition is
strict. **A machine counts only when both of these hold:**

1. It **runs in the browser** and reaches its own first prompt or a known
   program.
2. An **automated acceptance test passes for it in CI**: a boot check, plus at
   least one recognised test program or ROM for that machine where one exists.
   Where none exists, the boot check compares the machine's screen or output
   with a recorded result.

A machine that boots but has no passing test is shown as "in progress" and is
not counted. The figure on the site is computed from the machine registry and
the test results. It is never typed.

## The machines list

One data file, `machines/registry.json`, holds every 6502-family machine. It is
the source of the table on the site and of the count. Each entry has:

| Field | Meaning |
|---|---|
| `id` | Stable slug, for example `kim-1` |
| `name`, `year`, `maker` | As in the family document |
| `category` | `single-board`, `computer`, `console`, `arcade`, `peripheral`, `modern` |
| `cpu` | The chip, for example `6502`, `6507`, `2A03`, `65C02` |
| `core` | Which core variant runs it: `nmos`, `2a03`, `65c02`, or `none` for a different CPU |
| `status` | `running`, `in-progress`, `planned`, or `out-of-scope` |
| `acceptance` | The id of its acceptance test in the test results. Required when `status` is `running` |
| `rights` | Short note on system ROM and software rights, filled in by that machine's research |

`out-of-scope` is for machines whose CPU is not in the core (the 65C816 and
HuC6280 machines, for instance). They stay in the table, because the table is
the whole family, and are not counted in the total the count is measured
against.

[`docs/the-6502-family.md`](../../the-6502-family.md) stays the human-written
account of the family. A site test fails if a machine named in its tables is
missing from the registry, so the two cannot drift apart.

## How a machine is built

Every machine is its own short spec and plan, built and reviewed the way stage
1 was, with a journal entry as it goes. A machine is a project of its own that
uses the core:

- `src/machines/Dbhq.Machines.<Name>/`: the machine's bus, its chips, its
  memory map and its ROM loading.
- `tests/Dbhq.Machines.<Name>.Tests/`: its acceptance test, which is what the
  registry's `acceptance` field names.
- A browser page, built on a shared browser host.

**The shared parts are extracted, not designed up front.** The KIM-1 is built
first with what it needs. When the second machine needs something the first
also has, that piece moves into a shared library. A general framework written
before two machines exist is a guess.

**Rights are checked per machine before its page ships**, with a `legwork`
research pass. System ROMs are the usual question. Where a maker's ROM cannot
be served, the page boots an open replacement, or asks the visitor to supply
the file. Homebrew and freely licensed software are bundled, and nothing else.
Nothing third-party is committed to the repository.

## The order of machines

A proposed order, ranked by the size of the machine, whether its ROM can be
served, whether test material exists, and how many people would want it. Each
is a proposal until its own spec is written.

1. **KIM-1**: settled. First light, and it sets the pattern.
2. **BBC Micro Model B**: settled.
3. **Acorn Electron**: settled 4 October 2026. The smallest step from the BBC
   Micro: BBC BASIC is the same ROM, the operating system is another Acorn ROM
   from the same holder, and one ULA does the work of the BBC's video and sound
   chips.
4. **Atari 2600**: settled 4 October 2026. No system ROM and two chips, and a
   display drawn by the program in step with the beam, which needs exact cycle
   timing more than any other machine here.
5. **NES**: settled. It was third until 4 October 2026, partly as the reference
   for the later port; that reason went with the port on 1 October.
6. **Commodore 64**: settled 4 October 2026. Last of the four because the
   VIC-II and the SID are the hardest chips in the list, and because the
   KERNAL's rights need checking before its spec is written (open replacement
   ROMs exist).
7. From the family document, in roughly this order of ease: the VIC-20, the
   Apple I and II, the PET, the Atari 8-bit computers and the 5200, the Oric,
   the BBC Master (65C02), and then the arcade boards and the rest.

They are built one at a time, each with its own spec, design and plan.

The 65C02 machines run on the core already. Machines on a different CPU
(65C816, HuC6280, 65CE02) need a new core and are out of scope for now.

## The site

**6502.dbhq.uk**, a journal of the work and the home of the machines.

| Page | Shows | Source |
|---|---|---|
| Home | Hero, headline figures, the machines, the latest journal entries | Test results, the registry, the journal |
| Journal | Every entry, newest first, and a page per entry | `docs/journal/*.md`, read at build time |
| Machines | **A table of every 6502-family machine**, and a page for each machine that runs | `machines/registry.json` |
| Chips | A table of every 6502-family chip and what the core runs | The registry's chip data |
| Family | The account of the family | `docs/the-6502-family.md` |
| Status | The test breakdown, the speed check, and the known differences | Test results, `docs/known-differences.md` |
| About | What this is, the goal, licence, links | Written once in `site/` |
| Privacy | A link in the footer and the analytics notice (the consent choice, until 1 October 2026) to DBHQ's privacy policy at https://dbhq.uk/privacy/, which covers every DBHQ site | The estate's policy; no page of its own |

**The table of machines** lists the whole family, filterable by category,
core support and status, and sortable by every column. It works without
JavaScript as a plain table and gains sorting and filtering as an
enhancement. Its status column uses the registry's four values, and a running
machine links to its page.

**Journal entries** gain small front matter: a title, a date and a one-line
summary. The existing entries are updated in the same pull request.

### Headline figures

Home and Status show generated figures only: the number of machines
implemented, out of the number in scope; the variants of the core; the tests
passing; the Harte cases; and the speed as a multiple of a 2 MHz machine, from
the dated measurement. The first figure is the registry's count of `running`
machines, and the build fails if a running machine has no passing acceptance
test in `results.json`.

### How the figures reach the page

The deploy workflow runs the whole test suite with a results logger, and a
script turns the output into `results.json`: passed, failed and skipped, per
suite and per variant, with the commit, run id and date. The site build reads
that file for every figure. If the tests fail, nothing deploys.

The speed figures are local, one-machine measurements, so the benchmark tools
write a dated `measurements.json`, naming the machine, which is committed as a
record and shown with its date. It is refreshed by re-running the tool, never
by editing.

### Look and imagery

The look is decided: a phosphor terminal, after the Modal reference. Pure black
canvas, pale-green type, one lime accent used once per screen, hairline
borders, no shadows, and code-window cards for test output. Inter Tight for
headings, Inter for body text and Fira Mono for code, all open licence and
self-hosted. It is a **deliberate fork of the DBHQ brand**, green in place of
the brand blue, and `site/DESIGN.md` records that and the reasons, as
terraken's does.

Imagery is generated, at final quality, from three images: a chip die for the
hero, a set of logic-analyser waveforms behind the proof section, and a faint
circuit-trace texture. **Every generated image carries a caption saying it is
an illustration**, and none is used as evidence: the chip is a generic die, not
a photograph of the 6502. The exact prompts, model, quality, cost and date are
recorded in `site/DESIGN.md` so they can be regenerated. Originals are kept out
of git and the site ships WebP.

### Analytics and Search Console

The site follows the estate's settled pattern, recorded in `docs/reference/analytics.md`
in the private company repository, rather than inventing its own:

- **GA4 uses the estate's one measurement ID.** No new data stream. The site
  is separated in reports by the Hostname dimension.
- **Consent Mode v2, denied by default.** Nothing loads and no cookie is set
  until the visitor accepts. The choice is kept under the `dbhq-consent` key in
  `localStorage`, with the same three states as every other site. It is a
  modal, drawn to match the site. **Superseded on 1 October 2026: see the
  amendment below.** This is what the site launched with.
- **GA loads from an external `analytics.js`** and only on the live host,
  never on localhost or a preview. The Content-Security-Policy in `_headers`
  names the Google origins that need it, and nothing else.
- **No privacy page of its own.** The consent choice and the footer link to DBHQ's policy at https://dbhq.uk/privacy/, which says what is collected and that analytics runs unless the visitor opts out. One policy for the estate, so it cannot drift from the site it describes. (Amended 30 September 2026. The policy's analytics section is what the notice links since 1 October 2026.)
- **Search Console:** the domain property `sc-domain:dbhq.uk` already covers the
  host. A child property for `6502.dbhq.uk` is added for its own indexing view,
  its sitemap is submitted on the roll-up, and the service account is granted
  access on it in the Search Console interface, which the API cannot do.

Site tests check that GA does not load before consent, that the CSP names only
what is needed, that the measurement ID is the estate's, and that the sitemap
and `robots.txt` are present. (The first of those was replaced on 1 October
2026; see the amendment below.)

**Amended 1 October 2026: the site moved from an opt-in modal to the estate's
opt-out notice.** The site launched on 1 October 2026 on the pattern above, which
was the estate's pattern as it stood when the site was designed. On 30
September 2026 every other DBHQ site had moved to the new one: analytics on by
default, a non-modal notice and a simple opt-out, under the PECR
statistical-purposes exception. This site was built the same day and so launched
one step behind, out of line with the rest of the estate the next morning. Dan
decided it should match. What changed, and what did not:

- **Analytics runs by default on the live host,** for a visitor who has not
  opted out and is not a likely bot. Ad storage is denied, and Google Signals
  and ad personalisation are off in the tag, so the measurement stays statistics
  only. This is the line the exception depends on.
- **The choice is a cookie,** `dbhq_analytics=on|off`, on `Domain=dbhq.uk`, so
  opting out on one DBHQ site opts out on all of them. The `localStorage`
  `dbhq-consent` key was per origin and never did that. A choice left under the
  old prompt is honoured and moved to the cookie: `denied` becomes off and
  `granted` becomes on.
- **The notice is not a modal.** It informs, and offers OK and Opt out at the same
  weight. The footer's "Cookie settings" reopens it on every page and shows
  whether analytics is on or off.
- **Unchanged:** the one measurement ID, no data stream of its own, loading only
  on `6502.dbhq.uk`, the external `analytics.js`, and a Content-Security-Policy
  that allows no inline script and names the same three Google origins.
- **The tests** now run `analytics.js` and `consent.js` in a sandbox with a fake
  browser, instead of reading their text. Site tests check that an opted-out
  visitor loads nothing, that the ad signals stay denied, and that opting out is
  as easy as carrying on.

### Tests

`npm test` builds the site and fails on any of these:

- A figure that is not traced to `results.json` or `measurements.json`, a typed
  number on Home or Status, or a running machine with no passing acceptance
  test.
- A journal entry without its front matter, a machine in the family document
  that is missing from the registry, or a broken internal link.
- An en or em dash, or a forbidden name.
- A generated image without an "Illustration" caption or alt text.
- Text or the lime accent below the accessible contrast minimum, computed from
  the real colour tokens, or a `box-shadow` on a card.
- Invalid HTML, or a missing sitemap or `robots.txt`.

### Where it lives

- **Source:** `site/` in this repository: an Astro site with its own
  `package.json`, its tests and `DESIGN.md`. It sits beside the core because its
  checks read the core's test results and the registry, and across two
  repositories those checks cannot exist.
- **Infrastructure:** `infra/` in this repository: the Pages project `6502`, the
  custom domain `6502.dbhq.uk` and one DNS record in the `dbhq.uk` zone, with
  its state in its own R2 bucket. This is the `modem` and `bbs` layout, where
  the site, its Terraform and its deploy workflow sit together. (Amended 30
  September 2026. It was first drafted in the private `dbhq-uk/dbhq` repository
  on the `terraken` split, which exists because terraken's site tests read its
  Go source. Nothing here needs that, so the simpler layout won.)
- **Deploy:** `.github/workflows/deploy-site.yml` here. Its path filter is wide
  (`site/**`, `machines/**`, `docs/**`, `src/**`, `tests/**`) because a change
  to the core or a machine changes the figures.

## Order of work

1. **The site, first version.** Skeleton, tokens, components, the figures
   pipeline, the journal, the machines table and the family pages, the imagery,
   GA4 and consent, the tests, CI and deploy. It goes live with an honest
   "0 machines implemented" and every machine planned.
2. **The KIM-1**, as its own spec, plan and build. It is the first machine to
   count, and it sets the pattern for the shared browser host.
3. **Then each machine in turn**, one spec at a time.

## What needs Dan

Four steps touch credentials or the live domain, and are asked for at the time,
never assumed:

1. **A Cloudflare API token for this repository's deploy workflow**, scoped to
   Pages deploy and cache purge only, added as a GitHub Actions secret.
2. **The first `terraform apply` in `infra/`**, in the order HSTS forces:
   create the project, upload a real site, then publish the DNS record. `dbhq.uk`
   sends HSTS with `includeSubDomains`, so the hostname must serve valid HTTPS
   from its first request. Manual, never CI.
3. **Granting the service account on the new Search Console property**, one click
   in the interface.
4. **Adding the site to the estate's records** in the private repository:
   `hosting.md`, `analytics.md` and the GitHub metadata register.

## Decisions taken while writing this

| Decision | Chosen over | Why |
|---|---|---|
| The mission is as many machines as possible | Three named machines | Dan's call, 30 Sep |
| A machine counts only if it runs in the browser and passes a CI test | Any machine that boots; headless only | Dan's call, and it keeps the headline as strict as the core's own claims |
| A registry file is the source of the table and the count | Parsing the family document's tables | A data file is checkable; markdown tables are fragile |
| The family document stays hand-written and a test checks it against the registry | Generating the document from the registry | The document is prose as well as tables |
| The Electron, the Atari 2600, the NES and the Commodore 64 next, one at a time, smallest first | The NES first, keeping the published order; all four in parallel | Dan's call, 4 Oct. The count grows soonest and reviews come one at a time; four branches at once would all change the registry, the shared browser host and the site |
| Shared browser parts are extracted after the second machine | A framework designed up front | Two machines make the shared part visible; one does not |
| The look is a phosphor terminal, a fork of the brand | Paper like dbhq.uk; ink like skills.dbhq.uk | Dan's call, from the Modal reference; recorded in `site/DESIGN.md` |
| Generated imagery, captioned as illustration, never as evidence | Photographs of real parts | The site's claim is that nothing is overstated |
| GA4 on the estate's one measurement ID, with consent (opt-out notice since 1 October 2026) | A stream of its own; no analytics | Dan's call; the estate's rule, and it keeps sessions across subdomains |
| Site source here, infrastructure in the private repository | Everything in one repository | The terraken split, for the same coupling |

## Out of scope

The machines themselves (each gets its own spec), CPUs beyond the 65C02, a
search box or comments on the site, and any use of a generated image as
evidence.
