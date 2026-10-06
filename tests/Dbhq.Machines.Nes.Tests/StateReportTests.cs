using Dbhq.Machines.Nes.Mappers;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The state reports and the observer that the differential (<c>bench/nes-speed/differential</c>)
/// rests on, which is the gate for the lazy chips (<c>docs/superpowers/plans/2026-10-06-nes-lazy-chips.md</c>):
/// every field of every chip is reported or skipped with its reason, and the bus tells the
/// observer of each point where a chip can be seen.
/// </summary>
public class StateReportTests
{
    [Theory]
    [MemberData(nameof(Boards))]
    public void EveryFieldOfEveryPartIsReportedOrSkipped(int mapper, bool chrRam)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Banked(mapper, prgBanks: 2, chrBanks: chrRam ? 0 : 2)));
        nes.PowerOn();

        var (problems, checkedTypes) = StateCompleteness.Check(nes.Bus);

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        foreach (Type part in new[] { typeof(NesBus), typeof(Ppu), typeof(FrameBuffer), typeof(Apu), typeof(PulseChannel), typeof(TriangleChannel), typeof(NoiseChannel), typeof(DmcChannel), typeof(LengthCounter), typeof(Envelope), typeof(SampleBuffer), typeof(Controller) })
        {
            Assert.Contains(part, checkedTypes);
        }

        Assert.Contains(BoardType(mapper), checkedTypes);
    }

    public static TheoryData<int, bool> Boards()
    {
        var data = new TheoryData<int, bool>();
        foreach (int mapper in Cartridge.SupportedMappers)
        {
            data.Add(mapper, false);
            data.Add(mapper, true);
        }

        return data;
    }

    [Fact]
    public void ObservationIsSwitchedOnForTheTests()
    {
        // The project file sets the runtime option; without it the observer is never called.
        Assert.True(NesBus.Observable);
    }

    [Fact]
    public void TheObserverIsToldOfRegisterAccessesBoardWritesFrameEndsAndEveryCycle()
    {
        // LDA $2002, STA $4000, STA $8000, BIT $4016, then JMP to itself.
        byte[] program = [0xAD, 0x02, 0x20, 0x8D, 0x00, 0x40, 0x8D, 0x00, 0x80, 0x2C, 0x16, 0x40, 0x4C, 0x0C, 0x80];
        var nes = Machine(program);
        var observer = new Recorder();
        nes.Bus.Observer = observer;
        nes.PowerOn();
        Assert.Equal([true], observer.Resets);
        Assert.Equal(nes.Bus.Cycles, observer.Cycles);
        for (int i = 0; i < 4; i++)
        {
            nes.Step();
        }

        Assert.Equal(nes.Bus.Cycles, observer.Cycles);
        Assert.Equal(new[] { "read $2002", "write $4000", "write $8000", "read $4016" }, observer.Accesses);
        Assert.Equal(0, observer.Frames);
        nes.RunFrames(2);
        Assert.Equal(2, observer.Frames);
        Assert.Equal(nes.Bus.Cycles, observer.Cycles);
        nes.Reset();
        Assert.Equal([true, false], observer.Resets);
    }

    [Fact]
    public void TheObserverIsToldOfEachDmcFetch()
    {
        // A one-byte sample from $C000: $4012 = 0, $4013 = 0, then $4015 = $10 starts it.
        byte[] program = [0xA9, 0x00, 0x8D, 0x12, 0x40, 0x8D, 0x13, 0x40, 0xA9, 0x10, 0x8D, 0x15, 0x40, 0x4C, 0x0D, 0x80];
        var nes = Machine(program);
        var observer = new Recorder();
        nes.Bus.Observer = observer;
        nes.PowerOn();
        nes.Run(200);

        Assert.Equal(1, observer.DmcFetches);
    }

    [Fact]
    public void WithNoObserverNothingIsCalled()
    {
        var nes = Machine([0x4C, 0x00, 0x80]);
        Assert.Null(nes.Bus.Observer);
        nes.PowerOn();
        nes.RunFrames(1);
        Assert.Null(nes.Bus.Ppu.Observer);
    }

    // NROM with the program at $8000 and the reset vector pointing at it.
    private static Nes Machine(byte[] program)
    {
        byte[] file = TestCartridge.Ines1(prgBanks: 2, chrBanks: 1);
        program.CopyTo(file, 16);
        file[16 + 0x7FFC] = 0x00;
        file[16 + 0x7FFD] = 0x80;
        return new Nes(Cartridge.Load(file));
    }

    private static Type BoardType(int mapper) => mapper switch
    {
        0 => typeof(Nrom),
        1 => typeof(Mmc1),
        2 => typeof(Uxrom),
        3 => typeof(Cnrom),
        4 => typeof(Mmc3),
        _ => typeof(Axrom),
    };

    private sealed class Recorder : INesObserver
    {
        public List<string> Accesses { get; } = [];

        public long Cycles { get; private set; }

        public int Frames { get; private set; }

        public int DmcFetches { get; private set; }

        public void Accessed(ushort address, bool write, byte value)
        {
            if (Frames == 0)
            {
                Accesses.Add($"{(write ? "write" : "read")} ${address:X4}");
            }
        }

        public void DmcFetched() => DmcFetches++;

        public void FrameEnded() => Frames++;

        public void CycleEnded(bool nmi, bool irq) => Cycles++;

        public List<bool> Resets { get; } = [];

        public void ChipsReset(bool power) => Resets.Add(power);
    }
}
