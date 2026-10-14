namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>The TIA's write and read registers by address (<c>docs/atari-2600/facts/tia.md</c> section 2).</summary>
internal static class Reg
{
    public const int Vblank = 0x01;
    public const int Nusiz0 = 0x04;
    public const int Nusiz1 = 0x05;
    public const int Colup0 = 0x06;
    public const int Colup1 = 0x07;
    public const int Colupf = 0x08;
    public const int Colubk = 0x09;
    public const int Ctrlpf = 0x0A;
    public const int Refp0 = 0x0B;
    public const int Refp1 = 0x0C;
    public const int Pf0 = 0x0D;
    public const int Pf1 = 0x0E;
    public const int Pf2 = 0x0F;
    public const int Resp0 = 0x10;
    public const int Resp1 = 0x11;
    public const int Resm0 = 0x12;
    public const int Resm1 = 0x13;
    public const int Resbl = 0x14;
    public const int Grp0 = 0x1B;
    public const int Grp1 = 0x1C;
    public const int Enam0 = 0x1D;
    public const int Enam1 = 0x1E;
    public const int Enabl = 0x1F;
    public const int Vdelp0 = 0x25;
    public const int Vdelp1 = 0x26;
    public const int Vdelbl = 0x27;
    public const int Resmp0 = 0x28;
    public const int Resmp1 = 0x29;
    public const int Hmp0 = 0x20;
    public const int Hmp1 = 0x21;
    public const int Hmm0 = 0x22;
    public const int Hmm1 = 0x23;
    public const int Hmbl = 0x24;
    public const int Hmove = 0x2A;
    public const int Hmclr = 0x2B;
    public const int Cxclr = 0x2C;

    // Read registers (section 2.2).
    public const int Cxm0p = 0x00;
    public const int Cxm1p = 0x01;
    public const int Cxp0fb = 0x02;
    public const int Cxp1fb = 0x03;
    public const int Cxm0fb = 0x04;
    public const int Cxm1fb = 0x05;
    public const int Cxblpf = 0x06;
    public const int Cxppmm = 0x07;
}

/// <summary>
/// A TIA alone, from power on, driven a colour clock at a time: writes made at a line and colour
/// clock, as a store landing at W = 3k does (tia.md section 1), and the frame's rows read back.
/// Line 0 is the line from power on, and its colour clock 0 the first clock; nothing here strobes
/// RSYNC, so every line is 228 clocks. The colours are fixed so a pixel says which colour register
/// drew it: <see cref="P0"/> for player 0 and missile 0, <see cref="P1"/> for player 1 and missile
/// 1, <see cref="Pf"/> for the playfield and the ball, and 0 for the background.
/// </summary>
internal sealed class TiaScene
{
    public const byte P0 = 0x1E;
    public const byte P1 = 0x4E;
    public const byte Pf = 0x86;

    public TiaScene()
    {
        Tia.Write(Reg.Colup0, P0);
        Tia.Write(Reg.Colup1, P1);
        Tia.Write(Reg.Colupf, Pf);
    }

    public Tia Tia { get; } = new();

    /// <summary>Lines finished since power on.</summary>
    public int Line { get; private set; }

    /// <summary>Writes now, at the clock the next tick runs.</summary>
    public TiaScene Write(int register, byte value)
    {
        Tia.Write(register, value);
        return this;
    }

    /// <summary>Runs to colour clock <paramref name="clock"/> of line <paramref name="line"/> and writes there.</summary>
    public TiaScene At(int line, int clock, int register, byte value)
    {
        RunTo(line, clock);
        Tia.Write(register, value);
        return this;
    }

    /// <summary>Runs to colour clock <paramref name="clock"/> of line <paramref name="line"/> and reads a register there, D7 and D6.</summary>
    public byte ReadAt(int line, int clock, int register)
    {
        RunTo(line, clock);
        return Tia.Read(register);
    }

    /// <summary>Runs until the next tick is colour clock <paramref name="clock"/> of line <paramref name="line"/>.</summary>
    public void RunTo(int line, int clock)
    {
        if (line < Line || (line == Line && clock < Tia.HorizontalCounter))
        {
            throw new InvalidOperationException($"line {line} clock {clock} has passed");
        }

        while (Line < line || Tia.HorizontalCounter != clock)
        {
            Tick();
        }
    }

    /// <summary>Runs to the end of line <paramref name="lines"/> - 1, ends the frame there and returns its rows, one a line.</summary>
    public byte[][] Rows(int lines)
    {
        RunTo(lines, 0);
        Tia.Write(0x00, 0x02);
        Tia.Write(0x00, 0x00);
        if (Tia.Frame.Lines != lines)
        {
            throw new InvalidOperationException($"the frame has {Tia.Frame.Lines} rows, not {lines}");
        }

        var rows = new byte[lines][];
        for (int row = 0; row < lines; row++)
        {
            rows[row] = Tia.Frame.ColourNumbers.Slice(row * FrameBuffer.Width, FrameBuffer.Width).ToArray();
        }

        return rows;
    }

    /// <summary>The pixels of <paramref name="row"/> that show <paramref name="colour"/>.</summary>
    public static int[] Lit(byte[] row, byte colour) => [.. Enumerable.Range(0, row.Length).Where(x => row[x] == colour)];

    /// <summary>The pixels from <paramref name="first"/> to <paramref name="last"/>, both included.</summary>
    public static int[] Span(int first, int last) => [.. Enumerable.Range(first, last - first + 1)];

    /// <summary>Several spans, as one sorted list.</summary>
    public static int[] Spans(params (int First, int Last)[] spans) => [.. spans.SelectMany(s => Span(s.First, s.Last)).Order()];

    private void Tick()
    {
        Tia.Tick();
        if (Tia.HorizontalCounter == 0)
        {
            Line++;
        }
    }
}
