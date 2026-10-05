# Photographs

Photographs of each machine's original device, shown on the machine's page.
Every machine page has at least one, always, and every one is credited on the
page (issue 28; Dan, 2 October 2026: rights are not a gate, the credit is). The
credits are built from the machine's `photos` list in `machines/registry.json`,
main photograph first, so the registry, this file and the page say the same
thing. A test fails the build when a running machine has no photograph, or any
listed photograph lacks its author, its source, the day it was fetched or what
it was used for, and another fails when a file here does not match the SHA-256
given for it below.

**These are photographs, not illustrations.** A generated image never stands
in for one: generated images live in `../imagery/`, are captioned
"Illustration" and are never evidence. Nothing in this folder is generated.

**These files are not MIT.** Each keeps the licence its source gives it, named
below and on the page, or none, where the source states none. A file here is a
resized copy of the source, which is a derivative under that licence.

**What the 3D model takes from each**, for a machine that has one (the KIM-1),
is in its section below and on the page. The analysis that does it is in
`tools/kim1-model/` (its `README.md` says how to run it), and the journal for 2
October 2026 has every figure. The BBC Micro has no model, so its photograph is
shown and nothing is taken from it.

## kim-1.webp

| | |
|---|---|
| What | An original MOS Technology KIM-1, Rev B, on display at the Musée Bolo, EPFL, Lausanne, photographed from above on black. The main photograph |
| Source | Wikimedia Commons, [`File:MOS_KIM-1_IMG_4211_cropped.jpg`](https://commons.wikimedia.org/wiki/File:MOS_KIM-1_IMG_4211_cropped.jpg) |
| Author | Rama & Musée Bolo (the photograph, `File:MOS_KIM-1_IMG_4211.jpg`); cropped by Tomer T |
| Licence | CC BY-SA 2.0 fr, as the file page states it ([deed](https://creativecommons.org/licenses/by-sa/2.0/fr/deed.en)). The photographer offers every image under CeCILL as well, and asks for a credit line naming Rama and the licence |
| Taken | 24 August 2010, 19:08 (the original's Exif, as its file page gives it). The cropped file's page gives 9 March 2012, which is when the crop was made |
| Fetched | 2 October 2026, from `upload.wikimedia.org/wikipedia/commons/4/4f/MOS_KIM-1_IMG_4211_cropped.jpg`: 3792 by 4675 pixels, 2,687,798 bytes, SHA-1 `85523be8e5e213285190a686462c9b9754d90265` (the SHA-1 Commons records for the file), SHA-256 `70aa1194749165ea9dc76718ee6ae607b47355c5d8f6c2373a43fc1d2887f7b9` |
| This copy | `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1973 pixels, 342,232 bytes, SHA-256 `1e418a3d2d0281179456f1f833dceb2effb66a0f5c25364aef7cbb06a354c5a4`. The original is not committed, being over 1.5 MB; the line above is enough to fetch it again and check it |
| Used for | The top face's tracks, as one of three photographs of it; the keypad's size and its keys' places, the display window, the crystal's can, the name and the red wire; and a check on where the chips sit |

## kim-1-bolo-end.webp

| | |
|---|---|
| What | The same board at the Musée Bolo, from above its other end, slightly oblique |
| Source | Wikimedia Commons, [`File:MOS_KIM-1_IMG_4210.jpg`](https://commons.wikimedia.org/wiki/File:MOS_KIM-1_IMG_4210.jpg) |
| Author | Rama & Musée Bolo |
| Licence | CC BY-SA 2.0 fr, as the file page states it ([deed](https://creativecommons.org/licenses/by-sa/2.0/fr/deed.en)) |
| Taken | 24 August 2010, 19:08 (Exif: Canon EOS 5D Mark II, 100 mm, f/16) |
| Fetched | 2 October 2026, from `upload.wikimedia.org/wikipedia/commons/a/ac/MOS_KIM-1_IMG_4210.jpg`: 5616 by 3744 pixels, 3,286,622 bytes, SHA-1 `b091a9965622932eecee5f7ec978bac123f373ae` (as Commons records it), SHA-256 `0192f1414ca5b9a2f40cf01e2c8b4b75b9025dc8f695de1f13a6000d0f60e38e` |
| This copy | `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1067 pixels, 164,292 bytes, SHA-256 `cfba26be08313f6dfa6e10036d2a3459acd6af1165e54f0724f6c566f40685d3`. The original is not committed, being over 1.5 MB |
| Used for | The top face's tracks, as one of three photographs of it, and the parts' heights, by triangulation with the next photograph |

## kim-1-bolo-oblique.webp

| | |
|---|---|
| What | The same board at the Musée Bolo, from a low corner, so the parts' heights show |
| Source | Wikimedia Commons, [`File:MOS_KIM-1_IMG_4209.jpg`](https://commons.wikimedia.org/wiki/File:MOS_KIM-1_IMG_4209.jpg) |
| Author | Rama & Musée Bolo |
| Licence | CC BY-SA 2.0 fr, as the file page states it ([deed](https://creativecommons.org/licenses/by-sa/2.0/fr/deed.en)) |
| Taken | 24 August 2010, 19:07 (Exif: Canon EOS 5D Mark II, 100 mm, f/16) |
| Fetched | 2 October 2026, from `upload.wikimedia.org/wikipedia/commons/d/d4/MOS_KIM-1_IMG_4209.jpg`: 5616 by 3744 pixels, 2,774,408 bytes, SHA-1 `a17b16c8324c1894e32ea7cb0ec515bf3bdcb162` (as Commons records it), SHA-256 `1a8d86a420f1424f6e23b958c374b19c91deb5cbbc9f840d419bec29df901b7d` |
| This copy | `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1067 pixels, 169,822 bytes, SHA-256 `b295c0ea1fe9dddb596840998266e1b97e4f346ef8c26d3eb4ab24683918228f`. The original is not committed, being over 1.5 MB |
| Used for | The parts' heights, by triangulation with the photograph before |

## kim-1-rev-b-front.webp

| | |
|---|---|
| What | Another original MOS KIM-1 of the same revision, Rev B, its component side, photographed square on, on white |
| Source | Hans Otten's Retro Computing site, the page [KIM-1 revisions images](http://retro.hansotten.nl/6502-sbc/kim-1-manuals-and-software/kim-1-revisions/), under Rev B. The site serves no https (checked on 2 October 2026), so the link is http |
| Author | Not named. Hans Otten's Retro Computing site publishes it with no caption, and the page says its images are "some from photos by myself, others from the internet" |
| Licence | No licence stated, on the page or the site |
| Taken | 17 March 2016, 14:33:32 (Exif: LG-H815, a phone, 4.42 mm, an HDR shot) |
| Fetched | 2 October 2026, from `retro.hansotten.nl/wp-content/uploads/2022/03/20160317_143332_HDR-2.jpg` (the full size; the page shows a copy named `-scaled`): 2988 by 3984 pixels, 8,290,855 bytes, SHA-256 `e666f4a30c44267b27f21cab5ee3d75047291903e920799e246fbd8210d292e7` |
| This copy | `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 2134 pixels, 450,042 bytes, SHA-256 `22ab8b6194fe3c0443a17450abcc28034a256f2fd64d968ec0d6407c54db0b83`. The original is not committed, being over 1.5 MB |
| Used for | The top face's tracks, as one of three photographs of it. A different board from the Musée Bolo's, so where a part or the red wire hides a track on that one, this one often shows it; and a check on where the chips sit |

## kim-1-rev-b-back.webp

| | |
|---|---|
| What | The same Rev B board as the one above, turned over: its solder side, photographed square on, on white |
| Source | Hans Otten's Retro Computing site, the page [KIM-1 revisions images](http://retro.hansotten.nl/6502-sbc/kim-1-manuals-and-software/kim-1-revisions/), under Rev B, served over http only |
| Author | Not named. Hans Otten's Retro Computing site publishes it with no caption |
| Licence | No licence stated, on the page or the site |
| Taken | 17 March 2016, 14:33:41 (Exif: LG-H815) |
| Fetched | 2 October 2026, from `retro.hansotten.nl/wp-content/uploads/2022/03/20160317_143341_HDR-2.jpg`: 2988 by 3984 pixels, 9,104,580 bytes, SHA-256 `5365993e8a8a42abb07a3c64c825f2dc2a74986cf8105e16bdceabd8f7e5876d` |
| This copy | `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 2134 pixels, 507,520 bytes, SHA-256 `3e1d4161b19e5a624a97f0ab6b2f3c91444d62453e00f9cd14b44dfe4c1eb631`. The original is not committed, being over 1.5 MB |
| Used for | The underside's tracks: the only photograph of the solder side used |

## bbc-micro.webp

| | |
|---|---|
| What | An original Acorn BBC Micro, seen from the front left and above on a blue cloth: the whole case, the red function keys, the badge strip and the three lights below the bottom left of the keyboard, to the left of the space bar. The main photograph |
| Source | Wikimedia Commons, [`File:Acorn_BBC_Micro.jpg`](https://commons.wikimedia.org/wiki/File:Acorn_BBC_Micro.jpg), from simon.inns's Flickr photograph [40334359291](https://www.flickr.com/photos/130561631@N03/40334359291/), whose licence Commons's FlickreviewR 2 bot checked |
| Author | simon.inns, as the file page names him (Simon Inns, by his Flickr account) |
| Licence | CC BY 2.0, as the file page states it ([deed](https://creativecommons.org/licenses/by/2.0)), with attribution required: the page credits the author, links the source and links the licence to its deed. It asks for no share-alike, unlike the KIM-1's |
| Which model | The file page describes it as "An Acorn BBC Micro Model B from 1982", and the page's caption says "an original Acorn BBC Micro Model B" on that word. The picture alone cannot tell a Model B from a Model A: the case, the keyboard and the badge are the same on both, and the difference is inside and at the back, which the photograph does not show. Nothing in it is a later machine's (no numeric keypad, no Master badge) |
| Taken | 18 February 2018, 12:08 (`DateTimeOriginal`, as the file page gives it). Uploaded to Commons on 14 September 2021 |
| Fetched | 4 October 2026, from `upload.wikimedia.org/wikipedia/commons/e/e7/Acorn_BBC_Micro.jpg`: 5195 by 3463 pixels, 2,257,642 bytes, SHA-1 `71f6dfdfee2a4a43d207936c310a57b30e0b4b39` (the SHA-1 Commons records for the file), SHA-256 `2329615eda5436b6836132a4f08ba46be18ea30df990866d3c9ac69d9006ca91` |
| This copy | Cropped to the machine and a margin of 110 pixels of cloth on every side, then resized: `cwebp -q 82 -crop 152 60 4991 3342 -resize 1600 0 -metadata none`: 1600 by 1072 pixels, 88,150 bytes, SHA-256 `8796aa341c38d9399788f5d86b4a7e720c8334b331772405a0c093ee33416bfb`. Colours are the source's. The original is not committed, being over 1.5 MB; the line above is enough to fetch it again and check it |
| Used for | The photograph of the machine at the head of its page. Nothing is traced or measured from it |

The site builds AVIF and WebP copies of each file here at build time
(`astro:assets`), so a page never loads a 1600 pixel master.

## The drawing the photographs are registered to

Not a photograph, so not in this folder, but the model is measured against it
and the page credits it beside them, from the registry's `drawings` list.

| | |
|---|---|
| What | A replica of the KIM-1's Rev D board in KiCad: both copper layers, every pad, drill and footprint. Its README says how it was made: a high-resolution photograph of a KIM-1 scaled by its chips' footprints (0.1 inch pin pitch), its tracks traced over it, imported into KiCad and checked against the schematic; and that the replica has been built and works |
| Source | [github.com/eduardocasino/kim-1](https://github.com/eduardocasino/kim-1), `kim-1.kicad_pcb` at commit `7b356b6386422cc6f7154b3524b0a9011ce33bfb` |
| Author | Eduardo Casino |
| Licence | CC BY-NC 4.0, as its `LICENSE.md` states it ([deed](https://creativecommons.org/licenses/by-nc/4.0/)); its schematic is CC BY 4.0 |
| Fetched | 2 October 2026: 6,722,319 bytes, SHA-256 `87a344d408ca812429797f928029473a4f5421e979326b784f0cebc2e8239f3e`. Not committed; `tools/kim1-model/extract_kicad.py` checks that hash and writes what the analysis uses to `tools/kim1-model/data/` |
| Used for | What every photograph is registered to; the places of the parts soldered through the board; the tracks under the chips, the display and the keypad, which no photograph shows; and the reference the traced tracks are measured against |

## The track map, src/assets/tracks/kim-1.webp

The copper on both faces of the KIM-1's 3D model. One lossless WebP whose three
channels are kept apart because they come from different sources under
different terms:

| Channel | What | From | Licence |
|---|---|---|---|
| Red | The top face's copper, where a photograph shows the board | `kim-1.webp`, `kim-1-bolo-end.webp` and `kim-1-rev-b-front.webp`, registered and traced, then a vote | CC BY-SA 2.0 fr (the Musée Bolo photographs, share-alike), and no licence stated (Hans Otten's) |
| Green | The top face's copper where no photograph shows the board | The KiCad replica above | CC BY-NC 4.0 |
| Blue | The underside's copper | `kim-1-rev-b-back.webp`, registered and traced | No licence stated |

**These terms cannot all be met by one licence at once**: share-alike asks for
the same licence on a derivative, the replica asks for no commercial use, and
the Rev B photographs state no licence. Rights are not a gate (Dan, 2 October
2026), so the map is published crediting every source, on the page under the
model and here, with each channel's source named; none of it is MIT.

| | |
|---|---|
| Made by | `tools/kim1-model/`: `register.py`, `rectify.py`, `trace.py`, then `fuse.py`, from the full-size originals above, on 2 October 2026. Its `README.md` says how, and the journal has the output |
| This copy | 1600 by 2184 pixels, 8 pixels to the millimetre, covering the board's body edge to edge |
| Served | Copied to `/models/kim-1-tracks.webp` by `scripts/build-models.mjs` at every build, and fetched by the model's bundle when the model loads, never with the page |

The build does not run the analysis, and nothing in the tests or the build
fetches a photograph or the replica.

## The NES board's track map, src/assets/tracks/nes-famicom-board.webp

The copper on both faces of the NES main board, NES-CPU-10, for the NES's
inside 3D model, and its printed legend. **The map is traced from OpenTendo's
scans**, the bare board scanned on both faces at 300 dpi, read from the
`dbhq-uk` fork at commit `3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009`
([dbhq-uk/OpenTendo](https://github.com/dbhq-uk/OpenTendo), a fork of
[Redherring32/OpenTendo](https://github.com/Redherring32/OpenTendo)). The scans
are not photographs of ours and are not committed: they state no licence of
their own, and OpenTendo's README puts the repository under the TAPR Open
Hardware License (www.tapr.org/OHL).

| Channel | What | From | Terms |
|---|---|---|---|
| Red | The component side's copper | `NES-CPU-10_front_300dpi.png`, traced | TAPR Open Hardware License, as OpenTendo's README states it |
| Green | The solder side's copper | `NES-CPU-10_back_300dpi.png`, registered to the front and traced | The same |
| Blue | The printed legend, as it is printed, Nintendo's name included | `NES-CPU-10_front_300dpi.png`, traced | The same |

| | |
|---|---|
| Made by | `tools/nes-model/board_trace.py` on 5 October 2026, from the full-size scans, in the board frame of `tools/nes-model/data/frame.json`. Its `README.md` says how to run it; `tools/nes-model/data/copper.json` holds the map's size, bytes and SHA-256 and the checks on it; the journal for 5 October 2026 has the figures |
| This copy | 1959 by 1194 pixels, 10 pixels to the millimetre, lossless, covering the board edge to edge; each channel 255 or 0 |
| Terms | The TAPR Open Hardware License's, credited to OpenTendo and its authors; `NOTICE.md` says what is derived from the scans |

The build does not run the analysis, and nothing in the tests or the build
fetches a scan. The inside model that draws the map comes later.
