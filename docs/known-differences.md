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

## An NMI that comes while an interrupt reads its vector

**What.** The NMOS core takes an NMI whose edge comes while `BRK` or an IRQ
reads its vector if the line is still active in the cycle after the vector's
high byte, after the handler's first instruction; a shorter pulse is lost. An
NMI whose edge comes while an NMI reads its own vector is lost, held or not.
Until 4 October 2026 the core lost every NMI whose edge came on the vector's low
byte, a rule drawn in stage 1 from transistor-model runs that all released the
line two cycles after it went active. The BBC Micro's 8271 holds its interrupt
line, and with the old rule DFS lost the interrupt that announces a command's
result often enough to hang: a scratch soak of disc commands stalled within 692
commands in every seed (task 12's review), and none in 20,000 after the fix.

**Why it is listed.** It is no longer a difference from the transistor-level
model: the core was changed to match it (commit `e977cc9`). It is listed because
a public claim of stage 1 changed with it (the 30 September journal entries
carry dated corrections), and because the model is the reference, not a chip
measured on a bench.

**How the tests treat it.** `TransistorModelTests` compares the core with 266
runs of the Visual6502 model, 116 of them added with the fix, holding NMI or
pulsing it for one to four cycles around the vector reads of `BRK`, IRQ and NMI;
13 of them fail on the old rule. `DiscTests`' `*TITLE` case fails on the old rule
too. The 65C02 variants share the code; their own interrupt timing is the
section above.

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

## The BBC Micro: the bus and the memory map, where the sources stop

**What.** `BbcBus` is the memory map and the 1 MHz stretch of `bus.md`. Where
the sources stop, it chooses:

- **The 1 MHz clock's phase at power on.** A slow access waits one cycle from
  an even count of CPU cycles and two from an odd one. Which phase a real
  machine starts in is not known (section 6), so the count starts at zero,
  which is even. A program that times itself against the stretch could see the
  other phase on a real machine.
- **An empty ROM slot reads the high byte of the address,** the last value on
  the bus. That was measured for an absent fast device, on one machine, not for
  an empty ROM socket (section 6, item 3). The OS reads an empty slot's header
  at boot to decide whether a ROM is there, and with this value the slot fails
  that test as an empty socket does.
- **What is not fitted answers as the bus does:** FRED and JIM read `$FF`, an
  absent fast SHEILA device the high byte of its address (`$FE`), an absent slow
  one `$00` (section 1d). Each was measured on one machine by one poster, and
  the OS's Tube probe and the DFS's Econet probe depend on them.
- **`$FE18-$FE1F`, the station ID, is taken as slow.** No source lists it
  (section 6, item 2). It matters only with Econet, which is not modelled.
- **At power on** the ROM latch selects slot 0, an empty slot, and RAM is all
  zeros. Neither was measured (section 6, item 4): the OS writes the latch before
  it reads `$8000`, and a real machine's RAM holds whatever its chips power up
  with.

**How the tests treat it.** `BbcBusTests` pins the stretch from both phases,
every slow and fast range, the empty slot's value and the absent devices'
values, so a change to any choice is seen; none is a check against a machine.
Task 15's mutation pass found one change no test can see: in `Service()`, the
bus brings the CRTC up to date before it asks the system VIA for its next
event, and with that order reversed every test still passes, because the VIA
brings the CRTC up to date itself whenever it is asked anything (its
`SyncInputs`). The bus's order is a second guard on the same seam, so the
mutant behaves the same; removing the VIA's own guard is caught.

## The BBC Micro: the 6522 VIA's shift register, mode 010 only

**What.** `Via6522` models one of the shift register's eight modes: 010, shift
in under the system clock, because it is the only one the BBC's ROMs use (the
DFS, `via.md` section 1.8). In that mode IFR2 rises `ShiftMode2Cycles` (19)
cycles after the SR read or write that starts it. That number was measured by
counting pixels on the WDC datasheet's drawing of the CMOS part (Figure 2-7,
rendered at 260 dpi), and the sheet marks it `[guessing - verify]`. The eight bits are all taken from CB2 when the
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
  bus. `$FE88-$FE9F` repeat `$FE80-$FE87`, the block decoded by address bit 2
  alone, which the sheet infers from the board's decode (s1a); nothing uses it,
  and `BbcBusTests` pins it. The drive control input port (special register `$22`) reads like Read
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
  browser keeps its function keys). Those, and every other key, are on the
  page's on-screen keys, where a tap is a press and a release and SHIFT and CTRL
  latch for the next key rather than being held. CAPS LOCK is one press for each time the PC
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
- **A disc changed on the page is not seen by DFS at once,** as on a real BBC:
  DFS keeps the catalogue it last read until the drive stops, a few seconds
  after the last disc command, or until `*CAT`. The page says so, and does not
  make DFS read the new disc when it goes in (`DiscTests` shows the old
  catalogue until DFS reads it again).

## The NES: the bus, where the model stops

**What.** Task 3 of the NES plan, `NesBus` and `Nes`. The sources are
`docs/nes/facts/bus.md` and `timing.md`.

**RAM at power on is zero.** A real console's RAM holds an undefined pattern,
and some games read it. The model clears it to zeros so a run is repeatable.
`PowerOn` does it, and `Reset` does not touch RAM.

**The PAL fourth dot falls in the fifth cycle of every five.** The accumulator
starts at zero at power on and gives 3, 3, 3, 3, 4. A real console may start
in any of the five phases (`timing.md` section 3), and the pages do not say
which is common. The tests check the sum, 16 dots in 5 cycles, and this phase:
`NesBusTests.ThePalDotsRunThreeThreeThreeThreeFour` on a bus that was never
powered on, `PowerOnPutsThePalFourthDotInEveryFifthCycleCountedFromPowerOn`
through a power on, and `nestest`'s log on PAL. Before the mutation pass of 6
October 2026 only `nestest` saw the power-on phase, and nothing saw a second
power on that left the accumulator where it was.

**The reset button leaves the PAL phase where it was.** `Reset` puts the PPU
back at line 0 dot 0 and does not touch the accumulator, so after a reset the
fourth dot can fall in any of the five cycles counted from the PPU's new line 0
dot 0, depending on how many cycles ran before the button. The pages read do
not say what a console's reset does to the two dividers; this is the model's
choice, not a fact (`timing.md` open item 2). `NesBusTests`'
`AStatusReadOnTheDotTheFlagIsSetOrTheNextReadsItAndStopsTheNmi` uses it on
purpose, to move the cycles' starts against the PPU's dots on PAL.

**The order of the dots and the access inside a cycle** was measured in task 4
against the ten `ppu_vbl_nmi` singles: two dots before the access, the rest
after, and the CPU sees the interrupt lines as the chips held them when the
cycle began. It is one alignment of the several a real console can power up in
(`timing.md` section 4), the one the test ROMs are written for. On PAL the same
rule is applied with no test to check it. The IRQ line follows the NMI line's
rule, and task 8 checked it with `pal_apu_tests` 08.irq_timing, which fails a
cycle either side of it (`timing.md` section 3).

**Open bus is the last value that crossed the bus.** A read of `$4015` leaves
it alone (`NesBusTests.AReadOf4015LeavesTheOpenBusAsItWas`, added when the
mutation pass of 6 October 2026 found nothing held it). Nothing else drives the
bus in the model: there is no decay, and the only bus conflicts are the boards'
own AND on a register write (the section on the simple boards below).

**A mapper sees only `$4020` to `$FFFF`.** The sheet says a board sees every CPU
access except reads of `$4015`, so a board could put a register in the PPU or
sound range. The interface has no such board, so the bus does not pass those
addresses on.

**The controllers** are real (task 7): a read of a port gives the pad in bit
0, the open bus in bits 7 to 5 and zeros in bits 4 to 1. The DMC and its DMA
came in task 9; where they stop is the section on them below.

**OAM DMA came early, and its parity is a choice.** Task 5 built OAM DMA,
because the sprite test ROMs load OAM with it; task 7 owns it. A write to
`$4014` halts the CPU on its next read, and the copy takes 513 or 514 cycles
(`bus.md` section 5). Which CPU cycles are get cycles is random on a console at
power on; the model makes the even ones gets, so a write in an even cycle
costs 513 and one in an odd cycle 514. The sprite ROMs pass with either choice
(measured in task 5 by swapping it), so nothing pinned settles which.

**The reset button drops a copy that was waiting.** A write to `$4014` starts
OAM DMA on the CPU's next read. If the reset button is pressed between the two,
the model forgets the page, so no copy runs in the reset's first read, after the
PPU has been reset (`NesBus.Reset`). `bus.md` does not say what a console does
here; the reset stops the CPU, and the model takes it that the copy goes with
it. `OamDmaTests.TheResetButtonDropsACopyThatWasWaiting` holds it: the reset
takes its seven cycles and the next read one.

**A halt on a pad read clocks the pad once, on both chips.** The halt and the
alignment cycle repeat the CPU's read in consecutive cycles, and the pad sees
one clock for the run (`bus.md` 7). The model does this for the 2A07 too. The
sheet says the 2A07 lacks the extra reads of DMC DMA and does not say whether
OAM DMA differs there, so this is a guess for PAL (`bus.md` open item 2).

## The NES: the PPU's registers and timing, where the model stops

**What.** Task 4 of the NES plan, `Ppu`. The sources are
`docs/nes/facts/ppu.md` sections 1 to 5 and 12 and `timing.md` section 2. It
passes the ten `ppu_vbl_nmi` singles on NTSC. It draws nothing yet (task 5).

**The power-on alignment is a choice.** The PPU starts at line 0 dot 0 in the
same instant as the CPU's first reset cycle, which is what `nestest.log` needs.
A real console powers up with the CPU and PPU in one of several alignments, and
the `ppu_vbl_nmi` readme says some of them fail its tests (`timing.md` section
4). The model has one, the one the tests are written for. Its first VBlank is
set in cycle 27395 from power on on NTSC (25683 on PAL), which
`NesBusTests.TheFirstVblankFlagIsSetInTheCycleThatRunsLine241Dot1` holds; the
wiki says "around 27384", and nothing pinned settles which (`timing.md` open
item 4).

**On PAL the flags clear at dot 1 of line 311.** The pages give the clear at
dot 1 of the pre-render line on NTSC, and on PAL only imply it, from the 70
lines of VBlank; the PPU frame timing page says the PAL clear time awaits
confirmation (`timing.md` open item 1). The model clears VBlank, sprite 0 hit
and overflow at dot 1 of line 311.
`PpuTimingTests.TheVblankFlagIsSetAtLine241Dot1AndClearedAtDot1OfThePreRenderLine`
checks that the model does so on both; no pinned ROM times it on PAL.

**Power on is zeros.** VBlank is often set at power on and OAM, the palette and
the nametables are unspecified (`ppu.md` section 12). The model clears all of
them so a run is repeatable.

**The I/O latch decays at one fixed time.** Each bit reads 0 once it has gone
600 ms without being driven, the time the fork's `ppu_open_bus/readme.txt`
measured on a console. The wiki says at least one bit goes after 3 to 30 ms,
faster when the PPU is warm, and the readme says some decay sooner, depending
on the console and the temperature. The model gives every bit the same time on
every console. Which accesses drive which bits follows `ppu.md` section 1, and
`ppu_open_bus` passes (task 12).

**Writes are not ignored after power on or reset.** The chip ignores writes to
`$2000`, `$2001`, `$2005` and `$2006` until the end of the first VBlank
(`ppu.md` section 1). The model takes them at once. Games wait for VBlank first,
so this shows only for a program that does not.

**Two small delays are not modelled.** The second `$2006` write copies `t` to
`v` at once, where the chip takes 1 to 1.5 dots, and a `$2001` write switches
rendering at once, where the chip takes 3 to 4 dots (`ppu.md` sections 1 and
2). The odd-frame dot is sampled at dot 338, measured against `ppu_vbl_nmi`
test 10. That is the model's cutoff in its own alignment of CPU and PPU. Against
the wiki's dot 339, where the skip happens, it is an effective delay of one dot
for the `$2001` write, not the 3 to 4 the sheet gives, and why the two differ is
open (`timing.md` section 2).

**Left for the drawing PPU (task 5), and done there.** A `$2004` read during
rendering on a visible line returns what evaluation and the sprite fetches are
reading, OAMADDR is cleared on dots 257 to 320, and the pipeline moves `v`. The
next section says where the drawing stops.

## The NES: the PPU's picture, where the model stops

**What.** Task 5 of the NES plan: the background pipeline, the sprites, sprite 0
hit, the overflow flag, `PpuPalette` and `FrameBuffer`. The sources are
`docs/nes/facts/ppu.md` sections 2 and 6 to 11 and the wiki's NTSC video page.
`BlarggTests` runs every ROM of `sprite_hit_tests_2005.10.05` and of
`sprite_overflow_tests` on NTSC, and none is a known failure.

**The colours are one decode, computed.** The PPU makes a composite signal and a
television decodes it. `PpuPalette` makes the signal from the wiki's measured
voltages and decodes each colour alone, from one whole colour cycle, with black
at `$1D` (no 7.5 IRE setup) and white at `$20` (`ppu.md` section 11). A real
picture differs: colours bleed into their neighbours and crawl from line to line
and frame to frame, the hues of the brighter rows turn by the differential
phase distortion, and each television decodes and filters in its own way. None
of that is modelled. The wiki's Pally tables were not used, because their
licence is not stated.

**PAL uses the NTSC colours.** The 2C07's own decode is about 15 degrees of hue
away (`ppu.md` section 10). The model swaps the 2C07's red and green emphasis
bits, and nothing else about its colour. The colours were never compared with a
real PAL television, or a capture of one: `PpuPaletteTests` checks the decode
against the rules of the NTSC video page, and
`OnPalTheRedAndGreenEmphasisBitsAreSwapped` the one PAL rule the model has.

**The 2C07's border is not drawn.** The 2C07 blacks out columns 0, 1, 254 and
255 and line 0 of the picture (`ppu.md` section 10). The model draws them as the
2C02 does.

**The 2C07's OAM refresh is not modelled.** It refreshes OAM itself on lines 265
to 310, so OAM can be written only in the first 24 lines of its VBlank, and its
sprite evaluation cannot be fully turned off (`ppu.md` section 4). The model
lets OAM be written all through VBlank on both.

**The column a dot decides is bounded, not pinned.** Column `X` is decided on
dot `X + 2`, which is what the wiki's "sprite 0 hit acts as if the image starts
at cycle 2" says. The sprite 0 timing ROMs pass with the hit on any dot from
`X + 1` to `X + 4`, so they do not settle it. The first pixel leaving the chip
during dot 4, the analogue delay after that, is not modelled.

**Greyscale follows one page of two.** PPU registers says greyscale ANDs the
colour with `$30`; NTSC video says colours `$x1` to `$xD` become `$x0`, and says
nothing of `$xE` and `$xF`, which the AND turns grey. The model ANDs. No pinned
test reads the difference.

**Evaluation starts at sprite 0.** On the chip, an OAMADDR that is not 0 at dot
65 makes evaluation start elsewhere and treat another sprite as sprite 0
(`ppu.md` section 8). The model always starts at sprite 0. The `$2004` reads
during rendering show the bytes evaluation and the fetches read, one a dot,
which is close to what the sheet says but not checked against a ROM:
`oam_read` and `oam_stress` are task 12's.

**OAM does not decay**, and the 2C02G's OAM corruption on some OAMADDR writes
is not modelled (`ppu.md` section 4).

## The NES: the sound unit, where the model stops

**What.** Task 8 of the NES plan, `Apu` and its channels in `ApuChannels.cs`:
the two pulses, the triangle, the noise and the frame counter. The source is
`docs/nes/facts/apu.md`. Every pinned sound ROM that needs no DMC passes:
`apu_test` singles 1 to 6 on NTSC and all ten `pal_apu_tests` on PAL.

**Which parity takes the 3-cycle `$4017` delay is a choice.** The page says 3
cycles "during an APU cycle" and 4 "between". The model gives 3 to a write on an
odd cycle, which the bus counts as a put, so the reset lands on a get and the
steps on the puts the sheet's table names. The jitter ROMs on both regions fail
with a fixed delay and pass with the rule either way round, so they do not say
which (`apu.md` open item 4).

**A flag set in the cycle of a `$4015` read is cleared by it.** The APU page says
such a read returns 1 and does not clear the flag. With that rule on the third
of the frame counter's three sets, `apu_test` 6-irq_flag_timing and
`pal_apu_tests` 07.irq_flag_timing fail their "last set too late" check. The
model sets the flag on three cycles in a row, as the sheet's table has it, and
lets every read clear it; both ROMs pass.

**A length write in the cycle before a half frame meets the clock.** The halt
then takes effect after the clock, and a reload is dropped if the counter was
not 0. The rule is from `pal_apu_tests`' readme, tests 10 and 11, which pass on
PAL; the model applies it on NTSC too. The fork's `blargg_apu_2005.07.30` has
the NTSC tests of the same name, and task 12 runs them: both pass with the rule,
and both fail with it taken out (`apu.md` open item 5).

**A pulse with a period under 8 is silent on PAL too.** The APU Pulse page
leaves PAL's behaviour as an open question, and no pinned ROM checks it
(`apu.md` open item 3).

**The triangle's periods 0 and 1 are not halted.** They give the ultrasonic wave
the sheet describes, a step every CPU cycle or every second one. Some emulators
halt them to avoid the noise this makes in a sampled output. Task 9's resampler
does not need it: `ResamplerTests` holds an ultrasonic triangle more than 65 dB
under a full one, on both regions. On 6 October 2026 (03:32 UTC, load 2.4)
`dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --filter
"FullyQualifiedName~ResamplerTests" --logger "console;verbosity=detailed"`
printed -72.4 dB at worst, the NTSC triangle at period 0.

**The triangle starts at step 0 at power on.** The sheet gives step 0 after a
reset and an unknown phase at power on.

**The reset keeps the IRQ inhibit bit.** The fork's `apu_reset` readme says the
mode is written again at reset "but IRQ inhibit flag is sometimes cleared". The
model writes the last mode and keeps the inhibit bit. All six `apu_reset` ROMs
pass (task 12); `4017_written` checks the mode after each reset and not the
inhibit bit, so it does not choose between "kept" and "sometimes cleared".

**A pulse gives table entry 0 from a `$4003` or `$4007` write until its first
advance.** The APU Pulse page gives the order the sequencer reads its table in,
and not whether the first output is taken before or after the first advance
from entry 0. The model outputs entry 0 until then (`apu.md` section 2). No
pinned ROM is known to depend on it.

**The output is the sheet's mixer formulas** with every channel, the DMC
included, through tables built from them at start-up (task 9). The triangle,
noise and DMC table is single precision; `MixerTests` holds every entry of both
tables within 1e-6 of the formula.

## The NES: the DMC, its DMA and the sound out, where the model stops

**What.** Task 9 of the NES plan: the DMC in `ApuChannels.cs`, its DMA in
`NesBus.RunDma`, the mixer's tables in `ApuMixer.cs`, and `SampleBuffer`. The
sources are `docs/nes/facts/apu.md` sections 8 and 11 and `bus.md` section 6,
with the DMA page itself (revision 23450). Every pinned DMC ROM passes but one:
`apu_test` 7-dmc_basics and 8-dmc_rates, both `sprdma_and_dmc_dma` ROMs, and
four of `dmc_dma_during_read4`'s five, on NTSC.

**`dmc_dma_during_read4/double_2007_read` fails, and it is the PPU's.** It
reads `$2007` twice in adjacent cycles (`LDA $20F7,X` with X = `$10`, whose
dummy read is `$2007` and whose real one `$2107`), with no DMC in it. Its source
lists four outputs a console gives, by the CPU and PPU alignment, all of which
treat the second read oddly ("sometimes ignores extra read, and puts odd things
into buffer"). The model's PPU makes two whole reads and prints CRC `D84F6815`.
`TestRomTable.DmcDmaKnownFailures` holds that output, and `BlarggTests` and
`NesAcceptanceTests` run it, so a fix shows.
Task 12 looked again. The model prints `33 44 55 66 77` for the double read,
where the source's four console outputs begin `22 44`, `22 33`, `02 44` and
`32 44`: in each, the second read does not return the byte the first read put
in the buffer, so on the chip the buffer is filled some dots after the read and
not at once. The wiki's PPU registers page says only that the buffer is updated
"after the previous contents have been returned to the CPU", with no number of
dots, and the four outputs depend on the alignment. A refill delay chosen to
make one of them come out would be a guess at the chip, so it stays as it is.

**The DMA bugs are not modelled.** The DMA page's aborted one-cycle DMA (a
sample stopped in the APU cycle before a reload would be scheduled) and the
late 2A03G and 2A03H's extra fetch do not happen. No pinned ROM tests them.

**When a reload halts after the output unit empties the buffer is a choice.**
The page says a reload halts on a put. The model's output unit clocks on the
puts, and a reload may halt from the next put, two cycles later. Halting at the
put after that passes every ROM too: they synchronise themselves to the DMC, so
they test the parity and the cost, not the delay. The mutation pass of 6
October 2026 made the halt two cycles later and no test failed, so
`DmcDmaTests.AReloadHaltsOnTheNextPutAfterTheOutputClockThatEmptiedTheBuffer`
now pins the model's choice; it pins the choice, not the chip.

**The 2A07's DMC fetch reads its own address on its idle cycles.** The page
says the 2A07 has no extra reads by a mechanism "not yet understood", and
suspects the DMA's address is on the bus. On PAL the model reads the sample
address on the halt, dummy and alignment cycles of a DMC fetch, so no register
is read again; OAM DMA alone still repeats the halted read there, as task 7 left
it. A guess: no pinned ROM tests the 2A07's DMA.

**The 2A03's register select during a fetch is not modelled.** On the 2A03 a
DMC fetch while the CPU is halted on a read of `$4000-$401F` can select a
register by the sample address's low five bits (`bus.md` open item 3). The model
reads only the sample address.

**The sound is resampled, and the console's filters follow.** The level is
averaged over blocks of 8 CPU cycles, and each change of a block's mean is added
as a band-limited step (a Kaiser-windowed sinc, 16 samples each side, placed to
1/4096 of a sample); then high-passes at 90 Hz and 440 Hz and a low-pass at 14
kHz, first order each. The 8-cycle mean loses a fraction of a decibel at 20
kHz, which follows from its length (an 8-point mean's gain is sin 8x / (8 sin
x), with x = pi f over the CPU clock). `ResamplerTests` holds the worst alias
under the Nyquist, measured on pulses, more than 65 dB under the note; the run
of 6 October 2026 quoted for the triangle above printed -89.6 dB at worst. The
order and the form of the filters are not on the page.

**The sample buffer drops the oldest samples** when its reader falls behind
(the plan's Review Focus 3). The machine's holds a quarter of a second.

## The NES: the simple boards and MMC1, where the model stops

**What.** Task 10 of the NES plan: `Mmc1`, `Uxrom`, `Cnrom` and `Axrom` in
`src/Dbhq.Machines.Nes/Mappers/`, on a shared `Board`. The source is
`docs/nes/facts/mappers.md` sections 3 to 5. Both combined MMC1 test ROMs pass:
`ppu_vbl_nmi/ppu_vbl_nmi.nes` and `apu_test/apu_test.nes`.

**The reset button leaves the boards alone.** None of these chips has a reset
line, so `Reset(false)` changes nothing in them, MMC1's shift register and
control register included; only a power-on (`Reset(true)`) puts them back. The
sheet gives only MMC1's power-on state. That the button does nothing is from the
chips' having no reset pin, not from a page that says so (the nesdev MMC1 page
does not mention the console's reset). A program that is reset in PRG mode 2 or 0
therefore starts in the wrong place, as it would on a console, and a ROM must
write `$80` itself, as the pinned test ROMs' shells do.

**Bus conflicts follow the NES 2.0 submapper.** The sheet says to model none for
UxROM and AxROM and the AND for CNROM unless submapper 1. The model also takes
submapper 2 of mapper 2 and of mapper 7 as "AND-type bus conflicts" (the
nesdev UxROM and AxROM pages list it so), because a file that says it has them
should not be run without. An iNES file, and submappers 0 and 1, have none.
CNROM keeps the sheet's rule, which the sheet marks as a guess; nothing pinned
writes a value that differs from the ROM byte. The road not taken for UxROM and
AxROM is the sheet read strictly, no conflicts for any submapper. CNROM's risk
runs the other way: the AND is applied to iNES files and submapper 0, so a CNROM
game made for a board without conflicts would switch to the wrong bank
(`mappers.md` open items 2 and 5). The two CNROM test ROMs of task 12 pass with
the AND or without it (tried once, with the AND taken out), so they do not
settle this.

**Only the plain MMC1 boards.** SOROM, SUROM, SXROM and SZROM, which bank PRG
RAM or more PRG through the CHR registers, are not modelled, so a 512 KB MMC1
cartridge reads only its first 256 KB, as the PRG register's four bits reach.
PRG RAM is whatever the header says, repeated through `$6000` to `$7FFF`.
Bit 4 of the PRG register switches it off (the MMC1B's behaviour; the MMC1A has
no such bit).

**A file smaller than the registers can name wraps.** A bank number is taken
modulo the banks in the file, and a size that is not a whole bank (a NES 2.0
exponent size) is padded to one by repeating its bytes, so no register value
reads outside the file. A real board's chips would show open bus or mirrors;
which is board by board and no pinned file depends on it.

## The NES: MMC3, where the model stops

**What.** Task 11 of the NES plan: `Mmc3` in `src/Dbhq.Machines.Nes/Mappers/`,
mapper 4, from `docs/nes/facts/mappers.md` section 6. Of the pinned MMC3 test
ROMs, `mmc3_test_2`'s singles 1 to 5 and `mmc3_irq_tests` 1 to 4 and 6 pass.

**One revision of the chip.** MMC3 chips differ at latch 0. The model is the
Sharp ("new") chip, which raises the IRQ whenever a clock leaves the counter at
0; the sheet chooses it because games rely on it. The other chip, Crystalis's in
the fork's readmes, raises it only when the counter changes to 0 or is reloaded
by request. So the two ROMs that test that chip fail, and are kept in
`TestRomTable` as known failures with what they print:
`mmc3_test_2/rom_singles/6-MMC3_alt` (status 2, "IRQ shouldn't be set when
reloading to 0 due to counter naturally reaching 0 previously"; both its
sub-tests are the other chip's rule, and it stops at the first, so its test 3
never runs) and `mmc3_irq_tests/5.MMC3_rev_A` (failed test 3; its test 2, which
both chips share, passes). The readme of the second says at
most one of its last two ROMs can pass on any emulator.

**What is not modelled.** The "pathological" behaviour the readmes describe
(a `$C001` write, a clock and another `$C001` write make the next clock OR the
counter with `$80` or freeze it); the readme advises against it and no game it
tried needs it. MMC6 (StarTropics), which shares mapper 4: it runs as an MMC3,
whose `$A001` means something else, so its battery RAM may read as switched off.
"The pre-render line clocks twice every other frame" with the background at
`$1000`: the model clocks once on every rendering line there.

**The PPU's address bus is told in part.** The board is told of every pattern
fetch, each sprite slot's first nametable fetch, and, outside rendering only,
`v` when a `$2006` write or a `$2007` access moves it (during rendering the bus
carries the fetches, so those accesses are not told). The background's nametable and attribute fetches are
not told (their A12 is 0, and told they would give a second clock a line with
the background at `$1000`, where the sheet says one). The bus's `v` on the
post-render line, or when rendering is switched off mid-frame, is not told
until the program moves it, so a `v` with bit 12 set left by rendering is no
rise until then. Both are open items in the sheet.

**Power on.** The sheet leaves R6, R7 and the bank select unspecified. The model
starts R0 to R7 at 0, 2, 4, 5, 6, 7, 0, 1, so the first 32 KB of PRG and 8 KB of
CHR read in order, and PRG RAM on and writable. The test ROMs need both. A real
chip's power-on state may differ; a program that relies on it would also fail
on some consoles. The reset button changes nothing in the board, as for the
other boards.

**A file smaller than the registers can name wraps.** Bank numbers are taken
modulo the 8 KB PRG and 1 KB CHR banks in the file; the fixed second-last bank
of a one-bank program is that bank.

## The NES: the community test ROMs, where the model stops

**What.** Task 12 of the NES plan ran every ROM the plan lists that reports a
result a test can read, on the regions each ROM's readme or source gives. The
table is in the journal entry of 5 October 2026, task 12.

**`instr_test-v5` 03-immediate and `all_instrs` fail on opcode `$AB`.** `LXA`
(the ROM's `ATX #n`) sets A and X to (A OR a constant) AND the operand, and the
constant differs between chips. The core takes `$EE` from Harte's `nes6502`
data, which the core's own tests pin (the "Unstable NMOS opcodes" entry above).
The ROM's checksum was made on a console; with `$FF` in the core, tried once,
03-immediate passes, and with `$00` it fails. So the console Blargg used had
`$FF`. The two references disagree, and the core keeps Harte's, because a
change would take an exception into the core's reference tests.
`TestRomTable.RamReportingKnownFailures` holds both ROMs on both regions and
their output (status 1, `AB ATX #n`); every other instruction in the
suite passes.

**Decided, 5 October 2026:** LXA (`$AB`) on the Ricoh2A03 stays as the core has
it (`$EE`, from Harte's `nes6502` data), because changing it would override a
pinned core reference and would break that data unless `$AB` were excluded from
the `nes6502` set. The two `instr_test-v5` ROMs (`03-immediate` and
`all_instrs`, both regions) stay as known failures, with this cause. The road
not taken is to use `$FF` for the Ricoh2A03, the console-calibrated value the ROM
passes with, and exclude `$AB` from the `nes6502` Harte set; that is left for the
project owner to choose.


**Not run, and why.** Two folders of the fork are not pinned and not run, as
the journal entry of 5 October 2026, task 12, records. `nmi_sync` (`demo_ntsc`
and `demo_pal`) draws a line with timed `$2001` writes, and its readme says to
look at the picture ("the left pixel of the middle line will be darker");
nothing in RAM or on the nametable says pass or fail, and no frame check was
written for it later. `dmc_tests` has four ROMs with no readme and no source;
run, they leave nothing on the screen, in `$6000` or in zero page, and their
result is a sound to listen to. The fork's own `status.txt` marks all four "Not
sure yet".

**Every known failure, in one place.** `TestRomTable` holds each with what it
prints now, and `BlarggTests` and `NesAcceptanceTests` check that it still
fails as written down, so a fix shows: `instr_test-v5` `03-immediate` and
`all_instrs` on NTSC and on PAL (the `$AB` entry above);
`dmc_dma_during_read4/double_2007_read` (the DMC section); and
`mmc3_test_2/rom_singles/6-MMC3_alt` and `mmc3_irq_tests/5.MMC3_rev_A` (the
MMC3 section). Every other pinned ROM passes.

## The NES: the cartridge file and the page, where the model stops

**What.** `Cartridge`, `NesLoader` and the page's panel (`site/public/nes.js`).
The source is `docs/nes/facts/cartridge.md`.

**The iNES TV system bit is not read.** Byte 9 bit 0 of an iNES 1 header, and
the unofficial byte 10, name a region, but the sheet's source says almost no
file sets them, so an iNES file names no region and runs as NTSC unless the
visitor chooses PAL; the page says which it chose and why. Only NES 2.0 byte 12
is read: 0 NTSC, 1 PAL, 2 (either) NTSC.
`CartridgeTests.AnInesFileNamesNoRegionWhateverItsTvSystemBitsSay` and
`NesBusTests.TheMachineTakesTheHeadersRegionUnlessTheCallerNamesOne` hold it.

**A trainer is skipped, not loaded.** A file with flags 6 bit 2 set carries
512 bytes that a copier once loaded at `$7000-$71FF`. They are not on unmodified
dumps of real cartridges (`cartridge.md` section 1), and the sheet left skipping
or refusing them to the model (section 4). The model skips them, so the program starts 512
bytes later and nothing is put at `$7000`. A hacked dump that needs its trainer
there to run will not run.
`CartridgeTests.ATrainerIsSkippedSoPrgStarts512BytesLater` and
`ATrainerThatRunsPastTheEndOfTheFileIsRefused` hold it.

**The Dendy is refused.** A NES 2.0 file whose byte 12 says Dendy (3) is
refused with a sentence that names it
(`CartridgeTests.ANes20FileForTheDendyIsRefusedByName`), and the page offers
only NTSC and PAL. The Dendy runs the PAL frame at three dots a cycle with its
own VBlank timing; it is left out and has its issue
([#61](https://github.com/dbhq-uk/6502/issues/61)).

**The file and its RAM have caps.** A file over `Cartridge.MaxFileSize` (4 MB)
is refused before its header is read, and the page refuses it before reading it
at all, with the host's own limit (`NesHost.MaxRomBytes`). A header may ask for
at most `Cartridge.MaxRamSize` (64 KB) of PRG RAM and of CHR RAM: a NES 2.0
header that asks for more is refused, and an iNES byte 8 over it is clamped.
No board this machine models needs more, and a header must not make the page
allocate megabytes for a small file. `CartridgeTests`
(`AFileOverTheSizeLimitIsRefusedBeforeItsHeaderIsRead`,
`ANes20HeaderAskingForMegabytesOfRamIsRefusedWithoutAllocatingIt`,
`AnInesByte8Of255IsClampedToTheCapAndTheFileStillLoads`) and the page's
`nes-panel.test.mjs` hold them.

**The picture is the PPU's 256 by 240, shown in the region's pixel shape.** The
page shows all 240 lines, where a television hides some at the top and bottom
(overscan), and shows each pixel 8:7 on NTSC, so the 256 columns are as wide as
256 x 8/7 square pixels, and about 1.386:1 on PAL (`ppu.md` section 10, from
the Overscan page). The bleeding and crawl of a composite picture are not
modelled (the PPU section above; issue
[#69](https://github.com/dbhq-uk/6502/issues/69)). `nes-panel.test.mjs` ("the
picture is 256 by 240 pixels shown in the region's pixel shape") holds the
shape.

## The NES: its two 3D models, where they stop

**What.** The outside model (`site/src/models/nes-famicom-case.js`) and the
inside model (`nes-famicom-board.js`) on the NES's page, each drawn for the
NTSC NES-001 and the PAL NESE-001. They are measured by the offline tools in
`tools/nes-model/` from photographs, a design patent and bare board scans,
none of them Nintendo's own drawings. Every figure below is read from the data
file named beside it, measured on the day the journal
(`docs/journal/2026-10-05-the-nes-models.md`) gives, and the tests named hold
it. Each limit a visitor would notice is also said in the note under its model,
under "What it does not show, or shows less well".

**The case's size is not Nintendo's.** It is the published 254 by 203.2 mm and
88.9 mm high (`tools/nes-model/data/case.json`, `footprint.widthMm`,
`footprint.depthMm`, `heightMm`, with `heightFrom` "published"), from the NES
Fandom wiki, Thingiverse 243385 and dimensions.com, which disagree with each
other a little, so it is "good to about 3 per cent" (`footprint.widthSource`).
The height is the published one, not read from the photographs.

**The case's checks were revised twice, after the patent's figures were
seen.** As first written, depth to width and height to width were read from
two corner photographs and the depth stopped, at -8.77 per cent on O2-BR
(`spike.json`, `case.depthToWidthErrPctEach.br`); review found that measured
the camera, not the case. The first revision judged the design patent's
drawings against 203.2 / 254 within 1.5 per cent, stop over 3, and the top view
stopped at -3.46 per cent (`case.patent.firstRevisionAgainst254Mm`). The
sources disagree with each other by more than those limits can resolve, so the
second revision judges the patent's views against the published ratios'
midpoints, 0.797 and 0.349, within 5 per cent and stop over 8. On that, depth to
width reads -3.08, -2.64 and -0.68 per cent on the top, the bottom and the side
over the front, and height to width +1.12 and +1.37 per cent
(`case.patent.depthToWidthErrPctEach`, `heightToWidthErrPctEach`). What the
checks catch now is a gross error of scale, not an error of 3 per cent; every
earlier stop stays recorded in `spike.json`. The PAL front lies between pass
and stop: its label band is -3.49 per cent against the NTSC front's, against a
2 per cent pass and a 4 per cent stop (`palFront.ratioErrPct`).

**The rear connectors' check failed, and is accepted as measured.** Their
places worked out from the board plus the board's place in the case missed
where a photograph (O2-BR) shows them by 5.16, 3.5 and 0.85 mm, one of three
within the 2 mm limit (`case.json`, `rearCheck.within`, `rearCheck.worstMm`,
each connector's `checkMm`). The model keeps the photograph's places, which
agree with the patent's rear view within 1.3 mm
(`rearCheck.uncertaintyMm.placesUsed`). A recheck needs the RF modulator's face
measured on its own first, with its limit fixed before it runs; it is an open
item in the plan's Deferred list.

**The copper is traced to look at, and its connections are not verified.**
The check that would have shown them, that the ten chips' ground pins join in
one net and their +5V pins in another, failed: 1 of 10 ground pins fell in the
largest ground net, 2 of 10 +5V pins in the largest +5V net, and a ground and a
+5V pin came out in one net (`copper.json`, `nets`, and `verdicts.nets`
"fail"). The track map is kept to look at. A re-trace, judged by a new check
fixed before it runs, is an open item in the plan's Deferred list, for Dan to
decide.

**Each console's parts come from a photograph of another board.** The NTSC
parts were read off an NES-CPU-07 (I4, I5) and placed on the scanned
NES-CPU-10's footprints, each within 0.432 mm at the worst against a 2 mm limit
(`parts.json`, `sitsOn`). The PAL parts were read off an NES-CPU-11 (I3) and
drawn on the same CPU-10 layout. No bare PAL board was scanned, so the PAL
board's copper is the NTSC board's, on task 0's evidence that the two layouts
match: I3's part centres against I1's, held out one at a time, median 0.148 mm
and worst 0.386 mm (`spike.json`, `palLayout.heldOutMm`).

**What the chips do is read, not traced.** Which controller port each buffer
serves, U7 port 1 and U8 port 2, is by the board's print, checked against the
nesdev wiki and the KiCad redrawing's nets in task 6, not traced on the scan
(`ic-table.json`, `port.inferred` true on both). U9's jobs, inverting the PPU's
A13 and the reset line and clocking the lockout chip, are read from the
redrawing's nets.

**Heights are typical.** Every part's height is a typical one for its package,
none measured (`parts.json`, `model.heights`, each `measured` false).

**The PAL modulator and crystal.** The PAL modulator's can is drawn about 4 mm
to the right of the NTSC modulator's place (`parts.json`, `model.others`, x
193.674 against 189.656 mm), as I3 shows it; I3 was taken with a 20 mm lens, and
the photograph cannot say how much of the shift is perspective. The PAL crystal
is drawn as seen on I3, partly hidden by the expansion socket, so it is shorter
across the board than the NTSC one (8.899 against 11.512 mm, the same entries).

**The board's place in the case and the case's profile.** The board's place is
good to about 2 mm (`case.json`, `boardInCase.uncertaintyMm`) and was measured
in a PAL case (O9), so it assumes the PAL and NTSC cases share one moulding, an
inference (`boardInCase.from`). The bottom shell's ends lean in below a break;
the four ends read on two photographs run from 12.86 to 17.71 mm at the base, so
the model's inset is an average good to about 2.5 mm (`profileCheck.endsMm`,
`profileCheck.endsWhat`); the held-out figure, 0.71 mm, shows the averaging
repeats, not that it is the true inset.

**The machine's state, where the console's is not known or not emulated.**
Whether POWER latches in on a real console is not known: no photograph shows it
pressed (`case.json`, `powerLatch.seen` false). The model shows it in while the
machine runs, as the design chose. The lockout chip is not emulated, so the
power light never blinks as a real console's does when it refuses a cartridge,
and the inside model never marks U10. The buttons' travel, 3 mm, is a typical
one (`buttons[0].travelMm`, `travelFrom` "typical").

**What is simplified.** The case's corners are drawn square, where the
console's are rounded. The rear's window is drawn flat on the rear face; on the
console it is set in by about 4.4 mm (`case.json`,
`rearWindow.setBackReadMm`, 4.39). The underside's ribs and panels are drawn as
lines. The cartridge door does not open. The video and audio jacks on the side
and the PAL console's rear are not checked against the board. The PAL
console's words were read from photographs taken with a 20 mm lens
(`sources.json`, `focalLengthMm` on O4 and O10), and its rear words, printed on
three lines, are drawn on one. The resistors, capacitors and connectors are
plain shapes, and the track map has 10 pixels to the millimetre
(`copper.json`, `mapPxPerMm`). The site's shared stage lights every model with
its green-white, so the case's greys come out a little green.

**How the tests treat it.** `site/tests/nes-models.test.mjs` pins each figure
above to its data file: the rear check's failure and its three misses, the
nets check's FAIL, the case's published size and its checks' verdicts, the
parts' places and identities (against `docs/nes/facts/models.md`'s table since
task 10), the profile's spread, and every sentence of both notes against the
results files that `tools/nes-model/results.py` writes. A re-run of the tools
that moves any of them fails until the words move too.
