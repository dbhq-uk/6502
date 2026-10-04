# The BBC Micro models' measurements

Offline Python that measures the BBC Micro Model B's two 3D models, the
outside (case, keys, LEDs) and the inside (the Issue 7 main board, its chips
and both faces of copper), from flatbed scans, photographs and an MIT KiCad
keyboard, and writes what the site uses. The design is
[`docs/superpowers/specs/2026-10-04-bbc-micro-models-design.md`](../../docs/superpowers/specs/2026-10-04-bbc-micro-models-design.md)
and the plan
[`docs/superpowers/plans/2026-10-04-bbc-micro-models.md`](../../docs/superpowers/plans/2026-10-04-bbc-micro-models.md);
the sources and why each was chosen are
[`docs/bbc-micro/facts/models.md`](../../docs/bbc-micro/facts/models.md).

**It is offline and never fetches.** The site's build and tests never run it:
its outputs are committed. The inputs are fetched by hand, once, into a folder
outside the repository, and every script refuses an input whose SHA-256 is not
the one in [`data/sources.json`](data/sources.json). The scans and the
photographs with no stated licence are never committed, not even resized.

So far (tasks 0 and 2) it holds:

| File | What it does |
|---|---|
| `common.py` | Where things are; the inputs and their hashes; finding the tinned pads on a scan; fitting one set of points to another (similarity, affine, homography, or a homography and a cubic correction) with the error measured on points held out of the fit; writing the data files |
| `verify.py` | Checks every input against its SHA-256 |
| `spike.py` | Task 0's three numbers: the scan's scale, the solder side's registration and the keys' registration, each judged against the plan's thresholds. Kept as a record; not part of the run |
| `board_frame.py` | Task 2: the board frame on scan I1: x and y scales from rows of pads (held out and judged), the board's turn, its outline and its holes; writes `data/frame.json` and a rectified copy of I1 in `out/` |
| `board_register.py` | Task 3: the solder side (I2, mirrored) registered to I1 on every hole found, tinned pads and open rings, and judged on holes held out; the pads, drills and footprints (DIPs with pin 1, connectors, SIPs); the mounting holes on their top rims; writes `data/registration.json` and overlays in `out/` |
| `run-board.sh` | The inside model's scripts, in order (so far `verify.py I1 I2`, `board_frame.py` and `board_register.py`) |
| `tests/test_common.py`, `tests/test_board_frame.py`, `tests/test_board_register.py` | pytest, on made-up inputs |
| `data/` | See [`data/README.md`](data/README.md) |

## Fetching the inputs by hand

Make one folder, outside the repository, and fetch each input into it under
the name `data/sources.json` gives as its `file`:

| Id | Name in the folder | Fetch from |
|---|---|---|
| O1 | `outside-1-top-Acorn_BBC_Microcomputer.jpg` | [Commons: Acorn BBC Microcomputer.jpg](https://commons.wikimedia.org/wiki/File:Acorn_BBC_Microcomputer.jpg) (the original file) |
| O2 | `outside-2-rear-BBC_Micro_rear.jpeg` | [Commons: BBC Micro rear.jpeg](https://commons.wikimedia.org/wiki/File:BBC_Micro_rear.jpeg) (the original file) |
| O3 | `outside-3-keyboard-BBC_Micro_Type_1.jpg` | [Commons: BBC Micro Type 1.jpg](https://commons.wikimedia.org/wiki/File:BBC_Micro_Type_1.jpg) (the original file) |
| I1 | `inside-1-bare-iss7-top-amb5l.jpg` | [amb5l's top scan](https://drive.google.com/file/d/1llcLHl_ax6rtZRWxdoSyA5jNjFOo_-3N/view), posted in [stardot t=23329](https://stardot.org.uk/forums/viewtopic.php?t=23329) |
| I2 | `inside-supp-bare-iss7-bottom-amb5l.jpg` | [amb5l's bottom scan](https://drive.google.com/file/d/1EIRg4Ump3T5ixNCJtVkrz7DwODJ__YFn/view), the same post |
| I5 | `inside-2-populated-iss7-8bs.jpg` | [8BS](https://8bs.com/see/BBC_B_iss7.jpg) |
| K1 | `bbc-keyboard/` | `git clone https://github.com/dominicbeesley/bbc-keyboard` into the folder, then `git checkout 5ec53df566c77424caa6be827d0831da3916ecfa` inside it; the file read is `bbc-keyboard/KiCad/bbc-keyboard/bbc-keyboard.kicad_pcb` |

Later tasks add inputs (more photographs, the Service Manual) to
`data/sources.json` as they first need them, with the same fields.

## Running it

Python 3.12 with numpy, opencv-python-headless, Pillow, scipy and pytest, in a
virtual environment so nothing touches the system or the site:

```
python3 -m venv /tmp/bbcvenv
/tmp/bbcvenv/bin/pip install numpy opencv-python-headless pillow scipy pytest
```

Point `BBC_MODEL_INPUTS` at the folder of inputs, check them, and run:

```
export BBC_MODEL_INPUTS=~/dbhq-previews/bbc-model-research
/tmp/bbcvenv/bin/python tools/bbc-micro-model/verify.py          # every input; or name some: verify.py I1 I2
/tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q
cd tools/bbc-micro-model && /tmp/bbcvenv/bin/python spike.py      # writes data/spike.json, and overlays in out/
PYTHON=/tmp/bbcvenv/bin/python tools/bbc-micro-model/run-board.sh   # writes data/frame.json and data/registration.json, and overlays in out/
```

`verify.py` says, for each input, present, missing or hash differs. It exits
non-zero when a hash differs, or when an input it was asked for by name is
missing; an input not fetched yet is only reported. `spike.py` exits 3 when a
figure crosses one of the STOP thresholds as task 0 set them (it does: the
case's front edge, which the plan has since revised; see `data/spike.json`). `out/` is git-ignored.

The versions it was run with, and every figure it printed, are in the journal
for 4 October 2026.

## Licences

The scripts are MIT, like the rest of the repository. What they read is not:
[`data/README.md`](data/README.md) says which data file carries which terms.
