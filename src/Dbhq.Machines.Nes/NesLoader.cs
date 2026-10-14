namespace Dbhq.Machines.Nes;

/// <summary>
/// What a page does with a visitor's cartridge file: asks which region its header names, then
/// builds the machine in the region chosen and says, in one sentence, which that was and why. The
/// browser host (<c>NesHost</c> in <c>src/Dbhq.Machines.Nes.Wasm</c>) is these calls and little
/// else, so the rules are here, where they can be tested without WebAssembly.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bad file throws the cartridge's own sentence</b> (<see cref="NesFormatException"/>) from
/// both calls, the board's refusal of a mapper it does not model included: the machine's
/// constructor creates the board, and <see cref="RegionOf"/> asks for it too, so a page hears
/// about an unsupported board before it changes anything. Nothing is built until the file has
/// passed, so a page that keeps its machine until <see cref="Load"/> returns keeps it as it was.
/// </para>
/// <para>
/// <b>The region</b> is the caller's choice, "NTSC" or "PAL", or "" for the header's, and NTSC
/// when the header does not say (an iNES file never does: <see cref="Cartridge.HeaderRegion"/>).
/// A choice against the header is taken, and the sentence says the game may run at the wrong
/// speed (the plan's Review Focus 2).
/// </para>
/// </remarks>
public static class NesLoader
{
    /// <summary>
    /// The region <paramref name="rom"/>'s header names, "NTSC" or "PAL", or "" when it does not
    /// say.
    /// </summary>
    /// <exception cref="NesFormatException">The file is not one this machine can run, its board included.</exception>
    public static string RegionOf(byte[] rom)
    {
        Cartridge cartridge = Cartridge.Load(rom);
        _ = cartridge.CreateMapper();
        return cartridge.HeaderRegion?.Name ?? string.Empty;
    }

    /// <summary>
    /// Builds a machine with <paramref name="rom"/> in it, in <paramref name="region"/>, with
    /// <paramref name="options"/>, and switches it on.
    /// </summary>
    /// <param name="rom">The whole cartridge file.</param>
    /// <param name="region">"NTSC", "PAL", or "" for the header's region, else NTSC.</param>
    /// <param name="options">The machine's options, or null for the defaults.</param>
    /// <param name="why">One sentence for the page: the region chosen, and why.</param>
    /// <exception cref="NesFormatException">The file is not one this machine can run, its board included.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="region"/> is not "NTSC", "PAL" or "".</exception>
    public static Nes Load(byte[] rom, string region, NesOptions? options, out string why)
    {
        ArgumentNullException.ThrowIfNull(region);
        Region? asked = region switch
        {
            "" => null,
            "NTSC" => Region.Ntsc,
            "PAL" => Region.Pal,
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, "the region is NTSC, PAL, or empty for the header's"),
        };

        Cartridge cartridge = Cartridge.Load(rom);
        var nes = new Nes(cartridge, asked, options);
        nes.PowerOn();
        why = Why(nes.Region, cartridge.HeaderRegion, asked is not null);
        return nes;
    }

    private static string Why(Region chosen, Region? header, bool asked)
    {
        string name = chosen.Name;
        if (!asked)
        {
            return header is null
                ? $"Running as {name}: the file does not say, so {name}."
                : $"Running as {name}: the file says {name}.";
        }

        if (header is null)
        {
            return $"Running as {name}, as chosen here: the file does not say which.";
        }

        return header == chosen
            ? $"Running as {name}, as chosen here, which is what the file says."
            : $"Running as {name}, as chosen here, though the file says {header.Name}, so it may run at the wrong speed.";
    }
}
