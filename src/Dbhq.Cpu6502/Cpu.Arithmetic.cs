namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    private void AdcAt(ushort address)
    {
        byte value = Read(address);
        DecimalExtraCycle(address);
        Adc(value);
    }

    private void SbcAt(ushort address)
    {
        byte value = Read(address);
        DecimalExtraCycle(address);
        Sbc(value);
    }

    /// <summary>
    /// The 65C02 takes one more cycle for ADC and SBC in decimal mode, and
    /// spends it re-reading the operand. See docs/known-differences.md for
    /// the immediate mode, where Harte's data records something else.
    /// </summary>
    private void DecimalExtraCycle(ushort address)
    {
        if (_cmos && Flag(D))
        {
            Read(address);
        }
    }

    private void Adc(byte value)
    {
        int carryIn = P & C;
        if (_decimal && Flag(D))
        {
            AdcDecimal(value, carryIn);
            return;
        }

        int sum = A + value + carryIn;
        SetFlag(V, (~(A ^ value) & (A ^ sum) & 0x80) != 0);
        SetFlag(C, sum > 0xFF);
        A = NZ((byte)sum);
    }

    private void Sbc(byte value)
    {
        if (_decimal && Flag(D))
        {
            SbcDecimal(value, P & C);
            return;
        }

        Adc((byte)~value);
    }

    /// <summary>
    /// Decimal ADC as Bruce Clark describes it. The NMOS chip takes N and V
    /// from the sum before the high digit is adjusted and Z from the binary
    /// sum; the 65C02 takes N and Z from the result.
    /// </summary>
    private void AdcDecimal(byte value, int carryIn)
    {
        int lo = (A & 0x0F) + (value & 0x0F) + carryIn;
        if (lo >= 0x0A)
        {
            lo = ((lo + 0x06) & 0x0F) + 0x10;
        }

        int sum = (A & 0xF0) + (value & 0xF0) + lo;
        int signedSum = (sbyte)(A & 0xF0) + (sbyte)(value & 0xF0) + lo;
        bool negative = (sum & 0x80) != 0;
        SetFlag(V, signedSum < -128 || signedSum > 127);
        if (sum >= 0xA0)
        {
            sum += 0x60;
        }

        SetFlag(C, sum >= 0x100);
        byte result = (byte)sum;
        if (_cmos)
        {
            NZ(result);
        }
        else
        {
            SetFlag(N, negative);
            SetFlag(Z, ((A + value + carryIn) & 0xFF) == 0);
        }

        A = result;
    }

    /// <summary>
    /// Decimal SBC as Bruce Clark describes it. Carry and overflow come from
    /// the binary subtraction on both chips; the NMOS chip also takes N and Z
    /// from it, and the 65C02 takes them from the result.
    /// </summary>
    private void SbcDecimal(byte value, int carryIn)
    {
        int binary = A - value + carryIn - 1;
        byte binaryResult = (byte)binary;
        SetFlag(V, ((A ^ value) & (A ^ binaryResult) & 0x80) != 0);
        SetFlag(C, binary >= 0);
        int lo = (A & 0x0F) - (value & 0x0F) + carryIn - 1;
        byte result;
        if (_cmos)
        {
            int adjusted = binary;
            if (adjusted < 0)
            {
                adjusted -= 0x60;
            }

            if (lo < 0)
            {
                adjusted -= 0x06;
            }

            result = NZ((byte)adjusted);
        }
        else
        {
            if (lo < 0)
            {
                lo = ((lo - 0x06) & 0x0F) - 0x10;
            }

            int adjusted = (A & 0xF0) - (value & 0xF0) + lo;
            if (adjusted < 0)
            {
                adjusted -= 0x60;
            }

            result = (byte)adjusted;
            NZ(binaryResult);
        }

        A = result;
    }
}
