# Headcase Hotel

- **Author:** Stephen Scott
- **Year:** 1995 and 2021
- **Kind:** game
- **Licence:** MIT (SPDX)
- **Author's page:** https://github.com/sassquad/headcasehotel
- **Controls:** Z and X move left and right, F and C up and down, P pauses

An arcade adventure in a strange hotel, from an idea by Christopher West.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in LICENSE in the repository (https://github.com/sassquad/headcasehotel/blob/377478c56232da8855457201f346364b126e2306/LICENSE), verbatim with its line breaks joined:

> The MIT License (MIT) Copyright (c) 1995, 2016, 2021 Stephen Scott Permission is hereby granted, free of charge, to any person obtaining a copy ...

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `headcase-hotel.ssd` | the disc image, 30,464 bytes | https://raw.githubusercontent.com/sassquad/headcasehotel/377478c56232da8855457201f346364b126e2306/HHotel.ssd | `307d27425b07db912076663699c6d3a8f7b92752674e5b09be235a5d24c8b8a8` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/sassquad/headcasehotel/377478c56232da8855457201f346364b126e2306/LICENSE | `dfefa46271c8f6ae067c8508dc3f0917e269799631490102d8111e51c632d308` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Note

Photosensitivity: this game flashes. This is the revised 2021 build, which its author describes as having a vastly reduced strobing effect, but it still flashes.

## Caveats

The author's README warns that the original 1995 release "contains intense strobing effects, not recommended for anyone with epilepsy" and offers a "Revised, bug-fixed version (with vastly reduced strobing effect)". The image here is the repository's HHotel.ssd as last changed on 28 October 2021 ("further bugfix to death routine"); its title shows V1.12 27/10/2021. It is not the original 1995 release (a different disc, 204,800 bytes, SHA-256 af58ad4010f9341e3c007ecccc4f4af9fd6dc8a71a949315b771cc9f4463c9f2 on the author's site). It also differs from the redux copy on the author's site (SHA-256 7e47558028af7fc38b2c7adcfa7f508d87f6cf80cc14f01e56d046001181627e) in one program file, HOTEL2, which suggests the repository's is a later build of the same revision [inferring].

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
