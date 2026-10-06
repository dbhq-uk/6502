# Jet Set Miner

- **Author:** Sarah Walker
- **Year:** 2009
- **Kind:** game
- **Licence:** GPL-3.0-or-later (SPDX)
- **Author's page:** https://www.retrosoftware.co.uk/wiki/index.php?title=Jet_Set_Miner
- **Controls:** Z and X move, RETURN jumps

A small platform game, first written for a minigame competition on another machine and ported to the BBC Micro.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in PLAT.asm, the source, in the download (https://www.retrosoftware.co.uk/wiki/images/2/21/Jet_Set_Miner.zip), verbatim with its line breaks joined:

> This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

The author's Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=Jet_Set_Miner, last edited on 2 August 2017) says, verbatim: "Jet Set Miner Licence GNU GPLv3 license".

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `jet-set-miner.ssd` | the disc image, 46,080 bytes | JetSetMiner.ssd in https://www.retrosoftware.co.uk/wiki/images/2/21/Jet_Set_Miner.zip (the download's SHA-256 is `7465443f2c584040830f45c064e2019ac852e403708457f48cc628a1dbd5f13b`) | `5851abba582fa5115249d2bd3ca50c8dc8385f2da0851ead078c1f4233cf88d5` |
| `LICENSE` | the licence text, as the author published it | COPYING in https://www.retrosoftware.co.uk/wiki/images/2/21/Jet_Set_Miner.zip | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/Jet_Set_Miner.zip` | the source, 40,099 bytes | https://www.retrosoftware.co.uk/wiki/images/2/21/Jet_Set_Miner.zip | `7465443f2c584040830f45c064e2019ac852e403708457f48cc628a1dbd5f13b` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above, taken out of the download unchanged; the licence and source files are as published.

## Caveats

The Retro Software page credits the BBC Micro port to Sarah Walker, with contributions from Murray C. The download holds the disc, its source (PLAT.asm) and the licence together, so the whole download is kept as the source. The source credits the author under an earlier name.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
