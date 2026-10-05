# The NES's two 3D models: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two models of the NES on `/machines/nes-famicom/`, the outside (the front-loading case, its buttons, LED and ports) and the inside (the main board with its chips and both faces of copper), each drawn for the NTSC NES-001 and the PAL NESE-001 and following the page's region, offered as labelled views of the same machine with the same controls and lazy loading, each showing the running machine's state, as [the design](../specs/2026-10-05-nes-models-design.md) says.

**Architecture:** Offline Python in `tools/nes-model/` measures both models from bare board scans, photographs and a design patent, and writes what the site uses: a track map, two generated parts modules and two results files, all committed. The build never runs it. Each model is one browser module on the shared stage that holds both consoles and redraws on `nes:region`, bundled with three.js and loaded only when its view is shown. `MODELS` gains `regions`; the registry does not change. The machine gains one small counter class and one host export.

**Tech Stack:** Python 3.12 with numpy, opencv-python-headless, Pillow, scipy and pytest in a virtual environment; three.js and camera-controls as pinned in `site/package.json`; Astro; node's test runner; Playwright with Chrome's software WebGL; C# on .NET 10 and xUnit.

**Spec:** [`docs/superpowers/specs/2026-10-05-nes-models-design.md`](../specs/2026-10-05-nes-models-design.md). Sources and licences: [`docs/nes/facts/models.md`](../../nes/facts/models.md).

**What was run before this plan was written, and what was not.** On 5 October 2026, in a scratch copy of the repository at commit `ead017d` (outside the repository, nothing committed), every code block in this plan was run as written: the tool's `common.py` built by the commands in task 0 with its 22 pytest tests passing (`/tmp/bbcvenv/bin/python -m pytest tools/nes-model/tests -q`), `verify.py` passing on all 13 real inputs, the `sources.json` below with the five site tests in task 0 passing and two of them shown to fail on a wrong hash and on an original copied into `tools/`, the task 0 verdict function with its 3 tests and the site test that reads it (passing on a made-up `spike.json` whose verdicts match its figures, failing on one whose do not), the `regions` helper with its 3 tests, the access-rate function and its sampler with their 5 tests, the C# counter class with its 19 tests (`dotnet test -c Release`, warnings as errors), and the site's mirrors test with the new files in place. **The measuring code is not written here**, as in the BBC Micro's plan: it is image work whose code depends on what the scans show. Each such task states its interfaces, inputs, outputs, the thresholds that judge them and the tests; the implementer writes the tests first, then the code, and a reviewer gates each task.

## Global Constraints

- **Branch `feat/nes-models`**, in the worktree that holds this plan. One commit per task (a fix wave after review may add one), conventional-commit messages, each ending with the session's co-author line. Push and the draft pull request only when Dan says so at the start of execution. The pull request merges only when `Validate` is green and Dan says so, because merging deploys.
- **What this waits for.** Tasks 0 to 5 are offline and may run at once. **Task 6 waits for the machine's pull request to merge** (the NES row running, `Dbhq.Machines.Nes`, its host, its page, its region control). **Tasks 7 and 9 wait for the BBC Micro's models pull request to merge** (its tabs, its per-view loader, its legend table in `MachineModel.astro`, the rule that a machine claiming a model states `case`). Before task 6 the branch is rebased onto `main` with both merged; if either has not merged when task 6 is reached, the work stops there and Dan is told. The NES never builds its own tabs or loader.
- **Inputs are never committed.** No original in `data/sources.json` is committed, not even resized, except the public-domain and CC BY 4.0 photographs task 8 commits resized for "Photographs of the original". The full-size inputs stay in the folder `NES_MODEL_INPUTS` names. Stage files by name (`git add <paths>`), never `git add -A`. The site test in task 0 fails if any committed file's SHA-256 is an original's.
- **OpenTendo is read from the `dbhq-uk` fork** at commit `3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009`, never from upstream (rule 3). The fork is made in task 0, with Dan's yes, because it creates a repository in his organisation.
- **The tools never fetch.** `site/tests/mirrors.test.mjs` fails on a URL outside `dbhq-uk` in a `.py`, `.mjs`, `.sh` or `.cs` file under `tools/`, `tests/`, `src/`, `bench/` or `site/scripts/` that is not on a comment line. URLs live in `data/sources.json` and in markdown.
- **The build and the tests never run the tools.** `site/package.json` never names `nes-model` or `python` (task 0's test).
- **Thresholds, set before the measurements they judge.** A STOP is a task outcome, reported as BLOCKED with the figures, never a failed test to tune around. A crossed stop stays recorded as crossed; the plan may be revised only for a check that judged the wrong quantity, with the revision dated and the old verdict kept (the BBC Micro's journal for 4 October 2026).

  | Measure | Pass | STOP if | If it stops |
  |---|---|---|---|
  | Scan scale x: rows of pins at 2.54 mm that run across the board (DIP pin rows, the edge fingers, the expansion header), each row at least 30 mm long held out of the fit in turn, its error first pad to last scaled to a 48.26 mm row (error x 48.26 / the row's length); at least 4 rows | median at most 0.15 mm; the largest recorded with its row named, not judged | median over 0.25 mm | all models stop |
  | Scan scale y: DIP row spacings (15.24 mm and 7.62 mm) on **drill centres**, not pad centroids (the BBC Micro's task 0 found solder centroids read about 1 per cent wide), each footprint held out in turn, as a per cent of its spacing; at least 8 footprints; the 600-mil and 300-mil sets also reported apart | median at most 0.5 per cent | median over 1.0 per cent | all models stop |
  | x against y | recorded with the ratio, and beside the scan's stated 300 dpi | the two differ by more than 1.5 per cent | all models stop |
  | Solder side (I1-back mirrored) to component side, by drill centres, on 20 mm blocks held out in a chequerboard; at least 150 holes | median at most 0.20 mm, 90th percentile at most 0.40; the max recorded, not judged | median over 0.30 mm or 90th percentile over 0.60 | all models stop |
  | PAL layout: I3's part centres (the pins' feet at the board, not the bodies' tops) mapped to I1 by a homography fitted on the others, each part held out in turn: U1 to U10, P1, P2, P3; at least 10 parts | median at most 1.0 mm and none over 2.0 | any part over 3.0 mm, or a footprint in one board with no match in the other | the PAL models stop; Dan decides whether NTSC goes on alone |
  | Case depth to width: on the design patent's orthographic views O1, its three figures (FIG 5 top, FIG 6 bottom, and FIG 7 side over FIG 3 front), the case's body without the buttons and the feet, against the published ratio's midpoint, 0.797 (203.2 mm deep over the published widths, 256 mm (D1) and 254 mm (D2), 0.794 to 0.800); judged on every figure. **Revised twice on 5 Oct 2026, each time after task 0's figures were seen.** As first written it was from O2-FL and O2-BR, each with its camera from the Exif (112 mm on the D7000's 23.6 mm wide sensor, checked against the vanishing points), against 203.2 / 254, pass within 1.5 per cent and STOP over 3, and it crossed its STOP: -8.77 per cent on O2-BR, which stays recorded. First revision (Dan, after an independent review): it judged the wrong quantity, the cameras and not the case (the two photographs fail their camera's own check, are nearly the same view, and give depth to width, height to width and the front band 3 to 7 per cent below the patent's), so it moved to the patent with the same 1.5 and 3 per cent, against 203.2 / 254; that run crossed its STOP too, -3.46 per cent on the top view (-3.02 on the bottom view), which stays recorded. Second revision (the controller, with Dan's instruction to be pragmatic), **made after the patent's figures were seen**: the sources disagree with each other by more than 1.5 and 3 per cent can resolve (the patent's own top view gives 0.772 and its side over its front 0.791; the published ratio is 0.794 to 0.800), and the check's real purpose is to catch a gross scale error, so the limits are now 5 and 8 per cent against the published midpoint. The photographs' figures are recorded, not judged | every figure within 5 per cent | any figure over 8 per cent | the outside stops, so every model stops |
  | Case height to width: on the patent's FIG 3 front and FIG 7 side, the case's body without its feet, against 88.9 / 255 = 0.349 (255 mm the published widths' midpoint); judged on both figures. **Revised twice on 5 Oct 2026, each time after task 0's figures were seen**, with the depth row. As first written it was O2-FL against O2-BR, the two agreeing within 2 per cent, and it passed (0.67 per cent); that row was dropped as not meaningful, the two photographs being nearly the same view, and its figures stay recorded. First revision: on the patent against 88.9 / 254, within 1.5 per cent and STOP over 3; it passed (+0.97 per cent at the worst), which stays recorded. Second revision, **made after the patent's figures were seen**, for the depth row's reasons: 5 and 8 per cent against 88.9 / 255 | both figures within 5 per cent | either over 8 per cent | as the depth row |
  | PAL front against NTSC front: three ratios on the front face (the door's width, the label band's height and the buttons' span, each to the face's width), O4 against O2-FL's front face rectified by a homography on its four corners, judged on the ratios O4 shows (the buttons' span is not on O4, the top shell alone, so two). **Revised twice on 5 Oct 2026, each time after task 0's figures were seen.** As first written, as now, and the first run landed between pass and stop (door +0.35, label band -3.49 per cent), which stays recorded. First revision (with the depth row): O4 against the patent's FIG 3 front instead, O2-FL recorded; that run crossed its STOP, the label band -8.08 per cent (door -1.01), which stays recorded. Second revision, **made after the patent's figures were seen**: back to O2-FL's front. The door's width over the face's width needs no camera: the rectification on four corners is planar, so for that ratio the first revision's reason for moving it does not apply. The label band's height does need one: on O2-FL it is read through the case's camera, the one the first revision found faulty, and O4 is taken to be straight on, so the band's figure (-3.49 per cent against the 4 per cent stop) is the weakest and the nearest a stop; this is known and disclosed. The patent's front figures are recorded, not judged | at least 2 ratios measured, each within 2 per cent | any ratio over 4 per cent | the PAL models stop, as the PAL layout |
  | Copper coverage, each face | 10 to 50 per cent of the board | | |
  | Drills in copper | at least 95 per cent on each face | | |
  | Known nets | at least 8 ICs; at least 90 per cent of their GND pins in one net and of their +5V pins in another; the two never connected | | |
  | IC pad counts | every IC's pad count is its package's | | |
  | IC places against the KiCad redrawing's, after a best-fit similarity | none more than 3 mm off (catches a swap) | | |

- **The solder side's outlier rule, fixed before any measurement:** a matched hole may be left out of the registration's scoring only if its pad fails the roundness test on either face (the blob's ellipse from its second moments has a major to minor axis ratio over 1.25, or its area is outside 0.6 to 1.6 times the median of the matched pads on that face). Nothing else excludes a hole. The held-out figures are reported with and without it; pass and STOP are judged on the figures without.
- **Budgets, set before building:** each model bundle at most 200,000 bytes gzipped (`BUDGET_GZIP` in `model.test.mjs`); the track map at most 600,000 bytes (one map for both consoles: the copper is the same); `public/model-loader.js` under 4,000 bytes; nothing under `/models/` in any page's first load.
- **The KIM-1 and the BBC Micro do not change.** Their pages, models, browser checks and tests pass without edits, except edits a task names. Tasks 1, 7 and 9 each build the previous commit and this one and `cmp` `dist/machines/kim-1/index.html` and `dist/machines/bbc-micro/index.html`: identical.
- **Our own models.** No model made by someone else is used. Non-commercial sources never. The KiCad redrawing (I2) only as numbers compared, never as geometry drawn.
- **No Nintendo logo shapes.** The case's words are drawn in the site's own face; no logo outline, image or path is in any module (a test in task 9). The board's printed legend is traced as it is, in the track map.
- **Colours are tokens.** Every colour a model uses is a `--model-*` token in `site/src/styles/tokens.css`, read through `stage.token`; `model.test.mjs` already fails on a colour literal in `site/src/models/`. A new text colour goes in the contrast table in `site/tests/design.test.mjs`. The lime is spent on nothing new. The case is drawn as made, in grey tokens, not yellowed.
- **The CSP is unchanged.** `site/public/_headers` is not edited.
- **No figure describing the project as it stands is typed by hand** (rule 5). Every figure on the page is read from `site/src/data/nes-famicom-board-model.json` or `site/src/data/nes-famicom-case-model.json`, which `tools/nes-model/results.py` writes. The legend's rates are live readings of the running machine.
- **Regions are lower-case words in `MODELS`:** `ntsc` and `pal`. Where the machine's page names them otherwise (the machine's design says `Ntsc` and `Pal`), the page script maps them in one place.
- **Document as you go** (rule 6). Every task ends by adding to that working day's journal file, `docs/journal/YYYY-MM-DD-the-nes-models.md` (the date `date` prints): create it with front matter (`title`, `date`, `summary`, `order` one higher than the highest existing) if it does not exist, else append a section. Decisions with what they were chosen over, findings with how they were checked, surprises, mistakes, and every figure as a dated measurement with its command. **No digits in a journal's title or summary.** A difference from a reference also goes in `docs/known-differences.md`.
- **Hygiene, every task:** no em or en dashes (`LC_ALL=C.UTF-8 git grep -InP '[\x{2013}\x{2014}]'` prints nothing); British English; warnings are errors in C#.
- **Test floors.** A task that adds site tests raises the floor in both `.github/workflows/validate.yml` and `deploy-site.yml` (the `[ "${PASS:-0}" -ge N ]` lines) to the pass count it measured, the same in both, with a dated comment. Never lower it.
- **The deploy's serving list** (`deploy-site.yml`, the `for f in ...` line) names every model bundle and the track map; `model.test.mjs` fails if one is missing.
- **When a fact sheet is wrong,** fix it in the same commit with a note, and say so in the journal.

## Review Focus

The inputs and conditions the spec implies that the tasks' main tests would not reach, most likely first. Each has its test in the task named.

1. **The machine restarts under the inside model** (a region change makes a new machine, so its counters start again from zero): the next difference is negative and, taken modulo 2^32, would show a rate of billions. Expected: on `nes:region` and `nes:start` the model takes a fresh baseline and shows no rate until the next sample. Task 7, step 1.
2. **Timers are throttled** (a background tab, a busy machine): the quarter-second sampler fires late. Expected: rates are divided by the time measured between the two snapshots (`performance.now()`), never by the nominal 0.25 s. Task 7, step 1.
3. **The region changes while a view is hidden or not yet loaded.** Expected: when the view is shown or loaded later, it draws the region the page has now, not the one it had when the page loaded. Task 9, step 1 and step 5.
4. **POWER or RESET is clicked on the model before Start** (the machine's WebAssembly downloads on Start). Expected: POWER does exactly what Start does; RESET does nothing to any machine and the status line says the machine is not running. Task 9, steps 1 and 5.
5. **The PAL models stopped at task 0**, so a module draws `['ntsc']` only, and the visitor chooses PAL. Expected: the section says in words that there is no model of the PAL console and why, and still shows the NTSC model, labelled as NTSC. Task 9, step 1 (a made-up `MODELS` entry), whichever way task 0 ended.

## File structure

```
tools/nes-model/                   # offline: measures both models; outputs committed; never run by the build
  README.md                        # how to fetch the inputs by hand, set NES_MODEL_INPUTS, make the venv, run
  common.py                        # inputs and hashes, image reading, pads, KiCad reading, held-out fits, JSON writing
  verify.py                        # checks every input against its SHA-256; no network
  spike.py                         # task 0: the eight checks; kept as a record, not in the run
  board_frame.py                   # task 2: the scan's x and y scale, the outline and the holes
  board_register.py                # task 3: the solder side registered; pads, drills, footprints
  board_trace.py                   # task 4: both copper faces and the print; nets; the track map
  board_parts.py                   # task 5: every part's place and identity, both consoles; writes nes-famicom-board-parts.mjs
  case_measure.py                  # task 8: the case's shape and features, both consoles; writes nes-famicom-case-parts.mjs
  results.py                       # writes the two results files the page reads
  run-board.sh, run-case.sh        # each script in order
  tests/test_*.py                  # pytest on made-up inputs
  data/sources.json                # every input: id, kind, URL, author, licence as stated, fetched, size, SHA-256
  data/README.md                   # which data file carries which terms
  data/marks.json                  # points marked by hand (ours)
  data/ic-table.json               # each IC's identity per console, pinout source, package (ours)
  data/spike.json, frame.json, registration.json, copper.json, parts.json, case.json   # measurements (ours)
site/src/models/
  nes-famicom-board.js             # the inside model (task 7)
  nes-famicom-board-layout.mjs     # its constants, describe(region), the chip legend
  nes-famicom-board-parts.mjs      # generated by board_parts.py
  nes-famicom-board-notes.mjs      # made(figures, region)
  nes-famicom-access.mjs           # accessRates and createSampler: marks and rates from the counters (task 7)
  nes-famicom-case.js              # the outside model (task 9)
  nes-famicom-case-layout.mjs      # its constants, describe(region)
  nes-famicom-case-parts.mjs       # generated by case_measure.py
  nes-famicom-case-notes.mjs       # made(figures, region)
site/src/data/nes-famicom-board-model.json, nes-famicom-case-model.json   # written by results.py
site/src/assets/tracks/nes-famicom-board.webp                            # written by board_trace.py
site/src/assets/photos/nes-famicom-*.webp                                # task 8, resized
site/tests/nes-models.test.mjs     # the NES models' data and geometry (task 0 onwards)
site/tests/nes-spike-verdicts.mjs, nes-spike-verdicts.test.mjs           # task 0's verdicts and their own test
site/tests/model-regions.test.mjs  # task 1
site/tests/nes-famicom-access.test.mjs                                   # task 7
src/Dbhq.Machines.Nes/ChipAccesses.cs                                    # task 6
tests/Dbhq.Machines.Nes.Tests/ChipAccessesTests.cs                       # task 6
```

Modified along the way: `machines/registry.json` (the NES row's `models`, `case`, `references`, `photos`, in tasks 7 to 9 only), `site/src/models/models.mjs`, `site/src/components/MachineModel.astro`, `site/src/pages/machines/[id].astro`, `site/public/model-loader.js` (only if the BBC Micro's loader lacks a hook the region needs), the NES's panel script, the NES's browser check, `site/scripts/page-weight.mjs` if needed, `site/src/styles/`, `site/tests/model.test.mjs`, `site/tests/design.test.mjs`, `site/src/assets/photos/README.md`, `site/DESIGN.md`, `NOTICE.md`, `README.md`, `AGENTS.md`, `docs/the-6502-family.md`, `docs/known-differences.md`, `docs/nes/facts/models.md`, both workflows, and in task 6 `src/Dbhq.Machines.Nes/NesBus.cs`, the NES host and their tests.

## Facts every task relies on

- **The sources, their licences and their uses** are `docs/nes/facts/models.md`. Its ids are the ids in `data/sources.json`: O1 the patent; O2-FL, O2-BL, O2-BR Evan-Amos's NES-001 corners; O4, O5 the PAL set; I1-front, I1-back the bare NES-CPU-10 scans; I2 the KiCad redrawing; I3 the PAL NES-CPU-11; I4, I5 Evan-Amos's NES-CPU-07; I6 the PAL CPU close up.
- **The inputs** were downloaded on 5 October 2026 to `~/dbhq-previews/nes-model-research/full/`, with I2 one folder up. Copy I2 into `full/` and set `NES_MODEL_INPUTS=~/dbhq-previews/nes-model-research/full`. If that folder is gone, fetch each by hand from the URL in `sources.json` (OpenTendo's from the fork).
- **The scan:** 2376 by 1492 pixels at 300 dpi, about 11.81 px/mm; component side with the edge fingers at the bottom, the expansion slot in the middle, the RF modulator's place at the lower right ("MOD RF"), the controller and power headers at the upper right.
- **The KiCad redrawing** (read on 5 October 2026 with `kicad_footprints` and `kicad_outline_box` below): outline 196.252 by 118.700 mm; U6 "RP2A03 CPU" and U5 "RP2C02 PPU" DIP-40 at 600 mil; U1 "6116 (WRAM)" and U4 "6116 (VRAM)" DIP-24 at 600 mil; U2 74HC373 DIP-20; U3 74HC139, U7 "40H368 (CI)", U8 "40H368 (CII)" and U10 CIC, DIP-16; U9 74LS04 DIP-14; X1 21.477272 MHz; X2 4 MHz; P1 the 72-pin connector (72 pads), P2 the expansion connector (48), P3 the RF modulator (6), P4 and P5 the controller inputs (7 each, JST PH at 2.0 mm), P6 power and reset (5). Every DIP lies the same way. The scan's print may name parts differently (it prints "74HCU04" at the hex inverter); the scan wins, and the difference is recorded.
- **Package geometry:** pin pitch 2.54 mm; pin 1 to pin N/2 is (N/2 - 1) x 2.54 mm: 48.26 mm for a 40-pin, 27.94 for a 24-pin, 22.86 for a 20-pin, 17.78 for a 16-pin, 15.24 for a 14-pin; rows 15.24 mm apart for 600 mil and 7.62 mm for 300 mil.
- **The consoles' parts that differ** (from the photographs, to be confirmed in task 5): NTSC on I4, RP2A03G, RP2C02G-0, CIC 3193A, MB8416A RAMs, the 21.477272 MHz crystal; PAL on I3, RP2A07A, RP2C07-0, CIC 3195A, XRM6216-10 RAMs, MB74LS373, MC74HC368N twice, SN74LS139N, SN74HCU04N, and the PAL crystal (its value read from I3 in task 5).
- **The case:** published width and depth about 10 by 8 inches (254 by 203.2 mm), from more than one source, none Nintendo's; published heights disagree, 76 to 89 mm (D1 gives 88.9).
- **The page's state:** the machine's panel script exposes `panel.nes` (the machine's pull request). This plan adds `accessCounts()`, `running`, `reset()` if absent, and the events `nes:start`, `nes:reset` and `nes:region` (`detail = { region }`, one of `ntsc`, `pal`), unless the machine's pull request already has them under these names.
- **The scene:** centimetres, as the other models. The board frame is millimetres from the board's top left corner as I1-front lies, x right, y down the scan towards the edge fingers. The case frame is millimetres from the case's left rear corner at table level, x right, y towards the front, z up.
- **The pattern for everything on the page** is the BBC Micro's models, once merged: `bbc-micro-board.js` and `-layout.mjs`, `-notes.mjs`, the legend in `MachineModel.astro`, the tabs in `model-loader.js`, `browser-check-bbc.mjs`. Adapt, do not copy blindly.
- **Commands:** site `cd site && npm run build && npm test` (the build needs `src/data/results.json`: `npm run results`, or CI's artifact); one file `cd site && node --test tests/nes-models.test.mjs`; weight `cd site && node scripts/page-weight.mjs /machines/nes-famicom/`; C# `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`, and `dotnet test -c Release` once at the end of a task that touches C#; tools `/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q`. The venv: `python3 -m venv /tmp/nesvenv && /tmp/nesvenv/bin/pip install numpy opencv-python-headless pillow scipy pytest`; record the versions in the journal.

---

### Task 0: The inputs, and the eight checks everything rests on

A short spike, before any pipeline. If a STOP is crossed, the task ends BLOCKED with the figures, the verdicts are committed as measured, and the plan is reconsidered with Dan before task 1. The checks are independent: a PAL stop does not stop the NTSC checks, and all eight are measured either way.

**Files:**
- Create: `tools/nes-model/README.md`, `common.py`, `verify.py`, `spike.py`, `tests/test_common.py`, `data/sources.json`, `data/README.md`, `data/marks.json`, `data/spike.json`
- Create: `site/tests/nes-models.test.mjs`, `site/tests/nes-spike-verdicts.mjs`, `site/tests/nes-spike-verdicts.test.mjs`
- Modify: `docs/nes/facts/models.md` (the probe values; anything the spike corrects)
- Create or append: `docs/journal/YYYY-MM-DD-the-nes-models.md`

**Interfaces:**
- Produces, in `common.py` (from the BBC Micro's, below): `TOOL`, `REPO`, `DATA`, `OUT`; `inputs_dir()`, `sources()`, `source(id)`, `sha256(path)`, `original(id)`, `read_rgb(id, mirror=False)`, `oklab(rgb)`, `pad_mask(rgb)`, `find_pads(rgb, origin)`, `refine_to_pad(rgb, x, y, px_per_mm, limit_mm)`, `kicad_footprints(path) -> dict`, `kicad_outline_box(path) -> tuple`, `fit_held_out(src, dst, model, folds) -> dict`, `transform(model, params, pts)`, `stats(errors)`, `write_data(name, obj, places=4, rows_per_line=False)`. `fit_held_out` returns `{"n", "params", "fitMm": {"median","p90","max"}, "heldOutMm": {...}, "worst": [[id, mm], ...]}` (unchanged from the BBC Micro's).
- Produces `data/spike.json`, read by `nes-spike-verdicts.mjs`:
  ```json
  { "scale": { "x": { "rows": 0, "scaledErrMm": { "median": 0, "max": 0 }, "largestRow": "" },
               "y": { "footprints": 0, "errPct": { "median": 0, "max": 0 }, "errPct600": { "median": 0 }, "errPct300": { "median": 0 } },
               "pxPerMm": { "x": 0, "y": 0 }, "ratio": 1, "statedDpi": 300 },
    "solder": { "model": "", "holes": 0, "heldOutMm": { "median": 0, "p90": 0, "max": 0 }, "outliers": { "excluded": 0, "heldOutMmWithExclusion": { "median": 0, "p90": 0, "max": 0 } } },
    "palLayout": { "parts": 0, "heldOutMm": { "median": 0, "max": 0 }, "perPart": [], "unmatched": [] },
    "case": { "depthToWidth": { "fl": 0, "br": 0 }, "depthToWidthErrPct": 0, "heightToWidth": { "fl": 0, "br": 0 }, "heightToWidthAgreePct": 0, "focalPx": { "exif": 0, "vanishing": { "fl": 0, "br": 0 } } },
    "palFront": { "ratios": [], "ratioErrPct": [] },
    "outline": { "scanMm": [0, 0], "kicadMm": [196.252, 118.7] },
    "verdicts": [ { "check": "", "verdict": "" } ] }
  ```
  `depthToWidthErrPct` is the worse of the two photographs' errors against 203.2 / 254, signed; `heightToWidthAgreePct` is 100 x |fl - br| / mean.

- [ ] **Step 1: The fork, with Dan's yes.** Ask Dan, in a question box, whether to fork `Redherring32/OpenTendo` into `dbhq-uk`. On his yes: `gh repo fork Redherring32/OpenTendo --org dbhq-uk --clone=false`, then check the pinned commit is there: `gh api repos/dbhq-uk/OpenTendo/commits/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009 --jq .sha` prints the full hash. On his no, stop the task and report: nothing reads OpenTendo until it is forked.

- [ ] **Step 2: The venv and the inputs.** Make the venv (Facts). `cp ~/dbhq-previews/nes-model-research/opentendo_Motherboard.kicad_pcb ~/dbhq-previews/nes-model-research/full/` and `export NES_MODEL_INPUTS=~/dbhq-previews/nes-model-research/full`.

- [ ] **Step 3: The tool's shared code, from the BBC Micro's.** From the repository root:

  ```bash
  mkdir -p tools/nes-model/tests tools/nes-model/data
  git show 16d1fa9:tools/bbc-micro-model/common.py \
    | sed -e 's/BBC_MODEL_INPUTS/NES_MODEL_INPUTS/g' -e 's#tools/bbc-micro-model#tools/nes-model#g' > tools/nes-model/common.py
  git show 16d1fa9:tools/bbc-micro-model/tests/test_common.py \
    | sed -e 's/BBC_MODEL_INPUTS/NES_MODEL_INPUTS/g' -e 's#tools/bbc-micro-model#tools/nes-model#g' -e 's#/tmp/bbcvenv#/tmp/nesvenv#g' \
          -e '1s/.*/"""The shared parts of the NES model tools, on made-up inputs./' > tools/nes-model/tests/test_common.py
  git show 16d1fa9:tools/bbc-micro-model/verify.py | sed -e 's/BBC_MODEL_INPUTS/NES_MODEL_INPUTS/g' > tools/nes-model/verify.py
  ```

  Then in `common.py`: replace the module docstring (everything up to the first `import`) with this one:

  ```python
  """What the NES model tools share: where things are, the inputs and their
  hashes, reading an image, finding the pads on a scan of the board, reading the
  KiCad redrawing as a cross-check, fitting one set of points to another with an
  error measured on points held out of the fit, and writing the data files.
  Adapted from tools/bbc-micro-model/common.py at commit 16d1fa9.

  Nothing here fetches anything. The inputs (scans, photographs, the patent and
  the KiCad redrawing) are fetched by hand into the folder NES_MODEL_INPUTS
  names, as the README says, and every one is checked against the SHA-256 in
  data/sources.json before it is read.

  The board frame is millimetres from the board's top left corner as scan
  I1-front lies, the component side up: x to the right, y down the scan, towards
  the edge fingers that the cartridge connector clamps onto. Where the board sits
  in the case is found later, from the photographs of the case opened.
  """
  ```

  Replace the section from `# --- the KiCad keyboard (K1)` up to (not including) `def _sexpr` with the header `# --- KiCad files ---...` (a line of dashes to column 84, as the file's other headers), keep `_sexpr` and `_child`, and replace `switch_centres` (up to `# --- fits ---`) with:

  ```python
  # --- the KiCad redrawing (I2): a cross-check, never a source of geometry -------

  def kicad_footprints(path) -> dict:
      """Every footprint in a KiCad 8 board: {reference: {"value", "footprint",
      "at": (x, y), "rotation", "pads"}}, in KiCad's millimetres (x to the
      right, y down, from above the component side). `pads` counts the
      footprint's plated pads. Used only to compare with what the scan shows."""
      import re
      root = _sexpr(Path(path).read_text(encoding='utf8'))
      out = {}
      for fp in root[1:]:
          if not (isinstance(fp, list) and fp and fp[0] == 'footprint'):
              continue
          at = _child(fp, 'at')
          props = {p[1]: p[2] for p in fp[1:] if isinstance(p, list) and p and p[0] == 'property'}
          pads = [p for p in fp[1:] if isinstance(p, list) and p and p[0] == 'pad' and p[2] != 'np_thru_hole']
          out[props['Reference']] = {
              'value': props.get('Value'),
              'footprint': re.sub(r'^.*:', '', fp[1]),
              'at': (float(at[1]), float(at[2])),
              'rotation': float(at[3]) if len(at) > 3 else 0.0,
              'pads': len({p[1] for p in pads}),
          }
      return out


  def kicad_outline_box(path) -> tuple:
      """The bounding box of the board's Edge.Cuts drawing, (x0, y0, x1, y1) in
      KiCad's millimetres: every start, end, centre-free point of its lines,
      arcs and rectangles. Arcs are taken by their three points, so a bulge
      beyond them is not seen; the NES board's edge is straight lines."""
      root = _sexpr(Path(path).read_text(encoding='utf8'))
      xs, ys = [], []
      for g in root[1:]:
          if not (isinstance(g, list) and g and g[0] in ('gr_line', 'gr_arc', 'gr_rect')):
              continue
          layer = _child(g, 'layer')
          if not layer or layer[1] != 'Edge.Cuts':
              continue
          for key in ('start', 'mid', 'end'):
              p = _child(g, key)
              if p:
                  xs.append(float(p[1]))
                  ys.append(float(p[2]))
      return min(xs), min(ys), max(xs), max(ys)
  ```

  In `tests/test_common.py`, replace the section from `# --- the KiCad keyboard` up to (not including) `def test_write_data_can_put_one_record_to_a_line` with:

  ```python
  # --- the KiCad redrawing (I2) ----------------------------------------------------

  KICAD = '''(kicad_pcb (version 20240108) (generator "pcbnew")
    (gr_line (start 10 20) (end 206.25 20) (layer "Edge.Cuts") (width 0.1))
    (gr_line (start 206.25 20) (end 206.25 138.7) (layer "Edge.Cuts") (width 0.1))
    (gr_arc (start 10 138.7) (mid 9 130) (end 10 120) (layer "Edge.Cuts") (width 0.1))
    (gr_line (start 0 0) (end 300 300) (layer "F.SilkS") (width 0.1))
    (footprint "Package_DIP:DIP-40_W15.24mm" (layer "F.Cu")
      (at 58.006 100.89 90)
      (property "Reference" "U6")
      (property "Value" "RP2A03 CPU")
      (pad "1" thru_hole rect (at 0 0) (size 1.6 1.6) (drill 0.8) (layers "*.Cu" "*.Mask"))
      (pad "2" thru_hole oval (at 0 2.54) (size 1.6 1.6) (drill 0.8) (layers "*.Cu" "*.Mask"))
      (pad "" np_thru_hole circle (at 5 5) (size 3 3) (drill 3) (layers "*.Cu" "*.Mask"))
    )
    (footprint "Crystal:Crystal_HC49-U_Horizontal_1EP_style2" (layer "F.Cu")
      (at 98.15 48.8)
      (property "Reference" "X1")
      (property "Value" "21.477272 MHz")
      (pad "1" thru_hole circle (at 0 0) (size 1.5 1.5) (drill 0.8) (layers "*.Cu" "*.Mask"))
      (pad "1" thru_hole circle (at 1 0) (size 1.5 1.5) (drill 0.8) (layers "*.Cu" "*.Mask"))
    )
  )
  '''


  def test_kicad_footprints_reads_place_turn_value_and_plated_pads(tmp_path):
      path = tmp_path / 'b.kicad_pcb'
      path.write_text(KICAD)
      f = common.kicad_footprints(path)
      assert set(f) == {'U6', 'X1'}
      assert f['U6'] == {'value': 'RP2A03 CPU', 'footprint': 'DIP-40_W15.24mm', 'at': (58.006, 100.89), 'rotation': 90.0, 'pads': 2}
      assert f['X1']['rotation'] == 0.0
      assert f['X1']['pads'] == 1          # two copper pads named "1" are one pad, as KiCad counts them


  def test_kicad_outline_box_reads_edge_cuts_only(tmp_path):
      path = tmp_path / 'b.kicad_pcb'
      path.write_text(KICAD)
      x0, y0, x1, y1 = common.kicad_outline_box(path)
      assert (x0, y0, x1, y1) == (9.0, 20.0, 206.25, 138.7)   # the silkscreen line to (300, 300) is not the edge
  ```

  Run: `/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q`. Expected: 22 passed. Then `grep -n "BBC\|bbc\|keyboard" tools/nes-model/*.py tools/nes-model/tests/*.py` prints only the docstring's "Adapted from" line.

- [ ] **Step 4: `data/sources.json`.** Write it exactly as below (every size and hash was read from the downloaded files on 5 October 2026; the OpenTendo URLs are the fork's, which step 1 made):

  ```json
  {
    "sources": [
      {
        "id": "O1",
        "kind": "drawing",
        "file": "USD299726.pdf",
        "url": "https://patentimages.storage.googleapis.com/pdfs/USD299726.pdf",
        "page": "https://commons.wikimedia.org/wiki/File:NES_patented_design.png",
        "author": "Masayuki Yukawa, for Nintendo Co., Ltd.",
        "licence": "Public domain (a United States design patent; Commons tags its copy of sheet 1 PD-US-Patent)",
        "fetched": "2026-10-05",
        "bytes": 140750,
        "width": null,
        "height": null,
        "sha256": "4c699b1b31a40ddf4a80f9827b7c927058dd54979acdd1988aa94c3060775f3d",
        "committed": null,
        "use": "US Design Patent D299,726, the NES-001: front, rear, top, bottom and both sides as orthographic views. A check on the proportions the photographs give"
      },
      {
        "id": "O2-FL",
        "kind": "photograph",
        "file": "Nintendo-Entertainment-System-NES-Console-FL.jpg",
        "url": "https://upload.wikimedia.org/wikipedia/commons/8/82/Nintendo-Entertainment-System-NES-Console-FL.jpg",
        "page": "https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-FL.jpg",
        "author": "Evan-Amos",
        "licence": "Public domain",
        "fetched": "2026-10-05",
        "bytes": 2193817,
        "width": 4020,
        "height": 2880,
        "sha256": "53c4ff11da6ba56bc2d9d8138dd585d126e08fe22350e8e0363003f9ddb8ab3c",
        "committed": null,
        "use": "The NTSC NES-001 from the front left: the case's proportions with BR, the front, the buttons, the LED, the controller ports, the door, the colours"
      },
      {
        "id": "O2-BL",
        "kind": "photograph",
        "file": "Nintendo-Entertainment-System-NES-Console-BL.jpg",
        "url": "https://upload.wikimedia.org/wikipedia/commons/5/56/Nintendo-Entertainment-System-NES-Console-BL.jpg",
        "page": "https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-BL.jpg",
        "author": "Evan-Amos",
        "licence": "Public domain",
        "fetched": "2026-10-05",
        "bytes": 2269902,
        "width": 4020,
        "height": 2880,
        "sha256": "e068546c7b4ce5e2e5709c68b499bce9c7f5b2559e9b992650d74dc7e366be9d",
        "committed": null,
        "use": "The NTSC NES-001 from the rear left: the rear and the left side, their connectors"
      },
      {
        "id": "O2-BR",
        "kind": "photograph",
        "file": "Nintendo-Entertainment-System-NES-Console-BR.jpg",
        "url": "https://upload.wikimedia.org/wikipedia/commons/c/cf/Nintendo-Entertainment-System-NES-Console-BR.jpg",
        "page": "https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-BR.jpg",
        "author": "Evan-Amos",
        "licence": "Public domain",
        "fetched": "2026-10-05",
        "bytes": 2230258,
        "width": 4020,
        "height": 2880,
        "sha256": "0681170937ae3af9db3fc8898e155792e295dacb5cb73942f8d2371c8733907f",
        "committed": null,
        "use": "The NTSC NES-001 from the rear right: the case's proportions with FL, the rear and the right side"
      },
      {
        "id": "O4",
        "kind": "photograph",
        "file": "Geöffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png",
        "url": "https://upload.wikimedia.org/wikipedia/commons/d/d0/Ge%C3%B6ffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png",
        "page": "https://commons.wikimedia.org/wiki/File:Ge%C3%B6ffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png",
        "author": "PantheraLeo1359531",
        "licence": "CC BY 4.0",
        "fetched": "2026-10-05",
        "bytes": 92882699,
        "width": 8606,
        "height": 2253,
        "sha256": "3cf7eca8f43bafd3a8f341127d6c6f728acd2da3d2deb2464d93b66681b913d6",
        "committed": null,
        "use": "The PAL NESE-001's top shell, straight on from the front: its label, and its front face against the NTSC case's"
      },
      {
        "id": "O5",
        "kind": "photograph",
        "file": "Unterseite_NES_NESE-001_20221102_132229.jpg",
        "url": "https://upload.wikimedia.org/wikipedia/commons/2/23/Unterseite_NES_NESE-001_20221102_132229.jpg",
        "page": "https://commons.wikimedia.org/wiki/File:Unterseite_NES_NESE-001_20221102_132229.jpg",
        "author": "PantheraLeo1359531",
        "licence": "CC BY 4.0",
        "fetched": "2026-10-05",
        "bytes": 3025854,
        "width": 4000,
        "height": 3000,
        "sha256": "a8e10ed2cf0148beccab8f7979f0a92497b01846df684e6c5260c56d4c9a4c27",
        "committed": null,
        "use": "The PAL NESE-001's underside: the expansion port cover, the feet and the rating label"
      },
      {
        "id": "I1-front",
        "kind": "scan",
        "file": "opentendo_NES-CPU-10_front_300dpi.png",
        "url": "https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Scans/NES-CPU-10_front_300dpi.png",
        "page": "https://github.com/Redherring32/OpenTendo/blob/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/README.md",
        "author": "OpenTendo contributors (added by Kamoteshake; the scanner is not named)",
        "licence": null,
        "fetched": "2026-10-05",
        "bytes": 6354680,
        "width": 2376,
        "height": 1492,
        "sha256": "fd41c714258a4d379d034eaf39cdcfcc7aab5273f4dafb74ff8517b55c88ab3a",
        "committed": null,
        "use": "A bare NES-CPU-10, component side, 300 dpi: the board frame, its outline, holes and pads, the top copper and the print. The scan states no licence; the repository's README states the TAPR Open Hardware License"
      },
      {
        "id": "I1-back",
        "kind": "scan",
        "file": "opentendo_NES-CPU-10_back_300dpi.png",
        "url": "https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Scans/NES-CPU-10_back_300dpi.png",
        "page": "https://github.com/Redherring32/OpenTendo/blob/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/README.md",
        "author": "OpenTendo contributors (added by Kamoteshake; the scanner is not named)",
        "licence": null,
        "fetched": "2026-10-05",
        "bytes": 6710399,
        "width": 2376,
        "height": 1492,
        "sha256": "fa15ea9e5a57c8621932fa4cbd8b8121feba82a746cc996627f95b6d12b8a0a5",
        "committed": null,
        "use": "The same board, solder side, 300 dpi: registered to the front by drill centres, the bottom copper"
      },
      {
        "id": "I2",
        "kind": "drawing",
        "file": "opentendo_Motherboard.kicad_pcb",
        "url": "https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Board%20Files/Motherboard.kicad_pcb",
        "page": "https://github.com/Redherring32/OpenTendo/blob/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/README.md",
        "author": "Redherring32 and the OpenTendo contributors",
        "licence": "TAPR Open Hardware License (the repository's README)",
        "fetched": "2026-10-05",
        "bytes": 10954333,
        "width": null,
        "height": null,
        "sha256": "9cce8323c9c18f0c583f99d1e07d7650f85858ca2599cbf52b2c75f384e8224a",
        "committed": null,
        "use": "A redrawing of the front-loader's board in KiCad 8: its outline and part places compared with the scan's. Nothing is drawn from it"
      },
      {
        "id": "I3",
        "kind": "photograph",
        "file": "Frontalansicht_Mainboard_NES_NESE-001_HOF06378.png",
        "url": "https://upload.wikimedia.org/wikipedia/commons/6/65/Frontalansicht_Mainboard_NES_NESE-001_HOF06378.png",
        "page": "https://commons.wikimedia.org/wiki/File:Frontalansicht_Mainboard_NES_NESE-001_HOF06378.png",
        "author": "PantheraLeo1359531",
        "licence": "CC BY 4.0",
        "fetched": "2026-10-05",
        "bytes": 76772853,
        "width": 5462,
        "height": 3966,
        "sha256": "e9f606535f4b01507a62f185ce38aa3403862bb82f434cc51f148a90088ea8cd",
        "committed": null,
        "use": "A populated NES-CPU-11 \"PAL-EEC\", top side: its layout against the scan's, and the PAL parts and their markings"
      },
      {
        "id": "I4",
        "kind": "photograph",
        "file": "Nintendo-NES-Mk1-Motherboard-Top.jpg",
        "url": "https://upload.wikimedia.org/wikipedia/commons/5/5a/Nintendo-NES-Mk1-Motherboard-Top.jpg",
        "page": "https://commons.wikimedia.org/wiki/File:Nintendo-NES-Mk1-Motherboard-Top.jpg",
        "author": "Evan-Amos",
        "licence": "Public domain",
        "fetched": "2026-10-05",
        "bytes": 10715307,
        "width": 4570,
        "height": 3330,
        "sha256": "2158318ca6e7c913fce4220e8763dc8df4b37e70fea50cf29a1c975556a5b46c",
        "committed": null,
        "use": "A populated NES-CPU-07, top side, flat: the NTSC parts' identities and markings, the RF modulator, the cartridge connector"
      },
      {
        "id": "I5",
        "kind": "photograph",
        "file": "Nintendo-NES-Mk1-Motherboard-Bottom.jpg",
        "url": "https://upload.wikimedia.org/wikipedia/commons/4/43/Nintendo-NES-Mk1-Motherboard-Bottom.jpg",
        "page": "https://commons.wikimedia.org/wiki/File:Nintendo-NES-Mk1-Motherboard-Bottom.jpg",
        "author": "Evan-Amos",
        "licence": "Public domain",
        "fetched": "2026-10-05",
        "bytes": 9247611,
        "width": 5280,
        "height": 3690,
        "sha256": "64d52d1dbedfd123a56780def11821ef4157694e4d88d701924d567e2cb1ae10",
        "committed": null,
        "use": "The same board, solder side: what is soldered where, as a check on the back scan"
      },
      {
        "id": "I6",
        "kind": "photograph",
        "file": "RP2A07A_20221102.png",
        "url": "https://upload.wikimedia.org/wikipedia/commons/f/f5/RP2A07A_20221102.png",
        "page": "https://commons.wikimedia.org/wiki/File:RP2A07A_20221102.png",
        "author": "PantheraLeo1359531",
        "licence": "CC BY 4.0",
        "fetched": "2026-10-05",
        "bytes": 2438824,
        "width": 1206,
        "height": 408,
        "sha256": "99208c2054e674c68aa78942f2c784873b13463d7bd5f2d649ec3e96c7e68c72",
        "committed": null,
        "use": "The PAL CPU close up: its marking"
      }
    ]
  }
  ```

  Run: `cd tools/nes-model && python3 verify.py`. Expected: 13 lines "present, SHA-256 matches", exit 0.

- [ ] **Step 5: The site tests, first.** Create `site/tests/nes-models.test.mjs`:

  ```js
  import test from 'node:test';
  import assert from 'node:assert/strict';
  import crypto from 'node:crypto';
  import fs from 'node:fs';
  import path from 'node:path';
  import { REPO_ROOT } from '../src/lib/registry.mjs';

  // The NES's two 3D models, each drawn for the NTSC and the PAL console: their
  // inputs and the measurements they rest on. The tools in tools/nes-model/ run
  // offline, by hand; these tests read what they committed.

  const TOOL = path.join(REPO_ROOT, 'tools', 'nes-model');
  const sources = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'sources.json'), 'utf8')).sources;

  const KINDS = ['scan', 'photograph', 'drawing', 'document', 'model'];
  const FIELDS = ['id', 'kind', 'file', 'url', 'page', 'author', 'licence', 'fetched', 'bytes', 'width', 'height', 'sha256', 'committed', 'use'];

  test('every input in the NES models\' sources.json is fully described', () => {
    assert.ok(sources.length >= 13, `only ${sources.length} inputs`);
    assert.equal(new Set(sources.map((s) => s.id)).size, sources.length, 'an id is used twice');
    for (const s of sources) {
      for (const f of FIELDS) assert.ok(f in s, `${s.id} has no ${f}`);
      assert.ok(KINDS.includes(s.kind), `${s.id}: kind ${s.kind}`);
      assert.match(s.sha256, /^[0-9a-f]{64}$/, `${s.id}: sha256`);
      assert.ok(s.licence === null || (typeof s.licence === 'string' && s.licence.length > 0), `${s.id}: licence is a string or null`);
      assert.match(s.fetched, /^\d{4}-\d{2}-\d{2}$/, `${s.id}: fetched`);
      assert.ok(!Number.isNaN(Date.parse(s.fetched)), `${s.id}: fetched is not a date`);
      assert.ok(Number.isInteger(s.bytes) && s.bytes > 0, `${s.id}: bytes`);
      for (const d of ['width', 'height']) {
        if (s.kind === 'scan' || s.kind === 'photograph') assert.ok(Number.isInteger(s[d]) && s[d] > 0, `${s.id}: ${d}`);
        else assert.ok(s[d] === null || Number.isInteger(s[d]), `${s.id}: ${d}`);
      }
      for (const u of ['url', 'page']) assert.match(s[u], /^https?:\/\/\S+$/, `${s.id}: ${u}`);
      assert.ok(typeof s.author === 'string' && s.author.length > 0, `${s.id}: author`);
      assert.ok(typeof s.file === 'string' && s.file.length > 0 && !path.isAbsolute(s.file), `${s.id}: file`);
      assert.ok(typeof s.use === 'string' && s.use.length > 0, `${s.id}: use`);
      if (s.licence === null || s.kind === 'scan') assert.equal(s.committed, null, `${s.id} has no stated licence or is a scan, so it is never committed`);
      if (s.committed !== null) assert.ok(fs.existsSync(path.join(process.cwd(), 'src', 'assets', 'photos', s.committed)), `${s.id}: ${s.committed} is not in src/assets/photos`);
    }
  });

  test('OpenTendo\'s inputs are read from the dbhq-uk fork at the pinned commit', () => {
    const PIN = '3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009';
    for (const id of ['I1-front', 'I1-back', 'I2']) {
      const s = sources.find((x) => x.id === id);
      assert.ok(s, `${id} is not in sources.json`);
      assert.ok(s.url.startsWith(`https://github.com/dbhq-uk/OpenTendo/raw/${PIN}/`), `${id}: ${s.url} is not the fork at the pinned commit`);
    }
  });

  test('the inputs\' hashes are the ones the research recorded in models.md', () => {
    const md = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'nes', 'facts', 'models.md'), 'utf8');
    const section = md.split(/^## Downloaded for the work$/m)[1];
    assert.ok(section, 'models.md has no "Downloaded for the work" section');
    const rows = [...section.matchAll(/^\| (\S+) \| `([^`]+)` \| (\d+) \| `([0-9a-f]{64})` \|$/gm)].map(([, id, file, bytes, sha]) => ({ id, file, bytes: Number(bytes), sha }));
    assert.ok(rows.length >= 13, `only ${rows.length} rows read from the table`);
    for (const r of rows) {
      const s = sources.find((x) => x.id === r.id);
      assert.ok(s, `${r.id} is in models.md but not in sources.json`);
      assert.deepEqual([s.file, s.bytes, s.sha256], [r.file, r.bytes, r.sha], `${r.id}: sources.json and models.md disagree`);
    }
    for (const s of sources) assert.ok(rows.some((r) => r.id === s.id), `${s.id} is not in models.md's table`);
  });

  // Hashes every file under tools/, site/src/assets/, site/public/ and docs/: an
  // original copied anywhere a page or a tool could ship it fails.
  test('no original NES input is committed, under tools/, site/src/assets/, site/public/ or docs/', () => {
    const originals = new Map(sources.map((s) => [s.sha256, s.id]));
    const skip = new Set(['node_modules', 'bin', 'obj', '__pycache__', '.pytest_cache', 'out']);
    let hashed = 0;
    const walk = (dir) => {
      for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        if (skip.has(entry.name)) continue;
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) walk(full);
        else if (entry.isFile()) {
          const sha = crypto.createHash('sha256').update(fs.readFileSync(full)).digest('hex');
          hashed += 1;
          assert.ok(!originals.has(sha), `${path.relative(REPO_ROOT, full)} is the original of ${originals.get(sha)}`);
        }
      }
    };
    walk(path.join(REPO_ROOT, 'tools'));
    walk(path.join(process.cwd(), 'src', 'assets'));
    walk(path.join(process.cwd(), 'public'));
    walk(path.join(REPO_ROOT, 'docs'));
    assert.ok(hashed > 20, `only ${hashed} files hashed: the walk is looking in the wrong place`);
  });

  test('the site never runs the NES model tools', () => {
    const pkg = fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8');
    assert.doesNotMatch(pkg, /nes-model|python/);
  });
  ```

  Run: `cd site && node --test tests/nes-models.test.mjs`. Expected: 5 pass. Then prove two of them can fail, and undo both: change one hash in `docs/nes/facts/models.md`'s table and copy `$NES_MODEL_INPUTS/RP2A07A_20221102.png` to `tools/nes-model/data/x.png`; expected, "the inputs' hashes" and "no original NES input is committed" fail. Restore the table, delete the copy, run again: 5 pass.

- [ ] **Step 6: The verdicts, test first.** Create `site/tests/nes-spike-verdicts.test.mjs`:

  ```js
  import test from 'node:test';
  import assert from 'node:assert/strict';
  import { verdicts } from './nes-spike-verdicts.mjs';

  const good = {
    scale: { x: { rows: 6, scaledErrMm: { median: 0.08, max: 0.4 } }, y: { footprints: 10, errPct: { median: 0.3 } }, ratio: 1.004 },
    solder: { holes: 400, heldOutMm: { median: 0.12, p90: 0.3, max: 0.9 } },
    palLayout: { parts: 13, heldOutMm: { median: 0.6, max: 1.5 }, unmatched: [] },
    case: { depthToWidthErrPct: -0.8, heightToWidthAgreePct: 1.1 },
    palFront: { ratioErrPct: [0.5, -1.2, 0.9] },
  };
  const names = (s) => verdicts(s).map((x) => x.verdict);
  const tweak = (path, value) => { const s = structuredClone(good); let o = s; path.slice(0, -1).forEach((k) => { o = o[k]; }); o[path.at(-1)] = value; return s; };

  test('figures inside every pass limit pass every check; the largest x row error is recorded, not judged', () => {
    assert.deepEqual(names(good), Array(8).fill('pass'));
  });

  test('each check stops on its own figure, and only that check', () => {
    const cases = [
      [['scale', 'x', 'scaledErrMm', 'median'], 0.26, 0],
      [['scale', 'y', 'errPct', 'median'], 1.1, 1],
      [['scale', 'ratio'], 1.016, 2],
      [['solder', 'heldOutMm', 'p90'], 0.61, 3],
      [['palLayout', 'heldOutMm', 'max'], 3.1, 4],
      [['palLayout', 'unmatched'], ['U9'], 4],
      [['case', 'depthToWidthErrPct'], -3.2, 5],
      [['palFront', 'ratioErrPct'], [0.5, 4.5, 0.9], 7],
    ];
    for (const [p, value, i] of cases) {
      const got = names(tweak(p, value));
      assert.equal(got[i], 'STOP', `${p.join('.')} = ${JSON.stringify(value)}`);
      assert.equal(got.filter((x) => x === 'STOP').length, 1, `${p.join('.')} stops another check too`);
    }
  });

  test('between the limits is neither a pass nor a stop, and too few points is never a pass', () => {
    assert.equal(names(tweak(['scale', 'x', 'scaledErrMm', 'median'], 0.2))[0], 'between pass and stop');
    assert.equal(names(tweak(['solder', 'holes'], 149))[3], 'between pass and stop');
    assert.equal(names(tweak(['palLayout', 'parts'], 9))[4], 'between pass and stop');
    assert.equal(names(tweak(['case', 'heightToWidthAgreePct'], 6))[6], 'between pass and stop');
  });
  ```

  Run: `cd site && node --test tests/nes-spike-verdicts.test.mjs`. Expected: fails, the module is missing. Create `site/tests/nes-spike-verdicts.mjs`:

  ```js
  // The plan's task 0 thresholds, set before the measurements
  // (docs/superpowers/plans/2026-10-05-nes-models.md, Global Constraints).
  export const PASS = { xMedian: 0.15, yMedianPct: 0.5, solderMedian: 0.20, solderP90: 0.40, palMedian: 1.0, palMax: 2.0, depthPct: 1.5, heightAgreePct: 2.0, palFrontPct: 2.0 };
  export const STOP = { xMedian: 0.25, yMedianPct: 1.0, ratioPct: 1.5, solderMedian: 0.30, solderP90: 0.60, palAny: 3.0, depthPct: 3.0, palFrontPct: 4.0 };
  export const MIN = { xRows: 4, yFootprints: 8, solderHoles: 150, palParts: 10 };

  /** Each check's verdict from spike.json's figures: 'pass', 'between pass and stop' or 'STOP', in the table's order. */
  export function verdicts(s) {
    const v = (ok, stop) => (stop ? 'STOP' : ok ? 'pass' : 'between pass and stop');
    const so = s.solder.heldOutMm;
    const pal = s.palLayout;
    const depth = Math.abs(s.case.depthToWidthErrPct);
    const agree = Math.abs(s.case.heightToWidthAgreePct);
    const front = Math.max(...s.palFront.ratioErrPct.map(Math.abs));
    return [
      { check: 'scale x', verdict: v(s.scale.x.scaledErrMm.median <= PASS.xMedian && s.scale.x.rows >= MIN.xRows, s.scale.x.scaledErrMm.median > STOP.xMedian) },
      { check: 'scale y', verdict: v(s.scale.y.errPct.median <= PASS.yMedianPct && s.scale.y.footprints >= MIN.yFootprints, s.scale.y.errPct.median > STOP.yMedianPct) },
      { check: 'x against y', verdict: v(true, 100 * Math.abs(s.scale.ratio - 1) > STOP.ratioPct) },
      { check: 'solder side', verdict: v(so.median <= PASS.solderMedian && so.p90 <= PASS.solderP90 && s.solder.holes >= MIN.solderHoles, so.median > STOP.solderMedian || so.p90 > STOP.solderP90) },
      { check: 'PAL layout', verdict: v(pal.heldOutMm.median <= PASS.palMedian && pal.heldOutMm.max <= PASS.palMax && pal.parts >= MIN.palParts, pal.heldOutMm.max > STOP.palAny || pal.unmatched.length > 0) },
      { check: 'case depth', verdict: v(depth <= PASS.depthPct, depth > STOP.depthPct) },
      { check: 'case height, the two photographs', verdict: v(agree <= PASS.heightAgreePct, false) },
      { check: 'PAL front', verdict: v(front <= PASS.palFrontPct, front > STOP.palFrontPct) },
    ];
  }
  ```

  Run it again. Expected: 3 pass.

- [ ] **Step 7: The pads on this scan.** The BBC Micro's pad constants (`PAD_MIN_L`, `PAD_MAX_CHROMA`, `PAD_AREA_PX`) were read off its own scans at 15.7 px/mm. Read this scan's afresh on I1-front and I1-back (the 1st and 99th percentiles of a crop of at least 30 pads and of the lacquer, as the BBC Micro's comment says), set them in `common.py` with the probe values in a comment, and move the pad tests in `tests/test_common.py` to a synthetic board at 11.81 px/mm with this scan's pad size. Run pytest: pass.

- [ ] **Step 8: Scale.** Mark by hand in `data/marks.json` the first and last pin of every DIP row (both rows of all ten ICs) and the two end fingers of the edge connector on both faces of I1-front, each refined with `refine_to_pad`, plus the drill centres of every DIP's pin 1 and its opposite pin (pin N on the other row) on the hole's top rim. `spike.py` fits x from the 2.54 mm rows that run across the board and y from the DIP row spacings on drill centres, each held out as the table says, and writes `spike.json`'s `scale`. Record the scan's outline from its edges against the KiCad outline in `outline`.

- [ ] **Step 9: The solder side.** Find drill centres on I1-front and on I1-back mirrored. Seed an affine from four holes marked by hand in `marks.json`, match by nearest neighbour, fit an affine and a cubic, score each on a chequerboard of 20 mm blocks held out (the simpler wins a tie), apply the outlier rule as written, and write `spike.json`'s `solder`.

- [ ] **Step 10: The PAL layout.** On I3, mark the centre of each part's pins where they meet the board (both end pins of each DIP row, both ends of P1 and P2, P3's two outer pins), in `marks.json` with the part named. Do the same on I1-front from its pads. Fit a homography from I3 to I1 leaving each part out in turn, and write `palLayout` with every part's held-out error in `perPart` and any footprint seen on one board and not the other in `unmatched`. Say in the journal how the 20 mm lens was allowed for.

- [ ] **Step 11: The case.** On O2-FL and O2-BR, mark the case top's four corners and the bottom of the two nearest vertical edges. With the camera from the Exif (focal length in pixels = 112 / 23.6 x 4020, about 19078; record it), checked against the focal length the case top's two vanishing points give (record both), recover the box's depth to width and height to width in each photograph. Write `case`. If the two focal lengths differ by more than 10 per cent, say so in the journal and use the vanishing points' value. **Revised on 5 Oct 2026 after task 0's figures were seen** (the thresholds table's case rows): the photographs' figures above are recorded, not judged, and the first verdict, a STOP at -8.77 per cent, stays recorded. The judged figures come from O1: take sheets 2 and 3 out of the PDF at their 300 dpi, measure the outline extents of FIG 3, 5, 6 and 7 (the body, without the buttons and the feet; the overall extents recorded beside), and write depth to width from FIG 5, from FIG 6 and from FIG 7 over FIG 3, and height to width from FIG 3 and from FIG 7, in `case.patent`. **Revised again on 5 Oct 2026, after the patent's figures were seen** (the thresholds table's case rows): the patent's figures are judged against the published ratios' midpoints, 0.797 and 0.349, within 5 per cent and STOP over 8; the first revision's figures against 203.2 / 254 and 88.9 / 254 (a STOP at -3.46 per cent, and a pass) stay recorded beside them.

- [ ] **Step 12: The PAL front.** Rectify O2-FL's front face by a homography on its four corners, and measure the three ratios (door width, label strip height, buttons' span, each over the face's width) on it and on O4. Write `palFront`. **Revised on 5 Oct 2026 after task 0's figures were seen:** measure the three ratios on O1's FIG 3 front view as well; O4 is judged against FIG 3, on the ratios O4 shows (at least 2), and O2-FL's figures are recorded. **Revised again on 5 Oct 2026, after the patent's figures were seen:** O4 is judged against O2-FL's front, as first written, on the ratios O4 shows (at least 2). The door's width needs no camera (the rectification on four corners is planar); the label band's height on O2-FL is read through the case's camera, which the first revision found faulty, and O4 is taken to be straight on, so the band's figure is the weakest and the nearest a stop, known and disclosed; the patent's FIG 3 figures are recorded, and the first revision's verdict (a STOP, the label band -8.08 per cent against FIG 3) stays recorded.

- [ ] **Step 13: The verdicts, recorded.** `spike.py` writes `verdicts` from the same rules as `nes-spike-verdicts.mjs` (port it; the site test below checks they agree). Add to `site/tests/nes-models.test.mjs`:

  ```js
  import { verdicts } from './nes-spike-verdicts.mjs';
  const spike = JSON.parse(fs.readFileSync(path.join(TOOL, 'data', 'spike.json'), 'utf8'));

  test('spike.json records the verdicts its figures give against the plan\'s thresholds', () => {
    assert.deepEqual(spike.verdicts, verdicts(spike));
  });
  ```

  (the two lines at the top go with the file's other imports and constants). Run the whole site suite; raise both floors to the measured count.

- [ ] **Step 14: README and data README.** The tool's README in the BBC Micro's shape: what it is, that it is offline, how to fetch each input by hand (URLs in markdown, OpenTendo's from the fork), the venv, `NES_MODEL_INPUTS`, `verify.py`. `data/README.md`: each file and its terms; `marks.json` and `spike.json` MIT (ours); the sources' own terms are in `sources.json`.

- [ ] **Step 15: Journal.** Create the day's entry. First the design's decisions, from the spec's table, each with what it was chosen over; then the spike: the venv's versions, each figure with `NES_MODEL_INPUTS=... /tmp/nesvenv/bin/python spike.py` as the command, the probe values, the fits chosen and why, and any check near its limit. If a STOP was crossed, say which, and what it stops.

- [ ] **Step 16: Commit.** Hygiene checks. `git add` by name. `feat(models): the NES's model inputs, checked, and the eight checks they rest on`. If any STOP was crossed, stop here and tell Dan, one question at a time: for a PAL stop, whether NTSC goes on alone; for any other, that the models stop and the machine still counts.

---

### Task 1: A model can draw more than one console

**Files:**
- Modify: `site/src/models/models.mjs` (append the two functions below; mention `regions` in the "To add a model" comment), `site/src/pages/machines/[id].astro` and `site/src/components/MachineModel.astro` (read a model's words through `wordsFor(entry)`, not `entry.label`, `entry.about` and `entry.made` directly)
- Create: `site/tests/model-regions.test.mjs`

**Interfaces:**
- Produces, in `models.mjs`: `wordsFor(entry, region = null) -> { label, about, made }` and `regionProblems(module, entry) -> string[]`. An entry may carry `regions: ['ntsc', 'pal']`, with `label`, `about` and `made` keyed by region.

- [ ] **Step 1: The failing test.** Create `site/tests/model-regions.test.mjs`:

  ```js
  import test from 'node:test';
  import assert from 'node:assert/strict';
  import { MODELS, wordsFor, regionProblems } from '../src/models/models.mjs';

  const made = () => 'note';
  const both = {
    machine: 'nes-famicom', view: 'inside', regions: ['ntsc', 'pal'],
    label: { ntsc: 'NTSC label', pal: 'PAL label' },
    about: { ntsc: 'NTSC caption', pal: 'PAL caption' },
    made: { ntsc: made, pal: made },
  };

  test('a model with one console has one set of words, whatever region is asked for by default', () => {
    const kim = MODELS['kim-1'];
    assert.deepEqual(wordsFor(kim), { label: kim.label, about: kim.about, made: kim.made });
  });

  test('a model that draws two consoles gives each its own words, the first region by default', () => {
    assert.deepEqual(wordsFor(both), { label: 'NTSC label', about: 'NTSC caption', made });
    assert.equal(wordsFor(both, 'pal').about, 'PAL caption');
    assert.throws(() => wordsFor(both, 'dendy'), /draws ntsc and pal, not dendy/);
  });

  test('a model\'s regions are checked: listed once, each with a label, a caption and a note, and nothing else keyed', () => {
    for (const [module, entry] of Object.entries(MODELS)) assert.deepEqual(regionProblems(module, entry), [], module);
    assert.deepEqual(regionProblems('m', both), []);
    assert.deepEqual(regionProblems('m', { ...both, regions: [] }), ['m: regions, when given, must be a non-empty list']);
    assert.deepEqual(regionProblems('m', { ...both, regions: ['ntsc', 'ntsc'], label: { ntsc: 'a' }, about: { ntsc: 'b' }, made: { ntsc: made } }), ['m: a region is listed twice']);
    assert.deepEqual(regionProblems('m', { ...both, about: { ntsc: 'NTSC caption' } }), ['m: no about for pal']);
    assert.deepEqual(regionProblems('m', { ...both, made: { ntsc: made } }), ['m: made has no note for pal']);
    assert.deepEqual(regionProblems('m', { ...both, label: { ...both.label, dendy: 'x' } }), ['m: label has dendy, which is not in regions']);
    assert.deepEqual(regionProblems('m', { ...both, regions: ['NTSC', 'pal'], label: { NTSC: 'a', pal: 'b' }, about: { NTSC: 'a', pal: 'b' }, made: { NTSC: made, pal: made } }), ['m: region "NTSC" must be a lower-case word']);
  });
  ```

  Run: `cd site && node --test tests/model-regions.test.mjs`. Expected: fails, `wordsFor` is not exported.

- [ ] **Step 2: Implement.** Append to `site/src/models/models.mjs`:

  ```js
  /**
   * A model's words for one console: its accessible name, its caption and its
   * note on how it was made. A model that draws more than one console (the NES's,
   * NTSC and PAL) lists them in `regions` and keys `label`, `about` and `made` by
   * region; the first region is the one shown until the page names another. A
   * model with no `regions` draws one console and has one of each.
   */
  export function wordsFor(entry, region = null) {
    if (!entry.regions) return { label: entry.label, about: entry.about, made: entry.made };
    const r = region ?? entry.regions[0];
    if (!entry.regions.includes(r)) throw new Error(`this model draws ${entry.regions.join(' and ')}, not ${r}`);
    return { label: entry.label[r], about: entry.about[r], made: entry.made?.[r] };
  }

  /** What is wrong with a model's `regions`, as sentences: none for a model that has none. */
  export function regionProblems(module, entry) {
    if (entry.regions === undefined) return [];
    const errors = [];
    if (!Array.isArray(entry.regions) || entry.regions.length === 0) return [`${module}: regions, when given, must be a non-empty list`];
    if (new Set(entry.regions).size !== entry.regions.length) errors.push(`${module}: a region is listed twice`);
    for (const r of entry.regions) {
      if (!/^[a-z]+$/.test(r)) errors.push(`${module}: region "${r}" must be a lower-case word`);
      for (const field of ['label', 'about']) {
        if (typeof entry[field]?.[r] !== 'string' || entry[field][r].length === 0) errors.push(`${module}: no ${field} for ${r}`);
      }
      if (entry.made !== undefined && typeof entry.made?.[r] !== 'function') errors.push(`${module}: made has no note for ${r}`);
    }
    for (const field of ['label', 'about', 'made']) {
      const extra = Object.keys(entry[field] ?? {}).filter((k) => !entry.regions.includes(k));
      if (typeof entry[field] === 'object' && extra.length) errors.push(`${module}: ${field} has ${extra.join(', ')}, which is not in regions`);
    }
    return errors;
  }
  ```

  Run it again. Expected: 3 pass. Change the page and the component to read the words through `wordsFor(entry)`. A model with `regions` renders every region's caption, label and note in the page, each in an element carrying `data-model-region="<region>"`, all but the first `hidden`; task 7 switches them. A model without `regions` renders exactly as before.

- [ ] **Step 3: Prove the KIM-1 and the BBC Micro unchanged.** Build task 0's commit in a scratch copy and this one, and `cmp` both machines' `dist/machines/<id>/index.html`: identical. Run the site suite and `node scripts/browser-check.mjs`: pass without edits. Raise the floors.

- [ ] **Step 4: Journal and commit.** The field, why it is in `MODELS` and not the registry, how the two pages were shown unchanged. `feat(site): a model can draw more than one console, with its words for each`.

---

### Task 2: The board's frame: scale, outline and holes

**Files:**
- Create: `tools/nes-model/board_frame.py`, `tests/test_board_frame.py`, `data/frame.json`, `run-board.sh`
- Modify: `site/tests/nes-models.test.mjs`

**Interfaces:**
- Consumes: `common.py`, I1-front, `marks.json`.
- Produces `data/frame.json`: `{ "pxPerMm": {"x","y"}, "origin": [px, py], "rotationDeg", "heldOutX": {"rows": [{"row","pitches","errMm","scaledErrMm"}], "scaledErrMm": {"median","max"}, "largestRow"}, "heldOutY": {"footprints": [{"ref","spacingMm","errPct"}], "errPct": {"median","max"}, "errPct600", "errPct300"}, "board": {"widthMm","depthMm"}, "kicad": {"widthMm": 196.252, "depthMm": 118.7}, "outline": [[x,y], ...], "holes": [{"x","y","d"}], "verdicts": [...], "rectified": {"pxPerMm": 12} }`. Every later board script reads I1-front through this frame.

- [ ] **Step 1: pytest first.** Synthetic: a drawn board with pads at 2.54 mm along x, DIP rows 15.24 and 7.62 mm apart in y, on a known x and y scale, rotation and offset, with drill holes inside the pads; `board_frame` recovers each scale to 0.05 per cent, the corners to 0.1 mm and the hole centres to 0.05 mm, and takes y from the drill centres when the pads' solder is drawn off-centre by 1 per cent of the spacing. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Run on I1-front.** As task 0's scale over every DIP and connector row. Task 0 took the edge fingers at 2.54 mm, as the table words it, while they are on 2.50 mm (their end fingers 3 mm wide), which moved its x scale by 0.11 per cent; this frame takes x from the rows without the fingers, or with the fingers at their true pitch and widths, never at 2.54 mm; the rotation from the long pad rows (a line fit per row; the residual recorded, rows at or over 0.05 mm named; recorded, not judged); the outline from the board edge (a fit per edge, corners as intersections, the notches kept); the mounting holes on their top rims. Write a rectified copy at 12 px/mm to `out/` (git-ignored) and look at it.
- [ ] **Step 3: Site tests.** `frame.json` passes the x, y and x-against-y rows of the thresholds table, with the verdicts it records being the ones its figures give; the outline is closed and within 0.5 mm of `board`; `board` is within 2.0 mm of the KiCad outline each way (a sanity bound: the KiCad file is a redrawing). Raise the floors.
- [ ] **Step 4: Journal and commit.** `feat(models): the NES board's frame from the bare scan`.

---

### Task 3: The solder side registered, and the pads, drills and footprints

**Files:**
- Create: `tools/nes-model/board_register.py`, `tests/test_board_register.py`, `data/registration.json`; append to `run-board.sh`
- Modify: `site/tests/nes-models.test.mjs`

**Interfaces:**
- Produces `data/registration.json`: `{ "solder": {"model", "holes", "heldOutMm": {"median","p90","max"}, "outliers": {"rule", "excluded": [[hole, reason]], "heldOutMmWithExclusion": {...}}}, "drills": [{"x","y","d"}], "pads": [{"x","y","w","h","shape","face","drill"}], "footprints": [{"ref", "kind": "dip"|"connector"|"edge"|"crystal"|"axial"|"radial"|"other", "pins", "pin1": [x,y], "pads": [indexes], "box": [x0,y0,x1,y1]}] }`.

- [ ] **Step 1: pytest first.** A synthetic pair, one mirrored, rotated, scaled, with a mild cubic warp and 2 per cent of holes missing: registration recovers it to under 0.05 mm held out; pad grouping turns 2.54 mm rows into DIPs with the right pin count and pin 1 at the square pad; the edge fingers are one footprint of 36 per face. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Register I1-back to I1-front** over every hole, as task 0's spike; judge against the table on the figures without exclusion: STOP if crossed.
- [ ] **Step 3: Pads, drills, footprints**, grouped by pitch and by the printed outlines; each reference read from the print, by hand where grouping cannot tell (marks with reasons). Overlays in `out/`, looked at.
- [ ] **Step 4: Site tests.** The solder row of the table, with at least 150 holes; the excluded holes are those the rule names; every drill has a pad on both faces within 0.2 mm; U1 to U10 each have a DIP footprint with the pin count the Facts give; P1 has 72 fingers across both faces. Raise the floors.
- [ ] **Step 5: Journal and commit.** `feat(models): the NES board's solder side registered, with its pads and footprints`.

---

### Task 4: The copper on both faces, and the print

**Files:**
- Create: `tools/nes-model/board_trace.py`, `tests/test_board_trace.py`, `data/copper.json`, `data/ic-table.json` (the pinouts the nets check needs, each with its source), `site/src/assets/tracks/nes-famicom-board.webp`; append to `run-board.sh`
- Modify: `site/tests/nes-models.test.mjs`, `site/src/assets/photos/README.md` (a section for the track map: what it is traced from, its terms), `NOTICE.md` (the track map carries the TAPR Open Hardware License's terms, with OpenTendo credited)

**Interfaces:**
- Produces the track map: lossless WebP covering the board edge to edge in the board frame; **red** top copper, **green** bottom copper, **blue** the printed legend. Its pixels per millimetre is the highest of 10, 9, 8, 7 or 6 that keeps it inside 600,000 bytes, chosen before the final run and recorded.
- Produces `data/copper.json`: `{ "mapPxPerMm", "map": {"width","height","bytes","sha256"}, "coverage": {"top","bottom","print"}, "drillsInCopper": {"top","bottom"}, "nets": {"chips", "excluded": [[ref, reason]], "gnd": {"pins","inLargest"}, "vcc": {"pins","inLargest"}, "touching"}, "thresholds": {...} }`.

**Method.** Adapt `tools/bbc-micro-model/` and `tools/kim1-model/trace.py` as the BBC Micro's plan's task 4 says: OKLab, the lacquer's local colour by an opening, copper lighter than the lacquer by an Otsu threshold with hysteresis, pads kept by shape, thresholds measured on these scans with their probe values in the script. The print is traced separately into blue; copper under it is recovered only where the copper either side continues in line, and the journal says how much. Nothing is tuned against the nets check, which is the held-out test.

- [ ] **Step 1: pytest first.** Synthetic tracks, pads and print on a lacquer colour with noise: copper to an intersection over union of at least 0.95, the print apart; the net check finds two drawn nets apart and fails a drawn short. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Trace both faces and the print.** Overlays in `out/`, looked at closely around the CPU, the PPU and the edge connector; the journal says what was seen.
- [ ] **Step 3: The checks.** Coverage; drills in copper; the nets: for each IC whose pin 1 task 3 found, its GND and +5V pins from `ic-table.json` (each pinout with its source: a datasheet for the 74-series parts and the 6116; the nesdev wiki's pinout pages for the RP2A03 and RP2C02, GND pin 20 and +5V pin 40 [verify on the wiki page, and name it]; the CIC's from the nesdev wiki [verify]), joined through both faces by the drills. Judge against the table.
- [ ] **Step 4: The map.** Choose its resolution against the budget, write it, record its size and SHA-256.
- [ ] **Step 5: Site tests.** The map committed, WebP, the size `copper.json` gives, inside its budget, its three channels apart (red and green each 10 to 50 per cent, blue under 15); `copper.json` passes its table rows; its `map.sha256` is the committed file's; `NOTICE.md` names the map and the TAPR Open Hardware License. Raise the floors.
- [ ] **Step 6: Journal and commit.** `feat(models): the NES board's copper on both faces, traced from the bare scans`.

---

### Task 5: The parts, for both consoles

**Files:**
- Create: `tools/nes-model/board_parts.py`, `tests/test_board_parts.py`, `data/parts.json`, `site/src/models/nes-famicom-board-parts.mjs`; complete `data/ic-table.json`; append to `run-board.sh`
- Modify: `site/tests/nes-models.test.mjs`, `docs/nes/facts/models.md` (each console's parts, sourced)

**Interfaces:**
- Consumes: `registration.json`'s footprints; I2 (`kicad_footprints`) for the places check; I3 with task 0's PAL registration; I4 and I5, registered to I1-front by a homography on pins (record the held-out error).
- `data/ic-table.json`: `{ "ics": [{ "ref": "U6", "role": "the processor and sound unit", "pins": 40, "package": "DIP-40 600 mil", "parts": { "ntsc": { "part": "RP2A03G", "seen": "I4" }, "pal": { "part": "RP2A07A", "seen": "I3" } }, "gnd": 20, "vcc": 40, "pinoutSource": "..." }] }`, every IC and the crystal, with the part each console's photograph shows.
- Produces `nes-famicom-board-parts.mjs` (generated, with a header naming its sources). Its exports, as a shape, not code:
  ```text
  export const BOARD = { width, depth, thickness: 1.6, outline: [[x, y], ...], holes: [{ x, y, d }] };
  export const ICS = [{ ref, role, pins, x, y, w, l, rotation, height, parts: { ntsc, pal }, chip: 'ppu' | 'apu' | 'pad1' | 'pad2' | null, always: 'cpu' | 'ram' | 'decoder' | 'latch' | 'cartridge' | 'lockout' | null }];
  export const CONNECTORS = [{ ref, kind, x, y, w, l, h, rotation, label }];
  export const PASSIVES = [{ kind: 'axial' | 'radial' | 'other', x, y, rotation, length, diameter }];
  export const OTHERS = { ntsc: [/* modulator, crystal */], pal: [/* modulator, crystal */] };
  export const HEIGHTS = { board: 1.6, dip: { value, measured: true | false }, ... };
  ```
  `chip` names the counter (task 6's `NesChip`, lower case, the order of `COUNTED` in `nes-famicom-access.mjs`) that marks the IC; `always` says why an IC is never marked. The CPU's row carries `chip: 'apu'` (its sound unit). The RAMs, the decoder, the latch and the lockout chip carry `always` and `chip: null`.

- [ ] **Step 1: pytest first.** Synthetic footprints: the place is the pad group's centre and the rotation from pin 1; a footprint whose pad count is not its part's is reported, not placed; a similarity fit to the KiCad places finds a deliberate swap. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Places and identities.** Every IC and connector from its footprint; identities for NTSC from I4 and for PAL from I3, each a crop looked at and listed in the journal; heights from the oblique photographs where task 8's I7 is fetched and gives them, else typical and marked so. Check that each CPU-07 part sits on the CPU-10 footprint of the same reference.
- [ ] **Step 3: Site tests.** Every IC's pad count is its `pins`; each console's part is `ic-table.json`'s; every IC has a `chip` or an `always`, and every `chip` is one of `COUNTED`; U7 is `pad1` and U8 `pad2`; every part is inside the outline; no two IC bodies overlap; the KiCad places check passes the table; `parts.mjs` agrees with `parts.json`. Raise the floors.
- [ ] **Step 4: Journal and commit.** `feat(models): the NES board's parts, placed from the scan and named for both consoles`.

---

### Task 6: The machine tells the page which chips it talks to

Waits for the machine's pull request to merge (Global Constraints). Rebase the branch onto `main` first.

**Files:**
- Create: `src/Dbhq.Machines.Nes/ChipAccesses.cs`, `tests/Dbhq.Machines.Nes.Tests/ChipAccessesTests.cs`
- Modify: `src/Dbhq.Machines.Nes/NesBus.cs` (one `ChipAccesses` and a call to `Note` in its I/O decode), the NES host in `src/Dbhq.Machines.Nes.Wasm/` (`AccessCounts()`), the NES panel script and its site test (`accessCounts()`, `running`, `reset()`, the three events), `docs/nes/facts/models.md` (the address map, sourced)

**Interfaces:**
- Produces, in C#: `enum NesChip { Ppu, Apu, Pad1, Pad2 }`; `ChipAccesses` with `static NesChip? ChipAt(ushort address, bool write)`, `void Note(ushort address, bool write)`, `uint this[NesChip chip]`, `int[] Snapshot()`; `NesBus.Accesses` (the bus's `ChipAccesses`); the host's `public static int[] AccessCounts()`, `Snapshot()` of it.
- In the panel script: `panel.nes.accessCounts()` returns the host's array; `panel.nes.running` is true from Start until the page stops the machine; `panel.nes.region` is the region the page has now, `'ntsc'` or `'pal'`; `panel.nes.reset()` resets the machine and dispatches `nes:reset`; Start dispatches `nes:start`; a region change dispatches `nes:region` with `detail = { region: 'ntsc' | 'pal' }` after the new machine is running.

- [ ] **Step 1: The facts.** In `docs/nes/facts/models.md`, a section "What the counters count": the address map below, each line with the nesdev wiki page it comes from (the CPU memory map; the APU registers; the standard controller, which says what a write to `$4016` and a read of `$4016` and `$4017` do), and which 74HC368 serves which port, from the board's print ("40H368(CI)", "40H368(CII)") checked against the wiki's controller port pinout. If the wiki says otherwise, the wiki wins, the fact sheet says so and `ChipAt` follows it.
- [ ] **Step 2: The failing tests.** Create `tests/Dbhq.Machines.Nes.Tests/ChipAccessesTests.cs`:

  ```csharp
  using Dbhq.Machines.Nes;
  using Xunit;

  namespace Dbhq.Machines.Nes.Tests;

  public class ChipAccessesTests
  {
      [Theory]
      [InlineData(0x2000, false, NesChip.Ppu)]
      [InlineData(0x2007, true, NesChip.Ppu)]
      [InlineData(0x3FFF, false, NesChip.Ppu)]   // the last mirror of $2007
      [InlineData(0x4000, true, NesChip.Apu)]
      [InlineData(0x4014, true, NesChip.Apu)]    // starting OAM DMA is a write to the CPU's own register
      [InlineData(0x4015, false, NesChip.Apu)]
      [InlineData(0x4016, true, NesChip.Apu)]    // the strobe is the CPU's OUT0 pin, not a buffer
      [InlineData(0x4016, false, NesChip.Pad1)]
      [InlineData(0x4017, true, NesChip.Apu)]    // the frame counter
      [InlineData(0x4017, false, NesChip.Pad2)]
      public void An_access_reaches_the_chip_its_address_decodes_to(int address, bool write, NesChip chip)
      {
          Assert.Equal(chip, ChipAccesses.ChipAt((ushort)address, write));
      }

      [Theory]
      [InlineData(0x0000)]
      [InlineData(0x07FF)]
      [InlineData(0x1FFF)]
      [InlineData(0x4018)]    // the CPU's test registers, disabled on a console
      [InlineData(0x401F)]
      [InlineData(0x4020)]    // the cartridge, always in use, never counted
      [InlineData(0x8000)]
      [InlineData(0xFFFF)]
      public void Ram_the_cartridge_and_the_test_registers_are_not_counted(int address)
      {
          Assert.Null(ChipAccesses.ChipAt((ushort)address, false));
          Assert.Null(ChipAccesses.ChipAt((ushort)address, true));
      }

      [Fact]
      public void Each_access_adds_one_to_its_chip_and_nothing_to_the_others()
      {
          var a = new ChipAccesses();
          a.Note(0x2002, write: false);
          a.Note(0x2002, write: false);
          a.Note(0x4016, write: true);
          a.Note(0x4016, write: false);
          a.Note(0x8000, write: false);
          Assert.Equal([2, 1, 1, 0], a.Snapshot());
          Assert.Equal(2u, a[NesChip.Ppu]);
      }
  }
  ```

  Run: `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`. Expected: fails to build, `ChipAccesses` and `NesChip` are missing.
- [ ] **Step 3: The counter.** Create `src/Dbhq.Machines.Nes/ChipAccesses.cs`:

  ```csharp
  namespace Dbhq.Machines.Nes;

  /// <summary>
  /// The chips on the NES's board that the CPU's accesses can be told apart by address, for the
  /// inside model's marks (<c>docs/nes/facts/models.md</c>, "What the counters count").
  /// </summary>
  public enum NesChip
  {
      /// <summary>The PPU's registers, <c>$2000-$3FFF</c>; OAM DMA's writes to <c>$2004</c> included.</summary>
      Ppu,

      /// <summary>The sound and I/O registers on the CPU's own die: <c>$4000-$4015</c>, and the writes to <c>$4016</c> and <c>$4017</c>.</summary>
      Apu,

      /// <summary>Reads of <c>$4016</c>, through the 74HC368 printed "40H368(CI)".</summary>
      Pad1,

      /// <summary>Reads of <c>$4017</c>, through the 74HC368 printed "40H368(CII)".</summary>
      Pad2,
  }

  /// <summary>
  /// Counts the CPU's reads and writes of each <see cref="NesChip"/>, so the page can show which
  /// chips the processor talks to. The bus calls <see cref="Note"/> on every access it decodes to
  /// <c>$2000-$401F</c> and on no other, and never for a peek. Nothing here changes what any chip
  /// does or adds a cycle: it only counts.
  /// </summary>
  /// <remarks>
  /// The counters wrap, so a reader takes the difference between two snapshots, which stays right
  /// across a wrap as long as fewer than 2^32 accesses fall between them.
  /// </remarks>
  public sealed class ChipAccesses
  {
      private readonly uint[] _counts = new uint[Enum.GetValues<NesChip>().Length];

      /// <summary>The chip an access to <paramref name="address"/> reaches, or null for none of them.</summary>
      public static NesChip? ChipAt(ushort address, bool write) => address switch
      {
          >= 0x2000 and <= 0x3FFF => NesChip.Ppu,
          >= 0x4000 and <= 0x4015 => NesChip.Apu,
          0x4016 => write ? NesChip.Apu : NesChip.Pad1,
          0x4017 => write ? NesChip.Apu : NesChip.Pad2,
          _ => null,
      };

      /// <summary>Counts one access by the CPU, if it reaches a counted chip.</summary>
      public void Note(ushort address, bool write)
      {
          if (ChipAt(address, write) is { } chip)
          {
              unchecked { _counts[(int)chip]++; }
          }
      }

      /// <summary>The accesses to <paramref name="chip"/> since power on, wrapping.</summary>
      public uint this[NesChip chip] => _counts[(int)chip];

      /// <summary>Every counter, in <see cref="NesChip"/> order, as the host hands them to the page.</summary>
      public int[] Snapshot() => Array.ConvertAll(_counts, c => unchecked((int)c));
  }
  ```

  Run the tests. Expected: the 19 above pass.
- [ ] **Step 4: Into the bus, test first.** In the NES tests, with the bus alone: one CPU read of `$2002` adds one to `Ppu` and nothing else; a peek adds nothing; OAM DMA from page `$02` adds 256 to `Ppu` (its writes to `$2004`) and one to `Apu` (the write to `$4014`); `INC $4016` adds one to `Pad1` (its read) and two to `Apu` (the NMOS core's two writes, the old value then the new); a read of `$0000` or `$8000` adds nothing. After a frame of `nestest` or the bundled homebrew the `Ppu` count is not zero. Run: fail. Add the call to `Note` where `NesBus` decodes `$2000-$401F` and nowhere else; if the bus's OAM DMA hands its bytes to the PPU without passing that decode, call `Note(0x2004, true)` there for each; no change to any cycle or chip. Run the NES tests, then `dotnet test -c Release`: pass, no warnings.
- [ ] **Step 5: Prove nothing else changed, and measure the speed.** The machine's fingerprint or recorded-frame hashes, as its pull request's tests and bench define them, before and after: identical. The speed, before and after, with the machine's bench as its README says, on a quiet machine: both medians and the load average recorded; a slowdown over 2 per cent is explained before going on, and Dan is told if headroom is thin.
- [ ] **Step 6: The host and the page script, test first.** With the panel script's fake host in its site test: `accessCounts()` returns the host's array; `running` is false before Start and true after; Start dispatches `nes:start`; `reset()` calls the host's reset and dispatches `nes:reset`; a region change dispatches `nes:region` with the lower-case region after the new machine runs. Run: fail. Implement. Build the machine, run the site suite and the NES browser check: pass. Raise the floors.
- [ ] **Step 7: Journal and commit** (the address map and its sources, which buffer is which port, the fingerprint and the speed with their commands and the load). `feat(nes): the machine counts which chips the processor talks to, for the board model`.

---

### Task 7: The inside model on the page

Waits for the BBC Micro's models pull request to merge. After this task the NES row has `"models": [{ "view": "inside", "module": "nes-famicom-board" }]` and **no `case` field**, as the BBC Micro's task 7 did; task 9 adds `"case": true` and the outside, and the branch is not merged before task 9.

**Files:**
- Create: `site/src/models/nes-famicom-board.js`, `nes-famicom-board-layout.mjs`, `nes-famicom-board-notes.mjs`, `nes-famicom-access.mjs`, `site/tests/nes-famicom-access.test.mjs`, `tools/nes-model/results.py` (the board half), `site/src/data/nes-famicom-board-model.json`
- Modify: `machines/registry.json` (the NES row: `models` as above; `references` for I1-front, I1-back, I2, I3, I5 with title, author, URL, licence as stated or null, fetched, SHA-256 and what was used), `site/src/models/models.mjs` (the entry), the model styles and `tokens.css` (the NES board's lacquer, its underside, copper, print, socket and access mark), `.github/workflows/deploy-site.yml` (`models/nes-famicom-board.js models/nes-famicom-board-tracks.webp`), the NES browser check, `site/src/assets/photos/README.md`, `site/tests/model.test.mjs`, `site/tests/nes-models.test.mjs`, `site/tests/design.test.mjs`

**Interfaces:**
- `models.mjs` entry: `'nes-famicom-board': { machine: 'nes-famicom', view: 'inside', regions: ['ntsc', 'pal'], label: { ntsc, pal }, about: { ntsc: describeBoard('ntsc'), pal: describeBoard('pal') }, texture: BOARD_TRACKS.src, made: { ntsc: (f) => madeBoard(f, 'ntsc'), pal: (f) => madeBoard(f, 'pal') }, legend: chipLegend }`. If task 0 stopped the PAL models, `regions: ['ntsc']` and no `pal` keys.
- `chipLegend(region)`: `[{ ref, role, part, otherPart, chip, always }]` for every IC, in reference order, `part` the region's and `otherPart` the other console's.
- `accessRates(before, after, seconds)` and `createSampler({ counts, now })` in `nes-famicom-access.mjs`, below.
- The model reads `panel.nes` after `nes:start`, through `createSampler({ counts: () => panel.nes.accessCounts(), now: () => performance.now() })`: every 250 ms it samples, marks each chip whose count moved (its top tinted `--model-active`, its legend row `data-accessed`) and writes each counted chip's rate into its legend row. On `nes:start` and `nes:region` it calls the sampler's `reset()` and shows no rate until the next sample. Before Start nothing is marked and the status line says the machine is not running. On `nes:region` it swaps the PAL or NTSC parts, the crystal and the modulator, the caption, label and note (`data-model-region`), and the legend's parts, with no download.
- Test hooks on the root: `data-model-region`, `data-model-accessed` (the refs marked), `data-model-rates` (JSON of the last rates), `data-model-tracks`, `data-model-parts`; `root.modelChipPoint(ref)`, `root.modelBoardPoint(x, y, below)`, `root.modelView()`.

- [ ] **Step 1: The rates, test first.** Create `site/tests/nes-famicom-access.test.mjs`:

  ```js
  import test from 'node:test';
  import assert from 'node:assert/strict';
  import { accessRates, COUNTED } from '../src/models/nes-famicom-access.mjs';

  test('a chip whose count moved is marked, with its accesses per second', () => {
    const r = accessRates([10, 0, 5, 5], [70, 0, 7, 5], 0.25);
    assert.deepEqual(r.ppu, { moved: true, perSecond: 240 });
    assert.deepEqual(r.apu, { moved: false, perSecond: 0 });
    assert.deepEqual(r.pad1, { moved: true, perSecond: 8 });
    assert.deepEqual(r.pad2, { moved: false, perSecond: 0 });
  });

  test('a counter that wraps past 2^31 and past 2^32 still gives the right difference', () => {
    // The host's int[]: 2^31 - 2 then -2^31 + 2 (four on, past the sign), and -1 then 3 (four on, past zero).
    const r = accessRates([2 ** 31 - 2, -1, 0, 0], [-(2 ** 31) + 2, 3, 0, 0], 1);
    assert.equal(r.ppu.perSecond, 4);
    assert.equal(r.apu.perSecond, 4);
  });

  test('a snapshot of the wrong length, or no time between them, is refused', () => {
    assert.throws(() => accessRates([0, 0, 0], [0, 0, 0], 1), /expected 4 counters/);
    assert.throws(() => accessRates([0, 0, 0, 0], [0, 0, 0, 0], 0), /more than 0/);
    assert.equal(COUNTED.length, 4);
  });

  test('the sampler divides by the time it measured, not the time it was asked to wait', async () => {
    const { createSampler } = await import('../src/models/nes-famicom-access.mjs');
    let t = 0;
    let c = [0, 0, 0, 0];
    const s = createSampler({ counts: () => c, now: () => t });
    assert.equal(s.sample(), null, 'the first sample has nothing to compare with');
    t += 250; c = [100, 0, 0, 0];
    assert.equal(s.sample().ppu.perSecond, 400);
    t += 1000; c = [200, 0, 0, 0];         // a timer that fired four times late
    assert.equal(s.sample().ppu.perSecond, 100);
  });

  test('after a reset the sampler takes a fresh baseline, so a new machine\'s counters never show as billions', async () => {
    const { createSampler } = await import('../src/models/nes-famicom-access.mjs');
    let t = 0;
    let c = [5000, 50, 10, 10];
    const s = createSampler({ counts: () => c, now: () => t });
    s.sample();
    t += 250;
    s.reset();                              // nes:region: a new machine, counting from zero
    c = [3, 0, 1, 0];
    assert.equal(s.sample(), null, 'no rate across two machines');
    t += 250; c = [43, 0, 3, 0];
    assert.deepEqual(s.sample().ppu, { moved: true, perSecond: 160 });
    assert.equal(s.sample(), null, 'two samples at one instant give no rate');
  });
  ```

  Run: fails, the module is missing. Create `site/src/models/nes-famicom-access.mjs`:

  ```js
  /** The chips the machine counts, in the host's order (NesChip in C#). */
  export const COUNTED = ['ppu', 'apu', 'pad1', 'pad2'];

  /**
   * What changed between two snapshots of the machine's access counters, taken
   * `seconds` apart: for each counted chip, whether its count moved and its
   * accesses per second, rounded to a whole number. The host hands the counters
   * as signed 32-bit numbers that wrap, so the difference is taken modulo 2^32,
   * which is right across a wrap. A snapshot of another length is an error: the
   * host and this list have drifted apart.
   */
  export function accessRates(before, after, seconds) {
    if (before.length !== COUNTED.length || after.length !== COUNTED.length) throw new Error(`expected ${COUNTED.length} counters, got ${before.length} and ${after.length}`);
    if (!(seconds > 0)) throw new Error(`seconds must be more than 0, not ${seconds}`);
    return Object.fromEntries(COUNTED.map((chip, i) => {
      const d = (after[i] - before[i]) >>> 0;
      return [chip, { moved: d > 0, perSecond: Math.round(d / seconds) }];
    }));
  }

  /**
   * Samples the machine's counters for the board model. Each `sample()` reads
   * `counts()` and the clock `now()` (milliseconds, as performance.now gives) and
   * returns `accessRates` against the last sample, divided by the time measured
   * between the two, not the time the sampler was asked to wait: a timer that
   * fires late (a background tab, a busy machine) still gives true rates. The
   * first sample, and the first after `reset()`, returns null: the model calls
   * `reset()` when the machine starts or is replaced (a region change builds a
   * new machine whose counters start again from zero), so a difference is never
   * taken across two machines. Two samples at the same instant also give null.
   */
  export function createSampler({ counts, now }) {
    let last = null;
    return {
      sample() {
        const snap = { counts: Array.from(counts()), at: now() };
        const prev = last;
        last = snap;
        if (!prev || snap.at <= prev.at) return null;
        return accessRates(prev.counts, snap.counts, (snap.at - prev.at) / 1000);
      },
      reset() {
        last = null;
      },
    };
  }
  ```

  Run: 5 pass. The last two are Review Focus 1 and 2: rates divided by the time measured, and a fresh baseline after `reset()`.
- [ ] **Step 2: Failing tests for the model.** `model.test.mjs` covers every model in `MODELS`; add for this one: the texture beside the bundle byte for byte, the deploy lists both files. `nes-models.test.mjs`: the model draws every IC in the parts file; the legend lists every IC once with both consoles' parts from `ic-table.json`, and says in words what a mark means and why the CPU, RAMs, decoder, latch, cartridge and lockout chip are never marked; each region's caption starts "Model, not a photograph, of" and names its console; every figure in each note is read from `nes-famicom-board-model.json`, which agrees with `frame.json`, `registration.json`, `copper.json` and `parts.json`; every source the board used is credited in the registry by URL and the other way round; the test that ties `regions` to the NES page: read the region control's values from the built `dist/machines/nes-famicom/index.html` (find the control in the machine's merged page or panel component and write the selector here) and assert they equal the entry's `regions` once lower-cased. Run: fail.
- [ ] **Step 3: The model.** The board with its outline and holes; the map as colour, shine and relief on each face, as the KIM-1's `trackMaps`; ICs on the board; connectors, passives (instanced), and each console's modulator and crystal; Show tracks and Show tracks only, as the KIM-1's; pointing at a chip names it and marks its legend row. The whole board in the start view; the camera may go under it.
- [ ] **Step 4: Page weight.** `node scripts/page-weight.mjs /machines/nes-famicom/` before (task 6's commit) and after: nothing under `/models/` in the first load; the bundle and map under "later"; inside budgets. Record both.
- [ ] **Step 5: Browser check.** In the NES browser check, after its existing steps with a ROM running: nothing under `/models/` fetched before scrolling; scrolling loads the bundle and map; the canvas draws; the view from below has drawn pixels where the map has bottom copper; the tracks buttons work; the PPU (U5) and U7 are marked at least once in 2 s of sampling every 50 ms, and their legend rates are above zero; switching the region to PAL changes `data-model-region` and the PPU's legend part to RP2C07-0 with no new request under `/models/`, and the rates show nothing for one sample and then come back; no console errors, failed requests or CSP violations. Run the whole `node scripts/browser-check.mjs`: the KIM-1's and the BBC Micro's parts unchanged and passing.
- [ ] **Step 6: Credits, the KIM-1 and BBC Micro unchanged** (the `cmp` in the Global Constraints), the photographs README's table of uncommitted sources. Raise the floors.
- [ ] **Step 7: Journal and commit.** `feat(models): the NES's inside model, its copper, its chips for both consoles and what the processor is talking to`.

---

### Task 8: The outside measured, for both consoles

**Files:**
- Create: `tools/nes-model/case_measure.py`, `tests/test_case.py`, `run-case.sh`, `data/case.json`, `site/src/models/nes-famicom-case-parts.mjs`
- Create: `site/src/assets/photos/nes-famicom-*.webp` (O2-FL, O2-BR and one PAL photograph, resized: `cwebp -q 82 -resize 1600 0 -metadata none`, as the other machines')
- Modify: `machines/registry.json` (the photographs after the main one, each with credit, `fetched` and `used`), `site/src/assets/photos/README.md` (a section each, with both SHA-256), `tools/nes-model/data/sources.json` (O2-FR, O3's deconstruction photographs and I7 fetched and hashed; `committed` set for the photographs committed), `docs/nes/facts/models.md`, `site/tests/nes-models.test.mjs`

**Interfaces:**
- Produces `data/case.json`: `{ "footprint": {"widthMm": 254, "depthMm", "widthSource", "depthFrom"}, "heightMm", "heightFrom": "photographs" | "published", "outline": [...], "profile": [{"y","z"}], "profileCheck": {"what","errMm"}, "door": {...}, "vents": [...], "buttons": [{"name": "POWER"|"RESET", "x","y","w","d","travelMm","travelFrom"}], "led": {"x","y","d"}, "ports": [{"name","x","z","w","h"}], "rear": {"ntsc": [{"label","x","z","w","h","from"}], "pal": [...]}, "labels": {"ntsc": [{"words","box"}], "pal": [...]}, "underside": {"ntsc": {...}, "pal": {...}}, "boardInCase": {"x","y","z","from"}, "palFront": {...} }`.
- Produces `nes-famicom-case-parts.mjs` (generated): `CASE`, `PROFILE`, `DOOR`, `VENTS`, `BUTTONS`, `LED`, `PORTS`, `REAR` (by region), `LABELS` (by region, words only), `UNDERSIDE` (by region), `FEET`, each with a source note.

- [ ] **Step 1: pytest first.** Synthetic: a box with a stepped top photographed by a known camera from two corners recovers its proportions to 0.3 per cent and its height to 0.5 per cent; a feature on a face, rectified, comes back to 0.2 mm; ports placed by a known board offset come back to 0.1 mm. Run: fail. Implement. Run: pass.
- [ ] **Step 2: Shape.** The case's width is the published 254 mm; its depth and height from task 0's proportions (as first written; height from the photographs if they agreed, else the published 88.9 mm, and `heightFrom` says which). Since the second revision of 5 Oct 2026 the case's size in the model is the published width 254 mm, depth 203.2 mm and height 88.9 mm, which the page states are not Nintendo's figures and good to about 3 per cent (task 0 checked the patent's proportions against them within 5); the corner photographs give the features only. The side profile from O2-FL and O2-BR with one point held out as the check (within 2.0 mm, else the profile falls back to the patent's side views scaled to the case, said so). Cross-check the width on O3's deconstruction photographs against the board's 196.252 mm where the board and the case are in one view: recorded.
- [ ] **Step 3: Features.** The door, vents, buttons, LED and controller ports on the front face rectified; the rear connectors from O2-BL and O2-BR, placed against the modulator's place on the board plus the board's offset in the case (from O3), each labelled with what the photograph shows; the underside and its expansion cover from O1's FIG 6, with O5 for the PAL console. The PAL console's label words and rear from O4, O5 and the PAL set, recorded as differences from the NTSC console. Whether POWER latches in when on, from the photographs or the deconstruction set; if nothing shows it, say so, and the model shows it in while running as the design says, with the page saying so [guessing - verify].
- [ ] **Step 4: The photographs.** Fetch and hash what this task needs, resize and commit three, write their README sections and registry entries (alt text describing each; `used` saying what the model took).
- [ ] **Step 5: Site tests.** `case.json`'s depth and height agree with `spike.json` and its verdicts; every feature lies on the case; the buttons, LED and ports in the order the photographs show; every rear connector within 2 mm of its board place plus the offset; the profile check passes or its fallback is flagged; the labels are words, and no label has an image or a path; the PAL differences are listed; `case-parts.mjs` agrees with the data. Raise the floors.
- [ ] **Step 6: Journal and commit.** `feat(models): the NES's case measured, for both consoles`.

---

### Task 9: The outside model, both views, and the region

The largest task. If its first review sends it back more than twice, split it at step 4: the case model first, the views and the region second.

**Files:**
- Create: `site/src/models/nes-famicom-case.js`, `nes-famicom-case-layout.mjs`, `nes-famicom-case-notes.mjs`, `site/src/data/nes-famicom-case-model.json` (by `results.py`, the case half)
- Modify: `machines/registry.json` (`"case": true`; `models`: outside `nes-famicom-case`, then inside `nes-famicom-board`), `site/src/models/models.mjs`, the model styles and `tokens.css` (the case's two greys, the LED lit and dark, the buttons), `.github/workflows/deploy-site.yml` (`models/nes-famicom-case.js`), the NES browser check, `site/DESIGN.md`, `site/tests/model.test.mjs`, `nes-models.test.mjs`, `model-regions.test.mjs`, `design.test.mjs`

**Interfaces:**
- `models.mjs` entry: `'nes-famicom-case': { machine: 'nes-famicom', view: 'outside', regions, label, about: { ntsc: describeCase('ntsc'), pal: describeCase('pal') }, made: { ... } }`.
- The outside model: the LED lit (`--model-led`) while `panel.nes.running`, dark (`--model-led-off`) otherwise; POWER in while running; RESET down for 140 ms on `nes:reset` (at once with reduced motion); a click on POWER calls the page's Start exactly as its Start button does, and after Start says on the status line that the page has no power-off; a click on RESET calls `panel.nes.reset()` when running, and before Start says the machine is not running and does nothing else. On `nes:region` it swaps the labels, the rear and the underside. Test hooks: `data-model-region`, `data-model-led` (`lit` or `dark`), `data-model-power` (`in` or `out`), `data-model-presses`, `root.modelButtonPoint(name)`, `root.modelView()`.
- **A view loaded after a region change draws the page's region at that moment** (Review Focus 3): each module reads `panel.nes.region` when it mounts, not the region the page had when it loaded.

- [ ] **Step 1: Failing tests.** `model.test.mjs`: on the NES page, one model section with the BBC Micro's tab list, Outside first and selected, each panel with its own stage, reset, status line and every region's caption, ids unique; the KIM-1's and BBC Micro's pages unchanged (`cmp`). `nes-models.test.mjs`: the case's sizes are `case.json`'s; no module under `site/src/models/nes-famicom-*` names "logo", loads an image other than its own drawn atlas and track map, or holds an SVG path. `model-regions.test.mjs`: with a made-up entry that has `regions: ['ntsc']` and a page whose region is PAL, the section shows the NTSC model, labelled NTSC, and a sentence saying there is no model of the PAL console (Review Focus 5). A fake-DOM test of the outside module's click handling: POWER before Start calls Start; RESET before Start calls nothing and sets the status line (Review Focus 4). Run: fail.
- [ ] **Step 2: The case model.** Shell from footprint, profile and the stepped top; the door, vents, buttons, LED, ports, rear connectors and underside as measured; words drawn in the site's face into one canvas atlas after `document.fonts.load`; feet. Start view three-quarters from the front, bounds the case plus 2 cm; the camera may go under it.
- [ ] **Step 3: The views and the region.** The BBC Micro's tabs and loader, unchanged; each NES module mounts with the current region and redraws on `nes:region`; the captions and notes switch by `data-model-region`.
- [ ] **Step 4: Page weight.** Before (task 8's commit) and after: nothing under `/models/` in the first load; the outside's bundle when the section is reached; the inside's bundle and map when its tab is chosen; every file inside budget. Record both.
- [ ] **Step 5: Browser check, both views, both regions.** In the NES browser check: nothing under `/models/` before scrolling; scrolling fetches the outside's bundle and not the inside's; the outside draws; before Start `data-model-led` is `dark` and a click on RESET leaves the machine stopped; a click on POWER starts the machine and then `data-model-led` is `lit` and `data-model-power` `in`; the page's reset puts RESET down; a click on the model's RESET resets the machine (the machine's own reset signal, as its check reads it); under the case the view has drawn pixels; switching to PAL with the inside not yet loaded, then choosing Inside, loads it drawing PAL (Review Focus 3); Right and Left move between the tabs and each keeps its camera; Tab leaves the section; a reduced-motion context loads both views and switches without error; no console errors, failed requests or CSP violations. Run the whole `node scripts/browser-check.mjs`: the other machines' parts unchanged and passing.
- [ ] **Step 6: Provenance on the page.** The credits under each view list exactly the sources it used, by URL, with what each gave; `DESIGN.md` gets "The NES's 3D models". Raise the floors.
- [ ] **Step 7: Journal and commit.** `feat(models): the NES's outside model, and both models as views of either console`.

---

### Task 10: The final pass

**Files:**
- Modify: `docs/known-differences.md`, the day's journal, `README.md`, `AGENTS.md` (layout: `tools/nes-model/`, `docs/nes/facts/`), `docs/the-6502-family.md`, `site/DESIGN.md`, tests as the pass requires

- [ ] **Step 1: Mutation pass.** In a scratch copy outside the repository, break one thing at a time and run the site suite (and the NES C# tests where C# is broken), recording which test caught each: x and y scales swapped; the back scan not mirrored; one IC's identity swapped between the consoles; U7 and U8 swapped; `ChipAt` sending `$4016` writes to `Pad1`; a peek counted; the rates divided by 0.25 not the measured time; no fresh baseline on `nes:region`; a PAL part left on the NTSC board after a switch; a module drawing the first-load region when loaded late; the case height typed as 89; a colour literal in a model; the track map over budget; the cased rule removed; a logo path in the case module. Any that survives gets a test or a line in `known-differences.md` saying why not. Put the table in the journal.
- [ ] **Step 2: Known differences and limits.** A section on the NES's models: the case width not Nintendo's; the height's source; the NTSC parts identified on a CPU-07 and placed on a CPU-10; the PAL copper being the NTSC board's on task 0's evidence; typical heights; the lockout chip not emulated, so the LED never blinks; POWER's latch as found. The page's "What it does not show" lists match.
- [ ] **Step 3: The size and speed record.** `page-weight.mjs` for the NES and the other machines on the final build; the speed from task 6; both in the journal with their commands.
- [ ] **Step 4: Documents.** README (the NES's models; the licences paragraph names the track map's TAPR Open Hardware License terms and the CC BY 4.0 photographs), `AGENTS.md`'s layout, the family document's NES line, `DESIGN.md`. No figure typed by hand.
- [ ] **Step 5: Final review.** `superpowers:requesting-code-review` on the whole branch against the design and this plan, on the most capable model, with one question first: does any page, doc or journal sentence claim more than a test or a recorded measurement shows? One fix wave, re-checked.
- [ ] **Step 6: Mark the pull request ready** only when `Validate` is green on the last commit and Dan has said so. Do not merge.
- [ ] **Step 7: Journal and commit** `test(models): the NES models' mutation pass, known limits and record`. Report to Dan (below).

---

## What Dan must be told at the end

One thing at a time, the most important first:

1. **The machine changed, a little.** The bus counts the CPU's accesses to the PPU, the sound and I/O registers and the two pad buffers. The fingerprint showed no change in behaviour, and the speed before and after is in the journal.
2. **Which console each model shows, and how sure the PAL board is**, from task 0's PAL layout figures.
3. **The case's size is not Nintendo's.** The width and depth are published figures from other sources; the height came from the photographs or, if they disagreed, the published 88.9 mm, and the page says which.
4. **The track map carries the TAPR Open Hardware License's terms**, and OpenTendo is forked into `dbhq-uk`.
5. **What the page weighs**, first load and later, per view, from the last `page-weight.mjs` run.
6. Anything a STOP or a fallback changed.

## Deferred

Recorded here, not built: a shared three.js chunk for the two bundles; measured heights for every part from a stereo pair of a populated board; following a net on the board; the controllers, a cartridge and the case's insides; an opening door; the Famicom, the NES-101 and the PAL-A console; a real GPU, other browsers and a screen reader on both views.

## Self-review

**Spec coverage.** Two modules drawing both consoles, `regions` in `MODELS`: 1, 7, 9. The registry unchanged, `case` and both views: 7, 9. The page's views reused and waited for: Global Constraints, 7, 9. Outside state (LED, POWER, RESET): 6, 9. Inside state (counters, marks, rates, the words about chips never marked): 5, 6, 7. Region switch: 6, 7, 9. Provenance (fork, TAPR terms, CC BY credits, nothing original committed): 0, 4, 7, 8, 9. Proof table rows: scale 0, 2; outline 0, 2; solder 0, 3; copper 4; parts 5; PAL parts 0, 5; case 0, 8; structure 7, 9; regions 1, 7, 9; machine state 6; browser 7, 9. Task 0's four stop rules from the design, as eight checks: 0. Not in scope and limits: 10. What this waits for: Global Constraints, 6, 7.

**Placeholders.** The measuring code is not written here, by design (the header says why). Every threshold, budget, file name, interface and test assertion is stated. Two things can only be read once the machine's pull request has merged, and the task that needs each says where to read it: the region control's markup (task 7, step 2) and the machine's fingerprint and bench commands (task 6, step 5). Three facts are marked [verify] and each has a task that checks it before relying on it: which buffer serves which port (6), the CPU, PPU and CIC pinouts (4), and whether POWER latches (8).

**Type consistency.** `NesChip`'s order `Ppu, Apu, Pad1, Pad2` is `COUNTED`'s `ppu, apu, pad1, pad2` and the parts file's `chip` values (5, 6, 7). `wordsFor` and `regionProblems` (1) are read in 7 and 9. `panel.nes.accessCounts`, `running`, `reset`, `nes:start`, `nes:reset`, `nes:region` (6) are read in 7 and 9. `spike.json`'s keys (0) are `verdicts()`'s and are read again in 2, 3 and 8.
