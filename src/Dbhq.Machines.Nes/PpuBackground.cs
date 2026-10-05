namespace Dbhq.Machines.Nes;

/// <summary>The background half of the picture processing unit: the shifters and the 8-dot tile fetch (ppu.md 6). The rest of the class is in <c>Ppu.cs</c>.</summary>
public sealed partial class Ppu
{
    private void Shift()
    {
        _patternLow <<= 1;
        _patternHigh <<= 1;
        _attributeLow <<= 1;
        _attributeHigh <<= 1;
    }

    // The fetched tile into the low halves of the shifters, its attribute bits spread to 8.
    private void Reload()
    {
        _patternLow = (ushort)((_patternLow & 0xFF00) | _patternLowByte);
        _patternHigh = (ushort)((_patternHigh & 0xFF00) | _patternHighByte);
        _attributeLow = (ushort)((_attributeLow & 0xFF00) | ((_attributeBits & 1) != 0 ? 0xFF : 0x00));
        _attributeHigh = (ushort)((_attributeHigh & 0xFF00) | ((_attributeBits & 2) != 0 ? 0xFF : 0x00));
    }

    // The 8-dot fetch of one background tile (ppu.md 6): each read on the second dot of its pair.
    private void FetchBackground(int dot)
    {
        switch (dot & 7)
        {
            case 2:
                _nametableByte = _nametables[NametableIndex((ushort)(0x2000 | (_v & 0x0FFF)))];
                break;

            case 4:
            {
                int address = 0x23C0 | (_v & 0x0C00) | ((_v >> 4) & 0x38) | ((_v >> 2) & 0x07);
                byte attribute = _nametables[NametableIndex((ushort)address)];

                // The quadrant: coarse Y bit 1 picks the bottom half, coarse X bit 1 the right.
                _attributeBits = (attribute >> (((_v >> 4) & 4) | (_v & 2))) & 3;
                break;
            }

            case 5:
                _patternAddress = (ushort)(((_ctrl & 0x10) << 8) | (_nametableByte << 4) | ((_v >> 12) & 7));
                _mapper.PpuAddressChanged(_patternAddress, CpuCycle);
                break;

            case 6:
                _patternLowByte = _mapper.PpuRead(_patternAddress);
                break;

            case 7:
                _mapper.PpuAddressChanged((ushort)(_patternAddress + 8), CpuCycle);
                break;

            case 0:
                _patternHighByte = _mapper.PpuRead((ushort)(_patternAddress + 8));
                _v = IncrementCoarseX(_v);
                break;
        }
    }
}
