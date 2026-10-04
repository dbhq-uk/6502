using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The 6522 on its own, one 1 MHz cycle at a time. Every expected value is from the
/// fact sheet <c>docs/bbc-micro/facts/via.md</c> section 1, and the sixteen tests named
/// <c>ExampleNN</c> are its worked examples in section 1.10, read at the cycles it gives.
/// </summary>
/// <remarks>
/// W is the cycle in which the write that starts a timer happens. <see cref="Bench.Write"/>
/// starts a new cycle (one <see cref="Via6522.Tick"/>) and writes in it; <see cref="Bench.ReadAt"/>
/// moves on to cycle W+k and reads in it. A read that clears a flag changes what later
/// reads see, so a test that needs a value at every cycle and also needs the flag
/// untouched builds a fresh chip for each cycle. Some tests read a second register in the same
/// cycle as an access (IFR after a T1C-L read, say): that is a look at the chip's state after
/// the access, not two bus accesses in one cycle, which a CPU cannot make.
/// </remarks>
public class Via6522Tests
{
    private const int Orb = 0x0, Ora = 0x1, Ddrb = 0x2, Ddra = 0x3;
    private const int T1CL = 0x4, T1CH = 0x5, T1LL = 0x6, T1LH = 0x7;
    private const int T2CL = 0x8, T2CH = 0x9, Sr = 0xA, Acr = 0xB, Pcr = 0xC, Ifr = 0xD, Ier = 0xE, OraNoHandshake = 0xF;

    /// <summary>A chip and a cycle counter that reads 0 in cycle W.</summary>
    private sealed class Bench
    {
        public Via6522 Via { get; } = new();

        /// <summary>Cycles since W: 0 during W, k during W+k.</summary>
        public int Now { get; private set; }

        /// <summary>Starts the next cycle and writes in it.</summary>
        public void Write(int register, byte value)
        {
            Via.Tick();
            Now++;
            Via.Write(register, value);
        }

        /// <summary>Writes in the next cycle, and calls that cycle W.</summary>
        public void WriteW(int register, byte value)
        {
            Write(register, value);
            Now = 0;
        }

        /// <summary>Reads in the next cycle, and calls that cycle W.</summary>
        public byte ReadW(int register)
        {
            Via.Tick();
            Now = 0;
            return Via.Read(register);
        }

        /// <summary>Starts cycles until the current one is W+k.</summary>
        public void To(int k)
        {
            Assert.True(k >= Now, $"cycle W+{k} has already passed (now W+{Now})");
            while (Now < k)
            {
                Via.Tick();
                Now++;
            }
        }

        /// <summary>Reads in cycle W+k.</summary>
        public byte ReadAt(int k, int register)
        {
            To(k);
            return Via.Read(register);
        }

        /// <summary>Reads IFR in cycle W+k. Reading IFR clears nothing (section 1.6).</summary>
        public byte IfrAt(int k) => ReadAt(k, Ifr);

        public bool IrqAt(int k)
        {
            To(k);
            return Via.Irq;
        }
    }

    /// <summary>
    /// Timer 1 started with latch N: the low latch in the cycle before W, T1C-H in W. The
    /// counter is first sent far from zero and IFR cleared, because a timer runs from power on:
    /// left at its starting count, a free-running timer expires during the setup, and once in
    /// the very cycle of the T1C-H write, where the write cannot clear it (section 1.9).
    /// </summary>
    private static Bench StartTimer1(byte acr, ushort n, byte ier = 0xC0, byte ddrb = 0x00)
    {
        var bench = new Bench();
        bench.Write(T1CL, 0xFF);
        bench.Write(T1CH, 0xFF);
        bench.Write(Ifr, 0x7F);
        bench.Write(Acr, acr);
        bench.Write(Ier, ier);
        bench.Write(Ddrb, ddrb);
        bench.Write(T1CL, (byte)n);
        bench.WriteW(T1CH, (byte)(n >> 8));
        return bench;
    }

    /// <summary>Timer 2 started with N in one-shot mode unless the ACR says otherwise.</summary>
    private static Bench StartTimer2(byte acr, ushort n, byte ier = 0xA0)
    {
        var bench = new Bench();
        bench.Write(Acr, acr);
        bench.Write(Ier, ier);
        bench.Write(T2CL, (byte)n);
        bench.WriteW(T2CH, (byte)(n >> 8));
        return bench;
    }

    /// <summary>Runs until IFR6 has been set by a one-shot N=4 timer, then leaves the bench in W+7.</summary>
    private static Bench Timer1FlagSet(byte ier)
    {
        var bench = StartTimer1(acr: 0x00, n: 4, ier: ier);
        bench.To(7);
        return bench;
    }

    [Fact]
    public void Example01_OneShotN4_CountsDownThenFlagsAndIrqInW6()
    {
        byte[] expected = [4, 3, 2, 1, 0, 0xFF];
        for (int k = 1; k <= 6; k++)
        {
            Assert.Equal(expected[k - 1], StartTimer1(acr: 0x00, n: 4).ReadAt(k, T1CL));
        }

        var bench = StartTimer1(acr: 0x00, n: 4);
        for (int k = 1; k <= 5; k++)
        {
            Assert.Equal(0x00, bench.IfrAt(k));
            Assert.False(bench.Via.Irq, $"IRQ asserted in W+{k}");
        }

        Assert.Equal(0xC0, bench.IfrAt(6));
        Assert.True(bench.Via.Irq);
        Assert.Equal(0xC0, bench.IfrAt(7));
        Assert.True(bench.Via.Irq);
    }

    [Fact]
    public void Example02_NZero_ReadsZeroInW1AndFlagsInW2()
    {
        Assert.Equal(0x00, StartTimer1(acr: 0x00, n: 0).ReadAt(1, T1CL));
        Assert.Equal(0xFF, StartTimer1(acr: 0x00, n: 0).ReadAt(2, T1CL));

        var bench = StartTimer1(acr: 0x00, n: 0);
        Assert.Equal(0x00, bench.IfrAt(1) & 0x40);
        Assert.Equal(0x40, bench.IfrAt(2) & 0x40);
    }

    [Fact]
    public void Example03_FreeRunN4_FlagsEverySixCyclesAndAnAcknowledgeDoesNotMoveThePeriod()
    {
        // The counter over two periods: 4,3,2,1,0,&FF then the latch again.
        byte[] expected = [4, 3, 2, 1, 0, 0xFF, 4, 3, 2, 1, 0, 0xFF, 4];
        var counter = StartTimer1(acr: 0x40, n: 4);
        for (int k = 1; k <= expected.Length; k++)
        {
            Assert.Equal(expected[k - 1], counter.ReadAt(k, T1CL));
        }

        // The flag rises in W+6, W+12 and W+18 and in no other cycle. Each is
        // acknowledged by an IFR write in the cycle after it rose.
        var flags = StartTimer1(acr: 0x40, n: 4);
        var rose = new List<int>();
        for (int k = 1; k <= 20; k++)
        {
            if ((flags.IfrAt(k) & 0x40) != 0)
            {
                rose.Add(k);
                flags.To(k + 1);
                Assert.Equal(0x40, flags.Via.Read(Ifr) & 0x40); // still set in the cycle after: nothing clears it but the write
                flags.Via.Write(Ifr, 0x40);
                k++;
            }
        }
        Assert.Equal([6, 12, 18], rose);

        // A T1C-L read in W+7 clears the flag, and the next one is still in W+12.
        var ack = StartTimer1(acr: 0x40, n: 4);
        Assert.Equal(0x40, ack.IfrAt(6) & 0x40);
        ack.ReadAt(7, T1CL);
        Assert.Equal(0x00, ack.Via.Read(Ifr) & 0x40);
        for (int k = 8; k <= 11; k++)
        {
            Assert.Equal(0x00, ack.IfrAt(k) & 0x40);
        }
        Assert.Equal(0x40, ack.IfrAt(12) & 0x40);
    }

    [Theory]
    [InlineData(0x80)] // DDRB7 an output, as the example says
    [InlineData(0x00)] // DDRB7 an input: PB7 still reads the timer (section 1.3)
    public void Example04_FreeRunWithPb7_Pb7TogglesEverySixCycles(byte ddrb)
    {
        var bench = StartTimer1(acr: 0xC0, n: 4, ddrb: ddrb);
        for (int k = 1; k <= 36; k++)
        {
            int expected = (k / 6) % 2 == 1 ? 0x80 : 0x00;
            Assert.Equal(expected, bench.ReadAt(k, Orb) & 0x80);
            Assert.Equal(expected, bench.Via.PortBPins & 0x80);
        }
    }

    [Fact]
    public void Example05_OneShotWithPb7_Pb7RisesInW6AndStaysHigh()
    {
        var bench = StartTimer1(acr: 0x80, n: 4, ddrb: 0x80);
        for (int k = 1; k <= 40; k++)
        {
            int expected = k >= 6 ? 0x80 : 0x00;
            Assert.Equal(expected, bench.ReadAt(k, Orb) & 0x80);
        }
    }

    [Fact]
    public void Example06_TheOsTick_FlagsEvery10000CyclesExactly()
    {
        // The OS's own sequence (section 1.4): T1L-L = &0E, T1L-H = &27, then T1C-H = &27.
        // First the counter is sent far from zero, as in StartTimer1.
        var bench = new Bench();
        bench.Write(T1CL, 0xFF);
        bench.Write(T1CH, 0xFF);
        bench.Write(Ifr, 0x7F);
        bench.Write(Acr, 0x40);
        bench.Write(Ier, 0xC0);
        bench.Write(T1LL, 0x0E);
        bench.Write(T1LH, 0x27);
        bench.WriteW(T1CH, 0x27);

        var rose = new List<int>();
        for (int k = 1; k <= 30_001; k++)
        {
            if ((bench.IfrAt(k) & 0x40) != 0)
            {
                rose.Add(k);
                bench.To(k + 1);
                bench.Via.Write(Ifr, 0x40); // the OS acknowledges with an IFR write
                k++;
            }
        }
        Assert.Equal([10_000, 20_000, 30_000], rose);
    }

    [Fact]
    public void Example07_Timer2OneShotN3_DoesNotReloadAndFlagsOnce()
    {
        byte[] expected = [3, 2, 1, 0, 0xFF, 0xFE];
        for (int k = 1; k <= 6; k++)
        {
            Assert.Equal(expected[k - 1], StartTimer2(acr: 0x00, n: 3).ReadAt(k, T2CL));
        }

        var bench = StartTimer2(acr: 0x00, n: 3);
        for (int k = 1; k <= 4; k++)
        {
            Assert.Equal(0x00, bench.IfrAt(k) & 0x20);
        }
        Assert.Equal(0xA0, bench.IfrAt(5));
        bench.To(6);
        bench.Via.Write(Ifr, 0x20);

        // Past a whole wrap of the 16-bit counter, the flag does not come back.
        for (int k = 7; k <= 70_000; k++)
        {
            Assert.Equal(0x00, bench.IfrAt(k) & 0x20);
        }

        // A T2C-H write re-arms it.
        bench.To(70_001);
        bench.Via.Write(T2CH, 0x00);
        Assert.Equal(0x00, bench.IfrAt(70_005) & 0x20);
        Assert.Equal(0x20, bench.IfrAt(70_006) & 0x20);
    }

    [Fact]
    public void Example08_Timer2CountingPb6PulsesWithPb6HeldHigh_NeverCounts()
    {
        var bench = new Bench();
        bench.Via.PortBInput = 0xFF; // PB6 held high
        bench.Write(Acr, 0x60);
        bench.Write(Ier, 0xA0);
        bench.Write(T2CL, 0x00);
        bench.WriteW(T2CH, 0x00);

        for (int k = 1; k <= 70_000; k++)
        {
            Assert.Equal(0x00, bench.IfrAt(k) & 0x20);
        }
        Assert.Equal(0x00, bench.Via.Read(T2CL));
        Assert.Equal(0x00, bench.Via.Read(T2CH));
        Assert.False(bench.Via.Irq);
    }

    [Fact]
    public void Example09_AnAcknowledgeInTheCycleTheFlagRises_DoesNotClearItAndDelaysIrqOneCycle()
    {
        var bench = StartTimer1(acr: 0x00, n: 4);

        Assert.Equal(0xFF, bench.ReadAt(6, T1CL)); // the read in W+6 that would acknowledge
        Assert.Equal(0xC0, bench.Via.Read(Ifr));   // the flag is still set
        Assert.False(bench.Via.Irq);               // and IRQ is not asserted in W+6

        Assert.True(bench.IrqAt(7));               // it is from the start of W+7
        Assert.Equal(0xC0, bench.Via.Read(Ifr));

        bench.Via.Read(T1CL);                      // a read in W+7 clears it
        Assert.Equal(0x00, bench.Via.Read(Ifr));
        Assert.False(bench.Via.Irq);
    }

    // Example 9 is a T1C-L read. The rule (section 1.9; the class remarks) is for any clearing
    // access in the cycle a clocked flag rises, so the other ways of clearing timer 1's flag, and
    // timer 2's, are held to it too. The sheet's measurement is for timer 1; for timer 2 this pins
    // the model's choice, not the chip.

    [Theory]
    [InlineData("T1C-H write")]
    [InlineData("IFR write")]
    public void AnyClearingAccessToTimer1InTheCycleItsFlagRises_DoesNotClearItAndDelaysIrqOneCycle(string access)
    {
        var bench = StartTimer1(acr: 0x00, n: 4);
        bench.To(6);
        if (access == "T1C-H write")
        {
            bench.Via.Write(T1CH, 0x00);
        }
        else
        {
            bench.Via.Write(Ifr, 0x40);
        }

        Assert.Equal(0xC0, bench.Via.Read(Ifr));
        Assert.False(bench.Via.Irq);
        Assert.True(bench.IrqAt(7));
        Assert.Equal(0xC0, bench.Via.Read(Ifr));
    }

    [Fact]
    public void AT2CLReadInTheCycleTimer2sFlagRises_DoesNotClearItAndDelaysIrqOneCycle()
    {
        var bench = StartTimer2(acr: 0x00, n: 4);

        Assert.Equal(0xFF, bench.ReadAt(6, T2CL)); // the read in W+6 that would acknowledge
        Assert.Equal(0xA0, bench.Via.Read(Ifr));   // the flag is still set
        Assert.False(bench.Via.Irq);

        Assert.True(bench.IrqAt(7));
        bench.Via.Read(T2CL);                      // a read in W+7 clears it
        Assert.Equal(0x00, bench.Via.Read(Ifr));
        Assert.False(bench.Via.Irq);
    }

    [Fact]
    public void Example10_AnIfrWriteClearsTheFlagAndBit7FollowsIt()
    {
        var bench = Timer1FlagSet(ier: 0xC0);
        Assert.Equal(0xC0, bench.Via.Read(Ifr));

        bench.Via.Write(Ifr, 0x40);

        Assert.Equal(0x00, bench.Via.Read(Ifr));
        Assert.False(bench.Via.Irq);
    }

    [Fact]
    public void Example11_IfrBit7IsTheFlagsAndedWithIer()
    {
        var bench = Timer1FlagSet(ier: 0x00);
        Assert.Equal(0x40, bench.Via.Read(Ifr));
        Assert.False(bench.Via.Irq);

        bench.Via.Write(Ier, 0xC0);

        Assert.Equal(0xC0, bench.Via.Read(Ifr));
        Assert.True(bench.Via.Irq);
    }

    [Fact]
    public void Example12_AfterResetIerReads80AndBit7SelectsSetOrClear()
    {
        var via = new Via6522();
        via.Write(Ier, 0xFF);
        via.Reset();
        Assert.Equal(0x80, via.Read(Ier));

        via.Write(Ier, 0x7F);
        Assert.Equal(0x80, via.Read(Ier));

        via.Write(Ier, 0xF2);
        Assert.Equal(0xF2, via.Read(Ier));
    }

    [Fact]
    public void Example13_AfterResetThePortsAndDirectionsAreZeroAndPortBReadsItsPins()
    {
        var via = new Via6522();
        via.Write(Ddra, 0xFF);
        via.Write(Ddrb, 0xFF);
        via.Write(Ora, 0x5A);
        via.Write(Orb, 0xA5);
        via.Reset();

        Assert.Equal(0x00, via.Read(Ddra));
        Assert.Equal(0x00, via.Read(Ddrb));

        via.PortAInput = 0x00;
        via.PortBInput = 0x00;
        Assert.Equal(0x00, via.Read(Ora));
        Assert.Equal(0x00, via.Read(Orb));

        // The output registers themselves were cleared: made outputs, they drive 0.
        via.Write(Ddra, 0xFF);
        via.Write(Ddrb, 0xFF);
        Assert.Equal(0x00, via.Read(Ora));
        Assert.Equal(0x00, via.Read(Orb));

        // All inputs again, pulled high: port B reads &FF.
        via.Write(Ddrb, 0x00);
        via.PortBInput = 0xFF;
        Assert.Equal(0xFF, via.Read(Orb));
    }

    [Fact]
    public void Example14_PortBReadsOutputBitsFromOrbAndInputBitsFromThePins()
    {
        var via = new Via6522();
        via.Write(Ddrb, 0x0F);
        via.Write(Orb, 0x0A);
        via.PortBInput = 0xF0; // PB4-7 high; the low pins are driven by the chip

        Assert.Equal(0xFA, via.Read(Orb));
        Assert.Equal(0xFA, via.PortBPins);
    }

    [Fact]
    public void Example15_PortAReadsThePins()
    {
        var via = new Via6522();
        via.Write(Ddra, 0x7F);
        via.Write(Ora, 0x35);
        via.PortAInput = 0x80; // PA7 driven high from outside

        Assert.Equal(0xB5, via.Read(Ora));
        Assert.Equal(0xB5, via.Read(OraNoHandshake));
        Assert.Equal(0xB5, via.PortAPins);
    }

    [Fact]
    public void Example16_AT1LHWriteAfterAOneShotExpiryClearsIfr6()
    {
        var bench = Timer1FlagSet(ier: 0xC0);
        Assert.Equal(0xC0, bench.Via.Read(Ifr));

        bench.Via.Write(T1LH, 0x00);

        Assert.Equal(0x00, bench.Via.Read(Ifr));
    }

    [Fact]
    public void ReadingT1CHOrT1LLDoesNotClearIfr6()
    {
        var bench = Timer1FlagSet(ier: 0xC0);
        bench.Via.Read(T1CH);
        bench.Via.Read(T1LL);
        bench.Via.Read(T1LH);
        Assert.Equal(0xC0, bench.Via.Read(Ifr));
    }

    [Fact]
    public void AT1CHWriteClearsIfr6()
    {
        var bench = Timer1FlagSet(ier: 0xC0);
        bench.Via.Write(T1CH, 0x00);
        Assert.Equal(0x00, bench.Via.Read(Ifr));
    }

    [Fact]
    public void ALatchWriteDuringAFreeRunCountChangesOnlyTheNextPeriod()
    {
        // Section 1.4: a T1L-L or T1L-H write changes the latch, never the running count.
        var bench = StartTimer1(acr: 0x40, n: 4);
        bench.To(2);
        bench.Via.Write(T1LL, 0x02);
        Assert.Equal(2, bench.ReadAt(3, T1CL));      // still counting the old N: 4,3,2
        Assert.Equal(0x40, bench.IfrAt(6) & 0x40);   // first expiry where N=4 put it
        Assert.Equal(2, bench.ReadAt(7, T1CL));      // then the new latch
        Assert.Equal(0x40, bench.IfrAt(10) & 0x40);  // and a period of 2+2
    }

    [Fact]
    public void OneShotTimer1ReloadsFromTheLatchAfterExpiry_AnAssumption()
    {
        // via.md s1.4 leaves this unresolved: the datasheet text says the counter keeps
        // decrementing, the datasheet figure and a real-machine report say it reloads.
        // The chip follows the figure and the report; docs/known-differences.md says so.
        Assert.Equal(4, StartTimer1(acr: 0x00, n: 4).ReadAt(7, T1CL));
        Assert.Equal(3, StartTimer1(acr: 0x00, n: 4).ReadAt(8, T1CL));
    }

    [Fact]
    public void OneShotTimer1FlagsOnlyOnceUntilRearmed()
    {
        var bench = StartTimer1(acr: 0x00, n: 4);
        Assert.Equal(0x40, bench.IfrAt(6) & 0x40);
        bench.To(7);
        bench.Via.Write(Ifr, 0x40);
        for (int k = 8; k <= 40; k++)
        {
            Assert.Equal(0x00, bench.IfrAt(k) & 0x40);
        }
    }

    [Fact]
    public void Timer2CountingPb6Pulses_CountsFallingEdgesAndFlagsAtZero()
    {
        // Section 1.5 and WDC 2.10: the counter moves only when PB6 pulses low, and
        // IFR5 sets when the count reaches zero.
        var bench = new Bench();
        bench.Via.PortBInput = 0xFF;
        bench.Write(Acr, 0x20);
        bench.Write(Ier, 0xA0);
        bench.Write(T2CL, 0x02);
        bench.WriteW(T2CH, 0x00);
        bench.To(5);
        Assert.Equal(2, bench.Via.Read(T2CL));

        for (int pulse = 1; pulse <= 2; pulse++)
        {
            bench.Via.PortBInput = 0xBF; // PB6 low
            bench.To(bench.Now + 2);
            bench.Via.PortBInput = 0xFF; // and high again
            bench.To(bench.Now + 2);
            Assert.Equal(pulse == 2 ? 0xA0 : 0x00, bench.Via.Read(Ifr)); // before the T2C-L read clears it
            Assert.Equal(2 - pulse, bench.Via.Read(T2CL));
        }
    }

    [Fact]
    public void Ca1NegativeEdgeWithPcr04SetsIfr1AndAPositiveEdgeDoesNot()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x04);
        via.SetCa1(true);
        Assert.Equal(0x00, via.Read(Ifr));
        via.SetCa1(false);
        Assert.Equal(0x02, via.Read(Ifr));
    }

    [Fact]
    public void Ca1PositiveEdgeWithPcrBit0Set()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x01);
        via.SetCa1(true);
        Assert.Equal(0x02, via.Read(Ifr));
    }

    [Fact]
    public void Ca2PositiveEdgeWithPcr04SetsIfr0AsTheKeyboardNeeds()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x04);
        via.Write(Ier, 0x81); // the OS arms the keyboard interrupt with &81
        via.SetCa2(true);
        Assert.Equal(0x81, via.Read(Ifr));
        Assert.True(via.Irq);
        via.SetCa2(false);
        via.Write(Ifr, 0x01);
        Assert.Equal(0x00, via.Read(Ifr));
    }

    [Fact]
    public void Ca2NegativeEdgeWithPcr00()
    {
        var via = new Via6522();
        via.SetCa2(true);
        Assert.Equal(0x00, via.Read(Ifr));
        via.SetCa2(false);
        Assert.Equal(0x01, via.Read(Ifr));
    }

    [Fact]
    public void Cb1NegativeEdgeSetsIfr4AndCb2PositiveEdgeWithPcr40SetsIfr3()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x40); // CB1 negative edge, CB2 positive-edge input
        via.SetCb1(true);
        via.SetCb1(false);
        via.SetCb2(true);
        Assert.Equal(0x18, via.Read(Ifr));
    }

    [Fact]
    public void AnOraAccessClearsCa1AndCa2ButRegisterFDoesNot()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x05); // CA1 positive edge, CA2 positive-edge input
        via.SetCa1(true);
        via.SetCa2(true);
        Assert.Equal(0x03, via.Read(Ifr));

        via.Read(OraNoHandshake);
        via.Write(OraNoHandshake, 0x00);
        Assert.Equal(0x03, via.Read(Ifr));

        via.Read(Ora);
        Assert.Equal(0x00, via.Read(Ifr));
    }

    [Fact]
    public void InIndependentModeAnOraAccessLeavesTheCa2Flag()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x07); // CA1 positive edge, CA2 independent positive edge
        via.SetCa1(true);
        via.SetCa2(true);
        via.Write(Ora, 0x00);
        Assert.Equal(0x01, via.Read(Ifr));
    }

    [Fact]
    public void AnOrbAccessClearsCb1AndCb2()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x50); // CB1 positive edge, CB2 positive-edge input
        via.SetCb1(true);
        via.SetCb2(true);
        Assert.Equal(0x18, via.Read(Ifr));
        via.Read(Orb);
        Assert.Equal(0x00, via.Read(Ifr));
    }

    [Fact]
    public void Ca2AndCb2HeldLowOrHighByThePcr()
    {
        var via = new Via6522();
        via.Write(Pcr, 0xCC); // CA2 low, CB2 low
        Assert.False(via.Ca2Out);
        Assert.False(via.Cb2Out);
        via.Write(Pcr, 0xEE); // CA2 high, CB2 high: the user VIA's printer strobe at rest
        Assert.True(via.Ca2Out);
        Assert.True(via.Cb2Out);
        Assert.Equal(0xEE, via.Read(Pcr));
    }

    [Fact]
    public void IfrWritesIgnoreBit7AndClearOnlyTheOnesWritten()
    {
        var via = new Via6522();
        via.Write(Pcr, 0x05);
        via.Write(Ier, 0x83);
        via.SetCa1(true);
        via.SetCa2(true);
        Assert.Equal(0x83, via.Read(Ifr));

        via.Write(Ifr, 0x80);
        Assert.Equal(0x83, via.Read(Ifr));

        via.Write(Ifr, 0x02);
        Assert.Equal(0x81, via.Read(Ifr));

        via.Write(Ifr, 0x7F);
        Assert.Equal(0x00, via.Read(Ifr));
        Assert.False(via.Irq);
    }

    [Fact]
    public void IerWritesSetOrClearTheNamedBits()
    {
        // Section 1.1: IER=0, write &F2 -> &F2; then &02 -> &F0; then &7F -> &80.
        var via = new Via6522();
        via.Write(Ier, 0xF2);
        Assert.Equal(0xF2, via.Read(Ier));
        via.Write(Ier, 0x02);
        Assert.Equal(0xF0, via.Read(Ier));
        via.Write(Ier, 0x7F);
        Assert.Equal(0x80, via.Read(Ier));
    }

    [Fact]
    public void ResetClearsTheFlagsAndTheControlRegistersButNotTheTimerLatches()
    {
        var via = new Via6522();
        via.Write(Acr, 0x60);
        via.Write(Pcr, 0x04);
        via.Write(T1LL, 0x0E);
        via.Write(T1LH, 0x27);
        via.Write(T2CL, 0x12);
        via.SetCa1(true);
        via.SetCa1(false);
        Assert.Equal(0x02, via.Read(Ifr));

        via.Reset();

        Assert.Equal(0x00, via.Read(Ifr));
        Assert.Equal(0x80, via.Read(Ier));
        Assert.Equal(0x00, via.Read(Acr));
        Assert.Equal(0x00, via.Read(Pcr));
        Assert.Equal(0x0E, via.Read(T1LL));
        Assert.Equal(0x27, via.Read(T1LH));
    }

    [Fact]
    public void PortWritesRaiseTheirEvents()
    {
        var via = new Via6522();
        int a = 0, b = 0;
        via.PortAWritten += () => a++;
        via.PortBWritten += () => b++;

        via.Write(Ora, 0x01);
        via.Write(OraNoHandshake, 0x02);
        via.Write(Ddra, 0xFF);
        via.Write(Orb, 0x03);
        via.Write(Ddrb, 0x0F);
        via.Write(Acr, 0x00);

        Assert.Equal(3, a);
        Assert.Equal(2, b);
    }

    [Fact]
    public void ShiftMode2_SetsIfr2ShiftMode2CyclesAfterAnSrRead()
    {
        // Section 1.8: the DFS starts the shift with an SR read in mode 010 and polls IFR2.
        // The cycle count is a guess read off a drawing; the constant names it.
        // Here W is the cycle of the SR read that starts the shift.
        var bench = new Bench();
        bench.Write(Ier, 0x84);
        bench.Write(Acr, 0x08);
        bench.ReadW(Sr);

        for (int k = 1; k < Via6522.ShiftMode2Cycles; k++)
        {
            Assert.Equal(0x00, bench.IfrAt(k) & 0x04);
        }
        Assert.Equal(0x84, bench.IfrAt(Via6522.ShiftMode2Cycles));
        Assert.True(bench.Via.Irq);

        bench.To(Via6522.ShiftMode2Cycles + 1);
        bench.Via.Read(Sr);
        Assert.Equal(0x00, bench.Via.Read(Ifr) & 0x04);
        Assert.False(bench.Via.Irq);
    }

    [Fact]
    public void ShiftMode0_SetsNoFlag()
    {
        var bench = new Bench();
        bench.Write(Acr, 0x00);
        bench.ReadW(Sr);
        for (int k = 1; k <= 100; k++)
        {
            Assert.Equal(0x00, bench.IfrAt(k) & 0x04);
        }
    }

    [Fact]
    public void ShiftMode2Cycles_Is19()
    {
        Assert.Equal(19, Via6522.ShiftMode2Cycles);
    }
}
