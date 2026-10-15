using Dbhq.Cpu6502;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The core on a board with nothing else, so a test takes each instruction's bus accesses from the
/// core, which is checked cycle by cycle against Harte's tests, rather than from a table
/// (cartridge.md section 5). It is not the machine's bus (task 4): it keeps the 6507's 13 lines,
/// gives the board every access with A12 high, puts the rest in a flat 4K of RAM where the test's
/// code lives, and keeps the last byte on the data bus. It records every access the board sees.
/// </summary>
internal sealed class BoardBus : Bus
{
    private readonly byte[] _ram = new byte[0x1000];

    public BoardBus(IBoard board)
    {
        Board = board;
        Cpu = new Cpu(this, CpuVariant.Nmos6502);
    }

    public IBoard Board { get; }

    public Cpu Cpu { get; }

    /// <summary>The last byte on the data bus: the byte read, or the byte written.</summary>
    public byte DataBus { get; private set; }

    /// <summary>Every access the board saw: its 13-bit address, whether it was a write, and the byte on the bus after it.</summary>
    public List<(ushort Address, bool Write, byte Value)> BoardAccesses { get; } = [];

    /// <summary>A list of accesses, written as a test expects them.</summary>
    public static List<(ushort Address, bool Write, byte Value)> Accesses(params (ushort Address, bool Write, byte Value)[] accesses) => [.. accesses];

    /// <summary>
    /// Puts <paramref name="code"/> in RAM at <paramref name="at"/> (below <c>$1000</c>, so the
    /// fetches do not reach the board) and points the CPU at it.
    /// </summary>
    public void Load(ushort at, params byte[] code)
    {
        code.CopyTo(_ram, at);
        Cpu.PC = at;
    }

    public override byte Read(ushort address)
    {
        ushort line = (ushort)(address & 0x1FFF);
        if ((line & 0x1000) != 0)
        {
            DataBus = Board.Access(line, write: false, DataBus);
            BoardAccesses.Add((line, false, DataBus));
        }
        else
        {
            DataBus = _ram[line];
        }

        return DataBus;
    }

    public override void Write(ushort address, byte value)
    {
        ushort line = (ushort)(address & 0x1FFF);
        DataBus = value;
        if ((line & 0x1000) != 0)
        {
            Board.Access(line, write: true, value);
            BoardAccesses.Add((line, true, value));
        }
        else
        {
            _ram[line] = value;
        }
    }
}
