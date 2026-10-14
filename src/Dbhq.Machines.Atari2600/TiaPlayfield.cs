namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The playfield: twenty bits from PF0, PF1 and PF2, drawn four pixels a bit over the left half of
/// the line and repeated, or reflected by CTRLPF D0, over the right (<c>docs/atari-2600/facts/tia.md</c>
/// section 8). The TIA asks for each cell's bit as its horizontal sync counter reaches it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cells by the counter.</b> The playfield is decoded from the horizontal sync counter, which
/// RSYNC resets and HMOVE does not move (tia.md sections 3, 6 and 15): the left half's scan starts
/// on the RHB decode and the right half's on CNT, each shown four clocks later, so cell c of the
/// left half shows while the counter is at count 17 + c (colour clocks 68 + 4c to 71 + 4c) and the
/// right half's cell c at count 37 + c (Towers's table, its PF column).
/// </para>
/// <para>
/// <b>When a bit is read.</b> A cell takes its bit from the registers as they stand two colour
/// clocks before its first pixel: the TIA calls <see cref="Fetch"/> on the third clock of the count
/// before and <see cref="Show"/> on the first clock of the cell. tia.md section 8 fits this to the
/// suite's <c>midline-pf</c> picture (a write at W at most P - 3 shows, at W at least P - 1 does
/// not); which side W = P - 2 falls is not covered by it [guessing - verify]. The reflect bit is read
/// with the bit, which no source states [guessing - verify].
/// </para>
/// </remarks>
public sealed class TiaPlayfield : IReportsState
{
    // The first count of each half (tia.md section 3: RHB and CNT, four clocks late).
    private const int LeftStart = 17;
    private const int RightStart = 37;
    private const int Cells = 20;

    // The twenty bits in the order they are drawn: bit c is cell c (tia.md section 8's table).
    private int _bits;

    // CTRLPF D0: the right half reflected.
    private bool _reflect;

    // The bit fetched for the next count, and the bit being shown.
    private bool _next;
    private bool _output;

    // The half of the line the fetched and the shown cell are in: true for the right half, from
    // the CNT decode (score mode's colours, tia.md section 13).
    private bool _nextRight;
    private bool _right;

    internal TiaPlayfield()
    {
    }

    /// <summary>True while the cell being drawn is lit.</summary>
    public bool Output => _output;

    /// <summary>True while the cell being drawn is in the right half, from the CNT decode on.</summary>
    public bool RightHalf => _right;

    /// <summary>A write to PF0, PF1 or PF2 (<c>$0D</c> to <c>$0F</c>) or CTRLPF (<c>$0A</c>).</summary>
    internal void Write(int register, byte value)
    {
        switch (register)
        {
            case 0x0A:
                _reflect = (value & 0x01) != 0;
                break;
            case 0x0D:
                // PF0 D4 to D7 are cells 0 to 3.
                _bits = (_bits & ~0x0000F) | (value >> 4);
                break;
            case 0x0E:
                // PF1 D7 down to D0 are cells 4 to 11.
                _bits = (_bits & ~0x00FF0) | (Reverse(value) << 4);
                break;
            case 0x0F:
                // PF2 D0 up to D7 are cells 12 to 19.
                _bits = (_bits & ~0xFF000) | (value << 12);
                break;
        }
    }

    /// <summary>Reads the bit of the cell shown at <paramref name="count"/> of the horizontal sync counter; no cell, no bit.</summary>
    internal void Fetch(int count)
    {
        _nextRight = count >= RightStart;
        int cell;
        if (count >= LeftStart && count < LeftStart + Cells)
        {
            cell = count - LeftStart;
        }
        else if (count >= RightStart && count < RightStart + Cells)
        {
            cell = _reflect ? Cells - 1 - (count - RightStart) : count - RightStart;
        }
        else
        {
            _next = false;
            return;
        }

        _next = ((_bits >> cell) & 1) != 0;
    }

    /// <summary>The fetched bit becomes the one shown.</summary>
    internal void Show()
    {
        _output = _next;
        _right = _nextRight;
    }

    internal void PowerOn()
    {
        _bits = 0;
        _reflect = false;
        _next = false;
        _output = false;
        _nextRight = false;
        _right = false;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_bits), _bits);
        sink.Add(nameof(_reflect), _reflect);
        sink.Add(nameof(_next), _next);
        sink.Add(nameof(_output), _output);
        sink.Add(nameof(_nextRight), _nextRight);
        sink.Add(nameof(_right), _right);
    }

    private static int Reverse(byte value)
    {
        int reversed = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            reversed |= ((value >> bit) & 1) << (7 - bit);
        }

        return reversed;
    }
}
