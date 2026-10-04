# Jumbo text scroller

- **Author:** James Rayner
- **Year:** 2022
- **Kind:** demo
- **Licence:** MIT (SPDX)
- **Author's page:** https://github.com/jprayner/bbc-jumbo
- **Controls:** press 1, type a message, then RETURN

Type a message and it scrolls across the screen in giant letters.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in LICENSE in the repository (https://github.com/jprayner/bbc-jumbo/blob/e35f2ec9343fdda67eeddfc3fd1464c7f7ed9ca0/LICENSE), verbatim with its line breaks joined:

> MIT License Copyright (c) 2022 James Rayner

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `jumbo-scroller.ssd` | the disc image, 3,840 bytes | https://github.com/jprayner/bbc-jumbo/releases/download/v1.0.0/jumbo.ssd | `1d15f32a82870663dee7001541d11c0a50a25bce333c9508ae85453b6c5f17e0` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/jprayner/bbc-jumbo/e35f2ec9343fdda67eeddfc3fd1464c7f7ed9ca0/LICENSE | `1fee379c133f2dba3b6174790464effda0b3481ad3a9bae5ecf9de75be622cdb` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Caveats

Release v1.0.0 is tag commit e35f2ec9343fdda67eeddfc3fd1464c7f7ed9ca0. The menu also offers an Econet mode; the BBC Micro here has no Econet, so choose 1. The music credited in the README is for the author's video; the disc holds no music file [inferring from its catalogue].

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
