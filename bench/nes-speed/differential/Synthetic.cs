// The differential's synthetic cartridges: small 6502 programs, assembled here (Asm.cs) and put in
// a cartridge made from bytes, never a game. The test ROMs mostly draw a still screen or work in
// VBlank; these keep rendering on and touch the chips at dots that move frame by frame, so a lazy
// build that is wrong only in the middle of a rendered line is seen. Each is one workload on one
// board:
//
//   fuzz     an LFSR picks a register ($2000-$2007, $4014, $4015, $4016, $4017), a value and a
//            read or a write, then a delay; now and then the board's own write and a wait for
//            sprite 0; the DMC plays and IRQs are taken. Different seeds are different jobs
//   sprite0  BIT $2002 / BVC waits for sprite 0 hit, with sprite 0 swept over the whole picture:
//            its y from 0 to 239, at each x of 0, 1, 7, 8, 128, 248, 254 and 255, with the left
//            clips off and on (both, or the background's or the sprites' alone), one place a
//            frame that draws it, its flips and priority varied by the frame; the eight sprite0
//            cartridges each take an eighth of the 3,840 places, so together they cover them all
//            in each region (--coverage counts them); after the hit a delay that grows a cycle a
//            frame, then a $2005 and $2006 split and the board's write
//   scroll   from the NMI, a delay that grows a cycle a frame (below), then $2006, $2006, $2005,
//            $2005 and $2000 writes
//   mask     the same delay, then $2001 with one of bits 1 to 4 cleared, greyscale or emphasis, and
//            put back 7 cycles later
//   sprites  64 sprites in clusters, more than eight on many lines and moving at four speeds, so
//            the overflow flag's false positives and negatives come and go; the same delay, then
//            $2003 and $2004 writes and an OAM DMA during rendering
//   nmi      no NMI from the NMI handler's frame: VBlank found by polling, then a delay to just
//            before the next one, then a burst of $2000 bit 7 on and off and $2002 reads across
//            the dot the flag is set, its start moving a cycle a frame, for NTSC and PAL frames
//   apu      length counters loaded in 8 frames of 64 and left to run out over the silent rest,
//            the DMC looping in one frame and ending with an IRQ in the next, and after the delay a
//            $4017 write, which the delay's single cycles put on odd and even cycles, a $4015 read
//            and a $4015 write
//   background  the background alone, the sprites never on, scrolled every frame, for the fast
//            scanline renderer (task 3 of the scanline renderer work): horizontal mirroring in the
//            header; X on by an odd step a frame, so fine X takes every value in 8 frames and coarse
//            X goes round; Y on by a step a frame, wrapping at 240 for odd seeds (so the picture
//            crosses coarse Y 29 into the other vertical nametable) and running on through the
//            attribute rows 30 and 31 for even ones; the nametable select from the frame; the
//            background's pattern table switched every 16 frames; PPUMASK from a table of the
//            left clip on and off, greyscale and each emphasis; and v left in the palette in
//            VBlank, which a frame with rendering off shows (one frame in four, as for all). The
//            same delay, then the scroll workload's split writes, and the board's write. The
//            CHR and the nametables are noise, so every tile and attribute quadrant differs.
//
// The boards: NROM; MMC1 (a read-modify-write to the board, whose second write the chip ignores,
// then a CHR bank switch by its serial port mid-frame); CNROM (a CHR bank switch mid-frame,
// through a table so the bus conflict changes nothing); AxROM (the single screen switched
// mid-frame, the two screens different); UxROM (a PRG switch mid-frame, CHR RAM written in VBlank);
// MMC3 (its IRQ a number of lines into the frame that changes every frame, the handler switching a
// CHR bank under the sprites and toggling greyscale, 8 by 16 sprites or 8 by 8 sprites from $1000,
// and a CHR bank switch from the main loop mid-frame).
//
// The delay: the timed workloads run in the NMI handler, after its OAM DMA, so the delay starts a
// fixed time after VBlank begins. The frame counts up to 120 then starts again in the next of
// three windows, and the delay is the window's base plus the count, in single cycles (a
// clockslide). The windows start just before NTSC's pre-render line, just before PAL's, and in
// the middle of the picture, so in 600 frames a write lands on each dot of those lines and the
// next in turn, both regions (--coverage prints where they landed). The counts live in RAM, which
// the reset button keeps, so a run goes on from where it was; but at the third start, after the
// differential's second reset, the window before PAL's pre-render line begins again at once.
internal enum Workload
{
    Fuzz,
    Sprite0,
    Scroll,
    Mask,
    Sprites,
    Nmi,
    Apu,
    Background,
}

internal enum BoardKind
{
    Nrom,
    Mmc1,
    Uxrom,
    Cnrom,
    Mmc3,
    Axrom,
}

internal static class Synthetic
{
    // How many times the frames given a run the synthetic jobs run: 600 at the default 150.
    public const int Times = 4;

    // The sprite0 jobs run longer, 900 frames at the default 150: each takes 480 places of sprite
    // 0's sweep, one a frame that draws it, and one frame in four is not drawn (frame_setup), so
    // 600 frames, less the start and the reset half way, are not enough.
    public const int Sprite0Times = 6;

    public static IEnumerable<(string Name, byte[] Bytes, int Times)> Jobs()
    {
        (Workload, BoardKind, int)[] list =
        [
            (Workload.Fuzz, BoardKind.Nrom, 1), (Workload.Fuzz, BoardKind.Nrom, 2), (Workload.Fuzz, BoardKind.Mmc3, 3), (Workload.Fuzz, BoardKind.Mmc3, 4),
            (Workload.Fuzz, BoardKind.Mmc1, 5), (Workload.Fuzz, BoardKind.Mmc1, 6), (Workload.Fuzz, BoardKind.Cnrom, 7), (Workload.Fuzz, BoardKind.Axrom, 8),
            (Workload.Sprite0, BoardKind.Nrom, 1), (Workload.Sprite0, BoardKind.Mmc1, 1), (Workload.Sprite0, BoardKind.Mmc1, 2), (Workload.Sprite0, BoardKind.Cnrom, 1),
            (Workload.Sprite0, BoardKind.Axrom, 1), (Workload.Sprite0, BoardKind.Uxrom, 1), (Workload.Sprite0, BoardKind.Mmc3, 1), (Workload.Sprite0, BoardKind.Mmc3, 2),
            (Workload.Scroll, BoardKind.Nrom, 1), (Workload.Scroll, BoardKind.Mmc3, 1), (Workload.Scroll, BoardKind.Mmc3, 2), (Workload.Scroll, BoardKind.Mmc1, 1),
            (Workload.Scroll, BoardKind.Mmc1, 2), (Workload.Scroll, BoardKind.Axrom, 1),
            (Workload.Mask, BoardKind.Nrom, 1), (Workload.Mask, BoardKind.Mmc3, 1), (Workload.Mask, BoardKind.Mmc3, 2), (Workload.Mask, BoardKind.Mmc1, 1),
            (Workload.Mask, BoardKind.Cnrom, 1),
            (Workload.Sprites, BoardKind.Nrom, 1), (Workload.Sprites, BoardKind.Mmc3, 1), (Workload.Sprites, BoardKind.Uxrom, 2), (Workload.Sprites, BoardKind.Mmc1, 2),
            (Workload.Nmi, BoardKind.Nrom, 1), (Workload.Nmi, BoardKind.Mmc1, 1), (Workload.Nmi, BoardKind.Mmc3, 1),
            (Workload.Apu, BoardKind.Nrom, 1), (Workload.Apu, BoardKind.Uxrom, 1), (Workload.Apu, BoardKind.Mmc3, 1), (Workload.Apu, BoardKind.Mmc1, 1),

            // Last, so every job before keeps its place in the list, and so its PAL reset phase.
            (Workload.Background, BoardKind.Nrom, 1), (Workload.Background, BoardKind.Nrom, 2), (Workload.Background, BoardKind.Nrom, 3),
            (Workload.Background, BoardKind.Nrom, 4), (Workload.Background, BoardKind.Nrom, 5), (Workload.Background, BoardKind.Cnrom, 6),
            (Workload.Background, BoardKind.Cnrom, 7), (Workload.Background, BoardKind.Cnrom, 8), (Workload.Background, BoardKind.Cnrom, 9),
            (Workload.Background, BoardKind.Mmc1, 10), (Workload.Background, BoardKind.Mmc1, 11), (Workload.Background, BoardKind.Axrom, 12),
        ];
        int sprite0Part = 0;
        foreach (var (workload, board, seed) in list)
        {
            int part = workload == Workload.Sprite0 ? sprite0Part++ : 0;
            yield return ($"synthetic/{workload.ToString().ToLowerInvariant()}-{board.ToString().ToLowerInvariant()}-{seed}", new ProgramBuilder(workload, board, seed, part).Cartridge(), workload == Workload.Sprite0 ? Sprite0Times : Times);
        }
    }
}

// `part` is which eighth of sprite 0's sweep a sprite0 cartridge takes (0 to 7); the others ignore it.
internal sealed class ProgramBuilder(Workload workload, BoardKind board, int seed, int part = 0)
{
    // Zero page.
    private const int Frame = 0x00;
    private const int Go = 0x02;
    private const int DelayLo = 0x03;
    private const int DelayHi = 0x04;
    private const int Jump = 0x05;
    private const int Rng = 0x07;
    private const int Ctrl = 0x0A;
    private const int Mask = 0x0B;
    private const int NmiCount = 0x10;
    private const int Fine = 0x12;
    private const int Window = 0x13;
    private const int Ptr = 0x14;

    // The sprite0 workload's sweep: sprite 0's y, its x's place in the table, whether a left
    // clip is on, and whether the last frame_setup left rendering on, so the place it set was drawn.
    private const int Sprite0Y = 0x18;
    private const int Sprite0X = 0x19;
    private const int Sprite0Clip = 0x1A;
    private const int Drawn = 0x1B;

    // How many times the program has started: power on, then each press of the reset button.
    private const int Starts = 0x1C;

    // The background workload's scroll, and a byte to work in.
    private const int ScrollX = 0x20;
    private const int ScrollY = 0x21;
    private const int Scratch = 0x22;
    private const int Oam = 0x0200;

    // The windows' bases, in cycles of delay: the first write lands about 20 cycles before NTSC's
    // pre-render line (VBlank begins 2,273 cycles before it, and the handler takes about 655
    // before its first write), the same for PAL (7,459), and the middle of the picture. Each window
    // is FineCount frames, the delay a cycle longer each frame, so it covers a little over a line.
    private static readonly int[] Bases = [1598, 6784, 13000];
    private const int FineCount = 120;

    // For the nmi workload, from VBlank found to the burst: an NTSC frame and a PAL frame, less the
    // burst's lead and the setup.
    private static readonly int[] NmiBases = [29450, 32920];

    private readonly Asm _a = new(0xC000);
    private readonly Noise _random = new((ulong)((seed * 7919) + ((int)workload * 131) + ((int)board * 17)));

    private bool ChrRam => board is BoardKind.Uxrom or BoardKind.Axrom;

    private int ChrBanks => board switch
    {
        BoardKind.Nrom => 1,
        BoardKind.Mmc1 or BoardKind.Cnrom => 4,
        BoardKind.Mmc3 => 8,
        _ => 0,
    };

    // PPUCTRL: NMI on (but for the nmi workload), and the sprites: 8 by 16 for the sprites workload
    // and odd MMC3 seeds, from $1000 for even MMC3 seeds, so MMC3 sees A12 rise each line.
    private int CtrlValue
    {
        get
        {
            int value = workload == Workload.Nmi ? 0x00 : 0x80;
            if (board == BoardKind.Mmc3)
            {
                value |= seed % 2 == 1 ? 0x20 : 0x08;
            }
            else if (workload == Workload.Sprites && board != BoardKind.Uxrom && board != BoardKind.Mmc1)
            {
                value |= 0x20;
            }

            return value;
        }
    }

    public byte[] Cartridge()
    {
        Emit();
        byte[] code = _a.Assemble();
        if (code.Length > 0x3FFA)
        {
            throw new InvalidOperationException("the program does not fit");
        }

        // 32 KB of PRG: noise at $8000 to $BFFF, the program from $C000, the vectors at the top.
        byte[] prg = new byte[0x8000];
        _random.NextBytes(prg.AsSpan(0, 0x4000));
        code.CopyTo(prg, 0x4000);
        Vector(prg, 0x7FFA, _a.Address("nmi"));
        Vector(prg, 0x7FFC, _a.Address("reset"));
        Vector(prg, 0x7FFE, _a.Address("irq"));

        // CHR ROM: noise, with tiles 0 and 1 of both pattern tables opaque in every bank.
        byte[] chr = new byte[ChrBanks * 0x2000];
        _random.NextBytes(chr);
        for (int bank = 0; bank < chr.Length; bank += 0x1000)
        {
            Array.Fill(chr, (byte)0xFF, bank, 0x20);
        }

        int mapper = (int)board switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, 4 => 4, _ => 7 };
        // Vertical mirroring, but horizontal for the background workload.
        int vertical = workload == Workload.Background ? 0 : 1;
        byte[] header = [(byte)'N', (byte)'E', (byte)'S', 0x1A, 2, (byte)ChrBanks, (byte)(((mapper & 0x0F) << 4) | vertical), (byte)(mapper & 0xF0), 0, 0, 0, 0, 0, 0, 0, 0];
        return [.. header, .. prg, .. chr];
    }

    private static void Vector(byte[] prg, int at, int address)
    {
        prg[at] = (byte)address;
        prg[at + 1] = (byte)(address >> 8);
    }

    private void Emit()
    {
        Asm a = _a;
        a.Label("reset");
        a.Sei();
        a.Cld();
        a.LdxI(0xFF);
        a.Txs();
        a.LdaI(0);
        a.Sta(0x2000);
        a.Sta(0x2001);
        a.Sta(0x4010);
        a.LdaI(0x40);
        a.Sta(0x4017);
        a.Bit(0x2002);
        a.Label("vbl1");
        a.Bit(0x2002);
        a.Bpl("vbl1");
        a.Label("vbl2");
        a.Bit(0x2002);
        a.Bpl("vbl2");

        // The LFSR is seeded at power on only; RAM survives the reset button.
        a.Lda(Rng);
        a.Ora(Rng + 1);
        a.Bne("seeded");
        a.LdaI(0x5A ^ seed);
        a.Sta(Rng);
        a.LdaI(0xC3 + seed);
        a.Sta(Rng + 1);
        if (workload == Workload.Sprite0)
        {
            // This cartridge's eighth of the sweep: 480 places from place 480 x part, which is y
            // 0, the x 2 x part places on, and the clip on for the second four.
            a.LdaI(0);
            a.Sta(Sprite0Y);
            a.Sta(Drawn);
            a.LdaI((2 * part) & 7);
            a.Sta(Sprite0X);
            a.LdaI(part / 4);
            a.Sta(Sprite0Clip);
        }

        a.Label("seeded");
        if (workload is Workload.Scroll or Workload.Mask or Workload.Sprites or Workload.Apu or Workload.Background)
        {
            // The differential's second reset puts PAL's PPU on a dot phase chosen for the run;
            // the window just before PAL's pre-render line starts at once after it, so the run's
            // writes sweep that line on that phase.
            a.Inc(Starts);
            a.Lda(Starts);
            a.CmpI(3);
            a.Bne("third_start");
            a.LdaI(1);
            a.Sta(Window);
            a.LdaI(0);
            a.Sta(Fine);
            a.Label("third_start");
        }

        if (workload == Workload.Sprite0)
        {
            // The reset button comes after a frame end, before the place the last NMI set is
            // drawn: so after any reset that place is shown again, not passed over.
            a.LdaI(0);
            a.Sta(Drawn);
        }

        a.Jsr("board_init");
        a.Jsr("palette");
        a.Jsr("fill_nt");
        if (ChrRam)
        {
            a.Jsr("fill_chr");
        }

        a.Jsr("board_post_fill");
        a.Jsr("oam_init");
        a.Jsr("workload_init");
        a.LdaI(CtrlValue);
        a.Sta(Ctrl);
        a.LdaI(workload == Workload.Background ? 0x0A : 0x1E);
        a.Sta(Mask);
        a.Cli();
        a.Lda(Ctrl);
        a.Sta(0x2000);
        if (workload == Workload.Nmi)
        {
            a.Lda(Mask);
            a.Sta(0x2001);
        }

        a.Jmp("main");

        EmitMain();
        EmitNmi();
        EmitIrq();
        EmitCommon();
        EmitBoard();
        EmitWorkload();
    }

    private void EmitMain()
    {
        Asm a = _a;
        a.Label("main");
        switch (workload)
        {
            case Workload.Fuzz:
                a.Jsr("rand");
                a.AndI(0x0F);
                a.Tax();
                a.LdaX("reg_lo");
                a.Sta(Ptr);
                a.LdaX("reg_hi");
                a.Sta(Ptr + 1);
                a.LdyI(0);
                a.Jsr("rand");
                a.AndI(0x03);
                a.Beq("fuzz_read");
                a.Jsr("rand");
                a.OraX("reg_or");
                a.StaIndY(Ptr);
                a.Jmp("fuzz_after");
                a.Label("fuzz_read");
                a.LdaIndY(Ptr);
                a.Label("fuzz_after");
                a.Jsr("rand");
                a.AndI(0x0F);
                a.Bne("fuzz_no_board");
                a.Jsr("board_action");
                a.Label("fuzz_no_board");
                a.Jsr("rand");
                a.AndI(0x1F);
                a.Bne("fuzz_no_poll");
                a.Jsr("poll_sprite0");
                a.Label("fuzz_no_poll");
                a.Jsr("rand");
                a.AndI(0x3F);
                a.Tay();
                a.Iny();
                a.Label("fuzz_delay");
                a.Dey();
                a.Bne("fuzz_delay");
                a.Jmp("main");
                Table("reg_lo", [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x14, 0x15, 0x17, 0x01, 0x05, 0x06, 0x07, 0x16]);
                Table("reg_hi", [0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x40, 0x40, 0x40, 0x20, 0x20, 0x20, 0x20, 0x40]);
                Table("reg_or", [0x00, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
                break;

            case Workload.Sprite0:
                a.Label("s0_clear");
                a.Bit(0x2002);
                a.Bvs("s0_clear");
                a.Label("s0_hit");
                a.Bit(0x2002);
                a.Bvc("s0_hit");
                a.Lda(Fine);
                a.Sta(DelayLo);
                a.LdaI(0);
                a.Sta(DelayHi);
                a.Jsr("delay");
                a.Lda(Frame);
                a.Sta(0x2005);
                a.LdaI(0);
                a.Sta(0x2005);
                a.LdaI(0x20);
                a.Sta(0x2006);
                a.Lda(Frame);
                a.Sta(0x2006);
                a.Jsr("board_action");
                a.Jmp("main");
                break;

            case Workload.Nmi:
                a.Bit(0x2002);
                a.Bpl("main");
                a.Inc(Frame);
                a.Bne("nmi_main_1");
                a.Inc(Frame + 1);
                a.Label("nmi_main_1");
                a.Jsr("sweep");
                a.Jsr("frame_setup");
                a.Lda(Frame);
                a.AndI(1);
                a.Tax();
                a.Lda(Fine);
                a.Clc();
                a.AdcX("nmi_base_lo");
                a.Sta(DelayLo);
                a.LdaX("nmi_base_hi");
                a.AdcI(0);
                a.Sta(DelayHi);
                a.Jsr("delay");
                a.LdxI(12);
                a.Label("burst");
                a.LdaI(0x80);
                a.Sta(0x2000);
                a.Lda(0x2002);
                a.LdaI(0x00);
                a.Sta(0x2000);
                a.Lda(0x2002);
                a.Dex();
                a.Bne("burst");
                a.Jsr("board_action");
                a.Jmp("main");
                Table("nmi_base_lo", NmiBases.Select(b => (byte)b).ToArray());
                Table("nmi_base_hi", NmiBases.Select(b => (byte)(b >> 8)).ToArray());
                break;

            default:
                // The timed workloads run in the NMI handler, so the delay starts a fixed time
                // after VBlank begins; the main loop waits.
                a.Jmp("main");
                Table("base_lo", Bases.Select(b => (byte)b).ToArray());
                Table("base_hi", Bases.Select(b => (byte)(b >> 8)).ToArray());
                break;
        }
    }

    private void EmitNmi()
    {
        Asm a = _a;
        a.Label("nmi");
        if (workload == Workload.Nmi)
        {
            a.Inc(NmiCount);
            a.Rti();
            return;
        }

        a.Pha();
        a.Txa();
        a.Pha();
        a.Tya();
        a.Pha();
        a.LdaI(0);
        a.Sta(0x2003);
        a.LdaI(Oam >> 8);
        a.Sta(0x4014);
        a.Inc(Frame);
        a.Bne("nmi_1");
        a.Inc(Frame + 1);
        a.Label("nmi_1");
        if (workload is Workload.Scroll or Workload.Mask or Workload.Sprites or Workload.Apu or Workload.Background)
        {
            // From the DMA, the window's delay, the action, then the frame's setup after it.
            a.Jsr("sweep");
            a.Ldx(Window);
            a.Lda(Fine);
            a.Clc();
            a.AdcX("base_lo");
            a.Sta(DelayLo);
            a.LdaX("base_hi");
            a.AdcI(0);
            a.Sta(DelayHi);
            a.Jsr("delay");
            a.Jsr("workload_action");
            a.Jsr("board_action");
        }
        else
        {
            a.Jsr("sweep");
        }

        a.Jsr("frame_setup");
        a.LdaI(1);
        a.Sta(Go);
        a.Pla();
        a.Tay();
        a.Pla();
        a.Tax();
        a.Pla();
        a.Rti();
    }

    private void EmitIrq()
    {
        Asm a = _a;
        a.Label("irq");
        a.Pha();
        a.Txa();
        a.Pha();
        if (board == BoardKind.Mmc3)
        {
            // Acknowledge, enable again, switch the 1 KB CHR bank at $1000, and toggle greyscale.
            a.Sta(0xE000);
            a.Sta(0xE001);
            a.LdaI(2);
            a.Sta(0x8000);
            a.Lda(Frame);
            a.Sta(0x8001);
            a.Lda(Mask);
            a.EorI(0x01);
            a.Sta(0x2001);
        }

        // The frame counter's flag goes with the $4015 read, the DMC's with the $4010 write.
        a.Lda(0x4015);
        a.LdaI(0x4F);
        a.Sta(0x4010);
        a.Pla();
        a.Tax();
        a.Pla();
        a.Rti();
    }

    private void EmitCommon()
    {
        Asm a = _a;

        // A 16-bit Galois LFSR; the low byte in A.
        a.Label("rand");
        a.Lsr(Rng + 1);
        a.Ror(Rng);
        a.Bcc("rand_1");
        a.Lda(Rng + 1);
        a.EorI(0xB4);
        a.Sta(Rng + 1);
        a.Label("rand_1");
        a.Lda(Rng);
        a.Rts();

        a.Label("palette");
        a.LdaI(0x3F);
        a.Sta(0x2006);
        a.LdaI(0);
        a.Sta(0x2006);
        a.LdxI(0);
        a.Label("palette_1");
        a.LdaX("palette_data");
        a.Sta(0x2007);
        a.Inx();
        a.CpxI(32);
        a.Bne("palette_1");
        a.Rts();
        Table("palette_data", Enumerable.Range(0, 32).Select(_ => (byte)_random.Next(0x40)).ToArray());

        // 4 KB of noise into the nametables from $2000.
        a.Label("fill_nt");
        a.LdaI(0x20);
        a.Sta(0x2006);
        a.LdaI(0);
        a.Sta(0x2006);
        a.LdyI(16);
        a.LdxI(0);
        a.Label("fill_nt_1");
        a.Jsr("rand");
        a.Sta(0x2007);
        a.Inx();
        a.Bne("fill_nt_1");
        a.Dey();
        a.Bne("fill_nt_1");
        a.Rts();

        // 8 KB of noise into CHR RAM, then tiles 0 and 1 of both tables opaque.
        a.Label("fill_chr");
        a.LdaI(0);
        a.Sta(0x2006);
        a.Sta(0x2006);
        a.LdyI(32);
        a.LdxI(0);
        a.Label("fill_chr_1");
        a.Jsr("rand");
        a.Sta(0x2007);
        a.Inx();
        a.Bne("fill_chr_1");
        a.Dey();
        a.Bne("fill_chr_1");
        foreach (int table in new[] { 0x00, 0x10 })
        {
            a.LdaI(table);
            a.Sta(0x2006);
            a.LdaI(0);
            a.Sta(0x2006);
            a.LdxI(0x20);
            a.LdaI(0xFF);
            a.Label($"fill_chr_opaque_{table}");
            a.Sta(0x2007);
            a.Dex();
            a.Bne($"fill_chr_opaque_{table}");
        }

        a.Rts();

        a.Label("oam_init");
        a.LdxI(0);
        a.Label("oam_init_1");
        a.Jsr("rand");
        a.StaX(Oam);
        a.Inx();
        a.Bne("oam_init_1");
        a.Rts();

        // Waits for sprite 0 hit, or about 30,000 cycles.
        a.Label("poll_sprite0");
        a.LdxI(0);
        a.LdyI(24);
        a.Label("poll_sprite0_1");
        a.Bit(0x2002);
        a.Bvs("poll_sprite0_2");
        a.Dex();
        a.Bne("poll_sprite0_1");
        a.Dey();
        a.Bne("poll_sprite0_1");
        a.Label("poll_sprite0_2");
        a.Rts();

        // Once a frame: the sweep's counts, the board's and the workload's frame, the scroll, the
        // control and mask registers.
        a.Label("sweep");
        a.Inc(Fine);
        a.Lda(Fine);
        a.CmpI(FineCount);
        a.Bne("sweep_1");
        a.LdaI(0);
        a.Sta(Fine);
        a.Inc(Window);
        a.Lda(Window);
        a.CmpI(Bases.Length);
        a.Bne("sweep_1");
        a.LdaI(0);
        a.Sta(Window);
        a.Label("sweep_1");
        a.Rts();

        a.Label("frame_setup");
        a.Jsr("board_frame");
        a.Jsr("workload_frame");
        if (workload == Workload.Background)
        {
            a.Lda(ScrollX);
            a.Sta(0x2005);
            a.Lda(ScrollY);
            a.Sta(0x2005);
        }
        else
        {
            a.LdaI(0);
            a.Sta(0x2005);
            a.Sta(0x2005);
        }

        a.Lda(Ctrl);
        a.Sta(0x2000);

        // Rendering off in one frame in four, at random: on NTSC an odd frame then keeps its last
        // dot, so where the CPU's cycles fall among the PPU's dots moves at random, and over the
        // run a write lands on each of the three dots of a cycle, not only two.
        a.Jsr("rand");
        a.AndI(0x03);
        a.Php();
        a.Lda(Mask);
        a.Plp();
        a.Bne("frame_setup_on");
        a.AndI(0xE7);
        a.Label("frame_setup_on");
        a.Sta(0x2001);
        if (workload == Workload.Sprite0)
        {
            // Whether this frame draws the place workload_frame put sprite 0 at.
            a.AndI(0x18);
            a.Sta(Drawn);
        }

        a.Rts();

        // DelayHi x 256 cycles, then DelayLo single cycles down a clockslide: CMP #$C9 takes 2
        // cycles for 2 bytes, and the slide's end, CMP $EA or CMP #$C5 then NOP, takes 3 or 4, so
        // entering it n bytes from the end costs n + 3 cycles.
        a.Label("delay");
        a.Ldx(DelayHi);
        a.Beq("delay_fine");
        a.Label("delay_1");
        a.LdyI(50);
        a.Label("delay_2");
        a.Dey();
        a.Bne("delay_2");
        a.Dex();
        a.Bne("delay_1");
        a.Label("delay_fine");
        a.Sec();
        a.LdaLo("slide_end");
        a.SbcZ(DelayLo);
        a.Sta(Jump);
        a.LdaHi("slide_end");
        a.SbcI(0);
        a.Sta(Jump + 1);
        a.JmpInd(Jump);
        a.Data(Enumerable.Repeat((byte)0xC9, 255));
        a.Label("slide_end");
        a.Data(0xC5, 0xEA);
        a.Rts();
    }

    private void EmitBoard()
    {
        Asm a = _a;
        switch (board)
        {
            case BoardKind.Mmc1:
                a.Label("board_init");
                a.LdaI(0x80);
                a.Sta(0x8000);
                a.LdaI(0x1E);
                a.Jsr("mmc1_control");
                a.LdaI(0);
                a.Jsr("mmc1_chr0");
                a.LdaI(1);
                a.Jsr("mmc1_chr1");
                a.Rts();
                Serial("mmc1_control", 0x8000, 5);
                Serial("mmc1_chr0", 0xA000, 5);
                Serial("mmc1_chr1", 0xC000, 5);
                Serial("mmc1_chr0_4", 0xA000, 4);
                a.Label("board_frame");
                a.LdaI(0x80);
                a.Sta(0x8000);
                a.Lda(Frame);
                a.AndI(1);
                a.OraI(0x1E);
                a.Jsr("mmc1_control");
                a.LdaI(0);
                a.Jsr("mmc1_chr0");
                a.Rts();

                // The chip takes the first write of a read-modify-write and ignores the second,
                // which comes on the next cycle; four more writes make five.
                a.Label("board_action");
                a.Inc("mmc1_zero");
                a.Lda(Frame);
                a.Jsr("mmc1_chr0_4");
                a.Rts();
                a.Label("mmc1_zero");
                a.Data(0x00);
                break;

            case BoardKind.Cnrom:
                a.Label("board_init");
                a.Label("board_frame");
                a.LdxI(0);
                a.LdaX("cnrom_banks");
                a.StaX("cnrom_banks");
                a.Rts();
                a.Label("board_action");
                a.Lda(Frame);
                a.AndI(3);
                a.Tax();
                a.LdaX("cnrom_banks");
                a.StaX("cnrom_banks");
                a.Rts();
                Table("cnrom_banks", [0, 1, 2, 3]);
                break;

            case BoardKind.Axrom:
                a.Label("board_init");
                a.Label("board_frame");
                a.LdaI(0);
                a.Sta(0x8000);
                a.Rts();
                a.Label("board_action");
                a.Lda(Frame);
                a.AndI(0x10);
                a.EorI(0x10);
                a.Sta(0x8000);
                a.Rts();

                // The second screen, different from the first.
                a.Label("board_post_fill");
                a.LdaI(0x10);
                a.Sta(0x8000);
                a.LdaI(0x20);
                a.Sta(0x2006);
                a.LdaI(0);
                a.Sta(0x2006);
                a.LdyI(4);
                a.LdxI(0);
                a.Label("axrom_fill");
                a.Jsr("rand");
                a.Sta(0x2007);
                a.Inx();
                a.Bne("axrom_fill");
                a.Dey();
                a.Bne("axrom_fill");
                a.LdaI(0);
                a.Sta(0x8000);
                a.Rts();
                break;

            case BoardKind.Uxrom:
                a.Label("board_init");
                a.LdaI(0);
                a.Sta(0x8000);
                a.Rts();

                // Eight bytes of CHR RAM rewritten in VBlank, in the second table's tiles.
                a.Label("board_frame");
                a.LdaI(0x10);
                a.Sta(0x2006);
                a.Lda(Frame);
                a.OraI(0x20);
                a.Sta(0x2006);
                a.LdxI(8);
                a.Label("uxrom_chr");
                a.Lda(Frame);
                a.Sta(0x2007);
                a.Dex();
                a.Bne("uxrom_chr");
                a.Rts();
                a.Label("board_action");
                a.Lda(Frame);
                a.AndI(1);
                a.Sta(0x8000);
                a.Rts();
                break;

            case BoardKind.Mmc3:
                a.Label("board_init");
                for (int r = 0; r < 6; r++)
                {
                    a.LdaI(r);
                    a.Sta(0x8000);
                    a.LdaI(new[] { 0, 2, 4, 5, 6, 7 }[r]);
                    a.Sta(0x8001);
                }

                a.LdaI(0);
                a.Sta(0xA000);
                a.Rts();

                // The banks back, the mirroring by the frame, and the IRQ 8 to 71 lines down.
                a.Label("board_frame");
                a.LdaI(2);
                a.Sta(0x8000);
                a.LdaI(4);
                a.Sta(0x8001);
                a.LdaI(0);
                a.Sta(0x8000);
                a.Sta(0x8001);
                a.Lda(Frame);
                a.LsrA();
                a.AndI(1);
                a.Sta(0xA000);
                a.Lda(Frame);
                a.AndI(0x3F);
                a.Clc();
                a.AdcI(8);
                a.Sta(0xC000);
                a.Sta(0xC001);
                a.Sta(0xE001);
                a.Rts();
                a.Label("board_action");
                a.LdaI(0);
                a.Sta(0x8000);
                a.Lda(Frame);
                a.AndI(0x0E);
                a.Sta(0x8001);
                a.Rts();
                break;

            default:
                a.Label("board_init");
                a.Label("board_frame");
                a.Label("board_action");
                a.Rts();
                break;
        }

        if (board != BoardKind.Axrom)
        {
            a.Label("board_post_fill");
            a.Rts();
        }
    }

    // MMC1's serial port: the low bit of A, then the next, `count` writes to `address`.
    private void Serial(string label, int address, int count)
    {
        Asm a = _a;
        a.Label(label);
        for (int i = 0; i < count; i++)
        {
            a.Sta(address);
            if (i < count - 1)
            {
                a.LsrA();
            }
        }

        a.Rts();
    }

    private void EmitWorkload()
    {
        Asm a = _a;
        a.Label("workload_init");
        switch (workload)
        {
            case Workload.Fuzz:
                // The DMC loops a sample of the program's own bytes, and pulse 1 sounds.
                DmcLoop();
                a.LdaI(0xBF);
                a.Sta(0x4000);
                a.LdaI(0x80);
                a.Sta(0x4002);
                a.LdaI(0x08);
                a.Sta(0x4003);
                break;
            case Workload.Sprites:
                a.LdxI(0);
                a.Label("sprites_init");
                a.LdaX("sprites_oam");
                a.StaX(Oam);
                a.Inx();
                a.Bne("sprites_init");
                break;
            case Workload.Apu:
                a.LdaI(0x1F);
                a.Sta(0x4015);
                a.LdaI(0x00);
                a.Sta(0x4017);
                break;
            default:
                break;
        }

        a.Rts();

        a.Label("workload_frame");
        switch (workload)
        {
            case Workload.Sprite0:
                // The next place, if the last was drawn: y down the picture, then the next x,
                // then the clip the other way.
                a.Lda(Drawn);
                a.Beq("s0_same");
                a.Inc(Sprite0Y);
                a.Lda(Sprite0Y);
                a.CmpI(240);
                a.Bne("s0_same");
                a.LdaI(0);
                a.Sta(Sprite0Y);
                a.Inc(Sprite0X);
                a.Lda(Sprite0X);
                a.CmpI(8);
                a.Bne("s0_same");
                a.LdaI(0);
                a.Sta(Sprite0X);
                a.Lda(Sprite0Clip);
                a.EorI(1);
                a.Sta(Sprite0Clip);
                a.Label("s0_same");

                // Sprite 0 there, tile 0, flips and priority from the frame, written to OAM now,
                // in VBlank, so the frame after it shows it (the next NMI's DMA would be a frame
                // late), with OAMADDR back at 0.
                a.Ldx(Sprite0X);
                a.LdaX("sprite0_x");
                a.Sta(Oam + 3);
                a.Lda(Sprite0Y);
                a.Sta(Oam);
                a.LdaI(0);
                a.Sta(Oam + 1);
                a.Lda(Frame);
                a.AndI(0xE3);
                a.Sta(Oam + 2);
                a.LdaI(0);
                a.Sta(0x2003);
                for (int i = 0; i < 4; i++)
                {
                    a.Lda(Oam + i);
                    a.Sta(0x2004);
                }

                a.LdaI(0);
                a.Sta(0x2003);

                // The left clips: off, or on, both or one, by the frame.
                a.Lda(Sprite0Clip);
                a.Beq("s0_clip_off");
                a.Lda(Frame);
                a.AndI(3);
                a.Tax();
                a.LdaX("sprite0_clip_on");
                a.Jmp("s0_clip_done");
                a.Label("s0_clip_off");
                a.LdaI(0x1E);
                a.Label("s0_clip_done");
                a.Sta(Mask);
                break;
            case Workload.Sprites:
                // Each sprite down by 1 to 4 lines a frame, by its number.
                a.LdxI(0);
                a.Label("sprites_move");
                a.Txa();
                a.LsrA();
                a.LsrA();
                a.AndI(3);
                a.Sec();
                a.AdcX(Oam);
                a.StaX(Oam);
                a.Inx();
                a.Inx();
                a.Inx();
                a.Inx();
                a.Bne("sprites_move");
                break;
            case Workload.Background:
            {
                // X on by an odd step, so fine X takes every value in 8 frames; Y on by a step,
                // back by 240 past 239 for odd seeds, on through the attribute rows for even ones.
                a.Lda(ScrollX);
                a.Clc();
                a.AdcI((2 * seed) - 1);
                a.Sta(ScrollX);
                a.Lda(ScrollY);
                a.Clc();
                a.AdcI(new[] { 1, 2, 3, 5, 7, 11, 13, 4, 6, 9, 10, 15 }[(seed - 1) % 12]);
                if (seed % 2 == 1)
                {
                    a.CmpI(240);
                    a.Bcc("bg_y");
                    a.SbcI(240);
                    a.Label("bg_y");
                }

                a.Sta(ScrollY);

                // PPUCTRL: NMI on, the nametable select from frame bits 2 and 3, the background's
                // pattern table from frame bit 4.
                a.Lda(Frame);
                a.LsrA();
                a.LsrA();
                a.AndI(3);
                a.Sta(Scratch);
                a.Lda(Frame);
                a.AndI(0x10);
                a.Ora(Scratch);
                a.OraI(0x80);
                a.Sta(Ctrl);

                // PPUMASK from the table, by the frame.
                a.Lda(Frame);
                a.AndI(7);
                a.Tax();
                a.LdaX("background_masks");
                a.Sta(Mask);

                // v into the palette, at an entry from the frame: a frame with rendering off shows
                // it; with rendering on, the pre-render line copies t, which the scroll writes set.
                a.LdaI(0x3F);
                a.Sta(0x2006);
                a.Lda(Frame);
                a.AndI(0x1F);
                a.Sta(0x2006);
                break;
            }

            case Workload.Apu:
                a.Lda(Frame);
                a.AndI(0x3F);
                a.CmpI(8);
                a.Bcs("apu_silent");

                // Lengths loaded, halt off, so they count down through the silent frames.
                foreach (var (register, value) in new[] { (0x4000, 0x9F), (0x4004, 0x5A), (0x4008, 0x20), (0x400C, 0x1C) })
                {
                    a.LdaI(value);
                    a.Sta(register);
                }

                foreach (int register in new[] { 0x4002, 0x4006, 0x400A, 0x400E })
                {
                    a.Lda(Frame);
                    a.Sta(register);
                }

                foreach (int register in new[] { 0x4003, 0x4007, 0x400B, 0x400F })
                {
                    a.Lda(Frame);
                    a.OraI(0x08);
                    a.Sta(register);
                }

                a.Label("apu_silent");
                a.Lda(Frame);
                a.AndI(1);
                a.Beq("apu_loop");
                a.LdaI(0x8F);
                a.Sta(0x4010);
                a.LdaI(0x00);
                a.Sta(0x4013);
                a.Jmp("apu_start");
                a.Label("apu_loop");
                DmcLoop();
                a.Label("apu_start");
                a.LdaI(0x1F);
                a.Sta(0x4015);
                break;
            default:
                break;
        }

        a.Rts();

        a.Label("workload_action");
        switch (workload)
        {
            case Workload.Scroll:
            case Workload.Background:
                a.Lda(Frame);
                a.AndI(3);
                a.OraI(0x20);
                a.Sta(0x2006);
                a.Lda(Frame);
                a.Sta(0x2006);
                a.Lda(Frame);
                a.Sta(0x2005);
                a.Lda(Fine);
                a.Sta(0x2005);
                a.Lda(Frame);
                a.AndI(3);
                a.Ora(Ctrl);
                a.Sta(0x2000);
                break;
            case Workload.Mask:
                a.Lda(Frame);
                a.AndI(7);
                a.Tax();
                a.LdaX("mask_values");
                a.Sta(0x2001);
                a.Lda(Mask);
                a.Sta(0x2001);
                break;
            case Workload.Sprites:
                a.Lda(Frame);
                a.Sta(0x2003);
                a.EorI(0x5A);
                a.Sta(0x2004);
                a.Sta(0x2004);
                a.LdaI(Oam >> 8);
                a.Sta(0x4014);
                break;
            case Workload.Apu:
                a.Lda(Frame);
                a.AndI(3);
                a.Tax();
                a.LdaX("frame_counter_values");
                a.Sta(0x4017);
                a.Lda(0x4015);
                a.LdaI(0x1F);
                a.Sta(0x4015);
                break;
            default:
                break;
        }

        a.Rts();

        Table("sprite0_x", [0, 1, 7, 8, 128, 248, 254, 255]);
        if (workload == Workload.Sprite0)
        {
            // PPUMASK with a left clip on: both, the sprites' alone, both, the background's alone.
            Table("sprite0_clip_on", [0x18, 0x1A, 0x18, 0x1C]);
        }

        Table("mask_values", [0x16, 0x0E, 0x1C, 0x1A, 0x06, 0x18, 0x1F, 0xFE]);
        if (workload == Workload.Background)
        {
            // The background on and the sprites off: the left clip off and on, greyscale, each
            // emphasis bit and all three, and the sprites' clip bit, which changes nothing.
            Table("background_masks", [0x0A, 0x08, 0x0B, 0x29, 0x4A, 0x88, 0xEA, 0x0E]);
        }
        Table("frame_counter_values", [0x00, 0x40, 0x80, 0xC0]);
        if (workload == Workload.Sprites)
        {
            // 64 sprites: y in clusters of 16 three lines apart, odd tiles, every attribute, x 4 apart.
            Table("sprites_oam", Enumerable.Range(0, 64).SelectMany(i => new[] { (byte)(50 + ((i & 15) * 3)), (byte)((i * 4) | 1), (byte)(i & 0xE3), (byte)(i * 4) }).ToArray());
        }
    }

    // The DMC looping a 257-byte sample from $C000 at the fastest rate (rate 15).
    private void DmcLoop()
    {
        Asm a = _a;
        a.LdaI(0x4F);
        a.Sta(0x4010);
        a.LdaI(0x40);
        a.Sta(0x4011);
        a.LdaI(0x00);
        a.Sta(0x4012);
        a.LdaI(0x10);
        a.Sta(0x4013);
        a.LdaI(0x1F);
        a.Sta(0x4015);
    }

    private void Table(string label, byte[] bytes)
    {
        _a.Label(label);
        _a.Data(bytes);
    }
}

// The bytes the cartridges are filled with: xorshift64*, seeded, so the cartridges are the same on
// every run and every runtime (System.Random's seeded sequence is not promised across versions).
internal sealed class Noise(ulong seed)
{
    private ulong _state = (seed * 0x9E3779B97F4A7C15UL) | 1;

    public int Next(int below) => (int)(NextByte() % (uint)below);

    public void NextBytes(Span<byte> bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = NextByte();
        }
    }

    private byte NextByte()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return (byte)((_state * 0x2545F4914F6CDD1DUL) >> 56);
    }
}
