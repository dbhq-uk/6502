---
title: "The BBC Micro's models: the inputs, and what they rest on"
date: 2026-10-04
summary: "Before any of the BBC Micro's two models is built, a day's spike measures what both rest on: the scale of a flatbed scan of the bare board, how well the solder side lies on the component side, and how well a photograph of the keyboard fits an open keyboard layout. The scale and the keys hold, and the solder side holds except for a few holes. A further check, the case's front edge measured on the plane of the keys, fails, because the edge is not in that plane. The plan was revised the same day so that a later task judges the edge's width after correcting for that."
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
  time.

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
  uncertain by about half a millimetre.
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
passed. `site/tests/bbc-models.test.mjs` gains four tests: the verdicts in
`frame.json` are the ones its figures give; the outline is closed, starts at
the origin, runs across or down, and matches the board's width and depth
within 0.5 mm, which are within 5 mm of 309 by 229; the holes are inside the
board and the rectified copy stays in `out/`. The fifth, that `frame.json`
passes all three scale rows, is written as the plan states it and marked as a
to-do naming this entry, as task 0's was, so the suite stays green and
reports it. `cd site && npm test`: 282 passed before this task, 285 after,
one to-do, none failed; both workflows' floors were raised to 285.

Task 2 ends BLOCKED on the x row, with the figures above, and the plan is
reconsidered before task 3.
