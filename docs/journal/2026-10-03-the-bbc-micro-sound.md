---
title: "The BBC Micro makes a sound"
date: 2026-10-03
summary: "The sound chip is in: three tones and a noise channel, written through the system VIA as the OS writes them, and turned into samples a page can play. It is never ticked; it works out its clocks when it is written to or its samples are read, and a clock-by-clock copy kept beside it gives the same samples to the last bit. The start-up beep now sounds, and the fact sheet's list of what the OS writes at reset turned out to be short."
order: 21
---

# 3 October 2026: the BBC Micro makes a sound

Task 11 of the plan: the Texas Instruments SN76489, the Model B's sound chip, and
the buffer its samples go into. Three square-wave tone channels and a noise
channel, each with an attenuator in 2 dB steps, mixed into one output. The OS
writes it through the system VIA: the byte on port A, then latch bit 0 low for
about eight microseconds, then high again. It was built from the fact sheet,
`docs/bbc-micro/facts/via.md` section 4, after reading how the CRTC and the
video ULA joined the bus without being ticked
([the bus speed entry](2026-10-03-the-bbc-micro-bus-speed.md), "How a new chip
plugs in").

## The model

- **Registers and writes (s4.2).** A byte with bit 7 set latches one of eight
  registers and fills its low four bits; a byte with bit 7 clear fills the high
  six bits of a latched tone period, or the low bits of a latched attenuation or
  noise control. A write to the noise control, of either kind, resets the noise
  shift register to `&4000`.
- **Time (s4.3).** The chip runs on the video ULA's 4 MHz divided by 16: a chip
  clock at 250 kHz, every eighth CPU cycle. Each tone's counter counts down and
  toggles a flip-flop at zero, so a period of n is 125000 / n Hz. The noise has
  its own counter, reloaded with 16, 32 or 64 clocks, or with tone 3's period
  for rate 3, and the 15-bit shift register shifts each time the noise's
  flip-flop rises, taking bit 0 XOR bit 1 into bit 14 for white noise and bit 0
  alone for periodic. Bit 0 is the noise channel's output.
- **Output (s4.5, s4.6).** Each channel swings between 0 and its amplitude,
  10^(-2n/20) for attenuation n, not between minus and plus: on a real chip the
  mean level moves with the volume, which is what sampled sound on the BBC relies
  on. The mix is the four summed and divided by four, so 0 to 1.

### Decision: the chip is never ticked, and names no event

The controller's design for this task. The SN76489's READY line is wired to 0 V
on the BBC and software cannot see it (s4.1), so nothing the chip does can reach
the CPU. It therefore has no term in the bus's minimum, no case in the decode
(it is not on the bus at all: only the system VIA writes it), and nothing to do
on any cycle. It keeps the chip clock it has reached and catches up when it is
written to, when anything looks at it, and when its buffer is read. Chosen over
two things the brief allowed: ticking it at 250 kHz from the bus, which would put
work back into every eighth cycle that task 6b took out, and a regular "sound
pump" event in the bus to make samples as the machine runs, which is not needed
because the buffer keeps only the newest second (below) and the page reads it
every frame.

### Decision: a span of clocks is worked out at once

A tone channel between two writes is a counter and a flip-flop, so over any span
of clocks the number of clocks it is high, its new count and its new level come
from the span's length in a few divisions: the clocks before its first toggle,
then stretches of one period, alternating. The noise's output changes only at a
shift, and there are at most 125,000 shifts a second (rate 3 with tone 3 at
period 1), usually 7,812 or fewer, so the shift register is stepped once a
shift; when the noise is silent and only its state matters, the number of shifts
is first reduced by the register's own period, 32,767 for white noise (a maximal
15-bit register) and 15 for periodic (a rotation), since a write always reseeds
it and it is never 0. No jump-ahead table was needed.

The samples are made a sample at a time from these spans, about five clocks
each. When all four channels are off, which at the prompt is nearly always, a
run of whole samples is silence and is written in one go while the channels'
state moves on at once.

### Decision: a sample is the mean of whole chip clocks, at any rate

Sample j at S samples a second is the mean of the mix after each chip clock from
floor(j x 250000 / S) + 1 to floor((j + 1) x 250000 / S): a box average, five or
six clocks at 48 kHz, worked with whole numbers so no clock is lost or counted
twice over any length of run. The plan asked for "an integer-ratio resample
chosen in the journal", and the controller's design allowed an integer ratio or
a fractional accumulator. The integer ratios of 250 kHz are 50 kHz (5 clocks a
sample) or 41.67 kHz (6), and neither is a rate an `AudioContext` runs at, so
the browser would resample a second time. Sampling the mix at one clock a sample
was the other way, and was rejected because a tone at period 1, 125 kHz, would
alias into noise, where a real BBC's filters make it a steady level at half the
amplitude (s4.3) and the box average makes it the same, give or take a tenth of
the amplitude for the five-clock samples. The sample rate is `BbcOptions.SampleRate`,
48,000 unless the page gives its audio context's.

### Decision: the levels are whole numbers out of 32767

The mix is summed from the sheet's x 32767 column (s4.5), not from floating
point amplitudes, so a sample's sum is exact however its clocks are grouped, and
the lazy chip and a clock-by-clock one give the same float to the last bit.
Chosen over float sums, which would make the comparison a tolerance. The
rounding costs at most half a level in 32767; the sheet's per-channel share at
attenuation 14 is 0.0100 to four places and the model's is 0.00995, which the
test allows for.

### Decision: the buffer keeps the newest second, and drops the oldest

`SoundBuffer` is a ring of one second of samples. When it is full the oldest
sample goes and `Overruns` counts it. A page that reads it every frame (50 a
second) never fills it; a page with its sound off can leave it, and reads the
last second when it starts. Because what would be dropped is known in advance,
a span that would make more than a second of samples makes only the newest
second's, and moves the channels' state over the rest at once. Running short is
left to the page to count: the buffer cannot tell a short read from a page that
asked for more than it needed.

### Decision: a write lands at once, when latch bit 0 falls

The system VIA already raised `SoundWrite` with the byte on PA when latch bit 0
fell (task 4). The chip catches up to the chip clock of that CPU cycle and takes
the byte, so the output changes from the next clock, in the middle of a sample
if it falls there. The real chip takes about 32 of its clocks to load and was
measured answering 3.7 to 8 microseconds after write enable fell (s4.1). The
OS's routine at `&EB21` holds write enable low for 17 cycles and then pads 21
more before it can write again, so it never writes inside that window, and
taking the byte at once changes nothing the OS does: that is all that was
modelled. Not modelled, and in `known-differences.md`: the real chip taking the
byte again every 16 microseconds while write enable is held low, which some
sample-playing code uses. Where the chip clock falls against the CPU clock is not
documented; it is taken to be at the end of every CPU cycle that is a multiple
of eight.

### Decision: a period of 0 counts as 1024

The sheet could not establish what a real chip does with period 0 (SMS Power
says a constant level, like 1; jsbeeb, a cross-check only, says 1024) and
recommends 1024, because a 10-bit counter loaded with 0 would pass 1023 before
it reached zero. The model takes 1024, for tone 3 feeding the noise too, and
`known-differences.md` says so. The OS never writes 0: its pitch tables give 25
at the least.

### Smaller choices

- **Power on is silent**: every period and the noise control 0, the counters at
  1024 and 16, the flip-flops low, the shift register at `&4000`. A real chip
  starts in an unknown state; the OS silences it a quarter of a second after
  power on. The chip has no reset pin, so BREAK leaves it.
- **Rate 3 noise** has its own counter reloaded with tone 3's period, as SMS
  Power describes it, not a shift on tone 3's own edges; the rate is the same.
- **A write to the noise control** resets the shift register and not the noise
  counter or flip-flop, on which no source says anything.

## What the OS turned out to do

**The fact sheet's reset sequence was short.** Section 4.7 said the reset code
at `&EC60` writes `&9F, &BF, &DF, &FF`, the four attenuations. The first test
asserted that and failed: the strobes the machine saw were `&9F, &82, &3F, &BF,
&A1, &3F, &DF, &C0, &3F, &FF, &E0`. Disassembling `&ECA2` (with the fact sheet's
own `dis.py`) showed why: after the attenuation it stores pitch 0 for the
channel and always reaches `&ED06` (a `BMI` at `&ECBA`, with Y at `&FF` from the
loop before it), which writes that pitch, so each tone gets period 1008 plus its
detune (1010, 1009 and 1008 for chip tones 1, 2 and 3) and the noise gets
`&E0`, periodic at rate 0. The sheet is corrected, and the test now asserts the
eleven bytes from the ROM and the periods they leave.

**The OS writes a pitch only when it changes.** `&ED01` compares the new pitch
with the channel's last and skips the write when they match. So in the test of
the sheet's pitch table, `SOUND 1,-15,0,1` wrote no period at all, because the
reset had already written pitch 0 to that channel, and the test expected one.
The test now checks that the period is 1008 before the program runs and expects
written periods only for the pitches that change it. That row is added to the
sheet too.

**The queues play at once.** The same test first expected the periods of all
three channels in one sequence, and the SOUND 2 and SOUND 3 notes at the end
came out between the first SOUND 1 notes, because each channel has its own
queue. The test now checks each channel's sequence: chip tone 3 gets 994, 951,
504, 475, 469, 237 and 25 for pitches 1, 4, 48, 52, 53, 100 and 255, chip tone 2
gets 470 and chip tone 1 471 for pitch 53, as s4.8 says.

**The start-up beep** is on chip tone 1, the OS's channel 3. A throwaway probe
(`/tmp/t11probe`, not committed) that logged every strobe with its cycle from
power on showed the reset writes at about 512,300 cycles, then `&92`
(attenuation 2), `&8F, &0E` (period 239, 523 Hz) at 551,906 and 554,290, and
`&9F` at 1,132,748: the beep starts about 0.28 seconds after power on and lasts
about 0.29.

## The tests, and whether they can fail

Written first and run against no chip (they did not compile), then against the
chip. `Sn76489Tests` asserts the sheet's worked examples: every row of the tone
formula table on all three tones (the period, and a toggle every n clocks at the
table's frequency), every register byte test, the noise rate table, the noise
vectors of s4.4 for white and periodic noise, the white register's period of
32,767 shifts and the periodic one's 15, rate 3 following tone 3, the volume
table at each of its sixteen rows (the amplitude to six places, the level out of
32767, and the mix), the unipolar swing, period 0, the resample's arithmetic,
the buffer's ring, and that a chip in a machine refuses `Tick`. `SoundTests`, on
the bus and the real OS: only latch bit 0 falling writes the chip (not rising,
not a second low, not another latch bit, not a port A write); a write lands in
the sample of its chip clock; the eleven reset bytes; the beep makes sound in the
first second; the sheet's pitch table through BASIC; and `SOUND 1,-15,100,20`
typed at the keyboard sets chip tone 3 to period 237 at full volume, its output
swings between 0 and 1, and the samples, read off at their rising edges, are a
square wave at 527.43 Hz give or take 3.

**Equivalence.** `Oracle/ReferenceSn76489.cs` is the chip the plain way, a clock
at a time, written from the model above rather than from the lazy code. Two
comparisons, both sample by sample, exactly:

- the chip on its own against it, through eight runs of 4,000 random steps at
  six sample rates from 7 to 250,000 a second: random writes of every kind
  (short periods, every noise mode, mostly audible attenuations), single ticks,
  and spans from 0 clocks to 700,000, longer than the buffer's second, with the
  buffer read at random moments in random amounts. After every step the clock,
  the shift register, each channel's level and attenuation must match, and each
  read must give the oracle's samples, through a model of the ring's dropping
  rule, with the same overrun count. Each run must have compared samples both
  sounded and silent and overflowed the buffer at least once;
- the machine against it on the real OS: power on, the beep, then an `ENVELOPE`
  and a `FOR` loop of `SOUND` commands on all four channels typed a key at a
  time, with the buffer read between keys and then at 400 random moments. Every
  byte the system VIA strobes goes to the oracle at the chip clock of its cycle.

The fingerprint of task 6b's scripted run (`bench/bbc-micro-speed/native
--fingerprint`, which hashes every instruction and, every 100,000 cycles, all of
RAM and every VIA register) is identical before and after this task, as it
should be: the chip cannot be seen by the CPU.

**Planting mistakes.** Twenty-three deliberate one-line mistakes, each built and
run against the sound tests in a scratch copy of the tree
(`python3 /tmp/t11mut/mutate.py`, a scratch script, not committed). Twenty-two
were caught at once. What caught each:

| Mistake | Caught by |
| --- | --- |
| A tone's high clocks counting the last part-period the wrong way round | both equivalence tests |
| A tone toggling on the wrong parity of its periods | seven tests, the sheet's tone table among them |
| A tone's count one clock long after a toggle | five, the tone table and the `SOUND` command among them |
| A period of 0 taken as 1023 | the period-0 test and both equivalence tests |
| The number of noise shifts in a span miscounted | both equivalence tests |
| White noise tapping bits 0 and 3 (the SMS chip's) | the white noise vector, its period, both equivalence tests |
| The seed `&2000` | both noise vectors, the power-on test, both equivalence tests |
| Rate 0 noise reloading 32 | the noise rate table, the white period, both equivalence tests |
| A data byte after an attenuation latch ignored | the sheet's register test, the chip equivalence test |
| Attenuation 7 at 6568, SMS Power's typo (s4.5) | the volume table, both equivalence tests |
| One silent sample too few in a silent run | five, the resample's arithmetic and the write's sample among them |
| A span longer than the buffer not counting what it dropped | the chip equivalence test |
| A span longer than the buffer keeping one sample too few | the chip equivalence test |
| One sample a clock too long | the chip equivalence test |
| The strobe taken when latch bit 0 rises | the write protocol test, the write's sample, the reset bytes |
| The machine's chip clock rounded up from the CPU cycle | the write's sample, the machine equivalence test (re-planted in the review round: the rewritten write-timing test, with writes in the last even cycle of a clock, and the machine equivalence test) |
| A noise stretch a clock too long | both equivalence tests |
| A write taken before the chip catches up | the write's sample, the beep, the machine equivalence test |
| The noise flip-flop's level after a span the wrong way round | the noise rate tests, both equivalence tests |
| Periodic noise reduced by 16 shifts in place of 15 | both equivalence tests |
| White noise reduced by 32766 shifts in place of 32767 (review round) | the long silent noise test only |
| White noise reduced by 32768 (review round) | the long silent noise test only |
| Periodic noise reduced by 14 (review round) | the long silent noise test and both equivalence tests |
| The machine's chip clock one CPU cycle behind (review round) | the rewritten write-timing test, the machine equivalence test |
| The buffer's read not wrapping round the ring | the buffer's test, both equivalence tests |
| The mix not divided by four | six, the samples of the `SOUND` command among them |

The one that got through was **the power-on reset not reaching the chip**. A
machine is built with its chip already in the power-on state, so leaving the
chip out of `PowerOnReset` changes nothing until the machine is switched off and
on again while a note plays, which no test did. A test now does
(`SwitchingOffAndOnSilencesTheChipButBreakDoesNot`, which also checks that BREAK
leaves the chip alone), and catches it.

**Two mistakes of my own.** The first mutation run hung: the volume table test
waited for a channel's flip-flop to go high with no limit, and a mutant that
never toggled waited for ever, as the CRTC's first mutation run had. Every loop
in the tests now gives up and fails. Then, killing the hung run, a second copy
of the script was started in the same scratch tree while the first was still
alive, and the two wrote their mistakes into the same files: those results were
thrown away and the pass run again from a clean copy, once. The figures above
are that run's. And the first draft of two tests forgot that a period written
to the chip takes effect only when the counter from before runs out, which from
power on is 1024 clocks, and looked for the new tone too soon: one failed for
it (`AChannelSwingsBetweenZeroAndItsAmplitude`), and the other was put right
before it first ran.

`dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release`: 62 new test cases,
853 in the project, all passing; `dotnet test -c Release` for the whole
solution, all passing.

### The review round

The reviewer found the one gap that matters. **The chip's single shortcut was
never tested.** While the noise is silent only its state matters, so the number
of shifts in a span is reduced by the register's period before it is stepped
(32767 for white noise, 15 for periodic). The reviewer changed 32767 to 32766
and all eight runs of the random equivalence test still passed, while the
reviewer's own adversarial test failed on all twelve of its seeds. The random
test's longest span, 700,000 clocks, is at most 21,875 shifts at noise rates 0
to 2, so a silent span never reached a whole period of white noise; and the
spans that did at rate 3 were sounded, which steps every shift and never takes
the shortcut. My mutation pass had planted 16 for the periodic modulus, which
the random test caught, and never the white one. A new test,
`LongSilentNoiseSpansMatchTheOracle`, runs every noise control (white and
periodic, rates 0 to 3, tone 3 short for rate 3) with the noise silent, six
spans each of more than a whole period of shifts and not a whole number of
them, alternately with everything else off (the run of silent samples) and
with the tones sounding over more than the buffer's second (the samples it
drops), on a chip with a buffer and one without, against the oracle, which now
counts its shifts. It asserts that some span made at least a whole period of
shifts and that some span did not make a whole number of periods. With it the reviewer's mutant fails, and so do 32768 in place of 32767 and 14
or 16 in place of 15, each planted in the scratch copy and reverted
(`python3 /tmp/t11mut/mutate.py`, rows marked "review round" in the table
above). The white ones are caught by the new test alone, which is the gap it
closes.

The smaller points, all taken:

- The write-timing test asserted that the chip had done `cycle / 8` clocks
  after the write, which could never fail, because reading the count catches
  the chip up to now; and it allowed the write's own sample anything from 0 to
  0.125, so a write a clock early or late in that sample got through. It is
  rewritten: it waits for periodic noise at rate 0 to raise its output for one
  shift, 32 clocks in which the level cannot move (s4.4, s4.2), then times the
  strobe into clock 0, 3 and 7 of a sample, each with the write in the first
  and in the last even CPU cycle of its clock, and expects exactly 8, 5 and 1
  clocks of a quarter each, from the s4.3 clock and the sample rule. The first
  version of the rewrite timed every write into the first cycle of its clock,
  where rounding the CPU cycle up and rounding it down give the same clock, and
  the rounded-up mutant got past it; the last-cycle cases catch it.
- `SoundBuffer.Overruns` did not catch the chip up before answering, as `Count`
  and `Read` do, so a page reading it first got an old figure. It does now, and
  a test reads it with no read before it.
- The sheet's s4.8 row for `&9F, &BF, &DF, &FF` still called them the OS reset
  sequence; it now says all four channels off and points to s4.7.
- The bench's `tone` load now stops with an error if any field's samples are all
  silent, so a figure for it cannot come from a load the OS had quietly turned
  off.

`dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release` after the round:
867 test cases, all passing (14 more: eight for the long silent noise, five more write-timing cases, and the overruns test).

## The speed

**What changed in the hot path: nothing.** The bus's `Read` and `Write` are as
they were; the chip has no term in `Service()`; the only new work at all is
when the OS writes the chip (a catch-up and a byte) and when the buffer is read.
The speed check's standard workload never reads the buffer, so it pays for the
OS's writes and nothing else. So the before and after builds should run at the
same speed, and the question is only whether the machine still makes ten times a
real Model B in the browser.

**The figures, and the trouble with them.** The commit before this task
(`0197aa5`, published from an exported copy) and this task's code, both
compiled ahead of time and natively, run alternately with
`bench/bbc-micro-speed/alternate.sh`, from 22:11 UTC on 3 October. The shared
machine was busy: other sessions were compiling and a database backup was
running, at one-minute load averages from 0.6 to 17. Medians in millions of
cycles a second, with multiples of 2 MHz:

| Set | Load (1 min), before to after | Before this task | With the sound chip |
| --- | --- | --- | --- |
| Browser AOT, old first, 22:11 | 4.23 to 2.70 | 18.779 (9.39 times) | 14.641 (7.32 times) |
| Browser AOT, new first, 22:12 | 5.37 to 10.20 | 18.349 (9.17 times) | 19.249 (9.62 times) |
| Browser AOT, old first, 22:13 | 10.20 to 12.50 | 22.650 (11.32 times) | 22.075 (11.04 times) |
| Browser AOT, new first, 22:13 | 12.50 to 17.04 | 16.556 (8.28 times) | 16.779 (8.39 times) |
| Browser AOT, old first, 22:14 | 17.04 to 15.85 | 18.034 (9.02 times) | 15.886 (7.94 times) |
| Browser AOT, old first, 22:35 | 1.60 to 1.29 | 15.835 (7.92 times) | 15.408 (7.70 times) |
| Browser AOT, new first, 22:36 | 1.29 to 0.78 | 13.850 (6.92 times) | 18.886 (9.44 times) |
| Browser AOT, old first, 22:46 | 2.98 to 7.11 | 12.392 (6.20 times) | 17.841 (8.92 times) |
| Browser AOT, new first, 22:46 | 7.11 to 9.52 | 13.680 (6.84 times) | 22.222 (11.11 times) |
| Native, old first, 22:11 | 6.33 to 4.81 | 24.450 | 24.981 |
| Native, old first, 22:37 | 0.58 to 0.57 | 26.605 | 25.724 |
| Native, new first, 22:37 | 0.57 to 3.27 | 27.370 | 28.561 |

The two builds are level within the noise, which is what the code says they
should be. *Reworded in the review round: this said "within the spread of a set",
which no figure here gives.* The new code's median over the old's, set by set in
the order of the browser rows, is 0.78, 1.05, 0.97, 1.01, 0.88, 0.97, 1.36, 1.44
and 1.62 (worked in Python from the medians above; the reviewer had 0.98 for the
third, which is 0.9746 rounded the other way). The median of the old code's nine
medians is 8.28 times 2 MHz and of the new code's 8.92, and the median ratio is
1.01. The last two sets put the new code 44 and 62 per cent ahead, which is the
host, not the code: nothing on the CPU's path changed. **But both are under ten times in most sets, the old code
included, which the earlier tasks measured at twelve to fourteen.** The load
average does not explain it: the 22:35 sets ran at a load of about one. The
machine itself was slow. The bench's `--profile` mode, run on the old code at
22:36 UTC (load 0.72), put the bare CPU on a flat copy of memory, which has no
bus and no chips, at 15.52 to 24.54 ns a cycle (median 20.86), where task 6b's
journal measured 10.49 to 11.04 on the same virtual machine in the morning; and
the native timed runs above were bimodal, best runs about 65 MHz in every set
and medians about 26. The host behind the virtual machine was giving it about
half the speed it had in the morning, which the guest's load average cannot see.
The last two browser sets, after the commit, were taken when a profile at 22:46
(load 2.40) put the bare CPU at 12.26 to 23.58 ns a cycle; one straight after
them (load about 9) gave 19.16 to 26.32. The ten-times question for this task is
therefore not settled by these figures; it wants a set in a quiet hour.

**What the sound itself costs,** measured so that the load matters less: a
throwaway probe (`/tmp/t11probe`, not committed) timed one second of machine
time, best of many, at 22:15 UTC (load about 13). The chip on its own, with all
four channels sounding and read every field: 1.145 ms a second of machine time
(best of 60); silent: 0.022 ms. The whole machine at the prompt, best of 25:
32.3 and 33.5 ms a second of machine time with the buffer never read, 31.2 and
42.6 read every field with nothing playing, and 33.2 and 34.7 read every field
with all four channels sounding. So a page playing four channels at once pays
about three and a half per cent of the machine's time for its sound, and a page
whose sound is silent pays nothing that can be measured. The bench can now time
that load itself (`SOUND=silent` or `SOUND=tone`, in the bench's README).
