namespace Dbhq.Machines.Nes.Mappers;

/// <summary>
/// MMC3, mapper 4 (the TxROM boards): eight bank registers behind a bank select, two PRG modes and a
/// CHR inversion, a mirroring register, a PRG RAM protect register, and the scanline counter, which
/// counts rises of PPU address line A12 and raises an IRQ when it reaches 0.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/mappers.md</c> section 6. The registers are decoded by address bits
/// 15, 14, 13 and 0: an even and an odd register in each 8 KB from <c>$8000</c>. R0 and R1 are
/// 2 KB CHR banks (bit 0 ignored), R2 to R5 1 KB CHR banks, R6 and R7 8 KB PRG banks (bits 7 and 6
/// ignored). Bank select bit 6 swaps R6 with the fixed second-last bank; bit 7 swaps the two
/// halves of the pattern tables. The last bank is always at <c>$E000</c>.
/// </para>
/// <para>
/// <b>The scanline counter.</b> A clock happens on a rise of A12 that comes after A12 has been low
/// for three falling edges of M2. The board is told every address the PPU puts on its bus with the
/// CPU cycle it did so in (<see cref="IMapper.PpuAddressChanged"/>), keeps whether A12 is low and
/// the cycle it went low, and counts a rise only when at least <see cref="FilterCycles"/> cycles
/// have passed since: M2 falls once a cycle, at its end, so a low that began in cycle <c>c</c> has
/// seen three falls by cycle <c>c + 3</c>. That is a compare and two stores on the PPU's busiest
/// path, with nothing allocated. On a clock the counter reloads from the latch if it is 0 or a
/// reload was asked for (a write to <c>$C001</c>), and otherwise counts down; then, if it is 0 and
/// IRQs are on, the IRQ line goes low and stays low until <c>$E000</c> is written.
/// </para>
/// <para>
/// <b>Which MMC3.</b> The chips differ at latch 0. The model is the Sharp ("new") one, which the
/// sheet chooses because games rely on it: the IRQ is raised whenever a clock leaves the counter
/// at 0, so a latch of 0 raises it on every clock. The NEC ("old") chip raises it only when the
/// counter changes to 0 or is reloaded by request. <c>mmc3_test_2</c>'s <c>5-MMC3</c> tests the
/// first and passes; its <c>6-MMC3_alt</c> tests the second and is a known failure.
/// </para>
/// <para>
/// <b>Power on</b>, which the sheet leaves unspecified for R6, R7 and the bank select: every
/// register 0 but the banks, which are R0 to R7 = 0, 2, 4, 5, 6, 7, 0, 1, so the first 32 KB of
/// PRG and the first 8 KB of CHR read in order, as a program that never writes them expects (the
/// test ROMs' shells write none of them). PRG RAM starts on and writable, which the test ROMs also
/// need: they report through <c>$6000</c> and never write <c>$A001</c>. The mirroring is the
/// header's until <c>$A000</c> is written, and a four-screen header keeps four screens. The chip
/// has no reset line, so the reset button changes nothing (the same choice as the other boards).
/// </para>
/// <para>
/// MMC6 (StarTropics), which shares mapper 4, is not modelled: it runs as an MMC3, whose
/// <c>$A001</c> bits mean something else, so its battery RAM may read as switched off.
/// </para>
/// </remarks>
public sealed class Mmc3 : Board
{
    /// <summary>
    /// The CPU cycles A12 must have been low for, before a rise, for the rise to clock the counter:
    /// three falling edges of M2 (the sheet).
    /// </summary>
    public const int FilterCycles = 3;

    private readonly int[] _banks = new int[8];
    private readonly Mirroring _headerMirroring;
    private int _select;
    private int _latch;
    private int _counter;
    private bool _reload;
    private bool _irqEnabled;
    private bool _irq;
    private bool _a12Low;
    private long _lowSince;

    /// <summary>A board from a parsed cartridge.</summary>
    internal Mmc3(Cartridge cartridge)
        : base(cartridge, 0x2000)
    {
        _headerMirroring = cartridge.Mirroring;
        PowerOn();
    }

    /// <inheritdoc />
    public override bool Irq => _irq;

    /// <inheritdoc />
    public override void PpuAddressChanged(ushort address, long cpuCycle)
    {
        if ((address & 0x1000) == 0)
        {
            if (!_a12Low)
            {
                _a12Low = true;
                _lowSince = cpuCycle;
            }

            return;
        }

        if (_a12Low)
        {
            _a12Low = false;
            if (cpuCycle - _lowSince >= FilterCycles)
            {
                ClockCounter();
            }
        }
    }

    /// <inheritdoc />
    public override void Reset(bool power)
    {
        base.Reset(power);
        if (power)
        {
            PowerOn();
        }
    }

    private protected override void WriteRegister(ushort address, byte value)
    {
        switch (address & 0xE001)
        {
            case 0x8000:
                _select = value;
                ApplyBanks();
                break;
            case 0x8001:
                _banks[_select & 7] = value;
                ApplyBanks();
                break;
            case 0xA000:
                if (_headerMirroring != Mirroring.FourScreen)
                {
                    Mirroring = (value & 1) == 0 ? Mirroring.Vertical : Mirroring.Horizontal;
                }

                break;
            case 0xA001:
                PrgRamEnabled = (value & 0x80) != 0;
                PrgRamWritable = (value & 0x40) == 0;
                break;
            case 0xC000:
                _latch = value;
                break;
            case 0xC001:
                // The counter is cleared and reloads on the next clock; this raises nothing itself.
                _counter = 0;
                _reload = true;
                break;
            case 0xE000:
                _irqEnabled = false;
                _irq = false;
                break;
            default:
                _irqEnabled = true;
                break;
        }
    }

    // The Sharp ("new") behaviour: reload at 0 or on request, else count down; then 0 raises the IRQ.
    private void ClockCounter()
    {
        if (_counter == 0 || _reload)
        {
            _counter = _latch;
            _reload = false;
        }
        else
        {
            _counter--;
        }

        if (_counter == 0 && _irqEnabled)
        {
            _irq = true;
        }
    }

    private void PowerOn()
    {
        int[] banks = [0, 2, 4, 5, 6, 7, 0, 1];
        banks.CopyTo(_banks, 0);
        _select = 0;
        Mirroring = _headerMirroring;
        PrgRamEnabled = true;
        PrgRamWritable = true;
        _latch = 0;
        _counter = 0;
        _reload = false;
        _irqEnabled = false;
        _irq = false;

        // The PPU's bus starts at address 0, so A12 has been low since the power came on.
        _a12Low = true;
        _lowSince = 0;
        ApplyBanks();
    }

    // Turns the registers into the window bases the reads use, so a read does no work of its own.
    private void ApplyBanks()
    {
        int r6 = _banks[6] & 0x3F;
        bool prgMode1 = (_select & 0x40) != 0;
        SetPrg8(0, prgMode1 ? -2 : r6);
        SetPrg8(1, _banks[7] & 0x3F);
        SetPrg8(2, prgMode1 ? r6 : -2);
        SetPrg8(3, -1);

        // The 2 KB banks are at $0000 and $0800, the 1 KB banks at $1000 to $1C00; inversion swaps
        // the halves, which is window number XOR 4.
        int flip = (_select & 0x80) != 0 ? 4 : 0;
        SetChr1(0 ^ flip, _banks[0] & 0xFE);
        SetChr1(1 ^ flip, _banks[0] | 1);
        SetChr1(2 ^ flip, _banks[1] & 0xFE);
        SetChr1(3 ^ flip, _banks[1] | 1);
        SetChr1(4 ^ flip, _banks[2]);
        SetChr1(5 ^ flip, _banks[3]);
        SetChr1(6 ^ flip, _banks[4]);
        SetChr1(7 ^ flip, _banks[5]);
    }
}
