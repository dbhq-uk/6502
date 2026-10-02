---
title: "A photograph and a model of the KIM-1"
date: 2026-10-02
summary: "The KIM-1's page gets a credited photograph of an original board and a 3D model of it, measured from that photograph, whose six red digits show what the running machine shows and whose keys press the machine's own. The model loads only when a visitor reaches it. Later the same day its board gets the real copper tracks, traced from the photograph, and then five photographs of two boards and a replica of the layout are cross-referenced: tracks on both faces, measured heights, every part, and the page says how well it all agrees."
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


## Cross-referencing several photographs, later the same day

Dan, 2 October 2026: "Can you cross-reference multiple photos to get an
accurate representation". Until now everything in the model came from one
photograph, and the section above lists what that cost: heights were typical
values, tracks under parts and under the red wire were gaps, brown pads were
missed, the underside was plain, parts were placed to about a millimetre off a
grid, and track edges were soft. Rights are not a gate (Dan, the same day):
use the best photographs and credit every one.

### What was built

- **Five photographs, committed**, in `site/src/assets/photos/`, each with a
  section in its README: the file, the author, the source, the licence as
  stated, when it was taken and fetched, the original's size and SHA-256, the
  committed copy's SHA-256, and what the model took from it.
- **The registry's `photo` is now `photos`**, a list with the main photograph
  first, and each entry adds `fetched` and `used`. A new `drawings` list holds
  the replica the photographs are registered to. `validateRegistry` checks every
  entry of both, refuses the old `photo` field, and still fails a running machine
  with no photograph.
- **An offline analysis in Python**, `tools/kim1-model/`, which registers every
  photograph to one reference, traces and fuses the copper, triangulates heights
  and places the parts, and writes what the site uses: the track map
  `site/src/assets/tracks/kim-1.webp`, the parts list
  `site/src/models/kim-1-parts.mjs`, and every figure the page quotes, in
  `site/src/data/kim-1-model.json`. `tools/kim1-model/run-all.sh` runs it in
  order from the full-size originals. The build never runs it, and nothing in
  the build or the tests fetches a photograph.
- **The model** now draws every chip, resistor, capacitor, diode and transistor
  on the replica's pads (22 chips, 77 axial parts, 7 transistors, the trimmer and
  a disc capacitor), the white ceramic of the 6502 and one 6530, the socket under
  the other, the keypad as its own board with a bezel and keys sunk in their
  wells, the crystal's can, and the loose red wire; at measured heights; with
  copper on both faces.
- **The page** has a new section, "Photographs of the original", with all five,
  each credited and saying what the model took from it, and under the model a
  note, "How the model was made", whose every figure is read from
  `kim-1-model.json` by `site/src/models/kim-1-notes.mjs`, with the credits for
  every photograph and the replica and a list of what the model still does not
  show.

The work was started by an earlier session, which wrote most of the analysis
scripts and the registry change and then stopped before committing anything.
This session ran every script again from the originals, fixed what failed, and
finished the rest; every figure below is from this session's runs.

### The photographs found, and which were used

The searches were the earlier session's: the Commons API, the Otten page, the
replica's repository and a web search for a photograph of a solder side. This
session checked bitsavers and confirmed every file used against its hash.

**Wikimedia Commons, `Category:KIM-1`**, listed through the Commons API: 25
files.

- Used: `MOS_KIM-1_IMG_4211_cropped.jpg` (already the main photograph),
  `MOS_KIM-1_IMG_4210.jpg` (the same Musée Bolo board from above its other end)
  and `MOS_KIM-1_IMG_4209.jpg` (the same board from a low corner), all three by
  Rama & Musée Bolo, CC BY-SA 2.0 fr, taken a minute apart on 24 August 2010
  with one camera and lens.
- Not used, the same board: `IMG_4211` (the uncropped original of the main
  photograph), `IMG_4211_cropped_scale` in JPEG and PNG (the main photograph
  with a scale bar drawn on), and `IMG_4206` to `IMG_4208` (oblique views from
  other corners). One pair is enough to triangulate, and 4209 is the lowest
  view, so it has the longest baseline to 4210, which is nearly square on and
  also serves the tracks. More views would mean more points to mark by hand for
  the same kind of evidence.
- Not used, other boards: `Commodore KIM 1.jpg` (CC0) and
  `Commodore MOS KIM-1 (5437738203).jpg` (CC BY-SA 2.0) are whole
  Commodore-badged boards of later revisions, at an angle;
  `(5437737893)` and `(5437738037)` are close-ups of a Commodore board's crystal
  and name, and of its revision label; `The legendary KIM-1 from Commodore`
  is a Commodore board in its box; `KIM-1 single board computer.jpg` (CC BY-SA
  4.0) is in a museum case behind glass, at an angle, with a reflection; the
  two `HomeComputerMuseum` files show it in a display case among other things;
  `Kim-1-computer.jpg` is 640 by 480 pixels.
- Not boards: two pictures of the manuals' covers, the May 1976 advertisement,
  an advertisement for another product, and three modern replicas (two KIM Uno
  kits and a Micro-KIM).

**Hans Otten's Retro Computing site, "KIM-1 revisions images"**, 32 images,
fetched with the page on 2 October 2026 and laid out on a contact sheet. The
page groups them by revision: none, A, B, D, E, F and G (it found no Rev C).

- Used: the Rev B pair, `20160317_143332_HDR-2.jpg` (component side) and
  `20160317_143341_HDR-2.jpg` (solder side), one board photographed square on,
  fetched at full size (2988 by 3984) rather than the page's `-scaled` copies.
  The Musée Bolo's board says "Rev. B" on its silkscreen, so this is the same
  revision: the only other photograph of it found anywhere, and the only one of
  its solder side.
- Not used: every other revision. The copper changed between revisions (the
  replica, of Rev D, agrees with the Rev B photographs on well under all of it,
  below), and the model is of the Musée Bolo's Rev B board, so tracks from
  another revision would put copper where that board has none.
  `KIM1PCBA.jpg` is a drawing of a board's copper artwork, but at 726 by 947
  pixels it has under 4 pixels to the millimetre, too coarse for tracks 0.3 mm
  apart. One is a copy of the Commons photograph.

**Eduardo Casino's KiCad replica**, `github.com/eduardocasino/kim-1` at commit
`7b356b6386422cc6f7154b3524b0a9011ce33bfb`: a Rev D board, both copper layers
and every footprint, CC BY-NC 4.0 (its schematic CC BY 4.0). Its README says it
was traced over a high-resolution photograph scaled by the chips' footprints,
checked against the schematic, built, and run. Used as the reference every
photograph is registered to, for the places of the parts soldered through the
board, and for the copper where no photograph shows the board. Not committed;
`extract_kicad.py` checks its SHA-256 and writes what is used.

**bitsavers**, `components/mosTechnology/kim-1/`: the KIM-1 User Manual of
August 1976 (`6500-15B_KIM-1_Users_Manual_197608.pdf`, fetched at 16:48 UTC,
7,308,281 bytes, 140 pages, SHA-256
`d9f73dd5c587a0e507b3969db30c320dc9c93fbc5cddf093872673a613f94c90`), which holds
the system schematic, and folders of photographs of Rev A, F and G boards. Not
used and not committed: the replica already carries the connectivity, checked
against that schematic, and `pdftotext` finds no drawing of the board's layout
with dimensions in the manual; the photographs are of other revisions.

The web search for a photograph of a KIM-1's solder side found the Otten page
and the replica and nothing else new.

### Downloads and copies

Every original was hashed with `sha256sum` and the hash recorded in
`tools/kim1-model/data/sources.json` and the photographs' README; every script
refuses a file with another hash. The four new committed copies were made with
`cwebp -q 82 -resize 1600 0 -metadata none`, the command used for the main
photograph this morning, and running it again on the originals gave the same
bytes for all four (their SHA-256 begin `cfba26be`, `b295c0ea`, `22ab8b61` and
`3e1d4161`, as in the README). A test checks each committed file against the
SHA-256 in the README.

Hans Otten's site serves no https: on 2 October 2026 at 16:58 UTC `curl` to
the https page failed the TLS handshake and the http one answered 200. So the
validator now accepts an http source, and the credit links it as it is.

### Registration

Every photograph is registered to the replica's board frame, millimetres from
the top left corner of the board's body as the component side shows it. Chosen
over registering the photographs to the main one: the main one has its own
perspective and lens error, which the others would inherit, and the replica is
at true scale with both faces drawn. The method, in `register.py`: a homography
from the body's four corners, marked by hand in each original; refined by
OpenCV's ECC, matching a map of "lighter than the lacquer round it" in the
photograph to the replica's copper on the same face, coarse to fine at 2, 4 and
8 pixels to the millimetre; then the leftover shift measured in 24 mm blocks
every 16 mm, and a smooth cubic in x and y fitted to it robustly, for the lens.
The error is the shift left in blocks the cubic was not fitted to (it is fitted
on half the blocks in a chequerboard and measured on the other half).

`PYTHON=/tmp/kimvenv/bin/python tools/kim1-model/run-all.sh /tmp/kim/orig
/tmp/kim/casino/kim-1.kicad_pcb` on 2 October 2026, with Python 3.12.3, numpy
2.5.3, opencv-python-headless 5.0.0.93, Pillow 12.3.0 and scipy 1.18.1, took
9 minutes 43 seconds (`time`). Its registration lines:

```
bolo-top: ECC cc 0.6322, 0.6053, 0.5801; 71 blocks; residual after the homography median 0.163 mm, 90th percentile 0.346, max 1.6; after the cubic correction 0.091, 0.179, 0.282 (3 outliers); held out 0.123, 0.225, 1.59
rev-b-front: ECC cc 0.6289, 0.5875, 0.5444; 62 blocks; residual after the homography median 0.207 mm, 90th percentile 0.415, max 0.728; after the cubic correction 0.134, 0.258, 0.288 (2 outliers); held out 0.141, 0.288, 0.392
rev-b-back: ECC cc 0.635, 0.7148, 0.7235; 150 blocks; residual after the homography median 0.241 mm, 90th percentile 0.399, max 1.348; after the cubic correction 0.182, 0.365, 0.504 (3 outliers); held out 0.207, 0.389, 0.607
bolo-end: ECC cc 0.6447, 0.6142, 0.5764; 74 blocks; residual after the homography median 0.191 mm, 90th percentile 0.352, max 0.819; after the cubic correction 0.084, 0.156, 0.224 (6 outliers); held out 0.1, 0.192, 0.954
bolo-oblique: ECC cc 0.5403, 0.5082, 0.4857; 60 blocks; residual after the homography median 0.207 mm, 90th percentile 0.43, max 0.999; after the cubic correction 0.099, 0.199, 0.306 (4 outliers); held out 0.125, 0.468, 1.144
```

So every photograph is registered to a tenth to a fifth of a millimetre at the
median, on blocks held out of the fit. The largest held-out errors, 1.6 mm on
the main photograph and 1.1 mm on the oblique one, are single blocks near the
edges where the cubic extrapolates. The blocks are measured against the
replica, which is a later revision, so where the two boards differ a block can
be off for that reason and not the registration's.

### The tracks

`trace.py` finds the copper in each rectified photograph the same way for every
one, with no threshold tuned against the replica, because the replica is what
the result is measured against. In OKLab: where the board shows at all (green
within a band round the photograph's own median hue, which is about 104 degrees
in one photograph and 144 in another, or a light, colourless, pad-shaped blob
touching green), and on that, copper is lighter than the lacquer round it by
more than a threshold Otsu's method picks, with hysteresis. Print in the
board's own colours is dropped by shape: copper is a pad or part of something
longer than 5 mm. The packages that sit in the same place on every board, the
chips and the LED digits, are taken from the replica's footprints as not
showing the board, so their own markings cannot read as copper.

`fuse.py` makes the map. On the top face, pixel by pixel over the three
photographs of it: the majority where two or three see the board, a tie going
to the better photograph (native resolution over held-out registration error);
the one that sees it where only one does; the replica's copper where none does.
Copper wider than 2.5 mm both ways in one photograph counts only where another
has copper too: that is a pour, or a light part taken for one. Chosen over the
union, which would let in each photograph's false copper, and over the
intersection, which would drop every track one photograph cannot see. The
replica is used only where no photograph shows the board, because the model
follows the photographed board. The underside is its one photograph's trace.

The map keeps its sources apart: red is the top face from the photographs,
green the top face from the replica, blue the underside. Chosen over one
greyscale map per face because the three come under different licences (below),
and over two files because one image is one fetch. The model reads red or green
as the top face's copper and blue as the underside's.

The fuse step's output in the same run:

```
wrote site/src/assets/tracks/kim-1.webp: 1600 x 2184, 314614 bytes
top: seen by a photograph 65.3% of the body, from the replica 7.6%; sources disagree on 10.3% of what two or more see
top against the replica: before 0.4161, after 0.5716
   top left: the bus and the three 40-pin chips: before 0.5071, after 0.6014
   top right: memory, crystal and name: before 0.4768, after 0.555
   middle left: the bus between the connectors: before 0.4384, after 0.659
   middle right: display drivers and display: before 0.4518, after 0.5281
   bottom left: cassette, teletype and timer: before 0.2471, after 0.4723
   bottom right: the keypad: before 0.144, after 0.2072
underside against the replica: 0.5421
   top left: the bus and the three 40-pin chips: 0.654
   top right: memory, crystal and name: 0.5437
   middle left: the bus between the connectors: 0.5756
   middle right: display drivers and display: 0.5337
   bottom left: cassette, teletype and timer: 0.5298
   bottom right: the keypad: 0.2964
bolo-top against the other two: before 0.4174, after 0.8099
bolo-end against the other two: before 0.4324, after 0.8067
rev-b-front against the other two: before 0.3829, after 0.6604
```

Each figure is the intersection over union of copper pixels at 8 pixels to the
millimetre, measured only where a photograph shows the board. **The measure
that matters is the last three lines**: each photograph's own trace against the
map fused from the other two, so the photograph checked is not in what it is
checked against. Before, the map traced this morning from one photograph; after,
the vote of the other two. The two Musée Bolo photographs agree with the
others' vote on 81%, the other board's photograph on 66%: it is a different
board, made the same way but not identically. Against the replica the top face
went from 42% to 57%; it cannot reach 100%, being of Rev D. Its keypad region
scores lowest: the keypad covers most of the board there, so few pixels are
measured, and I have not looked into why those few disagree.

The old map scored 42% even against the photograph it was traced from, which
was a surprise. `python probe_before.py` (in `tools/kim1-model/`) says why:

```
old map against bolo-top's new trace: as placed 0.4088; at its best shift, +0.625 mm in x and -0.125 mm in y, 0.524; with a pixel's tolerance, 0.5871
copper share where bolo-top shows the board: old map 0.314, new trace 0.255
```

It sat about 0.6 mm off where the registration puts the photograph's copper
(I think because the old map was stretched over the board's edges as measured
on that photograph, where the registration places copper by the copper), and it traced the
tracks wider, so it covered 31% of the board where this trace covers 26%. Even
moved to its best place it scores 52%, against 81% for the new map. The page
says this in a sentence.

### The heights

`heights.py` measures them by triangulation between `IMG_4209` and `IMG_4210`.
Both are registered to the board, which gives each a homography from the
board's plane to the image; one pinhole camera is fitted to both at once (the
same body and lens a minute apart), its focal length and principal point free,
with a pose for each photograph, by least squares over 180 points of the board.
A point marked in both photographs is where the two rays through it pass
closest, and its height is above the board's top face. Each height comes with a
range: the same point with the principal point held at the frame's centre, and
with each mark moved by 3 pixels in eight directions. The marks are in
`data/height-marks.json`, each saying what it is, read off crops of each
original at 2 to 3.6 crop pixels to the pixel. The first mark is a check: the
centre of a mounting hole, on the board itself, which must come out at 0. The
run's lines:

```
camera, principal point free: f 16911 pixels (111.6 mm), principal point 2597, 2347; the plane fits to 1.32 pixels RMS
camera, principal point centred: f 18121 pixels (119.6 mm), principal point 2808, 1872; the plane fits to 1.48 pixels RMS
check: the centre of the mounting hole beside the keypad, on the board: at 122.6, 173.0 mm, 0.14 mm above the board (from -0.21 to 0.48); the rays miss by 2.8 px
the keypad's circuit board, top face, the corner nearest the display: at 127.6, 165.8 mm, 2.13 mm above the board (from 1.79 to 2.48); the rays miss by 0.5 px
the keypad's bezel, top face, the corner nearest the display: at 127.8, 173.7 mm, 9.05 mm above the board (from 8.70 to 9.39); the rays miss by 3.7 px
the GO key, the middle of the hole in its O: at 139.8, 184.9 mm, 7.19 mm above the board (from 6.85 to 7.54); the rays miss by 4.2 px
the display's window, top face, the corner by U18: at 122.0, 138.3 mm, 5.87 mm above the board (from 5.52 to 6.21); the rays miss by 2.4 px
U2, the black 40-pin package, top face, the corner at its notched end by pin 1's row: at 27.4, 104.6 mm, 8.76 mm above the board (from 8.44 to 9.09); the rays miss by 1.0 px
U1, the white ceramic 40-pin package, top face, the corner at its pin 1 end: at 28.3, 36.1 mm, 2.45 mm above the board (from 2.12 to 2.78); the rays miss by 1.1 px
U12, a 16-pin memory package, top face, the corner by its U12 label: at 102.1, 10.7 mm, 2.85 mm above the board (from 2.50 to 3.19); the rays miss by 1.3 px
U12, the same memory package, top face, the corner at its other end: at 121.1, 16.3 mm, 2.49 mm above the board (from 2.14 to 2.84); the rays miss by 2.2 px
the crystal's can, the middle of the BME stamped on its top: at 146.6, 17.5 mm, 8.07 mm above the board (from 7.72 to 8.43); the rays miss by 0.7 px
a transistor by the display, the end of the flat edge of its top: at 140.1, 130.2 mm, 8.19 mm above the board (from 7.84 to 8.54); the rays miss by 1.1 px
the trimmer, the middle of the cross in its rotor: at 62.2, 231.8 mm, 3.32 mm above the board (from 3.00 to 3.65); the rays miss by 4.5 px
the orange capacitor, a white fleck on its top: at 88.4, 174.6 mm, 9.10 mm above the board (from 8.77 to 9.44); the rays miss by 2.6 px
U13, a 14-pin logic package, the middle of the TI logo printed on its top: at 136.0, 70.1 mm, 4.46 mm above the board (from 4.11 to 4.81); the rays miss by 2.4 px
a cream capacitor beside the memory, the W of TAIWAN printed along its top: at 93.2, 63.7 mm, 6.39 mm above the board (from 6.05 to 6.73); the rays miss by 2.8 px
```

So a height is good to about a third of a millimetre either way, and the check
point is 0.14 mm off the board. The focal length came out at 111.6 mm against
the Exif's 100 mm, which is what a lens focused close does. Two surprises: U2,
the black 6530, stands 8.76 mm tall because it is in a socket, and the ceramic
6502 only 2.45 mm, flat on the board. The two corners of U12 differ by 0.36 mm,
about the range. The cream capacitor, C16 on the replica, measures 6.39 mm
against its footprint's 6.5 mm diameter.

The model uses each measured height for its part and for the others of its
kind (`HEIGHT_SOURCES` in `site/src/models/kim-1-layout.mjs` lists which), and
the two measured capacitors take their measured heights as their diameters. A
chip's body thickness under its measured top, the board's 1.6 mm, and the other
resistors, capacitors and diodes' sizes are from their packages and
footprints, not measured.

**The side view, before and after**: `results.py` reads the heights the old
model had from its code at `e0fd715` and sets them beside the measured ones:

| Part | Before | Measured |
|---|---|---|
| a ceramic 40-pin chip | 4.6 | 2.45 |
| the socketed 6530 | 4.6 | 8.76 |
| a memory chip | 4.6 | 2.67 |
| a logic chip | 4.6 | 4.46 |
| the display window | 4.5 | 5.87 |
| the keypad's top | 5.5 | 9.05 |
| a key's top | 9.5 | 7.19 |
| the crystal | 5.0 | 8.07 |

(millimetres above the board; from `heights.before` in
`site/src/data/kim-1-model.json`). The old heights were off by 2.33 mm on
average and by 4.16 mm at worst. The new ones are the measurements themselves,
so there is no independent after figure for the heights; the check point on the
board, at 0.14 mm, is the nearest thing to one.

### The parts' places

`layout.py`. The parts soldered through the board stand on the replica's pads,
which the photographs are registered to. Checked by finding each chip's body in
two photographs (a template of the body's size, with bands round it where the
board should show, slid within 3 mm of where the replica puts it); the keypad's
bezel edges were marked by hand on the rectified main photograph, because it is
black on a dark board with black keys in it and no threshold separated it; the
keys' centres are fitted as a grid to the white print of their legends; the
display window and the crystal's can are found by colour and moved so that the
corner triangulated above is where it should be; the name by its white letters;
the red wire by its red pixels, a point every 8 mm. The run's lines:

```
integrated circuits: 22; body found in a photograph 44 times (at least 85% of the box not showing the board); offset from the replica's centre: median 0.844 mm, 90th percentile 1.381, max 2.007
keypad bezel: [127.81, 173.68, 188.41, 258.68] (moved by (0.91, -0.22) mm to its triangulated corner); display window: [np.float64(121.98), np.float64(138.28), np.float64(187.07), np.float64(156.78)] (moved by (np.float64(-1.35), np.float64(-0.47))); crystal can: [np.float64(136.21), np.float64(9.73), np.float64(157.05), np.float64(25.23)]
keys: 23, on a grid of 12.5 by 12.4 mm; columns [138.91, 151.49, 164.24, 176.99], rows [184.86, 197.45, 210.07, 222.95, 235.57, 248.11]; a key's legend is 0.264 mm from its row and column at the median, 0.969 at most
the KIM-1 name's letters: x 156.1 to 187.4, y 35.0 to 41.2 mm
the red wire: 11768 pixels in 1 pieces, 14 points from [30.83, 193.67] to [5.08, 93.5]
wrote site/src/models/kim-1-parts.mjs: 22 integrated circuits, 77 axial parts, 7 transistors, 2 others
```

(The `np.float64(...)` in the second line is how numpy printed the boxes; the
script now turns them into plain numbers first.) A chip's body is found 0.84 mm from the replica's centre at the median. That
includes parallax: the photographs are registered on the board's plane, and a
chip's top stands a few millimetres above it, so it shifts in any photograph
not taken from straight above. It is a check on the replica's places, not a
correction to them. The grid of keys has a pitch of 12.5 by 12.4 mm against
the half inch the morning's measurement expected.

### What the page says

The note under the model, "How the model was made", gives these figures in
words, all read from `site/src/data/kim-1-model.json` (`results.py` writes it
from the analysis's own data files, and a test checks the two agree), and lists
what the model does not show. The credit under the model now names every
photograph and the replica, each with what was taken from it, from the
registry's `used` fields. The photographs section shows all five.

### Licences

The photographs keep their sources' terms: the three Musée Bolo photographs CC
BY-SA 2.0 fr, which is share-alike; the two from Hans Otten's site no licence
stated, and the site does not name the photographer. The replica is CC BY-NC
4.0. The track map's red channel is traced from the photographs, its green from
the replica, its blue from an Otten photograph, so no one licence covers it:
share-alike asks for the same licence on a derivative, the replica asks for no
commercial use, and the Otten photographs state nothing. Rights are not a gate
(Dan), so it is published crediting every source, channel by channel, in the
photographs' README and on the page; none of it is MIT, and the repository's
README now says so, with the replica's extracted data in
`tools/kim1-model/data/` named too.

### Decisions

- **One reference for everything: the replica.** Chosen over chaining the
  photographs to the main one. Its scale is its chips' 0.1 inch pins, it has
  both faces, and every photograph registers to it to a fifth of a millimetre.
  The cost: the board frame is now the replica's, 200 by 273 mm, where this
  morning's measurement gave 199.7 by 272.8, and the board's outline and tabs
  are the replica's.
- **Rev B photographs only, for copper.** Chosen over using every revision for
  more coverage, because copper from another revision would be wrong copper.
- **The replica only where no photograph shows the board** (7.6% of the top
  face, under the chips, the display and the keypad), kept in its own channel.
- **Heights measured where they could be**, typical where not, and said which.
- **Every resistor, capacitor and diode drawn**, from the replica's footprints,
  because the photographs show them all and the model drew none. Chosen over
  drawing only the large ones.
- **Instanced meshes** for the axial parts, their leads, the transistors and the
  keypad's walls: a few draws instead of several hundred (below).
- **The photographs section below the model**, not in the head: the head keeps
  one photograph beside the details, as this morning. Every image in the section
  loads lazily.
- **`make-board-tracks.mjs` is deleted.** It traced the one photograph and
  imported the old layout's names; `tools/kim1-model/` replaces it. The
  morning's sections above still name it, as a record of that day.
- **The budgets stay where they were**: 200,000 bytes gzipped for the model's
  bundle and 400,000 bytes for the track map, both held by tests.

### Mistakes on the way

- **The shared checkout.** The earlier session's work was left uncommitted in
  `~/dbhq-uk/6502`, and by the time this session started another session had
  switched that checkout to its own branch for other work. Committing there would
  have mixed the two, so this work moved to a separate worktree on
  `feat/model-accuracy`, and the files were copied across. One of the other
  session's new files was copied with them by mistake, and deleted from the
  worktree before anything was committed. Cleaning the shared checkout was not
  permitted, so its copy of this work is still there, uncommitted.
- **The tabs came out as four.** `layout.py` grouped the outline's points by
  their order along y, as if the outline were a polygon; it is a set of line and
  arc segments in no order, so the gaps fell in the wrong places and the first
  build failed reading a tab that did not exist. The tabs are now found from the
  pairs of points furthest left, and the script stops unless there are two.
- **The model got too heavy to draw.** With every part drawn as its own meshes
  (five for each axial part), the first browser check timed out taking the
  canvas's screenshot: software WebGL took 12.4 s to start the model and could
  not hold two frames still. Instancing cut it to a few draws.
- **The browser check's "part-free" stretch had parts on it.** It measured the
  tracks on the bus 10 to 30 mm in and 115 to 165 mm down, and with every part
  drawn that stretch now holds a capacitor, a resistor and the red wire, so its
  colour varied even with the tracks off (1076.2, against 17.7 this morning) and
  the check failed. It now uses 10 to 26 mm in and 160 to 200 mm down, checked
  against the parts list to be clear of every part and the wire.
- **Home went to the page, not the model.** The new underside check pressed Home
  to reset the view while a track button had focus, so the page scrolled to the
  top, the stage stopped drawing off screen, and the camera never came to rest.
  The check now brings the stage on screen and focuses it first.
- **Screenshots of the canvas timed out on a busy machine.** Playwright's
  element screenshot waits for the element to hold still over two animation
  frames; with the load average over 30 on 8 cores (other work), software WebGL
  could not draw two in 30 seconds. The check now clips a page screenshot to the
  canvas's box, with the check's own two-minute step timeout.
- **The underside check turned the model the wrong way on the CI runner.** It
  dragged down to go under the board, and there a drag down turned the camera
  up, to polar 0. The check before it already finds which way a drag goes, so
  the underside check now uses that.
- **Too few copper points on the underside.** It first asked for copper solid for
  a millimetre all round, which only the pours are, and found 18. Pads and wide
  tracks, solid for 0.375 mm, number in the hundreds.
- **A test that a commit could fail.** The CI run on the pull request's merge
  commit failed "every number on the home and status pages is a generated
  figure": the status page shows the commit's first seven characters, which for
  that merge commit were `1275329`, all digits, and the test allowed only the
  numbers inside the full hash. It now allows the seven as well. Nothing in this
  change caused it; the next merge commit would have passed by chance.
- **The local browser check could not finish.** With the machine's load average
  between 33 and 81 from other work, software WebGL took more than the check's
  15 seconds to bring the camera to rest, at different steps on different runs.
  The record below is from the CI runner, which runs the same script in
  headless Chrome (154 there, 153 here).
- **The replica was described from memory.** The first text said it was drawn
  "at true size from the board's 0.1 inch grid" and "traced from scans of a bare
  board". Its README says it was traced over a photograph scaled by the chips'
  footprints, and checked against the schematic; the page, the README and the
  scripts now say that.

### Page weight

`node scripts/page-weight.mjs /machines/kim-1/`, which now counts an image
marked `loading="lazy"` apart, because it is fetched only as it nears the
screen. On a build of `main` at `e0fd715`:

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

On this branch:

```
/machines/kim-1/ when it opens: file, bytes, gzipped
  /machines/kim-1/ (html)	57544	13336
  /analytics.js	6510	2983
  /consent.js	2743	1227
  /machines-table.js	2416	1021
  /kim-1.js	7013	2906
  /model-loader.js	1679	807
  /table-sort.js	766	449
  /_astro/kim-1.ufzgBkwp_ZO889l.webp	87100	87072
  total	165771	109801
loaded when scrolled to (images marked loading="lazy"):
  /_astro/kim-1-bolo-end.qV0CNCQu_D8tIa.webp	41266	41289
  /_astro/kim-1-bolo-oblique.DcBK6OO4_FgKWi.webp	39648	39554
  /_astro/kim-1-rev-b-front.1Hqc7X6F_MPpNt.webp	104202	104255
  /_astro/kim-1-rev-b-back.BOPlcWx0_ZsUNu2.webp	116286	116344
  total	301402	301442
loaded later, when the visitor reaches the model:
  /models/kim-1.js	637898	163048
  /models/kim-1-tracks.webp	314614	314732
  total	952512	477780
```

So the page's first load grew by 15,077 bytes of HTML (2,880 gzipped: the
photographs section and the note under the model). The four new photographs
cost 301,402 bytes, as fallbacks, and only when scrolled to; a browser takes
the 360 or 720 pixel AVIF from the srcset, which is smaller. What the model
loads grew by 48,145 bytes: 27,483 in the bundle (the parts list) and 20,662 in
the track map, which now holds three channels. Both stay inside the budgets the
tests hold: the bundle is 161,945 bytes gzipped by `gzip -9 -c | wc -c` against
200,000, and the map 314,614 bytes against 400,000.

### Tests

`npm test` in `site/`: 193, from 188. New in `model.test.mjs`: the parts are the
replica's and the heights the triangulated ones; the note says how the model
was made, word for word what `made()` writes from the results file, and credits
every photograph and drawing with what was taken from each; and the analysis's
sources agree with the registry and its results file with its data. New in
`registry.test.mjs`: drawings are credited like photographs, and every
photograph's README section gives its author, its source and the SHA-256 of the
file as committed, which must match. New in `machine-page.test.mjs`: the
photographs section shows every photograph in the registry's order, lazily,
and credits each. Changed: the track map test (three channels, not greyscale),
the tracks' credit, every test that read the old `photo` field, and the
figures test, which now allows the commit's first seven characters (below). Removed:
the test of `make-board-tracks.mjs`, with the script. The floor in both
workflows is 193.

### The browser check

`node scripts/browser-check.mjs`, as the `Validate` workflow's "The KIM-1 runs
in a browser" step ran it on 2 October 2026 for this pull request (Actions run
37043265765, at commit `f1c768c`), copied from the run's log:

```
browser 154.0.8037.57
loaded in 1067 ms: Running. Press RS to start the monitor.
step 1 ok   keys "[RS]" expected "xxxx xx" shown "0000 00" announced "Display 0000 00" (549 ms)
step 2 ok   keys "[AD] 17FA [DA] 00 [+] 1C" expected "17FB 1C" shown "17FB 1C" announced "Display 17FB 1C" (1522 ms)
step 3 ok   keys "[AD] 0200 [DA] AD [+] 10 [+] 02 [+] 18 [+] 6D [+] 11 [+] 02 [+] 8D [+] 12 [+] 02 [+] 4C [+] 0A [+] 02" expected "020C 02" shown "020C 02" announced "Display 020C 02" (3976 ms)
step 4 ok   keys "[AD] 0210 [DA] 27 [+] 15" expected "0211 15" shown "0211 15" announced "Display 0211 15" (1259 ms)
step 5 ok   keys "[AD] 0200 [GO]" expected "       " shown "       " announced "Display dark" (955 ms)
step 6 ok   keys "[ST]" expected "020A 4C" shown "020A 4C" announced "Display 020A 4C" (466 ms)
step 7 ok   keys "[AD] 0212" expected "0212 3C" shown "0212 3C" announced "Display 0212 3C" (844 ms)
status line: "Running. Press RS to start the monitor.", then "Running. Type the program below, or your own."
speed, once a second for 5 s: running 1.00 MHz, capacity 27.48 MHz; running 1.00 MHz, capacity 29.41 MHz; running 1.00 MHz, capacity 29.99 MHz; running 1.00 MHz, capacity 30.49 MHz; running 1.00 MHz, capacity 30.77 MHz
page speed line: Running at the board's own 1 MHz. This browser could run it about 30 times as fast.
photograph: loaded /_astro/kim-1.ufzgBkwp_1lGWCK.avif, 300x370 pixels, shown at 298x368
model bundle requested before scrolling to it: no
model: running in 1190 ms after scrolling to it, fetched /models/kim-1.js, /models/kim-1-tracks.webp; status "The model is running. Its digits show the machine's display."
model canvas: 778x458, 39.2% of its pixels are not black
digits match: page segments 63,91,6,91,79,57 (0212 3C), model segments 63,91,6,91,79,57 ("0212 3C")
page key 1 clicked: model key down "1", presses 0 then 1
model key 2 clicked at 801,559: model key down "2"; the machine showed "2121 02" after the page's 1 and "1212 12" after the model's 2
controls: start view azimuth 0.000, polar 0.716, distance 35.795, target 0.000,-1.000,1.500; bounds -13.000,-2.000,-15.650,12.000,2.000,15.650; focused false
hint on the model while unfocused: "Click the model, then scroll to zoom", shown true; touch-action pan-y pinch-zoom
wheel over the unfocused model: scrollY 2695 to 2995; distance 35.795 to 35.795
ctrl and the wheel over the unfocused model: distance 35.795 to 24.973; scrollY 2695 to 2695
after a click on empty space: focused true, ring {"active":true,"outline":"solid","width":"2px"}, touch-action none
wheel over the focused model, cursor on the 5 key at 766,531: distance 35.795 to 24.973; the key is now at 766,531, 0.2 px from the cursor; scrollY 2695 to 2695
Escape: focused false, ring {"active":false,"outline":"none","width":"3px"}, touch-action pan-y pinch-zoom
wheel after Escape: scrollY 2695 to 2995
left-drag 233 px left and 46 px down: azimuth 0.000 to 3.196, polar 0.716 to 0.088; target unchanged true
right-drag: target 0.000,-1.000,1.500 to 11.822,2.000,-2.048; azimuth unchanged true, distance unchanged true
shift and left-drag: target 0.000,-1.000,1.500 to 11.838,2.000,-2.011; azimuth unchanged true
nine right-drags, far and back and forth: target -13.000,2.000,0.970 within -13.000,-2.000,-15.650 to 12.000,2.000,15.650: true; it reached an edge: true
under the board: polar 0.716 to 3.142 (pi/2 is 1.571, pi is 3.142); 51.6% of the canvas is not black, mean brightness 69 of 255 (from above: 39.7%, 83)
dragged on to the stop: polar 3.142, never past pi (3.142)
the wheel to its limits: closest 3.000 (77.3% of the canvas drawn), farthest 150.000 (2.7% drawn)
double click on empty space: distance 150.000 to 35.795, polar 0.716, back at the start view: true
keyboard: ArrowLeft azimuth 0.000 to -0.262; ArrowDown polar 0.716 to 0.978; Shift+ArrowLeft target x 0.000 to -2.864; Shift+ArrowUp target -2.864,-1.000,1.483 to -2.864,0.880,-0.677; + distance 35.795 to 30.425; then - twice to 40.238; Home back at the start: true
reset button: back at the start view: true
tracks at the start: {"tracksLoaded":true,"tracks":true,"underside":true,"partsVisible":true,"partsLevel":1}, buttons pressed true,false, section data-model-tracks "on"
Show tracks, Enter: {"tracksLoaded":true,"tracks":false,"underside":false,"partsVisible":true,"partsLevel":1}, pressed false,false; status "The tracks are off: the board is plain."
the board's left-hand bus, 48x67 px (3149 pixels): colour variance 1591.6 with the tracks, 8.6 without; copper-coloured pixels on the canvas 14.57% with, 4.24% without
Show tracks only: {"tracksLoaded":true,"tracks":true,"underside":true,"partsVisible":false,"partsLevel":0}, pressed true,true, data-model-parts "hidden"; copper-coloured pixels 16.19% (parts shown: 14.57%); the bus's colour variance 1594.7
a click where the hidden 5 key was: presses 2 then 2
Show tracks with the parts hidden: {"tracksLoaded":true,"tracks":false,"underside":false,"partsVisible":true,"partsLevel":1}, pressed false,false
under the board, polar 3.142, tracks on: the map's underside copper at 286 points is 109,97,37, its bare board at 255 points 18,63,20, 98.4 apart; the same points mirrored end to end 15.3 apart; with the tracks off 3.2 apart
photographs section: 5 photographs; kim-1.webp loaded /_astro/kim-1.ufzgBkwp_1lGWCK.avif 300x370; kim-1-bolo-end.webp loaded /_astro/kim-1-bolo-end.qV0CNCQu_Z2te0wp.avif 300x200; kim-1-bolo-oblique.webp loaded /_astro/kim-1-bolo-oblique.DcBK6OO4_UG3Hn.avif 300x200; kim-1-rev-b-front.webp loaded /_astro/kim-1-rev-b-front.1Hqc7X6F_1I4Ioi.avif 300x400; kim-1-rev-b-back.webp loaded /_astro/kim-1-rev-b-back.BOPlcWx0_Z166GoH.avif 300x400
phone, unfocused: touch-action "pan-y pinch-zoom", hint "Tap the model, then drag to turn and pinch to zoom", shown true
phone, one finger swiped up on the unfocused model: scrollY 3241 to 3475; azimuth 0.000 to 0.000, polar 0.716 to 0.716
phone, after a tap on the model: focused true, touch-action "none"
phone, one finger dragged on the focused model: azimuth 0.000 to 2.199, polar 0.716 to 0.000; scrollY 3241 to 3241
phone, two fingers spread on the focused model: distance 35.795 to 21.091; scrollY 3241 to 3241
phone, after it loses focus: touch-action "pan-y pinch-zoom"
no console errors, no failed requests, no CSP violations
```

The underside line is the new proof that the underside's sheet is laid the
right way: from below, the 286 points the map calls copper and the 255 it calls
bare are 98.4 apart in colour, the same points mirrored end to end only 15.3,
and with the tracks off 3.2. The bus's colour variance with the tracks off is
8.6 on its new, part-free stretch.

### What is proven, and what is not

Proven by the tests and the browser check (above): every figure on the page is
the results file's, every photograph is credited and loads from this site, the
underside's copper shows from below where the map puts it, and nothing new
breaks the CSP. Seen, not measured: the before and after screenshots of the
model and the overlays of the old and new maps on the rectified photograph,
looked at while working. Not checked: a real GPU, any browser but Chrome, a
screen reader on the new section and note.

### Limitations that remain

- **The underside is one photograph of a different board** from the one the
  rest of the model follows, and only the replica checks it. A large tinned
  pour on it, on the left about halfway down, is traced in patches, because its
  wrinkled solder is lighter in some places than others.
- **Under the chips, the display and the keypad the copper is the replica's**,
  Rev D, not the photographed Rev B: 7.6% of the top face.
- **The vote decides where the photographs disagree**, on 10.3% of what two or
  more see, and the photograph outvoted may be right.
- **One part of each kind was measured for height**, and the rest of its kind
  take that height; the other resistors, capacitors and diodes take their
  footprints' sizes, and a chip's body thickness under its measured top is
  typical.
- **Resistors, capacitors and diodes are plain cylinders**, with no bands or
  printing, and transistors are round, not flat on one side.
- **The red wire** follows the path one photograph shows and is drawn lying just
  above the board; its height was not measured.
- **The map has 8 pixels to the millimetre**: close up a track's edge is soft.
  More would cost more than the budget allows for a model that loads on demand.
- **Parallax is not corrected** in the check on the chips' places, so that
  check bounds the replica's places rather than measuring them.
- **The board's outline and tabs are the replica's**, which may not match the
  Musée Bolo's board to the tenth of a millimetre.

