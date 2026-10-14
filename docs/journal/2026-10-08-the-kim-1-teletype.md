---
title: "The KIM-1's teletype"
date: 2026-10-08
summary: "The KIM-1 gets the terminal most of its owners used: a teletype on its twenty milliamp loops, its bits sent and decoded in machine cycles while the original monitor bit-bangs the line itself. The monitor times RUB OUT, prints KIM, and takes a program typed at the teletype, runs it and prints the result; the page has the terminal, with the session from the test's own file."
order: 37
---

# 8 October 2026: the KIM-1's teletype

The KIM-1 was built to be used from a teletype as much as from its keypad. A
Teletype Model 33, or a terminal pretending to be one, hangs on two 20 mA
current loops on the board's application connector, and the monitor in the
6530-002 talks to it by toggling one port pin and polling another, timing every
bit with its own delay loops. This was the largest of the three parts the first
version of the KIM-1 left out (issue 20). It is now modelled, tested against
the original ROM, and on the page.

## What was built

- **`src/Dbhq.Machines.Kim1/Kim1Teletype.cs`**, the terminal: a keyboard that
  turns queued characters into bits on the keyboard loop, and a printer that
  decodes the bits on the printer loop into characters, both timed in cycles.
- **`Kim1Mode`** and **`Kim1Bus.Mode`**, the TTY/KB switch, which starts at
  KB, so the keypad, the digits and every existing test behave as before.
- **`Kim1Bus`** wires the terminal to the 6530-002: PA7 reads the keyboard
  loop, PA0 is pulled low through the switch while the 74145 selects output 3,
  and every write to port B tells the terminal what PB0 and PB5 now drive.
- **`tests/Dbhq.Machines.Kim1.Tests/`**: `Kim1TeletypeTests`, the monitor
  driven through the teletype, and `Kim1TeletypeLineTests`, the terminal on its
  own with the line driven by hand.
- **`machines/kim-1/teletype.json`**, the session the page shows and the
  test types, as `try-it.json` is for the keypad.
- **The page**: a teletype on the KIM-1's panel, and a section that explains
  it and shows the session. The registry's notes now say the teletype is
  modelled; the cassette and the expansion bus still are not.
- **`tools/probes/kim1_serial_routines.py`**, which disassembles the
  monitor's teletype routines from the hash-checked ROM.
- **`site/scripts/browser-check-kim1-tty.mjs`**, the same session written to
  run in a real browser, by CI. It has not been run on the machine this was
  written on, which has no Chrome.

## What was read

- The KIM-1 User Manual, in Ruud Baltissen's text transcription
  (`http://www.baltissen.org/zip/user_asc.zip`, fetched over plain HTTP on 8
  October because his HTTPS certificate does not name his host): section 2.6
  "Adding a teleprinter" with figure 2.4, the wiring and the TTY/KB jumper;
  section 3's description of the reset and "Get Character" logic; the
  application connector table (figure 3.9); section 4.3 "Using a serial
  teleprinter", every command; and Appendix F, the paper tape format.
- Figure 3.7, the TTY interface, is "(see schematics)" in the text copy, so
  the circuit was read off Ruud Baltissen's redrawing of the KIM-1 schematic
  (`http://www.baltissen.org/images/kim.gif`) and his description of it
  (`http://www.baltissen.org/newhtm/kim.htm`): the keyboard loop switches a
  transistor (Q7) whose collector goes through a 7400 (U15D) to PA7, the other
  input of that gate is PB5 inverted by a 7404 (U16D), and the printer loop is
  closed by a 7438 open-collector NAND (U26C) whose inputs are PB0 and PA7.
- The monitor itself, disassembled from `roms/kim-1/6530-002.bin` by
  `python3 tools/probes/kim1_serial_routines.py`, which checks the ROM's
  SHA-256 first. Everything below about what the monitor does is from that
  listing, not from memory or from the published source.

The scanned manuals on bitsavers and Hans Otten's KIM-1 pages were not opened
for this: the text manual and the schematic answered every question, and the
ROM settled the timing.

## The monitor's serial routines, as disassembled

Run `python3 tools/probes/kim1_serial_routines.py` for the full listing. What
it shows:

- **RST ($1C22).** After setting up the ports it writes $FF to CNTH30
  ($17F3), then loads 1 and does `BIT $1740` (port A): if PA0 reads high the
  switch is open and it goes straight to the keypad. If PA0 reads low it loops
  `BMI` on PA7 until the line falls (a start bit), then counts: `CLC`, `ADC
  #$01`, `BCC`, `LDY $1740`, `BPL`, 14 cycles a turn, with a carry into CNTH30,
  until PA7 rises again. The count is the length of the start bit, which is why
  the manual insists on RUB OUT: its seven bits are all ones, so the first low
  stretch on the line is exactly one bit. The count goes to CNTL30 ($17F2), and
  the rest of the character is read and dropped through GETCH's second half.
- **The switch, and why it is a switch on PA0.** INIT1 ($1E8C) writes $00 to
  port A's direction register, $3F to port B's and $07 to port B: PB0 high,
  PB5 low, and the 74145 on output 3, which is the TTY/KB jumper's row. The
  jumper ties PA0 to that output, so with output 3 selected PA0 reads low. The
  model pulls PA0 low only when the switch is closed and output 3 is selected.
- **DELAY ($1ED4) and DEHALF ($1EEB)** count CNTL30 and CNTH30 down in the
  same 14-cycle steps (`SEC`, `SBC #$01`, `BCS`, `LDY $17F4`, `BPL`), one bit
  or half of one. **The monitor does not use the 6530's interval timer for the
  teletype at all,** which the brief for this work assumed it did: the timing
  is software loops calibrated by the measurement at reset.
- **GETCH ($1E5A)** returns at once if PA0 is high (keypad mode), otherwise
  waits for PA7 to fall, waits one bit and then half a bit, and takes eight
  samples a bit apart, least significant first, from PA7. It then waits half a
  bit and drops the eighth bit (`ROL`, `LSR`). It never writes to PB0: **the
  monitor does not echo in software.**
- **OUTCH ($1EA0)** waits a bit with PB0 high, drops PB0 for the start bit,
  shifts out eight bits on PB0, raises it for a stop bit and waits a bit. A
  character is therefore a start bit, eight bits and, with the leading wait of
  the next one, two stop bits. The eighth bit is the character's bit 7, a space
  for ASCII.
- **The strings** are one table at $1FD5, printed from an index down to 0:
  six NULs, LF, CR, then M, I, K, so CRLF prints CR, LF and six NULs (time for a
  real teletype's carriage to return), and the prompt prints KIM before its
  CR LF. The same table holds XOFF, `ERR` and XOFF again for the paper tape
  loader.
- **The commands** ($1DDB): SPACE opens the address typed, RUB OUT goes back
  to START, CR moves on a cell, LF back a cell, `.` stores the byte typed and
  moves on, `G` runs from the open cell, `Q` punches paper tape, `L` reads it.
  Anything else goes to the hex digit packer, which takes $30 to $46 only:
  capitals, as a Teletype Model 33 sends. Lower case is ignored, which is why
  the page types in capitals.

## The decisions

- **The echo is the wiring's, as on the board.** The printer loop is closed
  by a NAND of PB0 and PA7, so whatever the keyboard sends reaches the printer
  while PB0 is high, and nothing reaches it while PB0 is low. The model is
  exactly that: `OutputMark` is PB0 and (PB5 or the keyboard loop). Chosen over
  echoing in the terminal, which would print a key even when PB0 is low, and
  over not echoing, which would leave a blank paper where the board prints what
  was typed. One consequence is kept, not hidden: with the switch at KB, a key
  typed at the teletype is still echoed to its paper, as on a board with a
  teletype left plugged in.
- **PB5 gates the keyboard off PA7,** through the 7400 and 7404. The monitor
  raises PB5 only at the end of a paper tape load, to stop the reader while it
  prints. Modelled because it is two gates, and a test shows a key sent with
  PB5 high never reaches PA7 or the printer.
- **The terminal is driven every cycle, not caught up in a batch.** The bus
  calls `Advance` at the start of every cycle whose number has reached
  `Kim1Teletype.Due`, the next cycle on which anything can happen (an edge it
  sends, a sample its printer takes, or the moment the next key may go). Every
  edge and sample therefore lands on its own cycle, which is what AGENTS.md
  rule 1 asks of a chip, without a differential test to prove a batched
  version equal. The compare costs one comparison a cycle.
- **The printer is a real receiver**: a falling edge starts a character, the
  start bit is checked at its middle (a shorter space is a glitch and is
  ignored), the eight bits are sampled at their middles, and the stop bit must
  be a mark or the character is thrown away and counted as a framing error. It
  sees only the line: it never reads the monitor's RAM, and it never knows
  which routine is running. Chosen over watching writes to $1742 for
  characters, which would have made the test pass with a broken bit timing.
- **The keyboard sends seven bits of ASCII, the eighth a mark, and two stop
  bits,** a Teletype Model 33's framing. The monitor reads eight bits and drops
  the eighth, so the eighth bit's value does not matter to it; a mark was
  chosen over a parity bit because a mark makes RUB OUT all ones after its
  start bit, which is what the speed measurement needs.
- **Bit boundaries are at start + floor(k x 1,000,000 / baud)** cycles, so
  the error never adds up across a character: at 110 baud a bit is 9,090 or
  9,091 cycles.
- **The keyboard waits for quiet.** A key queued while the monitor is printing
  would arrive when it is not listening, and be lost, as it would on the real
  board. The queue therefore sends a key only when both loops have been quiet
  for four bit times (`Kim1Teletype.DefaultQuietBits`). Four was chosen from
  the listing: within one answer the monitor's line is never quiet for more
  than about two bits (a stop bit, then OUTCH's leading wait), and it starts
  answering about a bit after GETCH returns, which is before the end of the key's
  own second stop bit. So a whole command can be queued at once, which is how
  the page's field, the tests and a person typing ahead all work. A test sends
  the same command with no wait (`quietBits: 0`, a tape reader's behaviour)
  and the monitor loses it.
- **The terminal's speed is a property of the terminal, 110 baud by
  default,** and the page offers 110, 300 and 1200. The monitor measures
  whatever it is sent, so a change of speed needs RS and RUB OUT again, as on
  the real board; the page says so when the speed is changed.
- **The switch is a machine property, `Kim1Mode`, Keypad by default,** so
  `Kim1AcceptanceTests` and every other existing test run unchanged, and
  their file was not touched. `Kim1Session.Keys` runs the teletype to quiet
  after a keypad key only when the switch is at TTY.
- **Keys are written as the manual writes them**: `[SPACE]`, `[CR]`, `[LF]`
  and `[RUBOUT]` in brackets, every other character as itself, and a bare space
  refused, because on a page it cannot be told from nothing. `RUB OUT` has no
  space inside its brackets, so that the parser can split on brackets alone.
  `Kim1Teletype.Parse` and the site's `parseTeletype` read the same notation,
  and a site test checks their named keys are the same table.
- **The session on the page enters the keypad try-it's program through the
  teletype**: the NMI vector set to $1C00, the program at $0200 and its two
  numbers deposited with `.`, G to run it, ST on the keypad to stop it (the
  manual's own way out of a running program in teletype mode), and `0212`
  SPACE to read the sum. Chosen over a program that returns to the monitor by
  itself, because reusing the try-it's program means the two sessions on the
  page do the same thing two ways.
- **The paper is a model of a teletype's paper,** in the tests
  (`TeletypePaper`) and on the page (`public/teletype-paper.js`), with the same
  rules: CR to the start of the line, LF to a new line, a printable character
  printed at the head (over what was there), and NUL, RUB OUT and XOFF printing
  nothing. The two are separate code, one C# and one JavaScript; they are held
  together by the session, which the test checks on one and the browser check
  (run by CI) on the other.
- **The page's paper is a `role="log"` of one row per line, set not to be
  live, with a polite region beside it that is given each line once it is
  complete** (see the review fixes below for why). The field types
  only while it has focus, in capitals; Enter is CR and Backspace or Delete is
  RUB OUT; LF has a button, and so does RUB OUT for a touch screen. Tab is never
  taken. The keypad's own keyboard handler now ignores keys typed anywhere in
  the teletype, so a hex digit typed at the terminal is not also a keypad press.
- **The browser check is written to run the session at 1200 baud,** after
  logging on and opening an address at 110. At 110 baud the session is 51
  seconds of the machine's time (below), and the page runs the machine in real
  time, so the check would add that to every CI run; at 1200 baud the same
  bits take an eleventh of the time, and 110 is still exercised in the browser.
  The check has not been run: this box has no Chrome, so CI is where it runs,
  and nothing here says how long it takes there.
- **The CSP did not change.** The page loads one more same-origin module,
  `teletype-paper.js`, and `script-src` already allows the site's own scripts.

## What the tests showed

`Kim1TeletypeTests` (the monitor through the teletype) and
`Kim1TeletypeLineTests` (the terminal alone):

- **Log on.** With the switch at TTY, RS then RUB OUT prints, exactly,
  RUB OUT's echo, CR, LF, six NULs, `KIM`, CR, LF, six NULs, then `0000 00 `.
- **The manual's own teletype example**, section 2.6 step 6: `0002` SPACE,
  `18.`, `A5.`, LF shows `0003 A5`, RUB OUT prints KIM again.
- **The page's session**, from `machines/kim-1/teletype.json`, every step's
  last lines checked, and the sum $3C in RAM at the end.
- **The speed.** At 110 baud, every character the monitor sends after logging
  on, timed from its start edge to the edge into its stop bit (nine bits, since
  its eighth bit is always a space), is within half a per cent of 1,000,000 /
  110 cycles a bit. The monitor also logs on, opens a cell, stores a byte and
  steps back at 300, 1200 and 2400 baud.
- **Paper tape.** Q punches a record that the test works out itself from the
  bytes and Appendix F's rules (`;`, the count, the address, 24 bytes, a
  16-bit sum of everything after the `;`, then the closing `;00` record with
  the record count), byte for byte; the punched text, fed to a second KIM-1
  after L, loads and reads back. A record with a wrong sum prints `ERR`.
- **Typing ahead.** A whole command queued while the monitor answers the
  first space is all taken in; the same command sent with no wait loses its
  byte.
- **KB mode.** With the switch at KB the monitor ignores the teletype (only
  the wiring's echo reaches the paper) and the keypad works; opening the
  switch in the middle of a teletype session hands the monitor back to the
  digits, as section 3 of the manual says.
- **The terminal alone**: a key's exact edges at 110 baud worked out in the
  test; decoding at the middle of each bit, and with bits 3 per cent fast or
  slow; a stop bit that is a space thrown away and counted, and the next
  character still read; a six-cycle space ignored; PB5 high keeping the
  keyboard off PA7 and the printer; PB0 low holding the printer at a space;
  queued keys spaced by the quiet time; and the key notation.

Measured on 13 October 2026, on the ARM box (the figures are machine
cycles, so the box does not change them), with `dotnet test
tests/Dbhq.Machines.Kim1.Tests -c Release --filter Kim1TeletypeTests --logger
"console;verbosity=detailed"`, which prints what three of the tests measure.
Correction, 13 October 2026: this entry first quoted figures that were not tied to a
command. These replace them, and they came out the same:

- **The monitor's bit at 110 baud is 9,088.8 cycles**, against 9,090.9 for
  110 baud exactly, 0.02 per cent short. The test printed one value for all 27
  characters it measured after logging on ("9088.8 to 9088.8"), and a start
  edge to a stop edge of nine bits is 9 x 9,088.8, about 81,799 cycles. The
  shortfall is the 14-cycle step of the monitor's counting loop.
- **Logging on**, from power on to the line going quiet after the prompt (the
  RS key's 80 ms hold and rest included, and the test's run in 10,000-cycle
  steps): 2,920,123 cycles at 110 baud, 1,130,165 at 300, 350,030 at 1200 and
  220,014 at 2400.
- **The page's session at 110 baud takes 51,083,187 cycles**, 51 seconds of
  the KIM-1's time; the test prints each step's cycles and the total. The
  step that enters the program, `0200` SPACE and the program's fourteen bytes,
  is 28,211,802 of them, about two seconds a byte: each byte is three keys
  and an answer of sixteen characters (CR, LF, six NULs and `020x xx `), at
  ten characters a second. The entry first gave that figure for the fourteen
  bytes alone; the test counts the whole step (corrected 13 October 2026). This is why the browser check
  runs the session at 1200 baud.

## The new tests were shown to fail

Correction, 13 October 2026: this entry first had this heading over a placeholder, with no mutations run. They were run on 13 October 2026 and the results are below.

**How.** One fault at a time, applied in the working copy by `tools/probes/kim1_teletype_mutations.py`, which is in the repository so the results can be reproduced (`python3 tools/probes/kim1_teletype_mutations.py --list` shows every fault, and naming ids runs only those). Each fault was one exact replacement, checked to match once. The script then ran `dotnet test tests/Dbhq.Machines.Kim1.Tests -c Release`, recorded which tests failed, and put the file back. The whole project was run each time, and it passed with no fault planted. The faults were chosen from what the model gets right on purpose: the bit timing, the framing, the wiring and the queue's rules. They were first run in three batches with throw-away scripts, as tests were added in answer to what survived. After the second review the whole list was run again, in one go, with the script now committed, against the tree at that point (82 Kim1 tests); the table gives that run's results. No fault survived it.

| # | Fault planted | File | Tests that failed | Caught by |
| --- | --- | --- | --- | --- |
| 1 | The printer samples at the start of each bit, not the middle | `Kim1Teletype.cs` | 10 | `ThePrinterSamplesAtTheMiddleOfEachBitAndDecodesTheCharacter`, `ACharacterSentAFewPerCentSlowStillDecodes`, `ASpaceShorterThanHalfABitIsNotAStartBit`, and seven more, among them the monitor's log-on |
| 2 | The keyboard's eighth bit is a space, not a mark | `Kim1Teletype.cs` | 1 | `AKeySendsAStartBitSevenBitsOfAsciiAMarkAndTwoStopBitsAt110Baud` |
| 3 | The keyboard sends one stop bit, not two | `Kim1Teletype.cs` | 1 | `QueuedKeysWaitForBothLinesToBeQuiet` |
| 4 | The printer keeps a character whose stop bit is a space | `Kim1Teletype.cs` | 3 | `ACharacterWhoseStopBitIsASpaceIsThrownAwayAndTheNextStillDecodes`, `TheKeyboardWaitsFourBitsFromTheLineReturningToAMarkAfterAFramingError`, `WithoutTheHoldAStopKeyAndTypingQueuedTogetherAreGarbled` |
| 5 | The keyboard does not wait for a quiet line | `Kim1Teletype.cs` | 15 | `QueuedKeysWaitForBothLinesToBeQuiet`, `KeysQueuedWhileTheMonitorIsPrintingAreNotLost`, and thirteen more |
| 6 | PB5 high no longer holds PA7 at a mark | `Kim1Teletype.cs` | 1 | `PB5HighHoldsPA7AtAMarkAndKeepsTheKeyboardOffThePrinter` |
| 7 | PB0 low no longer holds the printer at a space | `Kim1Teletype.cs` | 19 | `PB0LowHoldsThePrinterAtASpaceWhateverTheKeyboardDoes`, the monitor's log-on, the session, and sixteen more |
| 8 | A short space is taken as a start bit | `Kim1Teletype.cs` | 5 | `ASpaceShorterThanHalfABitIsNotAStartBit`, `AfterRsThePullUpOnPB0HoldsThePrinterLoopAtAMark`, and three more |
| 9 | A key queued between cycles can start in the past (the guard in `NextStart` removed) | `Kim1Teletype.cs` | 2 | `CatchingUpDoesNotStartAKeyQueuedSinceTheLastCycle`, `ReadingThePaperDoesNotStartAKeyOnACycleThatHasAlreadyRun` |
| 10 | Reading the paper stamps a queued key with the last cycle run (`CatchUp` back to `Advance`) | `Kim1Machine.cs` | 1 | `ReadingThePaperDoesNotStartAKeyOnACycleThatHasAlreadyRun` |
| 11 | Teletype keys are not held back for the keypad taps before them | `Kim1Keystrokes.cs` | 3 | `AStopKeyAndTypingQueuedTogetherAreNotGarbled`, `TeletypeKeysWaitForTheKeypadTapsQueuedBeforeThem`, `TeletypeKeysQueuedBeforeATapWaitForTapsQueuedAfterTheCallToo` |
| 12 | The TTY switch is read on the wrong decoder output | `Kim1Bus.cs` | 13 | the monitor's log-on, the manual's example, the session and ten more |
| 13 | The quiet timer ignores edges on the printer loop | `Kim1Teletype.cs` | 1 | `TheKeyboardWaitsFourBitsFromTheLineReturningToAMarkAfterAFramingError` |
| 14 | The quiet timer ignores where the printer's receiver finished | `Kim1Teletype.cs` | 1 | `TheKeyboardWaitsFourBitsFromTheMiddleOfTheMonitorsLastStopBit` |
| 15 | Replacing the terminal loses what the old one had printed | `Kim1Machine.cs` | 1 | `ReplacingTheTerminalKeepsWhatItHadPrinted` |

**The site's side.** The same way, seven faults in the page's files, each run against `tests/machine-page.test.mjs` and `tests/html.test.mjs` after the site was rebuilt where the fault was in the panel. The browser check was not mutated, because it cannot run here.

| # | Fault planted | File | Tests that failed |
| --- | --- | --- | --- |
| S1 | `parseTeletype` checks the key table with `in`, not `Object.hasOwn` | `site/src/lib/machines.mjs` | teletype keys are read as `Kim1Teletype.Parse` reads them |
| S2 | The paper gives out its open line as if it were complete | `site/public/teletype-paper.js` | a screen reader is given a line once it is complete |
| S3 | The paper is left live (`aria-live="off"` removed) | `site/public/kim-1.js` | the same test |
| S4 | The page does not push the switch and speed into the machine at start | `site/public/kim-1.js` | the page pushes the switch and the speed it shows into the machine at start |
| S5 | The speed lets the browser restore it (`autocomplete` removed) | `site/src/components/Kim1Panel.astro` | the teletype is on the panel |
| S6 | The polite region for completed lines is missing | `site/src/components/Kim1Panel.astro` | the same screen reader test |
| S7 | A repeated line is written without clearing the region first | `site/public/teletype-paper.js` | the announcer test and the screen reader test |

One or two tests failed for each (S7 has two).

**What it found.** Two faults survived the first batch. Faults 13 and 14 were not caught because nothing tested when the quiet time starts, only that a second key waits, so the quiet timer could have run half a bit or more early. Each now has a test that works the start cycle out from the bit time (the last two in the table). Fault 11 was caught only by a test that checks the hold directly, and not by a test that shows what the hold is for, so `AStopKeyAndTypingQueuedTogetherAreNotGarbled` was added: it runs the page's session to the point where ST stops the program, queues ST and the next characters together, and checks the paper is clean. Two faults, 4 and 8, did not build the first time (the compiler refused unreachable code, with warnings as errors) and were planted again as conditions that are always true or false but not constant.

## Surprises and mistakes

- **The timer is not used.** The brief for this work said the serial
  routines time their bits with the 6530's interval timer. The listing shows
  plain counting loops. Nothing had to change in the 6530 model.
- **There is no software echo.** Until the schematic was read, it looked as
  though the monitor simply never showed what was typed. The echo is two
  gates on the board.
- **The first version of the keyboard computed its level from the cycle**,
  with floor(elapsed x baud / 1,000,000), while its edges were scheduled at
  start + floor(k x 1,000,000 / baud). The two roundings disagree by a cycle
  at most bit boundaries, so at the cycle of an edge the level had not yet
  changed and the printer loop saw no edge at all. Caught before any test ran,
  by reading it back; the level now comes from the bit the keyboard is on.
- **A key could have started in the past.** The bus calls the terminal only
  on cycles where something is due, to keep the cost to one comparison a
  cycle, and the first version stamped a newly queued key with the last cycle
  the terminal had been called on. After a quiet spell that was long ago, so the
  key's first bits would have been crammed into one cycle. Every test passed
  anyway, because each run reads the paper before typing and reading brings the
  terminal up to date, and the page does the same every frame. Found by reading
  the code back, not by a test. A key queued between cycles now goes on the
  first cycle the terminal is called on after it, and a test queues one after a
  long quiet spell and checks its start edge.
- **A test's check-sum was right when it was meant to be wrong.** The first
  `L` error test fed `;010300EA00EE`, whose sum, $01 + $03 + $00 + $EA, is
  $00EE: the record loaded and the test failed for want of `ERR`. It now feeds
  the right sum and checks the byte loads, then $00EF and checks for `ERR`.
- **The paper takes a blank line after LF.** The manual's example prints LF's
  answer on the same row, but the echo of LF is a line feed, so a real
  teletype's paper moves up a line before the monitor's own CR LF. The tests
  expect the blank line, because that is what the bits say.
- **The reset makes a six-cycle glitch on PB0**: INIT1 writes the direction
  register, which turns PB0 into an output at 0, before it writes $07 to the
  port. The printer sees a falling edge, checks the start bit half a bit later,
  finds a mark and ignores it, as a real receiver would.
- **The entry first claimed a mutation run it could not show** (corrected 13
  October 2026). An earlier run was lost when a `pkill -f` killed the shell
  running it part way through a mutation, and the entry went on to say every
  assertion had been shown to fail. A reviewer found the section was a
  placeholder. The claim came out and the mutations were run again on 13 October
  (above).

## The review of the page, and what was done about it

The page, the panel, the paper, the browser check and this entry were first committed as an unfinished work in progress. A reviewer read them without building and raised fourteen items. Each was checked against the code before anything was changed. All fourteen were real, and all were fixed, except for one part of two of them that could not be applied (a reference to `@125`, and a change to `validate.yml`; both are said below).

- **Mutations and figures (items 1, 2 and 14).** The mutation section was a placeholder and the figures had no command behind them. Both are redone above: the mutations were run, and the figures were measured again with the command the entry names. The 28,211,802 figure is printed by the session test as the cost of one step; the draft had given it for the program's bytes alone.
- **A race between the keypad and the teletype (item 3).** The reviewer's case was RS then RUB OUT queued together by the page. That one did not fail: in a scratch test on 13 October, which was not kept and so cannot be repeated from the repository, RS tapped and RUB OUT sent 0, 1,000 and 20,000 cycles later all logged on and printed KIM. That is consistent with the monitor timing the part of the start bit it sees, which is nearly all of 9,000 cycles, but it was not worked out further. The race is real for another pair, ST followed at once by typing. ST starts the monitor's answer a few milliseconds later, the teletype's rule (send on a quiet line) cannot see an answer that has not begun, and the first character of the next command arrived as the monitor started to print. The ST case is kept as two tests, `WithoutTheHoldAStopKeyAndTypingQueuedTogetherAreGarbled` and `AStopKeyAndTypingQueuedTogetherAreNotGarbled`, which queue ST and `0212` SPACE together, once straight into the teletype and once held until ST is done. In the scratch run that found it, straight in, the paper showed `020A 4C 212` and one framing error, the leading 0 lost. Held, it showed `020A 4C 0212` and `0212 3C`, and no error. The tests had not met it, because `Kim1Session.Keys` runs the keypad to idle before anything is typed, and the browser check waits for each step to settle. The fix is in the machine, not the script: `Kim1Keystrokes.TypeAtTeletype` holds teletype keys back until the keypad taps queued before them have been played and rested, and `Kim1Host.TeletypeSend` uses it, so a page cannot get the order wrong. `TeletypeIdle` is false while keys are held. Two tests cover it: one checks the hold, with RS and then RUB OUT, and one runs the ST case above and checks the paper.
- **The digits `02123C` written into the browser check (item 4).** Real as a risk: it was typed by hand. The check now takes the last line of the last step in `teletype.json`, takes the spaces out, and waits for those digits. The session test in C# now also opens the switch at the end and checks the digits show that same line, so the browser check's expectation rests on a test that runs here. The reviewer also asked for `@125` to be checked against `OpeningTheSwitchHandsTheMonitorBackToTheKeypad`; there is no `@125` in the script or the tests, so that part could not be done. That test shows `0200 00` after the switch opens, which agrees with the rule used.
- **The switch and the speed after a reload (item 5).** Real. The machine starts at KB and 110 baud, and Firefox restores form state on a reload, so the page could show a switch that was on and a machine with it off. The speed says `autocomplete="off"`. The switch cannot: the site's HTML check refused it (`autocomplete` is not allowed on a checkbox), which the first build of the fix showed. So for both, the page pushes what it shows into the machine when it starts, and the two agree whatever the browser restored.
- **A screen reader and the open line (item 6).** Real. The paper rewrote the open line a character at a time, and a `role="log"` is live, so a reader would announce partial lines. The paper is now `aria-live="off"`, and a separate polite region is given each line once a line feed has completed it (`Paper.takeCompleted`: blank lines left out, each line once, lines torn off the top not repeated). The paper is still a log with one row to a line, and still readable by browsing.
- **`docs/known-differences.md` and `AGENTS.md` (items 7 and 8).** Real. The known differences now have a section for the teletype, with the four simplifications from the limitations below. The layout line for `machines/` mentions `machines/kim-1/teletype.json`.
- **`in` on the key table (item 9).** Real. `"toString" in TELETYPE_KEYS` is true, so `[toString]` was accepted as a key and would have sent a function's source. It is `Object.hasOwn` now, and the test of bad notation includes `[toString]` and `[__proto__]`.
- **Comments in the deploy workflow (item 10).** Real in `deploy-site.yml`, where a new sentence was spliced into the middle of another; it is its own comment now. `validate.yml` was read for the same fault and needs no change: this branch does not touch it.
- **The check that the keypad was not pressed (item 11).** Real. It waited 400 ms and then read the digits, which races the wait. It now checks that nothing is pending and that no `kim1:key` event was announced, which is what pressing the keypad does, as well as the digits not changing.
- **A baud outside 50 to 9600 (item 12).** Real, though the page offers only valid speeds. `Kim1Host.SetTeletypeBaud` refuses a bad one before it changes anything, and says so.
- **`ReadTeletype` and a key queued between cycles (item 13).** Real, and it contradicted the entry's own account above. `ReadTeletype` called `Advance` with the last cycle run, and that stamped a key queued since then with that cycle, so the key's start bit began on a cycle that had already run. A new `Kim1Teletype.CatchUp` takes the due edges and samples without stamping a queued key, `ReadTeletype` and `Drive` use it, and `Advance` (the bus's call, at the start of a cycle) still stamps. Two tests cover it, one on the terminal alone and one through the machine. The effect was one cycle at most, but the entry said otherwise.

**Counts after the fixes**, 13 October 2026, on the ARM box. `dotnet build -c Release` gave 0 warnings and 0 errors. `dotnet test --configuration Release --logger trx --results-directory TestResults` passed 2,724 in `Dbhq.Machines.Nes.Tests`, 980 in `Dbhq.Machines.BbcMicro.Tests` and 77 in `Dbhq.Machines.Kim1.Tests` (70 before the first review; 82 after the second, which is in the next section). `Dbhq.Cpu6502.Tests` passed 1,596 of 1,608: the 12 that failed are Dormann's tests, because as65 is a 32-bit x86 program and this box could not run it. With the assembler run under qemu, as the branch for that fix does (`tests/Dbhq.Cpu6502.TestSupport/Dormann.cs`, copied in for the run and then put back), all 1,608 passed. Then `npm test` in `site` (after `npm ci`, the KIM-1's WebAssembly built here, and the BBC Micro's and the NES's copied from an older build on the same box) ran 378 tests and passed 377, with one marked todo. The site job's floor was 369 at that point, and was raised to 377 in `f91e469`; that figure was from this box. After main was merged in (`7de3519`) the floor was 475. CI's run on `7de3519` passed 483 site tests (the site job's `pass=483`, read from the run's log on 13 October 2026), and the floor in `validate.yml` and `deploy-site.yml` is now 483, from that count and not from this box.

What was not done: the browser check was not run. This box has no Chrome, so the check, the page's real timing and the screen reader behaviour are unverified here; CI runs the check.

## The second review

A second reviewer found no critical items and asked for these, all checked against the code and the log first.

- **The mutation script was not in the repository.** Fixed: `tools/probes/kim1_teletype_mutations.py` holds every fault (22 now) and runs them with `dotnet test` or `node --test`, and the tables above are from one run of it. Two faults are new, 15 (replacing the terminal loses what the old one had printed) and S7 (a repeated line is written without clearing the region first), for the fixes below.
- **`docs/known-differences.md` omitted the hold.** Fixed: it has a bullet for it and names the tests that cover it.
- **`TypeAtTeletype` then a later `Tap("RS")` holds the key behind the RS.** Real, and intended: the hold waits for every tap queued by the time it releases. The comment said "queued before" the call; it now says what it does, and `TeletypeKeysQueuedBeforeATapWaitForTapsQueuedAfterTheCallToo` pins it.
- **The idle test did not cover the handover from held keys to the teletype.** Fixed: the idle check is `Kim1Keystrokes.TeletypeIdle`, so a test can reach it, and the hold test asserts it false while the key waits, at the handover, and true at the end.
- **`SetTeletypeBaud` threw away what the old terminal had printed.** Real, though small. `Kim1Machine.Teletype`'s setter now keeps it for the next `ReadTeletype` (`ReplacingTheTerminalKeepsWhatItHadPrinted`).
- **The range 50 to 9600 was written twice.** It is `Kim1Teletype.MinBaud` and `MaxBaud` now, and one `CheckBaud` serves the constructor and the host.
- **The ST result came from an uncommitted scratch test.** Fixed: `WithoutTheHoldAStopKeyAndTypingQueuedTogetherAreGarbled` keeps it. The RS and RUB OUT scratch test is still not kept, and the entry says so.
- **"77, 70 before" against eight new tests.** Checked and not changed: seven tests had been added in the first review (`git diff` counts seven new `[Fact]`s, and 70 plus seven is 77). After this round there are five more, 82.
- **Wording that reads like a draft on a public page.** Reworded as dated corrections.
- **The floor of 377 was stale.** After main was merged the floor was 475. It is now 483, which is the passing count the site job printed in CI's log on `7de3519` (not this box's count).
- **A repeated line might not be re-announced.** Real: a live region only announces a change, and setting it to the text it already has is none. `Announcer` (in `teletype-paper.js`) clears the region on the frame after it writes, and writes what is waiting on the next clear frame. S7 and a test cover it.
- **A switch the browser restored gave no word that RS is needed.** Fixed: at start, if the switch is on (or the speed is not 110), the status says it was kept by the browser and that RS and RUB OUT are needed. The test reads the page's source; the real behaviour needs a browser and has not been seen.

After these fixes, 13 October 2026: `dotnet build -c Release` gave 0 warnings, `dotnet test tests/Dbhq.Machines.Kim1.Tests -c Release` passed 82, and `npm test` in `site` passed 485 of 486 on this box (one is marked todo).

CI's log of the browser check on `7de3519` (read on 13 October 2026) shows every teletype step passing at 1200 baud, after RS and RUB OUT at 110 baud. It also shows the first thing on the paper at 110 baud as a `~`, where the unit tests show nothing for the RUB OUT echo (the paper does not print DEL). It has not been explained; it is probably the RUB OUT's timing against the reset, since a held key now starts at the end of a frame, and the log-on that followed was correct.

## Limitations

- **No electrics.** The 20 mA loops are levels, not currents: no loop
  resistance, no slow edges, no external supply. A 60 mA loop, a reversed
  connection or a terminal without full duplex cannot be shown.
- **One terminal, at one speed per log-on.** The speed is measured from RUB
  OUT at each reset, as on the board; the page offers three speeds. This model
  logs on at every speed tested from 110 to 2400 baud; the real board's limit
  depends on its loop circuit, which is not modelled.
- **The keyboard is polite.** Keys wait for a quiet line, which a person
  typing on a real Teletype would not always do. A key typed while the monitor
  prints, on the real board, garbles both; here it waits its turn. The page's keys also wait for any
  keypad tap queued before them, which stands in for a person not typing until
  the monitor has answered.
- **PA7 as an output is not modelled against the gate driving it.** A program
  that makes PA7 an output would fight U15D on the real board; here the pin
  shows the 6530's output and the echo uses the gate's level.
- **The paper is a model of print, not of a printer**: it overprints by
  replacing a character rather than striking over it, and it keeps the last
  thousand lines on the page.
- **Paper tape is text, not a reader.** L and Q work, through the keyboard
  and the printer, but there is no tape reader or punch: on the page, a tape
  can only be typed or pasted into the field.
