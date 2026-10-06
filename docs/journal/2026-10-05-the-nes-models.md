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

## Task 3: the solder side registered, with its pads and footprints

`tools/nes-model/board_register.py` registers the solder side (I1-back,
flipped left to right) to the component side on every hole it finds on both,
judges the fit against the plan's solder row, and then builds the drills, the
pads on each face and the footprints. It writes
`data/registration.json` and pictures in `out/`. `run-board.sh` now runs
`verify.py I1-front I1-back I2`, `board_frame.py`, then this. Everything is
in task 2's frame; its x and y scales are used, not fitted again.

### Decisions

- **Every hole, by three finders, kept apart by kind.** Task 0's spike found
  holes one way: a pad's tinned ring round an open hole, fitted on its top rim
  (`ring`). It matched 323. The board has many more: vias as small open rings
  on the solder side, the RF modulator's large rings, and holes filled with a
  dome of solder, which show no rim. So a second finder takes round light
  blobs the first leaves out (the same light, low-colour mask opened only 5
  pixels, 40 to 2500 pixels), and fits each on its top rim with the hole's
  radius scaled to the blob (`open`), or, where there is no rim, fits the
  dome's outline by consensus (`dome`). Task 0's finder is kept as it was,
  with one refusal added: a rim the light does not rise across, from inside
  it into the ring, by 0.05 has no open hole under it. A made-up dome, lit
  brightest in its middle, was given a rim by both fitters until that was
  added. The rule was set on the made-up board, before any registration of
  the scans was run. Holes in the solder side's tinned planes show only as
  specks and are not found; nor are pads whose solder runs into the print's
  outline beside them (the DA rows on I1-front).
- **What was looked at before the first registration was run.** The finders
  were run on both scans alone and drawn by kind. Two things were set from
  those pictures: on I1-front nothing within 0.5 mm of a mounting hole's rim
  (task 2's) is taken for a hole, because the lid seen through one is a round
  light blob; and an open hole or a dome found on one face only is not a
  pad, because on I1-front the second finder also takes some printed figures
  (an 0, a 6) and scratches in the tinned planes. Only task 0's kind makes a
  pad on one face alone. Nothing about the finders, the match or the fits
  changed after the first registration was seen; the four runs below all
  give the same registration.
- **Match, folds, model choice and outlier rule: task 0's.** The four marked
  seed holes, mutual nearest within 1.0 mm, three rounds of affine, then an
  affine and a cubic each held out on a chequerboard of 20 mm blocks; the
  lower held-out median wins, within 0.005 mm the affine. The outlier rule is
  the plan's, applied to each hole's blob as found (task 0's blob for a ring
  hole, the second finder's for the others), with the median of each face's
  matched blobs, and it takes no residual. The verdict is on the figures
  without it.
- **A drill where the two faces agree within 0.4 mm.** A matched pair is a
  drill at the mean of its two faces' centres when they are within 0.4 mm, so
  each face's pad is within 0.2 mm of it, the plan's check; 31 pairs further
  apart are recorded in `notDrilled`, not drilled. The registration's figures
  include them. Each drill has a pad on each face at that face's own centre.
  Its diameter is the mean of the open rims it shows; a via part filled with
  solder shows less than its drill, so a few read under 0.3 mm (the smallest
  0.19).
- **Footprints by pitch and print, references by hand.** Rows 2.54 mm apart
  are paired at 7.62 or 15.24 mm into DIPs, the print deciding a row that
  could pair either way (an outline 1.2 to 2.2 mm inside both rows). Pin 1 is
  the print's notch where it can be seen, read as a half circle at the
  outline's end with its middle third on print; the print is not looked for
  within 1 mm of a hole. There are no square pads on this board; the made-up
  board tests that path. Every reference is read from the print by hand in
  `marks.json` `footprints`, each with its crop and reason. U1 and U4 each
  have three rows: their 600 mil footprint (the plan's Facts) and a 300 mil
  one sharing the lower row, which the PAL board's RAMs use; the marks say
  which pairing is the chip's and keep the other as an alternate, `U1 (300
  mil)` and `U4 (300 mil)`. P2 to P6, X1 and X2 are grouped by hand, row by
  row or pin by pin, because their pitches (4.0, 2.0, 5.0 and 7.5, 2.5 mm) or
  spacing (P2's rows 6.3 mm apart) are not a DIP's.
- **The edge fingers on their own.** Tall tinned strips, darker than the pads
  (light 0.36 to 0.44 on a strip, 0.17 to 0.22 in a gap), found on each face
  as one row with even gaps, 36 a face, one footprint, P1, of 72. Pin 1 is
  the component side's left finger: the print reads 1 above it and 36 above
  the right one.

### What it measured

`NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python
board_register.py`, run in `tools/nes-model` on 5 October 2026, exit 0, about
eight minutes on a busy machine (the venv as task 0's; `verify.py I1-front
I1-back I2` first: all three present, hashes match). It printed:

```
solder side: affine, 511 holes; held out median 0.089, p90 0.319, max 0.915 mm: pass
  with the roundness rule (280 excluded): median 0.070, p90 0.138, max 0.421 mm
  fits: affine 0.089/0.319/0.915; cubic 0.092/0.340/0.927
  found: {'front': {'ring': 583, 'open': 187, 'dome': 94, 'refused': 72, 'inMountingHoles': 4}, 'back': {'ring': 466, 'open': 107, 'dome': 116, 'refused': 43, 'inMountingHoles': 0}}
drills 480 (not drilled 31), pads 1368, footprints 27 (12 DIPs); fingers 36 + 36
```

| Check | Figure | Pass | STOP if | Verdict |
|---|---|---|---|---|
| Solder side, held out, without exclusion | 511 holes; median 0.089 mm, 90th percentile 0.319, largest 0.915 (recorded) | median at most 0.20, 90th percentile at most 0.40, at least 150 holes | median over 0.30 or 90th percentile over 0.60 | pass |

The affine is chosen: its held-out median is lower than the cubic's (0.089
against 0.092), and within 0.005 mm it would have won anyway. With the
outlier rule, 280 of the 511 are left out (a blob's axis ratio over 1.25 on
the solder side 188 times and on the component side 118, its area out of
range 148 and 135 times; a hole can fail more than one): median 0.070, 90th
percentile 0.138, largest 0.421. That figure is recorded, not judged.

The 90th percentile, 0.319 against 0.40, is the figure nearest a limit. It
comes from the kinds task 0 did not use. By the kind found on each face
(component side first), held out:

| Kinds | Holes | Median | 90th percentile | Largest |
|---|---|---|---|---|
| ring / ring | 318 | 0.075 | 0.237 | 0.915 |
| ring / dome | 60 | 0.127 | 0.356 | 0.738 |
| ring / open | 36 | 0.218 | 0.407 | 0.833 |
| open / ring | 32 | 0.166 | 0.400 | 0.736 |
| open / open | 24 | 0.169 | 0.380 | 0.592 |
| dome / open | 17 | 0.158 | 0.226 | 0.294 |
| dome / dome | 8 | 0.148 | 0.485 | 0.501 |
| dome / ring | 8 | 0.478 | 0.695 | 0.709 |
| open / dome | 8 | 0.176 | 0.475 | 0.621 |

The ring pairs alone read as task 0's spike did (0.074, 0.239, 0.921 on 323).
The others are less sure: a dome's centre is its solder's, not its hole's,
and some of the second finder's holes on I1-front are print matched to a
hole beside it. The eight dome on the component side against a ring on the
solder side read worst (median 0.478); those are not left out, since nothing
but the plan's rule may leave a hole out. Across the board the held-out
median rises from about 0.08 mm on the left to about 0.12 from x = 120 mm
on, where the overlay shows fewer holes matched. The largest, 0.915 mm, is a ring
pair at (132.4, 63.3) mm, a pin of U5's lower row.

480 drills: their two faces' centres a median 0.085 mm apart (90th percentile
0.263, the most 0.40 by the rule). 1368 pads: 960 at drills (two each), 310
found on one face only (task 0's kind), 72 edge fingers, 5 completing a DIP's
row from a hole found on one face, and 21 inferred where a footprint's pin
was found on neither (P3's five and P6's five large rings, which neither
finder takes; two of X1's and two of X2's; P2's pins 39, 40 and 44; U5's,
U2's and the narrow footprints' missing pins).

Footprints: the ten ICs, each a DIP with the pin count the plan's Facts give
(U5 and U6 40, U1 and U4 24, U2 20, U3, U7, U8 and U10 16, U9 14); the two
narrow alternates of 24; P1 72; P2 48, P3 5, P4 7, P5 7, P6 5, X1 4, X2 3;
and seven groups the grouping found that are not named: the two rows of
vias above the edge fingers, three resistor arrays (nine of RA1's 13 holes,
DA1, DA4), C28 to C31's column, and C33 to C37's two rows of five, which it
calls a connector. Pin 1 came
from the print's notch on seven DIPs and was marked by hand on five: U2 and
U7, which have a via in the notch; U1 and U4, whose 600 mil outline has no
notch at its middle (the grouping's own reading agreed on U1); and U1's
narrow footprint, whose middle row's first hole is not found. Every pin 1 is
at the lower left, as task 0's marks have every DIP. The print reads
74HCU04P at U9, where the KiCad redrawing has a 74LS04, as the plan's Facts
already say.

### Runs, and what changed between them

Four runs on 5 October 2026, each the command above; the registration's
figures were the same in all four, to the last digit printed.

1. The first found 3 edge fingers on I1-front and 9 on I1-back. The strips
   are darker than the pads' floor (0.34), and the scanner's lid below the
   board's edge, the bare edge under the strips and the vias above them join
   every strip to the next. The finger finder was rewritten on the light
   read across the strips (the probe above): averaged 15 pixels down the
   scan, a floor of 0.29, the lid left out, opened with a box 4 mm tall, and
   linked by even gaps, not even centres (the end fingers are wider).
2. The second found 36 on each face. Its footprint overlay showed the notch
   missed where a via's pad sits in it, U1's and U4's narrow footprints cut to
   20 pins (their middle rows' end holes not found), and P2 split between two
   rows of different lengths. So the print is now scored only where it is
   not cleared round a hole, an alternate runs the length of the row it
   shares, and connectors and crystals are grouped by hand; the grouping's
   own row of the same holes gives way to the hand's.
3. The third gave the footprints above, with U2, U7 and U1's narrow footprint
   still without a pin 1; their pin 1 marks were added.
4. The fourth is the one committed.

Looked at: every hole found on each face by kind (`out/register-holes-front.png`,
`-back.png`), the matched holes coloured by held-out error
(`out/register-held-out.png`), and the pads and footprints on I1-front
(`out/footprints.png`): the DIP boxes sit on their rows, pin 1's circle on the
lower left pin of each, P1's strips on the fingers of both faces, and the
connectors' pins on their rings.

### Tests

pytest first, on a made-up pair of scans drawn as task 0's pads are: DIPs
lying across with pin 1 by the print's notch, by a square pad, by hand, and
one turned round; a DIP with a third row (its first hole missing); a via in a
notch; a row of ten; small open vias and domes of solder lit brightest in the
middle; 36 edge fingers on each face, the end ones widened outwards; the
solder side mirrored, turned, scaled and warped by a mild cubic, with 2 per
cent of its holes missing. Run before the code: the module did not exist.
Then the first run stopped at its first failure, 184 of 191 holes matched
(the made-up domes taken for open holes); then 4 failed (the notch filled in
by the solder mask, so no pin 1 from the print, and the end fingers joined
to their neighbours),
then the three tests for the second run's changes failed before their code
(a hand-grouped row had no place to go). After: 20 passed; the tests ask
for the made-up pair's held-out median and 90th percentile under 0.05 mm and
its largest under 0.08.
`common._consensus` no longer fails on fewer than five rim points (a made-up
dome gave three); it refuses them, which changes nothing that ran before,
since before it stopped with an error.

`/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q`: 59 passed.
`cd site && node --test tests/nes-models.test.mjs`: 16 passed, five of them
new: the solder row passes on the figures without exclusion over at least 150
holes with its recorded verdict the one its figures give; the excluded holes
are exactly those the rule names; every drill has a pad on both faces within
0.2 mm; U1 to U10 each have one DIP with the Facts' pin count, pin 1 first,
marked by hand; P1 has 72 fingers, 36 a face. With the recorded verdict
changed to STOP and one excluded hole renumbered by hand, two of them failed.
`npm test` (with `results.json` copied in for the run and removed after):
tests 318, pass 317, fail 0, todo 1 (the BBC Micro's, already there). The
floors are 317 in both workflows.

### After review, the same day

- **P3 was marked about 1.1 mm low.** Its row was marked on a crop enlarged
  twice, and the marks sat on the rings' lower edges. None of its five rings
  is found by either finder, so all five pins are inferred at the marks, and
  the error went straight into `registration.json`. Each ring was read again
  on its own crop enlarged eight times, from its outer edge's four sides:
  centres (1973, 1232), (2020, 1232.5), (2067.5, 1232.5), (2113, 1232) and
  (2161, 1232) pixels, 4.0 mm apart. Every pin moved 1.10 mm up the scan and
  0.08 mm to the right. P6, X1 and X2 were looked at again the same way.
  P6's pin 1 had been marked 0.51 mm high and its pins 2 and 3 about 0.2 mm
  high; pins 4 and 5 were within 0.13 mm. All five were re-read on crops
  enlarged ten times, and every P6 pin is inferred, so each moved by those
  amounts. X1's two can holes were 0.3 mm off; the one that is inferred
  moved 0.30 mm, and the other takes the drill found there either way. X1's
  leads and X2 were within 0.2 mm and kept. The re-run, the same command as
  before, printed the same registration to the last digit; `solder`,
  `drills` and `notDrilled` in the file are unchanged, and only `pads` and
  `footprints` moved.
- **A check that could not fail.** "Every drill has a pad on both faces within
  0.2 mm" holds by construction: a drill is made only where its faces agree
  within 0.4 mm, at their mean. The site test now also checks that every
  matched hole is either a drill or recorded as not drilled (480 and 31 of
  511), and that no more than a tenth are not drilled. It failed with one
  not-drilled pair removed by hand, with the hole count changed by hand, and
  with the tenth lowered to a twentieth.
- **The roundness test's areas mix two masks.** A ring hole's blob is task
  0's, common.pad_mask opened 11 pixels; an open hole's or a dome's is the
  second finder's mask opened 5 pixels. So the areas are not comparable
  across kinds, and each face's median mixes them. This is the plan's rule as
  written, applied to the blob each hole was found from, and the judged
  figures, which are without the rule, are not touched by it.
- Smaller things: where the grouping found no pin 1 of its own, a hand mark's
  agreement with it is now recorded as null, not true (U2, U4, U7, U1's
  narrow footprint); the drill diameter's floor in the site test is 0.15 mm,
  with the 0.19 mm part-filled via named; every gap between the inner edge
  fingers is checked, not only their mean; and the plan's interface line for
  `outliers.excluded` now gives the shape the code writes.

## Task 4: the copper on both faces, and the print

`tools/nes-model/board_trace.py` traces the copper on both faces of the bare
board and its white print from the scans I1-front and I1-back, runs the
plan's three copper checks on them, and writes the track map,
`site/src/assets/tracks/nes-famicom-board.webp`, with `data/copper.json`
beside it. Both faces are resampled onto one grid at 12 pixels per
millimetre over the outline's box: the component side through task 2's
frame, the solder side, flipped, through task 3's affine. `data/ic-table.json`
holds the GND and +5V pins the nets check needs, each with its source.

**The nets check failed.** It was run once, after the method was fixed, and
its result stands as measured: of the ten ICs, the largest GND net holds 1 of
their 10 GND pins and the largest +5V net 2 of their 10 +5V pins, against at
least 90 per cent each, and a GND pin and a +5V pin share a net. Coverage and
drills in copper pass. What went wrong is below, under "Why the nets fail".

### Decisions

- **The solder side's affine is now in `registration.json`.** Task 3 kept the
  chosen fit's parameters in memory only. `board_register.py` now writes them
  as `solder.transform`: the board millimetres of I1-back's four corners,
  flipped (three fix the affine; the fourth checks it, within 0.002 mm, in
  `affine_from_corners`). Corners and not the matrix, because the file keeps
  three places and a matrix entry near 0.085 rounded to three places would be
  off by half a per cent. It was re-run (`cd tools/nes-model &&
  NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python
  board_register.py`, exit 0): it printed task 3's figures to the last digit
  (affine, 511 holes, 0.089/0.319/0.915), and the file is the same apart from
  the new key.
- **What a pixel is.** OKLab, as the KIM-1's trace. On these scans copper
  under the green lacquer is the laminate's own colour made lighter (hue 145
  to 147 degrees for both), so lightness tells them apart; tin is grey; the
  print white. The thresholds and the probes behind them (5 October 2026,
  boxes in the 12 px/mm grid unless said; all in the script's comments):
  - Laminate under the lacquer, component side (x 585-640, y 815-840): L
    0.146; solder side (x 1030-1090, y 970-1030): 0.144. Copper under the
    lacquer (x 425-455, y 760-800, between U6's rows; solder side x
    1122-1150, y 982-995): 0.212 and 0.213.
  - Tin: the component side's left plane (scan x 40-130, y 470-640) L 0.179
    / 0.339 / 0.520 (5th, 50th, 99th percentiles), chroma over lightness
    0.044 at the median; the solder side's hatched strip 0.237 / 0.380 /
    0.568, 0.033. The lacquer's chroma over lightness: 0.121 at its 1st
    percentile. **Tin: chroma under 0.08 of lightness, L over 0.15**, half way
    between the tin's median and the lacquer's 1st percentile.
  - Print ("NES-CPU-10", scan x 930-1230, y 828-862), its pixels over L 0.6:
    0.627 / 0.737 / 0.804. **Print: L 0.63 or more**, half way between the
    tin's 99th percentile and the print's median, on the component side only
    (the solder side has none), not on a pad task 3 found (a pad's solder is as
    white: up to L 0.80 and 0.89), grown by 0.15 mm for the strokes' blurred
    edges.
  - The NTSC sticker (scan x 530-720, y 1255-1300): L 0.742, hue 83, as light
    as the print, so it is marked by hand in `marks.json` (`hidden`), not told
    apart by colour. It hides 157 square mm, neither copper nor print.
- **The lacquer's level: a smooth surface, not an opening.** The plan asked
  for the KIM-1's local colour by an opening. On these scans a local level in
  blocks (the 10th percentile in 12 mm windows, and a two-component mixture in
  10 mm blocks) drew the board's layout, not the light: a block or an
  opening's disc inside a wide pour sees no laminate. Its map was looked at.
  The laminate itself drifts by more than half the copper's contrast across
  the component side (top rows about 0.18 to 0.20, lower right about 0.13). So
  the level is a quadratic in x and y fitted, outliers out, to the median L of
  the laminate in 10 mm blocks every 5 mm, the laminate being what lies under
  Otsu's split of L less a first surface fitted to the blocks' 10th
  percentiles. Its blocks scatter round it by a robust 0.010 (component side)
  and 0.011 (solder side).
- **Smoothing 1.25 pixels, over the lacquer alone.** The two probe boxes sit
  3.3 (component side) and 3.0 (solder side) of their pooled standard
  deviations apart unsmoothed, 6.2 and 4.4 after a Gaussian of 1.25 pixels
  (6.6 and 4.9 at 1.5, 5.9 and 4.0 at 1.0). The gaps between the wide tracks
  under U6 are 2 to 4 pixels, read by eye; on the tests' made-up board, four
  tracks 3 pixels apart came back as one piece at 1.5 and as four at 1.25 and
  1.0, so 1.25, the largest that keeps them. The smoothing is a normalised
  convolution over the lacquer's pixels, so the tin and the print do not
  bleed into it.
- **Otsu's threshold with hysteresis, the low threshold Otsu's.** The
  KIM-1's hysteresis (keep a region over 0.6 of the threshold if it holds a
  pixel over it) was tried first on the made-up board and widened every
  track by a pixel each side (the solder side's intersection over union
  0.82), which would close those gaps. So a region over Otsu's threshold is
  kept if it holds a pixel as far over it as the laminate's median is under
  it: the edge stays where Otsu puts it and the high threshold only refuses
  specks. Otsu put the threshold at dL 0.0327 (component side) and 0.0416
  (solder side).
- **The lacquer's bright rim round a pad.** Looked at on the solder side's
  overlay: between neighbouring DIP pins the lacquer is lighter than copper
  under it (sRGB about (6, 72, 47) between two of U6's pins, L 0.25 to 0.36),
  and copper under the lacquer joined every pin of a row. The rim's width is
  measured on each face as it is traced: the median L of the lacquer at each
  whole pixel's distance from the pads, the rim ending where that is within
  0.005 of its median 10 to 12 pixels out. It read **5 pixels on the
  component side and 9 on the solder side**. Copper under the lacquer within
  the rim is kept only where a track runs on past it: out from the pad, as
  many steps beyond the rim as the rim is wide; or, 3 pixels or more from
  every pad, both ways along any line, for a track passing between two pads.
  Both conditions were set on the made-up board, where a looser rule let a
  slanting walk from one pad's rim ride into a track between two pads and
  join them. It took 1,037 square mm off the component side's copper and
  1,906 off the solder side's.
- **Drills' holes filled when grey.** A drill in a pad is filled when the
  piece of not-copper is under 2 square mm and mostly grey. The first rule
  said "dark"; a made-up drill drawn near black came back with L 0.134 in
  OKLab, no darker than the laminate, so dark could not tell it. A ring of
  bare laminate round a pad is green and is never filled.
- **Copper under the print, only in line.** Along the line straight across
  the print's stroke (the nearest of 8 to its normal, from the print mask's
  structure tensor), the print must be crossed within 1.2 mm with copper on
  both sides running on for 0.3 mm the same way. First any of the 8 lines
  would do; on U6's outline a slanting line from a gap between two tracks
  reached a track each side. The made-up board's side by side tracks under a
  print line test it.
- **A pad's radius for the print.** The first look run showed no print at
  all: 95 of task 3's 1,296 pads (not counting the fingers) have blobs that
  ran into a plane, up to 201 mm across, and their discs covered the board.
  A pad's radius is now its blob's half side up to 1.6 mm, else 0.8 mm.
- **The map at 10 pixels to the millimetre.** The look run before the final
  printed its size at each choice: 144,774 bytes at 10, 125,654 at 9,
  108,026 at 8, 90,798 at 7 and 74,200 at 6, all inside 600,000, so 10, the
  highest offered.

### The order: the method fixed, then the nets run once

Everything above was set while only `--look` was run, which traces both
faces, draws the overlays and prints the map's sizes and runs no check (it
did print each face's copper share, so coverage was seen before the method
was fixed; the share moved from 59.8 and 40.0 per cent at the first look to
45.8 and 31.2 at the last, by the pad radius fix and the rim rule, both made
for what the overlays showed). At 19:15:51 UTC on 5 October 2026 the method
was fixed, with the SHA-256 of `board_trace.py`
(`d49305fe68694f2cc14c00d5b072a19d8a7c6fe289653ab23fe4e50a4fb78801`),
`ic-table.json`, `marks.json` and `registration.json` recorded, and the
checks run once:

```
cd tools/nes-model && NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python board_trace.py --map-ppm 10
top: Otsu dL 0.0327; level from 734 of 739 blocks, robust sd 0.0103; copper 45.8% of the board (tin 28.5%, under the lacquer 16.5%)
bottom: Otsu dL 0.0416; level from 832 of 842 blocks, robust sd 0.0109; copper 31.2% of the board (tin 18.4%, under the lacquer 12.8%)
coverage: top 45.8%, bottom 31.2%, print 9.1%: pass
drills in copper: top 99.6%, bottom 99.0% of 480: pass
nets: 10 ICs (U6, U5, U1, U4, U2, U3, U7, U8, U9, U10); excluded []; GND 1 of 10 in one net, +5V 2 of 10; touching True: fail
print 2049 mm2, copper recovered under it 241 mm2; map 10 px/mm, 1959 x 1194, 144774 bytes
```

Nothing in the method was changed after; the script's docstring was tidied
(two sentences re-wrapped and reworded, no code). The run was made once more, the
same command, only to write the rim's figures into `copper.json`, which the
first run printed but did not keep (one line of the output dict): it printed
the same lines, the map came out byte for byte the same, and `copper.json`
differs only by the new `rim` entries.

| Check | Figure | Pass | Verdict |
|---|---|---|---|
| Copper coverage, each face | component side 45.8 per cent, solder side 31.2 | 10 to 50 per cent | pass |
| Drills in copper | component side 99.6 per cent, solder side 99.0, of 480 drills | at least 95 per cent each | pass |
| Known nets | 10 ICs, none left out; GND 1 of 10 in the largest net, +5V 2 of 10; a GND and a +5V pin in one net | at least 8 ICs; 90 per cent each; never connected | fail |

The component side's 45.8 per cent is the nearest a limit: its tinned plane
round the edge alone is 28.5 per cent of the board.

**No IC was left out.** All ten have a footprint with pin 1 from task 3 and
a pinout with its source: U6 and U5 from the nesdev wiki's "CPU pinout" and
"PPU pinout" pages (GND pin 20, +5V pin 40, both as the plan said), U10 from
its "CIC lockout chip pinout" page (GND 8, +5V 16; the page also shows pins
11 to 15 grounded in an NES, not used), U1 and U4 from Hitachi's HM6116 data
sheet (Vss 12, Vcc 24), U2 from Texas Instruments' SN74HC373 data sheet (10,
20), U3 from its SN74HC139 (8, 16), U7 and U8 from its SN74HC368 (8, 16;
the board's TC40H368 is the 74LS368's pinout, as a page on it says, its own
data sheet not found as text), and U9 from its SN74HCU04 (7, 14). Each was
read on 5 October 2026; the board's parts are LS or Toshiba's 40H where the
data sheets are HC, and those families share each pinout.

### Why the nets fail

Looked at after the run, without changing anything: the pieces of copper
that each power pin sits on. Of the 20 power pins, 8 sit on a piece under 3
square mm on both faces, that is the pad alone, and 2 more on pieces under
6. The rim rule cut the tracks from those pads: a track leaving a pad in a
slant, or a pad in a pour joined by short spokes, rarely runs straight on
past the rim for 9 pixels, and the solder side's rim, 9 pixels (0.75 mm), is
wider than the 0.74 mm gap between two DIP pins. Thin tracks are missing too: on the solder side a
track shows mostly as two dark lines (the lacquer's step at each edge) with
copper between them no lighter than the laminate, so lightness does not find
it; inside the PPU's outline the component side's thin tracks are faint
stripes, mostly missed. The pins that do sit on large pieces sit on wrong
ones: U6's GND pin and U1's +5V pin end up in one net. The trace is not good
enough for the nets on this scan, with this method, and the check says so.
What I would try, not done here because it would be tuning after the check:
a track's two dark edges as the cue on the solder side, and the rim rule
replaced by the pads' known outlines from task 3.

### What the print hid

The print covers 2,049 square mm of the component side (9.1 per cent of the
board, under the 15 per cent the site's test allows blue). Copper was
recovered under 241 square mm of it, 11.7 per cent, where tracks cross the
DIPs' outlines and the labels' thin strokes; the wide printed blocks (the
resistors' value boxes, the white box by "MOD RF", the logo's letters over
pours) hide what is under them, and the map shows them blue only. The
sticker hides 157 square mm more.

### What the overlays showed

`out/trace-top.png` and `-bottom.png` draw each face's copper edge in red,
the print in blue and what was recovered under it in yellow;
`out/trace-*-masks.png` the same as flat colours; `out/trace-close-cpu.png`,
`-ppu.png` and `-edge.png` each face round U6, U5 and the edge fingers, the
component side above the solder side; `out/trace-nets.png` the power pins on
both faces' copper.

- Round the CPU: on the component side the wide tracks between U6's rows
  come back as separate pieces with their 3 to 5 pixel gaps kept, under the
  outline's print as well; the pads stand alone, their tracks cut at the rim.
  On the solder side the pins of a row are separate after the rim rule (before
  it, every row was one piece), and the pour to the lower left is traced, its
  pads joined to it only at a few spokes.
- Round the PPU: the outline and the legend are blue; inside the outline the
  component side's thin tracks are mostly missed and one pour on the left is
  found; the solder side's rows of pins stand alone.
- The edge connector: the fingers are tin on both faces and are traced as
  copper, the bare gaps between them as not.
- The board's tinned plane round the component side's edge, the solder side's
  hatched strips and the modulator's area come back whole; the logo and the
  "NES-CPU-10" legend are blue, over copper and laminate alike.

### Mistakes

- The print vanished on the first look run: the pads' discs, sized from task
  3's blobs, included 95 blobs run into planes, up to 201 mm across. Fixed
  before any check, as above.
- The hole fill first looked for dark pixels; in OKLab a drill is not dark.
- The first recovery under the print, along any line, joined tracks under
  U6's outline; the first hysteresis widened tracks; the first rim rule joined
  pads through a track between them. Each was caught on the made-up board or
  an overlay, before the nets check.
- I printed each face's copper share in the look runs, so coverage was seen
  before the method was fixed. It is not the held-out test, and no change was
  made to move it, but it was not hidden from me.

### Tests

pytest first: `tests/test_board_trace.py`, a made-up board drawn as the scans
show it (the probe colours, a 15 per cent fall in the light across it, noise
and a little blur): two nets, GND three pads joined through a via to the
solder side and +5V two pads, 0.4 mm apart; print lines across both nets'
tracks, a 3 mm block of print, a label and a sticker; four tracks side by
side 3 pixels apart under a print line; a row of pads on the solder side with
the lacquer's bright rim round each, a track leaving one and another passing
between two. Run before the code: the module did not exist. Then, as each
rule was added, the new test failed first: the side by side tracks' gap
recovered under the print (then the tracks joined at 1.5 pixels' smoothing),
and the rim test (the row's pads joined, then the track between two pads cut,
then joined to a pad). After: 10 passed; the copper comes back at an
intersection over union of 0.995 on the component side and 0.966 on the
solder side. `/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q`:
70 passed. Python 3.12.3, numpy 2.5.3, opencv-python-headless 5.0.0, Pillow
12.3.0, scipy 1.18.1, pytest 9.1.1.

`cd site && node --test tests/nes-models.test.mjs`: 21 passed, five new: the
map is committed, WebP, the size and SHA-256 `copper.json` gives, inside
600,000 bytes, red and green each 10 to 50 per cent and blue under 15; the
verdicts `copper.json` records are the ones its figures give; coverage and
drills pass (the nets row is not asserted to pass: it failed, as above);
every IC has its GND and +5V pins with a source; and `NOTICE.md` and the
photographs' README name the map, the TAPR Open Hardware License and
OpenTendo. With `copper.json`'s nets verdict set to pass and its map's
SHA-256 changed, an IC's GND pin changed and the licence's name taken out of
`NOTICE.md`, by hand, four failed. `npm test` (with `results.json` copied in
for the run and removed after): tests 323, pass 322, fail 0, todo 1 (the BBC
Micro's). The floors are 322 in both workflows.

### After review, the same day: the record made honest

Nothing in the trace, the map or any figure was changed in this round. The
review compared all 20 GND and +5V pins with the KiCad redrawing's netlist:
the nets check itself is sound (the right pins, the right way round), and
the trace is poor. **The controller's ruling, recorded as such:** the nets
row stays FAILED on record; the track map is accepted as traced to look at,
its connectivity not verified, and every page sentence that describes the
copper must say so; a better re-trace needs a new check fixed before it runs
and is an open item in the plan's Deferred list, not done now. The existing
nets check is no longer held out, since its failure has now been diagnosed.
The plan's Known nets row, task 4's steps 3 and 5, task 7's interface and
the Deferred list carry the dated revision.

**The diagnosis above was incomplete.** It blamed the rim rule and thin
tracks on the solder side. The review found more. I checked the piece's
area (268.8 square mm, the piece U6's GND pin sits on, from the diagnosis
script) and the rim-off figures by running them; the rest is the review's
reading of the overlays and the redrawing:

- The short: a single piece of copper on the component side, 269 square mm,
  joins about 20 pads across U6's lower row and both rows of U1. U6's GND
  pin sits on it, and the chain is how a GND pin and a +5V pin come to share a
  net.
- Missed: whole buses of fine tracks, and the diagonal hatched pours round
  the video RAM (U4). The power pins' own tracks, 1.25 to 1.5 mm wide, and
  their pours are partly missed too. On the solder side, wide tracks no lighter
  than the laminate are not found, not only thin ones.
- With the rim rule turned off (a scratch script that makes `keep_off_rims`
  return its input and writes nothing; run on 5 October 2026 with the same
  inputs) the check still fails: GND 3 of 10 pins in the largest net, +5V 5 of
  10, still touching (coverage 50.9 and 40.0 per cent). So the rim rule is not
  the whole cause.

**Coverage, every reading I saw.** The `--look` runs printed each face's
copper share. In order, the component side read 59.8, 59.8, 51.8 and 50.9
per cent (the solder side 40.0 each time), then 45.8 (solder side 31.2) on
the run that finished at 19:15:08 UTC, the first with the rim rule in; the
method was fixed at 19:15:51. So the coverage row was over its 50 per cent
limit on the component side before the rim rule went in, and the rim rule
that brought it under is the same rule this section blames, in part, for the
nets failing. The coverage pass does not show the trace is right: merges
(the 269 square mm piece) and misses (buses, hatched pours) partly cancel in
a share of the board.

**The made-up board does not predict the scan.** The tests' board has no
hatched pour, no dense bus, no track shown only by its edges and no thermal
spoke, so its intersection over union of 0.995 says nothing about the real
scan. Dan should see the map against the scan before task 7 shows it.

**The script's hash.** The copy fixed before the nets run had SHA-256
`d49305fe68694f2cc14c00d5b072a19d8a7c6fe289653ab23fe4e50a4fb78801`. The
copy committed with task 4 has
`e368c877385d92679485a740d4782bf44d3e003e90c56ff0bc40a59d48c5845c`. The
fixed copy was not kept. The statement that the two differ only by the
docstring and the rim's output field rests on the committed code reproducing
every recorded figure, which it does: run again it prints the same lines and
writes the same map, byte for byte.

**One output change.** `copper.json`'s `pinNets` stored each net as Python's
text for a tuple, such as `('b', 795)`. `board_trace.py` now writes a plain
label, `bottom:795`, and was run once to rewrite the file (`cd
tools/nes-model && NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10
/tmp/nesvenv/bin/python board_trace.py --map-ppm 10`): it printed the same
seven lines as before; the map's SHA-256
(`f78d9a5408de8227e9abd9603d5f461d3671d6ba3c9c7d9db4434e612b76144e`) and
size (144,774 bytes) are unchanged; and every entry of `copper.json` but
`pinNets` is identical, the labels mapping one to one.

`NOTICE.md` and the photographs' README now say the copper is traced to look
at, its connectivity not verified. The site test that let the nets verdict
be either value is replaced by one that pins it to FAIL with its figures and
reads the plan's revision; with the verdict set to pass by hand, and with
the revision's words taken out of the plan by hand, it failed.

## Task 5: the parts, placed from the scan and named for both consoles

`tools/nes-model/board_parts.py` puts every part on task 3's footprints in
task 2's frame, names each console's parts from the photographs, and writes
`data/parts.json`, each console's part into `data/ic-table.json`, and the
module the page will read, `site/src/models/nes-famicom-board-parts.mjs`.
`run-board.sh` now ends with `verify.py I3 I4 I5` and `board_parts.py`. The
venv as before: Python 3.12.3, numpy 2.5.3, opencv-python-headless 5.0.0.93,
Pillow 12.3.0, scipy 1.18.1, pytest 9.1.1.

### Decisions

- **An IC's place is its pads' centre, its turn from pin 1** to the last pin
  of pin 1's row. A footprint whose pad count is not its part's pin count is
  reported and not placed (none was).
- **The RAMs sit on the 300 mil footprints.** U1 and U4 each have two
  footprints (task 3). On I4 the NTSC RAMs read MB8416A-15-SK, about 7 mm
  wide, and the PAL RAMs on I3 are XRM6216-10 on the 300 mil rows (task 0).
  Held out on I4 against the 600 mil footprint's midline instead, the RAMs
  are 3.87 and 3.66 mm off, against 0.11 and 0.20 mm on the 300 mil ones.
- **Each photographed board on the scan, by a homography and parts held
  out.** I4 (top side, NES-CPU-07): at every pair of lead columns across a
  DIP, the leads' middles along the row and the body's two long edges, the
  midline half way between them; that is the midline of the two pins' pads
  on the scan. I5 (solder side): every pin's joint, the centre of its
  colourless disc (the lacquer is green). I3 (PAL): task 0's marks, the
  pins' feet, so task 0's fit is reused as it was asked. Four pins of U6
  marked by hand start I4 and I5; the fit then grows part by part. Each IC
  is then held out in turn: the homography on the others, its points through
  it, the mean against the mean of its pads. **The limit for "sits on the
  footprint of its name", 2.0 mm, was set before any figure was seen**: the
  plan's PAL layout row's "none over 2.0".
- **U7 and U8's midlines by hand on I4.** Their MN74HC368 leads show grey
  between two highlights, not as light flats, so the lead finder found two
  to four leads a row; on the first look U7 and U8 had too few columns to
  hold out. Their midline ends were then read by hand on crops enlarged four
  times (`marks.json` `parts.I4.midlines`, with the body edges read). This
  was done after seeing the finder fail on them, not after any held-out
  figure for them.
- **`chip` and `always`.** U6 `apu` (its sound unit), U5 `ppu`, U7 `pad1`,
  U8 `pad2`; U1 and U4 `ram`, U2 `latch`, U3 `decoder`, U10 `lockout`.
  U9, the hex inverter that runs the master clock's oscillator, had no
  reason in the plan's list, so it carries `clock`, and the plan's task 5
  interface says so, dated. That U7 is port one and U8 port two is from the
  print alone, "40H368(CI)" and "40H368(CII)"; `ic-table.json` marks it
  `inferred` and task 6 checks it.
- **Body sizes from I4, per package.** Each IC's body was measured on I4
  (three lines along it and three across, through the fit, the dark
  colourless run through the middle, the marking's letters bridged), and
  the model draws each package at the median of its parts: DIP-40 53.5 by
  14.3 mm, DIP-24 31.8 by 7.1, DIP-20 26.3 by 7.3, DIP-16 20.7 by 6.6, DIP-14
  19.6 by 7.1. A body's centre is not its pads' everywhere: U5, U1 and U4
  read about 1 mm to the right of their pads' centre and U8 1 mm to the left;
  recorded (`bodies.onI4`), not used, the place staying the pads'.
- **Heights are typical, not measured.** I7, the oblique photographs, was
  not fetched; the plan allows typical heights where it is not. Every entry
  in `HEIGHTS` says `measured: false` and what it is.
- **Connectors, crystals and modulators from outlines marked on the
  photographs** (`marks.json` `parts.I4.outlines` and `parts.I3.outlines`),
  each through its photograph's fit. That fit is the board's plane, so a
  tall part's top, which is what an outline from above shows, leans away
  from the middle of the picture: on I4 (a 60 mm lens, about 0.8 m up) a
  15 mm top 100 mm out leans about 2 mm. Not corrected; said in the marks.
- **Passives by hand.** Each two- or three-lead part fitted on I4 was found
  at the holes the CPU-10's print gives its reference, on I1-front
  rectified at 16 px/mm with a 1 mm grid and the scan's free holes numbered,
  then drawn back on I4 through its fit and looked at. 70 parts, plus X2;
  each lead is moved to the scan's pad within 0.6 mm (`leadMovedMm`; null
  where no pad was found and the read place is kept). Typical bodies by kind.

### Identities, each a crop looked at

Every crop is of the original, cut by `/tmp/t5/crop.py` with a grid
labelled in the original's pixels in strips outside the crop.

| Ref | NTSC on I4: crop (box, zoom), what it showed | PAL on I3: crop, what it showed |
|---|---|---|
| U6 | 300,1240,1420,1600 x1: RP2A03G, 8A2 B7 | 624,1541,1755,1872 x1: RP2A07A, 1HM 2V; I6 whole: the same |
| U5 | 1850,980,2920,1340 x1: RP2C02G-0, 7M3 52 | 2243,1326,3374,1638 x1: RP2C07-0, 1GM 3U |
| U1 | 740,1800,1380,2000 x1: Fujitsu's mark, MB8416A-15-SK, JAPAN, 8804 KR05 | 1053,2126,1755,2321 x1: XRM6216-10, 126 10963 |
| U4 | 1860,1850,2500,2050 x1: as U1 | 2223,2184,2926,2379 x1: as U1 |
| U2 | 1860,1440,2400,1640 x1: SN74LS373N, Motorola's mark, IICW8805S | 2262,1755,2809,1950 x1: Fujitsu's mark, MB74LS373, MALAYSIA 9122 KF69 |
| U3 | 330,880,780,1090 x1: J805XJL, SN74LS139N (a capacitor over its corner) | 663,1170,1111,1365 x1: SN74LS139N, XJAJ9128 |
| U7 | 2480,330,3000,860 x1: MN74HC368, 8 1 0; the print 40H368(CI) | 2945,643,3394,838 x1: Motorola's mark, MC74HC368N, JJBA9126A |
| U8 | the same crop: MN74HC368; the print 40H368(CII) | 2945,897,3394,1092 x1: as U7 |
| U9 | 2990,1180,3410,1400 x1: TOSHIBA 8807H, 74HCU04AP, JAPAN | 3433,1482,3881,1658 x1: TI's mark, SN74HCU04N, MALAYSIA 128ER |
| U10 | 2590,1540,3040,1760 x1: 3193A, (c) 1986 Nintendo, 8744 A | 3023,1872,3472,2048 x1: 3195A, (c) 1986 Nintendo, 9133 C |
| X1 | 1100,300,1560,640 x2: blue, "21.47727 KDS 8A" twice, the print X'tal | 1480,560,1900,840 x2, then 1580,640,1860,840 turned a quarter and x3: orange, "26.601712 KDS 1G" twice, upside down |
| X2 | 3040,1480,3480,1900 x1: a white block on X2's three holes, the print 4.000MHz | 3500,1800,3950,2250 x1: the same white block |
| P3 | 3300,1450,4570,3330 and 2600,1900,3500,3330: the modulator's frame, lid off, its board seen, no part number | 3500,1700,5462,3966: a closed can, "ALPS" embossed |

The PAL crystal's value was read, not taken from anywhere: 26.601712. The
NTSC crystal reads 21.47727, where the KiCad redrawing gives 21.477272 MHz;
both are in `ic-table.json`.

**Where the photographs and the CPU-10 disagree.** The CPU-10's print and
the redrawing call U7 and U8 40H368; the CPU-07 has MN74HC368s. The print
says 74LS373 and 74LS139 where the redrawing says 74HC373 and 74HC139, and
74HCU04P where the redrawing says 74LS04; the photographs agree with the
print. Printed on the CPU-10 and not fitted on the CPU-07 (I4): R14, R15,
R16, R17, C6 and C7, their holes bare. Where the CPU-10 has C2's two holes
5.2 mm apart, the CPU-07 has an axial part on holes about 8.7 mm apart: a
difference between the revisions, not drawn.

### What it measured

`cd tools/nes-model && NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10
/tmp/nesvenv/bin/python board_parts.py`, 5 October 2026, exit 0, after
`verify.py I3 I4 I5 I2` (all present, hashes match). Run twice; the second
run changed no file. It printed:

```
pad counts: pass
KiCad places: pass
CPU-07 parts on the CPU-10 footprints: pass
IC bodies inside the outline: pass
IC bodies apart: pass
KiCad places: max 0.438 mm, median 0.292, scale 0.99462, turn -0.262 deg
held out (mm), I4 / I5 / I3: U6 0.056/0.013/0.297; U5 0.153/0.038/0.066; U1 0.112/0.033/0.348; U4 0.197/0.060/0.280; U2 0.084/0.068/0.071; U3 0.230/0.008/0.346; U7 0.432/0.031/0.082; U8 0.246/0.007/0.038; U9 0.138/0.037/0.102; U10 0.302/0.099/0.217
fits: I4 98 points, median 0.127; I5 199, 0.054; I3 39, 0.170
U1 and U4 on the 600 mil footprint instead: 3.87 and 3.66 mm off
connectors and crystals on I5: P1 0/72 (sits on it); P2 47/48 (sits on it); P3 0/5 (sits on it); P4 3/7 (sits on it); P5 1/7 (sits on it); P6 0/5 (sits on it); X1 1/4 (sits on it); X2 1/3 (sits on it)
overhang: ['P1', 'P4', 'P5', 'P6', 'P3 (ntsc)', 'P3 (pal)']; IC bodies outside: 0; clashes: []
ICs 10, connectors 5, passives 71, others 2 + 2
```

- **The KiCad places check passes**: every IC within 0.438 mm (U5) of the
  redrawing's place after the best-fit similarity, against the plan's 3 mm;
  median 0.292. With U7 and U8 swapped (12.7 mm apart) the same check puts
  them 12.31 and 13.11 mm off and fails (computed on 5 October 2026 with
  `board_parts.kicad_check` on `parts.json`, the two places swapped); the
  pytest on made-up places and the site test with the two swapped by hand
  both fail on it too. The connectors and crystals through the
  same similarity, recorded and not judged, are within 0.90 mm (P3).
- **Every CPU-07 part sits on the CPU-10 footprint of the same reference.**
  The largest IC error held out is U7's on I4, 0.432 mm, from its hand
  marks, against 2.0; on I5 none is over 0.10 mm. The joint finder found
  fewer joints than holes at P2, P3, P4, P5, P6, X1 and X2; each was drawn
  on I5 and looked at, and every hole has its joint (the finder missed P2's
  pin 3, whose highlight runs off its disc). P1 has no holes: on I4 its body
  lies across the fingers, its middle 0.9 mm right of theirs.
- **Bodies over the outline.** The cartridge connector, the controller and
  power headers and both modulators run past the board's edge, as the
  photographs show them. The site test takes "every part is inside the
  outline" as: every IC's body, every passive and every pad of a connector,
  crystal or modulator inside it, and a body that runs over it is one
  `parts.json` records.
- **The two modulators.** The NTSC frame (I4) reads x 158.3 to 221.1 mm,
  y 95.3 to 155.4; the PAL can's top (I3) x 162.2 to 225.1, y 95.9 to 158.4,
  about 4 mm to the right. I3's 20 mm lens leans a 22 mm can's top by several
  millimetres, but a lean would grow with the distance from the middle of the
  picture and this shift does not, so the photograph cannot say how much is
  lean and how much the can's own place. Recorded as read.
- **The PAL crystal** is drawn as seen on I3: its lower edge is hidden by the
  expansion socket's frame, which stands taller and leans over it, so its
  box (8.9 mm across the board) is shorter than the NTSC one (11.5 mm).

### Mistakes

- The first full run gave the KiCad check a failure of 448 mm: the places
  were the pads' list positions in `registration.json`, not their places.
  A bug in passing the footprints, fixed; the check itself did not change.
- The KiCad reader skipped surface pads, so P1, whose fingers are surface
  pads in the redrawing, had none. It now takes them where a footprint has
  no plated holes.
- Writing `ic-table.json` through `write_data` sorted its keys and rewrote
  the whole file; it is now written in its own order.
- Three of the made-up tests were wrong as first written: a moved part's
  neighbours were held to 1 mm when they carry some of its error through
  their fits (now: only the moved part over 2.0, and it the largest); two
  bodies that touch were counted as overlapping; the upper row's predicted
  places were in the wrong order.

### Tests

- `tests/test_board_parts.py` first: RED, `ModuleNotFoundError`. Then 15
  pass and 3 fail (the three test mistakes above), then 18 pass.
  `/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q`: 88 passed.
- `site/tests/nes-models.test.mjs` gains 8 tests: pad counts and places;
  each console's part is `ic-table.json`'s; `chip` or `always`, `COUNTED`,
  U7 `pad1` and U8 `pad2` inferred; inside the outline; no overlap; the
  KiCad places, the similarity fitted again in the test; each CPU-07 part on
  its footprint; the module agrees with `parts.json`. Each was seen to fail
  on a hand edit of a copy, then restored (`cmp`): U7 and U8's places
  swapped (3 fail, the KiCad test among them), U7's chip set to `pad2`
  (2), the PAL PPU named RP2C02G-0 (2), U2 moved onto U5 (4, the overlap
  test among them), a passive moved off the board (1), U6's pad count set
  to 39 (1), the module edited by hand (1).
- `npm test` (the build first; `results.json` copied in and removed
  afterwards; both machines' WebAssembly already built): tests 331, pass
  330, fail 0, todo 1 (the BBC Micro's, as before). Floors raised from 322
  to 330 in both workflows.

### After review, the same day

**A correction: U9 does not run the master clock.** Above, U9 is "the hex
inverter that runs the master clock's oscillator", and it carried
`always: 'clock'`. That was wrong: I had not checked it. The review read
the KiCad redrawing's nets, and I read them again with a scratch script
(`common._sexpr` on I2, each pad's `net`, and every other pad on that net):
X1's two pins are on Q2's collector (with C42, C45, Q3, R10 and R11) and on
C41 with the trimmer TC1, a discrete transistor stage; U9's six inverters
are on X2, C7 and R1 (pins 1 and 2: the 4 MHz resonator's oscillator), on
`/CIC-CLK` (pin 4, to the cartridge and the expansion port) and U10's
`CLK_IN` (pin 12), on `/PPU-A13` in and `/PPU-~{A13}` out (pins 5 and 6,
to the cartridge), on `/~{RST}` (pin 9, to R5 from pin 8) and on
`/EXP-AUDIO-OUT` (pins 10 and 11, with R6 across them). So U9 runs the
lockout chips' clock, inverts the PPU's address line A13 and the reset line,
and amplifies the expansion port's audio. These are the redrawing's nets, a
cross-check, not traced on the scan. U9 is in use whenever the machine runs,
so `always` was right; its value is now `'inverter'`, in `board_parts.py`,
the site test and the plan, whose task 7 legend now says it in words.
`board_parts.py` was run again (`cd tools/nes-model &&
NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python
board_parts.py`, exit 0, the same lines as above): in the data files only
U9's `always` changed, and `sitsOn.what`, for the next point;
`ic-table.json` is byte for byte the same.

**I3 is judged too.** An IC now sits on the footprint of its name only if
its held-out error on I3, the PAL board, is also within 2.0 mm; the largest
is U1's 0.348 mm, so nothing moved. The site test holds it; with U3's I3
error set to 2.5 mm by hand, it failed.

**The overhang test can fail on its own.** Each body that runs past the
outline must now cover every pad of its own footprint, and its nearest pad
must be within 15 mm of the edge. I chose 15 mm after seeing the distances:
1.7 mm (P1), 4.8 (P4), 4.9 (P5), 8.9 (P6) and 11.9 (P3, the modulator's
pins); an IC's or the expansion socket's nearest pad is 18 mm or more in.
With P1's body slid down to y 135 mm, still over the edge but off its
fingers, it failed.

**Three connectors rest on a look, not the finder.** For P1, P3 and P6 the
joint finder found none of the joints (0 of 72, 0 of 5 and 0 of 5), so their
"sits on it" rests on the hand look recorded in `marks.json`
`parts.I5.looked`: P3's and P6's joints seen on I5 under each hole's cross,
P1's body seen on I4 lying across its fingers.

The docstring's step 1 said the bodies were each package's typical size; it
now says what the code does: measured on I4, the median of each package's
parts.

## Task 8: the case, measured for both consoles

`tools/nes-model/case_measure.py` measures the outside of the NTSC NES-001
and the PAL NESE-001, and writes `data/case.json` and the module the page will
read, `site/src/models/nes-famicom-case-parts.mjs`. `run-case.sh` runs it
after `verify.py` on its inputs. The venv as before: Python 3.12.3, numpy
2.5.3, opencv-python-headless 5.0.0, Pillow 12.3.0, scipy 1.18.1, pytest
9.1.1; `pdfimages` 24.02.0.

The NES machine's pull request (#55) has not merged, and it edits the NES row
of `machines/registry.json`, so this run did everything that is offline and
left out what needs the registry; the list is at the end of this section.

### The inputs fetched

Fetched by hand on 5 October 2026 with Commons' API (`prop=imageinfo`,
`iiprop=url|size|sha1|extmetadata`), each licence read from its
`LicenseShortName`, each file checked against Commons' SHA-1 before its
SHA-256 was taken, and kept out of the repository with the others:

| Id | What | Author | Licence, as stated |
|---|---|---|---|
| O3-01 to O3-04 | Evan-Amos's NES-001 opened in stages | Evan-Amos | Public domain |
| I7-FL, I7-FR, I7-Bottom | The NTSC board with its modulator, from two corners and from below | Evan-Amos | Public domain |
| O9 | HOF06601: the PAL bottom shell from above, the board in it | PantheraLeo1359531 | CC BY 4.0 |
| O10 | HOF06440: the PAL bottom shell from the rear | PantheraLeo1359531 | CC BY 4.0 |

Their sizes and hashes are in `sources.json` and the facts sheet. O2-FR was
fetched to look at and not used, so it is not listed. The 46 files in the
PAL set's Commons category were looked at as thumbnails first; O9 and O10 are
the two that show what O3 does not.

### Decisions

- **The size is the published 254 by 203.2 by 88.9 mm,** as the plan's second
  revision says, stated as not Nintendo's. The 88.9 is taken as the body
  without its feet: on the patent the height without the feet is within 1.4
  per cent of it, with them 4.6 to 6.4 per cent over. The feet, 3.96 mm, are
  read off FIG 3 and scaled with its height.
- **One face at a time.** Task 0 found O2's camera fails its own check, so
  nothing here relates two of a photograph's planes through a camera. Each
  face is rectified on its own by the homography that takes its four
  bounding lines to the published rectangle. A homography is exact for points
  on that face whatever the camera was, and whatever perspective correction
  Photoshop may have made, since that is a homography too; only lens
  distortion breaks it. Task 0 did the same for the door's width.
- **Small features are read off the rectified faces.** I first marked them as
  polygons on the original and mapped them; the overlay showed the buttons'
  and ports' boxes several millimetres out, my polygons being poor. So the
  script draws each rectified face at 8 px/mm with a millimetre grid
  (`--look`), and the features are boxes in millimetres read off that, about
  0.3 mm. The long edges are still lines fitted as task 0 fits them.
- **What stands out of or back from a face reads off its place.** The buttons
  stand about 1 mm proud of the front (FIG 5), so their holes in the panel,
  which lie in the face, are taken. The rear window's panel is set back: its
  base reads 4.39 mm above the base, and along the top's far short edge,
  which runs square to the face, that is a shift of 3.45 mm along the face,
  which the script takes off every point on the panel. Without it, the three
  rear openings read 3.5 mm off where the patent's FIG 4 draws them; with it,
  within 1.3 mm.
- **Photographs first, the patent where they cannot see.** The underside (no
  NTSC photograph of it was used), the AV jacks' side (O2-FL sees it nearly
  edge on), the feet and the notch's height come from the patent's views,
  each axis scaled to the published size on its own. Everything else is from
  the photographs, with the patent's figure recorded beside it in
  `case.json`.
- **The profile is the end seen from the front.** The patent's side views show
  the front and rear faces upright, so the side outline is a rectangle; what
  varies is the bottom shell's ends, which lean in below a break. The plan's
  `profile: [{y, z}]` became `[{inset, z}]`, noted in the plan.
- **The board lies solder side up, its fingers to the rear.** I7-Bottom's
  Commons description says the solder side faces up when the console is taken
  apart; O3-03, O9 and I7-FL agree, and put the modulator, so the rear jacks
  and the AV jacks, at the rear right. Board (bx, by) is at case (x + bx,
  y - by).
- **The board's offset from O9, not O3.** None of O3's four photographs shows
  the whole board and the case together: the cartridge tray or the shield
  covers the board, or it is out. O9 shows the PAL board in the PAL bottom
  shell from above, and its XMP records an ILCE-7RM4 at 20 mm, a crop of the
  whole 9504 by 6336 frame (CropLeft 0.173395, CropTop 0.232919) with no
  resize and no perspective correction, the lens profile applied. So its
  camera is known: f 5324.4 px, the principal point at the frame's centre less
  the crop's origin. The rim's homography with that camera gives the vertical
  vanishing point (the nadir). A point on the board, lower than the rim, reads
  through the rim's homography as F + k (P - F), F the nadir on the rim's
  plane, so the board's 48 points (the ten ICs' end pins and the eight holes
  not under the cartridge connector, found as blobs near a seed of two holes
  marked by hand) give the offset by a similarity. The PAL and NTSC boards
  share the layout (task 0) and the shells the moulding [inferring].
- **The labels are words.** "Nintendo" and "ENTERTAINMENT SYSTEM" are written
  as words, to be drawn in the site's own face; the registered and trade
  marks beside them and every logo (the house, TÜV, the Bundespost's horn,
  pkm) are left out.

### Thresholds, set before the figures

The plan's: the profile's held-out point within 2.0 mm, else the patent's
FIG 3 outline scaled to the case; each rear connector within 2 mm of its
board place plus the offset. The profile's check point (the inset at the
base on O2-BR's rear, held out of the profile built on O2-FL's front) was
written into the script's docstring before either was run. That the profile
averages O2-FL's two ends was decided after O2-FL's two ends were seen to
differ (17.6 and 14.4 mm) and before O2-BR was measured.

### What it measured

Measured on 5 October 2026 with `cd tools/nes-model &&
NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python
case_measure.py`, which printed:

```
profile from photographs: held-out 0.71 mm (limit 2.0)
board in case: x 24.39 y 164.24 z 37.84 mm, turn 0.24 deg
rear check: 1 of 3 within 2.0 mm, worst 5.16
  AC ADAPTER: board plus offset 230.19, O2-BR 225.03, O1 225.02, 5.16 mm
  CH3-CH4: board plus offset 208.85, O2-BR 205.35, O1 204.99, 3.5 mm
  RF SWITCH: board plus offset 192.05, O2-BR 191.2, O1 192.49, 0.85 mm
width cross-check (O9): depth to width 0.7813, -1.96 per cent
```

Run twice; the second run's `case.json` and module were byte for byte the
first's (`cmp`).

**The rear connectors' check fails, as measured: one of three within 2 mm,
the worst 5.16.** The board's places come from I7-FL's view of the
modulator's rear face, rectified on its four corners at the modulator's
width that task 5 measured on I4 (62.8 mm, its outline from above with the
lid off), plus O9's offset. O2-BR and the patent's FIG 4 agree with each
other within 1.3 mm on all three, and put the jacks about 13 per cent closer
together than I7-FL does at that width. So the likeliest cause is the width
I7-FL's face was scaled to, not the offset [inferring]: the RF jack, nearest
the face's left end, is within the limit, and the error grows along the
face. The model places the three where O2-BR shows them, records the board's
places beside, and `case.json`'s `rearCheck` says so; that choice was made
after the figures were seen. It is reported to the controller to rule on.

**The profile passes: 0.71 mm** against 2.0. Each end, the break (where the
end starts to lean, above the base) and the inset at the base:

| Photograph, end | Edge it shows | Break | Inset |
|---|---|---|---|
| O2-FL, the case's left | its silhouette | 35.96 | 17.60 |
| O2-FL, the case's right | the end turning away | 46.21 | 14.40 |
| O2-BR, the case's right | its silhouette | 37.88 | 17.71 |
| O2-BR, the case's left | the end turning away | 42.92 | 12.86 |

The two silhouettes, on opposite ends in two photographs, agree within 0.11
mm; the two ends seen turning away read 3 to 5 mm less. The edge between the
front and the end is rounded, and a silhouette and a change of light find
different places on it. The averaged profile (a break at 41.09 mm above the
base, clamped below the seam, and 16.00 mm at the base) is what the rule as
written gives, and the check passes on it; the patent's FIG 3 gives about 15.5
at the base (its outline 0.88 mm up) and a break near 36. Recorded, not
judged further.

**The board in the case:** its top left corner (as the scan lies) at x 24.39,
y 164.24 mm, turned 0.24 degrees; its solder face 10.73 mm below the rim,
37.84 mm above the table. On O9 the image to board homography's held-out
error is 0.20 mm median (0.73 the worst, U1's pin 1), all 48 points found;
the similarity leaves 0.46 mm median; moving the principal point 100 px any
way moves the place 0.20 mm. The camera stands 308.8 mm over the rim.

**The width cross-check:** on O3, not possible (above). On O9, the rim
through the XMP's camera has depth to width 0.7813, 1.96 per cent under the
published midpoint 0.797, beside the patent's top view's 0.772. Its width
against the board's 196.252 mm cannot be had from one view: the board lies
about 11 mm below the rim, and that depth and the rim's true size trade
against each other.

**The photographs against the patent**, front, places along the face: the
door's left side 33.32 against 34.47, its width 146.78 against 146.06; the
LED at 33.40 against 33.02; the ports at 183.40 and 201.75 against 183.65 and
201.91; the rear window 172.5 wide 61.5 against 173.24 wide 60.84. Where they
differ: the patent's POWER and RESET are 27.7 and 27.0 wide against the holes'
24.8; its panel 75.6 wide against 69.1; the ports' frames 23.8 tall against
26.2; the rear window's top 1.5 mm higher. The top: the black band's left
edge 181.48 against 179.01; the vents run from 38.48 to 164.37 mm from the
rear (the patent 38.84 to 163.56), with 20 gaps between slats on O2-FL's top
against 21 bars drawn on FIG 5.

**The PAL underside:** O5 rectified on its six screw holes against FIG 6's
places, held out one at a time: 2.20 mm median, 3.63 the worst. O5's feet
fall within 2.3 mm of FIG 6's. The labels' places come from it.

**POWER's latch:** not shown. O2-FL, O3-01 to O3-04 and the PAL set all have
both buttons out, and O3-01's view of the switch board (two push switches of
different sizes) does not say which latches. The model shows POWER in while
the machine runs, as the design says [guessing - verify].

### What the overlays showed

`--look` draws each rectified face with what was read off it in magenta and
the patent's boxes in cyan, O9 with its rim and every point found, and the
rectified faces with millimetre grids. Looked at, they showed three marks
wrong, each fixed before any check was read:

- O2-FL's left end: the fit followed the inside of the rounded edge, about 11
  px in from the silhouette; the segment was marked again on the silhouette
  with a narrower search.
- O2-FL's right end: the fit wandered (rms 8.3 px) on the soft change of
  light; marked again above the moulded slot at its foot, with a 6 px search
  (rms 3.2).
- O2-FL's top: the black band's right edge fitted across the vents' ends (rms
  9.5 px); marked again along the band's rear black end only.

O5's outline lines fitted poorly on the carpet (rms 3 to 15 px), and the
outline is the seam's, about 45 mm further from a 19 mm lens than the
bottom; so O5 is rectified on its screw holes instead, which lie in the
bottom.

### Mistakes

- The first O2-BR label polygons were typed from the wrong crop's scale (px
  = 400 + (sx - 40) / 1.5 read as 1.0); caught on the overlay, replaced by
  boxes read off the rectified rear.
- `ribCount` was first written `ribs_count`, out of the file's style.
- The site test's first word check matched "logo" in the module's own note,
  which said there was none; the note now just says the labels are words.

### Tests

pytest first: `tests/test_case.py`, 13 tests on made-up scenes (a stepped
box from two corners by a known camera; a face rectified on four fitted
lines; the board in the case from one view with its nadir, from three
camera places, and without it to show the nadir is needed; ports placed by a
known offset; the profile's choice; words only; the module). RED:
`ModuleNotFoundError: No module named 'case_measure'`. GREEN: 13 passed.
The whole tool suite: `/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests
-q`, 101 passed.

Site, in `site/tests/nes-models.test.mjs`, nine tests: the size is the
published figures and agrees with `spike.json` and its verdicts; every
feature lies on the case; the buttons, LED, ports and rear connectors are in
the photographs' order; the rear check is recorded as measured and its
verdicts agree with its figures; the profile check or its fallback; the
labels are words, with no image, path or logo in the module; the PAL
differences are listed; POWER's latch is marked a guess; the module agrees
with `case.json`. Each was seen to fail on a hand edit and the files were
restored (`cmp`): the height typed as 89, POWER and RESET swapped, a label
given a path, the profile's check set to fail, a rear error changed, the PAL
differences emptied, the latch's guess removed, the LED moved off the case,
the module edited by hand.

`cd site && npm run build && npm test` (results.json copied in from the main
checkout for the run and deleted after; both machines' WebAssembly already
built): tests 340, pass 339, fail 0, todo 1 (the BBC Micro's, already
there). The floors go from 330 to 339 in both workflows.

### Left for after the machine merges

Not done in this run, by the controller's ruling, because the NES machine's
pull request (#55) has not merged and edits the NES row of
`machines/registry.json`:

- step 4: resize and commit three photographs under `site/src/assets/photos/`
  (O2-FL, O2-BR and one PAL photograph, `cwebp -q 82 -resize 1600 0
  -metadata none`);
- their sections in `site/src/assets/photos/README.md`, with both SHA-256;
- their entries in the NES row's `photos` in `machines/registry.json`, each
  with its credit, `fetched`, `used` and alt text;
- `committed` set for each in `tools/nes-model/data/sources.json`;
- step 5's site test that the new photographs are committed, credited and
  match their README hashes, and the floors raised again for it.

The same list is in the plan, under task 8.

### After review, the same day

The review found the work sound and asked for the uncertainty to be said
where it is, not only in the figures. **The controller ruled** on the rear
connectors: the check is accepted as failed, as measured (one of three within
2 mm); the model keeps O2-BR's places, which agree with the patent's rear view
within 1.3 mm, since the plan's step 3 names the photographs as the source and
the board as the check; the page will state the uncertainty. No recheck now
and nothing rescaled to fit. A recheck needs the modulator face's width
measured on its own first, with its limit fixed in the plan before it runs;
it is in the plan's Deferred list.

What changed, all added fields, by one run of `cd tools/nes-model &&
NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python
case_measure.py`, which printed the same seven lines as before; a structural
diff of `case.json` against the previous run shows only fields added, the two
notes changed and the patent profile's first point taken out:

- `rearCheck.words`, also at the end of the module's `REAR.note`: "O2-BR and
  the patent's rear view agree within 1.3 mm; the board's places miss by up to
  5.2 mm; the check failed as measured". `rearCheck.uncertaintyMm` has
  `placesUsed` 1.3 (the largest difference between O2-BR's places, which the
  model uses, and FIG 4's: the RF jack's, 1.29 mm) and `boardMiss` 5.16.
- `profileCheck.endsMm` is 12.86 to 17.71: the four ends at the base, which the
  held-out figure hides. The profile's 16.0 is O2-FL's two ends averaged (all
  four average 15.6), good to about 2.5 mm; the 0.71 mm held-out figure shows
  that the averaging repeats on a second photograph, not that 16.0 is the true
  inset. `PROFILE.note` says so. The 16.0 and the rule are unchanged: both were
  fixed before O2-BR was measured.
- `boardInCase.uncertaintyMm` is 2: the similarity's worst residual, 1.25 mm,
  plus the 0.20 mm the place moves when the principal point is put 100 px out,
  rounded up to a whole millimetre. The shared-moulding assumption is not in
  that figure and stays flagged. The rim's scale ratio, 1.0239, is named as O9's
  own camera check.
- Every rear entry carries `checked`: true for the NTSC console's three on the
  rear, false with the reason for the AV jacks (no photograph shows the
  modulator's side square enough) and for the PAL rear (the PAL modulator's
  jacks are never photographed face on). The module carries both.
- The patent profile's row at the base itself (17.62 mm) is the bottom
  corner's rounded edge, not the end's lean, and is now left out of
  `profilePatent` and recorded under `profilePatentLeftOut` with the reason.
- **O2-BL is not used for the rear connectors,** though step 3 names it: O2-BR
  was read first and agreed with the patent's FIG 4 within 1.3 mm, and O2-BL
  was not read in this run, so that agreement rests on one photograph and the
  patent.

The rear-check site test now pins the figures as measured (1 of 3 within, the
worst 5.16, each connector's 5.16, 3.50 and 0.85) and the plan's sentence
"The rear connectors' check failed as measured", as task 4's nets test is
pinned: a re-run that moved them must change the words too. With the CH3-CH4
jack's figures edited to a consistent pass (its board place moved so that
every recomputation agreed), only the pin failed; with the plan's sentence
changed, it failed; with `endsMm` changed, the profile test failed. Each file
was restored and compared (`cmp`).

## The machine's id changes, 6 October 2026

The session doing the NES machine told this one that the registry id will be
`nes`, not `nes-famicom` (the planned row is replaced in that track's task 15),
and that a model module must be `nes` or start with `nes-`. The module names
here, `nes-famicom-case` and `nes-famicom-board`, start with `nes-`, so they
stay: renaming the committed files would touch about a dozen of them for no
gain. The plan now says the page is `/machines/nes/` and a model's `machine` is
`nes`, with a dated note under "Facts every task relies on". The approved design
still names `/machines/nes-famicom/` in two places; it is not edited, and Dan can
fold the change in. The made-up machine in task 1's test keeps its own id.

Dan then asked for the wording to be fixed where needed, so the approved design
now says `/machines/nes/` (it had that path once, not twice as the note above
said) and its legend sentence names the hex inverter, U9, beside the other
chips that are never marked, with its jobs credited to the redrawing's nets.
