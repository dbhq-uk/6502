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
