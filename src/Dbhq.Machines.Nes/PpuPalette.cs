namespace Dbhq.Machines.Nes;

/// <summary>
/// The PPU's 64 colours, under each of the 8 emphasis settings, as <c>0xAABBGGRR</c>, computed
/// from the composite signal the PPU makes and not copied from any table.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/ppu.md</c> section 11, which takes the method from the wiki's
/// "NTSC video" page (revision 24244). The PPU does not make RGB: it puts out a square wave, and
/// a television decodes it. So this class makes the wave and decodes it, once for each colour.
/// </para>
/// <list type="number">
/// <item><description><b>The levels.</b> A colour <c>$LH</c> has a level <c>L</c> (0 to 3) and a
/// hue <c>H</c>. Its wave swings between a low and a high voltage of its level, the page's
/// terminated measurements: low 0.228, 0.312, 0.552, 0.880 V and high 0.616, 0.840, 1.100,
/// 1.100 V, and while emphasis attenuates it, low 0.192, 0.256, 0.448, 0.712 V and high 0.500,
/// 0.676, 0.896, 0.896 V. Hue 0 is the high level all the time, hues <c>$D</c> to <c>$F</c> the
/// low level all the time, and hues <c>$E</c> and <c>$F</c> are level 1 whatever <c>L</c> says.
/// </description></item>
/// <item><description><b>The phase.</b> The wave has 12 phases a colour cycle. Hue <c>H</c> is
/// high in the 6 phases <c>p</c> where <c>(H + p) mod 12 &lt; 6</c>.</description></item>
/// <item><description><b>Emphasis.</b> PPUMASK bit 5 attenuates in the phases where hue
/// <c>$C</c> is high, bit 6 where hue 4 is, and bit 7 where hue 8 is; a hue of <c>$E</c> or
/// <c>$F</c> is never attenuated. So each bit tints the picture towards the opposite hue: red,
/// green and blue on the 2C02.</description></item>
/// <item><description><b>The decode.</b> Each of the 12 samples is scaled so that the level of
/// <c>$1D</c> (0.312 V, which is also what <c>$0F</c> puts out) is 0 and the level of
/// <c>$20</c> (1.100 V) is 1. Then Y is their mean, and U and V are twice the mean of the
/// sample times the sine and the cosine of the subcarrier at that sample, <c>pi (p + 2.5) / 6</c>.
/// That angle puts hue 8 on the colour burst, which the page gives as pure -U.</description></item>
/// <item><description><b>RGB.</b> The page's matrix: R = Y + 1.139883 V, G = Y - 0.394642 U -
/// 0.580622 V, B = Y + 2.032062 U, each clipped to 0 to 1 and rounded to 8 bits.</description></item>
/// </list>
/// <para>
/// This is one decode of many; a television blurs the colour over several pixels, and the
/// differential phase distortion the page describes turns the hues of brighter rows. Neither is
/// modelled: each colour is decoded alone, from a whole colour cycle. PAL uses the same table, so
/// the 2C07's own colours are a known difference. What the output was checked against is in
/// <c>ppu.md</c> section 11 and the journal.
/// </para>
/// </remarks>
public static class PpuPalette
{
    // The terminated voltages, by level: low, high, attenuated low, attenuated high.
    private static readonly double[] Low = [0.228, 0.312, 0.552, 0.880];
    private static readonly double[] High = [0.616, 0.840, 1.100, 1.100];
    private static readonly double[] LowAttenuated = [0.192, 0.256, 0.448, 0.712];
    private static readonly double[] HighAttenuated = [0.500, 0.676, 0.896, 0.896];

    // The level that is 0 after scaling ($1D, and what $0F puts out) and the one that is 1 ($20).
    private const double BlackVolts = 0.312;
    private const double WhiteVolts = 1.100;

    // The hue whose phases each emphasis bit attenuates: PPUMASK bit 5, 6, 7.
    private static readonly int[] EmphasisHue = [0xC, 0x4, 0x8];

    // Index (emphasis << 6) | colour, emphasis in the 2C02's order: bit 0 red, 1 green, 2 blue.
    private static readonly uint[] Straight = Build();

    // The same, for a PPU whose PPUMASK bits 5 and 6 are swapped (the 2C07): bit 0 green, 1 red.
    private static readonly uint[] Swapped = Swap(Straight);

    /// <summary>
    /// The colour the PPU shows for palette value <paramref name="index"/> (6 bits) under
    /// <paramref name="emphasis"/> (PPUMASK bits 7 to 5, as a number 0 to 7), with greyscale
    /// (PPUMASK bit 0) ANDing the value with <c>$30</c> first (ppu.md 10). With
    /// <paramref name="emphasisSwapsRedAndGreen"/>, emphasis bit 0 is green and bit 1 red, as on
    /// the 2C07. Alpha is always <c>0xFF</c>.
    /// </summary>
    public static uint Colour(int index, int emphasis, bool emphasisSwapsRedAndGreen, bool greyscale)
    {
        int colour = index & (greyscale ? 0x30 : 0x3F);
        return Table(emphasisSwapsRedAndGreen)[((emphasis & 7) << 6) | colour];
    }

    /// <summary>
    /// The 512 colours, indexed <c>(emphasis &lt;&lt; 6) | colour</c>, for the PPU to look up a
    /// pixel without a call. Greyscale is not applied; the caller ANDs the colour first.
    /// </summary>
    internal static uint[] Table(bool emphasisSwapsRedAndGreen)
    {
        return emphasisSwapsRedAndGreen ? Swapped : Straight;
    }

    private static uint[] Build()
    {
        var table = new uint[512];
        for (int emphasis = 0; emphasis < 8; emphasis++)
        {
            for (int colour = 0; colour < 64; colour++)
            {
                table[(emphasis << 6) | colour] = Decode(colour, emphasis);
            }
        }

        return table;
    }

    private static uint[] Swap(uint[] straight)
    {
        var table = new uint[512];
        for (int emphasis = 0; emphasis < 8; emphasis++)
        {
            int straightEmphasis = (emphasis & 4) | ((emphasis & 1) << 1) | ((emphasis & 2) >> 1);
            Array.Copy(straight, straightEmphasis << 6, table, emphasis << 6, 64);
        }

        return table;
    }

    private static bool InPhase(int hue, int phase)
    {
        return (hue + phase) % 12 < 6;
    }

    // The voltage of colour's wave at one of the 12 phases.
    private static double Signal(int colour, int emphasis, int phase)
    {
        int hue = colour & 0x0F;
        int level = hue >= 0xE ? 1 : (colour >> 4) & 3;

        bool attenuated = false;
        if (hue < 0xE)
        {
            for (int bit = 0; bit < 3; bit++)
            {
                if ((emphasis & (1 << bit)) != 0 && InPhase(EmphasisHue[bit], phase))
                {
                    attenuated = true;
                }
            }
        }

        double low = attenuated ? LowAttenuated[level] : Low[level];
        double high = attenuated ? HighAttenuated[level] : High[level];
        if (hue == 0)
        {
            low = high;
        }
        else if (hue >= 0xD)
        {
            high = low;
        }

        return InPhase(hue, phase) ? high : low;
    }

    /// <summary>
    /// The decoded signal of palette value <paramref name="colour"/> (6 bits) under
    /// <paramref name="emphasis"/> (0 to 7, the 2C02's order), before it becomes RGB: luma Y, 0
    /// at black and 1 at <c>$20</c>, and the chroma U and V. For tests and a debugger, which can
    /// check the decode against what the NTSC video page says of the signal, unclipped.
    /// </summary>
    public static (double Y, double U, double V) Yuv(int colour, int emphasis)
    {
        double y = 0;
        double u = 0;
        double v = 0;
        for (int phase = 0; phase < 12; phase++)
        {
            double level = (Signal(colour & 0x3F, emphasis & 7, phase) - BlackVolts) / (WhiteVolts - BlackVolts) / 12;
            double angle = Math.PI * (phase + 2.5) / 6;
            y += level;
            u += level * Math.Sin(angle) * 2;
            v += level * Math.Cos(angle) * 2;
        }

        return (y, u, v);
    }

    private static uint Decode(int colour, int emphasis)
    {
        (double y, double u, double v) = Yuv(colour, emphasis);
        uint r = Quantise(y + 1.139883 * v);
        uint g = Quantise(y - 0.394642 * u - 0.580622 * v);
        uint b = Quantise(y + 2.032062 * u);
        return 0xFF000000u | (b << 16) | (g << 8) | r;
    }

    private static uint Quantise(double channel)
    {
        return (uint)Math.Round(255 * Math.Clamp(channel, 0.0, 1.0));
    }
}
