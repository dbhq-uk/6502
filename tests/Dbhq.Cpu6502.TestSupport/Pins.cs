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

        // The combined apu_test, an MMC1 cartridge like the combined ppu_vbl_nmi above (task 10).
        ["apu_test/apu_test.nes"] = "00d4722bae1c82a14528dd3220462d3fb9ce4b14b8cec996619dea23e07fef0a",

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

        // Sprite 0 hit and the sprite overflow flag, every ROM in each folder, each NROM-128 with
        // CHR RAM (task 5 of the NES plan).
        ["sprite_hit_tests_2005.10.05/01.basics.nes"] = "51819e8e502bd88fe3b7244198a074dbeef2e848f66c587be04b04f1f0d4bb52",
        ["sprite_hit_tests_2005.10.05/02.alignment.nes"] = "125bbb3ce1e67370f1f4559c2ad3221e52a3e98880b9789400292b5f3a8b39e6",
        ["sprite_hit_tests_2005.10.05/03.corners.nes"] = "9dd57776bc6267fe6183c5521d67cbe3fccc6662ae545eb2c419949bf39644d3",
        ["sprite_hit_tests_2005.10.05/04.flip.nes"] = "5f7142bddb51b7577f93fa22f9f668efebbeea00346d7255089e1863acb9d46a",
        ["sprite_hit_tests_2005.10.05/05.left_clip.nes"] = "69b329658c17b953f149c2f0de77eb272089df22c815bd2fd3d6f43206791c13",
        ["sprite_hit_tests_2005.10.05/06.right_edge.nes"] = "8e6653fcb869e06873e29e5e4423122ea72ba0bf38f3ba9e39f471420db759a4",
        ["sprite_hit_tests_2005.10.05/07.screen_bottom.nes"] = "05849956f80267838c5b6556310266b794078a4300841cbb36339fd141905a0b",
        ["sprite_hit_tests_2005.10.05/08.double_height.nes"] = "127fd966b6b32d6d88a53c5f59d7e938827783c9ad056091f119be1c4ab21c71",
        ["sprite_hit_tests_2005.10.05/09.timing_basics.nes"] = "311698c717e50150edd0b5fd0016c41de686463205c20efb5630d6adb90859fd",
        ["sprite_hit_tests_2005.10.05/10.timing_order.nes"] = "0f36bc07bfe51c416e3cc1a5231053572aa6b15aa60e6d2fd0568be49b6dc2e9",
        ["sprite_hit_tests_2005.10.05/11.edge_timing.nes"] = "5a7c121f6e76617be88a0a7035c0e402293be5c685c95b97190a8d70835736ab",
        ["sprite_overflow_tests/1.Basics.nes"] = "1a6782f63ccb3a3dd1aa6a24272036c9c3aa232c2d1ff0b21e872741a3ee4fe2",
        ["sprite_overflow_tests/2.Details.nes"] = "6405a7ff1042fe7a50d9bfe521e43460a3251799425bd3e1863b29b67e7cd587",
        ["sprite_overflow_tests/3.Timing.nes"] = "2252ec8fc35932b408f409ef9b6863edf084aa871fc10ad180b0ac4c2468ef8c",
        ["sprite_overflow_tests/4.Obscure.nes"] = "aebf2199344321465ae0d8dcd81f6c528c7f661f31f1814711365b4e573a8263",
        ["sprite_overflow_tests/5.Emulator.nes"] = "cf994454219696de82794f0b84f2bd63458444d12a1171c85bba8697ab94acb4",

        // The sound unit's length counters, frame counter and IRQ (task 8 of the NES plan). The
        // apu_test singles 1 to 6 are NROM with the $6000 report, and run NTSC; 7 and 8 test the
        // DMC and wait for task 9. pal_apu_tests is every ROM in the folder, NROM-128 with CHR RAM,
        // reporting on the screen, and runs PAL.
        ["apu_test/rom_singles/1-len_ctr.nes"] = "aacf86c1d773badd11392e54506a43a06a3dd0b67a4c255909d1daf770a4a1e2",
        ["apu_test/rom_singles/2-len_table.nes"] = "c002ff1483b4dfb36a6eb004d49739cd58a2dff16e0bac167d5a7c12235caeeb",
        ["apu_test/rom_singles/3-irq_flag.nes"] = "dd888551665937391a2d691b1f96d1858316dbfce4951306146e9e396367f079",
        ["apu_test/rom_singles/4-jitter.nes"] = "bff573d72d0f134fe307f0bb8b968b8d2ffdb85e8aadad9c152839068d6db32a",
        ["apu_test/rom_singles/5-len_timing.nes"] = "4d88f8cc0b21303dc151af4d0f4169d79284634a73082d7ea1ae5cfafedd1e46",
        ["apu_test/rom_singles/6-irq_flag_timing.nes"] = "fc1daff82dd1a49c7c1242392ffbf1c6f44fb70156868582117f2a844cc4dffd",
        ["pal_apu_tests/01.len_ctr.nes"] = "5e4a07738703232dfefce6a26f12da304f333008c60224b27e7fbadf4a7cdc0c",
        ["pal_apu_tests/02.len_table.nes"] = "ac5537885469a85e733df1a7a6a0a76a76f157f080c60d04f1128902a45423d4",
        ["pal_apu_tests/03.irq_flag.nes"] = "e0c04111c61d0fc671990c5c3ac6cb7f57082ad687b5e11d380277c7d75e56d1",
        ["pal_apu_tests/04.clock_jitter.nes"] = "dc85b14f7ece5e7bd4010b831f5b796debfdf338837c8a29a1d221de8c63776d",
        ["pal_apu_tests/05.len_timing_mode0.nes"] = "04896f081373f5ab6ce83ce115c5fc0ff823acf831f1499d7d406f4a651e7cbc",
        ["pal_apu_tests/06.len_timing_mode1.nes"] = "454b1b6339bd2ea27e3f4e8a8de7e2d95e3afc26940a88255e24a033d42d5a05",
        ["pal_apu_tests/07.irq_flag_timing.nes"] = "c91aa1fc7bcb2638f3b07996270eb38c67e8b0fefa1a0db02a34b2e2ffd883c7",
        ["pal_apu_tests/08.irq_timing.nes"] = "dee9e8fac623327b04e8160456362cc1fe4ca0b2c8e3f45eedcb6851ebb00aae",
        ["pal_apu_tests/10.len_halt_timing.nes"] = "c41238ed0e7f4044c21fcd14c99b9e4516611adbee5c5f139d3bb95bebebcec9",
        ["pal_apu_tests/11.len_reload_timing.nes"] = "1e94a9c0d829378f93b460c2c5f875418490401afd50c30cd05ea22113819909",

        // The DMC, its DMA and the mixer (task 9 of the NES plan). Every one is NROM-256 (32 KB of
        // program; the header's mapper nibbles are 0): apu_test's DMC singles 7 and 8 and the four
        // apu_mixer ROMs with CHR ROM report through $6000; dmc_dma_during_read4's five and the two
        // sprdma_and_dmc_dma ROMs, with CHR RAM and CHR ROM, print on the screen. All NTSC.
        ["apu_test/rom_singles/7-dmc_basics.nes"] = "547324867ee0ba2aa11401001d8d1288530aa4e0ecaaac1667ce79980a388ec1",
        ["apu_test/rom_singles/8-dmc_rates.nes"] = "5d9a79a505b37fa277cacc95a362f7e2a56e59ace7a698213d78432cc06a8867",
        ["apu_mixer/dmc.nes"] = "036e7a3222f56e7a823b693bae0243e6c8c7ae032defbbd15c8631da468d6ea7",
        ["apu_mixer/noise.nes"] = "47b637cc911dc4416f55891c67976780e0985b6729b339a3f1db277ef6bd4910",
        ["apu_mixer/square.nes"] = "b16e333a2d3698201fc45c21375507a80981928562d3b10f865e23da3922696a",
        ["apu_mixer/triangle.nes"] = "3756d4be75126ab51b40e013e60a125e0f6f198307dcfb9612cd4477f6762279",
        ["dmc_dma_during_read4/dma_2007_read.nes"] = "a2e0fa3f6f155cbe0b8c9517b2f6a57f1fd68f13711c11d6d2fe5676c522d7b2",
        ["dmc_dma_during_read4/dma_2007_write.nes"] = "54c75d491c685fb4cfff281bcf3e199a41e95f6c523e2b0607d67ba039f19f84",
        ["dmc_dma_during_read4/dma_4016_read.nes"] = "c6af72e11c197b449129921a9992db2351d9121bb593b3d0ab71895b646b0ebe",
        ["dmc_dma_during_read4/double_2007_read.nes"] = "779e6e7db863a7405a3dda8723b8517a23d271e78ce4802970fb0a7d3039ce6b",
        ["dmc_dma_during_read4/read_write_2007.nes"] = "bc5281ca3f12a6d0ac9fe1a5e727ecc3cac5fc6a47f45ac130d644f0dbd522cf",
        ["sprdma_and_dmc_dma/sprdma_and_dmc_dma.nes"] = "db3199bc1b0bdc07a316b3ab999d8fd8bb361456d2154e364c132cb06a26a10f",
        ["sprdma_and_dmc_dma/sprdma_and_dmc_dma_512.nes"] = "3789f5134b0561b4344e3f4ce08b4d2a416f67435e083917a80d87fdb9d3583c",

        // MMC3 and its scanline counter (task 11 of the NES plan). mmc3_test_2's six singles, each
        // mapper 4 with 32 KB of PRG and 8 KB of CHR ROM, report through $6000; mmc3_irq_tests'
        // six, mapper 4 with 16 KB of PRG and CHR RAM, report on the screen. Both run NTSC.
        ["mmc3_test_2/rom_singles/1-clocking.nes"] = "b06d8a97f0ca672be92c841d6af7d1e650696e86e9cc0cf6eeb90d67a6ab499b",
        ["mmc3_test_2/rom_singles/2-details.nes"] = "e7af16c764b119e60effb7b1cfeec3dd8e2e657041283693cdbbeedb4081f1e3",
        ["mmc3_test_2/rom_singles/3-A12_clocking.nes"] = "b375f15b9f9d372c8084b9c50928be9e41a3ac48be831ce82d203c18891433ad",
        ["mmc3_test_2/rom_singles/4-scanline_timing.nes"] = "14a220b9d1272acc7a820ab38e9762a7cdf2d54c65e753be87f23dfcaf1bb845",
        ["mmc3_test_2/rom_singles/5-MMC3.nes"] = "e0824123d60b83868dac1189b28250f8e10376a01be468a5a74aa59937cb32ca",
        ["mmc3_test_2/rom_singles/6-MMC3_alt.nes"] = "56698b6918453d161a8d4e51f66e363d6966b054939c8176c53c401a6b55269b",
        ["mmc3_irq_tests/1.Clocking.nes"] = "699d0644bd2b6ff4c9ba598c9609f4a3da536594b6363585b2caf82cf337ac88",
        ["mmc3_irq_tests/2.Details.nes"] = "0af95238b69806c072c28aed0fa8ad812157dfee928a6c9cea8d5420268baade",
        ["mmc3_irq_tests/3.A12_clocking.nes"] = "3b936e1079f12bdc5e55aa82def017ddc76fd79e3879ff0478d41ca718302e7c",
        ["mmc3_irq_tests/4.Scanline_timing.nes"] = "3369e8f73a96ec97918c6c9440804a369a19256c57df91e62881555f21528894",
        ["mmc3_irq_tests/5.MMC3_rev_A.nes"] = "6b662c2d08ee4094d89b6d1ddde330e47f81929fb642dc217b6e4c33f8926944",
        ["mmc3_irq_tests/6.MMC3_rev_B.nes"] = "d8a2af42cdafe8046b36109e6f6ff71ca0d7f5d62c7d5953e0aa1d1828a86088",

        // The CPU's own tests (task 12 of the NES plan). instr_test-v5's sixteen singles, instr_timing's
        // two and cpu_interrupts_v2's five are NROM with CHR ROM and report through $6000; each folder's
        // combined ROM is MMC1 with CHR RAM. branch_timing_tests' three are NROM-128 with CHR RAM and
        // report on the screen and in $F8, as the 2005 ROMs do. cpu_reset's two report through $6000
        // and ask for the reset button.
        ["branch_timing_tests/1.Branch_Basics.nes"] = "7b69e3044eaeb86317147a900d1f4a467b666f59d375ec1ba6658233f23786cd",
        ["branch_timing_tests/2.Backward_Branch.nes"] = "f7966e9b86b04b4adb987439a442e926e9cfe6bb71436dd5dd56f41f9eb029a4",
        ["branch_timing_tests/3.Forward_Branch.nes"] = "d0fbc6b1899bc948172c45f37a981eb0dade212d7e807cc56efedae93d6f9c9b",
        ["cpu_interrupts_v2/cpu_interrupts.nes"] = "ccbac4e824eb96ecfe8b82d331a083be186eb6776aa57e25c52251eaf7df9c4f",
        ["cpu_interrupts_v2/rom_singles/1-cli_latency.nes"] = "e402d36118f77dcbbe8ddca90c15fc76a46bcb30b25cb028c383e4a621de5fc0",
        ["cpu_interrupts_v2/rom_singles/2-nmi_and_brk.nes"] = "6e6bf6205930afcfebdc213c583df53986a688a8b36f8856b805ef4c1853e6eb",
        ["cpu_interrupts_v2/rom_singles/3-nmi_and_irq.nes"] = "3008a9524d174a8aca562ff0361eba81da53e38cf1ebb5125322fe151f14d945",
        ["cpu_interrupts_v2/rom_singles/4-irq_and_dma.nes"] = "6d7b4c1947ada64679af56cf0c227286b2408afe1747dfaa4dc7363d57ff87f6",
        ["cpu_interrupts_v2/rom_singles/5-branch_delays_irq.nes"] = "f9e10b4a24d8f3cd3e51fb7457c72858aab96a6467fdbbd806d0661c2d32fdc7",
        ["cpu_reset/ram_after_reset.nes"] = "f1802a5618aaaa0c4d592caa45b0b13c54082af93fc311bda0c27bceacbc7c7f",
        ["cpu_reset/registers.nes"] = "a30f33fb6c9f56012fba38dc85ddc3dccc06bfc0b25fef7711b63f8207279715",
        ["instr_test-v5/all_instrs.nes"] = "353870c157242e3d428ef7387109deaee0d2e158bdb432ab9aae4e657072c785",
        ["instr_test-v5/official_only.nes"] = "589b8835deb5cbc69618dac193a3dbd675540f7f2794e2d2a92e97beb8abc3cb",
        ["instr_test-v5/rom_singles/01-basics.nes"] = "4dd1cdd406bc3f747972e7da314ce8ca89321eb7a836c1ced569ee54ae44a384",
        ["instr_test-v5/rom_singles/02-implied.nes"] = "1c4d4fa130cf6feebc072543a5cd3627ae71063b56b08642bf43e9a6c6f44996",
        ["instr_test-v5/rom_singles/03-immediate.nes"] = "6f7ad8ff31c762c37deaee0f323df03eb94025cf1f3b0343ebe6fe567da0e943",
        ["instr_test-v5/rom_singles/04-zero_page.nes"] = "7a8feada4bb4460250c8f05401e5d728878bbe71956756d0b11d488e57eb12fd",
        ["instr_test-v5/rom_singles/05-zp_xy.nes"] = "767f422dc4e651e331456b207f7c6d60d19329fde0c0827e83591dbd91ae5e23",
        ["instr_test-v5/rom_singles/06-absolute.nes"] = "98df36dc4fcc4f37d9eb0539c71283020776b1e5dc6a6ce58671739a8d6534af",
        ["instr_test-v5/rom_singles/07-abs_xy.nes"] = "9ff58d77d8d384cc918fcd3ed877898c5e7330cd475ed2dafb11cbe80ff32eff",
        ["instr_test-v5/rom_singles/08-ind_x.nes"] = "2ec6f5d4a8caee5d8295cebe563f203c26ea9bc05f1dbc967feb88f5dc4f261f",
        ["instr_test-v5/rom_singles/09-ind_y.nes"] = "0fbc8b228d5daa83a4a083bf87ae3a61b5247ebdd91a6b91c8cf8c42784804ac",
        ["instr_test-v5/rom_singles/10-branches.nes"] = "63ab768e88931db6f7dfcfafe43d5e29ebc3dcb80da8fc7fcda8c930f34aef54",
        ["instr_test-v5/rom_singles/11-stack.nes"] = "c534191fe3ea4c8940944fda98dd58eb42710268d453f97e8e2c4ae7f15f9cdb",
        ["instr_test-v5/rom_singles/12-jmp_jsr.nes"] = "f5b4652690fc04e6b573a2b3b54a29407ad0615d3c264e7cb618b6694b50de55",
        ["instr_test-v5/rom_singles/13-rts.nes"] = "b711d25bc55585c252046a1304a0bc64c13cacce7c96a1bac5c8e91f9fc2597f",
        ["instr_test-v5/rom_singles/14-rti.nes"] = "f084b00605be1840946b53935032581e68abe1bb24479942751cfe46ddfcb280",
        ["instr_test-v5/rom_singles/15-brk.nes"] = "da7ae9a191c4483b540771e15b1f6f18df68f1d1ecd717b59ea8b1ee3596ec3e",
        ["instr_test-v5/rom_singles/16-special.nes"] = "7d03410b61784e49920901e84b00a4f31a19078391f20005c6fac9036d2190f7",
        ["instr_timing/instr_timing.nes"] = "3d1bca14266f1e25b75a34ddd29c9df1ce9c6d990c8663a218f72e7861660fb0",
        ["instr_timing/rom_singles/1-instr_timing.nes"] = "e260068839fe3d0402376e97e4ee15f5790ee77c701fd0700bba057527910222",
        ["instr_timing/rom_singles/2-branch_timing.nes"] = "0afaa393f375844ab98834c1ecba7fa6d8c44880c8b6e738936d0f04a84c8538",

        // Dummy reads and writes, open bus and OAM (task 12). cpu_dummy_reads and ppu_read_buffer are
        // CNROM (mapper 3); the rest are NROM with CHR ROM. All report through $6000 except
        // cpu_dummy_reads, which prints on the screen only.
        ["cpu_dummy_reads/cpu_dummy_reads.nes"] = "db4f91b80c5fbc123e7dcb420fb7fea9b8a18613edf4de7f3d1e3ed95e3117c9",
        ["cpu_dummy_writes/cpu_dummy_writes_oam.nes"] = "7c1d71a38b2e873d0874add8b823ff39b99151bb29f50096d8021787020c566c",
        ["cpu_dummy_writes/cpu_dummy_writes_ppumem.nes"] = "f59ac329f4872277ccbeff9dd595b901d861af8d53e8a43dcca93bb86752a6b3",
        ["oam_read/oam_read.nes"] = "f298973dabeb61ca35007445f7a615f77e87703c958c870986af83b1aabde926",
        ["oam_stress/oam_stress.nes"] = "95882d72a7acabe928fd277e3b3e0372f21ef3d41e36d7d8fb17fc017a356f70",
        ["ppu_open_bus/ppu_open_bus.nes"] = "d4208a3ff6340532dd0fced7f9d408d5b6585853a0ddc9c1f64ee1722ef08e67",
        ["ppu_read_buffer/test_ppu_read_buffer.nes"] = "230a52fc557c098eba163801d1b6bbf9f57fe8c5ff79a3f968c804dedb1290ba",

        // The sound unit at power and reset, and the older APU tests (task 12). apu_reset's six are NROM
        // and report through $6000, asking for the reset button. blargg_apu_2005.07.30, the NTSC
        // edition of pal_apu_tests, is NROM-128 with CHR RAM and prints its result code on the screen.
        ["apu_reset/4015_cleared.nes"] = "ef83bc2831f0ddb9e563ac5cbcfa21b129f092911ef42adabae7d47b3e990d95",
        ["apu_reset/4017_timing.nes"] = "0e6072c6dcee98fb73dc7f3af2e48face78be300e8510515ed48dc75d15c1f13",
        ["apu_reset/4017_written.nes"] = "022bd3b45a733179d9a0a9bf0311d09ca81419d7e7434e6f559e42650b39616b",
        ["apu_reset/irq_flag_cleared.nes"] = "e2435b213bf21065b7c9c645359500c1c860a7395d12d051232ab14dad1b0bb5",
        ["apu_reset/len_ctrs_enabled.nes"] = "e05546cbfaa1414d9193b0212084b324ea6b13130af5f171a34c9e574f5ac373",
        ["apu_reset/works_immediately.nes"] = "c750113762ee375319b1bfbf65c457875dbb194649d7e7b3594fba38eb8eefa1",
        ["blargg_apu_2005.07.30/01.len_ctr.nes"] = "e1e3a29ab5369ab84a6f5f2f426c64bde86b9a1d26a906739d43fbf624bb8829",
        ["blargg_apu_2005.07.30/02.len_table.nes"] = "63cc6a57fae3da5e30df9520d02b723c5b93789e8a6eef6793f876345b245b51",
        ["blargg_apu_2005.07.30/03.irq_flag.nes"] = "6f71c7e3de4b6c00da92c20a86c1c2095196a55dc201ee3286004ecf33a08c2f",
        ["blargg_apu_2005.07.30/04.clock_jitter.nes"] = "46fa69b26fe8c24dc1d0b5908f90ab0141972eeb607bd563d28f53d6f4543fe6",
        ["blargg_apu_2005.07.30/05.len_timing_mode0.nes"] = "606802d6849ccfcf74e907a8512c03a50d443752d1f616e62a242a1fa7eca0ff",
        ["blargg_apu_2005.07.30/06.len_timing_mode1.nes"] = "0f34e26d56ad235d8d6d63565ed4728fbd0b9b8590a4fd4048eaf64283b429d4",
        ["blargg_apu_2005.07.30/07.irq_flag_timing.nes"] = "851c9698941d51da34b4bfbc9644aa08ee41b39c6c814cc9b8412c204ea68085",
        ["blargg_apu_2005.07.30/08.irq_timing.nes"] = "0a20a2b9ca9a8e78d65b500b161294c889399f0ff048edd104b07b15255946ca",
        ["blargg_apu_2005.07.30/09.reset_timing.nes"] = "bb04f8328a51abb2d17e6e5362b8375f3cbfbf0641733d068b22f23e7dc588e6",
        ["blargg_apu_2005.07.30/10.len_halt_timing.nes"] = "cbdaa9a5cf9c19ba2360d3349a47922eec25a3e610374d963c422f2b67c57ac9",
        ["blargg_apu_2005.07.30/11.len_reload_timing.nes"] = "40e633285a4a8710780bfd80d346dee62406f4be161eb75615f469cd9e84e132",

        // The browser speed check's ROM (task 6 of the NES plan): SNOW, a demo by Repulse that keeps
        // rendering on and changes the picture every frame, NROM-256 with CHR ROM. Fetched by
        // bench/nes-speed/ from the same fork at the same commit, and never committed.
        ["other/snow.nes"] = "7db551e868b2c0182941bd68adc447c2f0668098a4425e8e170b20e4327b7210",
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
