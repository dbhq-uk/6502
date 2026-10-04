# Data for the BBC Micro models' measurements

| File | What | Terms |
|---|---|---|
| `sources.json` | Every input: where it came from, its author and licence as stated, when it was fetched, its size and SHA-256, and what it is used for | MIT (ours): facts about the inputs, not copies of them |
| `marks.json` | Points marked by hand on I1, I2 and O1, each with the crop it was read from (task 2 added the connector rows across I1; task 3 the footprints' references and pin 1 where the print's chamfer was not found) | MIT (ours) |
| `frame.json` | Task 2's board frame on I1: the x and y scales with their held-out checks, the turn, each long row's straightness, the outline, the holes, and the verdict against each of the plan's scale thresholds | MIT (ours): numbers about the input, not a copy of it |
| `registration.json` | Task 3's solder side registered to the component side on every hole found, with the held-out figures with and without the outlier rule and the verdict; the drills, the pads on each face and the footprints, in the board frame; the mounting holes fitted on their top rims | MIT (ours): numbers about the inputs, not copies of them |
| `spike.json` | Task 0's measurements: the scan's scale, the solder side's registration and the keys' registration, with the verdict against each of the plan's thresholds | MIT (ours): numbers about the inputs, not copies of them |

No input is committed. The scans I1 and I2 (amb5l, posted to stardot) and the
photograph I5 (8BS) state no licence; O1 to O3 are public domain on Wikimedia
Commons. Each is credited, with its URL, in `sources.json`.

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
