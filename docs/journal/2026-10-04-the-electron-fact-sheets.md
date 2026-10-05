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
- **The boot finishes earlier in cycles than the sheet's model, and that is expected.** The sheet's model, which had the display contention, took about 1,040,000 cycles for the RAM clear and about 1,320,000 (0.66 s) to the finished screen (`ula.md` s10a, s10b). This machine does not model contention yet (task 6): the ULA starts in mode 0, where most RAM accesses wait for the display, but until task 6 every RAM access costs 2 or 3 cycles whatever the mode. So the boot reaches the prompt sooner, and nothing was tuned for it. The two-million-cycle boot in the tests leaves room for the slower boot that task 6 brings.
- **The cursor is drawn in software, and reads as an underscore.** The Electron has no hardware cursor. The OS's routine at `$D6DE` swaps line 7 of the cursor cell with a byte it keeps at `$080C`, and flashes the cursor by swapping it back. On a blank cell that line is `$FF`, which is exactly the font's `_`. So the cell after the prompt reads as `_` or as a space, depending on when it is looked at, and the tests accept either. The sheet's "`>` and the cursor in column 1" is right; this is what it looks like in memory.
- **BREAK.** `PressBreak` resets the CPU and nothing else, as the controller ruled: RAM and every ULA register are kept, and the power-on flag is not raised. The OS then takes the BREAK path. `$028D` is 0, so the banner has no bell glyph, BASIC and the prompt come back, and a byte poked at `$1234` before the BREAK is still there. A test steps to the instruction after the OS's `LDA $FE00` at `$D8E8` and reads the accumulator: bit 1 is 1 after a power on and 0 after a BREAK, and the power-on read cleared it.
- **The ROM scan is what starts BASIC.** A test-only seam (an internal option, defaulting to slots 10 and 11) builds the machine with BASIC moved or missing. With none, row 3 is `Language?`, the OS's error at `$DAA6`, with the cursor after it and no prompt. With BASIC in slot 10 only, or 11 only, it boots to the prompt, and the OS's record of the language at `$024B` is that slot. With both, it is 11, as section 2c says.
- **Typing.** The test session types through the matrix: one key held for a frame, released for a frame, Shift held across the key where the legend needs it. The table is written from section 7b and the Electron's key legends, and a test holds it against the OS ROM's own tables: the key table at `$EDD3` and the table the key handler reads with `LDA $EFB7,X` at `$EBA3`, which gives the Shift legend of every key. A typed `PRINT 6*7` prints `42`.
- **The IRQ line reaches the CPU between instructions.** The machine copies the ULA's line into the core after each instruction, caught up to that cycle first. A real 6502 samples it a cycle before an instruction ends, so an interrupt can be taken one instruction later here. It is recorded in `docs/known-differences.md`.

## The contention, checked against seven real timings (sixth task, 5 October)

The sixth task made the ULA hold the CPU off RAM while it fetches the display, the rule the design is built around, and then checked the whole machine against the two real Electrons' timings of a BASIC loop.

- **The tests came first and need no model.** Every expected number is from `ula.md` sections 4 and 11, or arithmetic on its rule: when the Nth access of a pure RAM stream completes, in all seven modes; how many such accesses a frame holds (19,520, 24,000 and 40,000); the histogram of their lengths (512 of 82 cycles a frame in modes 0 to 2, 400 in mode 3, none in modes 4 to 6); the longest stall, 82 from a boundary and 83 from a cycle off it, which is the 41.5 microseconds measured on a real machine; and direct cases at the edges of the window. Before the rule went in, every contended case failed and the BASIC loop ran in modes 0 to 3 at mode 4's speed.
- **The rule is the sheet's loop, as written.** A RAM or I/O access completes at the first even time at least two cycles on; then, for RAM in modes 0 to 3, while that boundary is at positions 2 to 80 of a contended line it moves on by two. The positions are the sheet's convention for the phase, which no total depends on. `Ula.FieldAndLine` gives the line and the position at any time from the frame of section 5d, and the mode is the ULA's as it is when the access starts. The faster arithmetic form waits for the speed check in the seventh task, and only if that asks for it.
- **The frame boundary.** An access that starts on the last cycle of a frame needs a boundary in the next one, at position 2 of its line 0, which is inside a window. It completes at position 82 of that line, not at position 2, and a test holds it, along with the same case at the start of the even field.
- **A read-modify-write's writes are held like any other RAM access.** `INC $1234` started at position 118 of a contended line makes its six accesses in the documented order, opcode, two address bytes, the read, the write of the old value, the write of the new. The first five take 2 cycles each and end at the next line's position 0, so the last write meets the window and takes 82. With the target in the OS ROM, the read and both writes take one cycle each: every access is judged by its own address.
- **Seven timings, against the real machines.** The test boots the OS, types the sheet's program for each mode through the keyboard matrix, runs it until `A%` is set, and reads it at `$0404`. On 5 October 2026, `dotnet test tests/Dbhq.Machines.Electron.Tests --filter "FullyQualifiedName~ForLoopAnchorTests" --logger "console;verbosity=detailed"` printed 14.71, 14.74, 14.89, 11.92, 7.08, 7.09 and 7.08 seconds for modes 0 to 6, against 14.71, 14.74, 14.89, 11.92, 7.07, 7.08 and 7.07 on hoglet's Issue 2 Electron and 14.71, 14.74, 14.90, 11.94, 7.08, 7.08 and 7.08 on davidb's Issue 4. Each is within a hundredth of both machines, against a tolerance of five hundredths.
- **The first attempt got nothing wrong, and that rests on earlier work.** The sheet records that a model which missed the dummy stack read in `RTS` and `RTI` ran 1.4 to 1.7 per cent fast in every mode. Here the core makes those reads and the second task's tests hold them, address by address. Mode 3's two blank lines under each row are left free, as the sheet says; contending them would give 14.34 seconds, and a test checks that mode 3 is nearer the real figure than that.
- **The earlier tests that timed RAM from power on had to move to mode 6.** The ULA starts in mode 0, which until now cost nothing extra. With contention, a RAM access at cycle 0 meets line 0's window and takes 82 cycles. The tests of the bare cost, the alignment loop and the `RTS` and `RTI` reads now put the ULA in mode 6 first, which has no contention; the tick count test stays in mode 0 and counts the stall's cycles too.
- **The boot is now about as slow as the sheet's model.** A throwaway probe (a test, not kept, that ran the machine ten thousand cycles at a time and looked for the prompt on row 5) saw the prompt at about 1,333,000 cycles on 5 October 2026, against the sheet's 1,320,000 or so. The two-million-cycle boot in the tests still leaves room, and was not changed.
- **The seven-mode check takes about half a minute** on this machine, so it runs with the rest of the tests and carries no trait to set it apart.

## The speed in a browser, with the bus, the ULA and the contention (seventh task, 5 October)

The seventh task measured how many 2 MHz cycles a second the machine reaches in a browser, with the ULA's interrupts and the contention judged on every RAM access, ahead-of-time compiled (AOT) and in the interpreter. The plan's rule: at least 10 times a real Electron in mode 0, carry on; 5 to 10, carry on and let the page's headroom line say so; under 5, stop and tell Dan. **The AOT median in mode 0 is 25.91 times, so it carries on.** The page needs no headroom warning on this figure.

- **What was built.** `src/Dbhq.Machines.Electron.Wasm/`, a plain .NET WebAssembly app in the BBC's shape (in `6502.slnx`), with `Load(os, basic, sampleRate)`, `Run`, `Cycles`, `Peek`, `Mode`, `SetMode` and `ScreenRow`. `ScreenRow` reads mode 6 text from RAM by matching each cell against the OS ROM's font, a copy of the test project's decoder, so the host stands alone. `SetMode(mode)` is the smallest seam for mode 0: one write of `$FE07` through the bus between instructions, so a few cycles pass, as they would for a program's own store. The bench is `bench/electron-speed/`, the BBC's bench with two ROMs (the OS from `roms/electron/` and BASIC from `roms/bbc-micro/`, each checked against its pin in `Pins.cs`) and a list of modes to time in turn. It is not run in CI.
- **The workload.** Power on, run the real OS for 4 million cycles, check the screen reads `Acorn Electron` and the bell glyph, `BASIC` and `>` (every launch printed `prompt yes`), then time `Run(2_000_000)` five times in mode 6 (the boot default, never held up by the display) and five times after `SetMode(0)` (every RAM access whose boundary falls in the display window of one of the 256 contended lines waits). Each timed run is one second of machine time. After each set the page checks the ULA is still in the mode it set (`held yes` every time), so the OS did not write `$FE07` behind the bench's back. The machine sits at the prompt, in the OS's keyboard loop with its interrupts: this is the idle loop, not a program, and it has no display, sound or tape yet.
- **Three fresh browser launches of each build and order**, five timed runs in each mode of each launch, so fifteen runs a mode. The task asked for five; the other ten show the spread. A second AOT set ran the modes in the other order (`0,6`), to see whether the order matters.
- **The machine.** The same 8 core virtual machine as the BBC's checks, Google Chrome 153.0.8010.47 headless, .NET SDK 10.0.400 with the `wasm-tools` workload. **It was fairly quiet.** `uptime` before the AOT runs: load average 1.71, 1.47, 1.82; after the AOT runs 1.56; after the interpreter runs 1.50. `top` showed the CPUs 82 per cent idle, with the .NET build server (VBCSCompiler) at about 17 per cent and the Paseo daemon at 8 per cent, and nothing else. The wasm code is one thread and seven cores were free. Runs were made 04:37:15 to 04:38:06 UTC.
- **The commands**, from the repository root:

  ```
  dotnet publish src/Dbhq.Machines.Electron.Wasm -c Release -o bench/electron-speed/publish/interpreter
  dotnet publish src/Dbhq.Machines.Electron.Wasm -c Release -p:RunAOTCompilation=true -o bench/electron-speed/publish/aot
  cd bench/electron-speed
  npm ci
  node run-in-browser.mjs publish/aot 3
  node run-in-browser.mjs publish/aot 3 2000000 4000000 5 0,6
  node run-in-browser.mjs publish/interpreter 3
  ```

  (The AOT publish needed `src/Dbhq.Machines.Electron.Wasm/obj/Release` deleted after the interpreter publish, as the BBC's README says.)

### The figures, as printed

Multiples of a 2 MHz Electron are the figure in MHz divided by two, worked in Python from the printed values. Best, median and slowest are over fifteen runs a mode.

| Build | Mode | Best | Median | Slowest |
| --- | --- | --- | --- | --- |
| AOT, modes 6 then 0 | 6, no contention | 33.898 MHz (16.95 times) | 30.864 (15.43 times) | 24.450 (12.22 times) |
| AOT, modes 6 then 0 | 0, contended | 55.710 (27.86 times) | 51.813 (25.91 times) | 31.447 (15.72 times) |
| AOT, modes 0 then 6 | 6, no contention | 33.727 (16.86 times) | 31.546 (15.77 times) | 24.125 (12.06 times) |
| AOT, modes 0 then 6 | 0, contended | 53.908 (26.95 times) | 50.378 (25.19 times) | 39.683 (19.84 times) |
| Interpreter, modes 6 then 0 | 6, no contention | 3.164 (1.58 times) | 2.821 (1.41 times) | 2.443 (1.22 times) |
| Interpreter, modes 6 then 0 | 0, contended | 4.690 (2.35 times) | 4.280 (2.14 times) | 3.488 (1.74 times) |

AOT, `node run-in-browser.mjs publish/aot 3`, started 04:37:15 UTC (the second set, with the modes in the other order, follows):

```
launch 1 boot cycles=4000006 ms=136.800 cycles_per_second=29239810 mhz=29.240
launch 1 prompt yes
launch 1 mode 6 timed 1 cycles=2000002 ms=64.200 cycles_per_second=31152679 mhz=31.153
launch 1 mode 6 timed 2 cycles=2000000 ms=65.500 cycles_per_second=30534351 mhz=30.534
launch 1 mode 6 timed 3 cycles=2000000 ms=61.500 cycles_per_second=32520325 mhz=32.520
launch 1 mode 6 timed 4 cycles=2000000 ms=60.400 cycles_per_second=33112583 mhz=33.113
launch 1 mode 6 timed 5 cycles=2000000 ms=59.000 cycles_per_second=33898305 mhz=33.898
launch 1 mode 6 held yes
launch 1 mode 0 timed 1 cycles=2000004 ms=35.900 cycles_per_second=55710418 mhz=55.710
launch 1 mode 0 timed 2 cycles=2000000 ms=38.300 cycles_per_second=52219321 mhz=52.219
launch 1 mode 0 timed 3 cycles=2000000 ms=37.500 cycles_per_second=53333333 mhz=53.333
launch 1 mode 0 timed 4 cycles=2000000 ms=43.900 cycles_per_second=45558087 mhz=45.558
launch 1 mode 0 timed 5 cycles=2000000 ms=38.000 cycles_per_second=52631579 mhz=52.632
launch 1 mode 0 held yes
launch 2 boot cycles=4000006 ms=129.800 cycles_per_second=30816687 mhz=30.817
launch 2 prompt yes
launch 2 mode 6 timed 1 cycles=2000002 ms=62.600 cycles_per_second=31948914 mhz=31.949
launch 2 mode 6 timed 2 cycles=2000000 ms=64.800 cycles_per_second=30864197 mhz=30.864
launch 2 mode 6 timed 3 cycles=2000000 ms=63.600 cycles_per_second=31446541 mhz=31.447
launch 2 mode 6 timed 4 cycles=2000000 ms=64.700 cycles_per_second=30911901 mhz=30.912
launch 2 mode 6 timed 5 cycles=2000000 ms=65.000 cycles_per_second=30769231 mhz=30.769
launch 2 mode 6 held yes
launch 2 mode 0 timed 1 cycles=2000004 ms=38.200 cycles_per_second=52356126 mhz=52.356
launch 2 mode 0 timed 2 cycles=2000000 ms=37.200 cycles_per_second=53763441 mhz=53.763
launch 2 mode 0 timed 3 cycles=2000000 ms=36.400 cycles_per_second=54945055 mhz=54.945
launch 2 mode 0 timed 4 cycles=2000000 ms=39.200 cycles_per_second=51020408 mhz=51.020
launch 2 mode 0 timed 5 cycles=2000000 ms=38.600 cycles_per_second=51813471 mhz=51.813
launch 2 mode 0 held yes
launch 3 boot cycles=4000006 ms=129.300 cycles_per_second=30935855 mhz=30.936
launch 3 prompt yes
launch 3 mode 6 timed 1 cycles=2000002 ms=81.800 cycles_per_second=24449902 mhz=24.450
launch 3 mode 6 timed 2 cycles=2000000 ms=70.800 cycles_per_second=28248588 mhz=28.249
launch 3 mode 6 timed 3 cycles=2000000 ms=71.700 cycles_per_second=27894003 mhz=27.894
launch 3 mode 6 timed 4 cycles=2000000 ms=66.500 cycles_per_second=30075188 mhz=30.075
launch 3 mode 6 timed 5 cycles=2000000 ms=77.500 cycles_per_second=25806452 mhz=25.806
launch 3 mode 6 held yes
launch 3 mode 0 timed 1 cycles=2000004 ms=45.600 cycles_per_second=43859737 mhz=43.860
launch 3 mode 0 timed 2 cycles=2000000 ms=50.400 cycles_per_second=39682540 mhz=39.683
launch 3 mode 0 timed 3 cycles=2000000 ms=63.600 cycles_per_second=31446541 mhz=31.447
launch 3 mode 0 timed 4 cycles=2000000 ms=60.100 cycles_per_second=33277870 mhz=33.278
launch 3 mode 0 timed 5 cycles=2000000 ms=49.400 cycles_per_second=40485830 mhz=40.486
launch 3 mode 0 held yes
```

AOT, modes in the other order, `node run-in-browser.mjs publish/aot 3 2000000 4000000 5 0,6`, started 04:37:22 UTC (the same session, straight after the first set; these are the lines it printed):

```
launch 1 boot cycles=4000006 ms=119.000 cycles_per_second=33613496 mhz=33.613
launch 1 prompt yes
launch 1 mode 0 timed 1 cycles=2000000 ms=39.700 cycles_per_second=50377834 mhz=50.378
launch 1 mode 0 timed 2 cycles=2000004 ms=43.100 cycles_per_second=46403805 mhz=46.404
launch 1 mode 0 timed 3 cycles=2000004 ms=39.300 cycles_per_second=50890687 mhz=50.891
launch 1 mode 0 timed 4 cycles=2000000 ms=38.200 cycles_per_second=52356021 mhz=52.356
launch 1 mode 0 timed 5 cycles=2000000 ms=39.000 cycles_per_second=51282051 mhz=51.282
launch 1 mode 0 held yes
launch 1 mode 6 timed 1 cycles=2000000 ms=61.000 cycles_per_second=32786885 mhz=32.787
launch 1 mode 6 timed 2 cycles=2000002 ms=60.800 cycles_per_second=32894770 mhz=32.895
launch 1 mode 6 timed 3 cycles=2000000 ms=63.000 cycles_per_second=31746032 mhz=31.746
launch 1 mode 6 timed 4 cycles=2000002 ms=63.400 cycles_per_second=31545773 mhz=31.546
launch 1 mode 6 timed 5 cycles=2000003 ms=61.500 cycles_per_second=32520374 mhz=32.520
launch 1 mode 6 held yes
launch 2 boot cycles=4000006 ms=125.800 cycles_per_second=31796550 mhz=31.797
launch 2 prompt yes
launch 2 mode 0 timed 1 cycles=2000000 ms=37.800 cycles_per_second=52910053 mhz=52.910
launch 2 mode 0 timed 2 cycles=2000004 ms=44.100 cycles_per_second=45351565 mhz=45.352
launch 2 mode 0 timed 3 cycles=2000004 ms=37.100 cycles_per_second=53908464 mhz=53.908
launch 2 mode 0 timed 4 cycles=2000000 ms=41.500 cycles_per_second=48192771 mhz=48.193
launch 2 mode 0 timed 5 cycles=2000000 ms=39.100 cycles_per_second=51150895 mhz=51.151
launch 2 mode 0 held yes
launch 2 mode 6 timed 1 cycles=2000000 ms=69.900 cycles_per_second=28612303 mhz=28.612
launch 2 mode 6 timed 2 cycles=2000002 ms=66.000 cycles_per_second=30303061 mhz=30.303
launch 2 mode 6 timed 3 cycles=2000000 ms=64.400 cycles_per_second=31055901 mhz=31.056
launch 2 mode 6 timed 4 cycles=2000002 ms=65.500 cycles_per_second=30534382 mhz=30.534
launch 2 mode 6 timed 5 cycles=2000003 ms=66.100 cycles_per_second=30257232 mhz=30.257
launch 2 mode 6 held yes
launch 3 boot cycles=4000006 ms=119.600 cycles_per_second=33444866 mhz=33.445
launch 3 prompt yes
launch 3 mode 0 timed 1 cycles=2000000 ms=44.900 cycles_per_second=44543430 mhz=44.543
launch 3 mode 0 timed 2 cycles=2000004 ms=48.100 cycles_per_second=41580125 mhz=41.580
launch 3 mode 0 timed 3 cycles=2000004 ms=50.400 cycles_per_second=39682619 mhz=39.683
launch 3 mode 0 timed 4 cycles=2000000 ms=46.300 cycles_per_second=43196544 mhz=43.197
launch 3 mode 0 timed 5 cycles=2000000 ms=37.700 cycles_per_second=53050398 mhz=53.050
launch 3 mode 0 held yes
launch 3 mode 6 timed 1 cycles=2000000 ms=68.200 cycles_per_second=29325513 mhz=29.326
launch 3 mode 6 timed 2 cycles=2000002 ms=82.900 cycles_per_second=24125476 mhz=24.125
launch 3 mode 6 timed 3 cycles=2000000 ms=59.300 cycles_per_second=33726813 mhz=33.727
launch 3 mode 6 timed 4 cycles=2000002 ms=60.700 cycles_per_second=32948962 mhz=32.949
launch 3 mode 6 timed 5 cycles=2000003 ms=61.600 cycles_per_second=32467581 mhz=32.468
launch 3 mode 6 held yes
```

Interpreter, `node run-in-browser.mjs publish/interpreter 3`, started 04:37:36 UTC:

```
launch 1 boot cycles=4000006 ms=3005.700 cycles_per_second=1330807 mhz=1.331
launch 1 prompt yes
launch 1 mode 6 timed 1 cycles=2000002 ms=708.900 cycles_per_second=2821275 mhz=2.821
launch 1 mode 6 timed 2 cycles=2000000 ms=818.600 cycles_per_second=2443196 mhz=2.443
launch 1 mode 6 timed 3 cycles=2000000 ms=699.800 cycles_per_second=2857959 mhz=2.858
launch 1 mode 6 timed 4 cycles=2000000 ms=723.800 cycles_per_second=2763194 mhz=2.763
launch 1 mode 6 timed 5 cycles=2000000 ms=645.300 cycles_per_second=3099334 mhz=3.099
launch 1 mode 6 held yes
launch 1 mode 0 timed 1 cycles=2000004 ms=465.000 cycles_per_second=4301084 mhz=4.301
launch 1 mode 0 timed 2 cycles=2000000 ms=467.300 cycles_per_second=4279906 mhz=4.280
launch 1 mode 0 timed 3 cycles=2000000 ms=479.000 cycles_per_second=4175365 mhz=4.175
launch 1 mode 0 timed 4 cycles=2000000 ms=437.800 cycles_per_second=4568296 mhz=4.568
launch 1 mode 0 timed 5 cycles=2000000 ms=466.400 cycles_per_second=4288165 mhz=4.288
launch 1 mode 0 held yes
launch 2 boot cycles=4000006 ms=2865.300 cycles_per_second=1396016 mhz=1.396
launch 2 prompt yes
launch 2 mode 6 timed 1 cycles=2000002 ms=632.100 cycles_per_second=3164059 mhz=3.164
launch 2 mode 6 timed 2 cycles=2000000 ms=663.800 cycles_per_second=3012956 mhz=3.013
launch 2 mode 6 timed 3 cycles=2000000 ms=658.100 cycles_per_second=3039052 mhz=3.039
launch 2 mode 6 timed 4 cycles=2000000 ms=651.800 cycles_per_second=3068426 mhz=3.068
launch 2 mode 6 timed 5 cycles=2000000 ms=646.900 cycles_per_second=3091668 mhz=3.092
launch 2 mode 6 held yes
launch 2 mode 0 timed 1 cycles=2000004 ms=447.400 cycles_per_second=4470282 mhz=4.470
launch 2 mode 0 timed 2 cycles=2000000 ms=437.800 cycles_per_second=4568296 mhz=4.568
launch 2 mode 0 timed 3 cycles=2000000 ms=426.400 cycles_per_second=4690432 mhz=4.690
launch 2 mode 0 timed 4 cycles=2000000 ms=474.800 cycles_per_second=4212300 mhz=4.212
launch 2 mode 0 timed 5 cycles=2000000 ms=451.400 cycles_per_second=4430660 mhz=4.431
launch 2 mode 0 held yes
launch 3 boot cycles=4000006 ms=2912.600 cycles_per_second=1373345 mhz=1.373
launch 3 prompt yes
launch 3 mode 6 timed 1 cycles=2000002 ms=799.600 cycles_per_second=2501253 mhz=2.501
launch 3 mode 6 timed 2 cycles=2000000 ms=760.900 cycles_per_second=2628466 mhz=2.628
launch 3 mode 6 timed 3 cycles=2000000 ms=789.600 cycles_per_second=2532928 mhz=2.533
launch 3 mode 6 timed 4 cycles=2000000 ms=759.000 cycles_per_second=2635046 mhz=2.635
launch 3 mode 6 timed 5 cycles=2000000 ms=737.000 cycles_per_second=2713704 mhz=2.714
launch 3 mode 6 held yes
launch 3 mode 0 timed 1 cycles=2000004 ms=572.000 cycles_per_second=3496510 mhz=3.497
launch 3 mode 0 timed 2 cycles=2000000 ms=545.000 cycles_per_second=3669725 mhz=3.670
launch 3 mode 0 timed 3 cycles=2000000 ms=561.500 cycles_per_second=3561888 mhz=3.562
launch 3 mode 0 timed 4 cycles=2000000 ms=564.400 cycles_per_second=3543586 mhz=3.544
launch 3 mode 0 timed 5 cycles=2000000 ms=573.400 cycles_per_second=3487967 mhz=3.488
launch 3 mode 0 held yes
```

### What the figures say

- **Mode 0 is faster than mode 6 in multiples of real time, and that is the contention working, not a fault.** In mode 0 the idle loop spends about half its time held off RAM, so the CPU runs half as many instructions in the same machine second. A throwaway console program (not kept; it built the machine from the pinned ROMs, booted four million cycles, wrote `$FE07` and counted `Step` calls over two million cycles) printed 414,005 instructions in mode 6 and 209,002 to 209,012 in mode 0, with `Ula.Mode` as set. The cost is the instructions, so the cheaper second is the one with fewer of them. Per instruction, mode 0 costs more (about 185 ns against 157 ns, from the AOT medians and the instruction counts): the question on every access is there, and it is small.
- **So for the idle loop, mode 6 is the heavier case for the browser, not mode 0.** The AOT median there is 15.43 times (15.77 in the other order), and its slowest readings, 12.22 and 12.06 times, are the lowest of any AOT set. Every AOT reading in either mode is over 10 times. Code that lives in ROM and makes few RAM accesses would run more instructions a second than the mode 6 idle loop, and nothing here measures that. The BBC's figure to beat was about 10 times at its worst and 13 at its boot screen, and this machine is above both. **That comparison is not like for like:** the BBC's worst case was a dense teletext page with all four sound channels sounding, and this is an idle prompt with no display drawing and no sound, so it flatters this machine. What it does show is that the bus asking a question on every access does not by itself bring the machine near ten times.
- **The interpreter is above real time in both modes** (1.41 and 2.14 times), which the BBC's was not (0.64 times at its first check).
- **The order of the modes does not matter**: the `0,6` set is within a few per cent of the `6,0` set in both modes.

### What this does not say

One machine at a fairly quiet hour, one browser, three launches a build, and an idle loop for the workload. The 25.9 times for mode 0 is for the idle loop at the prompt only, and is likely optimistic for busier programs: the idle loop is held off RAM about half the time there, so code that runs more instructions per machine second would come out lower. The figures are for the machine as it stands after task 6, with no display, sound or tape: the display, the sound and the tape will add to them, and the plan's later tasks measure again. The plain loop of the contention rule was fast enough, so the arithmetic form the plan keeps in reserve was not needed and was not written.

## The display, modes 0 to 6 (eighth task, 5 October)

The eighth task gave the ULA its picture: the start address, the mode, the palette and the address generator of `ula.md` section 5, drawing into a framebuffer of 640 by 256 pixels. The tests now read the boot screen off that picture, cell by cell against the OS ROM's font, and no longer only out of screen memory.

- **One field's lines, not two woven.** The BBC's framebuffer is 640 by 512, its two fields woven, because its CRTC puts them half a line apart. The Electron's two fields show the same lines (section 5d), so the picture is the 256 lines of one field, a row of pixels a line, and the page will stretch it to 4:3. Both fields draw into the same rows. `Frames` moves on at the end of the odd field, once a frame, which is 25 times a second of machine time.
- **A line at a time, lazily, and why.** The display does nothing on the cycle. Each line is drawn whole from the registers and RAM as they are at the cycle it starts, and it is drawn on demand: before a write to the start address, the mode or the palette; before a store to RAM that a line still to be drawn could show; when the ULA's interrupts are caught up, four times a frame; and when the picture is read. The store check is two comparisons on every RAM write, the address against the lowest address an undrawn line could fetch and the clock against the next line's start, so the idle loop's stores to its workspace below `$3000` cost nothing more. The alternative, fetching each byte in its own cycle, would cost every cycle a check; the speed milestone left headroom, but a line at a time is what the BBC does and nothing in the boot or BASIC needs finer. The rule that falls out of it: a write at cycle `t` is seen by every line that starts at `t` or later. That makes a mode write take effect at the end of the line it lands in, which is what section 5e says, and a palette write from the next line, where the sheet says at once. Both are in `docs/known-differences.md`.
- **The start address is read once a field, and a test of mine forgot it.** The bus test for the store check first set the mode and the start address and then stored in the same field. Line 0 had already started when the registers were written, so the whole field kept the old start, `$0000`, which the rule for a start below `$0800` turns into mode 0's `$3000`; the stored byte was not on screen. The model was right by section 5e; the test now does its stores in the next field. Two other first failures were also the tests' arithmetic and not the model: the wrap test expected pixel 511 to be dark, but byte 31 of a mode 4 line covers pixels 496 to 511, so its last pixel is 510 and 511; and the same bus test looked at line 41 before line 41 had started, when it could not yet have been drawn.
- **Red needs both palette registers.** The plan's palette test wrote `$FE09` with bit 2 alone clear and expected colour 8 to be red. By the table in section 5c, `$FE09` bit 2 is only colour 8's red: its green and blue are bits 2 and 6 of `$FE08`, which the OS's `$11` leaves on, so colour 8 stays white. The test says so and then writes `$FE08` = `$FF` as well, and colour 8 is red. The palette is built from the sheet's table as text, a row a register, so the code and the table can be compared line by line.
- **The 10-line rows.** Modes 3 and 6 have 10 lines to a character row, 8 of data and 2 blank, and the row base moves on by 8 times the bytes a line at the end of line 9, not by 10 lines' worth: the address counter does not count the blank lines (section 12 item 8 takes this from one source). 25 rows of 10 are 250 lines; the last six of the 256 are black. A test fills mode 3's and mode 6's memory with lit bytes and checks lines 8 and 9 of each row are black, and another that row 24 ends at `$7F3F` in mode 6 (section 5a).
- **The cursor is software, and that limits what a picture can say.** The OS draws the cursor by swapping line 7 of each byte of the cursor's cell with bytes it keeps at `$080C` (`$D6DE`), so the decode is told where the cursor is, from the OS's variables at `$0318` and `$0319`, and in that cell it also accepts a reading of lines 0 to 6 alone. Four pairs of glyphs in the font differ only in line 7: space and underscore, comma and full stop, colon and semicolon, `g` and `q`. Under a cursor that is shown, the second reading cannot tell them apart and reads as unknown, and a space and an underscore always read as a space; a test holds the four pairs against the font. The bell glyph after the banner is no character, so it reads as unknown too, and a separate test checks its sixteen by ten pixels against the eight bytes at `$C42B`.
- **The boot, read off the picture.** The mode 6 boot screen reads as section 10b says, row for row. Its twin, reading the same rows out of screen memory, is kept, so a fault in the display cannot hide a fault in the boot. A throwaway mutation (the 1-bit pixels drawn right to left, not kept) turned thirteen of the boot tests red and left the screen memory test green. Then `MODE 0` to `MODE 5` are typed in turn: in each, the prompt is at the top left of the picture, pixel for pixel the font's `>`, the grid is the OS's own (`$C3B4` and `$C3AD`), and the start address the ULA was given is the one the OS's tables at `$C3FB` and `$C40B` give for the mode. `RunUntilPrompt` waits for the prompt to be the last row with text, the OS's cursor beside it, and the screen unchanged a field later.
- **The display's cost was not measured.** The speed milestone was for the machine without a picture; the browser check in a later task measures again with it.

## The sound (ninth task, 5 October)

The ninth task gave the ULA its one-bit sound (`ula.md` section 8): `UlaSound` for the divider and the samples, and a `SoundBuffer` shaped like the BBC's for the page to drain.

- **Nothing on the cycle, and no event for the bus.** Between two writes the output is a square wave whose every edge is a known sum, `start + k x 32 x (S + 1)`, so the sound walks the toggles that have come due when something asks: a write to `$FE06` or `$FE07`, or the page counting or reading the buffer. I considered putting the next toggle into the ULA's `NextEvent`, which the bus compares on every access, and left it out: at the fastest pitch a toggle is 96 cycles apart, and the comparison would fire for work nobody has asked for yet. The ring keeps the newest second whatever the gap, and a long gap is cheap, because a run of one level is made in one go (an hour of silence is counted in a single step, which a test holds).
- **A sample is the mean of the level over its interval, in exact integers.** The boundaries are `k x 2,000,000 / sampleRate` cycles. The position is kept as cycles times the sample rate, so a sample is always 2,000,000 units long and the count never drifts: one second of machine time at 44,100 a second is exactly 44,100 samples.
- **Silence is 0, so the amplifier is modelled as coupled.** The mean is 0 to 1, and an output that sits high is not a sound. A sample straight off the mean would leave a held level as a constant, and a step into it as a click. So each sample goes through a one-pole high-pass at 10 Hz, as an AC-coupled amplifier would do: a square wave comes out as a swing of about plus and minus a half, and a held level decays to exactly 0 and stays there. At 122 Hz, the lowest pitch, a half wave droops to about three quarters of its height, which is what coupling does to a low square wave. The sheet says the filtering in the amplifier is open (section 12 item 9), so this is a choice, recorded in `known-differences.md`. The BBC's chip leaves its DC in; that is its own choice and is not copied.
- **The two open points of item 9 are chosen, not found.** A write to `$FE06` restarts the divider, and sound mode starts the divider when it is entered and keeps the level the output last held. A write to `$FE07` that stays in sound mode must not restart it: the OS writes that register for the screen mode and the LED as well (section 10c), and a test holds it.
- **The start-up beep is a property of the ROM.** The test types `VDU 7` and steps through the beep an instruction at a time, noting the counter before and after any step in which the output toggled. It asserts the range, 20 Hz to 20 kHz, and not a number. To see what the OS chose, I temporarily swapped the test's final assertion for `Assert.Fail` carrying the counters it had noted, ran `dotnet test tests/Dbhq.Machines.Electron.Tests --filter "FullyQualifiedName~StartUpBeep"` and put the assertion back (not kept): the one counter seen was S = 58, with 572 toggles in the beep. By the sheet's formula that is `1 MHz / (32 x 59)`, 529.7 Hz, a pitch a person hears. A second throwaway, a counter of every write to `$FE06` made in sound mode during the same run, saw exactly one, the write of 58, so the OS does not rewrite the counter on its 100 Hz tick during this beep. A control holds the idle prompt silent. The real boot writes `$FE06` as the tape's baud setting (counter 0, not sound mode), and a toggle counts only in sound mode, so the boot makes none.
- **The buffer does not depend on when the page reads it.** A review found one way it did. The first sample after the coupling had let a step go was snapped to exactly 0 when it fell inside a whole-sample run, but left at about a millionth when a read landed mid-sample and the sample was finished on its own. The snap now lives in the one routine that makes a sample, and the run is only a shortcut that starts once the last sample was exactly 0. Two kinds of test hold it: a script run jumping from write to write against the same script caught up in uneven steps and drained in pieces, compared for exact equality, and a read at every cycle for 200 either side of the sample where the coupling settles. The second was red against the old code, at a read in cycle 473,742 of its run.
- **Power on is in sound mode with S = 0.** Section 10 says the ULA resets to sound mode; with S of 0 the level is constant, so it is silent until a counter of 2 or more is written. Section 12 item 4 leaves the real power-on state open.
- **A sample rate that cannot be used is refused at construction.** The buffer holds one second, so its size is the rate: 1 to 384,000, with `ArgumentOutOfRangeException` for anything else, from the bus and from the machine.

## What the OS does on tape, found by running it (tenth task, 5 October)

The tape sheet left ten things open in its section 7, and the first two decide how the cassette is written: how long the leader is, and how the OS makes carrier tone. The tenth task found them by running the real OS ROM and BASIC against a test-only probe on the ULA's cassette registers, and wrote the answers into [`tape.md`](../electron/facts/tape.md) s7 before any tape code exists. Items 1 to 7 and 9 are settled; 8 (300 baud) and 10 (tone-level facts) stay open.

- **A seam, not the cassette.** The probe needed to see the OS's accesses and answer `$FE04`, so the ULA got the smallest seam that would do: an internal `ITapeTap` with two members, told of writes to `$FE04` to `$FE07` and asked for the byte a read of `$FE04` gives. With no tap set nothing changes, and a test holds that `$FE04` still reads as `$FE`. The probe itself lives in the test project. It acts at its own times through a hook the test session calls after every instruction, so an interrupt it raises reaches the CPU at the next instruction boundary, as the ULA's own do. The cassette task replaces the seam.
- **The answer to the main question: carrier is the idle line.** The likeliest answer in the plan was the right one. For the leader, between blocks and after the last block the OS writes nothing to `$FE04`. It turns the tape interrupts off and counts fields on its tape timer, which the display-end handler decrements once a field: 255 fields (5.1 s) before the first block, 45 between blocks, 265 after the last. Then it turns transmit-empty on with the sync byte waiting, and the interrupt handler sends the block's bytes. No `$FF` bytes and no dummy byte. The design's boundary at the ULA's serial register stands, and the cassette task's output rule is the one the plan expected: carrier is the time the line is idle.
- **One correction to the plan's pace.** The plan, and the brief's probe, set transmit-empty nine bit times after each write. Run that way, the OS wrote a byte every 15,140 to 15,522 cycles (scratch, not kept), which is faster than the 16,640 cycles ten bits take to leave the line: it writes the next byte soon after transmit-empty, inside the stop bit of the byte before. How soon is printed by the tests: 164 to 176 cycles over the 42 writes of the one-block `SAVE`, and 128 to 408 over the 609 writes of the three-block `*SAVE`, from `dotnet test tests/Dbhq.Machines.Electron.Tests --filter "FullyQualifiedName~Dbhq.Machines.Electron.Tests.Tape" --logger "console;verbosity=detailed"` on 5 October. Both tests assert every latency is at least 0 and below one bit time, 1,664 cycles. **The conclusion rests on that**: a write below one bit time after transmit-empty lands in the stop bit, so the byte must wait for it to end. A real ULA cannot put two bytes on the line at once, so the probe now starts a byte at the later of its write and the end of the byte before, and sets transmit-empty nine bit times after the byte starts. The bytes then go out back to back. This changes the cassette task's output side: its transmit-empty must count from the byte's start, not from the write. For a write to an idle line the two are the same, so the plan's first port test still holds; a write during a stop bit is where they differ. The task's report raises it for Dan.
- **The CRC on real files.** The sheet's CRC had been proved against a model of the OS's routine. Now the OS has written four blocks through its own routine, one for `SAVE "TEST"` and three for a 513-byte `*SAVE`, and all eight CRCs in them are the ones the sheet's loop gives.
- **The rest, in short.** Blocks hold 256 bytes (513 bytes went out as 256, 256 and 1). The next-file address is always 0. `SAVE` never sets the locked bit; a block with it set makes `LOAD` say `Locked`. Names are at most ten characters, and eleven gets `Bad string` before the motor starts. A bad header CRC and a bad data CRC both say `Data?` (the ROM has no `Header?`), a block out of order says `Block?`, and each is followed by `Rewind tape` and a new search. A missing file is not reported at all: the OS shows the catalogue line of each file it passes and searches on until Escape. A `SAVE` of a one-line program takes 10.76 s from RETURN to the motor going off, and loading it back 5.56 s, because a load takes as long as the tape up to the end of the file's last block.
- **The round trip already works, through the probe.** The tape the OS wrote was played back through the probe's input side, `LOAD "TEST"` then `LIST` gave `10 PRINT "HELLO"`, and the test asserts it. That is not yet the design's round trip, which needs the cassette, the UEF writer and the reader, but it shows the OS loads what it saved at the pace the sheet gives.
- **How it was found.** Scratch tests, not kept, ran each case and wrote a log of every cassette access to a file under `/tmp`, which I read; the ROM was disassembled around what the log showed. The committed tests then hold each finding as an assertion with the ROM and sheet numbers it depends on, and the timing tests print their figures: `dotnet test tests/Dbhq.Machines.Electron.Tests --filter "FullyQualifiedName~Dbhq.Machines.Electron.Tests.Tape" --logger "console;verbosity=detailed"`, run on 5 October. Two throwaway mutations, not kept, checked that the tests can fail: the CRC polynomial changed to `$8005` turned the block test red, and the pace reverted to counting from the write turned the back-to-back test red.
- **What the probe chose and nobody knows.** In input mode the probe raises high-tone-detect every ten bit times while carrier lasts, because the sheets do not say whether the ULA raises it again once the OS has cleared it. The OS loaded every tape that way. The question is left open in the sheet for the cassette task.
- **Cost.** The probe tests run in three classes side by side, so they add less to the suite than their sum. Each SAVE is run once and shared, and the broken tapes play behind a one-second leader instead of five, which the OS does not mind.
- **Review, the same day.** The review found that the first write-up quoted the latency as 164 to 176 cycles from a scratch log with no command to repeat it, and that the scratch spacing figure of 15,522 cycles meant a latency of 546 in that run, outside the quoted range. Both were right. The tests now print the least and most latency for both SAVEs and bound every one below a bit time; the wider range above is what they print. The review also had the out-of-order and CRC cases assert every row from the first `Searching` on, catalogue lines included, rather than only the message; it had the high-tone question numbered as item 11 and tagged as a guess; and it asked why the gap between blocks measures 43.8 fields against the ROM's 45. The count starts when the OS writes a block's last byte, before it leaves the line, and each wait starts partway through a field, and the test's slack cannot tell 44 from 45, so the 45 rests on the ROM. The sheet also now says how the writer turns carrier in CPU cycles into 2400 Hz cycles: divide by 832 and round to the nearest.

## The UEF reader and writer, the block parser and the CRC (eleventh task, 5 October)

The eleventh task wrote the tape's file side: `TapeCrc`, `TapeBlocks` (parse and encode), `UefReader` and `UefWriter`, in `src/Dbhq.Machines.Electron/Tape/`, with the events (`Carrier`, `TapeByte`, `Silence`) and one place for the tape's timing constants (`TapeTiming`: 832, 1,664 and 16,640 CPU cycles). The recorder that turns the ULA's output into events is the next task.

- **What the specification says a simple reader needs, and this reader does exactly that.** S2's "Simplified Usage" asks for chunks `&0100`, `&0110` and `&0111` and 1200 baud assumed. The reader does those, reads the two gap chunks as silence, and skips every other chunk by its length: the origin, target machine, inlay, position marker, security cycles, phase, the multiplexed copies, and the disc, ROM and snapshot chunks. A file of nothing but disc and ROM chunks reads as an empty tape.
- **What it refuses, naming the chunk.** `&0117` set to anything but 1200, `&0102` or `&0104` carrying data, `&0113` with a base frequency other than 1200, any major version above 0, a gap that is not a length of time, and a chunk whose length is wrong for its kind. Each is a `UefException` with a message that says which chunk, at which offset, and why.
- **What it does with damaged files.** Every damaged input is a `UefException` and nothing else. A chunk of length `0xFFFFFFFF` is refused before anything is allocated for it. A data chunk of a million bytes reads in well under a second, 362 ms with the test's own setup on 5 October (a test with a two second limit holds it). A random-damage test flips and cuts a valid file 3,000 times, plain and gzipped, from a fixed seed, and asserts nothing but a tape or a `UefException` ever comes out.
- **A cut gzip stream is silent in .NET.** `GZipStream` does not throw when a stream ends early, or when a bit inside it is wrong: cut at every point of a 50,000 byte stream, down to its 12th byte, it returned what it had and reported the end. So the reader checks the gzip trailer itself, the CRC-32 and the length of what was inflated, and says the tape is cut short or damaged when they do not match. The cost is that a gzip file of several members would be refused; a UEF is one file's worth of one member.
- **A gzip bomb is bounded, not avoided.** A stream of a few kilobytes can ask for any size, so the reader refuses one that inflates past 16 MiB, with a message. A tape of half an hour a side is a few hundred kilobytes. A test inflates 40 MB of zeros from about 40 KB of gzip and checks that it is refused and that the reader allocated less than four times the limit.
- **A mistake in the sheet, found by doing the arithmetic.** The sheet said chunk `&0111`'s dummy byte is "a `$55` pattern". The ten bits `0 0 1 0 1 0 1 0 1 1` are a start bit, `0 1 0 1 0 1 0 1` and a stop bit, and the data bits go down the wire least significant bit first, so the byte is `0b10101010`, `$AA`. S2 says "always `&AA`". `$55` would go out as `1 0 1 0 1 0 1 0`. The reader gives `$AA`, a test checks the bits, and the sheet is corrected.
- **A second one: the `&0112` formula.** The sheet, copying S2, gives an integer gap as `1 / (2 x n x base frequency)` seconds, which gets shorter as n grows, so it cannot be right as written. S2 calls n "a rest length counted relative to the base frequency". The reader takes n half cycles of 1200 Hz, `n / 2,400` seconds, so 2,400 is one second, 2,000,000 CPU cycles. This is a reading, not a finding: no tape in hand uses the chunk, and the writer never writes it.
- **Carrier is split and joined.** A carrier chunk holds up to 65,535 cycles, 27 seconds. The writer splits a longer carrier into chunks and the reader joins carrier chunks that follow each other, so a carrier of any length reads back as it was written. Real carriers are about 12,700 cycles at most (the 5.28 s after a last block).
- **The writer's gap is a float.** A `Silence` is written as `&0116`, seconds as an IEEE single. A single has 24 bits of precision, so a silence reads back to the cycle up to about four seconds and to within a cycle in 16 million after that. The OS never makes a silence; only a recorder that wants one will.
- **The block parser.** It reads blocks back to back, as the OS sends them, checks both CRCs and says which field and which offset when a stream stops short or a CRC is wrong, naming the block. A block has a data CRC when it has data and bit 6 of its flag is clear. The next-file address is read and kept and never checked. A name is 1 to 10 characters, as task 10 found.
- **The CRC, checked three ways, none by running the model.** The standard check value `$31C3` for `123456789`; the AUG's example data, `$5D65`, which is the `&655D` its `EQUW` stores read the other way round; and the sheet's block vector, header `$09D8` and data `$3994`, which also gives the 30 bytes `Encode` must produce. A fourth check is a property: data followed by its own CRC, high byte first, has a CRC of zero, which is true only with the bytes in that order.
- **One CRC and one block parser.** The test-only probe had its own copy of both, from task 10. Its tests now use the production `TapeCrc` and `TapeBlocks`, with their assertions unchanged, so there is one of each and what the OS wrote is read by the code that will read the user's tapes. The probe's constants for the bit and byte times come from `TapeTiming` too.

## The cassette, at the ULA's serial register (twelfth task, 5 October)

The twelfth task built the cassette itself, `UlaTape`, owned by the ULA, and closed the loop the design asked for: a program typed into the machine, saved by the OS, written as a UEF by the writer, read back by the reader and loaded and run on a fresh machine.

- **What the boundary bought.** The OS runs its own tape code unchanged: its leader counters, its CRC routine, its block decoder and its messages, against interrupts that come at the rate the sheet gives. So one `SAVE` and one `LOAD` prove the OS, the ULA's tape side, the writer and the reader together. Nothing in the loop is a recording made elsewhere, and the tape image the machine made for a one-line program is a UEF of 135 bytes.
- **What it cost.** No waveform: tone shape, phase, 300 baud and a damaged waveform are not modelled, and the high-tone detector is a count of bit times, not the RC circuit the sources do not describe. The quirk two games use as a timer, receive-full raised by entering output mode, is not there. And a tape takes as long as it does on a real machine, because the timing is the point: the round trip's `SAVE` took **21,513,995 cycles, 10.76 s** of machine time from RETURN at `RECORD then RETURN` to the motor going off, and the `LOAD` **11,116,233 cycles, 5.56 s** from RETURN to the motor going off. Both are printed by `dotnet test tests/Dbhq.Machines.Electron.Tests --filter "FullyQualifiedName~TapeRoundTripTests.ASavedProgram" --logger "console;verbosity=detailed"`, run on 5 October, from the machine's own cycle counter at 2 MHz. All of this is in `docs/known-differences.md`.
- **Lazy, and exact to the cycle.** The cassette does nothing on the cycle. Its next event, a high tone, a byte becoming ready, a byte being lost or transmit-empty coming back, goes into the ULA's `NextEvent`, which the bus already compares on every access. The tape's position is held as a cycle and the position at that cycle, so every event falls at an exact cycle whatever the host does in between. A test runs the same tape on two machines, one caught up at every cycle and one in a single jump, and they agree.
- **The probe is kept, as a second opinion.** The controller preferred keeping task 10's probe tests as they are, and they are, with every assertion. The `ITapeTap` seam therefore stays in production code, because the probe needs it. While a tap is set the bus gives it the cassette's registers and the cassette sees nothing, so the two never act on the same register. The probe and the cassette were then given the same `SAVE`, and they record the same tape, byte for byte and carrier for carrier once the probe's idle stretches are rounded by the rule below: a test holds it. The `SAVE` took the same 21,513,995 cycles on both. The `LOAD` ended 50 cycles sooner on the cassette than through the probe (11,116,283) [not traced].
- **One task 10 test changed, and why.** `WithNoTapAFe04ReadIsTheHighByteAsBefore` held that `$FE04` read as `$FE` with no tap set, when nothing was behind the register. The cassette is behind it now, and a read gives its receive register. The test is replaced by two: with no tap the cassette answers `$FE04`, and with a tap set the cassette neither moves nor records.
- **The recorder's rule.** A byte written to `$FE04` in output mode with the motor on is a `TapeByte`; idle line is a `Carrier`, rounded to the nearest 2400 Hz cycle of 832 CPU cycles. Idle line of one bit time or less is dropped: task 10 found the OS writes each byte of a block inside the stop bit of the one before, so the bytes of a block have no idle between them, and a gap under a bit time is none the OS meant and far too short to be heard as high tone. The one-line `SAVE` records 12,400 cycles of carrier before its block and 12,699 after.
- **Choices the sheets leave open.** The tape moves only with the motor on and in input or output mode (the controller's ruling); while recording it moves only in output mode, so its position is the recording's length. A played tape stops at its end. `Rewind` after a recording turns the recording into the tape that plays. High tone is raised again every ten bit times while a carrier lasts, as the probe did (`tape.md` s7 item 11, still open). A byte is lost if unread for 4,000 cycles, the brief's 2 ms; `ula.md` s6a's own window is two bit times, 3,328 cycles, and the model takes the larger. A good load loses none, and the round trip asserts it.
- **What the OS does when a load cannot finish, found by running it.** The brief expected that ejecting the tape mid-load would bring the prompt back after the OS's own timeout. **There is no timeout.** With the tape gone after `Loading`, the OS waited with the motor on and the screen unchanged for 30 s of machine time in a scratch run (not kept), and the test watches it for two seconds and then presses Escape, which prints `Escape` and gives the prompt with the motor off. A silent tape leaves `LOAD` at `Searching` the same way, and `*CAT` on an empty tape prints nothing and searches; `*TAPE` returns at once. **BREAK** stops the motor 4,736 cycles after it in the scratch run: the ULA keeps its registers on BREAK in this model, and what stops the motor is the OS's reset code writing `$B4`, motor bit clear, to `$FE07` at `$D96B-$D970`. All of this went into `tape.md` s7 item 12. Every one of these tests is bounded in cycles, so a hang fails it.
- **A bad data CRC.** One bit of the round trip's first data byte flipped, then through the writer and the reader as a user's damaged tape would come: the OS prints `Data?`, `Rewind tape` and searches again, as task 10 found, and RAM from the end of the block to the screen is unchanged.
- **The 600-byte program was put in memory, not typed.** Typing 600 characters at two frames a key is 96 million cycles. The test writes three REM lines into BASIC's own layout at PAGE and has BASIC take them with `OLD`, then checks BASIC's TOP is PAGE plus 600. The OS saved it as blocks of 256, 256 and 88, numbered 0 to 2 with only the last flagged, and a fresh machine loaded it back byte for byte behind a one-second leader.
- **The brief's round-trip code, adjusted.** As written it typed `SAVE "TEST"` and waited for the prompt, but the OS stops at `RECORD then RETURN` first, so the test presses RETURN there, as the probe's tests do.
- **How it was tested.** The port tests came first and failed against empty stubs of the machine's tape members (eleven of twelve; the motor-off test passes against a stub that does nothing, as it should). The round-trip and fault tests were written once the port worked; with the cassette disconnected from the bus, a throwaway edit, all six that do not need the probe failed.
- **Cost.** The round trip runs one `SAVE` of each program and shares it between tests. The failure cases play behind a one-second leader and watch the screen for two seconds, not thirty.

## The acceptance test and the machine as WebAssembly (thirteenth task, 5 October)

The thirteenth task wrote the test that makes the Electron count, and the WebAssembly host its page will run.

- **Three passes, one program.** `machines/electron/try-it.json` is the BBC Micro's short loop, `HELLO 1` to `HELLO 5`, with what the screen shows worked out from BASIC's rule for `PRINT` (a number after a semicolon has no padding). The Electron's BASIC is the BBC's, byte for byte, so the rule is the same book's. `ElectronAcceptanceTests` reads that file, the one the page will render. The first pass types the lines on the keyboard matrix and reads the screen back off the picture the ULA drew, not out of screen memory. The second types them through `ElectronKeyPresses`, the queue the page's keys will go through, with every key reported down and up at once, as a browser can. The third types only the lines BASIC stores (those that start with a line number), saves them with the OS's own `SAVE` onto a blank tape, writes the tape as a UEF, reads it back with the reader, loads it on a fresh machine with `LOAD`, clears the screen, and types the remaining line, `RUN`. Each pass checks every row: the lines echoed after the prompt, the five lines printed, and the prompt again. So the second pass proves the page's way in, and the third that a program a visitor saves to a file comes back and runs.
- **Can they fail?** They passed on the first run, so two throwaway mutations (not kept) checked them. `HELLO 3` written as `HELLO  3` in the file turned all three red. The key queue made to let a key up the moment it went down turned the second pass, and the `PRINT 6*7` test the site's browser check will mirror, red: the OS never saw the keys.
- **The key queue is a copy, on purpose.** `ElectronKeyPresses` is the BBC's `BbcKeyPresses` with the Electron's keys, the same 80,000-cycle hold and rest, and no SHIFT and BREAK, which on the BBC starts a disc. The two are not made one class: two machines show what they share, and the third will show where the line falls. Its tests are the BBC's queue tests with the Electron's keys.
- **The host's contract.** `ElectronHost` in `src/Dbhq.Machines.Electron.Wasm/` mirrors `BbcHost`. `Load(os, basic, sampleRate)`, `Run(cycles)` (the keys are played as it runs), `Cycles`, `PressKey` and `ReleaseKey`, `Break`, `Frames`, `Picture`, `SoundSamples` and `DropSound`, and the cassette: `InsertTape(uef)`, `EjectTape`, `Rewind`, `StartRecording`, `MotorOn`, `TapeSeconds` and `LostBytes`. A key is its place in the matrix, the value of `ElectronKey`, column plus sixteen times the bit (`ula.md` s7b): a number the hardware fixes, which the page maps from the browser's `event.code`. `Picture` and `SoundSamples` hand the page a view of the framebuffer and of the samples through the functions `picture` and `sound` it registers under the module name `electron`, the BBC's one-copy rule. `InsertTape` never throws into the page: it returns the reader's reason as text, or an empty string when the tape went in. `EjectTape` returns the tape as a UEF file's bytes, or null when there was nothing on it. The speed check's calls (`Peek`, `Mode`, `SetMode`, `ScreenRow`) are kept.
- **Cleanups from the earlier reviews.** The cassette now keeps its own copy of the list a tape is put in with, not the caller's. `LostBytes` counts one tape load: putting a tape in, rewinding, taking it out and starting a recording each set it to 0. The UEF reader's limit is 4 MiB, not 16, and now holds for a plain file too: half an hour of tape is 216,000 bytes, and every byte is an entry in a list the browser has to hold. The million-byte reader test times only the read, on the test's own thread rather than a task, with a limit of 30 seconds and the time printed: it had failed under a load average of 15 to 74 with a limit of two.
- **The speed, measured again with the display: under ten times, so the work stopped here.** The seventh task's check was re-run as it records it, `node run-in-browser.mjs publish/aot 3` on a fresh AOT publish (Chrome 153.0.8010.47 headless, .NET SDK 10.0.400), modes 6 then 0, at 08:13:53 to 08:14:24 UTC with a load average of 2.58 before and 1.56 after. Over fifteen runs a mode, mode 0 gave best 9.728 MHz (4.86 times), median 6.714 (3.36 times) and slowest 5.460 (2.73 times); mode 6 gave best 10.086 (5.04 times), median 8.636 (4.32 times) and slowest 3.614 (1.81 times). The plan's rule is ten times in mode 0, and under five is a stop.
- **But the machine was slower than it was at 04:37.** Other sessions were running browsers and builds on it all morning. So the seventh task's own build (commit `e405700`) and the eighth task's (`c5283d3`, the display) were published into `/tmp` and run with the same page, two launches each, back to back with this build, from 08:47:39 UTC (load average 3.45 at the start, 25.45 by the third). The seventh task's build gave a mode 0 median of 20.737 MHz (10.37 times), against the 51.813 it gave at 04:37, so the machine was about two and a half times slower. The eighth task's gave 7.467 (3.73 times) in mode 0 and 8.478 in mode 6, against the seventh's 10.122. The control's commands, each build from `git archive <commit> src Directory.Build.props` unpacked under `/tmp`: `dotnet publish src/Dbhq.Machines.Electron.Wasm -c Release -p:RunAOTCompilation=true -o /tmp/electron-objs/pub-<commit>`, then from `bench/electron-speed`, `node run-in-browser.mjs /tmp/electron-objs/pub-<commit> 2` for each build and `node run-in-browser.mjs publish/aot 2` for this one. So drawing the picture costs mode 0 about 2.8 times and mode 6 about 1.2 times, measured back to back. Scaled to the seventh task's quiet figures, that is about 9.3 times in mode 0 and 12.9 in mode 6 [inferring from two runs on a loaded machine]: under ten in mode 0 even on a quiet machine, and mode 0 is now the heavier case, the other way round from the seventh task.
- **Where the time goes, roughly.** Throwaway native timings (a console program, not kept, on the same loaded machine) agreed: mode 0 ran three to four times faster with the pixel writes taken out of `UlaDisplay.DrawLine`. The display draws 12,800 lines a second of machine time, 256 in each of 50 fields, about 8.2 million pixels in mode 0 and 6.4 million in mode 6, each written by its own `Span.Fill` call; and the two fields draw the same rows. A plain loop in place of the `Fill` calls gained about a fifth in the browser. The first commit of this task changed nothing in the display and stopped there, as the plan's rule asks; the next section is what came of it.

## The display, a byte at a time (thirteenth task, second commit, 5 October)

The controller ruled on the speed finding above without sending it to Dan, because the fix changes no behaviour and tests guard it: draw a line a byte at a time from a table of ready-made pixels, then measure again against a control.

- **The change.** `UlaDisplay` keeps, for each of the 256 byte values, the framebuffer pixels that byte draws in the current mode and palette: 8 in modes 0 to 3, 16 in modes 4 to 6. A line is then 80 or 40 copies, one a byte, where it was 640 separate `Span.Fill` calls in mode 0. The table is made again only when the mode or the palette has changed since it was last made; nothing is allocated per line.
- **The picture did not change, and a test says so.** Before the change, `DisplayPictureHashTests` recorded a hash of the whole framebuffer at fixed points: the boot screen in mode 6, the screen after `MODE 0` to `MODE 5` is typed, random screen memory in every mode over both fields, and a field with palette and mode changes in the middle of it. These are the only expected values in the Electron's tests that come from the old code rather than the sheets, because what they check is that the output stayed the same. They passed on the old code, they pass on the new one, and every earlier display test passes unchanged. A throwaway mutation (each line's bytes drawn in reverse order, not kept) turned all fifteen red.
- **Not done: skipping the second field.** The two fields draw the same rows, so the second could be skipped when nothing it shows has changed. Proving "nothing has changed" means watching every store into the displayed memory and every register write between the fields, and the table alone brought the figure past the line, so it was left out.
- **Measured against a control.** The machine was again shared with other sessions, so three AOT builds ran in turn, a launch of each at a time, with the new `bench/electron-speed/alternate.sh`: the seventh task's build (`e405700`), the build before this change (`7a3818b`) and this one. Each older build was unpacked with `git archive <commit> src Directory.Build.props` under `/tmp` and published there with `dotnet publish src/Dbhq.Machines.Electron.Wasm -c Release -p:RunAOTCompilation=true -o <folder>`. Then from `bench/electron-speed`, `./alternate.sh 4 /tmp/electron-objs/pub-e405700 /tmp/electron-objs/pub-7a3818b /tmp/electron-objs/pub-table`, twice. The first run started 09:20:53 UTC with `uptime` showing a load average of 1.85 and ended 09:23:37 at 45.86. The second started 09:28:26 at 3.36 and ended 09:30:41 at 23.96.
- **The figures**, medians over both runs, 40 timed runs per build per mode:

  | Build | Mode 6 | Mode 0 |
  | --- | --- | --- |
  | seventh task, `e405700` | 11.152 MHz (5.58 times) | 19.733 MHz (9.87 times) |
  | before the table, `7a3818b` | 9.381 MHz (4.69 times) | 7.309 MHz (3.65 times) |
  | the table | 9.556 MHz (4.78 times) | 11.713 MHz (5.86 times) |

  Each run on its own: mode 0 with the table at 11.189 and 12.391 MHz, against 7.514 and 7.092 before it. The table makes mode 0 1.60 times as fast as before it, and mode 6 1.02 times, which is no change. Against the seventh task's build, the table's mode 0 is 0.594 of it and mode 6 0.857.
- **The estimate for a quiet machine.** The seventh task's build gave 51.813 MHz in mode 0 and 30.864 in mode 6 when the machine was quiet, at 04:37. Scaled by the ratios above, this build would give about **30.8 MHz, 15.4 times, in mode 0** and 26.4 MHz, 13.2 times, in mode 6. **That is an estimate**, from two loaded runs and a ratio, not a measurement on a quiet machine. On that estimate mode 0 is over the plan's ten times, so the work carries on; the page's headroom sentence will say what a visitor's own browser does. Mode 6 is now the heavier case again, about as it was before the display.
