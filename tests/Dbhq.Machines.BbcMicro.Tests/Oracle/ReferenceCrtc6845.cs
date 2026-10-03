// The test oracle for the CRTC: the same rules as Crtc6845 (its remarks, from video.md section 1
// and the ACCC), written as plainly as possible, one character clock per Tick and nothing
// worked out ahead. Never used by the machine; the equivalence tests run it beside the lazy chip.
namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>A per-character HD6845S: every rule runs in every <see cref="Tick"/>.</summary>
public sealed class ReferenceCrtc6845
{
    private readonly byte[] _r = new byte[18];
    private int _address;

    private int _c0, _c9, _c4;
    private int _lineStart, _rowStart;
    private bool _hDisplay, _vDisplay, _firstField;
    private bool _lastLine, _adjustArmed, _inAdjust, _vsyncAllowed;
    private bool _hsync;
    private int _hsyncCount;
    private bool _vsyncLine, _vsyncMid;
    private int _vsyncCount;
    private bool _parityOdd, _parityR6;
    private int _fields;
    private readonly bool[] _display = new bool[3];
    private readonly bool[] _cursor = new bool[3];

    public ReferenceCrtc6845() => Reset();

    public event Action? VSyncFell;

    public event Action? FrameStarted;

    public int MemoryAddress => (_lineStart + _c0) & 0x3FFF;

    public int LineStartAddress => _lineStart;

    /// <summary>The same report as <see cref="Crtc6845.StateAt"/>, for the character the oracle stands at now.</summary>
    public CrtcState State(long cycle, int cyclesPerCharacter)
    {
        int displaySkew = (_r[8] >> 4) & 3;
        int cursorSkew = (_r[8] >> 6) & 3;
        bool blinkOn = ((_r[10] >> 5) & 3) switch
        {
            0 => true,
            1 => false,
            2 => (_fields & 8) == 0,
            _ => (_fields & 16) == 0,
        };
        bool cursorOnLine = VerticalDisplay && blinkOn
            && RasterAddress >= (_r[10] & 0x1F) && RasterAddress <= _r[11];
        return new CrtcState(
            cycle, _c0, MemoryAddress, _lineStart, RasterAddress, VerticalDisplay, DisplayEnable, Cursor,
            HSync, VSync, _r[1], displaySkew, cursorSkew, ((_r[14] << 8) | _r[15]) & 0x3FFF,
            cursorOnLine, cyclesPerCharacter);
    }

    public bool VerticalDisplay => _vDisplay && !_firstField;

    public int RasterAddress => SyncAndVideo ? ((_c9 << 1) | (_parityOdd ? 1 : 0)) & 0x1F : _c9;

    public bool DisplayEnable
    {
        get
        {
            int skew = (_r[8] >> 4) & 3;
            return skew != 3 && _display[skew];
        }
    }

    public bool HSync => _hsync;

    public bool VSync { get; private set; }

    public bool Cursor
    {
        get
        {
            int skew = (_r[8] >> 6) & 3;
            return skew != 3 && _cursor[skew];
        }
    }

    private bool Interlace => (_r[8] & 1) != 0;

    private bool SyncAndVideo => (_r[8] & 3) == 3;

    private bool RowEnds => SyncAndVideo ? _c9 == _r[9] >> 1 : _c9 == _r[9];

    private int AdjustLines => (_r[5] & 0x1F) + (Interlace && _parityR6 ? 1 : 0);

    public void WriteAddress(byte value) => _address = value & 0x1F;

    public void WriteData(byte value)
    {
        byte mask = _address switch
        {
            4 or 6 or 7 or 10 => 0x7F,
            5 or 9 or 11 => 0x1F,
            8 => 0xF3,
            12 or 14 => 0x3F,
            _ => 0xFF,
        };
        if (_address < 16)
        {
            _r[_address] = (byte)(value & mask);
        }
    }

    public byte ReadData() => _address is >= 12 and <= 15 ? _r[_address] : (byte)0;

    public void Reset()
    {
        bool wasHigh = VSync;
        _c0 = _c9 = _c4 = 0;
        _lineStart = _rowStart = 0;
        _hDisplay = _vDisplay = false;
        _firstField = true;
        _lastLine = _adjustArmed = _inAdjust = _vsyncAllowed = false;
        _hsync = false;
        _hsyncCount = 0;
        _vsyncLine = _vsyncMid = false;
        _vsyncCount = 0;
        _parityOdd = _parityR6 = false;
        _fields = 0;
        Array.Clear(_display);
        Array.Clear(_cursor);
        VSync = false;
        if (wasHigh)
        {
            VSyncFell?.Invoke();
        }
    }

    public void Tick()
    {
        bool vsyncBefore = VSync;
        bool frameStarted = false;

        // C0, and at the end of a line everything vertical.
        if (_c0 == _r[0] || _c0 == 255)
        {
            _c0 = 0;
            bool rowStarted = false;
            if (_inAdjust)
            {
                int next = (_c9 + 1) & 0x1F;
                if (next == AdjustLines || _c9 == AdjustLines)
                {
                    StartFrame();
                    frameStarted = true;
                    rowStarted = true;
                }
                else
                {
                    _c9 = next;
                }
            }
            else if (_lastLine && _adjustArmed)
            {
                _inAdjust = true;
                _c4 = (_c4 + 1) & 0x7F;
                _c9 = 0;
                rowStarted = true;
            }
            else if (_lastLine)
            {
                StartFrame();
                frameStarted = true;
                rowStarted = true;
            }
            else if (RowEnds)
            {
                _c4 = (_c4 + 1) & 0x7F;
                _c9 = 0;
                rowStarted = true;
            }
            else
            {
                _c9 = (_c9 + 1) & 0x1F;
            }

            _lineStart = _rowStart;
            _hDisplay = true;

            if (_vsyncLine)
            {
                _vsyncCount = (_vsyncCount + 1) & 0x0F;
                if (_vsyncCount == (_r[3] >> 4))
                {
                    _vsyncLine = false;
                }
            }

            if (rowStarted && _c4 == _r[6])
            {
                _vDisplay = false;
                _parityR6 = !_parityOdd;
            }

            if (rowStarted && _c4 == _r[7] && _vsyncAllowed && !_vsyncLine)
            {
                _vsyncLine = true;
                _vsyncCount = 0;
                _vsyncMid = Interlace && !_parityOdd;
            }

            _vsyncAllowed = false;
            if (!_vsyncMid)
            {
                VSync = _vsyncLine;
            }
        }
        else
        {
            _c0++;
        }

        if (_c0 == 0 || _c0 == 1)
        {
            _lastLine = _c4 == _r[4] && RowEnds;
            _adjustArmed = _lastLine;
        }

        if (_c0 == 2)
        {
            if (_lastLine && AdjustLines == 0)
            {
                _adjustArmed = false;
            }
            _vsyncAllowed = true;
        }

        if (_c0 == _r[1])
        {
            _hDisplay = false;
            if (RowEnds)
            {
                _rowStart = MemoryAddress;
            }
        }

        if (_hsync)
        {
            _hsyncCount = (_hsyncCount + 1) & 0x0F;
            if (_hsyncCount == (_r[3] & 0x0F))
            {
                _hsync = false;
            }
        }

        if (!_hsync && _c0 == _r[2] && (_r[3] & 0x0F) != 0)
        {
            _hsync = true;
            _hsyncCount = 0;
        }

        if (_vsyncMid && _c0 == (_r[0] + 1) >> 1)
        {
            VSync = _vsyncLine;
            if (!_vsyncLine)
            {
                _vsyncMid = false;
            }
        }

        bool display = _hDisplay && _vDisplay && !_firstField;
        bool blinkOn = ((_r[10] >> 5) & 3) switch
        {
            0 => true,
            1 => false,
            2 => (_fields & 8) == 0,
            _ => (_fields & 16) == 0,
        };
        bool cursor = display && blinkOn
            && RasterAddress >= (_r[10] & 0x1F) && RasterAddress <= _r[11]
            && MemoryAddress == (((_r[14] << 8) | _r[15]) & 0x3FFF);
        _display[2] = _display[1];
        _display[1] = _display[0];
        _display[0] = display;
        _cursor[2] = _cursor[1];
        _cursor[1] = _cursor[0];
        _cursor[0] = cursor;

        if (vsyncBefore && !VSync)
        {
            VSyncFell?.Invoke();
        }

        if (frameStarted)
        {
            FrameStarted?.Invoke();
        }
    }

    private void StartFrame()
    {
        _c4 = 0;
        _c9 = 0;
        _inAdjust = false;
        _rowStart = ((_r[12] << 8) | _r[13]) & 0x3FFF;
        _firstField = false;
        _parityOdd = _parityR6;
        _vDisplay = true;
        _fields = (_fields + 1) & 0x1F;
    }
}
