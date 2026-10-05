namespace Dbhq.Cpu6502.TestSupport;

/// <summary>
/// Every third-party file the tests use, pinned to a commit and a hash.
/// Nothing here is committed to this repository; see AGENTS.md rule 3.
/// </summary>
public static class Pins
{
    public const string HarteCommit = "2f6980a2d95757486c7bee24355c360e40e2a224";

    public const string DormannCommit = "7954e2dbb49c469ea286070bf46cdd71aeb29e4b";
    public const string DormannAssemblerSha256 = "74f2254d6034c5c04f5d60a33fc76ca58b67de5c645b31667deaaec0bcc411e3";
    public const string DormannFunctionalSha256 = "f2665bd02288866c2b210b908e3f387926b4c9f0e0af5ad5513c474361ad1265";
    public const string DormannDecimalSha256 = "dfbe4b907c5821d47d9f7f74eeb51197cf4b9f5260375bc1c6c48c008220e1a0";
    public const string DormannExtendedSha256 = "72b1f57dc8f22f418ac2e23fc57c43821da84060679a7fcc3302071aa2f76736";

    public const string NestestCommit = "95d8f621ae55cee0d09b91519a8989ae0e64753b";
    public const string NestestRomSha256 = "f67d55fd6b3cf0bad1cc85f1df0d739c65b53e79cecb7fea8f77ec0eadab0004";
    public const string NestestLogSha256 = "442c4dd5539c7e88b3fd73c7b732a7eadbd22b47c2cd9e58397ef147f64f6f8f";

    // The NES test ROMs, from the fork dbhq-uk/nes-test-roms at one commit: the
    // same commit nestest comes from, which a test checks. Each is keyed by its
    // path in the fork and checked against its SHA-256 every time it is read
    // (NesTestRoms.Read). Each task of the NES plan adds the ROMs it uses.
    // nestest's hash is the constant above, so the two can never disagree.
    public const string NesTestRomsCommit = "95d8f621ae55cee0d09b91519a8989ae0e64753b";

    public static IReadOnlyDictionary<string, string> NesTestRomHashes { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["other/nestest.nes"] = NestestRomSha256,
        ["ppu_vbl_nmi/ppu_vbl_nmi.nes"] = "8dbab1be785585c399cf055ef02147b788ab75fd80e81cf9568a2feafc03fb7d",

        // The ten singles of ppu_vbl_nmi, each NROM (task 4 of the NES plan).
        ["ppu_vbl_nmi/rom_singles/01-vbl_basics.nes"] = "06aea5af4edab4e3141c939cd5ac9936f8758203b25dcaf84ae1a09db49e024a",
        ["ppu_vbl_nmi/rom_singles/02-vbl_set_time.nes"] = "dd98856130078844e3aa4bd95a9be8ab501ea84c089f1d8ad49a1b20af4b3a80",
        ["ppu_vbl_nmi/rom_singles/03-vbl_clear_time.nes"] = "787fdaa4dd6c5b6df5f4308fb6d55b57e2c2f69bd5ecdf8ad5c69735db4fcc72",
        ["ppu_vbl_nmi/rom_singles/04-nmi_control.nes"] = "84722c75b896c47c8642f83220230fe14f0a31e55e26ecb83c400e6a26d91b32",
        ["ppu_vbl_nmi/rom_singles/05-nmi_timing.nes"] = "72e515d689d7404ae5779b8c9c4c7b3563a755a94bd44864516f1b03df044482",
        ["ppu_vbl_nmi/rom_singles/06-suppression.nes"] = "811dd5997bbf48c2e5687ab06845f17ea76b2be472786596c334137582cc72aa",
        ["ppu_vbl_nmi/rom_singles/07-nmi_on_timing.nes"] = "1ed154363660b5775b112ae63ce9bb4e400ebde2afef4d0ac12fc433efda3702",
        ["ppu_vbl_nmi/rom_singles/08-nmi_off_timing.nes"] = "1d2a4093091c8e58a7f99d6a3531bbc6346b52cfc59bcb17ca04c1f2376cf2fc",
        ["ppu_vbl_nmi/rom_singles/09-even_odd_frames.nes"] = "1ac04283021ddd9294cc74ee709c55e20a350dc4815c15a8a93b3654837e858d",
        ["ppu_vbl_nmi/rom_singles/10-even_odd_timing.nes"] = "7217d2d172ce11ad45c4da40c2f22201cf0eb758bc2cd8dd39d2cf0a7d4ca83e",
    };

    // The KIM-1 monitor ROM, MOS Technology's, as Hans Otten dumped it from
    // real 6530-002 and 6530-003 chips, with their $00 filler bytes. The files
    // are committed under roms/, with where they came from and what is known
    // about their rights in roms/README.md, so nothing here is fetched at all
    // (AGENTS.md rules 3 and 4). The paths are repository-relative, and the
    // hashes are checked every time a file is read.
    public const string Kim1Rom002Path = "roms/kim-1/6530-002.bin";
    public const string Kim1Rom002Sha256 = "e9e5245854603cdbc0208235310db1bf6e6a75904960385baaddd38fe60ef750";
    public const string Kim1Rom003Path = "roms/kim-1/6530-003.bin";
    public const string Kim1Rom003Sha256 = "f112a707188a82b87de5be78b7ffa014e18240aa08825055c90d2e6626092fd7";

    // The BBC Micro Model B's three 16 KB ROMs: the operating system (MOS 1.20),
    // BBC BASIC 2 and the Disc Filing System (DFS 1.20). Acorn's work, taken from
    // jsbeeb's public/roms/ at commit e27b20d4a33c2a7b17d2cf830f4695e961b6846e and
    // committed under roms/bbc-micro/, with where they came from and what is known
    // about their rights in roms/README.md (AGENTS.md rules 3 and 4). The paths
    // are repository-relative and the hashes are checked every time a file is read.
    public const string BbcOsPath = "roms/bbc-micro/os.rom";
    public const string BbcOsSha256 = "2d9fea69017864f6962704481829f95fee08446c8c3a13826d5d4e44000ac9de";
    public const string BbcBasicPath = "roms/bbc-micro/BASIC.ROM";
    public const string BbcBasicSha256 = "45bd55dc0f6f0f8f1fe9e2481de7def206565eec8f600ba3068b849ca4132079";
    public const string BbcDfsPath = "roms/bbc-micro/DFS-1.2.rom";
    public const string BbcDfsSha256 = "e745e34895225a6650b712c1dd0656cb0b0b15f072a8ae6d9ea8d1ac257eb3d6";
}
