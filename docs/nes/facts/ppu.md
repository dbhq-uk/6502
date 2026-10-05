# NES PPU: the 2C02 and the 2C07

Written 5 October 2026 for the NES plan, task 1. Tags and page revisions are in
[`README.md`](README.md). Used by tasks 4 and 5 (the PPU), 6 (the frame buffer)
and 11 (MMC3, which watches the PPU's address line A12). Frame and line timing is
in [`timing.md`](timing.md).

## 1. The registers

The eight registers sit at `$2000-$2007` and repeat every 8 bytes to `$3FFF`
[from PPU registers].

| Address | Name | Bits | Access | Source |
|---|---|---|---|---|
| `$2000` | PPUCTRL | `VPHB SINN`: NMI enable (V), master/slave (P), sprite size (H), background table (B), 8x8 sprite table (S), increment 1 or 32 (I), nametable (NN) | write | [from PPU registers] |
| `$2001` | PPUMASK | `BGRs bMmG`: emphasis (BGR, see 9), sprites on (s), background on (b), sprites in the left 8 pixels (M), background in the left 8 pixels (m), greyscale (G) | write | [from PPU registers] |
| `$2002` | PPUSTATUS | `VSO- ----`: VBlank (V), sprite 0 hit (S), sprite overflow (O); bits 4 to 0 are the I/O latch | read | [from PPU registers] |
| `$2003` | OAMADDR | OAM address | write | [from PPU registers] |
| `$2004` | OAMDATA | OAM data | read, write | [from PPU registers] |
| `$2005` | PPUSCROLL | X then Y, two writes | write x2 | [from PPU registers] |
| `$2006` | PPUADDR | high then low byte, two writes | write x2 | [from PPU registers] |
| `$2007` | PPUDATA | VRAM data | read, write | [from PPU registers] |

Effects of each access:

- **`$2000` write.** Sets the NN bits of `t` (section 2). Writing bit 7 from 0
  to 1 while the VBlank flag is set raises NMI at once [from PPU registers;
  NMI]. Bit 6 should never be set on a stock console [from PPU registers].
- **`$2001` write.** Rendering is on while either bit 3 or bit 4 is set; with
  only one set the other layer is transparent [from PPU registers]. Toggling
  rendering takes effect about 3 to 4 dots after the write [from PPU registers]
  [guessing - verify: the model may apply it at once until a test needs the
  delay].
- **`$2002` read.** Returns V, S and O in bits 7 to 5 and the I/O latch in bits
  4 to 0, then clears the VBlank flag and `w` [from PPU registers; PPU
  scrolling; Open bus behavior: the status read loads bits 7 to 5 onto the latch
  and leaves the rest]. Writing `$2002` does not change the VBlank flag [from the
  fork: ppu_vbl_nmi/readme.txt, test 1 item 4].
- **`$2003` write.** Sets OAMADDR [from PPU registers].
- **`$2004` write.** Writes OAM at OAMADDR and adds 1 to OAMADDR. During
  rendering (pre-render and visible lines with rendering on) it does not write
  OAM and bumps only the high 6 bits of OAMADDR [from PPU registers].
- **`$2004` read.** Returns OAM at OAMADDR without changing it. On dots 1 to 64
  of a visible line, while secondary OAM is cleared, it returns `$FF`; during the
  rest of rendering it shows the evaluation's own accesses [from PPU registers;
  PPU sprite evaluation].
- **`$2005` and `$2006` writes.** Section 2.
- **`$2007` read or write.** Section 3. After the access `v` goes up by 1 or 32
  (PPUCTRL bit 2) outside rendering; during rendering it does a coarse X and a Y
  increment instead [from PPU scrolling].

**The I/O latch.** A write to any PPU register, including `$2002`, fills an
8-bit latch. A read of `$2002`, `$2004` or `$2007` fills it with the bits read
(`$2002` only bits 7 to 5). A read of a write-only register returns the latch.
Bits decay after 3 to 30 ms [from PPU registers; Open bus behavior]. The test ROM
`ppu_open_bus` checks this [from Emulator tests].

**Writes ignored after reset.** `$2000`, `$2001`, `$2005` and `$2006` ignore
writes until the end of the first VBlank, about 29658 cycles NTSC and 33132 PAL
[from PPU power up state; PPU registers]. See `timing.md` section 4.

## 2. The scroll registers `v`, `t`, `x`, `w`

| Register | Width | What it is | Source |
|---|---|---|---|
| `v` | 15 bits | The current VRAM address; during rendering, the scroll position | [from PPU scrolling] |
| `t` | 15 bits | The address or scroll waiting to go into `v` | [from PPU scrolling] |
| `x` | 3 bits | Fine X scroll | [from PPU scrolling] |
| `w` | 1 bit | First or second write of `$2005`/`$2006` | [from PPU scrolling] |

During rendering `v` and `t` are laid out as `yyy NN YYYYY XXXXX`: fine Y (bits
14 to 12), nametable (11 to 10), coarse Y (9 to 5), coarse X (4 to 0) [from PPU
scrolling].

| Access | Effect | Source |
|---|---|---|
| `$2000` write | `t` bits 11 to 10 = data bits 1 to 0 | [from PPU scrolling] |
| `$2002` read | `w` = 0 | [from PPU scrolling] |
| `$2005` write, `w` = 0 | `t` bits 4 to 0 = data bits 7 to 3; `x` = data bits 2 to 0; `w` = 1 | [from PPU scrolling] |
| `$2005` write, `w` = 1 | `t` bits 14 to 12 = data bits 2 to 0; `t` bits 9 to 5 = data bits 7 to 3; `w` = 0 | [from PPU scrolling] |
| `$2006` write, `w` = 0 | `t` bits 13 to 8 = data bits 5 to 0; `t` bit 14 = 0; `w` = 1 | [from PPU scrolling; PPU registers] |
| `$2006` write, `w` = 1 | `t` bits 7 to 0 = data; `v` = `t` (1 to 1.5 dots after the write); `w` = 0 | [from PPU scrolling] |

During rendering (rendering on, on the pre-render line and lines 0 to 239):

| When | What | Source |
|---|---|---|
| Dots 8, 16, ... 256, then 328 and 336 | Coarse X increment of `v` | [from PPU scrolling] |
| Dot 256 | Y increment of `v` | [from PPU scrolling] |
| Dot 257 | `v` bits 10 and 4 to 0 = `t`'s (horizontal) | [from PPU scrolling] |
| Dots 280 to 304 of the pre-render line | `v` bits 14 to 11 and 9 to 5 = `t`'s (vertical), every dot | [from PPU scrolling] |

- **Coarse X increment:** if coarse X is 31, set it to 0 and flip bit 10;
  otherwise add 1 [from PPU scrolling].
- **Y increment:** if fine Y is under 7, add 1 to fine Y. Otherwise fine Y = 0
  and: coarse Y 29 becomes 0 and flips bit 11; coarse Y 31 becomes 0 without the
  flip; any other coarse Y adds 1 [from PPU scrolling].
- **Fetch addresses:** tile = `$2000 | (v & $0FFF)`; attribute = `$23C0 | (v &
  $0C00) | ((v >> 4) & $38) | ((v >> 2) & $07)` [from PPU scrolling].
- A `$2007` access during rendering does a coarse X and a Y increment together,
  whatever PPUCTRL bit 2 says [from PPU scrolling].

### Worked example 1: the register writes on PPU scrolling

Start with `t`, `v`, `x` and `w` all 0 [from PPU scrolling, its summary table].

| Access | `t` after | `v` after | `x` | `w` |
|---|---|---|---|---|
| `$2000` = `$00` | `$0000` | `$0000` | 0 | 0 |
| `$2002` read | `$0000` | `$0000` | 0 | 0 |
| `$2005` = `$7D` | `$000F` | `$0000` | 5 | 1 |
| `$2005` = `$5E` | `$616F` | `$0000` | 5 | 0 |
| `$2006` = `$3D` | `$3D6F` | `$0000` | 5 | 1 |
| `$2006` = `$F0` | `$3DF0` | `$3DF0` | 5 | 0 |

The page shows these as bit strings; the hex is [inferring, converted].

### Worked example 2: the Y increment

| `v` before | Fine Y, coarse Y | `v` after |
|---|---|---|
| `$0000` | 0, 0 | `$1000` |
| `$73A0` | 7, 29 | `$0800` (coarse Y 0, bit 11 flipped) |
| `$73E0` | 7, 31 | `$0000` (no flip) |

[inferring from the rule on PPU scrolling]

## 3. VRAM, palette RAM and `$2007`

| PPU address | What | Mapped by | Source |
|---|---|---|---|
| `$0000-$1FFF` | Pattern tables 0 and 1 (CHR) | cartridge | [from PPU memory map] |
| `$2000-$2FFF` | Four nametables of `$400`, each 960 tile bytes and 64 attribute bytes; the console's 2 KB, arranged by the cartridge (`mappers.md` 1) | cartridge | [from PPU memory map; PPU nametables] |
| `$3000-$3EFF` | Usually a mirror of `$2000-$2EFF` | cartridge | [from PPU memory map] |
| `$3F00-$3F1F` | Palette RAM | inside the PPU | [from PPU memory map] |
| `$3F20-$3FFF` | Mirrors of `$3F00-$3F1F` | inside the PPU | [from PPU memory map] |

- The PPU address space is 14 bits; `v` bit 14 is not used for `$2007` [from PPU
  scrolling; PPU registers].
- **Palette mirrors.** Entry 0 of each palette is shared between the background
  and sprite sets, so `$3F10`, `$3F14`, `$3F18` and `$3F1C` are `$3F00`,
  `$3F04`, `$3F08` and `$3F0C` [from PPU palettes]. Palette entries are 6 bits
  [from PPU palettes].
- **The read buffer.** A `$2007` read returns the buffer, then fills the buffer
  from `v`, so reads lag by one [from PPU registers]. The buffer changes only on
  `$2007` reads [from PPU registers].
- **Palette reads** return the palette entry at once, with the I/O latch in bits
  7 and 6 and greyscale applied; the buffer is filled with the byte "underneath"
  in VRAM [from PPU registers]. The 2C02G, 2C02H and the PAL PPUs do this
  [from PPU registers].

### Worked example 3: the read buffer

VRAM `$2000` = `$11`, `$2001` = `$22`, buffer = `$00`, increment 1. Write `$20`
then `$00` to `$2006`. Read `$2007` three times: `$00`, `$11`, `$22`. Then set
`$3F00` (holding `$0F`, with `$2F00` holding `$AB` underneath) and read once:
`$0F` with bits 7 and 6 from the latch, and the buffer now holds `$AB` [from PPU
registers; the values are an example].

## 4. OAM

- 256 bytes, 64 sprites of 4: Y, tile, attributes, X [from PPU OAM; PPU memory
  map].
- **Y** is the line above the sprite's top: a sprite with Y = 10 is drawn from
  line 11. Y of `$EF` to `$FF` hides it [from PPU OAM].
- **Tile.** 8x8: tile in the table PPUCTRL bit 3 picks. 8x16: bit 0 picks the
  table (`$0000` or `$1000`), bits 7 to 1 the top tile, and the bottom tile is the
  next [from PPU OAM].
- **Attributes.** Bits 1 to 0 palette 4 to 7, bit 5 behind the background, bit 6
  flip horizontal, bit 7 flip vertical. Bits 4 to 2 do not exist and read 0, so
  byte 2 reads back ANDed with `$E3` [from PPU OAM]. In 8x16 a vertical flip
  also swaps the two tiles [from PPU OAM].
- **X** is the left edge; a sprite cannot hang off the left [from PPU OAM].
- **OAMADDR during rendering** is set to 0 on each of dots 257 to 320 of the
  pre-render and visible lines [from PPU registers].
- **The 2C07** refreshes OAM itself on lines 265 to 310, starting 24 lines after
  NMI, so OAM can be written only in the first 24 lines of VBlank. Its sprite
  evaluation cannot be fully turned off [from PPU OAM; PPU registers; Cycle
  reference chart].
- OAM is DRAM and decays if not refreshed; most emulators do not model this
  [from PPU OAM]. The 2C02G corrupts OAM on some OAMADDR writes [from PPU
  registers]. Both are left out of the model [inferring from "Most emulators do
  not simulate the decay, and suffer no compatibility problems"].

## 5. VBlank and NMI

- The VBlank flag is set at line 241 dot 1 and cleared at dot 1 of the
  pre-render line, together with sprite 0 hit and overflow [from PPU registers;
  NMI; PPU rendering]. Line numbers per region are in `timing.md` section 2.
- `/NMI` is low exactly while the VBlank flag and PPUCTRL bit 7 are both set
  [from NMI]. So toggling bit 7 during VBlank without reading `$2002` gives
  several NMIs [from NMI].
- The read races are in `bus.md` section 2 [from PPU frame timing].
- `ppu_vbl_nmi` test 5 prints the instruction after which the NMI is taken for
  each dot offset, test 7 when enabling NMI near the clear time still gives one,
  test 8 when disabling it near the set time still gives one [from the fork:
  ppu_vbl_nmi/readme.txt].

## 6. The background pipeline, dot by dot

On every visible line (0 to 239) and the pre-render line, with rendering on
[from PPU rendering]:

| Dots | What | Source |
|---|---|---|
| 0 | Idle | [from PPU rendering] |
| 1 to 256 | 32 tiles fetched, 8 dots each: nametable byte (dots 1, 2), attribute byte (3, 4), pattern low (5, 6), pattern high (7, 8). Each fetch puts the address out on its first dot and reads on its second | [from PPU rendering] |
| 9, 17, 25, ... 257 | The fetched tile goes into the high 8 bits of the two 16-bit pattern shifters, and its 2 attribute bits into the attribute latches | [from PPU rendering] |
| 2 to 257, 322 to 337 | Shifters shift once a dot [inferring from PPU rendering: "the same cycle that the shifters shift for the first time" is dot 2, and reloads at 9, 17 ... 257] | |
| 257 to 320 | Sprite fetches (section 7) | [from PPU rendering] |
| 321 to 336 | The first two tiles of the next line, same 8-dot pattern | [from PPU rendering] |
| 337 to 340 | Two nametable fetches of the next tile 3, unused | [from PPU rendering] |

- The pixel is chosen by `x` from the top of the shifters [from PPU rendering].
- The background pattern address is `table + tile x 16 + fine Y` (low plane),
  and `+ 8` for the high plane, where `table` is `$1000` if PPUCTRL bit 4 is set
  [from PPU pattern tables].
- The attribute byte gives a 2-bit palette for each 16x16 quadrant: bits 1 to 0
  top left, 3 to 2 top right, 5 to 4 bottom left, 7 to 6 bottom right [from PPU
  attribute tables]. The quadrant is picked by coarse X bit 1 and coarse Y bit 1
  [from PPU rendering].
- **Pixel output.** "Sprite 0 hit acts as if the image starts at cycle 2", and
  the first pixel leaves the chip during dot 4 [from PPU rendering]. For the
  model: picture column `X` is decided on dot `X + 1` (dots 1 to 256)
  [guessing - verify: the `sprite_hit_tests_2005.10.05` timing tests settle the
  dot].
- **The PPU address bus.** During rendering it carries the fetch addresses
  above; in VBlank or with rendering off it carries `v` [from PPU rendering]. This
  is what MMC3 watches (`mappers.md` 6).

### Worked example 4: the first fetches of a line

`v` = `$2000`, rendering on, PPUCTRL = `$00`, nametable `$2000` holds tile `$24`.
On the pre-render line, dots 321 to 328 fetch the nametable byte from `$2000`
(tile `$24`), the attribute from `$23C0`, then pattern bytes from `$0240` and
`$0248`; dot 328 increments `v` to `$2001` [inferring from the tables above and
the fetch addresses in section 2].

## 7. Sprites

**Evaluation** (lines 0 to 239 only, never the pre-render line) [from PPU sprite
evaluation]:

| Dots | What | Source |
|---|---|---|
| 1 to 64 | Secondary OAM (32 bytes) is filled with `$FF` | [from PPU sprite evaluation] |
| 65 to 256 | Primary OAM is read on odd dots and written to secondary OAM on even dots; the first 8 sprites in range for the next line are copied | [from PPU sprite evaluation] |
| 257 to 320 | 8 sprites x 8 dots: garbage nametable, garbage nametable, pattern low, pattern high; X and attributes load during the second garbage fetch | [from PPU rendering; PPU sprite evaluation] |
| 321 to 340 | Background fetches | [from PPU sprite evaluation] |

- A sprite is in range on line `L` when `0 <= L - Y < height` (8 or 16), and it
  is drawn on the line after [inferring from PPU OAM: "delayed by one
  scanline"; PPU sprite evaluation: "sprites ... for the next scanline"].
- Empty slots fetch tile `$FF` [from PPU rendering]. In 8x16 mode that fetch is
  at `$1FE0-$1FFF` [from MMC3]. MMC3's counter depends on these fetches.
- Evaluation runs if either layer is enabled [from PPU sprite evaluation].
- **Sprite pattern addresses.** 8x8: `table + tile x 16 + row` (+8 high), where
  `table` is PPUCTRL bit 3, and `row` is `7 - row` when flipped vertically. 8x16:
  `table = tile & 1`, top tile `tile & $FE`, bottom `tile | 1`, rows 0 to 15
  with a vertical flip swapping the halves [from PPU OAM; PPU pattern tables;
  inferring the flipped row].
- **Priority.** The lowest-numbered sprite with an opaque pixel wins among
  sprites, whatever its priority bit; then that pixel is drawn in front of the
  background if its priority is 0 or the background is transparent [from PPU
  sprite priority; PPU rendering]. So a back-priority sprite at a low index can
  hide a front-priority sprite behind it [from PPU sprite priority].
- **The multiplexer** [from PPU rendering]:

| Background | Sprite | Priority | Shows |
|---|---|---|---|
| 0 | 0 | any | backdrop `$3F00` |
| 0 | 1 to 3 | any | sprite |
| 1 to 3 | 0 | any | background |
| 1 to 3 | 1 to 3 | 0 | sprite |
| 1 to 3 | 1 to 3 | 1 | background |

- PPUMASK bits 1 and 2 hide the background and sprites in columns 0 to 7 [from
  PPU registers].

## 8. Sprite 0 hit

Set when an opaque pixel of sprite 0 is drawn over an opaque background pixel,
whatever the priority and the colours [from PPU OAM; PPU registers]. Not set
[from PPU OAM]:

- with either the background or sprites off;
- at X 0 to 7 when either left-column bit (PPUMASK bit 1 or 2) is 0;
- at X = 255;
- if it has already been set this frame (it clears at dot 1 of the pre-render
  line).

The PAL border over X 0, 1 and 254 does not stop a hit [from PPU OAM; PPU
registers]. The PPU tracks "sprite 0 is on the next line" and "on this line"
separately [from PPU OAM]. Evaluation can treat another sprite as sprite 0 if
OAMADDR is not 0 at dot 65 [from PPU registers] [guessing - verify: left out
unless a test needs it].

### Worked example 5: a hit

Sprite 0 at Y = 30, X = 40, tile all `$FF` (opaque), background opaque
everywhere, PPUMASK = `$1E`. Sprite 0 is drawn on lines 31 to 38, so the hit
comes on line 31 at column 40 [inferring from sections 4, 6 and 8]. With X = 255
there is no hit [from PPU OAM]; with PPUMASK = `$18` and X = 4 there is no hit
[from PPU OAM: left clipping].

## 9. Sprite overflow and its bug

After 8 sprites are found, evaluation keeps looking for a ninth to set the
overflow flag (`$2002` bit 5), but it checks the wrong bytes [from PPU sprite
evaluation]:

1. With `n` the next sprite and `m` = 0, read `OAM[n][m]` as a Y.
2. In range: set the overflow flag, then read the next 3 bytes, carrying `m`
   into `n`.
3. Not in range: add 1 to `n` **and** to `m` (no carry). If `n` wraps to 0, stop;
   otherwise go to step 1.

So the second and later checks read tile, attribute and X bytes as Y values
[from PPU sprite evaluation]. Both false positives and false negatives follow
[from PPU sprite evaluation].

### Worked example 6: the bug, line 10

Sprites 0 to 7 have Y = 10, so 8 are found. Sprite 8 has Y = `$F0`, not in range:
`n` = 9 and `m` = 1. Sprite 9 has Y = `$F0` and tile `$0A`; the check reads
`OAM[9][1]` = `$0A` as a Y, which is in range for line 10, so the flag is set,
though only 8 sprites are on the line (a false positive). Change sprite 9's tile
to `$80` and its Y to 10, and the ninth sprite is missed (a false negative)
unless a later diagonal byte happens to be in range [inferring from the steps
above].

## 10. Palette, greyscale, emphasis and the 2C07

- **The 5-bit index** into palette RAM is `S AA PP`: sprite or background (S),
  palette (AA), pixel (PP) [from PPU palettes]. A pixel with PP = 0 shows the
  backdrop `$3F00` [from PPU rendering].
- **Colour bytes** are `VV HHHH`: value (brightness) and hue. Hue `$0` is grey,
  `$1` to `$C` the colours, `$D` dark grey, `$E` and `$F` black [from PPU
  palettes]. `$0D` is "blacker than black" and should not be used [from PPU
  palettes].
- **Greyscale** (PPUMASK bit 0) ANDs the colour with `$30`; palette reads show
  the AND too [from PPU registers].
- **Emphasis** (PPUMASK bits 7 to 5) darkens the other two components; all
  three dims every colour [from PPU registers]. The 2C02 order is blue (bit 7),
  green (bit 6), red (bit 5); **the 2C07 swaps red and green**: blue (7), red
  (6), green (5) [from PPU registers; Cycle reference chart]. How much the
  darkening is was not on the pages read [guessing - verify: on the wiki's NTSC
  video page, not yet read].
- **Rendering off.** The picture shows the backdrop; if `v` points into
  `$3F00-$3FFF` it shows that entry instead [from PPU rendering].

**The 2C07 differences** that matter to the model:

| What | 2C02 | 2C07 | Source |
|---|---|---|---|
| Lines | 262 | 312 | [from Cycle reference chart] |
| Odd-frame dot | skipped with rendering on | never | [from Cycle reference chart] |
| Emphasis bits 6 and 5 | green, red | red, green | [from PPU registers; Cycle reference chart] |
| Border | backdrop colour | black, and also over X 0, 1, 254, 255 and line 0 of the picture, with no emphasis or greyscale | [from PPU rendering; Overscan: "leftmost and rightmost 2 pixels and the top scanline"; Cycle reference chart] |
| OAM refresh | none | lines 265 to 310 | [from PPU OAM] |
| OAMADDR write corruption | yes (2C02G) | no | [from PPU registers] |
| Palette read-back | 2C02G and H | yes | [from PPU registers] |
| Colours | NTSC decode | about 15 degrees of hue shift, PAL decode | [from PPU palettes] |
| Pixel aspect | 8:7 | about 1.3862:1 | [from Overscan] |

## 11. The colour table

The PPU makes a composite signal, not RGB, so any colour table is one decode of
it; no single table matches every television [from PPU palettes]. The wiki gives
tables generated with the Pally tool; one is chosen here, and the choice is the
plan's [from PPU palettes].

**2C02, the wiki's `2C02G_U_wiki` table** (Pally v0.23.0, 7.5 IRE setup), as
`RRGGBB` [from PPU palettes, the cell colours of its table]:

| | x0 | x1 | x2 | x3 | x4 | x5 | x6 | x7 | x8 | x9 | xA | xB | xC | xD | xE | xF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `$0x` | `575757` | `000C8E` | `0800A6` | `340096` | `550061` | `630015` | `5A0000` | `3C0E00` | `112800` | `003B00` | `004200` | `003A05` | `002652` | `000000` | `000000` | `000000` |
| `$1x` | `A5A5A5` | `0041D9` | `2F1EFF` | `6704F2` | `9400B4` | `AA0057` | `A31800` | `803900` | `4B5B00` | `137600` | `008100` | `007923` | `006288` | `000000` | `000000` | `000000` |
| `$2x` | `FFFFFF` | `4A9FFF` | `797EFF` | `AF63FF` | `DD55FF` | `F757C2` | `F76A63` | `DC8810` | `AEA900` | `78C400` | `4AD211` | `2FCF64` | `2FBDC4` | `414141` | `000000` | `000000` |
| `$3x` | `FFFFFF` | `B9DDFF` | `CAD1FF` | `DEC6FF` | `F0C0FF` | `FCC0EE` | `FDC6CA` | `F5D0AA` | `E4DD95` | `D0E892` | `BDEEA2` | `B2EEC0` | `B0E8E3` | `B3B3B3` | `000000` | `000000` |

**2C07, the wiki's `2C07_wiki` table** (Pally v0.22.1), as `RRGGBB` [from PPU
palettes, the cell colours of its table]:

| | x0 | x1 | x2 | x3 | x4 | x5 | x6 | x7 | x8 | x9 | xA | xB | xC | xD | xE | xF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `$0x` | `626262` | `002263` | `0D107D` | `2B027D` | `440063` | `530036` | `530502` | `441500` | `2B2700` | `0D3600` | `003E00` | `003D02` | `003336` | `000000` | `000000` | `000000` |
| `$1x` | `ABABAB` | `1251A8` | `3438CB` | `5C24CB` | `7E19A8` | `921B6B` | `922924` | `7E3F00` | `5C5700` | `346B00` | `127600` | `007424` | `00676B` | `000000` | `000000` | `000000` |
| `$2x` | `FFFFFF` | `62A1FA` | `8589FF` | `AC75FF` | `CF6AFA` | `E36CBC` | `E37975` | `CF9037` | `ACA814` | `85BC14` | `62C737` | `4EC575` | `4EB7BC` | `4E4E4E` | `000000` | `000000` |
| `$3x` | `FFFFFF` | `C4DDFF` | `D1D3FF` | `E1CBFF` | `EFC7FF` | `F6C8E7` | `F6CDCB` | `EFD6B3` | `E1DFA6` | `D1E7A6` | `C4EBB3` | `BCEBCB` | `BCE5E7` | `B8B8B8` | `000000` | `000000` |

Whether these 128 numbers may be committed under this repository's licence was
not checked [guessing - verify: the task that builds `PpuPalette` reads the
wiki's licence terms first, and if they do not allow it, generates its own table
from the composite model on the NTSC video page and records that instead].

## 12. Power-up

| Register | At power | After reset | Source |
|---|---|---|---|
| PPUCTRL, PPUMASK | 0 | 0 | [from PPU power up state] |
| PPUSTATUS | `+0+x xxxx` (VBlank often set) | VBlank unchanged | [from PPU power up state] |
| OAMADDR | 0 | unchanged | [from PPU power up state] |
| `w` | 0 | 0 | [from PPU power up state] |
| `t`, `x` | 0 | 0 | [from PPU power up state] |
| `v` | 0 | unchanged | [from PPU power up state] |
| Read buffer | 0 | 0 | [from PPU power up state] |
| Odd frame | no | no | [from PPU power up state] |
| OAM, palette, nametables, CHR RAM | unspecified | unchanged (OAM unspecified) | [from PPU power up state] |

## 13. Open items

1. The dot of each picture column against sprite 0 hit timing (6) [guessing -
   verify].
2. The rendering-toggle delay of 3 to 4 dots (1) [guessing - verify].
3. The size of the emphasis darkening (10) [guessing - verify].
4. Whether the colour tables may be committed (11) [guessing - verify].
