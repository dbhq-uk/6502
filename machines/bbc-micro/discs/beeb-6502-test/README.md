# Beeb 6502 test

- **Author:** mungre, with Klaus Dormann's and Bruce Clark's tests
- **Year:** 2017
- **Kind:** tool
- **Licence:** GPL-3.0-only (SPDX)
- **Author's page:** https://github.com/mungre/beeb6502test
- **Controls:** RETURN runs the tests

Klaus Dormann's 6502 tests and Bruce Clark's decimal mode test, run on the machine's own processor.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in README.md in the repository (https://github.com/mungre/beeb6502test/blob/4f9ecfd8bd4258ca8b0739b42ef34b71038e4390/README.md), verbatim with its line breaks joined:

> Licensed under the GNU General Public License version 3

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `beeb-6502-test.ssd` | the disc image, 67,072 bytes | https://raw.githubusercontent.com/mungre/beeb6502test/4f9ecfd8bd4258ca8b0739b42ef34b71038e4390/6502test.ssd | `13183d924694bbe114a3993886e4a4b91b2d8ca78d7284df4e1d117f08e227fe` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/mungre/beeb6502test/4f9ecfd8bd4258ca8b0739b42ef34b71038e4390/license.txt | `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903` |
| `source/beeb6502test-4f9ecfd.tar.gz` | the source, 164,771 bytes | https://github.com/mungre/beeb6502test/archive/4f9ecfd8bd4258ca8b0739b42ef34b71038e4390.tar.gz | `b988cba9fce7b0823cc9bc4d4f3d99df35e1cc2bd9d956ca8935ad65f7f590b9` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Caveats

Bruce Clark's decimal mode test says of itself, in the copy in the source archive: "Written by Bruce Clark.  This code is public domain." Klaus Dormann's tests are GPL-3.0 in his own repository, which this project already uses (a fork is in the dbhq-uk organisation). A full run takes about a minute of the machine's time.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
