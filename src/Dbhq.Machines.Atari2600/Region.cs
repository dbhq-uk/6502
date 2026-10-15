namespace Dbhq.Machines.Atari2600;

/// <summary>
/// Every number that differs between the NTSC and the PAL console, so that no chip tests for a
/// region by name: the bus, the TIA and the page are given a <see cref="Region"/> and read what
/// they need from it.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/region.md</c>. The two consoles differ in the colour clock
/// and the colour signal, and in nothing a program can see (section 5), so the region can be
/// switched while a game runs. SECAM is not a region here (section 6, issue #106).
/// </para>
/// <para>
/// One number is kept per clock, the colour clock, as an exact expression of the colour
/// subcarrier it comes from; the CPU clock is computed from it (section 1). The palette is computed
/// from the region's colour signal (<see cref="TiaPalette"/>; tia.md section 19).
/// </para>
/// </remarks>
public sealed class Region
{
    private Region(
        string name,
        double colourClockHz,
        int nominalLines,
        int firstShownLine,
        int shownLines,
        int pixelWidth,
        int pixelHeight,
        Func<int, double?> hueAngle)
    {
        Name = name;
        ColourClockHz = colourClockHz;
        NominalLines = nominalLines;
        FirstShownLine = firstShownLine;
        ShownLines = shownLines;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Palette = new TiaPalette(hueAngle);
    }

    /// <summary>
    /// The NTSC console: the colour clock is the NTSC colour subcarrier, 315/88 MHz (region.md 1),
    /// a frame of 262 lines (section 2), and the window and pixel shape of sections 3 and 4.
    /// </summary>
    public static Region Ntsc { get; } = new(
        name: "NTSC",
        colourClockHz: 315_000_000.0 / 88,
        nominalLines: 262,
        firstShownLine: 19,
        shownLines: 228,
        pixelWidth: 12,
        pixelHeight: 7,
        hueAngle: TiaPalette.NtscHueAngle);

    /// <summary>
    /// The PAL console: the colour clock is four fifths of the PAL colour subcarrier of
    /// 4.43361875 MHz (region.md 1, open item 1), a frame of 312 lines (section 2), and the window
    /// and pixel shape of sections 3 and 4. The pixel's 2.079 is held as 52:25, 2.080.
    /// </summary>
    public static Region Pal { get; } = new(
        name: "PAL",
        colourClockHz: 4_433_618.75 * 4 / 5,
        nominalLines: 312,
        firstShownLine: 27,
        shownLines: 264,
        pixelWidth: 52,
        pixelHeight: 25,
        hueAngle: TiaPalette.PalHueAngle);

    /// <summary>"NTSC" or "PAL", for the page to show. No code chooses by it.</summary>
    public string Name { get; }

    /// <summary>The TIA's oscillator in hertz: one colour clock, one pixel (region.md 1).</summary>
    public double ColourClockHz { get; }

    /// <summary>The 6507's clock in hertz: the colour clock divided by three (region.md 1).</summary>
    public double CpuHz => ColourClockHz / 3;

    /// <summary>
    /// The lines in a frame of the games made for this region, Atari's recommended counts: 262 on
    /// NTSC and 312 on PAL (region.md 2). The page's PAL suggestion compares a frame against it; a
    /// program makes as many lines as it likes.
    /// </summary>
    public int NominalLines { get; }

    /// <summary>
    /// The first line of the frame the page shows, counted from the first line after VSYNC ends
    /// as 0 (region.md 3). A choice, since a television's visible lines vary from set to set.
    /// </summary>
    public int FirstShownLine { get; }

    /// <summary>The number of lines the page shows from <see cref="FirstShownLine"/> (region.md 3).</summary>
    public int ShownLines { get; }

    /// <summary>The width of one pixel, against <see cref="PixelHeight"/>: the pixel's shape as a ratio (region.md 4).</summary>
    public int PixelWidth { get; }

    /// <summary>The height of one pixel, against <see cref="PixelWidth"/> (region.md 4).</summary>
    public int PixelHeight { get; }

    /// <summary>
    /// The 128 colours this region's TIA shows, computed from its colour signal (tia.md sections
    /// 19.4 and 19.5), which is where the two TIAs differ (region.md section 5).
    /// </summary>
    public TiaPalette Palette { get; }
}
