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
/// The OS runs with real vertical sync interrupts: the CRTC's VSYNC drives the system VIA's CA1
/// on both buses. The script boots, types a BASIC program that drives the user VIA's timers, shift register,
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
        Assert.True(pair.VsyncFalls > 300, $"VSYNC fell only {pair.VsyncFalls} times");
        Assert.True(pair.ViaAccesses > 10_000, $"only {pair.ViaAccesses} accesses reached a VIA");
    }

    /// <summary>
    /// The bus alone, driven through the VIAs' and the CRTC's addresses with random accesses,
    /// CRTC programs, video ULA clock changes, keys, lines and resets, and long gaps of RAM reads,
    /// against the oracle bus. After every access the cycle count, the value read and the CPU's IRQ
    /// line must match, so an IRQ the bus sets a cycle late, a VSYNC edge that reaches CA1 a cycle
    /// early or late, or a stretch counted wrongly, fails at once. The CRTC's outputs are compared
    /// only now and then, so between those it catches up lazily, and the bus looks at it only at
    /// the events it works out ahead.
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

        long vsyncFalls = 0;
        oracle.Crtc.VSyncFell += () => vsyncFalls++;

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

        void SameCrtc()
        {
            var expected = (oracle.Crtc.MemoryAddress, oracle.Crtc.RasterAddress, oracle.Crtc.DisplayEnable, oracle.Crtc.HSync, oracle.Crtc.VSync, oracle.Crtc.Cursor, oracle.Crtc.LineStartAddress, oracle.Crtc.VerticalDisplay);
            var actual = (bus.Crtc.MemoryAddress, bus.Crtc.RasterAddress, bus.Crtc.DisplayEnable, bus.Crtc.HSync, bus.Crtc.VSync, bus.Crtc.Cursor, bus.Crtc.LineStartAddress, bus.Crtc.VerticalDisplay);
            if (expected != actual)
            {
                Assert.Fail($"after access {accesses} at cycle {oracle.Cycles}: expected CRTC {expected}, was {actual}");
            }
        }

        while (accesses < 250_000)
        {
            if (random.Next(50) == 0)
            {
                SameCrtc();
            }

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
                // A CRTC register, mostly small values, so VSYNC edges and frames come every few
                // hundred cycles; or the video ULA's control register, whose bit 4 is the CRTC's clock.
                if (random.Next(8) == 0)
                {
                    byte control = (byte)(random.Next(256));
                    oracle.Write(0xFE20, control);
                    bus.Write(0xFE20, control);
                }
                else
                {
                    int crtcRegister = random.Next(16);
                    byte value = crtcRegister switch
                    {
                        0 => (byte)random.Next(random.Next(4) == 0 ? 4 : 30),
                        3 => (byte)random.Next(256),
                        8 => (byte)random.Next(256),
                        9 => (byte)random.Next(random.Next(4) == 0 ? 32 : 5),
                        _ => (byte)random.Next(random.Next(6) == 0 ? 256 : 8),
                    };
                    ushort select = (ushort)(0xFE00 + (2 * random.Next(4)));
                    oracle.Write(select, (byte)crtcRegister);
                    bus.Write(select, (byte)crtcRegister);
                    oracle.Write((ushort)(select + 1), value);
                    bus.Write((ushort)(select + 1), value);
                    Same(oracle.Read((ushort)(select + 1)), bus.Read((ushort)(select + 1)));
                }
                Same(0, 0);
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

        SameCrtc();
        Assert.True(irqHigh > 10_000, $"the IRQ line was high after only {irqHigh} accesses");
        Assert.True(vsyncFalls > 10_000, $"VSYNC fell only {vsyncFalls} times");
    }

    /// <summary>
    /// The bus against the oracle with what the test above leaves out: the outside world changing
    /// the VIAs' inputs (the user port's pins and CB1, CB2 and CA2, the system VIA's port A and
    /// CA1), keys, a <see cref="BbcBus.Peek"/> between accesses, power-on resets in the middle of
    /// a run, and gaps of up to 300,000 cycles with nobody looking. Every access compares the value,
    /// the cycle count and the IRQ line, and a peek must change nothing either bus shows.
    /// </summary>
    /// <remarks>
    /// Written by task 6b's reviewer as a scratch check, and kept (task 15) with two of its four
    /// seeds at a fifth of its length so it runs in seconds, with the long gaps five times as likely so as many come up.
    /// The CPU is attached only for its IRQ line; it never runs.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheBusMatchesThePerCycleOracleWithInputsPeeksAndResets(int seed)
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
        var random = new Random(seed);
        long accesses = 0;

        void Same(byte expected, byte actual)
        {
            accesses++;
            if (expected != actual || oracle.Cycles != bus.Cycles || oracleCpu.Irq != cpu.Irq)
            {
                Assert.Fail($"access {accesses} at cycle {oracle.Cycles}: value {expected:X2}/{actual:X2}, IRQ {oracleCpu.Irq}/{cpu.Irq}, cycles {oracle.Cycles}/{bus.Cycles}");
            }
        }

        for (int step = 0; step < 60_000; step++)
        {
            int kind = random.Next(100);
            ushort via = random.Next(2) == 0 ? (ushort)0xFE40 : (ushort)0xFE60;
            int register = random.Next(16);
            if (kind < 30)
            {
                ushort address = (ushort)(via + register);
                Same(oracle.Read(address), bus.Read(address));
            }
            else if (kind < 60)
            {
                // Small timer values and frequent IER writes, so flags rise and are acknowledged often.
                byte value = register switch
                {
                    5 or 7 or 9 => random.Next(3) == 0 ? (byte)random.Next(256) : (byte)0,
                    4 or 6 or 8 => (byte)random.Next(random.Next(4) == 0 ? 256 : 12),
                    0xE => (byte)(random.Next(2) == 0 ? 0xFF : random.Next(256)),
                    _ => (byte)random.Next(256),
                };
                ushort address = (ushort)(via + register);
                oracle.Write(address, value);
                bus.Write(address, value);
                Same(0, 0);
            }
            else if (kind < 65)
            {
                byte pins = (byte)random.Next(256);
                oracle.UserVia.PortBInput = pins;
                bus.UserVia.PortBInput = pins;
            }
            else if (kind < 68)
            {
                byte pins = (byte)random.Next(256);
                oracle.SystemVia.PortAInput = pins;
                bus.SystemVia.PortAInput = pins;
            }
            else if (kind < 72)
            {
                bool level = random.Next(2) == 0;
                switch (random.Next(4))
                {
                    case 0: oracle.UserVia.SetCb1(level); bus.UserVia.SetCb1(level); break;
                    case 1: oracle.UserVia.SetCb2(level); bus.UserVia.SetCb2(level); break;
                    case 2: oracle.UserVia.SetCa2(level); bus.UserVia.SetCa2(level); break;
                    default: oracle.SystemVia.VsyncInput = level; bus.SystemVia.VsyncInput = level; break;
                }
            }
            else if (kind < 76)
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
            else if (kind < 80)
            {
                ushort address = (ushort)(via + random.Next(16));
                Assert.Equal(oracle.Peek(address), bus.Peek(address));
                Assert.Equal(oracle.Irq, bus.Irq);
            }
            else if (kind == 80 && random.Next(50) == 0)
            {
                oracle.PowerOnReset();
                bus.PowerOnReset();
                Assert.Equal(oracleCpu.Irq, cpu.Irq);
            }
            else if (kind == 81 && random.Next(100) == 0)
            {
                int gap = random.Next(1, 300_000);
                for (int i = 0; i < gap; i++)
                {
                    Same(oracle.Read(0x0000), bus.Read(0x0000));
                }
            }

            // Fast and slow accesses that are not the VIAs', so the stretch's parity varies.
            int after = random.Next(5);
            for (int i = 0; i < after; i++)
            {
                ushort address = random.Next(6) == 0 ? (ushort)0xFC10 : (ushort)0x1234;
                Same(oracle.Read(address), bus.Read(address));
            }
        }

        for (int r = 0; r < 16; r++)
        {
            Assert.Equal(oracle.SystemVia.Peek(r), bus.SystemVia.Peek(r));
            Assert.Equal(oracle.UserVia.Peek(r), bus.UserVia.Peek(r));
        }
    }

    /// <summary>
    /// A write that brings the CRTC's next VSYNC fall forward, made while VSYNC is high and the bus
    /// has already put its next look at the old fall: VSYNC cut to one line (R3), or the CRTC's
    /// clock doubled by the video ULA. With CA1's interrupt enabled, the IRQ must rise in the cycle
    /// of the new fall, through nothing but RAM reads, so the bus must look again after the write.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AWriteThatBringsVsyncsFallForwardRaisesTheIrqInItsCycle(bool doubleTheClock)
    {
        var bus = new BbcBus(BbcSession.Roms);
        var oracle = new ReferenceBbcBus(BbcSession.Roms);
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        var oracleCpu = new Cpu(oracle, CpuVariant.Nmos6502);
        bus.Cpu = cpu;
        oracle.Cpu = oracleCpu;
        bus.PowerOnReset();
        oracle.PowerOnReset();

        void Write(ushort address, byte value)
        {
            bus.Write(address, value);
            oracle.Write(address, value);
        }

        // Mode 4's registers (video.md s3.1, R7 + 1) on the 1 MHz clock, or mode 0's on 2 MHz.
        byte[] registers = doubleTheClock
            ? [0x3F, 0x28, 0x31, 0x24, 0x26, 0x00, 0x20, 0x23, 0x01, 0x07, 0x67, 0x08]
            : [0x7F, 0x50, 0x62, 0x28, 0x26, 0x00, 0x20, 0x23, 0x01, 0x07, 0x67, 0x08];
        Write(0xFE20, doubleTheClock ? (byte)0x88 : (byte)0x9C);
        for (int r = 0; r < registers.Length; r++)
        {
            Write(0xFE00, (byte)r);
            Write(0xFE01, registers[r]);
        }
        Write(0xFE4C, 0x04);
        Write(0xFE4E, 0x82);
        Write(0xFE4D, 0x7F);

        int changes = 0;
        for (int field = 0; field < 3; field++)
        {
            // To the start of a VSYNC pulse, looking only at the oracle, so the machine's bus
            // keeps the next look it chose.
            for (int n = 0; n < 100_000 && !oracle.Crtc.VSync; n++)
            {
                Assert.Equal(oracle.Read(0x0000), bus.Read(0x0000));
            }
            Assert.True(oracle.Crtc.VSync);

            if (doubleTheClock)
            {
                Write(0xFE20, 0x9C);
            }
            else
            {
                Write(0xFE00, 3);
                Write(0xFE01, 0x18);
            }

            for (int n = 0; n < 600; n++)
            {
                Assert.Equal(oracle.Read(0x0000), bus.Read(0x0000));
                Assert.True(oracleCpu.Irq == cpu.Irq, $"field {field}, cycle {oracle.Cycles}: IRQ expected {oracleCpu.Irq}, was {cpu.Irq}");
                changes += oracleCpu.Irq ? 1 : 0;
            }

            // Back as it was, and acknowledge.
            Write(0xFE20, doubleTheClock ? (byte)0x88 : (byte)0x9C);
            Write(0xFE00, 3);
            Write(0xFE01, registers[3]);
            Write(0xFE4D, 0x7F);
        }

        Assert.True(changes > 0, "the IRQ never rose");
    }

    /// <summary>
    /// <see cref="Crtc6845.StateAt"/> in the machine: the CRTC on the bus's clock at 1 MHz and
    /// 2 MHz, with CA1's interrupt on so the bus looks at it at its events, asked about random past
    /// cycles in order and compared with the oracle bus's CRTC as it was in that cycle. Every CRTC
    /// or ULA write is made after a look up to its cycle, as the contract says.
    /// </summary>
    [Fact]
    public void StateAtInTheMachineMatchesTheOracleAtPastCycles()
    {
        var bus = new BbcBus(BbcSession.Roms);
        var oracle = new ReferenceBbcBus(BbcSession.Roms);
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        var oracleCpu = new Cpu(oracle, CpuVariant.Nmos6502);
        bus.Cpu = cpu;
        oracle.Cpu = oracleCpu;
        bus.PowerOnReset();
        oracle.PowerOnReset();

        var random = new Random(6845 + 2);
        var record = new List<CrtcState>();
        int cyclesPerCharacter = 2;
        long checks = 0;

        void Look()
        {
            if (record.Count == 0)
            {
                return;
            }

            long from = Math.Max(bus.Crtc.StateCycle, record[0].Cycle);
            for (long t = from; t <= bus.Cycles; t += 1 + random.Next(random.Next(3) == 0 ? 3_000 : 70))
            {
                CrtcState expected = record[(int)(t - record[0].Cycle)];
                CrtcState actual = bus.Crtc.StateAt(t);
                if (expected != actual)
                {
                    Assert.Fail($"at cycle {t}: expected {expected}, was {actual}");
                }
                checks++;
            }
        }

        void Write(ushort address, byte value)
        {
            Look();
            oracle.Write(address, value);
            bus.Write(address, value);
            record.Clear();
        }

        Write(0xFE4C, 0x04);
        Write(0xFE4E, 0x82);
        for (int program = 0; program < 400; program++)
        {
            if (random.Next(3) == 0)
            {
                byte control = random.Next(2) == 0 ? (byte)0x9C : (byte)0x88;
                Write(0xFE20, control);
                cyclesPerCharacter = (control & 0x10) != 0 ? 1 : 2;
            }

            for (int n = random.Next(1, 6); n > 0; n--)
            {
                int register = random.Next(16);
                Write(0xFE00, (byte)register);
                Write(0xFE01, (byte)random.Next(register is 0 ? 70 : register is 3 or 8 ? 256 : 12));
            }
            Write(0xFE4D, 0x7F);

            int span = random.Next(4) == 0 ? random.Next(60_000) : random.Next(3_000);
            for (int i = 0; i < span; i++)
            {
                Assert.Equal(oracle.Read(0x0000), bus.Read(0x0000));
                record.Add(oracle.Crtc.State(oracle.Cycles, cyclesPerCharacter));

                // Since task 8 the video ULA brings the CRTC to the end of every line, so the past
                // cycles left to ask about are at most a line's worth: look often.
                if (random.Next(100) == 0)
                {
                    Look();
                }
            }
        }

        Look();
        Assert.True(checks > 20_000, $"only {checks} checks");
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
            _oracle.Crtc.VSyncFell += () => VsyncFalls++;
        }

        public long IrqAccesses { get; private set; }

        public long VsyncFalls { get; private set; }

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
