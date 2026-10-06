---
title: "The NES's WebAssembly host and its panel"
date: 2026-10-06
summary: "The NES now has what its page will run: a WebAssembly host in the BBC Micro's shape, a panel with the picture in the region's own pixel shape, the region control and the sentence saying which region was chosen and why, a cartridge picker that refuses a bad or oversized file and keeps the machine as it was, the keyboard, gamepads and an on-screen pad for the two controllers, Reset and the power switch, and sound through a worklet whose ring never grows. The shared host took two small additions, and the KIM-1's and the BBC Micro's browser checks pass unchanged. Task fifteen puts the panel on the page."
order: 32
---

# 6 October 2026: the NES's WebAssembly host and its panel

Task 14 of the NES plan. The machine has run in the tests since task 3, and in a
browser only as the speed bench's bare `Run`. This task gives it the rest of
what a page needs: a host class with the picture, the sound, the controllers,
Reset and the power switch, and a panel that uses them. Task 15 puts the panel
on the machine's page, builds the WebAssembly into the site and extends the
browser check, so nothing here is on the live site yet.

## What was built

- **`NesLoader`**, in the library: `RegionOf(rom)` says which region a file's
  header names, `"NTSC"`, `"PAL"` or `""`, and `Load(rom, region, options, out
  why)` builds the machine in the region chosen, switches it on and gives one
  sentence saying which region and why. Both throw the cartridge's own
  sentence for a bad file. Tested in `NesLoaderTests` without WebAssembly.
- **`Nes.RunFrames(n)`**: runs whole instructions until n more frames have
  completed, which is what the boot check's loop does.
- **`NesHost`**, the WebAssembly class, in `BbcHost`'s shape: `RegionOf`,
  `Load(rom, region, sampleRate)`, `MaxRomBytes`, `Run`, `RunFrames`, `Cycles`,
  `Frames`, `CpuHz`, `SetButtons`, `Reset`, `PowerCycle`, `Picture`, `Sound`
  and `DropSound`. The picture and the sound are handed to the page as views of
  the machine's own memory, through `picture` and `sound` functions the page
  registers in the module `nes`, as the BBC Micro's are.
- **The panel**: `site/src/components/NesPanel.astro`, `site/public/nes.js`,
  `site/public/nes-keys.js` and `site/public/nes-audio.js`, with its styles in
  `global.css`, and `site/tests/nes-panel.test.mjs` for its pure parts.
- **The bench**: `bench/nes-speed/main.js` passes the region by name, as `Load`
  now takes it, and prints the sentence `Load` returns.

## Decisions

**The region sentence is the machine's.** `NesLoader` writes it, because it is
the code that knows what the header said and what was chosen. The page shows it
as it stands, under the region control. Writing it in the page script as well
was the other choice, and two copies of the same five sentences would drift.
The cases: the file says PAL (or NTSC) and nothing was chosen; the file does
not say, so NTSC; a choice the file agrees with; a choice against the file,
which runs and says the game may run at the wrong speed (the plan's Review
Focus 2); and a choice where the file does not say.

**A bad file leaves the machine as it was** (Ruling J). `RegionOf` asks for the
board as well as reading the header, so a file with an unmodelled mapper is
refused before anything changes, and `Load` replaces the running machine only
once the new one is built. The page calls `RegionOf` first and `Load` second,
and catches both.

**The cycle count never goes back.** The shared host takes each frame's cycles
as the difference between two readings of `Cycles()`. A power cycle sets the
bus's count to 0, and a new cartridge is a new machine, so `NesHost.Cycles` is
the cycles since the first `Load`, carried across both. The other choice was a
reset hook in `machine-host.js`; this one keeps the shared host as it was.

**The shared host took two additions, both optional.** `clockMhz` may now be a
function, read every frame: the NES's clock is its region's, and changing the
region changes it without restarting the .NET runtime (a second
`startMachine` would load a second one). And `startMachine` now returns
`hold(on)` with `host` and `stop`: it stops running the machine until it is let
go, and a hidden page shown again stays held. The NES's test hook needs it. The
KIM-1 and the BBC Micro pass a number and never call `hold`, so nothing they do
changed, and their browser check passed with no edit (below).

**The picture's shape follows the region** (Ruling H). The PPU's picture is 256
by 240. NTSC's pixels are 8:7 (`ppu.md` 10, from the nesdev wiki's Overscan
page), so the canvas shows 256 pixels as wide as 292.6 square ones, about
1.22 to 1. The plan's "8:7, which makes 4:3" is true of the whole NTSC line,
280 pixels with its borders (280 times 8/7 is 320, and 320 by 240 is 4:3), not
of the 256 the PPU draws, so the page shows 1.22 to 1 and the test checks that.
PAL's pixels are about 1.3862 to 1, so 256 of them are as wide as 355: the
canvas shows 355 by 240. The script sets the canvas's `aspect-ratio` from
`pictureShape(region)` whenever a machine is built, and the stylesheet's own
value is NTSC's, for the page before the script runs. `image-rendering:
pixelated` keeps the pixels square-edged when they are scaled up.

**The gamepad mapping.** Standard layout, by index: button 0 (the bottom face
button) is A and 2 (the left one) is B, so B sits left of A as on the NES pad;
8 and 9, the middle pair, are Select and Start; 12 to 15 are the d-pad. The
other common choice puts A on the right face button (1) and B on the bottom
(0), which keeps the pair on a diagonal; the brief named 0 and 2 and the
side-by-side pair matches the pad. The first gamepad in the browser's list is
controller 1 and the second controller 2; a third does nothing. A pad the
browser does not know the layout of is read the same way, and the panel says
the layout is the standard one. The analogue sticks are not read.

**Controller 1 is three sources joined.** The keyboard while the screen has
focus (the arrows, Z for B and X for A, Enter for Start and the right Shift for
Select, by `event.code`), the on-screen pad, and the first gamepad. Their masks
are joined and sent as they are: the page filters nothing, so Left and Right
together reach the machine (the plan's Review Focus 4), as the machine's
`Controller` takes any mask. The masks go to the machine only when they change.
Tab is never taken, and Escape lets go of the screen, so the page never keeps
the keyboard.

**The on-screen pad holds a button while a finger is on it**, several at once,
with pointer capture so a finger that slides off still lifts it, and
`touch-action: none` so a held finger neither scrolls nor zooms. A button
pressed from the keyboard is held for 150 ms, long enough for a game to read
it.

**Sound, and the ring that never grows** (Review Focus 3). The worklet keeps its
samples in one `Float32Array` of 0.15 seconds, allocated once, with the same
prime and limit as the BBC Micro's queue: it plays once 0.04 seconds are
queued, and a burst that would pass the limit drops the oldest down to the
prime. The BBC Micro's worklet keeps a list of the arrays it was posted; the
ring was chosen here because its bound is in memory and not in a count of
arrays, and because the rule, `admit` and `take`, is two pure functions the
test runs. There is no high-pass in the worklet, as the BBC Micro's has:
`SampleBuffer` already applies the console's own filters, so the samples sit
round zero. When the ring runs dry the output falls from its last level rather
than jumping to zero. On the machine's side the buffer already drops its
oldest samples when nobody reads it (task 9), which is what happens while the
sound is off.

**The size limit is the machine's.** `NesHost.MaxRomBytes` is the library's
`Cartridge.MaxFileSize`, and the page compares a file's size with it before
reading the file. The test reads `nes.js` and fails if a limit is typed there.
The limit is known once the runtime is up, which is when the picker is
enabled; a file dropped before Start is told to wait, as on the BBC Micro's
page.

**The bundled cartridge** is the panel's `data-rom`, a file under `data-base`,
fetched while the runtime loads, as the BBC Micro's ROMs are. If there is none,
or it cannot be fetched, the machine waits for the visitor's file: the shared
host does not run a machine until `load` returns, so `load` waits.

**The test hook** (Ruling D). `panel.nes.stepTo(n)` holds the loop, lets go of
every button, switches the machine off and on, runs it until exactly n frames
have completed, and paints the canvas, so the browser check can hash the
canvas's bytes and compare them with `machines/nes/expected-frames.json`.
`panel.nes.play()` lets the loop go again.

**Saving is not built**, and the panel says so in those words: a page reload
loses battery RAM. Reset keeps the cartridge's RAM and Power off and on clears
it (Review Focus 5).

**The CSP needed nothing new.** The worklet is a same-origin module, which
`script-src 'self'` covers, the file is read with `File.arrayBuffer` in the
page, and the Gamepad API needs no permission.

## What the shared host shares, and what it does not

`machine-host.js` runs the machine in step with real time, caps a frame at a
tenth of a second of machine time, pauses a hidden page, and writes the
headroom sentence. That is all the NES takes from it. Everything a machine is
made of stays in its own script: its picture and sound handover (each
machine's module name, `bbc-micro` or `nes`), its input, its files, and its
lines. The KIM-1 and the BBC Micro use neither new hook.

## What was checked, and how

On 6 October 2026, in this worktree:

- `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`: 1,496 passed, none
  failed, 35 of them `NesLoaderTests`, written first and seen failing to
  compile (no `NesLoader`, no `RunFrames`, no `BootCheck.ExpectedHash`).
- `node --test tests/nes-panel.test.mjs` in `site/`: 24 passed, after failing
  first because `public/nes.js` did not exist. The two new
  `machine-host.test.mjs` tests failed first and pass.
- `node scripts/browser-check.mjs`, after `node scripts/build-machines.mjs
  kim-1 bbc-micro` and `npm run build`: the KIM-1's and the BBC Micro's checks
  passed with no edit, with no console error, failed request or CSP violation.
- `node run-in-browser.mjs` in `bench/nes-speed/`, on an ahead-of-time
  publish, in both regions: the bench runs with the new `Load` and prints its
  sentence, "Running as PAL, as chosen here: the file does not say which."
- A scratch check outside the repository (a temporary page with the panel, the
  NES published into `public/machines/nes/` by hand and served with the site's
  CSP, then removed): Start fetched nothing before it was pressed; Lan Master
  started, "Running as NTSC: the file does not say, so NTSC."; `stepTo(120)`
  painted a canvas whose SHA-256 matched `expected-frames.json` on NTSC, twice,
  and on PAL after the region was changed; Enter changed the picture; the
  canvas measured 1.2190 to 1 on NTSC and 1.4792 on PAL; a text file, a file
  one byte over the limit and an MMC5 header were each refused with their
  sentence and the machine carried on; Reset, Power, sound, Tab and Escape did
  what they say; no console error, failed request or CSP violation. Compiled
  ahead of time, the headroom line said "Running at the NES's own 1.79 MHz.
  This browser could run it about 2 times as fast." (load average 2.5).
  Without ahead-of-time compilation it ran at 0.65 to 0.80 MHz and said it
  could not keep up, which is why the site ships the compiled build.

## Surprises

- The plan's "8:7, which makes 4:3" is about the whole NTSC line, not the 256
  pixels the PPU draws; see above.
- A synthetic pointer event has no pointer the browser tracks, and
  `setPointerCapture` threw on it in the scratch check. A real finger cannot hit
  that, but the capture is now tried and its failure ignored, as the button is
  held either way.
- Three things already on the branch failed the site's own checks, none of
  them this task's code: the NES plan entry named how often CI runs, which the
  site cannot verify; the NES speed entry's summary, shown on the home
  page, carried two speed figures the home page cannot generate; and
  `known-differences.md` quoted the wiki's US spelling. Each was reworded
  without changing what it records.
