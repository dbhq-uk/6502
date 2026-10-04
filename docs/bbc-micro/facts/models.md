# BBC Micro Model B: sources for the two 3D models

Researched on 4 October 2026 for issue [#28](https://github.com/dbhq-uk/6502/issues/28): a machine with a case gets two models, the **outside** (case, keys, ports) and the **inside** (motherboard, chips and copper). Tags as in the other fact sheets: `[from <url>]` was read or measured directly, `[inferring ...]` joins dots, `[guessing - verify]` needs checking. Nothing here is code from another emulator, and no 3D model made by someone else is used.

**The rule for what goes in the repository.** This repository is MIT and public. A model is ours: geometry we measured, with credit for anything traced or derived from a photograph. Photographs are credited and are not MIT, as for the KIM-1 (rights are not a gate for a photograph, Dan, 2 October 2026). A photograph or scan that is too large or has no stated licence is not committed: the tool that reads it takes a path, and the repository records its URL, fetch date and SHA-256 and commits only what the tool made, as `tools/kim1-model/` does.

## What was found

| | Verdict | Why |
|---|---|---|
| **Inside** | Measure from photographs to about 1 mm; the copper better than that | Flatbed scans at 400 dpi of a **bare** original Issue 7 board, both sides, so no parts hide copper and every IC number is printed on the board. Better than the KIM-1's photographs. No open CAD replica exists, so the scans are the reference |
| **Outside** | Published dimensions are enough for the case; keys, ports and a few heights from photographs to about 1 to 2 mm | Acorn publishes the case size, an MIT KiCad file gives every key centre, and public-domain photographs cover the top, the keyboard out of its case and the rear |

**A correction to the issue's wording.** The Model B has **nothing on its left side**. The Tube, 1 MHz bus, user port, printer, disc drive and auxiliary power connectors face down, under the front of the case. The rear carries UHF, video, RGB, RS423, cassette, analogue in and Econet, with the mains switch at the rear right [from the underside and rear photographs `BBCB7I.jpg` and `BBCB7H.jpg` at http://chrisacorns.computinghistory.org.uk/Computers/BBCBI7.html].

## Outside: sources

| # | Source | Author, date | Licence, as stated | Size, view | Use |
|---|---|---|---|---|---|
| O1 | https://commons.wikimedia.org/wiki/File:Acorn_BBC_Microcomputer.jpg | Daniel Beardsmore, 2016-02-04 | "Public domain" | 2490x1868, from above, mild keystone | The top: every key legend, the red f0 to f9, BREAK, the badge strip, the grille, the recess, the three LEDs. At a 19.05 mm key pitch the front edge measures about 412 mm against Acorn's 415 [inferring] |
| O2 | https://commons.wikimedia.org/wiki/File:BBC_Micro_rear.jpeg | Stuart Brady (assumed by Commons), 26 Dec 2005 | "Public domain" | 2048x994, rear nearly square on | Every port label readable; the mains panel |
| O3 | https://commons.wikimedia.org/wiki/File:BBC_Micro_Type_1.jpg | Daniel Beardsmore, 2013-06-24 | "Public domain" | 1996x1512, from above | The keyboard out of its case: keycap shapes, the plate, the LEDs, the speaker |
| O4, O5 | `File:BBC_Micro.jpeg`, `File:BBC_Micro_left.jpeg` | Stuart Brady, 2005-12-26 16:03:25 and 16:04:45 | "Public domain" | 1929x1338 and 1756x1239 | One camera (Olympus C760UZ, 6.3 mm) 80 s apart [from the Exif]: a triangulation pair for the side profile. The left side is plain |
| O6 | `File:Acorn_BBC_Micro.jpg` | simon.inns | CC BY 2.0 | 5195x3463 | Already the page's photograph |
| O8 | Chris's Acorns `BBCB7I` (underside), `BBCB7H` (rear) | Not named | None stated | 640 px wide | The only underside view found: connector labels |
| D1 | Acorn BBC Microcomputer Service Manual, 1985, https://archive.org/details/bbc-micro-service-manual | Acorn | "Copyright ACORN Computers Limited 1985. Neither the whole or any part of the information contained in, or the product described in, this manual may be adapted or reproduced in any material form except with the prior written approval of ACORN Computers Limited" | Text and drawings | Case size "Height 73mm (including feet) Width 415mm Depth 345mm" [from the B+ section, PDF p.109; the B+ is assumed to share the case: guessing - verify]. Exploded views 9.2 and 9.3 (PDF p.79, 81) have no dimensions. Facts such as dimensions are used, with credit; the drawings are not copied |
| D3 | Science Museum object co64142 https://collection.sciencemuseumgroup.org.uk/objects/co64142/bbc-model-b-microcomputer | Science Museum Group | n/a | Text | 75 x 410 x 345 mm. Other published sizes disagree on depth by 13 mm, so D1 and this figure are used |
| K1 | https://github.com/dominicbeesley/bbc-keyboard at `5ec53df566c77424caa6be827d0831da3916ecfa` | Dominic Beesley, 2023 to 2024 | "MIT License / Copyright (c) 2023 dominicbeesley" | KiCad 6, 72 Cherry MX switches, plate and two "ears" | Every key centre on the BBC layout at a 19.05 mm pitch; six row offsets agree with O1 to about 1 mm [inferring]. "with the extra ears in the project it fits nicely in a Beeb case" [from stardot t=29178]. MIT: usable with the notice kept |
| K2 | https://www.thingiverse.com/thing:4867915 (Type 1 keycaps) | Pledg, 2021 | `"license":"http://creativecommons.org/licenses/by/4.0/"` | STL | A cross-check for the keycap profile only, with credit |
| M1 | https://www.printables.com/model/197766-bbc-micro-miniature | antirez, 2022 | "Creative Commons - Attribution - Noncommercial" | STL | **Not usable** |
| M2 | https://virtual.bbcmic.ro/ | "Models: Ant Mercer" | "CC BY-NC-SA 4.0" | 3D in the browser | **Not usable**: a visual cross-check only |

## Inside: sources

| # | Source | Author, date | Licence, as stated | Size, view | Use |
|---|---|---|---|---|---|
| I1 | Bare Issue 7, component side. https://stardot.org.uk/forums/viewtopic.php?t=23329 , file https://drive.google.com/file/d/1llcLHl_ax6rtZRWxdoSyA5jNjFOo_-3N/view | amb5l, posted 18 Sep 2021 (file saved 2014-11-04) | None stated | 4990x3768, a flat scan at 400 dpi | The reference frame. A genuine Acorn board ("203,000 Issue 7"); every IC, link, connector and pad labelled; tracks sharp at 0.3 mm. Scale 15.72 px/mm down and 15.79 px/mm across (0.4 per cent apart) from pin pitch; the board is about 309 x 229 mm [measured by the researcher] |
| I2 | Bare Issue 7, solder side, same post | amb5l | None stated | 5014x3736 | Bottom copper, with solder on many pads ("PCB MANUF BEPI 84 20") |
| I3, I4 | amb5l's Allegro `.brd` and OrCAD schematic, same thread | amb5l, 2016 and 2021 | None stated | Binary / PDF | The `.brd` is only partly traced. Not needed: I1 and I2 are better. The schematic gives connectivity only |
| I5 | https://8bs.com/see/BBC_B_iss7.jpg | 8BS, Canon A75, 2004-05-04 | None stated | 1536x2048, about 6 px/mm, from above | A populated Issue 7 with every marking readable (R6502AP, HD6845SP, both 6522s, SAA5050, 2C199E, 6850, D7002C, 68B54, the ROMs, RAM, the modulator). The 8271 socket is empty |
| I6 | Chris's Acorns `Pics/BBCB7LL.jpg` | Not named | None stated | 2005x1295, slightly oblique | A second board, in its case |
| I8 | Service Manual table 8.1, PDF p.66 to 67 | Acorn 1985 | As D1 | Text | Each IC's identity and an approximate position |
| I9 | Service Manual 9.4 "Main PCB layout", PDF p.83 | Acorn 1985 | As D1 | About 3 px/mm | Part outlines with item numbers; no copper |
| I10 | Bob's Bits replica board on Tindie | PeepoUK | Files withheld: "I won't be releasing the gerbers any time soon" [from https://stardot.org.uk/forums/viewtopic.php?t=24839] | n/a | **Unusable** |
| I11 | GitHub and web searches | n/a | n/a | n/a | **No open KiCad or Gerber replica of a Model B exists.** The nearest open work is a BBC Master 128 schematic (`Domesday86/Acorn-Master-AIV`, CC BY-SA 4.0), not a board |

**Key ICs (Service Manual table 8.1; positions are mm from the SW corner and approximate: take places from the scan and identities from the manual)** [inferring: on a rough check IC1, IC2, IC5 and IC6 sit right relative to each other but the offset from the scan's corner differs between chips]:

| IC | Part | Position | IC | Part | Position |
|---|---|---|---|---|---|
| IC1 | 6502A | 160,85 | IC18 | 76489 sound | 22,44 |
| IC2 | 6845 CRT controller | 160,140 | IC51 | OS ROM | 214,24 |
| IC3 | 6522 internal VIA | 90,75 | IC52 | BASIC ROM | 233,24 |
| IC4 | 6850 ACIA | 128,141 | IC53 to IC68 | 4816 RAM | |
| IC5 | SAA5050 teletext | 187,102 | IC69 | 6522 user VIA | 160,29 |
| IC6 | 5C094 Video ULA | 214,71 | IC73 | uPD7002 ADC | 100,173 |
| IC7 | 2C199 serial ULA | 128,182 | IC78 | 8271 disc controller | 60,75 |
| IC89 | 68B54 Econet | | IC98, IC99 | TMS6100, TMS5220 speech | |
| IC100, IC101 | Sideways ROM sockets | | | | |

## Method (the KIM-1's, adapted)

**Inside.** The bare scan I1 is the reference frame (no CAD to register to; scale from pin pitch, separate x and y scales). Register the solder side I2 to I1 by hundreds of drill-hole centres, the leftover error being the check. Find pads and drills as bright rings and group them into footprints by the printed outlines. Trace both copper layers (the yellow print hides copper on the top, solder on the bottom). Parts: places from the printed outlines and pads, identities from I8 and I5 (registered by homography), I6 and the manual's coordinates as checks. **Measured:** outline, holes, pads, both copper layers, part places, identities, connector places. **Typical:** all heights (a 1.6 mm board, DIP bodies about 4 mm, about 3 mm more where socketed, connectors from their makers' drawings [guessing - verify]), the board's thickness, passive body sizes.

**Outside.** Key centres from K1; register O1 and O3 to them by a homography on the key plane with a held-out error per key (this also places the LEDs, the badge strip and the recess). Sculpted keycaps in a typical profile checked against K2, legends drawn by us. Case footprint from D1; outline and recess from the rectified O1; side profile triangulated from O4 and O5 (one camera fitted to both, about 1 to 2 mm [guessing - verify by a check point]). Rear ports are soldered to the board, so their x positions come from scan I1 (SK1 to SK7), heights from O2 scaled by the standard 15-way D shell. Underside connectors: x from PL8 to PL12 on the scan, the panel from O8 (typical to a few mm). The three LEDs and key presses are driven by the emulator. **Typical:** keycap profile and travel, edge radii, the recess slope away from the triangulated points, socket depth, the feet.

**Order.** Inside first: it gives the outside its connector positions. Effort estimated at about 3 to 4 agent-days for the inside and 2 to 3 for the outside, roughly twice the KIM-1's.

## Choices taken (all reversible)

Taken on 4 October 2026 at the researcher's recommendations, Dan having said to get it done:

1. **Keys:** sculpted caps with drawn legends, over boxes with a legend texture or no legends. The keyboard is what a visitor looks at, and its layout is measured.
2. **Which board the inside shows:** the emulated machine, a Model B with the 8271 disc interface and DFS fitted, over a factory board with empty sockets or the 8bs photograph exactly as taken. The page runs DFS.
3. **The badge strip:** the strip with its words drawn by us and **no owl**, over a simplified owl. The owl is the BBC's mark, so drawing it adds trade-mark risk for nothing the visitor needs. The photograph on the page already shows the real badge.
4. **Underside connectors:** included, as labelled boxes.
5. **Copper:** in the inside model, because a track source exists.

**Not usable, and not to be used:** M1 (non-commercial) and M2 (non-commercial, share-alike). K2 only as a cross-check.

## Downloaded for the work

SHA-256 of the files the research downloaded (kept at `~/dbhq-previews/bbc-model-research/`, not committed: the scans are 13 to 15 MB with no stated licence):

| File | SHA-256 |
|---|---|
| `outside-1-top-Acorn_BBC_Microcomputer.jpg` | `a616c736c51c4bf0f796be21ba426d435acdbf2d6b692cbf0cc3256f5782949a` |
| `outside-2-rear-BBC_Micro_rear.jpeg` | `4072a59af7f7fab834ca2b912b0f6e3449aa8bdaa01bb621d9d556579dd5937e` |
| `outside-3-keyboard-BBC_Micro_Type_1.jpg` | `745b34357b2befd5d1bcc501bb48ad9c3e345e2b876d59b3f812cec48d2e2403` |
| `inside-1-bare-iss7-top-amb5l.jpg` | `5edc89d95872c9e70a1adb0b8d968fbccd3459816cacd64a630d95afd02e7b0e` |
| `inside-2-populated-iss7-8bs.jpg` | `7ae4b57399e9576d46bcfe0d4e2b39ddb362d7bb33782c002a39adffab208263` |
| `inside-supp-bare-iss7-bottom-amb5l.jpg` | `c9a36223ca7d3d28cbceb6cb25f625e3989655283e6bf4b4602c5199612b5bb6` |
