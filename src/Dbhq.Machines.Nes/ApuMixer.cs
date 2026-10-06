namespace Dbhq.Machines.Nes;

/// <summary>
/// The sound unit's mixer, <c>docs/nes/facts/apu.md</c> section 11: the two non-linear formulas
/// from the APU Mixer page,
/// <code>
/// pulse_out = 95.88 / (8128 / (pulse1 + pulse2) + 100)
/// tnd_out   = 159.79 / (1 / (triangle / 8227 + noise / 12241 + dmc / 22638) + 100)
/// output    = pulse_out + tnd_out
/// </code>
/// each group 0 when its inputs are all 0, the pulses, triangle and noise 0 to 15 and the DMC 0 to
/// 127. The output is 0 to 1.
/// </summary>
/// <remarks>
/// The bus asks for the mixed level once a CPU cycle, so the formulas are worked once, here, into
/// two tables: 31 entries for the pulses' sum, and one for every triangle, noise and DMC level
/// together, 16 x 16 x 128 = 32,768 entries, so the TND group is exact for each triple and not the
/// page's 4 % linear-index approximation. A mix is then two loads and an add. The TND table is
/// single precision (128 KB); <c>MixerTests</c> holds every entry within 1e-6 of the formula.
/// </remarks>
public static class ApuMixer
{
    private static readonly double[] PulseTable = BuildPulseTable();
    private static readonly float[] TndTable = BuildTndTable();

    /// <summary>The pulse group for the two pulse levels, 0 to 15 each.</summary>
    public static double Pulse(int pulse1, int pulse2) => PulseTable[pulse1 + pulse2];

    /// <summary>The triangle, noise and DMC group: triangle and noise 0 to 15, DMC 0 to 127.</summary>
    public static double Tnd(int triangle, int noise, int dmc) => TndTable[(triangle << 11) | (noise << 7) | dmc];

    /// <summary>The mixed level, 0 to 1: the two groups added.</summary>
    public static double Mix(int pulse1, int pulse2, int triangle, int noise, int dmc) =>
        PulseTable[pulse1 + pulse2] + TndTable[(triangle << 11) | (noise << 7) | dmc];

    private static double[] BuildPulseTable()
    {
        double[] table = new double[31];
        for (int sum = 1; sum < table.Length; sum++)
        {
            table[sum] = 95.88 / ((8128.0 / sum) + 100);
        }

        return table;
    }

    private static float[] BuildTndTable()
    {
        float[] table = new float[16 * 16 * 128];
        for (int triangle = 0; triangle < 16; triangle++)
        {
            for (int noise = 0; noise < 16; noise++)
            {
                for (int dmc = 0; dmc < 128; dmc++)
                {
                    double sum = (triangle / 8227.0) + (noise / 12241.0) + (dmc / 22638.0);
                    table[(triangle << 11) | (noise << 7) | dmc] = sum == 0 ? 0 : (float)(159.79 / ((1 / sum) + 100));
                }
            }
        }

        return table;
    }
}
