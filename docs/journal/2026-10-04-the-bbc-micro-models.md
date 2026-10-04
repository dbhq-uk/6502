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
