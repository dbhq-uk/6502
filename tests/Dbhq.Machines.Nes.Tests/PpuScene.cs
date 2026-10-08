namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// A PPU alone on a test board, and the few things the drawing tests do to it: tiles written
/// straight into the board's pattern RAM, nametable and palette bytes written through the PPU's
/// own registers, the scroll set as a program sets it, and the PPU run to a dot or a frame.
/// Never a game: every picture is a handful of tiles a test describes.
/// </summary>
internal sealed class PpuScene
{
    /// <summary>A PPU on a test board; with <paramref name="quiet"/>, one as the real boards are, which does not watch the PPU's address bus and has windows (<see cref="TestMapper.Quiet"/>, <see cref="TestMapper.Windowed"/>).</summary>
    public PpuScene(Region region, Mirroring mirroring = Mirroring.FourScreen, bool quiet = false)
    {
        Region = region;
        Mapper = new TestMapper { Mirroring = mirroring, Quiet = quiet, Windowed = quiet };
        Ppu = new Ppu(region, Mapper);
        Ppu.PowerOn();
    }

    public Region Region { get; }

    public TestMapper Mapper { get; }

    public Ppu Ppu { get; }

    public static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    /// <summary>A byte of PPU memory, written through <c>$2006</c> and <c>$2007</c>. Rendering must be off.</summary>
    public void Poke(ushort address, byte value)
    {
        Ppu.ReadRegister(2);
        Ppu.WriteRegister(6, (byte)(address >> 8));
        Ppu.WriteRegister(6, (byte)address);
        Ppu.WriteRegister(7, value);
    }

    /// <summary>A tile's two planes, eight rows each, put straight into the board's pattern RAM.</summary>
    public void Tile(int table, int tile, byte[] low, byte[] high)
    {
        int at = (table * 0x1000) + (tile * 16);
        low.CopyTo(Mapper.Chr, at);
        high.CopyTo(Mapper.Chr, at + 8);
    }

    /// <summary>A tile whose every pixel is <paramref name="value"/> (0 to 3).</summary>
    public void SolidTile(int table, int tile, int value)
    {
        byte low = (value & 1) != 0 ? (byte)0xFF : (byte)0;
        byte high = (value & 2) != 0 ? (byte)0xFF : (byte)0;
        Tile(table, tile, Enumerable.Repeat(low, 8).ToArray(), Enumerable.Repeat(high, 8).ToArray());
    }

    /// <summary>Tile <paramref name="tile"/> at column <paramref name="column"/>, row <paramref name="row"/> of nametable <paramref name="nametable"/>.</summary>
    public void PlaceTile(int nametable, int column, int row, byte tile)
    {
        Poke((ushort)(0x2000 + (nametable * 0x400) + (row * 32) + column), tile);
    }

    /// <summary>
    /// The scroll and the nametable as a program sets them: <c>$2000</c>, then the two
    /// <c>$2005</c> writes, after a <c>$2002</c> read to clear <c>w</c>.
    /// </summary>
    public void Scroll(int x, int y, int nametable = 0, byte ctrl = 0)
    {
        Ppu.ReadRegister(2);
        Ppu.WriteRegister(0, (byte)(ctrl | nametable));
        Ppu.WriteRegister(5, (byte)x);
        Ppu.WriteRegister(5, (byte)y);
    }

    /// <summary>The colour a palette value shows in this region with no emphasis.</summary>
    public uint Colour(int value) => PpuPalette.Colour(value, 0, Region.EmphasisSwapsRedAndGreen, false);

    /// <summary>The pixel at column <paramref name="x"/>, line <paramref name="y"/>.</summary>
    public uint Pixel(int x, int y) => Ppu.Screen.Pixels[(y * FrameBuffer.Width) + x];

    /// <summary>Runs until the PPU is at <paramref name="line"/>, <paramref name="dot"/>: the dot it runs next.</summary>
    public void TickTo(int line, int dot)
    {
        for (int i = 0; i < 2 * 312 * 341; i++)
        {
            if (Ppu.Line == line && Ppu.Dot == dot)
            {
                return;
            }

            Ppu.Tick();
        }

        throw new InvalidOperationException($"the PPU never reached line {line} dot {dot}");
    }

    /// <summary>Runs until <paramref name="frames"/> more frames have ended.</summary>
    public void RunFrames(int frames)
    {
        long end = Ppu.Frame + frames;
        while (Ppu.Frame < end)
        {
            Ppu.Tick();
        }
    }

    /// <summary>
    /// Switches rendering on with <paramref name="mask"/> and runs two frames: the first starts
    /// with <c>v</c> wherever the setup left it, and its pre-render line copies <c>t</c> in, so
    /// the second is the picture the registers describe. The buffer then holds the second.
    /// </summary>
    public void Show(byte mask)
    {
        Ppu.WriteRegister(1, mask);
        RunFrames(2);
    }
}
