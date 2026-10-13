---
title: "The BBC Micro's models: the inputs, and what they rest on"
date: 2026-10-04
summary: "Before the BBC Micro's two models are built, the work measures what both rest on: the scale of a flatbed scan of the bare board, how well the solder side lies on the component side, and how well a photograph of the keyboard fits an open keyboard layout. The keys, the scan's scale down the board, and the board's size and outline hold, and the solder side holds except for a few holes. Two checks failed and were revised the same day, each kept on record as it failed: the case's front edge, measured on the plane of the keys, which it does not lie in; and a row by row check of the board's width scale, which magnified the noise of short rows and met one long row off the pitch, and as revised lands between pass and stop. The solder side then lies on the component side within the plan's limits over every hole found, and the board's chips come out as footprints, pin one read from the print. Both faces' copper is then traced from the scans with its method fixed first, but its check on the board's power nets fails, and so does its comparison with a photograph of a populated board, so the track map is offered to look at, not as a netlist. Every chip is then placed on its footprint and named from the Service Manual, with the disc interface fitted and Econet left out; the places miss a few of the manual's approximate ones, and the markings on a populated board disagree with the manual for four causes, one a flaw in the rule, so both of those checks fail too, and are kept as they failed. The machine then reports its keyboard lights and how often the processor reaches each chip, counted without changing a single cycle; its speed in a browser is still to be measured, by a rule written down first. The inside model's drawing, its words and its figures are then built and tested, waiting off the page until the outside joins it; every word it says about the copper calls it traced to look at, and its note gives every failed check beside the figures. The outside is then measured: every key of an open keyboard drawing is matched to the machine's, two photographs of one machine are triangulated to set the keys and lights in the case, and the rear ports are checked against their sockets on the board; both checks pass. The front edge's correction for parallax cannot be measured, for want of the camera's height, and three measures put the case a few per cent wider than the manual's size, which is recorded, not resolved."
order: 28
---

# 4 October 2026: the BBC Micro's models, the inputs and what they rest on

Issue 28 gives a machine with a case two models: the outside (case, keys,
LEDs) and the inside (the board, its chips and both faces of copper). The
design is
[`docs/superpowers/specs/2026-10-04-bbc-micro-models-design.md`](../superpowers/specs/2026-10-04-bbc-micro-models-design.md),
the plan
[`docs/superpowers/plans/2026-10-04-bbc-micro-models.md`](../superpowers/plans/2026-10-04-bbc-micro-models.md),
and the sources with their licences
[`docs/bbc-micro/facts/models.md`](../bbc-micro/facts/models.md). This entry
records the design's decisions, then task 0: the inputs checked, and the
three numbers everything else rests on, each measured on data held out of its
fit and judged against thresholds the plan set before the measuring.

**Task 0 crossed a STOP threshold.** The case's front edge, measured on the
photograph's registration to the keys, is 2.78 per cent short of Acorn's
415 mm, where the plan stops at 2.5. The other figures are below. The plan is
reconsidered before task 1.

## The design's decisions

From the decisions table in the design, each with what it was chosen over:

- **Sculpted keycaps with drawn legends**, over boxes with a legend texture,
  or no legends. The keyboard is what a visitor looks at, and its layout is
  measured.
- **The inside is a Model B with the 8271 and the DFS fitted**, over a factory
  board with empty sockets, or the 8BS photograph exactly as taken. It is the
  machine the page emulates, and the page runs the DFS.
- **The badge strip with its words drawn and no owl**, over a simplified owl.
  The owl is the BBC's mark; the page's photograph already shows the real
  badge.
- **The underside connectors as labelled boxes**, over leaving them out. They
  are where the Model B's expansion is.
- **Copper traced on the inside**, over a plain board, because a track source
  exists: flatbed scans of a bare board.
- **The inside first**, over the outside first: it gives the outside the x
  places of its ports.
- **`case` and `models` in the registry, with views `board`, `outside` and
  `inside`**, over inferring the case from the machine's category, or one
  `model` field. A category does not say whether a machine has a case, and one
  field cannot hold two models.
- **A cased machine that claims a model claims both, and no machine is
  required to claim one**, over requiring a running cased machine to claim
  both. That would contradict Dan's ruling of 2 October that models never hold
  up a machine counting, and would fail validation today for the live BBC
  Micro.
- **The BBC Micro claims `inside` with no `case` in task 7, and `case` with
  both views in task 9**, over claiming nothing until task 9, or a preview
  flag that does not ship. The inside goes through the real page and browser
  check in its own task, with no half-claimed record and nothing to delete
  later.
- **Tabs in one section**, over two stacked sections, one canvas swapping
  scenes, or a toggle button. They are two views of one machine, each loaded
  only when chosen; one canvas would need one bundle for both.
- **One bundle per model, three.js in each**, over a shared code-split chunk.
  A hashed chunk would break the deploy's fixed serving list and change the
  KIM-1's build; the cost is one more three.js download for a visitor who
  opens both.
- **The inside marks the chips the processor read or wrote in the last
  quarter second, and the paged ROM**, over a static board, or a trace per
  cycle. It is honest, cheap and visible.
- **The LEDs from the addressable latch and the serial ULA's control byte**,
  over leaving the cassette LED dark: one stored byte, and every LED the case
  has is the machine's.
- **Chip names in a legend on the page**, over text in the 3D scene: readable
  at any angle and by a screen reader.
- **Scans and photographs with no stated licence are not committed, and are
  credited as references**, over committing resized copies as the KIM-1 did.
  The scans are 13 to 15 MB with no stated licence.
- **One tool folder, `tools/bbc-micro-model/`, for both models**, over a board
  folder and a case folder: one list of inputs and one verifier, and the case
  reads the board's connector places.
- **A feasibility task first, with stop thresholds**, over building the
  pipeline and finding out at the end. Scale, registration and the key fit are
  what everything rests on, and a day proves them or does not.

## Task 0: what was built

`tools/bbc-micro-model/`, offline Python that never fetches:

- `data/sources.json`: the six inputs the research hashed (O1, O2, O3, I1, I2,
  I5) and K1, each with its URL, the page that gives its author and licence,
  the licence as stated (null for the scans and the 8BS photograph, which
  state none), the fetch date, size, pixels and SHA-256. Nothing is committed
  from any of them.
- `verify.py`: present, missing or hash differs, for every input or the ones
  named. It fails on a wrong hash, or on a missing input it was asked for.
- `common.py`: the inputs (`original()` refuses, with exit code 2, a file
  whose SHA-256 is not the recorded one), a finder for the tinned pads on a
  scan, a reader for the KiCad keyboard's key centres, and `fit_held_out`,
  which fits a similarity, an affine, a homography, or a homography with a
  cubic correction, and scores it on points held out of the fit (each point in
  turn, or whole folds such as a chequerboard of blocks).
- `tests/test_common.py`: pytest on made-up inputs, written first and seen to
  fail. A known similarity, affine and homography come back to under 1e-6;
  with 0.1 mm of noise the held-out median comes out within a factor of two of
  it; a leave-one-out fit never contains the point it scores (one point moved
  10 mm scores exactly 10 mm held out); folds hold out a whole block.
- `data/marks.json`: every point marked by hand, with the crop it was read
  from. `spike.py` writes `data/spike.json`.
- `site/tests/bbc-models.test.mjs`: the sources are well formed; their hashes
  are the ones in `models.md`'s table, read from the markdown; no file under
  `tools/` or `site/src/assets/` has the SHA-256 of any original (checked by
  copying O2 into the tool's folder for a moment: the test failed, naming it);
  the verdicts in `spike.json` are what its figures give against the plan's
  thresholds; and `site/package.json` names neither the tool nor Python.

### The inputs

The six files in `~/dbhq-previews/bbc-model-research/` matched the SHA-256 in
`models.md`. K1 was fetched by hand, as the plan says: `git clone` of
github.com/dominicbeesley/bbc-keyboard into that folder, then `git checkout
5ec53df566c77424caa6be827d0831da3916ecfa`. Its
`KiCad/bbc-keyboard/bbc-keyboard.kicad_pcb` hashed to the value the research
had in its own checksum list, and that hash is now a row of `models.md`'s
table. Its 74 switch footprints are read for one point each, the switch's
4 mm locating hole, which is the middle of the switch whatever the footprint's
origin; on this file it is also the origin.

The virtual environment, made on 4 October 2026 with `python3 -m venv
/tmp/bbcvenv && /tmp/bbcvenv/bin/pip install numpy opencv-python-headless
pillow scipy pytest`: Python 3.12.3, numpy 2.5.3, opencv-python-headless
5.0.0.93, Pillow 12.3.0, scipy 1.18.1, pytest 9.1.1.

### How the marks were made

Marking by hand meant reading coordinates off crops of the original, each
enlarged three to five times with a grid every 10 pixels labelled in the
original's pixels. The crop box and the enlargement are recorded beside every
mark in `marks.json`.

- **The scan's scale:** the four corner pins (pins 1, N/2, N/2 + 1 and N) of
  16 DIP footprints spread over I1: five 40-pin (IC1, IC2, IC3, IC69, IC78),
  six 28 or 24-pin, three 14 or 16-pin, and two 16-pin footprints that lie
  across the board (IC74, IC75), so both directions are measured. One 28-pin
  footprint at the left edge is named by its place, because the label printed
  beside it could not be read with confidence. In code, each mark is moved to
  the centroid of the solder blob nearest it (the blob is the grey, light
  pixels, with its specks filled; it must be round and pad-sized), and the
  move is refused when that centroid is over 0.6 mm from the mark.
- **The solder side's seed:** one pad at a corner of each of four footprints
  far apart, on I1 and on I2 flipped left to right.
- **The keys:** the centre of the top face of 50 keycaps on O1, over every
  row: the middle of the face's outline left to right, and between its rear
  edge and the highlight on its front edge. Not refined in code. The space bar
  was left out before any fit: in O1 it is seen almost edge on and its top
  cannot be told from its front.
- **The case's front edge:** its two front corners on O1, where the line of
  the side meets the line of the front (the corners are rounded).

### The three numbers

Measured on 4 October 2026 with `cd tools/bbc-micro-model &&
BBC_MODEL_INPUTS=~/dbhq-previews/bbc-model-research /tmp/bbcvenv/bin/python
spike.py`, which writes `data/spike.json` and exits 3 when a figure crosses a
STOP threshold. It took about a minute.

**1. The scan's scale: passes.** Of the 64 marks, 53 were moved to a pad
centroid (median move 0.12 mm, largest 0.54 mm) and 11 were refused: their
pads carry solder too ragged to pass as a round blob, so those corners were
left out rather than forced. One linear map from millimetres to pixels (x and
y scale and any skew), with a free offset per footprint, was fitted on one
half of the footprints and every row of the other half measured through it;
then the halves swapped, so every footprint was held out once (the halves
were set in `marks.json` before any fit). On the 40-pin rows held out,
against 48.26 mm: median error 0.043 mm, largest 0.107, over 7 rows (three of
the ten lost a corner to a refusal). The plan's pass is a median of at most
0.10 and a largest of at most 0.25. Over all 21 rows held out the median is
0.054 mm and the largest 0.371 (a 28-pin row of IC7).

**The scale, x against y: recorded, and under the stop, but close.** Fitted on
all 16 footprints: 15.9283 pixels per millimetre across (x) and 15.7292 down
(y), a ratio of 1.0127, or 1.27 per cent apart, against a stop at 1.5. The
scan is turned 0.27 degrees, with no measurable skew (0.05 degrees).

That x figure is not the researcher's (15.79), and the reason is worth
having: 14 of the 16 footprints lie down the board, so across the board they
give only their row spacing, 15.24 or 7.62 mm, and the solder centroids of
the two rows sit about 1 per cent further apart than that. As a check that
uses no row spacing, the spike also fits a line to six rows of connector pads
that run across the board (PL8, PL9, PL13 and PL14, on the 0.1 inch pitch):
15.745 to 15.799 pixels per millimetre, median 15.764, which is the
researcher's figure and puts x and y about 0.2 per cent apart. Task 2 should
take x from pitch along a row, not from row spacing.

**2. The solder side: between pass and stop.** The pad finder found 2289
pads on I1 and 2536 on I2 mirrored. Seeded by the four marked pairs, 2092
holes were matched (each pad the other's nearest, within 1.0 mm, a gate set
before matching: under half the 2.54 mm pitch). On a chequerboard of 20 mm
blocks, each colour held out of the fit and scored by a fit to the other:

| Fit | Median | 90th percentile | Largest |
|---|---|---|---|
| Affine | 0.135 mm | 0.293 mm | 0.878 mm |
| Cubic (a homography, then a cubic correction) | 0.120 mm | 0.270 mm | 0.757 mm |

The cubic is used: every held-out figure is lower. Its median (pass 0.15)
and 90th percentile (pass 0.30) pass; its largest is over the pass value of
0.60, which is not a stop (the stops are a median over 0.25 or a 90th
percentile over 0.50). The large errors are not a region: the median is 0.11
to 0.15 mm in every 50 mm band of the board, except 5 holes at the far right
edge, and only 9 of the 2092 are over 0.6 mm, scattered. They look like single
pads whose solder sits off the hole on one face. The four seeds agree with the
final fit to within 5 pixels.

**3. The keys: pass.** A homography from O1 to K1's key centres, each of the
50 keys held out in turn: median 0.395 mm, 90th percentile 0.632, largest
0.989 (Q), and no key over 3 mm. The pass is a median of at most 1.0, a 90th
percentile of at most 2.0 and a largest of at most 3.0.

Keycap tops are not in one plane: the rows are sculpted, each row's top at
its own height and angle, so part of what the homography leaves is parallax
between rows. One offset per row explains 32 per cent of the squared error
left by the fit to every key; the row offsets are at most 0.38 mm (the row
from TAB to DOWN, across the board). Marking is the larger part: five keys
marked a second time on crops placed differently land 0.55 mm from their
first mark at the median, 0.76 at most. (Reworded after review, the same
day.) A difference between two marks carries the noise of both, so one mark's
noise is about 0.55 / 1.41, or 0.39 mm: the held-out median of 0.395 mm
equals one mark's noise. The fit is at its floor, so it cannot resolve
differences between K1 and the keyboard in O1 under about 0.4 mm, and the
repeat sample is only five keys, so that floor is itself a weak estimate.

**The case's front edge: STOP.** Mapped through the homography fitted to all
50 keys, the line between O1's two front corners is 403.5 mm long, 2.78 per
cent short of the 415 mm in the Service Manual. The plan passes within 1.5 per
cent and stops outside 2.5. After the stop, both corners were read again on
wider crops (recorded in `marks.json` as a repeat, not used for the verdict):
403.2 mm. The marks are not the cause.

### Why the front edge is short, and what I would try

[inferring] The front edge is not in the plane of the key tops, and a
homography is only true in its own plane. The case's front edge is lower than
the key tops, and a point below the plane, seen through it, lands closer to
the point under the camera, so a width there reads short. A rough camera can
be had from the homography alone (square pixels, the centre of the picture as
the principal point, the focal length chosen so the plane's two axes come out
at right angles). `spike.py` computes it, with a jackknife over the keys (the
same command as above; the figures are `keys.parallax` in `spike.json`): a
focal length of 2471 pixels and a height of 536 mm above the key plane, with
jackknife standard errors of 1671 pixels and 355 mm. From there a front edge
15 mm below the key plane would read 403.7 mm, or, the other way round, the
measured 403.5 mm corrected for 15 mm comes to 414.8. **The 15 mm is assumed,
not measured**, so the figure is consistent with the cause, not confirmed by
it; and with the camera's height that loose, almost any depth from about 10 to
25 mm could be made to fit. The reviewer, working independently, got a focal
length of 2545 pixels and a height of 543 mm, and the same jackknife spread
(540 plus or minus 343 mm); the small difference comes from fitting the
homography another way. The keyboard covers too little of the picture for
that camera to be trusted far, so this is a likely cause, not a measured one. The Science Museum's 410 mm would
still be 1.6 per cent off. The research's estimate of about 412 mm came from
the key pitch in pixels, not from a registration, and `models.md` now has a
note saying so.

What I would try, for the plan's next version to choose (the thresholds are
not mine to move):

1. Measure the front edge's height below the key tops from O4 and O5, the
   triangulation pair task 8 already plans, and correct the width for it; then
   judge the corrected width against 415 mm.
2. Or judge the case on its own plane: register the case top's four corners
   to D1's 415 by 345 mm footprint, and check the keys against that, with the
   key plane's height as the unknown.
3. Or drop the front-edge check from task 0, since the design already takes
   the case's size from D1 and uses O1 only for shape, and keep the key fit,
   which passes.

## Mistakes

- Reading the first sheet of crops, I took each crop's top to be at the top
  of its tile, forgetting the 20-pixel label strip above it, so the first row
  of marks came out 5 pixels low. Caught on the second sheet by checking the
  labelled grid lines, and corrected before any fit.
- The case's right front corner was first worked out from the wrong crop
  origin (2155 where the crop started at 2140), which put it 15 pixels to the
  right. Caught while writing the marks and corrected before any fit. Had it
  stood, the front edge would have read about 3 mm longer, and still been a
  stop.
- The first run of `spike.py` reported the four seed pads' fit as 1.7 mm. It
  was 1.7 pixels: that fit is from pixels to pixels. The field is now
  `seedFitPx`.
- The first `marks.json` said each footprint's pin count was found by
  counting its pads. It was read from the row's length in pixels at the
  2.54 mm pitch (one pin more or fewer is about 40 pixels away). A check that
  counted the pad finder's pads along each row was removed: it measured how
  many pads the finder misses, not the pin count.

## Where this leaves the plan

Task 0 ends BLOCKED, with the figures above, and the plan is reconsidered
before task 1. The site test that the spike's figures pass the plan's pass
column is written as the plan states it and marked as a to-do naming this
entry, so the suite stays green and reports it; it binds once the plan is
revised. The test that the recorded verdicts are what the figures give binds
now.

## The plan revised, later the same day

The stop was real, and it stays recorded as crossed: `spike.json` keeps the
front edge's verdict as measured, with a `revision` note pointing at the plan.
What the review found wrong was the check, not the board or the photograph.
The case's front edge is not in the plane of the key tops, so a homography
fitted on that plane cannot give the edge's width; the check judged the wrong
quantity. The parallax estimate above (403.7 mm predicted for an edge 15 mm
below the key tops, against 403.5 measured) is consistent with that, but the
15 mm was assumed, not measured, so it does not confirm it, and the revision
asks task 8 to measure the edge's depth rather than assume it.

The revision, in the plan's thresholds table and its Task 0 outcome:

- O1's raw front edge on the key-plane registration is now recorded, not a
  stop, with task 0's 403.5 mm and the old stop it crossed written beside it.
- A new row for task 8: the front edge's width after correcting for parallax,
  with the edge's height measured from O4 and O5 (or a camera model fitted to
  the key fit and the edge's measured height), judged against the same
  numbers: pass within 1.5 per cent of 415 mm, stop outside 2.5.
- A new recorded row for task 8: the case top's plan size from the published
  dimensions against O1 registered on the case top's own four corners.

Chosen over **dropping the check**: it is the only independent check of the
registration's scale out at the case, so without it nothing tests whether the
keys' fit puts the case the right size. Chosen over **relaxing the number**:
moving the stop after the measurement crossed it would be tuning, which the
plan rules out.

Two smaller revisions went in with it. Task 2 now takes the board's x scale
from pin pitch along rows that run across the board (the connector rows), not
from DIP row spacing, which task 0 found reads about 1 per cent wide (15.9283
pixels per millimetre from row spacing, 15.745 to 15.799 from connector pitch);
the 1.5 per cent stop on x against y stays. And the hygiene check for private
project names no longer carries the alternatives from the port goal dropped on
1 October, which matched historic plans and the site test that bans them.

The site test that was a to-do now binds to the revised rule: the scale and
the keys pass, nothing judged is a stop, the solder side's median and 90th
percentile pass (its full row, the largest error too, is task 3's test), and
the front edge's raw width is recorded with the verdict it got, not judged.

## The review, and a second revision of the plan

A reviewer reran every figure from the committed code and the inputs and
reproduced each one, and found the stop honest. Four things in the revision
above were too loose, and were fixed the same day.

**The corrected front edge could not fail.** The corrected width needs h, the
camera's height above the key plane, and the revision never said where h
comes from. From the key fit alone it is 536 mm with a jackknife standard
error of 355 mm (above), and for an edge 15 mm down the corrected width passes
for any h from about 340 to 1140 mm: a check that passes whatever the camera
did is not a check. Now h must come from a measurement independent of the
415 mm, with its uncertainty, and d from O4 and O5 against the same fitted key
plane, both recorded before the corrected width is computed. The width's
interval is judged: pass only if all of it is within 1.5 per cent of 415 mm,
stop only if all of it is outside 2.5, and anything between is inconclusive,
which is not a pass and goes back to the plan. If no independent h can be had,
the check is dropped as not measurable rather than fitted, and the case's size
rests on the Service Manual, as the design already takes it. Chosen over
fitting h from the key fit: that is the circle the reviewer found.

**The solder side's largest error had been pushed to task 3 with no rule
for outliers**, and task 3's data file had an `outliers` field with nothing
saying how a hole gets into it: an open door to a rule written after seeing
the residuals. Now the largest error is recorded, not a pass criterion (a few
scattered ragged pads, 9 of 2092 over 0.6 mm), the median and 90th percentile
stay the pass, the stops are unchanged, and the outlier rule is fixed before
task 3 runs and never looks at a residual: a hole may be left out of the
scoring only if its pad fails a roundness test on either face (an axis ratio
over 1.25 from the blob's second moments, or an area outside 0.6 to 1.6 times
that face's median), and the figures are reported with and without it.

**Nothing held the board's x scale to the 0.1 mm level.** All five 40-pin
footprints lie down the board, so the scale's pass row tests only y. Task 2
now holds out each row of connector pins from the x fit in turn and judges its
length the way y is judged (0.10 mm median and 0.25 largest to pass, 0.20 and
0.50 to stop, scaled to a 48.26 mm row). This check was added after task 0's
figures had been seen, so it is a stricter check, not a convenient one: the
connector rows already agree with each other to about 0.3 per cent, and the
check exists to hold that.

**The summary at the top of this entry was wrong**, and it is on the site's
home page: it said two of three measures held and the third did not, but the
front edge is a fourth check, not one of the three, and it said the plan stops
here, though the plan was revised the same day. It was rewritten.

Smaller fixes: the camera figures now come from `spike.py` with their
jackknife, where before they came from a calculation in a scratch session; the
wording on the marking noise and on the 15 mm says what is and is not shown;
a test now holds the KiCad reader to a turned footprint whose hole is off its
origin, since task 8 reuses it; and the site test that no original input is
committed now also hashes everything under `site/public/` and `docs/`.

## Task 1: the registry says which models a machine has

Plumbing only, no measuring. A machine record may now carry `case` (a
boolean: whether the machine has a case) and `models`, a list of
`{ "view", "module" }`, where `view` is `board`, `outside` or `inside`
(`VIEWS` in `site/src/lib/registry.mjs`) and `module` names
`site/src/models/<module>.js`. Both are optional. The KIM-1 now says
`"case": false` and claims one model, `{ "view": "board", "module": "kim-1" }`.
The BBC Micro is unchanged: no `case` and no `models`, and valid, because a
running machine is never required to claim a model (Dan, 2 October 2026). A
third optional list, `references`, holds the sources a model was measured from
that are never committed (the scans, the photographs with no stated licence):
each is credited like a drawing (title, author, source, licence as stated,
fetched, used) and names the SHA-256 of the original, 64 lower-case hex
digits, so the credit says which file was measured. Nothing uses it yet.

`src/models/models.mjs` is now keyed by module, and each entry names its
`machine` and its `view`; `modelsOf(machine)` is the registry's list, or none.
The machine page shows a model section for each module its machine claims,
where before it showed one for any machine id in the map. The credit under a
model reads `references` after `drawings`, in the same sentence.

**Each rule, and the made-up registry that fails it**
(`site/tests/registry.test.mjs`, with `modelFiles` injected so a made-up
module can be present or absent, as `photoExists` does for photographs):

| Rule | Fails on |
|---|---|
| `models`, when given, is a non-empty list | `[]`, and an object in place of a list |
| each entry is an object | a bare string |
| a view is one of the three | `top`, and an entry with no view |
| a machine claims each view once | two `board` entries |
| a module is claimed once in the whole registry | the same module on two machines, and twice on one |
| a module is named for its machine | `aim-65` and `kim-10` on the KIM-1, `../kim-1`, and no module at all |
| the module's file is there and exports `mount(root)` | `modelFiles` saying no file, and a file with no `mount(root)` |
| the results file is there | `modelFiles` saying no `<module>-model.json` |
| `case`, when given, is a boolean | `"yes"` and `null` |
| no case, no outside or inside | `"case": false` with `outside`, and with `inside` |
| a case, no bare board | `"case": true` with `board` |
| a cased machine that claims a model claims both | `"case": true` with only `inside`, and with only `outside` |
| `references` is a list of credited references | an object, a string entry, and each missing or malformed field, the SHA-256 in capitals and one digit short |

The last rule fires only when `case` is stated: a machine with no `case` may
claim `inside` alone, which is what the BBC Micro will do in task 7. The rule
that closes that gap, "a machine that claims a model states `case`", is task
9's, as the plan says, and is not here. A test also holds that a running cased
machine with no models and no case is valid (the BBC Micro today), and that
`"case": true` or `false` with no models is valid too.

`site/tests/model.test.mjs` holds the other half of "built", both ways: every
entry in `MODELS` is claimed by exactly one machine with the same view, every
claimed module is in `MODELS` with that machine and view, and each running
machine's built page has a model section for exactly the modules it claims
(the KIM-1 one, the BBC Micro none) and loads the model loader exactly then.

**Why `case` is stated and not read from `category`.** The categories are
`single-board`, `computer`, `console` and so on, and none of them says whether
the machine has a case: a single-board machine may be sold in one, and a
console's board is never the whole of it. The design's
decisions table chose an explicit field over inferring it, and a wrong
inference here would silently allow the half-claimed record the rule exists to
refuse.

**Decisions taken while building it.**

- A module whose name is not its machine's is reported and never looked for
  on disk, so a name like `../kim-1` cannot make the check read outside
  `src/models/`. Chosen over checking the file regardless: the name error is
  the one that matters, and a second error about a missing file would be noise.
- "A module claimed twice" is checked in `validateRegistry`, which sees every
  machine, not in `modelProblems`, which sees one; a module twice on one
  machine is caught by the same check.
- The page now throws at build time if a machine claims more than one model,
  saying the page offers one until the two views land as tabs (task 9). Chosen
  over rendering every claimed module as its own section, which would put two
  elements with the same ids on one page, and over rendering the first only,
  which would drop the second without saying so. No machine claims two yet;
  task 9 replaces the throw with the tabs.
- `model.test.mjs` takes `modelsOf` on an import line of its own rather than
  by editing the existing import, so that no line of the KIM-1's existing
  tests changed (the plan allows no edits to them in this task, and none were
  needed).

**The KIM-1 shown unchanged** (4 October 2026). Task 0's last commit,
`a14732e`, was exported with `git archive a14732e` into a scratch folder
outside the repository, given this checkout's `node_modules`, the built
machines in `site/public/machines/` and `site/src/data/results.json`, and
built with `npm run build`; this task's tree was built here with
`npm run build`. `cmp` of the two `dist/machines/kim-1/index.html` printed
nothing (both SHA-256 `177e8c64...48fa5e`), and `diff -rq` of the two whole
`dist/` folders, built before this entry was written, printed nothing either:
every page of the site, the BBC Micro's included, was byte for byte what it
was. After it, the one page that differs is this journal entry's. The KIM-1 has no references,
and its section is rendered from the same entry.

`node scripts/browser-check.mjs`, unedited, was run twice here on a shared
host with a load average between 20 and 50 on 8 cores. The first run failed
two timing checks: a finger on the focused KIM-1 model scrolled the page by
19 pixels, and the BBC Micro drew 15 fields in a second where the check wants
more than 20. The second run passed every KIM-1 check and failed the BBC one
again, again at 15 fields. Neither can come from this task: the site it
served is byte for byte the one task 0 built, and `Validate`, browser check
included, passed in CI on `a14732e`. The browser check on this task's commit
is CI's.

**Tests.** `cd site && npm test`: 267 passed before this task and 282 after
(12 in `registry.test.mjs`, 3 in `model.test.mjs`), none failed. Both
workflows' floors were raised from 267 to 282.

## Task 2: the board's frame, and a stop on the x scale

`tools/bbc-micro-model/board_frame.py` puts a frame on the bare scan I1: a
board millimetre X, from the left rear corner, is at pixel
origin + diag(sx, sy) R(turn) X. The scanner's two scales act after the
turn, because they belong to the scanner, so a row's length in millimetres
does not depend on the turn at all. `data/frame.json` holds the result;
`run-board.sh` runs it; a copy of I1 rectified to the frame at 16 pixels per
millimetre goes to `out/`, which is git-ignored, and was looked at.

**Task 2 crossed a STOP threshold.** The x scale, judged the way the plan's
revision asked, row by row across the board, has a largest error over the
stop. The y scale and the x against y check pass. The figures are below.

### How it was measured

- **Tests first.** `tests/test_board_frame.py` draws a made-up board four
  times over size, mixed in linear light as a scanner's blur mixes it, with a
  known x and y scale (15.80 and 15.72 pixels per millimetre), a 0.35 degree
  turn and an offset; a notch and a slot in its front edge, four holes, two
  connector rows and a DIP's row across, and two 40-pin footprints down. Run
  before `board_frame.py` existed, it failed on the import. It now recovers
  both scales to 0.005 per cent, every corner to 0.04 mm and every hole
  centre to 0.02 mm (the plan's bounds: 0.05 per cent, 0.1 and 0.05 mm), and
  a test holds that a row held out of the x fit is never in it (a row made
  1 mm long reads exactly 1 mm long).
- **The rows.** Down the board (y): task 0's DIP marks, pin 1 to pin N/2 of
  each row, never the row spacing. Across (x): the rule, written into
  `marks.json` before any row was fitted, was every connector row on the
  0.1 inch pitch that runs across the board with at least eight pads, and the
  rows of the two DIPs that lie across (IC74 and IC75), and nothing else.
  That is PL8 and PL9 (two rows each), PL10, PL11, PL12, PL13 and PL14. A
  search for chains of pads 40 pixels apart also found the rear socket SK6,
  but it is a D socket on 2.77 mm, so the rear sockets are not used. The
  first and last pad of each connector row were marked by hand on crops
  enlarged four times with a 10 pixel grid, recorded in `marks.json` with the
  crop; the pitch count is the pads counted on a 1:2 view and agrees with
  each row's length at 2.54 mm.
- **The scale.** Each end pad is moved to its solder blob's centroid
  (`common.refine_to_pad`, refused beyond 0.6 mm, as in task 0). sx and sy
  are fitted together so every row's length, first pad to last, is its
  pitches x 2.54 mm, by least squares in millimetres; rows across set sx and
  rows down set sy. Held out: y by the halves task 0 fixed, x one row at a
  time. The two differ: holding out one row leaves a connector's other row
  (PL8's rear row when its front row is out) in the fit. Measured after the
  review on 4 October 2026, holding out a whole connector at a time moves the
  median of the rows scored under the revised rule below from 0.136 to
  0.167 mm (largest unchanged, 0.502): small, and still between pass and
  stop.

### The figures

Measured on 4 October 2026 with `BBC_MODEL_INPUTS=~/dbhq-previews/bbc-model-research
PYTHON=/tmp/bbcvenv/bin/python tools/bbc-micro-model/run-board.sh`, which
exits 3 on a stop. It takes about a minute and a half on this host.

**y: passes.** 15.7256 pixels per millimetre. The 40-pin rows held out,
against 48.26 mm: median error 0.030 mm, largest 0.094, over 7 rows (the
same three of ten lose an end pad to a refusal as in task 0). All 19 rows
down held out: median 0.042, largest 0.362 (a 28-pin row).

**x against y: passes.** x is 15.7524 pixels per millimetre, a ratio of
1.0017, 0.17 per cent apart, against a stop at 1.5. Task 0's 1.27 per cent
came from the row spacing; from pitch along the rows the two agree, as the
revision expected.

**x, rows across each held out: STOP.** 11 rows scored (the two rows of IC74
and IC75 nearest the rear each lost an end pad to a refusal). Each error
scaled to a 48.26 mm row: median 0.157 mm, largest 1.192. The plan passes at
a median of 0.10 and a largest of 0.25, and stops at a median over 0.20 or a
largest over 0.50. The median is between pass and stop; the largest is a
stop. Two rows are over 0.50:

| Row | Length | Error | Scaled to 48.26 mm |
|---|---|---|---|
| IC74, the row nearest the front | 17.78 mm | +0.439 mm | +1.192 mm |
| PL11 | 40.64 mm | +0.423 mm | +0.502 mm |
| PL12 | 48.26 mm | -0.307 mm | -0.307 mm |
| PL10 | 22.86 mm | -0.092 mm | -0.194 mm |
| PL8, front row | 40.64 mm | -0.153 mm | -0.182 mm |
| IC75, the row nearest the front | 17.78 mm | -0.058 mm | -0.157 mm |
| PL8, rear row | 40.64 mm | -0.115 mm | -0.136 mm |
| PL9, rear row | 30.48 mm | -0.082 mm | -0.130 mm |
| PL13 | 40.64 mm | +0.083 mm | +0.098 mm |
| PL9, front row | 30.48 mm | +0.025 mm | +0.039 mm |
| PL14 | 22.86 mm | +0.015 mm | +0.033 mm |

Before calling it, I checked for my own mistakes rather than the board's,
on crops with every pad's centroid drawn (`/tmp`, not kept): every pitch
count is right, and every end pad was moved to its own pad, not a
neighbour. What the crops show:

- **IC74:** the solder on both end pads has spread outward, so their
  centroids sit 1 to 3 pixels (up to 0.2 mm) outside the pitch of the six
  pads between them, which are 40.2 pixels apart. On a 17.78 mm row the
  scaling multiplies that by 2.7.
- **PL11:** no pad looks wrong, and the row is long whichever way it is
  measured: 0.42 mm end to end, and a line through all 17 pads gives
  2.590 mm a pitch, 2 per cent long, with the left half of the row short of
  the line and the right half long. [guessing - verify] The pads may not sit
  on an exact 0.1 inch grid on this board's artwork, which was laid out in
  1982; I1 cannot tell that from solder that misleads.
- The rows across disagree with each other more than the rows down do: a
  line through all of each row's pads gives 2.528 mm a pitch on PL10 and
  2.590 on PL11, where task 0's six rows (PL8, PL9, PL13, PL14) spanned
  0.34 per cent.

That check was added on 4 October after task 0's figures were seen, as a
stricter check: nothing else held x to this level, and the plan's revision
said so. It is reported as crossed and not tuned: the rows, the rule that
chose them and the thresholds are as they were set before the fit.

### The turn and the straightness

The frame's turn is 0.2305 degrees, one turn fitted to every long row's pads
at once (30 rows of 10 pads or more, an offset each). The rows across and
the rows down do not agree: the connector rows lie at 0.064 degrees on
average (from -0.138 to 0.241), the DIP rows down at 0.286 (from 0.125 to
0.601), a skew of 0.206 degrees. The board's own edges are square to the
frame within 0.13 degrees (left edge 0.006, rear -0.011, the long front run
-0.063), so the skew is in the pads, not the scan: [inferring] parts placed a
little turned on the artwork, as a footprint at a time would be.

**Straightness: not met, and the plan's words say the journal must say so.**
Each long row's pads against a straight line through them, root mean square:
median 0.043 mm, largest 0.137 (IC51), and 12 of the 30 rows are at 0.05 mm
or more, where the plan wants every row under it. By that test the scan is not
flat. What the residuals look like is pad scatter rather than a bend: the
sagitta of a parabola through each row's residuals runs from -0.25 to +0.10 mm
with both signs among neighbouring rows, where a bent scan would bow its rows
one way; and the worst rows are the ones whose pads the refusal rule thins
(IC1's right row keeps 13 of 20). [inferring] The 0.05 mm limit is below
what centroids of ragged solder give on this scan.

### The outline

The lid is pale and grey and the board is green, yellow or dark, so the board
is the largest region that is not lid, filled. Its edge, turned to the
board's axes, is cut into runs across and down by the edge's direction over
1 mm either side, a line is fitted to each run robustly with 0.8 mm left off
each end (the corners are rounded), and the corners are where neighbouring
lines meet. The edge is put where the light, the median of profiles across
the run, is half way between the board's and the lid's.

The board is 309.99 mm wide and 229.40 mm deep, between the edges' lines at
the middle, against the researcher's estimate of about 309 by 229. The
outline has 20 corners: the plain rear corners, a notch in the front edge
15.3 mm wide and 13.5 mm deep between PL9 and PL10, and three slots about
1.2 mm wide and 4.7 to 5.0 mm deep beside the front connectors. Two things in
the scan to know about:

- A pale band lies along some edges, outside the green. Where the light
  crosses half way is 0.08 to 0.13 mm inside the board's last pixels on the
  rear, left and front edges, but 0.55 mm inside on the right edge, where the
  band is widest. Whether the band is the board's side seen at a slant or
  bare laminate cannot be told from I1, so the right edge, and the width, are
  uncertain by about half a millimetre. The x scale adds more: its standard
  error, a jackknife over the rows across, is 0.197 per cent, about 0.6 mm
  over the board's width, and leaving PL11 out of the fit alone moves the
  width from 309.99 to about 310.42 mm (measured after the review, the same
  day, from `measure_rows` and `fit_scale` on the same marks).
- The scanner's glass edge shows as a dark line beyond the board on the right
  and at the top; the lid between keeps them apart, so they are not board.

Two decisions changed on the way. The first version moved each edge's line
half a pixel outward, the middle between the last board pixel and the first
lid pixel; on the made-up board that put every edge 0.3 pixels out, because
the lid test calls a pixel board at about a third board, so it was replaced by
the half-light crossing, which is right whatever the threshold. And a
crooked bit of the left edge 120 mm down made a corner pair 0.1 mm apart; a
run under 2 mm between two runs on one line within 0.5 mm is now a jog and
joins them, which cannot merge a slot, whose walls are a slot's width apart.
An early look at a coarser mask had shown a fourth slot left of PL12; the
crop shows a long pad there, not a slot.

### The holes

Nine holes, 3.2 to 3.9 mm across: four near the rear and left (3.2 to
3.4 mm) and five larger (3.7 to 3.9 mm), four of them along the front. A
hole shows the lid through the board: pale, grey and smooth. A tinned pad is
pale and grey too, so smoothness decides: the spread of L over the region
less 3 pixels all round is 0.017 to 0.027 on every hole and 0.057 or more on
every tinned pad over 1.8 mm, read off I1's candidates before any hole was
fitted, and the limit is 0.04. On the made-up board the first run called
every pad a hole (125 of them), because drawn pads were smooth; they now
have a grain, as solder has.

A circle is fitted to where colour starts, along rays from the middle. On the
made-up board that is the hole's edge. **On I1 it is not:** the overlay shows
the circle on the edge of the lid seen through the hole, inside a grey crescent
on each hole's rear side (the hole's wall, seen because the scanner looks at a
slight slant) and, on some, a grey ring. So the diameters read small and a
centre may sit towards the front by part of the crescent. It is recorded in
`board_frame.py`'s docstring; the next task that uses a hole's place should
fit the outer edge of the crescent instead.

### Mistakes

- The first `frame.json` listed every pale blob inside the board as a hole
  candidate, over eight thousand; it now lists the 37 big enough to be one,
  each with why it was kept or refused.

### Tests, and where this leaves the plan

`/tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q`: 30
passed. `site/tests/bbc-models.test.mjs` gains three passing tests: the verdicts in
`frame.json` are the ones its figures give; the outline is closed, starts at
the origin, runs across or down, and matches the board's width and depth
within 0.5 mm, which are within 5 mm of 309 by 229; the holes are inside the
board and the rectified copy stays in `out/`. A fourth, that `frame.json`
passes all three scale rows, is written as the plan states it and marked as a
to-do naming this entry, as task 0's was, so the suite stays green and
reports it. `cd site && npm test`: 282 passed before this task, 285 after,
one to-do, none failed; both workflows' floors were raised to 285 (and to 286 after the revision below, with one to-do).

Task 2 ends BLOCKED on the x row, with the figures above, and the plan is
reconsidered before task 3.

## Task 2's x check revised, after its figures were seen

The controller ruled on the stop the same day. The stop was real, and it stays
recorded: `frame.json` keeps the verdict the x check got as first written (a
STOP, largest 1.192 mm) beside a `revision` note, as `spike.json` does for the
front edge.

What changed is the plan's x row, and the order matters. It was added after
task 0's figures were seen, as a stricter check than anything that held x
before; it was then **revised after task 2's figures were seen, by the
controller**, so this is a revision made knowing the result, not a rule set
in advance. The reason: scaling a row's length error up to a 48.26 mm row
multiplies the noise in its end pads' centres (solder spread) by up to 2.7
for a 17.78 mm row, so it is a poor measure of the scale on short rows; the
scale's other evidence is strong (x and y 0.17 per cent apart, the board's
size within the sanity bound). It is not made to pass: the median is judged
against the same numbers as before, and the revised check could still have
stopped.

The revised row (the plan's Global Constraints table and Task 2): a row
across is scored only if it is at least 30 mm long, a rule on length alone,
never on a residual; shorter rows are reported, not scored; the median of the
scored rows' scaled errors passes at 0.10 mm or less and stops over 0.20, and
between is recorded as between pass and stop, as the solder side; the largest
is recorded, with its row named, not judged. Applied to the same 11 rows, with
no mark added or removed and no row picked again.

Measured on 4 October 2026 with the same command as above (now exiting 0):
7 rows scored (PL8 and PL9, both rows each, PL11, PL12 and PL13; PL10, PL14
and the two DIP rows are under 30 mm). **Median 0.136 mm: between pass and
stop.** The largest is 0.502 mm, PL11, recorded. The y scale and the x
against y check are unchanged and pass. No judged figure is a stop.

**The revision had two parts, and the second is what ended the stop.** With
the largest still judged, the 30 mm rule alone would still stop: PL11, a
40.64 mm row, reads 0.502 mm against the 0.50 stop, and its scale factor is
only 1.19, so the short-row reason does not cover it. The largest was made
recorded-only too, as for the solder side, because one row whose pitch
disagrees with 2.54 mm (a line through all its pads gives 2.590) is not
evidence about the scanner's scale: dropping PL11 moves sx by 0.14 per cent
and leaves x and y 0.03 per cent apart. The cut did not pick the verdict:
the median is 0.157 with no cut, 0.136 at 20 mm and at 30 mm, 0.182 at
40 mm, and 0.110 without PL11: always between pass and stop. These are the
reviewer's figures, checked the same day with `measure_rows`, `fit_scale`
and `held_out_x` from `board_frame.py` on the same marks, which gave the same
numbers (sx without PL11 15.7305 against 15.7524).

Three more things from the same ruling:

- **Straightness is recorded, not a pass criterion**, and the plan now says
  so. The figures stand as above: 12 of the 30 long rows at or over 0.05 mm,
  their bows both ways. `frame.json` names the rows over it.
- **Holes.** Fitting each hole's outer edge, the rim outside the grey
  crescent, was tried on radial profiles of three holes and is not cheap:
  colour rises gradually through the crescent, the ring some holes have and
  the lacquer, with no single crossing to fit. So `frame.json`'s holes are
  unchanged, with their bias described, and the plan's Task 3 now says it fits
  holes and drills on the outer edge and records the difference from task 2's
  centres.
- The summary at the top of this entry, which the home page shows, described
  task 0 only. It was rewritten to cover tasks 0 to 2.

Tests: the site test that bound the check as first written is now the plan's
row as revised (nothing judged is a stop, y passes, x's median is judged, and
the verdict as first written is recorded); a strict pass of x's median stays a
to-do naming this entry, since it is between pass and stop.

## Task 3: the solder side registered, and the pads, drills and footprints

`tools/bbc-micro-model/board_register.py` registers the solder side (I2,
flipped left to right) to the board frame of task 2 on every hole it finds on
both faces, judges the fit on holes held out of it, and then finds the
board's pads, drills and footprints. It writes `data/registration.json`;
`run-board.sh` now runs it after `board_frame.py`.

### How it was measured

- **Tests first.** `tests/test_board_register.py` draws one set of holes
  twice: as the component side is scanned, and as the solder side is, turned
  over (mirrored), turned 0.7 degrees, scaled 0.3 per cent larger, warped by a
  cubic of up to about 0.3 mm, with 2 per cent of the holes missing. Run before
  the module existed, it failed on the import. It now recovers every hole to
  under 0.05 mm held out, with the cubic chosen and the affine worse. A second
  drawing has DIPs of 14 to 40 pins, pin 1 marked by a square pad or by the
  print's chamfer, two narrow DIPs side by side where only the print says
  which rows pair, a connector row, a DIP with a pad not drawn and one whose
  end pin is under a long blob of solder; each DIP comes back with its pin
  count and pin 1. Tests added later in the task were each seen to fail first
  (the two last DIP cases by switching off the code they test).
- **Holes, two kinds.** A tinned pad: most of this board's holes are full of
  solder, and the hole is the solder blob's centre, found by task 0's finder
  unchanged (a test holds that). An open hole, a via or a pad the solder did
  not fill: a light copper ring round a dark middle, lighter than the middle in
  each of eight sectors, centred on the centroid of the dark middle, which a
  track joining the ring cannot pull. Read off I1 and I2 before any fit: the
  ring L 0.7 to 0.95 out to about 0.5 mm, the middle 0.2 to 0.35.
- **The registration, as task 0's spike.** Four holes marked on both faces seed
  an affine; holes pair when each is the other's nearest within 1.0 mm; three
  rounds of refit and rematch. Then an affine and a cubic (a homography and a
  cubic correction), each scored on a chequerboard of 20 mm blocks of the
  board frame, each colour held out of the fit to the other. Written into the
  code before the run: the model is the lower held-out median, and within
  0.005 mm of each other the affine, the simpler, wins.
- **The outlier rule, as the plan fixed it.** A matched hole is left out of the
  scoring only if its blob on either face has an axis ratio from second
  moments over 1.25, or an area outside 0.6 to 1.6 times the median of the
  matched holes' blobs on that face. An open hole's blob is its ring and the
  middle it encloses. It reads no residual. `registration.json` keeps every
  matched hole's two blobs and held-out error, so the site test applies the
  rule itself and recomputes both sets of figures.

### The figures

Measured on 4 October 2026 with `cd tools/bbc-micro-model &&
BBC_MODEL_INPUTS=~/dbhq-previews/bbc-model-research /tmp/bbcvenv/bin/python
board_register.py` (the same as `run-board.sh`'s last step), which exits 3
on a stop. It took about nine minutes on this host, most of it waiting on a
load average over 40.

**The solder side: passes.** 2367 holes matched. Held out:

| Fit | Median | 90th percentile | Largest |
|---|---|---|---|
| Affine | 0.125 mm | 0.283 mm | 0.889 mm |
| Cubic (chosen) | 0.114 mm | 0.258 mm | 0.766 mm |

The cubic is chosen: its median is 0.011 mm lower, over the 0.005 tie. The
plan passes at a median of 0.15 and a 90th percentile of 0.30, and stops over
0.25 or 0.50; the largest is recorded, not judged. 9 holes are over 0.6 mm,
scattered (the largest at board x 77.8, y 11.0 mm, near the rear). By kind:
the 2092 pairs of solder blobs are task 0's set again, median 0.121, 90th
percentile 0.273, largest 0.766; the 271 pairs of open rings are much
tighter, median 0.075, 90th percentile 0.143, largest 0.218, because a ring's
dark middle is the hole itself where a blob's centroid is wherever the solder
went. 4 pairs are a ring on one face and solder on the other.

**With the outlier rule: 1087 of the 2367 excluded**, median 0.098, 90th
percentile 0.205, largest 0.746. That is far more than task 0's 9 ragged pads
suggested, and it is the rule doing what it says: 808 fail on axis ratio
alone, 95 on area alone and 184 on both, 821 times on the component side and
664 on the solder side. A tinned pad's blob is rarely round to 1.25 on these
scans (the solder is ridged and catches the light in streaks), and an open
ring's blob carries the stubs of the tracks that join it. The rule was fixed
before the run and is applied as written; the verdict is on the figures
without it, which pass.

**The first run, kept.** The first run of the day found 1167 rings on I1 and
945 on I2 and matched 2521 holes: cubic, median 0.116, 90th percentile 0.292,
largest 1.009 (affine 0.126, 0.320 and 0.980), a pass, with 1146 excluded by
the rule (0.100, 0.244 and 1.009). What was wrong with it showed on the
footprint overlay, not in the residuals: IC69's left row had two drills
0.47 mm apart where there is one pin, and IC74's row broke the same way. A
contact sheet of rings by their distance from the nearest solder blob showed
why. Within about 0.6 mm (339 on I1, 136 on I2) a ring was a dark speck in
the middle of a tinned pad, solder round a speck being ring-shaped; between
0.9 and 1.3 mm it was the edge of a pad or a letter of the print (R, 1, 3, 9
and 6 have dark middles); beyond 3 mm every one looked at was a via. On the
lighter half of the ring a via is green (OKLab a -0.068 to -0.007, median
-0.054; b 0.008 to 0.047), a speck in solder is grey (a -0.016 to 0, chroma
about 0.01), and print is yellow (b 0.05 to 0.12). Since then a ring must be
green (a at most -0.02) and not yellow (b at most 0.05), and clear of every
solder blob by 1.3 mm, where a pad (about 0.85 mm in radius) and a ring
(0.5 mm) would overlap. The rules were set from those rings, looked at, and the
registration was run again: 399 rings on I1 and 703 on I2, the figures above.
Both runs pass, and `registration.json` keeps the first run's figures under
`firstRun`, with this reason.

### Solder-filled holes, and the drills

A drill is a pad on each face that pair up through the registration; it sits
at the mean of the two faces' centres. 2797 drills: 2522 have solder in them
on at least one face, so their diameter cannot be seen and is recorded as
not known; 275 are open on both, and their dark middles measure 0.674 mm
across at the median (0.636 to 0.676 from the 5th to the 95th percentile).
Where the two faces put a hole further apart is where one face's solder sits
off the hole: the faces are 0.118 mm apart at the median and 0.278 at the
90th percentile, and 93 drills have them more than 0.4 mm apart, so that each
face's centre is over 0.2 mm from the mean. The site test that every drill
has a pad on both faces within 0.2 mm holds because each drill's pad is
placed at the drill; the faces' disagreement is recorded with each drill as
`spreadMm`, not hidden by it.

3040 pads: 2797 at drills, 22 that pair across the faces but are too ragged on
both to place a drill (kept as pads with no drill, unless one blob is bigger
than a pad, which may be two pads bridged: 25 of those were dropped), 207 seen
on one face only, and 14 a DIP's rows needed (below). A pad's shape is its
solder's, not its copper's: solder hides the copper's outline on both faces.

### Footprints

Pads 2.54 mm apart in a line are chained into rows, across and down. Two rows
that face each other pad for pad at 7.62, 10.16 or 15.24 mm are a DIP. The
first grouping found 80 DIPs, many short or split: one pad not found broke a
row in two. Now a row may skip up to two pads; every run of slots where one
row or the other has a pad is a candidate; a slot empty on both rows ends a
run; and the run is trimmed until both rows have a pad at each end, or one
row has its pad and the scan shows solder where the other's goes (IC52's top
right pin is under a blob 2.75 times as long as it is wide, on both faces).
A missing pin is then completed from a spare blob within 0.6 mm (8 were) or
inferred where its row puts it (6 were, recorded as `inferred`).

Where a row could pair either way (the RAM block's narrow DIPs sit 7.62 mm
apart, row to row), the print decides: a DIP's outline runs just inside both
its rows. Every pairing the first run accepted scored 0.74 or more on the
print, the wrong ones it was the evidence against 0.1 to 0.2; a pairing needs
0.5, set then. A row whose pads sit 0.46 mm off its line (IC1's last pin) is
still one row: the across tolerance went from 0.4 to 0.5 mm after that was
seen.

**Pin 1 from the print.** No pad on this board is square: the squareness of
every pad-sized blob on I1 runs smoothly from 0.75 to 0.93, with no group
apart. Pin 1 is marked by the outline's chamfered corner. The code looks for
print in a box inside each corner. The first rule, a corner with twice the
next corner's print, missed 11 chamfers that were plainly there, because the
narrow DIPs' outlines run about 1.9 mm inside their rows (1.1 mm on IC1) and
cross every corner's box; the rule became a margin (at least 0.15, and 0.10
over the next corner), after a floor of 0.30 was tried and lost the wide DIPs'
clean chamfers (about 0.2 against nothing). Every pin 1 is drawn on the
overlay and was looked at. Three are marked by hand in `marks.json`: IC69,
IC21 and the 28-pin at the left edge whose label task 0 could not read, each
with a chamfer larger than the box.

**References, by hand.** Nothing here reads text, so each reference is a mark
in `marks.json`: a point inside the footprint, the crop the label was read on,
and why. IC1, IC2, IC3, IC4, IC7, IC51, IC69 and IC78 are the footprints task
0 named, their labels checked again on crops; IC5, IC6 and IC52 were read now
(IC52's label sits between its left row and its outline, and the IC88 on its
far side belongs to the next socket); IC21 came with its pin 1. One more mark
says a "DIP" is not one: two columns of resistor and capacitor pads (R85 to
R87, C32 and C33), whose labels scored as an outline.

The result: 92 DIPs (35 of 14 pins, 27 of 16, 11 of 20, 2 of 24, 12 of 28 and
5 of 40), 25 connectors and 58 SIPs. IC1, IC2, IC3, IC69 and IC78 are 40-pin,
IC4 24-pin, and IC5, IC6, IC7, IC51 and IC52 28-pin. Not grouped, and left for
task 5: IC30 (board x about 76 to 84 mm, y 68 to 86), whose two rows were not
paired, and three DIPs at the right of the row below the RAM block (x about
273 to 305 mm, y 141 to 161), each left as rows. Two-pad parts (resistors,
capacitors, diodes) are not grouped at all: their pads are in the pad list.

### The mounting holes, on their top rims

Task 2's circle stops at the lid seen through a hole. Each of the nine is now
fitted on its top rim. The scanned face lies on the glass, so the rim nearest
the glass bounds the pale region on the side away from the crescent, and the
crescent's outer edge continues it on the other side. Along 180 rays from the
middle, the rim is where the light first falls below half way between the
lid and the lacquer round the hole, or the colour rises to the print's (the
first try ran into a print circle below one hole and fitted it; a ray now
stops at the print as well); a circle is fitted robustly. The circles were
looked at on the overlay: they follow the crescents' outer edges. Several
holes also have a wider grey band outside the fitted rim, about 0.5 mm
across, darker than the crescent. [guessing - verify] It is a copper or
bare annulus round the hole on the board's face, not the hole: if it were
the hole, the holes would be 4.5 to 5 mm across. I1 cannot tell, and the
fit stops at the crescent's edge, as the plan asked.

The diameters are now 3.75 to 4.00 mm, where task 2 read 3.20 to 3.86. The
centres move 0.131 mm at the mean and 0.268 at the most (the hole at x 6.05,
y 29.11). The move is mostly to the left (mean -0.103 mm in x) and only a
little to the rear (-0.036 in y): task 2's centres were biased right more
than forwards, and the crescents on the overlay sit on the rear and left
sides of the holes. The front edge's slots are not used here: task 2 found
their walls weakly fitted, and they place the slots only.

### Decisions, and what they were chosen over

- Holes from both solder blobs and open rings, over solder blobs alone (task
  0's set): "every hole found" asked for it, and the rings turned out the most
  precise holes on the board. The solder-only figures are recorded beside
  them.
- The excluded holes are left out of the scoring only, not out of the fit:
  the plan's words are "left out of the scoring".
- Drills at the mean of the two faces, over the component side alone: both
  faces measure the same hole, and neither is better in general.
- `registration.json` one record to a line and rounded to a micron, over the
  usual one number to a line: written the usual way it was 1.5 MB, the largest
  file in the repository; now about half that, 765 KB (`common.write_data`
  gained the option, with a test). Still large, and kept as one object per
  pad, drill and footprint because the plan's interface names them so.

### Tests

`/tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q`: 48
passed (4 October 2026). `run-board.sh` was run end to end from the inputs
and gave `frame.json` and `registration.json` back byte for byte. Each new
site test was seen to fail on a copy of `registration.json` broken its way (an
excluded hole dropped, the verdict changed, IC5's reference removed, a
drill's pad moved 0.3 mm, a DIP's last pad dropped). The logs of these runs
were kept outside the repository and are not committed, so the claims in this
paragraph rest on the record made at the time, not on a file anyone can open.
`cd site && npm test`:
286 passed before this task, 292 after, one to-do, none failed; both
workflows' floors are raised to 292. `site/tests/bbc-models.test.mjs` gains six
tests: the solder row passes on the figures without exclusion, with the model
chosen by its rule, the verdict the one the figures give, and the figures
recomputed from every matched hole; the excluded holes are exactly those the
roundness rule names, applied in the test to every matched hole's blobs, with
both sets of figures; every drill has a pad on both faces within 0.2 mm;
every DIP has an even pin count from 14 to 40, a pad per pin, pin 1 first and
rows the right length; IC1 to IC7, IC51, IC52, IC69 and IC78 each have a DIP
footprint whose reference was marked by hand with a reason; and the
mounting holes are refitted, with the move from task 2 recorded.

## Task 3's review, and the fix wave (5 and 13 October 2026)

Task 3 was reviewed on 5 October 2026. Pull request 48, which held tasks 0 to
3, was merged on 6 October before the review's fixes landed, so they are on a
second branch, `feat/bbc-micro-models-2` (pull request 88). An implementer
started them and stopped part-way on a usage limit; its work was committed
unverified as `9be4b55` on 13 October. This section records what the review
found, what `9be4b55` did, and what was finished and checked on 13 October.
No written copy of the review survives outside the repository: its findings
are read from the plan's notes dated 5 October, the code comments that cite
it, and `9be4b55`'s message.

### What the review found

- **A drill test that could not fail.** "Every drill has a pad on both faces
  within 0.2 mm" held because each drill's pad is placed at the drill (the
  task 3 section above says so). The plan's step 4 was revised: each drill
  records both faces' centres and sits at their mean; an open drill's faces
  must each be within 0.2 mm of it; for a drill filled with solder, at most 5
  per cent may have a face further off, because the solder sits off the hole,
  not the registration (93 of 2522, 3.7 per cent, was measured before the
  ceiling was set).
- **The fitted map was not saved.** Task 4 has to draw I2's copper through
  the registration, and `registration.json` kept only its figures. It now
  keeps the model's parameters unrounded, how I2 is flipped, and each drill's
  solder-side pixel.
- **Most chips had no reference.** 12 footprints were named; the plan now
  asks for every DIP to have a reference read from the print, or to say it is
  unread and why, with no reference twice.
- **Footprints were missed or wrong.** IC30 and the three DIPs right of the
  row below the RAM block had been left as rows for task 5; IC48's 14 pins
  and the pads of the parts below it made one 24-pin run; and the 18-pad
  group task 3 marked "not a DIP" (two columns of resistor and capacitor
  pads) is IC35, a 14-pin DIP whose outline and label are plain on the scan,
  with one pad of the parts above and below it at each end of its run.
- **A test that read a variable name.** The check that the outlier rule never
  looks at a residual asserted that no variable of the function was called
  `residual`. It is replaced by a test that moves every solder-side hole,
  changing every residual, and requires the same holes to be excluded.
- **The solder side's figure to cite.** The plan's Global Constraints now say
  that from task 3 on the figure cited is the one without exclusion. The rule
  fixed before task 3 sits at about the 70th to 80th percentile of the blobs'
  axis ratios, and its one area median per face mixes solder blobs with open
  rings, so it mostly excludes the rings, the most precise holes.
- **The manual's positions.** Task 5's note: the Service Manual's places for
  the 40-pin parts are a constant offset of about (+7.5, -26) mm from their
  footprints' centres, so task 5 settles the manual's convention on a smaller
  part before it uses the 10 mm check.

### What `9be4b55` did

The drill report (`drillFaces`) and each drill's two faces; the saved map
with a test that puts a drill's I2 pixel through it; references for every
DIP marked by hand in `marks.json` (its footprint marks went from 14 to 107,
each with its crop and why), with `unread` and a pin count a mark can cut a
run to; a second grouping pass in which spare blobs of solder join the pads
still free; a run whose outline scores under 0.7 tried trimmed to the
stretch its outline covers; a mark for a DIP whose printed outline is too faint on one side
(IC61); the behavioural outlier test; a test of the tie between the models;
the plan's notes. It did not run the tools on the scans, change the site
tests or write the journal.

### What was finished on 13 October

**The inputs.** This host had none of them. I1 and I2 were fetched again by
hand from the Google Drive links in `data/sources.json`, as the tool's README
says, into a folder outside the repository; `verify.py` found both SHA-256 as
recorded. Nothing from them is committed. The virtual environment, made on 13
October 2026 on an aarch64 host with `python3 -m venv ~/venv-models &&
~/venv-models/bin/pip install numpy opencv-python-headless pillow scipy
pytest`: Python 3.12.3, numpy 2.5.4, opencv-python-headless 5.0.0.93, Pillow
12.3.0, scipy 1.18.1, pytest 9.1.1.

**The run.** `BBC_MODEL_INPUTS=~/bbc-model-inputs
PYTHON=~/venv-models/bin/python tools/bbc-micro-model/run-board.sh`, on 13
October 2026, took 1 minute 10 seconds. `frame.json` came back byte for byte (the run's log is not committed).
The solder side printed the same figures as on 4 October (cubic, 2367 holes,
held out median 0.114, 90th percentile 0.258, largest 0.766 mm: pass), and
every rounded number in `registration.json` outside the pads and footprints
is unchanged; the unrounded parameters differ from `9be4b55`'s, which came
from another host, by at most 1.4 parts in a million. It printed `drills
2797, pads 3050, footprints 175 (97 DIPs)`.

- **The drills' faces.** Open (275): largest 0.103 mm from the drill, none
  over 0.2. Filled with solder (2522): median 0.062, 90th percentile 0.145,
  largest 0.499 mm, and 93 over 0.2 mm, 3.7 per cent, under the 5 per cent
  ceiling.
- **Footprints.** 97 DIPs (38 of 14 pins, 29 of 16, 12 of 20, 1 of 24, 12 of
  28 and 5 of 40), 21 connectors and 57 SIPs, where task 3 had 92, 25 and 58.
  The new DIPs are IC30, IC12, IC53 and IC61 (the last three right of the RAM
  block, where task 3 found one row of each as a connector), IC48 at 14 pins
  in place of the 24-pin run, and IC35. Pin 1 is from the print on 92 and
  marked by hand on 5 (IC21, IC35, IC61, IC69 and IC89). 17 pads came from a
  spare blob and 7 were inferred, where task 3 had 8 and 6.
- **References.** All 97 DIPs have a reference read from the print and none
  is unread; no reference is used twice; 10 connectors and resistor packs are
  named too (PL8 to PL14, SK6, RP1 and RP2). The 28-pin footprint at the left
  edge, whose label task 0 could not read, is IC89: its digit was compared at
  six times with IC39's 3. 12 of the new marks (IC8, IC10, IC23, IC39, IC43,
  IC45, IC61, IC89, IC94, IC100, IC101 and IC35) were looked at again on
  their crops on 13 October, and each label reads as marked.

**Two changes that changed nothing, removed.** `9be4b55` also added a step
that retried a candidate running into a DIP already taken, with the taken
slots dropped, and a rule that a blob seen only on the solder side never sets
where a row ends. No test reached either. Run on the scans with each switched
off in turn, the DIPs came out the same, so both were removed, and the run
gave `registration.json` back byte for byte. To reproduce it: copy
`tools/bbc-micro-model/` to a scratch folder, replace its `board_register.py`
with the one from before the change (`git show
793731e^:tools/bbc-micro-model/board_register.py`), run that copy's
`board_register.py` with `BBC_MODEL_INPUTS` set, and `cmp` the copy's
`data/registration.json` with the one at HEAD. A reviewer did this on 13
October 2026 and got identical files in 56 seconds; it was run again the same
day and gave identical files in 1 minute 20 seconds.

**A test that did not test its case.** The test for a stray blob beside a row
(IC68) still passed with the pads-only first pass removed. On the scans, with
that pass removed, IC68 is lost (96 DIPs). Its pads show why: pin 11 of the
right row has no pad, a spare blob sits 0.39 mm short of its place and pin
12's pad 0.16 mm short, so chained with the pads the blob is pin 12's next
link and has no next of its own (pin 10 is 0.36 mm off the pitch, over the
0.35 tolerance), and the row breaks in two. The test now draws that layout,
and fails with the first pass removed.

### Tests

`~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q`: 58
passed (13 October 2026). Each test added in the fix wave was seen to fail
with the code broken the way it guards against, one break at a time, with the
file restored after each (the log of that run is not committed): the outlier rule also taking holes with a residual
over 0.1 mm; no tie margin between the models; the drill report never
counting a filled, or an open, drill off; no second grouping pass; no
pads-only first pass; no trim to the outline; the faint mark ignored, and a
faint outline accepted with no mark; a marked run cut from its first slot;
`unread` not kept. The saved map's test failed with the parameters rounded to
4 places, as the file was written before: a drill's solder side then lands
0.017 mm off.

**Review minors, 13 October 2026.** The saved map's test now samples every 7th drill, not every 97th. The tie test gained the boundary, 0.115 against 0.120: the code is `c < a - TIE_MM`, a strict comparison, so exactly `TIE_MM` lower is a tie and the affine wins (in floats 0.120 - 0.005 is 0.11499999999999999, so the case is not below it), and 0.1149 gives the cubic. A site test now requires `solder.model`, `solder.params` (unrounded) and `solder.transform`, the names the plan's interface gives. The held-out-by-kind test says in a comment that it catches figures that disagree with their rows, not a wrong `kind`. Each was seen to fail on a break: drill 7 moved 0.01 mm; `TIE_MM` at 0.0049 and at 0.0051; `params` removed, rounded to 4 places, and `transform` removed.

`site/tests/bbc-models.test.mjs` gains two tests and rewrites one. The drill
test now checks each drill against its two faces as the plan revised it (a
face within 0.002 mm of the 0.2 mm limit is counted either way, since the
file is rounded to a micron), and that `drillFaces` says the same; a new test
recomputes the held-out figures by kind of hole from the matched holes; and a
new test requires every DIP to have a reference with its mark, crop and
reason, or an `unread` reason, with no mark unused. Each was seen to fail on
copies of `registration.json` broken its way (an open drill's faces 0.25 mm
off, 60 more filled drills 0.25 mm off, a drill off its faces' mean, the
report's count 10 short, the rings' median 0.01 out, IC40's reference
dropped, and a mark that found nothing), and to pass with IC40 said unread
with a reason. On main's `registration.json` all three fail; on `9be4b55`'s,
which had not been rerun, the references test fails. `cd site && npm test` on
13 October 2026, with the results and machines artefacts of main's Validate
run 38423721729: 476 tests, 475 passed, one to-do, before; 478 tests, 477
passed, the same to-do, after. Both workflows' floors are raised from 475 to
477.

## Re-plan of tasks 4 to 10 (13 October 2026)

Main had moved on since this plan was written. The NES built its two models after it, reusing its design, and they were merged in pull request 76. Tasks 4 to 10 were re-planned to run on main as it stands. The plan has a dated "Re-plan of 13 October 2026" section at its top and a note at the head of each of those tasks. Tasks 0 to 3 are unchanged. This branch merged `origin/main` at `8a30791` first (merge `3dad880`).

### What changed, and why

- **The BBC Micro claims both views and `case` at once.** The plan had task 7 claim `inside` with no `case`, so the inside could go through the page and the browser check before the outside existed. Main now has the NES's rule that a machine claiming a model states `case`, beside the rule that a cased machine claims both views. So no form of an inside-only claim passes. The claim moves to a new task 9b.
- **The scenes land before the claim, as `.mjs`.** `model.test.mjs` fails an unclaimed `.js` in `site/src/models/`. So tasks 7 and 9a build each model's drawing as a scene module with no `mount`, tested in node, and 9b adds the thin entries. Three other ways were rejected: a red commit between tasks, a placeholder outside, and loosening a rule.
- **The NES's tab list, loader and two-view markup are reused unchanged.** Old task 9's step for them is dropped.
- **The access sampler is lifted into a shared `access.mjs`**, so the BBC does not make a third copy of the NES's.
- **The counters follow the NES's variant A.** These are plain `uint` fields incremented in the bus's own decode, because a counter behind a call measured 2.8 per cent slower in Chromium on ARM on 13 October.
- **Task 4 freezes its method before its held-out checks run.** The scripts' SHA-256 and the time go in the journal first. It also says now what a failed nets check means: the map ships as "traced to look at", as the NES's did.
- **Task 6's speed rule is the NES's,** written down before the first launch. Where it runs is Dan's to say: `bench/` never runs in CI, and this box is not to be used for browser figures.

### The inputs for tasks 4 and 5

Fetched by hand on 13 October 2026 into `~/bbc-model-inputs`, outside the repository, each into its own empty folder first:

| Input | What | Check |
|---|---|---|
| I5 | `https://8bs.com/see/BBC_B_iss7.jpg`, 2,162,357 bytes | SHA-256 `7ae4b573...` equals the one recorded on 4 October |
| I6 | `http://chrisacorns.computinghistory.org.uk/Pics/BBCB7LL.jpg`, 530,347 bytes, 2005 by 1295 | SHA-256 `bab4176a...`, new; added to `sources.json` and `models.md` |
| D1 | archive.org `bbc-micro-service-manual`, 47,662,305 bytes, 222 pages with a text layer | SHA-256 `97455080...`, new; its MD5 equals archive.org's (`d64017d9...`). Its text layer shows table 8.1 on PDF p.66 to 67 and "3.11 Dimensions" (Height 73mm, Width 415mm, Depth 345mm) on p.109, the pages the research cited |

Afterwards, `verify.py` reported I1, I2, I5, I6 and D1 present with their hashes matching. O1 to O3 and K1 were reported missing, because task 8 needs them and they were not fetched again. No input is committed.

### A fact sheet that was wrong

`models.md` said Chris's Acorns stated no licence for I6 and O8. Its home page has a section headed "License to reuse": photographs "may be reused under the CC-BY-SA 3.0 license unless otherwise specified", with a byline asked for. An older paragraph on the same page says they "remain my copyright. If you want to use them please ask me first". The research read the photographs' own pages, which say nothing. Both sentences are now quoted in `models.md` and `sources.json`. Neither photograph is to be committed, so the plan does not change, but each will be credited by name with the site's byline.

### Waiting on Dan

- A fork of `dominicbeesley/bbc-keyboard` in `dbhq-uk`. Rule 3 asks for it, and making it is outward-facing. Task 8 needs it.
- A host for task 6's browser speed run.

## Task 4: the copper on both faces, and the print (13 October 2026)

`tools/bbc-micro-model/board_trace.py` traces the copper on both faces of the bare Issue 7 board and its yellow print, from the scans I1 and I2, runs the plan's checks on them, and writes the track map, `site/src/assets/tracks/bbc-micro-board.webp`, with `data/copper.json` beside it. Both faces are resampled onto one grid at 16 pixels per millimetre over the outline's box: the component side through task 2's frame, the solder side, flipped, through task 3's saved cubic map, inverted by a fixed-point iteration (a test puts points through the map and back to within a millionth of a pixel). `data/ic-table.json` holds Acorn's table 8.1 and the GND and +5V pins the nets check needs, each with its data sheet. It was built the NES's way, and with its lessons: the NES's task 4 is the precedent for every step here.

### The ICs, and where their pinouts come from

Table 8.1 ("Integrated circuits", D1, PDF pages 66 and 67) was transcribed by hand from the page images, which were extracted from the PDF with `pypdf` in a scratch environment outside the repository (`python3 -m venv /tmp/pdfvenv && /tmp/pdfvenv/bin/pip install pypdf pypdfium2`). The PDF's own text layer is an OCR with errors ("74800" for 74S00, "7T4S74" for 74S74), so it was not used. The table lists IC1 to IC101 with each one's part, its place in millimetres from the SW corner, and a comment; all 101 are in `ic-table.json` with the page.

The nets check takes an IC only if its GND and +5V pins come from a maker's data sheet that was read. The data sheets were fetched on 13 October 2026 into a scratch folder outside the repository and read as page images: Texas Instruments' for every 74-series part on the board (`https://www.ti.com/lit/ds/symlink/sn74ls00.pdf` and the like, one per part; the S and plain TTL parts are in the LS sheets), its CD4013B and CD4020B sheets, and Rockwell's R6502 (Document 29000D39) and R6522 (Document 29000D47) sheets from `archive.6502.org`. Every pinout is the usual corner one (GND 7 and VCC 14 of 14 pins, 8 and 16 of 16, 10 and 20 of 20); the 6502's VSS is pin 1 (and 21) and VCC pin 8; the 6522's VSS pin 1 and VCC pin 20. That gives 51 ICs. The other 50 are listed with why, before the run: no data sheet could be fetched for the 81LS95 buffers, the 6845, the 6850, the SAA5050, the 76489, the 4816 RAM, the ROMs, the uPD7002, the 8271 or the 68B54; table 8.1's "74LS34" (IC31) is a part no maker's sheet was found for; the ULAs are custom; the LM324, LM319, LM555 and LM386 are not logic ICs; the RS423 and Econet line parts may run from -5V; IC90 and IC92 are not fitted and task 3 found no footprint for IC92. Every one of the 51 has a footprint from task 3 with the package's pin count and pin 1.

### How the copper is traced

The NES's method (`tools/nes-model/board_trace.py`) is the starting point: OKLab, a smooth level fitted to the laminate, Otsu's threshold with its hysteresis, pads kept, specks dropped, and copper under the print recovered only in line. Three things are different here, each set from what these scans show, measured on probes whose boxes and figures are in the script's comments.

- **The cue is greenness, not lightness.** Copper under the lacquer is a strong green on both faces (OKLab a about -0.10; thin tracks peak at -0.08 to -0.12) and the laminate is nearly black and grey-green (a about -0.017). On the solder side every pad sits in an olive halo where the lacquer stops short of it, as light as copper (L about 0.40, against 0.43 to 0.66 for copper), and along a DIP's rows the halos touch. A lightness cue would join every pin of a row through them, which is how the NES's rims joined its pins. In -a the halo (0.020) sits with the laminate. Chosen over lightness, and over the NES's rim rule.
- **Pads come from task 3, not from the light round them.** Every pad task 3 found is a disc of its solder's radius and 0.15 mm more, which takes in the dark rim round the solder (the pads' radial profile on both faces, 400 pads a face: solder to 0.75 mm, the rim darkest at 0.85 to 0.94 mm). A track joins a pad where its green reaches the disc. The NES's rim rule cut its pads off their tracks; there is no rim rule here.
- **Tin is copper.** Solder (grey, chroma under 0.035, L 0.60 or more) counts as copper wherever it is: every pad, and solder run over the copper on the solder side.

No track on either scan was seen shown only by its two edges, as the NES's solder side showed its thin tracks: here a track is a bright line, often with a lighter stripe down its middle. The level of the laminate's greenness is the NES's smooth surface; its blocks scatter round it by a robust 0.0052 on both faces. Otsu put the threshold at 0.0271 over the level on the component side and 0.0262 on the solder side.

### The tests, and the NES's failure modes

`tests/test_board_trace.py` draws a made-up board in the scans' own colours (each a probe's median sRGB), with the light falling by 15 per cent across it, noise and a little blur. The plan's review asks that the tests fail on the NES's failure modes, so each is drawn: a track shown as two bright edges with a dim middle (it must come back whole and as one piece); pads with the dark rim and the halo or grey ring round them, joined to tracks through the rim (each must stay joined, and a row of pads in touching halos must stay apart, with a track passing between two of them); a hatched pour (one piece, intersection over union at least 0.85); and a drawn short (caught). There are two nets, GND through a via and +5V, with a GND stub whose edge is 0.3 mm from the +5V track. Run before the module existed, the file failed on the import. The first implementation failed one test, which found a real fault: where solder runs over a track, the one pixel where they meet is a mix of the two colours, neither grey enough for tin nor green enough for copper, and it cut the track from the solder. That pixel is now copper when it has tin and green on either side.

Then each of the 19 tests was shown to fail with the code broken the way it guards against, one break at a time, the file restored after each (a scratch script outside the repository; its log is not committed): the threshold tripled; tin taken down to L 0.35, so the halo became tin; the print's threshold raised past the print; recovery under the print without the in-line rule; hysteresis without its low threshold; the smoothing at 3 pixels; the pads' discs 0.1 mm short of the solder, and the seam pixel left out; the discs grown 0.6 mm, and lightness as the cue; the drills not joining the faces; `touching` always false; 19 ICs enough, and the I5 stop at 2.5 mm; a drill in copper with no copper round it; the inverse map with one iteration; the map's red and green swapped; a pin off by one, and a footprint of the wrong size accepted; I5's teal rule dropped, and its copper without the threshold; the component side drawn into I5 with x and y swapped. One break survived at first: the I5 registration's test still passed with each pin held out alone instead of each chip. The test now requires every pin of a chip shifted 3 mm to read over 3.5 mm held out, which held out together they do (about 4.1 mm) and held out alone they do not (about 3.0 mm), and it then failed on that break. `~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q`: 77 passed (13 October 2026).

### What the overlays showed, before the method was fixed

Three `--look` runs traced both faces, drew the overlays in `out/` and printed each face's copper share and the map's size at 8, 7 and 6 px/mm; they run no check. Looked at closely: the processor (IC1), the RAM block, the video ULA and the data bus buffers on both faces, and the solder side's upper left.

- On the solder side the tracks come back as single bright lines, joined to their pads; the halos are not copper; the vias' rings are joined to their tracks. The upper left is a copper pour shiny with flux, traced as one piece with its pads standing apart in their dark islands; the streaks of glare in it leave small holes, which do not cut it.
- On the component side the tracks between IC1's rows come back as separate pieces and are recovered under the outline's print where they cross it.
- **A track lost to the specks rule** (component side, grid x 3880, y 1690 to 1780, a track crossing an IC's outline and its "A"): the print cut it into pieces under 1 square mm, which the specks rule dropped before recovery under the print could join them. Recovery now runs before the specks are dropped. Part of that track is still lost where it runs along the outline's chamfer, under more print than the in-line rule reaches.

Coverage is not held out, and it was seen: the component side's copper was 31.9 per cent of the board at the first run and 32.2 at the last, the solder side's 35.1 both times. No change was made to move it. The map at 8 px/mm was 376,968 bytes at the last run, under the 600,000 budget, so 8, the highest offered, is the resolution.

### I5's pins, marked before the check

I5 is stored 2048 by 1536 with EXIF orientation 8; stored that way it lies as I1 does, rear connectors at the top, so its pixels are used as stored. Its chip pins show as bright legs. A leg is a blob of OKLab L over 0.75 and chroma under 0.05, 8 to 150 pixels; its centroid is the mark. Candidates for each chip's corner pins were proposed by a rough fit (an affine from eight chips' centres read by eye on a reduced copy, then a homography refitted on the candidates within 3 pixels), and each was checked by eye on a crop with the pin numbers drawn. Two corner legs were hidden or in shadow, so the pin next to each was taken (IC2's 39, IC5's 16). The packed RAM chips (IC53, IC56, IC68), IC20 (whose candidates were wrong), IC6 (in a socket) and IC78 (an empty socket) were left out. That gives 56 pins on 14 chips spread over the board, in `marks.json` `i5Pins` with each crop. The I5 check had not been run when they were marked. A leg seen from above can sit up to about half a millimetre outside its pad, away from the chip's body, so some of the registration's error will be that.

### The method fixed, and the checks as fixed

The plan's Task 4 note gained a dated block, "Fixed on 13 October 2026", with every check's pass and stop: coverage 10 to 50 per cent each face; drills in copper at least 95 per cent each face (weak by construction, and said so there); the nets at least 20 ICs (51 are used), 90 per cent of their GND pins in one net and of their +5V pins in another, never touching; I5 to I1 median at most 1.0 mm to pass, over 2.0 mm a STOP; the component side against I5 an intersection over union of at least 0.50; the map at 8 px/mm. `board_trace.py --hashes` printed, at 2026-10-13T19:16:43Z, before the nets or the I5 check had ever been run:

```
f7ff0fbdee5143e29f4dba34ef8d41125869181cf6af3c040dc0534922701e85 tools/bbc-micro-model/board_trace.py
d9d9e1bcea7e643c8a9c7b11a3035b8bd8f551ebd687eb930eef19b3f1e90dc9 tools/bbc-micro-model/common.py
e2754725ff1b859da595ad43ef33ac3983e956bc07ade8b19e9634089177407f tools/bbc-micro-model/board_frame.py
1c8b2fff24351fd3b521f0f283eeefa29fd86edcfe82930d1350d2a20bf01872 tools/bbc-micro-model/data/frame.json
4327f2c19b0d21e49ccac75989172e2618a3a58b54a3c942626ef1f76ca2d5fb tools/bbc-micro-model/data/registration.json
6d991b2c828d4dad5b369f3d0941ca305b362c8d63f671376868b452cf2d0361 tools/bbc-micro-model/data/ic-table.json
e8683a74d1bb4d82fb0251b02aac0a517b6df70799159ab698051be5ce2e9fe4 tools/bbc-micro-model/data/marks.json
```

The same hashes are written into `copper.json` (`methodFixed`) by the run, so a reader can see that the run used the fixed method. These files were committed as they are before the run.

### The run, once

Run once, at 2026-10-13T19:17:50Z, on the committed and pushed method (`b0b4644`), from the inputs in `~/bbc-model-inputs`:

```
cd tools/bbc-micro-model && BBC_MODEL_INPUTS=~/bbc-model-inputs nice -n 10 ~/venv-models/bin/python board_trace.py --map-ppm 8
top: Otsu d(-a) 0.0271; level from 2600 of 2736 blocks, robust sd 0.0052; copper 32.2% of the board (green 17.9%, tin 6.6%, pads 12.1%)
bottom: Otsu d(-a) 0.0262; level from 2607 of 2741 blocks, robust sd 0.0052; copper 35.1% of the board (green 21.0%, tin 8.1%, pads 12.3%)
coverage: top 32.2%, bottom 35.1%, print 12.2%: pass
drills in copper: top 100.0%, bottom 100.0% of 2797: pass
nets: 51 ICs; excluded 50; GND 39 of 51 in one net, +5V 30 of 51; touching True: fail
I5 to the board frame: 56 pins on 14 chips, each chip held out: median 0.363, p90 0.842, max 1.036 mm: pass
top against I5: intersection over union 0.411 over 17127 mm2: fail
print 8624 mm2, copper recovered under it 700 mm2; map 8 px/mm, 2480 x 1837, 376968 bytes
```

| Check | Figure | Pass | Verdict |
|---|---|---|---|
| Copper coverage, each face | component side 32.2 per cent, solder side 35.1 | 10 to 50 per cent | pass |
| Drills in copper | 100.0 per cent of 2797 on each face | at least 95 per cent each | pass (weak by construction) |
| Known nets | 51 ICs; GND 39 of 51 pins in the largest net, +5V 30 of 51; a GND pin and a +5V pin in one net | at least 20 ICs; 90 per cent each; never connected | **fail** |
| I5 to I1, each chip held out | 56 pins on 14 chips: median 0.363 mm, 90th percentile 0.842, largest 1.036 | median at most 1.0 mm; STOP over 2.0 | pass |
| The component side against I5 | intersection over union 0.411, over 17,127 square mm where I5 shows the board | at least 0.50 | **fail** |

Both failures stand as measured. Nothing in the method has been changed since.

**The I5 registration's pass is optimistic, and not an independent check** (added after review, 13 October 2026). The pins were chosen in a way that favours a good fit: the candidates came from a homography refitted on candidates already within 3 pixels of it, and the chips whose legs were ambiguous on their crops were left out. Holding each chip out of the final fit does not undo that choice. So the median of 0.363 mm says the marked legs are consistent with one homography; it does not measure how well I5 lies on the board elsewhere. As the plan's note says, the map ships traced to look at, its connections not verified: `NOTICE.md` and the photographs' README say so, and the plan's task 4 note records the outcome, which a site test pins with its figures. The page's sentences about the copper are tasks 7 and 9b's to write, and the plan now tells them to say it, I5's failure with the nets'. A re-trace needs a new check, fixed before it runs, as the NES's #78. The run's log was kept outside the repository and is not committed; the lines above are copied from it.

### Why the nets fail

Looked at after the run, without changing anything. The first look used a scratch script; after review it was committed as `tools/bbc-micro-model/diagnose_nets.py`, which rebuilds the nets check's pieces from the masks the run wrote to `out/` and prints what is below (`cd tools/bbc-micro-model && ~/venv-models/bin/python diagnose_nets.py`, 13 October 2026; it writes nothing and is no part of the method). Two faults.

- **A merge.** One piece of the component side's copper, 264 square mm, holds the GND pins of IC21, IC81 and IC82 and the +5V pins of IC82, IC84 and IC85. It runs along the row of chips from IC76 to IC82 and through the selection links S14, S17 and S18. That is how the largest net comes to hold 39 GND pins and 30 +5V pins at once. Where in the piece the two nets meet was not found: it may be a real merge in the trace, or a link the board makes with copper that the trace cannot tell from a track. Not looked into further, because that would be tuning after the check.
- **Breaks.** In `copper.json`'s `pinNets` the net with the most GND pins and the net with the most +5V pins are the same net (`bottom:1781`, 39 GND pins and 30 +5V pins). Taking copper away can only split a net, so without the merge no net could hold more than 39 of the 51 GND pins, under the 90 per cent; how many it would hold cannot be known from this run. Some power pins sit on their pad alone (under 6 square mm on both faces: IC1's +5V, IC86's +5V, IC70's GND, IC97's GND, and others). The five chips IC46 to IC50 have their GND pins on one piece of 99.6 square mm and four of their +5V pins on one of 162.8, each joined to the rest of the board nowhere (`bottom:635` holds those five GND pins, `bottom:342` those four +5V pins), so each rail is broken somewhere between the row and the rest of the board; where was not found.

This trace is much nearer a netlist than the NES's was (its largest nets held 1 and 2 of 10 pins), but it is not one, and the check says so.

### Why I5 does not agree

Looked at after the run on its overlay (`out/trace-against-i5.png`: red the component side alone, blue I5 alone, white both). Thin tracks agree in place, but nearly every one has a blue edge on the same side: an offset of about a pixel of I5's (0.2 mm), under the registration's 0.36 mm median, and I5's tracks read about a pixel wider than I1's from the photograph's blur. At about 5.4 pixels to the millimetre a 0.3 mm track is under two pixels wide, so a pixel's offset or spread halves its overlap. I5 alone covers 95,940 of its pixels, I1 alone 61,617 and both 110,023. A band of tracks just above IC1 is copper on I1 and is not found on I5; why was not looked into. The comparison was fixed at I5's resolution before the run, and it fails there; it is not run again at another.

### What the print hid

The print covers 8,624 square mm of the component side, 12.2 per cent of the board (under the 15 per cent the map's blue may take). Copper was recovered under 700 square mm of it, 8.1 per cent: where tracks cross an IC's outline or a thin stroke of a label. The wide blocks (the outlines' corners and chamfers where strokes meet, the large lettering of "ACORN COMPUTERS Ltd" and "203,000 Issue 7", the logo) hide what is under them, and the map shows them blue only.

### Mistakes

- The first implementation joined solder to green copper nowhere, because the pixel where they meet is neither. The made-up board's solder over a track found it.
- Recovery under the print first ran after the specks rule, which had already dropped a track cut into short pieces by print. Seen on an overlay.
- The I5 registration's test first passed whether each chip or each pin was held out. The break found it.
- A test of mine on the made-up I5 checked that the gaps between a chip's legs were not board, when they are. The test was wrong, not the code.
- I wrote "about 6.6 px/mm" for I5 in a first draft of a comment before measuring; it is about 5.4. Corrected before the method was fixed.
- I saw each face's copper share on every `--look` run, so coverage was seen before the method was fixed (above). Nothing was changed to move it.

### Tests

`~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q`: 77 passed (13 October 2026), 58 before this task.

`site/tests/bbc-models.test.mjs` gains six tests: the map is committed, WebP, its size and SHA-256 those `copper.json` gives, inside 600,000 bytes, at 8 px/mm, covering the outline's box, red and green each 10 to 50 per cent and blue under 15, each channel 255 or 0; the verdicts `copper.json` records are the ones its figures give against the fixed rows, and the shares, nets, pins and I5 marks are its own; coverage, drills and the I5 registration pass, and the nets and the I5 comparison are pinned to their FAILs, with their figures and the plan's outcome words; `ic-table.json` is table 8.1's 101 rows, a dozen parts read again from the page images, each IC of the nets check with its data sheet's address and pins; `copper.json`'s `methodFixed` equals the hashes recorded above; and `NOTICE.md` and the photographs' README name the map, amb5l's scans and that it is traced to look at. Each was seen to fail on a copy broken its way, restored after each: the map's SHA-256 changed, and its resolution said 7; one more GND pin in the largest net, and an I5 mark on a chip with no footprint; the nets verdict set to pass, and the plan's outcome words taken out; IC3's GND pin set to 2, and IC40 read as 74LS00; one hash changed in the journal; "traced to look at" taken out of `NOTICE.md`.

## Task 4's review, and what was changed (13 October 2026)

The review found nothing critical. What it found, and what was done:

- **Nothing in CI held the method to the files.** The reviewer changed `NETS_MIN_SHARE` and `I5_IOU_MIN` in a copy of `board_trace.py` and every site test still passed. A site test now hashes `board_trace.py` and `common.py` on disk against the hashes recorded above, before the run. Only those two are pinned: task 5 changes `ic-table.json` and `registration.json` on purpose. It failed with `NETS_MIN_SHARE` set to 0.75, and with a comment line added to `common.py`, each restored after.
- **A sentence the committed data did not support.** "Why the nets fail" said the largest GND net would hold 39 of 51 pins without the merge. `copper.json`'s `pinNets` shows the nets with the most GND and the most +5V pins are one net, so that cannot be known from this run; the bullet now says what the data shows (no net could hold more than 39). The scratch script behind the 264 and 99.6 square mm figures is committed as `tools/bbc-micro-model/diagnose_nets.py`, and its output on the run's masks is what that section quotes.
- **The I5 registration's pass is flattered by how its pins were picked.** Said now in "The run, once" and in the plan's outcome line.
- **The data sheets had no hashes.** The 26 sheets the pinouts were read from (24 from Texas Instruments, Rockwell's R6502 and R6522) are now inputs in `sources.json`, each with its address, size and SHA-256, under ids `P-...`, and each pinout in `ic-table.json` names its sheet's id. They are the copies fetched on 13 October 2026 for the pinouts, kept outside the repository. "(in the SN74LS00 data sheet)" and the like are as found: Texas Instruments' addresses for the SN74S00, SN74S04, SN74S74 and SN74S139A sheets return files with the same SHA-256 as the SN74LS00, SN74LS04, SN74LS74A and SN74LS139A sheets, and each of those sheets lists the S part on its pinout.
- **The reasons for leaving ICs out.** IC31's now says a data sheet was not found in the search made, which is a gap in the search, not evidence about the part. The ROMs' reasons say no ROM data sheet was read, and that a socket may be set by links for more than one type of ROM, marked as a guess to verify. The review also suggested that the 4816 RAM needs -5V and +12V rails. That was not checked, and the BBC Micro's supply has no +12V, so it is not given as a reason: the 4816's reason stays that no data sheet was read.
- **NOTICE.md** gains a section saying `ic-table.json` holds facts from table 8.1 of Acorn's Service Manual and from the makers' data sheets, and copies nothing. The table's comment column is restated as each IC's `role`, in our words.

**`ic-table.json` changed after the run, as documentation only.** The reasons above, the roles and `pinoutSourceId` were edited after the run. The set of ICs, every pin number and every source address are as the run used them. Its SHA-256 is now different from the `6d991b2c...` recorded before the run, which `copper.json` keeps. To show the edit changes no figure, the method was run again on a scratch copy of `tools/bbc-micro-model/` outside the repository (`BBC_MODEL_INPUTS=~/bbc-model-inputs nice -n 10 ~/venv-models/bin/python board_trace.py --map-ppm 8`, 13 October 2026). It printed the same lines, and the map came out byte for byte the same (`cmp`). Its `copper.json` differs from the committed one only in `ic-table.json`'s hash under `methodFixed` and in the wording of 22 of the 50 reasons under `nets.excluded`. The committed `copper.json` is left as the run wrote it, with the reasons as they were worded then.

## Task 5: the parts, places and identities (13 October 2026)

`tools/bbc-micro-model/board_parts.py` puts every IC on the footprint of its reference that task 3 found, names it from the Service Manual's table 8.1 through `data/ic-table.json`, places the connectors and the large parts from boxes read on I5, infers the two-lead parts from the print, and writes `data/parts.json` and the module the page will read, `site/src/models/bbc-micro-board-parts.mjs`. The NES's task 5 (`tools/nes-model/board_parts.py`) is the precedent; the BBC has no KiCad redrawing to check places against, so the Service Manual's places take that role, as the plan says.

### Decisions

- **An IC's place is its pads' centre, its turn from pin 1 to pin N/2.** Every DIP on the board lies with pin 1 at the rear left and its row running towards the front (90 degrees), except IC74 and IC75, which lie across. A footprint whose pad count is not its part's pin count is reported and not placed.
- **The two 8-pin DIPs.** Task 3 grouped DIPs of 14 to 40 pins, so IC16 (LM555) and IC19 (LM386) had no footprint; each row of IC16 was grouped as a 4-pad SIP. Their pads are named by hand in `marks.json` `parts.eightPin`, each read on a crop of the rectified scan with the pad numbers drawn on it: pin 1 at the printed chamfer, as on every other DIP. IC19's pin 3 pad shows on the crop but task 3 did not find it, so it is put on the pitch between its row's others and listed as inferred.
- **The fitted set** is the Model B the page runs: the 8271 disc interface and the DFS, no Econet, no speech. The Service Manual's section 4 lists, upgrade by upgrade, which ICs each fits (PDF pages 35 to 40), so each IC is fitted or not by the upgrade that fits it, and `fittedWhy` quotes it. That gives 88 fitted ICs; IC89 to IC101 are not. The plan's interface said "what I5 shows fitted, plus the 8271 ... the Econet and speech parts are not fitted". I5's board has Econet fitted and the disc interface empty, so "what I5 shows" and "no Econet" disagree; the more specific words, and the machine the page emulates, win. Where the plan feared it would have to guess (the empty footprints in the 8271's area taken as fitted), modification D names every part, so nothing is taken as fitted on a guess. The record is a new section of `docs/bbc-micro/facts/models.md`, "The fitted set", one row per IC.
- **Which counter marks which IC** comes from the Service Manual's own test program for SHEILA's select lines (PDF page 58), which names the IC pin each address reaches: `&FE30` is IC76 (ROMSEL), `&FE80` IC78, and so on. The sound chip is IC18. The names are the plan's fixed list.
- **The paged ROMs.** IC52 holds BASIC (table 8.1's role) and IC88 the DFS (modification D). The emulator calls them slots 15 and 14; which physical socket answers which slot number on a real board was not looked into, and `paged` names the ROM, not the slot.
- **Roles.** Table 8.1 gives a role for 17 ICs. Every other IC now has one: from the circuit description where it names the IC by number (for example IC76, "the ROM select latch", and the disc interface's chips, PDF pages 20, 29 and 32), else what the part is by its type ("four 2-input NAND gates"). `roleFrom` says which.
- **Pins.** The 50 ICs without a data sheet now have the pin count of the part's usual DIP. That is not read from a data sheet; `pinsFrom` says so, and the pad-count check compares it with the footprint.
- **Bodies and heights are typical.** No photograph of a populated Issue 7 board from the side was found. Each DIP is drawn at a typical size for its package; each height carries `measured: false`.
- **Connectors and the large parts** are boxes read by hand on I5 drawn onto the board frame through task 4's fit (`marks.json` `parts.outlines`), each with where its label comes from. SK2, the composite video BNC, is on the case and wired to the board (the parts list, PDF page 91), so it is not drawn; SK7 (Econet) is not fitted. Three labels take the socket's number from the B+ section of the manual, and say `[inferring]`.
- **Two-lead parts are inferred from the print.** On the bare scan every resistor and capacitor is printed as a line between its two pads. A pair of pads not in an IC or a named connector, 2.3 to 16 mm apart, with print along at least 60 per cent of the line between them and no other pad on the way, is taken as a part. Looking at the first overlay (`--look`) showed two kinds of false pair, so two rules were added before the method was fixed: two pads in one of task 3's rows are never a part (a header's or a link's pins), and nor is a pair under an IC's body or a connector's box. The capacitors' body was first a 5 mm square; one at the board's right edge (C60) then ran 0.2 mm over the outline, and the body was changed to the flat 5 by 3 mm the dipped capacitors on I5 show. None of this touches a held-out check. The two-lead parts are a picture, not a measurement: the journal's run section says how many.
- **copper.json's I5 fit cannot be reused.** Task 4 wrote the homography's parameters rounded to 4 places, which drops its perspective terms (about 2e-6 a pixel, about 1 mm across I5). `board_parts.py` refits it unrounded from the same marks, which gives task 4's figures. A finding about task 4's data file, not changed here.

### The manual's convention, settled before the check

Task 3's review found that the manual's places for the 40-pin parts sit near the far end of pin 1's row, and that with only 40-pin parts a corner cannot be told from a shifted origin. The plan asks for the convention to be settled on IC51, IC52 and at least one 14 or 16 pin 74-series part before the 10 mm check runs. `board_parts.py --settle` fits a similarity to IC51, IC52 (28 pins, 600 mil), IC20 (74LS139, 16 pins) and IC21 (74LS00, 14 pins) alone, under each of four conventions, and takes the smallest largest residual, a tie within 0.2 mm going to the earlier in the list. The manual measures from the SW corner, the front left, so its Y runs towards the rear and is flipped first. On 13 October 2026 (`cd tools/bbc-micro-model && BBC_MODEL_INPUTS=~/bbc-model-inputs ~/venv-models/bin/python board_parts.py --settle`):

```
centre: largest 1.68 mm, median 1.31, scale 1.0262, turn -4.01 deg; IC51 0.98, IC52 0.47, IC20 1.68, IC21 1.65
pin1: largest 2.56 mm, median 2.16, scale 0.9870, turn -8.16 deg; IC51 1.76, IC52 1.00, IC20 2.55, IC21 2.56
pinHalf: largest 0.25 mm, median 0.15, scale 1.0028, turn 0.41 deg; IC51 0.02, IC52 0.06, IC20 0.25, IC21 0.23
bodyCorner: largest 0.83 mm, median 0.47, scale 1.0020, turn 0.69 deg; IC51 0.12, IC52 0.15, IC20 0.83, IC21 0.79
chosen: pinHalf (on IC51, IC52, IC20, IC21; a tie within 0.2 mm goes to the earlier)
```

The manual gives each IC's place at pin N/2, the far end of pin 1's row, the package's front left pin. The other three need a turn of 4 to 8 degrees or a scale 2.6 per cent off to come near, and still miss by more. A disclosure: while finding which ICs had a footprint, before this, I printed a list of every footprint's centre and pin 1 beside the manual's place. I did not compute any residual from it, and the convention was chosen on the four parts above by the rule above; but the list was seen.

### The identities' rule, and what was seen first

The rule is in `identity` and the plan's task 5 note: letters and digits only; the table's part with its leading letters dropped must appear in the marking, not followed by a digit; a bare 68-family part matches its speed grades; the 74 families match exactly; the table's "previously" part is recorded as that; the ROM sockets are not compared. It has no list of second sources. While I located the connectors on I5, before the rule was written, I saw some markings: IC88 holds a 27C64 EPROM labelled "LFS 1.40", the RAMs are Mostek MK4516s and Hitachi HM4816s, and IC75 reads both DS3691N and AM26LS30PC. The ROM sockets were already to be left out (on the machine the page runs a socket's identity is its ROM, which a photograph of another board cannot show). The rule was not given a second-source list to pass the MK4516s; a disagreement it finds will be recorded as one.

### The method fixed

The plan's task 5 note gained a dated block, "Fixed on 13 October 2026, by task 5's build", with each check's pass and stop. `board_parts.py --hashes` printed, at 2026-10-13T20:17:29Z, before the manual check or the identity check had been run, and before any marking was read for the check:

```
76a3882e894f5ed7548dbbf4215f3678414ce219c11a1bd5ef1fe90b99580fce tools/bbc-micro-model/board_parts.py
d9d9e1bcea7e643c8a9c7b11a3035b8bd8f551ebd687eb930eef19b3f1e90dc9 tools/bbc-micro-model/common.py
f7ff0fbdee5143e29f4dba34ef8d41125869181cf6af3c040dc0534922701e85 tools/bbc-micro-model/board_trace.py
e2754725ff1b859da595ad43ef33ac3983e956bc07ade8b19e9634089177407f tools/bbc-micro-model/board_frame.py
1c8b2fff24351fd3b521f0f283eeefa29fd86edcfe82930d1350d2a20bf01872 tools/bbc-micro-model/data/frame.json
4327f2c19b0d21e49ccac75989172e2618a3a58b54a3c942626ef1f76ca2d5fb tools/bbc-micro-model/data/registration.json
85f63e9f642d5730d0cc71bd64776163b85c7169e895a74a2635bb3d09d31e36 tools/bbc-micro-model/data/ic-table.json
```

`board_parts.py` writes the same hashes into `parts.json` (`methodFixed`) when it runs.

### The tests, first

`tests/test_board_parts.py` was written before the module and failed on its import. It draws footprints as task 3 holds them and the manual's places as a similarity of them with Y flipped: a place is the pads' centre and the turn runs from pin 1 to pin N/2; a footprint one pad short is reported, not placed; an 8-pin DIP's missing pad is put on the pitch; each convention's point; the manual check passes on places within 1.5 mm of noise and fails on two chips swapped 30 mm apart and on two only 12 mm apart, naming them; `--settle` finds each convention it was given; nineteen markings against the rule, the earlier part, the ROM sockets and an unread marking; the row's verdicts; bodies that touch and that overlap; the outline's edge; the two-lead parts (a pair with print, a pair without, a via on the way, a header's pins, each pad once, axial or radial); the print's share along a line; the module's exports. `~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q`: 122 passed (13 October 2026), 77 before. Each of 20 breaks of `board_parts.py` was then made in a scratch copy, one at a time, and at least one of these tests failed on each: the turn to pin N; the pad count not checked; a missing pad copied from a neighbour; the body's corner on the wrong side; the manual's Y not flipped; `--settle` taking the largest; the 74 families' letters dropped; no speed grades; a digit allowed after the part; the earlier part ignored; the ROM sockets compared; no minimum read; touching bodies taken as overlapping; a point on the edge taken as inside; no on-the-way rule; no same-row rule; a pad used twice; every part radial; the print not cleared off the pads; `HEIGHTS_TYPICAL` not exported. The on-the-way break survived at first: the test's via had print to both ends, so the shorter pairs took its pads first. The test now has the via with no print of its own, and it failed on that break.

**A test that read past its section.** Task 4's two tests that pin its method's hashes took every hash line after the heading "The method fixed, and the checks as fixed" to the end of this file, so this section's hashes joined task 4's and both failed. Each now stops at the next heading. Seen to fail still, with one of task 4's recorded hashes changed by one digit (two tests failed), and the file restored.

### The markings read, after the method was fixed

`board_parts.py --crops` drew each placed IC's body on I5 and on I6 (13 sheets each, in `out/`, git-ignored), and every one was read by eye on 13 October 2026 after the commit above; `marks.json` `parts.identities` holds each reading, line by line, with the sheet it was read on. I6 is drawn through a homography on 16 corner pins of four chips marked by hand (`parts.i6Points`); with no point at the right of the board its crops of IC46 to IC50 and of the RAM fall across two chips, and those are recorded unread.

On I5, 83 ICs are populated outside the ROM sockets and 80 were read. IC6 is under a black cover; IC16 and IC19, the 8-pin parts, are too small and blurred to read. The disc interface's 9 sockets, IC98, IC99, IC100 and IC101 are empty. Some of what the crops showed, beyond the part numbers: the processor is Rockwell's R6502AP, the CRTC Hitachi's HD6845SP (marked HD46505SP too), the ULAs Ferranti's (2C199E-7 for the serial ULA), the ROMs Hitachi mask ROMs HN613128PB04 (OS) and PB05 (BASIC), and eight RAMs are Hitachi HM4816AP-11 and eight Mostek MK4516N-11. On I6 the 8271 is fitted (Intel P8271), the video ULA is VTI's VC2069 where I5's cannot be seen, and IC88 holds an Acorn mask ROM (CN61014N 00201666-03), which on that board is presumably its DFS [inferring].

### The run, once

Run once, at 2026-10-13T20:23:10Z, on the method committed and pushed as `6a15e74` (the hashes above, unchanged at the run):

```
cd tools/bbc-micro-model && BBC_MODEL_INPUTS=~/bbc-model-inputs nice -n 10 ~/venv-models/bin/python board_parts.py
pad counts: pass (99 placed; problems: [{'ref': 'IC92', 'problem': 'no footprint: not placed'}])
I5 to the board frame: 56 pins on 14 chips, each chip held out: median 0.363, p90 0.842, max 1.036 mm: pass
identities on I5: 80 read of 83 populated; disagree ['IC5', 'IC31', 'IC61', 'IC62', 'IC63', 'IC64', 'IC65', 'IC66', 'IC67', 'IC68', 'IC77']: fail
manual places (pinHalf): 99 ICs, largest 97.22 mm, median 1.44, scale 0.9984, turn 0.81 deg: fail
largest: IC94 97.22, IC74 18.55, IC75 18.34, IC42 11.87, IC39 10.45
inside: pass (ICs out [], two-lead parts out 0); overhang ['SK1', 'SK3', 'SK4', 'SK5', 'SK6', 'PL8', 'PL9', 'PL10', 'PL11', 'PL12', 'modulator']; apart: pass []
ICs 99 (88 fitted), connectors 13, two-lead parts 258, others 3
```

| Check | Figure | Pass | Verdict |
|---|---|---|---|
| Pad counts | 99 ICs placed, each with its part's pin count; every fitted IC placed | all | pass |
| I5 to I1 (task 4's fit, refitted) | 56 pins on 14 chips: median 0.363 mm, 90th percentile 0.842, largest 1.036 | median at most 1.0 mm; STOP over 2.0 | pass |
| Identities on I5 | 80 read of 83 populated; 11 disagree | none disagrees, three quarters read | **fail** |
| The manual's places, on pin N/2 | 99 ICs: largest 97.22 mm (IC94), median 1.44; five over 10 mm | none over 10 mm | **fail** |
| Inside the outline, apart | no IC body or two-lead part outside; no two IC bodies overlap | | pass |

Both failures stand as measured. Nothing in the method has been changed since. The model's places stay the scan's footprints and its identities table 8.1's, as the plan's note said before the run. `parts.json` has 258 two-lead parts (61 axial, 197 radial), 13 connectors and 3 other parts.

### Why the two checks fail

Looked at after the run, without changing anything.

**The manual's places.** IC94 is 97 mm off because table 8.1 gives it at (45, 93) (PDF page 67, read again on the page image after the run: it does say 45,93), where the board prints IC94 beside the footprint at about (48, 27) in the board frame, which is near Y 193 in the manual's frame; IC95, the same part beside it, is at (20, 193). So the table's 93 is probably a misprint of 193 [inferring]. IC74 and IC75 are the two DIPs that lie across the board; for them pin N/2 is at the far end of a row running to the right, not at the package's front left, so the convention settled on parts lying down the board does not fit them, and they are off by about a 16-pin package's length. IC39 is 10 mm off in X and IC42 10 mm off in Y against their neighbours in the same rows, which the table's own figures show (IC39 at X 197 beside IC40 at 206, where the board has 19 mm between them; IC42 at Y 158 in a row the table gives as 149 and 145). Without IC94, IC74 and IC75 the check would still fail on IC42 and IC39, so the verdict does not rest on any one cause. The places the manual calls approximate are approximate; the median of 1.44 mm says the rest agree.

**The identities.** Four causes, one of them the rule's own. IC5's marking reads "SAA 5050" on one line and "2188" on the next; the rule joins the lines before it looks, so "5050" is followed by a digit and fails. That is a flaw in the rule, found after the run, and it is not changed: the row fails without IC5. IC61 to IC68 are Mostek MK4516N-11s where table 8.1 says 4816 (I5's other eight RAMs are HM4816AP-11s, and I6's sixteen all read HM4816AP-11), a second source the rule has no list for. IC31 reads 74LS74AN on I5 and DM74LS74AN on I6, where table 8.1 says 74LS34; IC31's neighbours IC30 and IC34 are 74LS74s too, so the table's 34 may be a misprint [inferring]. IC77 reads 74LS00N on both boards, where table 8.1 says 74S00; the manual's own modification B lists "1 off 74LS00 IC77" (PDF page 35), so the manual disagrees with itself. None looks like two ICs swapped: where a marking is another IC's part (IC31's 74LS74 is IC30's, IC77's 74LS00 is IC21's), that other IC reads its own part too.

These go in `docs/known-differences.md` when task 10 writes the models' section; until then they are here and in `parts.json`. A re-check needs a new rule, fixed before it runs (kept lines apart, a second-source list typed from the data sheets first).

### Tests, and what lands unclaimed

The module the page will read is `site/src/models/bbc-micro-board-parts.mjs`. It is a `.mjs` that no `MODELS` entry lists and no machine claims, as the re-plan says the scene's data must land until task 9b; `model.test.mjs` fails only an unclaimed `.js`.

`site/tests/bbc-models.test.mjs` gains nine tests: `parts.json`'s `methodFixed` is the hash block above, and `board_parts.py` and `common.py` on disk are those files; every placed IC is at its footprint's pads' centre, turned from pin 1 to pin N/2, with a pad for each pin, recomputed from `registration.json` and the 8-pin marks, and only IC90 and IC92, neither fitted, are not placed; the fitted set, the counters and the paged ROMs in `ic-table.json`, `parts.json` and the module all equal the table in `models.md`; the fitted set is the plan's (IC78 to IC87 and the DFS fitted, IC89, IC98 and IC99 not); every counter is one of `BbcChip`'s names and marks one fitted IC; every IC body and two-lead part is inside the outline, no two IC bodies overlap, and only the edge connectors and the modulator run over it; the manual's places, recomputed in the test with its own similarity, give `parts.json`'s offsets to 0.01 mm and the same five ICs over 10 mm, pinned with the plan's outcome words; the identities equal `marks.json`'s readings and stay the recorded FAIL, the model keeping table 8.1's parts; the I5 registration is task 4's; the module equals `parts.json`'s model, its heights all `measured: false`, and no `bbc-micro*.js` exists. Each was seen to fail with a committed file broken one way at a time and restored after (a scratch script outside the repository; its log is not committed): a comment added to `board_parts.py` (1 test failed); IC3 and IC69 swapped in `parts.json` (3); IC78 made not fitted in `models.md` (1); IC89 made fitted in `ic-table.json` (2); IC2 counted as the ACIA (3); a two-lead part moved off the board (2); IC74 moved onto IC75 (4); IC3's and IC69's manual places swapped (1); IC5 read differently in `marks.json` (1); the identities said to pass (1); I5's median changed (1); the module edited by hand (2); the plan's outcome words taken out (1); IC2 named MC6845 (3); a height said measured (1).

`cd site && npm run build && npm test` on 13 October 2026, with the machines built and `src/data/results.json` from `scripts/make-results.mjs` as before: 496 tests, 495 passed, 0 failed, one to-do (task 2's x median). Both workflows' floors rise by the nine new tests, from 485 to 494.

### Mistakes

- The connectors' boxes were first written with their width and length the wrong way round for a body at rotation 0, so the first `--look` dropped 17 two-lead parts as under a box; seen when the list of dropped pairs was printed, and fixed before the method was fixed (7 are dropped now).
- The identities' rule joins a marking's lines before it looks, so a date code on the next line can follow the part number as a digit (IC5). Found after the run; not changed.
- `copper.json` keeps task 4's I5 fit rounded so that it cannot be reused; the first look at I5 drawn onto the board went through it and was about a millimetre out at the edges, before the fit was refitted from the marks.
- The private-names check in the plan's hygiene list could not be run: its rules file is not on this host.

## Task 5's review, and what was changed (13 October 2026)

The review found nothing critical and found the run's integrity sound. What it found, and what was done:

- **Descriptions that did not say the checks failed.** The tool's README, the data README, `parts.json`, the module's header and `models.md` described the parts as placed and named, and two of them as checked, without saying that both held-out checks failed, that the model keeps table 8.1's identities, or that the fitted set follows the emulated machine and not the board in I5. Each now says so. `parts.json` gained a field, `afterRun`, written by hand: its `about` and every figure the run wrote are unchanged. The module gained six comment lines after its generated header, written by hand and saying so; `board_parts.py` does not write them, so a rerun drops them, and the method's files are not touched. `models.md` gained a paragraph on the two failed checks and a note on what IC31 and IC77 are marked as on both boards (74LS74 and 74LS00, against the table's 74LS34 and 74S00). `NOTICE.md` gained a section on I5 and I6: what was read from each, that no image of either is committed, and the credit to Chris Whytehead.
- **The manual's similarity includes IC94.** The check fits one similarity to all 99 ICs, IC94's 97 mm included, which pulls the fit. Recomputed after the run without IC94, as a second figure and not a verdict (`board_parts.manual_check` on the same points less IC94, 13 October 2026): largest 19.31 mm (IC74), then IC75 19.17, IC42 11.66 and IC39 10.16; median 0.70 mm, scale 0.9994, turn 0.24 degrees. So IC74 and IC75 are off by the convention, not by the outlier, and IC42 and IC39 are still over 10 mm. The verdict, FAIL, does not change, and the recorded figures stay the run's.
- **marks.json was not among the method's hashes.** The readings and the boxes are data the run read, so the record of what it read is its hash as committed in `f865ab6`, the commit of the run's outputs, taken now and labelled as added after the run. The method's seven hashes above are not changed. A site test now checks all seven files on disk against them, and `marks.json` against this line:

```
added after the run: 0cf894ecccf8b24327a37a13bb82419ed3f910340a53cb7972131015a1754904 tools/bbc-micro-model/data/marks.json
```

- **The "not claimed" test** now also requires that no `MODELS` entry and no machine in the registry names `bbc-micro-board-parts`.
- **The summary** now says the markings disagree for four causes, one a flaw in the rule.

`site/tests/bbc-models.test.mjs` gains two tests (the seven method files and `marks.json` on disk against the journal's hashes; every description of the parts says both checks failed, that the identities are table 8.1's and that the fitted set is not the board in I5's, with the `NOTICE.md` section and its credit), and the "not claimed" test is strengthened. Each was seen to fail with one file broken at a time and restored: a space added to `marks.json`, and to `frame.json`; a `MODELS` entry and a registry claim for `bbc-micro-board`; the failures' sentence weakened in the tool README and in the module; IC31's marking note and the credit to Chris Whytehead taken out. Both workflows' floors rise by the two new tests, from 494 to 496.

## Task 6: the machine tells the page its lights, its keys and its chips (13 October 2026)

The machine now reports what the two models need from it: which chips the processor talks to, which paged ROM is in, and the keyboard's three lights; and the page tells a model every key pressed. The browser speed run is owed (below).

### What was built

- **`BbcChip`** (`src/Dbhq.Machines.BbcMicro/BbcChip.cs`): the ten chips in the order task 5's note fixed, `Crtc` to `Sound`, and **`BbcLeds`**, the three lights as flags.
- **`BbcBus.Accesses(chip)`** and **`AccessCounts()`**: nine plain `uint` fields that `ReadSheila` and `WriteSheila` add to inline, one per chip block, and the sound chip's count, `SystemVia.SoundStrobes`, added to where the system VIA strobes a byte into it. `ReadSheila` became a statement switch so each case can count; its values and calls are as before. The counts start at zero at power on; BREAK keeps them.
- **`BbcBus.SerialUlaControl`**, the last byte written to `$FE10-$FE17`, and **`BbcBus.Leds`**, the three lights read from it and from latch IC32.
- **The host** (`src/Dbhq.Machines.BbcMicro.Wasm/Program.cs`): `Leds()`, `AccessCounts()` and `RomSlot()`.
- **The page** (`site/public/bbc-micro.js`): `panel.bbc` gains `tap(name)`, `leds()`, `accessCounts()` and `romSlot()`; the panel sends `bbc:start` after `bbc:ready`, and `bbc:key` with `{ name, action }` for every key the PC keyboard, the on-screen keys, the Break button or `tap` presses. The header comment lists them all.

### Decisions, and what they were chosen over

- **Counted the NES's faster way, as the re-plan says:** a plain field added to in the decode, with no call. Chosen over the NES's first counter (`Note`, a call per access), which cost about three per cent in one region on ARM ([the NES's entry](2026-10-13-the-nes-counters-on-arm.md)). The fields are the bus's own, not an object's as on the NES, so a count is one add with no extra load.
- **The sound chip counted at the strobe, in the system VIA,** chosen over a wrapper on the `SoundWrite` event, which would add a call for every byte, and over a count in `Sn76489.Write`, which the benches call directly to start their tone load, which is no access by the CPU.
- **Every access in a chip's block counts, modelled or not.** The ACIA, the serial ULA and the ADC are not modelled, but they are on the board and the OS reaches them at boot; the inside model marks chips on the board, not chips in the code. A read of a write-only chip counts too: no source read says whether its select is gated by R/W (`bus.md` s1f, and `known-differences.md`).
- **The lights are worked out in the machine** (`BbcBus.Leds`), not in the page, so a C# test can hold them to what the OS does.
- **`bbc:start` is its own event,** sent after `bbc:ready`, as the NES has `nes:start`. The page makes one machine, at Start; a disc or BREAK makes none, so neither sends it.
- **BREAK is a key event too** (`{ name: 'Break', action: 'tap' }`) from the Break button, the Pause key and `tap('Break')`, so the outside model can press its BREAK key. CAPS LOCK from the PC keyboard is a `tap`, because the page sends it as one press. `tap` of a name the machine has no key for throws, rather than doing nothing.

### The motor's light, sourced

The plan pointed at the Advanced User Guide's s.17 for the motor bit. Section 17 is the SHEILA table; the bit is in s.20.9.4: "The cassette motor relay and LED can be turned on by setting this bit to 1, or off by setting it to 0". The guide was read from archive.org's OCR text, `BBC-Micro Advanced-User-Guide_djvu.txt`, fetched on 13 October 2026, 544,083 bytes, SHA-256 `d801fb6e98ce3793615ddab28b4ce278e174a2fde463df95cb26cc7d41bd8ae4`. The Service Manual (D1, read through its text layer with `pypdf` in a scratch environment) agrees: s.3.5, PDF pages 29 to 30, "and so is the motor control (bit7)", and the keyboard connector's pin 13 is "cassette LED" (PDF page 164). The sources agree, so the light is not left dark. `bus.md` gains s.1e, the lights, and s.1f, what the counters count.

### The lock lights' polarity, as the ROM shows it

The OS's routine at `$EEEB` writes latch bit 6 from keyboard status bit 4 and bit 7 from status bit 5, and those bits are 0 while CAPS LOCK and SHIFT LOCK are engaged (the guide's OSBYTE &CA). So a lock light is lit by a low latch bit. The MOS reassembly's notes on port B say the same ("6 Turn on CAPS LOCK LED", "14 Turn off"), and the machine shows it: after boot CAPS LOCK is on, latch bit 6 is 0, and `A` types a capital (`MachineStateTests`). The reassembly's KEYV also settled what the plan marked to verify: SHIFT LOCK disengages CAPS LOCK and shifts every key, so `1` types `!`; the test asserts that and it passes.

### Nothing else changed

- **The native fingerprint.** `dotnet run -c Release --project bench/bbc-micro-speed/native -- --fingerprint`, on `origin/main` (`9c12535`, exported with `git archive`; its BBC Micro code is the branch's merge base's) and on this change: both outputs 54 lines, byte-identical, SHA-256 `2c6c0b01d9d369eba54bb09ad9862dca8219a75425c6fdc08712747ad7be40a8`.
- **The trace test.** `BusTraceTests` runs a session that reaches every chip (boot, `*MOTOR 1` and `0`, a sound, `MODE 1` and `MODE 7`, both lock keys, a BASIC loop on the user VIA, `*CAT` on a blank disc, BREAK) with the CPU on a recorder that passes each access to the bus and hashes its address, value, direction, the cycle count after it and both interrupt lines. The file uses nothing the counters added, so it was copied into the `main` tree and run there first: `5FF95E1A116CE11C after 36536146 accesses`. That string is pinned in the test, and the branch gives it. `MachineStateTests` then counts the same session again by address, from the recorder, and by the system VIA's own strobe event, and every counter equals it.

### The tests, and that each can fail

`MachineStateTests` (16) and `BusTraceTests` (1) were written first. Against stubs that returned nothing, 12 of the 17 failed. The five that passed against stubs were each seen to fail with one thing broken at a time and put back: a peek of `$FE40` counting (`APeekCountsNothing`); the counters not zeroed at power on (the power-on test); CAPS LOCK's polarity inverted (four light tests); the motor read from bit 6 (four light tests); a member added before `Crtc` (nine tests, the names test among them); a stretch made one cycle longer after 30 million cycles, and an absent fast device read as `$FF` instead of `$FE` (the trace test, each time). Also seen failing: the ADC's reads counted on the ACIA's field (two tests) and the sound strobe not counted (three tests).

In the site's tests, `bbc-micro.test.mjs` gains ten: the PC keyboard's down and up events, CAPS LOCK and Pause as taps, the on-screen taps and latches, letting go, `tap(name)` doing what a click does, `tap('Break')`, `leds()`, `accessCounts()` and `romSlot()`, and two on the whole page in a fake browser, as `nes-panel.test.mjs` runs the NES's: Start sends `bbc:ready` then `bbc:start` with `panel.bbc` in place, and the running page's keys send `bbc:key`. All ten failed before the page script changed, and the Start test failed again with the `bbc:start` line taken out. `bbc-models.test.mjs` gains one: the counters' names are `BbcChip`'s members, in order, read from `BbcChip.cs`; it failed with a member renamed.

### The speed: owed

The browser speed rule was written before any launch, at 20:43 UTC, and committed in `0554ff6`: [`bench/bbc-micro-speed/chip-counters/rule.md`](../../bench/bbc-micro-speed/chip-counters/rule.md), the NES's two per cent rule with mode 7 and the dense page as its two cases, and `analyse.py`, which applies it (checked only on made-up launch files: equal builds pass, a five per cent and a two and a half per cent slowdown fail). **No launch has run.** This box is aarch64 and shared, with no Chrome, and the re-plan rules it out; where the run happens is Dan's to say. Until then the run is owed, and the pull request does not merge without it. No speed figure was taken here, and none is claimed.

### The checks before this was pushed

On 13 October 2026, on this box: `dotnet build -c Release`, 0 warnings and 0 errors; `dotnet test -c Release tests/Dbhq.Machines.BbcMicro.Tests`, 997 passed and 0 failed; in `site/`, `node scripts/build-machines.mjs bbc-micro`, then `npm test` (with the results artefact already in `src/data/`), 509 tests: 508 passed, 0 failed, 1 to-do. Both workflows' floors rise by the eleven new site tests, from 496 to 507; the suite passed 508 here, one more than the old floor plus the new tests, which this task did not add. The core (`src/Dbhq.Cpu6502`) is not touched. The browser check was not run here (it needs Chrome); Validate in CI is its record.

## Task 7: the inside's scene, words and data, not on the page (13 October 2026)

The inside model's drawing, its words and its results file are built, and none of it is on the page yet. Under main's rules the BBC Micro cannot claim the inside alone, and a model's `.js` cannot be committed unclaimed (the plan's re-plan), so everything here is a `.mjs` module that no `MODELS` entry lists and no machine claims, tested in Node. Task 9b adds the thin entry, the `MODELS` entry and the claim together.

### What was built

- **`site/src/models/bbc-micro-board-scene.mjs`**, all of the drawing, with no `mount`. `buildBoard(scene, token)` draws the board from its measured outline with its nine holes cut through (the NES's geometry, three groups: top, underside, edges), every fitted IC as a body on its legs or on a socket, the connectors, the 258 two-lead parts as two instanced meshes, and the modulator, crystal and relay. It returns the handles the entry needs: the chips by reference, the meshes a pointer names a chip by, `mark(refs)` for the access tint and `page(ref)` for the paged ROM's frame. Beside it: `stageOptions()` (the start view and the camera's bounds, computed from the parts), `texel` and `shade` (one pixel of the track map turned into a face's colour, shine and relief), `trackMaps` and `trackMaterials` (the browser half of the same, as the NES's), `createLayers` (Show tracks and Show tracks only, and the fade) and `canvasPrinter` (each chip's part printed on its top).
- **`bbc-micro-board-layout.mjs`**: the parts module's exports, `FITTED`, `COUNTED` (the host's order), `PAGED_SLOTS` and `pagedRef(slot)`, `marksFrom(rates)`, `TRACKS`, `chipLegend()`, `LEGEND_WORDS`, `TRACKS_HELP`, `LABEL`, `STATUS`, `pointWords()` and `describe()`.
- **`bbc-micro-board-notes.mjs`**: `made(figures)`, with `copperSentence()` and `partsSentence()`.
- **`tools/bbc-micro-model/results.py`**, the board half, which writes `site/src/data/bbc-micro-board-model.json`, and `tests/test_results.py`. It is the last line of `run-board.sh`.
- **`site/src/models/access.mjs`**: the NES's access sampler, shared (below).
- **Four tokens**: `--model-bbc-pcb`, `--model-bbc-pcb-under`, `--model-bbc-print` and `--model-bbc-socket`. None is a text colour, so the contrast table in `design.test.mjs` does not change.

### Decisions, and what they were chosen over

- **The scene is a function of a scene, a token reader and the layout,** and holds everything that draws, the track map's turning into colour included. Chosen over keeping `trackMaps` and the layers in the entry as the NES does, so that 9b's entry is the stage, this module, `panel.bbc` and the test hooks. The page wiring (the `bbc:start` reset, the quarter-second sampling, the status line, pointing) is left to the entry, because it needs the page; its words are in the layout (`STATUS`, `pointWords`), where a test reads them.
- **The paged ROM is framed, not tinted.** The plan asks for BASIC or the DFS to be marked as paged. A frame of four bars round the chip's top, in `--model-active`, is a shape as well as a colour and cannot be taken for the access tint. Chosen over a second colour token, which would be a colour alone.
- **Only fitted chips are drawn.** An unfitted IC has no body and no socket (the plan's Deferred list has empty sockets as a later idea). Sockets are the ones the parts file has, which are the ones the Service Manual names (`board_parts.py`, `MANUAL_SOCKETS`). A socketed chip has no legs drawn: they are inside the socket.
- **The ribbon connectors, PL8 to PL12, hang under the board,** as their `edge: 'underside'` says; the rear sockets and the headers stand on the component side.
- **A standing capacitor is a flat box,** 5 by 3 mm as task 5 drew its body, not the NES's cylinder.
- **The colours.** `--model-bbc-print` is the print's median colour on the bare scan, the probe task 4 recorded in `board_trace.py` (sRGB 212, 183, 39). The two greens and the socket's near-black are chosen to look like the board, not measured: the scan shows the lacquer over bare laminate nearly black, which would not read as a board. The copper is `--model-copper` and the mark `--model-active`, as the NES's.
- **The results file's sources** are the parts module's (I1, I2, I5 and D1), then I6, whose markings task 5 read and recorded. The 26 data sheets behind the nets check's pinouts are not in it: they gave facts to a check, not anything drawn, and `ic-table.json` lists each. Task 9b credits the sources by this list.

### The shared sampler

`site/src/models/access.mjs` is `accessRates` and `createSampler` from `nes-famicom-access.mjs`, word for word except that each takes the list of counted chips (`counted`) instead of reading the NES's `COUNTED`, and the sampler refuses to be made without one. `nes-famicom-access.mjs` now binds its `COUNTED` to the two and keeps its three exports. Chosen over a third copy, as the re-plan says. The NES's tests in `nes-famicom-access.test.mjs` run unedited and pass.

One of them reads the NES file's source for the expression `(after[i] - before[i]) | 0`. The expression now lives in `access.mjs`; the NES file's header comment says that and quotes it, so that test still passes on the NES file, and it now holds only the comment. What it guarded is held elsewhere: `access.test.mjs` requires the expression in `access.mjs`, and the NES's behavioural tests fail when it is `>>> 0` (three failed with that break, below). This is said here because a test that matches a comment can look like a test bent to pass.

The NES's board bundle changed by the move: 631,639 bytes before and 631,824 after, 162,050 and 162,138 gzipped (`node scripts/build-models.mjs` on the tree before and after this task, 13 October 2026). The NES's case bundle and the KIM-1's are byte for byte the same.

### The words

The caption starts "Model, not a photograph.", as the KIM-1's does and the plan's step asks, and every size, count and part in it is read from the layout. Every sentence about the copper, in the caption, the legend's words, `TRACKS_HELP`, the status lines and the note, says it is traced to look at and not verified; the caption and `TRACKS_HELP` add that the checks that would have shown its connections failed, and the note gives both failures with their figures, the nets' and the comparison with I5, and says the I5 registration's median is optimistic and not an independent check. The note's parts paragraph says the two checks on the parts failed, with the five ICs over 10 mm and the eleven markings that disagree, that the chips are named with table 8.1's parts, and that the fitted set is the emulated machine's, not the board in I5. The legend says what a mark means; that the processor, the RAM and the OS ROM are in use all the time and are never marked; that the paged ROM is framed; that the 6850, the serial ULA and the uPD7002 are counted though the machine does not emulate them (`bus.md` s1f); and that the sound chip is counted through the system VIA.

### What was checked, and how

Nothing was measured, so no threshold was fixed and no held-out check ran. The KIM-1's, the NES's and the BBC Micro's pages and the home page were built before and after this task (`npx astro build`, the tree before taken with `git stash -u`), and are the same byte for byte with their `<style>` elements taken out; the inlined stylesheet differs by the four new tokens' lines only.

### Tests, and that each can fail

`site/tests/access.test.mjs` (four tests, new) and twelve new tests at the end of `bbc-models.test.mjs` were written before the modules and failed on their imports. `tools/bbc-micro-model/tests/test_results.py` (four tests) failed the same way. Each was then seen to fail with one thing broken at a time and restored after, by a scratch script outside the repository whose log is not committed: in `access.mjs`, the difference unsigned (three tests, two of them the NES's), the rates keyed in another order (two), the sampler made with no list (one); in the NES file, its own rounding (one) and its list one place out (one); in the layout, two counters swapped, the paged slots swapped, the legend listing unfitted ICs, the RAM's part misread, the legend's "so none of them is ever marked" taken out, the caption's chip count typed as a number, the caption's opening changed, `TRACKS_HELP` without "traced to look at", and the map's width; in the scene, unfitted ICs drawn, no sockets, the ribbon connectors on top, the holes not cut, the tint on every chip, Show tracks off leaving the parts hidden, and the solder side's copper read from red; a figure in the results file, and I6 missing from it; in the notes, the nets' figures typed, and the I5 comparison's failure dropped (two); and a `mount` added to the scene. One break survived at first: the scene's board drawn in the KIM-1's green still passed, because the test pooled the tokens the board and the track map read and the map reads the BBC's green. The test now holds the board's plain faces, a socket and the tint to their own tokens, and failed on that break. In pytest, counting sockets on unfitted chips survived, because the made-up parts had no unfitted socket; one was added, and the break then failed.

`cd site && npm test` on 13 October 2026, with the machines built and `src/data/results.json` as before: 525 tests, 524 passed, 0 failed, one to-do (task 2's x median). `~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q`: 126 passed, 122 before. Both workflows' floors rise by the sixteen new site tests, from 507 to 523. The browser check was not run here; nothing in it changes until 9b.

### What is left for task 9b

The entry, `site/src/models/bbc-micro-board.js`: `createStage(root, stageOptions())`, `buildBoard` with `canvasPrinter`, `trackMaps` and `trackMaterials` into `createLayers`, the two buttons, a sampler from `access.mjs` with `COUNTED` reset on `bbc:start`, `marksFrom` into `mark()` and the legend rows, `pagedRef(romSlot())` into `page()`, `pointWords` on the status line, and the test hooks. Then the `MODELS` entry with `texture`, `legend: chipLegend`, `legendWords` and `tracksHelp`; the claim with the outside; the references from the results file's sources; the map's budget; the deploy list; and the browser check.

### Mistakes

- The first draft of the note said the I5 comparison "failed too" in a sentence about the copper that did not say it was traced to look at, and the print's limit did the same; the copper sentences test found both.
- The first draft of the legend's words for the serial ULA and the video ULA said the processor "writes to" them; the counters count a read of a write-only chip too (`bus.md` s1f), so every counted chip's words now say "reads or writes".
- The first pytest data held URLs, which `mirrors.test.mjs` would have failed in a `.py` under `tools/`; they were made plain strings before the site's tests were run.

## Task 8: the outside measured: keys, case, profile and ports (13 October 2026)

Task 8 measures the outside model: which K1 keycap is which key, where the keys, the lights, the opening and the badge strip sit in the case, how high the key tops stand, and where the rear ports and the underside connectors are. Four scripts do it (`keys.py`, `case_register.py`, `case_profile.py`, `case_parts.py`, run in that order by `run-case.sh`), and they write `data/keys.json`, `data/case.json` and `site/src/models/bbc-micro-case-parts.mjs`, which no machine claims (that is task 9b). The NES's task 8 is the precedent.

### The inputs

Fetched by hand on 13 October 2026 into `~/bbc-model-inputs`, outside the repository, each into its own empty folder first:

| Input | From | Check |
|---|---|---|
| O1, O2, O3 | Commons, again | Commons' API (`prop=imageinfo`, `iiprop=url\|size\|sha1\|extmetadata`): each file's SHA-1 is Commons', and its SHA-256 is the one recorded on 4 October |
| O4, O5 | Commons: `File:BBC_Micro.jpeg`, `File:BBC_Micro_left.jpeg` | The same API: `LicenseShortName` "Public domain" for both, the author "StuartBrady assumed (based on copyright claims)", each SHA-1 Commons'; SHA-256 new, in `sources.json` and `models.md` |
| O8-I, O8-H | Chris's Acorns, `Pics/BBCB7I.jpg` (underside) and `Pics/BBCB7H.jpg` (rear), 640 px wide | CC BY-SA 3.0 as the site states, as for I6. Not committed; credited. The site is now an archived copy hosted by The Centre for Computing History |
| K1 | The fork, `dbhq-uk/bbc-keyboard`, at `5ec53df5`: `gh api -H "Accept: application/vnd.github.raw" "repos/dbhq-uk/bbc-keyboard/contents/KiCad/bbc-keyboard/bbc-keyboard.kicad_pcb?ref=5ec53df566c77424caa6be827d0831da3916ecfa"` and the LICENSE the same way | The fork's API says it is a fork of `dominicbeesley/bbc-keyboard`, MIT; the file's git blob SHA-1 is the one the API lists, its SHA-256 the one task 0 recorded from the original. Nothing was fetched from the original repository. `sources.json` now names the fork |
| P-TE_1757819 | TE Connectivity's drawing 1757819 (Amplimite plugs, size 1 to 5), the source German Wikipedia's D-Sub article cites for its sizes | Read for one fact: shell size 2, 15 positions, dimension C (the mounting holes' spacing) 1.307 to 1.317 inch, 33.20 to 33.45 mm |

**K2 is dropped.** `https://www.thingiverse.com/thing:4867915` and its `/zip` answered with HTTP 403 and a "Just a moment..." challenge page, and Thingiverse's API answered 403 too; its API needs a token tied to an account. The plan says to drop the cross-check if the download needs a login, and not to work round it, so K2 is not used and nothing waits on it.

### Decisions, and what they were chosen over

- **Task 8's marks are in a file of their own,** `data/case-marks.json`, because `marks.json` is pinned by hash to task 5's run and a site test holds it there. Chosen over changing that pin.
- **Keys by reference, checked by value.** Each K1 footprint is mapped to the machine's key by its reference (`K1_KEYS`), and the value K1 prints on it must be the one the table expects, so a mistyped row fails. K1 has 74 keycaps: the 72 machine keys, BREAK, and a second SHIFT; O1 shows a SHIFT at each end of the bottom letter row, so the plan's `Shift2` exists.
- **No legend in `keys.json`.** The model draws `LEGENDS[name]` from `bbc-keys.js`, so the legends cannot drift from the on-screen keys; a copy here could.
- **O3's points are its legends, found in code,** not marks by hand: the middle of the bounding box of each legend's light pixels, for the 26 letters and the ten function keys. A legend is printed in the middle of its keycap's top. The bounding box, not the centroid: a centroid sits where the ink is, which for L, J or F is off the middle.
- **The keys are placed by triangulation, not by O1's top.** O1's registration on the case top's four corners is exact for the top's plane only; the key tops are not in it. So the keys, the lights and the key tops' height come from one camera fitted to O4 and O5, and K1's frame is put in the case by the turn and shift that take the opening's two front corners, triangulated, onto the same corners read on O1's top. Chosen over reading the keys on O1's top, which the plan's recorded row shows is a few per cent off scale.
- **O4 and O5 have painted backgrounds.** Their backgrounds are painted white, and the paint cuts across the case's front corners at an angle. So no point on the case's outline is used from them: only the legends, the opening's front corners and the lights.
- **One focal length, a principal point each.** Both photographs are crops of the camera's 2048 by 1536 frame (their Exif's size) by amounts no record gives, so each principal point is free. A soft L1 loss of scale 2 px keeps a legend found wrongly from pulling the fit. A radial distortion term was tried in a look run and moved nothing that mattered (the opening's corners by under a millimetre), so it is not in the method.
- **The rear's scale is the 15-way D's posts,** as the plan says: 33.325 mm, the middle of TE's 33.20 to 33.45 mm. The posts stand in the D's mounting holes, which a plug and its socket share. The D's outer width was not used: TE's figure is the plug's, and the socket's D is larger. Heights are read on the rear face's own height (its top at 73 mm, its bottom at the feet's typical 5 mm), because O2 looks down on the rear and a scale taken across would read heights short.

### Task 8's method fixed, and its checks

The plan's task 8 note gained a dated block, "Fixed on 13 October 2026, by task 8's build", with each check: the keys' mapping (a key K1 lacks is a STOP); O1 to K1 on the plan's row (task 0's marks again, not a new check); O3 to K1 on the same row, recorded; the front edge's parallax check **not measurable**, decided from h alone before any corrected width, because no h independent of the 415 mm exists here (K2 unobtainable, O1 without Exif and looking almost straight down with its case's base unseen, O4's and O5's camera another camera, the key fit's own camera ruled out by the plan); the side profile's check point, the key A, left out of the camera fit and triangulated, within 2.0 mm of K1's place on the key plane, else the typical profile; d recorded with its standard error; the case top's plan size recorded; and the rear ports, each scored port within 2.0 mm of its socket's x plus the board's offset, on the D's scale.

Three things were seen before the method was fixed, and are said here:

- **O3 to K1's figure.** While the legend finder was being written, one development run printed O3's held-out figure. That row is recorded, not a stop, and its numbers were the plan's since 4 October; but the method that gives it was chosen after the figure was seen.
- **The opening's two front corners disagree in height by about 5 mm** once the cameras are fitted (look runs), though the case top is level. Each corner moves about 0.2 to 0.5 mm in height for a pixel of its mark, so 5 mm is more than marking noise. Why was not found. It goes into d's standard error, and it means a pass of the check point says the cameras agree where the legends are, not at the case's edges.
- **The case's width on the D's scale.** Reading the marks, the case's ends were about 1786 px apart and the posts about 140 px, so the case reads about 425 mm on that scale against the manual's 415. The scale had been chosen first and was not changed.

The pytest file (`tests/test_case.py`, 13 tests) was written after a first draft of the scripts, not before, which the plan's step asks for; each test was then seen to fail with the scripts broken one way at a time in a scratch copy outside the repository (15 breaks, all caught; the log is not committed): the mapping ignoring BREAK and the second SHIFT; the second SHIFT mapped as SHIFT; a legend by its centroid; glare kept in a legend; the keys registered by an affine; the focal length not fitted; triangulation from its linear start only; triangulation from one view; the D's posts taken as its flange; the rear check always passing; heights on the photograph's scale; the machine's ends swapped; a rim's circle by the mean of its points; the rigid motion with no turn; a module export without its note. Four breaks first survived (glare kept, the linear start, the D's scale, and heights on the photograph's scale), and the tests were strengthened until each failed. A dry run of the whole method, printing nothing, ran end to end before the method was fixed. `~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q`: 139 passed (13 October 2026), 126 before.

`case_parts.py --hashes` printed, at 2026-10-13T22:05:05Z, before the check point or the rear check had ever been run:

```
a8f53d5828cfa89b60aadc5bf3cc62870de54ed5e8c3dd947a8e8d69ff7e2033 tools/bbc-micro-model/keys.py
b30245d1a30c380cd41853390dec5fbbdca2ee59f21627c4b6cf996cbcb8fdb8 tools/bbc-micro-model/case_register.py
96557265aeb0ecc03ae549f8ece2992c26da05125aeb6ec15fca65e836ff13a6 tools/bbc-micro-model/case_profile.py
84456ccfcf968c0886b6715a038cdbbe5c36562d20d7d199c739894f0c71f06f tools/bbc-micro-model/case_parts.py
d9d9e1bcea7e643c8a9c7b11a3035b8bd8f551ebd687eb930eef19b3f1e90dc9 tools/bbc-micro-model/common.py
204afb5b91fbfec4b8fe9a95840a159245f5942ad0aed272419593e17d989ced tools/bbc-micro-model/spike.py
f12fe6159a9872a81cca73b0b4796db9acf515c0d19bf22c093dc32f999e4318 tools/bbc-micro-model/data/case-marks.json
0cf894ecccf8b24327a37a13bb82419ed3f910340a53cb7972131015a1754904 tools/bbc-micro-model/data/marks.json
4012160780e9a94477b184eb10194c150606780055e526575053d2d4543748eb tools/bbc-micro-model/data/parts.json
```

`case.json` and `keys.json` carry the same hashes (`methodFixed`). These files are committed as they are before the run.

### The first run, and two changes after it

Run once, at 2026-10-13T22:08:29Z to 22:08:47Z, on the method committed and pushed as `5e35042` (`BBC_MODEL_INPUTS=~/bbc-model-inputs PYTHON=~/venv-models/bin/python nice -n 10 tools/bbc-micro-model/run-case.sh`). Its lines are below, under "The run", because the second run printed the same lines but one. Two things in its outputs were wrong, and neither is a check:

- **Colours.** The colour classes put 15 black keys with large legends (W, R, A, S, G, K, X, V, B, N, M, 0, @, CAPS LOCK, SHIFT LOCK) in "light" with the five cursor keys and COPY: in the small disc sampled, the white legend covered over half the pixels, so the median was the legend's. Changed to the median of the darker half of the disc by count (the cap; its legend is lighter), with a new pytest that fails on the first run's sampling (a black cap whose legend covers over half the disc). Its line was `colours: red 10, light 20, dark 44`.
- **The underside panel** is typical: a 10 mm margin round its connectors. With the board's offset from O2, PL12's right end lands 0.7 mm inside the case's right end, so the panel ran 9.3 mm past the case. It now stops at a typical 3 mm wall.

Neither change touches a check: the mapping, O1, O3, the check point, d and the rear check are computed by code that did not change. `case_parts.py --hashes` printed, at 2026-10-13T22:11:37Z, before the second run (only `keys.py` and `case_parts.py` changed):

```
cf4da9c9f09781a98c50e1c89d863617a1da9cd3eb0d3f0d72257149d71b4101 tools/bbc-micro-model/keys.py
0a36447dff0f0fe2f4691a004d996cd664b3857a29cf209424040c5d4296fc00 tools/bbc-micro-model/case_parts.py
```

**The second run** (2026-10-13T22:11:57Z to 22:12:15Z, on `ff3497e`) printed the first run's lines but one, `colours: light 6, red 4, dark 64`: now the cursor keys and COPY came out dark and six of the ten red keys light. The clustering of measured colours had disagreed with O1 twice, in two different ways. Rather than change it a third time against what O1 shows, which would be tuning by eye, each key's class is now read by eye on O1 at full scale (`case-marks.json` `keyColours`: the ten function keys red; the four cursor keys and COPY light, a dark brown lighter than the black keys; every other key dark, BREAK included), and the clustered class is kept beside it in `keys.json` (`colourClustered`), with how many agree. The checks are untouched again. Hashes at 2026-10-13T22:13:15Z, before the third run (only these two changed):

```
1c197719235ebde9e52ce611b287a07d904916db4a2caa199117d28b9f0efcbc tools/bbc-micro-model/keys.py
a5ace72698341acbcc0b72ba3305a7df9fe3b3ad963baa4fe830d7b255ddb978 tools/bbc-micro-model/data/case-marks.json
```

### The run

The third run, at 2026-10-13T22:13:26Z to 22:13:44Z, on `cbe712d` (the same command), wrote the committed `keys.json`, `case.json` and module. It printed the first run's lines but the colours line, which each run printed differently (above). Its lines, less `verify.py`'s eight "present, SHA-256 matches":

```
mapping: 74 keycaps for 72 machine keys and Break, Shift2; lacks [], twice [], other []: pass
O1 to K1 (task 0's marks), each key held out: median 0.395, p90 0.632, max 0.989 mm over 50 keys, 0 over 3 mm: pass
O3 to K1 (legends), each key held out: median 0.205, p90 0.329, max 0.588 mm over 36 keys, 0 over 3 mm: pass (recorded, not a stop)
colours clustered: light 6, red 4, dark 64; read by eye: red 10, light 5, dark 59; 63 of 74 agree
case top: O1 on its four corners to 415 by 345 mm; the keys on that plane: scale 1.0387, turn 0.08 deg, residual median 0.50 mm (recorded)
opening: x 17.2 to 398.4, y 173.0 to 331.6 mm
cameras: f 2479.8 px; principal points O4 [891.9 788.2], O5 [921.3 785.3]; legends {'O4': 35, 'O5': 27}; reprojection legends median 0.90 px (max 3.25), marked points median 0.14 px (max 0.53)
check point A: triangulated [109.64 178.13  -0.29], K1 [109.54 177.8 ] at z 0: 0.45 mm (limit 2.0): pass
the top at the opening's front: z [-8.76 -3.61] mm (se [2.07 0.8 ]); d 6.18 mm, se 2.80
openingFrontLeft: [ 23.4  233.6   -8.76] se [1.27 0.87 2.07]
openingFrontRight: [384.68 235.88  -3.61] se [0.71 0.6  0.8 ]
ledCassetteMotor: [ 75.37 225.4   -8.79] se [0.67 0.75 1.28]
ledCapsLock: [ 95.25 226.01  -7.77] se [0.46 0.68 1.08]
ledShiftLock: [114.95 226.23  -7.06] se [0.31 0.63 0.93]
front edge: raw 403.5 mm; h not obtained; d 6.18 mm (se 2.80): not measurable
rear: scale 4.2014 px/mm; board offset x 104.64 mm; 5 of 5 within 2.0 mm, worst 0.78: pass
  SK1 UHF out: case x 381.02, board x 275.60 plus offset, error +0.78 mm
  SK2 video out: case x 352.46, not scored
  SK3 RGB: case x 319.47, board x 215.15 plus offset, error -0.32 mm
  SK4 RS423: case x 298.82, board x 194.00 plus offset, error +0.18 mm
  SK5 cassette: case x 278.59, board x 173.95 plus offset, error +0.00 mm
  SK6 analogue in: case x 208.75, board x 104.55 plus offset, error -0.44 mm
  SK7 econet: case x 136.57, not scored
the case's width on O2's scale: 425.1 mm (recorded)
```

| Check | Figure | Pass | Verdict |
|---|---|---|---|
| The keys' mapping | 74 keycaps: the 72 machine keys, BREAK and a second SHIFT; none lacking, none twice | every key once; a key K1 lacks is a STOP | pass |
| O1 to K1, each key held out (task 0's marks again) | 50 keys: median 0.395 mm, 90th percentile 0.632, largest 0.989 | median at most 1.0, 90th percentile at most 2.0, largest at most 3.0 | pass |
| O3 to K1, each key held out | 36 legends: median 0.205 mm, 90th percentile 0.329, largest 0.588 | as O1, recorded | pass, recorded (its figure was seen in development) |
| The front edge, corrected for parallax | raw 403.5 mm; h not obtained; d 6.18 mm, standard error 2.80 | the corrected width's interval within 1.5 per cent of 415 mm | **not measurable**, decided before any width |
| The side profile's check point | the key A, 0.45 mm from K1's place | within 2.0 mm | pass |
| The rear ports | 5 of 5 within 2.0 mm; the worst 0.78 mm (SK1) | each within 2.0 mm | pass |
| The case top's plan size | the keys read 1.0387 times K1's size on the top's registration | recorded | recorded |

The commands, the order and the times are above; the figures are in `keys.json` and `case.json`, and the plan's task 8 note has the outcome.

### What the figures say, and what they do not

- **The key tops stand about 6 mm above the case top** (d 6.18 mm, standard error 2.80), and the panel round the keys, at the lights, about 7 to 9 mm below the key tops, so about 1 to 3 mm below the case top. The model takes these. The 2.80 is mostly the two front corners' 5 mm disagreement, which was not explained.
- **The check point says the two cameras agree where the legends are.** A lies among them; the opening's corners lie well outside them, which is where the corners disagree. The pass does not cover the case's edges, as the method block said before the run.
- **Three measures say the case is a few per cent wider than 415 mm, in K1's units and the D's.** On O1's top registered to 415 by 345 mm the keys read 3.87 per cent larger than K1; the turn and shift that put K1 in the case would need a scale of 1.0373 to take both triangulated front corners onto O1's; and on O2's D scale the case is 425.1 mm wide, 2.4 per cent over. The first two share K1 and O1, so they are not independent of each other; the third shares neither. So either the case is wider than the Service Manual's 415 mm (the Science Museum's 410 is narrower still), or K1's pitch and the D's spacing both read small, or something common to how the case's corners and ends were marked. Not resolved: the case's size rests on the manual, as the design says, and nothing was rescaled to fit. The rigid placing of K1 means the keys keep K1's size, and the opening's two front corners land about 6.7 mm from their triangulated places on each side: the keys still sit inside the opening.
- **The board in the case.** On O2's scale the board's left edge is 104.64 mm from the case's left end, so its right edge is 0.36 mm inside the case's right end, which leaves no room for the wall. With the case reading 425.1 mm on the same scale, the board's place is good to a few millimetres, not to the wall. The underside connectors take that offset; PL12, the Tube's, ends 0.7 mm inside the case.
- **The rear check passes,** but it judges how well five ports agree with five sockets after one shift, so it tests the D's scale over 170 mm, not the shift.

### Typical, not measured

The feet (5 mm), the case's corner radius (3 mm), the board's front to back place (4.5 mm from the rear face) and height (14 mm), the underside panel (10 mm round its connectors, stopping at a 3 mm wall), the ports' sizes, and the profile's shape (the top level, front and rear upright, at the Service Manual's 73 mm). Each is in `case.json` `typical`, with `measured: false`.

### Tests

pytest: `tests/test_case.py`, 14 tests (13 written after the scripts' first draft, the colour test after the first run); `~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q`: 140 passed (13 October 2026), 126 before task 8.

`site/tests/bbc-models.test.mjs` gains ten tests: the method's hashes as fixed and as changed after each run, against `case.json`, `keys.json` and the files on disk; every machine key one K1 keycap, with BREAK and the second SHIFT, read from the fork; `keys.json` passing O1's row and recording O3's and the raw front edge, with its verdicts its figures'; the front edge not measurable, with h's reason, d and no corrected width, pinned to the plan's outcome words; the check point within 2.0 mm, recomputed, pinned to the plan's words; the case's size, the keys and lights inside the opening and the opening inside the case, no two keycaps overlapping, the lights in O1's order; the keys K1's centres through the recorded turn and shift, their colours as read on O1; the rear ports in O2's order, each scored one within 2.0 mm of its socket plus the median offset, recomputed, pinned to the plan's words; the module generated from `case.json` and unclaimed; and the five photographs committed and credited, O8, I6, K1 and TE's drawing not committed, K1's notice in `NOTICE.md`. Each was seen to fail with a committed file broken one way at a time and restored after (a scratch script outside the repository; its log is not committed): a comment added to `case_parts.py` (1 test failed); the second SHIFT dropped from `keys.json` (2); K1's address set to the original repository (1); O1 said to be between pass and stop (1); the front edge said to pass (1); the check point set 2.5 mm off (1); the plan's rear outcome words changed (1); the cassette motor's light moved in the module (2); two keycaps overlapping (3); a key moved 1 mm in `case.json` (1); a cursor key read as dark (2); a rear port 3 mm off (1); the rear order swapped (1); a note dropped from the module (1); O4 not committed (1); K1's notice taken out of `NOTICE.md` (1). One tolerance was set after the data were seen: the keys' places through the turn and shift agree to 0.03 mm, not 0.01, because `case.json` keeps the turn to four places, which is 0.02 mm at 400 mm; that is rounding, not a check.

Both workflows' floors rise by the ten new site tests, from 523 to 533.

### Mistakes

- I first wrote task 8's marks into `marks.json`, which a site test pins to task 5's run by hash; reverted before anything ran, and the marks moved to `case-marks.json`.
- The camera fit's third axis points down into the key plane. The first look printed heights below the key plane as positive and the camera heights negative; both are now turned to up before they are written.
- The pytest file came after the scripts' first draft, not before, as the plan's step asks.
- A development run printed O3's held-out figure before the method was fixed (said in the method block).
- The colours were sampled two ways that each disagreed with O1, and are now read by eye (above).
- The typical underside panel ran past the case's right end in the first run.

### Left for task 9a

The scene draws the case from `CASE`, `RECESS` and `PROFILE`, the keys from `KEYS` with `LEGENDS[name]`, the lights from `LEDS`, the badge strip's words from `BADGE` and no owl, the rear vents from `GRILLE`, the ports and mains panel from `REAR`, the underside from `UNDERSIDE`, and the feet from `FEET`, each note into its words. The case's size is the manual's; the page says that the outside's measures disagree with it by a few per cent (above), and that the front edge's check was not measurable. K2 is not credited, being unused.
