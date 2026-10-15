using System.Text;

namespace Dbhq.Machines.Kim1;

/// <summary>
/// A teletype on the KIM-1's 20 mA current loop: a printer that decodes the
/// bits the monitor sends, and a keyboard that sends the bits of the
/// characters it is given. Both are worked out from the line, in machine
/// cycles, as a real terminal's receiver and sender would: nothing here reads
/// the monitor's RAM or calls its routines.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wiring</b>, from the KIM-1 User Manual (figures 2.4 and 3.7, and the
/// application connector table in figure 3.9) and the board's schematic. The
/// keyboard loop switches a transistor whose collector, gated by PB5 through a
/// 7400, drives PA7 of the 6530-002: current in the loop (a mark) reads 1, and
/// PB5 high holds PA7 at 1 whatever the loop does. The printer loop is closed
/// by a 7438 open-collector NAND whose inputs are PB0 and that same PA7 line,
/// so the printer sees a mark only while PB0 is high and the keyboard is
/// marking: what the keyboard sends is echoed to the printer by the hardware,
/// and the monitor never echoes in software. This class keeps that rule:
/// <see cref="OutputMark"/> is PB0 and (PB5 or the keyboard line).
/// </para>
/// <para>
/// <b>The characters.</b> Each is a start bit (space), eight bits least
/// significant first, and stop bits (mark). The keyboard sends seven bits of
/// ASCII with the eighth a mark, and two stop bits, as a Teletype Model 33
/// does; the monitor's GETCH reads eight bits and drops the eighth. The
/// printer reads eight bits and keeps seven. A start bit is a space held
/// unbroken from its falling edge to its middle; a mark inside that half bit
/// makes it a glitch, as a selector magnet that never held would.
/// </para>
/// <para>
/// <b>The clock.</b> The bus calls <see cref="Advance"/> at the start of every
/// cycle on which something is due (<see cref="Due"/>), so every edge and every
/// sample here happens on its own cycle, not in a batch. A bit lasts 1,000,000 / <see cref="Baud"/> cycles at the KIM-1's
/// 1 MHz; bit k of a character starts on cycle start + floor(k x 1,000,000 /
/// baud), so the error never adds up across a character.
/// </para>
/// <para>
/// <b>The pace of the keyboard.</b> A real teletype sends a key the moment it
/// is pressed, and one pressed while the monitor is printing is lost, because
/// the monitor is not listening then. The queue given to <see cref="Send(string)"/>
/// is played the way a careful typist types: a character goes when the line
/// has been quiet, in both directions, for <see cref="QuietBits"/> bit times,
/// so a whole command can be queued at once and none of it is lost while the
/// monitor answers the last key.
/// </para>
/// </remarks>
public sealed class Kim1Teletype
{
    /// <summary>The Teletype Model 33's speed, ten characters a second.</summary>
    public const int DefaultBaud = 110;

    /// <summary>The slowest speed a teletype is taken to run at, in bits a second.</summary>
    public const int MinBaud = 50;

    /// <summary>The fastest speed a teletype is taken to run at, in bits a second.</summary>
    public const int MaxBaud = 9600;

    /// <summary>How long both lines must be quiet, in bit times, before the next queued key is sent, unless the constructor says otherwise.</summary>
    public const int DefaultQuietBits = 4;

    /// <summary>The bits on the line for one character from the keyboard: start, eight, and two stop.</summary>
    public const int SendBits = 11;

    private const long Never = long.MaxValue;

    // A key queued between cycles: it may go from the next cycle Advance sees,
    // which is the first the machine runs after it was queued.
    private const long NextCycle = long.MinValue;

    private readonly Queue<char> _queue = new();
    private readonly StringBuilder _printed = new();

    // The keyboard: the character on the line, if any, and when it started.
    private int _sending = -1;
    private long _sendStart;
    private long _sendEnd = long.MinValue / 2;
    private int _sendBit = -1;
    private long _nextSendEdge = Never;

    // What the KIM-1 drives: PB0 and PB5 of the 6530-002, as pin levels.
    private bool _pb0 = true;
    private bool _pb5 = true;

    // The printer's receiver.
    private bool _outputMark = true;
    private long _lastOutputEdge = long.MinValue / 2;
    private bool _receiving;
    private long _receiveStart;
    private int _receiveSample;
    private int _receiveByte;
    private long _nextSample = Never;
    private long _receiverIdleSince = long.MinValue / 2;

    private long _queuedAt;
    private long _due = Never;

    /// <param name="baud">The terminal's speed, in bits a second. The monitor measures it from the first key, RUB OUT, after each reset.</param>
    /// <param name="quietBits">How long both lines must be quiet, in bit times, before the next queued key goes. 0 sends keys back to back, the way a tape reader would, whether the monitor is listening or not.</param>
    public Kim1Teletype(int baud = DefaultBaud, int quietBits = DefaultQuietBits)
    {
        CheckBaud(baud);

        ArgumentOutOfRangeException.ThrowIfNegative(quietBits);
        Baud = baud;
        QuietBits = quietBits;
    }

    /// <summary>Throws if <paramref name="baud"/> is outside <see cref="MinBaud"/> to <see cref="MaxBaud"/>.</summary>
    public static void CheckBaud(int baud)
    {
        if (baud is < MinBaud or > MaxBaud)
        {
            throw new ArgumentOutOfRangeException(nameof(baud), baud, $"a teletype runs between {MinBaud} and {MaxBaud} baud");
        }
    }

    /// <summary>How long both lines must be quiet, in bit times, before the next queued key is sent.</summary>
    public int QuietBits { get; }

    /// <summary>The terminal's speed, in bits a second, for both its keyboard and its printer.</summary>
    public int Baud { get; }

    /// <summary>One bit, in cycles of the KIM-1's 1 MHz clock.</summary>
    public double BitCycles => 1_000_000.0 / Baud;

    /// <summary>Characters queued at the keyboard and not yet started on the line.</summary>
    public int Queued => _queue.Count;

    /// <summary>True while a character from the keyboard is on the line.</summary>
    public bool Sending => _sending >= 0;

    /// <summary>True while the printer is in the middle of a character.</summary>
    public bool Receiving => _receiving;

    /// <summary>Characters the printer threw away because a stop bit was a space.</summary>
    public int FramingErrors { get; private set; }

    /// <summary>The level on the printer loop now: true for a mark, current flowing.</summary>
    public bool OutputMark => _outputMark;

    /// <summary>Raised on every change of the printer loop, with the cycle and the new level (true for a mark).</summary>
    public event Action<long, bool>? OutputChanged;

    /// <summary>Raised for each character the printer decodes, with the cycle its stop bit was read.</summary>
    public event Action<long, char>? Printed;

    /// <summary>The level of the keyboard loop now: true for a mark.</summary>
    public bool KeyboardMark => _sendBit switch
    {
        < 0 => true,
        0 => false,
        <= 8 => ((((_sending & 0x7F) | 0x80) >> (_sendBit - 1)) & 1) != 0,
        _ => true,
    };

    /// <summary>What PA7 of the 6530-002 reads now: the keyboard loop, or 1 while PB5 is high.</summary>
    public bool Pa7 => _pb5 || KeyboardMark;

    /// <summary>
    /// The first cycle on which <see cref="Advance"/> has anything to do: the
    /// next edge the keyboard sends, the next sample the printer takes, or the
    /// moment the next queued key may go.
    /// </summary>
    public long Due => _due;

    /// <summary>Queues one key at the keyboard.</summary>
    public void Send(char c)
    {
        if (c > 0x7F)
        {
            throw new ArgumentException($"'{c}' is not 7-bit ASCII, and a teletype sends nothing else", nameof(c));
        }

        if (_queue.Count == 0)
        {
            _queuedAt = NextCycle;
            _due = NextCycle;
        }

        _queue.Enqueue(c);
    }

    /// <summary>Queues each character of <paramref name="text"/> in turn.</summary>
    public void Send(string text)
    {
        foreach (char c in text)
        {
            Send(c);
        }
    }

    /// <summary>The keys with no printed character of their own, as the User Manual names them in brackets.</summary>
    public static IReadOnlyDictionary<string, char> Named { get; } = new Dictionary<string, char>
    {
        ["SPACE"] = ' ',
        ["CR"] = '\r',
        ["LF"] = '\n',
        ["RUBOUT"] = (char)0x7F,
    };

    /// <summary>
    /// Turns keys written as the User Manual writes them into the characters
    /// they send: [SPACE], [CR], [LF] and [RUBOUT] in brackets, and every
    /// other character as itself. "0200[SPACE]G" is six characters. A bare
    /// space is refused, because on a page it cannot be told from nothing.
    /// </summary>
    public static string Parse(string keys)
    {
        var text = new StringBuilder();
        for (int i = 0; i < keys.Length; i++)
        {
            char c = keys[i];
            if (c == '[')
            {
                int end = keys.IndexOf(']', i);
                string name = end < 0 ? keys[i..] : keys[(i + 1)..end];
                if (end < 0 || !Named.TryGetValue(name, out char named))
                {
                    throw new FormatException($"\"[{name}]\" is not a bracketed teletype key; the keys are {string.Join(", ", Named.Keys.Select(k => $"[{k}]"))}");
                }

                text.Append(named);
                i = end;
                continue;
            }

            if (c is < '!' or > '~')
            {
                throw new FormatException($"character {(int)c} in \"{keys}\" must be written as a bracketed key");
            }

            text.Append(c);
        }

        return text.ToString();
    }

    /// <summary>Drops every key still queued. One already on the line finishes.</summary>
    public void ClearQueue()
    {
        _queue.Clear();
        _due = Refresh();
    }

    /// <summary>Everything the printer has decoded since the last call, as it came: NULs, CR and LF included.</summary>
    public string Drain()
    {
        string text = _printed.ToString();
        _printed.Clear();
        return text;
    }

    /// <summary>
    /// True when nothing is queued or on the line, and both lines have been
    /// quiet for <see cref="QuietBits"/> bit times, or
    /// <see cref="DefaultQuietBits"/> if that is more: the monitor has
    /// finished answering.
    /// </summary>
    public bool Idle(long now) => _queue.Count == 0 && _sending < 0 && !_receiving && now >= LastActivity() + ((long)Math.Max(QuietBits, DefaultQuietBits) * 1_000_000 / Baud);

    /// <summary>
    /// Brings the teletype to <paramref name="cycle"/>: each edge it sends and
    /// each sample its printer takes, on the cycle it falls due. The bus calls
    /// this at the start of every cycle that has reached <see cref="Due"/>,
    /// before the access.
    /// </summary>
    public void Advance(long cycle)
    {
        if (_queuedAt == NextCycle)
        {
            // Every edge and sample before this cycle has been taken on its
            // own cycle already, so the key goes no earlier than this one.
            _queuedAt = cycle;
            _due = ComputeDue();
        }

        CatchUp(cycle);
    }

    /// <summary>
    /// Takes every edge and sample that has fallen due by
    /// <paramref name="cycle"/>, which the machine has already run, as
    /// <see cref="Advance"/> does, except that a key queued since the last
    /// cycle stays unstarted: it was queued after <paramref name="cycle"/>, so
    /// it may not go on or before it. For reading what the printer has decoded
    /// between cycles.
    /// </summary>
    public void CatchUp(long cycle)
    {
        while (ComputeDue() <= cycle)
        {
            long due = ComputeDue();
            if (due == _nextSendEdge)
            {
                SendEdge(due);
            }
            else if (due == _nextSample)
            {
                Sample(due);
            }
            else
            {
                StartSending(due);
            }
        }

        _due = Refresh();
    }

    /// <summary>
    /// The KIM-1 drove PB0 and PB5 to these levels on <paramref name="cycle"/>.
    /// The bus calls this after every write to the 6530-002's port B or its
    /// direction register, and after RES.
    /// </summary>
    public void Drive(long cycle, bool pb0, bool pb5)
    {
        CatchUp(cycle);
        _pb0 = pb0;
        _pb5 = pb5;
        UpdateOutput(cycle);
        _due = Refresh();
    }

    private long BitStart(long start, int bit) => start + (long)bit * 1_000_000 / Baud;

    private long QuietBitsCycles => (long)QuietBits * 1_000_000 / Baud;

    private long LastActivity() => Math.Max(Math.Max(_sendEnd, _lastOutputEdge), _receiverIdleSince);

    private long QuietUntil() => LastActivity() + QuietBitsCycles;

    // What Due says: a key queued since the last cycle is due on the next one,
    // whatever it is that comes first after that.
    private long Refresh() => _queuedAt == NextCycle && _queue.Count > 0 ? NextCycle : ComputeDue();

    private long ComputeDue() => Math.Min(Math.Min(_nextSendEdge, _nextSample), NextStart());

    private long NextStart()
    {
        if (_queue.Count == 0 || _sending >= 0 || _receiving || _queuedAt == NextCycle)
        {
            return Never;
        }

        return Math.Max(_queuedAt, QuietUntil());
    }

    private void StartSending(long cycle)
    {
        _sending = _queue.Dequeue();
        _queuedAt = cycle;
        _sendStart = cycle;
        _sendBit = 0;
        _nextSendEdge = BitStart(cycle, 1);
        UpdateOutput(cycle);
    }

    // The boundary into the next bit of the character on the keyboard line,
    // or the end of its last stop bit.
    private void SendEdge(long cycle)
    {
        _sendBit++;
        if (_sendBit == SendBits)
        {
            _sending = -1;
            _sendBit = -1;
            _sendEnd = cycle;
            _nextSendEdge = Never;
        }
        else
        {
            _nextSendEdge = BitStart(_sendStart, _sendBit + 1);
        }

        UpdateOutput(cycle);
    }

    private void UpdateOutput(long cycle)
    {
        bool mark = _pb0 && Pa7;
        if (mark == _outputMark)
        {
            return;
        }

        _outputMark = mark;
        _lastOutputEdge = cycle;
        OutputChanged?.Invoke(cycle, mark);
        if (mark && _receiving && _receiveSample == 0)
        {
            // Back to a mark before the middle of the start bit: the selector
            // magnet never held, so this is a glitch, not a character. The
            // monitor does this to the line when its delay count is left over
            // from a keypad-mode reset: it sends at about 15,000 baud, and a
            // receiver that looks only at the middle of the start bit would
            // find a space there by chance and print noise from the burst.
            EndReceive(cycle);
        }
        else if (!mark && !_receiving)
        {
            // A falling edge: perhaps a start bit. It is checked at its middle.
            _receiving = true;
            _receiveStart = cycle;
            _receiveSample = 0;
            _receiveByte = 0;
            _nextSample = SampleAt(0);
        }
    }

    // The middle of bit n of the character the printer is reading.
    private long SampleAt(int n) => _receiveStart + ((2L * n + 1) * 1_000_000 / (2L * Baud));

    private void Sample(long cycle)
    {
        bool mark = _outputMark;
        int n = _receiveSample;
        if (n == 0)
        {
            // A start bit is a space held from its falling edge to its middle:
            // a mark in between has already ended the attempt (UpdateOutput).
            // The line can only be a mark here if nothing moved it.
            if (mark)
            {
                EndReceive(cycle);
                return;
            }
        }
        else if (n <= 8)
        {
            if (mark)
            {
                _receiveByte |= 1 << (n - 1);
            }
        }
        else
        {
            if (mark)
            {
                char c = (char)(_receiveByte & 0x7F);
                _printed.Append(c);
                Printed?.Invoke(cycle, c);
            }
            else
            {
                FramingErrors++;
            }

            EndReceive(cycle);
            return;
        }

        _receiveSample = n + 1;
        _nextSample = SampleAt(n + 1);
    }

    private void EndReceive(long cycle)
    {
        _receiving = false;
        _nextSample = Never;
        _receiverIdleSince = cycle;

        // A line still at a space after a framing error starts nothing until
        // it has been back to a mark: only a falling edge starts a character.
    }
}
