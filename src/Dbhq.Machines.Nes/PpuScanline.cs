using System.Runtime.CompilerServices;

namespace Dbhq.Machines.Nes;

/// <summary>
/// The fast scanline renderer (task 3 of the lazy chips plan, <c>docs/superpowers/plans/2026-10-06-nes-lazy-chips.md</c>):
/// a whole line run in one call, inside a catch-up that owes all of it, leaving the PPU as the
/// per-dot path (<see cref="RunDot"/>) leaves it at the line's end. The rest of the class is in
/// <c>Ppu.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>When.</b> <see cref="CatchUp"/> asks at dot 0 of a line, when the dots owed reach past the
/// line's last dot, so no register access, board write or OAM DMA write falls inside it: the bus
/// catches up before each of those, so a catch-up never runs past one. Nothing can see the PPU
/// between the line's dots then, and only the state at its end matters. The line is taken when
/// the board does not watch the PPU's address bus (a board that watches, MMC3, is caught up every
/// cycle and never owes a whole line, and is refused here as well, since the fast path tells it
/// nothing) and it is one of:
/// </para>
/// <list type="bullet">
/// <item>a line after the picture or in VBlank, but not the pre-render line: the per-dot path
/// changes nothing on it but the position, and on line 241 the VBlank flag at dot 1;</item>
/// <item>a visible line with rendering off: the backdrop, or the palette entry <c>v</c> points
/// at, on columns 0 to 255, and at dot 257 no sprite fetched for the next line;</item>
/// <item>a visible line with the background on and the sprites off, on a board that keeps its
/// pattern tables as windows and its nametables as a page table, so a fetch is an array read: the
/// background's 34 tiles, the line's pixels, the Y increment and the horizontal copy. Sprite
/// evaluation and the sprite fetches run with either layer on (ppu.md 7), so they are run too,
/// with the per-dot code, for the state they leave: secondary OAM, the overflow flag, the line
/// buffer and the fetch latches. With the sprites off no sprite pixel and no sprite 0 hit can
/// come, so the pixel is the background's alone.</item>
/// </list>
/// <para>
/// Everything else is refused, and the per-dot path runs it: the pre-render line (its vertical
/// copy, the flags at dot 1 and the odd frame's dropped dot), any visible line with the sprites
/// on (task 4), a background line on a board without windows, and any line when
/// <see cref="WholeLines"/> is off. Refusing changes nothing, so it is always safe.
/// </para>
/// <para>
/// No call through an interface and no allocation is made in a line, and the board is never
/// told an address: the line is taken only when it does not watch.
/// </para>
/// </remarks>
public sealed partial class Ppu
{
    // Whether a catch-up may take a whole line here, and the lines each path has run: for the
    // tests and the differential's coverage. They are not the chip's state (PpuState.cs).
    private bool _wholeLines = true;
    private long _fastLines;
    private long _exactLines;

    /// <summary>Whether a catch-up may render a whole line at once (on unless a test turns it off).</summary>
    internal bool WholeLines
    {
        get => _wholeLines;
        set => _wholeLines = value;
    }

    /// <summary>Lines the fast path has run since the PPU was made.</summary>
    internal long FastLines => _fastLines;

    /// <summary>Lines the per-dot path has finished since the PPU was made.</summary>
    internal long ExactLines => _exactLines;

    /// <summary>
    /// Runs the whole of the line the PPU is at, from its dot 0 to its last, as its dots would, and
    /// returns true; or returns false, having changed nothing, when it may not (see the remarks).
    /// The caller is <see cref="CatchUp"/>, at dot 0 with at least a line's dots owed.
    /// </summary>
    private bool TryRenderLine()
    {
        if (!_wholeLines || _watchesAddresses)
        {
            return false;
        }

        int line = _line;
        if (line >= FrameBuffer.Height)
        {
            if (line == _preRenderLine)
            {
                return false;
            }

            if (line == VblankLine)
            {
                // Dot 1 of line 241 (RunDot).
                if (!_suppressVblank)
                {
                    _status |= StatusVblank;
                }

                _suppressVblank = false;
            }
        }
        else
        {
            int layers = _mask & 0x18;
            if (layers == 0)
            {
                RenderOffLine();
            }
            else if (layers == 0x08 && _chrWindows is not null && _nametablePages is not null)
            {
                RenderBackgroundLine();
            }
            else
            {
                return false;
            }
        }

        // The line is not the last (the pre-render line is refused), so no frame ends here.
        _line = line + 1;
        _caughtUpDots += Region.DotsPerLine;
        _fastLines++;
        return true;
    }

    // A visible line with rendering off: dots 2 to 257 draw columns 0 to 255 in the backdrop, or
    // the palette entry v points at, which no dot of the line moves; dot 257 leaves no sprite for
    // the next line.
    private void RenderOffLine()
    {
        int entry = (_v & 0x3F00) == 0x3F00 ? PaletteIndex(_v) : 0;
        _pixels.AsSpan(_line << 8, FrameBuffer.Width).Fill(_entryColours[entry]);
        _spriteCount = 0;
        ClearSpriteLine();
    }

    // A visible line with the background on and the sprites off. Dots 2 to 257: column x on dot
    // x + 2 from the shifters as they stand, which shift a pixel a dot and take each fetched tile
    // in their low half on dots 9, 17 ... 257, so the 8 columns from 8k are drawn from one value
    // of the shifters, column 8k + i from its pixel 15 - fine X - i. Dots 1 to 256 fetch a tile
    // each 8 dots, the nametable byte, the attribute byte and the two pattern bytes, with v as it
    // stands, and the eighth dot moves coarse X on; dot 256 the Y increment, dot 257 the
    // horizontal copy. Then the sprites (evaluation and fetches, with the per-dot code), and dots
    // 321 to 337: the next line's first two tiles, the shifters ending with the first in their
    // high half and the second in their low.
    private void RenderBackgroundLine()
    {
        uint[] colours = _entryColours;
        uint[] pixels = _pixels;
        int patternTable = (_ctrl & 0x10) << 8;
        int top = (15 - _x) << 2;
        int from = _backgroundFrom;
        int row = _line << 8;
        ushort v = _v;
        ulong shifters = _backgroundPixels;
        for (int x = 0; x < FrameBuffer.Width; x += 8)
        {
            for (int i = 0; i < 8; i++)
            {
                int column = x + i;
                int pixel = column >= from ? (int)(shifters >> (top - (i << 2))) & 0xF : 0;
                pixels[row | column] = colours[pixel];
            }

            shifters = (shifters << 32) | FetchTile(v, patternTable);
            v = IncrementCoarseX(v);
        }

        // Dot 256's Y increment, then dot 257's copy of t's horizontal bits.
        v = IncrementY(v);
        v = (ushort)((v & ~0x041F) | (_t & 0x041F));

        // The next line's first two tiles, on dots 321 to 336: dot 329 takes the first into the
        // shifters, which by dot 337 have moved it to their high half and take the second. The
        // background's latches are left as the second's fetch leaves them.
        uint first = FetchTile(v, patternTable);
        v = IncrementCoarseX(v);
        uint second = FetchTile(v, patternTable);
        _v = IncrementCoarseX(v);
        _backgroundPixels = ((ulong)first << 32) | second;

        EvaluateLine();
        FetchSpritesForNextLine();
    }

    // One tile's fetch with v as it stands (FetchNametableByte to FetchPatternHigh): the
    // nametable byte, the attribute bits of the tile's quadrant and the two pattern bytes, each
    // read from the board's page table and windows and kept in the latches as the per-dot fetch
    // keeps them; it returns the 8 pixels the shifters take from them.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint FetchTile(int v, int patternTable)
    {
        byte[] nametables = _nametables;
        int page = _nametablePages![(v >> 10) & 3] << 10;
        byte tile = nametables[page | (v & 0x3FF)];
        byte attribute = nametables[page | 0x3C0 | ((v >> 4) & 0x38) | ((v >> 2) & 0x07)];

        // The quadrant: coarse Y bit 1 picks the bottom half, coarse X bit 1 the right.
        int attributes = (attribute >> (((v >> 4) & 4) | (v & 2))) & 3;
        int address = patternTable | (tile << 4) | ((v >> 12) & 7);
        byte[] chr = _chr!;
        int[] windows = _chrWindows!;
        byte low = chr[windows[address >> 10] + (address & 0x3FF)];
        int upper = address + 8;
        byte high = chr[windows[upper >> 10] + (upper & 0x3FF)];
        _nametableByte = tile;
        _attributeBits = attributes;
        _patternAddress = (ushort)address;
        _patternLowByte = low;
        _patternHighByte = high;
        return Tile(low, high, attributes);
    }

    // A tile's 8 pixels as the shifters take them (Reload): each pixel's 2 pattern bits, with
    // its 2 attribute bits where it is not transparent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Tile(byte low, byte high, int attributes)
    {
        uint pixels = Spread[low] | (Spread[high] << 1);
        uint opaque = (pixels | (pixels >> 1)) & 0x11111111;
        return pixels | (opaque * (uint)(attributes << 2));
    }

    // Sprite evaluation on dots 1 to 256 (Evaluate): dot 1 starts it, dots 1 to 64 clear
    // secondary OAM, and from dot 65 each odd dot reads OAM and each even dot acts on it, until
    // the search is over. After that the odd dots read the same byte again, as n and m stay.
    private void EvaluateLine()
    {
        Array.Fill(_secondaryOam, (byte)0xFF);
        _evaluationN = 0;
        _evaluationM = 0;
        _secondaryIndex = 0;
        _found = 0;
        _secondaryFull = false;
        _evaluationDone = false;
        _sprite0Found = false;
        for (int dot = 65; dot <= 256; dot += 2)
        {
            _oamLatch = _oam[(_evaluationN << 2) | _evaluationM];
            if (_evaluationDone)
            {
                break;
            }

            EvaluationStep();
        }
    }

    // Dots 257 to 320 (RenderDot): the next line's sprites are those found, the line buffer is
    // emptied, OAMADDR is held at 0, and each of the 8 slots is fetched with the per-dot code.
    private void FetchSpritesForNextLine()
    {
        _spriteCount = _found;
        _sprite0OnLine = _sprite0Found;
        ClearSpriteLine();
        _oamAddress = 0;
        for (int k = 0; k < 64; k++)
        {
            FetchSprite(k);
        }
    }
}
