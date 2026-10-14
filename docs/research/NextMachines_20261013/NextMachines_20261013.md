# Keep the Atari 2600 next after the Electron; the Atari 8-bit family is the strongest case to move up

*standard · 6 angles, one round each, not saturated · 119 sources (54 opened, 0 via Bright Data, 10 blocked) · 2 disconfirming searches · no finding dropped*

This is a decision memo for dbhq-uk/6502: which 6502-family machine to build after the Acorn Electron, written on 13 October 2026 from the repository at `origin/main` (`54c1016`) and from web research the same day.

**Question:** Which machine should be implemented next after the Acorn Electron, and in what order should the rest follow?

**Scope:** Nine candidates: the Atari 2600, Commodore 64, VIC-20, Apple II, Atari 8-bit family, Oric, PET, BBC Master and Apple I. Judged against the rules in `AGENTS.md` (rules 3 and 4 on ROMs and inputs), the definition of "implemented" in `docs/superpowers/specs/2026-09-30-machines-and-site-design.md`, and the order Dan set on 4 October 2026 (Electron, Atari 2600, NES, Commodore 64), of which the NES is now done. No machine code is started.

## Answer

Keep the published order: the **Atari 2600** next. It is the only candidate with no system ROM at all, so rules 3 and 4 cost nothing, and it has the fewest new chips. An MIT-licensed hardware test suite for its TIA and RIOT exists on GitHub and can be forked into `dbhq-uk` [1]. That suite is young and was written largely with Claude [1], so it should be treated as a recognised test with care, not as a reference on the level of Dormann or Harte. The **Atari 8-bit family** should move up to follow it, ahead of the Commodore 64: it has a permissively licensed replacement OS and BASIC [21][22] and an MIT test suite that passes on real hardware [23], and its chip set serves up to four registry rows. The Commodore 64 has the weakest ROM position of the nine: the named holder says a licence is needed [5], and none has been found for open emulators.

## How the count works, and why it matters here

The headline figure is computed by `counts()` in `site/src/lib/registry.mjs`: one per registry row whose `status` is `running`. `validateRegistry()` in the same file makes a running row name an acceptance suite that passes with nothing failed or skipped, and carry at least one photograph. So a machine counts once per registry row, and a variant counts only if it has its own row. The NES precedent put NTSC and PAL in one row (`docs/superpowers/specs/2026-10-05-nes-design.md`), while the registry already gives separate rows to the Apple II and II Plus, the Oric-1 and Atmos, the BBC Micro B+ and Master, and four Atari 8-bit rows plus the Atari 5200. As counted on 13 October 2026 by `python3 -c "import json; print(sum(m['status']=='running' for m in json.load(open('machines/registry.json'))['machines']))"`, 3 rows are running.

## The yardstick

Sizes below are in plan tasks, each built by a fresh agent and reviewed, as the BBC Micro and NES plans were. As counted on 13 October 2026 by `grep -c -E '^#{2,3} Task'` on each plan, and `gh api repos/dbhq-uk/6502/pulls/<n>` for the pull requests:

| Machine | Plan tasks | Pull requests to count | Machine pull request |
|---|---|---|---|
| BBC Micro | 15 | design #32, plan #40, machine #41; then discs #50 and models #48 | 35 commits, 153 files, designed 2 Oct, merged 4 Oct |
| NES | 16 | machine #55; then lazy chips #70 (6 tasks), reset fix #71, scanline renderer #83, models #76 (11 tasks) | 61 commits, 200 files, designed 5 Oct, merged 6 Oct |
| Electron | 15 | design #52, machine #54, still open | 101 commits, 294 files on 13 Oct |

So a BBC-sized machine is about 15 tasks and three to five pull requests before it counts, and a machine with a chip that runs at several times the CPU clock (the NES PPU) needed a further speed pass of about 6 tasks.

## What the repository already covers

**The core** needs nothing new for any candidate. The 6507, 6510 and 6502C "Sally" all run as `Nmos6502` (`docs/the-6502-family.md`); the 6507's 13-bit address and the 6510's I/O port belong in the machine's bus by rule 2. Stalls already live in the bus: the NES bus halts the CPU on a read for its DMA units (`src/Dbhq.Machines.Nes/NesBus.cs`), and the BBC bus stretches cycles for 1 MHz devices (`src/Dbhq.Machines.BbcMicro/BbcBus.cs`). The 2600's WSYNC, ANTIC's DMA on the Atari 8-bit and the VIC-II's bad lines on the C64 are the same pattern. The BBC Master's G65SC12 is listed as the 65C02 without the bit instructions, the core's `Synertek65C02` set; whether it matches exactly is not established [28].

**The shared browser host** (`site/public/machine-host.js`) loads the WebAssembly, runs the frame loop against a cycle budget and reports speed. Each machine still writes its own panel, keys and audio script (`kim-1.js`, `bbc-micro.js`, `nes.js`, `bbc-audio.js`, `nes-audio.js`), and the Electron branch reuses no chip code from the BBC library. The NES page's load-your-own cartridge flow and bundled public-domain homebrew are the pattern a ROM-less console would copy.

**Chips that exist and could be extracted** into a shared library under the design's "extracted, not designed up front" rule: the 6522 VIA (`src/Dbhq.Machines.BbcMicro/Via6522.cs`, used by the VIC-20, PET, Oric and Master), the 6845 CRTC (later PETs and the Master), and the KIM-1's 6530 (`src/Dbhq.Machines.Kim1/Rriot6530.cs`), the nearest existing code to the 2600's 6532 RIOT.

## Findings

### Finding 1: The Atari 2600 has no ROM question and a forkable, MIT, but young, TIA and RIOT test suite

**Confidence: Moderate** - the suite's own repository page and the GitHub API, plus the Gopher2600 author's own page showing no older public suite; no independent user of the suite was found.

The 2600 runs whatever cartridge is inserted, as the NES does, so rules 3 and 4 need only a bundled homebrew with a clear licence. `missingno-vcs-tests` covers the TIA, the RIOT, bank switching and NTSC, PAL and SECAM behaviour, and is MIT [1]. It was "Verified on real hardware (an Australian PAL all-black unit, July 2026)", and the README says Claude "was used extensively in the development of this suite" [1]. It leaves the 6507's instructions to SingleStepTests [1], which this core already passes. The older material is not packaged: the Gopher2600 author says he aims to distribute his collected test ROMs "assuming I can get the required permissions" [2]. A disconfirming search for a recognised TIA suite found nothing older or better established. Homebrew to bundle exists with stated licences, such as Core-Escape under CC0 [3] and 2048-2600 under MIT with separately copyrighted cartridge art [4], but each needs a rights pass like the NES's Lan Master. The suite is assembled with DASM, which is GPL; the prebuilt binaries would come from a pinned fork, as Dormann's tests do.

### Finding 2: The Atari 8-bit family has the cleanest ROM position of any computer on the list and a hardware-verified test suite

**Confidence: Moderate** - the licence notices in the ROM source files themselves, the author's own pages and the test suite's own README; all primary, but all from one line of enquiry, so downgraded from Strong.

AltirraOS and Altirra BASIC carry an all-permissive notice: "Copying and distribution of this file, with or without modification, are permitted in any medium without royalty" [21][22]. The atari800 repository on GitHub holds both as source files [21][22], so a fork in `dbhq-uk` gives a pinned copy. Altirra as a whole is GPLv2, and its author cannot offer the original Atari ROMs for download [20]. The original ROMs' only grant found was to the authors of PC Xformer, not to the public [24]. Acid800 "is licensed under the MIT license" and its tests "all pass on real hardware" [23], so it is a recognised acceptance suite that can be committed. The cost is size: ANTIC, GTIA, POKEY and a 6520-family PIA are all new, and ANTIC halts the CPU for DMA.

### Finding 3: The Commodore 64 has the hardest ROM position: the named holder says a licence is needed, and the open replacement is LGPL and partial

**Confidence: Strong** - the licensee's own knowledge base, the Commander X16's ROM licence, Debian's package text and the Open ROMs project's own licence, from separate lines of enquiry.

Cloanto says the CBM 8-bit system ROMs, the C64's included, "are copyrighted works owned by Amiga Corporation" [5] and that redistribution needs "a proper permission (license)" [5]. The Commander X16's licence covers its KERNAL and BASIC only "in the context of the X16 computer" and sends other uses to Cloanto [7]. Debian strips the ROMs from VICE and says "redistribution is not permitted", naming a different holder, Tulip [13], so even the holder's name is contested. Hackaday reported in August 2026 that the 8-bit rights now sit with Amiga Corporation and are licensed to the new Commodore [6]; a disconfirming search found no 2026 source granting free redistribution to emulators. Copies exist in a forkable place: VICE's official GitHub mirror carries the ROMs with only a Commodore copyright line [12]. MEGA65's Open ROMs replace the KERNAL and BASIC under the LGPL [8][9], which rule 3 treats like GPL (pinned fork, not committed). Test material is good: the Lorenz suite says "Public Domain, no Copyright" [10], and VICE's test programs have an unlicensed GitHub mirror [11].

### Finding 4: The Apple II's ROMs have the most explicit refusal on record, but the best emulator test disk after Acid800

**Confidence: Moderate** - a second-hand account of Wozniak's words, the Franklin case and AppleWin's repository; no statement from Apple itself was found.

Vince Briel reported in 2005 that Wozniak told him "Apple would definately not allow Apple II firmware distribution" [16]. In Apple v. Franklin, "substantial portions of the Franklin ROM and operating system had been copied directly" and the ROM was held protectable [19]. AppleWin's GitHub repository nevertheless commits the II, II Plus and IIe ROM images [15], so a forkable pinned copy exists, with no stated basis. Microsoft's MIT release of 6502 BASIC has an Apple II build option [14], but it is not shown to reproduce Applesoft byte for byte. `a2audit` is an MIT emulator test disk for the II, II Plus, IIe and IIc [17]. The Apple I is the opposite case: Wozniak approved the Replica 1 and "Apple had no objection" [18], but there is no test suite, so it would count on a screen check only.

### Finding 5: The VIC-20, PET, Oric and BBC Master are smaller, but each has a weaker rights or test position than the 2600

**Confidence: Moderate** - package and repository listings and the community repositories' own ROM notes; no holder statement for the Oric or for Acorn found.

The VIC-20 and PET ROMs share the C64's holder question [5][13], with copies in VICE and in FreeBSD's package [32] and on zimmers.net [33]; VICE's test programs include VIC-20 tests, mostly of the VIA [11]. The BBC Master's MOS 3.20 is in jsbeeb [25] and b-em [26] on stated fair-use or good-faith terms with removal on request, the same position as the BBC Micro's ROMs already documented; a 2007 thread shows Acorn's successor talking of "further negotiation" and nothing after [31]. No Master test suite was found other than b2's GPL tests captured on hardware [27]. The Oric's owners went through receiverships [30], no holder statement was found, Oricutron ships only the newer Orix ROMs in its repository [29], and no Oric test suite was found.

## Comparison matrix

Sizes are this memo's estimate in plan tasks against the yardstick above, a judgement and not a measurement. "Rows" is how many registry rows the work could make count, each needing its own acceptance suite and photograph.

| Machine | Already covered | New chips | System ROM and rights | Acceptance test | Size (tasks) | Rows |
|---|---|---|---|---|---|---|
| Atari 2600 | Core as `Nmos6502`; NES cartridge flow and bus stall pattern; KIM-1 6530 nearest to the 6532 | TIA (video, audio, collisions, HMOVE), 6532 RIOT, cartridge bank switching | None needed; homebrew under CC0 or MIT exists [3][4] | missingno-vcs-tests, MIT, forkable, young [1] | 10 to 14, likely plus a speed pass | 1 |
| Atari 8-bit family | Core as `Nmos6502`; bus stall pattern | ANTIC with DMA, GTIA, POKEY, PIA, SIO disk | AltirraOS and Altirra BASIC, all-permissive, on GitHub in atari800 [21][22] | Acid800, MIT, passes on real hardware [23] | 20 to 26 for the first row, 2 to 4 per further row | 1, up to 4 with XL, XE, XEGS, and 5200 a fifth |
| Commodore 64 | Core as `Nmos6502`; bus stall pattern; VIA code is a partial start on the 6526 | VIC-II, SID, two 6526 CIAs, PLA; the 1541 drive is its own 6502 with two VIAs | Amiga Corporation per Cloanto, licence needed [5]; copy in VICE mirror [12]; Open ROMs LGPL [9] | Lorenz suite, public domain [10]; VICE testprogs mirror [11] | 25 to 35 with the 1541, 18 to 24 without | 1, and the MAX Machine later |
| VIC-20 | Core; 6522 VIA from the BBC | VIC 6560/6561 | Same holder question as the C64 [5][13]; copies in VICE [12][32] | VICE testprogs VIC-20 list, mostly VIA [11] | 10 to 12 | 1 |
| PET | Core; 6522 VIA; 6845 CRTC for later models | Two 6520 PIAs, discrete video | Same holder question [5][13]; copies in VICE [12][32]; MIT BASIC source with a PET option [14] | [unknown]; VICE's PET list is short and mostly CRTC screenshots, not confirmed here | 8 to 10 | 1 |
| Apple II | Core; nothing else | Discrete video with NTSC colour, Disk II controller, language card | Apple, with a reported refusal [16]; copies in AppleWin [15] | a2audit, MIT [17] | 10 to 14 | 1, and 1 more for the II Plus |
| Oric | Core; 6522 VIA | ULA video, AY-3-8912 sound, tape | Holder unknown after receiverships [30]; no GitHub copy found [29] | None found [29] | 10 to 12 | 1, and 1 more for the Atmos |
| BBC Master | Core as `Synertek65C02`, match not proven [28]; most of the BBC library | 1770 disc controller, RTC and CMOS RAM, shadow and paged RAM | Acorn, documented as for the BBC; copies in jsbeeb and b-em [25][26] | None recognised; b2's tests are GPL [27] | 8 to 12 | 1, and the B+ is close |
| Apple I | Core | 6821 PIA and the terminal | Wozniak approved the Replica 1, Apple did not object [18] | None; screen check only | 3 to 5 | 1, and the Replica 1 |

## Ranking

| Rank | Machine | Why |
|---|---|---|
| 1 | Atari 2600 | No ROM, fewest new chips, a forkable MIT suite, and it tests the core's cycle exactness hardest |
| 2 | Atari 8-bit family | Clean ROM and a hardware-verified MIT suite; most rows per chip set; large |
| 3 | BBC Master | Smallest step after the Electron; Acorn position already documented; no test suite |
| 4 | Commodore 64 | Largest audience and good tests, but the worst ROM position and the largest build |
| 5 | Apple II | Good test disk, few chips, but an explicit refusal on record |
| 6 | VIC-20 | Small, reuses the VIA, shares the C64's ROM problem |
| 7 | PET | Small, weak tests, shares the C64's ROM problem |
| 8 | Apple I | Tiny and cheap, but no test suite; a cheap row, not a milestone |
| 9 | Oric | Unknown holder, no copy found, no tests |

## Recommendation

**Build the Atari 2600 next, as published.** Reasons: it needs no system ROM, so rules 3 and 4 add no risk and no rights document beyond its homebrew; it has the fewest new chips; it has an MIT suite that can be forked and pinned [1]; and racing the beam is the strongest public proof of a cycle-exact core, which is the project's own claim. Plan for a speed pass from the start, because the TIA runs at three colour clocks per CPU cycle, as the NES PPU did.

**Then move the Atari 8-bit family ahead of the Commodore 64.** It is larger than the 2600 but its ROM is the only fully licensed system ROM on the list [21][22], its suite is MIT and hardware-verified [23], and its chips also serve the 5200. Settle the Commodore 64's KERNAL question (Cloanto, Open ROMs, or a documented position under rule 4) before its spec, as the 4 October entry already asked.

**What would overturn this.** If `missingno-vcs-tests` turns out to disagree with real consoles on more than a few tests, the 2600 falls back to the design's screen-check rule and loses its recognised test; it would still come first on ROM and size. If Dan rules that variants count only once per family, the Atari 8-bit's row advantage disappears and the BBC Master moves to second. If Cloanto or Amiga Corporation grants open emulators the C64 ROMs, the C64 moves to second on audience.

## Limitations

Each angle had one round of retrieval and is **not saturated**: every round was still finding new names when the run stopped, at the coordinator's request. No rights holder (Apple, Amiga Corporation, Atari, Acorn's successors or the Oric's) was asked directly, and the Apple position rests on a second-hand 2005 account [16]. VICE's own `README.ROMs`, the Debian copyright file, the Time Extension article on the 2026 Commodore deals and the AtariAge Acid800 thread all refused to open. The licences of 2048-2600, Berta And Butterflies and the GitHub mirror of VICE's test programs were read from READMEs or the GitHub API, not from their licence files, and the mirror has no licence at all [11]. The task sizes are judgements against two machines' records, not measurements. Nothing here was run: no test suite was assembled and no ROM was hashed.

## Found, not researched

Named by a research round and not used above, with the reason:

- **Atari 2600:** AtariTestCartridgeBR (MIT diagnostic cartridge, quote not confirmed on the page), Gopher2600 test ROM collection (unpublished), Paul Slocum console test cartridge (page not found), AtariAge cycle-75 HMOVE test ROM (snippet only, author called it flawed), Berta And Butterflies (MIT, a Game & Watch remake), genemedic (MIT, not opened), vcs-game-maker output games (tool only), doorframe, WashingtonProject and lord-of-the-rings-2600-prologue (GPL-3.0, cannot be committed), Space Treat Deluxe and Star Fire (licence unknown), Upp Plus Plus and Defend Your Castle (no licence stated), 8bitworkshop VCS examples (GPL-3.0 repository).
- **Commodore:** Commodore Business Machines and Commodore International (original authors), Microsoft Corporation (BASIC base, covered above), Tulip (named by Debian, covered above), Cloanto and C64 Forever (covered above), Commander X16 ROM (covered above), ksim65 free-c64-roms (snippet only), VICE SVN trunk data/C64 (SourceForge, not forkable on GitHub), VICE-Team/svn-mirror (covered above), Wolfgang Lorenz C64 Emulator Test Suite (covered above), Tom Seddon's site and the Christer Palm repackaging (not opened), FreeBSD emulators/vice port and Debian vice package (covered above), zimmers.net CBM firmware archive (covered above), libsidplayfp/VICE-testprogs (covered above), vpelletier/vice-emu-testprogs (an older unofficial clone).
- **Apple:** AppleWin and microsoft/BASIC-M6502 and a2audit (covered above), tomcw/a2audit (a fork), mb-audit (Mockingboard only), Wozanium pack (Apple I programs on an Apple II), Replica 1 (covered above), ntvao, Ryukojiro-apple1, yae, Apple II MiSTer, izapple2, OpenEmulator, zserge/bsoz, sma/dart6502 and davervw/wozmon_cbm (emulators or ports seen in snippets only).
- **Atari 8-bit:** the original Atari OS ROMs and the PC Xformer images (grant not public), AltirraOS, Altirra BASIC and Acid800 (covered above), OS++ (TPL licence, text not read), Acid5200 and AcidSAP (siblings of Acid800, not opened), the atari800 list of timing-sensitive titles (regression cases, not a suite).
- **Acorn and Oric:** mattgodbolt/jsbeeb, stardot/b-em and tom-seddon/b2 (covered above), b2's cmos_adc_sbc results (65C12 arithmetic captured on a real Master), pete-gordon/oricutron (covered above), flaxcottage.com BBC ROMs archive and TomHarte/CLK (not opened), SingleStepTests/65x02 synertek65c02 (covered above), DefenceForce Encounter testing mode (a game's own mode, not a suite).

## Bibliography

Every source was opened on 13 October 2026. Dates in brackets are the source's own date where it has one.

[1] ajoneil. "missingno-vcs-tests: Atari VCS/2600 test roms". GitHub. https://github.com/ajoneil/missingno-vcs-tests

[2] jetsetilly. "Gopher2600". GitHub. https://github.com/jetsetilly/gopher2600

[3] murilomgrosso. "Core-Escape" (repository record, licence CC0-1.0) [2025-11-08]. GitHub API. https://github.com/murilomgrosso/Core-Escape

[4] Carlos Duarte do Nascimento. "2048-2600" [2020-10-23]. GitHub. https://github.com/chesterbr/2048-2600

[5] Cloanto. Knowledge base article 13-122. C64 Forever. https://www.c64forever.com/kb/13-122

[6] Hackaday. "Amiga And Commodore, Back Together (Sort Of)" [2026-08-06]. https://hackaday.com/2026/08/06/amiga-and-commodore-back-together-sort-of/

[7] Commander X16 project. "commander-x16-rom-license". NetBSD pkgsrc. https://ftp.netbsd.org/pub/pkgsrc/current/pkgsrc/licenses/commander-x16-rom-license

[8] MEGA65. "Open ROMs". GitHub. https://github.com/MEGA65/open-roms/

[9] MEGA65. "Open ROMs LICENSE". GitHub. https://raw.githubusercontent.com/MEGA65/open-roms/master/LICENSE

[10] VICE team. "testprogs/general/Lorenz-2.15". SourceForge. https://sourceforge.net/p/vice-emu/code/HEAD/tree/testprogs/general/Lorenz-2.15/

[11] libsidplayfp. "VICE-testprogs" (mirror) [2026-08-12]. GitHub. https://github.com/libsidplayfp/VICE-testprogs

[12] VICE team. "svn-mirror" [2026-10-11]. GitHub. https://github.com/VICE-Team/svn-mirror

[13] Debian. "Package: vice (stable)". https://packages.debian.org/stable/vice

[14] Microsoft. "BASIC-M6502" [2025-09-05]. GitHub. https://github.com/microsoft/BASIC-M6502

[15] AppleWin. "resource" folder. GitHub. https://github.com/AppleWin/AppleWin/tree/master/resource

[16] Vince Briel. Forum post [2005-10-24]. Applefritter. https://www.applefritter.com/node/9002

[17] Zellyn Hunter. "a2audit". GitHub. https://github.com/zellyn/a2audit

[18] Wikipedia. "Replica 1" [revision 2026-04-19]. https://en.wikipedia.org/wiki/Replica_1

[19] Wikipedia. "Apple Computer, Inc. v. Franklin Computer Corp." [revision 2026-05-18]. https://en.wikipedia.org/wiki/Apple_Computer,_Inc._v._Franklin_Computer_Corp.

[20] Avery Lee. "Altirra". virtualdub.org. https://virtualdub.org/altirra

[21] atari800 project. "src/roms/altirraos_xl.c". GitHub. https://raw.githubusercontent.com/atari800/atari800/master/src/roms/altirraos_xl.c

[22] atari800 project. "src/roms/altirra_basic.c". GitHub. https://raw.githubusercontent.com/atari800/atari800/master/src/roms/altirra_basic.c

[23] Avery Lee. "Acid800 1.2" (archive and README). virtualdub.org. https://virtualdub.org/downloads/Acid800-1.2.7z

[24] SlackBuilds.org. "atari800_roms". https://slackBuilds.org/repository/15.0/games/atari800_roms/

[25] Matt Godbolt. "jsbeeb public/roms/README" [2024-12-20]. GitHub. https://github.com/mattgodbolt/jsbeeb/blob/main/public/roms/README

[26] stardot. "b-em roms/README.md" [2017-02-15]. GitHub. https://github.com/stardot/b-em/blob/master/roms/README.md

[27] Tom Seddon. "b2 etc/b2_tests/README.md" [2025-04-12]. GitHub. https://github.com/tom-seddon/b2/blob/master/etc/b2_tests/README.md

[28] SingleStepTests. "65x02 synertek65c02 README" [2025-05-10]. GitHub. https://github.com/SingleStepTests/65x02/blob/main/synertek65c02/README.md

[29] Pete Gordon. "Oricutron" [2026-01-23]. GitHub. https://github.com/pete-gordon/oricutron

[30] Wikipedia. "Oric (computer)". https://en.wikipedia.org/wiki/Oric_(computer)

[31] RISC OS Open. "Forum topic 73" [2007-06-11]. https://www.riscosopen.org/forum/forums/2/topics/73

[32] FreshPorts. "emulators/vice". https://www.freshports.org/emulators/vice/?page=1

[33] zimmers.net. "CBM firmware: VIC-20". http://www.zimmers.net/anonftp/pub/cbm/firmware/computers/vic20/index.html
