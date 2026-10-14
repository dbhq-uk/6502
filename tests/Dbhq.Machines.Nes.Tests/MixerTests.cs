using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The mixer, <c>docs/nes/facts/apu.md</c> section 11: the two non-linear formulas, which the
/// model keeps as tables built once from them. Each figure here is worked from the sheet's
/// formula in the test, never read from the table under test.
/// </summary>
public class MixerTests
{
    // apu.md 11, written out from the sheet for the test.
    private static double PulseFormula(int pulse1, int pulse2) =>
        pulse1 + pulse2 == 0 ? 0 : 95.88 / ((8128.0 / (pulse1 + pulse2)) + 100);

    private static double TndFormula(int triangle, int noise, int dmc) =>
        triangle + noise + dmc == 0 ? 0 : 159.79 / ((1 / ((triangle / 8227.0) + (noise / 12241.0) + (dmc / 22638.0))) + 100);

    [Fact]
    public void EveryChannelAtZeroGivesZero()
    {
        Assert.Equal(0.0, ApuMixer.Mix(0, 0, 0, 0, 0));
        Assert.Equal(0.0, ApuMixer.Pulse(0, 0));
        Assert.Equal(0.0, ApuMixer.Tnd(0, 0, 0));
    }

    [Fact]
    public void ThePulseMixMatchesTheFormulaForEveryPairOfLevels()
    {
        for (int p1 = 0; p1 <= 15; p1++)
        {
            for (int p2 = 0; p2 <= 15; p2++)
            {
                Assert.Equal(PulseFormula(p1, p2), ApuMixer.Pulse(p1, p2), 1e-6);
            }
        }

        // apu.md worked example 4: both pulses at 15.
        Assert.Equal(0.2585, ApuMixer.Pulse(15, 15), 4);
    }

    [Fact]
    public void TheTndMixMatchesTheFormulaForEveryLevel()
    {
        for (int t = 0; t <= 15; t++)
        {
            for (int n = 0; n <= 15; n++)
            {
                for (int d = 0; d <= 127; d++)
                {
                    Assert.Equal(TndFormula(t, n, d), ApuMixer.Tnd(t, n, d), 1e-6);
                }
            }
        }

        // apu.md worked example 4: the triangle at 15 alone, and the DMC at 127 alone.
        Assert.Equal(0.2464, ApuMixer.Tnd(15, 0, 0), 4);
        Assert.Equal(0.5743, ApuMixer.Tnd(0, 0, 127), 4);
    }

    [Fact]
    public void TheMixIsTheSumOfTheTwoGroupsAndAlwaysWithinZeroAndOne()
    {
        for (int p = 0; p <= 15; p++)
        {
            for (int t = 0; t <= 15; t++)
            {
                for (int n = 0; n <= 15; n++)
                {
                    for (int d = 0; d <= 127; d += 7)
                    {
                        double mix = ApuMixer.Mix(p, 15 - p, t, n, d);
                        Assert.InRange(mix, 0.0, 1.0);
                        Assert.Equal(PulseFormula(p, 15 - p) + TndFormula(t, n, d), mix, 1e-6);
                    }
                }
            }
        }

        // Every channel at its loudest is the most the mixer gives, and it is under 1.
        double highest = ApuMixer.Mix(15, 15, 15, 15, 127);
        Assert.Equal(PulseFormula(15, 15) + TndFormula(15, 15, 127), highest, 1e-6);
        Assert.InRange(highest, 0.0, 1.0);
    }
}
