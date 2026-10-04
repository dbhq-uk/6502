# BeebAsm demo

- **Author:** Rich Talbot-Watkins
- **Year:** 2007
- **Kind:** demo
- **Licence:** GPL-3.0-or-later (SPDX)
- **Author's page:** https://github.com/stardot/beebasm
- **Controls:** Z and X change the speed, ESCAPE stops

A spinning globe of stars: the demo that comes with the BeebAsm assembler.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in README.md in the repository (https://github.com/stardot/beebasm/blob/ca2cc5fd2fa3f73da3b0682ad004b2aca99840c3/README.md), verbatim with its line breaks joined:

> Copyright (C) Rich Talbot-Watkins and the contributors 2007-2025 This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `beebasm-demo.ssd` | the disc image, 3,072 bytes | https://raw.githubusercontent.com/stardot/beebasm/ca2cc5fd2fa3f73da3b0682ad004b2aca99840c3/demo.ssd | `2c803006dd13b196b01eb2c359fa6a72baa138e45651e3a0b5b9a28a05902508` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/stardot/beebasm/ca2cc5fd2fa3f73da3b0682ad004b2aca99840c3/COPYING.txt | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/demo.6502` | the source, 11,195 bytes | https://raw.githubusercontent.com/stardot/beebasm/ca2cc5fd2fa3f73da3b0682ad004b2aca99840c3/demo.6502 | `1f4f61cd52a6bb4ef82cc0f53b3073df58630dc46d10d57e754abea792ecf6cd` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Caveats

The repository's licence is for BeebAsm, and that it covers demo.ssd and demo.6502, which sit beside it in the same repository and carry no notice of their own, is [inferring]. The source of the disc is demo.6502 alone: it includes no other file, and the assembler that builds it is the rest of that repository. The year is when BeebAsm was first released, from the copyright line.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
