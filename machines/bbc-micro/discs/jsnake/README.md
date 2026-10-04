# JSnake

- **Author:** jbnbeeb
- **Year:** 2013
- **Kind:** game
- **Licence:** GPL-3.0-or-later (SPDX)
- **Author's page:** https://www.retrosoftware.co.uk/wiki/index.php?title=JSnake
- **Controls:** Z and X left and right, * and ? up and down, SPACE starts

A snake game: eat, grow longer and keep clear of your own tail.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in jsnake1_03.6502 in the source archive (https://www.retrosoftware.co.uk/wiki/images/5/5a/Jsnake_sourcefiles.zip), verbatim with its line breaks joined:

> This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

The author's Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=JSnake, last edited on 22 August 2014) says, verbatim: "JSnake by jbnbeeb Licence GNU GPLv3".

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `jsnake.ssd` | the disc image, 28,672 bytes | jsnake103.ssd in https://www.retrosoftware.co.uk/wiki/images/0/01/Jsnake103.zip (the download's SHA-256 is `a77c99f363551e7eddb0bcadb02dacfd87a900e44744c1e2a2f572726d1ad598`) | `8895c455d21e10471444f81eef449942cbd5faa25d8da4e008c0a10bb98ca5e5` |
| `LICENSE` | the licence text, as the author published it | jsnake_sourcefiles/COPYING.txt in https://www.retrosoftware.co.uk/wiki/images/5/5a/Jsnake_sourcefiles.zip | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/Jsnake_sourcefiles.zip` | the source, 38,507 bytes | https://www.retrosoftware.co.uk/wiki/images/5/5a/Jsnake_sourcefiles.zip | `b067799cbb01f0c16c252257a7edf9dbc98e8e799d71164fbf9ddc1ef56159e8` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above, taken out of the download unchanged; the licence and source files are as published.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
