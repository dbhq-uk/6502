using Dbhq.Cpu6502;

namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The Atari 2600's bus: the 6507's thirteen address lines, the decode of the TIA, the RIOT and the
/// cartridge with every mirror, the floating data bus, and the WSYNC halt. Each read or write is one
/// CPU cycle, and the other chips are advanced inside it.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/bus.md</c>. The decode, by the low thirteen lines (section 2):
/// </para>
/// <list type="table">
/// <item><term>A12 = 1</term><description>the cartridge's board, which decodes A0 to A11 and sees every access, read or write</description></item>
/// <item><term>A12 = 0, A7 = 0</term><description>the TIA: A0 to A5 for a write, A0 to A3 for a read; so it also answers at <c>$0100-$017F</c>, where the stack reaches it</description></item>
/// <item><term>A12 = 0, A7 = 1, A9 = 0</term><description>the RIOT's 128 bytes of RAM, A0 to A6; so also <c>$0180-$01FF</c>, the stack</description></item>
/// <item><term>A12 = 0, A7 = 1, A9 = 1</term><description>the RIOT's ports and timer, which decode A0 to A4</description></item>
/// </list>
/// <para>
/// A8, A10 and A11 are decoded by nothing below <c>$1000</c>, and A13 to A15 do not leave the 6507,
/// so the core's sixteen-bit address is cut to thirteen here and the core knows nothing of it
/// (AGENTS.md rule 2). The 6507 has no IRQ or NMI pin, so the bus never touches the CPU's lines,
/// and the RIOT's IRQ output goes nowhere.
/// </para>
/// <para>
/// <b>One place runs a cycle: <see cref="Cycle"/>.</b> It is the only code that counts a cycle, so
/// a cycle never passes without an access and an access never happens outside one (AGENTS.md rule
/// 1). It runs the TIA's three colour clocks, then ticks the RIOT, then makes the access. All three
/// colour clocks before the access is the first version, the split the sources state (tia.md
/// section 4); task 5 settles it by the plan's rule. The RIOT is ticked before the access, as
/// <see cref="Riot6532"/> asks, so a timer written on one cycle first falls on the next.
/// </para>
/// <para>
/// <b>The WSYNC halt</b> (section 5) is the NES's DMA halt in shape: a write to WSYNC sets the
/// TIA's latch, and the next <see cref="Read"/> repeats its read, each repeat a whole cycle through
/// <see cref="Cycle"/>, until the TIA reaches the line's start; then it makes the read. A write is
/// never held, so <c>DEC WSYNC</c> writes twice and halts on the next read.
/// </para>
/// <para>
/// <b>The floating bus</b> (section 4). The bus keeps the last byte driven onto it, read or
/// written. A TIA read drives only the lines <see cref="Tia.DrivenLines"/> names; the rest are that
/// byte. Every other chip drives all eight. At power on the byte is <c>$00</c>, a choice.
/// </para>
/// </remarks>
public sealed class Atari2600Bus : Bus, IReportsState
{
    private const ushort AddressMask = 0x1FFF;

    private readonly Tia _tia;
    private readonly Riot6532 _riot;
    private readonly IBoard _board;

    // Atari2600Options.PerColourClockReference: every build is the reference until the speed pass.
    private readonly bool _perColourClockReference;

    private Region _region;
    private long _cycles;
    private long _colourClocks;
    private byte _dataBus;

    // Told of each point where a chip can be seen (Observer), or null.
    private IAtari2600Observer? _observer;

    /// <summary>A bus with the cartridge's board fitted, at its power-on bank.</summary>
    public Atari2600Bus(Cartridge cartridge, Region region, Atari2600Options? options = null)
        : this(BoardOf(cartridge), region, options)
    {
    }

    /// <summary>A bus with <paramref name="board"/> fitted, for the tests to watch the slot.</summary>
    internal Atari2600Bus(IBoard board, Region region, Atari2600Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(region);
        _board = board;
        _region = region;
        _tia = new Tia();
        _riot = new Riot6532();
        _perColourClockReference = (options ?? new Atari2600Options()).PerColourClockReference;
    }

    /// <summary>The parts a CPU access can reach (<see cref="Decode"/>).</summary>
    internal enum Chip
    {
        Tia,
        RiotRam,
        RiotIo,
        Cartridge,
    }

    /// <summary>
    /// True when the process has switched observation on, with the runtime option
    /// <c>Dbhq.Machines.Atari2600.Observable</c> (the tests' project file sets it). Read once, so
    /// when it is off the JIT drops the observer's calls from the cycle, as the NES's are.
    /// </summary>
    internal static readonly bool Observable = AppContext.TryGetSwitch("Dbhq.Machines.Atari2600.Observable", out bool on) && on;

    /// <summary>The TIA, at <c>$0000-$007F</c> and its mirrors.</summary>
    public Tia Tia => _tia;

    /// <summary>The RIOT: its RAM at <c>$0080-$00FF</c> and its ports and timer at <c>$0280-$029F</c>, with their mirrors.</summary>
    public Riot6532 Riot => _riot;

    /// <summary>The cartridge's board, at <c>$1000-$1FFF</c>.</summary>
    public IBoard Board => _board;

    /// <summary>
    /// The region, switched at once with no restart: the two consoles differ in nothing a program
    /// can see (region.md section 5), so nothing in the bus changes with it.
    /// </summary>
    public Region Region
    {
        get => _region;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _region = value;
        }
    }

    /// <summary>CPU cycles since power on, the WSYNC halt's included.</summary>
    public long Cycles => _cycles;

    /// <summary>Colour clocks since power on, kept by the bus: three a cycle.</summary>
    public long ColourClocks => _colourClocks;

    /// <summary>The last byte on the data bus: what a chip drove on the last read, or the byte last written.</summary>
    public byte DataBus => _dataBus;

    /// <summary>
    /// The per-colour-clock reference (<see cref="Atari2600Options.PerColourClockReference"/>). In
    /// task 4 every build advances its chips inside every call, so it changes nothing yet.
    /// </summary>
    internal bool PerColourClockReference => _perColourClockReference;

    /// <summary>
    /// Told of each point where a chip can be seen (<see cref="IAtari2600Observer"/>), when
    /// <see cref="Observable"/> is on; otherwise it is never called. Null for none.
    /// </summary>
    internal IAtari2600Observer? Observer
    {
        get => _observer;
        set => _observer = value;
    }

    /// <summary>The part an address the core puts out reaches, by its low thirteen lines (bus.md section 2).</summary>
    internal static Chip Decode(ushort address)
    {
        if ((address & 0x1000) != 0)
        {
            return Chip.Cartridge;
        }

        if ((address & 0x0080) == 0)
        {
            return Chip.Tia;
        }

        return (address & 0x0200) == 0 ? Chip.RiotRam : Chip.RiotIo;
    }

    /// <summary>
    /// One CPU read: one cycle. If a WSYNC write has pulled RDY low, the read is first repeated, a
    /// cycle at a time, until the TIA reaches the line's start (bus.md section 5).
    /// </summary>
    public override byte Read(ushort address)
    {
        if (_tia.RdyLow)
        {
            Halt(address);
        }

        return Cycle(false, address, 0);
    }

    /// <summary>One CPU write: one cycle. RDY never holds a write (bus.md section 5).</summary>
    public override void Write(ushort address, byte value)
    {
        Cycle(true, address, value);
    }

    /// <summary>
    /// What a read of <paramref name="address"/> would give now, with no cycle and no side effect:
    /// no flag of the RIOT is cleared, no bank switches, and the data bus is left as it is.
    /// </summary>
    public byte Peek(ushort address)
    {
        ushort line = (ushort)(address & AddressMask);
        switch (Decode(line))
        {
            case Chip.Tia:
                int register = line & 0x0F;
                byte driven = Tia.DrivenLines(register);
                return (byte)((_tia.Read(register) & driven) | (_dataBus & ~driven));
            case Chip.RiotRam:
                return _riot.ReadRam(line);
            case Chip.RiotIo:
                return _riot.PeekRegister(line);
            default:
                return _board.Peek(line);
        }
    }

    /// <summary>Puts a byte in the RIOT's RAM (<paramref name="offset"/> 0 to 127) with no cycle, for a test to set up memory.</summary>
    public void PokeRam(int offset, byte value) => _riot.WriteRam(offset, value);

    /// <summary>
    /// The power-on state: the TIA, the RIOT and the board at theirs, the counts at zero and the
    /// data bus at <c>$00</c> (bus.md section 8 item 3, a choice).
    /// </summary>
    internal void PowerOn()
    {
        _tia.PowerOn();
        _riot.PowerOn();
        _board.PowerOn();
        _cycles = 0;
        _colourClocks = 0;
        _dataBus = 0;
    }

    /// <summary>
    /// The one cycle. Counts it, runs the TIA's three colour clocks, ticks the RIOT, makes the access
    /// and keeps the byte on the data bus. Returns the byte a read gave, and <paramref name="value"/>
    /// for a write.
    /// </summary>
    private byte Cycle(bool write, ushort address, byte value)
    {
        _cycles++;
        _colourClocks += 3;
        long frames = _tia.Frames;
        _tia.Tick();
        _tia.Tick();
        _tia.Tick();
        _riot.Tick();

        ushort line = (ushort)(address & AddressMask);
        Chip chip = Decode(line);
        int bank = 0;
        if (Observable && _observer is not null)
        {
            bank = _board.Bank;
        }

        if (write)
        {
            _dataBus = value;
            switch (chip)
            {
                case Chip.Tia:
                    _tia.Write(line & 0x3F, value);
                    break;
                case Chip.RiotRam:
                    _riot.WriteRam(line, value);
                    break;
                case Chip.RiotIo:
                    _riot.WriteRegister(line, value);
                    break;
                default:
                    _board.Access(line, write: true, value);
                    break;
            }
        }
        else
        {
            switch (chip)
            {
                case Chip.Tia:
                    int register = line & 0x0F;
                    byte driven = Tia.DrivenLines(register);
                    value = (byte)((_tia.Read(register) & driven) | (_dataBus & ~driven));
                    break;
                case Chip.RiotRam:
                    value = _riot.ReadRam(line);
                    break;
                case Chip.RiotIo:
                    value = _riot.ReadRegister(line);
                    break;
                default:
                    value = _board.Access(line, write: false, _dataBus);
                    break;
            }

            _dataBus = value;
        }

        if (Observable && _observer is not null)
        {
            Observe(chip, line, write, value, bank, frames);
        }

        return value;
    }

    // The points the observer is told of in a cycle: the TIA, the RIOT's I/O, a bank switch, and a
    // frame's end, which a colour clock or a VSYNC write can make.
    private void Observe(Chip chip, ushort line, bool write, byte value, int bank, long frames)
    {
        if (chip is Chip.Tia or Chip.RiotIo || (chip == Chip.Cartridge && _board.Bank != bank))
        {
            _observer!.Accessed(line, write, value);
        }

        if (_tia.Frames != frames)
        {
            _observer!.FrameEnded(_cycles);
        }
    }

    // The CPU's read of address held while RDY is low: the same read, a cycle at a time, until the
    // cycle about to run starts the line (Tia.HaltReleaseDue). Each is a full bus access, so the
    // board and the chips see it (bus.md section 5; section 8 item 2 for the board).
    private void Halt(ushort address)
    {
        while (!_tia.HaltReleaseDue)
        {
            Cycle(false, address, 0);
        }

        if (Observable && _observer is not null)
        {
            _observer.HaltReleased(_cycles);
        }
    }

    private static IBoard BoardOf(Cartridge cartridge)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        return cartridge.CreateBoard();
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_tia), _tia);
        sink.Add(nameof(_riot), _riot);
        sink.Add(nameof(_board), _board as IReportsState ?? throw new InvalidOperationException("the board does not report its state"));
        sink.Skip(nameof(_perColourClockReference), StateReport.Fixed);
        sink.Add(nameof(_region), _region.ColourClockHz);
        sink.Add(nameof(_cycles), _cycles);
        sink.Add(nameof(_colourClocks), _colourClocks);
        sink.Add(nameof(_dataBus), _dataBus);
        sink.Skip(nameof(_observer), "the observer itself");
    }
}
