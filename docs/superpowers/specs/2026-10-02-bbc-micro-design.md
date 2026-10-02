# The BBC Micro Model B: design

Written 2 October 2026. This is the design for the second machine on the core,
and the first to need a shared browser host. It follows
[the machines programme](2026-09-30-machines-and-site-design.md), where
"implemented" is defined, and [the core's design](2026-09-29-6502-design.md).
Issue [#26](https://github.com/dbhq-uk/6502/issues/26) tracks the work.

## What is being built

The Acorn BBC Micro Model B (1981): a 2 MHz NMOS 6502, 32 KB of RAM, the
operating system and BBC BASIC in ROM, an eight-mode display that includes
teletext, three-voice sound, a keyboard, and a floppy disc interface. It runs
in the browser at 6502.dbhq.uk.

**It counts as implemented when all of these hold.** The machine boots to the
BASIC prompt in every video mode, runs a BASIC program, saves a file to a disc
image and loads it back, and passes the chip tests below, with all of it in CI.
It also runs in the browser, and the real-browser check passes. The home page
count then goes from 1 to 2, computed from the registry as before.

## Decisions taken in this session

These are Dan's calls, 2 October 2026, each with what it was chosen over.

| Decision | Chosen over | Why |
|---|---|---|
| The disc controller is in the first version | Boot to BASIC with no disc; display and keyboard only | Dan's call. Disc images make the machine usable, and the disc test is the proof the 8271 works. First light waits on it |
| Cycle-accurate timing: every bus access ticks the video chips, timers and slow-device stretch | Scanline-based video | It is the core's own rule applied to the whole machine. Mid-line changes and timing tests need it, and a scanline machine can boot and still be wrong in ways found late |
| Our own tests first, no third-party test material in the acceptance test | Researching community test discs before any code | The KIM-1 showed that waiting costs more than it buys. Community material can be forked into `dbhq-uk` and added later as extra evidence |
| The machine counts when it passes and runs in the browser; the 3D models follow in a second pull request and do not block it | The count waiting for the models | The models need measured photographs and a traced board, which is a lot of work. This reads issue #28 as a rule for the page, not a gate on the count. Dan approved this part of the design on 2 October |

## Parts

A new library, `Dbhq.Machines.BbcMicro`, beside `Dbhq.Machines.Kim1`. It uses
the `Nmos6502` variant. Each part is one file with one job, tested alone.

| Part | What it does |
|---|---|
| Memory map | 32 KB RAM, a 16 KB paged language ROM area with the DFS and BASIC in it, a 16 KB operating system ROM, and the `FRED`, `JIM` and `SHEILA` I/O pages |
| System VIA (6522) | Keyboard scanning, the sound chip and latch lines, and the vertical sync interrupt |
| User VIA (6522) | Two timers and the user port |
| CRTC (6845) | Raster timing and the screen start address |
| Video ULA | Mode, palette and pixel output, driven by the CRTC |
| Teletext (SAA5050) | Mode 7 |
| Sound (SN76489) | Three tone channels and a noise channel, written to a sample buffer |
| Floppy controller (8271) | Commands, sector reads and writes, against `.ssd` and `.dsd` images |
| Keyboard matrix | The matrix the system VIA scans |

**Slow devices.** The 1 MHz chips stretch a 2 MHz CPU access to line up with
their clock. That lives in the bus's `Read` and `Write`, which tick extra cycles
before returning, as the core's design says. The core needs no special code.

**What is left out.** The 6850 serial port, the analogue-to-digital converter
and joystick port, the Tube connector, the cassette interface, the printer
port, and the 1 MHz bus. Each becomes its own issue the day the spec merges.
The page says what is not modelled, as the KIM-1's page does.

## Where the code comes from

**No code is taken from another emulator.** jsbeeb is GPL-3.0 and B-em is GPL.
This repository is MIT, so the chips are written from the datasheets and from
Acorn's hardware documentation, and checked by our own tests. Only the ROMs are
taken from jsbeeb, and they are not its code.

## ROMs

Three ROMs run the machine, all from jsbeeb's `public/roms/`, at jsbeeb commit
`e27b20d4a33c2a7b17d2cf830f4695e961b6846e`, the head on the day they were
fetched. jsbeeb pairs these three for its "BBC B with 8271 (DFS 1.2)" model
[from `src/models.js` in `mattgodbolt/jsbeeb`].

| File | Size | SHA-256 | What it is |
|---|---|---|---|
| `os.rom` | 16 KB | `2d9fea69017864f6962704481829f95fee08446c8c3a13826d5d4e44000ac9de` | The operating system. Its text starts "BBC Computer" and carries the Acorn contributors' credit |
| `BASIC.ROM` | 16 KB | `45bd55dc0f6f0f8f1fe9e2481de7def206565eec8f600ba3068b849ca4132079` | BBC BASIC, "(C)1982 Acorn" |
| `b/DFS-1.2.rom` | 16 KB | `e745e34895225a6650b712c1dd0656cb0b0b15f072a8ae6d9ea8d1ac257eb3d6` | The disc filing system, which names itself "DFS,NET" and so appears to carry an Econet filing system as well |

**Rights, documented.** jsbeeb's `public/roms/README` says the ROMs are not GPL
and are still copyrighted, that it thinks publishing them is fair use provided
you own them, and that it would remove them if anyone asked. The copyright is
Acorn's, and who holds it now was not established. No licence or permission
from any holder was found. They are used because Dan ruled on 1 October 2026
that a ROM is used when its position is documented, and this is that document.
`roms/README.md` gets an entry with the same wording and the hashes above, the
files go under `roms/bbc-micro/`, and the page says what the KIM-1's says: that
the position is documented, not cleared, and that the files are removed if a
holder asks.

**The operating system's version** is not established yet. jsbeeb names the file
`os.rom` and gives no version. The plan reads it off the ROM and the page states
it.

## The browser host

Issue #26 says the shared host is extracted with this machine, not before.
`site/public/kim-1.js` does two jobs today. One is generic: load the WebAssembly,
run the frame loop against a cycle budget, and report headroom. The other is the
KIM-1's keypad and digits.

- **`site/public/machine-host.js`** takes the generic job: start the machine,
  run the loop, show the headroom line and handle a failed load.
- **Each machine keeps its own panel script.** The BBC's covers the screen, the
  keyboard, sound and the disc.
- **The KIM-1 moves onto the host in the same pull request.** It is live and
  counts toward the headline, so this is the risky change. The existing
  real-browser check must pass unchanged. If it does not, the extraction is
  wrong and the check is not.

**The BBC page.**

- **Screen.** A canvas filled from the ULA's framebuffer at 50 Hz, in the real
  4:3 shape. The page says it models the picture and is not a CRT.
- **Keyboard.** Keys map by position on a PC keyboard, so each key presses the
  BBC key in the same place. The system VIA scans a matrix, so a press is a
  matrix bit and not a character.
- **Sound.** The SN76489 writes samples to a buffer that an `AudioWorklet`
  plays. Sound is off until the visitor clicks, because browsers block
  autoplay. CI can check the samples, not the audio.
- **Disc.** A visitor drops a `.ssd` or `.dsd` image on the page, or makes a
  blank one. Save downloads the image. Nothing is uploaded.
- **Start button.** The machine's WebAssembly downloads on click and not on
  page load. The KIM-1's is about 7 MB, tracked in issue
  [#25](https://github.com/dbhq-uk/6502/issues/25), and the BBC's will be
  larger. Its size is measured early.

**Photo and models.** Under issue [#28](https://github.com/dbhq-uk/6502/issues/28),
the BBC Micro has a case, so it gets two models: the outside, and the board with
its chips and tracks. The first pull request carries the credited photograph,
because the registry test already fails a running machine without one. The two
models are a second pull request, built the way the KIM-1's was: measured from
photographs, tracks traced from a credited photograph of the board.

## Proof

Five layers, built in this order. All run in CI. None needs third-party test
material, as Dan chose.

1. **Chip tests, one file per chip, each with the chip alone.**
   - **6522:** the timers, interrupt flags, shift register and handshake lines,
     against cycle counts worked out from the datasheet.
   - **6845:** the register behaviour and the raster counts.
   - **Video ULA:** mode and palette decoding.
   - **8271:** the command set against a disc image.
   - **SN76489:** tone and noise output.
   - **Keyboard matrix:** a key gives the matrix bit the system VIA expects.
2. **Boot test.** The headless machine runs until the system banner appears and
   the screen is compared with the text the ROM is known to print, which starts
   "BBC Computer" and is followed by the memory size and the `>` prompt
   [inferring from the ROM's strings; the plan checks the exact line]. It runs
   in all eight modes. A test helper decodes the framebuffer's pixels back into
   characters using the font in the operating system ROM, so the check reads the
   picture and not the machine's own text buffer, and it is not circular.
3. **BASIC test.** It types a short program through the keyboard matrix and
   checks what it prints, for example a loop and `PRINT 6*7`.
4. **Disc test.** A test helper writes an empty DFS catalogue from the format's
   published description, so the image is our own bytes. The test uses `*SAVE`,
   resets the machine, then `*LOAD`s and runs the file.
5. **Real-browser check.** `browser-check.mjs` grows to boot the BBC page in
   headless Chrome, type a line through the page, read the screen back, and
   check for no console errors and the headroom line.

**A mutation pass follows**, as the KIM-1 had: break one thing at a time and
confirm a test fails. Anything that survives gets a test or an entry in
`docs/known-differences.md`.

**Speed.** The core reached 51 MHz in the browser compiled ahead of time, but
only two of five runs reached 25 times a 2 MHz machine. The BBC adds video,
timer and stall ticking on every cycle. So the first milestone is a headless
machine, built before the rest, whose speed is measured in the browser, and the
plan records the figure. If headroom is thin, the fix is a cheaper per-cycle
path, and Dan is told before any change.

## Settled in the plan, not here

- **The teletext character set.** The SAA5050's character ROM is the chip's
  own. jsbeeb's copy is GPL and cannot go in an MIT repository. The default is
  to draw the glyphs from the published character table, and the plan confirms
  the source before any glyph is written.
- **The exact stall cycles** for each 1 MHz device, and the CRTC clock in each
  mode, which the plan takes from Acorn's hardware documentation and checks.
- **The memory map's I/O addresses,** from the same documentation.
- **The operating system's version,** as above.

## How the work lands

- **Spec and plan** in `docs/superpowers/` in this repository, the plan with every
  code block compiled and run before it goes in, as for stage 1.
- **Build** with a fresh agent per task, each reviewed before the next starts.
- **Journal** an entry per working day in `docs/journal/`, in the same pull
  request as the work (rule 6 in `AGENTS.md`).
- **Pull requests:** one for the machine, the host and the page, a second for the
  two models. Nothing merges until `Validate` is green, and the live count
  changes only with Dan's go.
- **Rights:** `roms/README.md` gets the BBC entry.
- **Left-out parts** become issues the day this spec merges, and are not started.

## Out of scope

The BBC Micro B+ and the BBC Master are separate registry rows and separate
specs. The Master's CPU is a 65C02, which the core already covers. The Tube and
the second processor, the 1770 disc controller and ADFS are not in this machine.
