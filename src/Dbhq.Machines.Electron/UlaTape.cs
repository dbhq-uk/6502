using Dbhq.Machines.Electron.Tape;

namespace Dbhq.Machines.Electron;

/// <summary>
/// The cassette, at the ULA's serial register (<c>tape.md</c> s4 and s5, <c>ula.md</c> s6a and s9):
/// the tape is a list of <see cref="TapeEvent"/>s, never a waveform, and the ULA's tape side answers
/// at bit-time pace with the interrupts the OS waits for. It belongs to the <see cref="Ula"/>, and
/// like the rest of the ULA it is lazy: <see cref="NextEvent"/> says when it next has something to
/// do, the ULA folds that into its own, and the bus pays one comparison an access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Moving.</b> The tape moves only with the motor on, <c>$FE07</c> bit 6, and in input mode
/// (bits 2 and 1 = <c>00</c>) or output mode (<c>10</c>). In sound mode or with the motor off it
/// stands still. Its position is in CPU cycles of tape, and an event on it falls at
/// <c>cycle = anchor + (tapeTime - anchorPosition)</c>, so the time it raises an interrupt is exact
/// to the cycle and does not depend on when anything looks. A played tape stops at its end. While
/// recording, the tape moves only in output mode, so its position is the time spent recording, which
/// is not quite the recording's length: that rounds its carriers and drops idle stretches under a bit time.
/// </para>
/// <para>
/// <b>Input.</b> In input mode a played tape delivers. Carrier raises high-tone-detect (status bit
/// 6) after ten bit times of carrier heard, 16,640 cycles, and again every ten bit times while it
/// lasts; consecutive carriers are one tone and a silence or a byte ends it. Whether the ULA raises
/// it again once the OS has cleared it is open (<c>tape.md</c> s7 item 11); the test probe did,
/// and the OS loaded every tape so. A byte takes ten bit times, and nine bit times in, 14,976
/// cycles, receive-full (status bit 4) is set with the byte in <c>$FE04</c>. Reading <c>$FE04</c>
/// clears it. A byte not read within 4,000 cycles, about 2 ms (<c>ula.md</c> s9), is lost: receive
/// full is cleared and <see cref="LostBytes"/> counts it. A silence delivers nothing.
/// </para>
/// <para>
/// <b>Output.</b> In any mode a write to <c>$FE04</c> clears transmit-empty (status bit 5). The
/// byte starts on the line at the later of the write and the end of the byte before, and
/// transmit-empty is set nine bit times after it starts (<c>tape.md</c> s5, s7 item 2: the OS
/// writes each next byte inside the stop bit of the one before). While recording in output mode
/// with the motor on, each byte is recorded as a <see cref="TapeByte"/> and each stretch of idle
/// line as a <see cref="Carrier"/>, rounded to the nearest 2400 Hz cycle of 832 CPU cycles
/// (<c>tape.md</c> s5, carrier units). The OS makes carrier by leaving the line idle (s7 item 2).
/// </para>
/// </remarks>
internal sealed class UlaTape
{
    /// <summary>One bit at 1200 baud (<c>tape.md</c> s4).</summary>
    public const long BitCycles = TapeTiming.BitCpuCycles;

    /// <summary>A byte of ten bits (s4).</summary>
    public const long ByteCycles = TapeTiming.ByteCpuCycles;

    /// <summary>Receive-full and transmit-empty come nine bit times into a byte (s4; <c>ula.md</c> s6a).</summary>
    public const long ReadyCycles = 9 * BitCycles;

    /// <summary>High-tone-detect needs ten bits of high tone, 20 cycles of 2400 Hz (<c>ula.md</c> s9).</summary>
    public const long HighToneCycles = 10 * BitCycles;

    /// <summary>A received byte must be read within about 2 ms (<c>ula.md</c> s9).</summary>
    public const long LostCycles = 4_000;

    /// <summary>
    /// Idle line of this many cycles or fewer is not recorded as carrier. Task 10 found the OS
    /// writes each byte of a block 128 to 408 cycles after transmit-empty, inside the stop bit of
    /// the byte before (<c>tape.md</c> s7 item 2), so the bytes of a block have no idle between
    /// them at all, and the OS's own carrier is the 0.9 s and 5 s stretches before, between and
    /// after blocks. An idle stretch under one bit time is therefore none the OS meant, and it is
    /// far below the ten bit times of carrier high-tone-detect needs, so it is dropped rather than
    /// written as a carrier nobody could hear.
    /// </summary>
    public const long CarrierThreshold = BitCycles;

    private const int ReceiveFull = 4;
    private const int TransmitEmpty = 5;
    private const int HighTone = 6;

    private const byte MotorBit = 0x40;
    private const byte ModeBits = 0x06;
    private const byte InputMode = 0x00;
    private const byte OutputMode = 0x04;

    private readonly Ula _ula;

    // The tape: what was inserted, its bytes and carrier runs in tape time, and its length. A
    // recording is kept instead while recording.
    private IReadOnlyList<TapeEvent> _events = [];
    private long[] _byteStarts = [];
    private byte[] _byteValues = [];
    private long[] _runStarts = [];
    private long[] _runEnds = [];
    private long _length;
    private List<TapeEvent>? _recording;

    // Motion: the control register, and the tape's position at a cycle.
    private byte _control;
    private bool _moving;
    private long _anchorCycle;
    private long _anchorPosition;

    // Input: the next byte to deliver, the carrier run being heard and the tape time from which
    // high tone has been heard without a break, and when the byte in the receive register is lost
    // if it is not read (long.MaxValue once it is read).
    private bool _listening;
    private int _byte;
    private int _run;
    private long _toneFrom;
    private long _lostAt = long.MaxValue;

    // Output: when transmit-empty comes, when the line is next free, and, while recording, when
    // the line went idle (-1 when not recording).
    private long _transmitEmptyAt = long.MaxValue;
    private long _lineFreeAt;
    private long _idleFrom = -1;

    public UlaTape(Ula ula) => _ula = ula;

    /// <summary>The next cycle at which the tape has something to do, or <see cref="long.MaxValue"/>.</summary>
    public long NextEvent { get; private set; } = long.MaxValue;

    /// <summary>Whether <c>$FE07</c> bit 6, the cassette motor relay, was last written set (<c>ula.md</c> s9).</summary>
    public bool MotorOn => (_control & MotorBit) != 0;

    /// <summary>
    /// Bytes the tape delivered that were not read within <see cref="LostCycles"/>, since the tape
    /// was last inserted, rewound, ejected or started recording: a count for one load.
    /// </summary>
    public int LostBytes { get; private set; }

    /// <summary>The receive register, as a read of <c>$FE04</c> would give it, without clearing anything.</summary>
    public byte Data { get; private set; }

    private bool Recording => _recording is not null;

    private bool RecordingNow => Recording && _moving;

    /// <summary>CPU cycles of tape played or recorded, at <paramref name="cycle"/>.</summary>
    public long PositionAt(long cycle)
    {
        if (!_moving)
        {
            return _anchorPosition;
        }

        long position = _anchorPosition + (cycle - _anchorCycle);
        return Recording ? position : Math.Min(position, _length);
    }

    /// <summary>Puts a tape in, rewound, in place of whatever was there or being recorded.</summary>
    public void Insert(IReadOnlyList<TapeEvent> events, long cycle)
    {
        ArgumentNullException.ThrowIfNull(events);
        Load(events, recording: null, cycle);
    }

    /// <summary>Starts a new, empty recording at <paramref name="cycle"/>, in place of any tape.</summary>
    public void StartRecording(long cycle) => Load([], recording: [], cycle);

    /// <summary>
    /// Takes the tape out: what was inserted, or what has been recorded since
    /// <see cref="StartRecording"/>, its idle line up to <paramref name="cycle"/> included. Leaves
    /// no tape.
    /// </summary>
    public IReadOnlyList<TapeEvent> Eject(long cycle)
    {
        IReadOnlyList<TapeEvent> tape = Contents(cycle);
        Load([], recording: null, cycle);
        return tape;
    }

    /// <summary>Winds back to the start. A recording stops and becomes the tape that plays.</summary>
    public void Rewind(long cycle) => Load(Contents(cycle), recording: null, cycle);

    /// <summary>A write to <c>$FE04</c> or <c>$FE07</c>, at <paramref name="cycle"/>, after the ULA has taken it.</summary>
    public void Write(int register, byte value, long cycle)
    {
        switch (register)
        {
            case 4:
                long start = Math.Max(cycle, _lineFreeAt);
                _lineFreeAt = start + ByteCycles;
                if (RecordingNow)
                {
                    RecordIdle(start);
                    _recording!.Add(new TapeByte(value));
                    _idleFrom = _lineFreeAt;
                }

                _ula.ClearStatus(TransmitEmpty);
                _transmitEmptyAt = start + ReadyCycles;
                break;
            case 7:
                _control = value;
                Move(cycle);
                break;
            default:
                return;
        }

        Schedule();
    }

    /// <summary>A read of <c>$FE04</c> by the CPU: the receive register, and receive-full cleared.</summary>
    public byte ReadData()
    {
        _lostAt = long.MaxValue;
        _ula.ClearStatus(ReceiveFull);
        Schedule();
        return Data;
    }

    /// <summary>Does, in order, everything due by <paramref name="cycle"/>.</summary>
    public void CatchUp(long cycle)
    {
        while (NextEvent <= cycle)
        {
            long at = NextEvent;
            if (_transmitEmptyAt == at)
            {
                _ula.SetStatus(TransmitEmpty);
                _transmitEmptyAt = long.MaxValue;
            }

            if (_lostAt == at)
            {
                LostBytes++;
                _lostAt = long.MaxValue;
                _ula.ClearStatus(ReceiveFull);
            }

            if (_listening)
            {
                if (_byte < _byteStarts.Length && CycleOf(_byteStarts[_byte] + ReadyCycles) == at)
                {
                    Data = _byteValues[_byte++];
                    _lostAt = at + LostCycles;
                    _ula.SetStatus(ReceiveFull);
                }

                long tone = NextTone();
                if (tone != long.MaxValue && CycleOf(tone) == at)
                {
                    _ula.SetStatus(HighTone);
                    _toneFrom = tone;
                }
            }

            NextEvent = Next();
        }
    }

    /// <summary>The tape as it would come out now: what was inserted, or the recording so far with its open idle line.</summary>
    private IReadOnlyList<TapeEvent> Contents(long cycle)
    {
        if (_recording is null)
        {
            return _events;
        }

        if (RecordingNow)
        {
            RecordIdle(cycle);
            _idleFrom = Math.Max(cycle, _lineFreeAt);
        }

        return [.. _recording];
    }

    private void Load(IReadOnlyList<TapeEvent> events, List<TapeEvent>? recording, long cycle)
    {
        var byteStarts = new List<long>();
        var byteValues = new List<byte>();
        var runStarts = new List<long>();
        var runEnds = new List<long>();
        long t = 0;
        foreach (TapeEvent e in events)
        {
            switch (e)
            {
                case Carrier c:
                    ArgumentOutOfRangeException.ThrowIfNegative(c.Cycles, nameof(events));
                    long length = (long)c.Cycles * TapeTiming.CarrierCycleCpuCycles;
                    if (length == 0)
                    {
                        break;
                    }

                    if (runEnds.Count > 0 && runEnds[^1] == t)
                    {
                        runEnds[^1] = t + length; // a carrier straight after a carrier is one tone
                    }
                    else
                    {
                        runStarts.Add(t);
                        runEnds.Add(t + length);
                    }

                    t += length;
                    break;
                case TapeByte b:
                    byteStarts.Add(t);
                    byteValues.Add(b.Value);
                    t += ByteCycles;
                    break;
                case Silence s:
                    ArgumentOutOfRangeException.ThrowIfNegative(s.Cycles, nameof(events));
                    t += s.Cycles;
                    break;
                default:
                    throw new ArgumentException($"Not a tape event: {e}.", nameof(events));
            }
        }

        _events = [.. events];
        _byteStarts = [.. byteStarts];
        _byteValues = [.. byteValues];
        _runStarts = [.. runStarts];
        _runEnds = [.. runEnds];
        _length = t;
        _recording = recording;

        _byte = 0;
        _run = 0;
        _lostAt = long.MaxValue;
        LostBytes = 0;
        _anchorCycle = cycle;
        _anchorPosition = 0;
        _moving = false;
        _listening = false;
        _idleFrom = -1;
        Move(cycle);
        Schedule();
    }

    /// <summary>Sets whether the tape moves and listens from <paramref name="cycle"/>, after a write to the control register or a new tape.</summary>
    private void Move(long cycle)
    {
        bool wasRecording = RecordingNow;
        bool wasListening = _listening;
        _anchorPosition = PositionAt(cycle);
        _anchorCycle = cycle;

        byte mode = (byte)(_control & ModeBits);
        _moving = MotorOn && (Recording ? mode == OutputMode : mode is InputMode or OutputMode);
        _listening = _moving && !Recording && mode == InputMode;

        if (_listening && !wasListening)
        {
            // Heard from here: a byte whose ready point has gone by is missed, and ten bit times
            // of high tone are counted from now.
            while (_byte < _byteStarts.Length && _byteStarts[_byte] + ReadyCycles < _anchorPosition)
            {
                _byte++;
            }

            _toneFrom = _anchorPosition;
        }

        if (RecordingNow && !wasRecording)
        {
            _idleFrom = Math.Max(cycle, _lineFreeAt);
        }
        else if (wasRecording && !RecordingNow)
        {
            RecordIdle(cycle);
            _idleFrom = -1;
        }
    }

    /// <summary>Records the idle line from when it went idle to <paramref name="until"/> as carrier, if it is longer than a bit time.</summary>
    private void RecordIdle(long until)
    {
        long idle = until - _idleFrom;
        if (idle > CarrierThreshold)
        {
            _recording!.Add(new Carrier((int)Math.Round(idle / (double)TapeTiming.CarrierCycleCpuCycles, MidpointRounding.AwayFromZero)));
        }
    }

    /// <summary>The tape time of the next high tone, or <see cref="long.MaxValue"/>: ten bit times after high tone was first heard unbroken, inside a carrier run.</summary>
    private long NextTone()
    {
        while (_run < _runStarts.Length)
        {
            long from = Math.Max(_toneFrom, _runStarts[_run]);
            if (from + HighToneCycles <= _runEnds[_run])
            {
                return from + HighToneCycles;
            }

            _run++;
        }

        return long.MaxValue;
    }

    private long CycleOf(long tapeTime) => _anchorCycle + (tapeTime - _anchorPosition);

    private long Next()
    {
        long next = Math.Min(_transmitEmptyAt, _lostAt);
        if (_listening)
        {
            if (_byte < _byteStarts.Length)
            {
                next = Math.Min(next, CycleOf(_byteStarts[_byte] + ReadyCycles));
            }

            long tone = NextTone();
            if (tone != long.MaxValue)
            {
                next = Math.Min(next, CycleOf(tone));
            }
        }

        return next;
    }

    /// <summary>Works out the next event and tells the ULA, whose own next event it may bring forward.</summary>
    private void Schedule()
    {
        NextEvent = Next();
        _ula.Reschedule();
    }
}
