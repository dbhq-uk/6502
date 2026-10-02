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

## The model's controls, later the same day

Dan, 2 October 2026: "on the 3D model should be able to move and explore it
using standard controls". His check in headless Chrome, before anything here
changed: left-drag turns the model, the wheel zooms and right-drag pans, so the
library's defaults worked. Exploring it was still limited and awkward.

### What was wrong

- **The board could not be turned over.** `maxPolarAngle` was 0.48 of pi, so the
  camera stayed above it: no underside, no edges.
- **The range was narrow.** The model asked for a distance between 8 and 75, and
  panning had no limit at all, so the board could be dragged clean off the
  screen.
- **The wheel trapped the page.** camera-controls called `preventDefault` on
  every wheel event over the canvas, so a mouse wheel over the model, which is
  460 pixels tall in the middle of the page, stopped the page scrolling.
- **A finger trapped it too.** The canvas was `touch-action: none`, so on a
  phone a finger on the model could not scroll the page. Reading the
  library's source showed why the CSS rule had never been the whole story:
  `CameraControls` sets `touch-action: none` on its canvas itself, inline, when
  it connects, and an inline style beats the stylesheet.
- **Nothing said what the controls were.** The only hint was in the accessible
  name. There was no way to pan with the keyboard or to reset it with one.

### What was changed, and what it was chosen over

- **Full orbit.** `maxPolarAngle` is pi. Chosen over stopping a little short of
  the pole to dodge a gimbal flip: camera-controls keeps its own up vector, and
  the check drags to the stop and reads polar 3.142, no flip and no overshoot.
- **The underside is a real surface.** The board was one box with one material,
  lit from above. A box has six faces, so the fourth, the underside, now takes
  its own material (`--model-pcb-under`, `#37602f`, a shade lighter than the
  top). Each chip leg ends in a tinned pad on the underside, and the edge
  contacts are gold on both faces of the tab. Two lights were added: a
  hemisphere light whose ground colour falls on faces that look down, and a
  second directional fill from below, opposite the key. Chosen over lighting
  both faces with one lighter colour, which would have changed the top that was
  measured against the photograph, and over an unlit material, which would look
  flat. The check measures the view from below: 51.7% of the canvas drawn, mean
  brightness 65 of 255, against 39.6% and 59 from above.
- **Ranges.** The closest is 3 (was 8) and the farthest 150 (was 75). A node
  one-liner on the 36 degree lens gives a visible width at distance 3 of
  3.30 cm on the page's 778 by 460 canvas and 2.24 cm on a 390 by 340 phone
  canvas, against a 52 mm 40-pin package, so a chip, or a key, fills the view.
  At 150 on a square canvas it is 97.5 cm across, against a 27.3 cm board.
- **The target is bounded.** `setBoundary` holds the camera's target in a box:
  the board and its tabs plus 2 cm each way, and 2 cm above and below. Chosen
  over `boundaryEnclosesCamera`, which would bound the camera itself and stop
  the zoom-out, and over no bound. The check pans far and the target ends at
  x -12.985, the box's edge, and stays inside it.
- **The wheel is ours.** camera-controls' wheel is switched off and a wheel
  listener on the canvas zooms only while the model has focus, or with Ctrl or
  Cmd held; otherwise it does nothing and the page scrolls. It zooms **to the
  cursor**: camera and target are scaled about the point where the cursor's ray
  meets the board's plane, so that point stays put. The check puts the cursor on
  the 5 key, zooms, and the key is 0.4 pixels from the cursor. Chosen over the
  library's own `dollyToCursor`, because its Ctrl-and-wheel path calls `ZOOM`,
  which changes the camera's zoom and ignores the distance limits.
- **Focus is the switch.** The `tabindex="0"` section is what has focus. A
  mouse going down on the canvas focuses it, and so does a finger's tap. While
  it has focus the stage carries `data-active`, which draws the lime focus ring
  (a mouse click does not raise `:focus-visible`, so the global ring would not
  show) and hides the hint. The lime ring is a new named rule in the lime test in
  `tests/site.test.mjs`. It is an outline, not a fill.
- **Touch.** An unfocused canvas is `touch-action: pan-y pinch-zoom`, and
  camera-controls' three touch actions are `NONE`. A focused one is `none`, with
  one finger turning and two pinching and panning. The brief said `pan-y`;
  `pan-y` alone also switches off the browser's pinch-zoom of the page, so
  `pinch-zoom` stays. The tap focuses on pointer-up, not pointer-down, so a
  finger that starts a scroll stays a scroll. Escape, or focus leaving, restores
  it. The inline style is set by the script after the library sets its own.
- **Reset.** The button stays. Home resets, and so does a double click or double
  tap on empty space. Chosen over the `dblclick` event, which touch browsers do
  not reliably send: the stage times two taps itself (under 350 ms, under 30
  pixels, neither a drag). **Empty space is the black round the board**, as the
  brief asked, meaning a click that hits nothing in the scene. The cost: zoomed in
  until the board fills the view there is no empty space to double-click, and
  the button and Home are the way back.
- **Keyboard.** Arrows turn, Shift and arrows pan, plus and minus zoom, Home
  resets, Escape lets go. Shift and a left-drag pans too, for a mouse with no
  right button.
- **The words.** A hint on the canvas while the model has no focus, real text
  and not an image: "Click the model, then scroll to zoom", and for a coarse
  pointer "Tap the model, then drag to turn and pinch to zoom". A paragraph under
  it lists everything. The keys are also the canvas's `aria-description`. All of it
  is generated from one object, `CONTROLS` in `src/models/models.mjs`, so the
  visible text and the description cannot drift, and a test holds that. The accessible
  name is now short.
- **Colours.** One new token, `--model-pcb-under`, which is not a text colour.
  The two new text styles are in the contrast table: `.model-hint` (white on
  iron) and `.model-help` (`--moss-80` on black), both colours that were already
  in use. No shadow and no lime fill.
- **Unchanged, and still checked:** a click on a key presses the machine's key
  (a click is a pointer that moved under 5 pixels), and the model's bundle is not
  requested until the visitor reaches it.
- **Reduced motion.** No easing and no auto-rotation, as before. The zoom and
  the keys use the same `reduced` flag.

### Mistakes on the way

- **Held-down keys under-counted.** The plus and minus keys scaled by the
  camera's current distance, which lags while it eases. In a first exploratory
  run 30 presses of plus from the home view left the distance at 13.7, where
  the same 30 presses would reach the closest. The keys now step from where the
  camera is heading.
- **The first Ctrl and wheel was far too fast.** It used the trackpad gain
  for every Ctrl event, and one 300 pixel wheel went straight to the closest
  distance, 3.000. A mouse wheel and a trackpad pinch both arrive as Ctrl and the
  wheel, and the deltas tell them apart: a notch is 100 or so, a pinch event is a
  few. Under 40 gets the pinch gain. The same step now gives 24.973.
- **The first browser-check run was wrong in several places at once, none of
  them the model's fault.** The page has `scroll-behavior: smooth`, so a
  `scrollIntoView` was still moving when the check measured and aimed. The
  stage was then off screen, and the stage draws only while it is on screen, so
  the camera did not move and every reading looked like "no change". The check now
  scrolls instantly and waits for the scroll to stop.
- **Waiting for the camera was wrong twice.** The first wait compared two
  readings 150 ms apart and passed in the middle of an ease, because software
  WebGL draws a frame slowly. The page now reports where the camera is heading
  (exact) and whether it is still on its way there. The first full run with
  that took 6 minutes 18 seconds, because the "arrived" test was within 0.0001, and I
  think the ease needs about nine time constants to get there at the few frames a
  second that software WebGL manages. At 0.02 it takes 2 minutes 33 seconds. (Both from `time`.) The
  figures that matter, the angles, the distances, the targets and the bounds,
  are the exact end values either way.

### What is proven, and what is not

Proven in headless Chrome 153 with software WebGL: everything printed below.
Touch is **synthesised** (DevTools touch events in an emulated 390 by 780 phone
with a coarse pointer), not a real finger on a real phone. Only Chrome has been
run. The `aria-description` is in the markup and a test holds its text, but no
screen reader has read it.

### Tests

`npm test` in `site/`: 182, from 177 (five new tests in `model.test.mjs`: the
controls in real text and in the aria-description, the focus wiring, the
orbit, range and bounds, the lit underside, and the double-tap reset). The floor
in both workflows is raised to 182. One existing test needed a change: the lime
test now names the model's focus ring.

### The browser check

`node scripts/browser-check.mjs` on 2 October 2026, from the line after the
model's first check to the end. Software WebGL, other work running on the machine.
(The speed lines and the earlier steps are as in the section above.)

```
model bundle requested before scrolling to it: no
model: running in 541 ms after scrolling to it, fetched /models/kim-1.js; status "The model is running. Its digits show the machine's display."
model canvas: 778x460, 39.1% of its pixels are not black
digits match: page segments 63,91,6,91,79,57 (0212 3C), model segments 63,91,6,91,79,57 ("0212 3C")
page key 1 clicked: model key down "1", presses 0 then 1
model key 2 clicked at 801,559: model key down "2"; the machine showed "2121 02" after the page's 1 and "1212 12" after the model's 2
controls: start view azimuth 0.000, polar 0.716, distance 35.795, target 0.000,-1.000,1.500; bounds -12.985,-2.000,-15.640,11.985,2.000,15.640; focused false
hint on the model while unfocused: "Click the model, then scroll to zoom", shown true; touch-action pan-y pinch-zoom
wheel over the unfocused model: scrollY 2695 to 2995; distance 35.795 to 35.795
ctrl and the wheel over the unfocused model: distance 35.795 to 24.973; scrollY 2695 to 2695
after a click on empty space: focused true, ring {"active":true,"outline":"solid","width":"2px"}, touch-action none
wheel over the focused model, cursor on the 5 key at 765,531: distance 35.795 to 24.973; the key is now at 765,531, 0.4 px from the cursor; scrollY 2695 to 2695
Escape: focused false, ring {"active":false,"outline":"none","width":"3px"}, touch-action pan-y pinch-zoom
wheel after Escape: scrollY 2695 to 2995
left-drag 233 px left and 46 px down: azimuth 0.000 to 3.196, polar 0.716 to 0.088; target unchanged true
right-drag: target 0.000,-1.000,1.500 to 11.826,2.000,-2.034; azimuth unchanged true, distance unchanged true
shift and left-drag: target 0.000,-1.000,1.500 to 11.837,2.000,-2.010; azimuth unchanged true
nine right-drags, far and back and forth: target -12.985,2.000,1.308 within -12.985,-2.000,-15.640 to 11.985,2.000,15.640: true; it reached an edge: true
under the board: polar 0.716 to 3.142 (pi/2 is 1.571, pi is 3.142); 51.7% of the canvas is not black, mean brightness 65 of 255 (from above: 39.6%, 59)
dragged on to the stop: polar 3.142, never past pi (3.142)
the wheel to its limits: closest 3.000 (78.3% of the canvas drawn), farthest 150.000 (2.9% drawn)
double click on empty space: distance 150.000 to 35.795, polar 0.716, back at the start view: true
keyboard: ArrowLeft azimuth 0.000 to -0.262; ArrowDown polar 0.716 to 0.978; Shift+ArrowLeft target x 0.000 to -2.864; Shift+ArrowUp target -2.864,-1.000,1.482 to -2.864,0.880,-0.678; + distance 35.795 to 30.425; then - twice to 40.238; Home back at the start: true
reset button: back at the start view: true
phone, unfocused: touch-action "pan-y pinch-zoom", hint "Tap the model, then drag to turn and pinch to zoom", shown true
phone, one finger swiped up on the unfocused model: scrollY 3241 to 3475; azimuth 0.000 to 0.000, polar 0.716 to 0.716
phone, after a tap on the model: focused true, touch-action "none"
phone, one finger dragged on the focused model: azimuth 0.000 to 2.199, polar 0.716 to 0.000; scrollY 3241 to 3241
phone, two fingers spread on the focused model: distance 35.795 to 21.091; scrollY 3241 to 3241
phone, after it loses focus: touch-action "pan-y pinch-zoom"
no console errors, no failed requests, no CSP violations

real	2m33.292s
user	0m10.937s
sys	0m3.245s
exit 0
```
