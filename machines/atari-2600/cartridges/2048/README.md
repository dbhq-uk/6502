# 2048 2600

The provenance and licence of the Atari 2600's bundled game, the one cartridge its page runs before a visitor loads their own.

- **Author:** Carlos Duarte do Nascimento (Chester)
- **Year:** 2014
- **Kind:** game
- **Licence:** MIT (SPDX)
- **Author's page:** https://github.com/chesterbr/2048-2600
- **Board:** a plain 2K cartridge, which `Cartridge.Load` chooses by its size
- **Controls:** the left fire button starts a one-player game and the right a two-player one; the joystick shifts the tiles; GAME SELECT switches between the modes

A port of Gabriele Cirulli's 2048 to the Atari VCS: shift the tiles on a four by four board, and two tiles of the same value join into one of twice the value.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Read on 2026-10-15 at commit `35de3c1b3b7bc4c3aadc8cf7fe630050a6984761`, verbatim with each statement's line breaks joined.

In `LICENSE` (https://github.com/chesterbr/2048-2600/blob/35de3c1b3b7bc4c3aadc8cf7fe630050a6984761/LICENSE), which is kept beside the game byte for byte:

> The MIT License (MIT) Copyright (c) 2014 Carlos Duarte do Nascimento (Chester) Permission is hereby granted, free of charge, to any person obtaining a copy of this software ...

In the source, `2048.asm`, at its end, the same licence with a second copyright line:

> The MIT License (MIT) Copyright (c) 2014 Carlos Duarte do Nascimento (Chester) Original 2048 game Copyright (c) 2014 Gabriele Cirulli Permission is hereby granted, free of charge, to any person obtaining a copy of this software ...

In the repository's `README.md`, under "Author and License":

> © 2014 by Carlos Duarte do Nascimento (Chester)
>
> Portions contributed and © by their respective authors, see below.
>
> Cartridge Art © 2014 by Jenn Harrison
>
> Based on the original 2048 game by Gabriele Cirulli, which is based on 1024 by Veewo Studio and conceptually similar to Threes by Asher Vollmer.
>
> This software is released under the MIT license, and pull requests are welcome. Keep in mind that by submitting a pull request, you are agreeing to the licensing terms and licensing your contribution under them (without undermining any of your other rights).

The full notice, both copyright lines included, is in [`NOTICE.md`](../../../../NOTICE.md).

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `2048.bin` | the game, 2,048 bytes, the author's own build | https://raw.githubusercontent.com/chesterbr/2048-2600/35de3c1b3b7bc4c3aadc8cf7fe630050a6984761/2048.bin | `c85dc2204299b0a29209d3f80ae112678c171dab216719593625647d2436f1e1` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/chesterbr/2048-2600/35de3c1b3b7bc4c3aadc8cf7fe630050a6984761/LICENSE | `e02e480fdb7e2f17a56fb0a1f8079eb3f3e8ac29abd28bb20da9c0ac47ea07e8` |

`2048.bin` was fetched once by hand with `curl` on 2026-10-15, into an empty directory outside the repository, and its size and SHA-256 checked against the design's rights pass, which fetched the same address on 2026-10-14 and recorded the same hash. `LICENSE` was read through the GitHub API the same day; its git blob hash, `b2abb55904a1a7dcb35ce431327a7af7a25413bd`, is the one the repository's tree gives at that commit. Commit `35de3c1` was the repository's head on 2026-10-14. Nothing fetches either file again: the tests read `2048.bin` from this folder and check it against the SHA-256 pinned in `tests/Dbhq.Cpu6502.TestSupport/Pins.cs` every time.

## What was changed

Nothing. The image is byte for byte the file at the address above, and the licence is as published. The game is not built here: DASM, which builds it, is GPL, and nothing in this repository depends on it.

## The file's history

`2048.bin` was last changed on 8 April 2014, in commit `0694b5464e` ("fixed 'always party' bug"). That is after the two contributions the repository's README credits: Ben Combee's fix to `2048.asm` (commit `2590aaf12b`, 25 March 2014, which the README credits as "Fixed the bug that prevented the last tile from being picked"; the binary was rebuilt with it the same day, in `5d3d8b4ed4`, "build with @unwiredben's fix") and SvOlli's PAL support (commit `020c75b105`, 28 March 2014, "PAL support via TV TYPE switch (by @SvOlli)"). The README licenses every contribution under the same terms (the last paragraph quoted above). The source's one later change, `c8050d0d3c` on 2 September 2014, adds only a comment about a known score bug, which the author left in "to avoid generating a deviant binary from the one that is already on the wild (including physical carts from GDG)". The bundled game has that bug.

## The cartridge art is not in the ROM

The repository's README credits the cartridge art separately: "Cartridge Art © 2014 by Jenn Harrison". That art is the label pictures in the repository (`cart-main.jpg`, `cart-orig.jpg`, `cart-small.jpg` and `cart-top.jpg`, named in the repository's tree at the pinned commit), which are not bundled, shown or committed here. None of it is in the ROM.

## How it runs

The Atari 2600's page runs it on Start (since task 14 of the plan), and credits it with a link to its licence. `BootTests` and `Atari2600AcceptanceTests` in `tests/Dbhq.Machines.Atari2600.Tests/` run it from power on in both regions, NTSC with the colour switch at colour and PAL with it at black and white, which is how the game is told to make PAL frames (its README: "just change the TV TYPE switch to the B/W position (the COLOR position is NTSC)"), and compare its picture with `machines/atari-2600/expected-frames.json`. Sound was not listened to.
