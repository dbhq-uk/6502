---
title: "The teletype's stray tilde: a burst the printer should not read"
date: 2026-10-15
summary: "A tilde headed the teletype's paper in the browser check. The monitor really had sent something: closing the TTY switch on a running monitor makes it print its log-on at about fifteen thousand baud, and the printer took a space on the line at the middle of a start bit for a character. The printer now needs the space held, a test pins it, and the browser check reads the paper's first two lines."
order: 70
---

# 15 October 2026: the teletype's stray tilde

Issue 120. CI's browser check showed a `~` as the first thing on the KIM-1's paper at 110 baud, where the .NET tests show nothing for RUB OUT's echo. [The teletype entry](2026-10-08-the-kim-1-teletype.md) guessed that it was RUB OUT's timing against the reset. That guess was wrong.

## What the cause is

The machine really sends a burst, and the printer read a character out of it.

- **The order of the clicks matters.** The browser check runs the try-it program first, so the monitor is in keypad mode with the display lit (`02123C`). It then clears the paper, closes the TTY switch and only then clicks RS and RUB OUT. The .NET log-on test (`Kim1Session.LogOn`) closes the switch on a machine that has never run the monitor, so it never saw the burst.
- **Closing the switch on a running monitor sends its log-on.** The monitor's keypad loop passes through its start routine and reads PA0 each time. With the switch closed and the 74145 on output 3, PA0 is low, so it takes the teletype branch and prints CR LF, KIM and the open cell. At the first edge the program counter was in OUTCH and the stack returned to `$1E37` (inside the CR LF loop) and then to `$1C5C` (the `JSR $1E2F` at `$1C59`, the CR LF in START).
- **It prints on the wrong clock.** The keypad-mode RS never measures a start bit, so CNTL30 and CNTH30 hold `$00` and `$FF`. The delay loop then ends after one pass, and a bit lasts about 64 cycles: about 15,600 baud. The edges in the .NET trace were 61, 64, 128 and 565 cycles apart.
- **The printer read noise as a character.** At 110 baud it took any falling edge for a start bit, looked at the line half a bit later (4,545 cycles) and went on if it was a space. In the burst the line is a space for about 565 of every 690 cycles, so by chance it was a space at the middle, and the eight samples a bit apart then read `0x7E`, a `~`. `0x7E` is printable, so it shows on the paper. RUB OUT's own echo is `0x7F`, which the paper does not print, so it was always correct and always invisible.

It was not RUB OUT's timing. In the page's log (an instrumented copy of `kim-1.js` in `dist`, not committed) the `0x7E` was printed in the frame that ended at cycle 11,400,097. The RS tap was queued at 11,350,095 and the RUB OUT was sent at 11,400,097. A character takes about 95,000 cycles from its start edge to its stop bit at 110 baud, so this one began before RS was pressed.

## What was decided

Fix the printer, not the page and not the machine.

- **The machine is right.** A real KIM-1 with the switch closed on a running monitor does the same: the ROM is the ROM. Nothing in the bus, the 6530 or the CPU changed.
- **A start bit is now a space held from its falling edge to its middle.** A rise inside that half bit ends the attempt as a glitch (`Kim1Teletype.UpdateOutput`). This is the rule the entry already claimed ("a shorter space is a glitch"), made true for spaces that are broken by a mark, not only for ones that stop early. The next falling edge starts a fresh attempt, so a real character that follows a glitch still decodes, as it did before.
- **It is an assumption about the real printer, not a measurement.** A Teletype Model 33 reads the line with an electromechanical selector that needs a space held for a good part of a bit time, and a burst at 15,600 baud would not move it. I did not measure a Model 33 and did not find a source that does. The rule is the minimum that makes the model stop printing noise; the bit time it accepts is not tuned to anything.
- **Chosen over** hiding it on the page (dropping what is printed before RS, or clearing the paper at RS), which would hide real output and would not fix a script that reads the printer; over stopping the page from letting the switch close on a running monitor, which the manual does not forbid and a visitor can always do; and over changing the monitor's timing in the model, which would be a lie about the ROM.

## What was checked, and how

- **Reproduced locally in Chrome for Testing**, on an interpreter build of the KIM-1 (`node scripts/build-machines.mjs kim-1 --interpreter`, not the AOT build CI uses), with the site built against a fake `results.json` that is git-ignored. A copy of `scripts/browser-check.mjs`'s first steps (load, try-it, `checkKim1Teletype`) printed the 110 baud paper as `"~\nKIM\n0212 3C 0002\n0002 00"`. Without the try-it first, it printed `"\nKIM\n0000 00 0002\n0002 00"`, which showed that the earlier state mattered. No speed figure was recorded.
- **Reproduced in .NET** without a browser: `Kim1Session` with RS, then the switch, then 30,000, 100,000 or 1,000,000 cycles, then RS and RUB OUT. Before the fix, `dotnet test tests/Dbhq.Machines.Kim1.Tests -c Release --filter "FullyQualifiedName~ClosingTheSwitch|FullyQualifiedName~SpacesThatEach"` failed all four new cases: `~` before RS in two, `~` heading the log-on in the third, and `` ` `` (`0x60`) from the hand-driven pulses in the line test. After the fix the whole project passes, 86 tests on 15 October 2026.
- **Fixed in the browser**: the same script printed the paper's first two lines as `["~","KIM"]` before and `["","KIM"]` after.

## What the tests hold

- `Kim1TeletypeTests.ClosingTheSwitchOnARunningMonitorPrintsNothingAt110BaudAndTheLogOnIsClean`: the machine, three lengths of time between the switch and RS. Nothing is printed before RS, and the log-on is RUB OUT's echo, CR LF, KIM and the cell, with no framing errors.
- `Kim1TeletypeLineTests.SpacesThatEachEndBeforeTheMiddleOfAStartBitAreNotACharacter`: twenty spaces of 3,000 cycles with 100-cycle marks between, where the line is a space at the middle of the first start bit and at each later sample. Nothing decodes.
- `site/scripts/browser-check-kim1-tty.mjs` now reads the paper's first two lines after the log-on at 110 baud and fails unless they are a blank line and `KIM`. The paper is cleared before the switch goes on, so a stray character before the log-on fails it. No site test was added, so `site/tests/floor.json` is unchanged.

## Limits

- **Only this burst was tried.** Any other stream a running monitor could put on the line before RS is not covered, and the rule is a plain one, not a model of a selector.
- **Why the keypad loop passes through START was not traced further** than the stack at the first edge. The host's comment says the monitor reads the switch "at RS, and in its keypad loop", which fits, and the burst follows it.
