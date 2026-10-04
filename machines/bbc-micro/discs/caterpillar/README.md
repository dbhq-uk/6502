# Caterpillar

- **Author:** Paul Newell
- **Year:** 1983 and 2026
- **Kind:** game
- **Licence:** MIT (SPDX)
- **Author's page:** https://github.com/newell-paul/caterpillar-assembler
- **Controls:** any key starts; Z and M steer

Guide the caterpillar through the mushroom patch, eating your way through the four seasons.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in LICENSE in the repository (https://github.com/newell-paul/caterpillar-assembler/blob/24a99039d428a00322b410b8393a4414c62515f2/LICENSE), verbatim with its line breaks joined:

> MIT License Copyright (c) 1983, 2026 Paul Newell

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `caterpillar.ssd` | the disc image, 5,376 bytes | https://raw.githubusercontent.com/newell-paul/caterpillar-assembler/24a99039d428a00322b410b8393a4414c62515f2/caterpillar.ssd | `06ab2ad82942c7cc330b38ebc218fd97602164cedfb8e66a4fe368e70d70b532` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/newell-paul/caterpillar-assembler/24a99039d428a00322b410b8393a4414c62515f2/LICENSE | `c80a2e8d1179954641b728e076fcb29018919623d0e839ca7be7d8d24802994f` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Caveats

The repository's README says the game is "A type-in BASIC game from 1983, rewritten in native 6502 assembly in 2026 by the same author". This is the 2026 disc, caterpillar.ssd, not caterpillar-original.ssd in the same repository; the README says "Original 1983 listing © Paul Newell, 1983. The 2026 rewrite is released under the MIT licence", so only the rewrite is bundled.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
