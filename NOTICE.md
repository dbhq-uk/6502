# Notices

This repository is MIT (see `LICENSE`). One thing in it was taken from outside
under different terms, and is recorded here. The system ROMs are recorded
separately, in [`roms/README.md`](roms/README.md).

## The SAA5050 teletext glyph table, from Bedstead

**What.** The row data of `src/Dbhq.Machines.BbcMicro/TeletextGlyphs.cs`: the
SAA5050's English character set, 96 glyphs of nine rows of five dots, which the
BBC Micro's mode 7 draws. It is the data of Bedstead's `glyphs[]` table, the US
ASCII (SAA5055) entries for codes `$20` to `$7F`, with the twelve codes where
the English set differs: eleven from the table's "Extra characters found in the
English (SAA5050) character set" entries, and the hash at `$5F`, which is the US
set's own number sign entry, moved from `$23`. What was taken is the row data,
which code each glyph belongs to, and Bedstead's name for each glyph, kept as a
comment beside its rows. None of Bedstead's code was.

**Where it came from.** `bedstead.c`, Bedstead version 3.261, by Ben Harris,
Simon Tatham, Marnanel Thurman and Neil Williamson
(<https://bjh21.me.uk/bedstead/>), fetched with `curl` on 3 October 2026:

- `http://bjh21.me.uk/bedstead/bedstead.c`, the copy the table was read from.
  The `https` address of the same file did not answer that day (a connection
  timeout after 40 seconds); plain `http` did.
- `https://web.archive.org/web/2026id_/https://bjh21.me.uk/bedstead/bedstead.c`,
  the Internet Archive's copy, fetched the same day as a check: byte for byte the
  same.

| File | Size | SHA-256 |
|---|---|---|
| `bedstead.c` (version 3.261) | 233,092 bytes | `432e8fe8b77cada259833170b4007494a2bc8584caeefcf8bebd385f5d4262b1` |

The table is committed, not fetched when the tests run (`AGENTS.md` rule 3).

**Rights.** Bedstead's header, as it stands in that file, and as it is repeated
at the top of `TeletextGlyphs.cs`:

> Many of the character bitmaps below formed the typeface embodied in
> the SAA5050 series of character-generator chips originally made and
> sold by British company Mullard in the early 1980s.  Copyright in the
> typeface will still be owned by Mullard's corporate successors, but
> under section 55 of the Copyright Designs and Patents Act 1988 that
> copyright is no longer infringed by the production or use of
> articles specifically designed or adapted for producing material in
> that typeface.
>
> The rest of the glyphs, and all of the code in this file, were
> written by Ben Harris <bjh21@bjh21.me.uk>, Simon Tatham
> <anakin@pobox.com>, Marnanel Thurman <marnanel@thurman.org.uk> and
> Neil Williamson <p298@tiddles.org> between 2009 and 2025.
>
> To the extent possible under law, Ben Harris, Simon Tatham, Marnanel
> Thurman and Neil Williamson have dedicated all copyright and related
> and neighboring rights to this software and the embodied typeface to
> the public domain worldwide.  This software and typeface are
> distributed without any warranty.
>
> You should have received a copy of the CC0 Public Domain Dedication
> along with this software. If not, see
> <http://creativecommons.org/publicdomain/zero/1.0/>.

So the table is used under its authors' CC0 dedication, which is compatible
with this repository's MIT licence. **The copyright in the original typeface
still stands**, held by Mullard's corporate successors, as the header says.
Bedstead's position is that under section 55 of the UK Copyright, Designs and
Patents Act 1988 that copyright is no longer infringed by producing or using
articles made to produce material in the typeface. Its web page puts it as a
belief: the original font is "essentially in the public domain in the United
Kingdom" as a result of section 55 "as applied by subparagraph 14(5) of Schedule
1", adding "I'm not a lawyer, though, so this may well be wrong" (read as quoted
in `video.md` section 4.5 and its source S13). This repository relies on
the same position and claims no more than it does. It is used because Dan
decided on 1 October 2026 that a source is used when its position is
documented, and this is that document; the choice among the candidates is in
`docs/bbc-micro/facts/video.md` section 4.5. If a rights holder asks for the
table to be removed, it will be.

**The independent check.** The test project types the same glyphs again from
the drawing in the Signetics SAA5050/55 datasheet, Figure 11
(<https://www.elektronikjk.com/elementy_czynne/IC/SAA5050.pdf>, page 16;
SHA-256 of the copy read on 3 October 2026:
`3479d135251adca12059e567ab64fc3e8c36d4cd40ae405f9170596d11dddd70`), in
`tests/Dbhq.Machines.BbcMicro.Tests/Figure11.cs`, and fails if the table differs
from it. That transcription is of the same typeface's dots, so the position
above covers it too. The datasheet itself is not in the repository.
