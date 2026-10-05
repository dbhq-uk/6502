---
title: "The Electron's fact sheets and plan"
date: 2026-10-04
summary: "Two fact sheets and a plan for the Electron, written before any code: what the ROM search and the sources settled, a contention rule that reproduces real timings, and what is left to be found by running the ROM."
order: 31
---

# 4 October 2026: the Electron's fact sheets and plan

The design was approved to be written up, and the plan came next. As with the BBC Micro, the chips are built from sources and not from memory, so two fact sheets came first: [`ula.md`](../electron/facts/ula.md) for the ULA, and [`tape.md`](../electron/facts/tape.md) for the cassette and the UEF format. The plan is [`2026-10-04-electron.md`](../superpowers/plans/2026-10-04-electron.md).

## What the ULA sheet settled

- **There is no keyboard ROM image.** The keyboard is hardware in slots 8 and 9: address lines A0 to A13 pick columns and D0 to D3 return keys. BASIC fills slots 10 and 11, one chip in both. The operating system and BASIC are all a stock machine needs. This closes the design's open question.
- **Every RAM access is a 1 MHz access, in every mode.** It costs two cycles of the 2 MHz clock from a boundary and three from one cycle off. Only ROM runs at full speed.
- **In modes 0 to 3 the CPU cannot complete a RAM access during the 40 microsecond display window of a line.** Mode 3 contends only the eight data lines of each ten-line row. The sheet's rule, run in a throwaway model of the sheet's author's own, reproduced seven real-hardware timings of one BASIC loop to a hundredth of a second, across two real machines.
- **A model that left out the dummy read in the return instructions ran about 1.5 per cent fast in every mode.** The plan makes every bus cycle reach the contention check, dummy reads included, and tests it.
- **A frame is exactly 80,000 cycles**, two fields of unequal length, which is why the two display-end interrupts are 39,936 apart and the two clock interrupts exactly 40,000 apart.

## What the tape sheet settled, and what it did not

The container, the chunk list, the block layout and the CRC are sourced. The CRC is CRC-16 CCITT, stored high byte first, and was checked three ways: the standard check value, the Advanced User Guide's own published block CRC (which it prints byte-swapped, as the value its assembler stores), and a simulation of the operating system's own routine on 304 inputs with no mismatch.

What no source read settles is how the operating system makes the leader tone on tape and how long it is. The plan makes that its own task, run against the real ROM with a logging stand-in for the ULA's tape side, and the answer goes into the sheet before any tape code is built.

## A mistake and its repair

The second sheet was started by a helper that stopped on a rate limit before it wrote anything, so the sheet was written directly. It was written from the primary text of the UEF specification and the Advanced User Guide, and where it could not be sourced it says so and names the task that will find out.

## The plan's form

Fifteen tasks in the BBC Micro plan's shape, with the numbers fixed by the sheets written into the tests in full: the contention table, the CRC vectors, a 30-byte tape block. The speed of the contention check is measured in a browser in task 7, before the display is built, because it is the one part of this machine that asks a question on every bus access.

## The ROM, fetched (first task, 5 October)

The first task of the plan fetched the operating system from `dmcoles/elkjs` at commit `ff123355407f79a91f808e31222dcca5d51ea87f`, with `curl`, on 5 October 2026, and stopped if either hash differed. Neither did.

- `os.rom` is 16,384 bytes with SHA-256 `b63f851d79498f598999d923b7c9f62e2525c34f0b9cd2d4b328b89d622dcda4`, as the fact sheets expected. It is committed as `roms/electron/os.rom`.
- That commit's `basic.rom` is 16,384 bytes with SHA-256 `45bd55dc0f6f0f8f1fe9e2481de7def206565eec8f600ba3068b849ca4132079`, which is the BBC Micro's BASIC hash exactly. It was deleted again: the Electron reads `roms/bbc-micro/BASIC.ROM`, and BASIC is kept once.
- The ROM carries the sheets' vectors at file offset `$3FFA` (NMI `$0D00`, reset `$D8D2`, IRQ `$DAE7`) and the text "OS 1.00". A test holds both, so a different dump fails on purpose.
- The ROM's own credits text reads `(C) 1983 Acorn Computers Ltd.` That is the copyright wording as written, and it is the only rights statement in the file.

**The rights, as found.** Acorn's copyright. Who holds it now was not established. The elkjs repository is GPL-2.0, and its README says nothing about the ROMs, so that licence is not a licence for them: a GPL text at the root of a repository covers the code its author wrote, and the author is not the ROM's author. No permission from any holder was found. It is used under Dan's rule of 1 October 2026 (a ROM is used when its position is documented), and removed if a holder asks. The record is in `roms/README.md`.

**The registry's `rights` field is not filled in yet.** The Electron's record stays `planned` with `rights: null` until the page task, which writes the whole record at once, as the BBC Micro's was.

## The bus, the memory map and the cost of an access (second task, 5 October)

The second task built the Electron's bus: the memory map, the paged ROM select and the cost of each bus cycle, without the display's contention, which is a later task.

- **The cost rule.** A ROM access is one cycle. A RAM access, or one to `$FC00-$FEFF` or the keyboard slots, takes the first 1 MHz boundary at least two cycles on: two cycles from a boundary and three from one cycle off. The bus judges each cycle by its own address, so the core's dummy reads and the first write of a read-modify-write are charged like any other access. Which parity is a boundary is not known, and no total depends on it, so a boundary is an even count, the fact sheet's own convention.
- **The alignment loop takes 18 cycles, not 17.** `SEI`, `LDA $C000`, `JMP $2000` makes eight RAM accesses at two cycles and one ROM access at one, which adds to 17. The ROM access leaves the count odd, so the `JMP`'s opcode fetch costs three. The real machine measured 9 microseconds, which is 18 cycles, and the test holds that figure as the measurement it is. The bus reproduced it on the first run, with the real core driving it.
- **The dummy read in `RTS` and `RTI`.** Both make six bus accesses. The test writes out the expected addresses from the 6502's documented behaviour (opcode, the next byte discarded, a read of the stack at S that nothing uses, then the pulls and, for `RTS`, the read at the new PC). The task brief said the second access is the stack read. By the documented trace it is the third: the second is the discarded read of the byte after the opcode, which is RAM too, so the cost is the same either way.
- **Two placeholders, owned by the next tasks.** A read of any ULA register returns the high byte of the address, `$FE`, until the ULA is built. A read of slots 8 and 9 returns `$00` until the keyboard is. Neither affects a boot, but both are stated in the code so nobody takes them for the answer.
- **An empty slot reads the high byte of the address.** That is an assumption, not a measurement: the sheet's open item 3 says nothing is known, recommends this value and shows the boot does not depend on it.
- **The ROM select's acceptance rule lives in the bus.** From slot 8 to 11 a write to `$FE05` is honoured only with bit 3 set, so an interrupt clear cannot drop BASIC to slot 0. The same write reaches the ULA's interrupt clears in the next task, as one added line beside the select.
- **Mirroring.** The ULA's registers repeat in every 16-byte block, so `$FEA5` is the select register too, and a test says so.
