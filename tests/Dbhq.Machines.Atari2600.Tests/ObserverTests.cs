using System.Globalization;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The observer the speed pass's differential will rest on (<see cref="IAtari2600Observer"/>): the
/// bus tells it of each point where a chip can be seen from outside, which is each point where a
/// build that advances the chips lazily must have caught them up (spec, "Speed"). Accesses to the
/// TIA and the RIOT's I/O and a board's bank switch are told; RAM and the cartridge's ROM are not.
/// </summary>
public class ObserverTests
{
    /// <summary>Writes down what the bus tells it.</summary>
    internal sealed class Recorder : IAtari2600Observer
    {
        public List<string> Accesses { get; } = [];

        public List<long> Halts { get; } = [];

        public List<long> Frames { get; } = [];

        public void Accessed(ushort address, bool write, byte value) =>
            Accesses.Add(string.Create(CultureInfo.InvariantCulture, $"{(write ? "write" : "read")} ${address:X4}"));

        public void HaltReleased(long cycle) => Halts.Add(cycle);

        public void FrameEnded(long cycle) => Frames.Add(cycle);
    }

    [Fact]
    public void Observation_is_switched_on_for_the_tests()
    {
        // The project file sets the runtime option; without it the observer is never called.
        Assert.True(Atari2600Bus.Observable);
    }

    [Fact]
    public void The_tia_the_riots_io_and_a_bank_switch_are_told_once_each_and_ram_and_rom_are_not()
    {
        // LDA $0280 (SWCHA); STA $1B (GRP0); LDA $80 (RAM); LDA $F800 (ROM); LDA $1FF8 (the hotspot
        // of bank 0, from bank 1, where F8 powers on); LDA $1FF8 again (no switch); then JMP to itself.
        byte[] code =
        [
            0xAD, 0x80, 0x02,
            0x85, 0x1B,
            0xA5, 0x80,
            0xAD, 0x00, 0xF8,
            0xAD, 0xF8, 0x1F,
            0xAD, 0xF8, 0x1F,
            0x4C, 0x10, 0xF0,
        ];
        var machine = new Atari2600(Cartridge.Load(TestPrograms.ImageF8(code)), Region.Ntsc);
        machine.PowerOn();
        Assert.Equal(1, machine.Bus.Board.Bank);

        // Set after power on: the CPU's reset reads $0000 and $0100, which are the TIA's.
        var observer = new Recorder();
        machine.Bus.Observer = observer;

        for (int i = 0; i < 7; i++)
        {
            machine.Step();
        }

        Assert.Equal(0, machine.Bus.Board.Bank);
        Assert.Equal(["read $0280", "write $001B", "read $1FF8"], observer.Accesses);
    }

    [Fact]
    public void The_observer_is_told_the_thirteen_bit_address()
    {
        // LDA $E280: the core puts out $E280; the 6507 has no A13 to A15, so the bus keeps $0280,
        // SWCHA, and that is the address the observer hears (bus.md section 1). The one place the
        // mask shows outside the parts, which decode their own low lines.
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, 0xAD, 0x80, 0xE2, 0x4C, 0x03, 0xF0);
        var observer = new Recorder();
        machine.Bus.Observer = observer;
        machine.Step();
        Assert.Equal(["read $0280"], observer.Accesses);
    }

    [Fact]
    public void A_halt_is_told_as_it_ends_with_the_cycle_count()
    {
        // STA WSYNC; NOP; JMP to the NOP.
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, 0x85, 0x02, 0xEA, 0x4C, 0x02, 0xF0);
        var observer = new Recorder();
        machine.Bus.Observer = observer;
        machine.Step();
        machine.Step();

        // Power on's seven cycles and the store's three put the write on cycle 10; the halt ends
        // after cycle 76, before the NOP's fetch.
        Assert.Equal([76L], observer.Halts);
        Assert.Equal(78, machine.Bus.Cycles);
    }

    [Fact]
    public void A_frame_end_is_told_at_the_end_of_vsync_and_at_the_line_limit()
    {
        // LDA #2; STA VSYNC; STA WSYNC; LDA #0; STA VSYNC; then STA WSYNC and JMP back to it.
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, 0xA9, 0x02, 0x85, 0x00, 0x85, 0x02, 0xA9, 0x00, 0x85, 0x00, 0x85, 0x02, 0x4C, 0x0A, 0xF0);
        var observer = new Recorder();
        machine.Bus.Observer = observer;
        for (int i = 0; i < 5; i++)
        {
            machine.Step();
        }

        Assert.Equal([machine.Bus.Cycles], observer.Frames);
        Assert.Equal(1, machine.Bus.Tia.Frame.Frames);

        machine.RunFrames(1);
        Assert.Equal(2, observer.Frames.Count);
        Assert.Equal(Tia.MaxLines, machine.Bus.Tia.Frame.Lines);

        // The limit is reached as the counter wraps from colour clock 227 to 0, in the last of the
        // three clocks of a line's cycle 76; from power on, cycle c runs clocks 3c - 3 to 3c - 1.
        long cycle = observer.Frames[1];
        Assert.Equal(0, cycle * 3 % 228);
    }

    [Fact]
    public void With_no_observer_set_nothing_is_told_and_nothing_changes()
    {
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, 0x85, 0x02, 0xAD, 0x80, 0x02, 0x4C, 0x00, 0xF0);
        var observer = new Recorder();
        machine.Bus.Observer = observer;
        machine.Bus.Observer = null;
        machine.Run(10_000);
        Assert.Empty(observer.Accesses);
        Assert.Empty(observer.Halts);
    }
}
