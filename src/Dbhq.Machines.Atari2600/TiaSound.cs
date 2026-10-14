namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The TIA's two sound channels: each a five-bit frequency divider and a nine-bit noise-tone
/// generator, a five-bit and a four-bit polynomial counter that its <c>AUDC</c> value wires
/// together, whose last bit is the channel's output. Built from <c>docs/atari-2600/facts/tia.md</c>
/// section 18, whose logic is jigo2600's written reading of the TIA schematic's sheet 4, restated
/// there; no emulator's code.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Step"/> is one audio step, the audio clock's phase two, which the <see cref="Tia"/>
/// makes twice a line, at colour clocks 36 and 148 (section 18.2). Nothing here runs on the colour
/// clocks between, so the sound costs two calls a line.
/// </para>
/// <para>
/// <b>The divider</b> (section 18.3): on each step the counter is compared with <c>AUDF</c>; if they
/// match, the step makes a divided pulse and the counter goes to 0, and otherwise it counts on,
/// modulo 32. So the pulse comes every <c>AUDF</c> + 1 steps, and a write that puts <c>AUDF</c>
/// under the count lets the counter run on through 31 first. The compare is made at the step
/// itself: jigo2600's reading puts a latch, sampled on the audio clock's other phase, between the
/// compare and the pulse, which is not modelled [guessing - verify].
/// </para>
/// <para>
/// <b>The noise-tone generator</b> (section 18.4): on each divided pulse every new bit is worked
/// out from the bits before the pulse, and all nine change together. Bits 0 to 4 are the five-bit
/// counter, bit 0 the newest; bits 5 to 8 the four-bit counter, bit 5 the newest; bit 8 is the
/// output.
/// </para>
/// <para>
/// <b>Power on</b> is not documented (tia.md open item 13): the model starts each channel with all
/// nine bits 1, the state a program reaches by holding <c>AUDC</c> at 0, and its divider at 0
/// [guessing - verify].
/// </para>
/// </remarks>
public sealed class TiaSound : IReportsState
{
    /// <summary>The nine bits at power on, all 1 (a choice: tia.md section 18.4).</summary>
    public const int PowerOnBits = 0x1FF;

    /// <summary>The largest mix, both volumes 15 with both bits 1.</summary>
    public const int LoudestMix = 30;

    private int _bits0;
    private int _bits1;
    private int _divider0;
    private int _divider1;

    /// <summary>Two channels at their power-on state.</summary>
    public TiaSound() => PowerOn();

    /// <summary>Channel 0's output bit, 0 or 1.</summary>
    public int Output0 => _bits0 >> 8;

    /// <summary>Channel 1's output bit, 0 or 1.</summary>
    public int Output1 => _bits1 >> 8;

    /// <summary>Channel 0's nine bits, bit 0 the five-bit counter's newest and bit 8 the output.</summary>
    internal int Bits0 => _bits0;

    /// <summary>Channel 1's nine bits.</summary>
    internal int Bits1 => _bits1;

    /// <summary>Channel 0's divider count, 0 to 31.</summary>
    internal int Divider0 => _divider0;

    /// <summary>Channel 1's divider count.</summary>
    internal int Divider1 => _divider1;

    /// <summary>Each channel's bits at all 1 and its divider at 0 (tia.md open item 13).</summary>
    public void PowerOn()
    {
        _bits0 = PowerOnBits;
        _bits1 = PowerOnBits;
        _divider0 = 0;
        _divider1 = 0;
    }

    /// <summary>
    /// One audio step for both channels, with each channel's <c>AUDC</c> and <c>AUDF</c> as they are
    /// written now (only their low four and five bits are used, tia.md section 2.1).
    /// </summary>
    public void Step(byte audc0, byte audf0, byte audc1, byte audf1)
    {
        if (_divider0 == (audf0 & 0x1F))
        {
            _divider0 = 0;
            _bits0 = Pulse(_bits0, audc0 & 0x0F);
        }
        else
        {
            _divider0 = (_divider0 + 1) & 0x1F;
        }

        if (_divider1 == (audf1 & 0x1F))
        {
            _divider1 = 0;
            _bits1 = Pulse(_bits1, audc1 & 0x0F);
        }
        else
        {
            _divider1 = (_divider1 + 1) & 0x1F;
        }
    }

    /// <summary>
    /// The mix before the pads, <c>s</c> = <c>AUDV0</c> x bit 0 + <c>AUDV1</c> x bit 1, 0 to 30:
    /// the two channels pull one node down through conductances in proportion to their volumes,
    /// which add (tia.md section 18.7). Only the low four bits of a volume are used.
    /// </summary>
    public static int Mix(int bit0, byte audv0, int bit1, byte audv1) => (bit0 * (audv0 & 0x0F)) + (bit1 * (audv1 & 0x0F));

    /// <summary>
    /// The output level for a mix <paramref name="mix"/>, from 0 to 1: linear, <c>s</c> / 30, a
    /// choice, because the curve the pads work against was not found (tia.md section 18.7)
    /// [guessing - verify].
    /// </summary>
    public static double Level(int mix) => mix / (double)LoudestMix;

    /// <summary>
    /// One divided pulse of a noise-tone generator: <paramref name="bits"/> before it, and the
    /// four-bit <paramref name="audc"/>; returns the bits after it (tia.md section 18.4, steps 1 to
    /// 3). Every new bit comes from the bits before the pulse.
    /// </summary>
    internal static int Pulse(int bits, int audc)
    {
        int low = audc & 3;
        int high = audc >> 2;
        int poly5 = bits & 0x1F;

        // 1. The five-bit counter shifts, and its new bit 0.
        int new0;
        if (audc == 0)
        {
            new0 = 1;
        }
        else if (low == 0)
        {
            // Chained with the four-bit counter into one of nine bits.
            new0 = (((bits >> 8) ^ (bits >> 4)) & 1) | (bits == 0 ? 1 : 0);
        }
        else
        {
            new0 = (((bits >> 2) ^ (bits >> 4)) & 1) | (poly5 == 0 ? 1 : 0);
        }

        int next = (bits & 0x1E0) | ((poly5 << 1) & 0x1E) | new0;

        // 2. Whether the four-bit counter is clocked.
        bool clock4 = low switch
        {
            0 or 1 => true,
            2 => poly5 is 8 or 24,
            _ => ((bits >> 4) & 1) != 0,
        };

        if (!clock4)
        {
            return next;
        }

        // 3. The four-bit counter shifts, and its new bit 5.
        int new5;
        if (audc == 0)
        {
            new5 = 1;
        }
        else
        {
            new5 = high switch
            {
                0 => (((bits >> 7) ^ (bits >> 8)) & 1) | ((bits & 0x1E0) == 0 ? 1 : 0),
                1 => ~(bits >> 5) & 1,
                2 => (bits >> 4) & 1,
                _ => (~(bits >> 7) & 1) | (((bits >> 5) & 7) == 5 ? 1 : 0),
            };
        }

        return (next & 0x1F) | ((bits << 1) & 0x1C0) | (new5 << 5);
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_bits0), _bits0);
        sink.Add(nameof(_bits1), _bits1);
        sink.Add(nameof(_divider0), _divider0);
        sink.Add(nameof(_divider1), _divider1);
    }
}
