using System.Text;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The TIA's two sound channels, from <c>docs/atari-2600/facts/tia.md</c> section 18: each
/// <c>AUDC</c> value's output from the sheet's starting state (all nine bits 1, a choice), the
/// divider, the volume and the mix, and when the channels are clocked. First the channels alone
/// (<see cref="TiaSound"/>), then inside a <see cref="Tia"/> driven a colour clock at a time.
/// </summary>
public class TiaSoundTests
{
    private const int Rsync = 0x03;
    private const int Audc0 = 0x15;
    private const int Audc1 = 0x16;
    private const int Audf0 = 0x17;
    private const int Audf1 = 0x18;
    private const int Audv0 = 0x19;
    private const int Audv1 = 0x1A;
    private const int Hmove = 0x2A;

    /// <summary>tia.md section 18.5's table: the period in pulses, the 1s in one period, and the first 64 output bits.</summary>
    public static TheoryData<int, int, int, string> Section18_5() => new()
    {
        { 0x0, 1, 1, "1111111111111111111111111111111111111111111111111111111111111111" },
        { 0x1, 15, 8, "1110001001101011110001001101011110001001101011110001001101011110" },
        { 0x2, 465, 248, "1111111111111111111111111111111111111111111111100000000000000000" },
        { 0x3, 465, 248, "1110000001100111100000111001111110000001000111001111100011111110" },
        { 0x4, 2, 1, "1110101010101010101010101010101010101010101010101010101010101010" },
        { 0x5, 2, 1, "1110101010101010101010101010101010101010101010101010101010101010" },
        { 0x6, 31, 13, "1111111111111111111111111111111111111111111111100000000000000000" },
        { 0x7, 31, 15, "1110111101101001100000111001000101011110110100110000011100100010" },
        { 0x8, 511, 256, "1111111100000111101111100010111001100100000100101001110110100011" },
        { 0x9, 31, 16, "1111111100011011101010000100101100111110001101110101000010010110" },
        { 0xA, 31, 18, "1111111111111111111111111111111111111111111111111111111111111111" },
        { 0xB, 1, 1, "1111111111111111111111111111111111111111111111111111111111111111" },
        { 0xC, 6, 3, "1110001110001110001110001110001110001110001110001110001110001110" },
        { 0xD, 6, 3, "1110001110001110001110001110001110001110001110001110001110001110" },
        { 0xE, 93, 44, "1111111111111111111111111111111111111111111111100000000000000000" },
        { 0xF, 93, 46, "1110000001111000000000111111000001111110000111110000000000111110" },
    };

    [Theory]
    [MemberData(nameof(Section18_5))]
    public void Section_18_5_each_AUDC_value_gives_the_sheets_first_64_bits_and_period(int audc, int period, int ones, string first64)
    {
        // AUDF = 0, so each audio step is one divided pulse; channel 0, from power on.
        var sound = new TiaSound();
        var bits = new StringBuilder();
        var sequence = new List<int>();
        for (int step = 0; step < 3000; step++)
        {
            sound.Step((byte)audc, 0, 0, 0);
            sequence.Add(sound.Output0);
            if (step < 64)
            {
                bits.Append(sound.Output0);
            }
        }

        Assert.Equal(first64, bits.ToString());

        // Past the start-up (600 pulses, longer than any), the shortest repeat and its 1s.
        List<int> tail = sequence[600..];
        int found = Enumerable.Range(1, 600).First(p => Enumerable.Range(0, tail.Count - p).All(i => tail[i] == tail[i + p]));
        Assert.Equal(period, found);
        Assert.Equal(ones, tail.Take(period).Sum());
    }

    [Theory]
    [MemberData(nameof(Section18_5))]
    public void Section_18_5_channel_1_runs_the_same_from_its_own_registers(int audc, int period, int ones, string first64)
    {
        _ = period;
        _ = ones;
        var sound = new TiaSound();
        var bits = new StringBuilder();
        for (int step = 0; step < 64; step++)
        {
            sound.Step(0x4, 31, (byte)audc, 0);
            bits.Append(sound.Output1);
        }

        Assert.Equal(first64, bits.ToString());
    }

    [Theory]
    [InlineData(0b101)]
    [InlineData(0b010)]
    public void Section_18_4_the_divide_by_6_leaves_its_two_unused_states_within_two_pulses(int unused)
    {
        // AUDC $C: the four-bit counter's first three stages count 111, 011, 001, 000, 100, 110
        // (bit 5 first), three 1s then three 0s. %101 and %010 (read with bit 5 least significant)
        // are not on that cycle, and without the escape each leads to the other for ever. From
        // either, within two pulses, the stages are on the cycle and the output repeats every 6.
        int bits = 0x1F | (unused << 5);
        for (int pulse = 0; pulse < 2; pulse++)
        {
            bits = TiaSound.Pulse(bits, 0xC);
        }

        Assert.DoesNotContain((bits >> 5) & 7, new[] { 0b101, 0b010 });
        var outputs = new List<int>();
        for (int pulse = 0; pulse < 18; pulse++)
        {
            bits = TiaSound.Pulse(bits, 0xC);
            outputs.Add(bits >> 8);
        }

        for (int i = 6; i < 18; i++)
        {
            Assert.Equal(outputs[i - 6], outputs[i]);
        }

        Assert.Equal(3, outputs.Take(6).Sum());
    }

    [Theory]
    [InlineData(0x1, 1)]
    [InlineData(0x4, 2)]
    [InlineData(0x8, 3)]
    [InlineData(0xC, 15)]
    [InlineData(0x4, 31)]
    public void Section_18_3_AUDF_divides_the_audio_steps_by_its_value_plus_one(int audc, int audf)
    {
        // The output after k steps at AUDF f is the output after floor(k / (f + 1)) pulses at AUDF 0
        // (the divider starts at 0, a choice, section 18.3).
        var undivided = new TiaSound();
        var outputs = new List<int> { undivided.Output0 };
        for (int pulse = 0; pulse < 200; pulse++)
        {
            undivided.Step((byte)audc, 0, 0, 0);
            outputs.Add(undivided.Output0);
        }

        var divided = new TiaSound();
        for (int k = 1; k <= 200 * (audf + 1) - 1; k++)
        {
            divided.Step((byte)audc, (byte)audf, 0, 0);
            Assert.Equal(outputs[k / (audf + 1)], divided.Output0);
        }
    }

    [Fact]
    public void Section_18_6_counter_at_20_then_AUDF_5_pulses_18_steps_later()
    {
        // AUDC 4 toggles on every pulse once its start-up has passed (section 18.5), so a toggle is
        // a pulse. Ten pulses at AUDF 0, then 20 steps at AUDF 31 bring the counter to 20 with no
        // pulse; then AUDF 5. The counter runs 21 to 31 and 0 to 5, reaching 5 on the 17th step,
        // and the pulse comes on the step after (jigo2600 Sound's sequence; section 18.6).
        var sound = new TiaSound();
        for (int i = 0; i < 10; i++)
        {
            sound.Step(0x4, 0, 0, 0);
        }

        int before = sound.Output0;
        for (int i = 0; i < 20; i++)
        {
            sound.Step(0x4, 31, 0, 0);
            Assert.Equal(before, sound.Output0);
        }

        Assert.Equal(20, sound.Divider0);
        int steps = 0;
        do
        {
            sound.Step(0x4, 5, 0, 0);
            steps++;
        }
        while (sound.Output0 == before && steps < 100);

        Assert.Equal(18, steps);
        Assert.Equal(0, sound.Divider0);
    }

    [Fact]
    public void Section_18_4_power_on_is_all_nine_bits_1_and_the_dividers_at_0_the_models_choice()
    {
        var sound = new TiaSound();
        for (int i = 0; i < 37; i++)
        {
            sound.Step(0x8, 3, 0x3, 7);
        }

        sound.PowerOn();
        Assert.Equal(TiaSound.PowerOnBits, sound.Bits0);
        Assert.Equal(TiaSound.PowerOnBits, sound.Bits1);
        Assert.Equal(0x1FF, TiaSound.PowerOnBits);
        Assert.Equal(0, sound.Divider0);
        Assert.Equal(0, sound.Divider1);
        Assert.Equal(1, sound.Output0);
        Assert.Equal(1, sound.Output1);
    }

    [Theory]
    [InlineData(15, 1, 0, 1, 15)]   // AUDV0 15 with bit 1, AUDV1 0: 15, output 0.5
    [InlineData(15, 1, 15, 1, 30)]  // both 15 with both bits 1: 30, output 1.0
    [InlineData(6, 1, 9, 0, 6)]     // AUDV0 6 with bit 1, AUDV1 9 with bit 0: 6, output 0.2
    [InlineData(0, 1, 0, 1, 0)]     // volume 0 is silent
    [InlineData(15, 0, 15, 0, 0)]   // both bits 0
    [InlineData(0xF7, 1, 0xF3, 1, 10)]   // AUDV's top four bits are not used (section 2.1)
    public void Section_18_7_the_mix_is_the_sum_of_each_volume_its_bit_lets_through(int audv0, int bit0, int audv1, int bit1, int sum)
    {
        Assert.Equal(sum, TiaSound.Mix(bit0, (byte)audv0, bit1, (byte)audv1));
        Assert.Equal(sum / 30.0, TiaSound.Level(sum));
    }

    [Fact]
    public void Section_18_7_volume_0_is_silent_for_every_AUDC_and_15_is_the_most()
    {
        for (int audc = 0; audc < 16; audc++)
        {
            var tia = new Tia();
            tia.Write(Audc0, (byte)audc);
            tia.Write(Audc1, (byte)(15 - audc));
            tia.Write(Audf0, 1);
            double loudest = 0;
            for (int clock = 0; clock < 228 * 64; clock++)
            {
                tia.Tick();
                Assert.Equal(0.0, tia.SoundLevel);
            }

            tia.Write(Audv0, 15);
            tia.Write(Audv1, 15);
            for (int clock = 0; clock < 228 * 64; clock++)
            {
                tia.Tick();
                loudest = Math.Max(loudest, tia.SoundLevel);
                Assert.InRange(tia.SoundLevel, 0.0, 1.0);
            }

            Assert.True(loudest > 0, $"AUDC {audc:X} at volume 15 never sounded");
        }
    }

    [Theory]
    [InlineData(Audc0, Audv0)]
    [InlineData(Audc1, Audv1)]
    public void Section_18_2_the_channels_step_at_colour_clocks_36_and_148_of_every_line(int audc, int audv)
    {
        var tia = new Tia();
        tia.Write(audc, 0x4);
        tia.Write(audv, 15);
        var changes = new List<(int Line, int Clock)>();
        int line = 0;
        for (int clock = 0; clock < 228 * 12; clock++)
        {
            double before = tia.SoundLevel;
            tia.Tick();
            if (tia.HorizontalCounter == 0)
            {
                line++;
            }

            if (tia.SoundLevel != before)
            {
                changes.Add((line, tia.HorizontalCounter));
            }
        }

        // AUDC 4 gives 1, 1, 1, then toggles on every step (section 18.5): from its third step on,
        // the level changes at both clocks of every line, and nowhere else.
        Assert.All(changes, c => Assert.Contains(c.Clock, new[] { 36, 148 }));
        for (int l = 2; l < 12; l++)
        {
            Assert.Contains((l, 36), changes);
            Assert.Contains((l, 148), changes);
        }
    }

    [Fact]
    public void Section_18_2_RSYNC_moves_the_audio_clocks_with_the_line_and_HMOVE_does_not()
    {
        // Recorded from line 2, past AUDC 4's start-up, through the RSYNC on line 3 and the HMOVE
        // on line 5, to the end of line 8, so both writes fall inside what is watched. (Corrected
        // by task 10's review: the first version began watching at line 6, after both writes, so
        // a fault that dropped the steps on an HMOVE line still passed.)
        var tia = new Tia();
        tia.Write(Audc0, 0x4);
        tia.Write(Audv0, 15);
        int line = 0;
        while (line < 2)
        {
            tia.Tick();
            if (tia.HorizontalCounter == 0)
            {
                line++;
            }
        }

        var changes = new List<(int Line, int Clock)>();
        while (line < 9)
        {
            if (line == 3 && tia.HorizontalCounter == 100)
            {
                tia.Write(Rsync, 0);
            }
            else if (line == 5 && tia.HorizontalCounter == 8)
            {
                tia.Write(Hmove, 0);
            }

            double before = tia.SoundLevel;
            tia.Tick();
            if (tia.HorizontalCounter == 0)
            {
                line++;
            }
            else if (tia.SoundLevel != before)
            {
                changes.Add((line, tia.HorizontalCounter));
            }
        }

        // Every step is a toggle (AUDC 4 past its start-up), at colour clock 36 or 148 of its line
        // and nowhere else. RSYNC on line 3 ends that line at about colour clock 103, before its
        // step at 148, and the next line steps at 36 and 148 of its own count; the HMOVE line steps
        // at both, as every line does.
        Assert.All(changes, c => Assert.Contains(c.Clock, new[] { 36, 148 }));
        Assert.Contains((3, 36), changes);
        Assert.DoesNotContain((3, 148), changes);
        foreach (int l in new[] { 2, 4, 5, 6, 7, 8 })
        {
            Assert.Contains((l, 36), changes);
            Assert.Contains((l, 148), changes);
        }

        Assert.Equal(13, changes.Count);
    }

    [Fact]
    public void Section_2_1_the_audio_registers_use_their_low_bits_only()
    {
        // AUDC uses D3 to D0, AUDF D4 to D0 and AUDV D3 to D0 (tia.md section 2.1): the same
        // values with the other bits set make the same sound, clock for clock.
        var plain = new Tia();
        var high = new Tia();
        plain.Write(Audc0, 0x08);
        plain.Write(Audf0, 0x03);
        plain.Write(Audv0, 0x09);
        plain.Write(Audc1, 0x0C);
        plain.Write(Audf1, 0x1E);
        plain.Write(Audv1, 0x06);
        high.Write(Audc0, 0xF8);
        high.Write(Audf0, 0xE3);
        high.Write(Audv0, 0xF9);
        high.Write(Audc1, 0xFC);
        high.Write(Audf1, 0xFE);
        high.Write(Audv1, 0xF6);
        for (int clock = 0; clock < 228 * 300; clock++)
        {
            plain.Tick();
            high.Tick();
            Assert.Equal(plain.SoundLevel, high.SoundLevel);
        }
    }

    [Fact]
    public void A_volume_write_changes_the_level_from_the_clock_it_lands_on()
    {
        // AUDC 0 holds the output at 1 (section 18.5), so the level is the volume alone. The
        // volume drives the pad directly (section 18.7), so the model takes a write at once and
        // not at the next audio step, which is what lets a program play samples by writing AUDV
        // [guessing - verify].
        var scene = new TiaScene();
        scene.Write(Audc0, 0x0);
        scene.RunTo(1, 100);
        Assert.Equal(0.0, scene.Tia.SoundLevel);
        scene.Write(Audv0, 15);
        Assert.Equal(0.5, scene.Tia.SoundLevel);
        scene.Write(Audv0, 6);
        Assert.Equal(0.2, scene.Tia.SoundLevel, 12);
        scene.Write(Audv1, 9);
        Assert.Equal(0.5, scene.Tia.SoundLevel, 12);
    }

    [Fact]
    public void Power_on_silences_the_TIA_and_restarts_its_channels()
    {
        // Channel 0 plays noise; channel 1 holds a level, AUDC 0 keeping its bit at 1.
        var tia = new Tia();
        tia.Write(Audc0, 0x8);
        tia.Write(Audv0, 15);
        tia.Write(Audc1, 0x0);
        tia.Write(Audv1, 15);
        for (int clock = 0; clock < 228 * 9; clock++)
        {
            tia.Tick();
        }

        Assert.True(tia.SoundLevel >= 0.5);
        Assert.NotEqual(TiaSound.PowerOnBits, tia.Audio.Bits0);
        tia.PowerOn();
        Assert.Equal(0.0, tia.SoundLevel);
        Assert.Equal(TiaSound.PowerOnBits, tia.Audio.Bits0);
        Assert.Equal(0, tia.Audio.Divider0);
    }
}
