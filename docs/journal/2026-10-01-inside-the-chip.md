---
title: "Inside the chip"
date: 2026-10-01
summary: "A 3D diagram of the 6502 you can fly round, lit by a recorded run of this project's core, and why it is a diagram rather than the die."
order: 7
---

# 1 October 2026: inside the chip

## What was built

A page, `/inside/`, with a 3D diagram of the 6502. You pick a stop in a tour (the pins, the instruction decoder, the control logic, the registers, the arithmetic unit, the status flags, the buses) and the camera flies there. A recorded run of this project's core plays through it: each bus cycle lights the address and data pins it used, and each register shows its bits.

## The decision: a diagram, not the die

The one public machine-readable layout of the real 6502, from the Visual6502 project, is licensed Creative Commons Attribution-NonCommercial-ShareAlike 3.0. The netlist that perfect6502 compiles carries the same licence in its header. perfect6502's MIT licence covers its simulator code, not that data. (An earlier journal entry calls perfect6502 "MIT licence" without saying so; it is true of the code only. The interrupt tests use it as a tool and commit only output this repository generated, so nothing here is affected.)

This repository is MIT, and share-alike would apply to anything derived from the data. So the page does not use it. The layout is drawn by hand in `site/src/chip/layout.mjs` after the 6502's floorplan in outline. Chosen over: a faithful 3D die from the Visual6502 polygons, which needs the licence question settled; a 3D die viewer on its own, which already exists in several forms. No independent trace of the 65C02 exists in public, so a diagram is also the only form that works for all five variants.

What the page says about that, in a test: "Diagram, not the die", sizes and places are approximate, and nothing is traced from a photograph of the chip. The pads are drawn in the order of the package's pins, not where they sit on the silicon.

## Where the values come from

Nothing is animated by hand. `tools/Dbhq.Cpu6502.ChipTrace` runs the speed check's program on the NMOS core and records every bus cycle: address, data and direction, and the registers after each instruction. That is `site/src/data/chip-trace.json`, committed so the site builds without .NET. A test in the core's suite, `ChipTraceTests`, fails if the committed file and the tool disagree, so a change to the core cannot leave the page showing something the core no longer does.

Two limits the page states. The registers show the core's state when each instruction finishes, not the chip's internal timing within it. And the ALU lights for instructions that compute a result; the real chip also uses it for indexed addresses and branch targets, which the view does not show. The core does not model the chip's internals, so this is as much as can honestly be shown.

## The technology, and what it was chosen over

- **three.js with camera-controls,** bundled with esbuild into one same-origin file, `chip.js`. The site's content security policy allows no inline script and no other origin, so the bundle has to be ours. The library licences are kept in the file.
- **Plain three.js rather than React Three Fiber.** The Visual6502 3D page found while researching uses it. Both DBHQ sites have no UI framework, and a page that needs one would be the first.
- **WebGL, with bloom for the glow.** The scene reads its colours from the site's own CSS tokens at run time, so the page and the scene cannot drift apart, and a test fails if the script holds a colour of its own.
- **The words are HTML, not part of the canvas.** The tour reads without WebGL, and a test checks every stop has its text.

## What went wrong on the way

The first render washed out to white: bloom was set far too strong and the lit parts all saturated. Looking at it in headless Chrome showed that at once; the tests could not have. The close-up tour stops were also too near to read. Both were fixed by looking, not by reasoning, and the page was checked again the same way.

## Measurements, dated

On 1 October 2026, on the build machine (a virtual machine, headless Chrome with software rendering, so frame rates are not worth quoting):

- `dotnet run --project tools/Dbhq.Cpu6502.ChipTrace` wrote a trace of 562 bus cycles over 177 instructions, eight laps of the loop, 29,422 bytes.
- The minified bundle was 848,128 bytes and 214,483 gzipped (`ls -l`, `gzip -c | wc -c`). That is large for a site of otherwise small pages, and it only loads on this one.

## Not done

- No check on a real GPU, or on a phone. Only software rendering has been seen.
- No keyboard path to the tour beyond the buttons, which are ordinary buttons.
