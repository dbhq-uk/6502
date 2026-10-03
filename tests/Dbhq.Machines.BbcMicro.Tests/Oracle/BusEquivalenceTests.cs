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
