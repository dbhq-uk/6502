using System.Text;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>What a Blargg test ROM reported: its status byte, its text, and how long it ran.</summary>
/// <param name="Status">The byte at <c>$6000</c>: 0 a pass, 1 a failure, 2 and up a numbered reason; -1 if the ROM never started reporting.</param>
/// <param name="Text">The zero-terminated text from <c>$6004</c>, which names the failing check.</param>
/// <param name="Cycles">CPU cycles run, from power on.</param>
/// <param name="TimedOut">True when the budget ran out before the ROM gave a result.</param>
public sealed record BlarggResult(int Status, string Text, long Cycles, bool TimedOut);

/// <summary>
/// Runs a Blargg test ROM headless and reads its result from cartridge RAM, by the protocol in
/// <c>docs/nes/facts/cartridge.md</c> section 5.
/// </summary>
/// <remarks>
/// The ROM writes a signature to <c>$6001</c> to <c>$6003</c> when the data at <c>$6000</c> is
/// valid. Until then <c>$6000</c> means nothing (it reads 0 after power on, which is not a pass).
/// Then <c>$6000</c> is <c>$80</c> while the test runs, <c>$81</c> when it wants the reset button
/// pressed at least 100 ms later, and the result once it is done.
/// </remarks>
public static class BlarggRunner
{
    /// <summary>The signature at <c>$6001</c> to <c>$6003</c>, read from a running ROM (cartridge.md 5).</summary>
    public static readonly byte[] Signature = [0xDE, 0xB0, 0x61];

    private const byte Running = 0x80;
    private const byte ResetWanted = 0x81;

    /// <summary>
    /// Runs the pinned ROM <paramref name="pinnedName"/> on <paramref name="region"/> until
    /// <c>$6000</c> leaves <c>$80</c>, with the signature in place, or until
    /// <paramref name="maxCpuCycles"/> have run.
    /// </summary>
    public static BlarggResult Run(string pinnedName, Region region, long maxCpuCycles)
    {
        var nes = new Nes(Cartridge.Load(NesTestRoms.Read(pinnedName)), region);
        nes.PowerOn();

        // A reset asked for is pressed this many cycles later: 100 ms, with a margin.
        long resetDelay = (long)(region.CpuHz * 0.15);
        long resetAt = -1;

        while (nes.Bus.Cycles < maxCpuCycles)
        {
            nes.Step();
            if (!HasSignature(nes.Bus))
            {
                continue;
            }

            byte status = nes.Bus.Peek(0x6000);
            if (status == ResetWanted)
            {
                if (resetAt < 0)
                {
                    resetAt = nes.Bus.Cycles + resetDelay;
                }
                else if (nes.Bus.Cycles >= resetAt)
                {
                    resetAt = -1;
                    nes.Reset();
                }

                continue;
            }

            if (status != Running)
            {
                return new BlarggResult(status, ReadText(nes.Bus), nes.Bus.Cycles, false);
            }
        }

        int last = HasSignature(nes.Bus) ? nes.Bus.Peek(0x6000) : -1;
        return new BlarggResult(last, ReadText(nes.Bus), nes.Bus.Cycles, true);
    }

    private static bool HasSignature(NesBus bus)
    {
        return bus.Peek(0x6001) == Signature[0] && bus.Peek(0x6002) == Signature[1] && bus.Peek(0x6003) == Signature[2];
    }

    private static string ReadText(NesBus bus)
    {
        var text = new StringBuilder();
        for (ushort address = 0x6004; address < 0x8000; address++)
        {
            byte b = bus.Peek(address);
            if (b == 0)
            {
                break;
            }

            text.Append((char)b);
        }

        return text.ToString();
    }
}
