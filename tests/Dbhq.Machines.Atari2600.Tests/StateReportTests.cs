using System.Security.Cryptography;
using Dbhq.Machines.Atari2600.Boards;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The state reports the speed pass's differential will hash (spec, "Speed"; issue #130): every
/// field of the bus, the TIA, the RIOT and every board is reported or skipped with its reason,
/// found by reflection, so a field added later without a report fails here. And the hooks change
/// nothing: a run with the observer set, or with the per-colour-clock reference, ends in the same
/// state at the same cycle as a run without.
/// </summary>
public class StateReportTests
{
    public static TheoryData<int, bool, string> Boards() => new()
    {
        { 2048, false, "2K" },
        { 4096, false, "4K" },
        { 8192, false, "F8" },
        { 8192, true, "F8 with the Superchip" },
        { 12288, false, "FA" },
        { 16384, false, "F6" },
        { 16384, true, "F6 with the Superchip" },
        { 32768, false, "F4" },
        { 32768, true, "F4 with the Superchip" },
    };

    [Theory]
    [MemberData(nameof(Boards))]
    public void Every_field_of_every_part_is_reported_or_skipped(int size, bool filler, string boardName)
    {
        var cartridge = Cartridge.Load(TestImages.BankNumbers(size, filler));
        Assert.Equal(boardName, cartridge.BoardName);
        var machine = new Atari2600(cartridge);
        machine.PowerOn();

        var (problems, checkedTypes) = StateCompleteness.Check(machine.Bus);

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        foreach (Type part in new[] { typeof(Atari2600Bus), typeof(Tia), typeof(TiaPlayer), typeof(TiaMissile), typeof(TiaBall), typeof(TiaPositionCounter), typeof(TiaMotion), typeof(Riot6532), machine.Bus.Board.GetType() })
        {
            Assert.Contains(part, checkedTypes);
        }

        if (machine.Bus.Board is Superchip)
        {
            Assert.Contains(checkedTypes, type => type.IsSubclassOf(typeof(BankedBoard)));
        }
    }

    [Fact]
    public void A_report_changes_when_the_state_does()
    {
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, 0x85, 0x02, 0x4C, 0x00, 0xF0);
        string before = Hash(machine.Bus);
        Assert.Equal(before, Hash(machine.Bus));
        machine.Step();
        Assert.NotEqual(before, Hash(machine.Bus));

        string now = Hash(machine.Bus);
        machine.Bus.Region = Region.Pal;
        Assert.NotEqual(now, Hash(machine.Bus));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void The_hooks_change_no_value_and_no_cycle(string regionName)
    {
        // The suite's riot/timer, which uses WSYNC, the timer, the TIA's mirror (its CLEAN_START)
        // and a frame loop: a run of each kind for the same cycles must end in the same state.
        Region region = TestPrograms.RegionNamed(regionName);
        byte[] rom = SuiteRunner.Rom("riot/timer", region);

        string plain = Run(rom, region, observe: false, reference: false);
        Assert.Equal(plain, Run(rom, region, observe: true, reference: false));

        // Vacuous until the speed pass: every build is the per-colour-clock reference now, so the
        // option has nothing to change yet. It holds the place of the gate the pass will need.
        Assert.Equal(plain, Run(rom, region, observe: false, reference: true));
    }

    private static string Run(byte[] rom, Region region, bool observe, bool reference)
    {
        var machine = new Atari2600(Cartridge.Load(rom), region, new Atari2600Options { PerColourClockReference = reference });
        if (observe)
        {
            machine.Bus.Observer = new ObserverTests.Recorder();
        }

        machine.PowerOn();
        machine.Run(200_000);
        return $"{machine.Bus.Cycles} {machine.Cpu.PC:X4} {machine.Cpu.A:X2} {machine.Cpu.X:X2} {machine.Cpu.Y:X2} {machine.Cpu.S:X2} {machine.Cpu.P:X2} {Hash(machine.Bus)}";
    }

    /// <summary>A SHA-256 of every value <paramref name="root"/> and its parts report, with their names.</summary>
    internal static string Hash(IReportsState root)
    {
        var sink = new HashSink();
        root.Report(sink);
        return sink.Finish();
    }

    // Hashes every value a report gives, with its name, parts included.
    private sealed class HashSink : IStateSink
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public void Add(string name, long value) => Bytes(name, BitConverter.GetBytes(value));

        public void Add(string name, ulong value) => Bytes(name, BitConverter.GetBytes(value));

        public void Add(string name, bool value) => Bytes(name, [value ? (byte)1 : (byte)0]);

        public void Add(string name, double value) => Bytes(name, BitConverter.GetBytes(value));

        public void Add(string name, ReadOnlySpan<byte> values) => Bytes(name, values);

        public void Add(string name, ReadOnlySpan<int> values) => Bytes(name, System.Runtime.InteropServices.MemoryMarshal.AsBytes(values));

        public void Add(string name, ReadOnlySpan<uint> values) => Bytes(name, System.Runtime.InteropServices.MemoryMarshal.AsBytes(values));

        public void Add(string name, ReadOnlySpan<long> values) => Bytes(name, System.Runtime.InteropServices.MemoryMarshal.AsBytes(values));

        public void Add(string name, ReadOnlySpan<float> values) => Bytes(name, System.Runtime.InteropServices.MemoryMarshal.AsBytes(values));

        public void Add(string name, ReadOnlySpan<double> values) => Bytes(name, System.Runtime.InteropServices.MemoryMarshal.AsBytes(values));

        public void Add(string name, IReportsState part)
        {
            Bytes(name, []);
            part.Report(this);
        }

        public void Skip(string name, string why)
        {
        }

        public string Finish() => Convert.ToHexStringLower(_hash.GetHashAndReset());

        private void Bytes(string name, ReadOnlySpan<byte> values)
        {
            _hash.AppendData(System.Text.Encoding.UTF8.GetBytes(name));
            _hash.AppendData(BitConverter.GetBytes(values.Length));
            _hash.AppendData(values);
        }
    }
}
