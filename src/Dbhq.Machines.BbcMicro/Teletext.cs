namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The Mullard SAA5050 teletext character generator, IC5 on the Model B: mode 7's picture. It
/// takes one character code per character cell and gives, for the scan line being drawn, the
/// colour of each of the cell's twelve half-dots.
/// </summary>
/// <remarks>
/// <para>
/// Built from the fact sheet <c>docs/bbc-micro/facts/video.md</c> section 4, and the Signetics
/// SAA5050/55 datasheet it cites (S11): Table 1 for the codes, Figures 9 and 10 for the graphics,
/// Figure 11 for the characters (the rows are Bedstead's CC0 table, <see cref="TeletextGlyphs"/>),
/// and its text on character rounding and the pins. Where the chip's behaviour comes from the
/// die-shot work rather than the datasheet (the sheet's S15), it says so.
/// </para>
/// <para>
/// <b>The cell (s4.1).</b> 6 dots by 10 lines a field, 20 a frame. The chip puts out a dot on
/// each edge of its 6 MHz clock, so a cell is 12 half-dots across. An alphanumeric is a 5 by 9
/// matrix below the cell's top line and right of its left column, each dot two half-dots wide.
/// </para>
/// <para>
/// <b>Character rounding (the datasheet, "Character rounding").</b> Each line of a field shows
/// one row of the matrix, and the chip compares it with a neighbouring row: the row before on the
/// even field and the row after on the odd one, as the CRS pin says (CRS is RA0 inverted on the
/// Model B, s1.5). Wherever the two rows make a diagonal across two adjacent dots, the dot that is
/// off gets the half nearest the dot that is on, so 5 by 9 becomes 10 by 18 over the two fields.
/// In double height the comparison goes up and down on alternate lines, from a signal inside the
/// chip, and CRS is not used.
/// </para>
/// <para>
/// <b>Graphics (Figures 9 and 10).</b> In graphics mode a code with bit 5 set is a block mosaic:
/// bits 0, 1, 2, 3, 4 and 6 light the top left, top right, middle left, middle right, bottom left
/// and bottom right of a 2 by 3 grid whose rows are 3, 4 and 3 lines of the 10. Codes <c>$40</c> to
/// <c>$5F</c> stay alphanumeric ("blast through"). Separated graphics leave each block's left two
/// half-dots and its last line as background (<see cref="SeparatedGap"/>).
/// </para>
/// <para>
/// <b>Control codes (s4.2, Table 1).</b> <c>$00</c> to <c>$1F</c> with bit 7 ignored, each shown as
/// a space in the background, or under hold graphics as the held block. Set-at codes act on their
/// own cell, set-after codes from the next. At the start of each displayed line (LOSE rising, the
/// datasheet's pin 26) the row's defaults come back: white alphanumerics, steady, normal height,
/// contiguous, black background, release, not concealed.
/// </para>
/// <para>
/// <b>What the chip counts itself (s4.1, s4.3).</b> It does not see RA. DEW (VSYNC) resets its line
/// count at each field and steps its flash counter; each line that had LOSE moves the count on, and
/// every ten lines a row. A row with a double height code makes the next row the lower half of the
/// pair, which shows the lower half of its own double height characters and the background
/// everywhere else. Flash is off for 16 fields of every 64 and on for 48 (the die-shot figure,
/// s4.3, not the datasheet's 0.75 Hz).
/// </para>
/// <para>
/// <b>Hold graphics (s4.3, from the die-shot description, S15).</b> The chip loads a graphics shift
/// register with each cell's block pattern, a space for anything that is not a block. Under hold
/// it does not load on a control cell in graphics mode but shows what the register already holds,
/// in that block's own contiguous or separated form; a height change on the cell stops that. Hold
/// takes effect from the cell after its code, so the hold code's own cell loads a space, which is
/// why the SAA5050 holds only blocks drawn after the hold code. Conceal hides the held block too.
/// </para>
/// <para>
/// Every place the sources stop and this class chooses is a constant or a remark here, and is in
/// <c>docs/known-differences.md</c>.
/// </para>
/// </remarks>
public sealed class Teletext
{
    /// <summary>Half-dots across a cell: 6 dots, each 2 (the chip uses both edges of its 6 MHz clock).</summary>
    public const int CellWidth = 12;

    /// <summary>Lines a character row has in one field (s4.1): 10, so 20 a frame.</summary>
    public const int LinesPerRow = 10;

    /// <summary>Fields a flash cycle lasts: the chip's flash counter is 6 bits (s4.3, the die-shot figure).</summary>
    public const int FlashCycleFields = 64;

    /// <summary>The first fields of each cycle, counted from 0, in which flashing characters are off (s4.3).</summary>
    public const int FlashOffFields = 16;

    /// <summary>
    /// What separated graphics take off each block, as background: the left two half-dots, and the
    /// last line of the block's 3, 4 or 3 in a field. Not given in figures by any source (s6 item 5):
    /// the datasheet's Figure 10, drawn to scale, shows each block flush with the top and right of
    /// its place and a gap left and below of about two of the cell's twelve half-dots and two of its
    /// twenty frame lines, which is also the sheet's S20, "shrink each block by 2 sub-pixels".
    /// </summary>
    public const int SeparatedGap = 2;

    /// <summary>
    /// Whether a height code that changes the height stops hold graphics on its own cell. The
    /// die-shot description (S15) says a height change pulse does this for "a height change control
    /// character"; for the double height code, which acts from the next cell, the model takes the
    /// pulse on the code's own cell too, which the description does not settle.
    /// </summary>
    public const bool HeightChangeStopsHoldOnItsOwnCell = true;

    // For each alphanumeric code $20 to $7F (index 0 to 95), 20 masks: one per glyph row 0 to 9
    // of the 10-line cell (row 0 is the blank top line) and per rounding direction (the row before,
    // then the row after). Bit i is half-dot i, the leftmost half-dot bit 0.
    private static readonly ushort[] AlphaMasks = MakeAlphaMasks();

    // For each block pattern (6 bits) and separated or not (bit 6), 10 masks, one per line 0 to 9 of
    // the cell.
    private static readonly ushort[] MosaicMasks = MakeMosaicMasks();

    // The chip's own count and row state, across lines.
    private int _lineInRow;
    private bool _lowerRow;
    private bool _rowHasDouble;
    private int _flashCount;

    // For the line being drawn: the mask indexes for normal and double height, and the flash.
    private int _normalAlpha, _doubleAlpha, _normalLine, _doubleLine;
    private bool _flashOn;
    private bool _lineLower;

    // The control state along a line, reset when the display starts (LOSE).
    private int _foreground, _background;
    private bool _graphics, _flash, _double, _separated, _hold, _conceal;

    // The graphics shift register: a block pattern (bits 0 to 5) and bit 6 for separated.
    private int _held;

    /// <summary>A chip at power on: line 0 of an upper row, the flash counter at 0.</summary>
    public Teletext() => PowerOn();

    /// <summary>The line of the character row the chip is on, 0 to 9, by its own count.</summary>
    public int LineInRow => _lineInRow;

    /// <summary>The row is the lower half of a double height pair.</summary>
    public bool LowerRow => _lowerRow;

    /// <summary>The flash counter, 0 to 63: one a field.</summary>
    public int FlashCount => _flashCount;

    /// <summary>Flashing characters show now: the counter is past its first <see cref="FlashOffFields"/> fields.</summary>
    public bool FlashOn => _flashCount >= FlashOffFields;

    /// <summary>
    /// Power on: line 0 of an upper row and the flash counter at 0. What the counts hold at power on
    /// is not documented (the datasheet's power-on reset names only the display modes, which the
    /// Model B does not use), so the model takes zero. The chip has no reset pin (the datasheet's
    /// pinning diagram), so BREAK leaves it as it is.
    /// </summary>
    public void PowerOn()
    {
        _lineInRow = 0;
        _lowerRow = false;
        _rowHasDouble = false;
        _flashCount = 0;
        BeginLine(roundFromRowBefore: true);
        StartDisplay();
    }

    /// <summary>
    /// DEW, the field's start (VSYNC on the Model B): the line count goes to 0 with an upper row, and
    /// the flash counter steps. The datasheet: DEW resets "the internal ROM row address counter prior
    /// to the display period" and is "used internally to derive the 'flash' period".
    /// </summary>
    /// <remarks>
    /// That the row state goes back to an upper row here, so a double height code on a field's
    /// last row does not make the next field's first row a lower half, is an assumption: no source
    /// says what resets the chip's height flip-flop (video.md s6 item 5).
    /// </remarks>
    public void FieldStart()
    {
        _lineInRow = 0;
        _lowerRow = false;
        _rowHasDouble = false;
        _flashCount = (_flashCount + 1) & (FlashCycleFields - 1);
    }

    /// <summary>
    /// A line has ended. If the chip had LOSE during it, its count moves on a line, and after the
    /// tenth a row: the new row is a lower half if the row just ended had a double height code and
    /// was not a lower half itself. On which edge the chip moves its count is not established
    /// (video.md s4.1, s6 item 5); the model takes the end of a line that had LOSE.
    /// </summary>
    public void LineEnd(bool hadDisplay)
    {
        if (!hadDisplay)
        {
            return;
        }

        if (++_lineInRow == LinesPerRow)
        {
            _lineInRow = 0;
            _lowerRow = _rowHasDouble && !_lowerRow;
            _rowHasDouble = false;
        }
    }

    /// <summary>
    /// A line starts, with the chip's own line count and row state. <paramref name="roundFromRowBefore"/>
    /// is the CRS pin, RA0 inverted on the Model B: set on the even field.
    /// </summary>
    public void BeginLine(bool roundFromRowBefore) =>
        SetLine(_lineInRow, roundFromRowBefore, FlashOn, _lowerRow);

    /// <summary>LOSE has risen: the display starts on this line, and the row's defaults come back.</summary>
    public void StartDisplay()
    {
        _foreground = 7;
        _background = 0;
        _graphics = false;
        _flash = false;
        _double = false;
        _separated = false;
        _hold = false;
        _conceal = false;
        _held = 0;
    }

    /// <summary>
    /// One displayed cell, the next along the line: its colours and which half-dots are foreground,
    /// packed as the mask in bits 0 to 11 (half-dot 0 in bit 0), the foreground in bits 12 to 14 and
    /// the background in bits 16 to 18, colours 0 to 7 as the ULA's physical colours. Then the
    /// code's set-after effects take hold for the cells after it.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int Cell(int code)
    {
        int c = code & 0x7F;
        if (c >= 0x20 && !_graphics)
        {
            // The common case, a character in alphanumerics mode, kept small enough to inline:
            // the graphics register loads a space.
            _held = 0;
            return Shown(AlphaMasks[((c - 0x20) * 20) + (_double ? _doubleAlpha : _normalAlpha)]) | (_foreground << 12) | (_background << 16);
        }

        return OtherCell(c);
    }

    /// <summary><see cref="Cell"/> for a control code, or any code in graphics mode.</summary>
    private int OtherCell(int c)
    {
        int mask;
        if (c < 0x20)
        {
            // Set-at effects first: they apply to this cell.
            bool heightChange = false;
            switch (c)
            {
                case 0x09:
                    _flash = false;
                    break;
                case 0x0C:
                    heightChange = _double;
                    _double = false;
                    break;
                case 0x0D:
                    heightChange = !_double && HeightChangeStopsHoldOnItsOwnCell;
                    break;
                case 0x18:
                    _conceal = true;
                    break;
                case 0x19:
                    _separated = false;
                    break;
                case 0x1A:
                    _separated = true;
                    break;
                case 0x1C:
                    _background = 0;
                    break;
                case 0x1D:
                    _background = _foreground;
                    break;
                case 0x00 or 0x10:
                    // NUL and DLE, ETSI's alpha black and graphics black. The datasheet's Table 1
                    // marks them reserved, "normally displayed as spaces", so they do nothing but
                    // show a space. An assumption: whether the BBC's chip really ignores them is
                    // not established (video.md s6 item 5).
                    break;
                case 0x0A or 0x0B or 0x0E or 0x0F or 0x1B:
                    // End box, start box, SO, SI and ESC do nothing but show a space. An
                    // assumption: Table 1 marks SO, SI and ESC reserved, and the boxes depend on
                    // the PO and DE pins, whose tie-offs on the board were not read (video.md s6
                    // item 6).
                    break;
            }

            if (_hold && _graphics && !heightChange)
            {
                // The register is not loaded: it shows the block it holds.
                mask = MosaicMasks[(_held * LinesPerRow) + (_double ? _doubleLine : _normalLine)];
            }
            else
            {
                _held = 0;
                mask = 0;
            }

            mask = Shown(mask);
            int packed = mask | (_foreground << 12) | (_background << 16);

            // Set-after effects: from the next cell.
            switch (c)
            {
                case >= 0x01 and <= 0x07:
                    _foreground = c;
                    _graphics = false;
                    _conceal = false;
                    break;
                case 0x08:
                    _flash = true;
                    break;
                case 0x0D:
                    _double = true;
                    _rowHasDouble = true;
                    break;
                case >= 0x11 and <= 0x17:
                    _foreground = c & 7;
                    _graphics = true;
                    _conceal = false;
                    break;
                case 0x1E:
                    // Hold acts from the next cell on the SAA5050 (video.md s4.3, the die-shot
                    // description), not on its own cell as ETSI has it.
                    _hold = true;
                    break;
                case 0x1F:
                    // Release acts from the next cell. An assumption: the die-shot simulation
                    // took two characters to clear hold, which its author doubts (video.md s6
                    // item 11); the model takes one, as for the set-after codes.
                    _hold = false;
                    break;
            }

            return packed;
        }

        if (_graphics && (c & 0x20) != 0)
        {
            _held = (c & 0x1F) | ((c & 0x40) >> 1) | (_separated ? 0x40 : 0);
            mask = MosaicMasks[(_held * LinesPerRow) + (_double ? _doubleLine : _normalLine)];
        }
        else
        {
            // An alphanumeric, in alphanumerics mode or blasting through in graphics mode: the
            // graphics register loads a space. For a capital blasting through that is inferred
            // from the die-shot description's "no alternate graphics" signal (video.md s4.3,
            // S15); ETSI would leave the held block alone.
            _held = 0;
            mask = AlphaMasks[((c - 0x20) * 20) + (_double ? _doubleAlpha : _normalAlpha)];
        }

        return Shown(mask) | (_foreground << 12) | (_background << 16);
    }

    /// <summary>
    /// One scan line of a row on its own, for a test: <paramref name="codes"/> from the start of the
    /// display, on line <paramref name="lineInRow"/> (0 to 9) of the field, with the given field,
    /// flash phase and row half, written as one colour (0 to 7) a half-dot into
    /// <paramref name="pixels"/>, twelve a cell. The chip's own counts are left as they were.
    /// </summary>
    public void RenderLine(ReadOnlySpan<byte> codes, int lineInRow, bool oddField, bool flashOn, bool lowerRow, Span<byte> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineInRow);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lineInRow, LinesPerRow);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixels.Length, codes.Length * CellWidth);
        bool rowHasDouble = _rowHasDouble;
        SetLine(lineInRow, !oddField, flashOn, lowerRow);
        StartDisplay();
        for (int i = 0; i < codes.Length; i++)
        {
            int packed = Cell(codes[i]);
            byte foreground = (byte)((packed >> 12) & 7);
            byte background = (byte)((packed >> 16) & 7);
            Span<byte> cell = pixels.Slice(i * CellWidth, CellWidth);
            for (int h = 0; h < CellWidth; h++)
            {
                cell[h] = (packed & (1 << h)) != 0 ? foreground : background;
            }
        }
        _rowHasDouble = rowHasDouble;
    }

    private void SetLine(int line, bool roundFromRowBefore, bool flashOn, bool lowerRow)
    {
        _lineLower = lowerRow;
        _flashOn = flashOn;
        _normalLine = line;
        _normalAlpha = (2 * line) + (roundFromRowBefore ? 0 : 1);

        // Double height: each row of the upper or lower half of the cell is two lines of the
        // field, rounded from the row before on the first and the row after on the second. The
        // model's own choice: the upper row shows cell lines 0 to 4 (the blank line and glyph rows
        // 0 to 3), the lower row lines 5 to 9; the datasheet says only that the rounding alternates
        // every line, and no source gives the mapping (video.md s4.3, s6 item 5).
        _doubleLine = (line + (lowerRow ? LinesPerRow : 0)) >> 1;
        _doubleAlpha = (2 * _doubleLine) + (line & 1);
    }

    /// <summary>What the cell's attributes leave of its foreground.</summary>
    private int Shown(int mask)
    {
        if (_conceal || (_flash && !_flashOn) || (_lineLower && !_double))
        {
            return 0;
        }

        return mask;
    }

    private static ushort[] MakeAlphaMasks()
    {
        var masks = new ushort[96 * 20];
        Span<int> rows = stackalloc int[LinesPerRow];
        for (int code = 0x20; code < 0x80; code++)
        {
            ReadOnlySpan<byte> glyph = TeletextGlyphs.Rows(code);
            rows[0] = 0;
            for (int r = 0; r < TeletextGlyphs.RowsPerGlyph; r++)
            {
                rows[r + 1] = glyph[r];
            }

            for (int line = 0; line < LinesPerRow; line++)
            {
                int current = rows[line];
                // The rows above the cell's first line and below its last are taken as blank: the
                // chip reads its ROM for one character, and no source says otherwise.
                int before = line > 0 ? rows[line - 1] : 0;
                int after = line < LinesPerRow - 1 ? rows[line + 1] : 0;
                masks[((code - 0x20) * 20) + (2 * line)] = Rounded(current, before);
                masks[((code - 0x20) * 20) + (2 * line) + 1] = Rounded(current, after);
            }
        }
        return masks;
    }

    /// <summary>
    /// One row of a glyph as twelve half-dots, rounded against <paramref name="neighbour"/>: dot d
    /// (0 the leftmost, bit 4 of the row) is half-dots 2 + 2d and 3 + 2d, after the cell's blank left
    /// column; where the two rows cross diagonally between adjacent dots, the dot that is off in this
    /// row gets its half next to the dot that is on.
    /// </summary>
    private static ushort Rounded(int row, int neighbour)
    {
        int mask = 0;
        for (int d = 0; d < 5; d++)
        {
            if (Dot(row, d))
            {
                mask |= 3 << (2 + (2 * d));
            }
        }

        for (int d = 0; d < 4; d++)
        {
            bool left = Dot(row, d), right = Dot(row, d + 1);
            bool aboveLeft = Dot(neighbour, d), aboveRight = Dot(neighbour, d + 1);
            if (left && !right && !aboveLeft && aboveRight)
            {
                mask |= 1 << (2 + (2 * (d + 1)));
            }
            else if (!left && right && aboveLeft && !aboveRight)
            {
                mask |= 1 << (3 + (2 * d));
            }
        }

        return (ushort)mask;
    }

    private static bool Dot(int row, int d) => (row & (0x10 >> d)) != 0;

    private static ushort[] MakeMosaicMasks()
    {
        var masks = new ushort[128 * LinesPerRow];
        for (int pattern = 0; pattern < 128; pattern++)
        {
            bool separated = (pattern & 0x40) != 0;
            for (int line = 0; line < LinesPerRow; line++)
            {
                // Rows of 3, 4 and 3 lines (Figure 9); the last line of each is the separated gap.
                int blockRow = line < 3 ? 0 : line < 7 ? 1 : 2;
                bool lastLine = line is 2 or 6 or 9;
                int mask = 0;
                if (!(separated && lastLine))
                {
                    int from = separated ? SeparatedGap : 0;
                    if ((pattern & (1 << (2 * blockRow))) != 0)
                    {
                        mask |= HalfDots(from, 6);
                    }
                    if ((pattern & (2 << (2 * blockRow))) != 0)
                    {
                        mask |= HalfDots(6 + from, 12);
                    }
                }
                masks[(pattern * LinesPerRow) + line] = (ushort)mask;
            }
        }
        return masks;
    }

    private static int HalfDots(int from, int to) => ((1 << to) - 1) & ~((1 << from) - 1);
}
