# Sparse Invaders

- **Author:** Neil Beresford
- **Year:** 2012
- **Kind:** game
- **Licence:** GPL-3.0-or-later (SPDX)
- **Author's page:** https://www.retrosoftware.co.uk/wiki/index.php?title=Sparse_Invaders
- **Controls:** Z and X move, RETURN fires

An invaders game: shoot the descending rows before they reach you.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in README in the source archive; each source file says "either version 3 of the License, or (at your option) any later version" (https://www.retrosoftware.co.uk/wiki/images/f/fd/SparseInvaders_1_1_source.zip), verbatim with its line breaks joined:

> Copyright (C) 2008,2009 Neil Beresford. All rights reserved. With code contributions from Pitfall Jones, Steve O'Leary and Paul Davis. This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License. See the LICENSE file.

The author's Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=Sparse_Invaders, last edited on 21 March 2012) says, verbatim: "This software is licensed under the GNU GPLv3 license.".

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `sparse-invaders.ssd` | the disc image, 11,520 bytes | SparseInvaders_1_1.ssd in https://www.retrosoftware.co.uk/wiki/images/e/eb/SparseInvaders_1_1.zip (the download's SHA-256 is `4a2a5857a787f92f4bd2ddef1368cfb678236f14a6eff6d21b201ec40359fbd6`) | `6b397f5761c38a3c03c4c2636fbdccc5c98297680058b8aaf4ae9086dd42f227` |
| `LICENSE` | the licence text, as the author published it | LICENSE in https://www.retrosoftware.co.uk/wiki/images/f/fd/SparseInvaders_1_1_source.zip | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/SparseInvaders_1_1_source.zip` | the source, 59,259 bytes | https://www.retrosoftware.co.uk/wiki/images/f/fd/SparseInvaders_1_1_source.zip | `a4a2bef7af8b741460872a942e0476f5ced326a1696504657a169b47ad1d1910` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above, taken out of the download unchanged; the licence and source files are as published.

## Caveats

An earlier diary entry on the same Retro Software page, written before the source was released, says "I am going for a free licence, however the source is not to used for profit type ventures." The source was then released under the GPL, which allows any use, and the page's licence section says GPLv3; the later licence is taken as the author's decision [inferring]. This site sells nothing.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
