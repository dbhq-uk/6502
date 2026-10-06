# 6502.dbhq.uk

The site for this repository: a journal of the work, the table of every 6502
machine, and the count of the ones that run. Astro, static, deployed to
Cloudflare Pages.

```sh
cd site
npm ci
npm run results     # runs the whole test suite and writes src/data/results.json
npm run machines    # builds every machine's WebAssembly and copies in their ROMs, into public/machines/
node scripts/build-machines.mjs bbc-micro   # the same for one machine by id, or several: kim-1 bbc-micro nes
npm run dev         # http://127.0.0.1:4333/
npm test            # builds the site, then checks it
npm run browser-check   # runs the KIM-1's, the BBC Micro's and the NES's pages in headless Chrome, after npm test has built dist/
node scripts/page-weight.mjs /machines/kim-1/   # what a page loads when it opens, after a build
node scripts/measure-kim1-photo.mjs <photo>     # the KIM-1 board's scale and size, off its main photograph's full-size original
```

The KIM-1 model's measurements are made offline, in Python, by
`tools/kim1-model/` at the repository root (its README says how to run them);
their outputs are committed and the build never runs them.

Needs Node 22.22 or later (`engines` in `package.json`). `dev` and `preview` serve
on `127.0.0.1`. To reach them from another machine, set `SITE_HOST` to the address
to bind, for example `SITE_HOST=0.0.0.0 npm run dev`. Nothing here is tied to one
network, and no private address is committed.

`npm run results` takes a few minutes and downloads about 5 GB of test data the
first time. `src/data/results.json` is generated and never committed. CI
produces it from the same test run that gates the merge.

`npm run browser-check` also checks the photograph and the 3D model: the
photograph loads, the model's bundle is not fetched until the page is scrolled
to it, its canvas draws, its digits match the page's after the program, the
page's 1 key puts the model's 1 down, and a click on the model's 2 presses 2 on
the machine. It also drives the model's controls: a left-drag turns it and a
right-drag pans, the wheel scrolls the page until the model is focused and zooms
it after, Escape lets go, the camera goes under the board and draws it, panning
stays inside the bounds, the distance limits hold, a double click on empty space
resets the view, the keyboard works, the copper tracks are on to start with and
show (a part-free stretch of the board has more colour variance with them than
without), Show tracks and Show tracks only work from the mouse and the keyboard
and hide the parts so a hidden key cannot be clicked, from below the underside's copper shows where the
track map puts it (and not where a mirrored map would), every photograph in the photographs
section loads from this site, and in an emulated phone one finger scrolls
the page until a tap focuses the model, then turns it, and two fingers pinch. It runs Chrome with SwiftShader, its software WebGL, so it needs no
GPU.

For the NES (`scripts/browser-check-nes.mjs`) it checks that nothing is
fetched before Start; that Start puts the bundled game in; that, run from
power on to the frame `machines/nes/expected-frames.json` records, the canvas
hashes to the recorded SHA-256 in each region (the canvas is cleared before each run, so a run that does not paint fails), with the line under the region
control changing when it is switched; that Start pressed on the keyboard
changes the picture; that the headroom line appears and the sound turns on and
off; that a tiny test cartridge it builds from bytes, never a game, goes in
through the picker and turns the screen its colour; and that a text file and a
file over the size limit are each refused with a sentence while the machine
carries on.

`npm run machines` needs the .NET 10 SDK with the `wasm-tools` workload
(`dotnet workload install wasm-tools`), because the machines are compiled ahead
of time; it takes a few minutes a machine. It writes `public/machines/<id>/`,
which is git-ignored: the published WebAssembly and the machine's ROMs, read
from `roms/` at the repository root and checked against the SHA-256 pinned in
`tests/Dbhq.Cpu6502.TestSupport/Pins.cs`, every ROM before anything is
published. For the BBC Micro it also writes `discs/<slug>.ssd`, each preset disc
listed in `machines/bbc-micro/discs/manifest.json`, checked against the
manifest's size and SHA-256 before the publish; the discs' licences and source
stay in the repository, where the page links to them. For the NES, which has no
system ROM, it writes the bundled game, `Lan_Master.nes`, checked against its
pin the same way. `npm test` fails if any machine's are missing. The machines it knows,
with their projects and ROMs, are `MACHINE_BUILDS` in `src/lib/machines.mjs`. `npm run browser-check` needs
Google Chrome (`CHROME_PATH` to use another); it serves `dist/` on `127.0.0.1`
with the site's own CSP and closes the server when it is done.

## Where everything comes from

Nothing on the site is typed twice. The repository is the source.

| On the site | Comes from |
|---|---|
| The journal | `docs/journal/*.md`, read at build time. Each entry has `title`, `date`, `summary` and `order` in its front matter, all required; `order` is the position within a day, and a tie on both is broken by file name |
| The family page | `docs/the-6502-family.md`, rendered unaltered |
| Known differences on the Status page | `docs/known-differences.md`, rendered unaltered |
| The machines and chips tables, and the count | `machines/registry.json` |
| Tests passing, variants, suites | `src/data/results.json`, made from the test run |
| The speed figures | `src/data/measurements.json`, made by `bench/collect-measurements.mjs` and committed as a dated record |
| A running machine's page | `src/pages/machines/[id].astro` with the machine's panel (the KIM-1's is `src/components/Kim1Panel.astro`, driven by `public/kim-1.js` on the shared host `public/machine-host.js`, which loads the WebAssembly, runs it in step with real time and says how fast the browser runs it), its "try it" program from `machines/<id>/try-it.json`, which its acceptance test also runs, and its rights from the registry |
| A machine's photographs | `src/assets/photos/<file>`, each named in the machine's `photos` list in `machines/registry.json` (main photograph first) with its author, source, licence (as the source states it, or `null` for none), date taken, the day it was fetched, alt text and what the 3D model took from it. `src/components/MachinePhoto.astro` shows the first in the page's head, and `src/components/MachinePhotos.astro` shows every one in a photographs section, each with a credit built from those fields. At least one is required of every running machine: the build fails without one. Where each came from, with its SHA-256, is in `src/assets/photos/README.md`, and a test checks the hashes. A drawing the model was measured from (a replica of the board's layout) is in the machine's `drawings` list and credited under the model |
| A machine's 3D model | `src/models/<module>.js`, claimed in the machine's `models` list in `machines/registry.json` (a view, `board`, `outside` or `inside`, and the module) and listed in `src/models/models.mjs` (which says how to add the next), on the stage every model shares (`src/models/stage.mjs`). The KIM-1's board is measured by `tools/kim1-model/` from five photographs of two real boards and a replica of its layout: the parts, places and heights it writes are `src/models/kim-1-parts.mjs`, shaped for the model by `src/models/kim-1-layout.mjs`; the copper of both faces is `src/assets/tracks/kim-1.webp` (its licences are in `src/assets/photos/README.md`); and the figures the note under the model quotes are `src/data/kim-1-model.json`, which `src/models/kim-1-notes.mjs` turns into words. The build never runs the analysis. `scripts/build-models.mjs` bundles each model into `public/models/<id>.js`, and copies its track map to `public/models/<id>-tracks.webp` (both git-ignored), before every build, and `public/model-loader.js` loads the bundle, which fetches the map, only when its section nears the screen |
| A machine's WebAssembly and ROM | `public/machines/<id>/`, made by `scripts/build-machines.mjs` and never committed (the ROM it copies in is committed, under `roms/`) |

**A machine counts as implemented only when it runs in the browser and passes an
automated test in CI.** In the registry that is `status: "running"` with an
`acceptance` field naming a test suite, and the build fails if that suite is not
in the results, or did not pass with nothing skipped.

## Adding a machine

Add it to `machines/registry.json` and to `docs/the-6502-family.md`; a test fails
if the two disagree. It starts as `planned`. When its own spec is built and its
acceptance test passes, set `status` to `running` and `acceptance` to the test
class. Its page at `/machines/<id>/` is generated from the registry by
`src/pages/machines/[id].astro`, and the machines table then links to it; there is
no page to write for the text. What the page cannot have without work is the
machine itself: a running machine needs a panel in `[id].astro` that runs it,
and the build fails without one, because "running" means it runs in the browser.
It also needs at least one photograph of the original: add the file to
`src/assets/photos/`, an entry in its `photos` list in the registry and its
provenance, with the SHA-256 of the file as committed, to that folder's README,
and the page shows it, credited. A 3D model is optional and per machine: see
`src/models/models.mjs`.

## The checks

`npm test` runs these, in `tests/`:

| File | Holds |
|---|---|
| `registry.test.mjs` | The registry is valid, agrees with the family document, and a running machine has a passing acceptance test and a photograph that names its author and links its source; a photograph's file, licence, date and alt text are checked, every photograph on disk belongs to a machine and is written up in its folder's README; a machine's `case` and `models` (both optional: no machine is required to claim a model) follow the rules in `src/lib/registry.mjs`, each tried on a made-up registry: views from `board`, `outside` and `inside`, each once, a module named for its machine and claimed once in the registry, built (its module exports `mount(root)` and its results file is there), no outside or inside without a case and no bare board with one, and a cased machine that claims a model claims both; a `references` entry is credited like a drawing and names the original's SHA-256 |
| `results.test.mjs` | The test results are read correctly, are publishable, and the real results file passes |
| `measurements.test.mjs` | The benchmark output is parsed correctly, and a machine is called physical only when `systemd-detect-virt` ran and said `none` (a missing tool is "a machine of unknown type") |
| `figures.test.mjs` | The figures are computed from the registry, the results and the measurements, a missing suite (or no Harte suite) stops the build instead of publishing 0, and the target verdict never rounds up |
| `figures-on-pages.test.mjs` | Every number on the home and status pages is a generated figure, the pages say honestly whether the speed target is met and that the figures are one collection of runs, and the results file names a full commit |
| `honest-pages.test.mjs` | The pages claim nothing they cannot back up: the home page names exactly the machines that run, from the registry, no page says the reference data came from a real chip, no test cadence is asserted, every generated image used as a background is captioned |
| `machines.test.mjs` | The machines and chips tables match the registry; only a running machine is linked (rules tested on a made-up registry as well as the real one); the filters are hidden until the script runs; the sort comparison puts blanks last; the family page renders the repository document |
| `mirrors.test.mjs` | Every file the tests and the build fetch comes from a `dbhq-uk` repository, and every pin is a full commit (AGENTS.md rule 3) |
| `machine-page.test.mjs` | The KIM-1's page, built against the real registry: it is linked, its WebAssembly and both ROM halves were built in and the ROM matches its pins, the keypad has every key once in the board's layout, it degrades without JavaScript, the digits have a polite live summary, the program on the page is the acceptance test's own, it states the registry's rights text and the pinned sources, both workflows build the machine before the site, and it shows the registry's photograph from this site, alt-texted, sized and credited (author, linked source, licence as stated) |
| `machine-host.test.mjs` | `machine-host.js` is run against a fake .NET runtime and a fake machine, in a fake browser whose clock and frames the test turns: it loads the runtime from the page's base and the machine class from its assembly before running anything; each frame runs the real time since the last at the clock, capped at a tenth of a second at 1 MHz and at 2 MHz; `onFrame` gets the cycles the machine really ran; the capacity and actual speed land on the panel and the three headroom sentences are the KIM-1 page's; a failed load says the machine could not start and runs nothing; `stop()` ends the loop; a hidden page is paused and its time away neither run nor counted; `load` is given the runtime as well as the machine, and `onPause` and `onResume` are called when a hidden page pauses and comes back, never at the first start; the clock can be a function read every frame (the NES's changes with its region), and `hold()` stops the loop until let go, through a hidden page shown again, and cycles run while held are neither the next frame's nor in the speed |
| `bbc-micro.test.mjs` | The BBC Micro page's parts without a browser: the key table is the machine's `BbcKey`, every BBC key is pressed by a PC key or listed with a reason, Tab, the function keys, Alt and the Command key are never taken; every key is on the on-screen keys exactly once, with the machine's legend, and a tap presses it while SHIFT and CTRL latch for one key; the characters come from the OS ROM's key table; the page script, played made-up key events, maps by `event.code`, leaves browser shortcuts alone, ignores repeats, holds a BBC key while either of two PC keys is down, lets go of everything on blur, and takes Pause as BREAK; the sound worklet waits to fill, plays round zero, fades to silence when it runs dry, cuts a long queue back and flushes; the left-out parts and their issues, the try-it file, and the registry preview; the disc drive, driven with a fake page and a download held open: Insert and run puts the library disc in, starts it with SHIFT and BREAK and brings the screen into view, Insert in drive 0 starts nothing, and the latest insert wins, so a file or blank disc chosen while a library disc downloads is not replaced by it, and a superseded read that fails leaves the status line alone |
| `nes-panel.test.mjs` | The NES panel's parts without a browser: the button bits are the machine's (`Controller.cs`), the key map by `event.code` and the gamepad map in the standard layout, Tab, Escape, the function keys, Alt and the Command key never taken; the page script, played made-up key and pointer events, holds a button while its key or finger is down, sends Left and Right together unfiltered, ignores repeats, lets go on blur and lets go of the screen on Escape; the pads go by index; the region shown is the one the host reports after `Load`, never worked out in the page, the line under the control is the host's sentence, a change of region says the machine restarted, and a bad file or an unmodelled board shows the cartridge's sentence with the machine as it was; the bundled cartridge is tried before the picker opens, so a chosen file is never replaced by it; the picture is shown 8:7 on NTSC and 355 by 240 on PAL; a file over the host's limit is refused unread, and the limit is never typed; the sound worklet's ring admits and drops by its rule, never holds more than its most, keeps the newest and fades when it runs dry |
| `nes.test.mjs` | The NES's registry record and the data its page is built from, without a build: one record, `nes`, replacing the planned `nes-famicom`; the BBC Micro's fields, the design's values, a case and no model claimed; the `cpu` field's clocks are `Region.cs`'s, NTSC first, which is the one the page reads; the rights name no system ROM, leave commercial games to the visitor, and give the bundled game's title, author and licence as `try-it.json` does and the author's words, the archive's address and its hash as `roms/README.md` does; the notes and the page's list name every part left out, each with its issue; every place that gives the number of cartridge boards (the notes, the README, the page, the left-out list) gives as many as `Cartridge.SupportedMappers` in `Cartridge.cs`, so a seventh board cannot leave them saying six; the try-it file's steps and controls, the pad's buttons, and a file in the wrong shape or naming any game but the pinned one, or the pinned name with other bytes, is refused |
| `nes-page.test.mjs` | The NES's page, as the site that deploys builds it: the registry says it runs, its acceptance test passes and it validates; the page is linked from the machines table; the home page's count is one more than with the NES not running, from the registry, and its sentence names it; the page states the acceptance count from the results; the photograph is credited with its author, its source linked and its licence as stated; the driver, the folder and the game it loads, and the clock taken from the machine; the WebAssembly built in and the game matching its pin; Start disabled without JavaScript and saying the download size read from the build, and nothing of the machine named by the page before it; a 256 by 240 canvas with the line that it models the picture; the region control and the sentence about the regions; the picker; every control disabled without JavaScript; that a reload loses battery RAM; that it needs a fast computer, with no speed typed; the left-out parts and the 3D models, each linked to its issue; whose game it is, in the registry's words; and the try-it steps and controls from the boot check's own file, with the keys from the page's map |
| `bbc-page.test.mjs` | The BBC Micro's page, as the site that deploys builds it: the registry says it runs, names its acceptance test and has its photograph; the page is linked from the machines table; the home page's count and its sentence about which machines run are the registry's, naming the KIM-1 and the BBC Micro; the photograph is credited with its author, its source linked and its licence linked to the deed; its driver and ROMs, its WebAssembly built in, Start disabled without JavaScript and saying the download size read from the build, a canvas shown four by three with a line saying it models the picture, sound off until asked, the disc drive, the six left-out parts linked to their issues, the rights text, the try-it program from the acceptance test's own file, the symbol table from the ROM, and the on-screen keys (every key once, real buttons, SHIFT and CTRL as toggles, 44 pixels at least) |
| `bbc-discs.test.mjs` | The BBC Micro's preset discs: the manifest is well formed with exactly one default, in the page's order; every image is in its folder with the manifest's size and SHA-256, whole sectors, 40 or 80 tracks; every disc has its licence text (the licence the manifest names), a README giving its facts and what was changed, and its source when its licence is the GPL or LGPL; a manifest that breaks a rule is refused with a reason; none of the titles the research said to avoid is there, no image or licence comes from a mirror, every file in a disc's folder is listed in its README with its SHA-256 (CP/M-65's lib6502 notice included), and no disc image is committed anywhere else; `_headers` serves the images as plain bytes, and no rule in it has more than one `*`, which Pages would skip; the build publishes exactly the manifest's images under `discs/`, hash-checked, and Start's download does not count them; the build reads the discs before the slow publish and the CI cache is keyed on them; the page's list is labelled, grouped, has the default chosen and is disabled without JavaScript, with Insert and run and Insert in drive 0 not in lime; each disc's credit under the list (licence linked to its text, the author's page, the source for copyleft, the photosensitivity notes first); the visitor's own-file controls are all still there; and the credits section lists every disc and says they are not MIT and commercial games are load-your-own |
| `build-machines.test.mjs` | `scripts/build-machines.mjs` builds the KIM-1, the BBC Micro and the NES by registry id from their own projects; the BBC Micro's ROMs are its pins in `Pins.cs`, in `BbcHost.Load`'s order, and match the files in `roms/`; the NES's one file is its bundled game's pin, an iNES file matching the one in `roms/`; a ROM that is not its pin, or is missing, is refused; an unknown machine or option stops it before any publish; every ROM is read before the first publish; CI and `npm run machines` publish every machine, each with a CI cache of its own keyed on its own projects |
| `model.test.mjs` | The 3D models: the KIM-1 model's keypad is the machine's and the panel's, every part is on the board, the contacts set the scale, it decodes digits with the machine's own table, it holds no colour that is not a token, every model in the map has a module and a bundle inside its budget, the page loads only the small loader and never the model, the section is hidden without JavaScript and has its name, reset button, label and text alternative, the controls are in real text and in the aria-description and the wiring that keeps them from trapping the page is in place (focus decides the wheel and touch-action, Escape lets go), the camera's range and bounds, the lit underside, the double-click reset, the driver exposes the machine and announces every tap, the model never turns by itself, and the deploy checks the bundles; the track map is committed, greyscale, 1536 by 2048, under 400 KB, built beside the bundle and loaded only with it, made into colour, shine and relief from tokens with no glow, on the top face alone; Show tracks and Show tracks only are labelled toggle buttons with `aria-pressed`, wired as described; the map is credited under the model with the photograph's licence and written up in the photographs' README; and the tracing script clears every part the model draws; the map and the registry agree both ways, every model claimed by exactly one machine with the same view, and a page has a model section exactly for the modules its machine claims; every model with a track map (the KIM-1's and the NES board's) has it committed inside its budget, built beside its bundle byte for byte and named on its page for the loader alone; a machine with a case has one model section with a tab list named Views of the model, a tab a view in the registry's order with the first selected and alone in the Tab order, each controlling its own panel with its own stage, reset button, status line, Load button, caption and note, the controls said once after the panels, and every id on the page once; the selected tab is white on veil with a bar under it, with no lime and no transition |
| `model-loader.test.mjs` | `public/model-loader.js` on a made-up page with a stand-in for the DOM: it shows the views' section, leaves a hidden panel hidden and never loads it until its tab is chosen or its Load button pressed, and loads each view once; the tabs select on a click, Enter and Space, Left and Right move and select and go round the ends, Home and End go to the ends, only the selected tab is in the Tab order, and Tab is never taken; the loader stays under 4,000 bytes and imports nothing when the page opens |
| `model-regions.test.mjs` | A model that draws more than one console: its words are read through `wordsFor`, one set for a model with no `regions` and one for each region for a model with them (the first by default, an error for a region it does not draw), `regionProblems` says what is wrong with a model's regions (empty, repeated, a missing label, caption or note, a stray key, a name that is not a lower-case word) and passes every model in the map, `regionViews` gives the page one view for each console with all but the first hidden and ids kept apart, and a model with one console gives the one view it always did; on the NES's built page, in each model's panel, each console has one caption, one note and one hidden accessible name, only the first console's shown, their ids carrying the view and the console, with every id on the page used once |
| `bbc-models.test.mjs` | The BBC Micro's models' inputs and what they rest on: every input in `tools/bbc-micro-model/data/sources.json` is fully described, and a scan or an input with no stated licence is never committed; their hashes are the ones in `docs/bbc-micro/facts/models.md`; no file under `tools/`, `src/assets/`, `public/` or `docs/` is an original; the verdicts in the spike's results are what its figures give against the plan's thresholds, and they pass the rows as revised on 4 October 2026 (the case's front edge on the key-plane registration is recorded, not judged, and the solder side's largest error is recorded); the build never runs the tools |
| `nes-models.test.mjs` | The NES's models' inputs and what they rest on: every input in `tools/nes-model/data/sources.json` is fully described, and a scan or an input with no stated licence is never committed; OpenTendo's inputs are read from the `dbhq-uk` fork at the pinned commit; their hashes are the ones in `docs/nes/facts/models.md`; no file under `tools/`, `src/assets/`, `public/` or `docs/` is an original; the verdicts in the spike's results are what its figures give against the plan's thresholds, and the PAL front's judged figures are O4 against O2-FL; the build never runs the tools; the board's frame (task 2): its recorded verdicts are what its figures give against the plan's x, y and x against y rows, and its figures what its rows give, and all three pass; its outline is closed, starts at the origin, is the board's size and is within 2 mm of the KiCad redrawing each way; its holes are inside the board, and its rectified copy is never committed; the solder side's registration (task 3): it passes the plan's solder row on the figures without exclusion over at least 150 holes, its recorded verdict is the one its figures give, and the excluded holes are exactly those the roundness rule names; every drill has a pad on both faces within 0.2 mm; U1 to U10 each have one DIP footprint with the pin count the plan's Facts give, pin 1 first, each reference marked by hand; P1 has 72 fingers, 36 on each face; the parts (task 5): every IC on a footprint whose pad count is its pins, at its pads' centre; each console's part is `ic-table.json`'s, the crystal's too; every IC has a `chip` (one of `COUNTED`) or an `always`, U7 `pad1` and U8 `pad2`, inferred; every part inside the outline, a body over it recorded; no two IC bodies overlap; the places within 3 mm of the KiCad redrawing's after a best-fit similarity; every CPU-07 part on the CPU-10 footprint of its name; the generated parts module agrees with `parts.json`; the inside model on the page (task 7): the NES claims it, with no `case` until the outside comes; its regions are the page's region control's, both ways; it draws every part in the parts file on the board's outline and holes, with no sockets; its track map is `copper.json`'s, read red for the component side, green for the solder side and blue for the print, and its access mark is a token of its own, not the lime; the legend lists every IC once in reference order with both consoles' parts from `ic-table.json`, a rate cell for each counted chip, and words for what a mark means, why some chips are never marked, U9's jobs by the redrawing's nets and U7's and U8's ports by the print, checked and not traced; each console's caption says it is a model of that console and its figures are the layout's; the results file agrees with `frame.json`, `registration.json`, `copper.json` and `parts.json`; each console's note is `made()`'s, word for word, with the nets check's failure as `copper.json` has it; every sentence about the copper says it is traced to look at and not verified; every source of the board is credited once by its address, and nothing else; the model reads `panel.nes`, takes a fresh baseline on `nes:start` and `nes:region`, and switches every console's words together |
| `nes-spike-verdicts.test.mjs` | The NES models' task 0 verdicts, from `nes-spike-verdicts.mjs`, on made-up figures: inside every pass limit every check passes; each check stops on its own figure and on no other; between the limits is neither, and too few points is never a pass; the PAL front passes only on at least two measured ratios; the case passes within 5 per cent and stops over 8 (the rows as revised twice on 5 October 2026: the case on the design patent's views, with limits wide enough for its sources' disagreement) |
| `nes-famicom-access.test.mjs` | The NES board model's marks and rates from the machine's access counters: a chip whose count moved is marked with its accesses per second; a counter that wraps past 2^31 or 2^32 gives the right difference, in the page's Int32Array as in a plain list; a snapshot of the wrong length or no time between two is refused; the sampler divides by the time it measured, not the time it waited; after a reset, and before the machine has counters, it takes a fresh baseline and shows no rate |
| `journal.test.mjs` | Every journal entry has its front matter, is built once with its own title, and is listed newest first; a link to another entry is a site link and a link to any other file goes to GitHub |
| `site.test.mjs` | Every page has a title, description and canonical link (the 404 has none and is `noindex`), no mention of the dropped port goal, one `h1` and its landmarks; no dashes or forbidden names; British English; no inline script; every internal link resolves; every image in `src/assets/imagery/` is captioned and alt-texted as an illustration, and every photograph in `src/assets/photos/` is captioned as a photograph and credited, never as an illustration; the lime fills one element and its other uses are named |
| `analytics.test.mjs` | `analytics.js` and `consent.js` are run in a sandbox with a fake browser: GA4 uses the estate's one ID, is analytics-only, loads only on the live host and for no likely bot, is never loaded for a visitor who opted out, and an old refusal is carried over; the notice shows once, Cookie settings reopens it, and OK and Opt out are the same weight; the CSP allows only what it needs |
| `design.test.mjs` | Contrast from the real tokens, on the black canvas, the cards and the worst pixel of the traces texture; no raw colours in the stylesheet; no shadows but the navigation bar's |
| `chip.test.mjs` | The chip page: 40 pins and every bus line has a pad and a wire; every frame is exactly what the core recorded, and registers change on an instruction's last cycle; the page calls itself a diagram and not the die, its figures are the trace's, and its script holds no colour of its own; the bundle is built and carries its licences |
| `html.test.mjs` | Every built page is valid HTML |
| `build.test.mjs` | The pages, the sitemap, `robots.txt` and the 404 page were built, and the sitemap lists exactly the pages that exist; the deploy checks every script the pages load; both workflows hold the same test floor; no local path or private address is published; this table lists every test file |

CI reads the number of passing tests back and fails below a floor, in `validate.yml`
and `deploy-site.yml`. The floor is the same number in both and must be raised when
the suite grows.

`DESIGN.md` records the look, including the fork from the DBHQ brand, and how
each image was made.

## Analytics and Search Console

GA4 uses the estate's one measurement ID (`G-3H3NFGSX85`) with no data stream of
its own, and runs by default on `6502.dbhq.uk` only, with a notice and a simple
opt-out. That is the pattern every other DBHQ site moved to on 30 September 2026,
under the PECR statistical-purposes exception: analytics only, so ad storage is
denied and Google Signals and ad personalisation are off. This site launched on
1 October 2026 with an opt-in modal that loaded nothing until a visitor
accepted, and moved to the notice the same day.

The choice is a cookie, `dbhq_analytics=on|off`, on `Domain=dbhq.uk`, so opting
out here opts out on every DBHQ site. An answer left under the old prompt (the
`dbhq-consent` key in `localStorage`) is honoured and moved to the cookie on the
first page load. A visitor who opted out loads no Google script at all. The notice is
not a modal. It shows until answered, offers OK and Opt out at the same weight,
and links the estate's privacy policy. "Cookie settings" in the footer reopens it
on every page.

`public/analytics.js` and `public/consent.js` are the estate's code with this
site's host and look. They are external files because the site's CSP allows no
inline script. `tests/analytics.test.mjs` runs both in a sandbox rather than
reading their text.

The Search Console property for the host sits under the `sc-domain:dbhq.uk`
roll-up. The estate's own record of both is in the private company repository.
