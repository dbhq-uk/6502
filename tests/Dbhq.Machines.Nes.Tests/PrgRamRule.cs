using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The plan's Review Focus 5 on the whole machine: a cartridge with battery-backed PRG RAM keeps
/// it through the reset button and loses it at a power cycle, because saving is not built. The
/// boards' own tests hold the same rule for each board alone (task 10); this holds it through
/// <see cref="Nes.Reset"/> and <see cref="Nes.PowerOn"/>, for every mapper the machine has.
/// <see cref="NesBusTests"/> and <see cref="NesAcceptanceTests"/> both call it.
/// </summary>
public static class PrgRamRule
{
    /// <summary>The mappers the machine builds, each made into a battery-backed cartridge with 8 KB of PRG RAM.</summary>
    public static TheoryData<int> Mappers() => new() { 0, 1, 2, 3, 4, 7 };

    /// <summary>
    /// Fills <c>$6000</c> to <c>$7FFF</c> through the bus with bytes none of which is 0, presses
    /// reset and reads every one back, then powers on and finds every one 0.
    /// </summary>
    public static void AssertResetKeepsItAndAPowerCycleClearsIt(int mapper)
    {
        // Two 16 KB PRG banks and one 8 KB CHR bank suit every board here; AxROM's CHR is RAM.
        var cartridge = Cartridge.Load(TestCartridge.Ines1(2, mapper == 7 ? 0 : 1, mapper, battery: true, prgRamUnits: 1));
        Assert.True(cartridge.Battery);
        var nes = new Nes(cartridge);
        nes.PowerOn();

        for (int address = 0x6000; address <= 0x7FFF; address++)
        {
            nes.Bus.Write((ushort)address, Pattern(address));
        }

        nes.Reset();
        for (int address = 0x6000; address <= 0x7FFF; address++)
        {
            Assert.True(nes.Bus.Peek((ushort)address) == Pattern(address), $"mapper {mapper}: ${address:X4} lost its byte at the reset button");
        }

        nes.PowerOn();
        for (int address = 0x6000; address <= 0x7FFF; address++)
        {
            Assert.True(nes.Bus.Peek((ushort)address) == 0, $"mapper {mapper}: ${address:X4} kept its byte through a power cycle");
        }
    }

    private static byte Pattern(int address) => (byte)((address % 255) + 1);
}
