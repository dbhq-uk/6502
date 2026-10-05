# Data for the NES models' measurements

| File | What | Terms |
|---|---|---|
| `sources.json` | Every input: where it came from, its author and licence as stated, when it was fetched, its size and SHA-256, and what it is used for | MIT (ours): facts about the inputs, not copies of them |
| `marks.json` | Points and segments marked by hand on I1-front (with the board outline's corners and notches, each footprint's reference read from the print, and the NTSC sticker that hides the board), I1-back, I3, O2-FL, O2-BR, O4 and the patent's sheets, each with the crop it was read from | MIT (ours) |
| `frame.json` | Task 2's board frame on I1-front: the x and y scales with their held-out errors and verdicts, the turn, the rows' straightness, the outline (corners, edges, notches) and the mounting holes, in millimetres from the board's top left corner; the KiCad redrawing's outline and holes beside, compared only | MIT (ours): numbers about the inputs, not copies of them |
| `registration.json` | Task 3: the solder side (I1-back, flipped) registered to the component side on every hole found on both, its held-out errors, verdict and the plan's outlier rule with the holes it names; the drills, the pads on each face and the footprints (DIPs, the edge fingers, connectors, crystals), each reference read from the print by hand | MIT (ours): numbers about the inputs, not copies of them |
| `copper.json` | Task 4: the copper on both faces and the print, traced from I1-front and I1-back: each face's coverage, the drills in copper, the known nets (each IC's GND and +5V pins, joined through both faces) with the verdict against each of the plan's rows, the thresholds and probe figures behind the trace, and the track map's size, bytes and SHA-256 | MIT (ours): numbers about the inputs, not copies of them. The track map it describes is not: see below |
| `parts.json` | Task 5: every part's place in the board frame, both consoles' parts, how each photograph was fitted to the board and each part held out, the places against the KiCad redrawing's, the bodies measured on I4, the passives at their pads, and the verdicts; its `model` is what `site/src/models/nes-famicom-board-parts.mjs` exports | MIT (ours): numbers about the inputs, not copies of them |
| `ic-table.json` | Each IC's reference, role, pin count, package, and its GND and +5V pins with the data sheet or page each was read from (task 4; task 5 adds each console's part) | MIT (ours): facts, each with its source |
| `spike.json` | Task 0's measurements: the scan's scale, its outline against the KiCad redrawing's, the solder side's registration, the PAL board's layout, the case's proportions on the patent's views and on the photographs, and the PAL front, with the verdict against each of the plan's thresholds as revised on 5 October 2026 and the first verdicts kept | MIT (ours): numbers about the inputs, not copies of them |

No input is committed. Their own terms, as each states them, are in
`sources.json`:

- **The bare board scans I1-front and I1-back** and **the KiCad redrawing I2**
  are from OpenTendo, read from the `dbhq-uk` fork at commit
  `3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009`. The repository's README says it
  is under the TAPR Open Hardware License; the scans state no licence of their
  own. Nothing is drawn from I2: its numbers are compared only.
- **The photographs O2-FL, O2-BL, O2-BR, I4 and I5** (Evan-Amos) are public
  domain, and **the design patent O1** is a United States design patent.
- **The photographs O4, O5, I3 and I6** (PantheraLeo1359531) are CC BY 4.0.

`marks.json`, `spike.json`, `frame.json`, `registration.json`, `copper.json`, `parts.json` and `ic-table.json` hold coordinates,
measurements and facts read from those inputs, not any part of them.

**The track map** that `board_trace.py` writes, `site/src/assets/tracks/nes-famicom-board.webp`,
is different: it is traced from OpenTendo's scans I1-front and I1-back, so it is
a derivative of them, credited to OpenTendo, and carries the TAPR Open Hardware
License's terms that OpenTendo's README states (www.tapr.org/OHL). `NOTICE.md`
at the repository's root says what is derived.
