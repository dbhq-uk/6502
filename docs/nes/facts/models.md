# NES: sources for the 3D models

Researched on 5 October 2026 for [the NES's models design](../../superpowers/specs/2026-10-05-nes-models-design.md): the NES has a case, so it gets two models, the **outside** (case, buttons, LED, ports) and the **inside** (the main board, its chips and copper), each drawn for both consoles, the NTSC NES-001 and the PAL NESE-001. Tags as in the other fact sheets: `[from <url>]` was read or measured directly, `[inferring ...]` joins dots, `[guessing - verify]` needs checking. Nothing here is code from another emulator, and no 3D model made by someone else is used.

**The rule for what goes in the repository** is the BBC Micro's ([its sheet](../../bbc-micro/facts/models.md)): a model is ours, geometry we measured, with credit for anything traced or derived from a photograph. Photographs are credited and are not MIT. A photograph or scan that is too large or has no stated licence is not committed: the tool that reads it takes a path, and the repository records its URL, fetch date and SHA-256 and commits only what the tool made.

## What was found

| | Verdict | Why |
|---|---|---|
| **Inside, NTSC** | Measure the board to about 0.2 mm; the copper on both faces | Flatbed scans at 300 dpi of a **bare** NES-CPU-10, both sides, so no parts hide copper, and the print names every part. Public-domain photographs of a populated NES-CPU-07, both sides, flat, at about 20 pixels a millimetre, give the chips' markings |
| **Inside, PAL** | The NTSC copper with the PAL parts, if one layout | A CC BY 4.0 photograph of a populated NES-CPU-11 "PAL-EEC", top side only, with every marking readable. No bare PAL board and no PAL solder side were found. ConsoleMods says the board is the same in every region with only the CPU, PPU, crystal and lockout chip changed. Task 0 checked the ten ICs' places against the bare scan's (below); the connectors could not be seen on I3 |
| **Outside, NTSC** | Proportions and features to about 1 to 2 mm; size from published figures | Public-domain photographs of an NES-001 from all four corners at 112 mm (little perspective), and the design patent's six orthographic views. No size from Nintendo was found |
| **Outside, PAL** | Features from photographs to a few mm | A CC BY 4.0 set of an NESE-001 taken apart, with the front label straight on, the underside and the case parts; a 20 mm lens, so strong perspective, and the plastic has yellowed |

## Outside: sources

| # | Source | Author, date | Licence, as stated | Size, view | Use |
|---|---|---|---|---|---|
| O1 | US Design Patent D299,726, https://patentimages.storage.googleapis.com/pdfs/USD299726.pdf ; Commons copy of sheet 1, `File:NES patented design.png` | Masayuki Yukawa, for Nintendo; filed 4 November 1985 | Commons: `{{PD-US-Patent}}` | 4 pages, each 2320x3408 at 300 dpi, 1-bit | FIG 3 front, FIG 4 rear, FIG 5 top, FIG 6 bottom (the expansion cover), FIG 7 and 8 sides, FIG 1 and 2 perspective with the door open. Proportions: superseded by task 0's measurement of the views on 5 October 2026 (`spike.json`'s `case.patent`, written by `cd tools/nes-model && NES_MODEL_INPUTS=<folder> python spike.py`; see "What task 0 found"). The drawings may not be exactly to scale [inferring] |
| O2 | https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-FL.jpg , and `-FR`, `-BL`, `-BR` | Evan-Amos, 27 July 2016 | `{{PD-self}}`: "I, the copyright holder of this work, release this work into the public domain" | 4020x2880 each; NTSC NES-001 | Front and rear corners. Nikon D7000 at 112 mm (168 mm in 35 mm terms) [from the Exif, read 5 October 2026]. Colours, buttons, LED, ports, door, vents, the rear connectors |
| O3 | `File:Nintendo-Entertainment-System-NES-Deconstruction-01.jpg` to `-04` | Evan-Amos, 27 July 2016 | `{{PD-self}}` | 4020x2640; NTSC | The case opened in stages: inner shell, shield, tray. Where the board sits in the case |
| O4 | `File:Geöffnetes deutsches NES 20221102 HOF06342 RAW-Export.png`, in the Commons category "Nintendo Entertainment System NESE-001" (42 files) | PantheraLeo1359531, 2 November 2022 | `{{self\|cc-by-4.0}}` | 8606x2253, 92.9 MB; PAL | The PAL top shell straight on from the front, its label legible. Sony A7R IV at 20 mm, so strong perspective. The set also has a near top-down case (HOF06345), the tray, the shield and the port bracket |
| O5 | `File:Unterseite NES NESE-001 20221102 132229.jpg` | PantheraLeo1359531, 2 November 2022 | CC BY 4.0 | 4000x3000; PAL | The underside, slightly oblique: the expansion cover, the feet, the rating label. Samsung SM-G988B at 19 mm [from the Exif] |
| O6 | `File:NES PAL.jpg` | RobinLe (from Pixabay), 2017 | `{{cc-zero}}` | 3456x2304; PAL | A PAL front, oblique. Reference only |
| O7 | `File:Nintendo Entertainment System - Mattel Version (42532353740).jpg` | Matthew Paul Argall, 2018 | CC BY 2.0 | 6000x4000 | The PAL-A "Mattel Version". Out of scope; not fetched |
| O8 | `File:Nintendo-Entertainment-System-NES-Controller-Plug.jpg` | Evan-Amos, 2016 | `{{PD-self}}` | 2400x2460 | The controller plug, for the port's shape |
| D1 | https://www.dimensions.com/element/nintendo-entertainment-system-nes | Dimensions.com | "©2026 Dimensions.com \| All rights reserved." | Text and drawings | "10.1 x 8 x 3.5 in" (256 x 203.2 x 88.9 mm), 2.27 kg. The figures are quoted as facts with credit; the drawings are not used |
| D2 | NES Fandom wiki, and Thingiverse thing 243385's description | Various | Not needed: figures only | Text | 10 x 8 x 3.5 inches; "254 X 203 X 89 mm". The open door adds about an inch [from the Fandom wiki] |
| D3 | RetroTechCollection | n/a | n/a | Text | 254 x 203 x 76 mm: the height is the outlier |

**Published sizes, and none of them Nintendo's.** No dimensions from Nintendo were found; the NESE-001 manual on manua.ls and the technical manual index at Limbofunk give none [from the research, 5 October 2026]. The width and depth agree at about 10 by 8 inches. The height does not (76 to 89 mm), so the plan measures it.

## Inside: sources

| # | Source | Author, date | Licence, as stated | Size, view | Use |
|---|---|---|---|---|---|
| I1 | OpenTendo, https://github.com/Redherring32/OpenTendo , `Scans/NES-CPU-10_front_300dpi.png` and `Scans/NES-CPU-10_back_300dpi.png`, at commit `3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009` (the head of `master` on 5 October 2026; the scans arrived in `e3caa02cfcbfbd0d77504259277e5c60fe922b16`, "fix: file structure", 22 August 2024) | Added by Kamoteshake, August 2024; the scanner is not named | The repository's README: "Licensed under the TAPR Open Hardware License (www.tapr.org/OHL)". GitHub reports `NOASSERTION`. The scans state no licence of their own; that the README's covers them is inferred | 2376x1492 each, 300 dpi (about 11.81 px/mm); a **bare** NES-CPU-10, "©1987 Nintendo", with an "NTSC" sticker | The reference frame: outline, holes, pads, both copper faces, the print, which names the parts (CPU, PPU, SRAM (WRAM), SRAM (VRAM), 74LS373, 74LS139, 40H368 twice, CIC, 74HCU04) |
| I2 | OpenTendo `Board Files/Motherboard.kicad_pcb`, same commit | Redherring32 and others, 2019 to 2025 | As I1 | KiCad 8, 10.95 MB | A redrawing of the front-loader's board ("almost 1:1 of the OEM NES") from a schematic of the NES-CPU-11. Its Edge.Cuts outline is 196.252 by 118.700 mm, read on 5 October 2026 with the `kicad_outline_box` the models plan's task 0 adds to `tools/nes-model/common.py` (run in a scratch copy before the plan was written). It names U6 "RP2A03 CPU", U5 "RP2C02 PPU", U1 "6116 (WRAM)", U4 "6116 (VRAM)", U2 74HC373, U3 74HC139, U7 "40H368 (CI)", U8 "40H368 (CII)", U9 74LS04, U10 CIC, X1 21.477272 MHz, X2 4 MHz, P1 the 72-pin connector, P2 the expansion connector, P3 the RF modulator, P4 and P5 the controller inputs and P6 the power and reset input A cross-check of the outline and the part places only |
| I3 | `File:Frontalansicht Mainboard NES NESE-001 HOF06378.png` | PantheraLeo1359531, 2 November 2022 | `{{self\|cc-by-4.0}}` (a Commons "Quality image") | 5462x3966, 76.8 MB; populated top side, background cut out; **NES-CPU-11, "PAL-EEC"** | The PAL parts and their markings: RP2A07A, RP2C07-0, two XRM6216-10 RAMs, CIC 3195A, MB74LS373, two MC74HC368N, SN74LS139N, SN74HCU04N, the ALPS RF modulator. About 21 px/mm from DIP pitch [from the research]. A 20 mm lens, so tall parts lean |
| I4 | https://commons.wikimedia.org/wiki/File:Nintendo-NES-Mk1-Motherboard-Top.jpg | Evan-Amos, 15 July 2015 | `{{PD-self}}` | 4570x3330; populated top, flat; **NES-CPU-07**, "NTSC" sticker; the modulator's lid off | The NTSC parts and their markings: RP2C02G-0, RP2A03G, CIC 3193A, MB8416A RAMs, SN74LS373N. About 20.7 px/mm. Nikon D7100, 60 mm macro [from the Exif] |
| I5 | `File:Nintendo-NES-Mk1-Motherboard-Bottom.jpg` | Evan-Amos, 15 July 2015 by the Exif (Commons gives 13 July) | `{{PD-self}}` | 5280x3690; populated, solder side, flat; NES-CPU-07 | The populated solder side: what is soldered where, as a check on I1's back |
| I6 | `File:RP2A07A 20221102.png` | PantheraLeo1359531, 2022 | CC BY 4.0 | 1206x408 | The PAL CPU close up |
| I7 | `File:Nintendo-Entertainment-System-NES-Motherboard-FL.jpg`, `-FR`, `-BL`, `-BR`, `-Bottom` | Evan-Amos, 2016 | `{{PD-self}}` | 3900 wide; oblique, populated; NTSC | Part heights, the modulator box's sides |
| I8 | `File:Nintendo 10nes pal-a.jpg`, `File:Ricoh 2a07.jpg` | it:User:Leo72, 2011 | Public domain | 1600x1200 | PAL-A parts. Out of scope; not fetched |

**What the scan shows** [from I1, looked at on 5 October 2026]: the main board alone. The RF modulator is a separate can soldered at the lower right ("MOD RF"), the 72-pin cartridge connector clamps onto the edge fingers at the bottom, and the 48-pin expansion port's footprint is the long slot in the middle. The controller ports reach the board through a header at the upper right.

## Not usable, and not to be used

- **Printables 132150 "NES Frontloader replica Shell" and 110082 "lid"**, RetroGameRevival: "Creative Commons - Attribution - Noncommercial". Non-commercial, and someone else's 3D model.
- **Thingiverse 243385 "Original NES Console"**, qpowel1: CC BY-SA 4.0, but a 3D model. Only its stated size is quoted (D2).
- **GrabCAD NES models**: other people's 3D models. Their licences were not checked, because they would not be used either way.
- **Dimensions.com's drawings**: all rights reserved. Its figures are quoted as facts (D1).
- **nesdev forum photographs, Flickr osr/7866767354, the ConsoleMods wiki's photographs**: no licence found, or the page would not load to check one.
- **Small or oblique views**: `File:10NES 1/2 (NES).jpg` and `NES ouverte 1-4` (256 pixels wide); `File:NES Motherboard.png` (CC BY 2.0) and `File:NESmainboardPCB.jpg` (liftarn, CC BY-SA 2.0, PAL-EEC, 3072x2304), oblique and coarse. Licensed well enough, but poor for measuring.

## Choices taken

Taken on 5 October 2026 with Dan; the design's decisions table has each with what it was chosen over.

1. **Both consoles**, NTSC and PAL, for both views (Dan).
2. **The NTSC board is an NES-CPU-10**, from I1, identified from I4 and I5.
3. **The PAL board is I1's copper with I3's parts**, only if task 0 shows one layout.
4. **No Nintendo logo shapes.** The case's words are drawn in the site's own face; the board's print is traced as it is.
5. **The console as made**, not as yellowed.
6. **OpenTendo forked into `dbhq-uk`** and pinned before it is read.

## What task 0 found

Measured on 5 October 2026 by `tools/nes-model/spike.py` (the figures are in
`tools/nes-model/data/spike.json`, and the journal for that day has the
command); each is a measurement of that day.

- **The edge fingers are on 2.50 mm, not 2.54.** The 35 gaps between the 36
  fingers on I1-front lie 2.499 mm apart, through the x scale of the DIP rows
  and the expansion header, and the two end fingers are 3.0 mm wide where the
  others are about 2 mm, so their centres are 3.0 mm from their neighbours: the
  end fingers' centres are 88.5 mm apart [from I1-front]. The KiCad redrawing
  agrees: P1's pads are 2.5 mm apart, its end pads 3 mm wide, 88.5 mm end to
  end [from I2]. The models plan's table counted the fingers among the rows at
  2.54 mm.
- **The pads on these scans** are tinned rings, light grey, round open holes,
  on lacquer so dark that it is no more coloured than the pads; light tells
  them apart, colour does not. The probe values are in `common.py`, beside
  `PAD_MIN_L`.
- **U1 and U4 each have two footprints** on both boards, a 600 mil one and a
  300 mil one sharing the row of pins 1 to 12 [from I1-front and I3]. The PAL
  board's XRM6216-10 RAMs sit in the 300 mil one [from I3].
- **The PAL NES-CPU-11's ten ICs are where the bare NES-CPU-10's are**, to
  about 0.4 mm at the most after a homography that each IC was held out of
  [from I1-front and I3]. Its 72-pin connector, expansion socket and modulator
  can hide their footprints on I3, so P1, P2 and P3 were not compared.
- **O2's photographs are not the camera's whole frame.** They are 4020 by
  2880 pixels, where the D7000 takes 4928 by 3264, so the plan's focal length
  of 112 / 23.6 x 4020 pixels, which takes the 4020 pixels to span the
  sensor, is not the camera's: the case top's vanishing points give about
  24,600 (O2-FL) and 23,800 pixels (O2-BR), near the 23,386 of a crop that was
  not resized [inferring]. With that camera the case's depth to width is 0.736
  and 0.730, against 0.800 from the published figures, and the corners'
  vertical edges do not lean as that camera says they must. The pictures'
  own records say how they were made [from the XMP and Exif in both files,
  read 5 October 2026]: Camera Raw's PerspectiveVertical and
  PerspectiveHorizontal are 0 and its HasCrop is False, the Software is Adobe
  Photoshop CS5 Windows, and the Exif SubjectDistance is 2.24 m; Camera Raw's
  LensProfileEnable is 1, so a lens profile was applied. So the crop
  was made in Photoshop, and Camera Raw corrected no perspective; a transform
  in Photoshop itself is not ruled out [guessing - verify]. The plan was
  revised that day: the case's proportions are judged on the patent, and
  these photographs give features only.
- **The patent's orthographic views** (O1, sheets 2 and 3 taken out of the
  PDF at their 300 dpi, the case's body without its buttons and feet),
  measured by `spike.py` (`spike.json`'s `case.patent`): depth to width 0.7723
  on the top view (FIG 5), 0.7758 on the bottom view (FIG 6) and 0.7914 from
  the side over the front (FIG 7 over FIG 3); height to width 0.3525 (FIG 3)
  and 0.3534 (FIG 7), 0.3682 and 0.3709 with the feet. Against 0.800 and 0.350
  (254 mm wide) the top view is 3.46 per cent short; against the midpoints of
  the published figures (widths of 254 and 256 mm), 0.797 and 0.349, it is
  3.08 per cent short. The plan judges the latter, within 5 per cent.

## What task 5 found: each console's parts

Read on 5 October 2026 by looking at crops of each part (the journal for that
day lists every crop), and recorded in `tools/nes-model/data/ic-table.json`
and `parts.json`. NTSC from I4 (an NES-CPU-07); PAL from I3 (an NES-CPU-11),
the PAL CPU also on I6. The marking is the part's top line; date codes left out.

| Ref | Role (the CPU-10's print) | NTSC, on I4 | PAL, on I3 |
|---|---|---|---|
| U6 | CPU | RP2A03G | RP2A07A (also I6) |
| U5 | PPU | RP2C02G-0 | RP2C07-0 |
| U1 | SRAM (WRAM) | MB8416A-15-SK (Fujitsu) | XRM6216-10 |
| U4 | SRAM (VRAM) | MB8416A-15-SK (Fujitsu) | XRM6216-10 |
| U2 | 74LS373 | SN74LS373N (Motorola's mark) | MB74LS373 (Fujitsu, Malaysia) |
| U3 | 74LS139 | SN74LS139N | SN74LS139N |
| U7 | 40H368(CI) | MN74HC368 | MC74HC368N (Motorola) |
| U8 | 40H368(CII) | MN74HC368 | MC74HC368N (Motorola) |
| U9 | 74HCU04P | 74HCU04AP (Toshiba) | SN74HCU04N (Texas Instruments) |
| U10 | CIC | 3193A | 3195A |
| X1 | X'tal | 21.47727, "KDS 8A", blue | 26.601712, "KDS 1G", orange |
| P3 | MOD RF | the modulator's frame, lid off, no marking seen | a closed can, "ALPS" embossed |

- **The RAMs are 300 mil parts on both boards.** The MB8416A-15-SK is a
  "skinny" DIP about 7 mm wide, and sits on the 300 mil footprint of U1 and
  U4, as the PAL board's XRM6216-10 does (task 0). Through I4's fit, the RAMs
  are 3.9 and 3.7 mm from where they would be on the 600 mil footprint.
- **The NTSC crystal reads 21.47727**, five places; the KiCad redrawing gives
  21.477272 MHz. **The PAL crystal reads 26.601712** [from I3, the can read
  upside down and turned].
- **U7 and U8 on the CPU-07 are MN74HC368s** (Panasonic's, by the MN prefix [inferring]), where the CPU-10's
  print and the redrawing say 40H368 (ic-table.json's pinout source is
  Toshiba's TC40H368, the 74LS368's pinout). Which controller port each serves
  is still inferred from the print, "CI" and "CII"; task 6 checks it.
- **The CPU-07 differs from the CPU-10 in its passives, not its chips.** Every
  IC and connector of the CPU-07 sits on the CPU-10 footprint of the same
  reference (each IC within 0.44 mm, held out, on I4 and on I5). Printed on
  the CPU-10 and not fitted on I4: R14, R15, R16, R17, C6, C7. Where the
  CPU-10 has C2's two holes 5.2 mm apart, the CPU-07 has an axial part on
  holes about 8.7 mm apart; it is not drawn.
- **Heights are typical, not measured.** The oblique photographs (I7) were not
  fetched for task 5; `HEIGHTS` in the parts module says `measured: false`
  for each.

## Downloaded for the work

SHA-256 of the files the research downloaded on 5 October 2026 (kept at `~/dbhq-previews/nes-model-research/full/`, I2 one folder up, not committed: the scans state no licence of their own and the PAL originals are up to 93 MB). From task 0 every input is listed in `tools/nes-model/data/sources.json` with its URL, author, licence as stated, size and fetch date, and checked against its hash before it is read.

| Id | File | Bytes | SHA-256 |
|---|---|---|---|
| O1 | `USD299726.pdf` | 140750 | `4c699b1b31a40ddf4a80f9827b7c927058dd54979acdd1988aa94c3060775f3d` |
| O2-FL | `Nintendo-Entertainment-System-NES-Console-FL.jpg` | 2193817 | `53c4ff11da6ba56bc2d9d8138dd585d126e08fe22350e8e0363003f9ddb8ab3c` |
| O2-BL | `Nintendo-Entertainment-System-NES-Console-BL.jpg` | 2269902 | `e068546c7b4ce5e2e5709c68b499bce9c7f5b2559e9b992650d74dc7e366be9d` |
| O2-BR | `Nintendo-Entertainment-System-NES-Console-BR.jpg` | 2230258 | `0681170937ae3af9db3fc8898e155792e295dacb5cb73942f8d2371c8733907f` |
| O4 | `Geöffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png` | 92882699 | `3cf7eca8f43bafd3a8f341127d6c6f728acd2da3d2deb2464d93b66681b913d6` |
| O5 | `Unterseite_NES_NESE-001_20221102_132229.jpg` | 3025854 | `a8e10ed2cf0148beccab8f7979f0a92497b01846df684e6c5260c56d4c9a4c27` |
| I1-front | `opentendo_NES-CPU-10_front_300dpi.png` | 6354680 | `fd41c714258a4d379d034eaf39cdcfcc7aab5273f4dafb74ff8517b55c88ab3a` |
| I1-back | `opentendo_NES-CPU-10_back_300dpi.png` | 6710399 | `fa15ea9e5a57c8621932fa4cbd8b8121feba82a746cc996627f95b6d12b8a0a5` |
| I2 | `opentendo_Motherboard.kicad_pcb` | 10954333 | `9cce8323c9c18f0c583f99d1e07d7650f85858ca2599cbf52b2c75f384e8224a` |
| I3 | `Frontalansicht_Mainboard_NES_NESE-001_HOF06378.png` | 76772853 | `e9f606535f4b01507a62f185ce38aa3403862bb82f434cc51f148a90088ea8cd` |
| I4 | `Nintendo-NES-Mk1-Motherboard-Top.jpg` | 10715307 | `2158318ca6e7c913fce4220e8763dc8df4b37e70fea50cf29a1c975556a5b46c` |
| I5 | `Nintendo-NES-Mk1-Motherboard-Bottom.jpg` | 9247611 | `64d52d1dbedfd123a56780def11821ef4157694e4d88d701924d567e2cb1ae10` |
| I6 | `RP2A07A_20221102.png` | 2438824 | `99208c2054e674c68aa78942f2c784873b13463d7bd5f2d649ec3e96c7e68c72` |

The two scans' sizes are the ones GitHub gives for those paths at the pinned commit (checked with `gh api` on 5 October 2026). Each id here is the id in `sources.json`: O2's corner photographs are O2-FL, O2-BL and O2-BR, and I1's two faces I1-front and I1-back. Not yet fetched: O2's `-FR`, O3, O6, O8, I7. The task that first needs each fetches it and records it here and in `sources.json`.
