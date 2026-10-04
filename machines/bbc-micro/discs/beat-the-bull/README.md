# Beat the Bull

- **Author:** Martin Burchell
- **Year:** 1986
- **Kind:** basic
- **Licence:** LGPL-3.0-only (SPDX)
- **Author's page:** https://github.com/martinburchell/beat-the-bull
- **Controls:** Z and X left and right, * and ? up and down

Guide a hiker across the field without being stampeded by the bull. A BASIC game.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in LICENSE in the repository; the README says "A silly BASIC game I wrote in 1986 for the BBC Micro" (https://github.com/martinburchell/beat-the-bull/blob/f6eb8e6c5970306407a662da9cf80fbe639e819a/LICENSE), verbatim with its line breaks joined:

> GNU LESSER GENERAL PUBLIC LICENSE Version 3, 29 June 2007

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `beat-the-bull.ssd` | the disc image, 204,800 bytes | https://github.com/martinburchell/beat-the-bull/releases/download/v1.0.0/beat-the-bull.ssd | `049f8ed19766f80e78cb43b4ee8b11a9ee75d9309f0170c023945b4a43cb1bda` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/martinburchell/beat-the-bull/f6eb8e6c5970306407a662da9cf80fbe639e819a/LICENSE | `e3a994d82e644b03a792a930f574002658412f62407f5fee083f2555c5f23118` |
| `COPYING.GPL-3.0` | The GNU General Public License version 3, which the LGPL adds its permissions to, as the Free Software Foundation publishes it. The author's repository carries the LGPL text only. | https://www.gnu.org/licenses/gpl-3.0.txt | `3972dc9744f6499f0f9b2dbf76696f2ae7ad8af9b23dde66d6af86c9dfb36986` |
| `source/beat-the-bull-f6eb8e6.tar.gz` | the source, 9,850 bytes | https://github.com/martinburchell/beat-the-bull/archive/f6eb8e6c5970306407a662da9cf80fbe639e819a.tar.gz | `c3fb3bfa5151375ab15e9e42c6652018457c9d9f47e68af98fb4ca54f1b70a11` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Caveats

The release v1.0.0 is tag commit f6eb8e6c5970306407a662da9cf80fbe639e819a, and the source archive is that commit. The game is BBC BASIC: the archive holds the programs as plain text (bull.bas, bull2.bas) and as the machine stores them. The repository states no version beyond 3, so this is LGPL-3.0-only. Its README notes that under emulation @ may be needed for *; on this site * is where the BBC Micro has it.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
