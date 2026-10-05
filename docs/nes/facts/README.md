# NES fact sheets

Six sheets, written on 5 October 2026 before any of the NES's code, so its
chips are built from the nesdev wiki and not from memory or from another
emulator. They are task 1 of
[the NES plan](../../superpowers/plans/2026-10-05-nes.md), and every later task
names the sections it is built from.

| Sheet | Covers |
|---|---|
| [`timing.md`](timing.md) | The clocks, the frame, the dot ratio, the odd frame, the CPU and PPU alignment at reset, the PAL fourth dot |
| [`bus.md`](bus.md) | The CPU memory map, mirrors, open bus, the register file, OAM DMA, DMC DMA, the controllers, the interrupt lines |
| [`ppu.md`](ppu.md) | The registers, the `v`/`t`/`x`/`w` scroll model, VRAM and OAM, the background and sprite pipelines dot by dot, sprite 0, overflow and its bug, palette and emphasis, the 2C07 differences, the colour table |
| [`apu.md`](apu.md) | Each channel, the frame counter, the mixer, the NTSC and PAL tables |
| [`mappers.md`](mappers.md) | NROM, MMC1, UxROM, CNROM, AxROM and MMC3: registers, banking, mirroring and IRQ |
| [`cartridge.md`](cartridge.md) | iNES, NES 2.0, the region byte, the trainer, and the test ROM protocol |

## Tags

Every statement carries one tag.

- `[from <page>]`: read directly on that nesdev wiki page. The page names are
  the wiki's titles, listed with their revisions below.
- `[from the fork: <path>]`: read directly in a file of `dbhq-uk/nes-test-roms`
  at commit `95d8f621ae55cee0d09b91519a8989ae0e64753b`.
- `[inferring ...]`: joins two read facts. The reasoning is given.
- `[guessing - verify]`: not established. A task that relies on it checks it
  first and corrects the sheet.

Where two pages disagree, the sheet says so and says which it trusts.

## Where the pages were read

The wiki at `www.nesdev.org` refuses automated fetches (a Cloudflare challenge),
so each page was read from its newest copy in the Internet Archive that holds
the page and not the challenge, on 5 October 2026. Each copy names the wiki
revision it is (`oldid`), so a later reader can open exactly the text these
sheets were written from at
`https://www.nesdev.org/w/index.php?title=<page>&oldid=<oldid>`.

| Page | Revision (`oldid`) | Archive copy |
|---|---|---|
| 2A03 | 21333 | 30 Jun 2026 |
| APU | 23811 | 28 Aug 2026 |
| APU DMC | 23783 | 10 Aug 2026 |
| APU Envelope | 23446 | 27 Jun 2026 |
| APU Frame Counter | 23448 | 29 Jun 2026 |
| APU Length Counter | 23449 | 23 May 2026 |
| APU Mixer | 23451 | 11 Feb 2026 |
| APU Noise | 23442 | 27 Jun 2026 |
| APU Pulse | 23444 | 29 Jun 2026 |
| APU Sweep | 23447 | 27 Jun 2026 |
| APU Triangle | 23816 | 19 May 2026 |
| AxROM | 21594 | 19 May 2026 |
| CNROM | 23680 | 26 Mar 2026 |
| Controller reading | 23772 | 11 Aug 2026 |
| CPU interrupts | 21632 | 30 Jun 2026 |
| CPU memory map | 21671 | 30 Jun 2026 |
| CPU power up state | 22091 | 7 Mar 2026 |
| Cycle reference chart (and its redirect, Clock rate) | 22030 | 2 Aug 2026 |
| Detect TV system | 20346 | 6 Mar 2026 |
| DMA | 23450 | 23 Jul 2026 |
| Emulator tests | 23774 | 28 Aug 2026 |
| iNES | 23194 | 20 Jun 2026 |
| Mirroring | 24013 | 6 Jul 2026 |
| MMC1 | 23241 | 23 May 2026 |
| MMC3 | 23702 | 23 May 2026 |
| NES 2.0 | 24066 | 4 Aug 2026 |
| NMI | 23420 | 30 Jun 2026 |
| NROM | 23660 | 19 May 2026 |
| NTSC video (read in task 5) | 24244 | 30 Sep 2026 |
| Open bus behavior | 23814 | 19 May 2026 |
| Overscan | 22760 | 26 Aug 2026 |
| PPU attribute tables | 21522 | 23 May 2026 |
| PPU frame timing | 19961 | 1 Oct 2026 |
| PPU memory map | 22765 | 23 May 2026 |
| PPU nametables | 22659 | 26 Mar 2026 |
| PPU OAM | 23161 | 26 Aug 2026 |
| PPU palettes | 24024 | 28 Aug 2026 |
| PPU pattern tables | 23925 | 23 Jul 2026 |
| PPU power up state | 20192 | 24 Mar 2026 |
| PPU registers | 22850 | 23 Jul 2026 |
| PPU rendering | 23300 | 23 May 2026 |
| PPU scrolling | 23139 | 3 Jun 2026 |
| PPU sprite evaluation | 22442 | 3 Jun 2026 |
| PPU sprite priority | 22861 | 6 Jul 2026 |
| Standard controller | 23466 | 28 Jun 2026 |
| UxROM | 23341 | 23 May 2026 |

**Nothing here is code from Mesen, FCEUX or Nestopia**, which are GPL. The wiki
is read for facts. Where it shows pseudocode (the scroll increments, the mixer
formula), the sheet restates the rule, and the chip is written from the rule.
