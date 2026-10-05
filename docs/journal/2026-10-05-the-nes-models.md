---
title: "The NES's models: the inputs, and the eight checks they rest on"
date: 2026-10-05
summary: "Before the NES's two models are built, the work measures what they rest on. The bare board scan's scale holds, its solder side lies on its component side, and the European board's chips sit where the American board's do. The case took three tries. Read from the two corner photographs, its depth came out well short of the published figure, and Dan ruled that this measured the camera, not the case. Judged on the design patent's drawings it was still just over the limit, and the European front then stopped against the drawing too. The sources disagree with each other by more than those limits could resolve, so the check was turned back to its purpose, catching a gross error of scale, with wider limits set after the drawings were seen and said so. On those it passes, and every stop stays on record. On the way, the cartridge connector's fingers turned out to be on a metric pitch, not the tenth of an inch the plan assumed."
order: 30
---

# 5 October 2026: the NES's models, the inputs and what they rest on

The NES's two models, the outside and the inside, each for the NTSC NES-001
and the PAL NESE-001, have a design
([`../superpowers/specs/2026-10-05-nes-models-design.md`](../superpowers/specs/2026-10-05-nes-models-design.md)),
a plan
([`../superpowers/plans/2026-10-05-nes-models.md`](../superpowers/plans/2026-10-05-nes-models.md))
and their sources ([`../nes/facts/models.md`](../nes/facts/models.md)). The
design's own entry for today records how it was made. This entry records the
design's decisions in short, then task 0: the inputs checked, and the eight
checks everything rests on, each judged against thresholds the plan set
before any measuring.

**Task 0 crossed STOP thresholds, and the plan's case rows were revised
twice the same day; as revised, nothing stops.** In short: the first run
stopped on the case's depth; the first revision moved the case to the patent
and stopped on its depth and on the PAL front; the second revision, made
after the patent's figures were seen, judges the case within 5 and 8 per cent
of the published ratios and the PAL front on O4 against O2-FL again, and on
those the case passes and the PAL front sits between pass and stop. Every
stop stays recorded. The detail: The case's depth to width, from the two corner photographs with the
camera the plan set, is 8.8 per cent short of the published 203.2 / 254 on
the worse photograph, where the plan stops at 3. Dan ruled the same day that
the check measured the camera, not the case, and the plan was revised to
judge the case on the design patent's orthographic views, with the same
limits ("The case revised, later the same day", below). On the patent the
top view is 3.46 per cent short: still a STOP. The PAL front, judged on O4
against the patent's front view, stops too, on its label band. The plan says
what they stop: the outside, and so every model, because a cased machine
claims both views or none; and the PAL models. The machine still counts. The
first figures are below as they were measured, then the revision.

## The design's decisions

From the decisions table in the design, each with what it was chosen over:

- **Both consoles, NTSC and PAL, inside and out** (Dan), over the NTSC console
  only, which the research recommended for its better inputs. The machine runs
  both regions.
- **One module per view, drawing both consoles, with `regions` in `MODELS`**,
  over four modules with a region on each registry claim. The registry's rules
  stay as written, and a region switch needs no download.
- **LED, POWER and RESET outside; chip marks with live rates inside** (Dan),
  over the BBC Micro's marks alone, or the outside's state only. A game reads
  the PPU and the pads every frame, so a mark alone would stay lit.
- **The NTSC board is an NES-CPU-10 from the bare scans**, identified from the
  NES-CPU-07 photographs, over tracing the NES-CPU-07 from its populated
  photographs, where parts hide the top copper.
- **The PAL board is the NTSC copper with the PAL parts, only if task 0 shows
  one layout**, over tracing PAL copper from a populated photograph, or drawing
  PAL parts on the NTSC board unchecked.
- **No Nintendo logo shapes**; the case's words in the site's face; the
  board's print traced as it is, over drawing the logos.
- **The console as made**, in the site's grey tokens, over yellowed as the
  PAL photographs show it.
- **OpenTendo forked into `dbhq-uk` and pinned**, the track map carrying the
  TAPR OHL's terms, over reading upstream or leaving the map's terms unstated.
- **Inside first**, over outside first: it gives the outside its ports.
- **Reuse the BBC Micro's tabs and loader, and wait for them**, over building
  the NES's own.
- **A feasibility task first, with stop thresholds**, over building the
  pipeline and finding out at the end.

## Task 0: what was built

`tools/nes-model/`, offline Python that never fetches:

- `data/sources.json`: the 13 inputs the research hashed, each with its URL,
  the page for its author and licence, the licence as stated (null for the
  scans, which state none), the fetch date, size, pixels and SHA-256. Nothing
  is committed from any of them. OpenTendo's three come from the `dbhq-uk`
  fork at `3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009`; the fork was made with
  Dan's yes before this task started, and `gh api
  repos/dbhq-uk/OpenTendo/commits/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009
  --jq .sha` printed the full hash.
- `common.py`, `verify.py` and `tests/test_common.py`, from the BBC Micro's at
  commit 16d1fa9 by the plan's commands. The KiCad keyboard reader became
  `kicad_footprints` and `kicad_outline_box`, read as a cross-check only. The
  pad constants were read afresh off these scans (below), and the pad tests
  moved to a made-up board at 300 dpi with this scan's ring-shaped pads.
- `spike.py`, which writes `data/spike.json` and pictures to look at in
  `out/` (git-ignored), and exits 3 on a stop. `data/marks.json` holds every
  point and segment marked by hand, with the crop it was read from.
- `site/tests/nes-models.test.mjs`: the sources are well formed and match
  `models.md`'s table; OpenTendo's come from the fork; no original is
  committed; the build never runs the tools; and the verdicts in `spike.json`
  are what its figures give. `site/tests/nes-spike-verdicts.mjs` holds the
  plan's thresholds and the verdict rules, with their own test.

### The inputs

All 13 matched: `NES_MODEL_INPUTS=/tmp/nes-inputs python3 verify.py`, run in
`tools/nes-model`, printed "present, SHA-256 matches" for each and exited 0.
`/tmp/nes-inputs` holds links to `~/dbhq-previews/nes-model-research/full/`,
where I2 was copied from one folder up as the plan says.

The virtual environment, made on 5 October 2026 with `python3 -m venv
/tmp/nesvenv && /tmp/nesvenv/bin/pip install numpy opencv-python-headless
pillow scipy pytest`: Python 3.12.3, numpy 2.5.3, opencv-python-headless
5.0.0.93, Pillow 12.3.0, scipy 1.18.1, pytest 9.1.1.

### The pads on this scan

The BBC Micro's scans show solder; these show tinned rings round open holes,
on lacquer so dark that colour does not tell it from the pads. Read on 5
October 2026 from the 40 pads of U6 on each face (the ring between 5.5 and 9
pixels from each centre) and from lacquer boxes with no pad or print, as the
1st, 50th and 99th percentiles (the boxes are named in `common.py`): lacquer
light 0.084, 0.171, 0.248 on the front and 0.056, 0.139, 0.197 on the back;
ring light 0.121, 0.517, 0.802 and 0.125, 0.433, 0.889; lacquer colour 0.015 to
0.068 and 0.006 to 0.054; ring colour 0.003 to 0.110 and 0.003 to 0.122. So
`PAD_MIN_L` is 0.34, half way between the lacquer's highest 99th percentile
and the rings' lowest median, and `PAD_MAX_CHROMA` is 0.12, the rings' highest
99th percentile, which refuses only what is clearly coloured. With those, the
U6 pads' blobs are 223 to 378 pixels, so `PAD_AREA_PX` is 120 to 600. The pad
tests' made-up ring is as light as this scan's (OKLab L 0.50), below the BBC
Micro's floor for solder, and was checked to fail with the BBC Micro's
constants.

### How the marks were made

Every mark was read by eye off a crop of the original, enlarged two to eight
times with a grid labelled in the original's pixels, the labels in a strip
outside the crop; the crop is recorded beside each mark. Each set was then
drawn back onto the picture and looked at before any figure was read.

- **Scale, on I1-front:** the first and last pin of both rows of the ten ICs
  and of the expansion header P2, moved to their blobs' centroids by
  `refine_to_pad` (two ends were refused, U4's pin 13 and U10's pin 16, and
  their rows left out); the two end fingers of the edge connector P1, each put
  half way between its edges, since `refine_to_pad` finds round pads only; and
  pin 1 and pin N of each IC on drill centres. Every DIP lies with its notch to
  the left, so pin 1 is at the lower left and pin N straight above it.
- **Drill centres, on the hole's top rim:** the ring's inner edge, where the
  board's face meets the hole, found along 72 rays as the steepest rise in
  light inside the ring's brightest point, with a circle fitted by consensus
  and least squares, three times over. Two simpler versions failed when
  looked at: a half-way level on each ray was pulled inward by holes whose lid
  shows light, and the first consensus fit settled on a small arc round a
  glint. The final version was drawn on all 20 holes and looked at; U7's and
  U10's pin N are the poorest fits (41 and 48 of 72 rays agree), each with a
  glint in the hole.
- **The solder side's seed:** four holes far apart on both faces.
- **The PAL layout, on I3:** the foot of each IC's end pins where it meets the
  board, the solder joint or pad, not the leg's shoulder. U3's pin 16 is under
  a capacitor on I3, so U3 is marked by pins 1, 8 and 9 on both boards.
- **The case:** each of the top's four edges as a segment along it, away from
  the rounded corners; the code fits each edge's line and puts each corner
  where two lines meet.

### The eight checks

Measured on 5 October 2026 with `cd tools/nes-model &&
NES_MODEL_INPUTS=/tmp/nes-inputs /tmp/nesvenv/bin/python spike.py`, which
wrote `data/spike.json`, printed the eight verdicts and exited 3. It took
about five minutes.

**1. Scale x: passes.** Seven rows of at least 30 mm are scored, each held out
of the fit in turn, its error scaled to a 48.26 mm row: median 0.113 mm, the
largest 0.206 mm on the edge fingers, recorded. The plan passes a median of
0.15. x is 11.7871 pixels a millimetre.

**The edge fingers are not on 2.54 mm.** The plan's table counts them among the
rows at 2.54 mm. They are on 2.50: the 35 gaps between the 36 fingers lie
2.499 mm apart through the scale of the other rows, the end fingers are 3.04
and 3.03 mm wide where the others are about 2, and their centres are 88.52 mm
apart, against 88.9 if they were on 2.54. The KiCad redrawing agrees to the
hundredth: P1's pads 2.5 mm apart, its end pads 3 mm wide, 88.5 mm end to end.
The check above is judged as the plan words it, with the fingers at 2.54 mm.
Without them, the six other rows' median is 0.032 mm and x is 11.8004. Either
way it passes. Taking the fingers at 2.54 mm moved the x scale by 0.11 per
cent (11.7871 against 11.8004), so the plan's task 2 now says its frame takes
x from the rows without the fingers, or with their true pitch and widths.

**2. Scale y: between pass and stop.** Ten footprints, each held out in turn,
their row spacing on drill centres: median error 0.566 per cent, largest 1.158
(U10). The plan passes at 0.5 and stops over 1.0. The 600 mil set (four
footprints) has a median of 0.179 per cent, the 300 mil set (six) 0.818. On
7.62 mm at 11.8 pixels a millimetre, one pixel is 1.1 per cent of the spacing,
so the 300 mil rows are at about the limit of what the rims can say; the two
largest errors are U7 and U10, whose pin N rims are the poorest fits. y is
11.8404 pixels a millimetre.

**3. x against y: passes.** 11.7871 against 11.8404, a ratio of 0.9955, 0.45
per cent apart (0.34 without the fingers), against a stop at 1.5 and beside
the scan's stated 300 dpi, 11.8110.

**4. The solder side: passes.** Drill centres on their top rims: 590 found on
I1-front and 478 on I1-back flipped (46 and 123 refused, most on the back
where solder fills the hole). Seeded by the four marked pairs (fitted to 0.15
pixels), 323 holes are each other's nearest within 1.0 mm. On a chequerboard
of 20 mm blocks, each colour held out of the fit to the other:

| Fit | Median | 90th percentile | Largest |
|---|---|---|---|
| Affine (chosen) | 0.074 mm | 0.239 mm | 0.921 mm |
| Cubic | 0.077 mm | 0.236 mm | 0.953 mm |

The affine is chosen: its median is lower, by more than the 0.005 mm the code
calls a tie, and the simpler wins a tie anyway. The plan passes a median of
0.20 and a 90th percentile of 0.40, over at least 150 holes. With the plan's
outlier rule, 107 holes are left out of the scoring: median 0.067, 90th
percentile 0.146, largest 0.430. The verdict is on the figures without it.

**5. The PAL layout: passes.** Ten ICs, each held out of a homography from I3
to I1-front fitted on the other nine, its centre (the mean of its marked pins)
measured through it: median 0.148 mm, largest 0.386 (U3). The plan passes a
median of 1.0 and none over 2.0, and stops on any over 3.0. No IC footprint is
on one board and not the other; both boards also carry U1's and U4's 600 and
300 mil footprints, and the PAL board's RAMs sit in the 300 mil ones.

**What could not be measured as the plan words it:** P1, P2 and P3. On I3 the
72-pin cartridge connector is fitted over the edge fingers, the expansion
port's socket hides its pins, and the PAL modulator's can covers the place of
the MOD RF footprint. So the check rests on the ten ICs, the plan's minimum,
and the connectors are recorded in `spike.json` as not measured, not as
unmatched: they were not seen, which is not the same as seen and different.

**How the 20 mm lens was allowed for.** The pins were marked where they meet
the board, the solder joint or the pad, never the leg's shoulder or the body,
so every mark is in the board's plane, where a homography is exact. Tall
things lean away from the picture's centre; the legs' shoulders, a millimetre
or two up, would lean by a fraction of a millimetre, so they were not used.
The homography takes up the plane's perspective; it does not take up the
lens's own distortion, and the figures are what is left with it.

**6. The case's depth: STOP.** With the plan's camera from the Exif, 112 / 23.6
x 4020 = 19,078 pixels, and the principal point at the picture's centre, the
top's depth to width is 0.677 (O2-FL) and 0.680 (O2-BR). The top's two
vanishing points give focal lengths of 24,596 and 23,831 pixels, 29 and 25
per cent away from the Exif's, over the plan's 10, so the plan's rule takes
the vanishing points' value: depth to width 0.7358 and 0.7298, against the
published 203.2 / 254 = 0.800. Errors of -8.03 and -8.77 per cent; the plan
passes within 1.5 and stops over 3.0 on either. Before calling it I checked
the marks: the four edge lines and their corners, drawn on both pictures, lie
on the edges; and moving any one corner 3 pixels moves the ratio only between
0.727 and 0.746 (O2-FL), so marking cannot make it 0.80.

**7. The case's height, the two photographs: passes.** Through the same
cameras, the bottom edge of the near long face, read through that face's
vertical plane, is 0.3355 (O2-FL) and 0.3332 (O2-BR) of the width below the
top, 0.67 per cent apart against the plan's 2. Their mean times 254 mm is
about 85 mm, against the published 88.9; the edge used is the bottom shell's,
above its feet. The corners' own vertical edges could not be used as the plan
says: each stops where the bottom shell starts, whose sides lean in, so none
reaches the table. This rests on the same cameras as the depth.

**8. The PAL front: between pass and stop, on two of its three ratios.** The
front of the top shell, a band the case's width, rectified by the homography
on its four corners in O2-FL and in O4. The door's width over the band's:
0.5744 and 0.5764, +0.35 per cent. The label strip's height over the band's
width, the label strip read as the top shell's front band, which carries the
console's name: 0.1637 and 0.1580, -3.49 per cent, between the plan's 2 and
4. The buttons' span could not be measured on O4: it is the PAL top shell
taken off its console, and POWER and RESET are on the bottom shell. Two
assumptions under the band's height: on O2-FL it comes from the same camera
as the case's depth, and on O4, taken straight on, from its mean height over
its mean width in pixels, which leaves out any tilt of the camera.

**The outline, recorded:** the scan's board is 196.49 by 120.27 mm (its
extent where it is darker than half way between the lid and the board's
tinned edges, through x and y), against the KiCad redrawing's 196.25 by
118.70 read from its Edge.Cuts by `kicad_outline_box`. The KiCad layout is a
redrawing, so this is not judged; the 1.6 mm in y is more than the scale can
explain, and task 2 measures the outline properly.

### Why the case's depth stops, and what I would try

[inferring] The camera is wrong, not the case. Three things say so:

- The pictures are 4020 by 2880 pixels, where the D7000 takes 4928 by 3264,
  so they are cropped, and the plan's 19,078 pixels takes 4020 pixels to span
  the sensor. A crop that was not resized would put the focal length at 23,386
  pixels, near what the vanishing points give, but its principal point need
  not be the picture's centre.
- The corners' vertical edges do not lean as the camera says they must. At the
  near long face's left end that camera wants the edge 5.1 degrees off the
  picture's vertical (5.3 on O2-BR); it is -0.8 (-0.6). At the right end it
  wants 1.3 (1.5) and the edge is 0.5 (0.0). Vertical lines that stay vertical
  in a picture taken from above are what a perspective correction does, or a
  crop far off centre; either breaks the camera the plan set.
- The published figures give 0.80, and the patent's top view, measured after
  the revision below, 0.77; the photographs give 0.73 with the camera that
  makes the top a rectangle.

So the stop is real and stays recorded, and the question is the plan's to
answer. What I would try, for the plan's next version to choose:

1. **Measure the camera from more than the top.** Three vanishing points (the
   verticals included) give the principal point as well as the focal length.
   On these pictures the verticals are almost parallel, so this is badly
   conditioned; it would need checking on a made-up box first.
2. **Take the case's proportions from the patent's orthographic views** (O1),
   which need no camera, and use the corner photographs for features only. The
   patent's drawings may not be exactly to scale, so they would be judged
   against the published figures the same way.
3. **Take the published width and depth as given**, as the design already
   does for the size, and judge only what the photographs can say without a
   camera: the features' places along each face, each face rectified on its
   own corners, as check 8 does.

## What the stop stops

The plan says it: the outside stops, so every model stops, because a cased
machine claims both views or none; the machine still counts. The checks the
inside rests on (the scale, the solder side, the PAL layout) pass or sit
between pass and stop, and none of them stops on its own. Whether the inside
goes on depends on Dan's answer about the outside.

## The case revised, later the same day

Dan ruled on 5 October 2026, after an independent review of the figures
above: the case depth check judged the wrong quantity. It measured the
camera, not the case. The two photographs fail their camera's own check (the
vertical edges), they are nearly the same view, and they put depth to width,
height to width and the front band 3 to 7 per cent below the patent's. The
STOP of -8.77 per cent stays on record as crossed, in `spike.json` under
`case.depthToWidthErrPct` with the photographs' other figures, now recorded
only, and the first eight verdicts are kept in `spike.json`'s `revision`. No
limit moved. The plan's three rows, and task 0's steps 11 and 12, now say:

- **Case depth to width**, on the patent's orthographic views (FIG 5 top,
  FIG 6 bottom, FIG 7 side over FIG 3 front), against the published 0.800,
  judged on the worst view: pass within 1.5 per cent, STOP over 3.
- **Case height to width**, on FIG 3 and FIG 7, against the published
  88.9 / 254 = 0.350, the same limits. The row on the two photographs'
  heights agreeing is dropped as not meaningful; its figure (0.67 per cent, a
  pass) stays recorded.
- **The PAL front**, O4 against the patent's FIG 3 front, O2-FL recorded. A
  pass needs at least two measured ratios, each within 2 per cent; a STOP is
  any over 4. The verdict function and its test now say so, and a test shows
  one ratio alone can never pass.

Two of the first figures rested on the same faulty camera, and are recorded,
not judged, for that reason: the case height "pass" (the bottom edge read
through the cameras' face plane) and O2-FL's label band, read through the
camera's plane of the front.

**What the photographs' own records say.** Both files' XMP has Camera Raw's
PerspectiveVertical="0", PerspectiveHorizontal="0" and HasCrop="False", with
LensProfileEnable="1"; the Software is Adobe Photoshop CS5 Windows, and the
Exif SubjectDistance is 2.24 m (read on 5 October 2026 from the files'
metadata). So the crop was made in Photoshop, not in Camera Raw, and Camera
Raw corrected no perspective; a transform in Photoshop itself is not ruled
out. The same is now in `docs/nes/facts/models.md`.

### The patent, measured

`spike.py` takes the PDF's pages 3 and 4 (the patent's sheets 2 and 3) out at
their own 300 dpi with `pdfimages` (poppler), the one program outside Python
it reads with. Each view is a box marked by hand round its drawing. Across,
the view's extent is its outermost drawn pixels; down, from the outer edge of
its first long line to that of its last, a long line being a row at least
half as black as the blackest, so the buttons in the top view and the feet in
the front and side views are left out; the overall extents are recorded
beside. Measured on 5 October 2026 with `cd tools/nes-model &&
NES_MODEL_INPUTS=/tmp/nes-inputs /tmp/nesvenv/bin/python spike.py` (exit 3),
figures in `spike.json`'s `case.patent`:

| View | Depth to width | Against 0.800 |
|---|---|---|
| FIG 5, top | 0.7723 | -3.46 per cent |
| FIG 6, bottom | 0.7758 | -3.02 per cent |
| FIG 7 side over FIG 3 front | 0.7914 | -1.07 per cent |

| View | Height to width | Against 0.350 |
|---|---|---|
| FIG 3, front | 0.3525 | +0.72 per cent |
| FIG 7, side | 0.3534 | +0.97 per cent |

With the feet, the heights are 0.3682 and 0.3709.

**Case depth: STOP, as revised.** The worst view, the top, is 3.46 per cent
short, over the 3 per cent stop; the bottom view is 3.02, also over it. The
ruling expected about 0.78 and so a result between pass and stop; the drawing
gives 0.772 to 0.776 on the two plan views. It is recorded as a STOP, not
tuned. Two things about it, neither a reason to move anything: a design
patent's drawings need not be exactly to scale, and the side view over the
front, from two drawings on different sheets, gives 0.791, so the patent does
not agree with itself to better than about 2.5 per cent.

**Case height: passes, as revised.** +0.97 per cent at the worst, within 1.5,
on the body without its feet.

**PAL front: STOP, as revised.** On the patent's FIG 3 (the top shell's sides
at 653.0 and 1786.5 px, its top and the seam, the door's left side and
the black strip's left edge, POWER's and RESET's middle outlines), against O4
as measured before:

| Ratio, over the face's width | Patent FIG 3 | O4 | O4 against the patent | O2-FL, recorded |
|---|---|---|---|---|
| Door width | 0.5823 | 0.5764 | -1.01 per cent | 0.5744 |
| Label band height | 0.1719 | 0.1580 | -8.08 per cent | 0.1637 |
| Buttons' span | 0.2144 | not on O4 | not measured | 0.2611 |

Two ratios are measured, and the label band is over the 4 per cent stop. The
two photographs agree with each other better than either does with the
drawing (O4 against O2-FL -3.49 per cent; O2-FL against the patent -4.75),
and O4's band rests on taking O4 as straight on, so the cause may be the
drawing's proportions as much as the PAL shell; the check cannot tell. O2-FL's
buttons' span, +21.8 per cent against the patent, is a poor figure in any case:
the buttons stand out of the face, and it was read through the face's plane.

### Where that left the plan, before the second revision

Two STOPs stand, as revised: the case depth, which stops the outside and so
every model, and the PAL front, which stops the PAL models. The inside's
checks (scale, solder side, PAL layout) pass or sit between pass and stop.
What happens next is Dan's call.

## The case revised again, after the patent was seen

The controller ruled a second revision the same day, with Dan's instruction
to be pragmatic and not stop. **It was made after the patent's figures were
seen**, and that is said here and in the plan's rows so that nobody takes it
for a limit set in advance. The reason: the sources disagree with each other
by more than the 1.5 and 3 per cent limits can resolve. The patent's own top
view gives depth to width 0.7723 and its side over its front 0.7914 (above);
the published widths are 254 mm (Fandom, Thingiverse) and 256 mm
(dimensions.com) at a depth of 203.2, so the published ratio is 0.794 to
0.800. The check's real purpose is to catch a gross error of scale, and the
limits now say that:

- **Case depth to width**: the patent's three figures against the published
  ratios' midpoint, 0.797; pass if every figure is within 5 per cent, STOP if
  any is over 8.
- **Case height to width**: the patent's two figures against 88.9 / 255 =
  0.349, 255 mm being the published widths' midpoint; the same 5 and 8.
- **The PAL front**: back to the plan's first form, O4 against O2-FL's front
  face rectified on its four corners. For the door's width that
  rectification is planar and needs no camera, so the first revision's
  reason for moving it does not apply. The label band's height does need
  one: on O2-FL it is read through the case's camera, which the first
  revision found faulty, and O4 is taken to be straight on, so the band's
  figure is the weakest and the nearest a stop, which is known and said here.
  At least two measured ratios, each within 2 per cent, to pass; STOP over 4.
  The patent's front figures are recorded, not judged.
- **The case's size in the model** (task 8): the published 254 by 203.2 by
  88.9 mm, said on the page to be no figures of Nintendo's and good to about 3
  per cent.

Every earlier result stays in `spike.json`'s `revision`: the photographs'
-8.77 per cent STOP, and the first revision's -3.46 per cent depth STOP
(kept beside the new figures as `case.patent.firstRevisionAgainst254Mm`) and
its -8.08 per cent PAL front STOP (kept as each ratio's `O4AgainstPatentPct`),
with both earlier sets of eight verdicts.

Measured on 5 October 2026 with `cd tools/nes-model &&
NES_MODEL_INPUTS=/tmp/nes-inputs /tmp/nesvenv/bin/python spike.py`, which now
exits 0:

| Check, as revised a second time | Figures | Verdict |
|---|---|---|
| Case depth | -3.08 (top), -2.64 (bottom), -0.68 per cent (side over front), against 0.7969 | pass |
| Case height | +1.12 (front), +1.37 per cent (side), against 0.3486 | pass |
| PAL front | door +0.35, label band -3.49 per cent, O4 against O2-FL; the buttons' span not on O4 | between pass and stop |

The other five checks are unchanged: scale x passes, scale y is between pass
and stop, x against y passes, the solder side passes, the PAL layout passes.
The PAL front's label band, at -3.49 per cent, is the figure nearest a stop;
it rests on O2-FL's band height through the case camera that the first
revision found faulty, and on O4 taken as straight on, so it is a weak figure
either way.

## Mistakes

- The first measure of the edge connector used the gaps either side of the
  second and second-last fingers, because the end fingers' outer edges run
  into the board's bevel. That is not what the plan says, which is the end
  fingers, and it mattered: with the wider end fingers left out, the row read
  1.6 per cent short where the plan's row reads 0.4, and the judged median
  went from 0.113 to 0.224 mm. Caught before anything was written up, and
  changed to the plan's end fingers, each put half way between its edges.
- Reading the I3 overview I took coordinates off a picture shrunk to fit the
  screen, and was off by more than 100 pixels; every mark was then read off
  crops at full scale or more, with the grid labelled in the original's
  pixels.
- The first made-up board for the pad tests drew its pads a third of a pixel
  off, from a supersampling offset (pixel i of the shrunk picture covers 4i to
  4i + 3 of the large one); the tests caught it, and the drawing was fixed.
- The made-up pad was first as light as the BBC Micro's solder, so the tests
  passed with the old constants too and proved nothing about this scan; it is
  now as light as this scan's rings, and fails with the old constants.

## Tests

`/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q`: 23 passed (the
plan's 22, and one that the made-up pad is this scan's size). `cd site && node
--test tests/nes-models.test.mjs`: 6 passed; two of them were seen to fail, on
a changed hash in `models.md` and on a copy of I6 in the tool's folder, and
the verdicts test on a changed verdict in `spike.json`.
`tests/nes-spike-verdicts.test.mjs` failed on the missing module, then 3
passed. `cd site && npm run build && npm test`: 301 passed, one to-do, none
failed, so both workflows' floors are raised from 292 to 301.

After the revision: pytest 23 passed again; `node --test
tests/nes-spike-verdicts.test.mjs` 4 passed, the new test seen to fail with
the two-ratio rule taken out of the verdict function; `cd site && npm run
build && npm test`: 302 passed, one to-do, none failed, so both floors are
raised to 302.

After the second revision: pytest 23 passed; `node --test
tests/nes-spike-verdicts.test.mjs` 4 passed, and failed (one test each time)
with the case limits put back to 1.5 and 3 and with the stop moved to 8.2;
`node --test tests/nes-models.test.mjs` 7 passed, the new one holding that
the PAL front's judged figures are O4 against O2-FL. `cd site && npm run
build && npm test`: 303 passed, one to-do, none failed, so both floors are
raised to 303.

## Task 1: a model can draw more than one console

Each NES module draws two consoles, the NTSC NES-001 and the PAL NESE-001, and
the page's region says which. So a model entry in `site/src/models/models.mjs`
may now carry `regions`, a non-empty list such as `['ntsc', 'pal']`, and then
its `label`, `about` and `made` are keyed by region: each console has its own
accessible name, caption and note on how it was made. An entry with no
`regions` is one console and is read as before.

**Why it lives in `MODELS` and not in the registry.** The registry says which
models a machine has, as a list of `{ view, module }`, and the check that ties
it to `MODELS` stays as it was: one module for one view. Which consoles a
module draws is a fact about the module, as its caption and its note are. Four
modules, with a region on each registry claim, would have made the registry
rules longer and cost a second download when the region changes. One module
draws both, and the region switch needs no download.

**What was added.** `wordsFor(entry, region)` returns a model's words for one
console, the first region by default, and throws for a region the model does
not draw. `regionProblems(module, entry)` says in sentences what is wrong with
an entry's regions: empty, listed twice, not a lower-case word, no label or
caption for one, no note for one, a key that is not a region. `regionViews(entry)`
gives the model section one view for each console: its words, whether it is
hidden (all but the first region), and a suffix that keeps the element ids
apart. `MachineModel.astro` now reads its words only through `regionViews`. For
a model with regions it draws each console's caption and note in an element
with `data-model-region="<region>"`, all but the first `hidden`. The accessible
name is an attribute and cannot be hidden, so each region's name also sits in a
hidden element with `data-model-label` and the same attribute. The model
switches them in task 7. The page `[id].astro` never read a model's words, only
its module, so it did not change. `site/tests/model-regions.test.mjs` has the
tests, with a made-up entry because no machine has regions yet; the real NES
entry arrives in task 7.

**How the two pages were shown unchanged.** The test came first and failed on
the missing export (`does not provide an export named 'regionProblems'`). Then
the code. Then the site was built from the commit before this change, in a
scratch copy (`git archive HEAD` into `/tmp/t1-old`, with `node_modules`
linked), and from this change, and the two machine pages compared:

```
cmp /tmp/t1-old/site/dist/machines/kim-1/index.html site/dist/machines/kim-1/index.html
cmp /tmp/t1-old/site/dist/machines/bbc-micro/index.html site/dist/machines/bbc-micro/index.html
```

Both are identical, byte for byte (59,889 and 49,456 bytes). The BBC Micro has
no model section on this branch's base, so its comparison shows only that the
page is untouched. One mistake on the way: the component still read
`model.made` once, in the check that the measurements exist. The test that the
component never reads the entry's fields caught it, and the check now asks the
views.

Tests for task 1: `cd site && node --test tests/model-regions.test.mjs` 5
passed (the three of the brief, one for the page's views and one that the
component reads its words only through them). `cd site && npm test`, with the
two machines' WebAssembly built first (`node scripts/build-machines.mjs`):
308 passed, one to-do, none failed, so both floors are raised from 303 to 308.
Without the WebAssembly built, the four tests that read it fail, as they do on
any checkout that has not run it; the BBC Micro's page then differs from a
build that has it, so the scratch build for the `cmp` was given the same built
files.

## Task 2: the board's frame from the bare scan

`tools/nes-model/board_frame.py` measures the frame every later board script
reads I1-front through: the scan's x and y scale, the board's turn on the
glass, the outline and the mounting holes. It writes `data/frame.json`, a
copy of the scan rectified at 12 pixels per millimetre in `out/` (git-ignored:
the scan states no licence) and pictures to look at. `run-board.sh` runs it
after `verify.py I1-front I2`.

**This task was taken over part way.** A first implementer had written a
`board_frame.py`, its tests and a `frame.json`, unreviewed, and stopped. Its
scale fits, held-out errors, verdicts, frame and rectification were read,
tested and kept. Three parts were rewritten, because the pictures showed them
wrong on the scan:

- **The outline.** It was traced automatically from the board's mask. On the
  scan that put the origin on top of a break-off tab's remains, about a
  millimetre above the board's routed edge, traced the short top left stretch
  as loose contour points over the tabs, refused the round notch in the left
  edge as not round and drew it as a polygon cut inside the notch, and put the
  right edge's step out in the shadow below it. Now the outline's shape is
  marked by hand (below) and only where each edge lies is measured.
- **The x scale's ends.** It took each row's first and last pad on its solder
  blob's centroid, as task 0 did. On this scan those wander by up to 4 pixels
  from the drills (the blobs are uneven, joined to tracks), and U5's pin 1 row
  read 0.18 mm short held out. Now every pin is found on its drill, as the
  plan already required for y.
- **The mounting holes' rim.** It chose each hole's sharp half by where the
  edges were steepest; dirt in one hole (at 179.7, 113.6 mm) turned the choice
  round and the circle sat inside the hole, 3.37 mm across where the hole is
  4.11. Now the crescent's side is
  found from the light and the circle is fitted by consensus.

`rim_centre` moved from `spike.py` to `common.py` unchanged, with a new test
that it finds the hole's centre, not the solder's, and refuses where there is
no hole. `spike.py` was run again after the move (`cd tools/nes-model &&
NES_MODEL_INPUTS=/tmp/nes-inputs /tmp/nesvenv/bin/python spike.py`, exit 0)
and `git diff` showed `spike.json` unchanged.

### Decisions

- **x from the drills, without the edge fingers.** The rows across are the
  ten DIPs' two rows each and both rows of the expansion header P2, first
  drill to last. The edge fingers are left out: task 0 found them on 2.50 mm,
  not 2.54, and their pitch is not known from anything but this scale. That
  pitch through the frame is recorded (2.4988 mm over their 35 gaps). Using
  the drills for x as well as y was decided before the drills' x figures were
  seen, on the grounds above; the solder centroids' figure, task 0's way, is
  recorded beside and not judged.
- **A drill is found twice and checked against its row.** Its top rim is
  fitted from where the marks put the pin and again from the solder's
  centroid; the drill is refused if the two fits are more than 0.10 mm apart,
  or if it lies more than 0.15 mm from where a straight, evenly spaced row
  through the row's other drills puts it, each drill judged held out of that
  row's fit so an end drill cannot pull the row to itself. Both limits were
  set from the rim fits before any scale figure was judged: fits that agree do
  so within 0.05 mm, and those that do not are 0.2 to 0.6 mm apart. Five
  drills were refused: U5's pin 16 and U2's pin 8 off their rows, U4's pin 2 on
  two fits that disagree, U10's pin 2 with no rim and its pin 5 off its row.
  Their pairs are left out of y.
- **y from every pin pair of a DIP, not pin 1 and pin N alone.** This is a
  change from task 0, which took each DIP's spacing from pin 1 to pin N only.
  Here each footprint's spacing is the mean of all its pin pairs, drill to
  drill, 8 to 20 of them. The reason: the brief asks for the scale over every
  DIP and connector row, and averaging 8 to 20 pairs evens out one drill's
  error, where a single pair carries it whole. The choice came with the code
  inherited from the stopped first implementer, with no record of when it was
  made. I kept it after reading it. It changes the verdict. Task 0's way reads
  0.566 per cent, between pass and stop, and that verdict stays recorded in
  `spike.json` (and here as `yFromPin1AndPinN`); every pair reads 0.432, a
  pass. The refusal rule above does not help this figure. With it turned off
  (both limits widened in a scratch script run on 5 October 2026, which wrote
  no data file), y reads 0.312 per cent, max 1.330 (U10), so the rule makes
  the judged figure worse, not better. The verdict rule for y is the plan's,
  median at most 0.5 per cent to pass and over 1.0 to stop, and it was not
  changed.
- **The outline's shape by hand, its place by the light.** Over tracing it
  automatically, which needed a rule for every kind of feature on this edge
  (tabs' remains, half holes, notches round and small, steps, slots) and got
  three of them wrong. `data/marks.json` now has `outline`: 18 corners in
  order from the top left, three round notches by their deepest points, and
  which edge carries tabs' remains. The marks only say where to look and
  where the outline turns. Each edge is measured at stations every 0.1 mm,
  1 mm in from its ends (the router leaves inside corners rounded, about
  0.6 mm), as where the light, linear as the scanner mixes it, rises fastest
  from the board to the lid within 1.2 mm of the mark. That is a blurred
  step's half way point; profiles across the left, bottom and step edges were
  read in linear light to check it before it was chosen. A line is fitted to
  the stations one sided, leaving out points standing out of the board, so the
  tabs' remains on the top right edge drop out. The coordinates were first
  placed from the first implementer's automatic trace and then checked on
  their crops and moved where they were wrong, which marks.json says.
- **The short top left stretch sits at its half holes' bottoms.** Its top
  edge is mostly the remains of break-off tabs, standing about 0.9 mm out, with
  half holes between them that reach down to the routed edge; on the top right
  stretch the same tabs stand on a straight routed edge at that level. So this
  one edge is held square at the innermost 5 per cent of its stations. The two
  stretches then agree within 0.09 mm, which they were not made to.
- **One crescent direction for the scan.** A hole's far wall shows as a dark
  crescent on one side, where the light falls fastest at the far rim, not the
  hole's top rim. The crescent comes from the scanner's optics, so it lies the
  same way in every hole; the direction used is the circular median of each
  hole's darkest stretch (270.0 degrees, to the rear; 11 of the 12 holes read
  within 5 degrees of it, and the twelfth, in a plated ring, read 84). The rim is then fitted
  on rays within 75 degrees of straight away from it, by consensus: of the
  circles through three ray points, the one most of the others lie within
  0.08 mm of, then refined. A first version took each hole's own darkest
  direction, and a speck of dirt drawn in the test's made-up hole turned it
  round; a robust least squares fit was then dragged by the quarter of rays
  that hit the dirt. The consensus fit is the one that passed.

### What it measured

`NES_MODEL_INPUTS=/tmp/nes-inputs PYTHON=/tmp/nesvenv/bin/python
tools/nes-model/run-board.sh` on 5 October 2026, exit 0, about a minute and a
half (the same venv as task 0), printed:

```
scale x: pass
scale y: pass
x against y: pass
x 11.8020, y 11.8222 px/mm, ratio 0.99829; x median 0.011 mm over 6 rows, largest 0.032 (U6 pins 1 to 20); y median 0.432 %, max 0.981 % over 10 footprints
recorded: x from the solder's centroids median 0.031 mm, largest 0.184; y from centroids median 0.293 %
board 195.95 x 119.45 mm (KiCad 196.252 x 118.700), turned 0.0398 degrees, 12 holes, 3 notches, outline 85 points; fingers' pitch 2.4988 mm
```

Against the plan's thresholds, as written:

| Check | Figure | Pass | STOP if | Verdict |
|---|---|---|---|---|
| Scale x | median 0.011 mm over the 6 scored rows (the 40-pin rows and P2's); largest 0.032 mm, U6 pins 1 to 20, recorded | median at most 0.15 mm, at least 4 rows | median over 0.25 | pass |
| Scale y | median 0.432 per cent over 10 footprints, max 0.981 (U6); 600 mil median 0.478 (4), 300 mil 0.427 (6) | median at most 0.5 per cent, at least 8 | median over 1.0 | pass |
| x against y | 11.8020 against 11.8222 px/mm, 0.17 per cent apart; the stated 300 dpi is 11.8110 | | over 1.5 per cent | pass |

The y median is the figure nearest a limit: 0.432 against 0.5. U6 and U8 read
0.98 and 0.97 per cent narrow. Checked on U6: its 20 pairs read 15.006 to
15.184 mm, standard deviation 0.037, so the narrowness is the footprint's or
the scan's, not one bad drill. Recorded beside, not judged: the same spacings
from the solder's centroids give a median of 0.293 per cent but a maximum of
1.787; pin 1 and pin N alone, task 0's way, 0.566 and 1.156; and x from the
centroids 0.031 mm, largest 0.184 (U5 pins 1 to 20). Task 0's own figures
were 0.113 mm (fingers at 2.54) and 0.032 mm (without them) for x, and 0.566
per cent for y.

The turn, from every long row's drills at once, is 0.0398 degrees. Each long
row's drills lie within 0.032 mm rms of a straight line; none reaches the
plan's 0.05 mm. The rows' own angles spread from -0.10 to 0.11 degrees and the
DIPs' spacings sit up to 0.22 degrees (U8) from square to them, median 0.03:
recorded, not judged.

The board is 195.95 by 119.45 mm between its outermost edges, against the
KiCad redrawing's 196.252 by 118.700: 0.30 mm narrower and 0.75 mm deeper,
inside the 2 mm sanity bound. The long edges are not quite square to the rows:
the left edge leans -0.07 degrees, the right -0.11 and -0.15, the top -0.04;
the slopes are kept, and edges under 5 mm are held square. The 12 mounting
holes are 2.30 to 4.24 mm across; the 10 that have a round hole in the KiCad
outline near them sit a median (0.29, 0.19) mm from it, the two frames'
difference, recorded. The round notch in the left edge fits a circle of
2.25 mm radius within 0.023 mm rms; the right edge's two small notches fit
1.11 mm (0.065 mm rms, the least round) and 0.96 mm (0.015).

**Known, and left as measured.** The right edge's step, the short edge where
the board narrows by 2.9 mm, is placed 0.69 mm beyond where it was marked, at
the dark lower edge of the bare laminate. Under the step the lid is in deep
shadow, boxed in by the board on two sides, and the light rises most steeply
at the shadow's far side, not at the board. The overlay shows it. Nothing was
changed to move it: the rule is one rule for every edge. On the other edges
that face down the scan the board shows a dark band before the lid, which the
rule counts as board; whether the shadow under the step is the edge's wall in
shadow or the lid, the scan cannot tell. The step's two corners carry that
error, at most the 0.69 mm.

Every corner, notch, hole and row end was looked at in close-ups drawn by
`board_frame.py` (`out/frame-close-ups.jpg`) and the outline on the rectified
copy (`out/frame-overlay.jpg`): the lines sit on the board's edges, the tabs'
remains stand outside the outline, the notch arcs follow the notches, the hole
circles sit on the holes' rims (the dirty one included), and the drill marks
sit in the holes' middles.

### Tests

pytest first, on a made-up scan drawn as task 0's pads are, with a known x and
y scale, turn and offset: drills inside rings whose solder is drawn off the
drill (1 per cent of the spacing on the DIPs, 0.1 mm outwards at every row's
ends), an outline with every kind of feature this board has, tabs' remains
with half holes, a lid shaded next to the board, mounting holes with the
crescent and one with dirt on its rim. The tests ask for each scale within
0.05 per cent, the turn within 0.01 degrees, every corner within 0.1 mm, the
notches' centres and radii within 0.1 mm, the holes' centres within 0.05 mm,
x and y from the drills and not the solder, a drill off its row refused, and
an outline marked where it does not turn refused. Run before the code: 2
failed and 8 errors, on the missing functions. After: 14 passed. The made-up
board's worst corner comes back 0.092 mm off, near the 0.1 bound: its short
edges' fitted slopes pick up the drawing's pixel steps, and the lid's shading
moves the light's edge 0.02 to 0.05 mm outwards.

`/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q`: 38 passed.
`cd site && node --test tests/nes-models.test.mjs`: 11 passed, four of them
new. They check that `frame.json`'s recorded verdicts are what its figures give
against the plan's x, y and x against y rows, and its figures what its rows
give; that all three pass; that the outline is closed, starts at the origin,
is within 0.5 mm of the board's size and within 2 mm of the KiCad outline each
way; and that the holes are inside the board and the rectified copy is not
committed. With one recorded verdict changed by hand, two of them failed.

The figure in task 1's section for the BBC Micro's page was corrected to
49,456 bytes, the size `cmp` printed in that task's report; it had been
written as 49,404.
