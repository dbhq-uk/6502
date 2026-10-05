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
/// It advances the dot accumulator, ticks the PPU the region's number of dots, ticks the sound
/// unit, makes the access, and then sets the CPU's NMI and IRQ lines from the chips, so the CPU,
/// which samples them in the last part of the cycle, sees what that cycle did. The first version is
/// "all the dots, then the access". Where in the cycle the dots fall relative to the access is task
/// 4's decision (<c>bus.md</c> section 2); it stays in this one method.
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
/// The PPU and the sound unit are stubs in this task, private nested types that tasks 4 and 8
/// replace. The controllers, OAM DMA and DMC DMA come in tasks 6 to 9.
/// </para>
/// </remarks>
public sealed class NesBus : IBus
{
    private readonly byte[] _ram = new byte[0x800];
    private readonly IMapper _mapper;
    private readonly StubPpu _ppu = new();
    private readonly StubApu _apu;
    private Cpu? _cpu;

    // The value last on the CPU's data bus.
    private byte _openBus;

    // The dots owed: grows by the region's numerator each cycle, and a dot runs for each whole
    // denominator it holds.
    private int _dotAccumulator;

    private long _cycles;
    private long _ppuDots;

    // Bit 0 of the last write to $4016, which both pads see. The controllers arrive in task 7.
    private byte _strobe;

    /// <summary>
    /// A bus with the cartridge's board fitted. The board is made here, so a cartridge for a mapper
    /// this machine does not model throws.
    /// </summary>
    /// <exception cref="NesFormatException">The cartridge needs a mapper this machine does not model.</exception>
    public NesBus(Cartridge cartridge, Region region, NesOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        ArgumentNullException.ThrowIfNull(region);
        Region = region;
        _mapper = cartridge.CreateMapper();
        _apu = new StubApu((options ?? new NesOptions()).SampleRate);
    }

    /// <summary>The region this console is: its dot ratio comes from it.</summary>
    public Region Region { get; }

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

    /// <summary>One CPU read: one cycle.</summary>
    public byte Read(ushort address)
    {
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
            return _openBus;
        }

        if (address < 0x4020)
        {
            return address switch
            {
                0x4015 => 0,
                0x4016 or 0x4017 => (byte)(_openBus & 0xE0),
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
    /// are the choice, a known difference), the counters and the open bus at zero, the board's PRG
    /// RAM cleared, the PPU and the sound unit reset.
    /// </summary>
    internal void PowerOn()
    {
        Array.Clear(_ram);
        _mapper.ClearPrgRam();
        _openBus = 0;
        _dotAccumulator = 0;
        _cycles = 0;
        _ppuDots = 0;
        _strobe = 0;
        _ppu.Reset();
        _apu.Reset();
        if (_cpu is not null)
        {
            _cpu.Nmi = false;
            _cpu.Irq = false;
        }
    }

    /// <summary>The console's reset button: the sound unit resets, and RAM, the PRG RAM and the counters are kept.</summary>
    internal void Reset()
    {
        _apu.Reset();
    }

    /// <summary>
    /// The one cycle. Counts it, runs the dots the region owes, ticks the sound unit and the board,
    /// makes the access, then sets the CPU's interrupt lines from what the chips now hold. Returns the
    /// byte a read gave, and <paramref name="value"/> for a write.
    /// </summary>
    private byte Cycle(bool write, ushort address, byte value)
    {
        _cycles++;

        _dotAccumulator += Region.DotsNumerator;
        int dots = _dotAccumulator / Region.DotsDenominator;
        _dotAccumulator %= Region.DotsDenominator;
        for (int i = 0; i < dots; i++)
        {
            _ppu.Tick();
        }

        _ppuDots += dots;
        _apu.Tick();
        _mapper.CpuCycle();

        if (write)
        {
            WriteAccess(address, value);
        }
        else
        {
            value = ReadAccess(address);
        }

        if (_cpu is not null)
        {
            _cpu.Nmi = _ppu.Nmi;
            _cpu.Irq = _apu.Irq || _mapper.Irq;
        }

        return value;
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
            value = _ppu.Read(address & 7, _openBus);
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
                case 0x4017:
                    // Bits 4 to 0 are the pad, which is not connected yet; 7 to 5 are open bus.
                    value = (byte)(_openBus & 0xE0);
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

    private void WriteAccess(ushort address, byte value)
    {
        _openBus = value;
        if (address < 0x2000)
        {
            _ram[address & 0x7FF] = value;
        }
        else if (address < 0x4000)
        {
            _ppu.Write(address & 7, value);
        }
        else if (address < 0x4020)
        {
            if (address == 0x4016)
            {
                // Both pads see bit 0 (bus.md section 7).
                _strobe = (byte)(value & 1);
            }
            else if (address <= 0x4017 && address != 0x4014)
            {
                _apu.Write(address, value);
            }
        }
        else
        {
            _mapper.CpuWrite(address, value);
        }
    }

    // The PPU of task 3: it has no state to run, and its registers are kept for task 4. The bus
    // counts the dots itself (PpuDots), so the count survives the real PPU replacing this.
    private sealed class StubPpu
    {
        private readonly byte[] _registers = new byte[8];

        public bool Nmi => false;

        public void Tick()
        {
        }

        public void Reset()
        {
            Array.Clear(_registers);
        }

        public byte Read(int register, byte openBus)
        {
            return openBus;
        }

        public void Write(int register, byte value)
        {
            _registers[register] = value;
        }
    }

    // The sound unit of task 3: it holds no line and answers nothing. Task 8 replaces it.
    private sealed class StubApu
    {
        public StubApu(int sampleRate)
        {
            SampleRate = sampleRate;
        }

        public int SampleRate { get; }

        public bool Irq => false;

        public void Tick()
        {
        }

        public void Reset()
        {
        }

        public byte ReadStatus()
        {
            return 0;
        }

        public void Write(ushort address, byte value)
        {
        }
    }
}
