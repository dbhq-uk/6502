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

**Done 6 Oct 2026**, once #55 had merged into this branch: see "Task 8 follow-up" at the end of this entry. Three photographs, not the two NTSC and one PAL the list names, and why is there.

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

## The tabs: the NES builds them itself, 6 October 2026

Dan asked what was needed to go on. Checked on 6 October 2026 with `gh pr view`,
`git log` and `git grep`:

- **#55 (the NES machine)** is a draft, mergeable, with green CI (dashes,
  machines, site and test), "Not for merge until Dan says". Its 16 tasks are
  done. It already holds the 11 commits of the BBC Micro models branch, so the
  registry's `case` and `models` rules arrive with it.
- **#48 (the BBC Micro's models)** is a draft, conflicting with `main`, last
  updated 4 October. It holds its tasks 0 to 3 only. `git grep tablist` finds no
  tab markup on that branch or on #55. So the plan's rule that the NES waits for
  the BBC Micro's tabs would have waited for work that nobody had started.

Decision, Dan's, from a question with three options: the NES builds the shared
two-view section itself (the tab list, the per-view loader, the legend table and
the rule that a machine claiming a model states `case`), to the BBC Micro's
design, chosen over waiting for the BBC Micro (open-ended) and over shipping the
inside alone first. The BBC Micro can adopt the same section later. The plan
(Global Constraints, tasks 7 and 9) and the design (the views section, "What this
waits for" and the decisions table) now say so. Cost if wrong: if the BBC Micro's
models are later built with different tabs, the two have to be reconciled; the
design says the NES's is built to its design to avoid that.

## Task 6: the machine counts which chips the processor talks to, 6 October 2026

The machine's pull request (#55) is merged into this branch, so the inside
model's live state could be built: the machine counts the CPU's reads and
writes of each chip it can tell apart by address, and the page hands the counts
to the model. The plan's code for the counter (`NesChip`, `ChipAccesses` and
their 19 tests) went in as written.

### The address map, and where each line comes from

Read on 6 October 2026 from the nesdev wiki, each page fetched that day; the
table, with a source on every row, is in
[`../nes/facts/models.md`](../nes/facts/models.md), "What the counters count".
In short: `$2000-$3FFF` is the PPU (the CPU memory map page); `$4000-$4015`
is the sound unit and OAM DMA on the CPU's own die (the APU registers page and
the 2A03 page); a write to `$4016` is the CPU's own output latch, OUT0 to OUT2,
whose OUT0 is the strobe (the CPU pinout page), so it counts as the CPU's, not
a buffer's; a write to `$4017` is the frame counter; reads of `$4016` and
`$4017` assert /OE1 and /OE2, controller ports one and two (the Input devices
and CPU pinout pages). RAM, `$4018-$401F` and the cartridge are not counted.

### Which buffer is which port

The design inferred from the board's print that "40H368(CI)" (U7) is port 1
and "40H368(CII)" (U8) port 2, and left task 6 to check it against the wiki.
None of the five wiki pages read names a chip on the board for the ports, so
the wiki cannot settle it by itself; it does say which CPU pin is which port.
The OpenTendo redrawing (I2) joins the two: U7's two enables are on the net
`/~{OE1}` with its inputs on `/4016-D0` to `D4`, U8's on `/~{OE2}` with
`/4017-D0` to `D4`, and the CPU has `/~{OE1}` on pin 36 and `/~{OE2}` on pin
35. So U7 is `Pad1` and U8 `Pad2`, as inferred. The wiki does not contradict
it, so `ChipAt`, the plan's mapping and `parts.json`'s `chip` stand. The nets
are the redrawing's, a cross-check, not traced on the scan, so the `inferred`
flag in `parts.json` and `ic-table.json` stays. After review its sentence,
`CHIP_INFERRED` in `tools/nes-model/board_parts.py`, says it was checked and
how, and the two files were written again by
`cd tools/nes-model && NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python board_parts.py`;
only that sentence changed in them.

### How it went in

- **One place, the I/O decode.** `NesBus.ReadAccess` and `WriteAccess` call
  `Note` in their two branches for `$2000-$3FFF` and `$4000-$401F`, and
  nowhere else. RAM and the cartridge, most cycles, pass no new code. The read
  branch notes before the switch, because the `$4015` case returns early.
- **OAM DMA needed nothing of its own.** Its 256 writes to `$2004` are made
  through `Cycle`, so they pass the same decode and count as the PPU's. A read
  the DMA units repeat while the CPU is halted on it counts each time, as each
  is a read on the bus.
- **Power on starts the counters again.** `PowerOn` puts a new `ChipAccesses`
  on the bus rather than clearing the old one, so the plan's class stayed as
  written. The reset button keeps them. The host builds a new machine for a
  new cartridge or a region change, which starts them again too.
- **The host** gains `[JSExport] int[] AccessCounts()`, the bus's snapshot.
  In the browser it arrives as an `Int32Array`, a copy, not a JavaScript
  array: the browser check found that, when its first version asked for an
  array and failed.

### Tests, red then green

- C#: the 19 tests of the plan, and 11 of the bus: one read of `$2002` adds
  one to the PPU and nothing else; a peek adds nothing; RAM, the cartridge
  and the test registers add nothing; OAM DMA from page `$02` adds 256 to
  `Ppu` and one to `Apu`; `INC $4016` adds one to `Pad1` and two to `Apu`
  (the NMOS core's two writes); reads of `$4017` are `Pad2` and its write
  `Apu`; reset keeps the counters and power on starts them at zero; counting
  adds no cycle; two frames of the bundled homebrew talk to the PPU, in both
  regions. Red: first the build failed for want of `NesChip`; then, with
  `NesBus.Accesses` in place and no `Note` yet, 7 of the 30 failed. Green:
  all 30, then the NES project's 1,533. After review, one more, in four
  cases: the CPU halted by OAM DMA on a read of `$2002` or `$4016` counts
  once for each repeat of that read (one or two, by the cycle the DMA
  starts on) and once for its own. A mutation that counted a repeated read
  once failed it in the two cases with two repeats; restored, 1,537 pass.
- The page: six tests drive `nes.js` from `prepare` to running, in a fake
  browser with a fake .NET runtime and the panel test's made-up host. Red
  against the script as it was: all six failed. Green: 33 of 33 in the file.
- The browser check now asks the running page for `accessCounts()` twice,
  half a second apart: four signed 32-bit counters, the PPU's moving.

### Nothing else changed: the fingerprint

The machine's differential check (`bench/nes-speed/differential`, which hashes
every instruction's registers, cycle and PPU position, every frame's pixels
and the sound, for every pinned NES test ROM in both regions) on the code
before, `f9e84b0` exported with `git archive`, and after:

```
cd bench/nes-speed
dotnet run -c Release --project differential -- before.txt   # in the export of f9e84b0
dotnet run -c Release --project differential -- after.txt    # in the worktree
cmp before.txt after.txt
```

254 runs each, identical; both files hash to SHA-256
`ee3d6fca0d9aa82cedd2dd67080ca7ae987b81966e2c00608fda79bbffded918`. The boot
check's recorded frames (`BootTests`, and the browser check's canvas against
`machines/nes/expected-frames.json`) match in both regions.

### The speed

No quiet window came. Through the morning of 6 October the host's other
sessions kept 40 to 50 per cent of its eight CPUs busy (`vmstat`) even when
the load average read 2 or 3, and it rose past 8 several times. The README's
method, launches of the native bench run one after another, alternating the
code before (the `git archive` export of `f9e84b0`) and after, three launches
of five timed runs each per region, was swamped: one set gave the code after
27 per cent slower on NTSC and 27 per cent faster on PAL, a few minutes apart.

So the two builds were run **at the same time**, each pinned to its own CPU,
the CPUs swapped every round, so that both saw the same load:

```
# each round, from each build's bench/nes-speed, the two at once:
taskset -c 2 dotnet native/bin/Release/net10.0/Dbhq.Machines.Nes.SpeedNative.dll 5 1790000 <ntsc|pal>   # before
taskset -c 5 dotnet native/bin/Release/net10.0/Dbhq.Machines.Nes.SpeedNative.dll 5 1790000 <ntsc|pal>   # after
```

Twenty rounds of five timed runs per set. The sets whose rounds all began at
a load average of 8 or less, in times real time:

| Region | Before, median | After, median | After over before, per round, median | Rounds the code after was faster | When (UTC), load average |
|---|---|---|---|---|---|
| PAL | 2.19 | 2.14 | 0.997 | 10 of 20 | 07:57 to 07:59, 2.09 to 7.96 |
| NTSC | 2.12 | 2.09 | 1.000 | 8 of 20 | 09:03 to 09:05, 2.86 to 7.39 |

Four other sets, whose loads went over 8 (to 8.4, 11.4, 11.1 and 21), are
not used; their per-round medians were 1.015, 1.009, 1.014 and 1.026.

**The per-round ratio is the measure, not the medians.** In each round the two
builds ran at once on the same host, so the load that slowed one slowed the
other, and the ratio of the round's two medians takes it out. The medians over
all of a set's runs do not: their difference, 2.19 to 2.14 on PAL, is minus
2.3 per cent, past the plan's 2 per cent bar, but it mixes rounds run under
different loads, and the ratios of single rounds run from 0.84 to 1.61 (PAL)
and 0.89 to 1.08 (NTSC). The review took bootstrap 90 per cent intervals for
the median ratio, after over before: PAL alone 0.967 to 1.013, so PAL alone
cannot rule out a slowdown of 3 per cent; NTSC 0.987 to 1.012; the 40 rounds
used 0.983 to 1.010; all 120 rounds 1.000 to 1.015.

So: **no slowdown measurable, to within about 2 per cent, paired under load.**
(task 10's README-method run, below, did not settle it)
That is what the code predicts: the counting runs only on an access to a
counted chip, about 108 thousand of SNOW's 1.79 million cycles in a timed run,
a few nanoseconds each, well under a tenth of a per cent. The absolute figures
are half those of a quiet day (native NTSC 4.00 on 5 October), which is the
host's load, not the code. The browser builds were not timed.

**Owed.** Task 6's step 5 asks for the README's method on a quiet machine, and
this is not that. It is owed before the pull request merges: the plan's
Deferred list and its final pass (task 10, step 3) now carry it, dated.

The machine's headroom is unchanged by this: its quiet figure is 2.82 times
real time on NTSC and 3.24 on PAL in the browser compiled ahead of time (5
October, the speed entry). Under today's load the browser check's speed line
read "with little to spare in this browser", capacity 1.87 MHz against the
NES's 1.79, which is the shared host, not the counters.

### How the page's interface was adapted

The plan was written before the machine's panel script existed. Read, it
already carried `panel.nes = { host, stepTo(n), play(), region() }`, with
`region()` a function giving `'NTSC'` or `'PAL'` as the host does. Kept so,
and added to rather than renamed:

- `running()` and `region()` are functions, not the properties the plan
  named, to match what was there. The plan and the design now say so, dated.
- **Lower case in one place.** `nes:region`'s detail is `'ntsc'` or `'pal'`,
  lowered by one function, `announceRegion`. `region()` stays upper case, and
  a model takes `panel.nes.region().toLowerCase()`.
- **`panel.nes` exists before Start.** The script set it only once running,
  so `running()` could not be false before Start, and a model loaded before
  Start (they load when scrolled to) could not ask the region. Now `prepare`
  sets `running()` false, `region()` from the region control, `accessCounts()`
  null and `reset()` doing nothing; Start fills in the rest on the same object.
- **What "running" means.** True from the moment Start has the machine
  running. The script never stops it: the page has no power-off, Power and
  Reset restart the game, a hidden tab only pauses the loop and `stepTo` only
  holds it, and in each the machine stays on. So once true it stays true
  until the page is left.
- **`nes:start` after every fresh set of counters**, not only Start: after the
  Power button and `stepTo` (both call `PowerCycle`, which starts the counters
  again), and after a new cartridge goes in while the machine runs (a new
  machine), with `nes:region` first when the new cartridge's file names the
  other region. Without that the model's next difference would be negative
  (Review Focus 1). After review, `nes:start` also follows a change of
  region from the control, after its `nes:region`: a region change builds a
  new machine, and without it a model taking its baseline on `nes:start`
  kept a stale one. The order is the region first, so the model redraws,
  then the start, as for a new cartridge; the test checks the order both
  ways, NTSC to PAL and back. A cartridge that Start puts in may name the
  other region from the one the control showed before Start, and no
  `nes:region` is sent for that; the script's header says a model reads
  `region()` again on `nes:start`, which was simpler than a second event.
- **`reset()` and the Reset button are one function**, so each does what the
  other does, and both dispatch `nes:reset`.

### Surprising

- The wiki says which CPU pin is which port but not which chip on the board;
  the redrawing's nets had to join them.
- SNOW, the speed workload, reaches a counted chip in about 6 per cent of its
  cycles, nearly all of them writes to the sound unit (91,011 to `Apu` and
  16,226 to `Ppu` in 1.79 million NTSC cycles after 5 million to boot, read
  with the counters themselves on 6 October 2026). Lan Master reads pad 1
  more often than it touches the sound unit.
- The JIT's optimised code for `NesBus.Cycle` differs from launch to launch
  of the same build (6,442 to 7,602 bytes over three launches of the code
  before, `DOTNET_JitDisasm=Cycle`), which is one reason single launches of
  the speed bench disagree by a fifth.

## Task 7: the board's inside model on the page, 6 October 2026

The NES's page now has its first 3D model: the main board, under "What is not
modelled", loaded only when the visitor scrolls to it. It is the NES-CPU-10 of
the bare scans, with each console's parts, and it shows which chips the
processor is talking to while the machine above runs.

### What the visitor sees

- **The board**, cut from the scan's measured outline with its twelve mounting
  holes, both faces carrying the track map: the component side's copper and
  its white printed legend on top, the solder side's copper underneath, as
  colour, shine and relief, the way the KIM-1's does. Show tracks takes them
  off and Show tracks only fades the parts out. The camera goes all the way
  round, under the board too.
- **The parts**: the ten chips as black bodies on their legs (all are
  soldered in, so no sockets), each with this console's part printed on its
  top; the cartridge connector over the edge fingers, the expansion socket,
  the two controller headers and the POWER and RESET header as plain boxes;
  the 71 resistors, capacitors and other small parts, three instanced meshes;
  and each console's crystal (its frequency printed on its can) and RF
  modulator.
- **The console** is the page's region. The model reads `panel.nes.region()`
  when it loads, so a model loaded after a change of region draws the region
  the page has, and on `nes:region` it swaps the chips' printed parts, the
  crystal and the modulator, the caption, the note, the legend's parts, the
  stage's accessible name and its `aria-describedby`, all at once, with
  nothing downloaded.
- **The marks.** Every quarter second, once the machine runs, the model reads
  `panel.nes.accessCounts()` through `createSampler`, tints the top of each
  chip whose count moved, marks its legend row and writes each counted chip's
  rate into its row. Before Start nothing is marked, the rate cells say "not
  running" and the status line says the machine is not running. On
  `nes:start` and `nes:region` it takes a fresh baseline and the cells say
  "measuring" until the next sample (Review Focus 1); the rates are divided by
  the time measured between samples (Review Focus 2).
- **Pointing at a chip** names it on the status line, with its part in this
  console and the other, and lights its legend row; a click or a tap does the
  same, for a screen with no pointer to hover.
- **The legend**, under the caption: one row a chip, U1 to U10, with its part
  in this console, in the other, what it is, when it is marked and its rate,
  and under it the words the plan asked for: what a mark means ("read or
  written by the processor in the last quarter second", not "working"); that
  the CPU, the work RAM, the address decoder and the cartridge are in use all
  the time, the video RAM and the latch are the PPU's own and the lockout chip
  is not emulated, so none is marked, and that the CPU's row is marked only
  for the sound and input registers on its own chip; U9's jobs "by the
  redrawing's nets", not traced on the scan; U7's and U8's ports by the
  board's print, checked against the nesdev wiki and the KiCad redrawing in
  task 6 and not traced; and that the copper is traced to look at and the
  heights are typical.
- **The caption** for each console starts "Model, not a photograph, of an
  NTSC NES-001, the front-loading console sold in North America" or "of a PAL
  NESE-001, the front-loading console sold in Europe", says the copper is
  traced to look at with its connections not verified, that the board is the
  scanned one with that console's parts and not one real board, and that the
  heights are typical; its sizes, counts and parts are read from the layout.
- **The note** for each console, made by `nes-famicom-board-notes.mjs` from
  `site/src/data/nes-famicom-board-model.json`: the scale, the solder side's
  registration, the copper's coverage and the nets check's failure as
  `copper.json` has it (one of ten ground pins in the largest ground net, two
  of ten +5V pins in the largest +5V net, a ground and a +5V pin in one net),
  saying the copper is not a netlist; the places against the KiCad
  redrawing; and the parts' photographs, the NES-CPU-07's for NTSC and the
  NES-CPU-11's for PAL, each with its worst distance from the scanned board's
  footprints. Then what it does not show.

### Decisions

- **The results file.** `tools/nes-model/results.py` (the board half; the
  case half comes with task 9) reads `frame.json`, `registration.json`,
  `copper.json`, `parts.json` and `sources.json` and writes only the figures
  the page quotes, and the six sources the board was made from (the parts
  module's five and the KiCad redrawing). Run with
  `/tmp/nesvenv/bin/python tools/nes-model/results.py`; three pytest tests on
  made-up data files. A site test recomputes every figure from the data
  files.
- **The tokens**, in `tokens.css`: `--model-nes-pcb` and `--model-nes-pcb-under`
  for the lacquer, a little lighter and yellower than the KIM-1's, chosen by
  eye and not measured; `--model-nes-print` for the white legend; `--model-active`, an
  amber, for the access mark, on the model and in the legend, chosen over the
  lime, which stays the page's own accent and is spent on nothing new, and
  over the KIM-1's lit-LED red, which on a chip reads as a fault. The copper
  is `--model-copper`, as the KIM-1's. None is a text colour. The legend's
  caption is the one new text colour rule, white on black, in the contrast
  table. No socket token: the NES's chips are soldered in.
- **The legend in `MachineModel.astro`**, built from the entry's `legend`
  (`chipLegend(region)`) and `legendWords`, for any model that has one; each
  console's part is a span with `data-model-region`, switched as the captions
  are. A row's `mark` words come from the layout, beside the plan's fields.
- **The credits.** A model whose results file lists its sources is credited
  with those alone, matched by address. Without that the NES's main
  photograph, which the board takes nothing from, would have been credited
  under the board as a source. The registry's `references` hold the six, each
  with its title, author, address, licence as `sources.json` records it,
  fetched date, SHA-256 and what was used; OpenTendo's are credited at the
  `dbhq-uk` fork the tools read, naming OpenTendo and its authors. The main
  photograph's `used` now says the board takes nothing from it.
- **The track buttons' help** says the tracks are traced to look at, with
  their connections not verified, through the entry's `tracksHelp`; the
  KIM-1's keeps `CONTROLS.tracks`.
- **The registry** (the controller's ruling of 6 October 2026): the NES row
  came from the machine's pull request with `"case": true`, which fails the
  cased rule with `inside` alone claimed, so `case` came off and `models`
  claims `inside` only, as the BBC Micro's plan did; task 9 puts `case` back
  with the outside. `git grep` found two assertions that read the row,
  `nes.case === true` and `'models' in nes === false`, both in
  `site/tests/nes.test.mjs`; both now say what the row is until task 9, with a
  dated comment. Nothing else reads the NES's `case`.

### What changed on contact

- **The KIM-1's and the BBC Micro's pages are not byte-identical.** Every
  stylesheet is inlined into every page (`inlineStylesheets: 'always'`), so
  the four tokens and the legend's six rules reach both pages' `<style>`. The
  plan's `cmp` cannot hold for a task that adds a token. Built from 4a96b7e
  (in a copy of the tree from `git archive`) and from this work, with the
  `<style>` element left out both pages are identical, and the stylesheet's
  only changes are two insertions, 30,899 to 31,480 bytes. Recorded in the
  plan, dated.
- **The NES page's speed test** forbade any "number MHz" on the page; the
  caption names each crystal by its marking, 21.47727 MHz on the NTSC board.
  The test now leaves the model section out, with a comment: a part's
  marking is not a speed.
- **The access module's sampler** also takes a machine with no counters yet
  (`accessCounts()` is null before Start) and leaves no baseline behind; one
  test more than the plan's five, and one that hands it the page's
  `Int32Array` across the 2^31 and 2^32 wraps. `(b - a) >>> 0` is right for
  an `Int32Array` input: its values are the same signed numbers as the plain
  list's, and the test holds it.
- **The built-page test for the regions**, owed since task 1's review,
  replaced the test of the component's source that matched
  `data-model-region={v.region} hidden={v.hidden}`: on the NES's page each
  console has one caption, one note and one hidden name, only the first
  shown, each console's words its own, every id on the page used once, and
  every other part marked for a console (the legend's) marked for each.
- **The browser check's model steps run before the cartridge picker's**, with
  Lan Master still in: the picker's test cartridge ends in a loop that
  touches no counted chip.
- **A busy host runs the game in bursts.** Under load, a quarter second can
  pass with no frame run, so the rates jump about (one sample read 32,311 PPU
  accesses a second and the next none, in the second run below, load average
  about 20); the check of the PAL machine's marks waits up to five seconds,
  having failed once at one and a half.

### Tests

- `site/tests/nes-famicom-access.test.mjs`: red first (the module missing),
  then 7 pass: the plan's five and two more.
- `site/tests/nes-models.test.mjs`, twelve for the board: the claim and the
  entry; the regions against the page's region control, read from the built
  `dist/machines/nes/index.html` (`input[name="nes-region"][data-nes-region]`),
  both ways; every part drawn from the parts file; the map read as
  `copper.json` gives it; the tokens; the legend against `ic-table.json`, on
  the page too; the legend's words; the captions; the results file against
  the four data files; each note word for word, with the nets result read
  from `copper.json`; every sentence about the copper saying it is traced to
  look at and not verified (it caught a sentence of the PAL note on its first run, and a limit worded like it was changed with it);
  the credits both ways; the model's wiring to `panel.nes`.
- `site/tests/model.test.mjs`: every model with a track map has it committed
  inside its budget and built beside its bundle byte for byte, named on its
  page for the loader alone; the deploy's serving list names
  `models/nes-famicom-board.js` and `models/nes-famicom-board-tracks.webp`
  (the existing tests now cover both).
- The site suite, built from this commit with the machines built and CI's
  results file (run 37419286133): 446 tests, 445 pass, 0 fail, 1 todo
  (`cd site && npm run build && node --test tests/*.test.mjs`, 6 October 2026).
  Both floors raised from 423 to 445.

### Page weight

`cd site && node scripts/page-weight.mjs /machines/nes/`, 6 October 2026,
before (4a96b7e, built from a copy) and after:

| | Before, bytes gzipped | After |
|---|---|---|
| The page when it opens (HTML, scripts, the photograph) | 41,817 | 47,677 |
| of which the HTML | 12,051 | 17,104 |
| of which the model loader | none | 807 |
| Later, when the visitor reaches the model: the bundle | none | 162,898 |
| Later: the track map | none | 144,726 |

Nothing under `/models/` is in the first load. The bundle is 161,772 bytes
gzipped at level 9 as `model.test.mjs` measures it, inside its 200,000; the
map is 144,774 bytes, inside its 600,000. The HTML grew by the two captions,
the two notes and the legend.

### The browser check

`cd site && node scripts/browser-check.mjs`, Chrome 153 with software WebGL,
on the shared host, 6 October 2026. The board's steps are in
`scripts/browser-check-nes.mjs`, after the NES's own and before the cartridge
picker's. Four whole runs: the first passed (load average about 9); the second
failed only the PAL marks, then judged over 1.5 s (load about 20), which led
to five seconds; the third passed every NES step and failed once in the
KIM-1's phone steps, a finger on the focused model moving the page 19 pixels
(load about 21), a step that passed in the other three runs on the same
markup, the KIM-1's page differing only by the inert new rules in its
stylesheet; the fourth, on this commit's code, passed whole, with "no console
errors, no failed requests, no CSP violations". Its board lines:

```
nes: model files requested before scrolling to the model: none
nes: model running in 1833 ms after scrolling to it, fetched /models/nes-famicom-board.js, /models/nes-famicom-board-tracks.webp; status "The model is running, and so is the machine: a marked chip was read or written by the processor in the last quarter second."
nes: model canvas 778x458, 26.6% of its pixels drawn; region ntsc, tracks on
nes: pointing at U5 at 657,355: status "U5, the picture processing unit (RP2C02): RP2C02G-0 in this console, RP2C07-0 in a PAL console. Marked: the processor read or wrote it in the last quarter second."; rows lit U5
nes: in 2 s on NTSC, chips marked U5 U6 U7, legend rows marked U5 U6 U7; highest legend rates: U5 12128 and U7 1021 a second
nes: region to PAL: model region pal, its caption shown true, described by model-about-pal, named "3D model of the NES's main board, with a PAL console's chips. Point at a chip to name it."; U5's part in the legend RP2C07-0; fetched since nothing; the rates after nes:region: none, none, 733, 1492, 1507, 1392, 1511, 1380, 1509, 1391, 1530
nes: in 5 s on PAL, chips marked U5 U6 U7; highest legend rates: U5 1641, U7 1358
nes: region to NTSC: model region ntsc, its caption shown true, described by model-about-ntsc, named "3D model of the NES's main board, with an NTSC console's chips. Point at a chip to name it."; U5's part in the legend RP2C02G-0; fetched since nothing; the rates after nes:region: none, none, 3628, 1024, 1392, 1391, 1393, 1392, 1409, 1359
nes: tracks at the start {"loaded":true,"tracks":true,"underside":true}; Show tracks: off, {"tracks":false,"underside":false}; Show tracks only: parts hidden, {"visible":false,"tracks":true}; and back: {"visible":true,"level":1,"tracks":true}
nes: under the board, polar 3.142: the map's solder-side copper at 300 points is 111,97,36, its bare board at 300 points 28,73,23, 86.7 apart; the same points mirrored side to side 8.9 apart
```

The rates are live readings of a machine in software WebGL on a busy host,
not figures about the project: they differ from run to run.

### Surprising

- The game reads controller port 1 several hundred to about a thousand
  times a second, and the
  PPU's rate jumps by thousands between quarter seconds, as the page runs
  the machine in bursts to keep up with the clock.
- A new machine after a region change can show one very high first rate (one
  sample read 32,311 PPU accesses a second), most likely the page catching
  up on frames just after the restart; not checked. The baseline is the new
  machine's, so it is not a difference across two machines.

### After review, the same day

- **Corrected 6 October 2026, after review: the very high first rate after a
  region change is the game, not the page catching up.** The entry above
  says the 32,311 PPU accesses a second were "most likely the page catching
  up on frames", and the bullet before it gives that sample as an example of
  the bursts. Both are wrong about the cause. The run loop never catches up:
  `site/public/machine-host.js` runs each animation frame for the real time
  since the last, capped at `MAX_FRAME_MS`, 100 ms of machine time, and drops
  the rest. The cause is Lan Master's reset code. Checked in
  `roms/nes/Lan_Master.nes` (40,976 bytes, SHA-256
  `becfeafb80479c330333c9e9385417f68f3c88e85443dfb37b05ae9283f3ea45`, NROM, two
  16 KB banks) with a short Python read of the file, 6 October 2026: the reset
  vector is `$8000`; the code there calls `$8A0D` twice, `JSR $8A0D` at
  `$8013` and at `$803C`; and `$8A0D` is `2c 02 20 2c 02 20 10 fb 60`, `BIT
  $2002; BIT $2002; BPL` back to the second `BIT`, then `RTS`: a wait for
  vblank. Its loop reads the PPU once every 7 cycles (`BIT` absolute 4, a
  taken `BPL` 3), so about 4,250 reads a frame on NTSC (29,781 CPU cycles) and
  4,750 on PAL (33,248). Two waits, the first starting part way into a frame,
  are up to about 8,500 reads on NTSC and 9,500 on PAL; 32,311 a second over
  a 250 ms sample would be about 8,080. The sample's own length was not
  recorded, so this agrees with the figure rather than proving it. The
  bursts themselves (whole quarter seconds with no frame run, on a busy
  host) are as described.
- **The PAL caption** said the PAL console's board was "an NES-CPU-10". The
  PAL parts were read off an NES-CPU-11 and drawn on the CPU-10's layout, so
  each console now has its own words: "an NES-CPU-10" for NTSC, and "drawn on
  an NES-CPU-10, whose layout the PAL console's NES-CPU-11 was checked to
  share" for PAL. A test holds both.
- **The KiCad redrawing's credit** said which buffer serves which controller
  port was read from its nets. It was not: that is by the board's print,
  checked against the wiki and the redrawing. The credit now says the ports
  were "checked against them", and a test fails a credit that says a port was
  read from a source.
- **The legend's second paragraph** began "Some chips are never marked. The
  CPU..." and then said the CPU's row is marked. It now says all but the CPU
  are never marked, and that the CPU's row is marked for the registers on its
  own chip that the machine counts as the sound unit's: `$4000` to `$4015`,
  the sprite copy's `$4014` among them, and the writes to `$4016` and
  `$4017` (`ChipAccesses.ChipAt`).
- **The access module** takes differences as `(b - a) | 0`, as
  `public/nes.js` tells a reader to, not `>>> 0`. A difference below zero now
  gives no rate and keeps the new counts as the baseline: should a new machine
  ever come without its `nes:start`, the old way read a fall of a few
  thousand as about 17 billion accesses a second. Two tests, red then green.
  The site suite after the round: 448 tests, 447 pass, 0 fail, 1 todo
  (`cd site && npm run build && node --test tests/*.test.mjs`, 6 October
  2026); both floors raised from 445 to 447.
- **The NES page's speed test** now takes out only the two crystals'
  markings, read from the parts file, rather than the whole model section,
  so a typed speed anywhere else in the section is still caught.
- **The browser check** looks for the marks for up to five seconds on each
  region, stopping when U5 and U7 have each been marked with a rate above
  zero: a timeout for the chips to show up, not a judged figure.
- **The plan's rule that the KIM-1's and the BBC Micro's pages stay
  identical** is revised in place, dated, with the old words kept: identical
  in markup, once the inlined `<style>` is left out. Task 9's Modify list now
  names the two tests that assert `case` undefined, which flip when it comes
  back.

## Task 9: the outside model, and both models as views of either console, 6 October 2026

The NES's page now has both its models: the outside, the case, and the
inside, the board, as two views of the same console in one section, each
drawn for the NTSC NES-001 and the PAL NESE-001 and following the page's
region. The work went in as three commits, each green: the views' section
and loader, then the outside model, then the registry's rule, the credits,
the browser check and these words.

A first attempt at this task was cut off by an API rate limit at about 13:51
UTC, before it had written anything; it was started again at 18:18 UTC from a
clean worktree.

### The views

- **One section, a tab a view.** A machine with a case gets one section,
  "Models of the machine", with a WAI-ARIA tab list named "Views of the
  model": Outside, then Inside, in the registry's order. Each tab controls a
  panel that holds everything the KIM-1's single section holds (the stage,
  Reset the view, the status line, the caption, the note and its credits),
  its ids suffixed by the view and then the console (`model-about-inside-pal`).
  The paragraph of controls, the same for both views, is said once under the
  panels. Each panel is its model's root, so the modules mount in it exactly as
  they would in a section of their own.
- **Chosen over** the BBC Micro's models building it first (Dan, 6 October
  2026: they hold only their first tasks), and over two sections one under the
  other, which the design had already turned down: a visitor would scroll past
  a second canvas and three.js would load twice for anyone who scrolled on.
- **The KIM-1 keeps its section.** The panel's content moved into
  `ModelPanel.astro`, used once by a single model's section and once by each
  view's panel. `dist/machines/kim-1/index.html` and
  `dist/machines/bbc-micro/index.html`, built at `af3835d` and after the first
  stage, are identical once the `<style>` element is left out of both
  (`cmp`, 6 October 2026); the stylesheet gained only the four tab rules.
- **The loader** shows the section, leaves a hidden panel hidden, and loads
  each view as before: when its panel nears the screen or its Load button is
  pressed. A hidden panel never nears the screen, so the inside is fetched
  only once its tab is chosen. The tabs select on a click, Enter and Space;
  Left and Right move and select, round the ends; Home and End go to the ends;
  only the selected tab is in the Tab order, and Tab is never taken, so focus
  goes from the tab into the panel's stage and on out. A view keeps its camera
  when switched away and back, because its stage is never torn down; a hidden
  stage does not draw, because its own observer says it is off screen. The
  loader is 3,357 bytes, under its 4,000 (1,679 before).
- **The tabs' look.** An unselected tab is `--moss-80` on the black canvas,
  `--white` when hovered; the selected one `--white` on `--veil`, with a bar
  under it in its own colour, so it is told by a shape as well as a colour. No
  lime and no transition, so reduced motion has nothing to stop.
- **Changed on contact.** The page offers tabs for any machine whose models
  are not a board alone, not only for one with two: in the first stage the NES
  claimed the inside alone, and its page showed one tab, which proved the
  inside working in a panel before the outside existed (a smoke check in
  Chrome: it ran, drawing NTSC, described by `model-about-inside-ntsc`, and
  fetched its bundle and map only when scrolled to). The board module found its
  caption by the id `model-about-<console>`, which a panel's ids no longer
  are; it now finds the caption by its console and takes its id.

### The outside model

- **The case**, from `nes-famicom-case-parts.mjs` (task 8's measurements): the
  two shells, split at the seam, the lower one's ends leaning in below their
  break by the measured profile; the black band down the front, over the top's
  two ends and down the rear; the slats of the top vents and the slots
  underneath; the cartridge door over the front's top edge and its lip; the
  line round the buttons' panel; POWER and RESET; the power light; the two
  controller ports; the rear's window with the power jack, the channel switch
  and the RF jack, and the video and audio jacks on the right end; the
  underside, with its expansion cover, its moulded ribs and panels as lines,
  its screw holes, and the feet. Start view three quarters from the front;
  the camera may go all the way round, under the case too, its target held in
  the case plus two centimetres.
- **Its words are ours.** Every word on either console (the front's label,
  the buttons', the ports' numbers, the rear's, and the PAL console's two
  labels underneath) is drawn by the model, in the site's own type once
  `document.fonts.load` has it, into one canvas. No Nintendo lettering, no
  image, no path: a test fails any of the NES's model modules that names a
  logo, loads an image but the board's track map, or holds an SVG path.
- **The greys are the case as made.** Six tokens of its own (the upper shell,
  the lower shell, the band, the buttons, the printed ink and the labels'
  paper), the two shells' greys checked to be greys by a test; the light is
  the KIM-1's `--model-led` and `--model-led-off`. The PAL console
  photographed has yellowed; the model has not.
- **Its state.** The light is lit and POWER in while `panel.nes.running()`,
  read every frame; RESET goes down for 140 ms on `nes:reset`, however the
  reset came. A click on POWER presses the page's own Start button, so it does
  exactly what Start does; once the machine runs it changes nothing, and the
  status line says the page has no power-off. A click on RESET calls
  `panel.nes.reset()` while the machine runs; before Start it does nothing to
  any machine and the status line says the machine is not running (Review
  Focus 4). That choice is one function, `press(name, panel)` in the layout,
  tested on a made-up panel. With no easing on the buttons, reduced motion
  changes nothing there.
- **The region.** The model reads `panel.nes.region().toLowerCase()` when it
  mounts, so a view loaded after a change of region draws the region the page
  has then (Review Focus 3), and on `nes:region` it swaps the words, the rear
  and the underside, all built at mount, with nothing downloaded.
- **What the page says, every figure from `nes-famicom-case-model.json`**,
  which `results.py`'s case half writes from `case.json`: the size is the
  published 254 by 203.2 by 88.9 mm, none of it Nintendo's, good to about 3
  per cent; the lower shell's lean is an average good to about 2.5 mm, of four
  ends reading 12.86 to 17.71 mm; the rear connectors are placed from a
  photograph that agrees with the patent's rear view within 1.3 mm, while the
  places worked out from the board missed by up to 5.16 mm, one of three
  within the 2 mm limit, so the check failed as measured; the board's place in
  the case is good to about 2 mm and assumes the PAL and NTSC cases share one
  moulding; the buttons' travel, 3 mm, is typical; whether POWER latches is
  not known, and the model shows it in while running, as designed; the side
  jacks and the PAL rear are not checked against the board; the PAL words and
  rear were read from photographs taken at 20 mm, with strong perspective; and
  the light never blinks, because the lockout chip is not emulated. The
  "about 3 per cent" and "about 2.5 mm" are read by `results.py` from the
  sentences `case_measure.py` wrote beside the figures in `case.json`, so they
  are not typed again.
- **The PAL lens** was not in any data file. It was read on 6 October 2026
  from each file's own XMP (`exif:FocalLength 200/10` on an ILCE-7RM4 for O4,
  O9 and O10; O5, the PAL underside, is a phone's, at 19 mm on a small sensor)
  and recorded in `sources.json` as `focalLengthMm`, where `results.py` reads
  it. Command: `head -c 3000000 <file> | strings -n 8 | grep FocalLength`.
- **Credits by view.** The NES's references gained the eight sources the case
  used besides its head photograph (O1, O2-BR, O3-01, O4, O5, O9, O10 and
  I7-FL), each with its licence, day and SHA-256 from `sources.json`. The head
  photograph is O2-FL, which the case was measured on, so its `used` now says
  what the outside took from it, and the board still takes nothing. Each
  view's note credits exactly the sources in its own results file, matched by
  address, so neither credits the other's. O2-BL was not read and is not
  credited.

### The registry

- **`"case": true` is back on the NES**, with both views claimed, the outside
  first; the two assertions task 7 had changed (`nes.test.mjs` and the claim
  test in `nes-models.test.mjs`) say so again. It went in with the outside
  model's commit rather than the last, because `model.test.mjs` fails an entry
  in `MODELS` that no machine claims, and the cased rule fails the outside
  without the inside.
- **The new rule: a machine that claims a model states `case`.** Tried on a
  made-up registry: the NES as task 7 left it (the inside, no `case`) now
  fails, as does any claim with no `case`; stated either way, a claim passes
  the new rule and the old rules judge it as before; the KIM-1 (`case: false`,
  its board) and the NES pass. Shown failing first, with the rule's line taken
  out: the test failed, and passed with it back.

### Tests

Red first, then green, each with its command (`cd site`, `results.json` from
CI run 37419286133 copied in for the build and deleted before each commit):

- **The loader**, `node --test tests/model-loader.test.mjs`, on a made-up page
  with a stand-in for the DOM: against `af3835d`'s loader, 3 of 4 failed (the
  section stayed hidden, nothing switched); with the new one, 4 pass.
- **The views' markup**, `node --test tests/model.test.mjs`, on the build of
  `af3835d`: the new test of the section failed; after the build, it passes.
- **The rule that a machine claiming a model states `case`**, `node --test
  tests/registry.test.mjs`: with the rule's line taken out of
  `src/lib/registry.mjs`, 1 failed; with it, 25 pass.
- **The case half of `results.py`**, `/tmp/nesvenv/bin/python -m pytest
  tools/nes-model/tests/test_results.py -q`: against the old `results.py`, 2
  failed; with the case half, 5 pass. The whole tool suite: 106 passed.
  `results.py` rewrote the board's results file byte for byte (`cmp`).
- **The outside's tests in `nes-models.test.mjs`** that read the built page
  failed on the old build (the entry's panel, the note on the page). The rest
  were written beside the code, so each was shown failing by a hand edit, then
  put back and checked with `cmp`: the rear sentence without "the check failed
  as measured" (the uncertainty test failed); `boardMissMm` 4.0 in the results
  file (the results-file test failed); RESET before Start calling
  `nes.reset()` (the Review Focus 4 test failed); the upper shell's token made
  a yellowed `#d8cf9a` (the machine-state test failed on "not a grey"); the
  region set to NTSC at mount whatever the page says (the same test failed).
- **The whole site suite**, `nice -n 10 node --test tests/*.test.mjs` after
  the build: 466 tests, 465 pass, 0 fail, 1 todo (the BBC Micro's). The floors
  in both workflows went from 447 to 453, 462 and 465, one step a commit.

### Page weight

`node scripts/page-weight.mjs /machines/nes/`, gzipped at level 9, before
(the build of `af3835d`) and after (this task's build), 6 October 2026:

| | Before | After |
|---|---|---|
| First load, total | 47,767 | 51,025 |
| The page's HTML | 17,194 | 19,794 |
| `model-loader.js` | 807 | 1,465 |
| Later: the outside's bundle, when the section is reached | none | 166,055 |
| Later: the inside's bundle, when its tab is chosen | 162,969 | 162,977 |
| Later: the inside's track map, with its bundle | 144,726 | 144,726 |

Nothing under `/models/` is in the first load. Each bundle is inside its
200,000 bytes. The HTML grew by the outside's caption, help and notes for both
consoles and their credits, and the tabs.

### The browser check

`cd site && nice -n 10 node scripts/browser-check.mjs`, Chrome 153 with
software WebGL, on the shared host, run twice as the brief allowed, 6 October
2026:

- **Run 1** (load about 5 to 33): every KIM-1 step and every NES step passed;
  one problem, in the BBC Micro's part, "the picture is not being redrawn
  (fields 129856 then 129861)", the same failure as in task 7's fix round
  under the same load. Nothing in this task touches the BBC Micro's machine,
  and its page is identical in markup. The NES's model lines:

  ```text
  nes: model files requested before scrolling to the models: none
  nes: outside model running in 3205 ms after scrolling to it, fetched /models/nes-famicom-case.js; region ntsc, light dark, POWER out; status "The model is running. The machine above is not running: click POWER on the model, or press Start above, to start it."
  nes: outside canvas 778x458, 28.6% drawn
  nes: a click on the model's RESET before Start: the machine is ready, fetched nothing; RESET went down 0 times; status "The machine is not running, so RESET did nothing: click POWER, or press Start above, first."
  nes: started by POWER on the model: light lit, POWER in; status "The model is running, and so is the machine: the power light is lit and POWER is in."
  nes: the page's Reset: RESET went nes:reset, down, up; presses 0 then 1
  nes: a click on the model's RESET: 1 nes:reset; the page's line "Reset: the game started again, and the cartridge's RAM was kept."; presses 2; status "RESET reset the machine, as the page's Reset does: the game started again."
  nes: a click on POWER while it runs: POWER in, light lit; status "The machine is already on, and this page has no power-off: POWER stays in. The page's Power off and on button starts the game again."
  nes: under the case, polar 3.142: 31.3% of the canvas drawn
  nes: the page to PAL with the inside not loaded (nothing of it fetched): the outside drew pal; Inside chosen: it running, drawing pal, described by model-about-inside-pal; fetched /models/nes-famicom-board.js, /models/nes-famicom-board-tracks.webp; panels shown {"outside":false,"inside":true}
  nes: Left from Inside: focus model-tab-outside, selected model-tab-outside, outside shown true, inside shown false; Right: focus model-tab-inside, selected model-tab-inside; the inside's camera kept true (azimuth 0.262 then 0.262), the outside's true
  nes: Tab from the Outside tab: the stage, BUTTON, A, A, A, A, A, A, A, A, A, A, A, A, A, A, out of the section
  nes: reduced motion (asked for): the views running and running, switched back to model-tab-outside
  ```

- **Run 2** (load about 10 to 31): it stopped in the KIM-1's part, on "the
  camera never came to rest", which throws and ends the run before the BBC
  Micro and the NES. The KIM-1's page is identical in markup, its bundle and
  the shared stage are unchanged, and run 1 passed that step; read as the
  host's load, not the change. Not run a third time.

The NES's part alone, through a harness kept outside the repository that
calls `checkNes` with the same server headers and watch, also passed once
before run 1, with no problems. A whole run on a quiet host, with every part
passing, is still owed.

Before Start the light is dark and POWER out; Start, by a click on the model's
POWER, lit the light and put POWER in, as the lines above show.

### Surprising

- **The stage's light is green.** The shared stage lights every model with the
  site's `--white`, which is the phosphor green's white, and a moss-green
  ground light, so the case's greys come out a little green on the canvas. The
  KIM-1's board has always been lit that way. Left as it is: changing the
  shared stage would change the KIM-1's model, and the case's tokens are
  greys.

### Simplified

- **The rear's window is drawn flat** on the rear face. On the console it is
  set in, by about 4.4 mm as `case.json` read it on O2-BR (its
  `rearWindow.setBackReadMm`, 4.39); the note under the model says so, from the
  results file.
- **The underside's ribs and panels are drawn as lines**, its screw holes as
  dark discs, and where the leaning ends reach under the case, both sit on the
  lean. The case's corners are square where the console's are rounded.
- **The PAL console's rear words** are printed on three lines on the case
  (O10, looked at full size on 6 October 2026: "ANSCHLUSS / NETZGERAT/ /
  ADAPTER", "KANAL3", a rule, "KANAL4", and "ANSCHLUSS / ANTENNE"); NETZGERAT
  is printed without its umlaut. The model draws each on one line, squeezed
  into the box the NTSC console's words take, and the note says so. The words
  and their boxes are task 8's, unchanged.

### After review, the same day

The review found the tabs, the registry, the state, the region, the credits
and the one-model pages sound, and asked for these, all done:

- **The rear's window and the ribs** are now in the note's limits, the window's
  set-in read by `results.py` from `case.json` (`rear.windowSetBackMm`), with a
  test that ties the sentence to it.
- **Review Focus 5 is wired.** `shownFor` and the page's region path moved into
  `site/src/models/regions.mjs`, which has no Node imports, so the browser
  bundles can import it (`models.mjs` re-exports `shownFor`). Both modules'
  `setRegion` call `followRegion`: a region a model does not draw keeps its
  first console drawn, named and described, and the status line says there is
  no model of the page's console and why; the modules no longer write over that
  sentence when they start. A test drives `followRegion` on a stand-in for a
  model's panel with a region it does not draw: red while the modules did not
  call it, green once they did.
- **The light's tint** is said in the note's limits and in `DESIGN.md`: the
  stage's light, the same for every model, tints the greys a little green.
- **The lens** sentence says the PAL console's front words and rear: the labels
  underneath are from O5, a phone's photograph.
- **The shared moulding** is no longer read from the word "moulding" in
  `boardInCase.from`, which a sentence denying it would also hold: `results.py`
  matches `case_measure.py`'s own tag, "the shells the moulding [inferring]",
  and stops on any other words about the moulding. `case.json` was not
  regenerated; `results.py` rewrote the board's results file byte for byte and
  the case's with only `windowSetBackMm` added.
- **The tab keys** leave a key with Alt, Ctrl or Meta held to the browser, so
  Alt and Left still goes back with a tab focused; the loader is 3,491 bytes.
- **`MachineModel.astro`** imports from `models.mjs` once.

### Task 8 follow-up: the NES's photographs, resized and credited (6 October 2026)

The NES row now exists in `machines/registry.json`, so the steps task 8 left
are done.

**Which three, and why not the three named.** The list named O2-FL, O2-BR and
one PAL photograph. The NES row's main photograph, `nes.webp`, is already
O2-FL (its entry and README section say so), so O2-FL is not committed twice.
The plan's other choice was O2-BL, but this entry's own record says O2-BL was
not read and is not credited, and a test pins that no credited source is
O2-BL: crediting it with a "used" would say something false. So the three are
the photographs the case did use:

- **O2-BR**, `nes-rear-right.webp` (Evan-Amos, public domain): the rear, the
  places of its three connectors, and the lean of the lower shell held out as
  a check.
- **O4**, `nes-pal-front.webp` (PantheraLeo1359531, CC BY 4.0): the PAL
  console's front words and the check of its front against the NTSC console's.
  Its original is 93 MB; a strip of 1600 by 419 pixels is what it gives.
- **O5**, `nes-pal-underside.webp` (PantheraLeo1359531, CC BY 4.0): the PAL
  underside, rectified on its six screw holes: its labels, feet and the
  expansion port's cover.

That is two PAL photographs, not one. O4 shows the PAL console's face and
O5 its underside, and each carries something the model took from it that the
other does not. Both were already credited as references; each moved to
`photos`, because the credits test requires a source to be credited once.

**Licences.** Checked on Commons' API on 6 October 2026 (`prop=imageinfo`,
`iiprop=extmetadata`): O2-BR `LicenseShortName` Public domain; O4 and O5 CC BY
4.0, `AttributionRequired` true, Artist PantheraLeo1359531, `Credit` Own work.
Each file's SHA-1 is also the one Commons records, and its SHA-256 is
`sources.json`'s. The dates are `DateTimeOriginal`: 2016-07-27 for O2-BR,
2022-11-02 for O4 and O5. `fetched` is 5 October 2026, the day the originals
were downloaded, since they were not fetched again.

**Commands.** From `~/dbhq-previews/nes-model-research/full/`, for each
original: `cwebp -q 82 -resize 1600 0 -metadata none <original> -o
site/src/assets/photos/<name>.webp`. `cwebp` 1.3.2 read the PNG and the JPEGs
as they are; no conversion first. The copies are 26,862, 18,370 and 244,400
bytes. Not one original is committed.

**Where the hashes are.** `site/src/assets/photos/README.md` has a section
for each file with both SHA-256, the original's and the copy's; the originals'
are also in `tools/nes-model/data/sources.json`, where `committed` is now set
for these three and for nothing else. (O2-FL's `committed` is still null
although `nes.webp` is its resized copy: it was left alone as outside this
step. It is a one-word change if Dan wants it.)

**Tests and floors.** One test added in `site/tests/nes-models.test.mjs`: the
three files exist and are resized copies, none is an original's hash, each is
credited in the NES's `photos` by author, address and licence (and the CC BY
link), none is also a reference, each is `committed` in `sources.json` (and
nothing else is), and the README gives the original's SHA-256 and the
copy's, which is the file's. Shown able to fail by hand: one digit of a README
hash changed, `committed` set to null, and a licence changed in the registry
each failed it and the files were restored. No existing test counted the NES's
photographs; the "credited once" test passes because the three references
moved. The registry's, `site.test.mjs`' and the README tests read the list as
it stands. The floors in `validate.yml` and `deploy-site.yml` are 468, from
the full suite's output on this branch (469 tests, 468 passing, one `todo` that
is the BBC Micro's known frame check). The page lists all four photographs.

**One thing the page does not do.** CC BY 4.0 asks that a change be
indicated. The photographs' credit line says author, source and licence but
not that the copy is resized, and that is the same for the KIM-1's CC BY-SA
photographs. The README says it. Adding the words to `MachinePhoto.astro`
changes every machine page, so it is not done here; it is a one-line change
for a later pass.

## Task 10, the final pass, 6 October 2026

The last task of the plan: break the models on purpose to see that the tests
notice, write down where the models stop, record what the page weighs and
how fast the machine runs, and bring the documents up to date. There is no
pull request for this branch and none is to be opened (Dan), so the plan's
step 6, marking it ready, does not apply. The final whole-branch review is
the controller's; this task wrote, for it, a list of every claim the page and
the documents make about the models and what shows each
(`.superpowers/sdd/2026-10-05-nes-models/final-claims-check.md`, kept out of
the repository with the other working files).

### The mutation pass

In a scratch copy of the branch outside the repository (`git archive HEAD`
into `/tmp/t10-mut`, made a git repository so each mutation could be undone
with `git checkout`, `node_modules` linked, `src/data/results.json` from CI
run 37419286133, the site built once with `npm run build`), one thing was
broken at a time and only the test files that should catch it were run, with
`nice -n 10 node --test <files>`, because the host is shared. C# mutations
ran `nice -n 10 dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --filter
ChipAccesses`. A mutation of a sentence the page shows was followed by
`npx astro build`, so that the built page and the module agreed and only the
test's own check could catch it. Mutation 2 was run for real: the solder
side's script on the real inputs, in a second copy, `/tmp/t10-mut2`.

| Mutation | What was broken | Caught by |
|---|---|---|
| 1 | x and y scales swapped in `frame.json` | "frame.json records the verdicts its figures give, and its figures are the ones its rows give" (its ratio is no longer x over y) |
| 1b | `board_frame.py`'s `fit_scale` returning y then x | pytest `tests/test_board_frame.py`: 7 of 14 failed, among them `test_the_scale_comes_from_rows_across_and_spacings_down` and `test_the_scale_and_turn_come_back` |
| 2 | `board_register.py` reading I1-back unflipped, run on the real inputs | the script itself: 139 holes, held-out median 0.457 mm, 90th percentile 0.918: STOP, exit 3; then 7 site tests, among them "registration.json passes the plan's solder row". pytest does not reach `main()`, which reads the real inputs |
| 3a | U2's two parts swapped between the consoles in `ic-table.json` | "each console's part is the one ic-table.json gives" and the legend's test |
| 3b | the same swap in `ic-table.json`, `parts.json` and the parts module together | **survived**; now "each console's part in ic-table.json is the one the fact sheet's table records" |
| 4 | U7 pad 2 and U8 pad 1 in `parts.json` and the module | "every IC has a chip or an always ... U7 is pad1 and U8 pad2" and the legend's test |
| 5 | `ChipAt` sending writes to `$4016` to `Pad1` | C#: `An_access_reaches_the_chip_its_address_decodes_to(16406, write, Apu)`, `Each_access_adds_one_to_its_chip_and_nothing_to_the_others`, `Inc_4016_reads_pad_1_once_and_writes_the_strobe_twice` (3 of 34) |
| 6 | `NesBus.Peek` calling `Note` | C#: `A_peek_adds_nothing` |
| 7 | the sampler dividing by 0.25 s, not the time measured | `nes-famicom-access.test.mjs`: "the sampler divides by the time it measured, not the time it was asked to wait" |
| 8a, 8b | no fresh baseline on `nes:region`, then on `nes:start` | "the model reads the machine through panel.nes, takes a fresh baseline on nes:start and nes:region" |
| 9a | the PAL crystal and modulator left showing after a change to NTSC | **survived**; now "a change of console leaves nothing of the other console on either model" |
| 9b | U5 left printed with its PAL part after a change | **survived**; the same new test |
| 10a | the board drawing NTSC at mount, whatever the page says | **survived**; now "the board model draws the console the page has when it mounts, not the first one (Review Focus 3)" |
| 10b | the case doing the same | "the outside model follows the machine: ... the region at mount and on nes:region" |
| 11a | the case's height 89 in the generated parts module | "the case parts module is generated from case.json" and "the case's sizes are case.json's" |
| 11b | 89 in the module, `case.json` and the results file together | "case.json's size is the published figures ... agrees with spike.json", the parts module's, the note's and the profile's tests |
| 11c | 89 typed into the case model's own code (`const TOP = 89 + CASE.feet`) | **survived**; now "the case module types none of the case's sizes"; it also fails on 203.2 typed for the depth |
| 12a | the rear check recorded as passing, three of three | "each rear connector's place ... judged at 2 mm as measured" and "the case's sizes are case.json's" |
| 12b | the rear check's limit widened to 6 mm | "each rear connector's place ... judged at 2 mm as measured" |
| 13 | the nets verdict `pass` in `copper.json` | four tests, among them "copper.json passes the plan's coverage and drills rows, and its nets row stays the FAIL" |
| 14a, 14b | a colour literal in the case module (`'#c8c4b8'`), in the board module (`0xc8c4b8`) | `model.test.mjs`: "the model and its stage hold no colour of their own" |
| 15 | the track map padded to 600,001 bytes, `copper.json`'s size and hash made to match | `model.test.mjs` "every model with a track map has it committed, inside the budget its plan set" and "the NES board's track map is committed ... inside its budget", both on the budget's own message |
| 16 | the rule that a machine claiming a model states `case` taken out | `registry.test.mjs`: "a machine that claims a model states case" |
| 17 | the NES claiming only the inside, with `case` true | `registry.test.mjs` "the real registry is valid", two of `nes.test.mjs`, and `model.test.mjs` and `nes-models.test.mjs` failing to load |
| 18a, 18b | the word "logo" in a comment, a `Path2D` in the case module | "the case's words are words: no module ... names a logo ... or holds an SVG path" |
| 18c | a path's data as a string, `{ d: 'M0 0 L10 0 L10 4 Z' }` | **survived**; the same test now also refuses a string of path data |
| 19 | the patent's PDF copied into `tools/nes-model/data/` | "no original NES input is committed" |
| 20a | 5.2 typed for the rear's miss in the note (5.16 in `case.json`) | "the outside's note states every uncertainty plainly" |
| 20b | 25.6 typed for the width in the case's caption (25.4) | **survived**; now "each console's caption on the case gives the case's size from case.json, and no figure typed by hand" |

**Seven survived, and each now has a test.** Each new test was run on its
mutation, red, and on the unmutated tree, green, in the scratch copy (`node
--test tests/nes-models.test.mjs`: 67 tests, 67 pass unmutated; one failing,
the new one, on each of 3b, 9a, 9b, 10a, 11c, 18c and 20b). Why they had
survived: the parts were compared only file with file, so a swap made in all
three passed (the fact sheet's table, written as each crop was read on 5
October, is now the record); the scene's half of a region change was in no
test and the browser check reads the legend, which `followRegion` switches,
not the scene; the board's mount was not pinned where the case's was; the
case tests read the parts module, not the code that draws; the path test
looked for `Path2D` and `d="M` only; and the caption test compared the
caption with the layout's own `describe()`. The tests that pin source text
with a regular expression follow the file's existing ones (the board's wiring
test); they catch these mutations, not every way of writing the same fault,
and the browser check remains the test of the scene.

### Where the models stop

`docs/known-differences.md` has a new section, "The NES: its two 3D models,
where they stop". Every figure in it is quoted with the data file and key it
was read from, not from memory: the case's published size and how well it is
known (`case.json`); the case checks' two revisions and what they now catch, a
gross error of scale and not one of 3 per cent, with every earlier stop
(`spike.json`); the rear connectors' failed check and its three misses, and
the 1.3 mm agreement with the patent the model's places rest on
(`case.json` `rearCheck`); the copper's failed nets check, one of ten ground
pins and two of ten +5V pins in their largest nets, the two touching
(`copper.json`), with the re-trace still open; the NTSC parts read off a
CPU-07 and placed on the CPU-10's footprints, and the PAL board drawn on the
NTSC board's copper on task 0's layout check (`parts.json`, `spike.json`);
the buffers' ports by the print and U9's jobs from the redrawing's nets;
typical heights; the PAL modulator's shift and short crystal; the board's
place in the case (about 2 mm, one moulding assumed) and the profile's spread
(about 2.5 mm); POWER's latch unknown; the lockout chip not emulated, so the
light never blinks; and what is drawn simply (square corners, a flat rear
window set in about 4.4 mm on the console, ribs as lines, no opening door,
the stage's green-white light).

**The page's lists against that section**, checked by hand, item by item: the
outside's limits each have their line there, and so do the inside's. Two
things the section said were not on the page, the PAL modulator's shift and
the PAL crystal's size, so the PAL board's limits now say them, in words with
no figure (the figures are `parts.json`'s, in the section). What the section
says beyond the page is the history of the checks, which the page needs only
in short.

**Three sentences on the page said more than their evidence, and were
changed** (6 October 2026, from the claims list):

- The case's note said the patent's proportions "were checked against it, and
  the depth and the height passed", which read as a tight check. It now goes
  on: "on limits widened after the drawings were measured, so that the check
  catches a gross error of scale and not a small one (two earlier checks, on
  photographs and then on the drawings with tighter limits, stopped, and stay
  on record)".
- The board's note said "of the board's 480 drilled holes". 480 is the number
  `registration.json` found drilled, of 511 holes matched, not a count of the
  board's holes. It now says "of the 480 drilled holes found on both scans".
- The PAL board's limits gained the modulator and crystal sentence above.

### What the documents now say

- **`README.md`**: under "Where it stands", that the NES's page has two 3D
  models, the outside and the inside, each for both consoles, measured from
  photographs, a design patent and bare board scans, with a note under each
  saying how well each part is known. In the licences: the NES's photographs,
  public domain for the NTSC console's two and CC BY 4.0 for the PAL
  console's two, and the board's track map, traced from OpenTendo's scans,
  read from the `dbhq-uk` fork, and so on the TAPR Open Hardware License's
  terms, with `NOTICE.md` saying what is derived. No figure.
- **`AGENTS.md`**'s layout: `site/src/models/` with the NES's two modules,
  `tools/nes-model/`, `NOTICE.md`'s three entries, and `docs/nes/facts/`'s
  `models.md`.
- **`docs/the-6502-family.md`** is unchanged: its KIM-1 and BBC Micro lines do
  not mention their models either, so the NES's does not.
- **`site/DESIGN.md`**'s "The NES's 3D models", from task 9, was read against
  the page and is still true; unchanged.
- **`site/README.md`** says the NES's models are measured offline by
  `tools/nes-model/`, as the KIM-1's are by its tool.
- **`tools/nes-model/README.md`** no longer says "so far": it lists every
  script, the run order, the fetch table with the fork's addresses, `pdfimages`
  and the venv, and now points at the journal and at the known-differences
  section, and says what the mutation pass saw `board_register.py` do on an
  unflipped back scan.

### What the page weighs

`cd site && node scripts/page-weight.mjs /machines/nes/` (and `/machines/kim-1/`,
`/machines/bbc-micro/`), on the scratch copy's build of this task's code with
CI's results file (run 37419286133), 6 October 2026, 20:27 UTC. Bytes gzipped
at level 9:

| | NES | KIM-1 | BBC Micro |
|---|---|---|---|
| When the page opens, total | 52,643 | 114,635 | 58,330 |
| of which the HTML | 21,347 | 14,454 | 16,872 |
| of which `model-loader.js` | 1,530 | 1,530 | none |
| of which the main photograph | 10,812 | 87,072 | 22,030 |
| When scrolled to, the other photographs | 82,776 | 301,442 | none |
| Later, the outside's bundle, when the models are reached | 166,237 | | |
| Later, the inside's bundle and track map, when its tab is chosen | 163,161 and 144,726 | 163,048 and 314,732 (the board, the KIM-1's only model) | |

Nothing under `/models/` is in any page's first load. Each bundle is inside
its 200,000 bytes, and the NES's track map inside its 600,000. Since task 9's
record (51,025 at first load), the NES page has gained the photographs
section of task 8's follow-up, task 9's fix round and this task's three
sentences; the growth is not split by commit. The machines' WebAssembly is
fetched on Start and is not counted by the script.

### The speed: the README's method, the native half

The plan owed the README's method (`bench/nes-speed/README.md`), the native
bench and the browser build compiled ahead of time, alternating the code
before task 6 (`f9e84b0`) and after, on a quiet machine. The controller's
ruling for this task: run it only while the one-minute load average is under
about 4, checking between rounds, and otherwise record that it was not
measured.

The native half ran in a window of 6 October 2026, 20:28:03 to 20:29:05 UTC,
when the load average fell from 3.98 to 1.53 (checked before each round and
each launch). Both trees were `git archive` exports outside the repository
(`/tmp/t10-before` at `f9e84b0`, `/tmp/t10-after` at this branch's head),
each built with `dotnet build -c Release bench/nes-speed/native`, then, three
rounds, NTSC and PAL, the order swapped each round:

```
cd /tmp/t10-<before|after>/bench/nes-speed
dotnet native/bin/Release/net10.0/Dbhq.Machines.Nes.SpeedNative.dll 5 1790000 <ntsc|pal>
```

Every launch printed `rendering yes` and `picture changes yes`. In times real
time, 15 timed runs a side:

| Region | Before, median | After, median | After over before | Per launch, after over before |
|---|---|---|---|---|
| NTSC | 1.73 | 1.74 | 1.006 | 0.902, 0.977, 1.093 |
| PAL | 1.88 | 2.11 | 1.122 | 1.043, 1.162, 0.963 |

**No slowdown is seen, and the method cannot show one of 2 per cent here.**
Single runs went from 1.39 to 2.46 times real time, and launch against launch
by up to 16 per cent either way. The load average was low, but the host was
not quiet: these figures are under half the quiet native figure of 5 October
(4.00 on NTSC, the task 6 entry), and `vmstat` straight after showed about
half the CPUs busy and the load back at 19 within a minute. Task 6's paired
runs, both builds at once on swapped CPUs, remain the better evidence: no
slowdown to within about 2 per cent, with PAL alone unable to rule out 3.

### The speed: the browser half, compiled ahead of time

The load stayed high for an hour after the native half (19.17 at 20:29 UTC,
swinging between 4 and 51, 33.02 at 21:12), so the browser half waited for
the browser check below. Then both trees were published as the README says,
`dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release -p:RunAOTCompilation=true
-o bench/nes-speed/publish/aot` (with `obj/Release` cleared first) and `npm ci`
in `bench/nes-speed`, and run one launch at a time, alternated as before:

```
cd /tmp/t10-<before|after>/bench/nes-speed
node run-in-browser.mjs publish/aot 1 1790000 5000000 5 <0 for NTSC|1 for PAL>
```

Set 1 ran from 21:43:32 to 21:45:00 UTC, the load average falling from 3.75 to
1.28; set 2 started at 21:45:50 at 2.14 and stopped itself after two rounds,
the load having reached 5.58 at the third. Chrome 153, every launch's ROM
hash checked. In times real time:

| Region | Set | Before, median | After, median | After over before | Per launch |
|---|---|---|---|---|---|
| NTSC | 1 (15 runs a side) | 1.85 | 1.78 | 0.962 | 0.971, 0.915, 1.108 |
| NTSC | 2 (10 runs a side) | 2.01 | 1.83 | 0.910 | 0.868, 0.951 |
| PAL | 1 | 2.19 | 2.01 | 0.918 | 0.915, 0.866, 1.356 |
| PAL | 2 | 1.85 | 2.18 | 1.178 | 1.244, 1.106 |

Over both sets, 25 runs a side: NTSC 1.89 before and 1.78 after (0.942), PAL
2.11 and 2.13 (1.010). **This does not settle it.** PAL changes sign from set
to set, and single launches differ by up to a third (0.87 to 1.36). NTSC is
lower after in both sets, so a slowdown of a few per cent in the browser on
NTSC cannot be ruled out. The host was not quiet even then: `vmstat` read
about half the CPUs busy at a load average of 1.4, and these figures are
below the quiet ones of 5 October (NTSC 2.09 to 2.82, PAL 2.44 to 3.24 times
real time in the browser, the NES speed entry). For scale: a timed run lasts
about half a second and reaches a counted chip about 107 thousand times (task
6's count of SNOW), so 4 per cent of it would be about 200 ns a count, far
more than an increment and a switch on an address should cost. That is
arithmetic, not a measurement. **The headroom**, if NTSC lost 6 per cent, would
still be about twice real time on a quiet machine, by the 5 October figures;
it was not measured again. The measurement on a quiet host stays owed, in the
plan's Deferred list.

### The whole browser check: run 1 failed outside the NES, run 2 passed whole

`cd site && nice -n 10 node scripts/browser-check.mjs`, Chrome 153 with
software WebGL, in the scratch copy with the NES machine built again from
this branch (`node scripts/build-machines.mjs nes`, compiled ahead of time; the
copy's earlier build predated task 6's `AccessCounts`). Started at 20:51:58
UTC, when the load average was 7.76, under the ruling's 8; ended at 21:12:52,
load 33.02, having risen past 40 on the way. **Run 1** failed in two parts,
both outside the NES:

- **The KIM-1's phone steps**: "a finger on the focused model scrolled the
  page", the page moving 19 pixels (scrollY 3241 to 3222) while one finger
  turned the focused model. The same step, the same 19 pixels, failed once in
  task 7's third run at a load of about 21, and passed in the other runs on the
  same markup. Task 10 changed nothing the KIM-1's page or model runs.
- **The BBC Micro**: "the picture is not being redrawn (fields 129853 then
  129873)", 20 fields in a second where a quiet run gives about 50, with its
  speed line "Running at 0.55 MHz, slower than the BBC Micro's 2 MHz: this
  browser cannot keep up". The same failure as in tasks 7 and 9 under load.

**Every NES step passed**, among them: nothing under `/models/` before
scrolling; the outside alone fetched when the models are reached; RESET before
Start doing nothing; POWER starting the machine, the light lit and POWER in;
the page's Reset and the model's RESET each putting RESET down; under the
case drawn; the page to PAL with the inside not loaded, then Inside chosen,
drawing PAL; U5, U6 and U7 marked with rates on NTSC and on PAL; the region
switched with nothing downloaded; the tracks buttons; the solder side's
copper under the board; the tabs' keys and cameras; Tab leaving the section;
the recorded frames' hashes in both regions; reduced motion. Its model lines:

```text
nes: model files requested before scrolling to the models: none
nes: outside model running in 3082 ms after scrolling to it, fetched /models/nes-famicom-case.js; region ntsc, light dark, POWER out
nes: a click on the model's RESET before Start: the machine is ready, fetched nothing; RESET went down 0 times
nes: started by POWER on the model: light lit, POWER in
nes: accessCounts, an Int32Array, [58883,7476,8928,0] then [64298,7825,9384,0] half a second later; running() true, region() NTSC
nes: the page's Reset: RESET went nes:reset, down, up; presses 0 then 1
nes: under the case, polar 3.142: 31.3% of the canvas drawn
nes: the page to PAL with the inside not loaded (nothing of it fetched): the outside drew pal; Inside chosen: it running, drawing pal, described by model-about-inside-pal; fetched /models/nes-famicom-board.js, /models/nes-famicom-board-tracks.webp
nes: on NTSC, after 198 ms, chips marked U5 U6 U7, legend rows marked U5 U6 U7; highest legend rates: U5 532 and U7 440 a second
nes: on PAL, after 92 ms, chips marked U5 U6 U7; highest legend rates: U5 1463, U7 1141
nes: under the board, polar 3.142: the map's solder-side copper at 300 points is 110,95,34, its bare board at 300 points 28,73,23, 85.6 apart; the same points mirrored side to side 9.9 apart
nes: reduced motion (asked for): the views running and running, switched back to model-tab-outside
```

**Run 2**, the one retry the ruling allowed, the same command on the same
build, started at 21:18:14 UTC at a load of 6.75 and ended at 21:35:29 at
1.75, though the load went to 48.91 in between. **It passed every part**: the
KIM-1's, the phone step with it (one finger on the focused model moved the
page not at all, scrollY 3241 to 3241); the BBC Micro's, its picture redrawn
(fields 129858 then 129914 a second later) at its own 2 MHz; and every NES
step, ending "no console errors, no failed requests, no CSP violations". Its
NES model lines, in short: nothing under `/models/` before scrolling; the
outside running in 622 ms with only its own bundle fetched; started by POWER,
the light lit and POWER in; on NTSC, U5, U6 and U7 marked within 602 ms, and
on PAL within 84 ms; the inside loaded after the page went to PAL, drawing
PAL; reduced motion switching both views. The NES's speed line read "Running
at the NES's own 1.79 MHz, with little to spare in this browser", capacity
2.58 MHz.

So the two runs differ only in what the host was doing: the two parts that
failed in run 1 passed in run 2 with no change between them. The rates are
live readings on a busy host and differ from run to run.

### Tests and floors

- **Five tests added** to `site/tests/nes-models.test.mjs`, and one assertion
  to the existing test of the case's words, each for a mutation that had
  survived (the table above).
- **The whole site suite**, in the scratch copy with this task's changes, after
  `npm run build` with the NES machine built and CI's results file:
  `nice -n 10 node --test tests/*.test.mjs`, 6 October 2026, 20:49 UTC: 474
  tests, 473 pass, 0 fail, 1 todo (the BBC Micro's known frame check). Both
  floors raised from 468 to 473, with a dated comment.
- **Not re-run here**: the C# suite as a whole (no C# changed in this task; the
  two C# mutations ran the NES project's `ChipAccesses` tests, 34 of them,
  which pass unmutated) and the tools' pytest suite (no tool changed; the scale
  mutation ran `tests/test_board_frame.py`).

### Left open

- The README's speed method on a quiet host: both halves ran, the native one
  at a load of 1.5 to 4 and the browser one at 1.3 to 5.6, but neither could
  resolve 2 per cent on this host (the plan's Deferred list).
- The rear connectors' recheck and the copper's re-trace, as before (the plan's
  Deferred list), and the site-wide question of saying on the page that a
  photograph's copy is resized, which CC BY asks (task 8's follow-up).

## Final fix wave, 6 October 2026

The final whole-branch review (the controller's) found no overclaim and no
blocker, and asked for one fix wave: one important item and six minor ones.
All are done.

- **The NES browser check's wait for a camera** (important).
  `settled()` in `site/scripts/browser-check-nes.mjs` caught its own 30 s
  timeout and carried on, so a view whose camera never came to rest was still
  read, and "a view kept its camera" could pass by chance. The KIM-1's check
  throws "the camera never came to rest". Now a timeout is a problem, "nes:
  the camera never came to rest in" the view, `settled()` gives null, and no
  caller compares after a null: the view from under the case is not judged,
  and the tabs' camera check is not made, its line saying so. `settled()` is
  exported, and `site/tests/browser-check-nes.test.mjs` tests it on a made-up
  page: two tests, one failing against the old wait (the problem was not
  pushed), both passing on the new. `node --check` passes on the script. The
  NES part of the browser check alone, through the harness task 9 kept outside
  the repository (`/tmp/t9smoke/nesonly.mjs`), ran once on the scratch copy's
  build, from 22:01:53 to 22:05:18 UTC, the load average 1.87 at the start:
  no problems; the cameras came to rest and were compared ("the inside's
  camera kept true").
- **O2-FL is marked committed**: `tools/nes-model/data/sources.json` gives
  `committed: "nes.webp"`, the main photograph, its resized copy. The
  photographs' test now expects it among the committed and checks
  `nes.webp` itself: not the original's hash, the main photograph credited at
  O2-FL's page, and the README's section giving the original's SHA-256 and the
  copy's, which is the file's.
- **The PAL board's caption and note say what was checked.** Task 0's check
  compared the ten chips' places on the PAL board with the scan's (P1 to P3
  could not be measured on I3, and no copper was compared). The caption said
  the CPU-10's "layout" was checked to be shared; it now says "whose chips'
  places the PAL console's NES-CPU-11 was checked to share". The PAL note
  said the check "found the two boards to be one layout"; it now says it
  "found the PAL board's chips where the scanned board's are, within its
  limits; the two boards' copper was not compared". A test pins the caption
  and fails on "whose layout" or "one layout" in the caption or the note. The
  legend says nothing of the layout. `docs/known-differences.md` says the same.
- **How well the size and the profile are known are numbers now**, not words
  for `results.py` to parse. `case_measure.py` writes `footprint.goodToPct`,
  the largest disagreement between the published proportions and the patent's
  drawings in task 0's judged figures (3.08 per cent) to a whole per cent, and
  `profileCheck.goodToMm`, half the range of the four ends, (17.71 - 12.86) /
  2 = 2.425 mm to one place, each with a `goodToWhy` beside it; its sentences
  are built from them. `results.py` reads the numbers, stops if one is missing
  or the profile's is not half the ends' range, and no longer parses "good to
  about". Run once each, 6 October 2026:
  `cd tools/nes-model && NES_MODEL_INPUTS=/tmp/nes-inputs nice -n 10 /tmp/nesvenv/bin/python case_measure.py`
  (21:58:46 to 21:59:41 UTC) and
  `nice -n 10 /tmp/nesvenv/bin/python tools/nes-model/results.py`. Nothing
  measured moved: in `case.json` the four new fields were added and two
  sentences changed, "good to about 2.5 mm" to "good to about 2.4 mm" (one of
  them gaining "half the four ends' range"); in the case parts module only
  `PROFILE`'s note changed, the same way; the case's results file changed
  `goodToMm` 2.5 to 2.4 and `goodToPct` 3.0 to 3; the board's results file
  came out byte for byte the same. pytest `tests/test_results.py` was changed
  first and failed 2 of 6, then passed 6 of 6 with the new `results.py`;
  `tests/test_case.py` passes too (19 in the two files).
- **The journal's task 6 speed sentence** keeps its dated bold "no slowdown
  measurable, to within about 2 per cent, paired under load" and now points
  on: "(task 10's README-method run, below, did not settle it)".
- **`case_measure.py`'s comment** on the patent profile's first row said the
  row 0.88 mm up reads about 15.5; the data's next row kept is 2.0 mm up, at
  14.63, and the comment now says that. Comment only.
- **"task 6 checks it"** in the design (the controller ports' mapping) and in
  the plan's task 5 is history: each now adds "(it did, 6 Oct 2026)".

**What changed on the page**, compared sentence by sentence (every caption,
note, limit, legend line, help and status line made from the modules, before
and after, with `diff`): on the case's note, for both consoles, "an average
good to about 2.5 mm" became "2.4 mm", in the paragraph on the profile and in
the limit "The bottom shell's lean is an average, good to about 2.4 mm"; the
PAL board's caption and its note's last sentence, as above. Nothing else on
the page changed; the "about 3 per cent" still reads 3.

**Tests.** The covering files (`nes-models`, `model-regions`, `nes-panel`,
`nes-page`, `model`, `registry` and the new `browser-check-nes`): 183 tests,
183 pass. The whole site suite, in the scratch copy after `npm run build`
with CI's results file and the machines built:
`nice -n 10 node --test tests/*.test.mjs`, 6 October 2026, 22:05 UTC: 476
tests, 475 pass, 0 fail, 1 todo. Its first run had failed one test, "the README
lists every test file", until the new test file was listed in
`site/README.md`'s table. Both floors raised from 473 to 475.

After the fix wave's re-review, two more: the case's note gave the published
sources' small disagreement as the reason the size is good to about 3 per
cent, and now says "against the design patent's drawings it is good to about 3
per cent", the reason `goodToWhy` records (`case.json`'s `widthSource` gives
no reason, so it and `case.json` were left as they were); and a paragraph of
`docs/known-differences.md` was rewrapped to the file's width.
