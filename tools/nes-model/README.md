# The NES models' measurements

Offline Python that measures the NES's two 3D models, the outside (the
front-loading case, its buttons, LED and ports) and the inside (the main
board, its chips and both faces of copper), each for the NTSC NES-001 and the
PAL NESE-001, from flatbed scans of a bare board, photographs, a design patent
and a KiCad redrawing used only as a cross-check, and writes what the site
uses. The design is
[`docs/superpowers/specs/2026-10-05-nes-models-design.md`](../../docs/superpowers/specs/2026-10-05-nes-models-design.md)
and the plan
[`docs/superpowers/plans/2026-10-05-nes-models.md`](../../docs/superpowers/plans/2026-10-05-nes-models.md);
the sources and why each was chosen are
[`docs/nes/facts/models.md`](../../docs/nes/facts/models.md).

**It is offline and never fetches.** The site's build and tests never run it:
its outputs are committed. The inputs are fetched by hand, once, into a folder
outside the repository, and every script refuses an input whose SHA-256 is not
the one in [`data/sources.json`](data/sources.json). No input is committed,
not even resized.

So far (tasks 0 to 5, and 8) it holds:

| File | What it does |
|---|---|
| `common.py` | Where things are; the inputs and their hashes; finding the pads on a scan; reading the KiCad redrawing's footprints and outline; fitting one set of points to another (similarity, affine, homography, or a homography and a cubic correction) with the error measured on points held out of the fit; writing the data files. Adapted from the BBC Micro's |
| `verify.py` | Checks every input against its SHA-256 |
| `spike.py` | Task 0's eight checks: the scan's x and y scales and the two against each other, the solder side's registration, the PAL board's layout against the scan's, the case's depth and height on the design patent's orthographic views (the corner photographs' figures recorded only, since the plan's revisions of 5 October 2026), and the PAL front, O4 against O2-FL's front, each judged against the plan's thresholds. Kept as a record; not part of the run |
| `board_frame.py` | Task 2: the board's frame on the bare scan I1-front. The x scale from the rows of pins across the board and the y scale from the DIPs' row spacings, both drill to drill, each row and footprint held out in turn and judged against the plan's thresholds; the board's turn; the outline, its shape marked by hand and each edge measured on the light, with its round notches; the mounting holes on their top rims. Writes `data/frame.json`, a copy of the scan rectified at 12 px/mm and overlays to look at, in `out/` |
| `board_register.py` | Task 3: the solder side, I1-back flipped, registered to the component side on every hole found on both faces (task 0's ring finder, and a second finder for vias, large rings and domes of solder), an affine and a cubic each scored on a chequerboard of 20 mm blocks held out, judged against the plan's solder row with the plan's outlier rule beside; then the drills, the pads on each face, the edge fingers, and the footprints, grouped by pitch and the print and named by hand from `data/marks.json`. Writes `data/registration.json` and overlays to look at, in `out/` |
| `board_trace.py` | Task 4: the copper on both faces and the print, traced from I1-front and I1-back (the solder side through task 3's affine) on one grid at 12 px/mm: OKLab; tin by its grey; the print by its white, off the pads; copper under the lacquer lighter than a smooth level fitted to the laminate, over Otsu's threshold with hysteresis; the lacquer's bright rim round each pad taken off except where a track runs on past it; copper under thin print recovered where it continues in line. Then the plan's checks (coverage, drills in copper, and the known nets from `data/ic-table.json`, the held-out test) and the track map. `--look` draws the overlays and prints the map's sizes and runs no check; `--map-ppm N` runs the checks and writes `data/copper.json` and the map |
| `board_parts.py` | Task 5: every part on the board, both consoles. Each IC on its footprint (its pads' centre, its turn from pin 1); I4 and I5 (the NTSC NES-CPU-07, top and solder side) fitted to the board by a homography on the ICs' midlines and solder joints, I3 (the PAL NES-CPU-11) by task 0's marks, each IC held out in turn to show each photographed part sits on the footprint of its name; the places against the KiCad redrawing's after a best-fit similarity (the plan's 3 mm); the bodies measured on I4; the connectors, crystals and modulators from outlines marked on I4 and I3; the passives at their pads. Each console's parts are in the script, read by looking at crops. Writes `data/parts.json`, each console's part into `data/ic-table.json`, and `site/src/models/nes-famicom-board-parts.mjs` |
| `case_measure.py` | Task 8: the case, both consoles. The size is the published 254 by 203.2 by 88.9 mm (not Nintendo's); every face of a photograph is rectified on its own four bounding lines (O2-FL's front and top, O2-BR's rear, the set-back of the rear's window panel taken off), so no camera relates two of its planes; the patent's views (O1) scaled to the case for what the photographs cannot show (the underside, the AV jacks' side, the feet) and recorded beside the rest; the end profile built on O2-FL and checked on a point of O2-BR held out of it; the board's place in the case from O9, the board and the bottom shell in one view, through the camera its XMP records; the rear connectors' board places (I7-FL's modulator face) against O2-BR; the PAL console's words (O4, O10) and underside labels (O5). Writes `data/case.json` and `site/src/models/nes-famicom-case-parts.mjs`; `--look` draws the rectified faces, with what was read off them, in `out/` |
| `run-case.sh` | The outside model's measurements: `verify.py` on its inputs, then `case_measure.py`. It reads `parts.json` and `registration.json`, so `run-board.sh` comes first |
| `run-board.sh` | The inside model's measurements in order (so far: `verify.py I1-front I1-back I2`, `board_frame.py`, `board_register.py`, `board_trace.py --map-ppm 10`, `verify.py I3 I4 I5`, `board_parts.py`); stops at the first that crosses a STOP |
| `tests/test_common.py`, `tests/test_board_frame.py`, `tests/test_board_register.py`, `tests/test_board_trace.py`, `tests/test_board_parts.py`, `tests/test_case.py` | pytest, on made-up inputs |
| `data/` | See [`data/README.md`](data/README.md) |

## Fetching the inputs by hand

Make one folder, outside the repository, and fetch each input into it under
the name `data/sources.json` gives as its `file`. OpenTendo's three files come
from the `dbhq-uk` fork at the pinned commit, never from the upstream
repository (AGENTS.md rule 3).

| Id | Name in the folder | Fetch from |
|---|---|---|
| O1 | `USD299726.pdf` | [US Design Patent D299,726](https://patentimages.storage.googleapis.com/pdfs/USD299726.pdf) |
| O2-FL | `Nintendo-Entertainment-System-NES-Console-FL.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-FL.jpg) (the original file) |
| O2-BL | `Nintendo-Entertainment-System-NES-Console-BL.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-BL.jpg) (the original file) |
| O2-BR | `Nintendo-Entertainment-System-NES-Console-BR.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-BR.jpg) (the original file) |
| O4 | `Geöffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png` | [Commons](https://commons.wikimedia.org/wiki/File:Ge%C3%B6ffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png) (the original file, 93 MB) |
| O5 | `Unterseite_NES_NESE-001_20221102_132229.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Unterseite_NES_NESE-001_20221102_132229.jpg) (the original file) |
| I1-front | `opentendo_NES-CPU-10_front_300dpi.png` | [the fork, `Scans/NES-CPU-10_front_300dpi.png`](https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Scans/NES-CPU-10_front_300dpi.png) |
| I1-back | `opentendo_NES-CPU-10_back_300dpi.png` | [the fork, `Scans/NES-CPU-10_back_300dpi.png`](https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Scans/NES-CPU-10_back_300dpi.png) |
| I2 | `opentendo_Motherboard.kicad_pcb` | [the fork, `Board Files/Motherboard.kicad_pcb`](https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Board%20Files/Motherboard.kicad_pcb) |
| I3 | `Frontalansicht_Mainboard_NES_NESE-001_HOF06378.png` | [Commons](https://commons.wikimedia.org/wiki/File:Frontalansicht_Mainboard_NES_NESE-001_HOF06378.png) (the original file, 77 MB) |
| I4 | `Nintendo-NES-Mk1-Motherboard-Top.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-NES-Mk1-Motherboard-Top.jpg) (the original file) |
| I5 | `Nintendo-NES-Mk1-Motherboard-Bottom.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-NES-Mk1-Motherboard-Bottom.jpg) (the original file) |
| I6 | `RP2A07A_20221102.png` | [Commons](https://commons.wikimedia.org/wiki/File:RP2A07A_20221102.png) (the original file) |
| O3-01 to O3-04 | `Nintendo-Entertainment-System-NES-Deconstruction-01.jpg` to `-04.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Deconstruction-01.jpg) (the original files; the others the same with `-02` to `-04`) |
| I7-FL | `Nintendo-Entertainment-System-NES-Motherboard-FL.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Motherboard-FL.jpg) (the original file) |
| I7-FR | `Nintendo-Entertainment-System-NES-Motherboard-FR.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Motherboard-FR.jpg) (the original file) |
| I7-Bottom | `Nintendo-Entertainment-System-NES-Motherboard-Bottom.jpg` | [Commons](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Motherboard-Bottom.jpg) (the original file) |
| O9 | `Geöffnetes_deutsches_NES_20221102_HOF06601_RAW-Export.png` | [Commons](https://commons.wikimedia.org/wiki/File:Ge%C3%B6ffnetes_deutsches_NES_20221102_HOF06601_RAW-Export.png) (the original file, 150 MB) |
| O10 | `Geöffnetes_deutsches_NES_20221102_HOF06440_RAW-Export.png` | [Commons](https://commons.wikimedia.org/wiki/File:Ge%C3%B6ffnetes_deutsches_NES_20221102_HOF06440_RAW-Export.png) (the original file, 125 MB) |

Later tasks add inputs to `data/sources.json` as they first need them, with
the same fields, and to the table in `docs/nes/facts/models.md`, which a site
test holds equal to it.

## Running it

Python 3.12 with numpy, opencv-python-headless, Pillow, scipy and pytest, in a
virtual environment so nothing touches the system or the site, and
`pdfimages` from poppler-utils, which `spike.py` uses to take the patent's
sheets out of O1 at their own resolution:

```
python3 -m venv /tmp/nesvenv
/tmp/nesvenv/bin/pip install numpy opencv-python-headless pillow scipy pytest
```

Point `NES_MODEL_INPUTS` at the folder of inputs, check them, and run:

```
export NES_MODEL_INPUTS=~/dbhq-previews/nes-model-research/full
/tmp/nesvenv/bin/python tools/nes-model/verify.py          # every input; or name some: verify.py I1-front I2
/tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q
cd tools/nes-model && /tmp/nesvenv/bin/python spike.py      # writes data/spike.json, and overlays in out/
PYTHON=/tmp/nesvenv/bin/python tools/nes-model/run-board.sh   # the board's measurements in order: data/frame.json, data/registration.json, ...
PYTHON=/tmp/nesvenv/bin/python tools/nes-model/run-case.sh    # the case's: data/case.json and the case parts module
```

`verify.py` says, for each input, present, missing or hash differs. It exits
non-zero when a hash differs, or when an input it was asked for by name is
missing; an input not fetched yet is only reported. `spike.py` takes about
five minutes; it exits 3 when a figure crosses one of task 0's STOP
thresholds. As the plan was revised twice on 5 October 2026 none does; the
earlier stops are kept in `data/spike.json`'s `revision`. `board_frame.py`
takes about a minute and exits 3 if the scale crosses a STOP;
`board_register.py` takes about eight minutes on a busy machine and exits 3
if the solder side crosses its STOP. `case_measure.py` takes about two
minutes; none of its checks stops anything, and it prints each. `out/` is git-ignored.

The versions it was run with, and every figure it printed, are in the journal
for 5 October 2026.

## Licences

The scripts are MIT, like the rest of the repository. What they read is not:
[`data/README.md`](data/README.md) says which data file carries which terms.
