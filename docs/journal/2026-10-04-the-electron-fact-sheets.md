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

## The ULA's registers, the frame and the interrupts (third task, 5 October)

The third task gave the bus a ULA: the interrupt enable and status at `$FE00`, the clears in `$FE05`, the display mode in `$FE07`, and the frame that raises the two interrupts the OS lives on.

- **The frame is 80,000 cycles in two fields that are not the same length.** The odd field is 39,936 cycles (312 lines) and the even field 40,064 (313). So the two clock interrupts, at 12,670 and 52,670, are exactly 40,000 apart, and the two display ends, at 32,736 and 72,672, are 39,936 apart, 64 cycles short of 40,000. A test finds each firing by stepping the ULA a cycle at a time and asserts the gaps 39,936 and 40,064 and their sum, 80,000. In modes 3 and 6 the display end is 768 cycles earlier, at 31,968 and 71,904: the display is 250 lines tall instead of 256, and six lines of 128 cycles is 768. The test holds the sheet's figures.
- **The ULA is lazy, as the BBC's chips are.** It does nothing on the cycle. It holds one number, the next cycle at which something happens, and the bus compares its clock against that once per access. Only when the clock has reached it does the bus call the ULA's catch-up, which raises every event that has come due, in order. The same call runs before an access reads or writes a register, and before the IRQ line is read, so the line is never stale. A write of the mode moves the pending display end, so the number is recomputed there too.
- **One write to `$FE05` does two jobs.** The bus keeps the paged ROM's acceptance rule and then passes the same write to the ULA, which takes bits 7 to 4 (the interrupt clears). The select logic did not move into the ULA.
- **The status bits are set whether or not the interrupt is enabled.** The enable only gates bit 0 and the IRQ line. A test sets the clock interrupt with nothing enabled, sees bit 3 set and bit 0 clear, then enables it and sees both.
- **The power-on flag.** Set at power on and cleared by the first read of `$FE00`. A peek at the register, which the machine's tools use, does not clear it. BREAK has no call that sets it, because the OS tells the two apart by it.
- **A read of a register the ULA does not answer is the high byte of the address**, task 2's rule, now only for registers 1 to 15. `$FE04`, the cassette, joins `$FE00` in task 10.
- **Three choices the sheet leaves open**, written in `docs/known-differences.md`: transmit empty is set at power on, the mode is 0 at power on, and BREAK resets nothing in the ULA.
- **The tape's hooks.** `SetStatus` and `ClearStatus` take bit 4, 5 or 6 and nothing else, so the tape tasks can raise receive full and high tone and drop transmit empty without reaching into the ULA's other state.

## The keyboard (fourth task, 5 October)

The fourth task built the keyboard matrix and wired it into slots 8 and 9.

- **It is a device, and it is one device in two slots.** Slots 8 and 9 hold no image. The bus sends a read in either slot to the same `ElectronKeyboard`, with the full 16-bit address, and slot 10 and 11 still read BASIC. The cost of the access is task 2's rule, unchanged: the keyboard slots were already a 1 MHz access.
- **The OR rule.** Each of the 14 address lines A0 to A13 selects a column when it is low, and a read ORs the four bits of every selected column. So `$A000` (A13 high, A0 to A12 low) asks "is any key down in columns 0 to 12", which is the OS's probe, and `$9FFF` asks about column 13 alone. A test holds that Escape, Caps Lock, Ctrl and Shift, which all sit in column 13, never show at `$A000`. Bits 7 to 4 read 0, and so does every address with no column line low.
- **54 keys, not 56.** The task brief said 56. The table in `ula.md` section 7b has 14 columns of 4 positions, which is 56 positions, and marks two of them not connected (column 0 bit 2 and column 2 bit 3). That leaves 54 keys. The enum has 54, and a test derives that figure from the table copied into it. The brief's 56 counted the positions, not the keys.
- **Two positions that are one key each.** Both Shift keys are one position in the matrix, so there is one `Shift`. Caps Lock and Func are one key, so there is one `CapsLock`. Pressed together with another key it is the same position, and the matrix cannot tell a function key from a lock toggle: that is the OS's job. A test presses Caps Lock with K and sees only the two positions.
- **A key is a state, not a count.** Pressing a key twice and releasing it once leaves it up, as a switch does.
- **A value that is not a key is refused.** The enum value is `column | (bit << 4)`, so a cast can name a position that is not connected. `Down`, `Up` and `IsDown` throw for it rather than set a bit that nothing can read.
- **Two clean-ups from the third task's review.** A test now writes the display mode at `$FEA7` and `$FEF7` and sees it in `Ula.Mode`, so the mirroring of the control register is held and not only the status register's. And `Ula.PowerOn` is internal: nothing outside the assembly needs it.

## The machine, and the OS boots to the prompt (fifth task, 5 October)

The fifth task joined the bus to the core as `ElectronMachine` and ran the real OS 1.00 for the first time, headless. The check reads mode 6 text straight out of screen memory at `$6000`, matching each 8-byte cell against the OS ROM's own font, so it needs no display and is not circular: the glyphs come from the ROM, and the text from the ROM running on the CPU.

- **It booted on the first run.** The plan expected faults here (the paging sequence, the power-on flag, the interrupt timing, the RAM clear) and none turned up: tasks 2 to 4 had already been written to the sheet's rules, and the boot exercised all of them. The screen is the sheet's table in section 10b row for row: blank, `Acorn Electron ` and the bell glyph, blank, `BASIC`, blank, `>`. The bell glyph's eight bytes are at `$6000 + 1 x 320 + 15 x 8`, and a test compares them with both the sheet's figures and the ROM at `$C42B`.
- **The ULA writes came in the sheet's order.** A throwaway probe (a test, not kept) logged every store to `$FE0n` during the boot. The sequence matched section 10c write for write: `$FE00` and `$FE05` at reset, the two `$0C`, n pairs after the RAM clear, `$B4` to `$FE07` and `$FE04`, `$0C` to `$FE00`, `$B0`, four writes of `$00` to `$FE06`, then the ROM scan of all sixteen slots.
- **The boot is about twice as fast in cycles as on the machine, and that is expected.** The same probe saw the RAM clear end at about 507,500 cycles, the banner at about 758,500 and the prompt at about 789,500 (5 October, the probe stepping the machine from power on and reading screen memory every few hundred cycles). The sheet's model, which had the display contention, gave about 1,040,000 for the RAM clear and about 1,320,000 for the banner. The difference is the contention, which is task 6: the ULA starts in mode 0, and in mode 0 most RAM accesses wait for the display. Until then every RAM access costs 2 or 3 cycles whatever the mode. Nothing was tuned for it. The same cause moves the first ULA write from the model's cycle 98 to about 30 here: the reset code's first RAM store, `STA $0D00`, falls in the display window of line 0 [inferring from section 4b].
- **The cursor is drawn in software, and reads as an underscore.** The Electron has no hardware cursor. The OS's routine at `$D6DE` swaps line 7 of the cursor cell with a byte it keeps at `$080C`, and flashes the cursor by swapping it back. On a blank cell that line is `$FF`, which is exactly the font's `_`. So the cell after the prompt reads as `_` or as a space, depending on when it is looked at, and the tests accept either. The sheet's "`>` and the cursor in column 1" is right; this is what it looks like in memory.
- **BREAK.** `PressBreak` resets the CPU and nothing else, as the controller ruled: RAM and every ULA register are kept, and the power-on flag is not raised. The OS then takes the BREAK path. `$028D` is 0, so the banner has no bell glyph, BASIC and the prompt come back, and a byte poked at `$1234` before the BREAK is still there. A test steps to the instruction after the OS's `LDA $FE00` at `$D8E8` and reads the accumulator: bit 1 is 1 after a power on and 0 after a BREAK, and the power-on read cleared it.
- **The ROM scan is what starts BASIC.** A test-only seam (an internal option, defaulting to slots 10 and 11) builds the machine with BASIC moved or missing. With none, row 3 is `Language?`, the OS's error at `$DAA6`, with the cursor after it and no prompt. With BASIC in slot 10 only, or 11 only, it boots to the prompt, and the OS's record of the language at `$024B` is that slot. With both, it is 11, as section 2c says.
- **Typing.** The test session types through the matrix: one key held for a frame, released for a frame, Shift held across the key where the legend needs it. The table is written from section 7b and the Electron's key legends, and a test holds it against the OS ROM's own tables: the key table at `$EDD3` and the table the key handler reads with `LDA $EFB7,X` at `$EBA3`, which gives the Shift legend of every key. A typed `PRINT 6*7` prints `42`.
- **The IRQ line reaches the CPU between instructions.** The machine copies the ULA's line into the core after each instruction, caught up to that cycle first. A real 6502 samples it a cycle before an instruction ends, so an interrupt can be taken one instruction later here. It is recorded in `docs/known-differences.md`.
