using System.Security.Cryptography;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// What the page's host (<c>NesHost</c> in <c>src/Dbhq.Machines.Nes.Wasm</c>) asks of the
/// library when a visitor's file arrives: <see cref="NesLoader.RegionOf"/> says which region the
/// header names, and <see cref="NesLoader.Load"/> builds the machine in the region chosen and
/// says, in one sentence, which it chose and why (the plan's Review Focus 2). A bad file throws
/// the cartridge's own plain sentence from either, the board's refusal included (Ruling J), and
/// no machine is built, so the page's machine stays as it was. Then
/// <see cref="Nes.RunFrames"/> and a power cycle, which the page's test hook uses to compare
/// the canvas with the recorded frame (Ruling D).
/// </summary>
public class NesLoaderTests
{
    private static readonly NesOptions Options = new();

    [Theory]
    [InlineData(0, "NTSC")]
    [InlineData(1, "PAL")]
    [InlineData(2, "")]
    public void RegionOfIsTheRegionANes20HeaderNames(int timing, string expected)
    {
        Assert.Equal(expected, NesLoader.RegionOf(TestCartridge.Nes2(1, 1, timing: timing)));
    }

    [Fact]
    public void RegionOfAnInesFileIsEmptyBecauseItsHeaderDoesNotSay()
    {
        Assert.Equal(string.Empty, NesLoader.RegionOf(TestCartridge.Ines1(1, 1)));
    }

    public static TheoryData<string, byte[]> BadFiles() => new()
    {
        { "empty", [] },
        { "not a NES file", TestCartridge.Filled(40_000, 0x41) },
        { "cut short", TestCartridge.Ines1(2, 1)[..20_000] },
        { "for the Dendy", TestCartridge.Nes2(1, 1, timing: 3) },
        { "an MMC5 board", TestCartridge.Ines1(1, 1, mapper: 5) },
    };

    [Theory]
    [MemberData(nameof(BadFiles))]
    public void ABadFileThrowsTheCartridgesPlainSentenceFromRegionOfAndFromLoad(string why, byte[] file)
    {
        NesFormatException fromRegionOf = Assert.Throws<NesFormatException>(() => NesLoader.RegionOf(file));
        NesFormatException fromLoad = Assert.Throws<NesFormatException>(() => NesLoader.Load(file, string.Empty, Options, out _));

        AssertPlainSentence(fromRegionOf.Message);
        Assert.Equal(fromRegionOf.Message, fromLoad.Message);
        Assert.False(string.IsNullOrEmpty(why));
    }

    [Fact]
    public void AFileOverTheSizeLimitIsRefusedByBoth()
    {
        // Not in the theory's data: a test runner would copy 4 MB into every case's name.
        byte[] file = TestCartridge.Filled(Cartridge.MaxFileSize + 1, 0);

        AssertPlainSentence(Assert.Throws<NesFormatException>(() => NesLoader.RegionOf(file)).Message);
        AssertPlainSentence(Assert.Throws<NesFormatException>(() => NesLoader.Load(file, "NTSC", Options, out _)).Message);
    }

    [Fact]
    public void AnUnsupportedBoardIsRefusedWithTheBoardsOwnMessage()
    {
        byte[] file = TestCartridge.Ines1(1, 1, mapper: 5);
        string board = Assert.Throws<NesFormatException>(() => Cartridge.Load(file).CreateMapper()).Message;

        Assert.Equal(board, Assert.Throws<NesFormatException>(() => NesLoader.RegionOf(file)).Message);
        Assert.Equal(board, Assert.Throws<NesFormatException>(() => NesLoader.Load(file, "PAL", Options, out _)).Message);
    }

    [Fact]
    public void WithNoChoiceAFileThatSaysPalRunsAsPalAndTheSentenceSaysTheFileSaysSo()
    {
        Nes nes = NesLoader.Load(TestCartridge.Nes2(1, 1, timing: 1), string.Empty, Options, out string why);

        Assert.Same(Region.Pal, nes.Region);
        Assert.Equal("Running as PAL: the file says PAL.", why);
    }

    [Fact]
    public void WithNoChoiceAFileThatSaysNtscRunsAsNtscAndTheSentenceSaysTheFileSaysSo()
    {
        Nes nes = NesLoader.Load(TestCartridge.Nes2(1, 1, timing: 0), string.Empty, Options, out string why);

        Assert.Same(Region.Ntsc, nes.Region);
        Assert.Equal("Running as NTSC: the file says NTSC.", why);
    }

    [Fact]
    public void WithNoChoiceAFileThatDoesNotSayRunsAsNtscAndTheSentenceSaysWhy()
    {
        Nes nes = NesLoader.Load(TestCartridge.Ines1(1, 1), string.Empty, Options, out string why);

        Assert.Same(Region.Ntsc, nes.Region);
        Assert.Equal("Running as NTSC: the file does not say, so NTSC.", why);
    }

    [Fact]
    public void AChoiceAgainstTheFileIsTakenAndTheSentenceSaysItMayRunAtTheWrongSpeed()
    {
        Nes nes = NesLoader.Load(TestCartridge.Nes2(1, 1, timing: 1), "NTSC", Options, out string why);

        Assert.Same(Region.Ntsc, nes.Region);
        Assert.Equal("Running as NTSC, as chosen here, though the file says PAL, so it may run at the wrong speed.", why);
    }

    [Fact]
    public void AChoiceTheFileAgreesWithSaysSo()
    {
        Nes nes = NesLoader.Load(TestCartridge.Nes2(1, 1, timing: 1), "PAL", Options, out string why);

        Assert.Same(Region.Pal, nes.Region);
        Assert.Equal("Running as PAL, as chosen here, which is what the file says.", why);
    }

    [Fact]
    public void AChoiceWhereTheFileDoesNotSaySaysSo()
    {
        Nes nes = NesLoader.Load(TestCartridge.Ines1(1, 1), "PAL", Options, out string why);

        Assert.Same(Region.Pal, nes.Region);
        Assert.Equal("Running as PAL, as chosen here: the file does not say which.", why);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("", 1)]
    [InlineData("", 2)]
    [InlineData("NTSC", 0)]
    [InlineData("NTSC", 1)]
    [InlineData("NTSC", 2)]
    [InlineData("PAL", 0)]
    [InlineData("PAL", 1)]
    [InlineData("PAL", 2)]
    public void EverySentenceIsOnePlainSentenceNamingTheRegionItRunsIn(string region, int timing)
    {
        Nes nes = NesLoader.Load(TestCartridge.Nes2(1, 1, timing: timing), region, Options, out string why);

        AssertPlainSentence(why);
        Assert.StartsWith($"Running as {nes.Region.Name}", why);
    }

    [Theory]
    [InlineData("pal")]
    [InlineData("Dendy")]
    [InlineData("ntsc ")]
    public void ARegionThatIsNotNtscPalOrEmptyIsTheCallersMistakeAndIsRefused(string region)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NesLoader.Load(TestCartridge.Ines1(1, 1), region, Options, out _));
    }

    [Fact]
    public void LoadSwitchesTheMachineOnWithTheOptionsSampleRate()
    {
        Nes nes = NesLoader.Load(TestCartridge.Ines1(1, 1), string.Empty, new NesOptions { SampleRate = 44_100 }, out _);

        // The reset sequence's seven cycles have run, so the machine is on.
        Assert.Equal(7, nes.Bus.Cycles);
        Assert.Equal(44_100, nes.Sound.SampleRate);
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void RunFramesStopsWithExactlyThatManyFramesCompleted(string name)
    {
        var nes = new Nes(Cartridge.Load(BootCheck.Rom()), BootCheck.RegionNamed(name));
        nes.PowerOn();

        nes.RunFrames(3);
        Assert.Equal(3, nes.Bus.Ppu.Frame);

        nes.RunFrames(2);
        Assert.Equal(5, nes.Bus.Ppu.Frame);

        nes.RunFrames(0);
        Assert.Equal(5, nes.Bus.Ppu.Frame);
    }

    [Fact]
    public void RunFramesRefusesANegativeCount()
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)));
        nes.PowerOn();

        Assert.Throws<ArgumentOutOfRangeException>(() => nes.RunFrames(-1));
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void APowerCycleAfterPlayingThenRunFramesGivesTheRecordedFrame(string name)
    {
        // What the page's stepTo does: the machine has been running, with a button held, then it
        // is switched off and on and run to the recorded frame count. The picture must be the
        // recorded one, to the byte, as the browser check will compare it.
        Nes nes = NesLoader.Load(BootCheck.Rom(), name, Options, out _);
        nes.RunFrames(40);
        nes.SetButtons(0, 0x08);
        nes.RunFrames(10);
        nes.SetButtons(0, 0);

        nes.PowerOn();
        nes.RunFrames(BootCheck.Frames);

        Assert.Equal(BootCheck.Frames, nes.Bus.Ppu.Frame);
        string hash = Convert.ToHexStringLower(SHA256.HashData(BootCheck.FrameBytes(nes.Bus.Ppu.Screen.Pixels)));
        Assert.Equal(BootCheck.ExpectedHash(nes.Region), hash);
    }

    /// <summary>One sentence for a page: not empty, one line, ends with a full stop, no dashes.</summary>
    private static void AssertPlainSentence(string message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.DoesNotContain('\n', message);
        Assert.EndsWith(".", message);
        Assert.Equal(1, message.Count(c => c == '.'));
        Assert.DoesNotContain('\u2013', message);
        Assert.DoesNotContain('\u2014', message);
    }
}
