namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    private bool _nmiLineLastCycle;
    private bool _needNmi;
    private bool _pollIrq;
    private bool _pollNmi;
    private bool _pollFrozen;
    private bool _pollSuppressed;
    private bool _interruptPending;

    /// <summary>
    /// The end of every cycle: count it, and sample the interrupt lines.
    /// </summary>
    /// <remarks>
    /// Checked against the transistor-level model: a line active during an
    /// instruction's last cycle is seen when that instruction ends. NMI is an
    /// edge, latched from the cycle the line becomes active until it is taken
    /// or lost. The poll snapshot is what Step decides from; a taken branch
    /// that stays on its page freezes it after its operand cycle.
    /// </remarks>
    private void EndCycle()
    {
        Cycles++;
        if (Nmi && !_nmiLineLastCycle)
        {
            _needNmi = true;
        }

        _nmiLineLastCycle = Nmi;
        if (!_pollFrozen)
        {
            _pollIrq = Irq && !Flag(I);
            _pollNmi = _needNmi;
        }
    }

    /// <summary>
    /// Decides, at the end of an instruction, whether the next Step runs the
    /// interrupt sequence instead of fetching an opcode.
    /// </summary>
    private void Poll()
    {
        _interruptPending = !_pollSuppressed && (_pollIrq || _pollNmi);
        _pollFrozen = false;
        _pollSuppressed = false;
    }

    /// <summary>
    /// IRQ and NMI: two reads of PC in place of the opcode fetch, the pushes,
    /// then the vector.
    /// </summary>
    private void InterruptSequence()
    {
        Read(PC);
        Read(PC);
        Push((byte)(PC >> 8));
        Push((byte)PC);
        Push((byte)((P & ~B) | U));
        EnterHandler();
    }

    /// <summary>
    /// The end of BRK, IRQ and NMI alike, as the transistor-level model shows
    /// it: an NMI seen by the time P is pushed takes the vector over; one that
    /// arrives on the vector's low byte is lost; one that arrives on its high
    /// byte waits until the handler's first instruction has run.
    /// </summary>
    private void EnterHandler()
    {
        bool nmi = _needNmi;
        SetFlag(I, true);
        if (_cmos)
        {
            SetFlag(D, false);
        }

        ushort vector = nmi ? (ushort)0xFFFA : (ushort)0xFFFE;
        byte lo = Read(vector);
        _needNmi = false;
        byte hi = Read((ushort)(vector + 1));
        PC = (ushort)(lo | hi << 8);
        _pollSuppressed = true;
    }

    /// <summary>
    /// WAI ends when an interrupt line becomes active. With the interrupt
    /// disable flag set, an IRQ wakes the chip without being taken.
    /// </summary>
    private bool WakeFromWait()
    {
        bool nmiEdge = Nmi && !_nmiLineLastCycle;
        if (!Irq && !nmiEdge && !_needNmi)
        {
            return false;
        }

        IsWaiting = false;
        if (nmiEdge)
        {
            _needNmi = true;
            _nmiLineLastCycle = true;
        }

        _interruptPending = _needNmi || (Irq && !Flag(I));
        return true;
    }
}
