# The Acorn Electron: design

Written 4 October 2026. This is the design for the third machine on the core. It
follows [the machines programme](2026-09-30-machines-and-site-design.md), where
"implemented" is defined, [the core's design](2026-09-29-6502-design.md), and
[the BBC Micro design](2026-10-02-bbc-micro-design.md), whose pattern it copies
wherever the machines agree. Issue [#51](https://github.com/dbhq-uk/6502/issues/51)
tracks the work.

## What is being built

The Acorn Electron (1983): a 2 MHz NMOS 6502, 32 KB of RAM, the operating system
and BBC BASIC in ROM, a display of seven modes (the BBC's eight, without
teletext), a one-bit sound output, a keyboard, and a cassette port. One custom
chip, the ULA, does the display, the sound, the cassette port, the keyboard
read, the ROM paging and the interrupts. It runs in the browser at 6502.dbhq.uk.

**It counts as implemented when all of these hold.** The machine boots to the
BASIC prompt, runs a BASIC program, saves a program to a tape image and loads it
back, and passes the contention test below, with all of it in CI. It also runs
in the browser, and the real-browser check passes. The home page count then goes
from 2 to 3, computed from the registry as before.

## Decisions taken in this session

These are Dan's calls, 4 October 2026, each with what it was chosen over.

| Decision | Chosen over | Why |
|---|---|---|
| The Electron is the third machine, then the Atari 2600, the NES and the Commodore 64 | The NES next, as published; all four at once | The count moves soonest and each review comes alone. Recorded in the machines design |
| Cassette is the first version's storage, as UEF tape images | The Plus 1 and Plus 3 with disc images; no storage | The tape port is the stock machine's own and lives in the ULA, which is being written anyway. It also gives the save-and-load proof the BBC set. Disc is a later issue |
| The ULA's holding off of the CPU is modelled exactly, access by access | One average slowdown per screen mode; none at all | Contention is what makes this machine differ from a slower BBC, and programs depend on it. The rest of the ULA's work is caught up lazily, as the BBC's chips are |

## Parts

A new library, `Dbhq.Machines.Electron`, beside `Dbhq.Machines.BbcMicro`. It uses
the `Nmos6502` variant. Each part is one file with one job, tested alone. The
addresses and register layouts below are from memory of Acorn's documentation
and are checked in the plan, which reads them from the documents before any
code is written.

| Part | What it does |
|---|---|
| Memory map | 32 KB RAM, paged ROM slots with BASIC in them, the 16 KB operating system ROM at the top, and the I/O pages with the ULA's registers |
| ULA, display | Screen modes 0 to 6, the palette, the frame and the vertical sync, drawn into a framebuffer |
| ULA, contention | Decides on every bus access whether the ULA is holding the RAM and the CPU must wait, from the display's position in the frame and the mode |
| ULA, sound | The one-bit output at the pitch the operating system sets, written to a sample buffer |
| ULA, cassette | The serial tape interface, a motor line and the interrupts it raises |
| ULA, paging and interrupts | The ROM select register, and the interrupt status and clear registers |
| Keyboard matrix | The ULA gives the keyboard read the high address lines, so a key press is a bit that appears when the right lines are selected, not a character |
| Tape images | A reader and writer for the UEF format, the community standard for Acorn tapes |

**Contention.** The Electron's RAM is four bits wide, so it cannot serve the ULA
and the CPU at once. While the ULA fetches the picture the CPU is held off, and
how often depends on the mode. The same code therefore runs at different speeds
in different modes. The model keeps a position in the frame and answers, on each
access, whether this one is held and for how many cycles, in the bus's `Read` and
`Write` as the BBC's slow devices are. The core needs no special code. The exact
fetch pattern is taken from the ULA's documentation in the plan and checked
against the test below, not assumed.

**What is left out.** The Plus 1 expansion and its cartridge slots, the Plus 3
and its disc controller, the joystick and printer ports, and any other add-on.
Each becomes its own issue the day the spec merges. The page says what is not
modelled, as the BBC's does.

## Where the code comes from

**No code is taken from another emulator.** Elkulator and elkjs are GPL and this
repository is MIT, so the ULA is written from Acorn's documentation and checked
by our own tests. Only the ROMs are taken from elkjs, and they are not its code.

## ROMs

Two ROMs run the machine, both from the `dmcoles/elkjs` repository at commit
`ff123355407f79a91f808e31222dcca5d51ea87f`, the head on the day they were found.
jsbeeb, which supplied the BBC's ROMs, has no Electron ROMs.

| File | Size | SHA-256 | What it is |
|---|---|---|---|
| `os.rom` | 16 KB | `b63f851d79498f598999d923b7c9f62e2525c34f0b9cd2d4b328b89d622dcda4` | The Electron operating system. It names itself "OS 1.00" and carries "(C) 1983 Acorn Computers Ltd." Its reset vector is `$D8D2` |
| `basic.rom` | 16 KB | `45bd55dc0f6f0f8f1fe9e2481de7def206565eec8f600ba3068b849ca4132079` | BBC BASIC, "(C)1982 Acorn" |

**BASIC is the BBC's file.** `basic.rom` has the same SHA-256 as the BBC Micro's
`BASIC.ROM`, checked on 4 October 2026, so it is kept once under `roms/` and not
twice, and the Electron's tests pin the same hash.

**Rights, documented.** The copyright is Acorn's, and who holds it now was not
established. The elkjs repository is licensed GPL-2.0 and says nothing about the
ROMs, so that licence is not a licence for them. No permission from any holder
was found. They are used because Dan ruled on 1 October 2026 that a ROM is used
when its position is documented, and this is that document. `roms/README.md`
gets an entry with this wording and the hashes, the registry's `rights` field
says the same, and the page states that the position is documented, not cleared,
and that the files are removed if a holder asks.

**Not yet established.** Whether the Electron needs a separate keyboard ROM
image, or has that code inside the operating system ROM, is checked in the plan
by reading the paging in the operating system. If a third image is needed, it is
sourced and documented the same way before any code depends on it.

## The browser host

The shared `site/public/machine-host.js` loads the WebAssembly, runs the frame
loop against a cycle budget and shows the headroom line. The Electron adds its
own panel script, and changes nothing in the host unless the plan finds that it
must.

- **Screen.** A canvas filled from the ULA's framebuffer at 50 Hz, in the real
  4:3 shape. The page says it models the picture and is not a CRT.
- **Keyboard.** Keys map by position on a PC keyboard, so each key presses the
  Electron key in the same place.
- **Sound.** The ULA's output writes samples to a buffer that an `AudioWorklet`
  plays, off until the visitor clicks.
- **Tape.** A visitor drops a `.uef` image on the page, or starts with a blank
  tape. Saving downloads the image. Nothing is uploaded. Loading from a real
  tape takes minutes at the machine's own speed, so the page says how long a
  load takes and runs it at the machine's real rate, not faster, unless the plan
  finds a faithful way to shorten the wait.
- **Start button.** The WebAssembly downloads on click and not on page load,
  as on the other two pages.

**Photo and models.** Under issue [#28](https://github.com/dbhq-uk/6502/issues/28),
the Electron has a case, so it gets a credited photograph in the first pull
request, because the registry test fails a running machine without one. The two
models, outside and inside, are a second pull request and do not block the count.

## Proof

Four layers, built in this order. All run in CI. None needs third-party test
material.

1. **Chip tests, one file per part, each alone.**
   - **Display:** mode and palette decoding, the frame's length and the sync.
   - **Contention:** the held cycles in each mode against a pattern worked out
     by hand from the ULA's documentation, written down first and not taken from
     the model.
   - **Sound:** the output's period against the register that sets it.
   - **Cassette:** the interrupts and the bits against a known byte stream.
   - **Paging and interrupts:** the select register and the status and clear
     registers.
   - **Keyboard matrix:** a key gives the bit the operating system's scan expects.
   - **Tape images:** a UEF file round-trips, including the gzip wrapping that
     `.uef` files are usually stored in.
2. **Boot test.** The headless machine runs until the banner appears and the
   screen is compared with the text the ROM is known to print, which reads
   "Acorn Electron" and ends in the BASIC prompt [inferring from the ROM's
   strings; the plan checks the exact line]. A test helper decodes the
   framebuffer's pixels back into characters using the font in the operating
   system ROM, so the check reads the picture and not the machine's text buffer.
3. **BASIC and tape test.** It types a short program through the keyboard
   matrix, checks what it prints, `SAVE`s it to a tape image, resets, `LOAD`s it
   and runs it. The tape image is written by the machine and read back by the
   UEF reader, so the check does not depend on a recording made elsewhere.
4. **Contention test.** One fixed machine-code loop is timed against the frame
   in two modes that load the RAM differently. The test states the cycle counts
   expected in each, computed independently of the model, and fails if they are
   not different or not these. It is the proof that the machine is not a slow
   BBC.
5. **Real-browser check.** `browser-check.mjs` grows to boot the Electron page in
   headless Chrome, type a line through the page, read the screen back, and
   check for no console errors and the headroom line.

**A mutation pass follows**, as the KIM-1 and the BBC had: break one thing at a
time and confirm a test fails. Anything that survives gets a test or an entry in
`docs/known-differences.md`.

**Speed.** The contention check runs on every bus access, which the BBC's
design did not. So the first milestone is a headless machine with the check in
place, whose speed is measured in the browser before the rest is built, and the
plan records the figure. If headroom is thin, the fix is a cheaper way of asking
the question, and Dan is told before any change. The BBC's figure to beat is
about ten times real speed at its worst.

## Settled in the plan, not here

- **The fetch pattern and the held cycles** for each mode, and the ULA's clock
  against the CPU's, from Acorn's documentation.
- **The memory map's I/O addresses and the ROM paging,** from the same
  documentation and read off the operating system.
- **Whether a keyboard ROM image is a separate file,** as above.
- **The tape's timing,** its baud rate and the carrier and gap lengths the
  operating system expects, from the UEF specification and the ROM.
- **The operating system's behaviour on a missing tape or a read error,** so the
  page can say what a visitor sees.

## How the work lands

- **Spec and plan** in `docs/superpowers/` in this repository, the plan with every
  code block compiled and run before it goes in.
- **Build** with a fresh agent per task, each reviewed before the next starts.
- **Journal** an entry per working day in `docs/journal/`, in the same pull
  request as the work (rule 6 in `AGENTS.md`).
- **Pull requests:** one for the machine and the page, a second for the two
  models. Nothing merges until `Validate` is green, and the live count changes
  only with Dan's go.
- **Rights:** `roms/README.md` gets the Electron entry.
- **Left-out parts** become issues the day this spec merges, and are not started.

## Out of scope

The Electron with a Plus 1 or a Plus 3, which is the same machine with add-ons
and would be a later issue on this row. The BBC Micro B+ and the BBC Master are
separate registry rows and separate specs. Other machines in the Acorn family
that share the ULA's ideas, such as the Acorn Atom, are not built on this work.
