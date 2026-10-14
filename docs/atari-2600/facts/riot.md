# The 6532 RIOT on the Atari 2600: decode, RAM, ports, timer, PA7 edge, power on

Written 14 October 2026 for the Atari 2600 plan, task 1. Tags and sources are in [`README.md`](README.md). Used by tasks 3 (`Riot6532`), 4 (the RIOT's part of the bus decode, and the suite's `riot/` self-tests) and 9 (the joysticks and the console switches).

This sheet uses three tags beyond the README's: `[from 6532 1977]` for the preliminary MCS6532 data sheet of February 1977, `[from 6530]` for the MCS6530 data sheet, and `[from Rriot6530.cs]` for this repository's KIM-1 chip, read only to say what the repository's 6530 does. `[from 6532]` is the Commodore Semiconductor Group sheet "6532 (Memory, I/O, Timer Array)" dated 2/81. `[from the schematic]` is the CX2600 and 2600A motherboard schematics in Atari's Field Service Manual FD100133 Rev. 02, in a copy redrawn in 2024 (section 13). The Stella guide's PIA chapter numbers its sections from 1.0 again, so they are tagged `[from SPG PIA 2.3]` and so on.

## 1. The 6530 against the 6532

The spec's table ("The RIOT, against the KIM-1's 6530"), row by row.

| Row | Spec says | Verdict | What the sources say |
|---|---|---|---|
| Memory | 6530: 1 KB mask ROM, 64 bytes of RAM. 6532: no ROM, 128 bytes of RAM | Confirmed | 6530: "1024 x 8 ROM", "64 x 8 static RAM" [from 6530]. 6532: a 128 x 8 static RAM and no ROM [from 6532] |
| Selecting RAM or I/O | 6530: fixed by the mask-programmed chip selects. 6532: the RS pin, which the 2600 wires to A9 | 6530 corrected, 6532 confirmed | The 6530 chooses ROM, RAM and I/O from RS0 and address lines A6 to A9 by a mask-programmed decode; CS1 and CS2 are optional, a mask option on pins PB5 and PB6 [from 6530]. The 6532 selects the chip with CS1 high and CS2 low, then RAM with RS low and I/O and timer with RS high [from 6532]. On the 2600, RS is A9, CS1 is A7 and CS2 is A12 [from the schematic] |
| Interrupt output | 6530: shared with PB7. 6532: a pin of its own, not connected on the 2600 | Confirmed | 6530: A3 enables the timer's interrupt onto PB7, which goes low [from 6530; Rriot6530.cs]. 6532: its own IRQ pin, active low, which needs an external pull-up [from 6532]. On the 2600, IRQ (pin 25) is marked N/C [from the schematic], and the 6507 has no IRQ pin anyway |
| PA7 edge detect | 6530: none. 6532: a flag set on a rising or falling edge of PA7, its polarity and enable chosen by a write | Confirmed, with detail | The 6530 sheet describes no edge detector; its PB5 to PB7 have other uses, PA7 none [from 6530]. On the 6532 the polarity and the enable come from the write's **address** (A0 and A1); the data written is discarded. The flag is set whether or not the enable is on (section 8) [from 6532] |
| Reading the flags | 6530: timer flag only. 6532: a flag register, bit 7 the timer, bit 6 the PA7 edge; reading it clears the edge flag | Confirmed, with detail | 6530: the flag reads on DB7 and DB0 to DB6 read 0 [from 6530]. 6532: bit 7 timer, bit 6 PA7, D0 to D5 read 0; reading the register clears the PA7 flag only; the timer flag is cleared by reading or writing the timer [from 6532; the suite: roms/riot/pa7-edge.asm, roms/riot/timer-divisors.asm] |
| Timer | Divide by 1, 8, 64 or 1024; after zero it counts down once a cycle; the read on the cycle it passes zero to be checked against `riot/timer-underflow-read` | Confirmed, and the open point settled | Both chips: 1T, 8T, 64T, 1024T, then once a cycle after the flag is set [from 6530; 6532]. A read of the timer on the cycle the flag is set returns $FF and does not clear the flag [from 6532; the suite: roms/riot/timer-underflow-read.asm]. The count drops on the first cycle after the write. The 1981 sheet's own figure is one cycle off its text; the 1977 sheet and the suite settle it (section 6) |

The line under the table, "The RIOT is clocked once a CPU cycle", is confirmed: one PIA cycle per machine cycle [from SPG PIA 2.0], and the chip's clock input is the 6507's φ2 [from the schematic].

## 2. The chip's pins on the 2600

| 6532 pin | Wired to | Source |
|---|---|---|
| A0 to A6 (pins 2 to 7, 40) | AB0 to AB6 | [from the schematic] |
| CS1 (38) | AB7 | [from the schematic] |
| CS2, active low (37; the redraw labels it CS0) | AB12 | [from the schematic] |
| RS (36) | AB9 | [from the schematic] |
| R/W (35), φ2 (39) | the 6507's R/W and φ2 | [from the schematic] |
| IRQ (25) | nothing (N/C) | [from the schematic] |
| RES (34) | its own RC: 24 kΩ to +5 V and 0.1 µF to ground (R217 and C220 on the CX2600, R227 and C238 on the 2600A). No other line reaches it | [from the schematic] |
| PA0 to PA7 (8 to 15) | the joystick ports: PA4 to PA7 to J202 pins 1 to 4, PA0 to PA3 to J203 pins 1 to 4, each with a small capacitor to ground | [from the schematic] |
| PB0, PB1, PB3, PB6, PB7 | Reset, Select, Colour/B.W., Left difficulty, Right difficulty | [from the schematic] |
| PB2, PB4, PB5 (22, 19, 18) | nothing (N/C) | [from the schematic] |

So the program cannot reset the RIOT: the console's Reset button is only PB0, and the chip's RES pin sees only its power-on RC [from the schematic; 2k6specs: "the Reset Button does not reset any hardware"].

## 3. The register decode by address bit

The 2600 reaches the RIOT where A12 = 0 and A7 = 1 (CS1 and CS2). A9 (RS) chooses RAM (0) or I/O and timer (1) [from the schematic; 6532; 2k6specs; the suite: roms/riot/ram-mirrors.asm]. Inside the I/O half the chip decodes A0 to A4 only; A5 and A6 reach the chip but its table marks no use for them in I/O [from 6532, Table 1]. A8, A10 and A11 reach nothing [from the schematic].

**Reads**, I/O half (A12 = 0, A9 = 1, A7 = 1):

| A4 | A3 | A2 | A1 | A0 | Reads | Base address and name | Source |
|---|---|---|---|---|---|---|---|
| x | x | 0 | 0 | 0 | Port A: the pins | `$0280` SWCHA | [from 6532; SPG PIA 6.0] |
| x | x | 0 | 0 | 1 | DDRA | `$0281` SWACNT | [from 6532; SPG PIA 6.0] |
| x | x | 0 | 1 | 0 | Port B: pins, or ORB for output bits (section 5) | `$0282` SWCHB | [from 6532; SPG PIA 6.0] |
| x | x | 0 | 1 | 1 | DDRB | `$0283` SWBCNT | [from 6532; SPG PIA 6.0] |
| x | e | 1 | x | 0 | The timer's count; A3 sets (1) or clears (0) the timer's interrupt enable | `$0284` INTIM | [from 6532; SPG PIA 2.2] |
| x | x | 1 | x | 1 | The flag register: bit 7 timer, bit 6 PA7, bits 5 to 0 zero | `$0285` TIMINT (the suite's name; 2k6specs calls it INSTAT) | [from 6532; the suite: roms/riot/pa7-edge.asm] |

**Writes**, I/O half:

| A4 | A3 | A2 | A1 | A0 | Writes | Base address and name | Source |
|---|---|---|---|---|---|---|---|
| x | x | 0 | 0 | 0 | ORA, all 8 bits | `$0280` SWCHA | [from 6532; the suite: roms/riot/io-output.asm] |
| x | x | 0 | 0 | 1 | DDRA | `$0281` SWACNT | [from 6532] |
| x | x | 0 | 1 | 0 | ORB, all 8 bits | `$0282` SWCHB | [from 6532] |
| x | x | 0 | 1 | 1 | DDRB | `$0283` SWBCNT | [from 6532] |
| 0 | x | 1 | i | p | PA7 edge control: A0 = p, 1 positive (low to high), 0 negative; A1 = i, 1 enables the PA7 interrupt; data ignored | `$0284` to `$0287` (the suite's `PA7_NEG` = `$0284`, `PA7_POS` = `$0285`) | [from 6532; the suite: roms/riot/pa7-edge.asm] |
| 1 | e | 1 | d | d | The timer: count = data; A1 A0 = 00 for 1, 01 for 8, 10 for 64, 11 for 1024; A3 = e, the timer's interrupt enable | `$0294` TIM1T, `$0295` TIM8T, `$0296` TIM64T, `$0297` T1024T | [from 6532; SPG PIA 2.1] |

x = not decoded. Within `$0280-$029F`, so: reads of INTIM answer at `$0284`, `$0286`, `$028C`, `$028E`, `$0294`, `$0296`, `$029C`, `$029E`; reads of TIMINT at `$0285`, `$0287`, `$028D`, `$028F`, `$0295`, `$0297`, `$029D`, `$029F`; the four port registers at `+$00`, `+$08`, `+$10`, `+$18` from `$0280` [inferring from Table 1 of 6532 and the wiring above]. The timer writes with the interrupt enabled are `$029C` to `$029F`; edge control also answers at `$028C` to `$028F` [inferring, same]. 2k6specs agrees for reads ("two copies of INTIM", the timer read at `$0294-$0297` as many games do) [from 2k6specs].

### Mirrors

| Part | Selected by | Copies in `$0000-$1FFF` | Source |
|---|---|---|---|
| RAM, 128 bytes, A0 to A6 pick the byte | A12 = 0, A9 = 0, A7 = 1 | `$0080`, `$0180`, `$0480`, `$0580`, `$0880`, `$0980`, `$0C80`, `$0D80`, each `$80` bytes | [from 2k6specs; the suite: roms/riot/ram-mirrors.asm; inferring the full list from A8, A10, A11 being free] |
| I/O and timer, 32 addresses | A12 = 0, A9 = 1, A7 = 1 | blocks `$0280`, `$0380`, `$0680`, `$0780`, `$0A80`, `$0B80`, `$0E80`, `$0F80`; in each, the 32 addresses at `+$00`, `+$20`, `+$40`, `+$60` (A5, A6 free) | [from 2k6specs; the suite: roms/riot/io-mirrors.asm; inferring, same] |

The 6507 drops A13 to A15 (`bus.md`).

### Worked example 1: decode

| Access | Reaches | Source |
|---|---|---|
| read `$0380`, `$0680`, `$0A80` | SWCHA | [from the suite: roms/riot/io-mirrors.asm, codes $02, $03, $06] |
| read `$0381` | SWACNT | [from the suite: roms/riot/io-mirrors.asm, code $04] |
| write `$0180`, read `$0080` | the same RAM byte (the stack works through `$0180-$01FF`) | [from the suite: roms/riot/ram-mirrors.asm] |
| write `$0480`, `$0880`, read `$0080` | the same RAM byte | [from the suite: roms/riot/ram-mirrors.asm] |
| read `$0294` | INTIM (A4 not decoded on a read) | [inferring from 6532 Table 1; 2k6specs] |
| read `$0295` | TIMINT | [inferring, same] |
| write `$0284` | arm the negative edge, PA7 interrupt off; not a timer write | [from 6532; the suite: roms/riot/pa7-edge.asm] |
| write `$029E` | TIM64T with the timer interrupt enabled | [inferring from 6532 Table 1] |
| read `$028C` | INTIM, and the timer interrupt enable is set | [inferring from 6532 Table 1] |
| write `$02A1` | SWACNT (A5 free) | [inferring from 6532 Table 1] |

## 4. RAM

128 bytes of static RAM; a write latches the byte, a read returns it; A0 to A6 pick it [from 6532; the suite: roms/riot/ram.asm]. No two of the 128 addresses share a cell [from the suite: roms/riot/ram.asm, the address-identity march]. At `$0080-$00FF` it is the zero page's top half and, through `$0180`, the stack [from SPG PIA 3.0; 2k6specs]. Neither data sheet says RES touches the RAM, and nothing says what it holds at power on (section 9).

## 5. Ports A and B

- **Registers.** Each port has an 8-bit output register (ORA, ORB) and a data direction register (DDRA, DDRB). A DDR bit of 1 makes that pin an output, 0 an input [from 6532; SPG PIA 5.1]. Both DDRs read back [from 6532 Table 1; the suite: roms/riot/io-mirrors.asm, code $04].
- **Writes keep all 8 bits.** A write to ORA stores all 8 bits whatever the directions; a bit written while its pin is an input appears on the pin when the pin becomes an output, and direction changes never alter the stored bits [from 6532: "for input lines, the processor can write into the corresponding bit of the Output Register"; the suite: roms/riot/io-output.asm]. The same holds for ORB [from 6532: port B works "exactly the same"].
- **Reading port A reads the pins.** An output pin reads its ORA bit "if the voltage on the pin is allowed to go to 2.4v"; an input pin reads what drives it [from 6532]. An input pin with nothing driving it reads 1: the chip has a pull-up on each input [from 6532; the suite: roms/riot/io-output.asm, "an idle Atari pin floats high"].
- **Reading port B reads ORB for output bits.** Port B has push-pull drivers, and the chip reads the output register instead of the pin for them [from 6532]. Input bits read the pin.
- **A port A output at 1 against a closed switch.** The 2600's joystick switches connect a pin to ground [from 2k6specs: "Connect all direction switches and push buttons to GND"], and port A's output sources only about 100 µA [from 6532, the output high current]. So an output bit at 1 whose switch is closed reads 0, and an output bit at 0 reads 0 whatever the switch [inferring from 6532 and 2k6specs; no source states it for the 2600; `[guessing - verify]` that the model should do this, see section 12].
- **Unconnected port B bits.** PB2, PB4 and PB5 reach nothing [from the schematic], so as inputs they read 1 through the pull-ups [inferring from 6532 and the schematic]. Programs may make them outputs and use them as 3 bits of storage, as Combat does [from 2k6specs].

### Worked example 2: port A output register (the suite's sequence)

Nothing plugged in, so every input reads 1 [from the suite: roms/riot/io-output.asm].

| Write | SWACNT then | ORA then | Read SWCHA |
|---|---|---|---|
| SWACNT = `$00`, SWCHA = `$55` | `$00` | `$55` | `$FF` |
| SWACNT = `$0F` | `$0F` | `$55` | `$F5` |
| SWCHA = `$AA` | `$0F` | `$AA` | `$FA` |
| SWACNT = `$FF` | `$FF` | `$AA` | `$AA` |
| SWACNT = `$00` | `$00` | `$AA` | `$FF` |
| SWACNT = `$FF` | `$FF` | `$AA` | `$AA` |

With SWACNT = `$FF`, writes of `$55`, `$AA`, `$FF`, `$00` read back unchanged [from the suite: roms/riot/io-ports.asm].

### Worked example 3: port B with outputs

All switches at rest: Reset and Select released, colour, both difficulties at B. The pins are `%0011 1111` = `$3F` (PB7 0, PB6 0, PB5 1, PB4 1, PB3 1, PB2 1, PB1 1, PB0 1) [inferring from section 10 and the pull-ups]. Write SWBCNT = `$34` (PB2, PB4, PB5 outputs) and SWCHB = `$00`: SWCHB reads `%0000 1011` = `$0B`, the three output bits from ORB and the rest from the pins [inferring from 6532].

## 6. The timer

### The rules

- **The write.** Writing N to TIM1T, TIM8T, TIM64T or T1024T loads N, chooses the divider D (1, 8, 64, 1024) and clears the timer flag [from 6532: "whenever the timer is written or read the interrupt is reset"; SPG PIA 2.1; the suite: roms/riot/timer-underflow-read.asm, "the write itself clears the timer flag"]. A3 of the address sets the timer's interrupt enable (section 7).
- **The count.** Call the write's cycle 0. The count drops on cycle 1, then every D cycles: a read on cycle n, for 1 ≤ n ≤ D × N, returns N - 1 - floor((n - 1) / D) [from 6532 1977, Figure 5: "N-1" over pulses 1 to 8, "N-2" from 9, "0" over 409 to 416, 255 at 417, for 52 at divide by 8; 6532: the first reads are "51, 50, 49"; 2k6specs: "decremented once immediately after writing"].
- **Passing zero.** On cycle D × N + 1 the count goes from 0 to `$FF` and the timer flag is set [from 6532 1977, Figure 5; 6532: "(52 x 8) + 1 = 417T"; SPG PIA 2.3: it "holds that 0 counts for one interval, then the counter flips to FF"]. The 1981 sheet's own Figure 3 puts it on pulse 416, one cycle early; the suite agrees with 417 (worked example 6), so this sheet follows the 1977 figure and the text.
- **After zero.** The count then falls once a cycle, whatever D was: cycle D × N + 1 + k reads `$FF` - k [from 6532; SPG PIA 2.3; the suite: roms/riot/timer.asm]. While it free-runs, the flag stays set, because only a timer read or write clears it and both end the free run.
- **A write of 0** underflows at once: cycle 1 reads `$FF` and sets the flag [from 2k6specs: "value 00h does immediately underflow"; inferring from the rule above with N = 0].
- **Reading INTIM.** Returns the count. It clears the timer flag [from 6532; the suite: roms/riot/timer-divisors.asm, code $04], except on cycle D × N + 1 itself, where it returns `$FF` and leaves the flag set [from 6532: "the reading of the timer at the same time the interrupt occurs will not reset the interrupt flag"; the suite: roms/riot/timer-underflow-read.asm]. A read after zero also ends the free run: the count goes on falling from the value read, once every D cycles [from the suite: roms/riot/timer-divisors.asm, "re-arms the selected interval"; 2k6specs: "originated at the current value"]. A read before zero changes nothing but the interrupt enable.
- **Reading TIMINT** does not clear the timer flag [from 6532; the suite: roms/riot/timer-divisors.asm, code $03; roms/riot/pa7-edge.asm, code $08].
- **The halt.** The timer counts through a WSYNC halt: it is clocked every CPU cycle, halted or not [from the suite: roms/riot/timer.asm, code $02; SPG PIA 2.0].

### Worked example 4: write 3, each divider

Cycle 0 is the write. "Flag" is TIMINT bit 7 as a read on that cycle would see it; an INTIM read on the `$FF` cycle leaves it set.

| Divider (address) | Reads `$02` | Reads `$01` | Reads `$00` | `$FF`, flag set | Then |
|---|---|---|---|---|---|
| TIM1T (`$0294`) | cycle 1 | 2 | 3 | 4 | 5 reads `$FE`, 6 `$FD` |
| TIM8T (`$0295`) | cycles 1 to 8 | 9 to 16 | 17 to 24 | 25 | 26 reads `$FE`, 27 `$FD` |
| TIM64T (`$0296`) | 1 to 64 | 65 to 128 | 129 to 192 | 193 | 194 reads `$FE` |
| T1024T (`$0297`) | 1 to 1024 | 1025 to 2048 | 2049 to 3072 | 3073 | 3074 reads `$FE` |

[inferring from the rules above, each from 6532 1977 Figure 5 scaled to D]. The data sheet's own case: 52 to TIM8T reads 25 on cycle 213 and 0 on cycle 415, `$FF` on 417, and `$AC` on 500 [from 6532; 6532 1977].

### Worked example 5: the flag's clearing rules

| Step | Cycle | INTIM or TIMINT reads | Timer flag after |
|---|---|---|---|
| Write 3 to TIM64T | 0 | | clear |
| Read TIMINT | 150 | `$00` in bit 7 | clear |
| Read TIMINT | 200 | `$80` in bit 7 | set (not cleared by TIMINT) |
| Read INTIM | 204 | `$F4` (`$FF` - 11) | clear; the divider is 64 again |
| Read INTIM | 210 | `$F4` | clear |
| Write 5 to TIM8T | 220 | | clear |

[from 6532; the suite: roms/riot/timer-divisors.asm; the row at 210 assumes the next decrement comes 64 cycles after the read, `[guessing - verify]`, section 12].

### Worked example 6: the read on the cycle it passes zero (`riot/timer-underflow-read`)

The test writes V to TIM1T with `STX` (write on cycle 0), runs six `NOP`s (cycles 1 to 12), and reads INTIM with `LDA` (read on cycle 16), stores it (`STA` zero page, cycles 17 to 19), then reads TIMINT (read on cycle 23) [from the suite: roms/riot/timer-underflow-read.asm, the code]. The flag is set on cycle V + 1.

| V | INTIM on cycle 16 | Timer flag seen on cycle 23 |
|---|---|---|
| 16 | `$00` | set (set on 17, after the read) |
| 15 | `$FF`: the read lands on the cycle the flag is set | **set**: this read does not clear it (the test's code $01) |
| 14 | `$FE` | clear: this read cleared it |

Only V = 15 reads `$FF`, which is how the test finds the cycle [inferring from the rules and the test's code].

### Worked example 7: the timer's edge at one-cycle resolution (`riot/timer-vblank-spin`)

The test writes 8 to TIM64T (cycle 0), runs 8 - IDX `NOP`s, then loops `INX; LDA INTIM; BNE` (9 cycles) until INTIM reads 0, for IDX = 0 to 7, and expects 46, 47, 47, 47, 47, 47, 48, 48 passes [from the suite: roms/riot/timer-vblank-spin.asm]. Counting the code's cycles, pass j reads INTIM on cycle 29 + 2(8 - IDX) + 9(j - 1). By the rules above INTIM first reads 0 on cycle 64 × 7 + 1 = 449, and the first pass reading on or after 449 gives exactly that profile. If the count reached 0 one cycle earlier (cycle 448, the 1981 Figure 3's reading), IDX = 1 would give 46 and the test would fail with code $02 [inferring from the rules, the test's code and the 6502's cycle counts; the loop's branch is taken as not crossing a page, the only reading under which any one zero cycle fits all eight counts].

The suite's other timer tests check cadence only, by subtracting two readings: TIM1T falls 76 a line, TIM8T 76 over 8 lines, TIM64T 19 over 16 lines [from the suite: roms/riot/timer.asm, roms/riot/timer-divisors.asm].

## 7. The flag register and the IRQ pin

- **TIMINT** reads bit 7 = timer flag, bit 6 = PA7 flag, bits 5 to 0 = 0, driven by the chip [from 6532, Figure 4; 6532 1977: "bit 5-0 are all 0"]. Reading it clears the PA7 flag only [from 6532; the suite: roms/riot/pa7-edge.asm, codes $03, $07, $08].
- **The timer's interrupt enable** is set or cleared by A3 on every timer read and every timer write [from 6532: "During a Read or Write operation address line A3 controls the interrupt capability"; 6532 Table 1, note (a) on both rows]. A TIMINT read does not change it (A3 is not decoded there) [from 6532 Table 1].
- **The PA7 interrupt enable** is A1 of an edge-control write [from 6532].
- **IRQ** is low when (timer flag and timer enable) or (PA7 flag and PA7 enable) [from 6532: the flag drives IRQ "if the PA7 interrupt has been enabled"; A3 "enables IRQ"]. The enables gate the pin, never the flags [from 6532: the PA7 flag is set "if interrupting from PA7 is disabled"; the suite: roms/riot/pa7-edge.asm]. On the 2600 the pin goes nowhere (section 2), so `Irq` is for the chip's own tests.

### Worked example 8: enables

Write 2 to `$029C` (TIM1T, A3 = 1): on cycle 3 the flag is set and `Irq` is true. Read INTIM at `$0284` (A3 = 0) on cycle 10: returns `$F8`, the flag clears and the enable clears, `Irq` false. Write 2 to `$0294` (A3 = 0): on cycle 3 the flag is set and `Irq` stays false [inferring from 6532 Table 1 and the rules in section 6].

## 8. The PA7 edge detect

- **What it watches** is the PA7 pin, whether PA7 is an input or an output, so a program can make an edge by writing SWCHA with PA7 an output [from 6532: "always enabled, no matter whether PA7 is set up as an input or an output"; the suite: roms/riot/pa7-edge.asm]. A change of SWACNT, or of the joystick, that moves the pin is an edge too [inferring from 6532: it watches the pin].
- **Polarity**: an edge-control write with A0 = 0 (`$0284`) arms high to low, A0 = 1 (`$0285`) arms low to high. The data is ignored [from 6532; the suite: roms/riot/pa7-edge.asm].
- **Setting**: an edge of the armed polarity sets bit 6; an edge of the other polarity does not [from 6532; the suite: roms/riot/pa7-edge.asm, codes $02, $04, $05, $06].
- **Arming sets nothing.** An edge-control write never sets the flag, whatever level PA7 is at and whatever the polarity was [from the suite: roms/riot/pa7-polarity.asm, measured: the matrix reads `$10`]. The data sheet says the flag "may also be set by changing the polarity"; the suite's console measurement contradicts it, and this sheet follows the suite (section 11).
- **Clearing**: only a read of the flag register clears it [from 6532]. Timer reads and writes, and edge-control writes, leave it [from the suite: roms/riot/pa7-edge.asm, codes $07 and $08; roms/riot/pa7-polarity.asm, cell H].
- **When**: the flag is readable by a TIMINT read at least one cycle after the cycle the pin changed [guessing - verify; the suite never reads sooner than the next instruction].

### Worked example 9: PA7 edges (the suite's `pa7-edge`)

SWACNT = `$80` (PA7 an output). Each TIMINT read below is masked with `$40` [from the suite: roms/riot/pa7-edge.asm].

| Step | PA7 | Armed | TIMINT & `$40` |
|---|---|---|---|
| SWCHA = `$80`, write `$0284`, read TIMINT twice | high | negative | `$00` |
| SWCHA = `$00` | high to low | negative | `$40` (the read clears it) |
| read again | low | negative | `$00` |
| SWCHA = `$80` | low to high | negative | `$00` |
| write `$0285`, read TIMINT, SWCHA = `$00` | high to low | positive | `$00` |
| SWCHA = `$80` | low to high | positive | `$40` |
| TIM1T = 2, wait, SWCHA = `$00` then `$80`, read TIMINT & `$C0` twice | low to high | positive | `$C0`, then `$80` |

## 9. Power on

- **RES** zeroes the four I/O registers (ORA, DDRA, ORB, DDRB), so every pin is an input; it disables both interrupts and sets the PA7 polarity to negative [from 6532; 6532 1977]. On the 2600 RES is pulled low only by its own RC at power on (section 2) [from the schematic].
- **Not stated** by either data sheet: the timer's count, its divider, the timer flag, the PA7 flag and the RAM [from 6532; 6532 1977: RES names only the I/O registers and the interrupts]. The suite treats the timer flag as "undefined until the first timer write" and masks it [from the suite: roms/riot/pa7-edge.asm].
- **The timer runs before the program starts.** The RIOT's RC is 24 kΩ with 0.1 µF, the 6507's 24 kΩ with 4.7 µF [from the schematic], so the RIOT leaves reset tens of milliseconds before the CPU does and counts φ2 meanwhile [inferring from the RC values; the exact thresholds are `[guessing - verify]`]. The timer's value at the program's first instruction is therefore unknown on the hardware. Rockwell notes that the 6502's first cycles after reset put arbitrary addresses on the bus [from Rockwell note], so on the 2600 those cycles can also touch the RIOT [inferring].
- **PA7 flag at power on.** RES makes PA7 an input pulled high, so no falling edge happens unless the left joystick is held right [inferring from 6532 and section 10]; 6532 warns the flag may be set during reset. `[guessing - verify]`.
- **For the model** (a choice for task 3, recorded in `docs/known-differences.md`): the I/O registers, enables and polarity as RES gives them; the timer, flags and RAM at fixed values the model picks, so runs repeat. No `riot/` test reads the timer before writing it, or reads the timer flag before a timer write without masking it [from the suite: every roms/riot/ file]; `CLEAN_START` clears the RAM [from the suite: README.md].

## 10. Port A as the joysticks, port B as the switches

Every input is active low: 0 means pressed, moved or "B"/"B/W" [from SPG PIA 4.1, 5.3; 2k6specs].

| SWCHA bit | Pin | Joystick pin | Meaning | Source |
|---|---|---|---|---|
| D7 | PA7 | J202 (left) pin 4 | P0 right | [from SPG PIA 5.3; the schematic; 2k6specs] |
| D6 | PA6 | J202 pin 3 | P0 left | same |
| D5 | PA5 | J202 pin 2 | P0 down | same |
| D4 | PA4 | J202 pin 1 | P0 up | same |
| D3 | PA3 | J203 (right) pin 4 | P1 right | same |
| D2 | PA2 | J203 pin 3 | P1 left | same |
| D1 | PA1 | J203 pin 2 | P1 down | same |
| D0 | PA0 | J203 pin 1 | P1 up | same |

P0 is the left player [from SPG PIA 5.3]. The schematic does not label which connector is left; that J202 is the left port is [inferring from SPG PIA 5.3 and PA4 to PA7 reaching J202]. The fire buttons are not on the RIOT; they are INPT4 and INPT5 on the TIA (`tia.md`) [from SPG PIA 5.3; 2k6specs]. SWACNT should be `$00` to read joysticks [from SPG PIA 5.3; 2k6specs].

| SWCHB bit | Pin | Switch | 1 means | 0 means | Source |
|---|---|---|---|---|---|
| D7 | PB7 | Right (P1) difficulty | A (pro) | B (amateur) | [from SPG PIA 4.1; the schematic; 2k6specs] |
| D6 | PB6 | Left (P0) difficulty | A | B | same |
| D5 | PB5 | not connected | reads 1 as an input | | [from the schematic; inferring the 1] |
| D4 | PB4 | not connected | reads 1 as an input | | same |
| D3 | PB3 | Colour / B.W. | colour | black and white | [from SPG PIA 4.1; the schematic; 2k6specs] |
| D2 | PB2 | not connected | reads 1 as an input | | [from the schematic; inferring the 1] |
| D1 | PB1 | Select | released | pressed | [from SPG PIA 4.1; the schematic] |
| D0 | PB0 | Reset | released | pressed | same |

Select and Reset spring back; the difficulty and colour switches stay where they are set [from the spec; 2k6specs: "Older consoles used switches instead of buttons for reset/select"]. SECAM consoles ground D3 [from 2k6specs]; SECAM is not modelled.

### Worked example 10: inputs

| Held | SWCHA | SWCHB |
|---|---|---|
| Nothing; colour; both difficulties B | `$FF` | `$3F` |
| P0 up and right; P1 nothing | `$6F` | `$3F` |
| P0 nothing; P1 down and left together | `$F9` | `$3F` |
| Reset held; colour; left difficulty A | `$FF` | `$7E` |
| Select held; B/W; right difficulty A | `$FF` | `$B5` |

[inferring from the two tables above]. Any combination is taken as written, up and down together included (spec, "Input").

## 11. Where the sources disagree

1. **When the timer passes zero.** The 1981 sheet's Figure 3 sets the flag on pulse 416 for 52 at divide by 8; its text says (52 x 8) + 1 = 417T, its own `$AC` on pulse 500 needs `$FF` on 417, and the 1977 sheet's Figure 5 shows 255 on pulse 417. The suite's `timer-vblank-spin` fits 417 and fails 416 (worked example 7). This sheet follows 417, that is cycle D × N + 1. The repository's 6530 does the same [from Rriot6530.cs, citing the 6530 sheet's "(52 x 8) + 1 = 417"].
2. **Arming the PA7 polarity.** Both 6532 sheets say a change of polarity may set the PA7 flag; the suite measured that it does not, on a PAL console [from the suite: roms/riot/pa7-polarity.asm]. This sheet follows the suite: it is a measurement, and the data sheet's "may" is a warning.
3. **The flag register.** 2k6specs says both bits 7 and 6 are set on underflow, bit 6 cleared by reading it and bit 7 by a timer write [from 2k6specs]. Both data sheets make bit 6 the PA7 flag and clear bit 7 on a timer read or write, and the suite's `timer-divisors` and `pa7-edge` check exactly that. This sheet follows the data sheets and the suite.
4. **Port B as output.** The Stella guide says port B is "hardwired to be an input port only" and SWBCNT is "hardwired as input" [from SPG PIA 4.1, 6.0]. The chip's DDRB is a real register [from 6532], and three of the pins are unconnected [from the schematic]; 2k6specs says programs use them as storage. This sheet follows the data sheet: SWBCNT and ORB work, and the guide means only that the switches are inputs.
5. **I/O mirrors.** 2k6specs describes sixteen 8-byte mirrors in each `$80` block. That holds for reads; for writes A4 separates edge control (`$0284-$0287`) from the timer (`$0294-$0297`) [from 6532 Table 1].
6. **PA7 edge mode as an input.** The data sheet says PA7 "should be set up as an input" for edge mode, and also that an output edge sets the flag. The suite relies on the second; this sheet does too.

## 12. Open items

1. **The divider's phase after an INTIM read ends the free run.** The sources say the selected interval resumes from the value read, not where in the interval the next decrement falls. This sheet's guess: D cycles after the read. No `riot/` test measures it. `[guessing - verify]`
2. **An INTIM read on the cycle the flag is set**: whether it ends the free run as a later read does. Guess: no, since it does not clear the flag either. No test measures it, and for TIM1T it makes no difference. `[guessing - verify]`
3. **The power-on timer, flags and RAM** (section 9): a model choice, recorded.
4. **A port A output at 1 against a pressed joystick** reads 0 by the inference in section 5. Plan task 9 says an output bit "reads as `riot.md` says, not as the stick"; that holds for an output at 0. No suite test drives an output against a held stick. Task 9 decides, and records it. (Decided by task 9, 14 October 2026: kept, an output at 1 reads 0 while its stick holds the pin low. The data sheet's output-high current of about 100 µA cannot hold a pin a switch grounds; the other reading, that the chip reads its output register as port B does, is what the data sheet says port B does differently, so it is not port A's. `ControllersTests` holds it through the joystick `[guessing - verify]`.)
5. **The cycle the PA7 flag becomes visible** (section 8).
6. **PB2, PB4 and PB5 reading 1** as inputs rests on the internal pull-ups and the N/C marks; no source states the value read on a 2600.

## 13. Sources opened

All read on 14 October 2026.

| Tag | Document | Where |
|---|---|---|
| `[from 6532]` | Commodore Semiconductor Group (MOS Technology), "6532 (Memory, I/O, Timer Array)", data sheet dated 2/81, 10 pages; a scan with an OCR text layer, its tables and figures read from the page images | http://www.6502.org/documents/datasheets/mos/mos_6532_riot.pdf |
| `[from 6532 1977]` | MOS Technology, MCS6532 preliminary data sheet, February 1977, 8 pages (scan with OCR) | http://www.6502.org/documents/datasheets/mos/mos_6532_riot_preliminary_feb_1977.pdf |
| `[from 6530]` | MOS Technology / Commodore Semiconductor Group, "MCS6530 (Memory, I/O, Timer Array)" data sheet, 12 pages (scan with OCR) | http://www.6502.org/documents/datasheets/mos/mos_6530_rriot.pdf |
| `[from Rockwell note]` | Rockwell, "R6500/R6532 Timer Interrupt Precautions", application note R6500N02, December 1978 | http://www.6502.org/documents/datasheets/rockwell/rockwell_r6530_r6532_timer_interrupt_precautions.pdf |
| `[from the schematic]` | Atari, "2600/2600A VCS Domestic (M/N) Field Service Manual", FD100133 Rev. 02, section 2, the pages "2600 Motherboard Schematic" and "2600A (Revs. 1-13) Motherboard Schematic"; a copy "rebuilt by Jonathan Grimm, 2024-08-01", with the schematics redrawn, not a scan of the original drawings. The ConsoleMods copy (https://ConsoleMods.org/wiki/images/e/ec/2600_Field_Service_Manual.pdf) refused the download with a browser challenge | https://www.videogamemanual.com/Atari%202600%20Field%20Service%20Manual%202024%20(USA).pdf |
| `[from 2k6specs]` | Martin Korth, "Atari 2600 Specifications" | https://problemkaputt.de/2k6specs.htm |
| `[from SPG ...]` | Steve Wright, Stella Programmer's Guide, the PIA chapter, sections 1.0 to 6.0 | README.md |
| `[from the suite: ...]` | every file in `roms/riot/` (`io-mirrors`, `io-output`, `io-ports`, `pa7-edge`, `pa7-polarity`, `ram-mirrors`, `ram`, `timer-divisors`, `timer-underflow-read`, `timer-vblank-spin`, `timer`), and `README.md` | dbhq-uk/missingno-vcs-tests at 891c079976a2f68033ca4af2fe2fd85aaf1906c9 |
| `[from Rriot6530.cs]` | this repository's `src/Dbhq.Machines.Kim1/Rriot6530.cs`, read for what the KIM-1's 6530 does | this repository |

Not opened: bitsavers (it refused with 403), and no original scan of the CX2600 schematic, so every `[from the schematic]` rests on the 2024 redraw, cross-checked between its two boards.
