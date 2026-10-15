---
title: "The Atari VCS's sound: the two channels, the samples and the resampler"
date: 2026-10-14
summary: "The tenth task of the Atari VCS: the TIA's two sound channels, each a frequency divider and two polynomial counters, clocked twice a line; their mix; and the samples made from it at the host's rate. The resampler was chosen by a rule written down and committed before anything was measured."
order: 64
---

# 14 October 2026: the Atari 2600's sound

Task 10 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): the TIA's two sound channels, built from [`tia.md`](../atari-2600/facts/tia.md) section 18, the samples at the host's rate, and the resampler, chosen by measurement. Dan decided on 14 October 2026 to carry on past the speed milestone's STOP ([the task 8 entry](2026-10-14-the-atari-2600-speed-milestone.md)), so the sound is built to cost as little as it can: it does its work twice a line, when the channels are clocked, and nothing on the colour clocks between.

## The resampler's rule, fixed before measuring

This section was written and committed before any resampler was built or measured. Nothing in it is changed after a result is seen.

The plan's item 12 of "Fixed before running": **the box filter is kept if, for `AUDC` 4 at `AUDF` 0, 15 and 31 and volume 15, in both regions, the strongest component that is not a harmonic of the tone is at least 40 dB below the fundamental at 48,000 samples a second; otherwise the band-limited step is used.** What it leaves open is fixed here.

- **The signal.** A `Tia` alone, with no CPU, from power on: `AUDC0` = 4, `AUDV0` = 15, `AUDF0` = 0, 15 or 31, written before the first colour clock, and `AUDV1` = 0. It is ticked one colour clock at a time, and each change of its mixed level reaches both resamplers at the colour clock it happens on. The colour clock is the region's `Region.ColourClockHz`; the sample rate is 48,000.
- **The two resamplers.** The **box filter**: each sample is the mean of the level over its own sample period, worked out exactly from the colour clocks at which the level changes. The **band-limited step**: each change of the level is added to the output as a step whose edge is shaped by the NES's kernel (`src/Dbhq.Machines.Nes/SampleBuffer.cs`): a windowed sinc with a Kaiser window, beta 9, 16 samples each side, cut off at 0.42 of the sample rate, the edge placed to 1/4096 of a sample.
- **What is measured.** From power on, the first 2,048 samples of each resampler are thrown away (the start, and the step's latency), and the next 65,536 are measured. Their spectrum is the NES's method (`tests/Dbhq.Machines.Nes.Tests/AudioMeasure.cs`): the mean removed, a four-term Blackman-Harris window, and the magnitude of the FFT.
- **The tone and its harmonics.** `AUDC` 4 toggles its output on each divided pulse (`tia.md` section 18.5), and the divided pulse comes every `AUDF` + 1 audio steps (section 18.3). Two audio steps come in each line, so the tone's period is `AUDF` + 1 lines and its fundamental is `ColourClockHz` / (228 x (`AUDF` + 1)). The fundamental's level is the largest bin within 8 bins of it. A bin is a harmonic if it is within 8 bins of a whole multiple of the fundamental, 0 Hz included; every other bin up to the Nyquist, 24,000 Hz, is a component that is not a harmonic.
- **The figure.** For each case, 20 log10 of the largest bin that is not a harmonic over the fundamental's level, in dB. A case meets the line when its figure is -40 dB or lower.
- **The choice.** The box filter is kept only if it meets the line in all six cases (three `AUDF` values, two regions). Otherwise the band-limited step is kept. Both are measured in every case, and the figures are recorded here with the command.
- **If the one kept misses the line**, it is kept all the same, as item 12 says; the test that asserts the line fails, and Dan is told. No parameter of either resampler is changed after a figure is seen unless a new rule is written down first.
- **The command:** `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter FullyQualifiedName~ResamplerTests --logger "console;verbosity=detailed"`.

## What was built

New: `TiaSound` and `SampleBuffer`, and in the tests `TiaSoundTests`, `SampleBufferTests`, `ResamplerTests` and `AudioMeasure` (the NES's spectrum, copied). Changed: `Tia` (the six audio registers, the audio clock, the mix), `Atari2600Bus` (`Sound`, `CatchUpSound`, the region switch and power on), `Atari2600` (`Sound`, and the catch-up at the end of `Run` and `RunFrames`), `Atari2600Options` and the WebAssembly host's comments.

- **The channels.** `TiaSound` holds each channel's nine bits and its five-bit divider. On each audio step the divider is compared with `AUDF` (at the step itself: jigo2600's reading puts a latch between the compare and the pulse, which is not modelled `[guessing - verify]`): on a match it makes a divided pulse and goes to 0, otherwise it counts on, modulo 32. A pulse steps the noise-tone generator by `tia.md` section 18.4's rules, every new bit from the bits before it; the output is bit 8. The rules are one small function, `Pulse`, of bit operations on an integer.
- **The audio clock.** The audio clock's phase two is a decode of the horizontal sync counter (section 18.2), so the `Tia` steps both channels in the counter's own step, the one that leaves count 8 or count 36: the new bits hold from colour clocks 36 and 148. This adds one comparison to a step that runs every fourth colour clock, and nothing to the colour clocks themselves. RSYNC moves the audio clock with the line, because it resets the same counter; HMOVE does not, because it chooses between two decodes of it and not the counter.
- **The mix and the level.** `s` = `AUDV0` x bit 0 + `AUDV1` x bit 1, and the level is `s` / 30, as section 18.7 gives it; that the mix is linear is a guess `[guessing - verify]`. It is worked out at each audio step and at each volume write.
- **The samples.** The `Tia` tells the bus's `SampleBuffer` only when the level may change, at an audio step or a volume write, with the colour clocks since it last told it, counted from the line's own colour clock and carried over each line's start. The buffer counts time in whole colour clocks, so which samples exist depends only on the clocks that have passed. `Atari2600.Run` and `RunFrames` end by bringing the samples up to now, and the region switch does the same before it changes the clock.
- **The ring** holds a quarter of a second, as the NES's does: a page reads it once a frame, about every sixtieth of a second, so a quarter of a second rides out a slow frame or two, and a hidden tab that stops reading loses the oldest sound and no memory. Adding allocates nothing.
- **The WebAssembly host** already passed the sample rate to the machine; only its comment changed. Handing the samples to the page is task 13's, so no export was added.

## Decisions

- **The TIA tells the sample buffer at its steps, not every cycle.** The plan's interface had `Add(double level)` "called as the channels are clocked"; that call cannot say how long a level held, because the steps are 112 and 116 colour clocks apart, and a volume write can come at any clock. So `Add` takes the colour clocks too. Chosen over a call every CPU cycle, the NES's way, which would cost about a million calls a second of machine time where this costs about thirty thousand (two steps a line); after the speed milestone's STOP, the sound was to cost as little as it could. The plan is corrected.
- **The samples reach the end of each run.** Made only at the steps, they would end up to 116 colour clocks short of the machine's time, which is about one and a half samples at 48,000 a second, and item 11's count could then be two short. So `Run` and `RunFrames` call `CatchUpSound`, which adds the colour clocks since the last step at the level that held; it changes no level and nothing a program can see, only the TIA's note of where it last told the buffer. Because time is counted in whole colour clocks, a run made in 997-cycle pieces gives the same samples, to the bit, as one run (`SampleBufferTests`). Chosen over leaving the samples short, which a page would never hear but which crossed a fixed check, and over a call every cycle. `Step` alone does not catch up.
- **A volume write takes effect at once**, from the colour clock it lands on, and not at the next audio step. The schematic's drivers are gated by the `AUDV` bits directly, and nothing read says they wait for the audio clock. It is what lets a program play a sample by writing the volume, which a write held to the next step would quantise to the audio clock. Recorded as a guess `[guessing - verify]`.
- **The divider's worked example in section 18.6 was off by one**, and is corrected: with the counter at 20 and `AUDF` written as 5, the count reaches 5 on the 17th step and the pulse comes on the 18th. The rule that makes a steady `AUDF` divide by `AUDF` + 1 gives 18, and jigo2600 Sound's sequence, read again for this (facts only), says the same.
- **No filter is modelled.** The NES's samples pass the console's high-passes and low-pass, which its fact sheet gives; the 2600's sheet gives none, so its samples run from 0 to 1 with a steady offset while a level is held `[guessing - verify]`. Task 13 decides what the page does with the offset.
- **The kernel is the NES's**, built the same way and with the same parameters, because the rule named it before measuring.

## The waveforms, against the sheet

`TiaSoundTests` takes section 18.5's table as it stands: for every `AUDC` value, on each channel, the first 64 output bits from all nine bits 1 with `AUDF` = 0, and, past a start-up of 600 pulses, the period and the count of 1s in it. All sixteen rows match the sheet on the first run, which is the check that the C# rules are the rules the sheet's probe (`tools/probes/atari-2600/tia_sound.py`) stepped to make the table; the table's own check against Korth's drawings and Stolberg's sequences is the sheet's. Then section 18.3's divider (the output at `AUDF` f is the undivided output held f + 1 steps), section 18.6's worked example as corrected, the power-on choice, section 18.7's three worked mixes, volume 0 silent for every `AUDC` and 15 the loudest, the steps at colour clocks 36 and 148 on both channels with RSYNC and HMOVE, and the volume write at once.

## The resampler, measured by the rule

The rule was committed first (`6034a7a`, 21:16 UTC); the two resamplers were committed after it. One deviation from the rule's signal: the test's feed also passes a held level to the resamplers every 1,024 colour clocks, where the rule says each change of the level reaches them at the clock it happens on (below, and task 10's review). A held level adds no step to the band-limited step and splits one interval of the box filter's sum in two, so neither resampler's output changes by it. The figures, in dB under the fundamental, for the strongest component that is not a harmonic up to 24,000 Hz:

| Region, `AUDF` | Fundamental | Band-limited step | Box filter |
|---|---|---|---|
| NTSC, 0 | 15699.8 Hz | -82.4 | -27.0 |
| NTSC, 15 | 981.2 Hz | -96.2 | -31.9 |
| NTSC, 31 | 490.6 Hz | -95.4 | -37.7 |
| PAL, 0 | 15556.6 Hz | -82.9 | -27.8 |
| PAL, 15 | 972.3 Hz | -93.9 | -31.5 |
| PAL, 31 | 486.1 Hz | -97.0 | -38.2 |

[`dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter FullyQualifiedName~ResamplerTests --logger "console;verbosity=detailed"`, 14 October 2026.] The box filter misses the line of -40 dB in all six cases, so by the rule **the band-limited step is kept**, and it meets the line in all six with 42 dB or more to spare. The box filter is worst at `AUDF` 0, where the tone is near the Nyquist and its harmonics fold straight back. `ResamplerTests` asserts the line for the sample buffer in every case and fails if the box filter ever meets it in all six, because the rule would then keep it.

The measurement was run twice, with the same figures. Between the runs the test's feed was changed so that a held level also reaches the resamplers every 1,024 colour clocks, and its loop was bounded: the fault pass below planted a tone that never changes, and the first feed, which passed the level on only when it changed, waited for ever. A held level adds no step to the band-limited step and splits one interval of the box filter's sum in two.

## The tests, test first

`TiaSoundTests`, `SampleBufferTests` and `ResamplerTests` were written from `tia.md` section 18, the plan's items 11 and 12 and its Review Focus 5, and `StateReportTests` was told to expect `TiaSound` and `SampleBuffer`. On 14 October 2026, before `TiaSound` and `SampleBuffer` existed, `dotnet build -c Release tests/Dbhq.Machines.Atari2600.Tests` failed on the missing `SampleBuffer`. With the code, `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~TiaSoundTests|FullyQualifiedName~SampleBufferTests|FullyQualifiedName~ResamplerTests|FullyQualifiedName~StateReportTests"` passed every case on the first run, and so did the whole project; the fault pass below shows each file able to fail, and found six holes, which are now tested. Then `dotnet build -c Release` of the solution gave no warnings, and `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests` passed every case.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/sound-faults.py <scratch>`), one fault at a time, building and running the whole Atari 2600 test project. The figure in brackets is the number of failing test cases in each file, from the run on the tests as they stood before the six were added; the six were run again afterwards (`... sound-faults.py <scratch> N5 N10 N11 T6 B8 B9`), and that run's figures are given for them.

| Fault | Planted | Test files that failed |
|---|---|---|
| N1 | the five-bit counter's tap moved from bit 2 to bit 3 | `TiaSoundTests` (16) |
| N2 | the four-bit counter's tap moved from bit 7 to bit 6 | `TiaSoundTests` (5) |
| N3 | the nine-bit chain's tap moved from bit 4 to bit 3 | `TiaSoundTests` (2) |
| N4 | the divide by 2 not toggling | `ResamplerTests` (7), `TiaSoundTests` (12) |
| N5 | the divide by 6 leaving the wrong unused state | none at first; after a test was added, `TiaSoundTests` (2) |
| N6 | the four-bit counter clocked at state 8 only | `TiaSoundTests` (7) |
| N7 | the four-bit counter clocked by bit 3 of the five-bit counter | `TiaSoundTests` (8) |
| N8 | the divider restarting at 1, dividing by `AUDF` | `ResamplerTests` (6), `TiaSoundTests` (22) |
| N9 | the divider counting modulo 31 | `ResamplerTests` (2), `TiaSoundTests` (2) |
| N10 | `AUDF`'s top three bits used | none at first; after a test was added, `TiaSoundTests` (1) |
| N11 | `AUDC`'s top four bits used | none at first; after a test was added, `TiaSoundTests` (1) |
| N12 | power on with all nine bits 0 | `TiaSoundTests` (19) |
| N13 | `AUDV`'s top four bits used | `TiaSoundTests` (1) |
| N14 | the level over 15, not 30 | `TiaSoundTests` (6) |
| T1 | channel 1 stepped with channel 0's `AUDC` | `TiaSoundTests` (1) |
| T2 | the channels stepped once a line | `ResamplerTests` (6), `TiaSoundTests` (3) |
| T3 | the first step at LRHB, not RHS | `TiaSoundTests` (3) |
| T4 | a volume write waits for the next step | `TiaSoundTests` (1) |
| T5 | the colour clocks at a line's start lost | `SampleBufferTests` (13) |
| T6 | power on keeps the level | none at first; after a test was changed, `TiaSoundTests` (1) |
| B1 | the sample period's colour clocks a thousandth out | `ResamplerTests` (6), `SampleBufferTests` (13) |
| B2 | an instant emitted one late | `ResamplerTests` (6), `SampleBufferTests` (1) |
| B3 | a full ring keeps its oldest place | `SampleBufferTests` (1) |
| B4 | dropped samples not counted | `SampleBufferTests` (13) |
| B5 | a step's edge placed at the wrong end of its sample | `ResamplerTests` (6) |
| B6 | a kernel two samples wide | `ResamplerTests` (2) |
| B7 | the clock changed with no re-base | `SampleBufferTests` (3) |
| B8 | clear keeps the level | none at first; after a test was added, `SampleBufferTests` (1) |
| B9 | clear keeps the steps under way | none at first; after a test was added, `SampleBufferTests` (1) |
| M1 | no catch-up at the end of `Run` | `SampleBufferTests` (2) |
| M2 | the region switch not passed to the sound | `SampleBufferTests` (2) |
| M3 | power on keeps the samples | `Atari2600BusTests` (2), `SampleBufferTests` (1) |
| M4 | the sample buffer a tenth of a second | `SampleBufferTests` (1) |
| S1 | the TIA does not report its mix | `StateReportTests` (9) |
| S2 | the TIA does not report its channels | `StateReportTests` (9) |
| S3 | the channels do not report a divider | `StateReportTests` (9) |
| S4 | the sample buffer does not report its level | `StateReportTests` (9) |
| S5 | the bus does not report the sample buffer | `StateReportTests` (9) |

In all, 38 faults were planted and 32 were caught on the first run. The six that survived, and what now catches them:

- **N5.** From all nine bits 1 the divide by 6 never enters its two unused states, so no test ran its escape. `Section_18_4_the_divide_by_6_leaves_its_two_unused_states_within_two_pulses` starts from each.
- **N10 and N11.** No test wrote an `AUDF` or `AUDC` with its unused top bits set. `Section_2_1_the_audio_registers_use_their_low_bits_only` runs two TIAs, one written with those bits set, and expects the same level at every colour clock.
- **T6.** The power-on test happened to stop at a 0 bit, so the level was 0 before power on as well. It now holds a level on the other channel first.
- **B8 and B9.** No test fed a cleared buffer anything. `Clear_starts_afresh_so_silence_after_it_is_exactly_zero` clears it just after a step and expects exact zeros.

The pass was first started with tests whose loops waited for a level change; the fault N4, a tone that never changes, made two of them wait for ever, and that run was stopped. Their loops are now bounded (above), and the table is from the runs after that.

The site's tests (`cd site && npm test`, 14 October 2026) pass every journal and copy check; the ten that fail here need the machines' WebAssembly built into the site, which this box does not do, and they are the same ten that failed in task 9's run.

## The speed, after the sound

The plan asks for task 8's workflow to be run again after the sound, and its figure recorded beside the first. It is in [the speed milestone's entry](2026-10-14-the-atari-2600-speed-milestone.md), "The run again after the sound, and a control", with both runs' links, commits and hosts: run 38646805950 on `0ea88f2` gave a lower AOT median of 4.52 times real time, still STOP, on an AMD EPYC 7763, where task 8's 8.22 was on an EPYC 9V45; a control run of the commit before the sound on the same processor model gave 4.80. Nothing was timed on this box.

## Task 10's review

A review of task 10 (up to `fe363f6`) found eight things, given here in five groups and folded in as one commit before task 11 with no change of behaviour:

- **A test that could not fail.** `TiaSoundTests.Section_18_2_RSYNC_moves_the_audio_clocks_with_the_line_and_HMOVE_does_not` wrote RSYNC on line 3 and HMOVE on line 5 and began watching at line 6, so it saw neither. With the fault the review planted, the audio steps skipped while HMOVE's latch is set (`state is Rhs or Cnt && !_motion.Extended`), every sound test passed. The test now watches lines 2 to 8, with both writes inside, and checks each line's two steps, the step RSYNC cuts off on line 3 included. Planted again in a scratch copy on 14 October 2026, the fault fails it, and only it, among the sound tests (`dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter FullyQualifiedName~TiaSoundTests`).
- **The resamplers were committed after the rule**, not "written and measured" after it, and the test's feed deviated from the rule's signal by passing a held level every 1,024 colour clocks; both are said above.
- **Guesses tagged**: the linear mix, the steady offset with no filter, and the compare at the step with jigo2600's latch not modelled, here, in the code, in `tia.md` section 18.8 and in `docs/known-differences.md`.
- "Up to one and a half samples" is "about"; the quoted jigo2600 fragments in `tia.md` section 18 are paraphrased.
- **The speed.** `bench/atari-2600-speed/rule.md` gains a dated amendment: the printed two-decimal median is what is judged, said after the first result, with no verdict changed. The speed milestone entry no longer says most of the fall from 8.22 is the host, nor that the sound costs 6 per cent on NTSC; it says what the control shows.
