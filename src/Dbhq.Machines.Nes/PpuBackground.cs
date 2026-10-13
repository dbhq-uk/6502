using System.Runtime.CompilerServices;

namespace Dbhq.Machines.Nes;

/// <summary>The background half of the picture processing unit: the shifters and the 8-dot tile fetch (ppu.md 6). The rest of the class is in <c>Ppu.cs</c>.</summary>
public sealed partial class Ppu
{
    // Each byte with bit b moved to bit 4b: a pattern byte's 8 pixels, one to each 4 bits.
    private static readonly uint[] Spread = BuildSpread();

    // The chip's four shifters move one bit: the combined one moves one pixel.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Shift()
    {
        _backgroundPixels <<= 4;
    }

    // The fetched tile into the low halves of the shifters, its attribute bits spread to 8: in the
    // combined shifter, 8 pixels into the low 32 bits, each with the attribute bits where it is
    // not transparent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reload()
    {
        uint pixels = Spread[_patternLowByte] | (Spread[_patternHighByte] << 1);
        uint opaque = (pixels | (pixels >> 1)) & 0x11111111;
        pixels |= opaque * (uint)(_attributeBits << 2);
        _backgroundPixels = (_backgroundPixels & 0xFFFFFFFF00000000UL) | pixels;
    }

    private static uint[] BuildSpread()
    {
        var table = new uint[256];
        for (int value = 0; value < 256; value++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                table[value] |= (uint)((value >> bit) & 1) << (4 * bit);
            }
        }

        return table;
    }

    // The 8-dot fetch of one background tile (ppu.md 6): each read on the second dot of its pair.
    // The dots 2 to 256 of a visible line call the steps from their own dispatch (RenderVisibleDot).
    private void FetchBackground(int dot)
    {
        switch (dot & 7)
        {
            case 2:
                FetchNametableByte();
                break;

            case 4:
                FetchAttributeBits();
                break;

            case 5:
                PutPatternLowAddress();
                break;

            case 6:
                FetchPatternLow();
                break;

            case 7:
                PutPatternHighAddress();
                break;

            case 0:
                FetchPatternHigh();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FetchNametableByte()
    {
        _nametableByte = _nametables[NametableIndex((ushort)(0x2000 | (_v & 0x0FFF)))];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FetchAttributeBits()
    {
        int address = 0x23C0 | (_v & 0x0C00) | ((_v >> 4) & 0x38) | ((_v >> 2) & 0x07);
        byte attribute = _nametables[NametableIndex((ushort)address)];

        // The quadrant: coarse Y bit 1 picks the bottom half, coarse X bit 1 the right.
        _attributeBits = (attribute >> (((_v >> 4) & 4) | (_v & 2))) & 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PutPatternLowAddress()
    {
        _patternAddress = (ushort)(((_ctrl & 0x10) << 8) | (_nametableByte << 4) | ((_v >> 12) & 7));
        Fetching(_patternAddress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FetchPatternLow()
    {
        _patternLowByte = ReadPattern(_patternAddress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PutPatternHighAddress()
    {
        Fetching((ushort)(_patternAddress + 8));
    }

    // The last read of the tile, and coarse X moves on.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FetchPatternHigh()
    {
        _patternHighByte = ReadPattern((ushort)(_patternAddress + 8));
        _v = IncrementCoarseX(_v);
    }
}
