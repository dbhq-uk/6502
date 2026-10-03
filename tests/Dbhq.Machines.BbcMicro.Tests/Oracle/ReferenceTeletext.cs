// The test oracle for the SAA5050: the same chip as Teletext (its remarks, from video.md section 4
// and the datasheet), written as plainly as possible and separately, with no lookup tables: each
// half-dot of each cell is worked out on its own from the glyph rows, the rounding rule, the block
// geometry and the control state. Only the glyph rows (data) are shared with the production code.
// Never used by the machine; the equivalence tests run it inside the reference ULA.
namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>A teletext chip that works out each half-dot of each cell directly.</summary>
public sealed class ReferenceTeletext
{
    private int _lineInRow;
    private bool _lowerRow, _rowHasDouble;
    private int _flashCount;

    private int _line;
    private bool _crsBefore, _flashOn, _lower;

    private int _foreground, _background;
    private bool _graphics, _flash, _double, _separated, _hold, _conceal;

    // The graphics shift register: the block code it holds (-1 for a space) and its form.
    private int _heldCode = -1;
    private bool _heldSeparated;

    public void PowerOn()
    {
        _lineInRow = 0;
        _lowerRow = false;
        _rowHasDouble = false;
        _flashCount = 0;
    }

    public void FieldStart()
    {
        _lineInRow = 0;
        _lowerRow = false;
        _rowHasDouble = false;
        _flashCount = (_flashCount + 1) % 64;
    }

    public void LineEnd(bool hadDisplay)
    {
        if (!hadDisplay)
        {
            return;
        }

        _lineInRow++;
        if (_lineInRow == 10)
        {
            _lineInRow = 0;
            _lowerRow = _rowHasDouble && !_lowerRow;
            _rowHasDouble = false;
        }
    }

    public void BeginLine(int rasterAddress)
    {
        _line = _lineInRow;
        _crsBefore = (rasterAddress & 1) == 0;
        _flashOn = _flashCount >= 16;
        _lower = _lowerRow;
    }

    public void StartDisplay()
    {
        _foreground = 7;
        _background = 0;
        _graphics = _flash = _double = _separated = _hold = _conceal = false;
        _heldCode = -1;
    }

    /// <summary>The next displayed cell: each half-dot's colour, 0 to 7, left to right.</summary>
    public int[] Cell(int code)
    {
        int c = code & 0x7F;
        var colours = new int[12];
        Func<int, bool> lit;
        if (c < 0x20)
        {
            bool heightChange = false;
            if (c == 0x09) { _flash = false; }
            if (c == 0x0C) { heightChange = _double; _double = false; }
            if (c == 0x0D) { heightChange = !_double; }
            if (c == 0x18) { _conceal = true; }
            if (c == 0x19) { _separated = false; }
            if (c == 0x1A) { _separated = true; }
            if (c == 0x1C) { _background = 0; }
            if (c == 0x1D) { _background = _foreground; }

            if (_hold && _graphics && !heightChange && _heldCode >= 0)
            {
                int held = _heldCode;
                bool separated = _heldSeparated;
                lit = h => BlockLit(held, separated, h);
            }
            else
            {
                if (!(_hold && _graphics && !heightChange))
                {
                    _heldCode = -1;
                }
                lit = _ => false;
            }

            Paint(colours, lit);

            if (c >= 0x01 && c <= 0x07) { _foreground = c; _graphics = false; _conceal = false; }
            if (c == 0x08) { _flash = true; }
            if (c == 0x0D) { _double = true; _rowHasDouble = true; }
            if (c >= 0x11 && c <= 0x17) { _foreground = c - 0x10; _graphics = true; _conceal = false; }
            if (c == 0x1E) { _hold = true; }
            if (c == 0x1F) { _hold = false; }
            return colours;
        }

        if (_graphics && (c & 0x20) != 0)
        {
            _heldCode = c;
            _heldSeparated = _separated;
            bool separated = _separated;
            lit = h => BlockLit(c, separated, h);
        }
        else
        {
            _heldCode = -1;
            lit = h => GlyphLit(c, h);
        }

        Paint(colours, lit);
        return colours;
    }

    private void Paint(int[] colours, Func<int, bool> lit)
    {
        bool hidden = _conceal || (_flash && !_flashOn) || (_lower && !_double);
        for (int h = 0; h < 12; h++)
        {
            colours[h] = !hidden && lit(h) ? _foreground : _background;
        }
    }

    /// <summary>Which line of the 10-line cell this field line shows, and which neighbour it rounds against.</summary>
    private (int Source, int Neighbour) Lines()
    {
        if (!_double)
        {
            return (_line, _crsBefore ? _line - 1 : _line + 1);
        }

        int source = (_line + (_lower ? 10 : 0)) / 2;
        return (source, _line % 2 == 0 ? source - 1 : source + 1);
    }

    private bool BlockLit(int code, bool separated, int h)
    {
        int source = Lines().Source;
        int band = source <= 2 ? 0 : source <= 6 ? 1 : 2;
        bool right = h >= 6;
        int bit = band == 2 && right ? 6 : (2 * band) + (right ? 1 : 0);
        if ((code & (1 << bit)) == 0)
        {
            return false;
        }

        if (separated && (h % 6 < 2 || source == 2 || source == 6 || source == 9))
        {
            return false;
        }
        return true;
    }

    private bool GlyphLit(int code, int h)
    {
        if (h < 2)
        {
            return false;
        }

        (int source, int neighbour) = Lines();
        int d = (h - 2) / 2;
        if (DotOn(code, source, d))
        {
            return true;
        }

        if ((h - 2) % 2 == 0)
        {
            // The left half of an off dot: on if the dot to its left is on here and off in the
            // neighbour, while this dot is on in the neighbour.
            return d >= 1 && DotOn(code, source, d - 1) && !DotOn(code, neighbour, d - 1) && DotOn(code, neighbour, d);
        }

        // The right half: the mirror image.
        return d <= 3 && DotOn(code, source, d + 1) && !DotOn(code, neighbour, d + 1) && DotOn(code, neighbour, d);
    }

    private static bool DotOn(int code, int cellLine, int dot)
    {
        if (cellLine < 1 || cellLine > 9)
        {
            return false;
        }

        return ((TeletextGlyphs.Rows(code)[cellLine - 1] >> (4 - dot)) & 1) != 0;
    }
}
