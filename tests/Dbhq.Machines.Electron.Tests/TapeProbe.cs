namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// A test-only stand-in for the ULA's tape side, used to find what the OS does on tape by running
/// it (task 10, <c>tape.md</c> s7). It logs every access to $FE04 to $FE07 with the cycle, and
/// answers at the pace <c>tape.md</c> s4 and <c>ula.md</c> s6a and s9 give. It is not the cassette:
/// it exists to watch the OS, and the real port replaces it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Output</b> (<c>$FE07</c> bits 2 and 1 = <c>10</c>): a write to $FE04 clears transmit-empty
/// (status bit 5). The byte goes onto the line at once if the line is free, or when the byte before
/// it has finished, stop bit and all, because a byte is ten bits and two cannot share the line
/// (s4). Transmit-empty is set again nine bit times, 14,976 cycles, after the byte starts on the
/// line (ula.md s6a: after bit 7 has gone out, before the stop bit). The OS writes the next byte
/// within that stop bit (<c>tape.md</c> s7 item 2), which is why the start is not simply the write.
/// The bytes written with the motor on are kept in <see cref="Written"/>, and what went onto the
/// tape in <see cref="Recorded"/>: carrier for every stretch the line is idle in output mode with
/// the motor on, and the bytes, which is how the OS was found to make carrier (s7 item 2).
/// </para>
/// <para>
/// <b>Input</b> (bits 2 and 1 = <c>00</c>): <see cref="Insert"/> gives it a tape of carrier and
/// bytes, which moves only while the motor is on. Carrier raises high-tone-detect (status bit 6)
/// after ten bit times, 16,640 cycles, and again every ten bit times while it lasts: the probe's
/// own choice, because the sheet does not say whether the ULA raises it again once the OS has
/// cleared it. A byte takes ten bit times; nine bit times in, 14,976 cycles, receive-full (status
/// bit 4) is set with the byte in $FE04, and a read of $FE04 clears it.
/// </para>
/// <para>
/// The probe acts at its own times through <see cref="Service"/>, which the session calls after
/// every instruction (<see cref="Attach"/>), so an interrupt it raises reaches the CPU at the next
/// instruction boundary, as the ULA's own interrupts do in this machine.
/// </para>
/// </remarks>
internal sealed class TapeProbe : ITapeTap
{
    /// <summary>One bit at 1200 baud in 2 MHz cycles (<c>tape.md</c> s4).</summary>
    public const long BitCycles = 1_664;

    /// <summary>A byte of ten bits (s4).</summary>
    public const long ByteCycles = 10 * BitCycles;

    /// <summary>Receive-full and transmit-empty come nine bit times into a byte (s4).</summary>
    public const long ReadyCycles = 9 * BitCycles;

    private const int ReceiveFull = 4;
    private const int TransmitEmpty = 5;
    private const int HighTone = 6;

    private readonly Ula _ula;
    private IReadOnlyList<Piece> _tape = [];
    private byte _control;
    private long _last;
    private long _transmitEmptyAt = long.MaxValue;
    private long _lineFreeAt;

    // When the line last went idle while recording (output mode, motor on), or -1 when not recording.
    private long _idleFrom = -1;

    // The tape's place: the piece being played, the tape time it started at, and within it the
    // next high tone (carrier) or whether the byte has been delivered.
    private int _index;
    private long _pieceStart;
    private long _nextTone = ByteCycles;
    private bool _delivered;
    private byte _data;

    public TapeProbe(Ula ula) => _ula = ula;

    /// <summary>One access to a cassette register: $FE04 read or written, or $FE05 to $FE07 written.</summary>
    public readonly record struct Access(long Cycle, int Register, bool IsWrite, byte Value);

    /// <summary>A piece of an input tape: a carrier lasting so many cycles, or one byte.</summary>
    public readonly record struct Piece(bool IsByte, byte Value, long CarrierCycles)
    {
        public long Length => IsByte ? ByteCycles : CarrierCycles;

        public static Piece Carrier(long cycles) => new(false, 0, cycles);

        public static Piece Byte(byte value) => new(true, value, 0);
    }

    public List<Access> Log { get; } = [];

    /// <summary>One byte written to $FE04 in output mode with the motor on: when it was written, when it started on the line, and the byte.</summary>
    public readonly record struct Sent(long Cycle, long Start, byte Value);

    /// <summary>Each byte written to $FE04 in output mode with the motor on.</summary>
    public List<Sent> Written { get; } = [];

    /// <summary>
    /// What went onto the tape in output mode with the motor on: a carrier for each stretch of idle
    /// line, from the motor going on, between bytes and up to the motor going off, and each byte.
    /// Built as the OS runs, so it is the input <see cref="Insert"/> plays back.
    /// </summary>
    public List<Piece> Recorded { get; } = [];

    /// <summary>Cycles the tape has moved under the motor.</summary>
    public long TapeTime { get; private set; }

    /// <summary>Whether every piece of the inserted tape has been played.</summary>
    public bool AtEnd => _index >= _tape.Count;

    /// <summary>Whether the OS has the cassette motor on, <c>$FE07</c> bit 6 (ula.md s7c).</summary>
    public bool MotorOn => (_control & 0x40) != 0;

    private bool InputMode => (_control & 0x06) == 0x00;

    private bool OutputMode => (_control & 0x06) == 0x04;

    private bool Recording => MotorOn && OutputMode;

    /// <summary>Puts the probe on the session's ULA and has the session service it after every instruction.</summary>
    public static TapeProbe Attach(ElectronSession session)
    {
        ElectronMachine m = session.Machine;
        var probe = new TapeProbe(m.Bus.Ula);
        m.Bus.Ula.Tap = probe;
        session.AfterEachStep = () =>
        {
            probe.Service(m.Bus.Cycles);
            m.Cpu.Irq = m.Bus.Irq;
        };
        return probe;
    }

    /// <summary>Loads a tape for input, rewound.</summary>
    public void Insert(IReadOnlyList<Piece> tape)
    {
        _tape = tape;
        _index = 0;
        _pieceStart = 0;
        _nextTone = ByteCycles;
        _delivered = false;
        TapeTime = 0;
    }

    /// <summary>Brings the probe up to <paramref name="cycle"/>: transmit-empty, and the tape under the motor.</summary>
    public void Service(long cycle)
    {
        if (cycle >= _transmitEmptyAt)
        {
            _ula.SetStatus(TransmitEmpty);
            _transmitEmptyAt = long.MaxValue;
        }

        if (MotorOn && cycle > _last)
        {
            Play(TapeTime + (cycle - _last));
        }

        _last = Math.Max(_last, cycle);
    }

    public void OnWrite(int register, byte value, long cycle)
    {
        Service(cycle);
        Log.Add(new Access(cycle, register, true, value));
        switch (register)
        {
            case 4:
                long start = Math.Max(cycle, _lineFreeAt);
                _lineFreeAt = start + ByteCycles;
                if (Recording)
                {
                    Written.Add(new Sent(cycle, start, value));
                    Idle(start);
                    Recorded.Add(Piece.Byte(value));
                    _idleFrom = _lineFreeAt;
                }

                _ula.ClearStatus(TransmitEmpty);
                _transmitEmptyAt = start + ReadyCycles;
                break;
            case 7:
                bool was = Recording;
                _control = value;
                if (!was && Recording)
                {
                    _idleFrom = cycle;
                }
                else if (was && !Recording)
                {
                    Idle(cycle);
                    _idleFrom = -1;
                }

                break;
        }
    }

    public byte OnReadData(long cycle)
    {
        Service(cycle);
        Log.Add(new Access(cycle, 4, false, _data));
        _ula.ClearStatus(ReceiveFull);
        return _data;
    }

    /// <summary>Records the idle line from <c>_idleFrom</c> to <paramref name="until"/> as carrier, if there was any.</summary>
    private void Idle(long until)
    {
        if (until > _idleFrom)
        {
            Recorded.Add(Piece.Carrier(until - _idleFrom));
        }
    }

    private void Play(long to)
    {
        while (_index < _tape.Count)
        {
            Piece p = _tape[_index];
            long end = _pieceStart + p.Length;
            if (p.IsByte)
            {
                if (!_delivered && _pieceStart + ReadyCycles <= to)
                {
                    _delivered = true;
                    if (InputMode)
                    {
                        _data = p.Value;
                        _ula.SetStatus(ReceiveFull);
                    }
                }
            }
            else
            {
                for (; _pieceStart + _nextTone <= Math.Min(to, end); _nextTone += ByteCycles)
                {
                    if (InputMode)
                    {
                        _ula.SetStatus(HighTone);
                    }
                }
            }

            if (end > to)
            {
                break;
            }

            _pieceStart = end;
            _index++;
            _nextTone = ByteCycles;
            _delivered = false;
        }

        TapeTime = to;
    }
}
