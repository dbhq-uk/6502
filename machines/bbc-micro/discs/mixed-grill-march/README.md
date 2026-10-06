# Mixed Grill March

- **Author:** Jools Henn
- **Year:** 2012
- **Kind:** game
- **Licence:** GPL-3.0-or-later (SPDX)
- **Author's page:** https://www.retrosoftware.co.uk/wiki/index.php?title=Mixed_Grill_March
- **Controls:** A and Z for the left hand, * and ? for the right

Mixed Grill Man has stolen your dinner: match his pose to follow him through the walls, then catch him.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in README.txt in the source archive (https://www.retrosoftware.co.uk/wiki/images/1/13/MixedGrillMarch_v1.01_source.zip), verbatim with its line breaks joined:

> Copyright 2012 Jools Henn This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

The author's Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=Mixed_Grill_March, last edited on 7 August 2013) says, verbatim: "Mixed Grill March by Jools Henn Licence GNU GPLv3 license".

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `mixed-grill-march.ssd` | the disc image, 10,752 bytes | MixedGrillMarch_v1.01.ssd in https://www.retrosoftware.co.uk/wiki/images/3/31/MixedGrillMarch_v1.01-ssd.zip (the download's SHA-256 is `405eda81a6bdfcf0511e4fc65e9d890fa539199889a697f62a73535442eb1455`) | `dab25b8156da8c09ab99b6da445392bc4daf5e9127e597e638ac3012f5bca35f` |
| `LICENSE` | the licence text, as the author published it | MixedGrillMarch_v1.01_source/COPYING in https://www.retrosoftware.co.uk/wiki/images/1/13/MixedGrillMarch_v1.01_source.zip | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/MixedGrillMarch_v1.01_source.zip` | the source, 47,875 bytes | https://www.retrosoftware.co.uk/wiki/images/1/13/MixedGrillMarch_v1.01_source.zip | `3d26125b386eb18b158589664c8ef87c059980a876a9409966d7824a129b4a71` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above, taken out of the download unchanged; the licence and source files are as published.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
