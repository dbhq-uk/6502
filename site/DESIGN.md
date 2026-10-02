# Design notes

The look of 6502.dbhq.uk, and why it is what it is.

## A deliberate fork of the DBHQ brand

The site is a phosphor terminal, after the Modal reference: a pure black canvas,
pale-green type, hairline borders, no shadows, and code-window cards for test
output. The DBHQ brand is blue and the other DBHQ sites are paper (dbhq.uk) or
ink (skills.dbhq.uk). This one is green, on purpose: it is a hardware project,
and the look carries that. Dan chose it on 30 September 2026 from five mocks:
paper, ink, an amber phosphor, a Basedash-style serif look, and this.

**Do not "fix" it back to dbhq.uk's paper.** The fork is the decision.

- **One lime accent, once per screen, as a fill.** The lime is the single
  strongest colour on the page, so it is rationed. A test fails the build if a
  page fills more than one element with it. It has two other uses, both named in
  that test: the terminal window's text on the home page (the "Passed" labels,
  the syntax colour of a code window, five of them there) and the keyboard focus
  ring. A fourth use has to be argued for in the test.
- **No shadows except the navigation bar's,** and depth is a change of surface
  value plus a one-pixel line. A test holds it.
- **Type:** Inter Tight for headings, Inter for body text and Fira Mono for code,
  all open licence and self-hosted through Fontsource, so nothing loads from a
  font host. The reference's own display face is a custom one; Inter Tight stands
  in for it.
- **Contrast:** the reference's dimmest text colour, `#697368`, measures 4.25 to 1
  on black, under the 4.5 that AA asks of small text. It is lightened to
  `#737d72` (4.91). A test computes every text pair from the real tokens, and for text on the
  traces texture it measures the brightest pixel of the image under its overlay.
- **The analytics notice is a small panel at the foot of the page, not a modal.**
  The behaviour is the estate's: analytics on by default, a notice, a simple
  opt-out. Only the look is this site's: an iron surface, a hairline border, no
  shadow, no backdrop and no lime fill, because the lime is rationed and a cookie
  notice should not spend it. OK and Opt out are the same size and the same
  button, side by side, with Opt out first. Its two text colours are in the
  contrast table in `tests/design.test.mjs`. It replaced a centred modal dialog
  on 1 October 2026, the day after launch, to match every other DBHQ site.

## The KIM-1 page

The first machine page, added on 1 October 2026. It is a panel on an iron
surface with a hairline border, like the terminal window, holding the six digits
on the black canvas and the keypad below them.

- **The digits are drawn in SVG, seven segments each, in the brightest text
  colour, `--white`.** The board's LEDs are red. Red is not in this palette, and
  the page is a drawing of what the machine shows, not a photograph of the
  board, so the segments take the site's phosphor green. Unlit segments are
  `--veil`, faintly visible, as an unlit LED is.
- **The keypad is the board's layout,** six rows of four: GO, ST, RS and the
  SST switch, then AD, DA, PC and +, then the hex keys from C D E F down to
  0 1 2 3. Each key is a real button in `--white` on `--veil`, `--card` when
  hovered and the black canvas while pressed. The SST switch is a checkbox with
  `role="switch"`, in the place the slide switch has on the board.
- **No lime on this page.** The lime is rationed to one fill per screen, and no
  key is more important than another; the focus ring is the lime, as everywhere.
- **Without JavaScript** the keys are disabled, with a dashed edge rather than a
  dimmed label, so they stay readable, and the status line says the machine
  needs JavaScript.
- **Screen readers** do not hear the digits flicker. The drawing is hidden from
  them and a polite live region reads out the display once it has held still
  for 400 ms.
- The new text colours, the keys, the status and speed lines and the key caps
  in the program, are in the contrast table in `tests/design.test.mjs`.

## The imagery

Three generated images, made with the `imager` skill on 30 September 2026 with
`gpt-image-2.5-flare`. Each was drafted first at low quality (four drafts, $0.018
billed in all), then the three that worked were made again at high quality with
the draft as the reference ($0.148 billed). The originals are 0.4 to 2 MB each (`ls -l`: 421,970, 1,096,527 and 2,055,376 bytes) and
are not in git; the site ships the WebP versions in `src/assets/imagery/`.

**They are illustrations and are captioned as illustrations.** None is evidence.
The prompt asked for a 6502 die, but a model draws a plausible die and not the
chip's real layout, so it is not a photograph or a drawing of the 6502, and a test fails the
build if a generated image is shown without an "Illustration" caption. The KIM-1
page's machine uses no image: its digits and keypad are drawn, and driven by the
emulator. The page does show a photograph of the original board, which is a
different kind of image with a different rule (below).

### hero-die

The hero, below the headline, faded at the edges.

- Draft: `1536x864`, quality low. Prompt: "A macro photograph of a single MOS 6502 microprocessor die seen from directly above, fine metal traces glowing in pale phosphor green (#ddffdc) with one small lime (#7fee64) highlight, deep black background, shallow depth of field, cinematic, no text, no logos"
- Final: 1536x864, quality high, the draft passed as the reference so the composition held. The draft prompt plus: "Keep the same composition and colours as the reference image, at higher fidelity and sharper detail."

### waveform

Behind the proof section, on the right, faded under the text.

- Draft: `1536x864`, quality low. Prompt: "Abstract stepped digital waveforms, like a logic analyser trace of a clock and data lines, thin pale phosphor-green lines (#ddffdc) on pure black, a single lime (#7fee64) trace, generous empty space on the left, very clean, no text, no numbers"
- Final: 1536x864, quality high, the draft passed as the reference so the composition held. The draft prompt plus: "Keep the same composition and colours as the reference image, at higher fidelity and sharper detail."

### traces

A faint texture behind the machines section.

- Draft: `1024x1024`, quality low. Prompt: "A subtle seamless texture of fine printed-circuit traces and vias in very dark green on pure black, extremely low contrast, evenly lit, no focal point, no text"
- Final: 1024x1024, quality high, the draft passed as the reference so the composition held. The draft prompt plus: "Keep the same composition and colours as the reference image, at higher fidelity and sharper detail."

### board

Not used.

- Draft: `1536x864`, quality low. Prompt: "A dark 1970s single-board computer seen from above at a slight angle: six red-orange seven-segment LED digits glowing in a row above a hex keypad of small square keys, black background, soft phosphor-green edge light (#ddffdc), one lime (#7fee64) glint, macro photography, shallow depth of field, no text, no logos, no letters on the keys"
- Final: not made, dropped: it is not a KIM-1 (twelve keys where the board has twenty-four) and reads as one

## The photographs

Every machine page shows a photograph of the original device in its head,
beside the machine's details, and credits it underneath (issue 28; Dan,
2 October 2026). Rights are not a gate; the credit is. A machine may have
several (Dan, 2 October 2026: "cross-reference multiple photos"): the first in
its registry list is the main one, in the head, and a section of its own,
"Photographs of the original", below the model shows every one, main one
first, each with the same credit and a line saying what the 3D model took
from it. The photographs live in
`src/assets/photos/`, apart from the generated imagery, and the two folders
never share a file.

- **A photograph is evidence and is captioned as one.** The caption starts
  "Photograph:", says what and where it is and when it was taken, then credits
  the author, links the source and gives the licence as the source states it,
  or "no licence stated". All of it comes from the machine's `photos` list in
  `machines/registry.json`, so it cannot be left off. A test fails if a
  photograph is captioned or alt-texted as an illustration, and the
  "Illustration" rule above still binds every generated image.
- **Its alt text describes the board**, not the fact of a photograph: what is
  where, so the picture is readable without being seen.
- **It is served from this site**, never hot-linked: AVIF at three widths with a
  WebP fallback, built by `astro:assets` from a 1600 pixel master. The img
  carries its width and height, so the page does not shift when it arrives.
- **A hairline frame and the iron surface behind it,** like every other panel:
  no shadow. The photograph sits on black because the source does, which suits
  the canvas.
- Below 760 pixels it drops under the details, no wider than 360 pixels.
- **The photographs section is a plain grid** of the same figures, as many
  columns of at least 220 pixels as fit, on the page's narrow measure; the
  head's photograph and its copy in the grid ask for the same widths, so it is
  fetched once. Every image in the grid loads lazily.

## The KIM-1's 3D model

A model of the board, below the program on the KIM-1's page, in three.js on the
same black canvas, using the stage every machine's model shares
(`src/models/stage.mjs`).

- **It is labelled a model, twice.** A tag in the corner of the canvas says
  "Model, not a photograph", and the caption, which is also the text
  alternative, starts with the same words and says what was measured and what
  was not.
- **Its colours are the board's, not the site's.** The page's own drawn display
  stays phosphor green, because it is a drawing of what the machine shows. The
  model is a model of a thing, so its board is green fibreglass, its contacts
  gold, its tracks copper, its legs tinned and its LEDs red, and its solder side a shade lighter
  than the top so the underside reads when the camera is below. Since the
  model was measured from several photographs it also has the parts those
  photographs show: the white ceramic of the 6502 and one 6530, the cream
  capacitors, the tan resistors and the red wire across the Musée Bolo's
  board. Those eleven colours are tokens (`--model-pcb`, `--model-pcb-under`,
  `--model-copper`, `--model-gold`, `--model-tin`, `--model-led`,
  `--model-led-off`, `--model-ceramic`, `--model-capacitor`, `--model-resistor`,
  `--model-wire`), used by the model's scene and nowhere else, and none is a
  text colour. The chips, keypad, keys and printing use the site's own iron,
  black, white and moss.
- **No lime.** The model spends none of it; the focus ring on the model is the
  lime, as everywhere.
- **Its two text colours,** the corner tag (`--white` on iron) and the status
  line (`--moss-80` on black), are in the contrast table in
  `tests/design.test.mjs`.
- **The controls are the usual ones and never trap the page.** Left-drag turns
  it all the way round, under the board as well as over it; right-drag, or
  Shift and left-drag, pans, with the target held inside the board's bounds
  plus two centimetres. The wheel zooms to the cursor only while the model has
  focus (click it, or Tab to it) or with Ctrl or Cmd held; otherwise it scrolls
  the page. On touch, one finger scrolls the page until a tap focuses the
  model; then one finger turns it and two pinch and pan, and the canvas is
  `touch-action: none` only while focused. A double click or double tap on
  empty space resets the view, as does the button. The keyboard: arrows turn,
  Shift and arrows pan, plus and minus zoom, Home resets, Escape lets go.
- **The focus ring is the lime, and it shows for a mouse click too,** because
  focus is what hands the model the wheel, so the visitor can see who has it.
  The hint on the canvas (`Click the model, then scroll to zoom`, or its touch
  form) is iron with white text and goes while the model has focus; the full
  list of controls is the paragraph under the model, in `--moss-80` on black,
  and the keyboard half of it is also the canvas's `aria-description`. Both
  text colours are in the contrast table in `tests/design.test.mjs`.
- **It never moves by itself.** No auto-rotation, and with reduced motion the
  camera and the keys move at once instead of easing.
- **It loads late.** The section is hidden in the markup and shown by a small
  loader, which fetches the model and three.js only when the section nears the
  screen or the visitor presses "Load the 3D model". Without JavaScript the
  section never appears, and the photograph and the drawn keypad are the page.
- **The board carries its real copper tracks, on both faces** (Dan, 2 October
  2026: "on the 3d model i would like to see the circuit board tracks", then
  "cross-reference multiple photos"). They are traced from photographs, not
  drawn, into one map (`src/assets/tracks/kim-1.webp`) whose channels are kept
  apart by source: the top face from three photographs, the top face under the
  parts from a replica of the layout, and the underside from its one
  photograph. The model turns each face's copper into colour, shine and relief:
  copper `--model-copper`, a warm olive gold a little lighter than the mask,
  smoother and slightly metallic, standing proud through a bump map; the mask
  stays the matte `--model-pcb`, or `--model-pcb-under` below. No glow: nothing
  on either face is emissive. The map and the parts are credited under the
  model, source by source, with what was taken from each.
- **How it was made is said under it, in words with measured figures.** A short
  note, "How the model was made", says what was cross-referenced, how well the
  photographs register, how well the tracks agree before and after, how the
  heights were measured, and what the model still does not show. Every figure
  in it is read from `src/data/kim-1-model.json`, which the analysis writes.
- **Show tracks and Show tracks only** are two toggle buttons beside Reset the
  view, in a group named Tracks. They are the site's ordinary small buttons; a
  pressed one is `--white` on `--veil` with a filled square before its label,
  an unpressed one an empty square, so the state is a shape as well as a
  colour, and `aria-pressed` says it to a screen reader. Show tracks starts
  pressed. Show tracks only fades the parts out over 0.3 seconds (at once with
  reduced motion) and leaves the board and its tracks; turning the tracks off
  brings the parts back, and Show tracks only turns them on. No lime: the
  buttons spend none of it. The pressed colour pair is in the contrast table.

## The chip page

`/inside/` is a 3D diagram of the chip, drawn in three.js, on the same black canvas.
The scene's colours are the site's tokens, read from the stylesheet when the
page starts, so nothing in the script holds a colour (a test checks that). The
lime is used for the one pulse colour and the register flash, which are lights
in a scene and not a fill, and the page keeps its one lime button, Play.

It is a diagram and says so. The only public layout of the real die is
share-alike and non-commercial, so the geometry is drawn by hand after the
floorplan in outline. See the journal entry for 1 October 2026.

## What the tests hold

`npm test` builds the site and then checks it. The checks that guard this design
are in `tests/design.test.mjs` (contrast, the lime, shadows, raw colours) and
`tests/site.test.mjs` (captions and alt text for every file in
`src/assets/imagery/` and every photograph in `src/assets/photos/`, the dash and
British English rules), `tests/model.test.mjs` (the model's colours are tokens,
its label, its lazy loading) and
`tests/honest-pages.test.mjs` (the backgrounds' captions). The rest of the suite is about the figures and the
content.
