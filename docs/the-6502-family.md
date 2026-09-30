# The 6502 family

Everything that ran on a 6502 or one of its descendants, and which of them use
a CPU that the core in this repository implements. Which machines are built is
tracked in `machines/registry.json`, not here.

MOS Technology launched the 6502 in September 1975 at $25. Motorola's 6800
cost $175 at the time, and the 6502 sold for less than a sixth of the price of
it or Intel's 8080. That price is why it ended up in most of the
machines that started home computing, and in the consoles that followed.

## Who made it

A team led by **Chuck Peddle** at MOS Technology in Pennsylvania. Peddle had
worked on Motorola's 6800 and proposed a cheaper microprocessor; Motorola's
management were not interested and told him to stop. On 19 August 1974 seven
of them left Motorola for MOS: Peddle, **Bill Mensch**, **Rod Orgill**,
**Wil Mathys**, **Harry Bawcom**, **Ray Hirt** and **Terry Holdt**.

Peddle, Orgill and Mathys designed the architecture. Mensch, who had
designed Motorola's 6820 interface chip, worked on the instruction decoder,
the arithmetic unit and the registers. Bawcom, Mike Janes and Sydney-Anne
Holt did the layout. The first chips had no rotate-right instruction, `ROR`;
a later revision added it.

Commodore bought MOS Technology in 1976. Peddle was the main designer of the
KIM-1 and of its successor, the Commodore PET. Mensch founded the Western
Design Center in 1978, and its first product was the 65C02, the CMOS 6502
that WDC still makes. The core's `Wdc65C02` variant is that chip.

Years are first release. Where a machine launched in different years in
different regions, the earliest is given.

## What the last column means

| Mark | Meaning |
|---|---|
| **Core** | Uses a CPU the core implements, as the NMOS 6502 |
| **Core, no decimal** | Uses a CPU the core implements, as the NES's 2A03, with decimal mode removed |
| **65C02** | Uses a CPU the core implements, as one of its three 65C02 variants: WDC, Rockwell or Synertek. Each is proven against its own set of Tom Harte's SingleStepTests, the same way as the NMOS chip |
| **Other** | A different CPU on 6502 foundations. Out of scope |

## The chips

### NMOS: the original 6502 and its cut-down versions

| Chip | Year | What is different | Used in | Core |
|---|---|---|---|---|
| 6502 | 1975 | The original | Most of this document | Core |
| 6501 | | Fits a 6800 socket. Withdrawn after Motorola sued | Little | Core |
| 6503, 6504, 6505, 6506 | | 28-pin packages with less address space, some interrupt or ready pins removed | Embedded designs | Core |
| 6507 | | 28-pin, 8 KB address space, no interrupts | Atari 2600, Atari 810 and 1050 disk drives | Core |
| 6508 | | Built-in 8-bit I/O port and 256 bytes of RAM | Embedded designs | Core |
| 6509 | | Addresses 1 MB as 16 banks of 64 KB | Commodore CBM-II | Core |
| 6510 | 1982 | Built-in 6-bit I/O port | Commodore 64 | Core |
| 6512, 6513, 6514, 6515 | | Take an external two-phase clock | BBC Micro B+ (6512) | Core |
| 6502C "Sally" | | Adds a HALT line so the video chip can take the bus for DMA | Atari 8-bit computers, Atari 5200, Atari 7800 | Core |
| 7501, 8501 | 1984 | 6510 variants with 7 I/O pins and no NMI | Commodore 16, 116, Plus/4 | Core |
| 8500 | 1985 | The 6510 in HMOS | Later Commodore 64s | Core |
| 8502 | 1985 | Runs at twice the 6510's clock | Commodore 128 | Core |
| Ricoh 2A03, 2A07 | 1983 | Decimal mode removed, sound unit built in. 2A03 for NTSC, 2A07 for PAL | NES, Famicom | Core, no decimal |
| 6591, 6592 | | A whole Atari 2600 system on one chip | Atari 2600 designs | Core |
| CM630 | | Eastern Bloc 1 MHz clone | Pravetz 8A and 8C, Apple II clones | Core |

### CMOS: the 65C02 and its relatives

| Chip | Year | What is different | Used in | Core |
|---|---|---|---|---|
| 65C02 (WDC), R65C02 (Rockwell), Synertek, GTE G65SC02 | | CMOS, lower power, faster, new instructions, NMOS quirks fixed | Apple IIc, enhanced Apple IIe, Laser 128 | 65C02 |
| G65SC12 | | A 65C02 without the bit-manipulation instructions | BBC Master | 65C02 |
| G65SC102 | | 4 MHz, different pinout | BBC Master Turbo co-processor | 65C02 |
| VL65NC02 | | 65C02 core inside Atari's Mikey chip | Atari Lynx | 65C02 |
| KS5360 | | 65C02 system chip | Watara Supervision | 65C02 |
| HuC6280 | 1987 | Hudson's 65C02 with extra instructions, 7.16 MHz | PC Engine, TurboGrafx-16 | Other |
| Rockwell R6500/11, /12, /15, R6511Q | | One-chip computers: bit instructions, on-chip RAM, a UART | Industrial control | Other |
| Rockwell R65F11, R65F12 | 1983 | As above, with a Forth kernel in ROM | Industrial control | Other |
| Rockwell R65C00, R65C21, R65C29 | | Two 6502s on one chip | Industrial control | Other |
| WDC W65C02S | | The 65C02 still in production today | Industrial and embedded kit, and the modern machines below | 65C02 |

### Beyond 8 bits and beyond the instruction set

| Chip | Year | What is different | Used in | Core |
|---|---|---|---|---|
| 65CE02 | | A third index register, Z, and a 16-bit stack | Commodore Semiconductor Group designs | Other |
| 4510 | 1991 | A 65CE02 with memory mapping and I/O on the chip | Commodore 65 prototype | Other |
| 45GS02 | | 65CE02 descendant with a 32-bit Q register, 40 MHz, built in an FPGA | MEGA65 | Other |
| 65C816 | | 16-bit, with a 6502 emulation mode | Apple IIGS, SNES (as Ricoh's 5A22) | Other |
| 65C802 | | A 65C816 in a 6502's 40-pin socket | Little | Other |

## Computers

### Single-board computers and kits

| Machine | Year | CPU | Core |
|---|---|---|---|
| **KIM-1** | 1976 | 6502, 1 MHz | Core |
| Synertek SYM-1 (first sold as the VIM-1) | 1978 | 6502 | Core |
| Rockwell AIM-65 | 1978 | 6502 | Core |
| Acorn System 1 | 1979 | 6502 | Core |
| Elektor Junior Computer | 1980 | 6502 | Core |

The KIM-1 was MOS Technology's own board, sold to show engineers the 6502:
1 KB of RAM, two 6530 chips (each a ROM, a little RAM, two I/O ports and a
timer), a 24-key keypad and six seven-segment LED digits, for $245. *Microchess*
by Peter Jennings, sold for it, is probably the first game for a microcomputer
to be sold commercially. It is the first machine this project builds.

### Apple

| Machine | Year | CPU | Core |
|---|---|---|---|
| Apple I | 1976 | 6502 | Core |
| Apple II | 1977 | 6502 | Core |
| Apple II Plus | 1979 | 6502 | Core |
| Apple III | 1980 | 6502A | Core |
| Apple IIe | 1983 | 6502 | Core |
| Apple IIc | 1984 | 65C02 | 65C02 |
| Apple IIe, enhanced | 1985 | 65C02 | 65C02 |
| Apple IIGS | 1986 | 65C816 | Other |
| Apple IIc Plus | 1988 | 65C02, 4 MHz | 65C02 |

**Apple II clones:** the Franklin Ace (6502, Core), the Laser 128 (65C02), and
Bulgaria's Pravetz 8A and 8C (CM630, Core).

### Commodore

| Machine | Year | CPU | Core |
|---|---|---|---|
| PET | 1977 | 6502 | Core |
| VIC-20 | 1980 | 6502 | Core |
| Commodore 64 | 1982 | 6510, later 8500 | Core |
| MAX Machine (Japan only) | 1982 | 6510 | Core |
| CBM-II | 1982 | 6509 | Core |
| Commodore 16, 116, Plus/4 | 1984 | 7501, 8501 | Core |
| Commodore 128 | 1985 | 8502, with a Z80 alongside | Core |
| Commodore 65 (prototype, never sold) | 1991 | 4510 | Other |

The Salora Manager was a VIC-20 sold under a Finnish brand. Every Commodore
floppy drive for the 8-bit line is a 6502 computer in its own right, the 1541
included, and the 8-inch drives for the PET had two.

### Atari 8-bit

| Machine | Year | CPU | Core |
|---|---|---|---|
| Atari 400, 800 | 1979 | 6502C "Sally" | Core |
| XL series | 1983 | 6502C "Sally" | Core |
| XE series | 1985 | 6502C "Sally" | Core |
| XE Game System | 1987 | 6502C "Sally" | Core |

### Acorn

| Machine | Year | CPU | Core |
|---|---|---|---|
| Atom | 1980 | 6502 | Core |
| **BBC Micro** | 1981 | 6502, 2 MHz | Core |
| Electron | 1983 | 6502 | Core |
| BBC Micro B+ | 1985 | 6512 | Core |
| BBC Master | 1986 | G65SC12 | 65C02 |

The BBC Micro's 6502 second processor, attached through Acorn's Tube
interface, is a 65C02 at 3 MHz. The BBC Micro Model B is the second machine
this project builds.

### Others

| Machine | Year | CPU | Core |
|---|---|---|---|
| Ohio Scientific Challenger range | 1977 | 6502 | Core |
| Compukit UK101 | 1979 | 6502 | Core |
| Tangerine Microtan 65 | 1979 | 6502 | Core |
| Oric-1 | 1983 | 6502A | Core |
| Oric Atmos | 1984 | 6502A | Core |

## Consoles and handhelds

| Machine | Year | CPU | Core |
|---|---|---|---|
| Atari 2600 | 1977 | 6507 | Core |
| Atari 5200 | 1982 | 6502C "Sally" | Core |
| **NES, Famicom** | 1983 | Ricoh 2A03 (NTSC), 2A07 (PAL) | Core, no decimal |
| Atari 7800 | 1986 | 6502C "Sally" | Core |
| PC Engine, TurboGrafx-16 | 1987 | HuC6280 | Other |
| Atari Lynx | 1989 | VL65NC02 | 65C02 |
| SNES, Super Famicom | 1990 | Ricoh 5A22 (65C816) | Other |
| Watara Supervision | 1992 | KS5360 (65C02) | 65C02 |

Famicom clones, such as the Dendy that was widely sold in Russia from 1992,
and the NES-on-a-chip plug-and-play TV games carry 2A03-compatible CPUs. The
NES is the third machine this project builds.

## Arcade

Most arcade games of the time ran on a Z80. Atari was the 6502's great
exception in the arcade.

| Game | Year | Notes |
|---|---|---|
| Lunar Lander | 1979 | Atari's first vector game, on its 6502 vector hardware |
| Asteroids | 1979 | 6502 with Atari's vector generator. The sound is hand-built circuitry, not a sound chip |
| Asteroids Deluxe | 1980 | Same hardware family |
| Battlezone | 1980 | 6502 at 1.5 MHz, plus a bit-slice "math box" for the 3D transforms and POKEY chips for sound |
| Missile Command | 1980 | 6502 and a POKEY |
| Centipede | 1981 | 6502 |
| Tempest | 1981 | 6502 at 1.5 MHz, two POKEYs, Atari's vector generator |

Later arcade boards kept a 6502 for smaller jobs: *Paperboy* uses one to run
the sound and the coin slots. Atari's original source code for many of these
games was published on GitHub in October 2021.

## Peripherals and other hardware

- **Disk drives.** Commodore's 8-bit drives (6502), and Atari's 810 and 1050
  (6507).
- **Accelerators and co-processors.** The TurboMaster for the Commodore 64
  (65C02 at 4.09 MHz); the BBC Micro's 6502 second processor (65C02 at 3 MHz).
- **Chess computers.** Many dedicated chess computers ran 65C02s at 4 to
  20 MHz.

## Built today

The 6502 never stopped. WDC still makes the W65C02S, and people still design
new machines around it.

| Machine | CPU | Notes |
|---|---|---|
| Commander X16 (2023) | WDC 65C02S, 8 MHz | Commodore-style KERNAL, video in an FPGA because nobody makes a suitable video chip any more |
| Olimex Neo6502 | W65C02, 6.25 MHz | An RP2040 alongside handles memory, HDMI video and sprites |
| Ben Eater's 6502 kit | W65C02 and W65C22 | A breadboard computer built along with his video series |
| Replica 1 | 65C02 | A modern replica of the Apple I |
| MEGA65 | 45GS02 in an FPGA, 40 MHz | Compatible with the Commodore 65, which Commodore never released |

## On screen

- **The Terminator (1984).** Some shots from the T-800's point of view scroll
  6502 assembly down the left of the screen. It is Apple II code: a type-in
  program from *Nibble* magazine, and part of the Apple II DOS 3.3 RAM disk
  driver.
- **Futurama (1999).** In "Fry and the Slurm Factory", an X-ray of Bender's
  head shows a chip labelled "6502". Executive producer David X. Cohen had
  programmed games on an Apple II Plus at school.

## What this means for this project

- The three machines planned, the KIM-1, the BBC Micro Model B and the NES,
  all use a CPU the core implements, the NES with decimal mode removed. Which
  are built is tracked in `machines/registry.json`.
- Nearly every 6502 machine from 1976 to 1985 uses one too.
- The core also covers the 65C02, which opens the BBC Master, the later Apple
  IIs, the Atari Lynx, the Watara Supervision and every modern machine above
  except the MEGA65, whose 45GS02 is a different CPU.
- The 65C816, the HuC6280 and the 4510 family are different CPUs, and out of
  scope.

## Sources

- [MOS Technology 6502](https://en.wikipedia.org/wiki/MOS_Technology_6502), Wikipedia: launch, price, variants and uses
- [WDC 65C02](https://en.wikipedia.org/wiki/WDC_65C02), Wikipedia: 65C02 machines, differences and current production
- [Chuck Peddle](https://en.wikipedia.org/wiki/Chuck_Peddle) and [Western Design Center](https://en.wikipedia.org/wiki/Western_Design_Center), Wikipedia: who made it
- [KIM-1](https://en.wikipedia.org/wiki/KIM-1), [SYM-1](https://en.wikipedia.org/wiki/SYM-1) and [AIM-65](https://en.wikipedia.org/wiki/AIM-65), Wikipedia. Sources disagree on both boards' years; 1978 is the best supported
- [6502-based home computers](https://en.wikipedia.org/wiki/Category:6502-based_home_computers), Wikipedia category
- [Atari 6502 Vector hardware](https://www.system16.com/hardware.php?id=759), System 16
- [6502-based video arcade source listings](https://6502disassembly.com/other-va.html) and the [Battlezone disassembly](https://6502disassembly.com/va-battlezone/), 6502disassembly.com
- [Commander X16](https://www.c64-wiki.com/wiki/Commander_X16), C64-Wiki
- [Neo6502](https://www.olimex.com/Products/Retro-Computers/Neo6502pc/open-source-hardware), Olimex
- [6502 computer kit](https://shop.eater.net/products/6502-computer-kit), Ben Eater
- [MEGA65 CPU chapter](https://github.com/MEGA65/mega65-user-guide/blob/master/cpu.tex), MEGA65 user guide
- [The 6502 in "The Terminator"](https://www.pagetable.com/?p=64), pagetable.com, and [Analyzing the code from the Terminator's HUD](https://hackaday.com/2024/04/15/analyzing-the-code-from-the-terminators-hud/), Hackaday
- [65 reasons to celebrate the 6502](https://thechipletter.substack.com/p/65-reasons-to-celebrate-the-6502), The Chip Letter
