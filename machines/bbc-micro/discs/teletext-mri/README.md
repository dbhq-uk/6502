# Teletext MRI

- **Author:** Jasper Renow-Clarke
- **Year:** 2020
- **Kind:** demo
- **Licence:** MIT (SPDX)
- **Author's page:** https://github.com/picosonic/teletext_mri
- **Controls:** none: it plays by itself

Slices through its author's own MRI brain scan, animated in teletext graphics.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in LICENSE in the repository (https://github.com/picosonic/teletext_mri/blob/25da3272a3abc1a2878740a58d58b7bd1c3795a9/LICENSE), verbatim with its line breaks joined:

> MIT License Copyright (c) 2020 Jasper Renow-Clarke ...

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `teletext-mri.ssd` | the disc image, 23,552 bytes | https://raw.githubusercontent.com/picosonic/teletext_mri/25da3272a3abc1a2878740a58d58b7bd1c3795a9/teletextmri.ssd | `e12460a41652006b8629d9e75208d7aa646dc7708589948857b91d886a6f4c26` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/picosonic/teletext_mri/25da3272a3abc1a2878740a58d58b7bd1c3795a9/LICENSE | `c579ae7217050784dbd694c0c39fb3f2c458369bd8bfb530f4e49487a746775d` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Caveats

The scan is the author's own: the README says "I had an MRI brain scan in July 2009 and was handed all the scan data".

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
