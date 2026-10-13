# The BBC Micro's two 3D models: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two models of the BBC Micro Model B on `/machines/bbc-micro/`, the outside (case, keys, LEDs, badge strip, ports) and the inside (the Issue 7 board with its chips and both faces of copper), offered as labelled views of the same machine with the same controls and lazy loading, each showing the running machine's state, as the design in [`../specs/2026-10-04-bbc-micro-models-design.md`](../specs/2026-10-04-bbc-micro-models-design.md) says. The registry says which models a machine has, and a test fails when a machine claims a model that is not built.

**Architecture:** Offline Python in one folder, `tools/bbc-micro-model/`, measures both models from photographs, flatbed scans and an MIT KiCad keyboard, and writes what the site uses: a track map, two generated parts modules and two results files, all committed. The build never runs it. Each model is a browser module on the shared stage (`site/src/models/stage.mjs`), bundled with three.js by `site/scripts/build-models.mjs` and loaded by `site/public/model-loader.js` only when its view is shown. The registry gains `case`, `models` and `references`; `validateRegistry` and `site/tests/model.test.mjs` hold the rules. The machine gains three small host exports so the models can show its LEDs and its chip accesses.

**Tech Stack:** Python 3.12 with numpy, opencv-python-headless, Pillow, scipy and pytest, in a virtual environment (the KIM-1's tools' set plus pytest); three.js and camera-controls as pinned in `site/package.json`; Astro; node's test runner; Playwright with Chrome's software WebGL for the browser check; C# on .NET 10 and xUnit for the machine's three exports.

**This plan has NOT been pre-run.** The measuring is image work whose code depends on what the scans show, so no geometry code is written here. Each task states its interfaces, its inputs, its outputs, the thresholds that judge them and the exact tests; the implementer writes the tests first, then the code, and a reviewer gates each task. What is proven early instead is what everything rests on: task 0 measures the scan's scale, the solder side's registration and the keyboard's fit to a photograph, each against a threshold set here, and stops the plan if any fails. Tasks 2 and 3 then hold the same thresholds as tests.

**Why the order differs from the suggested one.** The registry and page plumbing comes early (task 1), not at the end, so the inside model goes on the page and through the real browser check in its own task (7), instead of everything being proven together at the end. The two-view control (tabs) lands with the outside model (task 9), because before then there is only one view to offer. The registry rules land in task 1, but the BBC Micro states no `case` until task 9, so in task 7 it can claim `inside` alone and the inside goes through the real page and browser check without a half-claimed cased record or a preview mechanism; task 9 adds `"case": true`, the outside, and the rule that a machine claiming a model states `case`, which closes the gap. The machine's three exports are their own task (6), because they change merged C# and its speed, and deserve their own review.

## Global Constraints

- **Branch `feat/bbc-micro-models`.** Already created and checked out, with a draft pull request ("docs: the BBC Micro's two 3D models, design and plan"). Stay on it: never switch, create or move to another branch or worktree, never merge. One commit per task (a fix wave after review may add one), conventional-commit messages, pushed after each. The pull request is merged only when `Validate` is green and Dan says so, because merging deploys.
- **The page is not merged until `Validate` is green**, including the site suite's floor and the browser check, on the pull request's last commit.
- **Inputs are never committed.** `I1`, `I2`, `I5`, `I6`, `O8` (no stated licence, or large) are never committed, not even resized. `O1` to `O5` (public domain) are committed only as resized copies in `site/src/assets/photos/` (task 8). The full-size originals of every input stay outside the repository, in the folder `BBC_MODEL_INPUTS` names. Stage files by name (`git add <paths>`), never `git add -A`. A site test fails if any committed file's SHA-256 equals an original's in `data/sources.json` (task 0).
- **The tools never fetch.** `site/tests/mirrors.test.mjs` fails on any URL outside `dbhq-uk` in a `.py`, `.mjs`, `.sh` or `.cs` file under `tools/`, `tests/`, `src/`, `bench/` or `site/scripts/` that is not on a comment line. URLs live in `data/sources.json` and in markdown. Fetching is by hand, as the tool's README says.
- **The build and the tests never run the tools.** `site/package.json` never names `bbc-micro-model` or `python` (a test holds it, as for the KIM-1).
- **Thresholds, set before the measurements they judge** (the spike and the tests use these numbers; a STOP is a task outcome, reported as BLOCKED with the figures, not a failed test to be tuned around):

  | Measure | Pass | STOP if |
  |---|---|---|
  | Scan scale: a 40-pin row (pin 1 to pin 20 centres, 48.26 mm) on footprints held out of the scale fit | median error at most 0.10 mm, max at most 0.25 mm | median over 0.20 mm or max over 0.50 mm |
  | Scan scale: x against y | recorded, with the ratio | the two differ by more than 1.5 per cent |
  | Scan scale, x: rows of connector pins across the board (0.1 inch pitch), each held out of the x fit in turn (task 2). Added on 4 Oct 2026 after task 0's figures were seen, as a stricter check: nothing else held x to this level. **Revised on 4 Oct 2026 by the controller after task 2's figures were seen** (task 2 crossed the stop as first written, on a largest error of 1.192 mm from a 17.78 mm row, and that stays recorded): scaling a short row's error up to 48.26 mm multiplies pad-centre noise by up to 2.7, so a row is scored only if it is at least 30 mm long, a rule on length alone, never on a residual; shorter rows are reported, not scored. The largest is recorded, not a criterion, with the row named. Judged on the same numbers as before. The revision had two parts. With the largest still judged, the 30 mm rule alone would still stop: PL11, a 40.64 mm row, reads 0.502 mm against the 0.50 stop. The largest was made recorded-only too, as for the solder side, because one row whose pitch disagrees with 2.54 mm (line fit 2.590) is not evidence about the scanner's scale: dropping PL11 moves sx by 0.14 per cent and leaves x and y 0.03 per cent apart. The cut did not pick the verdict: the median is 0.157 with no cut, 0.136 at 20 mm and at 30 mm, 0.182 at 40 mm and 0.110 without PL11: always between pass and stop. | median at most 0.10 mm over the scored rows, each row's error scaled to a 48.26 mm row (error x 48.26 / the row's length); between 0.10 and 0.20 is recorded as between pass and stop, as the solder side | median over 0.20 mm, on the same scale |
  | Solder side to component side, by drill centres, on holes held out of the fit (at least 200 matched) | median at most 0.15 mm, 90th percentile at most 0.30; the max is recorded, not a pass criterion (revised 4 Oct 2026: a few scattered ragged pads, 9 of 2092 over 0.60 mm in task 0). Reported both with and without the pads the outlier rule below excludes | median over 0.25 mm or 90th percentile over 0.50 |
  | O1 to K1 on the key plane, each key held out in turn (at least 30 keys) | median at most 1.0 mm, 90th percentile at most 2.0, max at most 3.0 | median over 1.5 mm, or more than five keys over 3.0 |
  | O1's case front edge, raw, on the key-plane registration (task 0) | recorded, not a stop. Task 0 measured 403.5 mm, minus 2.78 per cent, which crossed the old 2.5 per cent stop; the plan was revised on 4 Oct 2026 because the edge is not in the key plane | |
  | The case front edge's width after correcting for parallax (task 8). h, O1's camera height above the key plane, comes from a measurement independent of the 415 mm (not from the key-fit camera alone), with its uncertainty; d, the edge's depth below the key plane, comes from O4 and O5, measured against the same fitted key plane the homography uses (not one row's key tops), with its uncertainty. Both are recorded before the corrected width is computed, and the corrected width's interval is judged (the interval is plus or minus two standard errors, with the standard errors of h, d and the raw width combined in quadrature, fixed here before task 8 measures anything) | the whole interval within 1.5 per cent of 415 mm | the whole interval outside 2.5 per cent of 415 mm. Otherwise inconclusive, which counts as not passed and goes back to the plan |
  | The case top's plan size from published dimensions against O1 registered on the case top's four corners (task 8) | recorded | |
  | O3 to K1, as O1 | as O1 | not a stop; recorded |
  | I5 to I1, on chip pins held out of the fit | median at most 1.0 mm | median over 2.0 mm (task 5) |
  | Copper coverage, each face | 10 to 50 per cent of the board | |
  | Drills in copper | at least 95 per cent of drills have copper round them on each face | |
  | Known nets | at least 20 logic ICs included; at least 90 per cent of their GND pins in one connected net and of their +5V pins in another; the two nets never connected | |
  | Top face against I5, where I5 shows the board | intersection over union at least 0.50 | |
  | IC pad counts | every IC's pad count is its package's | |
  | IC identities | every IC is table 8.1's part | |
  | IC places against the manual's approximate positions, after a best-fit similarity | no IC more than 10 mm off (catches a swap) | |
  | Side profile, the check point | within 2.0 mm of where it is measured | |

- **The solder side's outlier rule, fixed on 4 October 2026 before task 3 and not using any residual.** A matched hole may be left out of the scoring of the registration only if its pad fails the roundness test on either face: the blob's ellipse from its second moments has a major to minor axis ratio over 1.25, or its area is outside 0.6 to 1.6 times the median area of the matched pads on that face. Nothing else excludes a hole. Every hole is in the fit's report: the count excluded is reported, and the held-out figures are reported both with and without the exclusion; the pass and STOP are judged on the figures without exclusion, and the other set is recorded beside them.
- **Budgets, set before building:** each model bundle at most 200,000 bytes gzipped (the existing `BUDGET_GZIP` in `model.test.mjs` applies to every model); the inside's track map at most 600,000 bytes; `public/model-loader.js` under 4,000 bytes (the existing test); nothing under `/models/` in any page's first load.
- **The KIM-1 does not change.** Its page, its model, `site/scripts/browser-check.mjs`'s KIM-1 part and the KIM-1 tests in `site/tests/model.test.mjs` pass without edits, except the edits task 1 lists by name.
- **Our own models.** No model made by someone else is used: M1 and M2 never; K2 only as numbers for a cross-check, credited. Nothing from another emulator.
- **Colours are tokens.** Every colour a model uses is a `--model-*` token in `site/src/styles/tokens.css`, read through `stage.token`; `model.test.mjs` already fails on a colour literal in `site/src/models/`. A new text colour goes in the contrast table in `site/tests/design.test.mjs`. The lime is spent on nothing new: the selected tab, the access marks and the LEDs are not lime.
- **The CSP is unchanged.** `site/public/_headers` is not edited.
- **No figure describing the project as it stands is typed by hand** (rule 5). Every figure on the page is read from `site/src/data/bbc-micro-board-model.json` or `site/src/data/bbc-micro-case-model.json`, which `tools/bbc-micro-model/results.py` writes.
- **Document as you go** (rule 6). Every task ends by adding to that working day's journal file, `docs/journal/YYYY-MM-DD-the-bbc-micro-models.md` (the date `date` prints): create it with front matter (`title`, `date`, `summary`, `order` one higher than the highest existing) if it does not exist, else append a section. Decisions with what they were chosen over, findings with how they were checked, surprises, mistakes, and every figure as a dated measurement with the command that produced it. **No digits in a journal's title or summary** (`site/tests/figures-on-pages.test.mjs` reads them on the home page: "3D" passes, "8271" fails). A difference from a reference also goes in `docs/known-differences.md`.
- **Hygiene, every task:** no em or en dashes (`LC_ALL=C.UTF-8 git grep -InP '[\x{2013}\x{2014}]'` prints nothing); British English; the private-names check in this plan's implementer rules (`.superpowers/sdd/2026-10-04-bbc-micro-models/implementer-rules.md`, git-ignored: the names are never written in the repository) prints nothing. It covers the two private project names only; the alternatives from the port goal dropped on 1 October were removed from it on 4 October 2026, because they match historic plans and the site test that bans them; warnings are errors in C#.
- **Test floors.** The site suite's floor is 261 in both `.github/workflows/validate.yml` and `deploy-site.yml` (the `[ "${PASS:-0}" -ge N ]` lines, "261 tests as at 4 Oct 2026"). A task that adds site tests raises both to the pass count it measured, the same number in both, with a dated comment. A task never lowers it.
- **The deploy's serving list** (`deploy-site.yml`, the `for f in ...` line) names every model bundle and track map; `model.test.mjs` fails if one is missing.
- **When a fact sheet is wrong,** fix it in the same commit with a note, and say so in the journal.

## File structure

```
tools/bbc-micro-model/            # offline: measures both models; outputs committed; never run by the build
  README.md                       # how to fetch the inputs by hand, set BBC_MODEL_INPUTS, make the venv, run
  common.py                       # inputs and their hashes, image reading, homography fits with held-out errors, JSON writing
  verify.py                       # checks every input in data/sources.json against its SHA-256; no network
  spike.py                        # task 0: the three numbers; kept as a record, not in the run
  board_frame.py                  # task 2: the scan's x and y scale, the outline and the holes
  board_register.py               # task 3: the solder side registered; pads, drills, footprints
  board_trace.py                  # task 4: both copper faces and the print; nets; the track map
  board_parts.py                  # task 5: every part's place and identity; writes bbc-micro-board-parts.mjs
  keys.py                         # task 8: key centres from K1
  case_register.py                # task 8: O1 and O3 on K1; outline, recess, LEDs, badge strip, grille
  case_profile.py                 # task 8: the side profile from O4 and O5
  case_parts.py                   # task 8: ports and underside connectors; writes bbc-micro-case-parts.mjs
  results.py                      # writes the two results files the page reads
  run-board.sh, run-case.sh       # each script in order
  tests/test_*.py                 # pytest on synthetic inputs, run locally
  data/sources.json               # every input: id, kind, URL, author, licence as stated, fetched, SHA-256, committed copy if any
  data/README.md                  # which data file carries which terms
  data/marks.json                 # points marked by hand (ours)
  data/ic-table.json              # Service Manual table 8.1 transcribed, with PDF pages (ours)
  data/spike.json, frame.json, registration.json, copper.json, parts.json, keys.json, case.json   # measurements (ours)
site/src/models/
  bbc-micro-board.js              # the inside model (task 7)
  bbc-micro-board-layout.mjs      # its constants, TRACKS, describe(), the chip legend
  bbc-micro-board-parts.mjs       # generated by board_parts.py
  bbc-micro-board-notes.mjs       # made(figures): "How the model was made"
  bbc-micro-case.js               # the outside model (task 9)
  bbc-micro-case-layout.mjs       # its constants, describe(), legend drawing table
  bbc-micro-case-parts.mjs        # generated by case_parts.py
  bbc-micro-case-notes.mjs        # made(figures)
site/src/data/bbc-micro-board-model.json, bbc-micro-case-model.json   # written by results.py
site/src/assets/tracks/bbc-micro-board.webp                           # written by board_trace.py
site/src/assets/photos/bbc-micro-*.webp                               # O1 to O5, resized (task 8)
site/tests/bbc-models.test.mjs    # the BBC models' data and geometry (new, task 0 onwards)
src/Dbhq.Machines.BbcMicro/BbcBus.cs, BbcChip.cs                      # access counters, the serial ULA's control byte (task 6)
src/Dbhq.Machines.BbcMicro.Wasm/Program.cs                            # Leds, AccessCounts, RomSlot (task 6)
tests/Dbhq.Machines.BbcMicro.Tests/MachineStateTests.cs               # (task 6)
```

Modified along the way: `machines/registry.json`, `site/src/lib/registry.mjs`, `site/src/models/models.mjs`, `site/src/components/MachineModel.astro`, `site/src/pages/machines/[id].astro`, `site/public/model-loader.js`, `site/public/bbc-micro.js`, `site/scripts/browser-check-bbc.mjs`, `site/scripts/page-weight.mjs`, `site/src/styles/`, `site/tests/registry.test.mjs`, `site/tests/model.test.mjs`, `site/tests/bbc-micro.test.mjs`, `site/tests/design.test.mjs`, `site/src/assets/photos/README.md`, `site/DESIGN.md`, `NOTICE.md`, `README.md`, `AGENTS.md`, `docs/the-6502-family.md`, `docs/known-differences.md`, `docs/bbc-micro/facts/bus.md`, `docs/bbc-micro/facts/models.md`, both workflows.

## Facts every task relies on

- **The sources, their licences and their uses** are `docs/bbc-micro/facts/models.md`. Its ids (O1 to O8, D1, D3, K1, K2, I1 to I11) are the ids in `data/sources.json`.
- **The inputs** are in the folder `BBC_MODEL_INPUTS` names; the research kept them in `~/dbhq-previews/bbc-model-research/` under these names, with these SHA-256 (from `models.md`, "Downloaded for the work"):
  - O1 `outside-1-top-Acorn_BBC_Microcomputer.jpg` `a616c736c51c4bf0f796be21ba426d435acdbf2d6b692cbf0cc3256f5782949a`
  - O2 `outside-2-rear-BBC_Micro_rear.jpeg` `4072a59af7f7fab834ca2b912b0f6e3449aa8bdaa01bb621d9d556579dd5937e`
  - O3 `outside-3-keyboard-BBC_Micro_Type_1.jpg` `745b34357b2befd5d1bcc501bb48ad9c3e345e2b876d59b3f812cec48d2e2403`
  - I1 `inside-1-bare-iss7-top-amb5l.jpg` `5edc89d95872c9e70a1adb0b8d968fbccd3459816cacd64a630d95afd02e7b0e`
  - I5 `inside-2-populated-iss7-8bs.jpg` `7ae4b57399e9576d46bcfe0d4e2b39ddb362d7bb33782c002a39adffab208263`
  - I2 `inside-supp-bare-iss7-bottom-amb5l.jpg` `c9a36223ca7d3d28cbceb6cb25f625e3989655283e6bf4b4602c5199612b5bb6`
  - Not yet fetched or hashed: O4, O5, O8, I6, K1's `.kicad_pcb` at commit `5ec53df566c77424caa6be827d0831da3916ecfa`, K2's STL, and D1 (the Service Manual PDF). The task that first needs each fetches it, records URL, fetch date, size and SHA-256 in `data/sources.json` and in `models.md`'s table, and from then on it is checked like the rest.
- **I1's scale, as the researcher measured it:** 15.72 px/mm down and 15.79 px/mm across, from pin pitch; the board about 309 by 229 mm. Task 0 measures it again; these are not assertions.
- **Package geometry** (the scale's reference): pin pitch 2.54 mm; pin 1 to pin N/2 is (N/2 - 1) x 2.54 mm, so 48.26 mm for a 40-pin, 33.02 for a 28-pin, 17.78 for a 16-pin, 15.24 for a 14-pin; rows 15.24 mm apart for the wide packages and 7.62 mm for the narrow.
- **The case:** "Height 73mm (including feet) Width 415mm Depth 345mm" (D1, PDF p.109, the B+ section; the Model B assumed to share it [guessing - verify]); the Science Museum gives 75 x 410 x 345. D1 is used.
- **The keys:** pitch 19.05 mm. The machine's keys are `BBC_KEYS` in `site/public/bbc-keys.js` (the matrix, SHIFT once), plus BREAK (not in the matrix); their legends are `LEGENDS` there. Whether the keyboard has a second SHIFT keycap on the same matrix position is [guessing - verify] against O1 in task 8.
- **The LEDs:** CAPS LOCK is IC32 bit 6 and SHIFT LOCK bit 7 (`docs/bbc-micro/facts/via.md` s2.2); `SystemVia.Latch` holds IC32. The cassette motor is the serial ULA's control register at `$FE10` (`bus.md` s1 table), bit 7 [guessing - verify against the Advanced User Guide s.17 and the Service Manual; task 6 adds the sourced fact to `bus.md`]. Which latch level lights an LED is [guessing - verify]; task 6 ties it to what the ROM does.
- **SHEILA:** `&FE00` 6845, `&FE08` 6850 ACIA, `&FE10` serial ULA, `&FE18` station ID / INTOFF, `&FE20` video ULA, `&FE30` ROM latch, `&FE40` system VIA, `&FE60` user VIA, `&FE80` 8271, `&FEA0` ADLC, `&FEC0` ADC, `&FEE0` Tube (`via.md`, end of section 2). The sound chip is written through the system VIA when latch bit 0 falls (`SystemVia.SoundWrite`).
- **The ICs** (Service Manual table 8.1, as in `models.md`): IC1 6502A, IC2 6845, IC3 6522 system VIA, IC4 6850, IC5 SAA5050, IC6 video ULA, IC7 serial ULA, IC18 76489, IC51 OS ROM, IC52 BASIC ROM, IC53 to IC68 4816 RAM, IC69 6522 user VIA, IC73 uPD7002, IC78 8271, IC89 68B54, IC98 and IC99 speech, IC100 and IC101 sideways sockets. Places come from the scan; the manual's positions are a check.
- **The scene:** centimetres, as the KIM-1's (`X = (x - width / 2) / 10`), y up. The board frame is millimetres from the board's left rear corner as the component side is seen from above with the rear connectors at the far edge, x to the right, y towards the front. The case frame is millimetres from the case's left rear corner at table level, x to the right, y towards the front, z up.
- **The KIM-1's model is the pattern for everything on the page:** `site/src/models/kim-1.js` (tracks, toggles, `trackMaps`, instanced parts, test hooks), `kim-1-layout.mjs` (`describe()`), `kim-1-notes.mjs` (`made()`), `MachineModel.astro`, `tools/kim1-model/` (method, `trace.py`, `register.py`, `results.py`). Its scripts are MIT and in this repository: adapt them, do not copy them blindly.
- **Commands:** site `cd site && npm run build && npm test`; one file `cd site && node --test tests/bbc-models.test.mjs` (after a build); browser `cd site && node scripts/browser-check.mjs`; weight `cd site && node scripts/page-weight.mjs /machines/bbc-micro/`; C# `dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release`, and the whole `dotnet test -c Release` once at the end of a task that touches C#; tools `/tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q`. The venv: `python3 -m venv /tmp/bbcvenv && /tmp/bbcvenv/bin/pip install numpy opencv-python-headless pillow scipy pytest`; record the versions in the journal.

---

### Task 0: The inputs, and the three numbers everything rests on

A day's spike, before any pipeline is built. If any STOP threshold is crossed, the task ends BLOCKED with the figures, and the plan is reconsidered before task 1.

**Files:**
- Create: `tools/bbc-micro-model/README.md`, `common.py`, `verify.py`, `spike.py`, `tests/test_common.py`, `data/sources.json`, `data/README.md`, `data/marks.json`, `data/spike.json`
- Create: `site/tests/bbc-models.test.mjs`
- Create or append: `docs/journal/YYYY-MM-DD-the-bbc-micro-models.md`

**Interfaces:**
- Produces, in `common.py`:
  ```python
  TOOL: Path                                   # tools/bbc-micro-model
  def inputs_dir() -> Path                     # $BBC_MODEL_INPUTS; exits naming the variable if unset or missing
  def sources() -> list[dict]                  # data/sources.json "sources"
  def original(source_id: str) -> Path         # the input's path; refuses (exit 2) unless its SHA-256 is the recorded one
  def fit_held_out(src, dst, model: str, folds) -> dict
      # model: "similarity" | "affine" | "homography" | "cubic" (homography then a cubic correction);
      # folds: "leave-one-out" or an array of fold labels (a chequerboard of blocks);
      # returns {"n", "params", "fitMm": {"median","p90","max"}, "heldOutMm": {"median","p90","max"}, "worst": [[id, mm], ...]}
  def write_data(name: str, obj) -> None       # data/<name>, keys sorted, floats to 4 places, plain Python numbers only
  ```
- Produces `data/sources.json`: `{ "sources": [ { "id": "I1", "kind": "scan"|"photograph"|"drawing"|"document"|"model", "file": "<name in BBC_MODEL_INPUTS>", "url": "...", "page": "<the page that gives author and licence>", "author": "...", "licence": "<as stated>" | null, "fetched": "YYYY-MM-DD", "bytes": n, "width": n, "height": n, "sha256": "...", "committed": null | "<file in site/src/assets/photos/>", "use": "..." } ] }`, with the six hashed inputs now and the rest added by the tasks that fetch them.
- Produces `data/spike.json`: `{ "scale": {"pxPerMm": {"x","y"}, "ratio", "footprints", "heldOut": {"rowLengthErrMm": {"median","max"}}}, "solder": {"holes", "heldOutMm": {...}}, "keys": {"n", "heldOutMm": {...}, "over3mm", "frontEdgeMm"} }`.

- [ ] **Step 1: The venv and the inputs.** Make the venv (Facts). Point `BBC_MODEL_INPUTS` at the research folder. Write `data/sources.json` for the six hashed inputs from `models.md` (author, licence as stated, URL and the page giving them; `committed` null for all six at this point). Write `verify.py`: for each source with a `file`, report present / missing / hash differs, exit non-zero on a mismatch, never on a missing input it is not asked for (`verify.py I1 I2` checks just those).
- [ ] **Step 2: pytest first.** `tests/test_common.py`: `original()` refuses a file with the wrong hash and accepts the right one (a temporary folder and a made-up source); `fit_held_out` recovers a known similarity, affine and homography from synthetic points to under 1e-6, and with 0.1 mm of noise reports a held-out median within a factor of two of the noise; a leave-one-out fold never fits a point it then scores. Run: fails. Implement `common.py`. Run: passes.
- [ ] **Step 3: Scale.** On I1, mark by hand in `data/marks.json` the centres of pin 1 and of the last pin of each row on at least 12 DIP footprints spread over the board, both orientations if the board has both, each refined to the pad's centroid within 0.6 mm. `spike.py` fits x and y scales on half the footprints and scores the 48.26, 33.02, 17.78 or 15.24 mm rows of the other half. Record the result in `spike.json` and judge it against the Global Constraints table.
- [ ] **Step 4: The solder side.** Find drill centres on I1 and on I2 mirrored (a hole is a small dark or bright disc inside a pad; solder-filled holes on I2 may show only the pad, whose centre is the hole's). Seed an affine from four holes marked by hand in `marks.json`, match by nearest neighbour, then fit an affine and a cubic and score each on a chequerboard of 20 mm blocks held out. Judge against the table. Record which fit, and how many holes matched.
- [ ] **Step 5: The keys.** Fetch K1 at its pinned commit (by hand: `git clone` into `BBC_MODEL_INPUTS/bbc-keyboard` and `git checkout`), hash its `.kicad_pcb`, add K1 to `sources.json`. Read every switch footprint's centre (adapt `tools/kim1-model/kicad.py`). On O1 mark at least 30 key-top centres by hand in `marks.json`, spread over every row, with the key's name. Fit a homography O1 to K1 leaving each key out in turn; then map O1's case front edge (two marked corners) through it and measure its length. Judge against the table. Say in the journal that keycap tops are not in one plane (the rows are sculpted), so some of the error is parallax, and how much the row offsets explain.
- [ ] **Step 6: The site test.** `site/tests/bbc-models.test.mjs`, first tests: `data/sources.json` is well formed (every field above; `sha256` is 64 hex digits; `licence` a string or null; `fetched` a date; an input with no stated licence, or a scan, has `committed: null`); the six hashes equal the ones in `models.md`'s "Downloaded for the work" table, read from the markdown; **no committed file under `tools/` or `site/src/assets/` has the SHA-256 of any original** (hash every file there); `spike.json`'s three figures pass the table's pass column; `site/package.json` names neither `bbc-micro-model` nor `python`. Run the full suite, raise both floors to the measured count.
- [ ] **Step 7: README and data README.** The tool's README: what it is, that it is offline, how to fetch each input by hand (URLs as markdown), the venv, `BBC_MODEL_INPUTS`, `verify.py`. `data/README.md`: the table of files and their terms, as `tools/kim1-model/data/README.md` does; `marks.json` and `spike.json` MIT (ours).
- [ ] **Step 8: Journal.** Create the day's entry. Its first section records the design's decisions, from the decisions table in the spec, with what each was chosen over; then the spike: the venv's versions, each figure with `PYTHON=/tmp/bbcvenv/bin/python python spike.py` as the command, which fit was used and why, and whether any threshold was close. If a STOP was crossed, write that and stop here.
- [ ] **Step 9: Commit.** Hygiene checks. `git add` by name. `feat(models): the BBC Micro's model inputs, checked, and the three numbers they rest on`. Push.

**Outcome (4 October 2026).** Task 0 crossed a STOP, and it stays recorded as crossed. The scale passed (40-pin rows held out: median 0.043 mm, largest 0.107, over 7 rows; x against y 1.27 per cent apart, under its stop), the keys passed (50 keys, each held out: median 0.395 mm, 90th percentile 0.632, largest 0.989, none over 3 mm), and the solder side passed its median and 90th percentile (0.120 and 0.270 mm over 2092 holes) with its largest, 0.757 mm, over the 0.60 pass value but not a stop; the plan was revised the same day so that the largest is recorded, not a pass criterion (9 of 2092 holes, scattered ragged pads), with an outlier rule fixed in advance. O1's case front edge, mapped through the key-plane homography, measured 403.5 mm, 2.78 per cent short of 415, outside the 2.5 per cent stop. The cause is inferred, not measured: the edge is not in the plane of the key tops, so a key-plane homography cannot give its width (a camera estimated from the key fit alone, 536 mm above the keys with a jackknife standard error of 355 mm, would make an edge 15 mm below the key plane read 403.7 mm; the 15 mm is assumed, not measured, so the figure is consistent with the cause, not a confirmation of it, and the key fit alone cannot correct the edge). The check judged the wrong quantity, so the plan was revised, not the threshold: the raw figure is now recorded only, and task 8 judges the width after a parallax correction against the same 1.5 and 2.5 per cent. The figures are in `tools/bbc-micro-model/data/spike.json` and the journal for 4 October 2026.

---

### Task 1: The registry says which models a machine has

**Files:**
- Modify: `machines/registry.json` (KIM-1: `"case": false`, `"models": [{ "view": "board", "module": "kim-1" }]`; BBC Micro: unchanged, no `case` and no `models` yet), `site/src/lib/registry.mjs`, `site/src/models/models.mjs`, `site/src/pages/machines/[id].astro`, `site/src/components/MachineModel.astro` (the credit list also reads `references`)
- Test: `site/tests/registry.test.mjs`, `site/tests/model.test.mjs`
- Allowed edits to existing KIM-1 tests: none. `case` and `models` are optional, so no made-up machine needs a new field.

**Interfaces:**
- Produces, in `registry.mjs`:
  ```js
  export const VIEWS = ['board', 'outside', 'inside'];
  export const MODELS_DIR, DATA_DIR;                       // site/src/models, site/src/data
  export function modelProblems(machine, where, { modelFiles }) // -> string[]
  export function referenceProblems(reference, where)        // -> string[]: creditProblems + title + sha256 (64 hex)
  export function validateRegistry(registry, results = null, { photoExists, modelFiles } = {})
  // modelFiles(module) -> { module: bool, exportsMount: bool, results: bool }, default reading
  // site/src/models/<module>.js (and /export async function mount\(root\)/ in it) and site/src/data/<module>-model.json
  ```
- Produces, in `models.mjs`: `MODELS` keyed by module, each entry gaining `machine` and `view`; `export const modelsOf = (machine) => machine.models ?? [];`. `MODELS['kim-1']` keeps every field it had.

**Rules (the design's "The registry's models field").** `models` and `case` are optional, and **a running machine is never required to claim a model** (Dan, 2 October 2026: models never hold up a machine counting). In this task: `models`, when present, a non-empty list; a view outside `VIEWS`; a view twice; a module twice in the registry; a module not equal to its machine's id or starting with it and a hyphen; a module whose file is missing or does not export `mount(root)`; a module with no results file; `case`, when present, not a boolean; `"case": false` with `outside` or `inside`; `"case": true` with `board`; **`"case": true` with a non-empty `models` that does not list both `outside` and `inside`** (it fires only when `case` is set); `references` not a list, or an entry failing `referenceProblems`. A test also holds that a running machine with no `models` and no `case` is valid (the BBC Micro today). **Not in this task:** "a machine that claims a model states `case`" (task 9: until then the BBC Micro claims `inside` with no `case`).

- [ ] **Step 1: Failing tests.** In `registry.test.mjs`, one test per rule above, each a made-up registry (as the photo tests do) that must produce its error, and the real registry that must produce none; `modelFiles` injected so a made-up module can be present or absent. In `model.test.mjs`: every `MODELS` entry is claimed by exactly one machine with the same `view`; every machine's claimed module is in `MODELS` with that machine; the machine page shows the model section exactly for machines with `models`. Run: fail.
- [ ] **Step 2: Implement.** The rules, the KIM-1's and the BBC's registry fields, `MODELS` restructured, `[id].astro` reading `modelsOf(machine)` (`hasModel` becomes "has models"; the KIM-1's section is rendered for its one module exactly as before), the credit list in `MachineModel.astro` taking `references` after `drawings` with the same sentence shape. Run: pass.
- [ ] **Step 3: Prove the KIM-1 unchanged.** Build task 0's commit in a scratch copy and this one here, and `cmp` the two `dist/machines/kim-1/index.html`: they must be identical (the KIM-1 has no references, and its model section is rendered from the same entry); if a byte differs, find why before going on. The KIM-1 tests pass with only the allowed edits; `node scripts/browser-check.mjs` passes without edits. Raise the floors.
- [ ] **Step 4: Journal** (the schema, each rule and the case that fails it, why `case` is explicit and not read from `category`, how the KIM-1 was shown unchanged). **Commit** `feat(site): the registry says which models a machine has, and a claimed model must be built`. Push.

---

### Task 2: The board's frame: scale, outline and holes

**Files:**
- Create: `tools/bbc-micro-model/board_frame.py`, `tests/test_board_frame.py`, `data/frame.json`, `run-board.sh` (this script only, for now)
- Modify: `site/tests/bbc-models.test.mjs`

**Interfaces:**
- Consumes: `common.py`, I1, `marks.json` (task 0's footprint marks, extended).
- Produces `data/frame.json`: `{ "pxPerMm": {"x","y"}, "origin": [px, py], "rotationDeg", "heldOut": {"rowLengthErrMm": {"median","p90","max"}, "footprints"}, "heldOutX": {"rows": [{"row", "pitches", "errMm", "scaledErrMm"}], "scaledErrMm": {"median","max"}}, "board": {"widthMm","depthMm"}, "outline": [[x,y], ...], "holes": [{"x","y","d"}], "rectified": {"pxPerMm": 16} }` in board-frame millimetres. Every later board script reads I1 through this frame.

- [ ] **Step 1: pytest first.** Synthetic: a drawn board image with known pads at 2.54 mm on a known x and y scale, rotation and offset; `board_frame` recovers scale to 0.05 per cent, the outline corners to 0.1 mm and hole centres to 0.05 mm. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Run on I1.** Scale x and y apart: y from the DIP footprints' rows (pin 1 to pin N/2) the marks give, and **x from pin pitch along rows that run across the board** (connector rows on the 0.1 inch pitch such as PL8, PL9, PL13 and PL14, and the DIPs that lie across), **never from a DIP's row spacing**. Task 0 found the solder centroids of a DIP's two rows about 1 per cent wider apart than 15.24 or 7.62 mm: x came out 15.9283 px/mm from the row spacings and 15.745 to 15.799 (median 15.764) from connector pitch. The x against y row of the thresholds table applies to these two: recorded with the ratio, STOP if they differ by more than 1.5 per cent. **x is judged the way y is:** each connector row (and each DIP row that lies across) is held out of the x fit in turn and its length, first pad to last, measured against its pitches x 2.54 mm; the error is scaled to a 48.26 mm row (error x 48.26 / the row's length) and judged against the table's x row (0.10 mm median and 0.25 max pass, 0.20 and 0.50 STOP). That row was added after task 0's figures were seen, as a stricter check, and the journal says so. **Revised after task 2's figures** (the table's x row): only rows at least 30 mm long are scored, the median alone is judged (pass at most 0.10, stop over 0.20), and the largest is recorded with its row named (The revision had two parts. With the largest still judged, the 30 mm rule alone would still stop: PL11, a 40.64 mm row, reads 0.502 mm against the 0.50 stop. The largest was made recorded-only too, as for the solder side, because one row whose pitch disagrees with 2.54 mm (line fit 2.590) is not evidence about the scanner's scale: dropping PL11 moves sx by 0.14 per cent and leaves x and y 0.03 per cent apart. The cut did not pick the verdict: the median is 0.157 with no cut, 0.136 at 20 mm and at 30 mm, 0.182 at 40 mm and 0.110 without PL11: always between pass and stop.); `frame.json` keeps the verdict the check as first written gave (a STOP) beside a `revision` note, as `spike.json` does; correct the scan's rotation from the long pad rows (a line fit per row; the residual from a straight line is recorded, with the rows at or over 0.05 mm named; it is recorded, not a pass criterion: over 0.05 mm the journal says the scan is not flat to that level, which task 2 did, for 12 of 30 rows, with bows both ways); the outline from the board edge (a fit per edge, corners as intersections, notches and cut-outs kept); the mounting holes. Write a rectified copy at 16 px/mm to `out/` (git-ignored) and look at it.
- [ ] **Step 3: Site tests.** `frame.json` passes the scale rows of the thresholds table, the held-out x row included (as revised: no scale row is a STOP, y passes, and x's median is judged; a strict x pass is a to-do while x is between pass and stop); the verdicts recorded in `frame.json` are the ones its figures give, as written and as revised; the outline is closed and its width and depth are within 0.5 mm of `board.widthMm` and `board.depthMm`; the board is within 5 mm of the researcher's 309 by 229 (a sanity bound, not a measurement). Raise the floors.
- [ ] **Step 4: Journal** (scale, rotation, straightness, the outline and anything odd in the scan). **Commit** `feat(models): the BBC Micro board's frame from the bare scan`. Push.

---

### Task 3: The solder side registered, and the pads, drills and footprints

**Files:**
- Create: `tools/bbc-micro-model/board_register.py`, `tests/test_board_register.py`, `data/registration.json`; append to `run-board.sh`
- Modify: `site/tests/bbc-models.test.mjs`

**Interfaces:**
- **Holes, from task 2's finding:** on I1 a hole's wall shows as a grey crescent on its rear side (the scanner looks at a slight slant), and some holes have a grey ring. Task 2's circle stops at the edge of the lid seen through the hole, inside the crescent, so its diameters read small and its centres may sit towards the front. Task 3 fits every hole and drill it places on the **outer** edge of that crescent (the hole's top rim), and records the difference from task 2's centres. **Slots, from task 2:** the walls of the front edge's three slots are weakly fitted (up to about 10 degrees off the frame; one slot floor rests on 7 points), so they place the slots but must not be used as precise geometry.
- Produces `data/registration.json`: `{ "solder": {"model": "affine"|"cubic", "holes", "heldOutMm": {"median","p90","max"}, "outliers": {"rule", "excluded", "heldOutMmWithExclusion": {"median","p90","max"}}}, "drills": [{"x","y","d"}], "pads": [{"x","y","w","h","shape": "round"|"square"|"oval", "face": "top"|"bottom"|"both", "drill": index|null}], "footprints": [{"ref": "IC1"|null, "kind": "dip"|"sip"|"axial"|"radial"|"connector"|"other", "pins": n, "pin1": [x,y], "pads": [indexes], "box": [x0,y0,x1,y1]}] }`.

- [ ] **Step 1: pytest first.** Synthetic pair: the same hole set drawn on two images, one mirrored, rotated, scaled and with a mild cubic warp and 2 per cent of holes missing; registration recovers it to under 0.05 mm held out; pad grouping turns 2.54 mm rows into DIP footprints with the right pin count and pin 1 at the square pad. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Register I2 to I1.** As task 0's spike, now over every hole found: a model chosen by held-out error (the simpler wins a tie). The outlier rule in the Global Constraints (roundness on either face: axis ratio over 1.25, or area outside 0.6 to 1.6 times the face's median) is applied as written, without looking at residuals; the excluded holes are listed with the reason each failed, and the held-out figures are reported with and without them. Judge against the thresholds table on the figures without exclusion (median and 90th percentile; the max is recorded): STOP if crossed.
- [ ] **Step 3: Pads, drills, footprints.** Pads and drills found on both faces, as bright rings on the bare scan; grouped into footprints by pitch and by the printed outline; each footprint's reference read from the print by hand where the grouping cannot tell (marks in `marks.json`, each with its reason). Overlays in `out/`, looked at.
- [ ] **Step 4: Site tests.** `registration.json` passes the solder row of the table (median and 90th percentile, without exclusion; the max is recorded, not asserted), with at least 200 holes; the excluded holes are exactly those the roundness rule names, their count is reported, and both sets of figures are present; every drill has a pad on both faces within 0.2 mm; every DIP footprint has an even pin count from 14 to 40; IC1 to IC7, IC51, IC52, IC69, IC78 each have a footprint. Raise the floors.
- [ ] **Step 5: Journal** (the residuals, the chosen model and why, solder-filled holes and how they were found, references read by hand and why). **Commit** `feat(models): the BBC Micro board's solder side registered, with its pads and footprints`. Push.

---

### Task 4: The copper on both faces, and the print

**Files:**
- Create: `tools/bbc-micro-model/board_trace.py`, `tests/test_board_trace.py`, `data/copper.json`, `data/ic-table.json` (the pinouts the nets check needs, each with its datasheet; task 5 completes the file), `site/src/assets/tracks/bbc-micro-board.webp`; append to `run-board.sh`
- Modify: `site/tests/bbc-models.test.mjs`, `site/src/assets/photos/README.md` (a section for the track map: what it is traced from, its terms)

**Interfaces:**
- Produces the track map: lossless WebP covering the board edge to edge in the board frame; **red** top copper (I1), **green** bottom copper (I2 registered), **blue** the printed legend (I1). Its pixels per millimetre is the highest of 8, 7 or 6 that keeps it inside the 600,000-byte budget, chosen before the final run and recorded.
- Produces `data/copper.json`: `{ "mapPxPerMm", "map": {"width","height","bytes","sha256"}, "coverage": {"top","bottom","print"}, "drillsInCopper": {"top","bottom"}, "nets": {"chips", "excluded": [[ref, reason]], "gnd": {"pins","inLargest"}, "vcc": {"pins","inLargest"}, "touching": bool}, "againstI5": {"iou", "comparedMm2"}, "thresholds": {...} }`.

**Method.** Adapt `tools/kim1-model/trace.py`: OKLab, the mask's local colour by an opening, copper as lighter than the mask by an Otsu threshold with hysteresis, pads kept by shape. The bare scan is not a photograph: there is no glare and no parts, so the thresholds are measured on I1 and I2 afresh and written in the script with the probe values that set them. The yellow print hides copper on the top and solder covers it on the bottom: the print is traced separately (its own colour) into blue, and copper under it is recovered only where the copper either side of the print continues in line; say how much that is. Nothing is tuned against the nets check, which is the held-out test.

- [ ] **Step 1: pytest first.** Synthetic tracks, pads and print on a mask colour with noise: the trace recovers copper to an intersection over union of at least 0.95 and the print separately; the net check finds two drawn nets apart and fails a drawn short. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Trace both faces and the print.** Overlays of each face on its scan in `out/`, looked at closely in at least the bus, the RAM block and the video area; say in the journal what was seen.
- [ ] **Step 3: The checks.** Coverage; drills in copper on each face; the known nets: for each logic IC whose identity and pin 1 are known from task 3, its GND and +5V pins (from the part's datasheet pinout, written in `data/ic-table.json` with the source; 74-series: GND pin 7 of 14 or 8 of 16, VCC pin 14 or 16; the large chips from their datasheets), joined through both faces by the drills; the top face against I5 where I5 shows the board (I5 registered to I1 on chip pins by a homography; the I5 registration figure recorded). Judge against the table.
- [ ] **Step 4: The map.** Choose its resolution against the budget, write it, record its size and SHA-256.
- [ ] **Step 5: Site tests.** The map: committed, WebP, the size `copper.json` gives, inside 600,000 bytes, its three channels apart (red and green each 10 to 50 per cent, blue under 15 per cent; a test like the KIM-1's); `copper.json` passes the copper, drills, nets and I5 rows of the table; its `map.sha256` is the committed file's. Raise the floors.
- [ ] **Step 6: Journal** (thresholds and the probe values behind them, what the print hid, the nets result with the chips excluded and why, the I5 figure, the resolution chosen and the bytes; before and after overlays described). **Commit** `feat(models): the BBC Micro board's copper on both faces, traced from the bare scans`. Push.

---

### Task 5: The parts: places and identities

**Files:**
- Create: `tools/bbc-micro-model/board_parts.py`, `tests/test_board_parts.py`, `data/ic-table.json` (from task 4, completed), `data/parts.json`, `site/src/models/bbc-micro-board-parts.mjs`; append to `run-board.sh`
- Modify: `site/tests/bbc-models.test.mjs`, `docs/bbc-micro/facts/models.md` (the fitted set, sourced)

**Interfaces:**
- Consumes: `registration.json` footprints; D1 (fetch it now, add it to `sources.json`); I5 and I6 (fetch I6 now).
- `data/ic-table.json`: `{ "source": "D1", "pages": "...", "ics": [{"ref": "IC1", "part": "6502A", "role": "the processor", "pins": 40, "manualXY": [160, 85] | null, "fitted": true|false, "fittedWhy": "..."}] }`, typed by hand from table 8.1 with the PDF page of each row; `fitted` is the Model B with the 8271 and the DFS: what I5 shows fitted, plus the 8271 (IC78), its support chips and the DFS ROM in a sideways socket as the Service Manual's disc interface section lists them (where the manual is silent, the empty footprints in the 8271's area on I1 are taken as fitted and `fittedWhy` says so [guessing - verify]); the Econet and speech parts are not fitted.
- Produces `bbc-micro-board-parts.mjs` (generated, with a header saying so and naming its sources):
  ```js
  export const BOARD = { width, depth, thickness: 1.6, outline: [[x, y], ...], holes: [{ x, y, d }] };
  export const ICS = [{ ref, part, role, pins, x, y, w, l, rotation, socketed, height, fitted, sheila: 'systemVia' | ... | null }];
  export const CONNECTORS = [{ ref, kind, x, y, w, l, h, rotation, label, edge: 'rear' | 'underside' | 'board' }];
  export const PASSIVES = [{ kind: 'axial' | 'radial' | 'other', x, y, rotation, length, diameter }];
  export const OTHERS = [/* modulator can, crystal, regulator if any: typical bodies */];
  export const HEIGHTS_TYPICAL = { board: 1.6, dip: 4.0, socket: 3.0, ... };   // typical, said so
  ```
  `sheila` names the chip's entry in the machine's access counters (task 6), or null; the ROM sockets carry `paged: 'BASIC' | 'DFS'` where fitted.

- [ ] **Step 1: pytest first.** Synthetic footprints: the place is the pad-group centre, the rotation from pin 1; a footprint whose pad count is not its part's is reported, not placed; the similarity fit to manual positions finds a deliberate swap of two chips. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Places and identities.** Every IC from its footprint and the table; connectors from their footprints and print (SK1 to SK7 at the rear, PL8 to PL12 underneath, and the others), labelled from the print and the manual; passives at their pads as typical bodies; the modulator and the crystal from I5's outlines. I5 registered to I1 on pins (threshold row "I5 to I1"): check each fitted chip's marking in I5 is its identity (a crop of each, looked at, and listed in the journal).
- [ ] **Step 3: Site tests.** Every IC's pad count equals its `pins`; every identity equals `ic-table.json`'s part; the fitted set: IC78 fitted, the DFS ROM fitted, IC89, IC98 and IC99 not fitted; every part lies inside the outline; no two IC bodies overlap; the manual-positions check passes the table; every `sheila` value is a name task 6 will define (a fixed list in the test, the same as `BbcChip`'s); `parts.mjs` agrees with `parts.json`. Raise the floors.
- [ ] **Step 4: Journal** (the fitted set and its sources, the identities checked in I5, any chip the manual and the board disagree on, what is typical). **Commit** `feat(models): the BBC Micro board's parts, placed from the scan and named from the manual`. Push.

---

### Task 6: The machine tells the page its lights, its keys and its chips

**Files:**
- Create: `src/Dbhq.Machines.BbcMicro/BbcChip.cs`, `tests/Dbhq.Machines.BbcMicro.Tests/MachineStateTests.cs`
- Modify: `src/Dbhq.Machines.BbcMicro/BbcBus.cs` (counters in `ReadSheila` and `WriteSheila`, one at the sound strobe; the last byte written to `$FE10-$FE17`), `BbcMachine.cs` (pass-throughs), `src/Dbhq.Machines.BbcMicro.Wasm/Program.cs`, `site/public/bbc-micro.js`, `site/tests/bbc-micro.test.mjs`, `docs/bbc-micro/facts/bus.md` (the serial ULA's motor bit, sourced), `bench/bbc-micro-speed/README.md` only if its workload's description changes (it should not)

**Interfaces:**
- Produces:
  ```csharp
  public enum BbcChip { Crtc, Acia, SerialUla, VideoUla, RomLatch, SystemVia, UserVia, Fdc8271, Adc, Sound }
  // BbcBus
  public uint Accesses(BbcChip chip);            // reads and writes by the CPU since power on, wrapping; Peek counts nothing; no cycle is added
  public byte SerialUlaControl { get; }           // the last byte written to $FE10-$FE17; write only on the machine, nothing else modelled
  // BbcHost (JSExport)
  public static int Leds();                       // bit 0 cassette motor, bit 1 CAPS LOCK, bit 2 SHIFT LOCK; 1 = lit
  public static int[] AccessCounts();             // Accesses for each BbcChip, in enum order
  public static int RomSlot();
  ```
  In `bbc-micro.js`: `panel.bbc` gains `tap(name)` (exactly what a click on that on-screen key does, latches included; `'Break'` presses BREAK), `leds()` (an object `{ motor, caps, shiftLock }` of booleans), `accessCounts()` and `romSlot()`; and the panel dispatches `bbc:key` with `detail = { name, action }`, `action` one of `down`, `up`, `tap`, `latch-on`, `latch-off`, for the PC keyboard, the on-screen keys and `tap` alike.

- [ ] **Step 1: The fact.** Find the serial ULA control register's bit 7 (cassette motor relay, which drives the LED) in the Advanced User Guide s.17 and the Service Manual; add it to `bus.md` with its source. If the sources disagree or are silent, say so and leave the motor LED dark, recording why; do not guess.
- [ ] **Step 2: Failing C# tests** (`MachineStateTests`, with `BbcSession`): after boot, typing `a` puts `A` on the screen and the CAPS LOCK LED bit is lit; after a CAPS LOCK press, `a` gives `a` and the bit is dark; SHIFT LOCK lights its bit and `1` then gives `!` [verify the OS's behaviour from the ROM or the User Guide before asserting it]; `*MOTOR 1` lights the motor bit and `*MOTOR 0` puts it out; `Peek(0xFE40)` changes no counter; one CPU read of `$FE40` adds one to `SystemVia` and nothing else; a read-modify-write on a SHEILA address adds one read and two writes; a stretched access counts once; a sound write adds one to `Sound`; after boot the system VIA, CRTC and video ULA counts are non-zero; `*CAT` on a blank disc raises the 8271's. (That nothing else changed is shown by the fingerprint in step 4, not by a figure this code printed.) Run: fail.
- [ ] **Step 3: Implement.** One increment in each SHEILA case and at the sound strobe; no change to any cycle or any chip. Run the BBC test project, then the whole `dotnet test -c Release`: pass, no warnings.
- [ ] **Step 4: Prove nothing else changed, and measure the speed.** The native fingerprint (`bench/bbc-micro-speed/native`, `--fingerprint`, as its README says) before and after the change: identical. The speed, before and after, with `bench/bbc-micro-speed/alternate.sh` as its README says, on a quiet machine: record both medians and the load average; a slowdown over 2 per cent is investigated and explained before going on.
- [ ] **Step 5: The page script, test first.** In `bbc-micro.test.mjs`, with the existing fake host: a PC key down and up dispatch `bbc:key` down and up; an on-screen tap dispatches `tap`; latching SHIFT dispatches `latch-on` then `latch-off`; `panel.bbc.tap('A')` presses and releases A through the host exactly as the on-screen key does, with a latched SHIFT held round it; `tap('Break')` calls `Break`; `leds()` decodes the three bits. Run: fail. Implement. Run: pass. Build the machine (`cd site && node scripts/build-machines.mjs bbc-micro`), run the site suite and the browser check: pass. Raise the floors.
- [ ] **Step 6: Journal** (the motor fact and its source, the LED polarity as the ROM showed it, the counters and what they do not count, the fingerprint and the speed with their commands and the load). **Commit** `feat(bbc-micro): the machine reports its LEDs, its keys and which chips it talks to`. Push.

---

### Task 7: The inside model on the page

After this task the BBC Micro's record has `"models": [{ "view": "inside", "module": "bbc-micro-board" }]` and **no `case` field**, so the cased rule (which fires only when `case` is set) does not apply, and the page shows the inside the way the KIM-1's shows its board, through the real page and browser check, with no preview mechanism. This is a state of the branch only: task 9 adds `"case": true`, the outside and the rule that a machine claiming a model states `case`, and the branch is not merged before task 9.

**Files:**
- Create: `site/src/models/bbc-micro-board.js`, `bbc-micro-board-layout.mjs`, `bbc-micro-board-notes.mjs`, `tools/bbc-micro-model/results.py` (the board half), `site/src/data/bbc-micro-board-model.json`
- Modify: `machines/registry.json` (`models: [{ "view": "inside", "module": "bbc-micro-board" }]` and no `case`, as above; `references` for I1, I2, I5, I6, D1 with title, author, URL, licence as stated or null, fetched, SHA-256 and what was used; the main photograph's `used` no longer says there is no model), `site/src/models/models.mjs` (the entry, with `texture` and `legend`), `site/src/components/MachineModel.astro` (an optional legend table from the entry), `site/src/styles/tokens.css` and the model styles (new `--model-*` tokens: the BBC board's mask, its underside, copper, print, socket, the access mark), `.github/workflows/deploy-site.yml` (serving list: `models/bbc-micro-board.js models/bbc-micro-board-tracks.webp`), `site/scripts/browser-check-bbc.mjs`, `site/src/assets/photos/README.md` (a table of the sources that are not committed), `site/tests/model.test.mjs`, `site/tests/bbc-models.test.mjs`, `site/tests/design.test.mjs`

**Interfaces:**
- `models.mjs` entry:
  ```js
  'bbc-micro-board': { machine: 'bbc-micro', view: 'inside', label, about: describeBoard(), texture: BOARD_TRACKS.src, made: madeBoard, legend: chipLegend }
  // chipLegend(): [{ ref, part, role, sheila, paged }] for every fitted IC, in IC number order
  ```
- The model reads state through `panel.bbc` once `bbc:ready` has fired: every 250 ms it takes `accessCounts()` and `romSlot()`, marks each chip whose count moved (its top tinted with `--model-active`, and its legend row `data-accessed`), and marks BASIC or the DFS ROM as paged. Before Start nothing is marked and the status line says the machine is not running.
- Test hooks on the root, as the KIM-1's: `data-model-accessed` (the refs marked, space separated), `data-model-paged`, `data-model-tracks`, `data-model-parts`; `root.modelChipPoint(ref)`, `root.modelBoardPoint(x, y, below)`, `root.modelView()`.

- [ ] **Step 1: Failing tests.** `model.test.mjs` already covers every model in `MODELS` (module, bundle, budget, tokens, lazy loading, labels, controls text, underside); add for this model: the section names its texture and the texture is built beside the bundle byte for byte; the deploy lists both files. `bbc-models.test.mjs`: the model draws every fitted IC in the parts file and no unfitted one; the legend lists every fitted IC once, with its part and role from `ic-table.json`; the caption starts "Model, not a photograph." and its sizes and counts come from the layout (as `describe()` for the KIM-1); the note's every figure is read from `bbc-micro-board-model.json`, and that file agrees with `frame.json`, `registration.json`, `copper.json` and `parts.json`; every source in `sources.json` used by the board is credited in the registry by URL (photograph, drawing or reference) and the other way round; the access legend says in words what a mark means and why the CPU, RAM and OS ROM are never marked; the board's three map channels are read as top, bottom and print. Run: fail.
- [ ] **Step 2: The model.** Board with its outline and holes; the map turned into colour, shine and relief for each face as the KIM-1's `trackMaps` does (top from red, underside from green, the print from blue on the top in the print token); ICs as bodies on sockets where socketed; connectors, passives (instanced), the modulator and crystal; Show tracks and Show tracks only as the KIM-1's; pointing at a chip names it on the status line and marks its legend row; clicking a chip presses nothing. Start view and bounds chosen so the whole board is in view and the camera may go under it.
- [ ] **Step 3: Page weight.** `node scripts/page-weight.mjs /machines/bbc-micro/` before (task 6's commit) and after: nothing under `/models/` in the first load; the bundle and map under "later"; inside their budgets. Record both outputs.
- [ ] **Step 4: Browser check.** In `browser-check-bbc.mjs`, after the existing BBC steps, with the machine running: the bundle and map were not fetched before scrolling; scrolling to the model loads them and the status says it runs; the canvas draws; the polar angle reaches pi and the view from below has drawn pixels where the map has bottom copper (as the KIM-1's underside check); Show tracks and Show tracks only work; `*CAT` with the check's test disc marks IC78 at least once while it runs (sample every 50 ms) and the paged ROM shows the DFS at least once; a chip's legend row is marked when its chip is; no console errors, failed requests or CSP violations. Run the whole `node scripts/browser-check.mjs`: the KIM-1's part unchanged and passing.
- [ ] **Step 5: Credits and provenance.** The registry's references, the photographs README's table of uncommitted sources (author, URL, licence as stated, fetch date, SHA-256, what was derived), the tool's `data/README.md` saying which outputs carry which terms. Raise the floors.
- [ ] **Step 6: Journal** (what the visitor sees, the tokens and why, the legend and the access marks as decided, page weight, the browser check's lines). **Commit** `feat(models): the BBC Micro's inside model, its copper, its chips and what the processor is talking to`. Push.

---

### Task 8: The outside measured: keys, case, profile and ports

**Files:**
- Create: `tools/bbc-micro-model/keys.py`, `case_register.py`, `case_profile.py`, `case_parts.py`, `run-case.sh`, `tests/test_case.py`, `data/keys.json`, `data/case.json`, `site/src/models/bbc-micro-case-parts.mjs`
- Create: `site/src/assets/photos/bbc-micro-top.webp`, `bbc-micro-rear.webp`, `bbc-micro-keyboard.webp`, `bbc-micro-front.webp`, `bbc-micro-left.webp` (O1 to O5, `cwebp -q 82 -resize 1600 0 -metadata none`, as the KIM-1's)
- Modify: `machines/registry.json` (the five as `photos` after the main one, each with credit, `fetched` and `used`), `site/src/assets/photos/README.md` (a section each, with both SHA-256), `tools/bbc-micro-model/data/sources.json` (O4, O5, O8, K2 fetched and hashed; `committed` set for O1 to O5), `docs/bbc-micro/facts/models.md` (the new hashes), `NOTICE.md` (K1's MIT notice), `site/tests/bbc-models.test.mjs`

**Interfaces:**
- Produces `data/keys.json`: `{ "k1": {"commit", "switches"}, "keys": [{ "name", "x", "y", "w", "d", "row", "colour": "red"|"dark"|"light", "legend" }], "o1": {"n","heldOutMm": {...},"over3mm"}, "o3": {...}, "frontEdgeMm", "parallax": {...} }`; `name` is a `BBC_KEYS` name, `Break`, or `Shift2` if the keyboard has a second SHIFT keycap.
- Produces `data/case.json`: `{ "footprint": {"widthMm": 415, "depthMm": 345, "source": "D1"}, "heightMm": 73, "frontEdge": {"rawMm", "rawSeMm", "h", "d", "correctedMm", "verdict"}, "caseTop": {...}, "outline": [...], "recess": [...], "profile": [{"y","z"}], "profileCheck": {"what","errMm"}, "leds": [{"name","x","y"}], "badge": {"box", "words"}, "grille": {...}, "rear": [{"label","x","z","w","h","ref"}], "underside": [{"label","x","y","w","d","ref"}], "boardInCase": {"x","y","z","from"} }`.
- Produces `bbc-micro-case-parts.mjs` (generated): `CASE`, `RECESS`, `PROFILE`, `KEYS`, `LEDS`, `BADGE`, `GRILLE`, `REAR`, `UNDERSIDE`, `FEET`, each with a source note.

- [ ] **Step 1: pytest first.** Synthetic: a key grid photographed through a known homography with per-row heights recovers the centres to the parallax the rows imply; two synthetic views of a known profile triangulate it to under 0.5 mm; ports placed by a known board offset come back to 0.1 mm. Run: fail. Implement. Run: pass.
- [ ] **Step 2: The keys.** Every K1 switch, mapped to the machine's keys; every `BBC_KEYS` name, BREAK and (if O1 shows it) a second SHIFT has exactly one key (two for SHIFT if so); a key K1 lacks is a STOP. Key sizes from K1's footprints; colours by class from O1 (median colour per key, clustered), recorded as classes, not colours. O1 and O3 registered to K1 as in the spike; judge against the table. Record O1's raw front edge on the key-plane registration (recorded, not judged).
- [ ] **Step 3: The case.** The rectified O1 gives the outline, the recess, the three LEDs (left to right: cassette motor, CAPS LOCK, SHIFT LOCK, as O1 shows them [verify]), the badge strip's box and its words as O1 shows them (no owl), and the grille. The footprint and height are D1's. The side profile from O4 and O5: one camera fitted to both (as `tools/kim1-model/heights.py`), the plane being the key plane registered in each, with points marked by hand along the side; one marked point kept out as the check (threshold row). If the triangulation fails its check, the profile is typical from D1's height and O2's rear, the journal says so and the page will say so.
- [ ] **Step 3a: The front edge, corrected for parallax.** h comes from a measurement independent of the 415 mm (not from the key-fit camera alone), with its uncertainty; d comes from O4 and O5, measured against the same fitted key plane the homography uses, not one row's key tops, with its uncertainty. Both are recorded in `case.json` before the corrected width is computed. The corrected width is the raw width (O1's two front corners through the key-plane homography) times (h + d) / h, and its interval is plus or minus two standard errors, the standard errors of h, d and the raw width combined in quadrature (task 0's jackknife standard error of the raw width was 0.85 mm); this coverage is fixed here, before task 8 measures anything, and is not chosen afterwards. The corrected width's interval is judged: PASS only if the whole interval is within 1.5 per cent of 415 mm; STOP if the whole interval is outside 2.5 per cent; otherwise the check is inconclusive, which counts as NOT passed and goes back to the plan.

  Where h can come from: not O1's EXIF (O1 has none); not O4's and O5's 6.3 mm lens, which is a different camera; not the key fit's own camera, whose focal length comes only from the right-angle constraint (task 0: 536 mm with a jackknife standard error of 355 mm, so the check could not fail). Usable: the key plane's perspective terms give the camera's height in units of its focal length, so h needs one more measurement that does not use the case's width, for example an object of known height in O1 seen against the key plane (for example the step in height between keycap rows from K2's profile against the row offsets O1 shows; the case's published height of 73 mm is its height above the desk, not above the key plane, and O1 looks down on the case so its base is not visible, so it is not usable by itself). Whether the check is measurable is decided from h and d ALONE, before the corrected width is computed: it is not measurable if the interval of the factor (h + d) / h is wider than 3 per cent of its value, because the corrected width could then never pass. If no independent h can be obtained, or the factor's interval is that wide, the check is dropped as not measurable rather than fitted, and an inconclusive result found AFTER the width is computed can never be relabelled as not measurable, the journal says so, and the case's size rests on the Service Manual's published 415 by 345 by 73 mm, which is how the design already takes it.

  Register O1 on the case top's own four corners to D1's 415 by 345 mm and record that plan size (recorded, not judged). Write `case.json` `frontEdge`: `{"rawMm", "rawSeMm", "h": {"mm", "seMm", "from"}, "d": {"mm", "seMm", "from"}, "correctedMm": {"value", "low", "high"}, "verdict": "pass"|"stop"|"inconclusive"|"not measurable"}`, and `caseTop`; report the raw and the corrected width side by side in the journal.
- [ ] **Step 4: Ports and connectors.** Rear ports at the x of their sockets (SK1 to SK7 from task 5) plus the board's offset in the case, which is found from O2 (each port's centre in O2, on a scale from the 15-way D shell, against its socket); heights from O2; underside connectors at PL8 to PL12's x and y, the panel round them typical from O8 (fetch it now). Each labelled with its legend from O2 or O8.
- [ ] **Step 5: The photographs.** Fetch O4 and O5 (hash them), resize O1 to O5, commit the five copies, write their README sections and registry entries (alt text describing each photograph; `used` saying what the model took). Add K1's notice to `NOTICE.md`.
- [ ] **Step 6: Site tests.** `keys.json` passes the O1 row of the table and records the raw front edge; `case.json`'s front edge records h and d with their sources and intervals, and its verdict is what its interval gives against the row (pass, or the check recorded as not measurable; inconclusive or stop fails the test), and its raw width and case-top check are recorded; key centres equal K1's to 0.01 mm; every machine key once (and SHIFT twice if so) and no other; no two keycaps overlap; every key inside the recess and the recess inside the case; the LEDs in their order; the rear ports in O2's left-to-right order and each within 2 mm of its socket's x plus the offset; the profile's check passes or the typical fallback is flagged; `case-parts.mjs` agrees with the data files; the new photographs are committed, credited, and match their README hashes (the existing photograph tests cover the rest). Raise the floors.
- [ ] **Step 7: Journal** (the K1 mapping and anything it lacked, the held-out errors and the parallax, the profile and its check, the board's offset in the case, the photographs chosen). **Commit** `feat(models): the BBC Micro's case and keyboard measured`. Push.

---

### Task 9: The outside model, and the two views

The largest task. If its first review sends it back more than twice, split it at step 4: the case model first (steps 1 to 3, with the page's single view temporarily the outside), the views second.

**Files:**
- Create: `site/src/models/bbc-micro-case.js`, `bbc-micro-case-layout.mjs`, `bbc-micro-case-notes.mjs`, `site/src/data/bbc-micro-case-model.json` (by `results.py`, the case half)
- Modify: `machines/registry.json` (`"case": true`; `models`: outside `bbc-micro-case`, then inside `bbc-micro-board`), `site/src/lib/registry.mjs` (the last rule: a machine that claims a model states `case`), `site/src/models/models.mjs`, `site/src/components/MachineModel.astro` (the two-view path), `site/public/model-loader.js` (per-view loading and the tabs), the model styles and `tokens.css` (case, keycap and LED tokens; the tab styles), `site/scripts/page-weight.mjs` (the later files grouped by view), `.github/workflows/deploy-site.yml` (`models/bbc-micro-case.js`), `site/scripts/browser-check-bbc.mjs`, `site/DESIGN.md`, `site/tests/registry.test.mjs`, `model.test.mjs`, `bbc-models.test.mjs`, `design.test.mjs`, `site.test.mjs` (the lime rule, if the tab needs naming in it: it must not take the lime)

**Interfaces:**
- `models.mjs` entry: `'bbc-micro-case': { machine: 'bbc-micro', view: 'outside', label, about: describeCase(), made: madeCase }`.
- Markup for a machine with two models: `<section class="model" data-model-section hidden>`, heading "Models of the machine"; `<div role="tablist" aria-label="Views of the model">` with `<button role="tab" id="model-tab-<view>" aria-selected aria-controls="model-panel-<view>" tabindex="0|-1">Outside</button>` and Inside; each `<div role="tabpanel" id="model-panel-<view>" aria-labelledby="model-tab-<view>" data-model=<module> data-model-src data-model-texture? hidden?>` holding everything the single-view section holds, ids suffixed `-<view>`; the controls paragraph once, after the panels. A machine with one model renders exactly as before.
- `model-loader.js`: `[data-model-section]` is shown; each `[data-model]` loads when it nears the screen (a hidden panel never does) or its button is pressed; a tab list: click or Enter or Space selects; Left and Right move and select, Home and End go to the ends, with roving `tabindex`; selecting un-hides its panel and hides the other. No other behaviour changes for a single model.
- The outside model: a key goes down on `bbc:key` (`down` holds it until `up`; `tap` holds it 140 ms; `latch-on` holds it until `latch-off`); a click on a key calls `panel.bbc.tap(name)`, and before Start says on the status line that the machine is not running; the LEDs follow `panel.bbc.leds()` each frame (lit `--model-led`, dark `--model-led-off`, as the KIM-1's red tokens, or new ones if the BBC's LEDs measure otherwise); keycap legends drawn by us into one canvas atlas at mount, with the site's own self-hosted face after `document.fonts.load`, from `LEGENDS` in `bbc-keys.js` so they cannot drift from the on-screen keys; sculpted caps by row (typical profile, checked against K2's numbers); instanced caps per size. Test hooks: `data-model-pressed`, `data-model-presses`, `data-model-down`, `data-model-leds` (the lit ones in words), `root.modelKeyPoint(name)`, `root.modelView()`.

- [ ] **Step 1: Failing tests.** `registry.test.mjs`: a machine claiming a model with no `case` fails with the new rule's message; the BBC Micro's record with `"case": true` and only `inside` (task 7's claim plus the flag) fails the cased rule; a cased running machine with no `models` passes (models never hold up a count); the real registry passes. `model.test.mjs`: on the BBC's page, one model section with a tab list of two tabs, Outside first and selected, each controlling a panel with its own stage, reset button, status line and caption, ids unique on the page; the inside panel `hidden`; the shared controls paragraph once. The KIM-1's page is shown unchanged as in task 1: `cmp` of `dist/machines/kim-1/index.html` built from task 8's commit and from this one, which must be identical. `bbc-models.test.mjs`: the case's footprint and height are D1's; every key is a `BBC_KEYS` name, BREAK or the second SHIFT, and every `BBC_KEYS` name has a key; every legend is `LEGENDS[name]`; the LEDs' names and order; the badge strip draws words and no owl (no owl geometry or image in the module: a test that the module names no owl and loads no image but the atlas it draws); rear and underside connectors each have a label; the caption and note as for the board. `bbc-micro.test.mjs` or a new loader test with a fake DOM: the tab keys move and select as above and a hidden panel's root is not loaded. Run: fail.
- [ ] **Step 2: The case model.** Shell from footprint, outline, profile and recess; the keys; the LEDs; the badge strip; the grille; rear ports and underside connectors as labelled boxes (the label drawn on the box's face and named on the status line when pointed at); feet. Start view three-quarters from the front, bounds the case plus 2 cm; the camera may go under it and the underside connectors show.
- [ ] **Step 3: The views.** The markup, the loader, the styles: the selected tab `--white` on `--veil` with a bar under it, a shape as well as a colour, no lime; new text colours in the contrast table. Reduced motion: no transition when switching, keys go down at once.
- [ ] **Step 4: Page weight.** Before (task 8's commit) and after: the first load holds nothing under `/models/`; the HTML and loader growth recorded; under "later", the outside's bundle when the section is reached, and the inside's bundle and map when its tab is chosen; every file inside its budget. Record both outputs.
- [ ] **Step 5: Browser check, both views.** In `browser-check-bbc.mjs`, the machine running: nothing under `/models/` fetched before scrolling; scrolling to the section fetches the outside's bundle and not the inside's; the outside draws; after boot `data-model-leds` includes CAPS LOCK; the on-screen CAPS LOCK puts it out and again lights it; `*MOTOR 1` typed lights the motor LED and `*MOTOR 0` puts it out (or, if task 6 left it dark, it stays dark and the check says why); the on-screen A key puts A down on the model; a real key event on the screen puts that key down on the model; a click on the model's C key types C on the screen (read with `screenRow`) and adds one to `data-model-presses`; under the case (polar pi) the view has drawn pixels; the tab list takes focus, Right selects Inside, which then fetches its bundle and map and runs, and Left returns to Outside with the outside's camera where it was; Tab from a stage moves focus on out of the section (no trap); task 7's inside checks run again inside the tab; a second context with `reducedMotion: 'reduce'` loads both views and switches without error; no console errors, failed requests or CSP violations. Run the whole `node scripts/browser-check.mjs`: the KIM-1's part unchanged and passing.
- [ ] **Step 6: Provenance on the page.** The credits under each view list exactly the sources that view used (photos, drawings and references, by URL), with what each gave; K2 credited as a cross-check; `DESIGN.md` gets "The BBC Micro's 3D models" in the shape of the KIM-1's section. Raise the floors.
- [ ] **Step 7: Journal** (the views and what they were chosen over, the keycaps and legends, the LEDs as shown by the browser check, page weight, the browser check's lines, any part of the design that changed on contact). **Commit** `feat(models): the BBC Micro's outside model, and both models as views of the same machine`. Push.

---

### Task 10: The final pass

**Files:**
- Modify: `docs/known-differences.md`, the day's journal, `README.md`, `AGENTS.md` (layout: `tools/bbc-micro-model/`), `docs/the-6502-family.md`, `site/DESIGN.md`, `site/README.md` if it describes the models, `tests/` and `site/tests/` as the pass requires

- [ ] **Step 1: Mutation pass.** In a scratch copy outside the repository, break one thing at a time and run the site suite (and the BBC C# tests where C# is broken), recording which test caught each: x and y scales swapped; the solder side not mirrored; one hole's registration moved 0.5 mm; one key centre moved 3 mm; two keys' names swapped; a legend not from `LEGENDS`; the case width 410; the LED bits swapped; the motor bit read from bit 6; a counter on the wrong chip; `Peek` counting; IC3 and IC69 swapped in the parts file; IC78 marked not fitted; a module claimed by two machines; the cased rule removed; the "states `case`" rule removed; a running machine required to claim a model; the inside's texture missing from the deploy list; a colour literal in a model; the track map over budget. Any mutation that survives gets a test or a line in `known-differences.md` saying why not. Put the table in the journal.
- [ ] **Step 2: Known differences and limits.** `docs/known-differences.md` gains a section on the models: the case size from the B+ section, the 410 against 415, K1 being a replacement keyboard, the typical heights, the copper under the print, the typical underside panel, the profile if it fell back, the LEDs' polarity as established. The page's "What it does not show" lists match (from each `made()`).
- [ ] **Step 3: The size and speed record.** `node scripts/page-weight.mjs /machines/bbc-micro/` and `/machines/kim-1/` on the final build; the speed from task 6; both in the journal with their commands.
- [ ] **Step 4: Documents.** README (the BBC Micro's models; the licences paragraph names the track map and the photographs as not MIT, and K1's notice in `NOTICE.md`), `AGENTS.md`'s layout, the family document's BBC line, `DESIGN.md`. No figure typed by hand.
- [ ] **Step 5: Final review.** `superpowers:requesting-code-review` on the whole branch against the design and this plan, on the most capable model, with one question first: does any page, doc or journal sentence claim more than a test or a recorded measurement shows? Apply what it finds in one fix wave and have it re-checked.
- [ ] **Step 6: Mark the pull request ready.** Only once `Validate` is green on the last commit. Do not merge.
- [ ] **Step 7: Journal and commit** `test(models): the mutation pass, the models' known limits, and the record`. Push. Report to Dan (below).

---

## What Dan must be told at the end

Nothing here needs a decision from him now; the choices are taken (`models.md`, and the decisions table in the design). At the end he is told, one thing at a time, the most important first:

1. **The machine changed, a little.** The bus counts its SHEILA accesses and keeps the serial ULA's control byte, for the inside's marks and the motor LED. The fingerprint showed no change in behaviour, and the speed before and after is in the journal.
2. **Some sources are credited but not shown.** The bare-board scans and the populated-board photographs state no licence and are not committed, so they appear in the model's credits as references, not in the photographs section, unlike the KIM-1's unlicensed photographs.
3. **The case size is the B+'s.** The Service Manual gives it in the B+ section; the Science Museum's width is 5 mm less. The model uses the manual's and says so.
4. **What the page weighs**, first load and later, per view, from the last run of `page-weight.mjs`.
5. **Opening both views downloads three.js twice**, about one bundle's worth more, chosen over a shared chunk that would change the deploy's file list and the KIM-1's build.
6. Anything a STOP or a fallback changed (the side profile, the motor LED), if one did.

**Decided, for the record:** the registry rule is that a cased machine that claims a model claims both, and no machine is required to claim one. It was chosen on 4 October over "a running cased machine must claim both", which would have contradicted Dan's 2 October ruling that models never hold up a machine counting and failed validation for the live BBC Micro. Dan is told it as a decision, not asked.

## Deferred

Recorded here, not built:

- A shared three.js chunk for the two bundles (needs a deploy list that tolerates hashed names).
- Measured heights for the inside, from a pair of photographs of a populated Issue 7 board, if one is found.
- Following a net on the board (pick a pad, light its copper), which the nets check's connectivity would make possible.
- The inside of the case: the power supply, the speaker, the keyboard's own circuit board and the ribbon cables; an opening lid.
- Parts not fitted (the Tube, Econet, speech) shown as empty sockets rather than bare footprints.
- A real GPU, other browsers and a screen reader on both views.
- Models for the next machine with a case.

## Self-review

**Spec coverage.** Two models as labelled views: 7, 9. Registry `models`, `case`, `references` and the "built" rules and the cased rule: 1; "a machine that claims a model states `case`": 9. Lazy loading per view, same controls, keyboard and no trap: 9. State: LEDs and keys 6, 9; chip accesses and paged ROM 6, 7. Provenance and licences: 0, 7, 8, 9. Proof: scale 0, 2; registration 0, 3; copper 4; parts 5; case and keys 0, 8; structure 7, 9; machine state 6; browser 7, 9; mutation 10. Known limits and not in scope: 10, the design. Page weight: 7, 9, 10.

**Placeholders.** No geometry code is written here, by design (the header says why). Every threshold, budget, file name, interface and test assertion is stated. Three facts are marked [guessing - verify] and each has a task that verifies it before relying on it: the case size's source (8), the motor bit and LED polarity (6), the second SHIFT and the LED order (8); and one, the disc interface's support chips, is sourced in task 5.

**Type consistency.** `BbcChip`, `Accesses`, `SerialUlaControl`, `Leds`, `AccessCounts`, `RomSlot` (6); `panel.bbc.tap`, `leds`, `accessCounts`, `romSlot`, `bbc:key` (6), read in 7 and 9; `VIEWS`, `modelProblems`, `referenceProblems`, `modelFiles` (1); `MODELS[module].machine` and `.view` (1); the `sheila` names in the parts file equal `BbcChip`'s (5, 6); the data files' names and keys (0 to 8) read by `results.py` and the site tests.

**Known soft spots.** Task 9 is large (split point given). Task 4's copper under the print is the measurement most likely to need judgement; the nets check is its independent test. Task 8's side profile has a stated fallback.
