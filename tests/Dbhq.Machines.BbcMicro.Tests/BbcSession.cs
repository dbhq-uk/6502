using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>A Model B for a test: the pinned ROMs, the start-up mode, and ways to look at it.</summary>
public sealed class BbcSession
{
    /// <summary>
    /// How long <see cref="Type"/> holds each key down, in CPU cycles: 40 ms, four of the OS's
    /// 100 Hz ticks and two fields. The OS sees a key go down through the keyboard's CA2 interrupt
    /// and puts its character in the buffer at the next 100 Hz tick (OS <c>$F01F-$F026</c> sets the
    /// countdown at <c>$E7</c> to 1, and <c>$EF54-$EF66</c> counts it down on the tick); four ticks
    /// leave room for a tick that lands just before the press. It is well short of the
    /// auto-repeat delay, 50 cs from power on (OS default table <c>$D994</c> = <c>$32</c>, copied to
    /// <c>$0254</c> by <c>$DA5B-$DA62</c>), so no key repeats.
    /// </summary>
    public const long HoldCycles = 80_000;

    /// <summary>
    /// How long <see cref="Type"/> waits with every key up before the next, in CPU cycles: 40 ms
    /// again, so the OS's scan on its ticks sees the key go up before another comes down, and the
    /// next key is a new press rather than a second key held with the first.
    /// </summary>
    public const long RestCycles = 80_000;

    private static readonly Lazy<BbcRoms> LazyRoms = new(() => new BbcRoms(
        RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256),
        RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256),
        RepoPaths.ReadChecked(Pins.BbcDfsPath, Pins.BbcDfsSha256)));

    private static readonly Lazy<IReadOnlyDictionary<char, (BbcKey Key, bool Shift)>> LazyKeys = new(MakeKeys);

    /// <param name="mode">The screen mode the start-up links select, 0 to 7.</param>
    /// <param name="disc">A disc image for drive 0. No drive is fitted yet, so it must be null.</param>
    public BbcSession(int mode = 7, byte[]? disc = null)
    {
        if (disc is not null)
        {
            throw new NotSupportedException("The disc drive is not modelled yet.");
        }

        Machine = new BbcMachine(Roms, new BbcOptions { StartupMode = mode });
    }

    /// <summary>The three ROMs from <c>roms/bbc-micro</c>, each checked against its pinned SHA-256.</summary>
    public static BbcRoms Roms => LazyRoms.Value;

    /// <summary>
    /// For each character <see cref="Type"/> can type, the key and whether SHIFT goes with it, made
    /// from the OS's own key table (below).
    /// </summary>
    public static IReadOnlyDictionary<char, (BbcKey Key, bool Shift)> Keys => LazyKeys.Value;

    public BbcMachine Machine { get; }

    /// <summary>Switches on and runs; the default is three seconds of machine time.</summary>
    public BbcSession Boot(long cpuCycles = 6_000_000)
    {
        Machine.PowerOn();
        Machine.Run(cpuCycles);
        return this;
    }

    /// <summary>Runs for at least <paramref name="cpuCycles"/> more CPU cycles.</summary>
    public BbcSession RunFor(long cpuCycles)
    {
        Machine.Run(cpuCycles);
        return this;
    }

    /// <summary>
    /// Types <paramref name="text"/> on the keyboard matrix, a key at a time, running the machine
    /// throughout: SHIFT down with the key where the character needs it, the key held for
    /// <see cref="HoldCycles"/>, then every key up for <see cref="RestCycles"/>. A carriage return
    /// or a newline is RETURN.
    /// </summary>
    public BbcSession Type(string text)
    {
        BbcKeyboard keyboard = Machine.Keyboard;
        foreach (char c in text)
        {
            if (!Keys.TryGetValue(c == '\n' ? '\r' : c, out (BbcKey Key, bool Shift) press))
            {
                throw new ArgumentOutOfRangeException(nameof(text), c, "No key types this character.");
            }

            if (press.Shift)
            {
                keyboard.Press(BbcKey.Shift);
            }
            keyboard.Press(press.Key);
            Machine.Run(HoldCycles);
            keyboard.Release(press.Key);
            if (press.Shift)
            {
                keyboard.Release(BbcKey.Shift);
            }
            Machine.Run(RestCycles);
        }
        return this;
    }

    /// <summary>
    /// The text on the screen, a string a row, read off the picture by <see cref="global::Dbhq.Machines.BbcMicro.Tests.ScreenText"/>.
    /// Two things come from the OS's variables, neither of them screen memory: the mode, at
    /// <c>$0355</c> (<c>video.md</c> s3.3), which says how to cut the picture into cells, and the
    /// text cursor, column at <c>$0318</c> and row at <c>$0319</c> (absolute, not window relative:
    /// OS <c>$C66A-$C66D</c> compares the column with the window's right edge at <c>$030A</c>),
    /// which says which cell the cursor may be inverting.
    /// </summary>
    public string[] ScreenText() =>
        ScreenCells().Select(row => new string(row.Select(cell => cell.Text).ToArray())).ToArray();

    /// <summary><see cref="ScreenText"/> with each cell's colours as drawn.</summary>
    public global::Dbhq.Machines.BbcMicro.Tests.ScreenText.Cell[][] ScreenCells()
    {
        BbcBus bus = Machine.Bus;
        int mode = bus.Peek(0x0355);
        (int, int) cursor = (bus.Peek(0x0318), bus.Peek(0x0319));
        return global::Dbhq.Machines.BbcMicro.Tests.ScreenText.ReadCells(Machine.Screen.Pixels, mode, cursor, Roms.Os);
    }

    /// <summary>
    /// Mode 7 only: the 40 character codes of one text row, read straight from screen memory
    /// at <c>$7C00 + row * 40</c>. No video chip is involved.
    /// </summary>
    public string ScreenRowAsMemory(int row)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(row, 24);

        var text = new char[40];
        for (int i = 0; i < 40; i++)
        {
            text[i] = (char)Machine.Bus.Peek((ushort)(0x7C00 + row * 40 + i));
        }

        return new string(text);
    }

    /// <summary>
    /// The characters each key types, from the OS's key table and its SHIFT rule, not typed in by
    /// hand: the unshifted code of the key in column c of row r (1 to 7) is the byte at
    /// <c>$F03B + 16 (r - 1) + c</c> (<c>via.md</c> s3(b)). Letters are stored small, and with
    /// CAPS LOCK on, as it is from power on, they type capitals. SHIFT on a code from <c>$21</c> to
    /// <c>$3F</c> flips bit 4, except on <c>0</c> (OS <c>$EA9C</c>, <c>via.md</c> s3(b)): so
    /// <c>"</c> is SHIFT and 2 and <c>*</c> is SHIFT and colon, as on the keyboard's legends.
    /// Small letters, and SHIFT on the keys above <c>$3F</c>, are not needed here and not offered.
    /// </summary>
    private static IReadOnlyDictionary<char, (BbcKey Key, bool Shift)> MakeKeys()
    {
        byte[] os = Roms.Os;
        var keys = new Dictionary<char, (BbcKey, bool)>();
        foreach (BbcKey key in Enum.GetValues<BbcKey>())
        {
            int row = (int)key >> 4, column = (int)key & 0x0F;
            if (row == 0)
            {
                continue;
            }

            int code = os[0x303B + (16 * (row - 1)) + column];
            if (code is >= 0x61 and <= 0x7A)
            {
                keys.Add((char)(code - 0x20), (key, false));
            }
            else if (code is 0x0D or (>= 0x20 and <= 0x5F))
            {
                keys.Add((char)code, (key, false));
                if (code is >= 0x21 and <= 0x3F and not 0x30)
                {
                    keys.Add((char)(code ^ 0x10), (key, true));
                }
            }
        }
        return keys;
    }
}
