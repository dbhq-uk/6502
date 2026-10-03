using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>A Model B for a test: the pinned ROMs, the start-up mode, and ways to look at it.</summary>
public sealed class BbcSession
{
    private static readonly Lazy<BbcRoms> LazyRoms = new(() => new BbcRoms(
        RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256),
        RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256),
        RepoPaths.ReadChecked(Pins.BbcDfsPath, Pins.BbcDfsSha256)));

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

    public BbcMachine Machine { get; }

    /// <summary>Switches on and runs; the default is three seconds of machine time.</summary>
    public BbcSession Boot(long cpuCycles = 6_000_000)
    {
        Machine.PowerOn();
        Machine.Run(cpuCycles);
        return this;
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
}
