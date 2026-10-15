namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The 128 colours a region's TIA can show, computed from its colour signal as
/// <c>docs/atari-2600/facts/tia.md</c> sections 19.4 and 19.5 give it, never copied from another
/// emulator's table. A colour number's bits 7 to 4 are the hue and bits 3 to 1 the luminance; bit 0
/// is not wired to the colour decode and is ignored (section 19.1).
/// </summary>
/// <remarks>
/// <para>
/// <b>The formula</b>, Eric Ball's conversion of the console's signal (tia.md section 19.4):
/// </para>
/// <list type="bullet">
/// <item><description>luminance <c>Y = L / 7</c>, the three luminance pins scaled so that L = 7 is white (the CX2600's 0.09 L over 0.63, a contrast choice [guessing - verify]);</description></item>
/// <item><description>saturation <c>S = (20 / 92.5) / 0.63</c> for a hue with colour, 0 for one without: the burst's 20 IRE of 92.5, by the same scale;</description></item>
/// <item><description>the carrier's phase angle <c>θ</c> of the hue, which is the region's colour signal (below); <c>U = S cos θ</c>, <c>V = S sin θ</c>;</description></item>
/// <item><description><c>R = Y + 1.140 V</c>, <c>G = Y - 0.3947 U - 0.5808 V</c>, <c>B = Y + 2.033 U</c>, each clipped to 0 to 1, times 255 and rounded to the nearest, a half to even, with no gamma step [guessing - verify].</description></item>
/// </list>
/// <para>
/// <b>NTSC</b> (section 19.3, 19.4): hue 0 has no colour; hue H from 1 to 15 is at
/// <c>180° - (H - 1) x 360/14°</c>, hue 1 on the burst's phase and each later hue one delay stage
/// further. The step of 360/14 degrees makes hue 15 land on hue 1, as the service adjustment
/// matches them; a choice [guessing - verify], with 24 degrees the design figure.
/// </para>
/// <para>
/// <b>PAL</b> (section 19.5): hues 0, 1, 14 and 15 have no colour; the even hues 2 to 12 sit at
/// <c>165° - (H - 2) / 2 x 30°</c>, and each odd hue 3 to 13 is the mirror of the even hue before
/// it, <c>-(165° - (H - 3) / 2 x 30°)</c>. Fitted to the documented names and the colours the
/// suite's include/region.h records as hardware-confirmed on a PAL console [guessing - verify].
/// </para>
/// <para>
/// What a television shows depends on its controls and on the console's delay setting, so these
/// are the model's colours and not a measurement (<c>docs/known-differences.md</c>).
/// </para>
/// </remarks>
public sealed class TiaPalette
{
    private const double Saturation = 20.0 / 92.5 / 0.63;

    // The 128 colours, by colour number shifted right once, as 0xAABBGGRR.
    private readonly uint[] _colours = new uint[128];

    /// <summary>
    /// The palette of a TIA whose colour signal puts hue 0 to 15 at the phase angle
    /// <paramref name="hueAngle"/> gives, in degrees, or gives no colour where it returns null.
    /// </summary>
    internal TiaPalette(Func<int, double?> hueAngle)
    {
        for (int n = 0; n < 128; n++)
        {
            int hue = n >> 3;
            double y = (n & 7) / 7.0;
            double u = 0;
            double v = 0;
            if (hueAngle(hue) is double degrees)
            {
                double theta = degrees * Math.PI / 180;
                u = Saturation * Math.Cos(theta);
                v = Saturation * Math.Sin(theta);
            }

            uint r = Level(y + (1.140 * v));
            uint g = Level(y - (0.3947 * u) - (0.5808 * v));
            uint b = Level(y + (2.033 * u));
            _colours[n] = 0xFF000000u | (b << 16) | (g << 8) | r;
        }
    }

    /// <summary>The colour of <paramref name="colourNumber"/>, as <c>0xAABBGGRR</c> with alpha <c>0xFF</c>; bit 0 is ignored.</summary>
    public uint Rgba(byte colourNumber) => _colours[colourNumber >> 1];

    /// <summary>The NTSC TIA's colour signal: the phase of hue 1 to 15, none for hue 0 (tia.md section 19.4).</summary>
    internal static double? NtscHueAngle(int hue) => hue == 0 ? null : 180 - ((hue - 1) * 360.0 / 14);

    /// <summary>The PAL TIA's colour signal as the model fits it: hues 2 to 13, none for 0, 1, 14 and 15 (tia.md section 19.5).</summary>
    internal static double? PalHueAngle(int hue) => hue switch
    {
        < 2 or > 13 => null,
        _ when hue % 2 == 0 => 165 - ((hue - 2) / 2 * 30.0),
        _ => -(165 - ((hue - 3) / 2 * 30.0)),
    };

    private static uint Level(double value) => (uint)Math.Round(255 * Math.Clamp(value, 0, 1), MidpointRounding.ToEven);
}
