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
October 2026 has every figure. The BBC Micro's main photograph, `bbc-micro.webp`, is
shown and nothing is taken from it. Its five others are measured, on their full-size originals, for its outside model (task 8 of its models plan, 13 October 2026; the model is not on the page yet), in `tools/bbc-micro-model/` (the journal for 4 October 2026, task 8, has every figure). The NES has two: its board's model,
which takes nothing from the photographs here, being measured from scans and
photographs that are not committed, listed in their own section below; and its
outside's, which takes from `nes.webp`, `nes-rear-right.webp`, `nes-pal-front.webp` and
`nes-pal-underside.webp`, each measured on its full-size original.

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

## bbc-micro-top.webp

| | |
|---|---|
| What | A BBC Micro, Issue 7, with the Type 1 keyboard (the file page's words), from almost straight above on a dark red carpet: the case top, the keyboard, the badge strip and the three lights |
| Source | Wikimedia Commons, [`File:Acorn_BBC_Microcomputer.jpg`](https://commons.wikimedia.org/wiki/File:Acorn_BBC_Microcomputer.jpg), which credits Deskthority's wiki as where it came from |
| Author | Daniel Beardsmore, as the file page names him ("Daniel beardsmore") |
| Licence | Public domain, as the file page states it: "Public Domain. If your country doesn't have any such notion, treat the work as having all rights waived." Checked on Commons' API on 13 October 2026 (`LicenseShortName` is `Public domain`). No credit is owed; the page gives one anyway |
| Taken | 4 February 2016 (the file page's date). Uploaded to Commons on 9 February 2022 |
| Fetched | 13 October 2026 (first on 4 October), from `upload.wikimedia.org/wikipedia/commons/f/f8/Acorn_BBC_Microcomputer.jpg`: 2490 by 1868 pixels, 654,984 bytes, SHA-1 `f8ee3ba32bb3ea9c107a34e64282caca21c533e8` (the SHA-1 Commons records for the file; the file here matches it), SHA-256 `a616c736c51c4bf0f796be21ba426d435acdbf2d6b692cbf0cc3256f5782949a` |
| This copy | Not cropped. Resized with sharp 0.35.5 (libvips 8.18.7, libwebp 1.6.0) from `site/node_modules`, as `cwebp` is not installed on the machine that made it: `sharp(original).resize({ width: 1600 }).webp({ quality: 82 })`, which keeps no metadata, the same settings as the other photographs' `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1200 pixels, 176,898 bytes, SHA-256 `345d2df5d810254d3117a927377deade39423d522a47a2ba3ad714afdab5f36c`. Colours are the source's. The original is not committed; the line above is enough to fetch it again and check it |
| Used for | The outside model, measured on the full-size original (task 8 of the BBC Micro models plan): the case top registered on its four corners to the Service Manual's 415 by 345 mm, the keyboard's opening, the badge strip and its words, the order of the three lights, and the keys registered to Dominic Beesley's MIT keyboard drawing (K1) and their colours. The owl is not drawn |

## bbc-micro-rear.webp

| | |
|---|---|
| What | The rear of a BBC Micro on a white background (painted in), nearly square on: the ports along the black rear panel, each with its name, the two ventilation slots and the mains panel |
| Source | Wikimedia Commons, [`File:BBC_Micro_rear.jpeg`](https://commons.wikimedia.org/wiki/File:BBC_Micro_rear.jpeg) |
| Author | Stuart Brady (assumed by Commons): Commons names the uploader, StuartBrady, as the author, assumed from the copyright claims |
| Licence | Public domain, as the file page states it, the uploader's own work assumed by Commons from its copyright claims. Checked on Commons' API on 13 October 2026 (`LicenseShortName` is `Public domain`, `Artist` "StuartBrady assumed (based on copyright claims)"). No credit is owed; the page gives one anyway |
| Taken | 26 December 2005 (the file page's date, the day it was uploaded; the file has no Exif) |
| Fetched | 13 October 2026 (first on 4 October), from `upload.wikimedia.org/wikipedia/commons/2/29/BBC_Micro_rear.jpeg`: 2048 by 994 pixels, 107,231 bytes, SHA-1 `3dad0bd9d8efedc7b9f6f6297ee94c32c6e3d572` (as Commons records it; the file here matches it), SHA-256 `4072a59af7f7fab834ca2b912b0f6e3449aa8bdaa01bb621d9d556579dd5937e` |
| This copy | Not cropped. Resized with sharp 0.35.5 (libvips 8.18.7, libwebp 1.6.0) from `site/node_modules`, as `cwebp` is not installed on the machine that made it: `sharp(original).resize({ width: 1600 }).webp({ quality: 82 })`, which keeps no metadata, the same settings as the other photographs' `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 777 pixels, 41,976 bytes, SHA-256 `1f3a684c81e09d5932864c5177e731ebede49858f922e7d874f02f9b72f9719a`. The original is not committed |
| Used for | The outside model's rear, measured on the full-size original: each port's place and height on the scale of the 15-way D socket, checked against its socket on the board, the ventilation slots and the mains panel |

## bbc-micro-keyboard.webp

| | |
|---|---|
| What | A BBC Micro keyboard, the Type 1 (Futaba simplified linear, the file page's words), out of its case on a wooden table |
| Source | Wikimedia Commons, [`File:BBC_Micro_Type_1.jpg`](https://commons.wikimedia.org/wiki/File:BBC_Micro_Type_1.jpg), which credits Deskthority's wiki as where it came from |
| Author | Daniel Beardsmore, as the file page names him ("Daniel beardsmore") |
| Licence | Public domain, as the file page states it ("Public domain or nearest equivalent"). Checked on Commons' API on 13 October 2026 (`LicenseShortName` is `Public domain`). No credit is owed; the page gives one anyway |
| Taken | 24 June 2013 (the file page's date). Uploaded to Commons on 13 February 2022 |
| Fetched | 13 October 2026 (first on 4 October), from `upload.wikimedia.org/wikipedia/commons/4/41/BBC_Micro_Type_1.jpg`: 1996 by 1512 pixels, 355,138 bytes, SHA-1 `5bd4df29308488c8e34f24483f46bf96c71c746b` (as Commons records it; the file here matches it), SHA-256 `745b34357b2befd5d1bcc501bb48ad9c3e345e2b876d59b3f812cec48d2e2403` |
| This copy | Not cropped. Resized with sharp 0.35.5 (libvips 8.18.7, libwebp 1.6.0) from `site/node_modules`, as `cwebp` is not installed on the machine that made it: `sharp(original).resize({ width: 1600 }).webp({ quality: 82 })`, which keeps no metadata, the same settings as the other photographs' `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1212 pixels, 147,632 bytes, SHA-256 `62400e527ae21ad2b7f462a1f62fab8e02092017c4e8b2625b2ad0f20f630d3f`. The original is not committed |
| Used for | A check for the outside model, measured on the full-size original: its keys' legends registered to K1, recorded |

## bbc-micro-front.webp

| | |
|---|---|
| What | A BBC Micro on a white background (painted in, across some of the case's corners), from the front and above: the case top, the keyboard panel with its slot at the left, the badge strip and the three lights |
| Source | Wikimedia Commons, [`File:BBC_Micro.jpeg`](https://commons.wikimedia.org/wiki/File:BBC_Micro.jpeg) |
| Author | Stuart Brady (assumed by Commons): Commons names the uploader, StuartBrady, as the author, assumed from the copyright claims |
| Licence | Public domain, as the file page states it, the uploader's own work assumed by Commons from its copyright claims. Checked on Commons' API on 13 October 2026 (`LicenseShortName` is `Public domain`, `Artist` "StuartBrady assumed (based on copyright claims)"). No credit is owed; the page gives one anyway |
| Taken | 26 December 2005, 16:03:25 (Exif: Olympus C760UZ at 6.3 mm, a crop of its 2048 by 1536 frame) |
| Fetched | 13 October 2026, from `upload.wikimedia.org/wikipedia/commons/3/37/BBC_Micro.jpeg`: 1929 by 1338 pixels, 210,220 bytes, SHA-1 `6b13abeb0250c0fce17b7e8bf0a0d6f8388dc343` (as Commons records it; the file here matches it), SHA-256 `4129306b45dda6b403e4cbab6fa4e96e305e0f9eaa5eb93c223e3dc74fa968c4` |
| This copy | Not cropped. Resized with sharp 0.35.5 (libvips 8.18.7, libwebp 1.6.0) from `site/node_modules`, as `cwebp` is not installed on the machine that made it: `sharp(original).resize({ width: 1600 }).webp({ quality: 82 })`, which keeps no metadata, the same settings as the other photographs' `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1110 pixels, 100,960 bytes, SHA-256 `2aef9e41131bd73f801ca2a279787adbce106baa6759766a864bf291a9aba884`. The original is not committed |
| Used for | The outside model, measured on the full-size original: with `bbc-micro-left.webp`, one camera fitted to both on the keys' legends, which places the keys, the keyboard's opening and the three lights in height |

## bbc-micro-left.webp

| | |
|---|---|
| What | The same BBC Micro on a white background (painted in), from the front left and above, 80 seconds after `bbc-micro-front.webp` by the two files' Exif |
| Source | Wikimedia Commons, [`File:BBC_Micro_left.jpeg`](https://commons.wikimedia.org/wiki/File:BBC_Micro_left.jpeg), whose page says "Photographed by me. Released for the public domain." |
| Author | Stuart Brady (assumed by Commons): Commons names the uploader, StuartBrady, as the author, assumed from the copyright claims |
| Licence | Public domain, as the file page states it, the uploader's own work assumed by Commons from its copyright claims. Checked on Commons' API on 13 October 2026 (`LicenseShortName` is `Public domain`, `Artist` "StuartBrady assumed (based on copyright claims)"). No credit is owed; the page gives one anyway |
| Taken | 26 December 2005, 16:04:45 (Exif: Olympus C760UZ at 6.3 mm, a crop of its 2048 by 1536 frame) |
| Fetched | 13 October 2026, from `upload.wikimedia.org/wikipedia/commons/8/8a/BBC_Micro_left.jpeg`: 1756 by 1239 pixels, 118,678 bytes, SHA-1 `e29fb1ed2a95230eae211c3492407f46e6f20ad7` (as Commons records it; the file here matches it), SHA-256 `7370967288c56ff10355a754f5dca5f73b1caf8285749fd0deb263cde7986e94` |
| This copy | Not cropped. Resized with sharp 0.35.5 (libvips 8.18.7, libwebp 1.6.0) from `site/node_modules`, as `cwebp` is not installed on the machine that made it: `sharp(original).resize({ width: 1600 }).webp({ quality: 82 })`, which keeps no metadata, the same settings as the other photographs' `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1129 pixels, 69,102 bytes, SHA-256 `a4d1e06629bce60bc26d9e2bbea72ceb5485d225c84890b6b605401e14c57b34`. The original is not committed |
| Used for | The outside model, measured on the full-size original: the second view for the camera fitted with `bbc-micro-front.webp` |

## nes.webp

| | |
|---|---|
| What | An original Nintendo Entertainment System, the North American console, seen from the front left and a little above on white: the whole case, the cartridge slot's lid with the red name on its front, the POWER and RESET buttons and the power light, and the two controller ports. The main photograph |
| Source | Wikimedia Commons, [`File:Nintendo-Entertainment-System-NES-Console-FL.jpg`](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-FL.jpg), the photographer's own work |
| Author | Evan-Amos, as the file page names him |
| Licence | Public domain, as the file page states it: the photographer's `{{PD-self}}` release ("I, the copyright holder of this work, release this work into the public domain. This applies worldwide."). No credit is owed; the page gives one anyway |
| Which console | The file page describes it as "the first game console released in America by Nintendo", so the North American NES, the NTSC console with the 2A03. The picture alone cannot tell it from a PAL console, whose case is the same; the difference is the label underneath and the chips inside, which the photograph does not show |
| Taken | 27 July 2016, 21:32 (`DateTimeOriginal`: Nikon D7000). Uploaded to Commons on 29 July 2016, by the photographer, over two earlier photographs of 2011 under the same name; the file used is the current one, whose SHA-1 is below |
| Fetched | 6 October 2026, from `upload.wikimedia.org/wikipedia/commons/8/82/Nintendo-Entertainment-System-NES-Console-FL.jpg`: 4020 by 2880 pixels, 2,193,817 bytes, SHA-1 `339e959bbdb741c24a7f00e4256152b00e5c802c` (the SHA-1 Commons records for the file), SHA-256 `53c4ff11da6ba56bc2d9d8138dd585d126e08fe22350e8e0363003f9ddb8ab3c` |
| This copy | Not cropped: the console already sits in an even margin of about 240 pixels of white on every side (ImageMagick's trim box at 2 % fuzz is 3520 by 2395 at 243, 249). Resized only: `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1147 pixels, 32,014 bytes, SHA-256 `4325012461f3919fc899d0a9adca509d5f5f09d14833905c076c0cc1e6335f55`. Colours are the source's. The original is not committed, being over 1.5 MB; the line above is enough to fetch it again and check it |
| Used for | The photograph of the machine at the head of its page, and, measured on its full-size original, the outside model's front, top and left end: the cartridge door, the vents, the buttons, the power light, the controller ports and the lean of the lower shell (task 9 of the NES models plan, 6 October 2026; the measuring is task 8's). The board's model takes nothing from it |

## nes-rear-right.webp

| | |
|---|---|
| What | An original Nintendo Entertainment System, the North American console, seen from above and behind and a little from the right on white: the rear with its three connectors in a recess at the lower left, the top shell's block of vents between two black end blocks and the cartridge slot's lid |
| Source | Wikimedia Commons, [`File:Nintendo-Entertainment-System-NES-Console-BR.jpg`](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-BR.jpg), the photographer's own work |
| Author | Evan-Amos, as the file page names him |
| Licence | Public domain, as the file page states it: the photographer's `{{PD-self}}` release ("I, the copyright holder of this work, release this work into the public domain. This applies worldwide."). Checked on Commons' API on 6 October 2026 (`LicenseShortName` is `Public domain`). No credit is owed; the page gives one anyway |
| Which console | The file page describes it as showing "a DC in, channel 3/4 selector and RF A/V out", so the North American NES, the NTSC console. The same console as `nes.webp`, from the other side |
| Taken | 27 July 2016 (`DateTimeOriginal`: Nikon D7000). The file page's own date, 29 July 2016, is when it was uploaded |
| Fetched | 5 October 2026, from `upload.wikimedia.org/wikipedia/commons/c/cf/Nintendo-Entertainment-System-NES-Console-BR.jpg`: 4020 by 2880 pixels, 2,230,258 bytes, SHA-1 `ec812d15b0117d144825bf214b0eedab1f34aa90` (the SHA-1 Commons records for the file; the file here matches it), SHA-256 `0681170937ae3af9db3fc8898e155792e295dacb5cb73942f8d2371c8733907f` |
| This copy | Not cropped. Resized only: `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1147 pixels, 26,862 bytes, SHA-256 `2db816d995fdcba4b6d4492a4a92a74dfb7e0d9b1de723c513f5c6130bf41b19`. Colours are the source's. The original is not committed, being over 1.5 MB; the line above is enough to fetch it again and check it |
| Used for | The outside model's rear, measured on the full-size original with the rear face rectified: its window, the places of the three connectors and their words, and the notch; and the lean of the lower shell, held out as a check (task 8 of the NES models plan; the credit is moved here from the registry's references, where it stood until 6 October 2026) |

## nes-pal-front.webp

| | |
|---|---|
| What | The front of the top shell of a PAL Nintendo Entertainment System, a European console whose plastic has yellowed, seen straight on from the front against a pale ground: the cartridge slot's lid with its red words, including EUROPEAN VERSION, and the black band at the right. A wide, shallow strip |
| Source | Wikimedia Commons, [`File:Geöffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png`](https://commons.wikimedia.org/wiki/File:Ge%C3%B6ffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png), the photographer's own work |
| Author | PantheraLeo1359531, as the file page names the user |
| Licence | CC BY 4.0, as the file page states it ([deed](https://creativecommons.org/licenses/by/4.0/)), checked on Commons' API on 6 October 2026 (`LicenseShortName` is `CC BY 4.0`, `AttributionRequired` is true). The credit above, with the licence's name and link, is the attribution; the copy here is resized, which is a change, and this file says so (the page's credit line does not yet say it: see the journal's task 8 follow-up) |
| Which console | The file page is titled "Geöffnetes deutsches NES" and describes a German Nintendo Entertainment System, so the PAL console; its label underneath, in `nes-pal-underside.webp`, is NESE-001 |
| Taken | 2 November 2022 (the file page's `DateTimeOriginal`; the page's `DateTime`, 5 April 2023, is when the file was exported). The camera is a Sony ILCE-7RM4 at 20 mm, as `tools/nes-model/data/sources.json` records |
| Fetched | 5 October 2026, from `upload.wikimedia.org/wikipedia/commons/d/d0/Ge%C3%B6ffnetes_deutsches_NES_20221102_HOF06342_RAW-Export.png`: 8606 by 2253 pixels, 92,882,699 bytes, SHA-1 `be7b9617e71ce915a27b0b1950f53150bba1a1e5` (the SHA-1 Commons records for the file; the file here matches it), SHA-256 `3cf7eca8f43bafd3a8f341127d6c6f728acd2da3d2deb2464d93b66681b913d6` |
| This copy | Not cropped. Resized only: `cwebp -q 82 -resize 1600 0 -metadata none`, which reads the PNG as it is: 1600 by 419 pixels, 18,370 bytes, SHA-256 `0853dfe7e4545574ca4fe83b2c3283a6debc2ce5ae9606e12ee769aff57e3f08`. The original is not committed, being 93 MB; the line above is enough to fetch it again and check it |
| Used for | The outside model's PAL console: its front words, including the line EUROPEAN VERSION, and a check of its front against the NTSC console's, measured on the full-size original (task 8 of the NES models plan; the credit is moved here from the registry's references, where it stood until 6 October 2026) |

## nes-pal-underside.webp

| | |
|---|---|
| What | The underside of a PAL Nintendo Entertainment System, a NESE-001, photographed from above on a patterned carpet: its two labels, four feet, two vent grilles, the cover of the expansion port and a black plate with two screws |
| Source | Wikimedia Commons, [`File:Unterseite_NES_NESE-001_20221102_132229.jpg`](https://commons.wikimedia.org/wiki/File:Unterseite_NES_NESE-001_20221102_132229.jpg), the photographer's own work |
| Author | PantheraLeo1359531, as the file page names the user |
| Licence | CC BY 4.0, as the file page states it ([deed](https://creativecommons.org/licenses/by/4.0/)), checked on Commons' API on 6 October 2026 (`LicenseShortName` is `CC BY 4.0`, `AttributionRequired` is true). The credit above, with the licence's name and link, is the attribution; the copy here is resized, which is a change, and this file says so (the page's credit line does not yet say it: see the journal's task 8 follow-up) |
| Which console | The file page says "Bottom of the model NESE-001 of the Nintendo Entertainment System", and the labels in it are German, so the PAL console |
| Taken | 2 November 2022, 13:22 (`DateTimeOriginal`: a Samsung SM-G988B, a phone, so not the camera of `nes-pal-front.webp`) |
| Fetched | 5 October 2026, from `upload.wikimedia.org/wikipedia/commons/2/23/Unterseite_NES_NESE-001_20221102_132229.jpg`: 4000 by 3000 pixels, 3,025,854 bytes, SHA-1 `15faa3996e91bd7f0c9433c7baf3e64ae51c655f` (the SHA-1 Commons records for the file; the file here matches it), SHA-256 `a8e10ed2cf0148beccab8f7979f0a92497b01846df684e6c5260c56d4c9a4c27` |
| This copy | Not cropped. Resized only: `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1200 pixels, 244,400 bytes, SHA-256 `466c1ae3cb419ee6dfa5422f3546cebe5d731acf3aa3437f91ec12ba6a4b0c8b`. The original is not committed, being over 1.5 MB; the line above is enough to fetch it again and check it |
| Used for | The outside model's PAL underside, measured on the full-size original rectified on its six screw holes: the two labels and their words, the feet and the expansion port's cover (task 8 of the NES models plan; the credit is moved here from the registry's references, where it stood until 6 October 2026) |

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
| Connections | The copper is traced to look at: its connectivity is not verified (the check on its known nets failed, 5 October 2026) |
| Terms | The TAPR Open Hardware License's, credited to OpenTendo and its authors; `NOTICE.md` says what is derived from the scans |

The build does not run the analysis, and nothing in the tests or the build
fetches a scan. The NES board's model draws the map, served as
`/models/nes-famicom-board-tracks.webp` beside its bundle by
`scripts/build-models.mjs` and fetched with the model, never with the page.

## The BBC Micro board's track map, src/assets/tracks/bbc-micro-board.webp

The copper on both faces of the BBC Micro Model B's main board, Issue 7, for the BBC Micro's inside 3D model, and its printed legend. **The map is traced from amb5l's scans**, the bare board scanned on both faces at 400 dpi and posted to the stardot forum ([thread 23329](https://stardot.org.uk/forums/viewtopic.php?t=23329)). The scans state no licence and are not committed, not even resized.

| Channel | What | From | Terms |
|---|---|---|---|
| Red | The component side's copper | I1, amb5l's scan of the component side, traced | No licence stated for the scan; `NOTICE.md` says what is derived from it |
| Green | The solder side's copper | I2, amb5l's scan of the solder side, registered to I1 and traced | The same |
| Blue | The printed legend, as it is printed, Acorn's name included | I1, traced | The same |

| | |
|---|---|
| Made by | `tools/bbc-micro-model/board_trace.py` on 13 October 2026, from the full-size scans, in the board frame of `tools/bbc-micro-model/data/frame.json`. Its `README.md` says how to run it; `tools/bbc-micro-model/data/copper.json` holds the map's size, bytes and SHA-256 and the checks on it; the journal for 4 October 2026 has the figures, under task 4 |
| This copy | 8 pixels to the millimetre, lossless, covering the board edge to edge; each channel 255 or 0 |
| Connections | The copper is traced to look at: its connections are not verified (the check on its known nets failed, and so did the check of the component side against I5, 8BS's photograph of a populated board, 13 October 2026) |
| Terms | amb5l credited as the source of the scans, which state no licence; `NOTICE.md` says what is derived from them |

The build does not run the analysis, and nothing in the tests or the build fetches a scan.

## The NES board's sources, which are not committed

The NES board's model is measured from these, and none of them is in the
repository: the scans state no licence of their own, and the photographs are
large originals that are not shown on the page. Each is credited under the
model, from the NES's `references` in `machines/registry.json`, which give the
same address and SHA-256; `tools/nes-model/data/sources.json` has every input,
and the tools check each against its SHA-256 before reading it. Fetched on 5
October 2026.

| Id | What | Address | Author | Licence, as stated | SHA-256 of the original | Used for |
|---|---|---|---|---|---|---|
| I1-front | A flatbed scan of a bare NES-CPU-10, its component side, at 300 dots an inch, in OpenTendo, as this project's fork of it holds it | <https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Scans/NES-CPU-10_front_300dpi.png> | The OpenTendo project, by Redherring32 and its contributors (the scans added by Kamoteshake; the scanner is not named) | None stated | `fd41c714258a4d379d034eaf39cdcfcc7aab5273f4dafb74ff8517b55c88ab3a` | The board's outline, holes, pads and scale, the component side's copper and the printed legend, traced to look at, and the place of every part |
| I1-back | The same board's solder side, scanned the same way, in OpenTendo, as this project's fork of it holds it | <https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Scans/NES-CPU-10_back_300dpi.png> | The OpenTendo project, by Redherring32 and its contributors (the scans added by Kamoteshake; the scanner is not named) | None stated | `fa15ea9e5a57c8621932fa4cbd8b8121feba82a746cc996627f95b6d12b8a0a5` | The solder side's copper, traced to look at after the scan was turned over and fitted to the component side by its drilled holes |
| I3 | A photograph of a populated PAL board, an NES-CPU-11, from above | <https://commons.wikimedia.org/wiki/File:Frontalansicht_Mainboard_NES_NESE-001_HOF06378.png> | PantheraLeo1359531 | CC BY 4.0 | `e9f606535f4b01507a62f185ce38aa3403862bb82f434cc51f148a90088ea8cd` | The PAL console's parts and their markings, and a check that each sits on the scanned board's footprint of the same name |
| I4 | A photograph of a populated NES-CPU-07, from above | <https://commons.wikimedia.org/wiki/File:Nintendo-NES-Mk1-Motherboard-Top.jpg> | Evan-Amos | Public domain | `2158318ca6e7c913fce4220e8763dc8df4b37e70fea50cf29a1c975556a5b46c` | The NTSC console's parts and their markings, the chips' bodies, and a check that each sits on the scanned board's footprint of the same name |
| I5 | A photograph of the same NES-CPU-07's solder side | <https://commons.wikimedia.org/wiki/File:Nintendo-NES-Mk1-Motherboard-Bottom.jpg> | Evan-Amos | Public domain | `64d52d1dbedfd123a56780def11821ef4157694e4d88d701924d567e2cb1ae10` | A check, from below, that the NTSC console's parts sit on the scanned board's footprints |
| I2 | OpenTendo's redrawing of the front-loader's main board in KiCad, as this project's fork of it holds it | <https://github.com/dbhq-uk/OpenTendo/raw/3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009/Board%20Files/Motherboard.kicad_pcb> | Redherring32 and the OpenTendo contributors | TAPR Open Hardware License (the repository's README) | `9cce8323c9c18f0c583f99d1e07d7650f85858ca2599cbf52b2c75f384e8224a` | A cross-check only, from which nothing is drawn: the board's outline and the chips' places compared with the scan's, U9's jobs read from its nets, and which buffer serves which controller port checked against them |

## The BBC Micro board's sources, which are not committed

The BBC Micro's inside model is measured from these, and none of them is in the repository: the scans and 8BS's photograph state no licence, Chris Whytehead's photograph is read for markings only, and the Service Manual carries Acorn's copyright. Each is credited under the inside model (and D1 under the outside too), from the BBC Micro's `references` in `machines/registry.json`, which give the same address and SHA-256; `tools/bbc-micro-model/data/sources.json` has every input, and the tools check each against its SHA-256 before reading it.

| Id | What | Address | Author | Licence, as stated | SHA-256 of the original | Used for |
|---|---|---|---|---|---|---|
| I1 | A flatbed scan of a bare Issue 7 board, its component side, at 400 dpi, posted to the stardot forum (thread 23329) | <https://drive.google.com/file/d/1llcLHl_ax6rtZRWxdoSyA5jNjFOo_-3N/view> | amb5l | None stated | `5edc89d95872c9e70a1adb0b8d968fbccd3459816cacd64a630d95afd02e7b0e` | The board's outline, holes, pads and scale, the component side's copper and the print, traced to look at, and every chip's place |
| I2 | The same board's solder side, scanned the same way | <https://drive.google.com/file/d/1EIRg4Ump3T5ixNCJtVkrz7DwODJ__YFn/view> | amb5l | None stated | `c9a36223ca7d3d28cbceb6cb25f625e3989655283e6bf4b4602c5199612b5bb6` | The solder side's copper, traced to look at after the scan was turned over and fitted to the component side by its drilled holes |
| I5 | A photograph of a populated Issue 7 board | <https://8bs.com/see/BBC_B_iss7.jpg> | 8BS | None stated | `7ae4b57399e9576d46bcfe0d4e2b39ddb362d7bb33782c002a39adffab208263` | The chips' markings and the larger parts' boxes, read by eye, and two checks that failed: the copper against it, and the markings against the manual |
| I6 | A photograph of a second Issue 7 board, populated and in its case | <http://chrisacorns.computinghistory.org.uk/Pics/BBCB7LL.jpg> | Chris Whytehead, Chris's Acorns | CC BY-SA 3.0, as the site's home page states (an older paragraph there asks to be asked first) | `bab4176aa727598ab70717ecf921e54773e5c14401cdd16b9dba8ae02c320863` | The chips' markings only, recorded and not judged |
| D1 | Acorn's BBC Microcomputer Service Manual, 1985 | <https://archive.org/download/bbc-micro-service-manual/BBC-Micro-Service-Manual%20Sections-1-2-3.pdf> | Acorn Computers Limited | Acorn's copyright notice; facts read from it, nothing copied | `974550803fbce738b143ea0f1fdb6aa129a5c0a69c9e0072634d16f127a7fb99` | Every chip's part and role (table 8.1), and the case's size (section 3.11) |

## The BBC Micro's outside model's sources, which are not committed

The BBC Micro's outside model is measured from these too (task 8 of its models plan, 13 October 2026), and none of them is in the repository. `tools/bbc-micro-model/data/sources.json` has every input with its address, size and SHA-256, and the tools check each against its SHA-256 before reading it. Fetched on 13 October 2026. Since task 9b (13 October 2026) each is credited under the outside model, from the BBC Micro's `references` in `machines/registry.json`, which give the same address and SHA-256; the Service Manual, D1, is in the board's table below and is credited under both views.

| Id | What | Address | Author | Licence, as stated | SHA-256 of the original | Used for |
|---|---|---|---|---|---|---|
| K1 | Dominic Beesley's KiCad keyboard for the BBC Micro, as this project's fork of it holds it at commit `5ec53df566c77424caa6be827d0831da3916ecfa` | <https://github.com/dbhq-uk/bbc-keyboard/blob/5ec53df566c77424caa6be827d0831da3916ecfa/KiCad/bbc-keyboard/bbc-keyboard.kicad_pcb> | Dominic Beesley | MIT (its notice is in `NOTICE.md`) | `35b34ac3ef0a04fb311ee1971eb9fc32cb1c323877377c4d1a74986c709b0929` | Every key's centre and size |
| O8-I | The underside of an Issue 7 machine, 640 px wide | <http://chrisacorns.computinghistory.org.uk/Pics/BBCB7I.jpg> | Chris Whytehead, Chris's Acorns | CC BY-SA 3.0, as the site's home page states (an older paragraph there asks to be asked first) | `4f1a5dbd9a698aa7445dfbacd96e47aa2260f4ad723fdf55567c9df3a9fae3bd` | The words beside the five connectors under the front, in their order |
| O8-H | The rear of the same machine, 640 px wide | <http://chrisacorns.computinghistory.org.uk/Pics/BBCB7H.jpg> | Chris Whytehead, Chris's Acorns | As O8-I | `4ba8776121497fe59006e0e252c6789059810f28ea4f6574d81ea0c32dd34e1b` | Fetched, not used: looked at, nothing measured or read from it |
| P-TE_1757819 | TE Connectivity's drawing 1757819 of its Amplimite D-subminiature plugs | <https://www.farnell.com/datasheets/1917994.pdf> | TE Connectivity | None stated | `6c70fc080dafcc4dcfab667cf79beec7ce85c07fea8d78037726a1caa067a8b2` | One dimension: the 15-way D's mounting holes, 33.20 to 33.45 mm apart, the scale on the rear photograph |
