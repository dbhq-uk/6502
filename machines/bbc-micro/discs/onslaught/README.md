# Onslaught

- **Author:** Matt Godbolt and Rich Talbot-Watkins
- **Year:** 1993
- **Kind:** game
- **Licence:** MIT (SPDX)
- **Author's page:** https://github.com/mattgodbolt/onslaught
- **Controls:** ESCAPE then SPACE to start; Z and X move, SHIFT jumps, RETURN fires

An unreleased platform shoot-em-up, written by two schoolboys for Acorn User and never finished.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in LICENSE in the repository (https://github.com/mattgodbolt/onslaught/blob/111bd22706cb2a49eb596e360ae7d5168ce657f8/LICENSE), verbatim with its line breaks joined:

> Copyright 1993 Matthew Godbolt & Richard Talbot-Watkins Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `onslaught.ssd` | the disc image, 44,032 bytes | https://raw.githubusercontent.com/mattgodbolt/onslaught/111bd22706cb2a49eb596e360ae7d5168ce657f8/original-disc.ssd | `6da18af0f33ae59dceca00885ebd21df94fc5141023f839d1cf5d79e836eb1d9` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/mattgodbolt/onslaught/111bd22706cb2a49eb596e360ae7d5168ce657f8/LICENSE | `e0b0f3c03f5ab56adda7721b17642fd47e78a3bafd21780db089e91f808eab40` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Note

Unfinished: the game stops after level 6, as its own help file says it will.

## Caveats

The disc's own help file is addressed to the Acorn User team and gives a telephone number from 1993. The source is in the author's repository, at the commit named above; MIT does not require it to ship with the disc.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
