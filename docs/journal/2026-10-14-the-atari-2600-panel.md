---
title: "The Atari VCS's panel, its WebAssembly host, and the console parts it shares with the NES"
date: 2026-10-14
summary: "The thirteenth task of the Atari VCS: the WebAssembly host gains the picture, the sound, the joysticks, the switches and the region switch, and the panel that drives them is written. The parts of the NES's page a second console needs, the cartridge slot, the region control, the gamepads, the held buttons and the sound, moved into shared modules, with the NES's own tests unedited and passing."
order: 67
---

# 14 October 2026: the Atari 2600's panel and its host

Task 13 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md), done on 15 October 2026, after task 12's review was folded in as its own commit (recorded in [the task 12 entry](2026-10-14-the-atari-2600-bundled-game.md)). New: `site/public/atari-2600.js`, `atari-2600-keys.js`, `site/src/components/Atari2600Panel.astro` and its styles; the shared modules `cartridge-picker.js`, `region-line.js`, `gamepads.js`, `touch-pad.js`, `sound-output.js` and `sample-worklet.js`; the tests `site/tests/atari-2600-panel.test.mjs` and `PagePathTests`; the fault probe `tools/probes/atari-2600/panel-faults.py`. Changed: `Atari2600Host` (`src/Dbhq.Machines.Atari2600.Wasm/Program.cs`), `nes.js` and `nes-audio.js`, the serving check in `deploy-site.yml`, two site tests that place a new style rule, the site's README and its test floor, `region.md`'s open item on the window, and the plan's task 13 file list.

No page shows the panel yet: putting it on the machine's page, the registry row, the photographs and CI's build of the machine are task 14's.

## What moved out of the NES's files, and what stayed

The spec's rule is that shared parts are extracted when a second machine needs them. The plan named four modules; six were made.

| Module | What it holds | Why it is shared |
|---|---|---|
| `cartridge-picker.js` | The size refusal, the read in the tab, the order of the bundled cartridge and the visitor's file, the file button, the page as a drop target, and the fetch of the bundled file | The Atari 2600 needs the same slot with a different limit, which each host gives |
| `region-line.js` | The NTSC and PAL radios: showing the region, and a change that the page may refuse | The NES restarts on a change and the Atari 2600 does not, so what a change does stays in each page |
| `gamepads.js` | The poll by index, the standard layout's buttons through a table, the left stick past a share of the way, and the line that names each gamepad | The NES reads no stick; the line names a "controller" for the NES and a "joystick" for the Atari 2600 |
| `touch-pad.js` | On-screen buttons held while a finger is on them | The NES's controller, and the Atari 2600's joystick and its Reset and Select |
| `sound-output.js` | The sound button and the page's side of the worklet | Not in the plan's list. It could not go in `sample-worklet.js`, which runs in the audio thread |
| `sample-worklet.js` | The ring, its drop rule and its fade, and a function that registers a processor over it; it registers `sample-ring` itself | The plan's. `nes-audio.js` now imports it and registers `nes-sound` over it, as before |

**Stayed in `nes.js`:** the NES's buttons and keys, its keyboard handler, its cartridge and region rules (`loadCartridge` asks `RegionOf` first), the region-change restart and its events, the picture, Reset and Power, and the hooks its models use. The keyboard stayed because the two consoles' keys differ in kind: the NES's only hold, and three of the Atari 2600's switches flip on a press and stay. A shared keyboard would have needed both shapes for one caller each.

**Each moved part was moved, not rewritten.** The code is the same lines in a new file, with a parameter where the NES's was fixed (a selector, a name, a noun). `nes.js` re-exports what its tests import (`firstCartridge`, `readRom`, `sizeRefusal`), so `nes-panel.test.mjs` imports what it did before. `nes-page.test.mjs` reads `nes.js`'s text for the call to `MaxRomBytes`, the fetch of the bundled file inside `run()` and the clock read from the host; each is still there.

**The NES's checks, unedited.** No NES test or check was changed. `nes-panel.test.mjs` passed after the move, all of its cases. The site suite's NES tests that need the machine's WebAssembly built fail on this box, as they did before this task, because the box does not build the machines; CI builds them. The NES's real-browser check (`node scripts/browser-check.mjs`) cannot run here: there is no Chrome on this box. It runs in `Validate` on the pushed commit, unedited, and that run is the evidence that the move changed nothing in a browser; what it gave is in the pull request.

One risk the node tests cannot see: `nes-audio.js` now imports `/sample-worklet.js` from inside the audio thread. A worklet is a module script, so the import is allowed, and the CSP's `script-src 'self'` covers it; the NES's browser check turns the sound on and fails if it does not start.

`deploy-site.yml`'s check that each served script answers gained the six modules, because `site/tests/build.test.mjs` derives the list from what `nes.js` imports and failed until they were there. `sample-worklet.js` is imported only from the audio thread, which that test does not follow, so it was added by hand.

## The host

`Atari2600Host` grew from task 8's speed host into the page's, in `NesHost`'s shape: `Load` (now returning "The board is F8, chosen from the file.", the board's name from `Cartridge.BoardName`; the region is said by the region control, so the sentence no longer names it), `MaxRomBytes`, `Run`, `RunFrames`, `Cycles`, `Frames`, `Lines`, `Region`, `SetRegion`, `CpuHz`, `NominalLines`, `SetJoystick`, `SetSwitches`, `PowerCycle`, `Picture`, `Sound` and `DropSound`.

- **The picture is the region's window**, through `FrameBuffer.ToRgba`, into one buffer made for the larger window, handed to the page's `picture` function as a view. The browser check compares the canvas with the `rgba` hash `BootCheck` records, which is the same window written out the same way, so the two are one rule.
- **`SetRegion` is the bus's `Region` setter**: the palette and the clock change and nothing restarts (region.md section 5; `Atari2600BusTests` already holds that the switch changes nothing else).
- **`PowerCycle` keeps the region and the inputs**, which is what the browser check's hook relies on. `PagePathTests` checks that path headless: a machine built in one region, played with a stick held and every switch pressed, switched to the other region, then let go, set to the recorded switches, powered off and on and run the recorded count, gives `BootCheck`'s recorded frame, both hashes, in both regions. A second case shows one frame short or over is not the recorded frame, which is why the hook runs an exact count.
- **No test runs the host itself**, as none runs `NesHost`: it is browser-only. The site test checks its text instead: every `host.X(` the page calls is a `[JSExport]` of `Atari2600Host`, the limit is `Cartridge.MaxFileSize`, and the picture and the sound go to the module the page registers. The bench's calls are in the list, so it still builds against the host.

## The panel's decisions

- **Keys** as the plan lists them: joystick 1 on the arrows with Space to fire, joystick 2 on W, A, S and D with F; R holds Reset and T Select; C flips the colour switch and 1 and 2 the difficulties. By `event.code`, so by place. Tab and anything with Alt, Ctrl or the Command key are left to the browser, Escape lets go of the screen, and focus leaving it lets go of every held key while the flipped switches stay flipped.
- **Gamepads** by index: the first is joystick 1 and the second joystick 2; the d-pad and the left stick past half way move, button 0 fires, 9 is Reset and 8 Select. "Past half way" is strictly more than half, a choice: the plan's words, read as a test that half exactly does not cross.
- **The switches are shown where they are.** Reset and Select are buttons held while pressed, through the shared held buttons; the three that stay are buttons that flip, each saying its position ("Colour" or "Black and white", "Left difficulty B" or "A"), with `aria-pressed`. A key flips the same state, so the buttons and the keys never disagree.
- **Any combination is sent as pressed** (Review Focus 4): every key, the touch pad and both gamepads are joined with a bitwise or and sent; up and down together, and every input at once, reach the machine as written. A change is sent at once, and the gamepads are polled each frame.
- **The picture's window and shape** come from a table in `atari-2600-keys.js`, which the test checks against `Region.cs` (first line, lines, pixel ratio, nominal lines) and against region.md's figures within 1 per cent. The table is there because the panel's markup needs the canvas's size when the site is built, before any WebAssembly runs. A region change resizes the canvas to that region's window.
- **The PAL suggestion** is the plan's item 13: shown when the region is NTSC and the median of the last thirty frames' lines is nearer PAL's nominal count than NTSC's. Two choices: no suggestion before thirty frames have been seen, and a median exactly half way between is nearer neither. The frames counted are the frames painted, at most one an animation frame, so a frame the machine finished between two paints is not counted. A screen paints about as often as an NTSC game makes frames, and more often than a PAL one, so few are missed, and a missed frame moves a median of thirty little. The line count under the control is plain text, not a live region, so a screen reader is not told it sixty times a second; the suggestion is a live region and changes only when it turns on or off.
- **Sound.** The TIA's samples run from 0 to 1 (the task 10 entry), so a held volume is a steady offset. The page takes it out with a one-pole high-pass whose corner is 20 Hz, below hearing, before the samples are posted to the shared ring, so silence plays as 0 whatever volume was left. Chosen over subtracting a constant, which would make silence an offset of its own, and over filtering in the machine, whose samples task 10 and task 12 checked as they are. Sound was not listened to: there is no browser here.
- **A refused file**, by size before it is read or by the cartridge's own sentence after, leaves the machine running as it was, as the NES's does (Review Focus 2). The panel says which board a file ran as, and that a game made for another board of the same size will not run, linking issue 126.
- **A program that never strobes VSYNC, or strobes it every few lines** (Review Focus 3): the machine ends a frame at 400 lines (task 5), the page paints whatever window of it there is, black below a short frame and cut at the window in a long one, and the line count says how many lines it had.
- **A hidden tab, and the region switched while a game runs** (Review Focus 5). A hidden tab is the shared host's: the run loop stops, so the machine makes nothing, and the sound is suspended; on return it starts afresh, as the NES's does. While sound is off, nothing reads the machine's sample buffer, which drops its oldest samples at its capacity and never grows (task 10). A region change reads the new clock from the host at once, so the run loop's budget and the headroom line follow it.
- **Power off and on** is a button, as on the NES's panel; the 2600's Reset is a console switch the program reads, not a reset line.
- **The browser check's hook.** `panel.vcs.stepTo(n)` holds the run loop, lets go of both joysticks and of Reset and Select, keeps the switches that stay as the panel shows them, powers the machine off and on, runs exactly n frames and paints. Task 14's check sets the region and the colour switch through the page, then calls it with the count in `expected-frames.json`, as task 12's review asked (the title's colours change every frame).

## The window, looked at

Region.md left open whether the window holds a whole picture; task 13 was to look at the suite's calibration picture through it. A scratch test, not committed, ran `harness/calibration` to frame eight in each region and wrote the window, enlarged four times, as a PNG, and both were looked at on 15 October 2026. On NTSC the frame has 262 lines and the picture starts on line 37; on PAL, 312 lines and line 45; in both, the coloured bars sit in the window whole, with 18 lines of black above and below, as region.md's worked example 2 expects. The capture rig draws on every line below its picture too, which the window cuts. `region.md` records this under the open item.

## The fault pass

The fault pass ran on 15 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/panel-faults.py <scratch>`), one fault at a time, running the NES's and the Atari 2600's panel tests. S faults are in the shared modules, A in the Atari 2600's page script and tables, P in its panel's markup, H in the host's source. The figure in brackets is the number of failing cases in each file.

| Fault | Caught by |
|---|---|
| S1 the ring drops down to its most, not its prime | `atari-2600-panel.test.mjs` (1), `nes-panel.test.mjs` (1) |
| S2 the ring registers no processor of its own | `atari-2600-panel.test.mjs` (1) |
| S3 a file at the limit is refused | `nes-panel.test.mjs` (1) |
| S4 a file over the limit is read before it is refused | `atari-2600-panel.test.mjs` (2), `nes-panel.test.mjs` (1) |
| S5 the picker opens before the bundled cartridge is tried | `nes-panel.test.mjs` (2) |
| S6 a gamepad's buttons all read as its first | `atari-2600-panel.test.mjs` (3), `nes-panel.test.mjs` (2) |
| S7 the left stick moves at a quarter of the way | `atari-2600-panel.test.mjs` (1) |
| S8 a held button is not let go when its finger lifts | `atari-2600-panel.test.mjs` (1), `nes-panel.test.mjs` (1) |
| S9 a refused region change leaves the radios on the refused region | survived the first run; then `atari-2600-panel.test.mjs` (1) |
| S10 the sound is posted without its transform | survived the first run; then `atari-2600-panel.test.mjs` (1) |
| A1 Space is joystick 2's fire | `atari-2600-panel.test.mjs` (4) |
| A2 the PAL window one line short | `atari-2600-panel.test.mjs` (2) |
| A3 Reset flips and stays | `atari-2600-panel.test.mjs` (1) |
| A4 the PAL suggestion takes the mean | survived the first run; then `atari-2600-panel.test.mjs` (1) |
| A5 the PAL suggestion shows on PAL too | `atari-2600-panel.test.mjs` (1) |
| A6 up and down together are filtered | `atari-2600-panel.test.mjs` (3) |
| A7 a toggle key flips on its repeats | `atari-2600-panel.test.mjs` (1) |
| A8 a region change restarts the machine | `atari-2600-panel.test.mjs` (1) |
| A9 the canvas keeps NTSC's window on PAL | `atari-2600-panel.test.mjs` (2) |
| A10 stepTo does not power off and on | `atari-2600-panel.test.mjs` (1) |
| A11 stepTo lets go of the switches that stay | `atari-2600-panel.test.mjs` (1) |
| A12 the offset is kept: the corner at 0 Hz | `atari-2600-panel.test.mjs` (1) |
| A13 the page types the size limit | `atari-2600-panel.test.mjs` (1) |
| A14 a refused file is said to leave no machine running | `atari-2600-panel.test.mjs` (1) |
| A15 the gamepad's Reset is on button 8 | `atari-2600-panel.test.mjs` (2) |
| P1 the panel has no line count | `atari-2600-panel.test.mjs` (1) |
| P2 a switch button enabled before the script runs | `atari-2600-panel.test.mjs` (1) |
| P3 the panel no longer says it is not a CRT | `atari-2600-panel.test.mjs` (1) |
| H1 the host types the size limit | `atari-2600-panel.test.mjs` (1) |
| H2 the host hands the picture to another module | `atari-2600-panel.test.mjs` (1) |
| H3 the host exports no SetRegion | `atari-2600-panel.test.mjs` (1) |

**Three survived the first run**, each a gap in the tests, not a fault the tests were right to miss: S9, because neither panel's test refused a change of region (the Atari 2600's page never refuses one, and the NES's test does not try); S10, because nothing ran the sound button's `play` without a browser; and A4, because each of the suggestion's cases had the same answer by the mean as by the median. Each got a case in `atari-2600-panel.test.mjs` (the shared region control refusing a change, the sound button's transform, and sixteen NTSC frames with fourteen of the line limit), and the three were run again (`... <scratch> S9 S10 A4`): all were caught. The NES's test file was not edited; the cases for the shared modules are in the Atari 2600's file.

`nes-panel.test.mjs`, unedited, catches S1, S3, S4, S5, S6 and S8. It misses S2 and S7, parts only the Atari 2600's page uses, and S9 and S10, which no NES test exercised before the move either.

## Commands

All on 15 October 2026, on the development box, in the worktree:

- `node --test tests/atari-2600-panel.test.mjs` before the host was changed: one failure, the host's exports; every other case passed, because the page script and the panel had been written before the test file, in the author's order of work. That the tests can fail is shown by the fault pass above, not by that order.
- `node --test tests/nes-panel.test.mjs`, unedited, after the move: every case passed.
- `dotnet build -c Release`: no warnings. `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests`: every test passed, `PagePathTests` among them.
- `cd site && npm test`: every test passed but the ten that need the machines' WebAssembly built, which fail on this box as before this task; CI builds the machines. The floor in `site/tests/floor.json` rises to the suite's count less its one to-do test, which CI's run of the pushed commit is to confirm.
- `python3 -I tools/probes/atari-2600/panel-faults.py <scratch>`, as above.
