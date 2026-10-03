// The test oracle for the video ULA: the same picture as VideoUla (its remarks, from video.md
// section 2, and Framebuffer's), made as plainly as possible, one CRTC character at a time, with
// the byte read from RAM in the character's own cycle and the shift register stepped one pixel
// clock at a time. Never used by the machine; the equivalence tests run it beside the lazy chip.
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

    public ReferenceVideoUla()
    {
        Array.Fill(Pixels, Black);
        BeginLine(0, 0, false, false);
    }

    public byte Control { get; private set; }

    public uint[] Pixels { get; } = new uint[Width * 512];

    public long Frames { get; private set; }

    public void WriteControl(byte value) => Control = value;

    public void WritePalette(byte value) => _palette[value >> 4] = (byte)(value & 0x0F);

    /// <summary>Power on: both registers to zero.</summary>
    public void PowerOn()
    {
        Control = 0;
        Array.Clear(_palette);
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
        byte screenByte, bool displayEnable, bool cursor, int rasterAddress)
    {
        if (lineStarted)
        {
            FinishLine();
            if (line == 0)
            {
                EndField(completed: true);
            }
            BeginLine(cycle, line, oddField, interlace);
        }

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
        if (_rowA >= 0)
        {
            for (int p = _x; p < Math.Min(x, Width); p++)
            {
                Put(p, Black);
            }

            bool teletext = (Control & 0x02) != 0;
            bool shown = displayEnable && !teletext && (rasterAddress & 8) == 0;
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
    public void CrtcReset(long cycle, bool oddField, bool interlace)
    {
        FinishLine();
        EndField(completed: false);
        BeginLine(cycle, 0, oddField, interlace);
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

    private void BeginLine(long cycle, int line, bool oddField, bool interlace)
    {
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
