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
