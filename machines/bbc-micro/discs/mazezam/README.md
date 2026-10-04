# MazezaM

- **Author:** Kian Vincent, puzzles by Malcolm Tyrrell
- **Year:** 2011
- **Kind:** puzzle
- **Licence:** GPL-3.0-only (SPDX)
- **Author's page:** https://www.retrosoftware.co.uk/wiki/index.php?title=MazezaM
- **Controls:** any key starts; Z, X, * and ? move; ESCAPE restarts the level

Push whole rows of blocks left and right to open a way through each maze.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in the Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=MazezaM), verbatim with its line breaks joined:

> MazezaM by Kian Vincent Licence GNU GPLv3 license

The author's Retro Software page (https://www.retrosoftware.co.uk/wiki/index.php?title=MazezaM, last edited on 9 June 2012) says, verbatim: "MazezaM by Kian Vincent Licence GNU GPLv3 license".

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `mazezam.ssd` | the disc image, 12,800 bytes | MazezaM_BBC_v1_33/MazezaM.ssd in https://www.retrosoftware.co.uk/wiki/images/6/60/MazezaM_BBC_v1_33.zip (the download's SHA-256 is `34cb82a1cc5c2edb539fe0fb9f50c3d133660e61072afc3e3f37be874b950ace`) | `9ddc5785b52379870053b84b2d5f38c14ac01f2f894f3191392eea2f6998e5e4` |
| `LICENSE` | the licence text, as the author published it | MazezaM sources BBC & Elk/gpl-3.0.txt in https://www.retrosoftware.co.uk/wiki/images/9/9a/MazezaM_sources_BBC_%26_Elk.zip | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/MazezaM_sources_BBC_and_Elk.zip` | the source, 102,633 bytes | https://www.retrosoftware.co.uk/wiki/images/9/9a/MazezaM_sources_BBC_%26_Elk.zip | `6a61836adaefd4dbc7bb5bd001d2e6cad365190b7590557c7c82076ff22a5e61` |
| `source/ReadMe.txt` | the release notes from the disc's own download, which say whose the puzzles are | MazezaM_BBC_v1_33/ReadMe.txt in https://www.retrosoftware.co.uk/wiki/images/6/60/MazezaM_BBC_v1_33.zip | `77e6dbee163bf80424de0f6bc3e89da9f316b8254e231ee9ffcab84463b3685c` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above, taken out of the download unchanged; the licence and source files are as published.

## Caveats

The code is Kian Vincent's, and its source says "either version 3 of the License, or (at your option) any later version". The puzzles are Malcolm Tyrrell's: the release notes say they are "the extended set of 'mazezams' provided with the ZX81 version of the game, which Malcolm kindly made available to me, for which he still retains the copyright". Tyrrell's own release of that version says "You can use and distribute 1k MazezaM under the terms of the GNU General Public License v3.0." (https://github.com/Malcohol/1kMazezaM, README, commit 13f44d213143960a48f6e11032f27d8886f496e7, seen 4 October 2026), and his ZX Spectrum release says "You may use and distribute these files under the terms of the GNU General Public Licence v3." (https://github.com/Malcohol/ZXMazezaM, README, commit 5dd9f7668e56cad15887fd39ed357e615e3ff8c1, seen 4 October 2026). That the puzzles on this disc are the ones his GPL release covers is [inferring]: the first puzzle has the same name in both. As his grant names version 3 only, the disc as a whole is taken as GPL-3.0-only. The source archive is the author's published source for the BBC Micro and Electron versions; it is not marked with the disc's version number, v1.33 [inferring that it matches].

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
