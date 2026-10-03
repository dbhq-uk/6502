// The test oracle for the video ULA: the same picture as VideoUla (its remarks, from video.md
// section 2, and Framebuffer's), made as plainly as possible, one CRTC character at a time, with
// the byte read from RAM in the character's own cycle and the shift register stepped one pixel
// clock at a time. Never used by the machine; the equivalence tests run it beside the lazy chip.
// Task 9 added mode 7: the teletext chip's pipeline kept character by character, each byte with
// the LOSE that comes one character after it, each cell drawn three characters after its fetch
// where it was fetched, from ReferenceTeletext, which works out each half-dot directly.
namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>A video ULA fed every character clock with the CRTC's outputs and the byte fetched.</summary>
public sealed class ReferenceVideoUla
{
    private const int Width = 640;
    private const uint Black = 0xFF000000;

    private readonly byte[] _palette = new byte[16];
    private long _lineStart;
    private int _x;
    private int _rowA = -1, _rowB = -1;
    private int _fieldLines;
    private bool _fieldOdd, _fieldInterlace;
    private int _cursorAge = 4;

    // Mode 7: the line's characters so far, each byte and the LOSE that came with it; the first
    // character of an unbroken stretch with the teletext select on, or -1; whether the last cell
    // had LOSE; whether any character of the line had DISPTMG; VSYNC after the last character and
    // at the end of the line before.
    private readonly int[] _lineBytes = new int[260];
    private readonly bool[] _lineLose = new bool[260];
    private int _character;
    private int _ttxFirst = -1;
    private bool _ttxShowing;
    private bool _loseSeen;
    private bool _vsyncLast, _vsyncAtLastLineEnd;

    public ReferenceVideoUla()
    {
        Array.Fill(Pixels, Black);
        BeginLine(0, 0, false, false, 0);
    }

    public byte Control { get; private set; }

    public uint[] Pixels { get; } = new uint[Width * 512];

    public long Frames { get; private set; }

    public ReferenceTeletext Teletext { get; } = new();

    public void WriteControl(byte value) => Control = value;

    public void WritePalette(byte value) => _palette[value >> 4] = (byte)(value & 0x0F);

    /// <summary>Power on: both registers to zero.</summary>
    public void PowerOn()
    {
        Control = 0;
        Array.Clear(_palette);
        Teletext.PowerOn();
    }

    /// <summary>The address the ULA's fetch reads, from video.md s2.5, written as the hardware's adder on MA11 to MA8.</summary>
    public static int ScreenAddress(int ma, int ra, int latch)
    {
        if ((ma & 0x2000) != 0)
        {
            // TTX VDU: DA14 = MA11, DA13 to DA10 = 1, DA9 to DA0 = MA9 to MA0.
            return ((ma & 0x0800) != 0 ? 0x4000 : 0) | 0x3C00 | (ma & 0x03FF);
        }

        // HI RES: DA14 to DA11 = MA11 to MA8, plus the adder's number when MA12 is high;
        // DA10 to DA3 = MA7 to MA0; DA2 to DA0 = RA2 to RA0.
        int high = (ma >> 8) & 0x0F;
        if ((ma & 0x1000) != 0)
        {
            int add = latch switch
            {
                0 => 0x4000, // C1 C0 = 00, mode 3
                1 => 0x6000, // 01, mode 6
                2 => 0x3000, // 10, modes 0 to 2
                _ => 0x5800, // 11, modes 4 and 5
            };
            high = (high + (add >> 11)) & 0x0F;
        }

        return (high << 11) | ((ma & 0xFF) << 3) | (ra & 7);
    }

    /// <summary>
    /// One character clock, in CPU cycle <paramref name="cycle"/>: the CRTC has just ticked, and
    /// <paramref name="screenByte"/> is what its MA and RA fetch from RAM now.
    /// </summary>
    public void Clock(
        long cycle, bool lineStarted, int line, bool oddField, bool interlace,
        byte screenByte, bool displayEnable, bool cursor, int rasterAddress, bool vsync)
    {
        if (lineStarted)
        {
            FinishLine();
            if (line == 0)
            {
                EndField(completed: true);
            }
            BeginLine(cycle, line, oddField, interlace, rasterAddress);
        }

        int t = _character++;
        _loseSeen |= displayEnable;

        // The cursor: CUDISP starts it, then segments 0, 1 and 2 (two characters) in turn,
        // each drawn if its control bit (7, 6, 5) is set.
        if (cursor)
        {
            _cursorAge = 0;
        }
        else if (_cursorAge < 4)
        {
            _cursorAge++;
        }
        int segmentBit = _cursorAge switch { 0 => 0x80, 1 => 0x40, 2 or 3 => 0x20, _ => 0 };
        bool invert = (Control & segmentBit) != 0;

        int width = (Control & 0x10) != 0 ? 8 : 16;
        int x = (int)(cycle - _lineStart) * 8;
        if ((Control & 0x02) != 0)
        {
            TeletextClock(t, x, width, screenByte, displayEnable, invert);
            _vsyncLast = vsync;
            return;
        }

        _ttxFirst = -1;
        _ttxShowing = false;
        _vsyncLast = vsync;
        if (_rowA >= 0)
        {
            for (int p = _x; p < Math.Min(x, Width); p++)
            {
                Put(p, Black);
            }

            bool shown = displayEnable && (rasterAddress & 8) == 0;
            int pixelRate = 2 << ((Control >> 2) & 3); // MHz: 2, 4, 8, 16
            int pixelWidth = 16 / pixelRate;
            for (int i = 0; i < width && x + i < Width; i++)
            {
                int physical = 0;
                if (shown)
                {
                    int shifts = i / pixelWidth;
                    int sr = screenByte;
                    for (int k = 0; k < shifts; k++)
                    {
                        sr = ((sr << 1) | 1) & 0xFF;
                    }

                    int index = (((sr >> 7) & 1) << 3) | (((sr >> 5) & 1) << 2) | (((sr >> 3) & 1) << 1) | ((sr >> 1) & 1);
                    int entry = _palette[index];
                    physical = (entry & 8) != 0 && (Control & 1) != 0 ? entry & 7 : (entry & 7) ^ 7;
                }

                if (invert)
                {
                    physical ^= 7;
                }

                Put(x + i, Rgba(physical));
            }
        }

        _x = x + width;
    }

    /// <summary>The CRTC has just been reset in CPU cycle <paramref name="cycle"/>: a new field and a new line start there.</summary>
    public void CrtcReset(long cycle, bool oddField, bool interlace, int rasterAddress, bool vsync)
    {
        FinishLine();
        EndField(completed: false);
        BeginLine(cycle, 0, oddField, interlace, rasterAddress);
        _vsyncLast = vsync;
    }

    /// <summary>
    /// Character <paramref name="t"/> of the line with the teletext select on: the chip takes its
    /// byte, the byte before takes this character's DISPTMG as LOSE, and the cell fetched three
    /// characters ago is drawn where it was fetched, inverted if the ULA's cursor is on now.
    /// </summary>
    private void TeletextClock(int t, int x, int width, byte screenByte, bool displayEnable, bool invert)
    {
        if (_ttxFirst < 0)
        {
            _ttxFirst = t;
        }

        _lineBytes[t] = screenByte;
        if (t >= 1)
        {
            _lineLose[t - 1] = displayEnable;
        }

        int cell = t - 3;
        if (cell < _ttxFirst)
        {
            return;
        }

        int[] colours;
        if (_lineLose[cell])
        {
            if (!_ttxShowing)
            {
                Teletext.StartDisplay();
                _ttxShowing = true;
            }
            colours = Teletext.Cell(_lineBytes[cell]);
        }
        else
        {
            _ttxShowing = false;
            colours = new int[12];
        }

        int at = x - (3 * width);
        if (_rowA < 0 || at < 0 || at >= Width)
        {
            return;
        }

        for (int p = _x; p < at; p++)
        {
            Put(p, Black);
        }

        for (int i = 0; i < width && at + i < Width; i++)
        {
            // The half-dot under the pixel's centre: the cell is twelve half-dots across.
            int physical = colours[((2 * i) + 1) * 12 / (2 * width)];
            Put(at + i, Rgba(invert ? physical ^ 7 : physical));
        }

        _x = Math.Max(_x, at + width);
    }

    private static uint Rgba(int physical) =>
        0xFF000000u | ((physical & 4) != 0 ? 0x00FF0000u : 0) | ((physical & 2) != 0 ? 0x0000FF00u : 0) | ((physical & 1) != 0 ? 0x000000FFu : 0);

    private void Put(int x, uint colour)
    {
        Pixels[(_rowA * Width) + x] = colour;
        if (_rowB >= 0)
        {
            Pixels[(_rowB * Width) + x] = colour;
        }
    }

    private void BeginLine(long cycle, int line, bool oddField, bool interlace, int rasterAddress)
    {
        Teletext.BeginLine(rasterAddress);
        _character = 0;
        _ttxFirst = -1;
        _ttxShowing = false;
        _loseSeen = false;
        _lineStart = cycle;
        _x = 0;
        _fieldOdd = oddField;
        _fieldInterlace = interlace;
        if (line >= 256)
        {
            _rowA = _rowB = -1;
            return;
        }

        _fieldLines = line + 1;
        if (interlace)
        {
            _rowA = (2 * line) + (oddField ? 1 : 0);
            _rowB = -1;
        }
        else
        {
            _rowA = 2 * line;
            _rowB = (2 * line) + 1;
        }
    }

    private void FinishLine()
    {
        // The teletext chip's DEW, VSYNC rising as seen at a line's end, or its line count.
        if (_vsyncLast && !_vsyncAtLastLineEnd)
        {
            Teletext.FieldStart();
        }
        else
        {
            Teletext.LineEnd(_loseSeen);
        }
        _vsyncAtLastLineEnd = _vsyncLast;

        if (_rowA < 0)
        {
            return;
        }

        for (int p = _x; p < Width; p++)
        {
            Put(p, Black);
        }
    }

    private void EndField(bool completed)
    {
        for (int line = _fieldLines; line < 256; line++)
        {
            if (_fieldInterlace)
            {
                Array.Fill(Pixels, Black, ((2 * line) + (_fieldOdd ? 1 : 0)) * Width, Width);
            }
            else
            {
                Array.Fill(Pixels, Black, 2 * line * Width, 2 * Width);
            }
        }

        _fieldLines = 0;
        if (completed)
        {
            Frames++;
        }
    }
}
