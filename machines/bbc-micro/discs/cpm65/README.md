# CP/M-65

- **Author:** David Given
- **Year:** 2022 onwards
- **Kind:** tool
- **Licence:** BSD-2-Clause AND MIT AND FSFAP (SPDX)
- **Author's page:** https://github.com/davidgiven/cpm65
- **Controls:** type DIR, then RETURN, at the A> prompt

David Given's CP/M for the 6502: a disc operating system with a prompt, an editor, an assembler and BASIC.

This is other people's software, redistributed here under its own licence, not under this repository's MIT licence.

## The licence, as its author states it

Seen on 2026-10-04, in README.md in the repository (https://github.com/davidgiven/cpm65/blob/ff7f5f938607195c562e53f0a4558086aab4663a/README.md), verbatim with its line breaks joined:

> Everything here so far _except_ the contents of the `third_party` directory is © 2022-2023 David Given, and is licensed under the two-clause BSD open source license. Please see [LICENSE](LICENSE) for the full text. The tl;dr is: you can do what you like with it provided you don't claim you wrote it.

## Files

| File | What it is | Where it came from | SHA-256 |
|---|---|---|---|
| `cpm65.ssd` | the disc image, 204,800 bytes | https://github.com/davidgiven/cpm65/releases/download/dev/bbcmicro.ssd | `70bbfa4bad054c8d4b53150a9bd92db3c01644a5ee15e1da77cedc4ad0ccbd4d` |
| `LICENSE` | the licence text, as the author published it | https://raw.githubusercontent.com/davidgiven/cpm65/ff7f5f938607195c562e53f0a4558086aab4663a/LICENSE | `350999c0c37fe0ba35954cbd832282afc7c59e0f1396c37a58a5933056e5376f` |
| `LICENSE.altirra-basic` | Altirra BASIC (ATBASIC.COM on the disc), (C) 2014 Avery Lee, under the FSF all-permissive licence (SPDX FSFAP). | https://raw.githubusercontent.com/davidgiven/cpm65/ff7f5f938607195c562e53f0a4558086aab4663a/third_party/altirrabasic/LICENSE.md | `817b611a3281b0936b02ad02c1536251cfbba927c4f7ac53621228553905f780` |
| `LICENSE.pascal-m` | Pascal-M (PINT.COM, PASC.OBB and PLOAD.COM on the disc), (c) 1978, 2021 Hans Otten, under the MIT licence. | https://raw.githubusercontent.com/davidgiven/cpm65/ff7f5f938607195c562e53f0a4558086aab4663a/third_party/pascal-m/LICENSE | `b43973504aa9b5e5a68f9ec63b98c80d0ea08de209b910e6e901a17d6d1e98f5` |
| `(not kept here)` | the source at the release commit, 2,008,220 bytes | https://github.com/davidgiven/cpm65/archive/ff7f5f938607195c562e53f0a4558086aab4663a.tar.gz | `cf76b52d2d16d0a583f645a0d7e42dcad899cebf586d2b90d1af95f4ca3981b9` |

Everything was fetched by hand with curl on 2026-10-04, from the author's own site or repository, never from a mirror.

## What was changed

Nothing. The image is byte for byte the file at the address above; the licence and source files are as published.

## Note

On a stock BBC Micro it has little memory to spare for programs; its own README says it works better in mode 7.

## Caveats

The image is the project's rolling `dev` release ("Development build 2026-04-05"), which points at commit ff7f5f938607195c562e53f0a4558086aab4663a; the asset was last updated on 5 April 2026. Because that URL can change at any time, the copy here is pinned by its hash. The README says the `third_party` directory "as a whole contains GPL software". The BBC Micro disc was checked part by part against the build files at that commit (src/arch/bbcmicro/build.py and config.py): its third-party parts are Altirra BASIC (FSF all-permissive) and Pascal-M (MIT), and none of the GPL parts (DOS/65's editor, among others) is on it. Their licences are kept here beside David Given's. The source is not kept here, because no licence on the disc requires it and the source archive at that commit is about 2 MB; it is at https://github.com/davidgiven/cpm65/archive/ff7f5f938607195c562e53f0a4558086aab4663a.tar.gz (2,008,220 bytes, SHA-256 given in the table below) [checked 4 October 2026].

## How it runs on the site

The BBC Micro page offers it in its list of discs. Insert and run puts it in drive 0 and starts it the way the machine does, SHIFT held through BREAK, so the DFS runs its `!BOOT`. `DiscLibraryTests` in `tests/Dbhq.Machines.BbcMicro.Tests/` starts it that way on every test run. Sound was not listened to.
