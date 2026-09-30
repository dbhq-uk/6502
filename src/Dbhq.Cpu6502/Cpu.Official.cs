namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    /// <summary>
    /// The 151 opcodes every member of the family shares. Where the NMOS chip
    /// and the 65C02 differ in how one of these uses the bus, the difference
    /// lives in the addressing helpers, not here.
    /// </summary>
    /// <returns>False when the opcode is not one of the shared set.</returns>
    private bool ExecuteOfficial(byte opcode)
    {
        switch (opcode)
        {
            case 0xEA: Read(PC); break;

            default: return false;
        }

        return true;
    }
}
