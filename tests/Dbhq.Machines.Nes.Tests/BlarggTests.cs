using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// Blargg's test ROMs, from the fork at the pinned commit. The <c>ppu_vbl_nmi</c> singles: the
/// VBlank flag, NMI and the odd frame, each to one PPU dot. NTSC only, because the readme says
/// they test "the NTSC PPU". The combined ROM is an MMC1 cartridge and runs in task 12; each single
/// is NROM. Then <c>sprite_hit_tests_2005.10.05</c> and <c>sprite_overflow_tests</c> (task 5),
/// every ROM in each folder, all NROM with CHR RAM, which report on the screen.
/// </summary>
public class BlarggTests
{
    // A budget well past what any of these ROMs needs, so one that hangs fails instead of running
    // on. The cycles each needed when it was written are in the journal, tasks 4 and 5.
    private const long Budget = 18_000_000;

    public static TheoryData<string> PpuVblNmiSingles() => new()
    {
        "01-vbl_basics",
        "02-vbl_set_time",
        "03-vbl_clear_time",
        "04-nmi_control",
        "05-nmi_timing",
        "06-suppression",
        "07-nmi_on_timing",
        "08-nmi_off_timing",
        "09-even_odd_frames",
        "10-even_odd_timing",
    };

    public static TheoryData<string> SpriteHitTests() => new()
    {
        "01.basics",
        "02.alignment",
        "03.corners",
        "04.flip",
        "05.left_clip",
        "06.right_edge",
        "07.screen_bottom",
        "08.double_height",
        "09.timing_basics",
        "10.timing_order",
        "11.edge_timing",
    };

    public static TheoryData<string> SpriteOverflowTests() => new()
    {
        "1.Basics",
        "2.Details",
        "3.Timing",
        "4.Obscure",
        "5.Emulator",
    };

    [Theory]
    [MemberData(nameof(SpriteHitTests))]
    public void EachSpriteHitTestPasses(string rom)
    {
        AssertScreenReportingRomPasses($"sprite_hit_tests_2005.10.05/{rom}.nes");
    }

    [Theory]
    [MemberData(nameof(SpriteOverflowTests))]
    public void EachSpriteOverflowTestPasses(string rom)
    {
        AssertScreenReportingRomPasses($"sprite_overflow_tests/{rom}.nes");
    }

    // The readmes say both folders test "the NTSC NES PPU", so they run on NTSC only.
    private static void AssertScreenReportingRomPasses(string pinnedName)
    {
        BlarggResult result = BlarggRunner.RunScreenReporting(pinnedName, Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"{pinnedName} did not finish in {Budget} cycles (on test {result.Status}). Its screen:\n{result.Text}");
        Assert.True(result.Status == 1, $"{pinnedName} failed test {result.Status} after {result.Cycles} cycles. Its screen:\n{result.Text}");
    }

    [Theory]
    [MemberData(nameof(PpuVblNmiSingles))]
    public void EachPpuVblNmiSinglePasses(string single)
    {
        BlarggResult result = BlarggRunner.Run($"ppu_vbl_nmi/rom_singles/{single}.nes", Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"{single} gave no result in {Budget} cycles (status {result.Status}). Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"{single} reported status {result.Status} after {result.Cycles} cycles. Its text:\n{result.Text}");
    }
}
