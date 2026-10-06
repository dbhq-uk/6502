# roms

The system ROMs the machines run. Nothing here is ours: each file is somebody's
work, kept beside where it came from and what is known about its rights, because
a test or a page that needs a ROM should never depend on a server that might
disappear. The tests and the site build read these files and check each against
the SHA-256 pinned in `tests/Dbhq.Cpu6502.TestSupport/Pins.cs`, so a changed
file fails on purpose. AGENTS.md rules 3 and 4 say why.

## KIM-1 monitor ROM

| File | SHA-256 | What it is |
|---|---|---|
| `kim-1/6530-002.bin` | `e9e5245854603cdbc0208235310db1bf6e6a75904960385baaddd38fe60ef750` | The contents of the MOS 6530-002 (1 KB), with the unused bytes filled with `$00` |
| `kim-1/6530-003.bin` | `f112a707188a82b87de5be78b7ffa014e18240aa08825055c90d2e6626092fd7` | The contents of the MOS 6530-003 (1 KB), same filler |

**Where it came from.** Hans Otten's dumps of real 6530-002 and 6530-003 chips,
published on his retro computing site. His own site does not complete an HTTPS
handshake, so the copy taken here is the Internet Archive's of his files, fetched
raw on 23 June 2025:

- `https://web.archive.org/web/20250623140338id_/http://retro.hansotten.nl/uploads/files/6530-002%20fillerbyte00.bin`
- `https://web.archive.org/web/20250623140339id_/http://retro.hansotten.nl/uploads/files/6530-003%20fillerbyte00.bin`

**Rights.** The monitor ROM is MOS Technology's, copyright 1975, and MOS was
bought by Commodore in 1976. Who holds it now was not established, and no
licence or permission from any holder was found. It is here because Dan decided
on 1 October 2026 that a ROM is used when its position is documented, and this
is that document. If a rights holder asks for it to be removed, it will be.

## BBC Micro Model B ROMs

| File | SHA-256 | What it is |
|---|---|---|
| `bbc-micro/os.rom` | `2d9fea69017864f6962704481829f95fee08446c8c3a13826d5d4e44000ac9de` | The Model B's operating system, MOS 1.20 (16 KB, at `$C000` to `$FFFF`) |
| `bbc-micro/BASIC.ROM` | `45bd55dc0f6f0f8f1fe9e2481de7def206565eec8f600ba3068b849ca4132079` | BBC BASIC 2 (16 KB, paged ROM slot 15) |
| `bbc-micro/DFS-1.2.rom` | `e745e34895225a6650b712c1dd0656cb0b0b15f072a8ae6d9ea8d1ac257eb3d6` | The Disc Filing System, DFS 1.20, which names itself "DFS,NET" in its header (16 KB, paged ROM slot 14) |

**Where it came from.** jsbeeb's `public/roms/` folder, at commit
`e27b20d4a33c2a7b17d2cf830f4695e961b6846e`, fetched with `curl` on 2 October
2026:

- `https://raw.githubusercontent.com/mattgodbolt/jsbeeb/e27b20d4a33c2a7b17d2cf830f4695e961b6846e/public/roms/os.rom`
- `https://raw.githubusercontent.com/mattgodbolt/jsbeeb/e27b20d4a33c2a7b17d2cf830f4695e961b6846e/public/roms/BASIC.ROM`
- `https://raw.githubusercontent.com/mattgodbolt/jsbeeb/e27b20d4a33c2a7b17d2cf830f4695e961b6846e/public/roms/b/DFS-1.2.rom`

Only the ROM files are taken. jsbeeb is GPL and this repository is MIT, so none
of its code is used.

**Rights.** jsbeeb's `public/roms/README` says the ROMs are copyrighted and are
not GPL, that it thinks publishing them is fair use provided you own them, and
that it would remove them on request. The copyright is Acorn's, and who holds it
now was not established. No licence or permission from any holder was found. The
ROMs are here because Dan decided on 1 October 2026 that a ROM is used when its
position is documented, and this is that document. If a rights holder asks for
them to be removed, they will be.

## NES: the bundled homebrew, Lan Master

The NES has no system ROM. What is here is the one title its page runs before a
visitor loads their own: a game, not a system ROM, chosen by a rights pass for
being released by its author in words that allow it to be copied here. The
candidates and why each was or was not taken are in
`docs/journal/2026-10-06-the-nes-homebrew.md`.

| File | SHA-256 | What it is |
|---|---|---|
| `nes/Lan_Master.nes` | `becfeafb80479c330333c9e9385417f68f3c88e85443dfb37b05ae9283f3ea45` | Lan Master, the 2015 update of the 2011 game: an iNES file, mapper 0 (NROM), 32 KB PRG ROM and 8 KB CHR ROM, vertical mirroring, no battery |
| `nes/Lan_Master-manual.pdf` | `5d47e2e28e768de7d11ab50abb5bdbf909449032f74e994c90a67361d0c53a42` | The game's manual from the same archive, kept because it carries the licence |

**Who made it.** Shiru, who wrote the code, drew the graphics and made the
music, as his development notes in the archive say. The notes date it: begun
about November 2010, released in 2011, and fixed in 2015. His notes for Lawn
Mower add that Lan Master was made for the NES Coding Competition of 2011. A
routing puzzle: turn the pieces of wire until every computer is connected.

**Where it came from.** Shiru's own site lists it on its software page,
`https://shiru.untergrund.net/software.shtml`, as
`https://shiru.untergrund.net/files/nes/lan_master.zip`. On 6 October 2026 the
site's files answered `403 Forbidden` over `https` and refused connections over
`http`, so the copy taken here is the Internet Archive's. The archive holds the
file with one content digest in every capture from 27 March 2016 to 16 September
2025, so it did not change in that time. The capture of 27 March 2016 was fetched
raw with `curl` on 6 October 2026, and the capture of 15 August 2025 the same day
as a check, byte for byte the same:

- `https://web.archive.org/web/20160327132502id_/http://shiru.untergrund.net/files/nes/lan_master.zip`
  (1,126,553 bytes, SHA-256
  `79410cc133f1100d2fe5409fd49297a62afc748aeeb5faccb8fe69c68c16e6aa`)
- `https://web.archive.org/web/20250815233727id_/https://shiru.untergrund.net/files/nes/lan_master.zip`

The archive holds `Lan_Master.nes`, `manual.pdf`, `notes.txt` and two label
pictures. The ROM is committed as it came out of the archive, and the manual
too, under a name that says whose manual it is. The software page as archived on
17 September 2026 still lists the same 1.07 MB file.

**Rights.** The author released it into the public domain, and says so in two
places:

- The manual, in English: "The game is not licensed or endorsed by Nintendo
  company in any way. It is free of charge, released into Public Domain, and
  provided "as is", without warranty or responsibility of any kind." Its cover
  reads "PD 2011 Shiru".
- The game's own title screen, under the name: "PD2011 Shiru".

Nothing in the archive or on the software page says otherwise. A public-domain
release asks for nothing in return, so no notice is owed. The credit is given
anyway, here, and the page is to give it too. Dan can overrule the choice
before the work is merged.
