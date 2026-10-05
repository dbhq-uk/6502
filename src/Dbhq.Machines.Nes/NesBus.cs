using Dbhq.Cpu6502;

namespace Dbhq.Machines.Nes;

/// <summary>
/// The NES's address decoding and its clock: each read or write is one CPU cycle, and the other
/// chips are advanced inside it.
/// </summary>
/// <remarks>
/// <para>
/// The memory map (fact sheet <c>bus.md</c> section 1):
/// </para>
/// <list type="table">
/// <item><term>$0000-$1FFF</term><description>2 KB of RAM, repeated four times</description></item>
/// <item><term>$2000-$3FFF</term><description>the PPU's eight registers, repeated every eight bytes</description></item>
/// <item><term>$4000-$4017</term><description>the sound unit and I/O: the pulse, triangle, noise and DMC registers, OAM DMA at $4014, the channel enables at $4015, the controller strobe and pad 1 at $4016, the frame counter and pad 2 at $4017. Fully decoded, no mirrors</description></item>
/// <item><term>$4018-$401F</term><description>disabled test registers: open bus</description></item>
/// <item><term>$4020-$FFFF</term><description>the cartridge's board</description></item>
/// </list>
/// <para>
/// <b>One place runs a cycle: <see cref="Cycle"/>.</b> It is the only code that counts a cycle, so a
/// cycle never passes without an access and an access never happens outside one (AGENTS.md rule 1).
/// It advances the dot accumulator, notes the chips' interrupt lines, runs two of the cycle's dots,
/// ticks the sound unit, makes the access, runs the rest of the dots, and hands the CPU the lines it
/// noted.
/// </para>
/// <para>
/// <b>The order inside a cycle was measured</b> against Blargg's <c>ppu_vbl_nmi</c> singles (task 4
/// of the NES plan; the table of what was tried is in <c>timing.md</c> section 3 and the journal).
/// Two things were varied: how many of the cycle's dots run before the access (0 to 3), and when
/// in the cycle the PPU's NMI output is taken for the CPU (at the start, after the dots before the
/// access, after the access, at the end). Of the sixteen, one passes all ten singles: <b>two dots
/// before the access, the rest after, and the lines as the chips held them when the cycle
/// began</b>. A line that changes during a cycle reaches the CPU in the next one, which is the
/// core's convention, "the line changed at the start of a cycle" (its interrupts were checked
/// against the transistor-level model that way). So an NMI enabled by a write in an instruction's
/// last cycle is taken after the next instruction, as test 4 asks. On PAL the cycle with a fourth
/// dot runs it after the access: two before, two after. Nothing tests that on PAL; it is the same
/// rule, kept.
/// </para>
/// <para>
/// <b>The dot ratio is whole numbers.</b> NTSC is three dots a cycle. PAL is 3.2, which is 16 dots
/// in 5 cycles: each cycle adds 16 to an accumulator, runs one dot for every 5 it holds and keeps
/// the remainder, which gives 3, 3, 3, 3, 4 from zero (<c>timing.md</c> section 3). The numbers come
/// from <see cref="Region"/>, so the bus never asks which region it is.
/// </para>
/// <para>
/// <b>Open bus</b> (<c>bus.md</c> section 3). The bus holds the last value that crossed it, and a
/// read of an address nothing drives returns it. Every read and write updates it, except a read of
/// <c>$4015</c>, which is internal to the CPU. The controller ports drive bits 4 to 0 and leave 7 to
/// 5 to it. It is an assumption that nothing else drives the bus: the sheet's own worked example
/// (<c>LDA $5000</c> returns <c>$50</c>) holds because the CPU's last read was the operand's high
/// byte.
/// </para>
/// <para>
/// The sound unit is <see cref="Apu"/> (task 8): it is ticked before the access, so a read of
/// <c>$4015</c> sees a flag set in its own cycle, and its IRQ line reaches the CPU by the same
/// start-of-cycle rule as the NMI, which <c>pal_apu_tests</c> 08.irq_timing confirms to the cycle.
/// Its mixed level goes to <see cref="Sound"/> each cycle (task 9). The two DMA units, OAM's and
/// the DMC's, halt the CPU on a read and run in <see cref="RunDma"/>. OAM DMA came early, in task
/// 5, because the sprite test ROMs need it; the controllers and the audit of OAM DMA are task 7;
/// the DMC's DMA is task 9.
/// </para>
/// </remarks>
public sealed class NesBus : IBus
{
    private readonly byte[] _ram = new byte[0x800];
    private readonly IMapper _mapper;
    private readonly Ppu _ppu;
    private readonly Apu _apu;
    private readonly SampleBuffer _sound;
    private readonly bool _dmcRepeatsHaltedRead;
    private Cpu? _cpu;

    // The value last on the CPU's data bus.
    private byte _openBus;

    // The dots owed: grows by the region's numerator each cycle, and a dot runs for each whole
    // denominator it holds.
    private int _dotAccumulator;

    // The accumulator worked out once for each value it can hold, 0 to the denominator less one:
    // the dots a cycle starting there runs, and the value it leaves. A cycle then divides nothing.
    private readonly int[] _dotsFrom;
    private readonly int[] _accumulatorAfter;

    // What the board said it needs, asked once: the per-cycle call, and its IRQ line read.
    private readonly bool _mapperCountsCycles;
    private readonly bool _mapperCanInterrupt;

    private long _cycles;
    private long _ppuDots;

    // Of each cycle's dots, how many run before the access; the rest run after (measured, see above).
    private const int DotsBeforeAccess = 2;

    // The two controller ports: $4016 reads the first, $4017 the second. A write to $4016 strobes both.
    private readonly Controller[] _controllers = [new Controller(), new Controller()];

    // The address the previous cycle read, or -1 when it was a write or there was none. A pad's clock
    // is the read line, so reads in consecutive cycles of one address are one clock (bus.md 6, 7).
    private int _lastReadAddress = -1;

    // The page a write to $4014 asked OAM DMA to copy, or -1 when none is waiting.
    private int _dmaPage = -1;

    /// <summary>
    /// A bus with the cartridge's board fitted. The board is made here, so a cartridge for a mapper
    /// this machine does not model throws.
    /// </summary>
    /// <exception cref="NesFormatException">The cartridge needs a mapper this machine does not model.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options' sample rate is not above 0, or is above an eighth of the CPU clock.</exception>
    public NesBus(Cartridge cartridge, Region region, NesOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        ArgumentNullException.ThrowIfNull(region);
        Region = region;
        _mapper = cartridge.CreateMapper();
        _ppu = new Ppu(region, _mapper);
        _apu = new Apu(region);
        int sampleRate = (options ?? new NesOptions()).SampleRate;
        _sound = new SampleBuffer(sampleRate, region.CpuHz, Math.Max(1, sampleRate / 4));
        _dmcRepeatsHaltedRead = region.DmcDmaRepeatsHaltedRead;
        _mapperCountsCycles = _mapper.CountsCpuCycles;
        _mapperCanInterrupt = _mapper.CanInterrupt;
        _dotsFrom = new int[region.DotsDenominator];
        _accumulatorAfter = new int[region.DotsDenominator];
        for (int held = 0; held < region.DotsDenominator; held++)
        {
            _dotsFrom[held] = (held + region.DotsNumerator) / region.DotsDenominator;
            _accumulatorAfter[held] = (held + region.DotsNumerator) % region.DotsDenominator;
        }
    }

    /// <summary>The PPU, whose registers sit at <c>$2000</c> to <c>$3FFF</c>.</summary>
    public Ppu Ppu => _ppu;

    /// <summary>The sound unit, whose registers sit at <c>$4000</c> to <c>$4013</c>, <c>$4015</c> and <c>$4017</c>.</summary>
    public Apu Apu => _apu;

    /// <summary>
    /// The sound, as samples at the options' rate: the mixed level goes in once a cycle. It holds a
    /// quarter of a second, and drops the oldest when the reader falls behind.
    /// </summary>
    public SampleBuffer Sound => _sound;

    /// <summary>The region this console is: its dot ratio comes from it.</summary>
    public Region Region { get; }

    /// <summary>Controller <paramref name="pad"/>, 0 or 1: the one the port at <c>$4016</c> or <c>$4017</c> reads.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The pad is neither 0 nor 1.</exception>
    public Controller GetController(int pad)
    {
        if (pad is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pad), pad, "the console has two controller ports, 0 and 1");
        }

        return _controllers[pad];
    }

    /// <summary>The CPU whose NMI and IRQ lines the bus sets at the end of each cycle.</summary>
    public Cpu? Cpu
    {
        get => _cpu;
        set => _cpu = value;
    }

    /// <summary>CPU cycles since power on, DMA stalls included.</summary>
    public long Cycles => _cycles;

    /// <summary>
    /// PPU dots since power on. It is kept by the bus and never reset except by power on, so the
    /// PPU of task 4 can count its own position and this stays the total.
    /// </summary>
    public long PpuDots => _ppuDots;

    /// <summary>
    /// One CPU read: one cycle. If a write to <c>$4014</c> is waiting, or the DMC wants a sample
    /// byte, the DMA runs first, halting the CPU on this read (<see cref="RunDma"/>).
    /// </summary>
    public byte Read(ushort address)
    {
        if (_dmaPage >= 0 || _apu.Dmc.WantsHalt(_cycles + 1))
        {
            RunDma(address);
        }

        return Cycle(false, address, 0);
    }

    /// <summary>One CPU write: one cycle.</summary>
    public void Write(ushort address, byte value)
    {
        Cycle(true, address, value);
    }

    /// <summary>
    /// What a read of <paramref name="address"/> would give, with no cycle and no side effect: no
    /// register is read, the open bus latch is not changed.
    /// </summary>
    public byte Peek(ushort address)
    {
        if (address < 0x2000)
        {
            return _ram[address & 0x7FF];
        }

        if (address < 0x4000)
        {
            return _ppu.PeekRegister(address & 7);
        }

        if (address < 0x4020)
        {
            return address switch
            {
                0x4015 => (byte)(_apu.PeekStatus() | (_openBus & 0x20)),
                0x4016 => (byte)((_openBus & 0xE0) | _controllers[0].Peek()),
                0x4017 => (byte)((_openBus & 0xE0) | _controllers[1].Peek()),
                _ => _openBus,
            };
        }

        return _mapper.CpuRead(address, _openBus);
    }

    /// <summary>Puts a byte in RAM with no cycle, for a test to set up memory.</summary>
    public void PokeRam(ushort address, byte value)
    {
        _ram[address & 0x7FF] = value;
    }

    /// <summary>
    /// The power-on state of the chips: RAM zero (a real console's pattern is undefined, so zeros
    /// are the choice, a known difference), the counters and the open bus at zero, the board's
    /// registers and PRG RAM cleared, the PPU and the sound unit reset.
    /// </summary>
    internal void PowerOn()
    {
        Array.Clear(_ram);
        _mapper.Reset(true);
        _mapper.ClearPrgRam();
        _openBus = 0;
        _dotAccumulator = 0;
        _cycles = 0;
        _ppuDots = 0;
        _controllers[0].PowerOn();
        _controllers[1].PowerOn();
        _dmaPage = -1;
        _lastReadAddress = -1;
        _ppu.PowerOn();
        _apu.PowerOn();
        _sound.Clear();
        if (_cpu is not null)
        {
            _cpu.Nmi = false;
            _cpu.Irq = false;
        }
    }

    /// <summary>
    /// The console's reset button: the PPU and the sound unit reset (the NES-001 resets the PPU with
    /// the CPU, timing.md 4), and RAM, the PRG RAM, the board's registers and the counters are kept
    /// (the boards' chips have no reset line, <see cref="IMapper.Reset"/>).
    /// </summary>
    internal void Reset()
    {
        // A DMA still waiting would run in the reset's first read, after the PPU was reset. The
        // reset button stops the CPU, so the copy that was asked for is dropped.
        _dmaPage = -1;
        _lastReadAddress = -1;
        _mapper.Reset(false);
        _ppu.Reset();
        _apu.Reset();
    }

    /// <summary>
    /// The one cycle. Counts it, notes the interrupt lines the chips hold as it begins, runs two of
    /// the dots the region owes, ticks the sound unit and the board, makes the access, runs the rest
    /// of the dots, then hands the CPU the lines it noted. Returns the byte a read gave, and
    /// <paramref name="value"/> for a write.
    /// </summary>
    private byte Cycle(bool write, ushort address, byte value)
    {
        _cycles++;
        _ppu.CpuCycle = _cycles;

        // The lines as the cycle begins: a change made during this cycle is the CPU's next cycle's.
        bool nmi = _ppu.Nmi;
        bool irq = _apu.Irq || (_mapperCanInterrupt && _mapper.Irq);

        int dots = _dotsFrom[_dotAccumulator];
        _dotAccumulator = _accumulatorAfter[_dotAccumulator];
        int before = Math.Min(DotsBeforeAccess, dots);
        for (int i = 0; i < before; i++)
        {
            _ppu.Tick();
        }

        _apu.Tick();
        _sound.Add(_apu.Output);
        if (_mapperCountsCycles)
        {
            _mapper.CpuCycle();
        }

        if (write)
        {
            WriteAccess(address, value);
            _lastReadAddress = -1;
        }
        else
        {
            value = ReadAccess(address);
            _lastReadAddress = address;
        }

        for (int i = before; i < dots; i++)
        {
            _ppu.Tick();
        }

        _ppuDots += dots;

        if (_cpu is not null)
        {
            _cpu.Nmi = nmi;
            _cpu.Irq = irq;
        }

        return value;
    }

    /// <summary>
    /// The DMA units (bus.md sections 5 and 6), with the CPU halted on its read of
    /// <paramref name="halted"/>, which it makes again once they are done. Every cycle goes
    /// through <see cref="Cycle"/>, so the PPU and the sound unit run through them. The model makes
    /// the even cycles gets and the odd ones puts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>OAM DMA</b>, after a write to <c>$4014</c>: one halt cycle, an alignment cycle if the next
    /// is not a get, then 256 pairs of a read of page <c>N</c> on a get and a write to
    /// <c>$2004</c> on a put, 513 or 514 cycles (task 5 built it on the CPU's next read, which the
    /// sheet's halt is, and task 7 audited it).
    /// </para>
    /// <para>
    /// <b>DMC DMA</b>, from the cycle the DMC asks for: a halt cycle, a dummy cycle, then the read
    /// of the sample byte on the next get, 3 or 4 cycles. Its halt and dummy cycles do nothing on
    /// the bus of their own, so inside OAM DMA they overlap the copy's reads and writes; its read
    /// takes a get from the copy, which then spends a put realigning. So a fetch in the middle of
    /// the copy costs 2, one on its second-to-last put 1 and one on its last put 3, as the DMA page
    /// shows them.
    /// </para>
    /// <para>
    /// <b>A cycle with no transfer</b> (a halt, a dummy or an alignment cycle) repeats the CPU's
    /// halted read on the 2A03, which is how a fetch that lands on a read of <c>$2007</c>,
    /// <c>$4015</c> or a pad reads it again (bus.md 6). The 2A07 "fixes these extra read problems",
    /// by a mechanism the page says is not understood and suspects puts the DMA's own address on
    /// the bus; so on PAL the DMC's cycles with no transfer read the sample address instead, which
    /// has no side effect. OAM DMA alone repeats the halted read on both, as task 7 left it.
    /// </para>
    /// </remarks>
    private void RunDma(ushort halted)
    {
        int page = _dmaPage << 8;
        bool oam = _dmaPage >= 0;
        _dmaPage = -1;

        bool oamHalted = false;
        bool holding = false;
        byte value = 0;
        int index = 0;

        // 0: no fetch; 1: this cycle is its halt; 2: its dummy; 3: waiting for a get to read.
        int dmc = 0;

        while (true)
        {
            long cycle = _cycles + 1;
            bool get = (cycle & 1) == 0;
            if (dmc == 0 && _apu.Dmc.WantsHalt(cycle))
            {
                dmc = 1;
            }

            if (!oam && dmc == 0)
            {
                return;
            }

            if (dmc == 3 && get)
            {
                _apu.Dmc.CompleteFetch(Cycle(false, _apu.Dmc.FetchAddress, 0));
                dmc = 0;
            }
            else if (oam && oamHalted && get && !holding)
            {
                value = Cycle(false, (ushort)(page | index), 0);
                holding = true;
            }
            else if (oam && oamHalted && !get && holding)
            {
                Cycle(true, 0x2004, value);
                holding = false;
                oam = ++index < 256;
            }
            else if (dmc != 0 && !_dmcRepeatsHaltedRead)
            {
                Cycle(false, _apu.Dmc.FetchAddress, 0);
            }
            else
            {
                Cycle(false, halted, 0);
            }

            oamHalted = true;
            if (dmc is 1 or 2)
            {
                dmc++;
            }
        }
    }

    private byte ReadAccess(ushort address)
    {
        byte value;
        if (address < 0x2000)
        {
            value = _ram[address & 0x7FF];
        }
        else if (address < 0x4000)
        {
            value = _ppu.ReadRegister(address & 7);
        }
        else if (address < 0x4020)
        {
            switch (address)
            {
                case 0x4015:
                    // The status read is internal to the CPU: it drives nothing outside, so the
                    // bus latch is not changed, and only bit 5 shows it.
                    return (byte)(_apu.ReadStatus() | (_openBus & 0x20));
                case 0x4016:
                    // Bit 0 is the pad and bits 4 to 1 read 0, which nothing drives; 7 to 5 are open bus.
                    value = (byte)((_openBus & 0xE0) | ReadPad(0, address));
                    break;
                case 0x4017:
                    value = (byte)((_openBus & 0xE0) | ReadPad(1, address));
                    break;
                default:
                    value = _openBus;
                    break;
            }
        }
        else
        {
            value = _mapper.CpuRead(address, _openBus);
        }

        _openBus = value;
        return value;
    }

    // A pad's shift register moves when the read ends, and a run of reads in consecutive cycles of
    // one address is a single read to it, because the line stays low (bus.md 7). That happens when
    // OAM DMA halts the CPU on a read of a pad: the halted cycles repeat the read, the pad sees one
    // clock for the lot, and the CPU's own read afterwards is a second one.
    private byte ReadPad(int pad, ushort address)
    {
        Controller controller = _controllers[pad];
        return _lastReadAddress == address ? controller.Peek() : controller.Read();
    }

    private void WriteAccess(ushort address, byte value)
    {
        _openBus = value;
        if (address < 0x2000)
        {
            _ram[address & 0x7FF] = value;
        }
        else if (address < 0x4000)
        {
            _ppu.WriteRegister(address & 7, value);
        }
        else if (address < 0x4020)
        {
            if (address == 0x4016)
            {
                // Both pads see bit 0 (bus.md section 7).
                _controllers[0].Strobe((value & 1) != 0);
                _controllers[1].Strobe((value & 1) != 0);
            }
            else if (address == 0x4014)
            {
                // OAM DMA starts on the CPU's next read; a second write first, as INC $4014
                // makes, replaces the page (bus.md section 5).
                _dmaPage = value;
            }
            else if (address <= 0x4017)
            {
                _apu.Write(address - 0x4000, value);
            }
        }
        else
        {
            _mapper.CpuWrite(address, value);
        }
    }
}
