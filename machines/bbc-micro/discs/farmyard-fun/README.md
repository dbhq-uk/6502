# Farmyard Fun

- **Author:** Mark W
- **Year:** 1989 and 2008
- **Kind:** adventure
- **Licence:** GPL-3.0-only (SPDX)
- **Author's page:** https://www.retrosoftware.co.uk/wiki/index.php?title=Farmyard_Fun
- **Controls:** type commands, then RETURN

A text adventure on a farm. Type what to do: N, EXAMINE FILOFAX, MILK COWS.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in its Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=Farmyard_Fun), verbatim with its line breaks joined:

> Farmyard Fun by Mark W Licence GNU GPLv3 license

The author's Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=Farmyard_Fun, last edited on 25 February 2012) says, verbatim: "Farmyard Fun by Mark W Licence GNU GPLv3 license".

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `farmyard-fun.ssd` | the disc image, 58,880 bytes | FarmyardFun2008.ssd in https://www.retrosoftware.co.uk/wiki/images/d/db/FarmyardFun2008v1-1.zip (the download's SHA-256 is `c7724f4a935ae8f87c94c8e08220ad5de8d368ab0264e475c19c09663632fd04`) | `eb4934f2155c13ee0e028628b686335e86e3b3988f5a2d6d1a354569ecc509fc` |
| `LICENSE` | the licence text, as the author published it | $.COPYING on the disc itself, taken out of the image | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/LOADER` | the file $.LOADER from the disc, 1,104 bytes (its .inf beside it) | taken out of the image | `5431f31c80a85a2a921692dcd953689314ea62a15ae8fa8584c53fcb9833a9de` |
| `source/FYF` | the file $.FYF from the disc, 19,740 bytes (its .inf beside it) | taken out of the image | `00460ded2136c01acf072b81333bc4d7cadcc3fdb22162e371860ad558437c5d` |
| `source/!BOOT` | the file $.!BOOT from the disc, 16 bytes (its .inf beside it) | taken out of the image | `159399bff56576331d44232e47c27c107a5a122e454ab7091384d1ea60c9977e` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above, taken out of the download unchanged; the licence and source files are as published.

## Caveats

The page says the game "was originally written in 1989" and this is the "slightly-remastered" version 1.1 of 2008. It is a BBC BASIC program, and the program on the disc is its source: source/ holds the disc's files, taken out of the image byte for byte with a .inf file each giving its load and run addresses. The licence file here is the disc's own $.COPYING, taken out the same way. No version beyond 3 is stated, so this is GPL-3.0-only.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
