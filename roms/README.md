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
