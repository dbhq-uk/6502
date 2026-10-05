using Dbhq.Machines.Nes.Mappers;

namespace Dbhq.Machines.Nes;

/// <summary>
/// A cartridge file, read: the iNES and NES 2.0 formats, with the header's fields, its program and
/// character memory, and the board that goes with them.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/cartridge.md</c>. <see cref="Load"/> is given what a visitor chose
/// to open, so it trusts nothing: every size is worked out as a 64-bit number and checked against
/// the file's length before anything is allocated, a file over <see cref="MaxFileSize"/> is refused
/// before its header is read, and every refusal is a <see cref="NesFormatException"/> whose message
/// is one plain sentence.
/// </para>
/// <para>
/// The region is read only where the format is reliable. NES 2.0 byte 12 names it
/// (<see cref="HeaderRegion"/>), and the value for the Dendy is refused. The iNES 1 TV system bit
/// is not read at all, because the sheet's source says almost no file sets it, so an iNES file
/// names no region and the page lets the visitor choose.
/// </para>
/// </remarks>
public sealed class Cartridge
{
    /// <summary>
    /// The largest file <see cref="Load"/> will read, in bytes: 4 MB. That is sixteen times the
    /// largest ROM the tests use (the 256 KB <c>ppu_vbl_nmi</c>, cartridge.md 4) and AxROM's
    /// largest PRG (mappers.md), so it leaves room for any board this machine models and for
    /// homebrew, and it is small enough that a header cannot make the page copy more ROM than that.
    /// </summary>
    public const int MaxFileSize = 4 * 1024 * 1024;

    private const int HeaderSize = 16;
    private const int TrainerSize = 512;
    private const int PrgBankSize = 16 * 1024;
    private const int ChrBankSize = 8 * 1024;
    private const int DefaultInesChrRamSize = 8 * 1024;
    private const int InesPrgRamUnit = 8 * 1024;

    /// <summary>
    /// The most PRG RAM, and the most CHR RAM, a header may ask for: 64 KB each. The boards this
    /// machine models have 8 KB of each (mappers.md; a few MMC1 boards have up to 32 KB of PRG RAM),
    /// but a header's size fields can name megabytes, and RAM is allocated and not read from the
    /// file, so a 16 KB file could otherwise make the page allocate many times that. An iNES
    /// header's byte 8 is often junk, so it is clamped to this and the file still loads; a NES 2.0
    /// header states its sizes on purpose, so a size over it is refused.
    /// </summary>
    public const int MaxRamSize = 64 * 1024;

    /// <summary>
    /// The mapper numbers <see cref="CreateMapper"/> can build, kept in one place for its message.
    /// Each mapper's task adds its number here and its case there.
    /// </summary>
    public static IReadOnlyList<int> SupportedMappers { get; } = [0, 1, 2, 3, 7];

    private readonly byte[]? _chrRom;
    private byte[]? _chrRamBlock;

    private Cartridge(
        int mapper,
        int submapper,
        byte[] prg,
        byte[]? chrRom,
        int chrRamSize,
        int prgRamSize,
        bool battery,
        Mirroring mirroring,
        Region? headerRegion)
    {
        Mapper = mapper;
        Submapper = submapper;
        Prg = prg;
        _chrRom = chrRom;
        ChrRamSize = chrRamSize;
        PrgRamSize = prgRamSize;
        Battery = battery;
        Mirroring = mirroring;
        HeaderRegion = headerRegion;
    }

    /// <summary>The mapper number: 8 bits in an iNES header, 12 in a NES 2.0 one.</summary>
    public int Mapper { get; }

    /// <summary>The NES 2.0 submapper, or 0 for an iNES file.</summary>
    public int Submapper { get; }

    /// <summary>The program ROM, the bytes the CPU sees at <c>$8000</c> and up.</summary>
    public byte[] Prg { get; }

    /// <summary>
    /// The character memory: the ROM's bytes, or when <see cref="ChrIsRam"/> is true a block of
    /// zeros the size of the RAM, which no board shares: each takes its own, sized by
    /// <see cref="ChrRamSize"/>. The block of zeros is made on first use, so a board that does not
    /// ask for it does not allocate it twice.
    /// </summary>
    public byte[] Chr => _chrRom ?? (_chrRamBlock ??= new byte[ChrRamSize]);

    /// <summary>True when the file has no CHR ROM and the board has CHR RAM in its place.</summary>
    public bool ChrIsRam => _chrRom is null && ChrRamSize > 0;

    /// <summary>Bytes of CHR RAM, or 0 when the cartridge has CHR ROM or no character memory at all.</summary>
    internal int ChrRamSize { get; }

    /// <summary>
    /// Bytes of PRG RAM at <c>$6000</c>. An iNES header counts it in 8 KB units and says 0 for 8 KB,
    /// as the format does, and the test ROMs rely on it; a NES 2.0 header gives the sizes and 0 means
    /// none.
    /// </summary>
    public int PrgRamSize { get; }

    /// <summary>True when the PRG RAM is battery backed.</summary>
    public bool Battery { get; }

    /// <summary>The header's nametable arrangement, for boards that do not choose their own.</summary>
    public Mirroring Mirroring { get; }

    /// <summary>
    /// The region the header names: NTSC or PAL for a NES 2.0 file that says so, and null for an
    /// iNES file and for NES 2.0's "either". The iNES 1 bit is not trusted, so it is not read.
    /// </summary>
    public Region? HeaderRegion { get; }

    /// <summary>
    /// Reads a cartridge file, iNES or NES 2.0.
    /// </summary>
    /// <param name="file">The whole file.</param>
    /// <exception cref="NesFormatException">
    /// The file is empty, over <see cref="MaxFileSize"/>, shorter than a header, not a NES file, cut
    /// short of what its header says, without program ROM, or for the Dendy.
    /// </exception>
    public static Cartridge Load(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length == 0)
        {
            throw new NesFormatException("The file is empty.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new NesFormatException(
                $"The file is larger than {MaxFileSize / (1024 * 1024)} MB, which is more than any cartridge this machine will load.");
        }

        if (file.Length < HeaderSize)
        {
            throw new NesFormatException(
                $"The file is shorter than the {HeaderSize} bytes of an iNES header, so it is not a NES cartridge.");
        }

        if (file[0] != 0x4E || file[1] != 0x45 || file[2] != 0x53 || file[3] != 0x1A)
        {
            throw new NesFormatException("The file does not start with the iNES signature, so it is not a NES cartridge.");
        }

        bool nes2 = (file[7] & 0x0C) == 0x08;
        bool trainer = (file[6] & 0x04) != 0;

        Region? region = null;
        if (nes2)
        {
            region = (file[12] & 0x03) switch
            {
                0 => Region.Ntsc,
                1 => Region.Pal,
                2 => null,
                _ => throw new NesFormatException(
                    "This cartridge is for the Dendy, a clone console that this machine does not model."),
            };
        }

        long prgSize = nes2 ? Nes2Size(file[4], file[9] & 0x0F, PrgBankSize) : file[4] * (long)PrgBankSize;
        long chrSize = nes2 ? Nes2Size(file[5], file[9] >> 4, ChrBankSize) : file[5] * (long)ChrBankSize;

        if (prgSize == 0)
        {
            throw new NesFormatException("The file has no program ROM, so there is nothing for the CPU to run.");
        }

        // Each size is checked alone first, so the sum cannot overflow whatever the header says.
        if (prgSize > file.Length || chrSize > file.Length
            || HeaderSize + (trainer ? TrainerSize : 0) + prgSize + chrSize > file.Length)
        {
            throw new NesFormatException(
                "The file is shorter than its header says, so part of the cartridge is missing.");
        }

        bool battery = (file[6] & 0x02) != 0;
        int prgRamSize;
        int chrRamSize = 0;
        if (nes2)
        {
            long prgRam = RamSize(file[10] & 0x0F) + RamSize(file[10] >> 4);
            long chrRam = chrSize > 0 ? 0 : RamSize(file[11] & 0x0F) + RamSize(file[11] >> 4);
            if (prgRam > MaxRamSize || chrRam > MaxRamSize)
            {
                throw new NesFormatException(
                    $"The header asks for more than {MaxRamSize / 1024} KB of cartridge RAM, which is more than this machine will give a cartridge.");
            }

            prgRamSize = (int)prgRam;
            chrRamSize = (int)chrRam;
            battery |= (file[10] >> 4) != 0;
        }
        else
        {
            prgRamSize = Math.Min((file[8] == 0 ? 1 : file[8]) * InesPrgRamUnit, MaxRamSize);
            chrRamSize = chrSize > 0 ? 0 : DefaultInesChrRamSize;
        }

        // Every size is now no more than the file's length, or than MaxRamSize, so the arrays below are small.
        int offset = HeaderSize + (trainer ? TrainerSize : 0);
        byte[] prg = file.AsSpan(offset, (int)prgSize).ToArray();
        offset += (int)prgSize;
        byte[]? chrRom = chrSize > 0 ? file.AsSpan(offset, (int)chrSize).ToArray() : null;

        Mirroring mirroring = (file[6] & 0x08) != 0
            ? Mirroring.FourScreen
            : (file[6] & 0x01) != 0 ? Mirroring.Vertical : Mirroring.Horizontal;

        int mapper = file[6] >> 4;
        int submapper = 0;
        if (nes2)
        {
            mapper |= (file[7] & 0xF0) | ((file[8] & 0x0F) << 8);
            submapper = file[8] >> 4;
        }
        else if ((file[7] & 0x0C) == 0 && file[12] == 0 && file[13] == 0 && file[14] == 0 && file[15] == 0)
        {
            // Old tools wrote text into bytes 7 to 15, which would add to the mapper (cartridge.md 1).
            mapper |= file[7] & 0xF0;
        }

        return new Cartridge(mapper, submapper, prg, chrRom, chrRamSize, prgRamSize, battery, mirroring, region);
    }

    /// <summary>
    /// The board for this cartridge. Each call gives a new one, with its own RAM and its registers
    /// at power-on.
    /// </summary>
    /// <exception cref="NesFormatException">The mapper is not one this machine models.</exception>
    public IMapper CreateMapper()
    {
        switch (Mapper)
        {
            case 0:
                return new Nrom(this);
            case 1:
                return new Mmc1(this);
            case 2:
                return new Uxrom(this);
            case 3:
                return new Cnrom(this);
            case 7:
                return new Axrom(this);
            default:
                throw new NesFormatException(
                    $"This cartridge needs mapper {Mapper}, and this machine models only these mappers: {string.Join(", ", SupportedMappers)}.");
        }
    }

    /// <summary>
    /// A NES 2.0 ROM size in bytes (cartridge.md 2): a 12-bit count of banks, or, when the high
    /// nibble is 15, the exponent and multiplier form. Returns the size as a 64-bit number, and
    /// <see cref="long.MaxValue"/> when the exponent is too big to hold, which no file can match.
    /// </summary>
    private static long Nes2Size(byte low, int highNibble, int bankSize)
    {
        if (highNibble != 0x0F)
        {
            return ((highNibble << 8) | low) * (long)bankSize;
        }

        int exponent = low >> 2;
        int multiplier = (low & 0x03) * 2 + 1;
        return exponent > 40 ? long.MaxValue : (1L << exponent) * multiplier;
    }

    /// <summary>A NES 2.0 RAM size from its shift count: 64 shifted left by it, and 0 for none.</summary>
    private static long RamSize(int shift)
    {
        return shift == 0 ? 0 : 64L << shift;
    }
}
