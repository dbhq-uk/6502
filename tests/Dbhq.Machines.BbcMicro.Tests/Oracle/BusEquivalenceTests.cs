using Dbhq.Cpu6502;
using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The whole machine against the per-cycle oracle: the real OS and BASIC run on the machine's
/// bus and on the oracle bus side by side, an instruction at a time, and every bus access of
/// each is compared: its address, its value, whether it was a write, the cycle count after it
/// and the CPU's IRQ line after it.
/// </summary>
/// <remarks>
/// The script boots, types a BASIC program that drives the user VIA's timers, shift register,
/// pulse counting and handshake modes and prints what it reads back, runs it, presses BREAK and
/// boots again. A single access that lands a cycle early or late, or an IRQ that rises a cycle
/// late, fails it at that access.
/// </remarks>
public class BusEquivalenceTests
{
    private const string Program =
        "10 FOR I=1 TO 30:?&FE6B=I*8:?&FE62=255:?&FE60=I*4:?&FE6C=I*34 AND 255:?&FE61=I"
        + ":?&FE64=I:?&FE65=0:?&FE68=I:?&FE69=0:PRINT ?&FE6D;?&FE6A;?&FE68;?&FE44:NEXT";

    [Fact]
    public void TheMachineMatchesThePerCycleOracleOnEveryBusAccess()
    {
        var pair = new Pair(BbcSession.Roms);
        pair.PowerOn();
        pair.Run(6_000_000);

        foreach (string line in new[] { Program, "RUN" })
        {
            foreach (char c in line)
            {
                pair.Type(c);
            }
            pair.Type('\r');
        }
        pair.Run(8_000_000);

        // The script did what it says: BASIC took the program and ran it without an error.
        string screen = pair.Screen();
        Assert.DoesNotContain("Mistake", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("Syntax", screen, StringComparison.Ordinal);

        pair.Break();
        pair.Run(4_000_000);

        Assert.True(pair.IrqAccesses > 10_000, $"the IRQ line was high on only {pair.IrqAccesses} accesses");
        Assert.True(pair.ViaAccesses > 10_000, $"only {pair.ViaAccesses} accesses reached a VIA");
    }

    /// <summary>
    /// The bus alone, driven through the VIAs' addresses with random accesses, keys, lines and
    /// resets, and long gaps of RAM reads, against the oracle bus. After every access the cycle
    /// count, the value read and the CPU's IRQ line must match, so an IRQ the bus sets a cycle
    /// late, or a stretch counted wrongly, fails at once.
    /// </summary>
    /// <remarks>
    /// The real OS rarely acknowledges a flag in the very cycle it rises with that interrupt
    /// enabled, so the run above may never meet it; here small timer values and random IER
    /// writes make it common. The CPU is attached only so the buses have an IRQ line to drive;
    /// it never runs.
    /// </remarks>
    [Fact]
    public void TheBusMatchesThePerCycleOracleThroughRandomAccesses()
    {
        var bus = new BbcBus(BbcSession.Roms);
        var oracle = new ReferenceBbcBus(BbcSession.Roms);
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        var oracleCpu = new Cpu(oracle, CpuVariant.Nmos6502);
        bus.Cpu = cpu;
        oracle.Cpu = oracleCpu;
        bus.PowerOnReset();
        oracle.PowerOnReset();

        BbcKey[] keys = Enum.GetValues<BbcKey>();
        var random = new Random(6522 + 2);
        long accesses = 0, irqHigh = 0;

        void Same(byte expected, byte actual)
        {
            if (expected != actual || oracle.Cycles != bus.Cycles || oracleCpu.Irq != cpu.Irq)
            {
                Assert.Fail($"after access {accesses} at cycle {oracle.Cycles}: expected value {expected}, IRQ {oracleCpu.Irq}; "
                    + $"was {actual}, IRQ {cpu.Irq}, at cycle {bus.Cycles}");
            }
            irqHigh += cpu.Irq ? 1 : 0;
        }

        while (accesses < 250_000)
        {
            if (random.Next(100) == 0)
            {
                // A gap: the timers run out, flag, wrap and reload with nobody looking.
                int gap = random.Next(1, 20_000);
                for (int i = 0; i < gap; i++)
                {
                    Same(oracle.Read(0x0000), bus.Read(0x0000));
                }
            }

            accesses++;
            int kind = random.Next(100);
            ushort via = random.Next(2) == 0 ? (ushort)0xFE40 : (ushort)0xFE60;

            // The registers whose access acknowledges a flag come up most: T1C-L, T2C-L, SR, IFR.
            int[] registers = [0, 1, 2, 3, 4, 4, 4, 5, 6, 7, 8, 8, 8, 9, 0xA, 0xA, 0xA, 0xB, 0xC, 0xD, 0xD, 0xE, 0xE, 0xF];
            int register = registers[random.Next(registers.Length)];
            if (kind < 35)
            {
                ushort address = (ushort)(via + register + (random.Next(2) * 16)); // A4 is a mirror
                Same(oracle.Read(address), bus.Read(address));
            }
            else if (kind < 75)
            {
                byte value = register switch
                {
                    5 or 7 or 9 => random.Next(4) == 0 ? (byte)random.Next(256) : (byte)0,
                    4 or 6 or 8 => (byte)random.Next(random.Next(4) == 0 ? 256 : 30),
                    0xB => (byte)((random.Next(256) & ~0x1C) | (random.Next(2) == 0 ? 0x08 : random.Next(8) << 2)),
                    _ => (byte)random.Next(256),
                };
                ushort address = (ushort)(via + register);
                oracle.Write(address, value);
                bus.Write(address, value);
                Same(0, 0);
            }
            if (kind < 75)
            {
                // Cycles with no VIA access after it, in which a late IRQ would show.
                int after = random.Next(4);
                for (int i = 0; i < after; i++)
                {
                    Same(oracle.Read(0x1234), bus.Read(0x1234));
                }
            }
            else if (kind < 88)
            {
                int ram = random.Next(1, 8);
                for (int i = 0; i < ram; i++)
                {
                    Same(oracle.Read(0x1234), bus.Read(0x1234));
                }
            }
            else if (kind < 93)
            {
                BbcKey key = keys[random.Next(keys.Length)];
                if (random.Next(2) == 0)
                {
                    oracle.Keyboard.Press(key);
                    bus.Keyboard.Press(key);
                }
                else
                {
                    oracle.Keyboard.Release(key);
                    bus.Keyboard.Release(key);
                }
            }
            else if (kind < 96)
            {
                bool level = random.Next(2) == 0;
                oracle.SystemVia.VsyncInput = level;
                bus.SystemVia.VsyncInput = level;
            }
            else if (kind < 99)
            {
                bool level = random.Next(2) == 0;
                oracle.UserVia.SetCa1(level);
                bus.UserVia.SetCa1(level);
            }
            else if (random.Next(10) == 0)
            {
                oracle.BreakReset();
                bus.BreakReset();
                Same(0, 0);
            }
        }

        for (int register = 0; register < 16; register++)
        {
            Assert.Equal(oracle.SystemVia.Peek(register), bus.SystemVia.Peek(register));
            Assert.Equal(oracle.UserVia.Peek(register), bus.UserVia.Peek(register));
        }

        Assert.True(irqHigh > 10_000, $"the IRQ line was high after only {irqHigh} accesses");
    }

    private readonly record struct Access(ushort Address, byte Value, bool Write, long Cycles, bool Irq);

    /// <summary>An IBus between a CPU and a bus that writes down every access.</summary>
    private sealed class Recorder(IBus inner, Func<long> cycles) : IBus
    {
        public List<Access> Log { get; } = [];

        public Cpu? Cpu { get; set; }

        public byte Read(ushort address)
        {
            byte value = inner.Read(address);
            Log.Add(new Access(address, value, false, cycles(), Cpu!.Irq));
            return value;
        }

        public void Write(ushort address, byte value)
        {
            inner.Write(address, value);
            Log.Add(new Access(address, value, true, cycles(), Cpu!.Irq));
        }
    }

    private sealed class Pair
    {
        private readonly BbcBus _bus;
        private readonly ReferenceBbcBus _oracle;
        private readonly Recorder _busLog;
        private readonly Recorder _oracleLog;
        private readonly Cpu _cpu;
        private readonly Cpu _oracleCpu;

        public Pair(BbcRoms roms)
        {
            _bus = new BbcBus(roms);
            _oracle = new ReferenceBbcBus(roms);
            _busLog = new Recorder(_bus, () => _bus.Cycles);
            _oracleLog = new Recorder(_oracle, () => _oracle.Cycles);
            _cpu = new Cpu(_busLog, CpuVariant.Nmos6502);
            _oracleCpu = new Cpu(_oracleLog, CpuVariant.Nmos6502);
            _busLog.Cpu = _cpu;
            _oracleLog.Cpu = _oracleCpu;
            _bus.Cpu = _cpu;
            _oracle.Cpu = _oracleCpu;
        }

        public long IrqAccesses { get; private set; }

        public long ViaAccesses { get; private set; }

        public void PowerOn()
        {
            _bus.PowerOnReset();
            _oracle.PowerOnReset();
            _cpu.Reset();
            _oracleCpu.Reset();
        }

        public void Break()
        {
            _bus.BreakReset();
            _oracle.BreakReset();
            _cpu.Reset();
            _oracleCpu.Reset();
        }

        public void Run(long cycles)
        {
            long end = _oracle.Cycles + cycles;
            while (_oracle.Cycles < end)
            {
                _cpu.Step();
                _oracleCpu.Step();

                List<Access> expected = _oracleLog.Log;
                List<Access> actual = _busLog.Log;
                for (int i = 0; i < Math.Min(expected.Count, actual.Count); i++)
                {
                    if (expected[i] != actual[i])
                    {
                        Assert.Fail($"access {i} of the instruction at cycle {expected[0].Cycles}: expected {expected[i]}, was {actual[i]}");
                    }

                    IrqAccesses += expected[i].Irq ? 1 : 0;
                    ViaAccesses += expected[i].Address is >= 0xFE40 and <= 0xFE7F ? 1 : 0;
                }

                Assert.Equal(expected.Count, actual.Count);
                Assert.Equal(
                    (_oracleCpu.PC, _oracleCpu.A, _oracleCpu.X, _oracleCpu.Y, _oracleCpu.S, _oracleCpu.P),
                    (_cpu.PC, _cpu.A, _cpu.X, _cpu.Y, _cpu.S, _cpu.P));
                expected.Clear();
                actual.Clear();
            }

            Assert.Equal(_oracle.Cycles, _bus.Cycles);
            Assert.Equal(_oracle.Irq, _bus.Irq);
            Assert.Equal(_oracle.SystemVia.Latch, _bus.SystemVia.Latch);
            for (int register = 0; register < 16; register++)
            {
                Assert.Equal(_oracle.SystemVia.Peek(register), _bus.SystemVia.Peek(register));
                Assert.Equal(_oracle.UserVia.Peek(register), _bus.UserVia.Peek(register));
            }
        }

        /// <summary>Mode 7's screen memory as text, in memory order.</summary>
        public string Screen()
        {
            var text = new char[0x400];
            for (int i = 0; i < text.Length; i++)
            {
                text[i] = (char)_bus.Peek((ushort)(0x7C00 + i));
            }
            return new string(text);
        }

        /// <summary>Holds a key down for twenty milliseconds of machine time, then lets it go for twenty.</summary>
        public void Type(char c)
        {
            (BbcKey key, bool shift) = KeyFor(c);
            foreach (BbcKeyboard keyboard in new[] { _bus.Keyboard, _oracle.Keyboard })
            {
                if (shift)
                {
                    keyboard.Press(BbcKey.Shift);
                }
                keyboard.Press(key);
            }
            Run(40_000);

            foreach (BbcKeyboard keyboard in new[] { _bus.Keyboard, _oracle.Keyboard })
            {
                keyboard.Release(key);
                keyboard.Release(BbcKey.Shift);
            }
            Run(40_000);
        }

        private static (BbcKey Key, bool Shift) KeyFor(char c) => c switch
        {
            >= 'A' and <= 'Z' => (Enum.Parse<BbcKey>(c.ToString()), false),
            >= '0' and <= '9' => (Enum.Parse<BbcKey>("D" + c), false),
            ' ' => (BbcKey.Space, false),
            '\r' => (BbcKey.Return, false),
            ':' => (BbcKey.Colon, false),
            ';' => (BbcKey.Semicolon, false),
            '=' => (BbcKey.Minus, true),
            '?' => (BbcKey.Slash, true),
            '&' => (BbcKey.D6, true),
            '*' => (BbcKey.Colon, true),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, "No key for this character in the script."),
        };
    }
}
