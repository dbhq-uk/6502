using Xunit;
using Dbhq.Cpu6502;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Electron.Tests;

public class ElectronTimingTests
{
    // Mode 6 has no contention (ula.md s4c), so Complete's mode makes no difference yet.
    [Theory]
    [InlineData(AccessKind.Rom, 0, 1)]
    [InlineData(AccessKind.Rom, 1, 1)]
    [InlineData(AccessKind.Ram, 0, 2)]
    [InlineData(AccessKind.Ram, 1, 3)] // 2 from a boundary, 3 from one cycle off (ula.md s4b)
    [InlineData(AccessKind.Io, 0, 2)]
    [InlineData(AccessKind.Io, 1, 3)]
    public void AnAccessCostsWhatTheSheetSays(AccessKind kind, long start, long cost) =>
        Assert.Equal(start + cost, ElectronTiming.Complete(start, kind, mode: 6));

    // ula.md s4b: the cost depends only on the parity of the start, so it holds a long way from
    // power on as well.
    [Theory]
    [InlineData(AccessKind.Ram, 1000, 2)]
    [InlineData(AccessKind.Ram, 1001, 3)]
    [InlineData(AccessKind.Rom, 1001, 1)]
    public void TheCostRepeatsWithTheParityOfTheStart(AccessKind kind, long start, long cost) =>
        Assert.Equal(start + cost, ElectronTiming.Complete(start, kind, mode: 6));

    private static readonly byte[] Os = RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256);
    private static readonly byte[] Basic = RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256);

    private static ElectronBus NewBus() => new(new ElectronRoms(Os, Basic));

    /// <summary>Wraps the bus and writes down every access the core makes, in order.</summary>
    private sealed class RecordingBus(ElectronBus inner) : IBus
    {
        public List<(ushort Address, bool IsWrite)> Accesses { get; } = [];

        public byte Read(ushort address)
        {
            Accesses.Add((address, false));
            return inner.Read(address);
        }

        public void Write(ushort address, byte value)
        {
            Accesses.Add((address, true));
            inner.Write(address, value);
        }
    }

    [Fact]
    public void TheAlignmentLoopTakesEighteenCyclesAnIteration()
    {
        // ula.md s11a: SEI, LDA $C000, JMP $2000 makes 8 RAM accesses (2 cycles each) and 1 ROM
        // access (1 cycle), which is 17. 18 is the real-hardware measurement (9 us, ula.md s4g,
        // hoglet on an Issue 2 Electron), not the model's output: the ROM access leaves the next
        // access one cycle off a 1 MHz boundary, which costs 3 instead of 2.
        ElectronBus bus = NewBus();
        byte[] program = [0x78, 0xAD, 0x00, 0xC0, 0x4C, 0x00, 0x20];
        for (int i = 0; i < program.Length; i++)
        {
            bus.PokeRam((ushort)(0x2000 + i), program[i]);
        }

        var cpu = new Cpu(bus, CpuVariant.Nmos6502) { PC = 0x2000 };
        for (int settle = 0; settle < 20; settle++)
        {
            Iterate(cpu);
        }

        for (int i = 0; i < 10; i++)
        {
            long before = bus.Cycles;
            Iterate(cpu);
            Assert.Equal(18, bus.Cycles - before);
        }
    }

    private static void Iterate(Cpu cpu)
    {
        cpu.Step(); // SEI
        cpu.Step(); // LDA $C000
        cpu.Step(); // JMP $2000
    }

    [Fact]
    public void RtsMakesSixAccessesAndTheThirdIsADummyReadOfTheStack()
    {
        // The 6502's documented RTS (6502 programming manuals, and the Visual6502 trace): cycle 1
        // fetches the opcode, 2 reads the next byte and discards it, 3 reads the stack at S
        // without using it, 4 and 5 pull the return address low then high, 6 reads the byte at
        // the pulled address and increments PC. Expected addresses are written out from that, not
        // taken from the model. ula.md s4g: leaving the dummy stack read out ran the BASIC loop
        // 1.4 to 1.7 per cent fast in every mode.
        ElectronBus bus = NewBus();
        bus.PokeRam(0x2000, 0x60); // RTS
        bus.PokeRam(0x01F1, 0x33); // return address minus one, low
        bus.PokeRam(0x01F2, 0x12); // high: $1233
        var recorder = new RecordingBus(bus);
        var cpu = new Cpu(recorder, CpuVariant.Nmos6502) { PC = 0x2000, S = 0xF0 };

        long before = bus.Cycles;
        cpu.Step();

        Assert.Equal(
            new (ushort, bool)[]
            {
                (0x2000, false), (0x2001, false), (0x01F0, false), (0x01F1, false), (0x01F2, false), (0x1233, false),
            },
            recorder.Accesses.ToArray());
        Assert.Equal(0x1234, cpu.PC);
        // All six are RAM, 2 cycles each from a boundary: ula.md s4b.
        Assert.Equal(12, bus.Cycles - before);
    }

    [Fact]
    public void RtiMakesSixAccessesAndTheThirdIsADummyReadOfTheStack()
    {
        // The documented RTI: 1 opcode, 2 the next byte discarded, 3 the stack at S unused, 4 pull
        // P, 5 pull PCL, 6 pull PCH. Written out from the 6502's behaviour, not from the model.
        ElectronBus bus = NewBus();
        bus.PokeRam(0x2000, 0x40); // RTI
        bus.PokeRam(0x01F1, 0x00); // P
        bus.PokeRam(0x01F2, 0x34); // PCL
        bus.PokeRam(0x01F3, 0x12); // PCH: $1234
        var recorder = new RecordingBus(bus);
        var cpu = new Cpu(recorder, CpuVariant.Nmos6502) { PC = 0x2000, S = 0xF0 };

        long before = bus.Cycles;
        cpu.Step();

        Assert.Equal(
            new (ushort, bool)[]
            {
                (0x2000, false), (0x2001, false), (0x01F0, false), (0x01F1, false), (0x01F2, false), (0x01F3, false),
            },
            recorder.Accesses.ToArray());
        Assert.Equal(0x1234, cpu.PC);
        Assert.Equal(12, bus.Cycles - before);
    }
}
