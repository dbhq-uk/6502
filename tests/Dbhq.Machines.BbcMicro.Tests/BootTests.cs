using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The real MOS 1.20 booting with BASIC and the DFS, checked before any video exists. In mode 7
/// the OS writes the banner into screen memory as character codes at <c>$7C00</c>, so the text
/// can be read from RAM; it comes from the ROMs running on the CPU, so the check is not circular.
/// </summary>
/// <remarks>
/// <para>
/// Every expected string is in the ROMs (bus.md s4b): <c>BBC Computer </c> and <c>32K</c> at OS
/// <c>$C304</c>, the language title <c>BASIC</c> at BASIC <c>$8009</c>, printed by the OS at
/// <c>$DBF2</c>, and the prompt <c>&gt;</c> from BASIC <c>$8B06</c>.
/// </para>
/// <para>
/// <c>Acorn DFS</c> is not on the screen yet, and that is the ROM's doing. Before it serves any
/// call the DFS reads the 8271's status at <c>$FE80</c> and does nothing if either of its two
/// low bits is set (DFS <c>$B495-$B49A</c>). No 8271 is fitted, so <c>$FE80</c> reads as an
/// absent fast device, <c>$FE</c>, and the DFS stays silent, as a real Model B with the ROM and
/// no controller would. The row comes back when the 8271 does.
/// </para>
/// </remarks>
public class BootTests
{
    [Fact]
    public void ColdBootPrintsTheBannerAndTheBasicPromptInMode7()
    {
        var s = new BbcSession(mode: 7).Boot();
        Assert.Equal("", s.ScreenRowAsMemory(0).TrimEnd());
        Assert.Equal("BBC Computer 32K", s.ScreenRowAsMemory(1).TrimEnd());
        Assert.Equal("", s.ScreenRowAsMemory(2).TrimEnd());
        Assert.Equal("BASIC", s.ScreenRowAsMemory(3).TrimEnd());
        Assert.Equal("", s.ScreenRowAsMemory(4).TrimEnd());
        Assert.Equal(">", s.ScreenRowAsMemory(5).TrimEnd()); // plus the cursor
        Assert.Equal("", s.ScreenRowAsMemory(6).TrimEnd());
    }

    [Fact]
    public void BreakPrintsTheBannerWithoutTheMemorySize()
    {
        // A soft BREAK leaves the system VIA alone, so its IER tells the OS this is not a power
        // on; $028D is then 0 and the OS skips the "32K" (OS $DB71-$DB74), then re-enters the
        // language it was running, printing its title (OS $DBBE-$DBF2).
        var s = new BbcSession(mode: 7).Boot();
        s.Machine.PressBreak();
        s.Machine.Run(6_000_000);

        Assert.Equal(0, s.Machine.Bus.Peek(0x028D)); // the last reset was a soft BREAK
        Assert.Equal("BBC Computer", s.ScreenRowAsMemory(1).TrimEnd());
        Assert.Equal("BASIC", s.ScreenRowAsMemory(3).TrimEnd());
        Assert.Equal(">", s.ScreenRowAsMemory(5).TrimEnd());
    }

    [Theory]
    [InlineData(0, 0x3000, 0x06)]
    [InlineData(1, 0x3000, 0x06)]
    [InlineData(2, 0x3000, 0x06)]
    [InlineData(3, 0x4000, 0x08)]
    [InlineData(4, 0x5800, 0x0B)]
    [InlineData(5, 0x5800, 0x0B)]
    [InlineData(6, 0x6000, 0x0C)]
    [InlineData(7, 0x7C00, 0x28)]
    public void EachStartUpModeBootsIntoThatMode(int mode, int screenBase, int r12)
    {
        var s = new BbcSession(mode).Boot();
        BbcBus bus = s.Machine.Bus;

        // The OS keeps the current mode at $0355: it is stored by STX $0355 at $CB3D, after
        // AND #$07 on the requested mode.
        Assert.Equal(mode, bus.Peek(0x0355));

        // The banner's pixels (or, in mode 7, its characters) are in that mode's screen memory
        // (bus.md s3c); the RAM clear at power on left it all zero.
        int nonZero = 0;
        for (int a = screenBase; a < 0x8000; a++)
        {
            nonZero += bus.Peek((ushort)a) != 0 ? 1 : 0;
        }
        Assert.True(nonZero > 0, $"Nothing was drawn in screen memory from ${screenBase:X4}.");

        // The CRTC holds the mode's screen start, start address / 8, or ($7C00 hi - $74) EOR $20
        // in mode 7 (bus.md s3b).
        bus.Write(0xFE00, 12);
        Assert.Equal(r12, bus.Peek(0xFE01));
        bus.Write(0xFE00, 13);
        Assert.Equal(0x00, bus.Peek(0xFE01));
    }
}
