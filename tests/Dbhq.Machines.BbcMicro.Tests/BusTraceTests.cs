using Dbhq.Cpu6502;
using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// A Model B whose CPU reaches the bus through a recorder, so every bus access the CPU makes
/// can be seen: its address, its value, whether it was a write, the cycle count after it and the
/// two interrupt lines then. The recorder only passes each access on, so the machine runs exactly
/// as a <see cref="BbcMachine"/> does; it is built by hand because <see cref="BbcMachine"/> makes
/// its own CPU on its own bus.
/// </summary>
/// <remarks>
/// It uses only what the machine had before the chip counters (models plan, task 6), so this file
/// compiles against <c>main</c> as it was and gives the same trace there. That is how the
/// counters are shown to change nothing (<see cref="BusTraceTests"/>).
/// </remarks>
internal sealed class TracedMachine : Bus
{
    private const ulong Prime = 0x100000001B3;

    public TracedMachine(DiscImage? disc = null)
    {
        Machine = new BbcBus(BbcSession.Roms);
        Cpu = new Cpu(this, CpuVariant.Nmos6502);
        Machine.Cpu = Cpu;
        Machine.Fdc.Insert(0, disc);
    }

    /// <summary>The machine's bus, which the recorder passes every access to.</summary>
    public BbcBus Machine { get; }

    public Cpu Cpu { get; }

    /// <summary>Every access so far folded into one FNV-1a hash.</summary>
    public ulong Hash { get; private set; } = 0xCBF29CE484222325;

    /// <summary>Bus accesses by the CPU so far.</summary>
    public long Accesses { get; private set; }

    /// <summary>Told of every access after it is made: the address and whether it was a write.</summary>
    public Action<ushort, bool>? Accessed { get; set; }

    public override byte Read(ushort address)
    {
        byte value = Machine.Read(address);
        Note(address, value, write: false);
        return value;
    }

    public override void Write(ushort address, byte value)
    {
        Machine.Write(address, value);
        Note(address, value, write: true);
    }

    public void PowerOn()
    {
        Machine.PowerOnReset();
        Cpu.Reset();
    }

    public void Break()
    {
        Machine.BreakReset();
        Cpu.Reset();
    }

    /// <summary>Runs whole instructions until at least <paramref name="cycles"/> more CPU cycles have passed.</summary>
    public void Run(long cycles)
    {
        long end = Machine.Cycles + cycles;
        while (Machine.Cycles < end)
        {
            Cpu.Step();
        }
    }

    /// <summary>Types <paramref name="text"/> as <see cref="BbcSession.Type"/> does.</summary>
    public void Type(string text)
    {
        foreach (char c in text)
        {
            (BbcKey key, bool shift) = BbcSession.Keys[c == '\n' ? '\r' : c];
            if (shift)
            {
                Machine.Keyboard.Press(BbcKey.Shift);
            }
            Tap(key);
            if (shift)
            {
                Machine.Keyboard.Release(BbcKey.Shift);
            }
        }
    }

    /// <summary>One key down for <see cref="BbcSession.HoldCycles"/>, then up for <see cref="BbcSession.RestCycles"/>.</summary>
    public void Tap(BbcKey key)
    {
        Machine.Keyboard.Press(key);
        Run(BbcSession.HoldCycles);
        Machine.Keyboard.Release(key);
        Run(BbcSession.RestCycles);
    }

    /// <summary>
    /// A session that reaches every chip the machine has: boot, *MOTOR on and off, a sound, a mode
    /// change, CAPS LOCK and SHIFT LOCK, a short BASIC program on the user VIA, *CAT on the disc in
    /// drive 0, and BREAK.
    /// </summary>
    public void RunSession()
    {
        PowerOn();
        Run(6_000_000);
        Type("*MOTOR 1\r");
        Type("*MOTOR 0\r");
        Type("SOUND 1,-15,100,10\r");
        Run(1_000_000);
        Type("MODE 1\r");
        Type("MODE 7\r");
        Tap(BbcKey.CapsLock);
        Tap(BbcKey.ShiftLock);
        Type("1");
        Tap(BbcKey.ShiftLock);
        Tap(BbcKey.CapsLock);
        Type("\r10 FOR I=1 TO 9:?&FE62=255:?&FE60=I:X%=?&FE64:NEXT\rRUN\r");
        Run(1_000_000);
        Type("*CAT\r");
        Run(4_000_000);
        Break();
        Run(6_000_000);
    }

    private void Note(ushort address, byte value, bool write)
    {
        Accesses++;
        Mix((ulong)Machine.Cycles);
        Mix(address | ((ulong)value << 16) | (write ? 1UL << 24 : 0) | (Cpu.Irq ? 1UL << 25 : 0) | (Cpu.Nmi ? 1UL << 26 : 0));
        Accessed?.Invoke(address, write);
    }

    private void Mix(ulong value)
    {
        ulong hash = Hash;
        for (int i = 0; i < 8; i++)
        {
            hash ^= (byte)(value >> (8 * i));
            hash *= Prime;
        }
        Hash = hash;
    }
}

/// <summary>
/// The machine's trace of bus accesses against the trace <c>main</c> gave before the chip
/// counters were added (models plan, task 6): the same session, every access's address, value,
/// direction, cycle count and interrupt lines, hashed. The counters may count, and must change
/// nothing else, so the hash must not move.
/// </summary>
public class BusTraceTests
{
    /// <summary>
    /// What <see cref="TracedMachine.RunSession"/> gave on <c>main</c> at <c>9c12535</c>, whose BBC
    /// Micro code is the branch's merge base's (<c>8a30791</c>), with this file copied into a copy
    /// of that tree and run there on 13 October 2026. A change to the
    /// machine that moves any access, value or cycle changes it, and is then a change in behaviour
    /// to be argued for, not a counter.
    /// </summary>
    private const string MainTrace = "5FF95E1A116CE11C after 36536146 accesses";

    [Fact]
    public void TheSessionsBusTraceIsTheOneMainGaveBeforeTheCounters()
    {
        var machine = new TracedMachine(DiscImage.Blank(80, false));
        machine.RunSession();

        Assert.Equal(MainTrace, $"{machine.Hash:X16} after {machine.Accesses} accesses");
    }
}
