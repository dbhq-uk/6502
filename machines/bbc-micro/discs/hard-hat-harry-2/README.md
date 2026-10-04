# Hard Hat Harry 2

- **Author:** Sarah Walker
- **Year:** 2012
- **Kind:** game
- **Licence:** GPL-3.0-or-later (SPDX)
- **Author's page:** https://www.retrosoftware.co.uk/wiki/index.php?title=Hard_Hat_Harry_2
- **Controls:** Z and X move, SPACE starts

The sequel to Hard Hat Harry: a platform adventure in the caverns under a new arts centre.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in HHH2.asm in the source archive (https://www.retrosoftware.co.uk/wiki/images/b/b1/HardHatHarry2-Source.zip), verbatim with its line breaks joined:

> This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

The author's Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=Hard_Hat_Harry_2, last edited on 2 August 2017) says, verbatim: "Hard Hat Harry 2 by Sarah Walker Licence GNU GPLv3 license".

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `hard-hat-harry-2.ssd` | the disc image, 20,480 bytes | HardHatHarry2-V1.0.ssd in https://www.retrosoftware.co.uk/wiki/images/7/75/HardHatHarry2-V1.0.zip (the download's SHA-256 is `0f989e9ed4f34a9e35a3a1cbe4400e02682497733f5674d22e340b2e8b1d8285`) | `ffabda2305f4a27a0db9ea64ebefb30933566e57e41e3b03a6e4c666c32a6dfb` |
| `LICENSE` | the licence text, as the author published it | COPYING in https://www.retrosoftware.co.uk/wiki/images/b/b1/HardHatHarry2-Source.zip | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/HardHatHarry2-Source.zip` | the source, 36,495 bytes | https://www.retrosoftware.co.uk/wiki/images/b/b1/HardHatHarry2-Source.zip | `11985d0b07b82c7a6247b7d67e6a08266034d018d36a4eefdd36bc80950d25ea` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above, taken out of the download unchanged; the licence and source files are as published.

## Caveats

The title screen credits the author under an earlier name; the Retro Software page credits Sarah Walker today, and so does this site. The year is 2012, though the title screen shows (C) 2011: the source says "Copyright 2012", the image inside the download is dated 31 January 2012, and the Retro Software page's design notes have 2011 struck through and 2012 written after it.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
