namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The TIA, as far as task 6 of the plan builds it: its write registers, the horizontal sync
/// counter and the blank it decodes, WSYNC's latch that holds the CPU's RDY low, RSYNC, VSYNC ending
/// a frame, VBLANK, the playfield, the two players, the two missiles and the ball, priority and
/// score mode, the fifteen collision latches, and a frame of colour numbers written a pixel a
/// colour clock; with the lines each read drives; HMOVE (task 7) and the six input ports (task 9).
/// Task 10 adds the sound.
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
/// otherwise the colour register of the front object lit on that clock, by section 13's priority,
/// and COLUBK where none is. The colour registers are read on the clock itself, so a colour write
/// shows from the clock it lands on (tia.md section 4). VBLANK's gate is taken on the clock after,
/// as section 4 reads from the suite's <c>midline-vblank</c> picture [guessing - verify].
/// </para>
/// <para>
/// <b>The objects and the collisions</b> (sections 9 to 14). Each clock, the five objects and the
/// playfield give their serial outputs; the picture takes the front one, and the collision latches
/// take every pair that is on together, from the same six signals, so a collision is never worked
/// out apart from what is drawn. The latches set in the horizontal blank too, where the objects'
/// outputs hold as their counters wait, and only VBLANK's gate stops them (section 14). The objects'
/// counters are clocked by MOTCK, which runs while the blank is off; RSYNC restarts the line and
/// not them (section 6).
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
    private const int Nusiz0 = 0x04;
    private const int Nusiz1 = 0x05;
    private const int Colup0 = 0x06;
    private const int Colup1 = 0x07;
    private const int Colupf = 0x08;
    private const int Colubk = 0x09;
    private const int Ctrlpf = 0x0A;
    private const int Pf0 = 0x0D;
    private const int Pf1 = 0x0E;
    private const int Pf2 = 0x0F;
    private const int Refp0 = 0x0B;
    private const int Refp1 = 0x0C;
    private const int Resp0 = 0x10;
    private const int Resp1 = 0x11;
    private const int Resm0 = 0x12;
    private const int Resm1 = 0x13;
    private const int Resbl = 0x14;
    private const int Grp0 = 0x1B;
    private const int Grp1 = 0x1C;
    private const int Enam0 = 0x1D;
    private const int Enam1 = 0x1E;
    private const int Enabl = 0x1F;
    private const int Vdelp0 = 0x25;
    private const int Vdelp1 = 0x26;
    private const int Vdelbl = 0x27;
    private const int Resmp0 = 0x28;
    private const int Resmp1 = 0x29;
    private const int Hmp0 = 0x20;
    private const int Hmbl = 0x24;
    private const int Hmove = 0x2A;
    private const int Hmclr = 0x2B;
    private const int Cxclr = 0x2C;

    // The six serial outputs, as bits of the mask the collision table is indexed by.
    private const int P0Bit = 1;
    private const int P1Bit = 2;
    private const int M0Bit = 4;
    private const int M1Bit = 8;
    private const int BlBit = 16;
    private const int PfBit = 32;

    // The decoded states of the horizontal sync counter, as Towers prints them (tia.md section 3).
    private const int Rhb = 0b011100;
    private const int Lrhb = 0b010111;
    private const int Shb = 0b010100;
    private const int Illegal = 0b111111;

    /// <summary>The colour clock of pixel 0 (tia.md section 1).</summary>
    private const int FirstPixel = 68;

    /// <summary>The count of each state of the horizontal sync counter, 0 to 62 from 000000; the illegal state counts as 0.</summary>
    private static readonly byte[] CountOf = Counts();

    /// <summary>The latches each set of lit outputs sets: indexed by the six-bit mask of P0Bit to PfBit.</summary>
    private static readonly ushort[] CollisionsOf = CollisionTable();

    // What each write register last had written to it.
    private readonly byte[] _registers = new byte[WriteRegisters];

    private readonly TiaPlayfield _playfield = new();
    private readonly TiaPlayer _player0 = new();
    private readonly TiaPlayer _player1 = new();
    private readonly TiaMissile _missile0 = new();
    private readonly TiaMissile _missile1 = new();
    private readonly TiaBall _ball = new();
    private readonly TiaMotion _motion = new();
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

    // The fire buttons' pins, bit 0 INPT4's and bit 1 INPT5's, 1 high (up); the outside world, so
    // power on leaves them. And the two latches VBLANK D6 enables, 1 while nothing has pulled them.
    private const byte FireBoth = 0b11;
    private byte _firePins = FireBoth;
    private byte _fireLatches = FireBoth;

    /// <summary>A chip at its power-on state (<see cref="PowerOn"/>).</summary>
    public Tia() => PowerOn();

    /// <summary>
    /// The colour clock of the line the next <see cref="Tick"/> runs, 0 to 227; 0 starts the
    /// horizontal blank. In the three clocks between an RSYNC and the line it starts, the count runs
    /// on from where it was, so an RSYNC landing at colour clock 227 reads 228 and 229 before the new
    /// line's 0: the beam's place on a line cut short, not a clock of a 228-clock line.
    /// </summary>
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

    /// <summary>Player 0, for the tests.</summary>
    internal TiaPlayer Player0 => _player0;

    /// <summary>Player 1, for the tests.</summary>
    internal TiaPlayer Player1 => _player1;

    /// <summary>Missile 0, for the tests.</summary>
    internal TiaMissile Missile0 => _missile0;

    /// <summary>Missile 1, for the tests.</summary>
    internal TiaMissile Missile1 => _missile1;

    /// <summary>The ball, for the tests.</summary>
    internal TiaBall Ball => _ball;

    /// <summary>HMOVE and the motion clocks, for the tests.</summary>
    internal TiaMotion Motion => _motion;

    /// <summary>True while the horizontal blank is on, for the colour clock the next <see cref="Tick"/> runs.</summary>
    internal bool HorizontalBlank => _hblank;

    /// <summary>The horizontal sync counter's six bits, as Towers prints them (tia.md section 3).</summary>
    internal int PolynomialCounter => _counter;

    /// <summary>
    /// The fire buttons' pins, INPT4's on bit 0 and INPT5's on bit 1, 1 high, the button up (tia.md
    /// section 20.1); set by <see cref="Controllers"/>. While VBLANK D6 enables the latches, a pin
    /// that goes low clears its latch, which stays 0 until D6 is cleared (section 20.2).
    /// </summary>
    internal byte FirePins
    {
        get => _firePins;
        set
        {
            _firePins = (byte)(value & FireBoth);
            CatchFire();
        }
    }

    /// <summary>The collision latches as the read registers show them, for the tests.</summary>
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
    /// One colour clock: RDY released if a line starts here, the playfield's bit moved on, the six
    /// outputs taken, the collision latches set and the pixel drawn from them, the objects clocked,
    /// VBLANK's gate taken, and every fourth clock the counter stepped. Allocates nothing.
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

        byte[] r = _registers;

        // HMOVE's extra clocks act before the pixel is latched (TiaMotion); an object given one
        // takes no motion clock at the end of this colour clock, so on the drawn line the two merge.
        bool motionClock = !_hblank;
        int extra = _motion.Tick(_phase, r);
        if (extra != 0)
        {
            ClockObjects(extra, motionClock: true);
        }

        int lit = 0;
        if (_player0.Output(r[Refp0], r[Vdelp0]))
        {
            lit |= P0Bit;
        }

        if (_player1.Output(r[Refp1], r[Vdelp1]))
        {
            lit |= P1Bit;
        }

        if (_missile0.Output(r[Enam0], r[Resmp0], r[Nusiz0]))
        {
            lit |= M0Bit;
        }

        if (_missile1.Output(r[Enam1], r[Resmp1], r[Nusiz1]))
        {
            lit |= M1Bit;
        }

        if (_ball.Output(r[Vdelbl], r[Ctrlpf]))
        {
            lit |= BlBit;
        }

        if (_playfield.Output)
        {
            lit |= PfBit;
        }

        // Only VBLANK's gate stops the latches (section 14); the model takes it as the picture
        // does, a colour clock after the write [guessing - verify].
        if (!_vblank)
        {
            _collisions |= CollisionsOf[lit];
        }

        uint x = (uint)(_clock - FirstPixel);
        if (x < FrameBuffer.Width)
        {
            byte colour = _hblank || _vblank ? (byte)0 : r[ColourRegister(lit, r[Ctrlpf], _playfield.RightHalf)];
            _frame.Put((int)x, (byte)(colour & 0xFE));
        }

        // The objects' counters run on MOTCK, while the blank is off, but for those an extra clock
        // has stepped already.
        ClockObjects(~extra & 0x1F, motionClock);
        _player0.SeeRegisters();
        _player1.SeeRegisters();

        _vblank = (r[Vblank] & 0x02) != 0;
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
    /// no side effect. INPT0 to INPT3 read 0, as ports with no paddle connected do, the dump on or off
    /// (tia.md section 20.3). INPT4 and INPT5 read their fire button's pin, or its latch while VBLANK
    /// D6 enables it (section 20.2).
    /// </summary>
    public byte Read(int register)
    {
        if (register < 0x08)
        {
            return (byte)(((_collisions >> (2 * register)) & 0x03) << 6);
        }

        if (register is not (0x0C or 0x0D))
        {
            return 0x00;
        }

        byte level = (_registers[Vblank] & 0x40) != 0 ? _fireLatches : _firePins;
        return (byte)(((level >> (register - 0x0C)) & 1) << 7);
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
            case Vblank:
                // D6 clear removes the latches and resets them to 1; D6 set brings them in, and a
                // button already held clears its latch at once (tia.md section 20.2).
                if ((value & 0x40) == 0)
                {
                    _fireLatches = FireBoth;
                }
                else
                {
                    CatchFire();
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
            case Resp0:
                _player0.Reset();
                break;
            case Resp1:
                _player1.Reset();
                break;
            case Resm0:
                _missile0.Reset();
                break;
            case Resm1:
                _missile1.Reset();
                break;
            case Resbl:
                _ball.Reset();
                break;
            case Grp0:
                // GRP0 writes player 0's new register and copies player 1's new to its old.
                _player0.WriteGraphics(value);
                _player1.CopyNewToOld();
                break;
            case Grp1:
                // GRP1 writes player 1's new register and copies player 0's and the ball's new to old.
                _player1.WriteGraphics(value);
                _player0.CopyNewToOld();
                _ball.CopyNewToOld();
                break;
            case Enabl:
                _ball.WriteEnable(value);
                break;
            case Ctrlpf:
            case Pf0:
            case Pf1:
            case Pf2:
                _playfield.Write(register, value);
                break;
            case Hmove:
                _motion.Strobe();
                break;
            case Hmclr:
                // HMCLR sets the five motion registers to 0, and not the more-motion latches (section 15).
                Array.Clear(_registers, Hmp0, Hmbl - Hmp0 + 1);
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
    /// counter at 000000 and colour clock 0, the blank on, every object's counter at count 0 and
    /// quarter 0 with nothing held or drawing, the fire latches disabled and at 1 (VBLANK D6 is 0),
    /// and no frame yet; a choice recorded in <c>docs/known-differences.md</c> [guessing - verify].
    /// The fire buttons' pins are left as they are: they are the outside world.
    /// </summary>
    public void PowerOn()
    {
        Array.Clear(_registers);
        _playfield.PowerOn();
        _player0.PowerOn();
        _player1.PowerOn();
        _missile0.PowerOn();
        _missile1.PowerOn();
        _ball.PowerOn();
        _motion.PowerOn();
        _frame.PowerOn();
        _counter = 0;
        _phase = 0;
        _clock = 0;
        _rsync = false;
        _hblank = true;
        _rdyLow = false;
        _vblank = false;
        _collisions = 0;
        _fireLatches = FireBoth;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_registers), _registers);
        sink.Add(nameof(_playfield), _playfield);
        sink.Add(nameof(_player0), _player0);
        sink.Add(nameof(_player1), _player1);
        sink.Add(nameof(_missile0), _missile0);
        sink.Add(nameof(_missile1), _missile1);
        sink.Add(nameof(_ball), _ball);
        sink.Add(nameof(_motion), _motion);
        sink.Add(nameof(_frame), _frame);
        sink.Add(nameof(_counter), _counter);
        sink.Add(nameof(_phase), _phase);
        sink.Add(nameof(_clock), _clock);
        sink.Add(nameof(_rsync), _rsync);
        sink.Add(nameof(_hblank), _hblank);
        sink.Add(nameof(_rdyLow), _rdyLow);
        sink.Add(nameof(_vblank), _vblank);
        sink.Add(nameof(_collisions), _collisions);
        sink.Add(nameof(_firePins), _firePins);
        sink.Add(nameof(_fireLatches), _fireLatches);
    }

    // While VBLANK D6 enables the latches, a fire pin that is low clears its latch (tia.md section
    // 20.2). The pins change only between the CPU's accesses and D6 only by a write, so the latches
    // are worked out there and not every colour clock, and a read sees them as it would if they were.
    private void CatchFire()
    {
        if ((_registers[Vblank] & 0x40) != 0)
        {
            _fireLatches &= _firePins;
        }
    }

    // The counter's step, every fourth colour clock, where the decoded state it leaves acts.
    private void Step()
    {
        int state = _counter;
        if (state == (_motion.Extended ? Lrhb : Rhb))
        {
            // RHB ends the blank at colour clock 68, or LRHB at 76 when HMOVE's latch is set (section 15).
            _hblank = false;
        }

        if (_rsync || state == Shb)
        {
            // A line starts: the counter at 000000, the blank on, a new row of the frame.
            _rsync = false;
            _counter = 0;
            _clock = 0;
            _hblank = true;
            _motion.LineStart();
            _frame.NextLine();
            return;
        }

        _counter = NextState(state);
    }

    /// <summary>
    /// The colour register a pixel takes from the lit outputs (tia.md section 13). With CTRLPF D2
    /// clear, P0 and M0 are in front, then P1 and M1, then the playfield and the ball; with it set
    /// the playfield and the ball are in front. Score mode, D1 with D2 clear, draws the playfield's
    /// left half in COLUP0 at player 0's place and its right half in COLUP1 at player 1's, and the
    /// ball keeps COLUPF (2k6specs, the one source for all three [guessing - verify]).
    /// </summary>
    private static int ColourRegister(int lit, byte ctrlpf, bool rightHalf)
    {
        bool pf = (lit & PfBit) != 0;
        if ((ctrlpf & 0x04) != 0)
        {
            if ((lit & (PfBit | BlBit)) != 0)
            {
                return Colupf;
            }
        }
        else if (pf && (ctrlpf & 0x02) != 0)
        {
            // Score mode: the playfield is player 0's on the left and player 1's on the right.
            lit = (lit & ~PfBit) | (rightHalf ? P1Bit : P0Bit);
        }

        if ((lit & (P0Bit | M0Bit)) != 0)
        {
            return Colup0;
        }

        if ((lit & (P1Bit | M1Bit)) != 0)
        {
            return Colup1;
        }

        return (lit & (PfBit | BlBit)) != 0 ? Colupf : Colubk;
    }

    // A clock of each object in mask (TiaMotion's bits): a player's lock point resets its missile's
    // counter when RESMP is set (sections 9 and 11), so the players go first.
    private void ClockObjects(int mask, bool motionClock)
    {
        byte[] r = _registers;
        if ((mask & TiaMotion.P0) != 0 && _player0.Clock(motionClock, r[Nusiz0]) && (r[Resmp0] & 0x02) != 0)
        {
            _missile0.LockToPlayer();
        }

        if ((mask & TiaMotion.P1) != 0 && _player1.Clock(motionClock, r[Nusiz1]) && (r[Resmp1] & 0x02) != 0)
        {
            _missile1.LockToPlayer();
        }

        if ((mask & TiaMotion.M0) != 0)
        {
            _missile0.Clock(motionClock, r[Nusiz0]);
        }

        if ((mask & TiaMotion.M1) != 0)
        {
            _missile1.Clock(motionClock, r[Nusiz1]);
        }

        if ((mask & TiaMotion.Bl) != 0)
        {
            _ball.Clock(motionClock);
        }
    }

    // The fifteen latches each pair of outputs sets (tia.md section 2.2): bit 2r + 1 is read
    // register r's D7 and bit 2r its D6.
    private static ushort[] CollisionTable()
    {
        (int A, int B, int Latch)[] pairs =
        [
            (M0Bit, P1Bit, 1), (M0Bit, P0Bit, 0),
            (M1Bit, P0Bit, 3), (M1Bit, P1Bit, 2),
            (P0Bit, PfBit, 5), (P0Bit, BlBit, 4),
            (P1Bit, PfBit, 7), (P1Bit, BlBit, 6),
            (M0Bit, PfBit, 9), (M0Bit, BlBit, 8),
            (M1Bit, PfBit, 11), (M1Bit, BlBit, 10),
            (BlBit, PfBit, 13),
            (P0Bit, P1Bit, 15), (M0Bit, M1Bit, 14),
        ];
        var table = new ushort[64];
        for (int lit = 0; lit < 64; lit++)
        {
            foreach ((int a, int b, int latch) in pairs)
            {
                if ((lit & a) != 0 && (lit & b) != 0)
                {
                    table[lit] |= (ushort)(1 << latch);
                }
            }
        }

        return table;
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
