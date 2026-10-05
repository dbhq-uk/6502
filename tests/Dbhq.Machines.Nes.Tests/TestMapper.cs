namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// A board for testing the PPU alone: 8 KB of pattern-table RAM, a mirroring the test sets, and a
/// record of every address the PPU reports on its bus.
/// </summary>
public sealed class TestMapper : IMapper
{
    public byte[] Chr { get; } = new byte[0x2000];

    public List<(ushort Address, long CpuCycle)> Reported { get; } = [];

    public Mirroring Mirroring { get; set; } = Mirroring.Horizontal;

    public bool Irq => false;

    public byte[] PrgRam { get; } = [];

    public byte CpuRead(ushort address, byte openBus) => openBus;

    public void CpuWrite(ushort address, byte value)
    {
    }

    public byte PpuRead(ushort address) => Chr[address & 0x1FFF];

    public void PpuWrite(ushort address, byte value) => Chr[address & 0x1FFF] = value;

    public void PpuAddressChanged(ushort address, long cpuCycle) => Reported.Add((address, cpuCycle));

    public void CpuCycle()
    {
    }

    public void Reset(bool power)
    {
    }

    public void ClearPrgRam()
    {
    }
}
