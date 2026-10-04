using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The acceptance test for the machine's disc: the real MOS 1.20, DFS 1.20 and BASIC driving the
/// 8271, commands typed on the keyboard and the answers read off the picture, and the disc image
/// read with <see cref="DfsCatalogue"/>, which is checked against the sheet's worked examples
/// first (<see cref="DfsCatalogueTests"/>).
/// </summary>
/// <remarks>
/// Every expected screen row is the fact sheet's (<c>disc.md</c> s6), captured from the DFS ROM's
/// own output in the probe, or follows from its stated rules; NUL bytes the DFS prints are VDU 0,
/// which draws nothing, so the screen shows the text without them. An error message comes on the
/// row after a blank one: BASIC's default error handler (BASIC <c>$B433</c>) starts with REPORT,
/// which calls <c>$BC25</c>, <c>JSR OSNEWL</c>, before the message (BASIC <c>$BFE4-$BFE7</c>).
/// </remarks>
public class DiscTests
{
    /// <summary>What a command leaves on the screen: time for the DFS and two fields of picture.</summary>
    private const long Settle = 1_000_000;

    /// <summary>The program the brief names: LDA #'A', JSR OSWRCH, RTS; then 250 bytes of a known pattern.</summary>
    private static readonly byte[] Program = MakeProgram();

    [Fact]
    public void CatOnABlankDiscShowsTheEmptyCatalogue()
    {
        // s6a: a 12-character title of NULs, so the row starts with the space before "(00)".
        var s = new BbcSession(mode: 7, disc: DiscImage.Blank(40, false)).Boot();
        string[] rows = Command(s, "*CAT");

        Assert.Equal(
            [" (00)", "Drive 0             Option 0 (off)", "Dir. :0.$           Lib. :0.$", "", ">"],
            rows.Take(5));
    }

    [Fact]
    public void SaveWritesTheCatalogueAndTheDataThenLoadAndRunReadThemBack()
    {
        DiscImage disc = DiscImage.Blank(40, false);
        var s = new BbcSession(mode: 7, disc: disc).Boot();
        Poke(s, 0x2000, Program);
        Command(s, "*SAVE TEST 2000 2100 2000 2000");

        // The image (s5c): one file TEST in $, load and exec $2000, length $0100, start sector 2,
        // the cycle number 1; the data in sector 2.
        DfsCatalogue catalogue = DfsCatalogue.Read(disc);
        Assert.Equal(1, catalogue.Cycle);
        Assert.Equal(400, catalogue.Sectors);
        Assert.Equal(new DfsFile('$', "TEST", false, 0x2000, 0x2000, 0x0100, 2), Assert.Single(catalogue.Files));
        Assert.Equal([0x54, 0x45, 0x53, 0x54, 0x20, 0x20, 0x20, 0x24], disc.Sector(0, 0, 0)[8..16].ToArray());
        Assert.Equal([0x00, 0x20, 0x00, 0x20, 0x00, 0x01, 0x00, 0x02], disc.Sector(0, 0, 1)[8..16].ToArray());
        Assert.Equal(Program, disc.Sector(0, 0, 2).ToArray());

        // *INFO * (s6c): the sheet's format, which the helper's line follows.
        Assert.Equal("$.TEST        002000 002000 000100 002", Command(s, "*INFO *")[0]);

        // A new machine on the same image: the file comes back, and runs.
        var fresh = new BbcSession(mode: 7, disc: disc).Boot();
        Command(fresh, "*LOAD TEST 4000");
        Assert.Equal(Program, Peek(fresh, 0x4000, 256));
        Assert.Equal('A', Command(fresh, "*RUN TEST")[0][0]);
    }

    [Fact]
    public void AccessTitleOptAndDeleteRewriteTheCatalogue()
    {
        // s5a, s6a: each rewrite adds one to the cycle number; *ACCESS L sets bit 7 of the
        // directory byte; *TITLE pads with spaces to twelve; *OPT 4,n puts n in bits 5 and 4 of
        // sector 1 byte 6; *DELETE takes the entry out.
        DiscImage disc = DiscImage.Blank(40, false);
        var s = new BbcSession(mode: 7, disc: disc).Boot();
        Poke(s, 0x2000, Program);
        Command(s, "*SAVE TEST 2000 2100 2000 2000");

        Command(s, "*ACCESS TEST L");
        Assert.Equal(0xA4, disc.Sector(0, 0, 0)[15]);
        Assert.True(Assert.Single(DfsCatalogue.Read(disc).Files).Locked);
        Assert.Equal(2, DfsCatalogue.Read(disc).Cycle);
        Assert.Equal("$.TEST     L  002000 002000 000100 002", Command(s, "*INFO TEST")[0]);

        Command(s, "*ACCESS TEST");
        Assert.Equal(0x24, disc.Sector(0, 0, 0)[15]);

        Command(s, "*TITLE MYDISC");
        Assert.Equal("MYDISC      ", DfsCatalogue.Read(disc).Title);

        Command(s, "*OPT 4,3");
        Assert.Equal(0x31, disc.Sector(0, 0, 1)[6]);
        Assert.Equal(3, DfsCatalogue.Read(disc).BootOption);
        Assert.Equal(400, DfsCatalogue.Read(disc).Sectors);

        // The catalogue as *CAT shows it (s6a, s6b): the padded title and the cycle, option 3, and
        // the file, which starts four spaces in.
        string[] rows = Command(s, "*CAT");
        Assert.Equal(["MYDISC       (05)", "Drive 0             Option 3 (EXEC)", "Dir. :0.$           Lib. :0.$", "", "    TEST"], rows.Take(5));

        Command(s, "*DELETE TEST");
        DfsCatalogue catalogue = DfsCatalogue.Read(disc);
        Assert.Empty(catalogue.Files);
        Assert.Equal(0, disc.Sector(0, 0, 1)[5]);
        Assert.Equal(6, catalogue.Cycle);
    }

    [Fact]
    public void AReadOnlyDiscSaysDiskReadOnly()
    {
        // s6d: $C9 "Disk read only", after DFS's eleven attempts; the image is not touched.
        DiscImage disc = DiscImage.Blank(40, false);
        disc.ReadOnly = true;
        byte[] before = disc.ToBytes();
        var s = new BbcSession(mode: 7, disc: disc).Boot();
        Poke(s, 0x2000, Program);

        Assert.Equal("Disk read only", Error(s, "*SAVE TEST 2000 2100 2000 2000"));
        Assert.Equal(before, disc.ToBytes());
    }

    [Fact]
    public void ASideTheDiscDoesNotHaveIsDiskFaultEighteen()
    {
        // *CAT 2 is drive 0's side 1 (s1f): DFS writes 3A 23 68 and reads track 0 sector 0, which a
        // single-sided image does not have, so sector not found, $18, eleven times (s6d).
        var s = new BbcSession(mode: 7, disc: DiscImage.Blank(40, false)).Boot();
        Assert.Equal("Disk fault 18 at 00/00", Error(s, "*CAT 2"));
    }

    [Fact]
    public void SideOneOfADoubleSidedDiscIsDriveTwo()
    {
        DiscImage dsd = DiscImage.Blank(40, doubleSided: true);
        "SIDEONE"u8.CopyTo(dsd.Sector(1, 0, 0));
        var s = new BbcSession(mode: 7, disc: dsd).Boot();

        Assert.Equal("SIDEONE (00)", Command(s, "*CAT 2")[0]);
        Assert.Equal("Drive 2             Option 0 (off)", Command(s, "*CAT 2")[1]);
        Assert.Equal(" (00)", Command(s, "*CAT 0")[0]);
    }

    [Fact]
    public void TheMessagesAreDisksNotDiscs()
    {
        // s6d: "Not found", not "File not found"; "Bad command".
        var s = new BbcSession(mode: 7, disc: DiscImage.Blank(40, false)).Boot();
        Assert.Equal("Not found", Error(s, "*LOAD NOPE"));
        Assert.Equal("Not found", Error(s, "*INFO NOPE"));
        Assert.Equal("Bad command", Error(s, "*NOSUCH"));
        Assert.Equal("Bad command", Error(s, "*RUN NOPE"));
    }

    [Fact]
    public void CatWithNoDiscPollsUntilEscape()
    {
        // s1h, s3, s6d: with an empty drive the ready bit never comes, and DFS polls Read Drive
        // Status with no timeout. Ten seconds of machine time later it is still at it: no prompt,
        // the DFS ROM paged in, and the 8271 still being asked. Escape ends it with "Escape".
        var s = new BbcSession(mode: 7).Boot();
        s.Type("*CAT\r");
        int echo = Echo(s, "*CAT");

        s.RunFor(20_000_000);
        string[] screen = s.ScreenText();
        Assert.All(screen.Skip(echo + 1), row => Assert.Equal("", row.TrimEnd()));
        Assert.Equal(14, s.Machine.Bus.RomSlot);
        byte status = s.Machine.Fdc.Peek(0);
        Assert.Equal(0x00, status & 0x08);   // no interrupt: not a data command, only polling
        int polls = 0;
        for (int i = 0; i < 2_000; i++)
        {
            s.RunFor(100);
            polls += s.Machine.Fdc.Peek(0) is 0x10 or 0x80 or 0xC0 ? 1 : 0;
        }
        Assert.True(polls > 0, "the 8271 was not being asked");

        s.Machine.Keyboard.Press(BbcKey.Escape);
        s.RunFor(BbcSession.HoldCycles);
        s.Machine.Keyboard.Release(BbcKey.Escape);
        s.RunFor(Settle);

        screen = s.ScreenText();
        Assert.Equal(["", "Escape", ">"], screen.Skip(echo + 1).Take(3).Select(row => row.TrimEnd()));
    }

    [Fact]
    public void ASwappedDiscShowsTheOldCatalogueUntilDfsReadsItAgain()
    {
        // s2b: DFS keeps the catalogue it read in RAM and reads it again only when its ready test
        // fails, which it does once the 8271 has unloaded the head (index count 12 from DFS's
        // specify, x 400,000 cycles after the last drive command: 2.4 seconds), when the drive
        // changes, or after a *CAT or some errors, which set $1082, the drive its copy came from,
        // to $FF (DFS $A420, and $9FC0 for errors raised through $9FB8): *CAT sorts its listing in
        // the RAM copy. A disc changed behind its back is not seen before then. This is how DFS
        // behaves on a real machine, not a fault of the model. Each command below takes about
        // three million cycles to type and run, well inside the 2.4 seconds.
        DiscImage first = WithFile("FIRST", "ONLYA"), second = WithFile("SECOND", "ONLYB");
        var s = new BbcSession(mode: 7, disc: first).Boot();
        Assert.StartsWith("$.ONLYA ", Command(s, "*INFO *")[0], StringComparison.Ordinal);

        // Swapped: the cached catalogue, for *CAT as for *INFO.
        s.Machine.Insert(0, second);
        Assert.Equal("FIRST (00)", Command(s, "*CAT")[0]);

        // That *CAT threw its copy away, so the next command reads the new disc.
        Assert.StartsWith("$.ONLYB ", Command(s, "*INFO *")[0], StringComparison.Ordinal);

        // Swapped back: stale again, until the head has unloaded.
        s.Machine.Insert(0, first);
        Assert.StartsWith("$.ONLYB ", Command(s, "*INFO *")[0], StringComparison.Ordinal);
        s.RunFor(12 * Fdc8271.RevolutionCycles);
        Assert.StartsWith("$.ONLYA ", Command(s, "*INFO *")[0], StringComparison.Ordinal);
    }

    /// <summary>A blank disc with a title and one file, its catalogue entry built from the layout (s5a).</summary>
    private static DiscImage WithFile(string title, string name)
    {
        DiscImage disc = DiscImage.Blank(40, false);
        Span<byte> sector0 = disc.Sector(0, 0, 0), sector1 = disc.Sector(0, 0, 1);
        System.Text.Encoding.ASCII.GetBytes(title).CopyTo(sector0);
        System.Text.Encoding.ASCII.GetBytes(name.PadRight(7) + "$").CopyTo(sector0[8..]);
        byte[] entry = [0x00, 0x20, 0x00, 0x20, 0x06, 0x00, 0x00, 0x02];
        entry.CopyTo(sector1[8..]);
        sector1[5] = 8;
        Assert.Equal(name, Assert.Single(DfsCatalogue.Read(disc).Files).Name);
        return disc;
    }

    /// <summary>
    /// Types a command and RETURN, lets it finish, clears the screen first so nothing scrolls, and
    /// returns the rows after the one it was echoed on, ends trimmed.
    /// </summary>
    private static string[] Command(BbcSession s, string command)
    {
        s.Type("CLS\r");
        s.Type(command + "\r").RunFor(Settle);
        int echo = Echo(s, command);
        return s.ScreenText().Skip(echo + 1).Select(row => row.TrimEnd()).ToArray();
    }

    /// <summary>The error a command gives: the row after the blank one REPORT prints first.</summary>
    private static string Error(BbcSession s, string command)
    {
        string[] rows = Command(s, command);
        Assert.Equal("", rows[0]);
        Assert.Equal(">", rows[2]);
        return rows[1];
    }

    /// <summary>The row the command was echoed on after the prompt.</summary>
    private static int Echo(BbcSession s, string command)
    {
        string[] screen = s.ScreenText();
        int row = Array.FindLastIndex(screen, r => r.TrimEnd() == ">" + command);
        Assert.True(row >= 0, $"\"{command}\" is not on the screen:\n{string.Join("\n", screen)}");
        return row;
    }

    private static void Poke(BbcSession s, ushort address, byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            s.Machine.Bus.PokeRam((ushort)(address + i), bytes[i]);
        }
    }

    private static byte[] Peek(BbcSession s, ushort address, int length) =>
        Enumerable.Range(0, length).Select(i => s.Machine.Bus.Peek((ushort)(address + i))).ToArray();

    private static byte[] MakeProgram()
    {
        var bytes = new byte[256];
        new Random(12).NextBytes(bytes);
        byte[] code = [0xA9, 0x41, 0x20, 0xEE, 0xFF, 0x60];
        code.CopyTo(bytes, 0);
        return bytes;
    }
}
