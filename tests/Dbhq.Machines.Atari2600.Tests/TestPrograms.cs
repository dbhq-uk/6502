using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// Small programs for the bus and the machine, each built by the test into a plain 4K cartridge
/// (or an 8K F8 one) so a test can watch the CPU's own bus accesses. None is a game.
/// </summary>
internal static class TestPrograms
{
    /// <summary>Where the code starts: the cartridge's first byte, as the CPU sees it with A13 to A15 set.</summary>
    public const ushort Start = 0xF000;

    /// <summary>Where the IRQ and BRK vector points: a <c>JMP</c> to itself, so a stray interrupt is seen as a PC there.</summary>
    public const ushort Trap = 0xFFF0;

    /// <summary>The two regions, for a <c>Theory</c>.</summary>
    public static TheoryData<string> Regions => ["NTSC", "PAL"];

    /// <summary>The region a <c>Theory</c> row names.</summary>
    public static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    /// <summary>
    /// A 4K image: <paramref name="code"/> at <c>$F000</c>, <c>NOP</c> everywhere else, the reset
    /// vector at <c>$F000</c> and the IRQ vector at <see cref="Trap"/>, which holds <c>JMP Trap</c>.
    /// </summary>
    public static byte[] Image4K(params byte[] code)
    {
        byte[] image = new byte[4096];
        Array.Fill(image, (byte)0xEA);
        code.CopyTo(image, 0);
        image[0xFF0] = 0x4C;
        image[0xFF1] = Trap & 0xFF;
        image[0xFF2] = Trap >> 8;
        image[0xFFC] = Start & 0xFF;
        image[0xFFD] = Start >> 8;
        image[0xFFE] = Trap & 0xFF;
        image[0xFFF] = Trap >> 8;
        return image;
    }

    /// <summary>The machine with <paramref name="code"/> in a plain 4K cartridge, powered on.</summary>
    public static Atari2600 Machine(Region region, params byte[] code)
    {
        var machine = new Atari2600(Cartridge.Load(Image4K(code)), region);
        machine.PowerOn();
        return machine;
    }

    /// <summary>
    /// An 8K F8 image whose two banks hold the same <paramref name="code"/> and vectors, so the
    /// program runs on whichever bank shows; byte <c>$F800</c> of bank n holds n, to see which.
    /// </summary>
    public static byte[] ImageF8(params byte[] code)
    {
        byte[] bank = Image4K(code);
        byte[] image = new byte[8192];
        for (int n = 0; n < 2; n++)
        {
            bank.CopyTo(image, n * 4096);
            image[(n * 4096) + 0x800] = (byte)n;
        }

        return image;
    }
}

/// <summary>
/// A board that passes every access to the real one and writes it down: what the cartridge slot
/// sees, cycle by cycle. <see cref="Peek"/> is not written down, because it is not an access.
/// </summary>
internal sealed class RecordingBoard(IBoard inner) : IBoard
{
    public List<(ushort Address, bool Write)> Accesses { get; } = [];

    public int Bank => inner.Bank;

    public byte Access(ushort address, bool write, byte dataBus)
    {
        Accesses.Add((address, write));
        return inner.Access(address, write, dataBus);
    }

    public byte Peek(ushort address) => inner.Peek(address);

    public void PowerOn() => inner.PowerOn();
}
