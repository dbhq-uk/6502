using Dbhq.Machines.Nes;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

public class ChipAccessesTests
{
    [Theory]
    [InlineData(0x2000, false, NesChip.Ppu)]
    [InlineData(0x2007, true, NesChip.Ppu)]
    [InlineData(0x3FFF, false, NesChip.Ppu)]   // the last mirror of $2007
    [InlineData(0x4000, true, NesChip.Apu)]
    [InlineData(0x4014, true, NesChip.Apu)]    // starting OAM DMA is a write to the CPU's own register
    [InlineData(0x4015, false, NesChip.Apu)]
    [InlineData(0x4016, true, NesChip.Apu)]    // the strobe is the CPU's OUT0 pin, not a buffer
    [InlineData(0x4016, false, NesChip.Pad1)]
    [InlineData(0x4017, true, NesChip.Apu)]    // the frame counter
    [InlineData(0x4017, false, NesChip.Pad2)]
    public void An_access_reaches_the_chip_its_address_decodes_to(int address, bool write, NesChip chip)
    {
        Assert.Equal(chip, ChipAccesses.ChipAt((ushort)address, write));
    }

    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x07FF)]
    [InlineData(0x1FFF)]
    [InlineData(0x4018)]    // the CPU's test registers, disabled on a console
    [InlineData(0x401F)]
    [InlineData(0x4020)]    // the cartridge, always in use, never counted
    [InlineData(0x8000)]
    [InlineData(0xFFFF)]
    public void Ram_the_cartridge_and_the_test_registers_are_not_counted(int address)
    {
        Assert.Null(ChipAccesses.ChipAt((ushort)address, false));
        Assert.Null(ChipAccesses.ChipAt((ushort)address, true));
    }

    [Fact]
    public void Each_access_adds_one_to_its_chip_and_nothing_to_the_others()
    {
        var a = new ChipAccesses();
        a.Note(0x2002, write: false);
        a.Note(0x2002, write: false);
        a.Note(0x4016, write: true);
        a.Note(0x4016, write: false);
        a.Note(0x8000, write: false);
        Assert.Equal([2, 1, 1, 0], a.Snapshot());
        Assert.Equal(2u, a[NesChip.Ppu]);
    }
}
