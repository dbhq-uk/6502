using Dbhq.Cpu6502;

namespace Dbhq.Machines.Electron;

/// <summary>
/// The Electron's address decoding, its paged ROM select and its clock: each read or write is
/// one bus cycle, and its cost is set by the address on the bus in that cycle (fact sheet
/// <c>ula.md</c> sections 1 to 4).
/// </summary>
/// <remarks>
/// <para>
/// The memory map (s1a):
/// </para>
/// <list type="table">
/// <item><term>$0000-$7FFF</term><description>32 KB of RAM, with no aliasing</description></item>
/// <item><term>$8000-$BFFF</term><description>the paged ROM that $FE05 chose: BASIC in slots 10 and 11, the keyboard in 8 and 9, nothing in the rest; writes are ignored</description></item>
/// <item><term>$C000-$FBFF</term><description>the operating system ROM</description></item>
/// <item><term>$FC00-$FDFF</term><description>FRED and JIM, with nothing fitted</description></item>
/// <item><term>$FE00-$FEFF</term><description>the ULA, in every 16-byte block</description></item>
/// <item><term>$FF00-$FFFF</term><description>the operating system ROM again, with the vectors</description></item>
/// </list>
/// <para>
/// The cost of a cycle (s4b) is <see cref="ElectronTiming.Complete"/>: a ROM access is one
/// cycle, and a RAM or I/O access is two from a 1 MHz boundary and three from one cycle off.
/// Each cycle is judged by its own address, so the core's dummy reads and the first write of a
/// read-modify-write are charged like any other access. The wait comes first and the access
/// happens at the end of it, as on the BBC's bus. <see cref="Cycles"/> moves in one place,
/// <c>Advance</c>, which calls <see cref="Tick"/> once for every cycle that passes.
/// </para>
/// <para>
/// The chips join in the decode, in <c>ReadSheila</c> and <c>WriteSheila</c> for the ULA and in
/// <c>ReadPaged</c> for the keyboard: each later task adds a case there.
/// </para>
/// </remarks>
public class ElectronBus : IBus
{
    private const int Slots = 16;
    private const int KeyboardSlotLow = 8;
    private const int BasicSlotHigh = 11;

    private readonly byte[] _ram = new byte[0x8000];
    private readonly byte[] _os;
    private readonly byte[] _basic;
    private readonly bool[] _basicIn = new bool[Slots];
    private readonly Ula _ula = new();
    private long _cycles;

    public ElectronBus(ElectronRoms roms, int sampleRate = 44100)
        : this(roms, sampleRate, [10, BasicSlotHigh])
    {
    }

    /// <summary>
    /// A bus with BASIC in the given slots and no other: for the boot tests, which take it out or
    /// leave it in one slot (s2c). Slots 8 and 9 are the keyboard, so a slot there is refused.
    /// </summary>
    internal ElectronBus(ElectronRoms roms, int sampleRate, IEnumerable<int> basicSlots)
    {
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(basicSlots);
        _os = roms.Os;
        _basic = roms.Basic;
        foreach (int slot in basicSlots)
        {
            if (slot is < 0 or >= Slots or KeyboardSlotLow or KeyboardSlotLow + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basicSlots), slot, "BASIC goes in a slot from 0 to 15 other than 8 and 9, which are the keyboard.");
            }

            _basicIn[slot] = true;
        }

        // The sample rate is for the sound task, which builds the sound buffer here.
        _ = sampleRate;
    }

    /// <summary>2 MHz cycles since power on, wait cycles included.</summary>
    public long Cycles => _cycles;

    /// <summary>The ULA: its interrupt registers and the frame that times them.</summary>
    public Ula Ula => _ula;

    /// <summary>The keyboard matrix, which slots 8 and 9 read (s7).</summary>
    public ElectronKeyboard Keyboard { get; } = new();

    /// <summary>
    /// The CPU's IRQ line: the ULA's, made current for this cycle first. The machine copies it
    /// into the core after each step.
    /// </summary>
    public bool Irq
    {
        get
        {
            CatchUpUla();
            return _ula.Irq;
        }
    }

    /// <summary>
    /// The paged ROM select, 0 to 15. Slot 0 at power on: that is not established (s12 item 4),
    /// and the OS writes $F8 first, which selects slot 8 from any slot.
    /// </summary>
    public int RomSlot { get; private set; }

    public byte Read(ushort address)
    {
        Advance(ElectronTiming.Complete(_cycles, Classify(address), _ula.Mode));
        CatchUpUla();
        return DecodeRead(address);
    }

    public void Write(ushort address, byte value)
    {
        Advance(ElectronTiming.Complete(_cycles, Classify(address), _ula.Mode));
        CatchUpUla();
        if (address < 0x8000)
        {
            _ram[address] = value;
        }
        else if (address is >= 0xFC00 and < 0xFF00)
        {
            if (address >= 0xFE00)
            {
                WriteSheila(address & 0x0F, value);
            }
        }

        // $8000-$FBFF and $FF00-$FFFF are ROM, and FRED and JIM have nothing fitted:
        // a write goes nowhere.
    }

    /// <summary>Reads memory without a bus cycle: for tests and debuggers, never for the CPU.</summary>
    public byte Peek(ushort address) => Decode(address, peek: true);

    /// <summary>Loads RAM without a bus cycle: for tests.</summary>
    public void PokeRam(ushort address, byte value)
    {
        if (address >= _ram.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "RAM ends at $7FFF.");
        }

        _ram[address] = value;
    }

    /// <summary>
    /// One cycle for the parts that run on the cycle: called once for each cycle that elapses,
    /// after <see cref="Cycles"/> has moved on. Empty until the chips that need it arrive.
    /// </summary>
    protected internal virtual void Tick()
    {
    }

    /// <summary>What kind of access the address on the bus is now, which sets its cost (s3a).</summary>
    private AccessKind Classify(ushort address)
    {
        if (address < 0x8000)
        {
            return AccessKind.Ram;
        }

        if (address < 0xC000)
        {
            // Slots 8 and 9 are the keyboard, which the real ULA reads at 1 MHz (s3a, from source
            // S8 there). Every other slot, empty or BASIC, is a 2 MHz ROM position.
            return RomSlot is KeyboardSlotLow or KeyboardSlotLow + 1 ? AccessKind.Io : AccessKind.Rom;
        }

        // $FC00-$FEFF is I/O whatever the OS ROM image holds there. FRED, JIM and the ULA are
        // judged as 1 MHz accesses (s3a, s12 item 2).
        return address is >= 0xFC00 and < 0xFF00 ? AccessKind.Io : AccessKind.Rom;
    }

    private byte DecodeRead(ushort address) => Decode(address, peek: false);

    /// <summary>The byte at the address. A read of the status register clears the power-on flag; a peek does not.</summary>
    private byte Decode(ushort address, bool peek)
    {
        if (address < 0x8000)
        {
            return _ram[address];
        }

        if (address < 0xC000)
        {
            return ReadPaged(address);
        }

        if (address is >= 0xFC00 and < 0xFF00)
        {
            // FRED and JIM have nothing fitted. The sheet recommends the high byte of the address
            // (s12 item 3); the OS does not depend on it (s1b).
            return address < 0xFE00 ? (byte)(address >> 8) : ReadSheila(address, peek);
        }

        return _os[address - 0xC000];
    }

    private byte ReadPaged(ushort address)
    {
        int slot = RomSlot;
        if (slot is KeyboardSlotLow or KeyboardSlotLow + 1)
        {
            // One device in both slots (s2b): the low 14 address bits pick the columns (s7a).
            return Keyboard.Read(address);
        }

        if (_basicIn[slot])
        {
            return _basic[address - 0x8000];
        }

        // An empty slot reads the high byte of the address (s12 item 3: not measured; the boot
        // does not depend on it).
        return (byte)(address >> 8);
    }

    /// <summary>
    /// A read of the ULA. The registers are the same in every 16-byte block, so the register is
    /// the low four bits of the address. $FE00 is the status; task 10 adds $FE04, the cassette.
    /// Any other register cannot be read, and the bus returns the high byte of the address, $FE
    /// (s1b, s12 item 3: what the real bus returns is not settled).
    /// </summary>
    private byte ReadSheila(ushort address, bool peek)
    {
        int register = address & 0x0F;
        if (register == 0)
        {
            return peek ? _ula.Status : _ula.Read(0);
        }

        return (byte)(address >> 8);
    }

    /// <summary>A write to the ULA; the offset is the low four bits of the address.</summary>
    private void WriteSheila(int offset, byte value)
    {
        switch (offset)
        {
            case 0x5:
                // One write does both jobs: bits 3 to 0 select the paged ROM, here, and bits 7 to 4
                // are the ULA's interrupt clears (s2a, s6b). The select's acceptance rule stays in
                // the bus.
                SelectRom(value);
                _ula.Write(offset, value);
                break;
            default:
                _ula.Write(offset, value);
                break;
        }
    }

    /// <summary>
    /// The acceptance rule of s2a. With slot 8 to 11 paged in a write is honoured only if its bit
    /// 3 is set, so an interrupt clear ($10, $20, $40) cannot drop BASIC to slot 0. From any other
    /// slot every write is honoured, including that one. A write of $0C to $0F therefore selects
    /// slot 12 to 15 and leaves the register able to take any slot on the next write.
    /// </summary>
    private void SelectRom(byte value)
    {
        bool pagedIn8To11 = RomSlot is >= KeyboardSlotLow and <= BasicSlotHigh;
        if (!pagedIn8To11 || (value & 0x08) != 0)
        {
            RomSlot = value & (Slots - 1);
        }
    }

    /// <summary>One comparison per access: the ULA does its work only when its next event has come.</summary>
    private void CatchUpUla()
    {
        if (_cycles >= _ula.NextEvent)
        {
            _ula.CatchUp(_cycles);
        }
    }

    /// <summary>Moves the clock to <paramref name="to"/>, one cycle at a time (constraint 1).</summary>
    private void Advance(long to)
    {
        while (_cycles < to)
        {
            _cycles++;
            Tick();
        }
    }
}
