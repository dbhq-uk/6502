using Dbhq.Cpu6502;

namespace Dbhq.Cpu6502.TestSupport;

/// <summary>One bus access, as a test records it.</summary>
public readonly record struct BusAccess(ushort Address, byte Value, bool IsWrite)
{
    public override string ToString() => $"{(IsWrite ? "write" : "read")} ${Address:X4} = ${Value:X2}";
}

/// <summary>A plain 64 KB of RAM that can record every access.</summary>
public sealed class FlatBus : Bus
{
    public byte[] Memory { get; } = new byte[0x10000];

    public List<BusAccess> Log { get; } = [];

    /// <summary>Off for long runs, where the log would only cost time and memory.</summary>
    public bool Recording { get; set; } = true;

    public override byte Read(ushort address)
    {
        byte value = Memory[address];
        if (Recording)
        {
            Log.Add(new BusAccess(address, value, false));
        }

        return value;
    }

    public override void Write(ushort address, byte value)
    {
        Memory[address] = value;
        if (Recording)
        {
            Log.Add(new BusAccess(address, value, true));
        }
    }
}
