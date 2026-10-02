# Data for the KIM-1 model's measurements

| File | What | Terms |
|---|---|---|
| `sources.json` | Every photograph and drawing used, with its URL, size and SHA-256, and the board's four corners marked by hand in each photograph | MIT (ours) |
| `height-marks.json` | Points marked by hand in two photographs, for `heights.py` | MIT (ours) |
| `layout-marks.json` | The keypad's edges, marked by hand on a rectified photograph | MIT (ours) |
| `kicad-copper-top.png`, `kicad-copper-bottom.png`, `kicad-parts.json` | Drawn and listed from Eduardo Casino's KiCad replica of the Rev D board, by `extract_kicad.py` | CC BY-NC 4.0, the replica's licence: a derivative of it |
| `registration.json`, `agreement.json`, `heights.json`, `layout.json` | Measurements: numbers about the photographs and the replica, not copies of them | MIT (ours) |

The replica is at github.com/eduardocasino/kim-1, commit
`7b356b6386422cc6f7154b3524b0a9011ce33bfb`; its `LICENSE.md` puts everything
in it under CC BY-NC 4.0 except the schematic and symbols, which are CC BY 4.0.
`site/src/assets/photos/README.md` credits it with the photographs.
