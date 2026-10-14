namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The TIA, as far as task 5 of the plan builds it: its write registers, the horizontal sync
/// counter and the blank it decodes, WSYNC's latch that holds the CPU's RDY low, RSYNC, VSYNC ending
/// a frame, VBLANK, the playfield, the background, and a frame of colour numbers written a pixel a
/// colour clock; with the collision latches as the read registers show them and the lines each read
/// drives. Task 6 adds the objects that set the latches, task 7 HMOVE, task 9 the inputs and task
/// 10 the sound.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/tia.md</c> sections 2 to 8 and 19, and <c>bus.md</c>
/// section 4. The bus calls <see cref="Tick"/> three times a CPU cycle, all three before the cycle's
/// access (task 5's choice by the plan's rule, recorded in tia.md section 4).
/// </para>
/// <para>
/// <b>The horizontal sync counter</b> is the chip's: a six-bit polynomial counter stepped once
/// every four colour clocks by a two-phase clock, whose states are decoded for the line's events
/// (tia.md section 3). Each decode acts on the counter's next step, four clocks after the state is
/// entered: RHB (count 16) ends the blank at colour clock 68, and SHB (count 56) resets the counter
/// to 000000 at 228, which starts the next line with the blank on. The playfield is decoded from the
/// same counter, so RSYNC, which resets it, moves the playfield. Task 7's HMOVE latch chooses LRHB
/// (count 18) over RHB; the decode is here for it.
/// </para>
/// <para>
/// <b>The beam.</b> <see cref="HorizontalCounter"/> is the colour clock of the line, 0 to 227, as
/// the beam crosses it; pixel x is colour clock 68 + x. It is the counter's count times four plus
/// the two-phase clock's place, except in the three clocks between an RSYNC and the line it starts.
/// </para>
/// <para>
/// <b>What a pixel is</b>: black (colour 0) in the blank or while VBLANK's output gate is on;
/// otherwise COLUPF where the playfield is lit and COLUBK where it is not, read on the clock itself,
/// so a colour write shows from the clock it lands on (tia.md section 4). VBLANK's gate is taken on
/// the clock after, as section 4 reads from the suite's <c>midline-vblank</c> picture
/// [guessing - verify]. Score mode and priority are task 6's.
/// </para>
/// </remarks>
public sealed class Tia : IReportsState
{
    /// <summary>Colour clocks in a line, in both regions (tia.md section 3).</summary>
    public const int ColourClocksPerLine = 228;

    /// <summary>
    /// The lines a frame may have: one with no end of VSYNC by then ends here (the plan's "Fixed
    /// before running", item 2).
    /// </summary>
    public const int MaxLines = FrameBuffer.MaxLines;

    /// <summary>The write registers are <c>$00</c> to <c>$2C</c>; <c>$2D</c> to <c>$3F</c> select none (tia.md section 2.1).</summary>
    private const int WriteRegisters = 0x2D;

    private const int Vsync = 0x00;
    private const int Vblank = 0x01;
    private const int Wsync = 0x02;
    private const int Rsync = 0x03;
    private const int Colupf = 0x08;
    private const int Colubk = 0x09;
    private const int Ctrlpf = 0x0A;
    private const int Pf0 = 0x0D;
    private const int Pf1 = 0x0E;
    private const int Pf2 = 0x0F;
    private const int Cxclr = 0x2C;

    // The decoded states of the horizontal sync counter, as Towers prints them (tia.md section 3).
    private const int Rhb = 0b011100;
    private const int Shb = 0b010100;
    private const int Illegal = 0b111111;

    /// <summary>The colour clock of pixel 0 (tia.md section 1).</summary>
    private const int FirstPixel = 68;

    /// <summary>The count of each state of the horizontal sync counter, 0 to 62 from 000000; the illegal state counts as 0.</summary>
    private static readonly byte[] CountOf = Counts();

    // What each write register last had written to it.
    private readonly byte[] _registers = new byte[WriteRegisters];

    private readonly TiaPlayfield _playfield = new();
    private readonly FrameBuffer _frame = new();

    // The horizontal sync counter's state, and which of its four colour clocks the next Tick runs.
    private int _counter;
    private int _phase;

    // The colour clock of the line the next Tick runs, 0 to 227.
    private int _clock;

    // RSYNC strobed: the counter resets to 000000 at its next step.
    private bool _rsync;

    // The horizontal blank, set at a line's start and cleared by RHB.
    private bool _hblank;

    // WSYNC's latch, which pulls RDY low until the next line starts (tia.md section 5).
    private bool _rdyLow;

    // VBLANK's output gate, as the pixel sees it: D1 taken a colour clock after the write.
    private bool _vblank;

    // The fifteen collision latches: bit 2r + 1 is read register r's D7, bit 2r its D6 (tia.md 2.2).
    // Bit 12, CXBLPF's D6, is no latch.
    private const ushort Latches = 0xEFFF;
    private ushort _collisions;

    /// <summary>A chip at its power-on state (<see cref="PowerOn"/>).</summary>
    public Tia() => PowerOn();

    /// <summary>The colour clock of the line the next <see cref="Tick"/> runs, 0 to 227; 0 starts the horizontal blank.</summary>
    public int HorizontalCounter => _clock;

    /// <summary>True while WSYNC's latch holds the CPU's RDY line low (tia.md section 5).</summary>
    public bool RdyLow => _rdyLow;

    /// <summary>
    /// True when RDY is high, so a read cycle whose access comes now completes: the bus's WSYNC
    /// test, made at each read's access (bus.md section 5). WSYNC's latch is reset on colour clock 0
    /// of the next line, after a write that lands on it, so a WSYNC written on a line's cycle 76
    /// costs nothing (tia.md section 5).
    /// </summary>
    public bool HaltReleaseDue => !_rdyLow;

    /// <summary>The picture as drawn: the frame being drawn, and the last finished.</summary>
    public FrameBuffer Frame => _frame;

    /// <summary>The playfield, for the tests.</summary>
    internal TiaPlayfield Playfield => _playfield;

    /// <summary>True while the horizontal blank is on, for the colour clock the next <see cref="Tick"/> runs.</summary>
    internal bool HorizontalBlank => _hblank;

    /// <summary>The horizontal sync counter's six bits, as Towers prints them (tia.md section 3).</summary>
    internal int PolynomialCounter => _counter;

    /// <summary>The collision latches as the read registers show them, for the tests until task 6 sets them.</summary>
    internal ushort CollisionLatches
    {
        get => _collisions;
        set => _collisions = (ushort)(value & Latches);
    }

    /// <summary>
    /// The data lines a read of <paramref name="register"/> (A0 to A3) drives; the rest keep the
    /// byte last on the bus (bus.md section 4; tia.md section 2.2). A collision register drives D7
    /// and D6, except CXBLPF, whose D6 is unused and is taken to float [guessing - verify]; an
    /// input port drives D7; <c>$E</c> and <c>$F</c> drive nothing.
    /// </summary>
    public static byte DrivenLines(int register) => register switch
    {
        0x06 => 0x80,
        < 0x08 => 0xC0,
        < 0x0E => 0x80,
        _ => 0x00,
    };

    /// <summary>
    /// The horizontal sync counter's next state with no decode acting: the six bits shift one place
    /// right, the new left bit being 1 when the two rightmost are equal, and the illegal state
    /// 111111 resets to 000000 (tia.md section 3).
    /// </summary>
    internal static int NextState(int state)
    {
        if (state == Illegal)
        {
            return 0;
        }

        int feedback = ((state ^ (state >> 1)) & 1) == 0 ? 0b100000 : 0;
        return (state >> 1) | feedback;
    }

    /// <summary>
    /// One colour clock: RDY released if a line starts here, the playfield's bit moved on, the pixel
    /// drawn, VBLANK's gate taken, and every fourth clock the counter stepped. Allocates nothing.
    /// </summary>
    public void Tick()
    {
        if (_phase == 0)
        {
            // The leading edge of the blank resets WSYNC's latch, on the line's first clock.
            if (_counter == 0)
            {
                _rdyLow = false;
            }

            _playfield.Show();
        }
        else if (_phase == 2)
        {
            _playfield.Fetch(CountOf[_counter] + 1);
        }

        uint x = (uint)(_clock - FirstPixel);
        if (x < FrameBuffer.Width)
        {
            byte colour = _hblank || _vblank ? (byte)0 : _registers[_playfield.Output ? Colupf : Colubk];
            _frame.Put((int)x, (byte)(colour & 0xFE));
        }

        _vblank = (_registers[Vblank] & 0x02) != 0;
        _clock++;
        if (++_phase == 4)
        {
            _phase = 0;
            Step();
        }
    }

    /// <summary>
    /// A read of <paramref name="register"/> (A0 to A3, already decoded): the bits it drives, the
    /// others 0; the bus puts the residue on the lines <see cref="DrivenLines"/> leaves. Reads have
    /// no side effect. Until task 9 the inputs are fixed: INPT0 to INPT3 read 0, as ports with no
    /// paddle connected do (tia.md section 20.3), and INPT4 and INPT5 read 1, both fire buttons up
    /// with the latch off (section 20.2).
    /// </summary>
    public byte Read(int register)
    {
        if (register < 0x08)
        {
            return (byte)(((_collisions >> (2 * register)) & 0x03) << 6);
        }

        return register is 0x0C or 0x0D ? (byte)0x80 : (byte)0x00;
    }

    /// <summary>
    /// A write to <paramref name="register"/> (A0 to A5, already decoded), taken at the end of the
    /// CPU's cycle (tia.md section 4), so it is seen from the colour clock the next
    /// <see cref="Tick"/> runs. <c>$2D</c> to <c>$3F</c> do nothing [guessing - verify].
    /// </summary>
    public void Write(int register, byte value)
    {
        if (register >= WriteRegisters)
        {
            return;
        }

        byte old = _registers[register];
        _registers[register] = value;
        switch (register)
        {
            case Vsync:
                // The end of VSYNC (D1 from 1 to 0) ends the frame (tia.md section 7; the plan's item 2).
                if ((old & 0x02) != 0 && (value & 0x02) == 0)
                {
                    _frame.EndOfVsync();
                }

                break;
            case Wsync:
                _rdyLow = true;
                break;
            case Rsync:
                // The strobe restarts the two-phase clock and the counter resets a full cycle of it
                // later (Towers, tia.md section 6). The model counts that cycle from one clock before
                // the write lands, where the strobe is seen while phase two is high, so the line
                // restarts three clocks after W: a choice [guessing - verify], which keeps a store on
                // cycle k at colour clock 3k after CLEAN_START's two strobes, as the suite's headers
                // place it (CycleOrderTests).
                _rsync = true;
                _phase = 1;
                break;
            case Ctrlpf:
            case Pf0:
            case Pf1:
            case Pf2:
                _playfield.Write(register, value);
                break;
            case Cxclr:
                _collisions = 0;
                break;
        }
    }

    /// <summary>What was last written to <paramref name="register"/>, for the tests.</summary>
    internal byte Written(int register) => _registers[register];

    /// <summary>
    /// The power-on state, none of which a source gives: every register 0, the latches clear, the
    /// counter at 000000 and colour clock 0, the blank on, and no frame yet; a choice recorded in
    /// <c>docs/known-differences.md</c> [guessing - verify].
    /// </summary>
    public void PowerOn()
    {
        Array.Clear(_registers);
        _playfield.PowerOn();
        _frame.PowerOn();
        _counter = 0;
        _phase = 0;
        _clock = 0;
        _rsync = false;
        _hblank = true;
        _rdyLow = false;
        _vblank = false;
        _collisions = 0;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_registers), _registers);
        sink.Add(nameof(_playfield), _playfield);
        sink.Add(nameof(_frame), _frame);
        sink.Add(nameof(_counter), _counter);
        sink.Add(nameof(_phase), _phase);
        sink.Add(nameof(_clock), _clock);
        sink.Add(nameof(_rsync), _rsync);
        sink.Add(nameof(_hblank), _hblank);
        sink.Add(nameof(_rdyLow), _rdyLow);
        sink.Add(nameof(_vblank), _vblank);
        sink.Add(nameof(_collisions), _collisions);
    }

    // The counter's step, every fourth colour clock, where the decoded state it leaves acts.
    private void Step()
    {
        int state = _counter;
        if (state == Rhb)
        {
            _hblank = false;
        }

        if (_rsync || state == Shb)
        {
            // A line starts: the counter at 000000, the blank on, a new row of the frame.
            _rsync = false;
            _counter = 0;
            _clock = 0;
            _hblank = true;
            _frame.NextLine();
            return;
        }

        _counter = NextState(state);
    }

    private static byte[] Counts()
    {
        byte[] counts = new byte[64];
        int state = 0;
        for (int count = 0; count < 63; count++)
        {
            counts[state] = (byte)count;
            state = NextState(state);
        }

        return counts;
    }
}
