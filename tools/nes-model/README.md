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

So far (task 0) it holds:

| File | What it does |
|---|---|
| `common.py` | Where things are; the inputs and their hashes; finding the pads on a scan; reading the KiCad redrawing's footprints and outline; fitting one set of points to another (similarity, affine, homography, or a homography and a cubic correction) with the error measured on points held out of the fit; writing the data files. Adapted from the BBC Micro's |
| `verify.py` | Checks every input against its SHA-256 |
| `spike.py` | Task 0's eight checks: the scan's x and y scales and the two against each other, the solder side's registration, the PAL board's layout against the scan's, the case's depth and height on the design patent's orthographic views (the corner photographs' figures recorded only, since the plan's revision of 5 October 2026), and the PAL front against the patent's front view, each judged against the plan's thresholds. Kept as a record; not part of the run |
| `tests/test_common.py` | pytest, on made-up inputs |
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
```

`verify.py` says, for each input, present, missing or hash differs. It exits
non-zero when a hash differs, or when an input it was asked for by name is
missing; an input not fetched yet is only reported. `spike.py` takes about
five minutes; it exits 3 when a figure crosses one of task 0's STOP
thresholds (it does: the case's depth and the PAL front, both as revised, see
`data/spike.json`). `out/` is
git-ignored.

The versions it was run with, and every figure it printed, are in the journal
for 5 October 2026.

## Licences

The scripts are MIT, like the rest of the repository. What they read is not:
[`data/README.md`](data/README.md) says which data file carries which terms.
