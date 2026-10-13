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

So far (tasks 0, 2, 3, 4 and 5) it holds:

| File | What it does |
|---|---|
| `common.py` | Where things are; the inputs and their hashes; finding the tinned pads on a scan; fitting one set of points to another (similarity, affine, homography, or a homography and a cubic correction) with the error measured on points held out of the fit; writing the data files |
| `verify.py` | Checks every input against its SHA-256 |
| `spike.py` | Task 0's three numbers: the scan's scale, the solder side's registration and the keys' registration, each judged against the plan's thresholds. Kept as a record; not part of the run |
| `board_frame.py` | Task 2: the board frame on scan I1: x and y scales from rows of pads (held out and judged), the board's turn, its outline and its holes; writes `data/frame.json` and a rectified copy of I1 in `out/` |
| `board_register.py` | Task 3: the solder side (I2, mirrored) registered to I1 on every hole found, tinned pads and open rings, and judged on holes held out; the fitted map from I2 to the board frame, kept unrounded for task 4; the pads, drills (each with both faces' centres) and footprints (DIPs with pin 1 and their references read by hand, connectors, SIPs); the mounting holes on their top rims; writes `data/registration.json` and overlays in `out/` |
| `board_trace.py` | Task 4: the copper on both faces of the board and its printed legend, traced from I1 and I2 in the board frame; the plan's checks on them (coverage, drills in copper, the known nets of 51 ICs, and the component side against I5, registered on chip pins); writes `data/copper.json`, the track map `site/src/assets/tracks/bbc-micro-board.webp`, and overlays in `out/`. `--look` traces and draws the overlays and runs no check; `--hashes` prints the SHA-256 of the method's files |
| `board_parts.py` | Task 5: every part on the board, placed on task 3's footprints (and the two 8-pin DIPs, from pads named by hand) and named from the Service Manual's table 8.1 through `data/ic-table.json`, with the fitted set the page runs; the manual's places checked after a similarity, on the convention `--settle` chose from four parts first; the markings read on I5 checked against the table (I6 read and recorded); connectors and the large parts from boxes read on I5; two-lead parts inferred from the print; writes `data/parts.json` and `site/src/models/bbc-micro-board-parts.mjs`. Both held-out checks failed on the one run (13 October 2026): the manual's places (IC94 97.22 mm off, four more over 10 mm) and the markings read on I5 (11 of 80 disagree). The model keeps the scan's footprints as places and table 8.1's parts as identities, and its fitted set follows the machine the page emulates (the disc interface fitted, no Econet or speech), not the board in I5. `--look` places the parts and draws them in `out/` and runs neither held-out check; `--crops` writes the crops the markings are read on; `--hashes` prints the SHA-256 of the method's files |
| `diagnose_nets.py` | After task 4's run, looking only: rebuilds the nets check's pieces from the masks the run left in `out/` and prints why the check failed (the piece joining GND to +5V, and the pieces under the pins left out of the largest net). Written after the check failed; no part of the method |
| `results.py` | Task 7: gathers the figures the inside model's note quotes, each as `frame.json`, `registration.json`, `copper.json` and `parts.json` hold it, and the sources the board used, by address, into `site/src/data/bbc-micro-board-model.json`, so the page types no figure. It reads no input. The nets check and the comparison with I5 failed, and so did both checks on the parts; the file keeps those verdicts, and the note says so |
| `run-board.sh` | The inside model's scripts, in order (so far `verify.py I1 I2 I5 I6 D1`, `board_frame.py`, `board_register.py`, `board_trace.py --map-ppm 8`, `board_parts.py` and `results.py`) |
| `tests/test_common.py`, `tests/test_board_frame.py`, `tests/test_board_register.py`, `tests/test_board_trace.py`, `tests/test_board_parts.py`, `tests/test_results.py` | pytest, on made-up inputs |
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
| I6 | `inside-6-cased-iss7-chrisacorns-BBCB7LL.jpg` | [Chris's Acorns](http://chrisacorns.computinghistory.org.uk/Pics/BBCB7LL.jpg), linked as the hi-res motherboard picture from [the Issue 7 page](http://chrisacorns.computinghistory.org.uk/Computers/BBCBI7.html). CC BY-SA 3.0 as [the site's home page](http://chrisacorns.computinghistory.org.uk/) states under "License to reuse"; an older paragraph there asks to be asked first. Not committed |
| D1 | `document-1-bbc-micro-service-manual-acorn-1985.pdf` | [archive.org](https://archive.org/download/bbc-micro-service-manual/BBC-Micro-Service-Manual%20Sections-1-2-3.pdf), from [its item page](https://archive.org/details/bbc-micro-service-manual); archive.org's MD5 for it is `d64017d9f5d956500965648b354af6ee`. Acorn's copyright notice: read for facts only, not committed |
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
export BBC_MODEL_INPUTS=~/bbc-model-inputs
/tmp/bbcvenv/bin/python tools/bbc-micro-model/verify.py          # every input; or name some: verify.py I1 I2
/tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q
cd tools/bbc-micro-model && /tmp/bbcvenv/bin/python spike.py      # writes data/spike.json, and overlays in out/
PYTHON=/tmp/bbcvenv/bin/python tools/bbc-micro-model/run-board.sh   # writes data/frame.json, data/registration.json, data/copper.json, the track map, data/parts.json and the parts module, and overlays in out/
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
