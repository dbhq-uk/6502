# Photographs

A photograph of each machine's original device, shown on the machine's page.
Every machine page has one, always, and every one is credited on the page
(issue 28; Dan, 2 October 2026: rights are not a gate, the credit is). The
credit is built from the machine's `photo` entry in `machines/registry.json`,
so the registry, this file and the page say the same thing. A test fails the
build when a running machine has no photograph, or one without its author and
source.

**These are photographs, not illustrations.** A generated image never stands
in for one: generated images live in `../imagery/`, are captioned
"Illustration" and are never evidence. Nothing in this folder is generated.

**These files are not MIT.** Each keeps the licence its source gives it, named
below and on the page. A file here is a resized copy of the source, which is a
derivative under that licence.

## kim-1.webp

| | |
|---|---|
| What | An original MOS Technology KIM-1, on display at the Musée Bolo, EPFL, Lausanne, photographed from above on black |
| Source | Wikimedia Commons, [`File:MOS_KIM-1_IMG_4211_cropped.jpg`](https://commons.wikimedia.org/wiki/File:MOS_KIM-1_IMG_4211_cropped.jpg) |
| Author | Rama & Musée Bolo (the photograph, `File:MOS_KIM-1_IMG_4211.jpg`); cropped by Tomer T |
| Licence | CC BY-SA 2.0 fr, as the file page states it ([deed](https://creativecommons.org/licenses/by-sa/2.0/fr/deed.en)). The photographer offers every image under CeCILL as well, and asks for a credit line naming Rama and the licence |
| Taken | 24 August 2010, 19:08 (the original's Exif, as its file page gives it). The cropped file's page gives 9 March 2012, which is when the crop was made |
| Fetched | 2 October 2026, from `upload.wikimedia.org/wikipedia/commons/4/4f/MOS_KIM-1_IMG_4211_cropped.jpg`: 3792 by 4675 pixels, 2,687,798 bytes, SHA-1 `85523be8e5e213285190a686462c9b9754d90265` (the SHA-1 Commons records for the file), SHA-256 `70aa1194749165ea9dc76718ee6ae607b47355c5d8f6c2373a43fc1d2887f7b9` |
| This copy | `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1973 pixels, 342,232 bytes, SHA-256 `1e418a3d2d0281179456f1f833dceb2effb66a0f5c25364aef7cbb06a354c5a4`. The original is not committed, being over 1 MB; the line above is enough to fetch it again and check it |

The site builds AVIF and WebP copies at three widths from this file at build
time (`astro:assets`), so the page never loads the 1600 pixel master.

Why this one: of the KIM-1 photographs on Commons that show a whole board from
above, the others are a later Commodore-badged board taken by phone at a slight
angle on a pale ground (`File:Commodore KIM 1.jpg`, CC0), a Commodore board
upside down in its packing box, and this shot uncropped. This one is the
original MOS board, square on, sharp, and on black, which is the site's own
canvas. The 3D model on the same page is measured from it. The journal for
2 October 2026 has the full comparison.
