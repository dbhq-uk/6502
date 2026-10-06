# The NES: design

Written 5 October 2026. This is the design for the third machine on the core.
It follows [the machines programme](2026-09-30-machines-and-site-design.md),
where "implemented" is defined, and [the core's design](2026-09-29-6502-design.md).
The BBC Micro's [design](2026-10-02-bbc-micro-design.md) is the pattern for how
it is laid out, proved and landed.

## What is being built

The Nintendo Entertainment System (NES, 1983 in Japan as the Famicom): a Ricoh
2A03 (an NMOS 6502 with decimal mode removed, and the sound unit built in), the
2C02 picture processing unit (PPU), 2 KB of work RAM, 2 KB of video RAM, a
cartridge with its own mapper chip, and two controllers. It runs in the browser
at 6502.dbhq.uk. **Both regions are built:** NTSC (2A03 and 2C02) and PAL (2A07
and 2C07). They are one registry row, one library and one page, with a region
setting.

**It counts as implemented when all of these hold.** In both regions, the
headless machine passes `nestest` and the chip tests below. It passes the pinned
community test ROMs listed below, except where a failure is written in
`docs/known-differences.md` with its cause. The bundled homebrew boots to its
recorded picture. The machine runs in the browser with picture, sound and
controller input, and the real-browser check passes. All of it runs in CI. The
home page count then goes up by one, computed from the registry as before.

## Decisions taken in this session

These are Dan's calls, 5 October 2026, each with what it was chosen over.

| Decision | Chosen over | Why |
|---|---|---|
| A dot-accurate PPU, stepped inside every CPU bus cycle | A scanline PPU; dot-accurate but staged | It is the core's rule 1 applied to the whole machine. NMI, sprite-0 and MMC3 timing tests need it, and the later port of a game needs a trustworthy reference |
| Six mappers: NROM, MMC1, UxROM, CNROM, AxROM, MMC3 | Five without MMC3; NROM and MMC1 only | The proof ROMs need the first two. The rest cover most of the common library. MMC3's scanline counter watches the PPU's address line A12, which tests the dot-accurate PPU |
| A freely licensed homebrew game is bundled, and a file picker takes the visitor's own ROMs | Picker only; a test ROM as the first-load program | The page has something to play on first load. The game is picked after a rights check |
| Tick inside every bus call | Catch-up scheduling | Catch-up is faster but easy to get wrong near NMI, sprite-0 and the MMC3 A12 edge, which is where the proof looks |
| PAL is in this machine, with NTSC | NTSC only | Dan's call, 5 October, after the first draft. The PAL PPU runs 3.2 dots to a CPU cycle, so the bus's tick is a ratio per region and not a fixed three |
| The 3D models are in scope, and are handed to another agent as their own project, in a second pull request | Models in this spec; models left out | Dan's call, 5 October. The BBC Micro's models were their own spec and plan, and Dan ruled on 2 October that models never hold up a count. The NES is cased, so it must claim both models or none |

Dendy (the Russian Famicom clone, 50 Hz with 3 dots to a cycle) is a third
timing and stays out of scope. A NES 2.0 header that says Dendy is refused with
a plain message.

## Parts

Two new libraries beside the BBC Micro's, `Dbhq.Machines.Nes` and
`Dbhq.Machines.Nes.Wasm`. The machine uses the existing `Ricoh2A03` variant, so
the core needs no change. Each part is one file with one job, tested alone.

| Part | What it does |
|---|---|
| `NesBus` | The CPU's bus. 2 KB RAM mirrored to `$1FFF`, the PPU registers at `$2000` and mirrored to `$3FFF`, the APU and I/O registers, the controller ports, and the cartridge from `$4020`. Each `Read` and `Write` advances the PPU by the region's ratio and the APU one cycle, then does its access. NTSC is three dots a cycle. PAL is 3.2, which is 16 dots in every 5 cycles, kept by a whole-number accumulator, so a cycle has three dots or four |
| `Region` | A small value type for NTSC or PAL: the PPU dots per CPU cycle, lines a frame (262 or 312), the line the pre-render pass runs on, whether odd frames drop a dot, the CPU clock, the APU's noise and DMC rate tables and frame counter step cycles, and the PPU's colour-emphasis bit order (the 2C07 swaps red and green). One place holds every number that differs, so no chip tests for a region by name |
| `Ppu` | One method, `Tick()`, steps one dot. Owns VRAM, OAM, palette RAM, the scroll registers, background and sprite pipelines, and the frame buffer. Raises NMI on a line the bus passes to the CPU |
| `Apu` | Two pulse channels, triangle, noise, DMC and the frame counter. Ticks once per CPU cycle and writes to a sample buffer |
| OAM DMA | A write to `$4014` stalls the CPU for 513 or 514 cycles. It lives in the bus, because the core knows no machine (rule 2) |
| DMC DMA | A DMC sample fetch steals CPU cycles, also inside the bus |
| `Controller` | The strobe and serial shift protocol for two standard pads |
| `Cartridge` | Parses iNES and NES 2.0 headers and picks the mapper. It reports the region the header names, where it names one (NES 2.0 byte 12; the iNES 1 flag is too unreliable to trust alone) |
| `IMapper` | CPU and PPU reads and writes, nametable mirroring, an IRQ line, and a hook that sees every change of the PPU address line A12 |
| `Mappers/` | One class each: `Nrom`, `Mmc1`, `Uxrom`, `Cnrom`, `Axrom`, `Mmc3` |
| `Nes` | Joins the parts for a region, resets, runs a frame, and exposes the frame buffer, samples and controller input. The region is fixed when the machine is built, so a change of region is a new machine |

**The order inside a cycle matters.** A CPU access to a PPU register sees the
dot the PPU is on. The bus can run the dots before the access, after it, or
split around it. The plan fixes this from the nesdev wiki's timing notes and
the `ppu_vbl_nmi` tests, and records the choice and what it was chosen over. On
PAL it also fixes which cycles in the five get the fourth dot, and how that
lines up at reset. Real consoles differ from power-on to power-on, so the choice
and its limit go in `docs/known-differences.md`.

**Region.** The page and the headless host take a region (`Ntsc` or `Pal`). The
page picks it from the header where the header names one, and a visitor can
override it. The two regions share every class. What differs is in `Region`.

**What is left out.** Dendy, the Famicom Disk System and the
Famicom's expansion audio, the Zapper and other peripherals, the four-player
adapters, unlicensed and exotic mappers, and the PPU's analogue quirks such as
the NTSC colour artefacts. The page says what is not modelled, as the others do.
Each becomes its own issue the day the spec merges.

## Where the code comes from

**No code is taken from another emulator.** Mesen, FCEUX and Nestopia are GPL,
and this repository is MIT. The chips are written from the nesdev wiki's
documentation, in fact sheets under `docs/nes/facts/`. Each sheet is written
before its code, as the BBC Micro's were, and says which wiki page each
statement comes from. The wiki is read, and nothing in it is copied as code.

## Ricoh 2A03 and the core

`CpuVariant.Ricoh2A03` exists, and decimal mode on it is a no-op. Its comment
says it covers the 2A07 too: the PAL chip differs from the 2A03 in its clock
divider and its sound tables, not in the CPU, so both regions use it. The NES has
the sound unit on the same die, which is the `Apu` here, not the core
(rule 2). The plan checks that the `$4017` frame counter and the CPU's interrupt
lines are wired through the bus, and that an NMI arriving through the bus on the
cycle the PPU raises it is taken at the same point the core takes it for the
KIM-1 and BBC Micro.

## ROMs and rights

**The NES has no system ROM.** Nothing about it is committed under `roms/`
except what the bundled homebrew needs.

- **Commercial games are never bundled and never committed** (rule 4). The
  visitor loads their own, from their own disk, and nothing is uploaded.
- **The bundled homebrew** is chosen by a `legwork` rights pass before any page
  work. It must have a licence that allows redistribution here. The registry's
  `rights` field and `roms/README.md` get its author, its licence, the original
  URL and a sha256. If no title passes, the page ships with the picker only, and
  Dan is told.
- **Test ROMs** are not committed (rule 3). They come from the existing
  `dbhq-uk/nes-test-roms` fork, at a pinned commit, each with a recorded hash in
  `Pins.cs`. `nestest` is already pinned there.

## Proof

Five layers, built in this order. All run in CI.

1. **The CPU on the NES bus.** `nestest` in automation mode (start at `$C000`)
   through `NesBus`, against its pinned log. The core's own `nestest` test
   already passes. This one checks the bus.
2. **Chip tests, one file per chip, each with the chip alone.**
   - **PPU:** register behaviour, the `$2002` read and the `$2005`/`$2006` write
     latch, the VBlank flag and NMI at line 241 dot 1, the odd-frame dot skip,
     sprite 0, sprite overflow, the palette and nametable mirroring, and the
     scroll copies at dots 257 and 280 to 304. Run for each region, with the
     region's line count, its VBlank length (20 lines NTSC, 70 PAL), the odd-frame
     dot skip on NTSC only, and the dots-per-cycle accumulator on PAL.
   - **APU:** length counters, envelopes, sweep, the frame counter's 4-step and
     5-step modes and its IRQ, the DMC and the cycles it steals, and the mixer.
     Each is run with the NTSC and the PAL tables.
   - **DMA:** OAM DMA's 513 and 514 cycles.
   - **Controllers:** the strobe and the shift order.
   - **Cartridge and mappers:** the header parser, and each mapper's banking,
     mirroring and IRQ.
3. **Community test ROMs from the fork.** The plan picks the exact list from
   `instr_test-v5`, `instr_timing`, `cpu_interrupts_v2`, `ppu_vbl_nmi`,
   `ppu_sprite_hit`, `ppu_sprite_overflow`, `apu_test`, `apu_mixer`,
   `dmc_dma_during_read4`, `mmc3_test_2` and `pal_apu_tests`, which the fork has
   and which is the PAL sound check. Most of Blargg's ROMs are NTSC-only. The
   plan lists which run on PAL, and where a PAL timing has no ROM the chip tests
   above hold it, with a figure from the nesdev wiki written beside each. The
   test reads the result byte at
   `$6000` and the text from `$6004`, so it does not read the machine's own
   verdict about itself. A ROM that fails is listed in
   `docs/known-differences.md` with its cause. It is not skipped silently.
4. **Boot check of the bundled homebrew.** The headless machine runs a set
   number of frames, and the frame buffer's hash is compared with a recorded one.
   The recording was looked at by eye first, and its command is in the journal.
   The hash is test output, not a typed claim (rule 5).
5. **Real-browser check.** `browser-check.mjs` grows to load the NES page in
   headless Chrome, run frames, check for no console errors and the headroom
   line, and load a test ROM through the picker.

**A mutation pass follows**, as the KIM-1 and BBC Micro had: break one thing at a
time and confirm a test fails. Anything that survives gets a test or an entry in
`docs/known-differences.md`.

**Speed.** The PPU alone is about 5.4 million dots a second at full speed. The
first milestone is a headless machine, built before the rest, whose speed is
measured in the browser, and the plan records the figure. If headroom is thin,
the fix is a cheaper per-cycle path, and Dan is told before any change.

## The browser host

It uses the shared `site/public/machine-host.js` from the BBC Micro's work. The
NES adds its own panel script.

- **Screen.** A 256 by 240 canvas filled from the PPU's frame buffer at the
  region's rate (about 60.1 Hz NTSC, 50.0 Hz PAL), in the 8:7 pixel shape that
  gives 4:3. The page says it models the picture and is not a CRT.
- **Region.** A control chooses NTSC or PAL. It starts from the ROM's header
  where the header names a region, and the page says which it chose and why.
  Changing it restarts the machine.
- **Sound.** The APU's mixer output is resampled in the machine to 48 kHz and
  played by an `AudioWorklet`. Sound is off until the visitor clicks. CI can
  check the samples, not the audio.
- **Input.** Keyboard and the Gamepad API, for two pads.
- **ROM picker.** A visitor drops a `.nes` file on the page. Nothing is uploaded.
- **Start button.** The WebAssembly downloads on click, not on page load. Its
  size is measured early, as the issue about the KIM-1's size asks.

## Registry and the page

The row has `core: "2a03"` and `acceptance` naming the acceptance test. Its
`rights` field is written from the section above. The registry test already
fails a running machine without a credited photograph, so the first pull
request carries one: a freely licensed photograph of an original console, with
its author, source and licence, fetched and recorded as the others were.

## The 3D models

In scope, and **handed to another agent as their own project**: the NES is
cased, so it gets two models, the outside and the board, as the BBC Micro did
(see [its models design](2026-10-04-bbc-micro-models-design.md) and the KIM-1's
journal). That agent writes the NES's models design and plan first, in the same
form, and Dan approves them before any model is built.

- **Second pull request, after the machine's.** Models never hold up a count
  (Dan, 2 October). Until both models are built the registry row claims none, and
  the registry rules already stop a cased machine claiming half.
- **Inputs first.** The models are built from credited, freely licensed
  photographs and scans, with no downloaded model. The agent's task 0 checks what
  such material exists for the NES, and stops and tells Dan if it is not good
  enough, as the BBC Micro's task 0 did. The machine still counts.
- **Which console.** A model is of one console, and the page says which. The
  chips that differ by region (2A03 or 2A07, 2C02 or 2C07) are named in the
  legend for both.
- **Machine state.** Which state each model shows, and the host calls it needs
  (such as a power LED, the reset button, and the chips' recent activity), are
  that design's to settle. This machine's host exposes nothing for it in the
  first pull request.

## Settled in the plan, not here

- **The sub-cycle order** of the PPU's three dots against the CPU's access, and
  where within the cycle the CPU's read of a PPU register is sampled.
- **The exact test-ROM list** and the pinned commit, with which ROMs run on PAL.
- **The PAL fourth-dot pattern** and its reset alignment, and the PAL tables
  (noise periods, DMC rates, frame counter steps), each from the nesdev wiki with
  its page named in the fact sheet.
- **The bundled homebrew's title**, from the rights pass.
- **The resampler**: a box filter or a band-limited step, chosen by measurement.
- **The sample and frame budget** the browser host runs per animation frame.

## How the work lands

- **Spec and plan** in `docs/superpowers/` in this repository, the plan with
  every code block compiled and run before it goes in.
- **Build** with a fresh agent per task, each reviewed before the next starts.
- **Journal** an entry per working day in `docs/journal/`, in the same pull
  request as the work (rule 6 in `AGENTS.md`).
- **Pull request:** one for the machine, the host and the page. Nothing merges
  until `Validate` is green, and the live count changes only with Dan's go.
- **Rights:** `roms/README.md` gets the homebrew's entry. `AGENTS.md`'s layout
  and `docs/known-differences.md` are updated as the code arrives.
- **Left-out parts** become issues the day this spec merges, and are not started.

## Out of scope

The Dendy, the Famicom Disk System, expansion audio, the Zapper and other
peripherals, and unlicensed mappers. The NES-on-a-chip plug-and-play clones are
separate rows and separate specs. PAL and the 3D models are in scope, above.
