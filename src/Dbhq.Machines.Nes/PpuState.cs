namespace Dbhq.Machines.Nes;

/// <summary>The picture processing unit's state report, for the differential and the tests. The rest of the class is in <c>Ppu.cs</c>.</summary>
public sealed partial class Ppu
{
    /// <summary>
    /// Every field of the PPU, as <see cref="IReportsState"/> asks: the registers, the scroll
    /// registers, the I/O latch and when each bit was driven, the read buffer, the position, the
    /// flags, PPUMASK's caches, the palette, the nametable RAM, OAM, the background's latches and
    /// shifters, sprite evaluation, the sprite fetches and the line buffer, and the picture. A
    /// field added to the class goes here too; <c>StateReportTests</c> fails until it does.
    /// </summary>
    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Skip(nameof(_region), StateReport.Fixed);
        sink.Skip(nameof(_mapper), "the board, which the bus reports");
        sink.Skip(nameof(_watchesAddresses), StateReport.Fixed);
        sink.Skip(nameof(_chr), "the board's pattern memory, which it reports where it is RAM; ROM never changes");
        sink.Skip(nameof(_chrWindows), "the board's pattern windows: a board reports its own, and NROM's are fixed when it is made");
        sink.Skip(nameof(_nametablePages), "the board's nametable layout: a board reports its own, and NROM's is fixed when it is made");
        sink.Skip(nameof(_preRenderLine), StateReport.Fixed);
        sink.Skip(nameof(_lines), StateReport.Fixed);
        sink.Skip(nameof(_oddFrameSkipsADot), StateReport.Fixed);

        // The registers, the latch and the read buffer.
        sink.Add(nameof(_ctrl), _ctrl);
        sink.Add(nameof(_mask), _mask);
        sink.Add(nameof(_status), _status);
        sink.Add(nameof(_oamAddress), _oamAddress);
        sink.Add(nameof(_latch), _latch);
        sink.Add(nameof(_readBuffer), _readBuffer);
        sink.Add(nameof(_latchDriven), _latchDriven);
        sink.Skip(nameof(_latchDecayDots), StateReport.Fixed);
        sink.Add(nameof(_timeBase), _timeBase);

        // The scroll registers.
        sink.Add(nameof(_v), _v);
        sink.Add(nameof(_t), _t);
        sink.Add(nameof(_x), _x);
        sink.Add(nameof(_w), _w);

        // The position and the frame.
        sink.Add(nameof(_line), _line);
        sink.Add(nameof(_dot), _dot);
        sink.Add(nameof(_oddFrame), _oddFrame);
        sink.Add(nameof(_frame), _frame);
        sink.Add(nameof(_suppressVblank), _suppressVblank);
        sink.Add(nameof(_dropDot), _dropDot);
        sink.Add(nameof(CpuCycle), CpuCycle);

        // The colours and PPUMASK's caches.
        sink.Skip(nameof(_colours), StateReport.Fixed);
        sink.Add(nameof(_emphasis), _emphasis);
        sink.Add(nameof(_greyscaleMask), _greyscaleMask);
        sink.Add(nameof(_entryColours), _entryColours);
        sink.Add(nameof(_backgroundFrom), _backgroundFrom);
        sink.Add(nameof(_spritesFrom), _spritesFrom);

        // The memories.
        sink.Add(nameof(_palette), _palette);
        sink.Add(nameof(_nametables), _nametables);
        sink.Add(nameof(Oam), Oam);

        // The background.
        sink.Add(nameof(_nametableByte), _nametableByte);
        sink.Add(nameof(_attributeBits), _attributeBits);
        sink.Add(nameof(_patternLowByte), _patternLowByte);
        sink.Add(nameof(_patternHighByte), _patternHighByte);
        sink.Add(nameof(_patternAddress), _patternAddress);
        sink.Add(nameof(_backgroundPixels), _backgroundPixels);

        // Sprite evaluation.
        sink.Add(nameof(_secondaryOam), _secondaryOam);
        sink.Add(nameof(_evaluationN), _evaluationN);
        sink.Add(nameof(_evaluationM), _evaluationM);
        sink.Add(nameof(_oamLatch), _oamLatch);
        sink.Add(nameof(_secondaryIndex), _secondaryIndex);
        sink.Add(nameof(_found), _found);
        sink.Add(nameof(_secondaryFull), _secondaryFull);
        sink.Add(nameof(_evaluationDone), _evaluationDone);
        sink.Add(nameof(_sprite0Found), _sprite0Found);

        // The sprites of the line being drawn, and the fetches for the next.
        sink.Add(nameof(_spriteLine), _spriteLine);
        sink.Add(nameof(_spriteLeft), _spriteLeft);
        sink.Add(nameof(_spriteWidth), _spriteWidth);
        sink.Add(nameof(_spriteCount), _spriteCount);
        sink.Add(nameof(_sprite0OnLine), _sprite0OnLine);
        sink.Add(nameof(_fetchY), _fetchY);
        sink.Add(nameof(_fetchTile), _fetchTile);
        sink.Add(nameof(_fetchAttributes), _fetchAttributes);
        sink.Add(nameof(_fetchX), _fetchX);
        sink.Add(nameof(_fetchLow), _fetchLow);
        sink.Add(nameof(_spriteAddress), _spriteAddress);

        // The picture, last: it is the largest.
        sink.Skip(nameof(_pixels), "the same array as Screen.Pixels, reported with Screen");
        sink.Add(nameof(Screen), Screen);
        sink.Skip(nameof(Observer), "the observer itself");
    }
}
