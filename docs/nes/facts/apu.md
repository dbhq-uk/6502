# NES APU: the channels, the frame counter, the mixer

Written 5 October 2026 for the NES plan, task 1. Tags and page revisions are in
[`README.md`](README.md). Used by tasks 8 (the channels and frame counter) and 9
(the DMC, its DMA and the samples). The DMA cycle costs are in
[`bus.md`](bus.md) section 6.

## 1. Clocking

- The APU is in the 2A03 (NTSC) and the 2A07 (PAL); its registers are
  `$4000-$4013`, `$4015` and `$4017` [from APU].
- One **APU cycle** is two CPU cycles [from APU Frame Counter]. The pulse,
  noise and DMC timers count APU cycles, so their periods are even in CPU
  cycles; the triangle's timer counts CPU cycles [from APU].
- A **divider** with period P counts P, P-1, ... 0, and on the clock after 0
  reloads P and outputs a clock, so its period is P + 1 [from APU, glossary].
- The two halves of an APU cycle are the DMA's get and put cycles (`bus.md` 5)
  [from DMA].

## 2. Pulse 1 and 2 (`$4000-$4007`)

| Register | Bits | Source |
|---|---|---|
| `$4000`/`$4004` | `DDLC VVVV`: duty, length halt and envelope loop, constant volume, volume or envelope period | [from APU Pulse] |
| `$4001`/`$4005` | `EPPP NSSS`: sweep (section 3) | [from APU Sweep] |
| `$4002`/`$4006` | timer low 8 bits | [from APU Pulse] |
| `$4003`/`$4007` | `LLLL LTTT`: length load, timer high 3 bits | [from APU Pulse] |

- A `$4003`/`$4007` write loads the length counter (if the channel is enabled),
  restarts the sequencer at its first step and restarts the envelope; it does
  not reset the timer's divider [from APU Pulse; APU Length Counter].
- A `$4000` write changes the duty but not the sequencer's position [from APU
  Pulse].
- The 11-bit timer `t` counts in APU cycles; the 8-step sequencer advances when
  it passes 0, so a period is `16 x (t + 1)` CPU cycles, `f = fCPU / (16 x (t +
  1))` [from APU Pulse].
- **Duty sequences**, as the output steps after a restart [from APU Pulse]:

| Duty | Output | |
|---|---|---|
| 0 | `0 1 0 0 0 0 0 0` | 12.5% |
| 1 | `0 1 1 0 0 0 0 0` | 25% |
| 2 | `0 1 1 1 1 0 0 0` | 50% |
| 3 | `1 0 0 1 1 1 1 1` | 25% negated |

  The sequencer counts down from 0, so it reads its table in the order 0, 7, 6,
  ... 1 [from APU Pulse].
- **Output** is the envelope volume, or 0 when the sequencer output is 0, the
  sweep mutes, the length counter is 0, or `t < 8` [from APU Pulse].
- The two channels differ only in the sweep's negate (section 3) [from APU
  Pulse].

## 3. Sweep

- `$4001`/`$4005` `EPPP NSSS`: enabled, divider period P (P + 1 half frames),
  negate, shift. A write sets the reload flag [from APU Sweep].
- **Target period**, computed all the time: `change = t >> S`; negated, pulse 1
  adds `-change - 1` (ones' complement) and pulse 2 adds `-change`; a negative
  sum clamps to 0 [from APU Sweep].
- **Muting**, whether or not the sweep is enabled: when `t < 8` or the target is
  over `$7FF` [from APU Sweep].
- **On each half-frame clock:** if the divider is 0, the sweep is enabled, S is
  not 0 and the channel is not muted, `t` = target. Then, if the divider is 0 or
  the reload flag is set, the divider = P and the reload flag clears; otherwise
  the divider counts down [from APU Sweep].

### Worked example 1: the sweep's negate

`t` = 20, S = 0 so `change` = 20, negate on: pulse 1's target is 20 - 21 = -1,
clamped to 0; pulse 2's is 0 [from APU Sweep: "Making 20 negative produces a
change amount of -21" and "-20"]. With negate off and S = 0, `t` = `$400` gives a
target of `$800`, over `$7FF`, so the channel is muted even with the sweep
disabled [from APU Sweep].

## 4. Envelope (pulses and noise)

- Each has a start flag, a divider and a decay level [from APU Envelope].
- A write to the channel's fourth register sets the start flag [from APU
  Envelope].
- **On each quarter-frame clock:** if the start flag is set, clear it, set decay
  to 15 and reload the divider with V; otherwise clock the divider. When the
  divider passes 0 it reloads V and clocks the decay: decay counts down to 0,
  and at 0 goes back to 15 if the loop flag is set [from APU Envelope].
- **Output** is V if the constant-volume flag is set, else the decay level; the
  decay keeps running either way [from APU Envelope].

## 5. Length counter

- On the pulses, triangle and noise. Writing the channel's length register
  loads entry `L` (bits 7 to 3) of this table, if the channel is enabled in
  `$4015` [from APU Length Counter]:

```
     0   1   2   3   4   5   6   7   8   9   A   B   C   D   E   F
00: 10,254, 20,  2, 40,  4, 80,  6,160,  8, 60, 10, 14, 12, 26, 14
10: 12, 16, 24, 18, 48, 20, 96, 22,192, 24, 72, 26, 16, 28, 32, 30
```

- Clearing the channel's bit in `$4015` sets the counter to 0 and holds it there
  [from APU Length Counter].
- **On each half-frame clock** it counts down unless it is 0 or the halt flag is
  set [from APU Length Counter]. The channel is silent while it is 0 [from APU
  Length Counter].
- The table is the length plus one, for the model where a channel stops when the
  counter **becomes** 0 [from APU Length Counter].

## 6. Triangle (`$4008-$400B`)

- `$4008` `CRRR RRRR`: control flag (also length halt), linear counter reload
  value. `$400A` timer low. `$400B` `LLLL LTTT`: length load, timer high, and it
  sets the linear counter's reload flag [from APU Triangle].
- The timer counts CPU cycles, so `f = fCPU / (32 x (t + 1))` [from APU
  Triangle].
- **On each quarter-frame clock:** if the reload flag is set, linear counter =
  R, else if it is not 0 it counts down; then if the control flag is clear the
  reload flag clears [from APU Triangle].
- The sequencer advances only while both the linear counter and the length
  counter are not 0 [from APU Triangle]. Its 32 steps are `15 14 ... 1 0 0 1
  ... 14 15` [from APU Triangle].
- Silenced, it holds its last value, not 0 [from APU]. Periods 0 and 1 give an
  ultrasonic wave, which some emulators halt instead [from APU Triangle]
  [guessing - verify: the model keeps the real behaviour unless the resampler
  needs otherwise].

## 7. Noise (`$400C-$400F`)

- `$400C` `--LC VVVV` as the pulse; `$400E` `M--- PPPP`: mode and period index;
  `$400F` `LLLL L---`: length load and envelope restart [from APU Noise].
- **Periods** in CPU cycles [from APU Noise]:

| Index | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | A | B | C | D | E | F |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NTSC | 4 | 8 | 16 | 32 | 64 | 96 | 128 | 160 | 202 | 254 | 380 | 508 | 762 | 1016 | 2034 | 4068 |
| PAL | 4 | 8 | 14 | 30 | 60 | 88 | 118 | 148 | 188 | 236 | 354 | 472 | 708 | 944 | 1890 | 3778 |

- **The shift register** is 15 bits. On each timer clock: feedback = bit 0 XOR
  bit 6 (mode set) or bit 1 (mode clear); shift right one; bit 14 = feedback
  [from APU Noise].
- **Output** is the envelope volume, or 0 when bit 0 is set or the length
  counter is 0 [from APU Noise].
- **At power-up** the register is 1 [from APU Noise]. CPU power up state says
  `$0000` with the first clock shifting in a 1. The two disagree; APU Noise is
  trusted because a register of 0 with the feedback rule above stays 0 for ever
  [inferring].

### Worked example 2: the noise register

From 1 in mode 0: feedback = bit 0 (1) XOR bit 1 (0) = 1; shift right gives 0;
bit 14 set gives `$4000`. Next: bit 0 = 0, bit 1 = 0, feedback 0, result
`$2000` [inferring from the rule on APU Noise].

## 8. DMC (`$4010-$4013`)

| Register | Bits | Source |
|---|---|---|
| `$4010` | `IL-- RRRR`: IRQ enable (clearing it clears the flag), loop, rate index | [from APU DMC] |
| `$4011` | `-DDD DDDD`: output level, loaded at once | [from APU DMC] |
| `$4012` | sample address = `$C000 + A x 64` | [from APU DMC] |
| `$4013` | sample length = `L x 16 + 1` bytes | [from APU DMC] |

**Rates** in CPU cycles between output changes [from APU DMC]:

| Index | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | A | B | C | D | E | F |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NTSC | 428 | 380 | 340 | 320 | 286 | 254 | 226 | 214 | 190 | 160 | 142 | 128 | 106 | 84 | 72 | 54 |
| PAL | 398 | 354 | 316 | 298 | 276 | 236 | 210 | 198 | 176 | 148 | 132 | 118 | 98 | 78 | 66 | 50 |

- **The reader.** When the sample buffer is empty and bytes remain, a DMA
  fetches the next byte (`bus.md` 6) into the buffer; the address goes up,
  wrapping `$FFFF` to `$8000`; bytes remaining goes down, and at 0 the sample
  restarts if loop is set, or else the IRQ flag is set if IRQ is enabled [from
  APU DMC].
- **The output unit** has an 8-bit shift register, a bits-remaining counter and
  a silence flag. When the counter reaches 0 an output cycle ends: the counter
  reloads 8, and the buffer, if full, moves into the shift register and clears
  the silence flag; if empty, the silence flag is set [from APU DMC].
- **On each timer clock:** if not silent, bit 0 of the shift register adds 2 to
  the level (if the level is 125 or less) or takes 2 (if it is 2 or more); the
  register shifts right; the bits-remaining counter counts down [from APU DMC].
- The level (0 to 127) always goes to the mixer, enabled or not; it is 0 at
  power-up [from APU DMC].
- The IRQ flag holds IRQ low until it is cleared [from APU DMC].

### Worked example 3: a DMC byte

Level 64, the byte `$0F` (bits 0 to 3 set) in the shift register. Eight timer
clocks give 66, 68, 70, 72, then 70, 68, 66, 64 [inferring from the rule on APU
DMC]. At rate index `$F` on NTSC that byte lasts 8 x 54 = 432 CPU cycles [from
APU DMC: "432 CPU cycles ... between boundaries"].

## 9. Status (`$4015`)

- **Write** `---D NT21`: each 0 bit silences that channel and zeroes its length
  counter. D = 0 sets the DMC's bytes remaining to 0; D = 1 restarts the sample
  only if bytes remaining is 0. The write clears the DMC IRQ flag [from APU].
- **Read** `IF-D NT21`: DMC IRQ, frame IRQ, DMC bytes remaining over 0, length
  counters over 0. The read clears the frame IRQ flag but not the DMC's; a flag
  set on the same cycle as the read reads 1 and is not cleared [from APU].
- Bit 5 is open bus, and the read does not drive the external bus (`bus.md` 3)
  [from APU].

## 10. The frame counter (`$4017`)

- `$4017` write `MI-- ----`: mode (0 = 4-step, 1 = 5-step), IRQ inhibit. Setting
  I clears the frame IRQ flag [from APU Frame Counter].
- After a write the sequencer is reset 3 CPU cycles later if the write is
  during an APU cycle and 4 if between, and with M = 1 a quarter and a half
  frame clock happen at once [from APU Frame Counter]. "PAL behavior is currently
  assumed to be the same" [from APU Frame Counter].
- At power and reset the APU acts as if `$4017` was written 10 cycles before the
  first instruction; at power `$4017` is 0, so the frame IRQ is enabled [from
  PPU power up state; CPU power up state].

**The steps in APU cycles** [from APU Frame Counter], and in CPU cycles taking a
put as `2n + 1` and a get as `2n` [inferring; the NTSC results agree with the
CPU-cycle figures the plan remembered from the older version of the page]:

| Step | Quarter | Half | IRQ (4-step, I clear) | NTSC APU | NTSC CPU | PAL APU | PAL CPU |
|---|---|---|---|---|---|---|---|
| 1 | yes | | | 3728 put | 7457 | 4156 put | 8313 |
| 2 | yes | yes | | 7456 put | 14913 | 8313 put | 16627 |
| 3 | yes | | | 11185 put | 22371 | 12469 put | 24939 |
| 4 (4-step) | | | set | 14914 get | 29828 | 16626 get | 33252 |
| | yes | yes | set | 14914 put | 29829 | 16626 put | 33253 |
| | | | set | 0 (14915) get | 29830 | 0 (16627) get | 33254 |
| 4 (5-step) | | | | 14914 put | 29829 | 16626 put | 33253 |
| 5 (5-step) | yes | yes | | 18640 put | 37281 | 20782 put | 41565 |
| | | | | 0 (18641) get | 37282 | 0 (20783) get | 41566 |

- In 4-step mode the IRQ flag is set every 29830 CPU cycles on NTSC and 33254 on
  PAL [from APU Frame Counter]. In 5-step mode it is never set [from APU Frame
  Counter].
- Quarter-frame clocks drive the envelopes and the triangle's linear counter;
  half-frame clocks the length counters and sweeps [from APU Frame Counter].

## 11. The mixer

The output, 0.0 to 1.0, is [from APU Mixer]:

```
pulse_out = 95.88 / (8128 / (pulse1 + pulse2) + 100)
tnd_out   = 159.79 / (1 / (triangle / 8227 + noise / 12241 + dmc / 22638) + 100)
output    = pulse_out + tnd_out
```

with each group 0 when its inputs are all 0. Pulse, triangle and noise are 0 to
15 and the DMC 0 to 127 [from APU Mixer]. The wiki also gives a lookup-table form
within 4% [from APU Mixer].

After the DACs the NES has high-pass filters at 90 Hz and 440 Hz and a low-pass
at 14 kHz [from APU Mixer]. These belong to the resampler's task.

### Worked example 4: the mixer

- Both pulses at 15: `pulse_out = 95.88 / (8128 / 30 + 100) = 95.88 / 370.93 =
  0.2585` [inferring, computed].
- Triangle 15, noise 0, DMC 0: `tnd_out = 159.79 / (8227 / 15 + 100) = 159.79 /
  648.47 = 0.2464` [inferring, computed].
- DMC 127 alone: `tnd_out = 159.79 / (22638 / 127 + 100) = 159.79 / 278.25 =
  0.5743` [inferring, computed].

## 12. What differs by region

| | NTSC | PAL | Source |
|---|---|---|---|
| CPU clock | 1.789773 MHz | 1.662607 MHz | [from APU Pulse; Cycle reference chart] |
| Noise periods | section 7 | section 7 | [from APU Noise] |
| DMC rates | section 8 | section 8 | [from APU DMC] |
| Frame counter steps | section 10 | section 10 | [from APU Frame Counter] |
| Length table, duty, envelope, sweep, mixer | same | same | [inferring: the pages give one table each] |
| DMC DMA register conflicts | yes | no | [from DMA] |

## 13. Power-up

| What | At power | After reset | Source |
|---|---|---|---|
| `$4000-$4013` | 0 | unchanged (`$4011` keeps bit 0 only) | [from CPU power up state] |
| `$4015` | 0 | 0 | [from CPU power up state; APU] |
| `$4017` | 0, IRQ enabled | unchanged | [from CPU power up state] |
| Triangle phase | unknown | step 0, output 15 | [from CPU power up state] |
| Noise register | 1 (section 7) | unchanged | [from APU Noise] |
| DMC level | 0 | `&= 1` | [from APU DMC; CPU power up state] |

## 14. Open items

1. The frame counter's PAL `$4017` write delay, assumed equal to NTSC on the page
   [from APU Frame Counter] [guessing - verify: `pal_apu_tests` checks it].
2. The CPU-cycle conversion of the step table (10) [inferring; task 8 checks it
   against `apu_test` and `pal_apu_tests`].
3. Whether `t < 8` silences a PAL pulse ("TODO: PAL behavior?" on APU Pulse)
   [guessing - verify].
