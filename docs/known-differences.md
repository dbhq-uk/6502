# Known differences

Where the core knowingly differs from a reference it is tested against, or
where no reference we trust exists. Each entry says what, why, and how the
tests treat it. It was written with the plan, after the plan's code had been
run against every reference, and is kept true as the code lands.

## The 65C02's extra decimal cycle, in immediate mode

**What.** On the three 65C02 variants, `ADC #imm` (`$69`) and `SBC #imm`
(`$E9`) take one extra cycle when the decimal flag is set. Tom Harte's data
records that cycle as a read of a fixed address: `$007F`, `$0059` or `$0056`
for `ADC` on WDC, Rockwell and Synertek, and `$0000` for `SBC` on all three.

**Why we differ.** A fixed address that changes between chips and between
two sibling instructions looks like a property of the program that generated
the data, not of the chip. In every other addressing mode the same extra
cycle re-reads the operand's address, so the core does that here too: it
re-reads the immediate byte.

**How the tests treat it.** For those two opcodes, on those three variants,
with the decimal flag set, the comparison checks that the third cycle exists
and is a read, and does not compare its address or value. Everything else in
those cases is compared as normal.

## Synertek's bit-instruction opcodes, where two references disagree

**What.** On the Synertek 65C02, which has no `RMB`, `SMB`, `BBR` or `BBS`,
the opcodes in columns 7 and F are no-ops. Harte's data says the column 7
opcodes are two bytes long and read zero page, and the column F opcodes three
bytes, with an extra cycle in odd rows. Klaus Dormann's extended test, set up
to check them as no-ops, expects `$07` to be one byte long.

**Why we differ from Dormann.** Harte's data is the per-instruction
authority in this project, and the core matches all of it. Which of the two
is right about real Synertek silicon is not known here.

**How the tests treat it.** The Synertek build of Dormann's extended test is
assembled with `rkwl_wdc_op = 2`, which is the test's own setting for leaving
those opcodes out. Everything else in that test still runs.

## `WAI` and `STP`

**What.** WDC's `WAI` (`$CB`) and `STP` (`$DB`) have no data in Harte's WDC
set, because neither can be tested one instruction at a time.

**How the tests treat it.** Tests of our own check what they do: `WAI` waits
for an interrupt and then either takes it or carries on, depending on the
interrupt-disable flag, and `STP` stops until reset. The cycle counts follow
WDC's datasheet and are not checked against the chip. How many cycles `WAI`
takes to wake is not asserted, because no reference we trust gives it.

## 65C02 interrupt timing

**What.** The NMOS interrupt tests are checked against the Visual6502
transistor-level model, and the 65C02 uses the same timing rules in the core.
There is no public transistor-level model of the 65C02 to check that against.

**How the tests treat it.** The 65C02's own interrupt behaviour follows WDC's
datasheet for the decimal-flag clear, the `WAI` and `STP` cycle counts and the
wake rules. It is not checked against the chip. That the 65C02 times
interrupts like the NMOS chip is assumed. The tests check the decimal flag
and the pushed P, and the `WAI` and `STP` cycle counts, wake outcome and
stop-until-reset; they hold no bus logs of a 65C02 taking an interrupt.

## JAM

**What.** The NMOS `JAM` opcodes (for example `$02`) lock the chip up. Harte
records one step of one: the opcode fetch plus ten reads.

**How the tests treat it.** The core makes those eleven bus accesses and
matches Harte's data. What happens after that is not checked against any
reference: the core reads `$FFFF` on every `Step`, ignores IRQ and NMI, and
is cleared by `Reset`.

## Unstable NMOS opcodes

**What.** `ANE`, `LXA`, `SHA`, `SHX`, `SHY` and `TAS` give results on real
chips that vary between individual parts, and for `ANE` and `LXA` with
temperature.

**How the tests treat it.** The core matches Harte's data, which fixes the
`ANE` and `LXA` constant at `$EE`. That is one answer, not every
chip's.

## The KIM-1: reading the 6530 timer after it has passed zero

**What.** Appendix H of the KIM-1 User Manual says that once the timer has
counted past zero, reading either `$1706` or `$170E` will "disable the
interrupt option". The MCS6530 data sheet says address line A3 sets the
interrupt enable on every read or write of the timer, and `$170E` has A3 high.

**Why we differ from the manual.** The data sheet describes the chip; the
manual describes how the board's users should use it, and its own table one
paragraph earlier says reading `$170E` enables the interrupt. Which one real
silicon does after zero was not checked here.

**How the tests treat it.** `Rriot6530Tests` checks that a read with A3 high
enables the interrupt. Nothing on a stock KIM-1 connects the timer's
interrupt to the CPU, so the monitor never depends on it.

## The KIM-1: the data sheet's timer example, off by one

**What.** The data sheet's worked example writes 52 at divide by 8. Its text
says the interrupt comes at (52 x 8) + 1 = 417 clocks, and its two later
readings, `$E4` at 444 and `$AC` at 500, agree with 417. Its Figure 5 says the
interrupt occurs "at pulse 416".

**How the tests treat it.** The timer follows the text and the two readings:
the flag rises on clock N x k + 1 after the write. `TheDataSheetsWorkedExampleHolds`
checks 25 at clock 213, 0 at 415, `$FF` at 417, `$E4` at 444 and `$AC` at 500.

## The KIM-1: what is modelled rather than measured

**What.** Four behaviours of the board come from a reasoned model, not from a
reference that could be tested against:

- **Open bus.** A read where nothing answers ($0400-$13FF and $1400-$16FF)
  returns the last byte on the data bus. No measurement of a real board was
  found.
- **RAM at power on** is all zeros. Real static RAM starts with arbitrary
  contents.
- **The display.** A digit shows each segment that was on for more than half
  of the cycles it was selected, and goes dark 20 ms after it was last
  scanned. That stands in for the eye, not for the LEDs.
- **Single step.** The SST logic raises NMI on the cycle the CPU fetches an
  opcode outside `$1C00-$1FFF`. The machine treats the first access of every
  instruction, and of every interrupt sequence, as that fetch.

**How the tests treat it.** The acceptance tests depend on the display
model and on single step, and pass with the original monitor ROM doing what
the User Manual says it does. The open-bus value and power-on RAM are pinned
by tests of the memory map and the boot state, so a change to either is
seen.

## The BBC Micro: the 6522 VIA's shift register, mode 010 only

**What.** `Via6522` models one of the shift register's eight modes: 010, shift
in under the system clock, because it is the only one the BBC's ROMs use (the
DFS, `via.md` section 1.8). In that mode IFR2 rises `ShiftMode2Cycles` (19)
cycles after the SR read or write that starts it. That number was measured
with a ruler off the WDC datasheet's drawing of the CMOS part, and the sheet
marks it `[guessing - verify]`. The eight bits are all taken from CB2 when the
flag rises, not one per shift pulse, and CB1 puts out no shift clock.

**The other modes** (000, 001, 011, 100, 101, 110 and 111) are not modelled:
the register can be written and read, but it shifts nothing and IFR2 is never
set. A program that uses them, which nothing on a stock Model B's boot or disc
path does, will see no flag.

**How the tests treat it.** `ShiftMode2_SetsIfr2ShiftMode2CyclesAfterAnSrRead`
checks the flag at the constant's cycle, and `ShiftMode0_SetsNoFlag` checks
mode 000. Neither is a check against a real chip.

## The BBC Micro: timer 1 after a one-shot expiry

**What.** After timer 1 expires in one-shot mode, the datasheets' text says the
counter keeps decrementing past `$FFFF`; the datasheet's figure, and Rich
Talbot-Watkins from real machines, say it reloads from the latch as in
free-run (`via.md` section 1.4 and its disagreement table). Which one the
silicon does was not settled.

**Why we chose.** `Via6522` reloads in both modes, following the figure and
the real-machine report over the text. The operating system never reads timer
1 after a one-shot expiry, so the boot does not depend on it.

**How the tests treat it.** `OneShotTimer1ReloadsFromTheLatchAfterExpiry_AnAssumption`
pins the choice, so a change to it is seen. It is not a check against a chip.

## The BBC Micro: 6522 behaviour taken from words, not measured

**What.** Some of the VIA comes from the datasheets' prose or from inference,
with no cycle count to test against, and some measured quirks are not built:

- **The coincident acknowledge.** A clearing access in the cycle a flag rises
  does not clear it, and IRQ follows a cycle late. Stardot measured this for
  timer 1 on real Model Bs and Masters (`via.md` section 1.9). `Via6522`
  applies it to every flag set by the clock: both timers and the shift
  register. It does not apply it to CA1, CA2, CB1 and CB2, whose edges arrive
  between cycles.
- **Not built:** an ACR write in the same cycle as a timer 1 expiry (measured:
  ACR `$00` wins), a latch write racing the timer 1 reload (measured, but the
  rule could not be decoded from the published numbers), a change of ACR bit 5
  taking effect one cycle late (measured), and the exact timing of a T2C-H
  write in pulse-counting mode. All are in `via.md` sections 1.4, 1.5 and 1.9.
- **From words only:** the CA2 and CB2 handshake and pulse outputs (low from
  the access; in pulse mode high again at the start of the second cycle after
  it), and timer 2 counting a PB6 pulse when PB6 is low at the start of a cycle
  having been high at the one before.
- **Power-on values nobody measured:** the timer counters and latches and the
  shift register start at zero, the PB7 timer output starts high, the four
  control lines start low, and the port inputs start at `$FF` (inputs with
  nothing attached read 1 on a real Model B). Reset leaves no one-shot armed.

**How the tests treat it.** The coincident acknowledge for timer 1 is
`Example09`, from the real-machine result. The rest is pinned only where a
test names it, and the BBC's own firmware uses none of it.

## The BBC Micro: the VIAs' wiring and the keyboard, where the sources stop

**What.** `SystemVia`, `UserVia` and `BbcKeyboard` follow `via.md` sections 2 and
3, and in five places the sources give no measurement:

- **The latch is strobed on writes to ORB and DDRB only.** The real board's
  flip-flop (IC31) strobes IC32 on every write to the system VIA. The two are
  the same while PB0 to PB3 are outputs, which the OS sets at reset and never
  changes (`via.md` section 2.2). A program that made PB0 to PB3 inputs and then
  wrote some other register would, on a real machine, strobe whatever the
  floating pins read into the latch; here nothing would change.
- **What IC32 holds at power on** is not known; its /CLR is tied high, so no
  reset clears it. It starts at `$00`.
- **PA7 with the keyboard disabled** (latch bit 3 high) reads 0. Nothing drives
  it then, and the sheet recommends 0 with no speech chip; nobody measured it.
- **Autoscan** is a counter that walks columns 0 to 15, one a microsecond, and
  CA2 is high while the counter's column has a key down in rows 1 to 7. That is
  the schematic's design (a 74LS163 clocked at 1 MHz), so a held key gives one
  edge per 16 microseconds; no source measured the edge's timing.
- **Idle levels nothing measured:** the system VIA's PB6 reads 1, and the user
  VIA's CB1 and CB2 idle high like the rest of the user port. CB1 and CB2 on the
  system VIA (the ADC and the light pen, not fitted) never change.

**How the tests treat it.** `SystemViaTests` pins each choice that a test can
see. The OS's own sequences (the link read, the key test at `$F02A`, the column
test of the full scan and the CA2 interrupt) are driven through the bus as the
ROM does them, and those are the checks that matter for the boot.

## The BBC Micro: booting before the video and the disc exist

**What.** From task 5 the real MOS 1.20 boots to the BASIC prompt, but three
parts of the Model B are not there yet, and each shows in what the machine does:

- **The 6845 CRTC was a set of registers** until task 7, with no counters and
  no vertical sync, so the OS's 50 Hz vsync interrupt never came. Since task 7
  it is the counter model in the next section, its VSYNC drives CA1, and the OS
  takes a vsync interrupt every field.
- **The video ULA was a pair of registers** that took writes and drew nothing
  until task 8. Since then it draws modes 0 to 6 (the next section but one);
  mode 7's picture waits for the teletext chip, task 9. A read is Econet's
  INTON and answers as an absent fast device, `$FE`.
- **No 8271 was fitted** until task 12, so `$FE80` read `$FE`. The DFS reads
  that status before it serves any call, saw bits 0 and 1 set, and stayed
  silent, so the `Acorn DFS` line a real Model B prints was missing from the
  boot screen (`bus.md` section 4b, corrected in task 5). Since task 12 the
  8271 is there, its idle status is `$00`, and the line is printed.

**How the tests treat it.** `BootTests` reads the screen as character codes from
mode 7's screen memory, which needs no video chip, and expects the screen the
ROMs print: `BBC Computer 32K` on row 1 and, since task 12, `Acorn DFS`,
`BASIC` and `>` on rows 3, 5 and 7 (rows 3 and 5 held `BASIC` and `>` before).
Tasks 7 and 8 put a working CRTC and video ULA in place of the stand-ins, and
task 12 fitted the 8271.

## The BBC Micro: the 6845 CRTC, where the model stops

**What.** `Crtc6845` is the HD6845S counter model of `video.md` section 1.4, from
the Amstrad CPC CRTC Compendium's "CRTC 0" chapters and the Hitachi datasheet,
with the two quirks section 1.6 asks for: the end of the frame decided while C0
is 0 or 1, and a line of two characters (R0 = 1) never disarming the vertical
adjust. Where the sources disagree or stop, it chooses:

- **The half-line VSYNC is `(R0 + 1) / 2` characters late, not `R0 / 2`.** The
  Compendium puts it at C0 = R0/2, which for the BBC's odd R0 (127 and 63) is a
  character short of half a line, and would make the vsync interrupts 39,999 and
  40,001 cycles apart (39,998 and 40,002 at 1 MHz). On a real Model B a scope
  showed "exactly half a line (32us)" and interrupts were timed at about 40,000
  cycles in both fields (`video.md` s1.4, S14). The model takes the BBC
  measurement; a one-character difference was not resolved by either.
- **The VSYNC width is counted in whole lines from the pulse's start,** so in an
  even field the pulse's fall is half a line late too. The Compendium says a
  shortened VSYNC "stops at the end of the HSYNC of this line", which suggests
  HSYNC drives the count; counted that way, both fields' falls would land at the
  same point of a line and the interrupts would alternate 39,936 and 40,064
  cycles, which the S14 timing rules out.
- **Not modelled:** R0 = 0 freezing C9 and C4 (Compendium 13.2.6; here every
  character is a line end); a VSYNC started by writing R7 to equal C4 in the
  middle of a row, and the Compendium's blocking of the VSYNC when R7 is written
  while C0 is below 2 (here VSYNC starts only at the start of row R7); the
  vertical display switched by an R6 write in the middle of a row; the
  balancing of an odd R9 in interlace sync and video between rows of even and
  odd lines, and the VSYNC a line late on odd rows that comes with it (here a
  field's rows have R9 / 2 + 1 lines; the datasheet asks for an even R9 and the
  BBC uses 18); the parity rules when R8 changes in the middle of a frame; R9
  written exactly at C0 = R0 changing C4 and C9 together; the light pen.
- **Choices nobody measured:** a register write takes effect from the next
  character after the cycle it is written in, and the CRTC's 1 MHz character
  clock ticks on the same even cycles as the VIAs; R3's HSYNC width 0 gives no
  HSYNC (section 6, item 9); the cursor is on when RA is between R10 and R11,
  so R10 above R11 gives none; the cursor blinks with a 50 per cent duty,
  counted from the reset; C0 running past R0 to 255 ends the line there; the
  field after a reset is an even one; R0 to R11 read `$00`.
- **What resets it.** Power on and BREAK both reset the CRTC's counters and keep
  its registers. Its /RES is taken to be on RST, which the hardware guide says
  goes to all the circuitry but the system VIA (S9 s3.14); IC2's pin was not
  read on a schematic. A reset that drops a high VSYNC is an edge on CA1.

**How the tests treat it.** `Crtc6845Tests` asserts the sheet's section 5
numbers for each mode set: 40,000 cycles between vsync falls, 39,936 with
interlace off, 625 lines a frame with VSYNC at line 280, ten rasters a field
row in mode 7, 40 characters and HSYNC at 49 for 4 in mode 4, and 256 or 250
displayed lines, plus the two quirks. `CrtcEquivalenceTests` and the bus
tests compare the lazy chip with a per-character model of the same rules
(`Oracle/ReferenceCrtc6845.cs`), so they pin the model, not the hardware where
the list above says the model guessed.

## The BBC Micro: the video ULA, where the model stops

**What.** `VideoUla` is the ULA of `video.md` section 2: the control register,
the palette with its XOR 7 and flash, the shift register read at bits 7, 5, 3
and 1 and filled with 1s, DISEN as DISPTMG and not RA3, the cursor's three
segments, and the address translation with the hardware wrap. It draws a line
at a time from the CRTC's state at the line's end, and before any write that
changes what it draws it draws up to that write's cycle. Where the sources stop
or the model takes a shortcut, it chooses:

- **Screen memory is read when a line is drawn, not in each byte's own
  cycle.** The ULA draws a line at its last character, or up to a register
  write that comes first, and reads that stretch's bytes then. A store to screen
  memory in the middle of a line's scan is seen by the whole of the stretch
  drawn after it, where a real ULA fetching byte by byte would show the old
  bytes left of the store and the new ones right of it. Doing better would mean
  logging every store the CPU makes to RAM, which is most of its writes, so
  every program would pay for the few that race the beam. The equivalence tests
  change screen memory only where the two readings agree, which is why they can
  be exact.
- **Register writes are exact to the character, not the cycle.** Palette,
  control, CRTC and latch writes are seen from the first character clocked after
  the write's cycle. At 1 MHz (modes 4 to 6) a character lasts two CPU cycles,
  and a write in the second of them is still applied from the next character,
  half a character (8 of the framebuffer's pixels) after the write: the ULA's
  pipeline delay is not known (next item), so placing a write inside a character
  would be guesswork. `APaletteChangeInTheMiddleOfALineChangesTheColourFromTheNextCharacter`
  pins both halves.
- **Together these narrow Dan's choice** in the design of a cycle-accurate video
  path rather than a scanline renderer: registers to the character, screen
  memory to the line. The narrowing was the task 8 controller's, for speed, and
  the journal entry for 3 October 2026 says exactly what is and is not
  cycle-exact.
- **The ULA's pipeline is taken to have no delay.** `video.md` s6 item 3: how
  many characters lie between the CRTC's fetch and the pixel is not documented,
  and nor is how DISEN and CURSOR line up with it. The model has none
  (`VideoUla.PipelineDelayCharacters` is 0): a character's pixels start in the
  cycle of its own character clock, and a register write in cycle `t` is seen
  from the first character clocked after `t`. So a palette change in the middle
  of a line changes the colour from the next character, where the sheet's
  guess was "from the next pixel"; a real machine may change it a character or
  two later, or part way through a character.
- **The RA3 gate is taken to be off while the teletext select is on.** Mode 7
  runs RA 0 to 19 and is not blanked, so something must switch the gate off;
  `video.md` s6 item 4 says no source describes it. Task 9 kept this choice: the
  teletext path never looks at RA3, and a test shows a descender on the row's
  last two lines. How the board does it is still not known.
- **Mode 7 is the SAA5050's picture,** with the ULA's cursor drawn over it; the
  next section says where that model stops.
- **What the registers hold at power on is not known,** and the model takes
  zero, which also starts the CRTC on the 1 MHz clock. The ULA has no reset pin
  (the pin list in the sheet's S8), so BREAK leaves both registers; the OS
  writes them again in the mode change every BREAK makes, so nothing the OS does
  depends on it.
- **The shift register fills with 1s.** BeebWiki says "or random values on some
  machines". It shows only where a character outlasts its byte, 80 columns on
  the 1 MHz clock, which no OS mode uses.
- **Not modelled:** the INVERT pin on link S26, so the picture is always normal
  video; any analogue behaviour of the RGB outputs.
- **Where the picture lands is the model's, not a television's.** The
  framebuffer measures across from the CRTC's character 0 and down from its
  frame start (`Framebuffer`'s remarks), so moving HSYNC with R2 or VSYNC with
  R7 does not move the picture as it would on a screen, and only the first 256
  lines of a CRTC frame are kept. With the OS's registers every mode lands in
  the same place.

**How the tests treat it.** `VideoUlaTests` asserts the sheet's numbers: the
eight control bytes, the palette protocol and its flash pairs, one byte per
mode turned into pixels by hand, the address for MA and RA with the wrap for
each mode and the ROM's latch bits, RA3 blanking in modes 3 and 6, a scroll by
R12 and R13, the cursor's width per mode, the flash select on the screen, a
palette change in the middle of a line, and mode 1 booting with its banner
drawn. `VideoUlaEquivalenceTests` compares the whole framebuffer at the end of
each frame (at most one every 4,000 cycles) with a plain model that draws every character in its own cycle
(`Oracle/ReferenceVideoUla.cs`), so it pins the model, including the choices
above that both share, not the hardware.

## The BBC Micro: the SAA5050 teletext chip, where the model stops

**What.** `Teletext` is the SAA5050 of `video.md` section 4 and the Signetics
datasheet: the English character set (Bedstead's CC0 table, checked against all
96 glyphs of the datasheet's Figure 11), character rounding as the datasheet
describes it, the block graphics of Figures 9 and 10, the control codes of
Table 1, double height, hold graphics, conceal and flash, and the chip's own
count of lines and rows. `VideoUla` feeds it and draws its cells. Where the
sources stop, the model chooses:

- **`$80` and `$90` do nothing.** Table 1 marks codes `0/0` and `1/0` (NUL and
  DLE) reserved, "normally displayed as spaces"; ETSI's alpha and graphics black
  are not on this chip. Whether the BBC's chip really ignores them is `video.md`
  s6 item 5. The box codes (`$8A`, `$8B`), SO and SI (`$8E`, `$8F`) and ESC
  (`$9B`) also do nothing but show a space: the PO and DE tie-offs that decide
  the boxes on the board were not read (s6 item 6).
- **Hold graphics is the die-shot description's shift register** (`video.md`
  s4.3, its S15), not ETSI's rule. The chip loads a register with each cell's
  block, a space for anything else, and under hold shows the register on a
  control cell in graphics mode instead of loading it. Hold takes effect from
  the cell after `$9E`, so the hold code's own cell loads a space, which is why
  the SAA5050 holds only blocks drawn after the hold code. The sheet's table in
  s4.2 calls `$9E` set-at, as ETSI does; this model follows s4.3.
  **Release** (`$9F`) is taken to act from the next cell; the die-shot
  simulation's two characters is doubted by its own author (s6 item 11).
  **Capitals in graphics mode** (`$40` to `$5F`, which blast through) are taken
  to load a space into the register, inferred from the description of the
  "no alternative graphics" signal; ETSI would have them leave the held block
  alone. **A height code that changes the height** stops hold on its own cell,
  `$8D` included, though `$8D` changes the height only from the next cell
  (`Teletext.HeightChangeStopsHoldOnItsOwnCell`).
- **Double height maps rows the model's own way** (s4.3, s6 item 5): the upper
  row shows the cell's lines 0 to 4 (its blank top line and glyph rows 0 to 3)
  and the lower row lines 5 to 9 (glyph rows 4 to 8), each on two
  lines of the field, rounded against the line before on the first and the line
  after on the second, the same in both fields. The sheet's guess was glyph
  rows 0 to 4 for the upper half and 4 to 8 for the lower, repeating row 4; the
  model instead splits the ten-line cell into two halves of five, so no row is
  shown twice. A new field starts with an
  upper row (the row state is reset at DEW, which no source states), and the
  model sees a double height code only on lines drawn with the teletext select
  on.
- **Separated graphics leave the left two half-dots and the last line of each
  block as background** (`Teletext.SeparatedGap`): no source gives the positions
  (s6 item 5). Figure 10, drawn to scale, shows each block flush with the top
  and right of its place, with gaps left and below of about two of the cell's
  twelve half-dots and two of its twenty frame lines.
- **The chip counts its own lines.** It is taken to step its line count at the
  end of every line during which DISPTMG after the skew (LOSE) was high, and to
  reset it, with an upper row, at DEW, which the model sees as VSYNC having
  risen by the end of a line. The datasheet and the sheet say DEW resets the
  count and GLR and LOSE move it, not on which edge (s4.1). So a program that
  changes R9 in mode 7 does not move the rows, as the sheet's MODE 7/75 note
  says real hardware does not.
- **The pipeline is three characters, and lined up by the R8 skews.** The cell
  for the byte fetched in character `c` is shown if DISPTMG after the skew is
  high in character `c + 1` (the chip latches the byte with LOSE a character
  after the fetch), and its picture leaves the chip three characters after the
  fetch (`VideoUla.TeletextDelayCharacters`), when the ULA's cursor inverts it.
  No source gives either figure; three is what the datasheet's 2.6 to 2.77 us
  and the OS's two-character cursor skew plus segment 1 both give. The datasheet
  puts graphics out about a sixth of a microsecond (one dot) earlier than
  alphanumerics; the model draws both in the same cell.
- **Cells are drawn where their bytes were fetched,** three characters left of
  when they leave the chip, so mode 7 fills the same 640 pixels as the other
  modes. A pixel shows the half-dot under its centre: 16 pixels for 12 half-dots
  at 1 MHz, 8 at 2 MHz.
- **The ULA's display enable does not blank the teletext picture;** the chip's
  own LOSE does. Inferred: DISEN is a character ahead of the chip's output, and
  if it gated the picture the last cells of each row would be cut.
- **Changing the teletext select in the middle of a line** shows the three cells
  already in the chip as black, and the model does not run the chip's decoding
  on the stretches drawn with the select off, so its colours and attributes
  start again from the row's defaults when the select comes back on that line.
- **Flash** is off for 16 fields and on for 48 of every 64, the die-shot figure
  (s4.3), not the datasheet's 0.75 Hz at 3 to 1. The counter is 0 at power on.
- **At power on** the chip's counts are taken to be 0, an upper row; the chip
  has no reset pin, so BREAK leaves it.
- **Character rounding** is the datasheet's description: compare each row with
  the row before (even field) or after (odd field), and give an off dot the half
  next to an on dot where the two rows cross diagonally. The rows above the
  cell's first line and below its last are taken as blank.
- **Screen memory** is read when the line is drawn, as in the other modes.

**How the tests treat it.** `TeletextGlyphsTests` checks every glyph against the
grids typed from Figure 11. `TeletextTests` asserts, half-dot by half-dot and
worked by hand, the colour codes, flash and steady, the backgrounds, conceal,
the blocks of Figures 9 and 10, blast-through capitals, rounding in both fields
and in double height, the double height halves and the lower row's background,
hold and release with the SAA5050's late start, the held block's own form,
height change and conceal against hold, the flash's 16 and 48 fields, the
chip's line and row count, and on the machine the text, colours, descenders and
cursor of mode 7 and the boot banner. `VideoUlaEquivalenceTests` compares the
lazy ULA's mode 7 frames with the per-character oracle, whose teletext chip
(`Oracle/ReferenceTeletext.cs`) is written separately and works out each
half-dot directly; it pins the model, including the choices above, not the
hardware.

## The BBC Micro: the SN76489 sound chip, where the model stops

**What.** `Sn76489` is the chip of `via.md` section 4: three tone counters, a
noise counter and a 15-bit shift register (seed `&4000`, taps 0 and 1, white or
periodic), the 2 dB attenuators, and a unipolar mix of the four, 0 to 1. Where
the sources stop, the model chooses:

- **A tone period of 0 counts as 1024.** No source establishes it for a real
  chip (s4.3): SMS Power treats 0 like 1, a constant level, and jsbeeb (a
  cross-check only) like 1024. The sheet recommends 1024, because a 10-bit
  counter loaded with 0 would pass 1023 before it reached zero, and the model
  takes that. The same holds for the noise at rate 3 when tone 3's period is 0.
  The OS never writes a period of 0: its tables give 25 at the least.
- **A write lands at once, when latch bit 0 falls,** after the chip clock of
  that CPU cycle. The real chip takes about 32 of its clocks to load and was
  measured responding 3.7 to 8 microseconds after write enable fell (s4.1); the
  OS holds write enable low for 17 cycles and pads 21 more, so it never writes
  inside that window, and taking the byte at once changes nothing it does.
  Holding write enable low does not take the byte again every 16 microseconds
  as the real chip does, so sample-playing code that relies on that re-latch
  (and the noise reset each re-latch makes) is not modelled.
- **The chip clock is every eighth CPU cycle, from cycle 0.** The chip runs on
  the video ULA's 4 MHz divided by 16; its phase against the CPU's 2 MHz is not
  documented, so a chip clock is taken to fall at the end of every CPU cycle
  that is a multiple of eight.
- **Power on is silent,** with every period and the noise control 0, the tone
  counters at 1024, the noise counter at 16, every flip-flop low and the shift
  register at `&4000`. A real chip starts in an unknown state (s4, "Could NOT
  establish"); the OS silences it at reset within about a quarter of a second.
- **Rate 3 noise has its own counter, reloaded with tone 3's period,** as SMS
  Power describes it, rather than shifting on tone 3's own output edges, which
  the datasheet's "tone generator 3 output" might mean. The rate is the same;
  the noise's phase against tone 3 may differ.
- **A write to the noise control does not reset the noise counter or its
  flip-flop,** only the shift register; no source says either way.
- **A sample is the mean of its chip clocks** (box averaging), not the output
  of an analogue chain. A real BBC's filters smooth a period-1 tone's 125 kHz
  into a steady half level; the box average gives a sample of five or six
  clocks (at 48 kHz) a level within a tenth of the channel's amplitude of that,
  so period-1 sample playback carries a little noise the real machine filters
  away. No DC is removed and the output is not inverted (s4.6: neither is
  audible).

**How the tests treat it.** `Sn76489Tests` asserts the sheet's worked examples
(s4.8), the noise vectors (s4.4), the volume table (s4.5) and the noise rates,
and pins the period-0 choice. `SoundTests` checks the write protocol through the
system VIA, the cycle a write lands in, and on the real OS the reset writes, the
start-up beep, the pitch table and a `SOUND` command's tone in the samples.
`SoundEquivalenceTests` compares the lazy chip with a clock-by-clock oracle
(`Oracle/ReferenceSn76489.cs`), written separately from the same model; it pins
the model, including the choices above, not the hardware.

## The BBC Micro: the 8271 and its discs, where the model stops

**What.** `Fdc8271` is the minimal controller of `disc.md` section 3, which the
fact sheet's probe showed DFS 1.20 cannot tell from a real drive, and
`DiscImage` holds `.ssd` and `.dsd` files. Where the sources stop, the model
chooses:

- **Time.** Seeks are instant, the head never needs to load or settle and the
  disc never spins up. Every drive command reaches its first byte, or its result
  if it moves none, a fixed 2,000 cycles after it starts, the sheet's guess at a
  plausible figure; DFS passed with anything from 0 to 60,000 (s1h). Bytes come
  every 128 cycles, a sector after another with no gap, and the result one byte
  time after the last byte. A real drive would wait for the sector to come round.
- **Late data.** A byte not taken in its 128 cycles ends the command with
  `$0A`, INT staying high from the byte to the result, as the sheet's design
  has it (s3). *Corrected in task 12b: task 12 first let INT fall for a cycle
  between them and listed a residual hang, both because the core lost an NMI
  that a real 6502 takes; the core is fixed, see the 4 October journal entry.*
- **Not ready** is latched by a drive command that finds no disc, and the next
  Read Drive Status reports not ready once and clears it (D1's footnote: issue it
  twice to clear it on a ready drive). Ready is "a disc, the drive selected and
  the motor bit set" in the drive control port; the index-pulse timer that makes
  it on a real BBC is not modelled.
- **Not modelled:** Scan, Read ID and Format end after the start delay with
  result `$00` and move no data; deleted-data marks (Read Data and Deleted reads
  as Read Data, Write Deleted Data writes ordinary data); DMA mode (the mode
  register is stored and ignored); bad-track registers; 128-byte and 512-byte
  sectors (none exist on these images, so asking for one gives `$18`); the
  index, fault and count lines (they read 0); holding the chip in reset (a 1
  written to `$FE82` resets it at once); an opcode not in the datasheet ends at
  once and does nothing. `$FE82` and `$FE83` read `$FE`, as nothing drives the
  bus. The drive control input port (special register `$22`) reads like Read
  Drive Status, a guess, since D1 gives no layout and DFS never reads it.
- **Power on and BREAK.** At power on the chip is as after a reset, with every
  special register 0. BREAK leaves it alone: no source read says its reset pin
  is on the reset line, and the DFS resets it through `$FE82` itself.
- **Images.** A `.dsd` is read track interleaved, the layout the sheet calls the
  most common (s4); a sequential one reads wrongly. An image shorter than its disc
  is extended with zeros, as the sheet recommends, which has never been tried
  against a real drive because no real disc is short. 40 or 80 tracks is decided
  by the length, except that an image short enough to be 40 tracks whose
  catalogue says 800 sectors is taken as an 80-track disc trimmed to the part in
  use (s4's second test).

**How the tests treat it.** `Fdc8271Tests` asserts the sheet's chip test (s3),
the status values DFS saw (s1b), not ready, write protect, sector not found, side
select, late data and the head unloading, on the chip alone. `DiscImageTests`
checks the image layout and the blank catalogue, and `DiscTests` the real DFS
through the machine: `*CAT`, `*SAVE`, `*LOAD`, `*RUN`, `*INFO`, `*ACCESS`,
`*TITLE`, `*OPT 4`, `*DELETE`, the error messages, the empty-drive poll and the
disc swap. `FdcEquivalenceTests` compares the lazy chip with a cycle-by-cycle
oracle (`Oracle/ReferenceFdc8271.cs`), written separately from the same model,
on its own, on the bus and through DFS; it pins the model, including the choices
above, not the hardware.

## The BBC Micro: its page in the browser, where it is not the machine

The page (`site/public/bbc-micro.js`, on the host `BbcHost` in
`src/Dbhq.Machines.BbcMicro.Wasm/`) puts a PC's keyboard, screen and speakers in
front of the model. Where that differs from sitting at a Model B:

- **Every key is held for at least 40 ms and rested 40 ms before the same key
  goes down again** (`BbcKeyPresses`, 80,000 CPU cycles each). A browser can
  report a key down and up in the same frame, with no machine time between, and
  the OS, which reads the keyboard on its 100 Hz tick, would never see it. On a
  real keyboard a tap that short is missed; here it counts. A key held longer is
  held as long as it is held, so the OS's own auto-repeat is unchanged. Keys are
  played in the order they came, so a key may still wait behind the hold of the
  one before.
- **Keys are mapped by where they are on a PC keyboard** (`site/public/bbc-keys.js`),
  so some BBC keys have no PC key here: TAB (Tab moves the focus, so the page
  never keeps the keyboard), SHIFT LOCK, and the red function keys f0 to f9 (the
  browser keeps its function keys). CAPS LOCK is one press for each time the PC
  key goes down, whatever the PC does with its own light.
- **The picture is the framebuffer, shown four wide by three high,** with no
  model of a television: no scan lines, no blur between lines, no glow, no
  overscan and no curve. The framebuffer starts at the CRTC's first displayed
  character, not at a fixed distance from sync, so the picture does not move when
  a program moves the sync, as it would on a television (`Framebuffer`).
- **The sound has its steady level taken off** by a high-pass filter in the
  worklet (`site/public/bbc-audio.js`), standing in for the coupling capacitor on
  the machine's output, whose real value is not modelled. It plays through a
  short queue that is cut back when it grows past 0.15 s, so a long stall drops
  sound rather than playing it late.
- **One drive.** The model has both of the 8271's drives; the page offers drive 0
  only.
