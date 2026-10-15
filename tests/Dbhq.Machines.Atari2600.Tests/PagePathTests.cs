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

        uint[] window = new uint[FrameBuffer.Width * region.ShownLines];
        machine.Bus.Tia.Frame.ToRgba(region, window);
        BootRun recorded = BootCheck.Run(region);
        Assert.Equal(recorded.ColoursSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(machine.Bus.Tia.Frame.ColourNumbers)));
        Assert.Equal(recorded.RgbaSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(BootCheck.RgbaBytes(window))));
    }

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
        string before = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(machine.Bus.Tia.Frame.ColourNumbers));
        machine.RunFrames(2);
        string after = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(machine.Bus.Tia.Frame.ColourNumbers));

        BootRun recorded = BootCheck.Run(region);
        Assert.NotEqual(recorded.ColoursSha256, before);
        Assert.NotEqual(recorded.ColoursSha256, after);
    }
}
