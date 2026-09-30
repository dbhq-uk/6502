namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    private void Branch(bool taken)
    {
        sbyte offset = (sbyte)Read(PC++);
        if (!taken)
        {
            return;
        }

        ushort target = (ushort)(PC + offset);
        bool crossesPage = (target & 0xFF00) != (PC & 0xFF00);
        if (!crossesPage)
        {
            // A taken branch that stays on its page decides about interrupts
            // at its operand cycle, not its last, so one that arrives on the
            // last cycle waits for another instruction.
            _pollFrozen = true;
        }

        Read(PC);
        if (crossesPage)
        {
            Read((ushort)((PC & 0xFF00) | (target & 0x00FF)));
        }

        PC = target;
    }

    /// <summary>
    /// JMP ($xxxx). The NMOS chip never carries into the high byte of the
    /// pointer, so JMP ($12FF) reads $12FF and $1200. The 65C02 reads the
    /// wrong address as a throwaway and then the right one.
    /// </summary>
    private void JmpIndirect()
    {
        ushort pointer = FetchWord();
        ushort samePage = (ushort)((pointer & 0xFF00) | ((pointer + 1) & 0x00FF));
        byte lo = Read(pointer);
        if (_cmos)
        {
            Read(samePage);
            PC = (ushort)(lo | Read((ushort)(pointer + 1)) << 8);
        }
        else
        {
            PC = (ushort)(lo | Read(samePage) << 8);
        }
    }

    private void Jsr()
    {
        byte lo = Read(PC++);
        Read(StackAddress);
        Push((byte)(PC >> 8));
        Push((byte)PC);
        byte hi = Read(PC);
        PC = (ushort)(lo | hi << 8);
    }

    private void Rts()
    {
        Read(PC);
        Read(StackAddress);
        byte lo = Pull();
        byte hi = Pull();
        PC = (ushort)(lo | hi << 8);
        Read(PC);
        PC++;
    }

    private void Rti()
    {
        Read(PC);
        Read(StackAddress);
        P = (byte)((Pull() & ~B) | U);
        byte lo = Pull();
        byte hi = Pull();
        PC = (ushort)(lo | hi << 8);
    }

    private void Brk()
    {
        Read(PC++);
        Push((byte)(PC >> 8));
        Push((byte)PC);
        Push((byte)(P | B | U));
        EnterHandler();
    }
}
