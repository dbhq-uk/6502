# The NES's 3D models: design

Written 5 October 2026 with Dan, for the section "The 3D models" of
[the NES's design](2026-10-05-nes-design.md): the NES has a case, so it gets two
models, the outside and the inside, offered as labelled views of the same
machine, with the same controls and lazy loading, and the machine's state shown
in both. The BBC Micro's [models design](2026-10-04-bbc-micro-models-design.md)
is the pattern, and this design keeps everything it settled unless a line here
says otherwise. The sources, what each is good for and their licences are in
[`docs/nes/facts/models.md`](../../nes/facts/models.md). The plan is
[`../plans/2026-10-05-nes-models.md`](../plans/2026-10-05-nes-models.md).

**This is the second pull request**, after the machine's (Dan, 5 October 2026).
Models never hold up a count (Dan, 2 October 2026), so the NES counts without
them, and its registry row claims no model until both views are built.

## What is built

Two model modules on `/machines/nes/`, both ours, built in three.js on
the shared stage (`site/src/models/stage.mjs`). No downloaded model is used.
**Each module draws two consoles,** the NTSC NES-001 and the PAL NESE-001, and
draws the one the page's region names (Dan, 5 October 2026: "NTSC and PAL, both
views").

- **Outside** (`site/src/models/nes-famicom-case.js`): the front-loading case,
  its two greys, the cartridge door, the vents, the POWER and RESET buttons,
  the power LED, the two controller ports, the rear connectors (the NTSC
  console's channel switch, RF and AV jacks and power jack; the PAL console's
  as its photographs show them), the underside with the expansion port cover
  and the feet. The words on the case are drawn by us in the site's own face,
  with no Nintendo logo shape. The PAL console carries its own front label and
  underside rating label, measured from its photographs.
- **Inside** (`site/src/models/nes-famicom-board.js`): the main board as the
  emulated machine has it. For NTSC it is an NES-CPU-10: outline, holes, pads,
  both copper faces and the printed legend traced from flatbed scans of a bare
  board; every IC placed from the scan's footprints and print, and identified
  from photographs of a populated board; the 72-pin cartridge connector, the RF
  modulator, the expansion port connector and the controller and power headers
  placed from the scan. For PAL it is the same copper with the PAL parts: the
  RP2A07A, the RP2C07-0, the 3195A lockout chip, the PAL crystal and the PAL
  modulator, placed from a photograph of an NES-CPU-11 "PAL-EEC" board, **if
  task 0 shows the two layouts are one** (below). Heights are measured where
  the photographs allow and typical elsewhere, and the page says which. The
  camera can go under the board.

The inside is built first: it gives the outside the places of the ports it
carries, as for the BBC Micro.

## Two consoles in one module

The page has a region setting (NTSC or PAL; the machine's design, "Region").
The models follow it.

- **One module per view, not one per console.** `nes-famicom-case` is the
  outside and `nes-famicom-board` the inside, as the registry claims them. Each
  module holds both consoles' geometry, which is mostly shared, and takes the
  region when it mounts. On `nes:region` it redraws for the new console with no
  new download.
- **`MODELS` says which consoles a module draws.** An entry may carry
  `regions`, a non-empty list (`['ntsc', 'pal']` for both NES modules). With
  `regions`, its `about` (caption), `label` and `made` (how it was made) are
  keyed by region, so each console has its own words and its own figures. A
  test ties the list to the regions the NES page offers, both ways, and fails a
  module that lacks a caption or a note for one of its regions. An entry with
  no `regions` is one console, as the KIM-1's and the BBC Micro's are, and
  nothing about them changes.
- **The registry does not change.** Its `case` and `models` fields and every
  rule `validateRegistry` holds (the BBC Micro's design, "The registry's
  `models` field") apply as written: one module per view, a cased machine that
  claims a model claims both `outside` and `inside`. The regions are a fact
  about the model, not the machine, so they live in `MODELS`.
- **The page says which console it shows.** The caption starts with the
  console: "Model, not a photograph, of an NTSC NES-001, the front-loading
  console sold in North America" or "of a PAL NESE-001, the front-loading
  console sold in Europe". The chips that differ by region are named in the
  legend for both, the one shown first: "CPU: RP2A03G here; RP2A07A in a PAL
  console".
- **If the PAL models stop at task 0,** `regions` is `['ntsc']`, and with PAL
  chosen the section says in words that there is no model of the PAL console
  and why, and still shows the NTSC one, labelled as NTSC. Whether the work
  goes on that way is Dan's call when it happens (below).

## How the page offers the two views

As the BBC Micro's design says, with nothing new: one section, a tab list named
"Views of the model" with tabs "Outside" and "Inside", each panel with its own
stage, reset button, status line, caption and note, loaded only when its view is
shown, each keeping its camera. Outside is shown first. The NES reuses the
BBC Micro's markup, loader and styles; it does not build its own (see
"What this waits for").

## What state each model shows

Dan's choice, 5 October 2026: the LED and buttons outside, and chip marks with
live rates inside.

**Outside.**

| Part | Shows | Source |
|---|---|---|
| Power LED | lit while the machine runs | the page's running state, `panel.nes.running`; no host call |
| POWER button | in while the machine runs, out before Start; the NES-001's button latches [guessing - verify against the photographs in task 8] | the same |
| RESET button | goes down for 140 ms on `nes:reset` | the page |

A click on POWER does what the page's Start button does; after Start it changes
nothing, and the status line says the page has no power-off. A click on RESET
resets the machine through `panel.nes.reset()`, which announces `nes:reset`,
so the button goes down however the reset came. The lockout chip is not
emulated, so the LED never blinks as a real console's does when it refuses a
cartridge, and the page says so.

**Inside.** The machine counts the CPU's accesses to each chip it can tell
apart by address, and the model shows them. `NesBus` gains one counter per
`NesChip`, decoded by address in its existing register decode:

| `NesChip` | Counts | Marks on the board |
|---|---|---|
| `Ppu` | reads and writes of `$2000-$3FFF`, OAM DMA's writes to `$2004` included | the PPU |
| `Apu` | reads and writes of the sound and I/O registers on the CPU's own die, `$4000-$4015`, and the writes to `$4016` (the controllers' strobe, the CPU's own output pin) and `$4017` (the frame counter) | the CPU's legend row (the sound unit is on the CPU's die) |
| `Pad1`, `Pad2` | reads of `$4016` and of `$4017` | the 74HC368 printed "40H368(CI)" and the one printed "40H368(CII)" [from the board's print on the scan, and the same values in the KiCad redrawing; that CI and CII are controller ports one and two, and so `$4016` and `$4017`, is inferred from the names, and task 6 checks it against the nesdev wiki before relying on it] |

`Peek` counts nothing and no cycle is added. The host gains `AccessCounts()`
(wrapping counters, so a reader takes differences); `panel.nes` gains
`accessCounts()`, `running`, `region` (so a view loaded after a region change
draws the region the page has now) and `reset()`, and the page announces
`nes:start`, `nes:reset` and `nes:region`. Every quarter second the model marks each chip whose count
moved, and the legend shows each counted chip's accesses per second, read from
the same differences. The words say what a mark means: "read or written by the
processor in the last quarter second", not "working". The legend says in words
that the CPU, the work RAM, the address decoder and the cartridge are in use all
the time, that the video RAM and the address latch are the PPU's own and in use
whenever it draws, that the hex inverter (U9) inverts the PPU's address line
A13 and the reset line and clocks the lockout chip, so it is in use all the
time, and that the lockout chip is not emulated: none of them is marked. (The
inverter's jobs are read from OpenTendo's KiCad redrawing, a cross-check, not
traced on the scan, and the page says so.) The rates are live readings of the running machine, not figures about
the project, so rule 5 is met by reading them from the machine.

**Region.** On `nes:region` the page restarts the machine (the machine's
design); the models redraw for the console and the counters start again, which
the differences absorb.

**The machine changes, a little.** The counters are the only change to the
machine's behaviour code. A fingerprint of the headless machine before and after
must be identical, and the speed is measured before and after on a quiet
machine; a slowdown over 2 per cent is explained before going on, and Dan is
told if headroom is thin (the machine's design, "Speed").

**Labels.** The chips are named in an on-page legend under the inside model
(reference, part for this console, part for the other, role, the access mark as
a shape and a colour, and the rate), generated from the parts file. Pointing at
a chip names it on the status line and marks its row. The board's own printed
legend is in the track map.

## Provenance and licences

The BBC Micro's rules hold: the code, the tools and the geometry we measured
are MIT and ours; photographs are credited and are not MIT; originals that are
large or state no licence are never committed; the tools take a folder
(`NES_MODEL_INPUTS`) and refuse a file whose SHA-256 is not the one recorded in
`tools/nes-model/data/sources.json`; outputs are committed, each with its terms
in `data/README.md`.

- **The bare board scans and the KiCad layout** are in OpenTendo, whose README
  says "Licensed under the TAPR Open Hardware License (www.tapr.org/OHL)". The
  scans carry no licence of their own; that the repository's covers them is
  inferred. **The repository is forked into `dbhq-uk` and pinned** (rule 3)
  before any work reads it, so the inputs cannot move or vanish. The scans are
  never committed here. The track map traced from them is credited and carries
  the TAPR OHL's terms, written in `NOTICE.md` and the tool's `data/README.md`,
  as the BBC Micro's map keeps its scans' position. The KiCad file is a
  cross-check only: numbers from it are compared, nothing is drawn from it.
- **The public-domain photographs** (Evan-Amos's NES-001 and NES-CPU-07, the
  design patent's drawings) may be committed resized and shown in "Photographs
  of the original", as the BBC Micro's public-domain ones are.
- **The CC BY 4.0 photographs** (the PAL set) are credited by author, licence
  and URL wherever used, and may be committed resized under that licence.
  Their full-size originals (up to 93 MB) are never committed.
- **Published case sizes** that are not Nintendo's are quoted as facts with
  their source, and the page says they are not Nintendo's.

## Proof

| What | How | Where |
|---|---|---|
| Board scale | x and y scales apart, from DIP pin pitch on footprints held out of the fit, against 2.54 mm | tool, `data/frame.json`, site test |
| Board outline | from the scan's edge; compared with OpenTendo's KiCad outline, recorded | `data/frame.json` |
| Solder side registration | the back scan mirrored and fitted to the front by drill centres, residual on held-out holes | `data/registration.json`, site test |
| Copper | coverage per face; every drill in copper on both faces; the GND and +5V pins of the logic ICs join into two nets that never touch, through both faces and the holes | `data/copper.json`, site test |
| Parts, NTSC | every IC's pad count is its package's; every identity is read from the populated NES-CPU-07 photographs; places against the KiCad layout's, after a best-fit similarity, to catch a swap | `data/parts.json`, site test |
| Parts, PAL | the NES-CPU-11 photograph registered to the scan; every PAL IC on its NTSC footprint within the task 0 limits; identities read from the photograph | `data/parts.json`, site test |
| Case | proportions from the corner photographs against the published width and depth; the PAL case against the NTSC case on the front face | `data/case.json`, site test |
| Structure | the `model.test.mjs` pattern for each model, and for each region of it | site tests |
| Regions | every module's `regions` equal the page's; a caption, a label and a note for each | site tests |
| Machine state | the counters, tied to what a ROM does; the fingerprint unchanged | the NES's C# tests |
| In a browser | both views load only when shown, draw, orbit under, switch and keep their cameras; the region switch redraws both without a download; Start lights the LED and puts POWER in; a click on RESET resets the machine and the page's reset puts the button down; the PPU and a pad buffer are marked while a ROM that reads the pads runs, and the legend's rates move; no console errors, failed requests or CSP violations | the NES's browser check, which the machine's pull request adds |

Every threshold is set in the plan before the measurement it judges. Every
figure on the page is read from a results file the tools write (rule 5).

## Task 0, and when the work stops

Before any pipeline is built, a short spike measures what everything rests on,
against thresholds the plan fixes before the measurements. A crossed stop is
recorded as crossed, and the plan is reconsidered; it is never tuned around
(the BBC Micro's journal for 4 October 2026 shows why that rule matters).

| Check | If it stops |
|---|---|
| The bare scan's scale, from DIP rows held out of the fit, and x against y | All models stop. Dan is told, and the machine still counts |
| The solder side on the component side, by drill centres held out | As above |
| The PAL board's layout against the NTSC scan, by IC footprint centres | The PAL models stop. Dan decides whether NTSC goes on alone, with the section saying there is no PAL model |
| The case's proportions from the corner photographs against the published width and depth | The outside stops, so every model stops (a cased machine claims both or none). Dan is told |

The numbers are in the plan. The scan is 300 dots an inch, about 11.8 pixels
a millimetre against the BBC Micro's 15.7, so its limits are looser in
proportion. The scan's outline against the KiCad file's (196.252 by 118.700
mm, read from its Edge.Cuts on 5 October 2026 by the reader the plan's task 0
adds) is recorded, not judged: the KiCad layout is a redrawing, "almost 1:1",
not the original. Every DIP on this board lies the same way, so pin pitch
gives the scan's x scale only; the y scale comes from the DIP row spacings on
drill centres, and x against y is the check between them.

## Measured, typical, and known limits

**Measured:** the board's outline, holes, pads, both copper faces and the
print; every part's place; IC identities (both consoles); the case's
proportions and features (corner photographs and the patent's views); the PAL
case's labels and rear (its photographs). **Published, not Nintendo's:** the
case's width and depth, about 10 by 8 inches (254 by 203 mm), from more than
one source that agree. **Measured if the photographs allow, else typical:** the
case's height (published figures disagree, 76 to 89 mm; the plan measures it
from the photographs' proportions and the published width), the heights of the
parts on the board (from the oblique photographs of the populated boards, else
typical, and the page says which).

**Limits:** one bare NES-CPU-10 for the copper; the NTSC parts identified on an
NES-CPU-07, an earlier revision with the same parts [inferring - task 5 checks
each footprint against the photograph]; the PAL copper is the NTSC board's,
justified only by task 0's layout check; the case width is not Nintendo's own
figure; only Chrome with software WebGL is checked.

## What this waits for

- **The machine's pull request, merged.** The NES row running, its page, its
  region control and its panel script. Tasks that only measure (the tools) do
  not wait; the counters and everything on the page do.
- **The BBC Micro's models, merged at least to its two-view task.** The tabs,
  the per-view loader and the rule that a machine claiming a model states
  `case` come from there. The NES does not build a second version: if the BBC
  Micro's work has not reached its task 9 when the NES's page work is ready,
  the NES work waits and Dan is told.
- **Its own fork.** OpenTendo in `dbhq-uk`, before task 0 reads the scans.

## Not in scope

The controllers, a cartridge, the power supply and the RF switch box; the
inside of the case beyond the main board (the shield, the tray, the controller
port assembly, the cartridge tray's spring mechanism, the cables); an opening
cartridge door or any animation of the case; a 3D model of each passive's
internals; the Famicom, the top-loading NES-101, the PAL-A Mattel console and
every other variant; following a net on the board; a shared three.js chunk
between the bundles; other machines' models. The PAL console shown is the one
the photographs show; which PAL variant it is (PAL-B is inferred from the 3195A
lockout chip) is checked in task 0 and stated on the page.

## Decisions

These are Dan's calls of 5 October 2026 and the choices taken by precedent, each
with what it was chosen over.

| Decision | Chosen over | Why |
|---|---|---|
| Both consoles, NTSC and PAL, inside and out (Dan) | The NTSC console only, which the research recommended for its better inputs | Dan's call. The machine runs both regions, so the models show both consoles |
| One module per view, drawing both consoles, with `regions` in `MODELS` | Four modules, with a region on each registry claim | The registry's rules stay as written; the consoles share most geometry, so one bundle per view carries both cheaply; a region switch needs no download |
| LED, POWER and RESET outside; chip marks with live rates inside (Dan) | The BBC Micro's marks alone; the outside's state only, with no machine change | On an NES the PPU and the pads are read every frame, so a mark alone would stay lit and say little; a rate says how much |
| The NTSC board is an NES-CPU-10 from the bare scans, identified from the NES-CPU-07 photographs | The NES-CPU-07 traced from its populated photographs | The bare scans show both copper faces with nothing hiding them; parts hide the CPU-07's top face |
| The PAL board is the NTSC copper with the PAL parts, only if task 0 shows one layout | Tracing PAL copper from a populated top photograph; drawing PAL parts on the NTSC board without a check | No bare PAL board or PAL solder side was found; the check makes the reuse a measurement, not an assumption |
| No Nintendo logo shapes; the case's words drawn in the site's face; the board's print traced as it is | Drawing the logos | As the BBC Micro's owl: the logo is a trade mark and adds nothing the visitor needs; the page's photograph shows the real one. The board's print is part of the board, as the BBC Micro's is |
| The console as made, in the site's grey tokens | As yellowed in the PAL photographs | The model shows the design, not one console's age; the photographs show the age |
| OpenTendo forked into `dbhq-uk` and pinned; the track map carries the TAPR OHL's terms | Reading the upstream repository; leaving the map's terms unstated | Rule 3; the map is traced from the scans, so their terms travel with it |
| Inside first | Outside first | It gives the outside its port positions |
| Reuse the BBC Micro's tabs and loader, and wait for them | Building the NES's own | One way to offer two views; a second would drift from the first |
| A feasibility task first, with stop thresholds, including the PAL layout | Building the pipeline and finding out at the end | Scale, registration, the PAL reuse and the case proportions are what everything rests on |
