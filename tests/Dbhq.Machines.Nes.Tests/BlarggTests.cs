using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// Blargg's test ROMs, from the fork at the pinned commit. The <c>ppu_vbl_nmi</c> singles: the
/// VBlank flag, NMI and the odd frame, each to one PPU dot. NTSC only, because the readme says
/// they test "the NTSC PPU". The combined ROM is an MMC1 cartridge and runs from task 10; each single
/// is NROM. Then <c>sprite_hit_tests_2005.10.05</c> and <c>sprite_overflow_tests</c> (task 5),
/// every ROM in each folder, all NROM with CHR RAM, which report on the screen. Then the sound unit
/// (task 8): <c>apu_test</c>'s singles 1 to 6 on NTSC, and every <c>pal_apu_tests</c> ROM on PAL.
/// Then the DMC (task 9): <c>apu_test</c> 7 and 8, <c>dmc_dma_during_read4</c>,
/// <c>sprdma_and_dmc_dma</c> and <c>apu_mixer</c>, all NTSC. Then MMC3 (task 11):
/// <c>mmc3_test_2</c>'s singles and <c>mmc3_irq_tests</c>, NTSC, one ROM of each a known failure
/// because it tests the other revision of the chip. Then the rest (task 12): the CPU's own tests,
/// dummy reads and writes, open bus, OAM, the read buffer, power and reset, and the 2005 APU tests,
/// each on the regions its readme or source gives (the journal quotes them).
/// </summary>
public class BlarggTests(ITestOutputHelper output)
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

        // A stop in some other JMP to itself with $F8 at 1 would not have printed this.
        Assert.True(result.Text.Contains("PASSED", StringComparison.Ordinal), $"{pinnedName} stopped with $F8 = 1 but did not print PASSED. Its screen:\n{result.Text}");
    }

    // apu_test's singles: 1 to 6 need the pulses, triangle, noise and frame counter (task 8), and
    // 7-dmc_basics and 8-dmc_rates the DMC (task 9).
    public static TheoryData<string> ApuTestSingles() => new()
    {
        "1-len_ctr",
        "2-len_table",
        "3-irq_flag",
        "4-jitter",
        "5-len_timing",
        "6-irq_flag_timing",
        "7-dmc_basics",
        "8-dmc_rates",
    };

    // Every ROM in pal_apu_tests (the folder has no 09).
    public static TheoryData<string> PalApuTests() => new()
    {
        "01.len_ctr",
        "02.len_table",
        "03.irq_flag",
        "04.clock_jitter",
        "05.len_timing_mode0",
        "06.len_timing_mode1",
        "07.irq_flag_timing",
        "08.irq_timing",
        "10.len_halt_timing",
        "11.len_reload_timing",
    };

    // apu_test's readme does not name a region; its timings (29831 in 6-irq_flag_timing) are the
    // NTSC frame counter's, so it runs on NTSC.
    [Theory]
    [MemberData(nameof(ApuTestSingles))]
    public void EachApuTestSinglePasses(string single)
    {
        BlarggResult result = BlarggRunner.Run($"apu_test/rom_singles/{single}.nes", Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"{single} gave no result in {Budget} cycles (status {result.Status}). Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"{single} reported status {result.Status} after {result.Cycles} cycles. Its text:\n{result.Text}");
    }

    // pal_apu_tests' readme: "These tests verify the PAL APU's frame sequencer timing. They have
    // been tested on a PAL NES". They report as the 2005 ROMs do, on the screen and in $F8.
    [Theory]
    [MemberData(nameof(PalApuTests))]
    public void EachPalApuTestPasses(string rom)
    {
        string pinnedName = $"pal_apu_tests/{rom}.nes";
        BlarggResult result = BlarggRunner.RunScreenReporting(pinnedName, Region.Pal, Budget);

        Assert.False(result.TimedOut, $"{pinnedName} did not finish in {Budget} cycles (on test {result.Status}). Its screen:\n{result.Text}");
        Assert.True(result.Status == 1, $"{pinnedName} failed test {result.Status} after {result.Cycles} cycles. Its screen:\n{result.Text}");
        Assert.True(result.Text.Contains("PASSED", StringComparison.Ordinal), $"{pinnedName} stopped with $F8 = 1 but did not print PASSED. Its screen:\n{result.Text}");
    }

    // dmc_dma_during_read4: the ROMs print what they read and stop in their forever loop. Three
    // check their own CRC and print "Passed"; dma_2007_read prints the CRC for the reader to
    // compare with the two its source lists, one for each CPU and PPU alignment at power on. The
    // sources say "DMC DMA during $2007 read causes 2-3 extra $2007 reads" and "DMC DMA during
    // $4016 read causes extra $4016 read": the 2A03's conflicts, so they run on NTSC.
    public static TheoryData<string, string[]> DmcDmaDuringRead4() => new()
    {
        { "dma_2007_read", ["159A7A8F", "5E3DF9C4"] },
        { "dma_2007_write", ["Passed"] },
        { "dma_4016_read", ["Passed"] },
        { "read_write_2007", ["Passed"] },
    };

    [Theory]
    [MemberData(nameof(DmcDmaDuringRead4))]
    public void EachDmcDmaDuringRead4RomPrintsAnOutputItsSourceAccepts(string rom, string[] accepted)
    {
        string pinnedName = $"dmc_dma_during_read4/{rom}.nes";
        BlarggResult result = BlarggRunner.RunUntilForever(pinnedName, Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"{pinnedName} did not finish in {Budget} cycles. Its screen:\n{result.Text}");
        Assert.True(accepted.Any(a => result.Text.Contains(a, StringComparison.Ordinal)), $"{pinnedName} printed none of {string.Join(", ", accepted)}. Its screen:\n{result.Text}");
    }

    // The known failures: pinned ROMs the model does not pass, each with what it prints now and
    // the cause, which docs/known-differences.md also gives. They run like the others and are not
    // skipped: the test holds the wrong output, so a change that fixes one (or changes how it
    // fails) shows here and the row moves to the table above.
    public static TheoryData<string, string, string[], string> KnownFailures() => new()
    {
        {
            "dmc_dma_during_read4/double_2007_read",
            "D84F6815",
            ["85CFD627", "F018C287", "440EF923", "E52F41A5"],
            "two $2007 reads in adjacent cycles (LDA $20F7,X with X = $10 reads $2007, then $2107) are two whole reads in the PPU model, which fills the read buffer at once, so the second returns what the first fetched (33 44 55 66 77); in all four console outputs the source lists, the second read does not, so the chip fills the buffer some dots later, and no sheet or wiki page gives how many. No DMC in it: a PPU matter"
        },
    };

    [Theory]
    [MemberData(nameof(KnownFailures))]
    public void EachKnownFailureStillFailsAsWrittenDown(string rom, string printsNow, string[] accepted, string cause)
    {
        BlarggResult result = BlarggRunner.RunUntilForever($"{rom}.nes", Region.Ntsc, Budget);
        output.WriteLine($"{rom}: known failure, because {cause}. Its screen:\n{result.Text}");

        Assert.False(result.TimedOut, $"{rom} did not finish in {Budget} cycles. Its screen:\n{result.Text}");
        Assert.False(accepted.Any(a => result.Text.Contains(a, StringComparison.Ordinal)), $"{rom} now passes: move it out of the known failures and its known-differences entry. Its screen:\n{result.Text}");
        Assert.True(result.Text.Contains(printsNow, StringComparison.Ordinal), $"{rom} fails differently from the record ({printsNow}). Its screen:\n{result.Text}");
    }

    // sprdma_and_dmc_dma: OAM DMA against DMC DMA, cycle by cycle, with a CRC of the counts. The
    // ROMs report through $6000 and print "This test is meant for NTSC NES only".
    [Theory]
    [InlineData("sprdma_and_dmc_dma")]
    [InlineData("sprdma_and_dmc_dma_512")]
    public void EachSprdmaAndDmcDmaRomPasses(string rom)
    {
        string pinnedName = $"sprdma_and_dmc_dma/{rom}.nes";
        BlarggResult result = BlarggRunner.Run(pinnedName, Region.Ntsc, 4 * Budget);

        Assert.False(result.TimedOut, $"{pinnedName} gave no result. Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"{pinnedName} reported status {result.Status}. Its text:\n{result.Text}");
        Assert.True(result.Text.Contains("Passed", StringComparison.Ordinal), $"{pinnedName} did not print Passed. Its text:\n{result.Text}");
    }

    // apu_mixer: each ROM plays a tone on the channel under test and the inverse wave on the DMC's
    // DAC, which cancel to near silence if the mixer is right (the fork's apu_mixer/readme.txt).
    // The ROM cannot hear itself, so it ends with 0 whatever it played; the test listens. Its tone
    // is the short tone's pitch, a period of 1792 cycles (896 x 2 in each source), so the test
    // measures that pitch in the machine's own audio, block by block: the two short tones are the
    // loud blocks, and between them, the test, the tone must be far quieter.
    public static TheoryData<string> ApuMixerRoms() => new() { "dmc", "noise", "square", "triangle" };

    // How far under the short tone the cancelled test must stay, in its 90th percentile block.
    // With the sheet's formulas the four ROMs gave -32.2 (noise) to -38.5 dB; with the APU Mixer
    // page's linear approximation put in for one run, -9.5 to -25.7 dB (the journal, task 9).
    private const double ApuMixerQuiet = -30;

    [Theory]
    [MemberData(nameof(ApuMixerRoms))]
    public void EachApuMixerRomCancelsItsToneToNearSilence(string rom)
    {
        // The readme: "Tests MUST be run from a freshly-powered NES". Its timings are NTSC's.
        (BlarggResult result, double[] blocks) = BlarggRunner.RunListening($"apu_mixer/{rom}.nes", Region.Ntsc, 1792, 70_000_000);
        Assert.False(result.TimedOut, $"apu_mixer/{rom} gave no result. Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"apu_mixer/{rom} reported status {result.Status}. Its text:\n{result.Text}");

        // The loud blocks are the two short tones, 300 ms each: the first run of them and the last.
        double loudest = blocks.Max();
        int[] loud = [.. Enumerable.Range(0, blocks.Length).Where(b => blocks[b] > loudest / 2)];
        int firstToneEnd = loud[0];
        foreach (int b in loud.Where(b => b <= firstToneEnd + 2))
        {
            firstToneEnd = Math.Max(firstToneEnd, b);
        }

        int secondToneStart = loud[^1];
        foreach (int b in loud.Reverse().Where(b => b >= secondToneStart - 2))
        {
            secondToneStart = Math.Min(secondToneStart, b);
        }

        Assert.True(secondToneStart - firstToneEnd > 100, $"apu_mixer/{rom}: the two short tones were not found apart");

        // Leave out the 300 ms of silence each side of the test, which the tones ring into. The
        // figure is the block nine in ten of the test's blocks are quieter than: the noise ROM's
        // loudest blocks are where its volume steps, and they are as loud with either mixer.
        const int margin = 20;
        double tone = loud.Select(b => blocks[b]).Order().ElementAt(loud.Length / 2);
        double[] test = [.. blocks[(firstToneEnd + margin)..(secondToneStart - margin)].Select(b => 20 * Math.Log10(b / tone)).Order()];
        double ninetieth = test[(int)(test.Length * 0.9)];
        output.WriteLine($"apu_mixer/{rom}: over {test.Length} blocks of the test, against the short tone: 90th percentile {ninetieth:F1} dB, median {test[test.Length / 2]:F1} dB, loudest {test[^1]:F1} dB");

        Assert.True(ninetieth < ApuMixerQuiet, $"apu_mixer/{rom}: the tone during the test is {ninetieth:F1} dB against the short tone (90th percentile block)");
    }

    // The combined ROMs, each every one of its singles in one MMC1 cartridge. Each is run from
    // power on to its report through $6000: status 0 and the text "All N tests passed" its shell
    // prints at the end. Task 10 added the first two; task 12 added official_only, every
    // instr_test-v5 single's official instructions in one ROM, on both regions as the singles, and
    // instr_timing's and cpu_interrupts_v2's, NTSC as theirs (all_instrs is a known failure). The
    // fourth column is the budget in millions of cycles, a hang guard about three times what the
    // ROM needed (the journal, tasks 10 and 12).
    public static TheoryData<string, string, Region, int> CombinedMmc1Roms() => new()
    {
        { "ppu_vbl_nmi/ppu_vbl_nmi.nes", "All 10 tests passed", Region.Ntsc, 150 },
        { "apu_test/apu_test.nes", "All 8 tests passed", Region.Ntsc, 72 },
        { "instr_test-v5/official_only.nes", "All 16 tests passed", Region.Ntsc, 180 },
        { "instr_test-v5/official_only.nes", "All 16 tests passed", Region.Pal, 180 },
        { "instr_timing/instr_timing.nes", "All 2 tests passed", Region.Ntsc, 120 },
        { "cpu_interrupts_v2/cpu_interrupts.nes", "All 5 tests passed", Region.Ntsc, 72 },
    };

    [Theory]
    [MemberData(nameof(CombinedMmc1Roms))]
    public void EachCombinedMmc1RomPasses(string pinnedName, string expected, Region region, int millions)
    {
        long budget = millions * 1_000_000L;
        BlarggResult result = BlarggRunner.Run(pinnedName, region, budget);
        output.WriteLine($"{pinnedName} on {region.Name}: status {result.Status} after {result.Cycles} cycles");

        Assert.False(result.TimedOut, $"{pinnedName} gave no result in {budget} cycles (status {result.Status}). Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"{pinnedName} reported status {result.Status} after {result.Cycles} cycles. Its text:\n{result.Text}");
        Assert.True(result.Text.Contains(expected, StringComparison.Ordinal), $"{pinnedName} did not print {expected}. Its text:\n{result.Text}");
    }

    // Task 12's ROMs that report through $6000, each with the region it runs on and its budget in
    // millions of cycles (a hang guard, about three times what it needed; the journal, task 12).
    // instr_test-v5's singles build with REGION_FREE, so they run on both, except 01-basics and
    // 16-special, which do not and print "This test is meant for NTSC NES only" on PAL. 03-immediate
    // is in RamReportingKnownFailures. instr_timing and cpu_interrupts_v2 time against the NTSC frame
    // counter (29830 in their sources; cpu_interrupts_v2 builds NTSC_ONLY), and apu_reset builds
    // NTSC_ONLY: NTSC. cpu_reset, cpu_dummy_writes, ppu_open_bus, oam_read, oam_stress and
    // ppu_read_buffer name no region and their sources hold no frame timing: both. The cpu_reset
    // and apu_reset ROMs ask for the reset button, which the runner presses.
    public static TheoryData<string, Region, int> RamReportingRoms()
    {
        var data = new TheoryData<string, Region, int>();
        string[] regionFree =
        [
            "02-implied", "04-zero_page", "05-zp_xy", "06-absolute", "07-abs_xy", "08-ind_x", "09-ind_y",
            "10-branches", "11-stack", "12-jmp_jsr", "13-rts", "14-rti", "15-brk",
        ];
        data.Add("instr_test-v5/rom_singles/01-basics.nes", Region.Ntsc, 30);
        foreach (string single in regionFree)
        {
            data.Add($"instr_test-v5/rom_singles/{single}.nes", Region.Ntsc, 30);
            data.Add($"instr_test-v5/rom_singles/{single}.nes", Region.Pal, 30);
        }

        data.Add("instr_test-v5/rom_singles/16-special.nes", Region.Ntsc, 30);
        data.Add("instr_timing/rom_singles/1-instr_timing.nes", Region.Ntsc, 90);
        data.Add("instr_timing/rom_singles/2-branch_timing.nes", Region.Ntsc, 18);
        foreach (string single in new[] { "1-cli_latency", "2-nmi_and_brk", "3-nmi_and_irq", "4-irq_and_dma", "5-branch_delays_irq" })
        {
            data.Add($"cpu_interrupts_v2/rom_singles/{single}.nes", Region.Ntsc, 36);
        }

        foreach (string rom in new[] { "4015_cleared", "4017_timing", "4017_written", "irq_flag_cleared", "len_ctrs_enabled", "works_immediately" })
        {
            data.Add($"apu_reset/{rom}.nes", Region.Ntsc, 18);
        }

        foreach (Region region in new[] { Region.Ntsc, Region.Pal })
        {
            data.Add("cpu_reset/ram_after_reset.nes", region, 18);
            data.Add("cpu_reset/registers.nes", region, 18);
            data.Add("cpu_dummy_writes/cpu_dummy_writes_oam.nes", region, 36);
            data.Add("cpu_dummy_writes/cpu_dummy_writes_ppumem.nes", region, 36);
            data.Add("ppu_open_bus/ppu_open_bus.nes", region, 36);
            data.Add("oam_read/oam_read.nes", region, 18);
            data.Add("oam_stress/oam_stress.nes", region, 150);
            data.Add("ppu_read_buffer/test_ppu_read_buffer.nes", region, 120);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RamReportingRoms))]
    public void EachRamReportingRomPasses(string pinnedName, Region region, int millions)
    {
        long budget = millions * 1_000_000L;
        BlarggResult result = BlarggRunner.Run(pinnedName, region, budget);
        output.WriteLine($"{pinnedName} on {region.Name}: status {result.Status} after {result.Cycles} cycles");

        Assert.False(result.TimedOut, $"{pinnedName} gave no result in {budget} cycles (status {result.Status}). Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"{pinnedName} reported status {result.Status} after {result.Cycles} cycles. Its text:\n{result.Text}");
    }

    // The known failures among the ROMs that report through $6000. Each runs as the others do and
    // is not skipped: the test holds the status and text it gives now, so a change shows. Columns:
    // the ROM, the region, the status now, text it prints now, the budget in millions of cycles,
    // and the cause, which docs/known-differences.md also gives.
    public static TheoryData<string, Region, int, string, int, string> RamReportingKnownFailures()
    {
        const string lxa = "opcode $AB (LXA, which the ROM calls ATX #n) is A = X = (A | magic) AND the operand, with a magic constant that differs between chips; the core takes $EE from Harte's nes6502 data, which its own tests pin, and the ROM's checksum, made on a console, is matched by $FF (tried once in task 12: $FF passes, $00 fails)";
        var data = new TheoryData<string, Region, int, string, int, string>();
        foreach (Region region in new[] { Region.Ntsc, Region.Pal })
        {
            data.Add("instr_test-v5/rom_singles/03-immediate.nes", region, 1, "AB ATX #n", 30, lxa);
            data.Add("instr_test-v5/all_instrs.nes", region, 1, "AB ATX #n", 180, lxa);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RamReportingKnownFailures))]
    public void EachRamReportingKnownFailureStillFailsAsWrittenDown(string pinnedName, Region region, int statusNow, string printsNow, int millions, string cause)
    {
        long budget = millions * 1_000_000L;
        BlarggResult result = BlarggRunner.Run(pinnedName, region, budget);
        output.WriteLine($"{pinnedName}: known failure, because {cause}. Its text:\n{result.Text}");

        Assert.False(result.TimedOut, $"{pinnedName} gave no result in {budget} cycles. Its text:\n{result.Text}");
        Assert.False(result.Status == 0, $"{pinnedName} now passes: move it out of the known failures and its known-differences entry. Its text:\n{result.Text}");
        Assert.True(result.Status == statusNow && result.Text.Contains(printsNow, StringComparison.Ordinal), $"{pinnedName} fails differently from the record (status {statusNow}, {printsNow}): status {result.Status}. Its text:\n{result.Text}");
    }

    // branch_timing_tests (task 12): the 2005 shell, the result in $F8 and on the screen. Its first
    // ROM times the PPU's NMI period, the NTSC frame's, so the three run NTSC; the readme says they
    // must run, and pass, in order, which the rows keep.
    [Theory]
    [InlineData("1.Branch_Basics")]
    [InlineData("2.Backward_Branch")]
    [InlineData("3.Forward_Branch")]
    public void EachBranchTimingTestPasses(string rom)
    {
        AssertScreenReportingRomPasses($"branch_timing_tests/{rom}.nes");
    }

    // blargg_apu_2005.07.30 (task 12), the NTSC edition of pal_apu_tests: its tests.txt says each
    // reports "a result code on screen", 1 when every test passed. The shell prints the code as
    // "$01" and stops in its forever loop. NTSC, as its figures (29830 and 14915) are. 10 and 11
    // check the length counter's write timing that task 8 applied to NTSC with no ROM to check it.
    public static TheoryData<string> BlarggApu2005() => new()
    {
        "01.len_ctr",
        "02.len_table",
        "03.irq_flag",
        "04.clock_jitter",
        "05.len_timing_mode0",
        "06.len_timing_mode1",
        "07.irq_flag_timing",
        "08.irq_timing",
        "09.reset_timing",
        "10.len_halt_timing",
        "11.len_reload_timing",
    };

    [Theory]
    [MemberData(nameof(BlarggApu2005))]
    public void EachBlarggApu2005RomPrintsResultCode1(string rom)
    {
        string pinnedName = $"blargg_apu_2005.07.30/{rom}.nes";
        BlarggResult result = BlarggRunner.RunScreenReporting(pinnedName, Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"{pinnedName} did not finish in {Budget} cycles. Its screen:\n{result.Text}");
        Assert.True(result.Text == "$01", $"{pinnedName} printed a result code other than $01. Its screen:\n{result.Text}");
    }

    // cpu_dummy_reads (task 12), CNROM, prints on the screen only: its name, then nothing more on a
    // pass, "Failed" or "Error n" otherwise, and stops in its forever loop. Its source waits 29800
    // cycles after the VBlank flag to read $2002 just before the next one, the NTSC frame: NTSC.
    [Fact]
    public void CpuDummyReadsPasses()
    {
        BlarggResult result = BlarggRunner.RunUntilForever("cpu_dummy_reads/cpu_dummy_reads.nes", Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"cpu_dummy_reads did not finish in {Budget} cycles. Its screen:\n{result.Text}");
        Assert.Equal("cpu_dummy_reads\nPassed", result.Text);
    }

    // mmc3_test_2's singles (task 11), mapper 4, reporting through $6000. The readme names no
    // region; the timing test's figures are NTSC's (6976 PPU clocks from the VBlank flag to line
    // 0's IRQ, 341 a line), and mmc3_irq_tests, the same tests before, says "an NTSC NES PPU".
    // 5-MMC3 tests the Sharp ("new") chip at latch 0, the model's; 6-MMC3_alt tests the other
    // chip and is in Mmc3OtherRevision below.
    public static TheoryData<string> Mmc3Test2Singles() => new()
    {
        "1-clocking",
        "2-details",
        "3-A12_clocking",
        "4-scanline_timing",
        "5-MMC3",
    };

    [Theory]
    [MemberData(nameof(Mmc3Test2Singles))]
    public void EachMmc3Test2SinglePasses(string single)
    {
        BlarggResult result = BlarggRunner.Run($"mmc3_test_2/rom_singles/{single}.nes", Region.Ntsc, Budget);

        Assert.False(result.TimedOut, $"{single} gave no result in {Budget} cycles (status {result.Status}). Its text:\n{result.Text}");
        Assert.True(result.Status == 0, $"{single} reported status {result.Status} after {result.Cycles} cycles. Its text:\n{result.Text}");
    }

    // mmc3_irq_tests (task 11), the older build of the same tests, mapper 4 with CHR RAM, which
    // report on the screen and in $F8 as the 2005 ROMs do. Its readme: "The last two ROMs test
    // different revisions of the MMC3, so at most only one will pass on a particular emulator".
    // 6.MMC3_rev_B is the Sharp chip's (Super Mario Bros. 3, Mega Man 3); 5.MMC3_rev_A is in
    // Mmc3OtherRevision below.
    public static TheoryData<string> Mmc3IrqTests() => new()
    {
        "1.Clocking",
        "2.Details",
        "3.A12_clocking",
        "4.Scanline_timing",
        "6.MMC3_rev_B",
    };

    [Theory]
    [MemberData(nameof(Mmc3IrqTests))]
    public void EachMmc3IrqTestPasses(string rom)
    {
        AssertScreenReportingRomPasses($"mmc3_irq_tests/{rom}.nes");
    }

    // The known failures of the MMC3 ROMs: the two that test the chip the model is not. The model
    // is the Sharp ("new") MMC3, which raises the IRQ whenever a clock leaves the counter at 0; the
    // other chip (the readmes' "revision A", Crystalis's) raises it only when the counter changes
    // to 0 or is reloaded by request (mappers.md 6). Each runs as the other tables do and is not
    // skipped: the test holds what it reports now, so a change shows. Columns: the ROM, whether it
    // reports on the screen ($F8) or through $6000, the status it gives now, text it prints now,
    // and the cause, which docs/known-differences.md also gives.
    public static TheoryData<string, bool, int, string, string> Mmc3OtherRevision() => new()
    {
        {
            "mmc3_test_2/rom_singles/6-MMC3_alt",
            false,
            2,
            "IRQ shouldn't be set when reloading to 0 due to counter naturally reaching 0 previously",
            "both its sub-tests are the other chip's latch-0 rule, and it stops at its first failure, test 2: with the counter run down to 0 and the latch set to 0, that chip raises no IRQ on the clocks that follow, and the Sharp chip modelled raises one on each, as 5-MMC3 asks; its test 3 never runs"
        },
        {
            "mmc3_irq_tests/5.MMC3_rev_A",
            true,
            3,
            "FAILED #3",
            "its test 3, \"IRQ shouldn't occur when reloading after counter normally reaches 0\", is the other chip's rule, the opposite of 6.MMC3_rev_B's test 2, which passes; its test 2, which both chips share, passes"
        },
    };

    [Theory]
    [MemberData(nameof(Mmc3OtherRevision))]
    public void EachMmc3RomOfTheOtherRevisionStillFailsAsWrittenDown(string rom, bool screen, int statusNow, string printsNow, string cause)
    {
        string pinnedName = $"{rom}.nes";
        BlarggResult result = screen
            ? BlarggRunner.RunScreenReporting(pinnedName, Region.Ntsc, Budget)
            : BlarggRunner.Run(pinnedName, Region.Ntsc, Budget);
        output.WriteLine($"{rom}: known failure, because {cause}. Its text:\n{result.Text}");

        Assert.False(result.TimedOut, $"{rom} gave no result in {Budget} cycles. Its text:\n{result.Text}");
        Assert.False(result.Status == (screen ? 1 : 0), $"{rom} now passes: move it out of the known failures and its known-differences entry. Its text:\n{result.Text}");
        Assert.True(result.Status == statusNow && result.Text.Contains(printsNow, StringComparison.Ordinal), $"{rom} fails differently from the record (status {statusNow}, {printsNow}): status {result.Status}. Its text:\n{result.Text}");
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
