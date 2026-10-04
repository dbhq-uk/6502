# Blinkenlights

- **Author:** Steven Flintham
- **Year:** 2020
- **Kind:** demo
- **Licence:** MIT (SPDX)
- **Author's page:** https://github.com/ZornsLemma/blinkenlights
- **Controls:** the keys 0 to 8 choose the settings; SPACE starts and stops

A panel of flashing lights, the kind a film's supercomputer has.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in README.md in the repository, as it reads on the page (the word MIT is a link to the licence); LICENCE.txt says "Copyright 2020 Steven Flintham" (https://github.com/ZornsLemma/blinkenlights/blob/3d746f105883ecbf8a786d60c5004a9dee799c64/README.md), verbatim with its line breaks joined:

> This code is covered by the MIT licence; see the LICENCE.txt file.

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `blinkenlights.ssd` | the disc image, 7,424 bytes | built from https://github.com/ZornsLemma/blinkenlights/tree/3d746f105883ecbf8a786d60c5004a9dee799c64, as the caveats say | `b471f6d01a33e44bc7418bc933b041ec400fbc98a3462567119030aabbd02f77` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/ZornsLemma/blinkenlights/3d746f105883ecbf8a786d60c5004a9dee799c64/LICENCE.txt | `7ac14692c1bde0b1c94eb919b0391694b76ca62e786ee1326d6715cfacd0e839` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

The image was built from the author's source, as described below; nothing in the source was changed.

## Note

Photosensitivity: this demo flashes many lights at once, and its settings can make them flash fast.

## Caveats

The repository holds no disc image, so this one was built from its source at commit 3d746f105883ecbf8a786d60c5004a9dee799c64 (2 March 2021) with its own make.sh, using BeebAsm at commit ca2cc5fd2fa3f73da3b0682ad004b2aca99840c3 and Python 3.12.3, the only change being python3 for python in make.sh. Built twice, on 4 October 2026, from two separate clones, it came out byte for byte the same both times. The README says the author's stardot thread has a pre-built copy; that copy was not used.

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
