namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The Intel 8271 floppy disc controller, IC78 on the Model B, with its two drives, as Acorn DFS
/// 1.20 drives it: non-DMA, a byte at a time through the data register, and an interrupt for each.
/// </summary>
/// <remarks>
/// <para>
/// Built from the fact sheet <c>docs/bbc-micro/facts/disc.md</c>: the registers (s1a, s1b), the
/// commands (s1c), the result codes (s1d), Read Drive Status (s1e), the special registers (s1f),
/// non-DMA operation (s1g) and the minimal design that DFS cannot tell from a real drive (s3).
/// Times are 2 MHz CPU cycles. The 8271 is a 2 MHz device on the bus, never stretched
/// (<c>bus.md</c> s2a).
/// </para>
/// <para>
/// <b>Registers.</b> Status (bit 7 busy, 6 command full, 5 parameter full, 4 result full, 3 INT,
/// 2 non-DMA data request), command, result, parameter, reset and data. A parameter is taken the
/// moment it is written, so parameter full is never seen set (s3: 0 cycles passes). Reading the
/// result clears result full and the completion interrupt. An access to the data register while
/// a byte is requested takes it and clears the request and its interrupt.
/// </para>
/// <para>
/// <b>Commands.</b> Read Drive Status, Read Special Register, Write Special Register and Specify
/// end the moment their last parameter is written, with no interrupt; the first two leave a
/// result. Every other command uses a drive and ends with an interrupt and a result, after a
/// fixed <see cref="StartCycles"/> that stands for the seek, the settling and the wait for the
/// sector (s3: DFS passes with anything from 0 to 60,000). Seeks are instant. Read Data, Write
/// Data and Verify then move one byte every <see cref="ByteCycles"/>, which is the 64 microsecond
/// byte of a mini-floppy's 8 microsecond bit cell (s1g): the request and INT rise together at
/// each byte, and the data register access clears both, so each byte is a fresh NMI edge. A byte
/// not taken by the time the next is due ends the command with result <c>$0A</c>, late data
/// (s3), with INT held high from the byte to the result. Otherwise the result follows the last
/// byte's time. A command reaches only sectors 0 to 9 of
/// 256 bytes on the tracks and sides the image has; anything else is sector not found, <c>$18</c>.
/// Scan, Read ID and Format are not modelled: they end after the start delay with result
/// <c>$00</c> and move no data (no DFS 1.20 command uses them, s1c).
/// </para>
/// <para>
/// <b>Drives.</b> Bits 7 and 6 of a command select drive 1 or 0 (s1c). Side select is bit 5 of
/// special register <c>$23</c>, the drive control output port, and not a parameter (s4); bit 3 of
/// the port is LOAD HEAD, which on the BBC is the motor (s1f). A drive is ready when it holds a
/// disc and its select bit and the motor bit are set. A command that uses a drive first clears
/// port bits 0 to 4 if it selects other drives than the port's (D1 p8-124), then sets the motor
/// itself when there is a disc. With no disc the command ends with not ready, <c>$10</c>, and
/// that drive's not-ready latch holds until a Read Drive Status, which reports it once as not
/// ready and clears it (D1 p8-129 footnote: issue it twice to clear it on a ready drive).
/// </para>
/// <para>
/// <b>Head unload.</b> Index count times 400,000 cycles (a 200 ms revolution) after a drive
/// command ends, with no drive command started since, the head unloads: port bits 0 to 4, 6 and 7
/// clear, so the motor stops and the drive is no longer ready. An index count of 15 keeps the head
/// loaded. This is how DFS notices a disc swap (s2b): it re-reads the catalogue only after a ready
/// test fails.
/// </para>
/// <para>
/// <b>Lazily.</b> In a machine the chip is never ticked. Nothing changes inside it between an
/// access and its next scheduled step (a byte, a late-data check, a result, the head unloading),
/// so catching up is a loop over those steps, at most one every 128 cycles, and an idle chip costs
/// nothing. It tells the bus the cycle of its next step while a command runs, because that is
/// when INT may rise; INT falls only at an access, after which the bus looks anyway. On its own the
/// chip's time is the calls to <see cref="Tick"/> and <see cref="Run"/>.
/// </para>
/// </remarks>
public sealed class Fdc8271
{
    /// <summary>CPU cycles from one byte to the next: 64 microseconds at 2 MHz (s1g, s3).</summary>
    public const int ByteCycles = 128;

    /// <summary>
    /// CPU cycles from a drive command's start to its first byte, or to its result if it moves
    /// none: one millisecond, the sheet's suggestion for a plausible figure [guessing], standing for
    /// the seek, the head settling and the sector coming round (s3).
    /// </summary>
    public const int StartCycles = 2_000;

    /// <summary>CPU cycles in one revolution, one index pulse to the next: 200 ms at 300 rpm (s3).</summary>
    public const int RevolutionCycles = 400_000;

    private const long Never = long.MaxValue;

    // The status bits, the result and the data register.
    private bool _busy, _commandFull, _resultFull, _request, _completion;
    private byte _result, _data;

    // The command being written, and its parameters.
    private byte _command;
    private readonly byte[] _parameters = new byte[5];
    private int _parameterCount, _parametersNeeded;

    // The special registers (s1f): all 256 addresses, of which the chip gives meaning to a few.
    private readonly byte[] _special = new byte[256];

    // The drives: the disc in each, the track its head is over, and the not-ready latch.
    private readonly DiscImage?[] _discs = new DiscImage?[2];
    private readonly int[] _headTrack = new int[2];
    private readonly bool[] _notReady = new bool[2];

    // The drive command running: what it does, where, and its next step (byte index and cycle).
    private Kind _kind;
    private int _failure = -1;
    private int _drive, _side, _track, _firstSector, _sectorCount;
    private bool _sizeIs256;
    private int _step;
    private long _nextStep = Never;
    private byte[]? _sectorData;
    private int _sectorBase;

    // When the head unloads, or Never.
    private long _unloadAt = Never;

    // The time: the machine's clock, or the chip's own count.
    private readonly BbcClock? _clock;
    private long _ownCycles;

    /// <summary>A chip on its own, whose time is the calls to <see cref="Tick"/> and <see cref="Run"/>.</summary>
    public Fdc8271()
    {
    }

    /// <summary>The chip in a machine: its time is the machine's clock.</summary>
    internal Fdc8271(BbcClock clock)
    {
        _clock = clock;
    }

    private enum Kind
    {
        None,
        Seek,
        Read,
        Write,
        Verify,
        NoData,
    }

    /// <summary>INT, which on the BBC drives the CPU's NMI line (s1g): high while a byte is requested or a result waits with its interrupt.</summary>
    public bool Interrupt
    {
        get
        {
            Sync();
            return _request || _completion;
        }
    }

    /// <summary>
    /// The CPU cycle of the chip's next step while a drive command runs, when INT may rise, or
    /// <see cref="long.MaxValue"/>: the bus need not look at the chip before then.
    /// </summary>
    internal long NextEventCycle
    {
        get
        {
            Sync();
            return _nextStep;
        }
    }

    private long Now => _clock is null ? _ownCycles : _clock.Cycles;

    /// <summary>The status register, <c>$FE80</c> read. Reading it changes nothing.</summary>
    public byte ReadStatus()
    {
        Sync();
        return Status;
    }

    /// <summary>The result register, <c>$FE81</c> read: clears result full and the completion interrupt.</summary>
    public byte ReadResult()
    {
        Sync();
        _resultFull = false;
        _completion = false;
        return _result;
    }

    /// <summary>The data register, <c>$FE84</c> read: takes a requested byte.</summary>
    public byte ReadData()
    {
        Sync();
        _request = false;
        return _data;
    }

    /// <summary>
    /// A register without its side effects, for a debugger: 0 status, 1 result, 4 data; anything
    /// else reads 0.
    /// </summary>
    public byte Peek(int register)
    {
        Sync();
        return register switch
        {
            0 => Status,
            1 => _result,
            4 => _data,
            _ => 0,
        };
    }

    /// <summary>
    /// The command register, <c>$FE80</c> write. A command written while another runs replaces it;
    /// the datasheet forbids that and does not say what happens.
    /// </summary>
    public void WriteCommand(byte value)
    {
        Sync();
        _command = value;
        _busy = true;
        _commandFull = true;
        _parameterCount = 0;
        _parametersNeeded = ParametersFor(value & 0x3F);

        // A command running is abandoned, and a byte it requested with it.
        _kind = Kind.None;
        _nextStep = Never;
        _request = false;
        if (_parametersNeeded == 0)
        {
            Start(Now);
        }
    }

    /// <summary>The parameter register, <c>$FE81</c> write. Ignored when no command is waiting for one.</summary>
    public void WriteParameter(byte value)
    {
        Sync();
        if (!_commandFull)
        {
            return;
        }

        _parameters[_parameterCount++] = value;
        if (_parameterCount == _parametersNeeded)
        {
            Start(Now);
        }
    }

    /// <summary>
    /// The reset register, <c>$FE82</c> write. A value with bit 0 set resets the chip: the status and
    /// result clear, a command is abandoned and the drive control port clears, so the head unloads.
    /// DFS writes 1 then 0 (s1a); the model resets at the 1 and does not hold the chip in reset.
    /// </summary>
    public void WriteReset(byte value)
    {
        Sync();
        if ((value & 1) != 0)
        {
            ResetState();
        }
    }

    /// <summary>The data register, <c>$FE84</c> write: gives a requested byte, which Write Data puts on the disc.</summary>
    public void WriteData(byte value)
    {
        Sync();
        _data = value;
        if (_request)
        {
            _request = false;
            if (_kind == Kind.Write)
            {
                _sectorData![_sectorBase + ((_step - 1) & 0xFF)] = value;
            }
        }
    }

    /// <summary>One CPU cycle, for a chip on its own. Steps due in it happen before an access made after the call.</summary>
    public void Tick() => Run(1);

    /// <summary><paramref name="cycles"/> CPU cycles at once, for a chip on its own.</summary>
    public void Run(long cycles)
    {
        if (_clock is not null)
        {
            throw new InvalidOperationException("This chip's time is the machine's clock.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(cycles);
        _ownCycles += cycles;
    }

    /// <summary>Puts <paramref name="image"/> in drive 0 or 1, or empties it with null. A sector being read keeps coming from the disc it started on.</summary>
    public void Insert(int drive, DiscImage? image)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(drive, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(drive, 1);
        Sync();
        _discs[drive] = image;
    }

    /// <summary>Takes the disc out of drive 0 or 1 and returns it, or null if the drive was empty.</summary>
    public DiscImage? Eject(int drive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(drive, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(drive, 1);
        Sync();
        DiscImage? image = _discs[drive];
        _discs[drive] = null;
        return image;
    }

    /// <summary>The disc in drive 0 or 1, or null.</summary>
    public DiscImage? Disc(int drive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(drive, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(drive, 1);
        return _discs[drive];
    }

    /// <summary>
    /// Power on: what the chip holds is not known, so it starts as after a reset with every
    /// special register 0 and both heads over track 0.
    /// </summary>
    internal void PowerOn()
    {
        Sync();
        ResetState();
        Array.Clear(_special);
        Array.Clear(_headTrack);
    }

    /// <summary>Does the steps owed up to now; on its own, up to the chip's own count.</summary>
    internal void Sync()
    {
        long now = Now;
        while (true)
        {
            long next = Math.Min(_nextStep, _unloadAt);
            if (next > now)
            {
                break;
            }

            if (next == _nextStep)
            {
                Step(next);
            }
            else
            {
                Unload();
            }
        }
    }

    private byte Status => (byte)((_busy ? 0x80 : 0) | (_commandFull ? 0x40 : 0) | (_resultFull ? 0x10 : 0)
        | (_request || _completion ? 0x08 : 0) | (_request ? 0x04 : 0));

    /// <summary>Parameters each command takes, by opcode (s1c, D1 p8-129). An opcode not in the table takes none.</summary>
    private static int ParametersFor(int opcode) => opcode switch
    {
        0x00 or 0x04 => 5,
        0x0A or 0x0E or 0x12 or 0x16 or 0x1E => 2,
        0x0B or 0x0F or 0x13 or 0x17 or 0x1F => 3,
        0x1B => 3,
        0x23 => 5,
        0x29 => 1,
        0x35 => 4,
        0x3A => 2,
        0x3D => 1,
        _ => 0,
    };

    private void ResetState()
    {
        _busy = _commandFull = _resultFull = _request = _completion = false;
        _result = 0;
        _parameterCount = _parametersNeeded = 0;
        _kind = Kind.None;
        _nextStep = Never;
        _unloadAt = Never;
        _special[0x23] = 0;
        Array.Clear(_notReady);
    }

    /// <summary>The command's last parameter has arrived, at cycle <paramref name="now"/>.</summary>
    private void Start(long now)
    {
        _commandFull = false;
        int opcode = _command & 0x3F;
        switch (opcode)
        {
            case 0x2C:
                // Read Drive Status: the drive's input lines, at once (s1e). The not-ready latch
                // is reported and cleared.
                int drive = Selected(_command);
                byte lines = InputLines(drive);
                if (drive >= 0)
                {
                    _notReady[drive] = false;
                }
                Immediate(lines);
                return;
            case 0x3D:
                Immediate(ReadSpecial(_parameters[0]));
                return;
            case 0x3A:
                _special[_parameters[0]] = _parameters[1];
                _busy = false;
                return;
            case 0x35:
                Specify();
                _busy = false;
                return;
        }

        if (ParametersFor(opcode) == 0)
        {
            // Not a command the datasheet lists: it ends at once and does nothing [guessing].
            _busy = false;
            return;
        }

        StartDriveCommand(opcode, now);
    }

    private void Immediate(byte result)
    {
        _result = result;
        _resultFull = true;
        _busy = false;
    }

    private void Specify()
    {
        int first = _parameters[0] switch
        {
            0x0D => 0x0D,
            0x10 => 0x10,
            0x18 => 0x18,
            _ => -1,
        };
        if (first >= 0)
        {
            _special[first] = _parameters[1];
            _special[first + 1] = _parameters[2];
            _special[first + 2] = _parameters[3];
        }
    }

    private byte ReadSpecial(byte register)
    {
        if (register != 0x22)
        {
            return _special[register];
        }

        // The drive control input port: the lines of the drive the output port selects. Its bit
        // layout is not in D1; it is taken to be Read Drive Status's [guessing; DFS never reads it].
        byte port = _special[0x23];
        int drive = (port & 0x40) != 0 ? 0 : (port & 0x80) != 0 ? 1 : -1;
        return drive < 0 ? (byte)0 : Lines(drive);
    }

    private void StartDriveCommand(int opcode, long now)
    {
        _unloadAt = Never;
        int select = _command & 0xC0;
        if ((_special[0x23] & 0xC0) != select)
        {
            _special[0x23] = (byte)((_special[0x23] & 0x20) | select);
        }

        int drive = Selected(_command);
        if (drive >= 0 && _discs[drive] is not null)
        {
            _special[0x23] |= 0x08;
        }

        _kind = opcode switch
        {
            0x29 => Kind.Seek,
            0x12 or 0x13 or 0x16 or 0x17 => Kind.Read,
            0x0A or 0x0B or 0x0E or 0x0F => Kind.Write,
            0x1E or 0x1F => Kind.Verify,
            _ => Kind.NoData,
        };
        _failure = -1;
        _step = 0;
        _nextStep = now + StartCycles;

        if (drive < 0)
        {
            _failure = 0x10;
            return;
        }

        DiscImage? disc = _discs[drive];
        if (disc is null)
        {
            _notReady[drive] = true;
            _failure = 0x10;
            return;
        }

        if (_notReady[drive])
        {
            _failure = 0x10;
            return;
        }

        if ((_kind == Kind.Write || opcode == 0x23) && disc.ReadOnly)
        {
            _failure = 0x12;
            return;
        }

        // The seek, instant: the head and the surface's current track register (s1f).
        _drive = drive;
        _track = _parameters[0];
        _headTrack[drive] = _track;
        _special[drive == 0 ? 0x12 : 0x1A] = (byte)_track;
        _side = (_special[0x23] >> 5) & 1;
        _firstSector = _parameters[1];

        // A 128-byte command takes two parameters and moves one sector; the variable-length ones
        // take size << 5 | count as the third (s1c).
        bool variable = (opcode & 1) != 0;
        _sectorCount = variable ? _parameters[2] & 0x1F : 1;
        _sizeIs256 = variable && (_parameters[2] >> 5) == 1;
    }

    /// <summary>The drive a command's select bits name, or -1 for none or both.</summary>
    private static int Selected(byte command) => (command & 0xC0) switch
    {
        0x40 => 0,
        0x80 => 1,
        _ => -1,
    };

    /// <summary>Read Drive Status's value for a drive: its lines with ready cleared while the not-ready latch holds.</summary>
    private byte InputLines(int drive)
    {
        if (drive < 0)
        {
            return 0;
        }

        byte lines = Lines(drive);
        return _notReady[drive] ? (byte)(lines & ~0x44) : lines;
    }

    /// <summary>
    /// A drive's input lines (s1e): ready in bits 6 and 2 together, since one circuit drives both
    /// on the BBC; write protect in bit 3; track 0 in bit 1. Index, fault and count read 0.
    /// </summary>
    private byte Lines(int drive)
    {
        DiscImage? disc = _discs[drive];
        byte port = _special[0x23];
        bool ready = disc is not null && (port & 0x08) != 0 && (port & (drive == 0 ? 0x40 : 0x80)) != 0;
        return (byte)((ready ? 0x44 : 0) | (disc?.ReadOnly == true ? 0x08 : 0) | (_headTrack[drive] == 0 ? 0x02 : 0));
    }

    /// <summary>The running command's step at cycle <paramref name="cycle"/>: a check, a byte, or the end.</summary>
    private void Step(long cycle)
    {
        int step = _step;
        bool moves = _kind is Kind.Read or Kind.Write;
        if (moves && step > 0 && _request)
        {
            // The last byte was not taken in its time: late data (s3). INT, high for the byte,
            // stays high for the result.
            _request = false;
            Finish(cycle, 0x0A);
            return;
        }

        if (_failure >= 0)
        {
            Finish(cycle, _failure);
            return;
        }

        if (_kind is Kind.Seek or Kind.NoData || step == _sectorCount * DiscImage.SectorSize)
        {
            Finish(cycle, 0x00);
            return;
        }

        if ((step & 0xFF) == 0)
        {
            // A sector starts: the scan sector register says which, for DFS's error message.
            int sector = _firstSector + (step >> 8);
            _special[0x06] = (byte)sector;
            DiscImage? disc = _discs[_drive];
            if (!_sizeIs256 || disc is null || !disc.Contains(_side, _track, sector))
            {
                Finish(cycle, 0x18);
                return;
            }

            _sectorData = disc.Data;
            _sectorBase = disc.Offset(_side, _track, sector);
        }

        if (_kind == Kind.Read)
        {
            _data = _sectorData![_sectorBase + (step & 0xFF)];
            _request = true;
        }
        else if (_kind == Kind.Write)
        {
            _request = true;
        }

        _step = step + 1;
        _nextStep = cycle + ByteCycles;
    }

    private void Finish(long cycle, int result)
    {
        _kind = Kind.None;
        _nextStep = Never;
        _result = (byte)result;
        _resultFull = true;
        _busy = false;
        _completion = true;

        int count = _special[0x0F] >> 4;
        if (count == 0)
        {
            Unload();
        }
        else if (count != 15)
        {
            _unloadAt = cycle + ((long)count * RevolutionCycles);
        }
    }

    private void Unload()
    {
        _special[0x23] &= 0x20;
        _unloadAt = Never;
    }
}
