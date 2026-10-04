using Dbhq.Cpu6502;
using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The lazy 8271, which catches up over the steps it owes only when it is looked at, against
/// <see cref="ReferenceFdc8271"/>, which is ticked in every cycle. On its own: random command
/// sequences, random access times and random spans between looks, with the status and INT compared
/// after every access. On the bus: the same against the oracle bus, with the CPU's NMI line compared
/// after every access, so an NMI the bus sets a cycle late fails at once. In the machine: DFS
/// saving and loading through the real OS, every bus access compared.
/// </summary>
public class FdcEquivalenceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void TheLazyChipMatchesTheCycleByCycleOne(int seed)
    {
        var random = new Random(8271 + seed);
        var chip = new Fdc8271();
        var reference = new ReferenceFdc8271();
        var discs = MakeDiscs(random);
        var results = new HashSet<byte>();
        long accesses = 0, bytes = 0;

        void Same(byte expected, byte actual, string what)
        {
            accesses++;
            if (expected != actual || reference.ReadStatus() != chip.ReadStatus() || reference.Interrupt != chip.Interrupt)
            {
                Assert.Fail($"seed {seed}, access {accesses} ({what}): expected {expected:X2}, status {reference.ReadStatus():X2}, INT {reference.Interrupt}; "
                    + $"was {actual:X2}, status {chip.ReadStatus():X2}, INT {chip.Interrupt}");
            }
        }

        void Pass(long cycles)
        {
            chip.Run(cycles);
            for (long i = 0; i < cycles; i++)
            {
                reference.Tick();
            }
        }

        // Each access is in a cycle of its own, after that cycle's tick.
        byte Read(int register)
        {
            Pass(1);
            (byte expected, byte actual) = register switch
            {
                0 => (reference.ReadStatus(), chip.ReadStatus()),
                1 => (reference.ReadResult(), chip.ReadResult()),
                _ => (reference.ReadData(), chip.ReadData()),
            };
            Same(expected, actual, $"read {register}");
            if (register == 1)
            {
                results.Add(expected);
            }
            return expected;
        }

        void Write(int register, byte value)
        {
            Pass(1);
            switch (register)
            {
                case 0:
                    reference.WriteCommand(value);
                    chip.WriteCommand(value);
                    break;
                case 1:
                    reference.WriteParameter(value);
                    chip.WriteParameter(value);
                    break;
                case 2:
                    reference.WriteReset(value);
                    chip.WriteReset(value);
                    break;
                default:
                    reference.WriteData(value);
                    chip.WriteData(value);
                    break;
            }
            Same(0, 0, $"write {register} {value:X2}");
        }

        for (int step = 0; step < 6_000; step++)
        {
            int choice = random.Next(100);
            if (choice < 22)
            {
                byte[] command = RandomCommand(random);
                int length = random.Next(12) == 0 ? random.Next(command.Length) : command.Length;
                for (int i = 0; i < length; i++)
                {
                    Write(i == 0 ? 0 : 1, command[i]);
                    Pass(random.Next(4) == 0 ? random.Next(6) : 0);
                }
            }
            else if (choice < 40)
            {
                Pass(Span(random));
            }
            else if (choice < 52)
            {
                // DFS's NMI handler, for every byte until the result: 23 to 41 cycles after the
                // NMI (s1h), and now and then too late. The wait for each NMI is cycle by cycle on
                // the oracle and in one span on the lazy chip.
                for (int n = 0; n < 3_000; n++)
                {
                    int wait = 0;
                    while (wait < 3_000 && !reference.Interrupt)
                    {
                        reference.Tick();
                        wait++;
                    }
                    chip.Run(wait);
                    if (!reference.Interrupt)
                    {
                        break;
                    }

                    Pass(random.Next(200) == 0 ? random.Next(100, 140) : random.Next(18, 40));
                    byte status = Read(0);
                    if ((status & 0x04) != 0)
                    {
                        if (random.Next(2) == 0)
                        {
                            Read(4 + random.Next(4));
                        }
                        else
                        {
                            Write(4 + random.Next(4), (byte)random.Next(256));
                        }
                        bytes++;
                    }
                    else if ((status & 0x08) != 0)
                    {
                        Read(1);
                        break;
                    }
                }
            }
            else if (choice < 62)
            {
                Read(0);
            }
            else if (choice < 70)
            {
                Read(1);
            }
            else if (choice < 76)
            {
                Read(4 + random.Next(4));
            }
            else if (choice < 82)
            {
                Write(4 + random.Next(4), (byte)random.Next(256));
            }
            else if (choice < 84)
            {
                Write(2, 1);
                Pass(random.Next(12));
                Write(2, 0);
            }
            else if (choice < 87)
            {
                int drive = random.Next(4) == 0 ? 1 : 0;
                Pass(1);
                if (random.Next(5) == 0)
                {
                    reference.Eject(drive);
                    chip.Eject(drive);
                }
                else
                {
                    (DiscImage forChip, DiscImage forReference) = discs[random.Next(discs.Length)];
                    reference.Insert(drive, forReference);
                    chip.Insert(drive, forChip);
                }
                Same(0, 0, "insert");
            }
            else if (choice < 89)
            {
                (DiscImage forChip, DiscImage forReference) = discs[random.Next(discs.Length)];
                bool readOnly = !forReference.ReadOnly;
                forChip.ReadOnly = readOnly;
                forReference.ReadOnly = readOnly;
            }
            else
            {
                // The port read back as DFS reads it, which shows whether the head has unloaded.
                Write(0, 0x7D);
                Write(1, 0x23);
                Read(1);
            }
        }

        foreach ((DiscImage forChip, DiscImage forReference) in discs)
        {
            Assert.Equal(forReference.ToBytes(), forChip.ToBytes());
        }

        // Every path was taken: each result DFS can meet, bytes moved, and the head unloading.
        Assert.Superset(new HashSet<byte> { 0x00, 0x0A, 0x10, 0x12, 0x18 }, results);
        Assert.True(bytes > 2_000, $"only {bytes} bytes moved");
        Assert.True(reference.Unloads > 20, $"the head unloaded only {reference.Unloads} times");
    }

    /// <summary>
    /// The bus alone against the oracle bus: random 8271 accesses at every address of
    /// $FE80-$FE9F, DFS's handler taking bytes, VIA reads (which are stretched), and long gaps of RAM
    /// reads. After every access the value, the cycle count and the CPU's IRQ and NMI lines must
    /// match, so the bus must look at the 8271 in the very cycle of each byte and result.
    /// </summary>
    [Fact]
    public void TheBusSetsTheNmiInTheCycleTheOracleDoes()
    {
        var bus = new BbcBus(BbcSession.Roms);
        var oracle = new ReferenceBbcBus(BbcSession.Roms);
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        var oracleCpu = new Cpu(oracle, CpuVariant.Nmos6502);
        bus.Cpu = cpu;
        oracle.Cpu = oracleCpu;
        bus.PowerOnReset();
        oracle.PowerOnReset();

        var random = new Random(8271);
        var discs = MakeDiscs(random);
        var seen = new Dictionary<byte, int>();
        oracle.Fdc.Insert(0, discs[0].Item2);
        bus.Fdc.Insert(0, discs[0].Item1);
        long accesses = 0, nmiRises = 0;
        bool nmiBefore = false;

        void Same(byte expected, byte actual)
        {
            accesses++;
            if (expected != actual || oracle.Cycles != bus.Cycles || oracleCpu.Irq != cpu.Irq || oracleCpu.Nmi != cpu.Nmi)
            {
                Assert.Fail($"after access {accesses} at cycle {oracle.Cycles}: expected value {expected:X2}, IRQ {oracleCpu.Irq}, NMI {oracleCpu.Nmi}; "
                    + $"was {actual:X2}, IRQ {cpu.Irq}, NMI {cpu.Nmi}, at cycle {bus.Cycles}");
            }
            nmiRises += cpu.Nmi && !nmiBefore ? 1 : 0;
            nmiBefore = cpu.Nmi;
        }

        void Ram(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Same(oracle.Read(0x0100), bus.Read(0x0100));
            }
        }

        ushort Fdc(int register) => (ushort)(0xFE80 + (8 * random.Next(4)) + register + (register == 4 ? random.Next(4) : 0));

        void Write(int register, byte value)
        {
            ushort address = Fdc(register);
            oracle.Write(address, value);
            bus.Write(address, value);
            Same(0, 0);
        }

        // The handler, until the result: wait for each NMI through RAM reads and now and then a
        // stretched VIA read, then take the byte as the handler does, mostly in time and now and
        // then too late.
        void Handler()
        {
            // Only while a command runs or a result waits (the oracle's status, read without effect).
            for (int n = 0; n < 3_000 && (oracle.Fdc.ReadStatus() & 0x88) != 0; n++)
            {
                for (int wait = 0; wait < 3_000 && !oracleCpu.Nmi; wait++)
                {
                    if (random.Next(8) == 0)
                    {
                        ushort via = (ushort)(0xFE40 + random.Next(0x40));
                        Same(oracle.Read(via), bus.Read(via));
                    }
                    else
                    {
                        Ram(1);
                    }
                }
                if (!oracleCpu.Nmi)
                {
                    break;
                }

                Ram(random.Next(300) == 0 ? random.Next(100, 130) : random.Next(15, 40));
                ushort status = Fdc(0);
                byte value = oracle.Read(status);
                Same(value, bus.Read(status));
                if ((value & 0x04) != 0)
                {
                    if (random.Next(2) == 0)
                    {
                        ushort data = Fdc(4);
                        Same(oracle.Read(data), bus.Read(data));
                    }
                    else
                    {
                        Write(4, (byte)random.Next(256));
                    }
                }
                else if ((value & 0x08) != 0)
                {
                    ushort result = Fdc(1);
                    byte code = oracle.Read(result);
                    Same(code, bus.Read(result));
                    seen[code] = seen.GetValueOrDefault(code) + 1;
                    break;
                }
            }
        }

        // DFS's start: reset, then its specify table, with a short index count so the head unloads.
        Write(2, 1);
        Write(2, 0);
        foreach (byte value in new byte[] { 0x35, 0x0D, 0x02, 0x08, 0x10 })
        {
            Write(value == 0x35 ? 0 : 1, value);
        }

        while (accesses < 1_500_000)
        {
            int choice = random.Next(100);
            if (choice < 15)
            {
                byte[] command = RandomCommand(random);
                for (int i = 0; i < command.Length; i++)
                {
                    Write(i == 0 ? 0 : 1, command[i]);
                    Ram(random.Next(3));
                }
                if (random.Next(5) != 0)
                {
                    Handler();
                }
            }
            else if (choice < 40)
            {
                Handler();
            }
            else if (choice < 70)
            {
                int register = random.Next(8);
                ushort address = (ushort)(0xFE80 + (8 * random.Next(4)) + register);
                Same(oracle.Read(address), bus.Read(address));
            }
            else if (choice < 80)
            {
                // A VIA read: stretched, so a step can fall inside the stretch.
                ushort via = (ushort)(0xFE40 + random.Next(0x40));
                Same(oracle.Read(via), bus.Read(via));
            }
            else if (choice < 95)
            {
                Ram(random.Next(random.Next(40) == 0 ? 20_000 : 300));
            }
            else if (choice < 98)
            {
                (DiscImage forBus, DiscImage forOracle) = discs[random.Next(discs.Length)];
                int drive = random.Next(4) == 0 ? 1 : 0;
                oracle.Fdc.Insert(drive, forOracle);
                bus.Fdc.Insert(drive, forBus);
            }
            else
            {
                Write(0, 0x3A);
                Write(1, 0x23);
                Write(1, (byte)(0x08 | (random.Next(4) == 0 ? 0x80 : 0x40) | (random.Next(2) == 0 ? 0x20 : 0)));
            }
        }

        foreach ((DiscImage forBus, DiscImage forOracle) in discs)
        {
            Assert.Equal(forOracle.ToBytes(), forBus.ToBytes());
        }
        Assert.True(nmiRises > 5_000, $"the NMI line rose only {nmiRises} times");
        Assert.Superset(new HashSet<byte> { 0x00, 0x0A, 0x10, 0x12, 0x18 }, seen.Keys.ToHashSet());
    }

    /// <summary>
    /// The whole machine on DFS against the oracle bus, an instruction at a time, every bus access
    /// compared, the NMI line included: boot with a blank disc, then save a file, catalogue the
    /// disc, load the file back and run it. Each image is the machine's own, and at the end the two
    /// must hold the same bytes.
    /// </summary>
    [Fact]
    public void TheMachineMatchesTheOracleThroughDfs()
    {
        DiscImage forMachine = DiscImage.Blank(40, false), forOracle = DiscImage.Blank(40, false);
        var bus = new BbcBus(BbcSession.Roms);
        var oracle = new ReferenceBbcBus(BbcSession.Roms);
        var busLog = new Recorder(bus);
        var oracleLog = new Recorder(oracle);
        var cpu = new Cpu(busLog, CpuVariant.Nmos6502);
        var oracleCpu = new Cpu(oracleLog, CpuVariant.Nmos6502);
        busLog.Cpu = cpu;
        oracleLog.Cpu = oracleCpu;
        bus.Cpu = cpu;
        oracle.Cpu = oracleCpu;
        bus.Fdc.Insert(0, forMachine);
        oracle.Fdc.Insert(0, forOracle);
        long nmiAccesses = 0, fdcAccesses = 0;

        void Run(long cycles)
        {
            long end = oracle.Cycles + cycles;
            while (oracle.Cycles < end)
            {
                cpu.Step();
                oracleCpu.Step();
                for (int i = 0; i < Math.Min(oracleLog.Log.Count, busLog.Log.Count); i++)
                {
                    if (oracleLog.Log[i] != busLog.Log[i])
                    {
                        Assert.Fail($"access {i} of the instruction at cycle {oracleLog.Log[0].Cycles}: expected {oracleLog.Log[i]}, was {busLog.Log[i]}");
                    }
                    nmiAccesses += oracleLog.Log[i].Nmi ? 1 : 0;
                    fdcAccesses += oracleLog.Log[i].Address is >= 0xFE80 and <= 0xFE9F ? 1 : 0;
                }
                Assert.Equal(oracleLog.Log.Count, busLog.Log.Count);
                oracleLog.Log.Clear();
                busLog.Log.Clear();
            }
        }

        void Type(string text)
        {
            foreach (char c in text)
            {
                (BbcKey key, bool shift) = BbcSession.Keys[c];
                foreach (BbcKeyboard keyboard in new[] { bus.Keyboard, oracle.Keyboard })
                {
                    if (shift)
                    {
                        keyboard.Press(BbcKey.Shift);
                    }
                    keyboard.Press(key);
                }
                Run(40_000);
                foreach (BbcKeyboard keyboard in new[] { bus.Keyboard, oracle.Keyboard })
                {
                    keyboard.Release(key);
                    keyboard.Release(BbcKey.Shift);
                }
                Run(40_000);
            }
        }

        bus.PowerOnReset();
        oracle.PowerOnReset();
        cpu.Reset();
        oracleCpu.Reset();
        Run(6_000_000);
        byte[] program = [0xA9, 0x41, 0x20, 0xEE, 0xFF, 0x60];
        for (int i = 0; i < program.Length; i++)
        {
            bus.PokeRam((ushort)(0x2000 + i), program[i]);
            oracle.PokeRam((ushort)(0x2000 + i), program[i]);
        }

        foreach (string line in new[] { "*SAVE TEST 2000 2100 2000 2000\r", "*CAT\r", "*LOAD TEST 4000\r", "*RUN TEST\r" })
        {
            Type(line);
            Run(1_000_000);
        }

        Assert.Equal(forOracle.ToBytes(), forMachine.ToBytes());
        Assert.Equal(program, Enumerable.Range(0, program.Length).Select(i => bus.Peek((ushort)(0x4000 + i))).ToArray());
        Assert.True(nmiAccesses > 2_000, $"the NMI line was high on only {nmiAccesses} accesses");
        Assert.True(fdcAccesses > 5_000, $"only {fdcAccesses} accesses reached the 8271");
    }

    private static (DiscImage, DiscImage)[] MakeDiscs(Random random)
    {
        var pairs = new (DiscImage, DiscImage)[4];
        for (int i = 0; i < pairs.Length; i++)
        {
            int tracks = i % 2 == 0 ? 40 : 80;
            bool doubleSided = i >= 2;
            var bytes = new byte[tracks * DiscImage.TrackSize * (doubleSided ? 2 : 1)];
            random.NextBytes(bytes);
            pairs[i] = (DiscImage.FromBytes(bytes, doubleSided), DiscImage.FromBytes(bytes, doubleSided));
            if (i == 3)
            {
                pairs[i].Item1.ReadOnly = pairs[i].Item2.ReadOnly = true;
            }
        }
        return pairs;
    }

    /// <summary>A command and its parameters, leaning on the ones DFS sends (disc.md s2a).</summary>
    private static byte[] RandomCommand(Random random)
    {
        byte drive = (random.Next(10)) switch
        {
            < 6 => 0x40,
            < 8 => 0x80,
            8 => 0x00,
            _ => 0xC0,
        };
        byte track = (byte)(random.Next(5) == 0 ? random.Next(90) : random.Next(3));
        byte sector = (byte)(random.Next(6) == 0 ? random.Next(32) : random.Next(10));
        byte[] sizes = [0x21, 0x21, 0x22, 0x23, 0x2A, 0x20, 0x41, 0x01];
        byte size = random.Next(8) == 0 ? (byte)random.Next(256) : sizes[random.Next(sizes.Length)];
        byte[] ports = [0x48, 0x68, 0x88, 0xA8, 0x08, 0x40, 0x00, 0x4B];
        int[] counts = [0, 1, 1, 2, 12, 15];
        return random.Next(20) switch
        {
            0 or 1 => [(byte)(drive | 0x2C)],
            2 => [(byte)(drive | 0x3D), random.Next(3) switch { 0 => 0x23, 1 => 0x06, _ => (byte)random.Next(256) }],
            3 or 4 => [0x3A, 0x23, random.Next(5) == 0 ? (byte)random.Next(256) : ports[random.Next(ports.Length)]],
            5 => [0x35, 0x0D, (byte)random.Next(16), (byte)random.Next(16), (byte)((counts[random.Next(counts.Length)] << 4) | random.Next(16))],
            6 => [0x35, random.Next(2) == 0 ? (byte)0x10 : (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)],
            7 or 8 => [(byte)(drive | 0x29), track],
            9 or 10 or 11 => [(byte)(drive | 0x13), track, sector, size],
            12 or 13 => [(byte)(drive | 0x0B), track, sector, size],
            14 => [(byte)(drive | 0x1F), track, sector, size],
            15 => [(byte)(drive | 0x12), track, sector],
            16 => [(byte)(drive | 0x0A), track, sector],
            17 => [(byte)(drive | (random.Next(2) == 0 ? 0x1B : 0x23)), track, sector, size, 0, 0],
            18 => [(byte)(drive | 0x17), track, sector, size],
            _ => [(byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)],
        };
    }

    private static long Span(Random random) => random.Next(100) switch
    {
        < 30 => random.Next(6),
        < 60 => random.Next(200),
        < 80 => random.Next(2_000),
        < 97 => random.Next(20_000),
        _ => random.Next(300_000, 1_300_000),
    };

    private readonly record struct Access(ushort Address, byte Value, bool Write, long Cycles, bool Irq, bool Nmi);

    /// <summary>An IBus between a CPU and a bus that writes down every access, with the lines after it.</summary>
    private sealed class Recorder(IBus inner) : IBus
    {
        public List<Access> Log { get; } = [];

        public Cpu? Cpu { get; set; }

        private long Cycles => inner switch
        {
            BbcBus bus => bus.Cycles,
            ReferenceBbcBus oracle => oracle.Cycles,
            _ => 0,
        };

        public byte Read(ushort address)
        {
            byte value = inner.Read(address);
            Log.Add(new Access(address, value, false, Cycles, Cpu!.Irq, Cpu.Nmi));
            return value;
        }

        public void Write(ushort address, byte value)
        {
            inner.Write(address, value);
            Log.Add(new Access(address, value, true, Cycles, Cpu!.Irq, Cpu.Nmi));
        }
    }
}
