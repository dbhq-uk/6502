// The test oracle for the 8271: the same rules as Fdc8271, written as a plain stepper that is ticked
// in every CPU cycle and counts down to each thing it does, with no catching up and no events. It is
// never used by the machine; the equivalence tests run it beside the lazy chip and compare them.
namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The Intel 8271 a cycle at a time: <see cref="Tick"/> is one CPU cycle, called before that
/// cycle's access. The rules are <see cref="Fdc8271"/>'s, from <c>disc.md</c> s1 and s3.
/// </summary>
public sealed class ReferenceFdc8271
{
    private bool _busy, _commandFull, _resultFull, _request, _completion;
    private byte _result, _data, _command;
    private readonly List<byte> _parameters = [];
    private int _wanted;
    private readonly byte[] _special = new byte[256];
    private readonly DiscImage?[] _discs = new DiscImage?[2];
    private readonly int[] _head = new int[2];
    private readonly bool[] _latch = new bool[2];

    // The command running: a countdown to its next step, and what that step is.
    private bool _running;
    private int _countdown;
    private int _opcode;
    private int _failWith;
    private int _drive, _side, _track, _sector, _count;
    private bool _size256;
    private int _byteIndex;
    private DiscImage? _sectorDisc;
    private int _sectorSide, _sectorTrack, _sectorNumber;

    // A countdown to the head unloading, or 0 for none.
    private long _unloadIn;

    public bool Interrupt => _request || _completion;

    /// <summary>How many times the head has unloaded after a command, for a test to see it happened.</summary>
    public int Unloads { get; private set; }

    public void Insert(int drive, DiscImage? image) => _discs[drive] = image;

    public DiscImage? Eject(int drive)
    {
        DiscImage? image = _discs[drive];
        _discs[drive] = null;
        return image;
    }

    public byte ReadStatus() => Status();

    public byte ReadResult()
    {
        _resultFull = false;
        _completion = false;
        return _result;
    }

    public byte ReadData()
    {
        _request = false;
        return _data;
    }

    public byte Peek(int register) => register switch
    {
        0 => Status(),
        1 => _result,
        4 => _data,
        _ => 0,
    };

    public void WriteCommand(byte value)
    {
        _command = value;
        _busy = true;
        _commandFull = true;
        _parameters.Clear();
        _running = false;
        _request = false;
        _wanted = Wanted(value & 0x3F);
        if (_wanted == 0)
        {
            Begin();
        }
    }

    public void WriteParameter(byte value)
    {
        if (!_commandFull)
        {
            return;
        }

        _parameters.Add(value);
        if (_parameters.Count == _wanted)
        {
            Begin();
        }
    }

    public void WriteReset(byte value)
    {
        if ((value & 1) == 1)
        {
            Reset();
        }
    }

    public void WriteData(byte value)
    {
        _data = value;
        if (!_request)
        {
            return;
        }

        _request = false;
        if (_running && IsWrite(_opcode))
        {
            _sectorDisc!.Sector(_sectorSide, _sectorTrack, _sectorNumber)[(_byteIndex - 1) % 256] = value;
        }
    }

    public void PowerOn()
    {
        Reset();
        Array.Clear(_special);
        Array.Clear(_head);
    }

    /// <summary>One CPU cycle.</summary>
    public void Tick()
    {
        if (_unloadIn > 0)
        {
            _unloadIn--;
            if (_unloadIn == 0)
            {
                _special[0x23] &= 0x20;
                Unloads++;
            }
        }

        if (_running)
        {
            _countdown--;
            if (_countdown == 0)
            {
                Next();
            }
        }
    }

    private byte Status()
    {
        int status = 0;
        if (_busy) status |= 0x80;
        if (_commandFull) status |= 0x40;
        if (_resultFull) status |= 0x10;
        if (_request || _completion) status |= 0x08;
        if (_request) status |= 0x04;
        return (byte)status;
    }

    private static int Wanted(int opcode)
    {
        switch (opcode)
        {
            case 0x00: case 0x04: case 0x23: return 5;
            case 0x0A: case 0x0E: case 0x12: case 0x16: case 0x1E: case 0x3A: return 2;
            case 0x0B: case 0x0F: case 0x13: case 0x17: case 0x1F: case 0x1B: return 3;
            case 0x29: case 0x3D: return 1;
            case 0x35: return 4;
            default: return 0;
        }
    }

    private static bool IsRead(int opcode) => opcode is 0x12 or 0x13 or 0x16 or 0x17;

    private static bool IsWrite(int opcode) => opcode is 0x0A or 0x0B or 0x0E or 0x0F;

    private static bool IsVerify(int opcode) => opcode is 0x1E or 0x1F;

    private void Reset()
    {
        _busy = false;
        _commandFull = false;
        _resultFull = false;
        _request = false;
        _completion = false;
        _result = 0;
        _parameters.Clear();
        _wanted = 0;
        _running = false;
        _unloadIn = 0;
        _special[0x23] = 0;
        _latch[0] = false;
        _latch[1] = false;
    }

    private void Begin()
    {
        _commandFull = false;
        int opcode = _command & 0x3F;
        int drive = (_command >> 6) == 1 ? 0 : (_command >> 6) == 2 ? 1 : -1;

        if (opcode == 0x2C)
        {
            byte lines = drive < 0 ? (byte)0 : Lines(drive);
            if (drive >= 0 && _latch[drive])
            {
                lines &= 0xBB;
                _latch[drive] = false;
            }
            Answer(lines);
            return;
        }

        if (opcode == 0x3D)
        {
            int register = _parameters[0];
            if (register == 0x22)
            {
                int selected = (_special[0x23] & 0x40) != 0 ? 0 : (_special[0x23] & 0x80) != 0 ? 1 : -1;
                Answer(selected < 0 ? (byte)0 : Lines(selected));
            }
            else
            {
                Answer(_special[register]);
            }
            return;
        }

        if (opcode == 0x3A)
        {
            _special[_parameters[0]] = _parameters[1];
            _busy = false;
            return;
        }

        if (opcode == 0x35)
        {
            int at = _parameters[0] == 0x0D ? 0x0D : _parameters[0] == 0x10 ? 0x10 : _parameters[0] == 0x18 ? 0x18 : -1;
            if (at >= 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    _special[at + i] = _parameters[1 + i];
                }
            }
            _busy = false;
            return;
        }

        if (Wanted(opcode) == 0)
        {
            _busy = false;
            return;
        }

        // A command that uses a drive.
        _unloadIn = 0;
        if ((_special[0x23] & 0xC0) != (_command & 0xC0))
        {
            _special[0x23] = (byte)((_special[0x23] & 0x20) | (_command & 0xC0));
        }
        if (drive >= 0 && _discs[drive] != null)
        {
            _special[0x23] |= 0x08;
        }

        _opcode = opcode;
        _running = true;
        _countdown = Fdc8271.StartCycles;
        _byteIndex = 0;
        _failWith = -1;

        if (drive < 0)
        {
            _failWith = 0x10;
        }
        else if (_discs[drive] == null)
        {
            _latch[drive] = true;
            _failWith = 0x10;
        }
        else if (_latch[drive])
        {
            _failWith = 0x10;
        }
        else if ((IsWrite(opcode) || opcode == 0x23) && _discs[drive]!.ReadOnly)
        {
            _failWith = 0x12;
        }
        else
        {
            _drive = drive;
            _track = _parameters[0];
            _head[drive] = _track;
            _special[drive == 0 ? 0x12 : 0x1A] = (byte)_track;
            _side = (_special[0x23] & 0x20) != 0 ? 1 : 0;
            _sector = _parameters.Count > 1 ? _parameters[1] : 0;
            if (opcode % 2 == 1 && _parameters.Count == 3)
            {
                _count = _parameters[2] % 32;
                _size256 = _parameters[2] / 32 == 1;
            }
            else
            {
                _count = 1;
                _size256 = false;
            }
        }
    }

    private void Answer(byte value)
    {
        _result = value;
        _resultFull = true;
        _busy = false;
    }

    private byte Lines(int drive)
    {
        int lines = 0;
        bool selected = drive == 0 ? (_special[0x23] & 0x40) != 0 : (_special[0x23] & 0x80) != 0;
        if (_discs[drive] != null && selected && (_special[0x23] & 0x08) != 0)
        {
            lines |= 0x44;
        }
        if (_discs[drive] != null && _discs[drive]!.ReadOnly)
        {
            lines |= 0x08;
        }
        if (_head[drive] == 0)
        {
            lines |= 0x02;
        }
        return (byte)lines;
    }

    /// <summary>The countdown ran out: check the last byte was taken, then the next byte or the end.</summary>
    private void Next()
    {
        bool transfers = IsRead(_opcode) || IsWrite(_opcode);
        if (transfers && _byteIndex > 0 && _request)
        {
            // Late: the request goes, and the result comes in the next cycle.
            _request = false;
            _failWith = 0x0A;
            _countdown = 1;
            return;
        }

        if (_failWith >= 0)
        {
            End(_failWith);
            return;
        }

        bool data = transfers || IsVerify(_opcode);
        if (!data || _byteIndex == _count * 256)
        {
            End(0);
            return;
        }

        if (_byteIndex % 256 == 0)
        {
            int sector = _sector + (_byteIndex / 256);
            _special[0x06] = (byte)sector;
            DiscImage? disc = _discs[_drive];
            if (!_size256 || disc == null || !disc.Contains(_side, _track, sector))
            {
                End(0x18);
                return;
            }

            _sectorDisc = disc;
            _sectorSide = _side;
            _sectorTrack = _track;
            _sectorNumber = sector;
        }

        if (IsRead(_opcode))
        {
            _data = _sectorDisc!.Sector(_sectorSide, _sectorTrack, _sectorNumber)[_byteIndex % 256];
            _request = true;
        }
        else if (IsWrite(_opcode))
        {
            _request = true;
        }

        _byteIndex++;
        _countdown = Fdc8271.ByteCycles;
    }

    private void End(int result)
    {
        _running = false;
        _result = (byte)result;
        _resultFull = true;
        _busy = false;
        _completion = true;

        int revolutions = _special[0x0F] / 16;
        if (revolutions == 0)
        {
            _special[0x23] &= 0x20;
            Unloads++;
        }
        else if (revolutions < 15)
        {
            _unloadIn = (long)revolutions * Fdc8271.RevolutionCycles;
        }
    }
}
