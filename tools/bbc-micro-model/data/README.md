# Data for the BBC Micro models' measurements

| File | What | Terms |
|---|---|---|
| `sources.json` | Every input: where it came from, its author and licence as stated, when it was fetched, its size and SHA-256, and what it is used for | MIT (ours): facts about the inputs, not copies of them |
| `marks.json` | Points marked by hand on I1, I2, I5 and O1, each with the crop it was read from (task 2 added the connector rows across I1; task 3 the footprints' references and pin 1 where the print's chamfer was not found; task 4 the chip pins on I5; task 5 the two 8-pin DIPs' pads, the connectors' and large parts' boxes on I5, and the markings read on I5 and I6) | MIT (ours) |
| `frame.json` | Task 2's board frame on I1: the x and y scales with their held-out checks, the turn, each long row's straightness, the outline, the holes, and the verdict against each of the plan's scale thresholds | MIT (ours): numbers about the input, not a copy of it |
| `registration.json` | Task 3's solder side registered to the component side on every hole found, with the held-out figures with and without the outlier rule and the verdict, and the fitted map from I2 to the board frame; the drills with both faces' centres, the pads on each face and the footprints with their references, in the board frame; the mounting holes fitted on their top rims | MIT (ours): numbers about the inputs, not copies of them |
| `ic-table.json` | Acorn's table 8.1 of the Issue 7 board's ICs (D1, PDF pages 66 and 67), transcribed by hand: each IC's part, its place as the manual gives it and the table's comment; for the 51 ICs task 4's nets check uses, the GND and +5V pins with the maker's data sheet each was read from, and for the others why they are left out; task 5 completed it with every IC's pins, a role for each, the fitted set with the upgrade that fits each IC, and the paged ROMs | MIT (ours): facts read from the manual and the data sheets, not their text |
| `copper.json` | Task 4's trace of both copper faces and the print: the coverage, the drills in copper, the known nets, I5's registration and the component side against I5, each with its verdict against the plan; the track map's size and SHA-256; the SHA-256 of the method's files | MIT (ours): numbers about the inputs, not copies of them |
| `parts.json` | Task 5's parts: every IC placed on its footprint with its identity from table 8.1 and whether it is fitted, the connectors and large parts from boxes read on I5, the two-lead parts inferred from the print, typical bodies and heights; the manual's places and the markings read on I5 checked, each with its verdict, and I6's markings recorded; the SHA-256 of the method's files. Both held-out checks failed on the one run (13 October 2026): the manual's places (IC94 97.22 mm off, four more over 10 mm) and the markings read on I5 (11 of 80 disagree). The model keeps the scan's footprints as places and table 8.1's parts as identities, and its fitted set follows the machine the page emulates (the disc interface fitted, no Econet or speech), not the board in I5. `model` is what `site/src/models/bbc-micro-board-parts.mjs` exports | MIT (ours): numbers and facts about the inputs, not copies of them |
| `spike.json` | Task 0's measurements: the scan's scale, the solder side's registration and the keys' registration, with the verdict against each of the plan's thresholds | MIT (ours): numbers about the inputs, not copies of them |

No input is committed. The scans I1 and I2 (amb5l, posted to stardot) and the
photograph I5 (8BS) state no licence; O1 to O3 are public domain on Wikimedia
Commons. Each is credited, with its URL, in `sources.json`.

I6, a photograph of a populated Issue 7 board in its case, is Chris Whytehead's, from Chris's Acorns, whose home page says its photographs "may be reused under the CC-BY-SA 3.0 license unless otherwise specified" and asks for a byline; an older paragraph on the same page asks to be asked first. D1, the Service Manual (Acorn, 1985), carries Acorn's copyright notice and is read for facts only. Both were fetched on 13 October 2026, and neither is committed.

K1 is Dominic Beesley's KiCad keyboard, github.com/dominicbeesley/bbc-keyboard
at commit `5ec53df566c77424caa6be827d0831da3916ecfa`, read for its key centres
only. It is under the MIT licence, whose notice is kept here:

```
MIT License

Copyright (c) 2023 dominicbeesley

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
