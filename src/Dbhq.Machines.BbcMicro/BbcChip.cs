namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The chips on the Model B's board that the CPU's accesses can be told apart by, for the inside
/// model's marks: the nine SHEILA devices in the Service Manual's strobe list, in address order,
/// and the sound chip, which the CPU reaches only through the system VIA
/// (<c>docs/bbc-micro/facts/bus.md</c>, "What the chip counters count").
/// </summary>
/// <remarks>The names and their order are fixed by the models plan (task 5's note), and the page reads the counts in this order.</remarks>
public enum BbcChip
{
    /// <summary>IC2, the 6845, <c>$FE00-$FE07</c>.</summary>
    Crtc,

    /// <summary>IC4, the 6850, <c>$FE08-$FE0F</c>. Not modelled; its accesses are counted all the same.</summary>
    Acia,

    /// <summary>IC7, the serial ULA, <c>$FE10-$FE17</c>. Only its control byte is kept (<see cref="BbcBus.SerialUlaControl"/>).</summary>
    SerialUla,

    /// <summary>IC6, the video ULA, <c>$FE20-$FE2F</c>.</summary>
    VideoUla,

    /// <summary>IC76, the paged ROM latch, <c>$FE30-$FE3F</c>.</summary>
    RomLatch,

    /// <summary>IC3, the system VIA, <c>$FE40-$FE5F</c>.</summary>
    SystemVia,

    /// <summary>IC69, the user VIA, <c>$FE60-$FE7F</c>.</summary>
    UserVia,

    /// <summary>IC78, the 8271, <c>$FE80-$FE9F</c>.</summary>
    Fdc8271,

    /// <summary>IC73, the uPD7002, <c>$FEC0-$FEDF</c>. Not modelled; its accesses are counted all the same.</summary>
    Adc,

    /// <summary>IC18, the SN76489: a byte strobed into it by the system VIA's latch bit 0 falling.</summary>
    Sound,
}

/// <summary>The three lights on the keyboard, as a set: lit when the bit is set.</summary>
[Flags]
public enum BbcLeds
{
    None = 0,

    /// <summary>The cassette motor's light: lit with the relay, by serial ULA control bit 7 set.</summary>
    CassetteMotor = 1,

    /// <summary>CAPS LOCK's light: lit by latch IC32 bit 6 low.</summary>
    CapsLock = 2,

    /// <summary>SHIFT LOCK's light: lit by latch IC32 bit 7 low.</summary>
    ShiftLock = 4,
}
