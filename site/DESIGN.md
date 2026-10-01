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
page uses no image at all: its digits and keypad are drawn, and driven by the
emulator.

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

## What the tests hold

`npm test` builds the site and then checks it. The checks that guard this design
are in `tests/design.test.mjs` (contrast, the lime, shadows, raw colours) and
`tests/site.test.mjs` (captions and alt text for every file in
`src/assets/imagery/`, the dash and British English rules) and
`tests/honest-pages.test.mjs` (the backgrounds' captions). The rest of the suite is about the figures and the
content.
