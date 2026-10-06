using Dbhq.Cpu6502;

namespace Dbhq.Machines.Kim1;

/// <summary>
/// The KIM-1's address decoding, and its clock: each read or write is one
/// cycle, and both 6530s are clocked at the start of it.
/// </summary>
/// <remarks>
/// <para>
/// A 74145 decodes A10 to A12 into eight 1 KB selects, K0 to K7. Its fourth
/// input, DECODE ENABLE, is grounded on a board with no expansion, so A13 to
/// A15 are ignored and the 8 KB repeats through all 64 KB. That is how the
/// CPU finds its vectors at $FFFA: they are the last six bytes of the
/// 6530-002's ROM. (KIM-1 User Manual, section 6.1.)
/// </para>
/// <list type="table">
/// <item><term>K0, $0000-$03FF</term><description>1 KB of static RAM (eight 6102s)</description></item>
/// <item><term>K1 to K4, $0400-$13FF</term><description>nothing: brought out for expansion</description></item>
/// <item><term>K5, $1400-$16FF</term><description>nothing: no 6530 answers here</description></item>
/// <item><term>K5, $1700-$173F</term><description>6530-003 ports and timer, 16 registers repeated four times</description></item>
/// <item><term>K5, $1740-$177F</term><description>6530-002 ports and timer, likewise</description></item>
/// <item><term>K5, $1780-$17BF</term><description>6530-003 RAM</description></item>
/// <item><term>K5, $17C0-$17FF</term><description>6530-002 RAM</description></item>
/// <item><term>K6, $1800-$1BFF</term><description>6530-003 ROM</description></item>
/// <item><term>K7, $1C00-$1FFF</term><description>6530-002 ROM</description></item>
/// </list>
/// <para>
/// Inside K5 the 6530s decode A6 to A9 themselves, from their mask options:
/// RAM where A9 to A7 are all high, ports and timer where A9 and A8 are high
/// and A7 low, and A6 tells the two chips apart.
/// </para>
/// </remarks>
public sealed class Kim1Bus : Bus
{
    private readonly byte[] _ram = new byte[0x400];
    private byte _dataBus;
    private bool _syncNext;

    public Kim1Bus(ReadOnlySpan<byte> rom002, ReadOnlySpan<byte> rom003)
    {
        U2 = new Rriot6530(rom002);
        U3 = new Rriot6530(rom003);
    }

    /// <summary>The 6530-002 (U2): the monitor's half of the ROM, and the keypad, display and teletype lines.</summary>
    public Rriot6530 U2 { get; }

    /// <summary>The 6530-003 (U3): the cassette half of the ROM, and the user's ports and timer.</summary>
    public Rriot6530 U3 { get; }

    public Kim1Keypad Keypad { get; } = new();

    public Kim1Display Display { get; } = new();

    /// <summary>Bus cycles since power on.</summary>
    public long Cycles { get; private set; }

    /// <summary>The CPU whose NMI line the ST key and the single-step logic drive.</summary>
    public Cpu? Cpu { get; set; }

    /// <summary>The SST slide switch.</summary>
    public bool SingleStep { get; set; }

    /// <summary>The ST key, held down.</summary>
    public bool StopHeld { get; set; }

    /// <summary>
    /// The output of the 74145 that PB1 to PB4 of the 6530-002 select: 0 to 2
    /// are the key rows, 4 to 9 the digits, 3 the teletype jumper, and 10 to
    /// 15 select nothing.
    /// </summary>
    public int DecoderOutput => (U2.PortBPins >> 1) & 0x0F;

    /// <summary>
    /// Marks the next access as an opcode fetch, when the CPU's SYNC line is
    /// high. The machine calls it before each instruction.
    /// </summary>
    public void BeginInstruction() => _syncNext = true;

    public override byte Read(ushort address)
    {
        BeginCycle(address);
        int a = address & 0x1FFF;
        byte value = (a >> 10) switch
        {
            0 => _ram[a & 0x3FF],
            5 => ReadK5(a),
            6 => U3.ReadRom(a),
            7 => U2.ReadRom(a),
            _ => _dataBus,
        };
        _dataBus = value;
        return value;
    }

    public override void Write(ushort address, byte value)
    {
        BeginCycle(address);
        _dataBus = value;
        int a = address & 0x1FFF;
        switch (a >> 10)
        {
            case 0:
                _ram[a & 0x3FF] = value;
                break;
            case 5:
                WriteK5(a, value);
                break;
        }
    }

    /// <summary>Reads memory without a bus cycle: for tests and debuggers, never for the CPU.</summary>
    public byte Peek(ushort address)
    {
        int a = address & 0x1FFF;
        return (a >> 10) switch
        {
            0 => _ram[a & 0x3FF],
            5 when (a & 0x380) == 0x380 => Chip(a).ReadRam(a),
            6 => U3.ReadRom(a),
            7 => U2.ReadRom(a),
            _ => 0,
        };
    }

    /// <summary>
    /// The RS key also drives the 6530s' RES line, which turns every port pin
    /// into an input. The display sees that as all digits deselected.
    /// </summary>
    public void ResetChips()
    {
        U2.Reset();
        U3.Reset();
        ObserveDisplay();
    }

    private void BeginCycle(ushort address)
    {
        Cycles++;
        U2.Tick();
        U3.Tick();
        bool sync = _syncNext;
        _syncNext = false;
        if (Cpu is not null)
        {
            // SST: an NMI on each opcode fetch outside K7, so the monitor's
            // own code in $1C00-$1FFF is never stepped. The CPU latches the
            // edge and takes the NMI when the instruction ends.
            Cpu.Nmi = StopHeld || (sync && SingleStep && (address & 0x1C00) != 0x1C00);
        }
    }

    private Rriot6530 Chip(int a) => (a & 0x40) != 0 ? U2 : U3;

    private byte ReadK5(int a)
    {
        if ((a & 0x300) != 0x300)
        {
            return _dataBus;
        }

        Rriot6530 chip = Chip(a);
        if ((a & 0x80) != 0)
        {
            return chip.ReadRam(a);
        }

        if (chip == U2)
        {
            U2.PortAInput = Keypad.Columns(DecoderOutput);
        }

        return chip.ReadRegister(a & 0x0F);
    }

    private void WriteK5(int a, byte value)
    {
        if ((a & 0x300) != 0x300)
        {
            return;
        }

        Rriot6530 chip = Chip(a);
        if ((a & 0x80) != 0)
        {
            chip.WriteRam(a, value);
            return;
        }

        chip.WriteRegister(a & 0x0F, value);
        if (chip == U2)
        {
            ObserveDisplay();
        }
    }

    private void ObserveDisplay()
    {
        int output = DecoderOutput;
        U2.PortAInput = Keypad.Columns(output);
        Display.Observe(Cycles, output, U2.PortAPins);
    }
}
