using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The 8271 on its own, driven the way DFS 1.20 drives it, from the fact sheet <c>disc.md</c>:
/// the sheet's tidy unit test (s3), the status values DFS saw (s1b), not ready (s1d, s1e),
/// write protect, sector not found, side select (s1f, s4), late data and the head unloading (s3).
/// </summary>
/// <remarks>
/// Time is the chip's own: each access below is made after one <see cref="Fdc8271.Tick"/>, as a
/// CPU cycle would be, and <see cref="Chip.Cycle"/> counts the ticks, so a command whose last
/// parameter is written at cycle S has its first byte at S + <see cref="Fdc8271.StartCycles"/>.
/// </remarks>
public class Fdc8271Tests
{
    [Fact]
    public void TheSheetsTidyTestFiveHundredAndTwelveNmisAHundredAndTwentyEightCyclesApart()
    {
        // disc.md s3: "after 3A 17 C1, 35 0D 02 08 C0, 69 00, 53 00 00 22, expect 512 NMIs 128
        // cycles apart, then INT, status $18, result $00". The disc's first two sectors hold a
        // pattern, and each byte is taken 30 cycles after its NMI, inside DFS's measured 23 to 41.
        DiscImage disc = DiscImage.Blank(40, doubleSided: false);
        byte[] pattern = Pattern(512, 3);
        pattern.AsSpan(0, 256).CopyTo(disc.Sector(0, 0, 0));
        pattern.AsSpan(256).CopyTo(disc.Sector(0, 0, 1));
        var chip = new Chip(disc);

        chip.Command(0x3A, 0x17, 0xC1);
        Assert.Equal(0x00, chip.Status());
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xC0);
        Assert.Equal(0x00, chip.Status());
        chip.Command(0x69, 0x00);
        Assert.Equal(0x80, chip.Status());
        chip.RunUntilInterrupt();
        Assert.Equal(0x18, chip.Status());
        Assert.Equal(0x00, chip.Result());
        Assert.False(chip.Fdc.Interrupt);

        chip.Command(0x53, 0x00, 0x00, 0x22);
        long start = chip.Cycle;
        var rises = new List<long>();
        var bytes = new List<byte>();
        bool before = false;
        while (bytes.Count < 512)
        {
            chip.Run(1);
            bool now = chip.Fdc.Interrupt;
            if (now && !before)
            {
                rises.Add(chip.Cycle);
                Assert.Equal(0x8C, chip.Status()); // busy, INT, data request (s1b)
                chip.Run(28);
                bytes.Add(chip.Data());
                Assert.Equal(0x80, chip.Status()); // busy alone between bytes
                Assert.False(chip.Fdc.Interrupt);
                now = false;
            }
            before = now;
        }

        Assert.Equal(512, rises.Count);
        Assert.Equal(start + Fdc8271.StartCycles, rises[0]);
        for (int i = 1; i < rises.Count; i++)
        {
            Assert.Equal(128, rises[i] - rises[i - 1]);
        }
        Assert.Equal(pattern, bytes);

        // Then INT, with the result: one byte time after the last byte.
        chip.RunUntilInterrupt();
        Assert.Equal(rises[^1] + 128, chip.Cycle);
        Assert.Equal(0x18, chip.Status());
        Assert.Equal(0x00, chip.Result());
        Assert.Equal(0x00, chip.Status());
        Assert.False(chip.Fdc.Interrupt);
    }

    [Fact]
    public void TheStatusIsWhatDfsSaw()
    {
        // s1b: idle $00; after a command byte $C0 (busy, command full) until its parameters are in;
        // then busy alone while it runs; an immediate command's result is $10 (result full alone).
        var chip = new Chip(DiscImage.Blank(40, false));
        Assert.Equal(0x00, chip.Status());

        chip.Command(0x69);
        Assert.Equal(0xC0, chip.Status());
        chip.Parameter(0x00);
        Assert.Equal(0x80, chip.Status());
        chip.RunUntilInterrupt();
        chip.Result();

        chip.Command(0x6C);
        Assert.Equal(0x10, chip.Status());
        chip.Result();
        Assert.Equal(0x00, chip.Status());
    }

    [Fact]
    public void ReadDriveStatusShowsReadyOnceTheMotorRunsAndWriteProtect()
    {
        // s1e: bits 6 and 2 are ready, set together; bit 3 write protect; bit 1 track 0. Ready
        // follows the motor, LOAD HEAD (bit 3 of special register $23) on the selected drive (s3).
        DiscImage disc = DiscImage.Blank(40, false);
        var chip = new Chip(disc);

        Assert.Equal(0x02, chip.Immediate(0x6C));          // a disc, track 0, motor off
        chip.Command(0x3A, 0x23, 0x48);                    // select drive 0, motor on (s1f)
        Assert.Equal(0x48, chip.Immediate(0x7D, 0x23));
        Assert.Equal(0x46, chip.Immediate(0x6C));
        disc.ReadOnly = true;
        Assert.Equal(0x4E, chip.Immediate(0x6C));
        Assert.Equal(0x02, chip.Immediate(0xAC));          // drive 1: empty, its head at track 0
        chip.Command(0x3A, 0x23, 0x88);                    // drive 1 selected instead
        Assert.Equal(0x0A, chip.Immediate(0x6C));          // drive 0 now not ready
    }

    [Fact]
    public void NoReadyDriveGivesTenUntilReadDriveStatus()
    {
        // s1d, s3: a drive command with no disc ends with $10, and the not-ready latch holds even
        // once a disc is in, until a Read Drive Status. That one reports not ready and clears the
        // latch (D1 p8-129: issue it twice to clear it on a ready drive); the next says ready.
        var chip = new Chip(null);
        chip.Command(0x3A, 0x17, 0xC1);
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xC0);

        Assert.Equal(0x10, chip.Execute(0x69, 0x00));
        Assert.Equal(0x10, chip.Execute(0x53, 0x00, 0x00, 0x22));
        chip.Fdc.Insert(0, DiscImage.Blank(40, false));
        Assert.Equal(0x10, chip.Execute(0x53, 0x00, 0x00, 0x22));

        Assert.Equal(0x02, chip.Immediate(0x6C) & 0x46);
        Assert.Equal(0x46, chip.Immediate(0x6C) & 0x46);
        Assert.Equal(0x00, chip.Execute(0x69, 0x00));
    }

    [Fact]
    public void ACommandWithNoDriveSelectedIsNotReady()
    {
        var chip = new Chip(DiscImage.Blank(40, false));
        Assert.Equal(0x10, chip.Execute(0x29, 0x00));
        Assert.Equal(0x10, chip.Execute(0xE9, 0x00));
    }

    [Fact]
    public void AWriteToAProtectedDiscGivesTwelveAndLeavesItAlone()
    {
        DiscImage disc = DiscImage.Blank(40, false);
        disc.ReadOnly = true;
        byte[] before = disc.ToBytes();
        var chip = new Chip(disc);
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xC0);

        Assert.Equal(0x12, chip.Execute(0x4B, 0x00, 0x02, 0x21));
        Assert.Equal(0x12, chip.Execute(0x4A, 0x00, 0x02));
        Assert.Equal(before, disc.ToBytes());
    }

    [Theory]
    [InlineData(0x00, 0x0A, 0x21, 0, 0x0A)]   // sector 10: sectors are 0 to 9
    [InlineData(0x28, 0x00, 0x21, 0, 0x00)]   // track 40 of a 40-track disc
    [InlineData(0x00, 0x09, 0x22, 256, 0x0A)] // sector 9 is read, then sector 10 is not there
    [InlineData(0x00, 0x00, 0x41, 0, 0x00)]   // 512-byte sectors: there are none
    public void ASectorOffTheDiscGivesEighteen(byte track, byte sector, byte sizeCount, int bytesFirst, byte scanSector)
    {
        // s3, "Image mapping": anything outside the image gives $18. The scan sector register
        // ($06), which DFS reads for "at tt/ss", says which sector was missed.
        var chip = new Chip(DiscImage.Blank(40, false));
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xC0);

        chip.Command(0x53, track, sector, sizeCount);
        Assert.Equal(bytesFirst, chip.TakeBytes().Count);
        Assert.Equal(0x18, chip.Status());
        Assert.Equal(0x18, chip.Result());
        Assert.Equal(scanSector, chip.Immediate(0x3D, 0x06));
    }

    [Fact]
    public void AHundredAndTwentyEightByteCommandFindsNoSector()
    {
        // Read Data with two parameters asks for a 128-byte sector, and these discs have only
        // 256-byte ones (s1c, s4).
        var chip = new Chip(DiscImage.Blank(40, false));
        Assert.Equal(0x18, chip.Execute(0x52, 0x00, 0x00));
    }

    [Fact]
    public void SideSelectIsBitFiveOfTheDriveControlPort()
    {
        // s4: side 1 is chosen by $23 bit 5 (DFS writes $68), then the same 53 00 00 22. On a
        // single-sided disc side 1 is not there.
        DiscImage dsd = DiscImage.Blank(40, doubleSided: true);
        byte[] side0 = Pattern(256, 5), side1 = Pattern(256, 9);
        side0.CopyTo(dsd.Sector(0, 2, 4));
        side1.CopyTo(dsd.Sector(1, 2, 4));
        var chip = new Chip(dsd);
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xC0);

        chip.Command(0x3A, 0x23, 0x48);
        chip.Command(0x53, 0x02, 0x04, 0x21);
        Assert.Equal(side0, chip.TakeBytes());
        Assert.Equal(0x00, chip.Result());

        chip.Command(0x3A, 0x23, 0x68);
        chip.Command(0x53, 0x02, 0x04, 0x21);
        Assert.Equal(side1, chip.TakeBytes());
        Assert.Equal(0x00, chip.Result());

        chip.Fdc.Insert(0, DiscImage.Blank(40, doubleSided: false));
        Assert.Equal(0x18, chip.Execute(0x53, 0x00, 0x00, 0x21));
    }

    [Fact]
    public void WriteDataPutsEachByteOnTheDisc()
    {
        DiscImage disc = DiscImage.Blank(40, false);
        var chip = new Chip(disc);
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xC0);
        byte[] data = Pattern(512, 7);

        chip.Command(0x4B, 0x03, 0x09, 0x21);
        chip.GiveBytes(data.AsSpan(0, 256).ToArray());
        Assert.Equal(0x00, chip.Result());
        chip.Command(0x4B, 0x04, 0x00, 0x21);
        chip.GiveBytes(data.AsSpan(256).ToArray());
        Assert.Equal(0x00, chip.Result());

        DiscImage expected = DiscImage.Blank(40, false);
        data.AsSpan(0, 256).CopyTo(expected.Sector(0, 3, 9));
        data.AsSpan(256).CopyTo(expected.Sector(0, 4, 0));
        Assert.Equal(expected.ToBytes(), disc.ToBytes());
    }

    [Fact]
    public void VerifyMovesNoDataAndTakesTheSectorsTime()
    {
        // DFS's 5F 00 08 22 before every catalogue write (s2a): no data request, a result after
        // two sectors' worth of byte times.
        var chip = new Chip(DiscImage.Blank(40, false));
        chip.Command(0x5F, 0x00, 0x08, 0x22);
        long start = chip.Cycle;
        chip.RunUntilInterrupt();
        Assert.Equal(start + Fdc8271.StartCycles + (512 * 128), chip.Cycle);
        Assert.Equal(0x00, chip.Result());
    }

    [Fact]
    public void AByteNotTakenInItsTimeEndsTheCommandWithLateData()
    {
        // s3, "Late data": a byte not taken within one byte time ends the command with $0A. The
        // request is withdrawn when the next byte is due, so INT falls for a cycle, and the result
        // raises it again: a fresh NMI edge, even if the CPU lost the byte's own.
        var chip = new Chip(DiscImage.Blank(40, false));
        chip.Command(0x53, 0x00, 0x00, 0x21);
        chip.RunUntilInterrupt();
        long first = chip.Cycle;
        chip.Run(126);
        Assert.Equal(0x8C, chip.Status());
        Assert.Equal(0x80, chip.Status());
        Assert.Equal(first + 128, chip.Cycle);
        Assert.False(chip.Fdc.Interrupt);
        Assert.Equal(0x18, chip.Status());
        Assert.True(chip.Fdc.Interrupt);
        Assert.Equal(0x0A, chip.Result());
    }

    [Fact]
    public void TheHeadUnloadsTheIndexCountOfRevolutionsAfterTheLastCommand()
    {
        // s3: index count (the high nibble of the specify's last parameter, 12 for DFS's $C0)
        // x 400,000 cycles after a drive command ends, port bits 0-4, 6 and 7 clear, so the motor
        // stops and the drive is no longer ready. This is how DFS notices a disc swap (s2b). The
        // port is read through 7D 23 as DFS reads it, sampled in the cycle before and the cycle of
        // the unload, on two chips run the same way.
        foreach (int offset in new[] { -1, 0 })
        {
            var chip = new Chip(DiscImage.Blank(40, false));
            chip.Command(0x35, 0x0D, 0x02, 0x08, 0xC0);
            chip.Command(0x3A, 0x23, 0x6B);                // side 1 and bits 0, 1 and 3 set
            chip.Command(0x69, 0x00);
            chip.RunUntilInterrupt();
            long unload = chip.Cycle + (12 * 400_000);
            chip.Result();
            Assert.Equal(0x46, chip.Immediate(0x6C));

            chip.Run(unload + offset - Chip.SampledAfter - chip.Cycle);
            byte port = chip.Immediate(0x7D, 0x23);
            Assert.Equal(unload + offset, chip.Cycle - Chip.SampledAfter);
            Assert.Equal(offset < 0 ? 0x6B : 0x20, port);
        }

        // And Read Drive Status after it: not ready, with the head still over track 0.
        var after = new Chip(DiscImage.Blank(40, false));
        after.Command(0x35, 0x0D, 0x02, 0x08, 0x10);
        Assert.Equal(0x00, after.Execute(0x69, 0x00));
        Assert.Equal(0x46, after.Immediate(0x6C));
        after.Run(400_000);
        Assert.Equal(0x02, after.Immediate(0x6C));
    }

    [Fact]
    public void AnIndexCountOfFifteenKeepsTheHeadLoaded()
    {
        var chip = new Chip(DiscImage.Blank(40, false));
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xF0);
        Assert.Equal(0x00, chip.Execute(0x69, 0x00));
        chip.Run(20_000_000);
        Assert.Equal(0x48, chip.Immediate(0x7D, 0x23));
    }

    [Fact]
    public void ResetAbandonsACommandAndStopsTheMotor()
    {
        // DFS writes 1 then 0 to $FE82 (s1a).
        var chip = new Chip(DiscImage.Blank(40, false));
        chip.Command(0x35, 0x0D, 0x02, 0x08, 0xF0);
        chip.Command(0x53, 0x00, 0x00, 0x22);
        chip.RunUntilInterrupt();
        Assert.Equal(0x8C, chip.Status());

        chip.Reset(1);
        chip.Reset(0);
        Assert.Equal(0x00, chip.Status());
        Assert.False(chip.Fdc.Interrupt);
        chip.Run(200_000);
        Assert.Equal(0x00, chip.Status());
        Assert.Equal(0x00, chip.Immediate(0x7D, 0x23));
    }

    private static byte[] Pattern(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    /// <summary>The chip on its own, with a disc in drive 0, and each access a cycle after the last.</summary>
    private sealed class Chip
    {
        public Chip(DiscImage? disc)
        {
            Fdc.Insert(0, disc);
        }

        public Fdc8271 Fdc { get; } = new();

        /// <summary>Ticks so far: the cycle the next access would be made in is this plus one.</summary>
        public long Cycle { get; private set; }

        public void Run(long cycles)
        {
            for (long i = 0; i < cycles; i++)
            {
                Fdc.Tick();
                Cycle++;
            }
        }

        public byte Status()
        {
            Run(1);
            return Fdc.ReadStatus();
        }

        public byte Result()
        {
            Run(1);
            return Fdc.ReadResult();
        }

        public byte Data()
        {
            Run(1);
            return Fdc.ReadData();
        }

        public void Parameter(byte value)
        {
            Run(1);
            Fdc.WriteParameter(value);
        }

        public void Reset(byte value)
        {
            Run(1);
            Fdc.WriteReset(value);
        }

        public void Command(byte command, params byte[] parameters)
        {
            Run(1);
            Fdc.WriteCommand(command);
            foreach (byte parameter in parameters)
            {
                Parameter(parameter);
            }
        }

        /// <summary>An immediate command: its result, read as DFS reads it.</summary>
        public byte Immediate(byte command, params byte[] parameters)
        {
            Command(command, parameters);
            Assert.Equal(0x10, Status());
            return Result();
        }

        /// <summary>
        /// How many cycles before <see cref="Cycle"/> a one-parameter <see cref="Immediate"/> took
        /// its value: the parameter that starts it is written two accesses before the result is read.
        /// </summary>
        public const int SampledAfter = 2;

        /// <summary>A command that moves no data: its result, after its INT.</summary>
        public byte Execute(byte command, params byte[] parameters)
        {
            Command(command, parameters);
            RunUntilInterrupt();
            Assert.Equal(0x18, Status());
            return Result();
        }

        public void RunUntilInterrupt()
        {
            for (int i = 0; i < 10_000_000 && !Fdc.Interrupt; i++)
            {
                Run(1);
            }
            Assert.True(Fdc.Interrupt, "no interrupt");
        }

        /// <summary>Takes each byte a read offers, 30 cycles after its INT, until the result.</summary>
        public List<byte> TakeBytes()
        {
            var bytes = new List<byte>();
            while (true)
            {
                RunUntilInterrupt();
                if ((Fdc.Peek(0) & 0x04) == 0)
                {
                    return bytes;
                }

                Run(29);
                bytes.Add(Data());
            }
        }

        /// <summary>Gives a byte for each request, 30 cycles after its INT, then waits for the result.</summary>
        public void GiveBytes(byte[] bytes)
        {
            foreach (byte value in bytes)
            {
                RunUntilInterrupt();
                Assert.Equal(0x8C, Fdc.Peek(0));
                Run(29);
                Run(1);
                Fdc.WriteData(value);
            }
            RunUntilInterrupt();
            Assert.Equal(0x18, Fdc.Peek(0));
        }
    }
}
