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

VRAM `$2000` = `$11`, `$2001` = `$22`, buffer = `$00`, increment 1, rendering
off. Write `$20` then `$00` to `$2006` (`v` = `$2000`). Read `$2007` three
times: `$00` (the old buffer), `$11`, `$22`; `v` ends at `$2003` [from PPU
registers: the read returns the buffer, then refills it from `v`].

Then write `$3F` then `$00` to `$2006` (`v` = `$3F00`), with palette entry
`$3F00` = `$0F` and nametable byte `$2F00` = `$AB`, and read `$2007` once. It
returns `$0F` in bits 5 to 0 and the I/O latch in bits 7 and 6; the latch holds
`$00` from the last write, so the read is `$0F` [from PPU registers]. The buffer
now holds `$AB`: the read "underneath" `$3F00` goes to PPU memory `$3F00`, which
the console's wiring mirrors to `$2F00` [inferring from PPU memory map: `$3000-$3EFF`
is usually a mirror of `$2000-$2EFF`; PPU registers: "usually mirrored
nametables"; the values are an example].

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
  model: picture column `X` is decided on dot `X + 2` (dots 2 to 257), from the
  shifters as they stand before that dot shifts them [from PPU rendering]. The
  timing ROMs of `sprite_hit_tests_2005.10.05` (09, 10 and 11) bound it but do
  not pin it: they pass with the hit decided on any dot from `X + 1` to `X + 4`,
  and from `X + 5` test 10 fails with code 7, "Lower-left corner too late"
  [measured in task 5, 5 October 2026: the hit flag held back by 1 to 5 dots,
  each run through `BlarggRunner.RunScreenReporting`]. A dot earlier than
  `X + 1` was not tried.
- **The PPU address bus.** During rendering it carries the fetch addresses
  above; in VBlank or with rendering off it carries `v` [from PPU rendering]. This
  is what MMC3 watches (`mappers.md` 6). For the model (task 11): the board is
  told of every pattern fetch and of each sprite slot's first garbage nametable
  fetch, and, outside rendering only, of `v` whenever a `$2006` write or a
  `$2007` access moves it, nametable and palette addresses included (`$3F00`
  has A12 set); during rendering such an access is not told, as the bus is
  carrying the fetches. The
  background's nametable and attribute fetches are not told, though their A12
  is always 0. Between two pattern fetches they are a 4-dot low, under MMC3's
  filter either way. From dot 337 to the next line's dot 4 they are a 9-dot
  low, which is exactly 3 CPU cycles on NTSC: told, it would clock the counter
  a second time each line with the background at `$1000`, where `mappers.md` 6
  says it clocks once. Whether the real filter takes that 9-dot low is not on
  the sheet (`mappers.md` open item 9) [inferring].

### Worked example 4: the first fetches of a line

`v` = `$0000` (fine Y 0, nametable 0, coarse Y 0, coarse X 0; in the rendering
layout of section 2, the `$2000` of the tile address comes from the fetch rule,
not from `v`), rendering on, PPUCTRL = `$00`, nametable byte `$2000` = `$24`.
Dots 321 to 328 of the pre-render line fetch:

| Dots | Fetch | Address | Why |
|---|---|---|---|
| 321, 322 | nametable | `$2000` | `$2000 \| (v & $0FFF)` = `$2000 \| $000` |
| 323, 324 | attribute | `$23C0` | `$23C0 \| (v & $0C00) \| ((v >> 4) & $38) \| ((v >> 2) & $07)` with every field 0 |
| 325, 326 | pattern low | `$0240` | table `$0000` + tile `$24` x 16 + fine Y 0 |
| 327, 328 | pattern high | `$0248` | the low address + 8 |

Dot 328 then increments coarse X, so `v` = `$0001` [inferring from the tables
above and the fetch rules in section 2].

With `v` = `$2000` instead, the fetch addresses for the nametable and attribute
are the same (bits 14 to 12 are not part of either), but `v` = `$2000` is fine Y
= 2, so the pattern bytes come from `$0242` and `$024A` [inferring from the
same rules]. A test taken from this example should check both.

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

Sprite 0 at Y = 30, X = 40, its tile's pattern bytes all `$FF` (every pixel
opaque, colour 3), 8x8 sprites, background opaque everywhere, PPUMASK = `$1E`.
Sprite 0 is drawn on lines 31 to 38 and columns 40 to 47, so the hit comes on
line 31 at column 40 [inferring from sections 4, 6 and 8].

- X = 255: the sprite covers only column 255, and there is no hit at 255, so no
  hit [from PPU OAM].
- PPUMASK = `$18` (both layers on, both left columns hidden) and X = 4: the
  sprite covers columns 4 to 11. Columns 4 to 7 are clipped, so the hit comes at
  column 8 on line 31 [from PPU OAM: no hit at X 0 to 7 while clipping is on;
  inferring the column].
- PPUMASK = `$18` and X = 0: the sprite covers columns 0 to 7, all clipped, so no
  hit on that line [from PPU OAM].

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
  the AND too [from PPU registers]. NTSC video says "all colors between `$x1-$xD`
  are treated as `$x0`", which does not say what happens to `$xE` and `$xF`;
  the AND makes them `$x0` too. The model follows PPU registers [noting a
  difference between two pages; no pinned test checks it].
- **Emphasis** (PPUMASK bits 7 to 5) darkens the other two components; all
  three dims every colour [from PPU registers]. The 2C02 order is blue (bit 7),
  green (bit 6), red (bit 5); **the 2C07 swaps red and green**: blue (7), red
  (6), green (5) [from PPU registers; Cycle reference chart]. **How much:** each
  bit attenuates the signal during the 6 of the 12 colour phases in which one
  hue's wave is high, bit 5 hue `$C`, bit 6 hue `$4`, bit 7 hue `$8`, so the
  picture tints towards the opposite hue, red, green or blue; one shared
  attenuator, active for 6, 10 or 12 of the 12 phases with one, two or three bits
  set. Hues `$E` and `$F` are not affected; `$D` is. The attenuated voltages are
  in section 11, on average 0.816328 of the plain ones [from NTSC video].
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
tables generated with the Pally tool [from PPU palettes]. Their licence is not
stated: no page read gives terms for the wiki's content, and the one licence on
NTSC video covers its example programs, not the tables (Creative Commons
Attribution-ShareAlike 4.0) [from NTSC video]. So the model does not use them.
`PpuPalette` **computes** its 64 colours, under each of the 8 emphasis
settings, from the signal the NTSC video page describes.

**The signal** [from NTSC video, revision 24244, the terminated measurements]:

| Level | Low | High | Low, attenuated | High, attenuated |
|---|---|---|---|---|
| 0 | 0.228 V | 0.616 V | 0.192 V | 0.500 V |
| 1 | 0.312 V | 0.840 V | 0.256 V | 0.676 V |
| 2 | 0.552 V | 1.100 V | 0.448 V | 0.896 V |
| 3 | 0.880 V | 1.100 V | 0.712 V | 0.896 V |

- A colour `$LH` swings between the low and high voltage of level `L` as a
  square wave of 12 phases; hue `H` is high in the phases `p` where
  `(H + p) mod 12 < 6`. Hue 0 is high all the time, hues `$D` to `$F` low all
  the time, and `$E` and `$F` are level 1 whatever `L` says, so they are the
  same voltage as `$1D` [from NTSC video].
- Emphasis attenuates in the phases of section 10, and not for hues `$E` and
  `$F` [from NTSC video].
- `$0F`, the blanking level, is the same voltage as `$1D` [from NTSC video].

**The decode** [from NTSC video, "Composite decoding" and its example program,
restated, not copied]:

1. Scale each of the 12 samples so that `$1D` (0.312 V, black) is 0 and `$20`
   (1.100 V) is 1. The page allows either black point (with or without the
   7.5 IRE setup) and either white point; this choice takes black without the
   setup and `$20` for white, as its example program does.
2. Y is the mean of the 12. U and V are twice the mean of each sample times the
   sine, and the cosine, of the subcarrier at that sample, at angle
   `pi (p + 2.5) / 6`. The 2 is the page's saturation correction. The angle puts
   hue 8 on the colour burst, which the page gives as pure -U; it is the page's
   example program's `p + 3 - 0.5` [inferring: the centre of hue 8's six high
   phases is at `p = 6.5`, and `pi (6.5 + 2.5) / 6` is 270 degrees, pure -U].
3. R = Y + 1.139883 V, G = Y - 0.394642 U - 0.580622 V, B = Y + 2.032062 U,
   each clipped to 0 to 1, times 255, rounded [from NTSC video].

The page's own program decodes a whole line, so colours bleed into their
neighbours; the model decodes each colour alone, from one whole colour cycle,
so it has no artefacts. Neither the differential phase distortion the page
describes (the hues of brighter rows turned by about 2.5 to 5 degrees a row) nor
a television's filtering is modelled. PAL uses the same table, with the 2C07's
emphasis bits swapped (section 10); the 2C07's own decode, about 15 degrees of
hue apart, is a known difference.

**What it was checked against.** The prose of NTSC video and the rules of
section 10, never a table: hue 8 decodes as pure -U, the colour burst's phase;
each hue on turns the chroma by 30 degrees, one phase of the twelve; the hues of
a row share one luma and one saturation ("exactly the same luminosity; only the
chroma phase differs"); each row's luma is above the row below; hue 0 and `$D`
are greys with no chroma, `$E` and `$F` black, `$20` and `$30` white, `$0D`
blacker than black; greyscale ANDs with `$30`; each emphasis bit darkens the
other two channels [from NTSC video; section 10]. `PpuPaletteTests` checks each,
on the decoded Y, U and V where the rule is about the signal.

**The tables taken out.** Task 1 copied the wiki's `2C02G_U_wiki` and
`2C07_wiki` tables into this section. Their licence is not stated, so task 5
removed them, and a later review removed the last row kept for a test. They
remain in the git history, in commit `8a11beb`; whether to rewrite that history
is Dan's decision. Before they were removed, a throwaway script compared all 64
computed colours with the 2C02G table on 5 October 2026: every hue within 28
degrees, the largest gaps in rows 2 and 3, where Pally's phase distortion turns
the hues most, and the greys brighter by the missing 7.5 IRE setup [measured].

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

1. The dot of each picture column against sprite 0 hit timing (6): bounded in
   task 5, not pinned. The model takes `X + 2` from PPU rendering; the ROMs
   accept `X + 1` to `X + 4`.
2. The rendering-toggle delay of 3 to 4 dots (1) [guessing - verify].
3. The size of the emphasis darkening (10): settled in task 5, from NTSC video.
4. Whether the colour tables may be committed (11): not settled, so they are not
   used, and none of their values is kept; the colours are computed.
5. Greyscale on `$xE` and `$xF` (10): PPU registers and NTSC video read
   differently [guessing - verify].
