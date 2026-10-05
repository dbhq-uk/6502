using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// Blargg's <c>ppu_vbl_nmi</c> singles, from the fork at the pinned commit: the VBlank flag, NMI
/// and the odd frame, each to one PPU dot. NTSC only, because the readme says they test "the NTSC
/// PPU". The combined ROM is an MMC1 cartridge and runs in task 12; each single is NROM.
/// </summary>
public class BlarggTests
{
    // A budget well past what any single needs, so one that hangs fails instead of running on.
    // The cycles each needed when this was written are in the journal, task 4.
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

    [Theory]
    [MemberData(nameof(PpuVblNmiSingles))]
    public void EachPpuVblNmiSinglePasses(string single)
    {
        BlarggResult result = BlarggRunner.Run($"ppu_vbl_nmi/rom_singles/{single}.nes", Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"{single} gave no result in {Budget} cycles (status {result.Status}). Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"{single} reported status {result.Status} after {result.Cycles} cycles. Its text:\n{result.Text}");
    }
}
