using System.Runtime.CompilerServices;

namespace Dbhq.Machines.Nes;

/// <summary>The sprite half of the picture processing unit: evaluation, the fetches and the line buffer (ppu.md 7 and 9). The rest of the class is in <c>Ppu.cs</c>.</summary>
public sealed partial class Ppu
{
    // One dot of sprite evaluation on a visible line, dots 1 to 256 (ppu.md 7 and 9). The dots 2
    // to 256 call the odd or the even half from their own dispatch (RenderVisibleDot).
    private void Evaluate(int dot)
    {
        if (dot == 1)
        {
            Array.Fill(_secondaryOam, (byte)0xFF);
            _evaluationN = 0;
            _evaluationM = 0;
            _secondaryIndex = 0;
            _found = 0;
            _secondaryFull = false;
            _evaluationDone = false;
            _sprite0Found = false;
        }

        if ((dot & 1) == 1)
        {
            EvaluateOddDot(dot);
        }
        else
        {
            EvaluateEvenDot(dot);
        }
    }

    // An odd dot: secondary OAM is being cleared to $FF up to dot 64, and after it OAM is read.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EvaluateOddDot(int dot)
    {
        _oamLatch = dot <= 64 ? (byte)0xFF : Oam[(_evaluationN << 2) | _evaluationM];
    }

    // An even dot: secondary OAM is being cleared to $FF up to dot 64, and after it the byte the
    // odd dot read is acted on.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EvaluateEvenDot(int dot)
    {
        if (dot <= 64)
        {
            _oamLatch = 0xFF;
        }
        else if (!_evaluationDone)
        {
            EvaluationStep();
        }
    }

    // What an even dot from 65 does with the byte the odd dot before it read, until the search ends.
    private void EvaluationStep()
    {
        int row = _line - _oamLatch;
        bool inRange = row >= 0 && row < ((_ctrl & 0x20) != 0 ? 16 : 8);
        if (_secondaryFull)
        {
            // The search for a ninth sprite, with the hardware's bug: out of range, n and m both
            // go up, so the next check reads another byte of the next sprite as its Y.
            if (inRange)
            {
                _status |= StatusOverflow;
                _evaluationDone = true;
            }
            else
            {
                _evaluationM = (_evaluationM + 1) & 3;
                NextSprite();
            }

            return;
        }

        if (_evaluationM == 0)
        {
            // The Y is written to secondary OAM whether it is in range or not.
            _secondaryOam[_secondaryIndex] = _oamLatch;
            if (inRange)
            {
                _sprite0Found |= _evaluationN == 0;
                _secondaryIndex++;
                _evaluationM = 1;
            }
            else
            {
                NextSprite();
            }

            return;
        }

        _secondaryOam[_secondaryIndex++] = _oamLatch;
        if (++_evaluationM == 4)
        {
            _evaluationM = 0;
            _found++;
            NextSprite();
        }
    }

    private void NextSprite()
    {
        if (++_evaluationN == 64)
        {
            _evaluationN = 0;
            _evaluationDone = true;
        }
        else if (_found == 8)
        {
            _secondaryFull = true;
        }
    }

    // One dot of the sprite fetches, dots 257 to 320: slot k / 8, step k % 8 (ppu.md 7).
    private void FetchSprite(int k)
    {
        int slot = k >> 3;
        switch (k & 7)
        {
            case 0:
                if (slot < _spriteCount)
                {
                    int at = slot << 2;
                    _fetchY = _secondaryOam[at];
                    _fetchTile = _secondaryOam[at + 1];
                    _fetchAttributes = _secondaryOam[at + 2];
                    _fetchX = _secondaryOam[at + 3];
                }
                else
                {
                    // An empty slot fetches tile $FF (ppu.md 7).
                    _fetchY = 0xFF;
                    _fetchTile = 0xFF;
                    _fetchAttributes = 0xFF;
                    _fetchX = 0xFF;
                }

                _oamLatch = _fetchY;

                // The slot's first fetch is a garbage nametable one (ppu.md 7), so A12 falls here
                // between two slots' pattern fetches; the second garbage fetch changes nothing.
                Fetching((ushort)(0x2000 | (_v & 0x0FFF)));
                break;

            case 1:
                _oamLatch = _fetchTile;
                break;

            case 2:
                _oamLatch = _fetchAttributes;
                break;

            case 3:
                _oamLatch = _fetchX;
                break;

            case 4:
            {
                int row = slot < _spriteCount ? _line - _fetchY : 0;
                if ((_ctrl & 0x20) == 0)
                {
                    if ((_fetchAttributes & 0x80) != 0 && slot < _spriteCount)
                    {
                        row = 7 - row;
                    }

                    // Three bits of row: PPUCTRL bit 5 can change between evaluation and this
                    // fetch, and a row found for 8 by 16 must not reach address bit 3.
                    _spriteAddress = (ushort)(((_ctrl & 0x08) << 9) | (_fetchTile << 4) | (row & 7));
                }
                else
                {
                    // 8 by 16: bit 0 of the tile picks the table, and the two tiles are a pair; a
                    // vertical flip runs the 16 rows backwards, so it swaps them too.
                    if ((_fetchAttributes & 0x80) != 0 && slot < _spriteCount)
                    {
                        row = 15 - row;
                    }

                    int tile = (_fetchTile & 0xFE) + (row >> 3);
                    _spriteAddress = (ushort)(((_fetchTile & 1) << 12) | (tile << 4) | (row & 7));
                }

                Fetching(_spriteAddress);
                break;
            }

            case 5:
                _fetchLow = ReadPattern(_spriteAddress);
                break;

            case 6:
                Fetching((ushort)(_spriteAddress + 8));
                break;

            default:
            {
                byte high = ReadPattern((ushort)(_spriteAddress + 8));
                if (slot < _spriteCount)
                {
                    LaySprite(slot, _fetchLow, high);
                }

                break;
            }
        }
    }

    // A fetched sprite's 8 pixels into the next line's columns, where no lower slot has an opaque pixel.
    private void LaySprite(int slot, int low, int high)
    {
        if ((_fetchAttributes & 0x40) != 0)
        {
            low = Reversed[low];
            high = Reversed[high];
        }

        int tag = ((_fetchAttributes & 3) << 2) | (_fetchAttributes & SpriteBehind) | (slot == 0 && _sprite0OnLine ? SpriteIsSprite0 : 0);
        int x = _fetchX;
        int right = Math.Min(x + 8, 256);
        if (_spriteWidth == 0)
        {
            _spriteLeft = x;
            _spriteWidth = right - x;
        }
        else
        {
            int left = Math.Min(_spriteLeft, x);
            _spriteWidth = Math.Max(_spriteLeft + _spriteWidth, right) - left;
            _spriteLeft = left;
        }

        for (int bit = 7; bit >= 0 && x < 256; bit--, x++)
        {
            int value = ((low >> bit) & 1) | (((high >> bit) & 1) << 1);
            if (value != 0 && _spriteLine[x] == 0)
            {
                _spriteLine[x] = (byte)(tag | value);
            }
        }
    }

    // Empties the line buffer: no sprite is laid in it.
    private void ClearSpriteLine()
    {
        Array.Clear(_spriteLine);
        _spriteLeft = 0;
        _spriteWidth = 0;
    }

    private static byte[] BuildReversed()
    {
        var table = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            int reversed = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                reversed |= ((i >> bit) & 1) << (7 - bit);
            }

            table[i] = (byte)reversed;
        }

        return table;
    }
}
