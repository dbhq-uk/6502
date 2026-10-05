using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The picture, bit for bit, at fixed points, held against SHA-256 hashes of the framebuffer.
/// </summary>
/// <remarks>
/// <para>
/// <b>These expected values come from the old implementation, on purpose.</b> They are the one
/// place in the Electron's tests where they do: the hashes were recorded from the display as it
/// was before the pixel table (task 13's second commit, which made <c>UlaDisplay.DrawLine</c>
/// draw a byte at a time from ready-made pixels), and the point of the test is that the output
/// did not change. Every other display test works its expected pixels from <c>ula.md</c>; these
/// say only "the same as before". A change that is meant to change the picture must say why and
/// record new hashes.
/// </para>
/// <para>
/// Recorded on 5 October 2026 at commit <c>7a3818b</c>, from the failure messages of this class
/// run with placeholders. The points: the boot screen in mode 6; the screen after <c>MODE 0</c>
/// to <c>MODE 5</c> is typed (only the prompt shows, so modes that draw a white <c>&gt;</c> the
/// same width give the same hash: 0 and 3, 1 and 4, 2 and 5); the display alone with random screen memory in each mode, over both fields; and a
/// field with a palette change and a mode change in the middle of it, as the display tests make
/// them.
/// </para>
/// </remarks>
public class DisplayPictureHashTests
{
    private const int Line = 128;

    /// <summary>The first 128 bits of the SHA-256 of the framebuffer's bytes, in hex.</summary>
    private static string Hash(ReadOnlySpan<uint> pixels) =>
        Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels)))[..32];

    [Fact]
    public void TheBootScreenInMode6()
    {
        var s = new ElectronSession().Boot();
        Assert.Equal("1546EF8D0D6E8D10D29EFB2121916174", Hash(s.Machine.Bus.Screen.Pixels));
    }

    [Theory]
    [InlineData(0, "8ADFF46B1C552A92E7817434B6B84BB1")]
    [InlineData(1, "39FE80DF5D6A2F413FDCADFAE40091F1")]
    [InlineData(2, "EDB842F1DEBB8B439ADB2D6DD214E142")]
    [InlineData(3, "8ADFF46B1C552A92E7817434B6B84BB1")]
    [InlineData(4, "39FE80DF5D6A2F413FDCADFAE40091F1")]
    [InlineData(5, "EDB842F1DEBB8B439ADB2D6DD214E142")]
    public void TheScreenAfterTypingMode(int mode, string hash)
    {
        var s = new ElectronSession().Boot();
        s.Type($"MODE {mode}\r").RunUntilPrompt();
        Assert.Equal(hash, Hash(s.Machine.Bus.Screen.Pixels));
    }

    [Theory]
    [InlineData(0, "758981B90BA0CD4D3C0434C3DAEE19EE")]
    [InlineData(1, "F78FF09CB6BBDAF6F60E268DF2E41E7D")]
    [InlineData(2, "71325705D72DE620C388601E420723CD")]
    [InlineData(3, "CC43F704063A0F5B8ADD42AA89C818FB")]
    [InlineData(4, "EEA8B5955921D3C87C60F41854B6A934")]
    [InlineData(5, "97C6725F44789691F9132D55F1B61B67")]
    [InlineData(6, "431C7FF778F38BB3DA67C631B249AD8A")]
    public void RandomScreenMemoryInEveryModeOverBothFields(int mode, string hash)
    {
        // Seeded random RAM, so every byte value meets the pixel table, and the OS's palette for
        // the mode (as UlaDisplayTests sets it, ula.md s5c), so every colour the mode has differs.
        var ram = new byte[0x8000];
        new Random(1983 + mode).NextBytes(ram);
        var display = new UlaDisplay(ram);
        display.Write(7, (byte)(mode << 3), 0);
        byte[] palette = mode switch
        {
            1 or 5 => [0x73, 0x31],
            2 => [0xF5, 0x5F, 0x05, 0x5F, 0x05, 0x50, 0xF5, 0x50],
            _ => [0x11, 0x11],
        };
        for (int i = 0; i < palette.Length; i++)
        {
            display.Write(8 + i, palette[i], 0);
        }

        display.CatchUp(39_936 + (255 * Line) + 1);
        Assert.Equal(hash, Hash(display.Screen.Pixels));
    }

    [Fact]
    public void AFieldWithAPaletteChangeAndAModeChangeInTheMiddle()
    {
        // As UlaDisplayTests.APaletteWriteTakesEffectFromTheNextLine and
        // AModeWriteTakesEffectAtTheEndOfTheScanline, on random screen memory, in one field.
        var ram = new byte[0x8000];
        new Random(1984).NextBytes(ram);
        var display = new UlaDisplay(ram);
        display.Write(7, 1 << 3, 0);
        display.Write(8, 0x73, 0);
        display.Write(9, 0x31, 0);
        display.Write(8, 0xFF, (50 * Line) + 10);
        display.Write(9, 0xFB, (50 * Line) + 10);
        display.Write(7, 2 << 3, (100 * Line) + 50);
        display.Write(10, 0x05, (120 * Line) + 3);
        display.Write(7, 4 << 3, 150 * Line);
        display.Write(7, 0 << 3, (200 * Line) + 127);
        display.CatchUp((255 * Line) + 1);
        Assert.Equal("18DC67DABE992B62CF4F61D2A5EC2478", Hash(display.Screen.Pixels));
    }
}
