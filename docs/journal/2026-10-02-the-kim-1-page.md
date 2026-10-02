---
title: "A photograph and a model of the KIM-1"
date: 2026-10-02
summary: "The KIM-1's page gets a credited photograph of an original board and a 3D model of it, measured from that photograph, whose six red digits show what the running machine shows and whose keys press the machine's own. The model loads only when a visitor reaches it."
order: 9
---

# 2 October 2026: a photograph and a model of the KIM-1

Issue 28 asks for two things on every machine page: a photograph of the
original computer, always, credited on the page, and a 3D model of it. Dan,
2 October 2026: rights are not a gate; use the best photograph and credit it.
The KIM-1 is first, and the BBC Micro is not to get either until Dan has signed
the KIM-1's page off.

## What was built

- **The photograph**, in the head of `/machines/kim-1/` beside the machine's
  details: `site/src/assets/photos/kim-1.webp`, shown by
  `site/src/components/MachinePhoto.astro` with a caption and a credit built
  from a new `photo` entry in the machine's registry record.
- **The registry's `photo` field**: `file`, `author`, `sourceUrl`, `licence`
  (as the source states it, or `null` for none, which the page shows as "no
  licence stated"), `date` taken, `alt`, and optionally `licenceUrl` and
  `caption`. `validateRegistry` now fails a running machine with no
  photograph, and any photograph without its author, an https source, a
  well-formed date, real alt text or a file on disk, or whose alt text calls it
  an illustration.
- **The 3D model**, below the program on the same page: `site/src/models/kim-1.js`
  draws the board, on a stage every model shares, `site/src/models/stage.mjs`.
  The board's layout, measured from the photograph, is
  `site/src/models/kim-1-layout.mjs`.
- **Lazy loading**: `site/public/model-loader.js`, 1,679 bytes, is the only new
  script the page names. It shows the hidden model section, and imports the
  model's bundle only when the section comes within 600 pixels of the screen,
  or when the visitor presses "Load the 3D model".
- **The machine's state, shared**: `public/kim-1.js` puts `panel.kim1` on the
  panel once the machine is running, with `tap(key)` and `segments(digit)`,
  and announces every tap as a `kim1:key` event. The model reads its digits
  from `segments`, which is what the drawn display last drew, and its keys go
  down on `kim1:key`, whichever way the key was pressed.
- **For the next machine**: `site/src/models/models.mjs` is the map of models
  by machine id, and says in its header how to add one. The page template
  shows the photograph for every running machine and the model section for
  any machine in that map. The board itself is the KIM-1's own code and is not
  generalised.
- **Two scripts**: `site/scripts/measure-kim1-photo.mjs` measures the board's
  scale and size off the full-size photograph, and
  `site/scripts/page-weight.mjs` says what a page loads when it opens and what
  it loads later.
- **Tests**: 19 more, in `registry.test.mjs`, `machine-page.test.mjs`,
  `site.test.mjs` and a new `model.test.mjs`; the floor in both workflows is
  raised to match. `browser-check.mjs` now checks the photograph and the model
  in a real browser too.

## The photograph

**Where it came from.** Wikimedia Commons,
`https://commons.wikimedia.org/wiki/File:MOS_KIM-1_IMG_4211_cropped.jpg`. The
author, as the file page gives it: Rama & Musée Bolo (the photograph,
`File:MOS_KIM-1_IMG_4211.jpg`), cropped by Tomer T. The licence, as the file
page states it: CC BY-SA 2.0 fr; the photographer offers every image under
CeCILL as well, and asks for a credit line naming Rama and the licence, which
the page's credit does. Taken 24 August 2010 at 19:08, by the original's Exif;
the cropped file's page gives 9 March 2012, which is the date of the crop, so
the registry has the date the photograph was taken.

**Fetched** on 2 October 2026 at 08:25 UTC from
`upload.wikimedia.org/wikipedia/commons/4/4f/MOS_KIM-1_IMG_4211_cropped.jpg`:
3792 by 4675 pixels and 2,687,798 bytes, and `sha1sum` gave
`85523be8e5e213285190a686462c9b9754d90265`, the SHA-1 Commons records for the
file. Its SHA-256 is in `site/src/assets/photos/README.md`.

**Chosen over** the other KIM-1 photographs on Commons, found with the Commons
API (a search for `KIM-1` in the file namespace) and looked at as thumbnails.
Three others show a whole board from above: this one's uncropped original
(`IMG_4211`, the same shot with more black round it);
`File:Commodore KIM 1.jpg` (CC0), a later Commodore-badged board, taken by phone
at a slight angle on a pale ground; and
`File:The legendary KIM-1 from Commodore (9371208824).jpg` (CC BY-SA 2.0), a
Commodore board upside down in its packing box. Rama's other shots of the same
board that were looked at (`IMG_4206`, `IMG_4209`) are at an angle, and
`File:KIM-1 single board computer.jpg` is in a museum case behind glass, with a
visitor reflected in it. This one is the original MOS board, square on, sharp,
and on black, which is the site's own canvas, and the crop leaves the board
filling the frame.

**Processed** with `cwebp -q 82 -resize 1600 0 -metadata none`: 1600 by 1973
pixels and 342,232 bytes, committed. The original is over 1 MB and is not
committed; its URL and hash are enough to fetch it again. The site builds AVIF
at 360, 720 and 1080 pixels wide and WebP at the same widths from the master,
and the img's own fallback is the 720 WebP. The img carries width and height,
so nothing moves when it arrives.

**It is a photograph, not an illustration.** The existing rule, that a test
fails if a generated image in `src/assets/imagery/` is shown without an
"Illustration" caption, is unchanged and still bites: the photographs are in a
separate folder, the two folders may share no file, and a new test fails if a
photograph is captioned or alt-texted as an illustration, or shown without
"Photograph:", its author, its source and its licence.

## Measuring the board

`node scripts/measure-kim1-photo.mjs` on the full-size photograph, on
2 October 2026, printed:

```
image 3792 x 4675 pixels
board body: x 441 to 3471, y 248 to 4387: 3030 x 4139 pixels; the tabs reach x 290
tabs at x 320: y 417 to 1792, y 2831 to 4210
contacts: 22 and 22; pitch 59.98 and 60.26 pixels
scale 15.17 pixels to the millimetre, from 0.156 inch contacts
board 199.7 x 272.8 mm; tabs stand 10.0 mm proud, from y 11.1 to 101.8 and 170.2 to 261.1 mm; first contacts at y 14.9 and 174.0 mm
```

So the board is 27.3 by 20.0 cm, close to the 28 by 21 that the brief guessed.
The scale rests on one assumption, that the contacts are at the 0.156 inch
pitch of the KIM-1's 44-way connectors, and two things check it: on that scale,
read off the grid described below, the three 40-pin packages measure 50.8 to
52.1 mm long, against the 52 mm of a
40-pin package, and the keys sit 12.5 to 13.0 mm apart, against half an inch.

The chips, the memory column, the display, the keypad, its keys and the crystal
were read off a grid laid over a reduced copy of the photograph, to about a
millimetre, and converted to millimetres on the same scale. The smaller logic
chips were read more roughly. Heights are not in a photograph taken from
above, so they are typical ones (a 1.6 mm board, a 40-pin package about 4 mm
high), and the caption says so.

## The model

Our own, built from boxes in three.js: no downloaded model file, so there is no
model licence to record. The green board and its two tabs with 22 gold
contacts each, four mounting holes, the 6502 and the two 6530s as black 40-pin
packages where the photograph has them (the 6502 at the top, then the 6530s,
U3 then U2), the eight 6102 memory chips, ten smaller logic chips, the crystal,
the KIM-1 name, the display's six digits of seven segments, and the keypad with
its 23 keys and the SST switch.

- **The digits are red** and light from the machine: each frame the model
  reads `segments(d)` for the six digits, the same values the page's drawn
  display shows, and swaps each segment between a lit and an unlit material.
  It decodes them with the machine's own table (a test compares it with
  `Kim1Display.Decode`).
- **A click on a model key presses that key on the machine** through
  `panel.kim1.tap`, the same function a click on the page's keypad calls. A
  click on the SST switch clicks the page's SST checkbox. A click is told from a
  drag by the pointer moving less than 5 pixels.
- **A key pressed anywhere goes down on the model**, for 140 ms, because every
  tap is announced.
- **Labelled a model twice**: a corner tag, "Model, not a photograph", and the
  caption, which is also the text alternative and is generated from the layout,
  so its sizes and counts cannot drift from the model.
- **Keyboard**: with the model focused, the arrow keys turn it by 15 degrees
  and plus and minus zoom; "Reset the view" returns it to the start.
- **Motion**: it never turns by itself. With reduced motion, the camera and the
  keys move at once instead of easing.
- **Colours**: five new tokens, used by the model's scene only:
  `--model-pcb`, `--model-gold`, `--model-tin`, `--model-led` and
  `--model-led-off`. None is a text colour. The two text colours the model adds,
  the corner tag and the status line, are in the contrast table.
- **The CSP is unchanged.** The model is a same-origin module imported by a
  same-origin module, and WebGL needs nothing in the policy.

## Decisions

- **The photograph in the page's head, the model below the program.** Chosen
  over a "The board" section holding both. A visitor sees the real machine first,
  next to its name, and the program stays next to the keypad it is typed into.
  The cost: on a phone the photograph comes between the details and the
  machine. It is held to 360 pixels wide there.
- **A required photograph, an optional model.** The registry check fails a
  running machine with no photograph, because the issue says always. A model
  is per machine, in the `MODELS` map, and the page shows the section only for
  a machine in it. Chosen over requiring both, because a model is a piece of
  work in its own right and the BBC Micro could run before its model exists.
- **One bundle per model, built like the chip page's, and not shared with it.**
  `scripts/build-models.mjs` bundles each model with three.js into
  `public/models/<id>.js`. Chosen over code splitting with `/inside/`'s bundle:
  that would change the chip page's build for no gain on this page.
- **three.js imported by name**, with camera-controls given only the nine
  classes it needs, so the bundler drops the rest. The chip page imports the
  whole namespace. No post-processing either: no bloom on the LEDs.
- **The budget, set before building**: the model must not cost more than the
  chip page's bundle (`public/chip.js` was 853,892 bytes, 216,790 gzipped by
  `gzip -c | wc -c` that morning), so 200,000 bytes gzipped, held by a test;
  and none of it may be in the page's first load.
- **The red is a token, not the site's green.** The page's own display stays
  phosphor green, because it is a drawing of what the machine shows. The model
  is a model of a thing, and that thing has red LEDs.
- **Picture with a WebP fallback at 720 wide.** The first build used Astro's
  default fallback, PNG, at four widths up to the full 1600, and wrote a
  4,188 kB PNG. Setting `fallbackFormat` to WebP and `width` to 720 stopped
  that.

## Page weight

`node scripts/page-weight.mjs /machines/kim-1/` counts the HTML, the scripts the
page names and what they import, and the src of each img. On a build of `main`
at `6f74c3f`:

```
/machines/kim-1/ when it opens: file, bytes, gzipped
  /machines/kim-1/ (html)	35058	8380
  /analytics.js	6510	2983
  /consent.js	2743	1227
  /machines-table.js	2416	1021
  /kim-1.js	6279	2615
  /table-sort.js	766	449
  total	53772	16675
```

On this branch:

```
/machines/kim-1/ when it opens: file, bytes, gzipped
  /machines/kim-1/ (html)	39577	9805
  /analytics.js	6510	2983
  /consent.js	2743	1227
  /machines-table.js	2416	1021
  /kim-1.js	7013	2906
  /model-loader.js	1679	807
  /table-sort.js	766	449
  /_astro/kim-1.ufzgBkwp_ZO889l.webp	87100	87072
  total	147804	106270
loaded later, when the visitor reaches the model:
  /models/kim-1.js	604092	153130
  total	604092	153130
```

So the page's own files grew by 6,932 bytes (4,519 of HTML, 734 in `kim-1.js`
and the 1,679-byte loader), plus the photograph. The script counts the img's
fallback, an upper bound: the browser check's Chrome, at a 300-pixel slot on a
1x screen, took the 360-pixel AVIF, 14,878 bytes; at 2x it would take the
720-pixel one, 41,424 bytes. The model's bundle, 604,092 bytes and 153,130
gzipped, is loaded only when the visitor reaches it, and the browser check
confirms nothing under `/models/` was fetched before then.

## The browser check

`node scripts/browser-check.mjs` on this machine, on 2 October 2026, against
the built site served with the site's own headers. Chrome runs with SwiftShader,
its software WebGL, because a CI runner has no GPU and current Chrome no longer
falls back to it without `--enable-unsafe-swiftshader`. The speed lines are low
because other work was running on the machine at the time.

```
browser 153.0.8010.47
loaded in 5131 ms: Running. Press RS to start the monitor.
step 1 ok   keys "[RS]" expected "xxxx xx" shown "0000 00" announced "Display 0000 00" (1073 ms)
step 2 ok   keys "[AD] 17FA [DA] 00 [+] 1C" expected "17FB 1C" shown "17FB 1C" announced "Display 17FB 1C" (4507 ms)
step 3 ok   keys "[AD] 0200 [DA] AD [+] 10 [+] 02 [+] 18 [+] 6D [+] 11 [+] 02 [+] 8D [+] 12 [+] 02 [+] 4C [+] 0A [+] 02" expected "020C 02" shown "020C 02" announced "Display 020C 02" (12503 ms)
step 4 ok   keys "[AD] 0210 [DA] 27 [+] 15" expected "0211 15" shown "0211 15" announced "Display 0211 15" (7107 ms)
step 5 ok   keys "[AD] 0200 [GO]" expected "       " shown "       " announced "Display dark" (3689 ms)
step 6 ok   keys "[ST]" expected "020A 4C" shown "020A 4C" announced "Display 020A 4C" (982 ms)
step 7 ok   keys "[AD] 0212" expected "0212 3C" shown "0212 3C" announced "Display 0212 3C" (2993 ms)
status line: "Running. Press RS to start the monitor.", then "Running. Type the program below, or your own."
speed, once a second for 5 s: running 0.81 MHz, capacity 3.65 MHz; running 0.90 MHz, capacity 3.46 MHz; running 1.00 MHz, capacity 9.09 MHz; running 1.00 MHz, capacity 14.71 MHz; running 0.97 MHz, capacity 5.12 MHz
page speed line: Running at the board's own 1 MHz. This browser could run it about 5 times as fast.
photograph: loaded /_astro/kim-1.ufzgBkwp_1lGWCK.avif, 300x370 pixels, shown at 298x368
model bundle requested before scrolling to it: no
model: running in 3030 ms after scrolling to it, fetched /models/kim-1.js; status "The model is running. Its digits show the machine's display."
model canvas: 778x460, 39.4% of its pixels are not black
digits match: page segments 63,91,6,91,79,57 (0212 3C), model segments 63,91,6,91,79,57 ("0212 3C")
page key 1 clicked: model key down "1", presses 0 then 1
model key 2 clicked at 801,559: model key down "2"; the machine showed "2121 00" after the page's 1 and "1212 12" after the model's 2
no console errors, no failed requests, no CSP violations
```

The last line is the proof that a model key presses the machine's key: the
monitor was in address mode, so the page's 1 shifted the address 0212 to
2121, and the model's 2 shifted it to 1212.

Separately, with `reducedMotion: 'reduce'` the model loaded from the button,
turned at once on an arrow key and reset, with no errors; and with JavaScript
off the model section stayed hidden while the photograph and the keypad
showed.

## Mistakes

- **The first key check was a false pass.** It waited for the machine's
  display to show a 2 at the end of the address or the data, and the data was
  already 02, so it passed at once, before the click had done anything. The
  display is read only after it has held still for 400 ms, so a check has to
  wait for it to change first. Both key checks now do.
- **Two of the existing tests had to change, and both for a reason.** The
  machine page test that no `<img>` on the KIM-1's page mentions the KIM-1 was
  there to keep the machine drawn rather than pictured; it now looks inside
  the panel only, because the page has a photograph by design. And two
  made-up running machines in the registry and machines tests now carry a
  photograph, because a running machine without one is now invalid.
- **The repository's README said no ROM was committed.** That stopped being
  true when the ROMs moved into `roms/`. Its licence section now names both
  kinds of file here that are not MIT: the ROMs and the photographs.
