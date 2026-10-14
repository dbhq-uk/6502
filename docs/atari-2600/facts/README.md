# Atari 2600 fact sheets

The index of the Atari 2600's fact sheets, written on 14 October 2026 before any of its code, so its chips are built from documents and not from memory or from another emulator. They are task 1 of [the Atari 2600 plan](../../superpowers/plans/2026-10-14-atari-2600.md), and every later task names the sections it is built from.

| Sheet | Covers |
|---|---|
| [`bus.md`](bus.md) | The 6507, the 13-bit decode and every mirror, the data bus and what each read drives, WSYNC and RDY, read-modify-write on strobes, the cartridge slot |
| [`riot.md`](riot.md) | The 6532 against the KIM-1's 6530, its pins on the 2600, the register decode, RAM, the ports, the timer, the flag register, the PA7 edge, power on, the joysticks and the console switches bit by bit |
| [`tia.md`](tia.md) | The registers by address, the horizontal counter, WSYNC, RSYNC, VSYNC and VBLANK, the playfield, the objects, priority, the collisions, HMOVE and the motion clocks, the order inside a cycle, the inputs, the sound and the colour signal |
| [`cartridge.md`](cartridge.md) | The window and the 2K mirror, F8, F6, F4, FA and the Superchip, when a new bank answers, the dummy reads, FA's D0 gate, the power-on bank, the board by size and the filler rule |
| [`region.md`](region.md) | Each region's clocks and line counts, the picture window, the pixel shape, and what differs between NTSC and PAL |
| [`suite.md`](suite.md) | Every test of `missingno-vcs-tests`, with its kind, its status under the trust split, its board, its parts and the task in which it should first pass |

## Tags

Every statement carries one tag.

- `[from <source>]`: read directly in that source, opened on 14 October 2026. The short names are in the table below. A section is named after the short name where it helps, for example `[from SPG 3.2]`.
- `[from the suite: <path>]`: read directly in a file of `dbhq-uk/missingno-vcs-tests` at `891c079976a2f68033ca4af2fe2fd85aaf1906c9`, usually a test's header.
- `[from the binaries]`: the built `.a26` files of `dbhq-uk/missingno` at `a4159f47ce7ef6f9c3d5402a8c58b18940045adc`, under `crates/systems/vcs/tests/accuracy/roms/`, read for their size and bytes only.
- `[from the harness: <path>]`: missingno's test harness in the same fork and commit, read only for the protocol numbers it states (below). None of missingno's emulator code was read, and none of the harness's code is used.
- `[inferring ...]`: joins two read facts. The reasoning is given.
- `[guessing - verify]`: not established. A task that relies on it checks it first and corrects the sheet.
- `[measured ...]`: a later task's measurement, added with its date and command.

Where two sources disagree, the sheet says so and says which it follows. Two cautions apply throughout. The Stella Programmer's Guide is Atari's own documentation of 1979, reconstructed later from a scan, and written for programmers, not as a description of the chip's logic. The suite's headers state behaviour checked on one PAL console (the cartridge tests excepted, which were never run on a console), so a statement from the suite is that console's behaviour.

## Sources

| Short name | What | Where it was read |
|---|---|---|
| SPG | Steve Wright, *Stella Programmer's Guide*, Atari, 3 December 1979, reconstructed by Charles Sinnett (1993) and put into HTML by B. Watson (2001). Its PIA chapter numbers its sections from 1.0 again, so they are tagged `SPG PIA 2.3` and so on; the TIA 1A hardware description at its end is tagged `SPG TIA 1A` | `http://www.alienbill.com/2600/101/docs/stella.html`, read through the Internet Archive's copy (`https://web.archive.org/web/2024id_/http://www.alienbill.com/2600/101/docs/stella.html`), because the site's own copy now answers "not found" |
| Towers | Andrew Towers, *Atari 2600 TIA Hardware Notes*, v1.0, 6 March 2003, written from the TIA-1A schematics | `https://www.atarihq.com/danb/files/TIA_HW_Notes.txt` |
| 2k6specs | Martin Korth, *Atari 2600 Specifications* (the nocash documentation) | `https://problemkaputt.de/2k6specs.htm` |
| 6532 | Commodore Semiconductor Group (MOS Technology), *6532 (Memory, I/O, Timer Array)*, data sheet dated 2/81 | `http://www.6502.org/documents/datasheets/mos/mos_6532_riot.pdf` |
| 6532 1977 | MOS Technology, *MCS6532* preliminary data sheet, February 1977 | `http://www.6502.org/documents/datasheets/mos/mos_6532_riot_preliminary_feb_1977.pdf` |
| 6530 | MOS Technology, *MCS6530 (Memory, I/O, Timer Array)* data sheet | `http://www.6502.org/documents/datasheets/mos/mos_6530_rriot.pdf` |
| Rockwell note | Rockwell, *R6500/R6532 Timer Interrupt Precautions*, application note R6500N02, December 1978 | `http://www.6502.org/documents/datasheets/rockwell/rockwell_r6530_r6532_timer_interrupt_precautions.pdf` |
| MOS 6500 | MOS Technology, *MCS6500 Microcomputer Family* preliminary data sheet, May 1976, its signal descriptions ("Ready (RDY)") | `http://archive.6502.org/datasheets/mos_6500_mpu_preliminary_may_1976.pdf`, read through the Internet Archive's copy, because the address now answers "not found" |
| SY6500 | Synertek, *SY6500 8-Bit Microprocessor Family* data sheet, the family table and the pin-outs (the SY6507) | `https://www.princeton.edu/~mae412/HANDOUTS/Datasheets/6502.pdf` |
| the schematic | Atari, *2600/2600A VCS Domestic (M/N) Field Service Manual*, FD100133 Rev. 02, the CX2600 and 2600A motherboard schematics, in a copy redrawn by Jonathan Grimm on 1 August 2024, not a scan of the original drawings | `https://www.videogamemanual.com/Atari%202600%20Field%20Service%20Manual%202024%20(USA).pdf` |
| Horton | Kevin Horton, *Cart Information*, V6.00, 18 April 1997 | `https://atariage.com/2600/programming/bankswitch_sizes.txt` |
| US 4,368,515 | Carl J. Nielsen, Atari Inc, *Bank switchable memory system*, granted 11 January 1983 | `https://patents.google.com/patent/US4368515A/en` |
| US 4,432,067 | Carl J. Nielsen, Atari Inc, *Memory cartridge for video game system*, a division of the above, granted 14 February 1984 | `https://patents.google.com/patent/US4432067A/en` |
| US 4,485,457 | Richard K. Balaska and others, CBS Inc, *Memory system including RAM and page switchable ROM* (RAM Plus, the FA board), filed 31 May 1983 | `https://patents.google.com/patent/US4485457A/en` |
| Wikipedia "Color burst" | The English Wikipedia article, revision 1379521833 (10 October 2026): the NTSC and PAL subcarriers and the 2600's CPU clocks | `https://en.wikipedia.org/w/index.php?title=Color_burst&oldid=1379521833` |
| Wikipedia "Pixel aspect ratio" | The English Wikipedia article, revision 1374925940 (14 September 2026): the square-pixel sampling rates | `https://en.wikipedia.org/w/index.php?title=Pixel_aspect_ratio&oldid=1374925940` |
| Mott 1998 | Bradford W. Mott, "Re: [stella] Games that do bad things to HMOVE...", Stella mailing list, 24 April 1998: his late HMOVE measurements, which Towers cites | `http://www.biglist.com/lists/stella/archives/199804/msg00198.html` |
| TIA schematic sheet 1, sheet 4 | Atari Inc., *TIA-1A Logic Diagram*, rev. E, sheet 1 (sync counter, playfield and motion registers) and sheet 4 (audio circuits), scanned by Mark De Smet; read for structure and labels, the transistor-level logic not traced | `https://www.atariage.com/2600/archives/schematics_tia/index.html` |
| jigo2600 Sound, Composite, Visual Objects, Ports | The jigo2600 project's written analyses of the TIA schematics, `doc/TIA_Sound.md`, `doc/TIA_Composite.md`, `doc/TIA_Visual_Objects.md` and `doc/TIA_Ports.md` at commit `92662bb3006c487f5b3b85bd0996a30950325d59` (BSD-2-Clause). jigo2600 is an emulator; only these documents were read, never its code | `https://github.com/jigo2600/jigo2600/tree/master/doc` |
| FSM | Atari Inc., *Atari Video Computer System Field Service Manual, Model 2600/2600A Domestic (M/N)*, FD100133 Rev. 02, 1983, the original scan (the same manual as "the schematic", which is a 2024 redraw) | `http://www.atariguide.com/pdfs/Atari_2600_VCS_Domestic_Field_Service_Manual.pdf`, read through the Internet Archive's copy of 15 February 2017 |
| Stolberg freqform | Eckhard Stolberg, *Atari 2600 VCS Sound Frequency and Waveform Guide*, v1.01, 25 May 2000. By its own account calculated from Ron Fries's TIASOUND emulation, so it is a cross-check only | `http://buerger.metropolis.de/estolberg/texts/freqform.txt`, read through the Internet Archive |
| Ball 2003 | Eric Ball's posts in the Stella mailing list thread "tia hue luminance", 5, 6, 9 and 11 December 2003, and Eckhard Stolberg's replies: a derivation of the colours from the schematics and the video standard | `https://www.biglist.com/lists/stella/archives/200312/` (messages 37, 49, 71, 90, 96 and 117) |
| Galloway 2003 | David Galloway, "Re: [stella] TIA Audio Polynomials", Stella mailing list, 19 November 2003 | `https://www.biglist.com/lists/stella/archives/200311/msg00142.html` |
| Jentzsch 2008 | Thomas Jentzsch, AtariAge forum thread "2600 and 5200 palettes compared", page 3, October 2008: candidate palettes judged by eye | `https://atariage.com/forums/topic/132561-2600-and-5200-palettes-compared/page/3/`, read through the Internet Archive |
| AtariAge 216590 | AtariAge forum thread 216590, page 4, posts by Trebor (2013 to 2015) and Thomas Jentzsch (May 2020): owners' observations of the colour adjustment | `https://atariage.com/forums/topic/216590-stella-gets-some-new-clothes/page/4/`, read through the Internet Archive |
| the 6502 spec | This repository's core design, `docs/superpowers/specs/2026-09-29-6502-design.md` | this repository |
| Rriot6530.cs | This repository's KIM-1 chip, `src/Dbhq.Machines.Kim1/Rriot6530.cs`, read only to say what the repository's 6530 does | this repository |
| the GitHub issues | `dbhq-uk/6502` issues #107 and #131 to #155, their titles and the tests each names | `https://github.com/dbhq-uk/6502/issues` |

**Nothing here is code from Stella, Gopher2600, missingno, MAME, z26 or jigo2600.** Stella and Gopher2600 are GPL, and missingno is left out because the suite was developed beside it (spec, "Where the code comes from"). jigo2600's written analyses of the schematics were read; its code was not. Stolberg's waveform guide was made from an emulator and is used only as a cross-check. The documents are read for facts, and the sheets restate them in their own words.

## The numbers the plan takes from the suite's harness

The plan's "Fixed before running" uses the protocol the suite was written for, read in `crates/systems/vcs/tests/accuracy/common/mod.rs` of `dbhq-uk/missingno` at `a4159f47ce7ef6f9c3d5402a8c58b18940045adc` on 14 October 2026, its constants and comments only:

| Plan item | Number | Where in the file |
|---|---|---|
| The result protocol | `$80` RESULT, `$81` CODE, `$82` OBSERVED, `$83` EXPECTED; `$A5` pass, `$5A` fail | the constants `RESULT` to `FAIL_MAGIC`; the same in the suite's `include/result.h` (`suite.md` section 2) |
| 1, a self-test's limit | 5,000,000 instructions | the constant `MAX_INSTRUCTIONS` |
| 2, the frame's line limit | 400 lines | the constant `FRAME_LINE_BUDGET`, "enough lines to bound a single frame for either region (PAL is 312)" |
| 3, the screenshot frame | the last of eight frames | the loop in the screenshot comparison, "let the picture settle; keep the last frame produced" |
| 4, the alignment | references "carry the VSYNC-period lines" | the comment in the screenshot comparison; the suite's README says the opposite, and the PNG sizes agree with the README (`suite.md` section 4) |

The harness also states that the board a test runs on is always named, never guessed from the size, and that a source with no `; mapper:` line is a plain 4K board. `suite.md` section 1.4 uses that.
