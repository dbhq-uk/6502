---
title: "The Atari VCS's regions, its cartridge and its boards"
date: 2026-10-14
summary: "The second task of the Atari VCS: the two regions' clocks, picture window and pixel shape; the cartridge, its board chosen by the file's size and the Superchip by its filler; and Atari's and CBS's bank switching boards, each tested alone against images the tests build and through the core's own bus accesses. Every test file was shown able to fail by breaking the code on purpose, one fault at a time."
order: 54
---

# 14 October 2026: the Atari 2600's regions, its cartridge and its boards

Task 2 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): `Region`, `Cartridge`, `CartridgeException`, `IBoard` and the boards in `src/Dbhq.Machines.Atari2600/Boards/`, built from [`cartridge.md`](../atari-2600/facts/cartridge.md) and [`region.md`](../atari-2600/facts/region.md). Nothing runs a program yet: the bus is task 4, so no suite test passes here, and none is claimed.

## What was built

- **`Region`** holds the colour clock as an exact expression of the colour subcarrier it comes from (315/88 MHz on NTSC, four fifths of 4.43361875 MHz on PAL) and computes the CPU clock from it, so no clock is typed twice. It also holds Atari's recommended line counts, the picture window and the pixel shape. Nothing reads its name except the page.
- **`Cartridge.Load`** checks the file's length against the modelled sizes before it copies anything, then keeps its own copy. The sizes and their boards live in one table, which the refusal's sentence is built from: "This file is 3,072 bytes, and this machine runs cartridges of 2,048 (2K), 4,096 (4K), 8,192 (F8), 12,288 (FA), 16,384 (F6) or 32,768 (F4) bytes only." `MaxFileSize` is the largest, for the page.
- **The boards.** `Plain` (2K mirrored, 4K), `F8`, `F6` and `F4` on one `BankedBoard` base, `Fa`, and `Superchip`, which wraps an F8, F6 or F4 and answers only its RAM ports. A board sees the 13-bit address and decodes A0 to A11; keeping thirteen lines is the bus's job in task 4. Every access reaches the board, read or write, real or dummy, because the slot has no read/write line.

## Decisions

- **The power-on bank is the last bank**, as `cartridge.md` section 8 recommends, over bank 0, the suite's "home bank". Both pass every suite test; the last bank was chosen because a game that breaks Korth's rule of an entry point in every bank most likely keeps its start-up in the last bank, as the fixed-segment boards do. The suite's `power-on-bank` diagnostic should therefore report `$B1` when it runs from task 4. In `docs/known-differences.md`.
- **Superchip and FA RAM is clear at power on**, over a random fill: no source states it, and zero keeps runs repeatable. In `docs/known-differences.md`.
- **FA's D0 gate is built as the sheet settles it**: a write switches only with D0 of the byte written set, a read only with D0 of the hotspot's byte in the bank showing before the access, which is the byte the read returns. CBS's patent and the suite's `bank-fa` have the gate; Horton and Korth have none. `FaTests` follows the sheet and its remarks name the disagreement; `docs/known-differences.md` says a CBS game that fails to change bank points here first.
- **A hotspot read on F8, F6 and F4 returns its byte in the bank it selects**, the rule the sheet took from the suite's `bank-hotspot-window` and which corrected the plan's first draft. FA does not follow it, for the reason above.
- **`Peek` of a RAM write port shows the cell.** A read there drives nothing, so there is no byte a read would return; the cell is what a debugger wants to see. Everywhere else `Peek` gives exactly what a read would, a hotspot included, without switching. Chosen over adding the data bus to `Peek`'s parameters, which would have changed the plan's interface.
- **One shared `BankedBoard`** under `F8`, `F6` and `F4`, which differ only in their bank count and first hotspot, so the Superchip wraps one type and the switching is written once. The plan listed a file per board and no base; the base is one more file.
- **The boards' constructors are internal**: the page and the bus get a board from `Cartridge.CreateBoard`, and the tests build boards directly through the project's `InternalsVisibleTo`.
- **The PAL pixel is 52:25** (2.080), within the sheet's one part in a thousand of 2.079; the NTSC pixel is 12:7 exactly.

## What the sheets settled, and what was found

- **An indexed store reads its address before it writes**, every time. `cartridge.md` worked example 4 marked `STA $1FF0,X` reading `$1FF8` first `[guessing - verify]`. The tests do not take that from the table: a small test bus, `BoardBus`, puts the core (checked cycle by cycle against Harte's tests) on a board alone, and `BankedBoardTests.An_indexed_store_to_a_hotspot_reads_it_first` sees the read then the write. The same bus gives `INC $1FF8` its three accesses, `LDA $1FF9,X` its dummy read of the hotspot, the suite's `LDY #$80`, `LDA $F080,Y` its ghost write of `$F0` into the Superchip's cell 0, and FA's page-crossing load reading back the byte its dummy read wrote.
- **The filler rule on the suite's files.** `CartridgeTests.The_suites_own_cartridge_files_get_the_boards_fixed_before_running` reads the `_ntsc` and `_pal` builds of `mirror-2k`, `bank-f8`, `bank-f6`, `bank-f4`, `bank-fa`, `bank-hotspot-window`, `power-on-bank` and `ram-superchip` through `Atari2600Suite.Read`, and each got the board the plan fixed before running (item 6), `ram-superchip` alone with the Superchip. That is detection only: the tests themselves run from task 4.
- **The rule's known miss stands.** Korth names Dig Dug as a Superchip game with other bytes where the RAM hides the image, so it gets plain F6 and fails; issue #126 covers detection from the image, and `docs/known-differences.md` names it.

## The tests, test first

In the author's order of work, each test file was written before its code; the tests and the code landed in one commit, so the history cannot show that order. The first build failed to compile, naming `Region`, `IBoard`, `Cartridge` and the `Boards` namespace as missing. With the code written, the project passed on the first run. The test files: `RegionTests`, `CartridgeTests`, `PlainTests`, `BankedBoardTests`, `FaTests`, `SuperchipTests` and `BoardContractTests`, with `TestImages` building every image (each 4K bank holds its own bank number, so a read shows which bank answered) and `BoardBus` putting the core on a board.

On 14 October 2026 `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName!~SuiteManifestTests"` passed 198 test cases, the sixteen rows that read the suite's files among them; the whole project, with task 1's tests, passed 206. `dotnet build -c Release` of the solution gave no warnings.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/board-faults.py <scratch>`, which copies the projects, plants one fault at a time, builds, runs the project without `SuiteManifestTests` and puts the file back; the two faults added for the rerun are in it too). Each fault is listed under the test file it was written against, with every test file that failed on it.

| Test file | Fault planted | Test files that failed |
|---|---|---|
| `RegionTests` | PAL clock typed as Korth's | `RegionTests` (1) |
| `RegionTests` | CPU clock a quarter | `RegionTests` (3) |
| `RegionTests` | NTSC window a line late | `RegionTests` (1) |
| `RegionTests` | PAL pixel 2:1 | `RegionTests` (1) |
| `RegionTests` | PAL nominal lines 313 | `RegionTests` (1) |
| `CartridgeTests` | filler checked in bank 0 only | `CartridgeTests` (1) |
| `CartridgeTests` | filler 128 bytes | `BoardContractTests` (7), `CartridgeTests` (2) |
| `CartridgeTests` | superchip on FA too | `CartridgeTests` (1) |
| `CartridgeTests` | no copy kept | `CartridgeTests` (1) |
| `CartridgeTests` | copied before the size check | `CartridgeTests` (1) |
| `CartridgeTests` | message without the size | `CartridgeTests` (1) |
| `CartridgeTests` | F6 and F4 sizes swapped | `BoardContractTests` (7), `CartridgeTests` (3) |
| `PlainTests` | 2K not mirrored | `BoardContractTests` (4), `PlainTests` (3) |
| `PlainTests` | a write changes the image | `BoardContractTests` (2), `PlainTests` (1) |
| `BankedBoardTests` | hotspot read returns the old bank's byte | `BankedBoardTests` (5), `BoardContractTests` (1), `SuperchipTests` (1) |
| `BankedBoardTests` | hotspot switches on read only | `BankedBoardTests` (3), `SuperchipTests` (2) |
| `BankedBoardTests` | F6 hotspots one late | `BankedBoardTests` (4), `SuperchipTests` (3) |
| `BankedBoardTests` | one hotspot too many | `BankedBoardTests` (3), `SuperchipTests` (1) |
| `FaTests` | D0 gate inverted | `FaTests` (13) |
| `FaTests` | no D0 gate | `FaTests` (3) |
| `FaTests` | read gates on the bus | `FaTests` (3) |
| `FaTests` | FA read of the write port writes nothing | `FaTests` (2) |
| `FaTests` | FA read port at $1080 | `BoardContractTests` (6), `FaTests` (8) |
| `FaTests` | FA hotspot read returns the new bank's byte | `FaTests` (2) |
| `SuperchipTests` | Superchip dummy read writes nothing | `SuperchipTests` (4) |
| `SuperchipTests` | Superchip write to the read port stores | `SuperchipTests` (1) |
| `SuperchipTests` | Superchip read port shows the ROM | `BoardContractTests` (3), `SuperchipTests` (6) |
| `SuperchipTests` | Superchip RAM cleared on a bank switch | `SuperchipTests` (2) |
| `BoardContractTests` | Peek switches the bank | `BoardContractTests` (1) |
| `BoardContractTests` | Peek of the write port clears the cell | `BoardContractTests` (1) |
| `BoardContractTests` | power on in bank 0 | `BankedBoardTests` (4), `BoardContractTests` (3), `CartridgeTests` (1), `SuperchipTests` (2) |
| `BoardContractTests` | Superchip power on keeps the RAM (first run) | none: it survived |
| `BoardContractTests` | Peek of a hotspot shows the bank showing | `BoardContractTests` (1) |
| `BoardContractTests` | FA power on in bank 0 | `BoardContractTests` (3) |
| `BoardContractTests` | Superchip power on keeps the RAM | `BoardContractTests` (1) |
| `BoardContractTests` | FA power on keeps the RAM | `BoardContractTests` (1) |

The figure in brackets is the number of failing test cases. **One fault survived the first run**: a Superchip whose power on kept its RAM. `BoardContractTests`' power-on test ran a script that writes every write-port cell before it reads any read port, so stale RAM was overwritten before it could be seen. The test now fills the RAM, powers on, and reads every cell through the read port before the script; run again with the fault and with its FA twin (an FA board whose power on keeps its RAM), both were caught by that test.
