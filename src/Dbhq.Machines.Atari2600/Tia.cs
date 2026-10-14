namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The TIA, as far as task 4 of the plan builds it: its write registers stored, its horizontal
/// counter, the WSYNC latch that holds the CPU's RDY low, VSYNC ending a frame, the collision
/// latches as the read registers show them, and the lines each read drives. Task 5 adds the line
/// and the picture, task 6 the objects that set the latches, task 7 HMOVE, task 9 the inputs and
/// task 10 the sound.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/tia.md</c> sections 2, 3, 5 and 7 and <c>bus.md</c> section 4.
/// The bus calls <see cref="Tick"/> three times a CPU cycle; the order of those three and the
/// access is the bus's (tia.md section 4; task 5 settles it).
/// </para>
/// <para>
/// <b>The horizontal counter</b> here counts colour clocks, 0 to 227, where the chip has a
/// polynomial counter stepped every four (tia.md section 3). The count is the same at every colour
/// clock; task 5 builds the decodes from the chip's own counter, so the later effects come from it.
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
    public const int MaxLines = 400;

    /// <summary>The write registers are <c>$00</c> to <c>$2C</c>; <c>$2D</c> to <c>$3F</c> select none (tia.md section 2.1).</summary>
    private const int WriteRegisters = 0x2D;

    private const int Vsync = 0x00;
    private const int Wsync = 0x02;
    private const int Cxclr = 0x2C;

    // What each write register last had written to it.
    private readonly byte[] _registers = new byte[WriteRegisters];

    // The colour clock the next Tick runs: 0 to 227, 0 the first clock of the horizontal blank.
    private int _horizontal;

    // WSYNC's latch, which pulls RDY low until the next line starts (tia.md section 5).
    private bool _rdyLow;

    // The fifteen collision latches: bit 2r + 1 is read register r's D7, bit 2r its D6 (tia.md 2.2).
    // Bit 12, CXBLPF's D6, is no latch.
    private const ushort Latches = 0xEFFF;
    private ushort _collisions;

    // Lines begun since the frame began, and the frames ended since power on.
    private int _line;
    private long _frames;
    private int _lastFrameLines;

    /// <summary>A chip at its power-on state (<see cref="PowerOn"/>).</summary>
    public Tia() => PowerOn();

    /// <summary>The colour clock the next <see cref="Tick"/> runs, 0 to 227; 0 starts the horizontal blank.</summary>
    public int HorizontalCounter => _horizontal;

    /// <summary>True while WSYNC's latch holds the CPU's RDY line low (tia.md section 5).</summary>
    public bool RdyLow => _rdyLow;

    /// <summary>
    /// True when the next colour clock is the line's first, which resets WSYNC's latch: a cycle
    /// that starts here is not halted (tia.md section 5: RDY goes high at colour clock 0, so a
    /// write on a line's cycle 76 costs nothing). The bus's test for the end of a halt.
    /// </summary>
    public bool HaltReleaseDue => _horizontal == 0;

    /// <summary>Frames ended since power on: at each end of VSYNC, or at <see cref="MaxLines"/> lines without one.</summary>
    public long Frames => _frames;

    /// <summary>The lines the last frame had.</summary>
    public int LastFrameLines => _lastFrameLines;

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
    /// One colour clock. The first clock of a line resets WSYNC's latch, and the counter wraps
    /// after 227, ending a frame at the line limit.
    /// </summary>
    public void Tick()
    {
        if (_horizontal == 0)
        {
            _rdyLow = false;
        }

        if (++_horizontal < ColourClocksPerLine)
        {
            return;
        }

        _horizontal = 0;
        if (++_line >= MaxLines)
        {
            EndFrame();
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
    /// CPU's cycle (tia.md section 4). <c>$2D</c> to <c>$3F</c> do nothing [guessing - verify].
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
                    EndFrame();
                }

                break;
            case Wsync:
                _rdyLow = true;
                break;
            case Cxclr:
                _collisions = 0;
                break;
        }
    }

    /// <summary>What was last written to <paramref name="register"/>, for the tests.</summary>
    internal byte Written(int register) => _registers[register];

    /// <summary>
    /// The power-on state, none of which a source gives: every register 0, the latches clear, and
    /// the counter at colour clock 0, a choice recorded in <c>docs/known-differences.md</c>
    /// [guessing - verify].
    /// </summary>
    public void PowerOn()
    {
        Array.Clear(_registers);
        _horizontal = 0;
        _rdyLow = false;
        _collisions = 0;
        _line = 0;
        _frames = 0;
        _lastFrameLines = 0;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_registers), _registers);
        sink.Add(nameof(_horizontal), _horizontal);
        sink.Add(nameof(_rdyLow), _rdyLow);
        sink.Add(nameof(_collisions), _collisions);
        sink.Add(nameof(_line), _line);
        sink.Add(nameof(_frames), _frames);
        sink.Add(nameof(_lastFrameLines), _lastFrameLines);
    }

    private void EndFrame()
    {
        _lastFrameLines = _line;
        _line = 0;
        _frames++;
    }
}
