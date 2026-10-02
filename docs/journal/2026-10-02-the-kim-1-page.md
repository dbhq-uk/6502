---
title: "A photograph and a model of the KIM-1"
date: 2026-10-02
summary: "The KIM-1's page gets a credited photograph of an original board and a 3D model of it, measured from that photograph, whose six red digits show what the running machine shows and whose keys press the machine's own. The model loads only when a visitor reaches it. Later the same day its board gets the real copper tracks, traced from the photograph."
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

## The copper tracks, later the same day

Dan, 2 October 2026: "on the 3d model i would like to see the circuit board
tracks". The board was a flat green slab with the parts on it. It now carries
the real board's copper tracks, traced from the photograph on the same page,
not invented.

### What was built

- **A track map**, `site/src/assets/tracks/kim-1.webp`: greyscale, white where
  there is copper, 1536 by 2048 pixels, lossless WebP, 293,952 bytes, covering
  the board's body edge to edge. Committed, so the build never makes it.
- **The script that made it**, `site/scripts/make-board-tracks.mjs`, with its
  method and thresholds in its header. `site/README.md` says how to run it, and
  `site/src/assets/photos/README.md` says where the map came from and under
  which licence.
- **The top face**: the model loads the map as a same-origin image and turns it
  into three maps for a `MeshStandardMaterial`: colour (the mask's
  `--model-pcb` to a new token, `--model-copper`), surface (roughness 0.55 and
  metalness 0.1 on the mask, the plain board's own values, and 0.35 and 0.25 on
  the copper) and relief (the map blurred by 1.5 pixels as a bump map, scale
  1.5). Nothing on the face is emissive.
- **Two buttons**, Show tracks and Show tracks only, beside Reset the view.
- **The credit**, under the model's caption: "Tracks traced from the photograph
  above, by Rama & Musée Bolo, cropped by Tomer T, and shared under its licence:
  CC BY-SA 2.0 fr", with the licence linked, built from the photograph's own
  registry entry.
- **Tests**: six more in `site/tests/model.test.mjs` and one changed; the
  browser check drives the buttons. The floor in both workflows is 188.

### The photograph it was traced from, and the mapping

**The full-size source, not the committed copy.** The first passes ran on
`site/src/assets/photos/kim-1.webp`, the 1600 pixel copy in the repository, at
6.4 pixels to the millimetre. The bus of tracks down the left of the board has
tracks 1.27 mm apart with gaps of about 0.3 mm between them, two pixels at that
size, and WebP had blurred them together. The full-size source, at 15.17 pixels
to the millimetre, separates them cleanly. It was fetched again on 2 October
2026 at 12:36 UTC from the same Commons URL as in the morning, and `sha256sum`
gave `70aa1194749165ea9dc76718ee6ae607b47355c5d8f6c2373a43fc1d2887f7b9`, the
hash in `site/src/assets/photos/README.md`. The script refuses a full-size file
with any other hash, and still accepts the committed copy, at its lower
resolution. Chosen over tracing the committed copy only because the brief asked
for the photograph in the repository: the full-size file is the same
photograph, and `measure-kim1-photo.mjs` already works from it.

**The mapping is the one the layout already had.** The morning's measurement
found the board's body at x 441 to 3471 and y 248 to 4387 on the full-size
source. That box is now `PHOTO.body` in `site/src/models/kim-1-layout.mjs`,
with the source's size and hash, and the script cuts exactly that box out and
scales it to the map. A test holds that the box is `BOARD` at `PX_PER_MM` to
within a pixel.

**The residual perspective is under two pixels on the committed copy**, about
0.3 mm, so the board is cut as a box, not warped. Checked by fitting a line to
each edge of the board on the committed 1600 pixel copy (the edge being the
first run of five pixels whose channels sum to over 90):

```
top     y = -0.0000672 x + 104.87   (21 points, largest residual 0.85 px)
bottom  y =  0.00101 x + 1849.82    (21 points, 0.63 px)
right   x =  0.000907 y + 1462.79   (31 points, 1.02 px)
left    x =  0.00390 y + 182.13     (21 points between the tabs, 0.68 px)
```

Across the board that is a drift of 0.08 pixels along the top, 1.2 along the
bottom and 1.5 down the right edge; the left edge was fitted only over the
400 pixels between the two tabs, so its slope is the least certain. The
morning's box, scaled to the copy, is x 186.1 to 1464.6 and y 104.6 to 1851.1,
against edges measured at 185.7 to 1464.4 and 104.8 to 1850.5.

### How the tracks were told from the mask

The tracks on this board are tinned copper under a green solder mask. In the
photograph they are a lighter, yellower green than the mask round them, and
their centres carry a near-white highlight where the rounded tin catches the
light. The script works in OKLab, a perceptual colour space (L lightness, a
green to red, b blue to yellow), at the photograph's own resolution, then
shrinks the result to the map with a smooth kernel so the edges are soft.

Values read off the full-size photograph with a probe across the bus, 130 mm
down the board, 8 to 14 mm in: the mask between tracks L 0.24 to 0.29, b 0.03
to 0.05; a track's flanks L 0.45 to 0.67, b 0.06 to 0.12, hue 101 to 119
degrees, saturation (chroma over L) 0.12 to 0.20; its highlight L up to 0.97,
saturation 0.11 to 0.15. And off the parts, on a resampled copy of the
committed photograph: the cream capacitors at hue 85 to 89, the resistors and
other brown and grey parts 53 to 68, the ceramic chips' gold about 74, the
orange capacitor 35. The silkscreen, probed through the letters of
"EC-715" in the glare at the top right, has saturation 0.05 to 0.09, up to 0.14
at its edges.

The rules, all in `THRESHOLDS` in the script:

- **The mask's own colour round each pixel** is an opening (a minimum, then a
  maximum, over a square 0.8 mm each side of the pixel, wider than a track,
  which removes the tracks) blurred over 1.6 mm. A track is measured against it,
  so the glare across the top right does not read as copper.
- **Strong copper**: b at least 0.02 and L at least 0.07 above the local mask,
  saturation over 0.13, and a hue from 97 to 135 degrees with chroma over 0.03.
- **Weak copper**: b 0.012 and L 0.05 above the mask, saturation over 0.09, hue
  92 to 140. A weak region is kept only if it holds a strong pixel. This is
  what keeps the highlights along the middle of a track.
- **Pours**: b over 0.062 and L over 0.42 outright, kept only where that colour
  covers a patch wider than 1.2 mm. The copper beside the notch between the
  tabs is wider than the opening, so it is found this way.
- **Pads**: silver (chroma under 0.05), L over 0.42 and 0.12 above the mask,
  0.5 to 6 square mm, 0.6 to 3.2 mm a side, no more than 1.8 to 1 long, at least
  0.45 of their box filled, within 0.4 mm of a track. 289 were found.
- **Clean up**: a one-pixel closing; then everything inside the bodies of the
  parts the model draws (the three 40-pin chips, the eight memory chips, the ten
  logic chips, the crystal, the display and the keypad, from the layout) and
  inside ten boxes listed by hand in `HIDDEN` (two cream capacitors whose
  highlights passed, the "Rev. B" and "EC-715" silkscreen, four U-number
  labels, the U18 to U23 labels over the display, and the strip down the right
  edge with the owner's label and the glare) is set to mask; then regions under
  0.35 square mm are dropped, holes under 1 square mm are filled, and 0.8 mm
  round the edge is cleared.

**Before cleaning**, the candidates (the weak mask) held all the tracks and
pours, and also the edges of the silkscreen labels, the highlights on the cream
capacitors, the glare and fibreglass weave along the right edge, specks of
JPEG noise, and the legs of resistors. **After**, it is the copper alone.
The final run, `node scripts/make-board-tracks.mjs <the full-size photograph>`
on 2 October 2026:

```
photograph 3792 x 4675, SHA-256 70aa1194749165ea9dc76718ee6ae607b47355c5d8f6c2373a43fc1d2887f7b9
board body x 441 to 3471, y 248 to 4387: 3030 x 4139 pixels, 15.17 pixels to the millimetre
pixels: strong 1358749, weak 1841443, kept by hysteresis 1775279; pads 289; cleared 3483857 pixels; specks dropped 189; holes filled 406
copper covers 15.1% of the board's top face
wrote src/assets/tracks/kim-1.webp: 1536 x 2048, 293952 bytes
```

It took 3 minutes 20 seconds (`time`), on a machine with other work running.
Run again from the committed script after the last comment edits, the map
came out byte for byte the same: `sha256sum` gave
`bfec20d30c433f4b54d817f5cec2d2b0138fae5682e82f6a1415880141180e54` before and
after.

**Under the model's own parts the map is mask**, because the part hides the
board there anyway, and the bodies are cleared by the layout's own sizes. The
pin rows either side of each chip are not cleared, so the pads under the pins
and the tracks that run to them stay, and show when the parts are hidden.

### Decisions

- **Traced, not drawn.** Every track on the face is where the photograph has
  one. Chosen over drawing plausible tracks, which would be the same mistake
  the site already refuses for the 6502's die.
- **A greyscale map, coloured in the browser from tokens.** Chosen over
  committing a coloured texture: the test that the model holds no colour of its
  own still holds, the colours stay in `tokens.css`, and one map gives the
  colour, the shine and the relief.
- **Colour, shine and relief, no normal map.** A bump map from the same map, so
  there is one file. Chosen over a normal map, which would be a second file of
  about the same size for a relief this shallow.
- **The copper's colour.** The first token, `#9a8f45`, rendered as an olive
  green, about (72, 88, 24) in the screenshot. I think that is the scene's mint
  fill light, and metalness 0.45 with no environment to reflect, which leaves a
  metallic surface dark and tinted by the light. `#b4874a` with metalness 0.25 renders about
  (120, 104, 40): warm, lighter than the mask and distinct from the gold
  contacts at (136, 120, 40).
- **The underside is left plain.** The photograph shows only the component
  side, so nothing is known of the solder side's tracks, and none are drawn.
  Chosen over printing "underside: not photographed" on the underside: a label
  printed on the model would read as silkscreen on the board, which the real
  board does not carry. The caption says it instead: the tracks are on the top
  face, and the underside, which the photograph does not show, is left plain.
- **Tracks on to start with,** as Dan asked to see them. Show tracks takes them
  off and the board is exactly as it was this morning, because the mask's
  shine is the plain board's. Show tracks only fades the parts out over 0.3
  seconds (at once with reduced motion) and leaves the board, its contacts, its
  holes and its pads underneath. Turning the tracks off while the parts are
  hidden brings the parts back, and Show tracks only turns the tracks on,
  because an empty plain board is not a view of anything.
- **Toggle buttons, not a radio group.** Two `aria-pressed` buttons in a group
  named Tracks, as the brief asked, chosen over three radio buttons (board,
  tracks, tracks only): the two states are independent enough to read as
  switches, and a radio group would hide which one is the default.
- **The pressed state is a shape as well as a colour**: a filled square before
  the label, an empty one when not pressed, and `--white` on `--veil`, the pair
  a hovered button already uses, which is in the contrast table.
- **A hidden part cannot be clicked.** three.js's raycaster hits hidden objects
  too, so the shared stage now takes the first hit whose object and every parent
  are shown. A click where a key was, with the parts hidden, presses nothing.
- **The map is served beside the bundle.** `scripts/build-models.mjs` copies it
  to `public/models/kim-1-tracks.webp` at every build, and the model fetches it
  when it mounts, so the page's first load does not change. Chosen over
  bundling it as a hashed asset: the name is stable for the deploy's serving
  check, which now lists it.
- **The budget**: 400 KB for the map, held by a test, set before the final run.

### Page weight

`node scripts/page-weight.mjs /machines/kim-1/`, now also counting the map the
page names in `data-model-texture`. On a build of `main` at `75bca28`:

```
/machines/kim-1/ when it opens: file, bytes, gzipped
  /machines/kim-1/ (html)	41172	10177
  /analytics.js	6510	2983
  /consent.js	2743	1227
  /machines-table.js	2416	1021
  /kim-1.js	7013	2906
  /model-loader.js	1679	807
  /table-sort.js	766	449
  /_astro/kim-1.ufzgBkwp_ZO889l.webp	87100	87072
  total	149399	106642
loaded later, when the visitor reaches the model:
  /models/kim-1.js	607298	154249
  total	607298	154249
```

On this branch:

```
/machines/kim-1/ when it opens: file, bytes, gzipped
  /machines/kim-1/ (html)	42467	10456
  /analytics.js	6510	2983
  /consent.js	2743	1227
  /machines-table.js	2416	1021
  /kim-1.js	7013	2906
  /model-loader.js	1679	807
  /table-sort.js	766	449
  /_astro/kim-1.ufzgBkwp_ZO889l.webp	87100	87072
  total	150694	106921
loaded later, when the visitor reaches the model:
  /models/kim-1.js	610415	155521
  /models/kim-1-tracks.webp	293952	293996
  total	904367	449517
```

So the page's first load grew by 1,295 bytes of HTML (279 gzipped: the two
buttons, the credit and the longer caption). What the model loads grew by
297,069 bytes: 3,117 in the bundle and the 293,952-byte map, which WebP has
already compressed, so gzip does nothing for it. The browser check confirms the
map is fetched with the bundle and not before.

### The browser check

`node scripts/browser-check.mjs` on 2 October 2026, software WebGL, other work
running on the machine. From the model's first line to the end (the program
steps and the speed lines are as in the runs above):

```
model bundle requested before scrolling to it: no
model: running in 3722 ms after scrolling to it, fetched /models/kim-1.js, /models/kim-1-tracks.webp; status "The model is running. Its digits show the machine's display."
model canvas: 778x460, 39.1% of its pixels are not black
digits match: page segments 63,91,6,91,79,57 (0212 3C), model segments 63,91,6,91,79,57 ("0212 3C")
page key 1 clicked: model key down "1", presses 0 then 1
model key 2 clicked at 801,559: model key down "2"; the machine showed "2121 00" after the page's 1 and "1212 12" after the model's 2
controls: start view azimuth 0.000, polar 0.716, distance 35.795, target 0.000,-1.000,1.500; bounds -12.985,-2.000,-15.640,11.985,2.000,15.640; focused false
hint on the model while unfocused: "Click the model, then scroll to zoom", shown true; touch-action pan-y pinch-zoom
wheel over the unfocused model: scrollY 2695 to 2995; distance 35.795 to 35.795
ctrl and the wheel over the unfocused model: distance 35.795 to 24.973; scrollY 2695 to 2695
after a click on empty space: focused true, ring {"active":true,"outline":"solid","width":"2px"}, touch-action none
wheel over the focused model, cursor on the 5 key at 765,531: distance 35.795 to 24.973; the key is now at 765,531, 0.5 px from the cursor; scrollY 2695 to 2695
Escape: focused false, ring {"active":false,"outline":"none","width":"3px"}, touch-action pan-y pinch-zoom
wheel after Escape: scrollY 2695 to 2995
left-drag 233 px left and 46 px down: azimuth 0.000 to 3.196, polar 0.716 to 0.088; target unchanged true
right-drag: target 0.000,-1.000,1.500 to 11.818,2.000,-2.064; azimuth unchanged true, distance unchanged true
shift and left-drag: target 0.000,-1.000,1.500 to 11.841,2.000,-2.011; azimuth unchanged true
nine right-drags, far and back and forth: target -12.985,2.000,1.308 within -12.985,-2.000,-15.640 to 11.985,2.000,15.640: true; it reached an edge: true
under the board: polar 0.716 to 3.142 (pi/2 is 1.571, pi is 3.142); 51.7% of the canvas is not black, mean brightness 65 of 255 (from above: 39.6%, 71)
dragged on to the stop: polar 3.142, never past pi (3.142)
the wheel to its limits: closest 3.000 (78.3% of the canvas drawn), farthest 150.000 (2.9% drawn)
double click on empty space: distance 150.000 to 35.795, polar 0.716, back at the start view: true
keyboard: ArrowLeft azimuth 0.000 to -0.262; ArrowDown polar 0.716 to 0.978; Shift+ArrowLeft target x 0.000 to -2.863; Shift+ArrowUp target -2.863,-1.000,1.473 to -2.863,0.880,-0.687; + distance 35.795 to 30.425; then - twice to 40.238; Home back at the start: true
reset button: back at the start view: true
tracks at the start: {"tracksLoaded":true,"tracks":true,"partsVisible":true,"partsLevel":1}, buttons pressed true,false, section data-model-tracks "on"
Show tracks, Enter: {"tracksLoaded":true,"tracks":false,"partsVisible":true,"partsLevel":1}, pressed false,false; status "The tracks are off: the board is plain."
the board's left-hand bus, 53x73 px (3816 pixels): colour variance 1704.8 with the tracks, 17.7 without; copper-coloured pixels on the canvas 11.23% with, 1.04% without
Show tracks only: {"tracksLoaded":true,"tracks":true,"partsVisible":false,"partsLevel":0}, pressed true,true, data-model-parts "hidden"; copper-coloured pixels 11.70% (parts shown: 11.23%); the bus's colour variance 1697.7
a click where the hidden 5 key was: presses 2 then 2
Show tracks with the parts hidden: {"tracksLoaded":true,"tracks":false,"partsVisible":true,"partsLevel":1}, pressed false,false
phone, unfocused: touch-action "pan-y pinch-zoom", hint "Tap the model, then drag to turn and pinch to zoom", shown true
phone, one finger swiped up on the unfocused model: scrollY 3241 to 3475; azimuth 0.000 to 0.000, polar 0.716 to 0.716
phone, after a tap on the model: focused true, touch-action "none"
phone, one finger dragged on the focused model: azimuth 0.000 to 2.199, polar 0.716 to 0.000; scrollY 3241 to 3241
phone, two fingers spread on the focused model: distance 35.795 to 21.091; scrollY 3241 to 3241
phone, after it loses focus: touch-action "pan-y pinch-zoom"
no console errors, no failed requests, no CSP violations

real	8m48.005s
user	0m17.932s
sys	0m4.572s
exit 0
```

"53x73 px" is the screen box round a part-free stretch of the board, the bus 10
to 30 mm in and 115 to 165 mm down, found with a new test hook,
`modelBoardPoint(x, y)`. Its colour variance is the sum of the three channels'
variances there. A copper-coloured pixel is one whose red is at least 0.6 of its
green and both are well over its blue: the copper and the gold contacts, not
the green mask or the grey parts. The 1.04% without the tracks is, I think,
the contacts.

### What is proven, and what is not

Proven in headless Chrome 153 with software WebGL: the lines above. Seen, not
measured: screenshots of the model with the tracks on, off and alone, and up
close, looked at during tuning. Not checked: a real GPU, any browser but Chrome,
and a screen reader on the two buttons.

### Limitations

- **The top side only.** The photograph shows nothing of the solder side.
- **Tracks under the photographed parts are gaps.** Where a resistor, a
  capacitor or a chip that the model does not draw covers a track in the
  photograph, the track is not traced, so some tracks stop short and start again.
  The loose red wire across the left of the board does the same to the bus.
- **Some pads are missing.** Pads whose solder has darkened to brown are within
  0.03 to 0.05 of the mask's lightness and have no hue to speak of, so the rules
  cannot tell them from the mask without letting the mask in too. Their tracks
  end a little short of where the pad would be.
- **A little silkscreen may remain** where a label touches a track, and the
  edges of a few photographed parts' legs.
- **The map is 7.5 to 7.7 pixels to the millimetre**, half the source's 15.17,
  to stay inside its budget. Close up, a track's edge is soft.
- **The perspective**, under two pixels on the committed copy, is not corrected.
- **The tabs are not mapped.** Their gold contacts are the model's own, as
  before, and the tracks start at the board's edge.

### Mistakes on the way

- **The first hue band let the capacitors in.** With the band from 85 degrees,
  the cream capacitors (85 to 89) came through as copper. Measuring them moved
  the band's start to 97 (92 for weak copper).
- **A closing that merged the bus.** Closing holes up to 0.2 mm filled the
  0.3 mm gaps between the bus's tracks, and the bus came out as one slab. It is
  now one pixel, and the highlights along each track are kept by the weak rule
  instead.
- **A lightness ceiling that cut the tracks in half.** The first rules dropped
  anything with L over 0.8 as white ceramic or label, but the highlight along a
  tinned track reaches 0.97. Chroma and saturation leave the white parts out
  without it, so the ceiling went.
- **The section's own data attribute matched a button.** The model reports its
  state as `data-model-tracks` on the section, and the first Show tracks button
  was `data-model-tracks` too, so a selector for the button found the section
  first. The buttons are `data-toggle-tracks` and `data-toggle-tracks-only`.
- **The first copper check measured the wrong colour.** It counted pixels
  whose red beat their green, and the first copper rendered greener than red,
  so the tracks "added no copper": 0.82% with them, 0.83% without. The colour
  was changed (above) and the check now uses the measured rendered colours.
- **A flaky wait in the phone check.** One run failed "a finger on the focused
  model scrolled the page", 3222 to 3241: the check had read the scroll position
  before the page had stopped moving, because it waited for two equal readings
  120 ms apart, and on a loaded machine software WebGL can hold a frame back
  longer than that. It now waits for three. The next full run passed, above.

