---
title: "A library of discs for the BBC Micro"
date: 2026-10-04
summary: "The BBC Micro's page gets a library of homebrew and freely licensed discs, each kept in the repository with its author's licence and, where the licence asks for it, its source. One click puts a disc in and starts it the way the machine does, with SHIFT held through BREAK, and a test starts every one of them that way. A disc of your own still goes in as before."
order: 29
---

# 4 October 2026: a library of discs for the BBC Micro

Dan, the same evening the BBC Micro went live: "find some disc for bbc and put
them in the repo and make them loadable on the site ... some presets basically
... keep ability to load file as well". Until now a visitor needed a disc image
of their own to see the drive do anything beyond `*CAT` on a blank disc.

## What was found

The research ran first and is the sourced record:
[`docs/bbc-micro/facts/discs.md`](../bbc-micro/facts/discs.md). It went through
Retro Software's wiki, GitHub, the stardot community's repositories, 8BS,
Stairway to Hell, bbcmicro.co.uk, the Internet Archive and several authors' own
sites, looking for one thing: an author or rights holder who has written down
that the program may be shared. It found nineteen that met that and booted on
this machine, and seventeen are shipped: ten games, a puzzle, four demos and two
tools. Each was booted in a scratch copy of the test project with SHIFT held
through power on, its start key pressed and a movement key seen to change the
picture.

## Decisions

### Decision: only what an author has licensed, and the classics stay load-your-own

The rule is `AGENTS.md` rule 4, which Dan set on 1 October for system ROMs and
which says commercial games are load-your-own. A disc is bundled only when its
author's licence is written down and kept with it; hosting by an archive,
"abandonware" and "preserved" are not a basis. Non-commercial licences are not
used either, because DBHQ is a company, even though this site sells nothing.
Chosen over shipping the well-known commercial titles, which is what most
emulator sites do: none of them is cleared. Two are worth asking about, and the
fact sheet says what to ask: **Elite**, whose two authors have said different
things in writing (one that he does not mind non-commercial copies, then later
"You have no such permission"), and **Level 9**, whose public repository is
Apache-licensed but disagrees with itself about whether the grant covers the
games' text, and holds no BBC Micro build.

### Decision: two finds dropped

**HEX survivors** is GPL but a day old, carries no copyright line and was made
mostly with AI tools; it can come in later if it settles. **BBC BASIC
one-liners** was a menu the researcher assembled from other people's short
programs, so its "author" would have been us, and its slot is better filled by a
BASIC disc DBHQ writes itself under MIT. Seventeen is the list.

### Decision: the discs live in this repository, not a repository of their own

Chosen over a separate `dbhq-uk` repository of discs, the way the test data are
forked. The discs are small (the whole library, with every licence and source
archive, is under 2 MB), the site serves them and the tests boot them, and
keeping them here puts the image, its licence, its source and the test that
boots it in one pull request. Rule 3 said games are never committed; it now says
commercial games are never committed, and names the preset discs as the one kind
of program that is, with their licences beside them. Rule 4 gained the bundling
rule in full.

### Decision: SHIFT and BREAK go through the machine's key queue

A disc starts on a real BBC Micro when SHIFT is held while BREAK is pressed and
let go: the DFS sees SHIFT as the machine restarts and runs the disc's `!BOOT`.
The page cannot do that with wall-clock timers, because SHIFT has to be in the
keyboard on the exact cycle the reset happens, and the machine runs in bursts a
frame long. So the key queue the page's keys already go through,
`BbcKeyPresses`, gained `ShiftBreak()`: SHIFT down, then a BREAK event that waits
its turn in the queue like a key, then SHIFT up half a second of machine time
later (`ShiftBreakHoldCycles`). Because it is queued, a key the visitor pressed
just before cannot land after the reset. The host exports it as
`BbcHost.ShiftBreak`. Chosen over the research harness's way, power on with SHIFT
held, because on the page the machine is already running and a visitor may pick
a second disc while the first plays; the test does both of those.

### Decision: a list, not a row of buttons

The library is a native `<select>`, grouped by kind, with the chosen disc's
credit under it and two buttons, Insert and run and Insert in drive 0. Chosen
over a button for each disc, which on a phone would be a column longer than the
screen. The list works as soon as the script does, so the credits can be read
before Start; the buttons wait for the machine, as the other disc controls do.
Start keeps the page's one lime fill.

### Decision: a library disc goes in writable

Some games keep high scores on their disc. A library disc goes in as the
Write-protect switch says, which is off unless the visitor turns it on, and a
game's writes last until the disc is changed or the page is left; Save disc
downloads the disc as it stands, as for any other. Chosen over write-protecting
library discs by default, which would make those games fail to save with a DFS
error the visitor did not cause.

### Decision: fetched when inserted, never counted in Start

Each image is published beside the machine's files, at
`/machines/bbc-micro/discs/<slug>.ssd`, same origin, so the CSP is unchanged.
It is fetched only when the visitor inserts it, and the Start button's download
size leaves the discs out, because Start fetches none of them. Reading
`downloadBytes` showed it walked the whole machine folder, so the discs would
have been added to the figure on the button; it now skips `discs/`, and a test
says so.

## How the GPL source is handled

Eleven of the seventeen are GPL or LGPL. Each of those folders has `source/`,
holding the source exactly as its author published it: the author's source
archive for the Retro Software games, a tarball of the repository at the
release's commit for Beat the Bull and Beeb 6502 test, and `demo.6502` for the
BeebAsm demo, which includes no other file. Farmyard Fun is BBC BASIC, so the
program on the disc is its source: its files were taken out of the image byte
for byte, each with a `.inf` giving its addresses. Every folder has `LICENSE`, the
licence text as its author shipped it, kept byte for byte (git treats the disc
folders as binary, so no line ending is ever changed), and a README with the
statement verbatim, where the image came from, its SHA-256 and what was changed.

CP/M-65 is the exception the fact sheet left open. Its README says its
`third_party` folder as a whole contains GPL software. Read against the build
files at the release's commit, the BBC Micro disc carries only two third-party
parts, Altirra BASIC (FSF all-permissive) and Pascal-M (MIT), and none of the GPL
ones, so its licence is `BSD-2-Clause AND MIT AND FSFAP`, all three licence texts
are kept beside it, and its source, about 2 MB, is recorded by address and hash
rather than committed.

## Photosensitivity

Headcase Hotel and Blinkenlights flash. Each carries a note, shown first in its
credit with `role="note"` before anything is loaded, and again in the credits
list. Headcase Hotel's is the revised 2021 build, which its author says has a
vastly reduced strobing effect; reduced is not none, so the note stays.

## How each disc was tested

`DiscLibraryTests` reads the manifest, checks each image against its SHA-256,
boots the machine to BASIC's prompt, inserts the disc and calls `ShiftBreak`, as
the page does. Each disc must leave the prompt, never show a DFS or BASIC error,
and reach the first screen it is known to show: words on it where it has them
("Instructions for *ONSLAUGHT*", "Escape from the Mazezams", "A>"), and
otherwise a screen mode and a count of picture cells the screen reader cannot
read as text. A disc that does not get there within twelve seconds of machine
time fails. A control test does the same with BREAK alone and must stay at the
prompt, and another starts Caterpillar over a running Onslaught. On 4 October,
`dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release --filter
"FullyQualifiedName~DiscLibraryTests"` passed 20 tests in 15 seconds. The
browser check does the same on the page: Insert and run for the default, its
mode and colours, a second disc's credit, Insert in drive 0 and `*CAT`, a third
disc over the first, then the visitor's own file through the file input.

This shows that each disc starts and draws its first screen on this machine. It
does not show that every game plays to its end, and the sound was not listened
to.

## Surprises and mistakes

- **The licence versions were narrower than the fact sheet said.** It gave
  "GPL-3.0" for most. The source files of five of them say "or (at your option)
  any later version", MazezaM's puzzles are licensed "v3.0" only, and three state
  version 3 and nothing more. The manifest carries the exact SPDX id for each,
  and the fact sheet is corrected, with a section saying what was checked.
- **Two open questions closed.** Bruce Clark's decimal test, which the fact
  sheet marked unknown, says of itself "This code is public domain." And
  Headcase Hotel's image is the repository's own build of 28 October 2021, not
  the original strobing release.
- **Blinkenlights was built, not downloaded,** because its repository has no
  disc image. It was built twice from separate clones and came out the same
  both times, so its README can say exactly how to make it again.
- **MazezaM's first screen was guessed wrong.** The test first looked for a
  picture, from a research screenshot taken after a key press; the disc's first
  screen is a title page with words on it. The failing test showed the screen,
  and the expectation now looks for its words.
- **The CI cache would have gone stale.** The BBC Micro's browser build is
  cached on its own inputs; once it publishes the discs, a disc changed with
  nothing else changed would have been served from the old cache. The key now
  hashes `machines/bbc-micro/discs/` and the module that reads them, and a test
  holds that.
- **Sparse Invaders' page has an older diary note** asking that the source not
  be "used for profit type ventures", written before its author chose a licence.
  He then released it under the GPL, which allows any use, and the page's own
  licence section says GPLv3. It is shipped, with the note quoted in its README.
- The brief for this work asked for the visitor descriptions to be taken from a
  "one-line" section of the fact sheet, which does not exist. They were written
  here from each author's own page and README instead, and each is in the
  manifest.
