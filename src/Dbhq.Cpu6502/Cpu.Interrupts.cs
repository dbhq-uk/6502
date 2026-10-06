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
    /// The end of every cycle: count it, and sample the NMI line.
    /// </summary>
    /// <remarks>
    /// Checked against the transistor-level model: a line active during an
    /// instruction's last cycle is seen when that instruction ends. NMI is an
    /// edge, latched from the cycle the line becomes active until it is taken
    /// or lost, so it is sampled here every cycle; in most cycles the line has
    /// not moved and nothing is stored.
    /// <para>
    /// The poll snapshot, what Step decides from, is the IRQ line, the I flag
    /// and the latched NMI as they stood at the end of the instruction's last
    /// cycle. Until task 17 it was taken here at every cycle. It is now taken
    /// once, in <see cref="Poll"/>, which gives the same values: after an
    /// instruction's last bus access nothing changes the lines (a bus sets
    /// them only inside an access) or the latch (except in
    /// <see cref="EnterHandler"/>, whose poll is suppressed), and the three
    /// instructions that change the I flag after their last access, CLI, SEI
    /// and PLP, freeze the snapshot first (<see cref="FreezePoll"/>), as a
    /// taken branch that stays on its page does after its operand cycle.
    /// </para>
    /// </remarks>
    private void EndCycle()
    {
        Cycles++;
        bool nmi = Nmi;
        if (nmi != _nmiLineLastCycle)
        {
            if (nmi)
            {
                _needNmi = true;
            }

            _nmiLineLastCycle = nmi;
        }
    }

    /// <summary>
    /// Takes the poll snapshot now, as it stands at the end of the cycle just
    /// made, and keeps it for <see cref="Poll"/> whatever the rest of the
    /// instruction does.
    /// </summary>
    private void FreezePoll()
    {
        _pollIrq = Irq && !Flag(I);
        _pollNmi = _needNmi;
        _pollFrozen = true;
    }

    /// <summary>
    /// Decides, at the end of an instruction, whether the next Step runs the
    /// interrupt sequence instead of fetching an opcode.
    /// </summary>
    private void Poll()
    {
        bool pending = _pollFrozen ? _pollIrq || _pollNmi : (Irq && !Flag(I)) || _needNmi;
        _interruptPending = !_pollSuppressed && pending;
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
    /// it. An NMI seen by the time P is pushed takes the vector over. One whose
    /// edge comes while the BRK or IRQ vector is read is taken only if the line
    /// is still active in the cycle after the vector's high byte, the handler's
    /// first fetch, and then after the handler's first instruction; a shorter
    /// pulse is lost. One whose edge comes while the NMI vector itself is read
    /// is lost, held or not.
    /// </summary>
    /// <remarks>
    /// Until task 12b (4 October 2026) this said an NMI arriving on the low
    /// byte is lost, from model runs that all released the line two cycles
    /// after it went active. Runs that hold it, or pulse it for one to four
    /// cycles, showed the rule above (tools/perfect6502/harness.c).
    /// </remarks>
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
        bool late = !nmi && _needNmi;
        _needNmi = false;
        byte hi = Read((ushort)(vector + 1));
        if (late || _needNmi)
        {
            // An edge seen during the vector reads is dropped. On the BRK or
            // IRQ vector the detector forgets the line, so the next cycle's
            // end latches it again if it is still active.
            _needNmi = false;
            if (!nmi)
            {
                _nmiLineLastCycle = false;
            }
        }

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
