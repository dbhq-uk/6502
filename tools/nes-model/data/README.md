# Data for the NES models' measurements

| File | What | Terms |
|---|---|---|
| `sources.json` | Every input: where it came from, its author and licence as stated, when it was fetched, its size and SHA-256, and what it is used for | MIT (ours): facts about the inputs, not copies of them |
| `marks.json` | Points and segments marked by hand on I1-front, I1-back, I3, O2-FL, O2-BR, O4 and the patent's sheets, each with the crop it was read from | MIT (ours) |
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

`marks.json` and `spike.json` hold coordinates and measurements read from
those inputs, not any part of them.
