using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// What the page's browser check relies on, run headless as <c>Atari2600Host</c> runs it
/// (src/Dbhq.Machines.Atari2600.Wasm): the page builds the machine in the region its control
/// shows, the visitor may switch the region and play, and the check's <c>stepTo(n)</c> then lets
/// go of every input, keeps the switches that stay, powers the machine off and on and runs exactly
/// n frames (site/public/atari-2600.js). The frame it leaves must be the one the boot check
/// recorded, because the bundled game's title changes every frame and the browser check compares
/// the canvas with <c>machines/atari-2600/expected-frames.json</c> at exactly that count.
/// </summary>
public sealed class PagePathTests
{
    public static TheoryData<string> Regions => new() { "NTSC", "PAL" };

    private static Region Named(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    [Theory]
    [MemberData(nameof(Regions))]
    public void A_machine_switched_to_a_region_played_then_powered_off_and_on_gives_the_recorded_frame(string name)
    {
        Region region = Named(name);
        Region other = region == Region.Pal ? Region.Ntsc : Region.Pal;

        // Built in the other region, played with a stick held and the switches every way, then switched.
        var machine = new Atari2600(Cartridge.Load(BootCheck.Rom()), other);
        machine.PowerOn();
        machine.SetJoystick(0, Joystick.Left | Joystick.Fire);
        machine.SetSwitches(ConsoleSwitches.Reset | ConsoleSwitches.Select | ConsoleSwitches.LeftDifficultyA);
        machine.RunFrames(20);
        machine.Bus.Region = region;
        machine.RunFrames(5);

        // stepTo: every input let go, the recorded switches, power off and on, exactly the count.
        machine.SetJoystick(0, Joystick.None);
        machine.SetJoystick(1, Joystick.None);
        machine.SetSwitches(BootCheck.Switches(region));
        machine.PowerOn();
        machine.RunFrames(BootCheck.Frames(region));

        // The picture as Atari2600Host.Picture hands it to the page: the region's window through
        // ToRgba, and its bytes as they lie in memory, which is what the canvas is given. The host
        // itself runs only in a browser (its JSImport), so its two lines are repeated here.
        uint[] window = new uint[FrameBuffer.Width * region.ShownLines];
        machine.Bus.Tia.Frame.ToRgba(region, window);
        byte[] canvas = MemoryMarshal.AsBytes(window.AsSpan()).ToArray();
        Assert.Equal(BootCheck.RgbaBytes(window), canvas);

        // Compared with the committed file, not with another run, so a fault that changes every run
        // alike (the picture, the power on) fails here as it does in the browser.
        BootCheck.AssertHashesAreTheRecorded(
            region,
            Convert.ToHexStringLower(SHA256.HashData(machine.Bus.Tia.Frame.ColourNumbers)),
            Convert.ToHexStringLower(SHA256.HashData(canvas)));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Power_off_and_on_after_play_leaves_the_machine_as_a_fresh_one_powered_on(string name)
    {
        // The bundled game starts the same from a CPU that was not reset or chips that were not
        // powered on (its BRK vector is its start, and it clears the chips itself), so its frame
        // cannot tell (task 13's review: those two faults passed the frame). The page's power off
        // and on must still be a fresh power on's, so the whole state is compared, CPU and every chip.
        Region region = Named(name);
        var played = new Atari2600(Cartridge.Load(BootCheck.Rom()), region);
        played.PowerOn();
        played.SetJoystick(0, Joystick.Left | Joystick.Fire);
        played.SetSwitches(ConsoleSwitches.Reset | ConsoleSwitches.Select);
        played.RunFrames(20);
        played.SetJoystick(0, Joystick.None);
        played.SetSwitches(BootCheck.Switches(region));
        played.PowerOn();

        var fresh = new Atari2600(Cartridge.Load(BootCheck.Rom()), region);
        fresh.SetSwitches(BootCheck.Switches(region));
        fresh.PowerOn();

        Assert.Equal(State(fresh), State(played));

        // And a fresh power on runs the CPU's reset, so the CPU is at the
        // cartridge's reset vector ($1FFC, the last four bytes but two of the image) with three
        // pushes' worth taken off the stack pointer and interrupts masked.
        byte[] rom = BootCheck.Rom();
        ushort vector = (ushort)(rom[^4] | (rom[^3] << 8));
        Assert.Equal(vector, played.Cpu.PC);
        Assert.Equal(0xFD, played.Cpu.S);
        Assert.Equal(0x04, played.Cpu.P & 0x04);
    }

    private static string State(Atari2600 machine) =>
        $"{machine.Cpu.PC:X4} {machine.Cpu.A:X2} {machine.Cpu.X:X2} {machine.Cpu.Y:X2} {machine.Cpu.S:X2} {machine.Cpu.P:X2} {StateReportTests.Hash(machine.Bus)}";

    [Theory]
    [MemberData(nameof(Regions))]
    public void One_frame_short_or_over_is_not_the_recorded_frame(string name)
    {
        // The title's colours change every frame, so near the count is not the count.
        Region region = Named(name);
        var machine = new Atari2600(Cartridge.Load(BootCheck.Rom()), region);
        machine.SetSwitches(BootCheck.Switches(region));
        machine.PowerOn();
        machine.RunFrames(BootCheck.Frames(region) - 1);
        string before = Convert.ToHexStringLower(SHA256.HashData(machine.Bus.Tia.Frame.ColourNumbers));
        machine.RunFrames(2);
        string after = Convert.ToHexStringLower(SHA256.HashData(machine.Bus.Tia.Frame.ColourNumbers));

        string recorded = BootCheck.RecordedColours(region);
        Assert.NotEqual(recorded, before);
        Assert.NotEqual(recorded, after);
    }
}
