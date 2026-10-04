using System.Text.Json;
using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The preset discs the BBC Micro page offers (<c>machines/bbc-micro/discs/manifest.json</c>), each
/// started the way the page's Insert and run starts it: the machine boots to BASIC's prompt, the
/// disc goes in drive 0, and SHIFT and BREAK go through the page's key queue
/// (<see cref="BbcKeyPresses.ShiftBreak"/>), so the DFS runs the disc's <c>!BOOT</c>. Each disc
/// must then leave the prompt, show no DFS or BASIC error, and reach the first screen it is known
/// to show.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where the expectations come from.</b> Each disc's first screen was seen when the discs were
/// chosen, in a scratch harness that powered the machine on with SHIFT held, and is recorded in
/// <c>docs/bbc-micro/facts/discs.md</c> and the journal for 4 October 2026. Where that screen has
/// words, the test looks for them; where it is pictures, the test counts the cells the screen
/// reader cannot read as a character, which a picture fills and BASIC's prompt does not.
/// </para>
/// <para>
/// <b>How long.</b> A disc loads at the drive's own speed, so the slowest takes several seconds of
/// machine time before its title shows. Each disc runs until its screen is there, looked at every
/// half second, and fails if it is not there by <see cref="Limit"/>.
/// </para>
/// <para>
/// This proves each disc starts and draws its first screen on this machine. It does not prove
/// the games play properly, and the sound was not listened to.
/// </para>
/// </remarks>
public sealed class DiscLibraryTests
{
    private const long Second = 2_000_000;

    /// <summary>The longest any disc is given to reach its first screen: twelve seconds of machine time.</summary>
    private const long Limit = 12 * Second;

    /// <summary>What a working disc never leaves on the screen: the DFS's and BASIC's error messages.</summary>
    private static readonly string[] Errors = ["Disk fault", "Bad command", "Not found", "Bad drive", "Disk changed", "Mistake", "No such", "Bad program", "Syntax error"];

    /// <summary>
    /// The first screen each disc shows, from the screens seen when it was chosen: words on it, or
    /// at least this many cells of picture. Every disc in the manifest must have one.
    /// </summary>
    private static readonly Dictionary<string, Expect> Expected = new()
    {
        ["hard-hat-harry-2"] = Expect.Words("HARD HAT HARRY 2"),
        ["mixed-grill-march"] = Expect.Words("Markie, our hero"),
        ["onslaught"] = Expect.Words("Instructions for *ONSLAUGHT*"),
        ["headcase-hotel"] = Expect.Words("V1.12"),
        ["caterpillar"] = Expect.Words("Guide the caterpillar"),
        ["jet-set-miner"] = Expect.Words("JET SET MINER"),
        ["beat-the-bull"] = Expect.Words("BEAT THE BULL"),
        ["sparse-invaders"] = Expect.Picture(2, 100),
        ["jsnake"] = Expect.Picture(1, 100),
        ["farmyard-fun"] = Expect.Words("Welcome to Farmyard Fun."),
        ["mazezam"] = Expect.Words("Escape from the Mazezams"),
        ["beebasm-demo"] = Expect.Picture(7, 20),
        ["blinkenlights"] = Expect.Words("A software novelty by Steven Flintham"),
        ["jumbo-scroller"] = Expect.Words("Jumbo text scroller"),
        ["teletext-mri"] = Expect.Picture(7, 100),
        ["beeb-6502-test"] = Expect.Words("CPU Tests"),
        ["cpm65"] = Expect.Words("A>"),
    };

    private sealed record Expect(string? Text, int Mode, int Cells)
    {
        public static Expect Words(string text) => new(text, -1, 0);

        public static Expect Picture(int mode, int cells) => new(null, mode, cells);

        public bool Met(BbcSession bbc, string[] screen) => Text is not null
            ? screen.Any(row => row.Contains(Text, StringComparison.Ordinal))
            : bbc.Machine.Bus.Peek(0x0355) == Mode && screen.Sum(row => row.Count(c => c == '?')) >= Cells;

        public override string ToString() => Text is not null ? $"the words \"{Text}\"" : $"mode {Mode} with at least {Cells} cells of picture";
    }

    /// <summary>One disc of the manifest, as the page reads it.</summary>
    public sealed record Disc(string Slug, string Title, string Sha256, int Bytes, int Tracks)
    {
        public override string ToString() => Slug;
    }

    public static IReadOnlyList<Disc> Manifest { get; } = ReadManifest();

    public static TheoryData<string> Slugs => new(Manifest.Select(d => d.Slug));

    private static IReadOnlyList<Disc> ReadManifest()
    {
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "machines", "bbc-micro", "discs", "manifest.json")));
        return file.RootElement.GetProperty("discs").EnumerateArray().Select(d => new Disc(
            d.GetProperty("slug").GetString()!,
            d.GetProperty("title").GetString()!,
            d.GetProperty("sha256").GetString()!,
            d.GetProperty("bytes").GetInt32(),
            d.GetProperty("tracks").GetInt32())).ToArray();
    }

    /// <summary>The disc's image, read from its folder and checked against the manifest's SHA-256.</summary>
    private static byte[] Image(Disc disc) =>
        RepoPaths.ReadChecked(Path.Combine("machines", "bbc-micro", "discs", disc.Slug, disc.Slug + ".ssd"), disc.Sha256);

    private static string[] Rows(BbcSession bbc) => bbc.ScreenText().Select(r => r.TrimEnd()).ToArray();

    /// <summary>BASIC waiting for a line: the last row with anything on it is the prompt alone.</summary>
    private static bool AtThePrompt(string[] screen) => screen.LastOrDefault(r => r.Length > 0) == ">";

    private static string Show(string[] screen) => string.Join(" | ", screen.Where(r => r.Length > 0));

    /// <summary>Power on to BASIC's prompt, as the page is when the visitor first picks a disc.</summary>
    private static BbcSession AtBasic()
    {
        BbcSession bbc = new BbcSession().Boot();
        Assert.True(AtThePrompt(Rows(bbc)), "the machine did not boot to BASIC's prompt");
        return bbc;
    }

    /// <summary>Insert and run, as the page does it: the disc in drive 0, then SHIFT and BREAK through the key queue.</summary>
    private static BbcKeyPresses InsertAndRun(BbcSession bbc, Disc disc)
    {
        DiscImage image = DiscImage.FromBytes(Image(disc), doubleSided: false);
        Assert.Equal(disc.Tracks, image.Tracks);
        bbc.Machine.Insert(0, image);
        var keys = new BbcKeyPresses(bbc.Machine);
        keys.ShiftBreak();
        return keys;
    }

    /// <summary>Runs until the disc's first screen is there, or the limit; returns the screen.</summary>
    private static string[] RunToFirstScreen(BbcSession bbc, BbcKeyPresses keys, Disc disc)
    {
        Expect expect = Expected[disc.Slug];
        long start = bbc.Machine.Cycles;
        string[] screen = Rows(bbc);
        while (bbc.Machine.Cycles - start < Limit)
        {
            keys.Run(Second / 2);
            screen = Rows(bbc);
            Assert.False(Errors.Any(e => Show(screen).Contains(e, StringComparison.Ordinal)), $"{disc.Title} shows an error: {Show(screen)}");
            if (expect.Met(bbc, screen))
            {
                return screen;
            }
        }

        Assert.Fail($"{disc.Title} did not show {expect} within {Limit / Second} seconds of machine time; the screen shows: {Show(screen)}");
        return screen;
    }

    [Fact]
    public void EveryDiscInTheManifestHasAFirstScreenToLookFor()
    {
        Assert.NotEmpty(Manifest);
        Assert.Equal(Manifest.Select(d => d.Slug).Order(), Expected.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(Slugs))]
    public void InsertAndRunStartsTheDisc(string slug)
    {
        Disc disc = Manifest.Single(d => d.Slug == slug);
        BbcSession bbc = AtBasic();
        string[] screen = RunToFirstScreen(bbc, InsertAndRun(bbc, disc), disc);
        Assert.False(AtThePrompt(screen), $"{disc.Title} left the machine at BASIC's prompt: {Show(screen)}");
    }

    [Fact]
    public void WithoutShiftTheSameDiscDoesNotStart()
    {
        // The control: BREAK alone, with the same disc in, leaves BASIC's prompt, so the test above
        // is seeing SHIFT start the disc and not something BREAK does by itself.
        Disc disc = Manifest.Single(d => d.Slug == "caterpillar");
        BbcSession bbc = AtBasic();
        bbc.Machine.Insert(0, DiscImage.FromBytes(Image(disc), doubleSided: false));
        bbc.Machine.PressBreak();
        bbc.RunFor(4 * Second);
        string[] screen = Rows(bbc);
        Assert.True(AtThePrompt(screen), $"BREAK alone did not leave the prompt: {Show(screen)}");
        Assert.DoesNotContain(screen, row => row.Contains("Guide the caterpillar", StringComparison.Ordinal));
    }

    [Fact]
    public void ASecondDiscStartsOverTheFirst()
    {
        // The visitor picks one disc, then another, while the first is running: the second's
        // Insert and run must start it, whatever the first left in memory.
        BbcSession bbc = AtBasic();
        Disc first = Manifest.Single(d => d.Slug == "onslaught");
        RunToFirstScreen(bbc, InsertAndRun(bbc, first), first);
        bbc.RunFor(2 * Second);

        Disc second = Manifest.Single(d => d.Slug == "caterpillar");
        RunToFirstScreen(bbc, InsertAndRun(bbc, second), second);
    }
}
