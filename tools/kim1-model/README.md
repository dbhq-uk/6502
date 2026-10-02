# The KIM-1 model's measurements

Offline scripts that measure the KIM-1's 3D model from photographs of real
boards and a replica of the board's layout, and write what the site uses. The
site's build and tests never run them: their outputs are committed, and the
build only copies the track map beside the model's bundle.

What they write, and where it is used:

| Output | Written by | Used by |
|---|---|---|
| `site/src/assets/tracks/kim-1.webp` | `fuse.py` | the model's copper on both faces |
| `site/src/models/kim-1-parts.mjs` | `layout.py` | the model's parts, places and heights |
| `site/src/data/kim-1-model.json` | `results.py` | every figure in the note under the model |
| `data/registration.json`, `data/agreement.json`, `data/heights.json`, `data/layout.json` | `register.py`, `fuse.py`, `heights.py`, `layout.py` | `results.py`, and the journal |
| `data/kicad-copper-top.png`, `data/kicad-copper-bottom.png`, `data/kicad-parts.json` | `extract_kicad.py` | the reference every photograph is registered to |
| `out/` (git-ignored) | every script | looking at: rectified photographs, traces, overlays |

The inputs that are not committed are the full-size photographs and the
replica's `kim-1.kicad_pcb`. `data/sources.json` gives the URL and SHA-256 of
each, and each script refuses a file whose hash is not the one recorded. The
hand-made inputs are committed: `data/sources.json` (the board's four corners
in each photograph), `data/height-marks.json` (points marked in two
photographs) and `data/layout-marks.json` (the keypad's edges).

## Running it

Python 3.12 with `numpy`, `opencv-python-headless`, `Pillow` and `scipy`, in a
virtual environment so nothing touches the system or the site:

```
python3 -m venv /tmp/kimvenv
/tmp/kimvenv/bin/pip install numpy opencv-python-headless pillow scipy
```

Fetch the five photographs into one folder under the names their URLs give
them, and clone the replica at the pinned commit (both in `data/sources.json`),
then:

```
PYTHON=/tmp/kimvenv/bin/python tools/kim1-model/run-all.sh <folder> <the replica's kim-1.kicad_pcb>
```

`run-all.sh` runs the scripts in order. Each one's docstring says what it does
and how; in short:

1. `extract_kicad.py`: draws the replica's two copper layers and lists its
   footprints, in the board frame (millimetres from the top left corner of the
   board's body, as the component side shows it).
2. `register.py`: a homography from each photograph's four marked corners to
   the board, refined by matching the photograph's copper to the replica's
   (OpenCV's ECC), then a smooth cubic correction for the lens, tested on
   blocks held out of its fit. The residuals are in millimetres.
3. `rectify.py`: each photograph square on, in the board frame, at 12 pixels to
   the millimetre.
4. `trace.py`: the copper in each, and where each shows the board at all.
5. `fuse.py`: the track map, by a vote of the three top-face photographs, the
   replica only where no photograph shows the board, the underside from its
   one photograph; and the before and after figures.
6. `heights.py`: one camera fitted to two photographs of the same board, and
   each marked point triangulated.
7. `layout.py`: the parts' places and sizes, checked against the photographs,
   and `kim-1-parts.mjs`.
8. `results.py`: the figures the page quotes.

`probe_before.py` is not in the run: it says why the map the model had
before scores low even against the photograph it was traced from.

It took about ten minutes on the machine it was written on. The versions it
was run with are in the journal for 2 October 2026, with every figure it
printed.

## Licences

The scripts are MIT, like the rest of the repository. What they write from
the sources is not: `data/README.md` says which files carry which terms.
