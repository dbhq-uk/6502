---
title: "The KIM-1's teletype"
date: 2026-10-08
summary: "The KIM-1 gets the terminal most of its owners used: a teletype on its 20 mA loops, its bits sent and decoded in machine cycles while the original monitor bit-bangs the line itself. The monitor times RUB OUT, prints KIM, and takes a program typed at the teletype, runs it and prints the result; the page has the terminal, with the session from the test's own file."
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
- **`site/scripts/browser-check-kim1-tty.mjs`**, the same session in a real
  browser.

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
  on the other.
- **The page's paper is a `role="log"` of one row per line**, so a screen
  reader hears a new line rather than the whole paper again. The field types
  only while it has focus, in capitals; Enter is CR and Backspace or Delete is
  RUB OUT; LF has a button, and so does RUB OUT for a touch screen. Tab is never
  taken. The keypad's own keyboard handler now ignores keys typed anywhere in
  the teletype, so a hex digit typed at the terminal is not also a keypad press.
- **The browser check runs the session at 1200 baud,** after logging on and
  opening an address at 110. At 110 baud the session is the best part of a
  minute of the machine's time (below), and the page runs the machine in real
  time, so the check would add that to every CI run; at 1200 it takes seconds,
  and 110 is still exercised in the browser.
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

Measured on 8 October with `dotnet test tests/Dbhq.Machines.Kim1.Tests -c
Release --filter Kim1TeletypeTests --logger "console;verbosity=detailed"`,
which prints what three of the tests measure:

- **The monitor's bit at 110 baud is 9,088.8 cycles**, against 9,090.9 for
  110 baud exactly, 0.02 per cent short. All 27 characters it prints after
  logging on measured the same, start edge to stop edge 81,799 cycles (nine
  bits). The shortfall is the 14-cycle step of its counting loop.
- **Logging on**, from power on to the line going quiet after the prompt (the
  RS key's 80 ms hold and rest included, and the test's run in 10,000-cycle
  steps): 2,920,123 cycles at 110 baud, 1,130,165 at 300, 350,030 at 1200 and
  220,014 at 2400.
- **The page's session at 110 baud takes 51,083,187 cycles**, 51 seconds of
  the KIM-1's time, of which the program's fourteen bytes are 28,211,802: each
  byte is three keys and an answer of sixteen characters (CR, LF, six NULs and
  `020x xx `), at ten characters a second. This is why the browser check runs
  the session at 1200 baud.

## Every new assertion was shown to fail

MUTATIONS

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
- **A mutation run was lost to `pkill -f`**, which matched the shell running
  it and killed the run part way through a mutation, leaving it applied in the
  scratch copy. The scratch copy was rebuilt and the whole run done again.

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
  prints, on the real board, garbles both; here it waits its turn.
- **PA7 as an output is not modelled against the gate driving it.** A program
  that makes PA7 an output would fight U15D on the real board; here the pin
  shows the 6530's output and the echo uses the gate's level.
- **The paper is a model of print, not of a printer**: it overprints by
  replacing a character rather than striking over it, and it keeps the last
  thousand lines on the page.
- **Paper tape is text, not a reader.** L and Q work, through the keyboard
  and the printer, but there is no tape reader or punch: on the page, a tape
  can only be typed or pasted into the field.
