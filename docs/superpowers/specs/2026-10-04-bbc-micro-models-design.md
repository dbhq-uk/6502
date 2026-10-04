# The BBC Micro's two 3D models: design

Written 4 October 2026 for issue [#28](https://github.com/dbhq-uk/6502/issues/28):
a machine with a case gets two models, the outside and the inside, offered as
labelled views of the same machine, with the same controls and lazy loading,
and the machine's state shown in both where it makes sense. The sources, what
each is good for and the choices already taken are in
[`docs/bbc-micro/facts/models.md`](../../bbc-micro/facts/models.md). The KIM-1's
model is the pattern: [its journal](../../journal/2026-10-02-the-kim-1-page.md),
`tools/kim1-model/` and `site/src/models/`. The plan is
[`../plans/2026-10-04-bbc-micro-models.md`](../plans/2026-10-04-bbc-micro-models.md).

## What is built

Two models on `/machines/bbc-micro/`, both ours, built in three.js on the stage
every model shares (`site/src/models/stage.mjs`). No downloaded model is used.

- **Outside** (`site/src/models/bbc-micro-case.js`): the beige case from
  Acorn's published size and a triangulated side profile, the keyboard recess,
  every key as a sculpted cap with a legend we draw, the red function keys and
  BREAK, the three LEDs, the badge strip with its words drawn by us and no owl,
  the rear ports and the underside connectors as labelled boxes, and the feet.
- **Inside** (`site/src/models/bbc-micro-board.js`): the Issue 7 main board as
  the emulated machine has it, a Model B with the 8271 and the DFS fitted. Both
  copper faces and the printed legend are traced from flatbed scans of a bare
  board; every IC is placed from the scan and identified from the Service
  Manual; the connectors are placed from the scan; the passives are typical
  bodies on their measured pads. The camera can go under the board.

The inside is built first: it gives the outside the x positions of its ports.

## The registry's `models` field

A machine record may carry `case` (a boolean: whether the machine has a case)
and `models`, a list of `{ "view": ..., "module": ... }`. Both are optional.

| Field | Values |
|---|---|
| `view` | `board` (a machine with no case: the board is the whole machine), `outside`, `inside` |
| `module` | the model's id: `site/src/models/<module>.js`, served as `/models/<module>.js`; it is the machine's id or starts with it and a hyphen |

The KIM-1 becomes `"case": false, "models": [{ "view": "board", "module": "kim-1" }]`.
When both its models are built the BBC Micro has `"case": true` with `outside` / `bbc-micro-case` and
`inside` / `bbc-micro-board`, in that order (the first is the view shown first).

**A model is built** when all four hold, and a test fails on each separately:

1. `site/src/models/<module>.js` exists and exports `mount(root)`
   (`validateRegistry`, through an injectable `modelFiles` check like
   `photoExists`).
2. `MODELS[<module>]` in `site/src/models/models.mjs` exists, and its `machine`
   and `view` are the registry's (`site/tests/model.test.mjs`, both ways: every
   entry in `MODELS` is claimed by exactly one machine).
3. Its provenance is recorded: `site/src/data/<module>-model.json` exists
   (`validateRegistry`), and every input its tool's `data/sources.json` lists
   is credited in the machine's registry entry by URL, as a photograph, a
   drawing or a reference (`model.test.mjs`).
4. After a build, `dist/models/<module>.js` exists, and its track map if its
   entry names one (`model.test.mjs`).

**The rules `validateRegistry` adds**, each with a made-up registry that must
fail in `site/tests/registry.test.mjs`: `models`, when present, is a non-empty
list; a view outside the three; a view claimed twice; a module claimed twice
anywhere in the registry; a module not named for its machine; a module whose
file is missing (rule 1); a module with no results file (rule 3); `case`, when
present, not a boolean; a machine with `"case": false` claiming `outside` or
`inside`; a machine with `"case": true` claiming `board`; **a machine with
`"case": true` that claims a model and does not claim both `outside` and
`inside`**; and a machine that claims a model without stating `case`. The KIM-1
claims one model and passes.

**Models never hold up a machine counting** (Dan, 2 October 2026). A running
machine is never required to claim a model, so a cased machine (the NES next)
can count before its models exist; what it may not do is claim half of them.

**One rule lands last.** "A machine that claims a model states `case`" comes in
with the outside model (plan task 9). Before then the BBC Micro claims only
`inside` and states no `case` (plan task 7), so the inside model goes through
the real page and browser check on its own without a half-claimed cased record
and with no preview mechanism; task 9 adds `"case": true`, the outside, and this
rule together, which closes the gap.

## How the page offers the two views

- **One section, two tabs.** `MachineModel.astro` keeps its single-view markup
  for a machine with one model, so the KIM-1's page does not change. A machine
  with two gets a WAI-ARIA tab list named "Views of the model", tabs "Outside"
  and "Inside" (labels, no full stop), each controlling a tab panel that holds
  its own stage, Reset the view, status line, caption (its text alternative)
  and "How the model was made" note, with ids suffixed by the view. The
  controls paragraph is shared. Left and Right move between the tabs and select,
  Home and End go to the ends, Tab moves into the selected panel's stage and on
  out of it: no focus trap, and Escape on a stage lets go as now.
- **The same controls.** Each panel is a `[data-model]` root mounted on
  `createStage` exactly as the KIM-1's is: the same orbit, wheel, touch,
  keyboard and reset, the same tag "Model, not a photograph".
- **Lazy, per view.** `public/model-loader.js` shows the section, and loads a
  view's bundle only when that view's panel nears the screen or its Load button
  is pressed. A hidden panel never intersects, so the inside's bundle and track
  map are fetched only the first time its tab is chosen. Each view keeps its
  camera when the visitor switches away and back; a hidden stage does not draw.
  Without JavaScript the section stays hidden, as now.
- **Reduced motion:** no easing, no fades on highlights, and the keys go down at
  once.
- **Unchanged:** the KIM-1's page markup, its tests and its browser check, which
  must pass without edits; the CSP; the page's first load holds no file under
  `/models/`.

## What state each model shows

**Outside.** Keys go down as they are pressed, whichever way: the PC keyboard on
the screen, the on-screen keys, or a click on the model, which presses that key
through the same function the on-screen key calls (`panel.bbc.tap`). The page
announces each with a `bbc:key` event, as the KIM-1's does with `kim1:key`. A
latched SHIFT or CTRL shows down. The three LEDs are the machine's:

| LED | Source | Cost |
|---|---|---|
| CAPS LOCK | system VIA addressable latch IC32 bit 6 (`via.md` s2.2) | `SystemVia.Latch` is already public: one export |
| SHIFT LOCK | latch bit 7 | the same export |
| Cassette motor | the serial ULA's control register at `$FE10`, bit 7 [guessing - verify against the Advanced User Guide s.17 and the Service Manual; add the fact to `bus.md`] | today the write goes nowhere; the bus keeps the last byte written to `$FE10-$FE17` (no read side, nothing else modelled) |

The host gains `Leds()`, three bits, 1 meaning lit. Which latch level lights an
LED is [guessing - verify]: the C# test ties each bit to behaviour the ROM
shows, not to what the code prints (after boot letters come out as capitals and
CAPS LOCK is lit; after CAPS LOCK is pressed they do not and it is dark;
`*MOTOR 1` lights the motor LED and `*MOTOR 0` puts it out).

**Inside.** The chips the CPU has read or written lately are marked: the
simplest honest activity. The bus counts accesses per chip in its SHEILA decode
(CRTC, ACIA, serial ULA, video ULA, ROM latch, both VIAs, the 8271, the ADC) and
a sound chip write at the existing latch strobe; `Peek` counts nothing and no
cycle is added. The host gains `AccessCounts()` (wrapping counters, so a reader
takes differences) and `RomSlot()`. Each quarter second the model marks every
chip whose count moved, and marks BASIC or the DFS ROM as paged in. The words
say what a mark means, "read or written by the processor in the last quarter
second", not "working". The CPU, RAM and OS ROM are never marked (always in
use), and the system VIA and ACIA almost always are (the 100 Hz tick and the IRQ
poll); the legend says both. The speed is measured before and after.

**Labels.** The chips are named in an on-page legend under the inside model (IC
number, part, role, and the access mark as a shape and a colour), generated from
the parts file. Pointing at a chip names it on the status line and marks its
row. The board's own printed legend is in the track map.

## Provenance and licences

- **MIT (ours):** the code, the tools, and the geometry we measured (outlines,
  places, sizes, key centres as numbers, the results files).
- **K1** (Dominic Beesley's KiCad keyboard at
  `5ec53df566c77424caa6be827d0831da3916ecfa`) is MIT: its notice goes in
  `NOTICE.md` and the tool's `data/README.md`.
- **Photographs are credited and are not MIT.** The public-domain ones used (O1
  to O5) are committed resized and shown in "Photographs of the original", as
  the KIM-1's are.
- **Originals are not committed.** A scan or photograph that is large or states
  no licence (I1, I2, I5, I6, O8) is never committed, not even resized. The
  tools take a folder (`BBC_MODEL_INPUTS`) and refuse a file whose SHA-256 is not
  the one in `tools/bbc-micro-model/data/sources.json`; URL, fetch date and hash
  are also in `site/src/assets/photos/README.md` and in a new registry list,
  `references`, credited under the model like `drawings`. The KIM-1 committed
  its unlicensed photographs resized; the rule in `models.md` is the newer one.
- **Outputs are committed**, each with its terms in `data/README.md`. The track
  map is traced pixel by pixel from I1 and I2 and keeps their position (no
  licence stated, credited). M1 and M2 are never used; K2 only as numbers for a
  cross-check, credited.

## Proof

| What | How | Where |
|---|---|---|
| Board scale | x and y scales apart, from pin pitch on DIP footprints across the board; checked on held-out footprints against 2.54 mm and a 40-pin row of 48.26 mm | tool, `data/frame.json`, site test |
| Solder side registration | I2 mirrored and fitted to I1 by drill centres, residual on held-out holes | `data/registration.json`, site test |
| Copper | coverage per face in a band; every drill sits in copper on both faces; the GND and +5V pins of the logic ICs join into two nets that never touch, through both faces and the holes; the top face against I5 where I5 shows the board, as an agreement figure | `data/copper.json`, site test |
| Parts | every IC's pad count is its package's; every identity is table 8.1's; the fitted set is the Model B with the 8271 and the DFS; places against the manual's approximate positions, to catch a swap | `data/parts.json`, site test |
| Case | footprint 415 by 345 mm and height 73 mm with feet, from D1; the rectified O1's front edge in key pitches within 1.5 per cent of 415 | `data/case.json`, site test |
| Keys | key centres equal K1's; O1 and O3 registered to them with a held-out error per key; every key the machine has, once, with its legend from `bbc-keys.js` | `data/keys.json`, site test |
| Structure | the `model.test.mjs` pattern for each model: tokens only, module and bundle, budgets, lazy loading, labels, controls text, underside lit | site tests |
| Machine state | the LED bits and the access counters, tied to ROM behaviour | `tests/Dbhq.Machines.BbcMicro.Tests` |
| In a browser | both views load only when shown, draw, orbit under, switch and keep their cameras; a page key lights a model key; a model key types; the LEDs follow CAPS LOCK and `*MOTOR`; the 8271 is marked during `*CAT`; no console errors, failed requests or CSP violations | `site/scripts/browser-check-bbc.mjs` |

Every threshold is set in the plan before the measurement it judges. Every
figure on the page is read from a results file the tools write (rule 5).

## Measured, typical, and known limits

**Measured:** the board's outline, holes, pads, both copper faces and the
print; every part's place; IC identities; the case outline and recess (O1); its
side profile (O4 and O5, triangulated, with a check point); key centres (K1,
checked on O1 and O3); LED, badge and grille places (O1); port and connector
places (I1's sockets and plugs, heights from O2). **Published:** the case
footprint and height (D1). **Typical, and said so on the page:** every height on
the board, passive bodies, the keycap profile and travel (checked against K2),
edge radii, socket depth, the feet, and the underside panel round the
connectors (O8 is 640 pixels wide).

**Limits:** one bare Issue 7 board for the copper, with tracks under the print
and the solder recovered only where the rules see them; the case size is the B+
section's, assumed the Model B's [guessing - verify], and the Science Museum
gives 5 mm less width; K1 is a replacement keyboard made to fit the case, not
Acorn's drawing; only Chrome with software WebGL is checked.

## Not in scope

A 3D model of each passive's internals; an opening lid or any animation of the
case; the inside of the case (power supply, speaker, keyboard circuit board,
ribbon cables); parts not fitted (the Tube, Econet, speech): their footprints
show in the track map and nothing stands on them; following a net on the board;
a shared three.js chunk between the two bundles; other machines' models.

## Decisions

| Decision | Chosen over | Why |
|---|---|---|
| Sculpted keycaps with drawn legends | Boxes with a legend texture; no legends | The keyboard is what a visitor looks at, and its layout is measured (`models.md`, choice 1) |
| The inside is a Model B with the 8271 and DFS fitted | A factory board with empty sockets; the 8bs photograph as taken | It is the emulated machine; the page runs DFS (choice 2) |
| Badge strip with drawn words, no owl | A simplified owl | The owl is the BBC's mark; the page's photograph shows the real badge (choice 3) |
| Underside connectors as labelled boxes | Leaving them out | They are where the Model B's expansion is (choice 4) |
| Copper traced on the inside | A plain board | A track source exists (choice 5) |
| Inside first | Outside first | It gives the outside its port positions (`models.md`, order) |
| `case` and `models` in the registry, with views `board`, `outside`, `inside` | Inferring the case from `category`; one `model` field | A category does not say whether a machine has a case; one field cannot hold two models |
| A cased machine that claims a model claims both; no machine is required to claim one | A running cased machine must claim both models | That would contradict Dan's 2 October ruling that models never hold up a machine counting, and it would fail validation today for the live BBC Micro (decided 4 October, at the controller's review of this design) |
| The BBC Micro claims `inside` with no `case` in task 7, and `case` with both views in task 9, where "a machine that claims a model states `case`" lands | Claiming nothing until task 9; a preview page or flag that does not ship | The inside goes through the real page and browser check in its own task, with no half-claimed cased record and no preview mechanism to delete later |
| Tabs in one section | Two stacked sections; one canvas swapping scenes; a toggle button | Two views of one machine, each loaded only when chosen; tabs are the pattern for switching views; one canvas would need one bundle for both |
| One bundle per model, three.js in each | Code splitting a shared chunk | A hashed chunk breaks the deploy's fixed serving list and changes the KIM-1's build; the cost is about one more three.js download for a visitor who opens both, measured |
| Inside shows chip accesses per quarter second, and the paged ROM | Static; a per-cycle trace | Honest, cheap (SHEILA accesses and one strobe), and visible: the 8271 during a disc command, the CRTC as text scrolls |
| LEDs from the latch and the serial ULA's control byte | The cassette LED left dark | One stored byte, no new behaviour; every LED the case has is the machine's |
| Chip names in an on-page legend | 3D text in the scene | Readable at any angle and by a screen reader |
| Scans and no-licence photographs not committed, credited as `references` | Committing resized copies, as the KIM-1 did | The rule in `models.md`; the scans are 13 to 15 MB with no stated licence |
| One tool folder, `tools/bbc-micro-model/`, for both models | Separate board and case folders | One `sources.json` and one verifier for the machine's inputs, and the case reads the board's connector places |
| A feasibility task first, with stop thresholds | Building the pipeline and finding out at the end | Scale, registration and the key fit are what everything rests on; a day proves them |
