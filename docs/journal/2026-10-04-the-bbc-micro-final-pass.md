---
title: "The BBC Micro's final pass"
date: 2026-10-04
summary: "The last task of the BBC Micro plan: faults planted across the seams between the chips to see which tests notice, the known differences gathered and checked against the code, the minor findings the reviews deferred cleared or set out, and the browser speed measured again on a quiet machine. Then the branch in summary."
order: 26
---

# 4 October 2026: the BBC Micro's final pass

Task 15 of the BBC Micro plan, before the whole branch goes to a final review.
Four parts: a mutation pass across the seams between the chips, the known
differences consolidated, the minor findings the reviews deferred, and the speed
in the browser measured again on a quiet machine, as Dan was told it would be.
The last section is the branch in summary, the source for the site's entry.

## The mutation pass

Each task's implementer ran a mutation table for its own chip. This pass did not
repeat them. It planted faults where per-chip tables do not look: where one
chip's output is another's input, where the lazy bus decides when to look, and
in the page's own code. One fault at a time, in a scratch copy outside the
repository (`git archive HEAD | tar -x -C /tmp/t15m`, `.testdata` linked, a
throwaway git there to reset between faults), then the BBC test project, or the
site's tests for the page's code. The narrowest classes that should notice ran
first; a fault that survived them ran against the whole project.

```
cd /tmp/t15m && <apply one fault>
dotnet build tests/Dbhq.Machines.BbcMicro.Tests -c Release -v q
dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release --no-build --filter "<classes>"   # or no filter
git checkout -- .                                                                           # reset
cd site && node --test tests/<file>.test.mjs                                                # the page's code
```

| # | Fault planted | Caught by | Tests failing |
| --- | --- | --- | --- |
| 1 | The 1 MHz stretch's parity flipped: `1 + ((T + 1) & 1)` | `BbcBusTests` (every stretch test); the boot tests did not notice | 21 |
| 2 | Timer 1 a count off after a free-running reload in the lazy closed form (`_t1Latch - rest` for `- (rest - 1)`) | `ViaEquivalenceTests` (three), `Via6522Tests` (the one-shot reload) | 4 |
| 3 | The keyboard's column and row swapped in the system VIA's matrix read | `SystemViaTests` (the OS's key test), `BasicTests`, `BootTests` (the start-up links) | 118 |
| 4a | The CRTC's extra adjust line in an interlaced even field dropped (R5 not incremented) | `Crtc6845Tests` (625 lines, 40,000 cycles a field), `CrtcEquivalenceTests` | 11 |
| 4b | The half-line VSYNC in interlace dropped | `Crtc6845Tests`, `CrtcEquivalenceTests` | 10 |
| 5 | The palette's XOR 7 dropped | `BootTests` (white on black in every mode), `BasicTests` (VDU 19, flash), `VideoUlaTests` | 63 |
| 6 | One attenuation level wrong (level 7 as level 6) | `Sn76489Tests` (the volume table) | 1 |
| 7 | The 8271 moving a byte every 64 cycles, not 128 | `Fdc8271Tests`, `DiscTests` (DFS sees late data) | 10 |
| 8 | The side select bit (`$23` bit 5) inverted | `Fdc8271Tests`, `DiscTests` | 15 |
| 9a | `Service()` asks the system VIA for its next event before bringing the CRTC up to date | **survived**, the whole project | 0 |
| 9b | The system VIA no longer brings the CRTC up to date itself (`SyncInputs` emptied) | `BusEquivalenceTests` (random accesses) | 1 |
| 9c | Both of 9a and 9b | `BusEquivalenceTests` | 1 |
| 10 | VSYNC delivered to CA1 the wrong way up | `BusEquivalenceTests` (four), `FdcEquivalenceTests`; not by the CRTC's own in-machine test, until it was strengthened (below) | 5 |
| 11 | The stage 1 core fix reverted: an NMI on a vector read lost again | `TransistorModelTests` (13 of the 266 model runs), `DiscTests` (the `*TITLE` case) | 13 and 1 |
| 12 | The odd field's row offset in the framebuffer dropped | `BasicTests`, `BootTests`, `TeletextTests`, `VideoUlaTests` | 66 |
| 13 | The host's cap of a tenth of a second of machine time a frame removed | `machine-host.test.mjs` (the stall and slow-browser tests) | 2 |
| 14 | Two keys (Q and W) swapped in the on-screen key table | **survived** the existing tests; caught by a new test (below) | 0, then 1 |
| 15 | The OS ROM's pinned SHA-256 changed by one character | `RomTests`, `BootTests`, every test that boots | 41 of 43 run |
| 16 | The BBC Micro photograph's author blanked in the registry | `registry.test.mjs` ("the real registry is valid") | 1 |
| 17 | A boot screen row changed in `BootScreen.Rows` | `BootTests`, `BasicTests` | 18 |
| 18 | Teletext red and green swapped | `TeletextTests`, `VideoUlaEquivalenceTests`, `TeletextAdversarialEquivalenceTests` | 12 |
| 19 | The 40 or 80 track threshold off by one (`<` for `<=`) | `DiscImageTests` | 2 |
| 20 | A sample dropped by a full buffer not counted as an overrun | `SoundEquivalenceTests`, `SoundAdversarialEquivalenceTests` | 22 |
| 20b | The audio worklet's queue cap removed | `bbc-micro.test.mjs` (the queue is cut back) | 1 |
| 21 | The screen start latch's two bits swapped | `SystemViaTests`, `VideoUlaTests`, `BasicTests` (the scroll and the wrap) | 11 |
| 22 | The sound chip's write taken on latch bit 0 rising, not falling | `SoundTests`, `SystemViaTests` | 9 |
| 23 | The ULA not drawn up to a change of the screen start latch | `VideoUlaEquivalenceTests`, `BlackRowStressTests`, `TeletextAdversarialEquivalenceTests` | 7 |
| 24 | The bus not looking at the chips after an 8271 data read | `FdcEquivalenceTests`, `DiscTests` | 6 |
| 25 | A row 0 key (SHIFT, CTRL) raising CA2 in the autoscan | `BbcKeyboardTests`, `SystemViaTests` | 5 |
| 26 | A key held for a tenth of the page's hold | `BbcAcceptanceTests` (both typed-through-the-queue tests); not `BbcKeyPressesTests` | 2 |
| 27 | BREAK resetting the system VIA too | `BootTests` (BREAK's banner), `SystemViaTests` | 2 |
| 28 | The teletext pipeline two characters, not three | `TeletextTests`, `BootTests`, both mode 7 equivalence tests | 12 |

The plan's fault "R7 not incremented in the interlace case" has no single line
to break in this CRTC, which does not add to R7; 4a and 4b are the two
adjustments it does make for interlace.

Two survived the tests as they stood, and one more was caught only by the oracle.

**9a survived, and cannot be caught, because it changes nothing.** The bus brings
the CRTC up to date before asking the system VIA anything, but the VIA also does
it itself, every time it is asked (its `SyncInputs`), so the two orders behave
the same. The bus's order is a second guard on the same seam. Removing the VIA's
own guard (9b) is caught. This is recorded in `docs/known-differences.md`, in the
new bus section, so nobody hunts for the test that cannot exist.

**14 survived: the on-screen keys could be in any order.** The table was checked
sorted against the machine's keys, and the page test compares the rendered page
with the same table. A new test in `bbc-micro.test.mjs` pins each row to the
Model B's keyboard as the page's own photograph shows it, left to right; with Q
and W swapped it fails.

**10 was caught only by comparing with the oracle.** The CRTC's in-machine test
checked the gaps between vsync interrupts, and an edge taken the wrong way up
keeps the gaps. The test now also checks that VSYNC was high before the read
that first sees IFR1 and low after it; with the fault it fails in all five of its
cases.

The core fix (11) was checked in the core's own tests too:
`dotnet test tests/Dbhq.Cpu6502.Tests -c Release --filter "FullyQualifiedName~Interrupt"`
gave 13 failures of 281 with the old `EnterHandler`, all of them the model runs
added with the fix. The 20,000-command disc soak of task 12 was scratch and is
not in the repository; the committed `*TITLE` test is the guard that stands in
for it.

## Known differences, consolidated

`docs/known-differences.md` was checked against the list the plan gives and the
list the task added, entry by entry. Four entries were added:

- **The bus and the memory map,** which had none: the 1 MHz clock's power-on
  phase, the empty ROM slot's value, the absent devices' values, `$FE18` taken
  as slow, the ROM latch and RAM at power on, and the mutant above.
- **An NMI that comes while an interrupt reads its vector:** the core fix, why it
  is listed though it now matches the transistor model, and the tests that hold
  it.
- **The 8271's register mirror:** `$FE88-$FE9F` repeating the first eight.
- **A disc changed on the page:** DFS keeps the catalogue it last read until the
  drive stops or `*CAT`, as on a real BBC, and the page does not force it.

Everything else on the lists was already there: shift modes other than 010,
timer 1 after a one-shot expiry, the video pipeline latency, screen memory read
at line granularity, the 1 MHz half-character, the RA3 gate, the ULA's reset,
the tone period 0, a write taking effect when latch bit 0 falls, the `.dsd`
layout, short images, the teletext choices (release a cell after, `$80` and
`$90`, double height, separated gaps, the three-character delay, the LOSE
latch), and the 8271's start delay, `$FE82` and `$FE83` reading `$FE`, and BREAK
leaving it alone.

**The consistency check.** Every `[guessing]`, `[guessing - verify]`, "not known",
"assumption" and "no source" in `src/Dbhq.Machines.BbcMicro` was read
(`grep -n -i "assum\|guess\|verify\]\|not known\|nobody\|no source" *.cs`), and
every named constant (`grep -n "const "`). Each has an entry. Three had none
before this pass, all in `BbcBus`: the clock's phase, `$FE18`, and the empty
slot; they are the new bus section. Two kinds of constant need none: the CRTC's
`QuickSteps` and `DeepSteps`, which say how far ahead it predicts and change no
behaviour (the equivalence tests hold it to the per-character oracle), and the
facts taken from a sheet rather than chosen (`ByteCycles`, `RevolutionCycles`,
`ClockRate`, `ShiftRegisterSeed`, the disc geometry). Two guesses in the VIAs'
code were not tagged in it and now are: IC32 at power on, and the user VIA's CB1
and CB2 idling high.

## The deferred minors

Every `minor (deferred` and `carry` line in the plan's ledger was read. Done, in
three groups:

- **Wording made true** (`d298ed8`): the absent-device values in the plan entry;
  the empty ROM slot (the OS does read its header, and fails it); the DFS's
  service calls go first to its DFS half only while bit 7 of `$028F` is set,
  in the boot entry, `bus.md` and `BootTests`; the fingerprint's BREAK and `RUN`
  (a soft BREAK leaves BASIC with no program, so that `RUN` runs nothing; the
  script is unchanged so the hashes stand); "7 to 12 per cent" with the reviewer's
  four fingerprint lines; the shift register's 19 cycles counted in pixels, not
  read with a ruler; the stage 1 plan's listing marked superseded; the bench
  README naming the two dense sets under ten times; the bench's comment naming
  `run-in-browser.mjs`; `alternate.sh`'s usage line put back together; two long
  lines wrapped.
- **Tests** (`d298ed8`): task 5's tracer findings pinned (`$028D` 1, `$028E`
  `$80`, `$0277` `$FF`, `$028F` bit 7) and the OS's clock counting a hundred ticks
  a second; the coincident acknowledge for a T1C-H write, an IFR write and timer
  2; the free-running timer's flag read before it is acknowledged; the
  read-modify-write's total of seven. The reviewers' scratch checks, kept and
  trimmed to run in seconds: the bus with outside inputs, peeks, resets and long
  gaps (two seeds); the video ULA's black-row flags checked against the picture
  (two seeds); mode 7 heavy programs (four seeds); the sound chip at the edges of
  its sample rates (four rates).
- **Code** (`d298ed8`, `6e55f93`): `Via6522`'s members for its subclasses
  `private protected`; the bench's silence check skips a last field with no
  samples; the photograph's lights described where they are (below the bottom
  left of the keyboard, to the left of the space bar, checked on the photograph);
  "credited" for one photograph; the about page on photographs with no licence;
  the BBC page test asking for both names in the running sentence rather than
  today's wording; a latched on-screen SHIFT or CTRL let go when the screen takes
  the keyboard and at Break; the drop message saying to press Start only before
  Start; the host's meter reset on resume tested; the build script's tests run
  with a fake `dotnet` first on the path, and the read-before-publish test made
  behavioural (a spoilt ROM stops the script before `dotnet` runs); the site's
  test floor in the plain style.

**Not done, and why:**

- `Via6522._catchingUp` is not reset in a `finally`. It matters only after an
  exception thrown inside a catch-up, which is a bug that leaves the machine
  unusable anyway, and a `try` on the catch-up path is a change to the hot path
  after the speed was measured.
- `SystemVia.VsyncInput` stays public with no guard. `SystemViaTests` drives CA1
  through it on a machine's bus on purpose, and its summary says a level set
  there is overwritten at the CRTC's next edge. A guard means redesigning that
  test.
- Task 7's test ideas: a bus test that reaches `StateAt`'s jump branch, a
  standalone 1 MHz mode for the CRTC equivalence test, comparing the cycle each
  event carries, the handler-writes-the-chip throw, and the mode 7 parity
  assertion. Each is test design, and the behaviour is held by the equivalence
  tests. The "dead branch" at `CrtcEquivalenceTests.cs:548` could not be found:
  the file is 239 lines now.
- `BbcBus.Irq` stays public. Harmless, and the reviewer said so.
- The on-screen keys on a phone, which wrap to about 710 pixels tall at 390
  wide, so the screen and the keys cannot be seen together. That is design work.
- The deploy workflow checks that every machine file is serving after it has
  published, so a failure turns the run red without stopping the publish. A
  change to the order of the deploy is Dan's to make.

Two ledger items needed no change: the CRTC's remark that a write to the ULA's
registers brings the CRTC up to the write's cycle has been true since task 8,
whose `DrawTo` asks the CRTC for its state up to that cycle; and the host test's
assertion message at `machine-host.test.mjs:315` reads as a description of the
failure, like the file's others.

## The speed on a quiet machine

Task 11 measured the browser speed at an evening hour when the shared virtual
machine ran at about half its morning speed, and several sets fell under ten
times a 2 MHz machine. Dan was told it would be measured again on a quiet
machine. This is that measurement.

**The builds,** each compiled ahead of time from an exported copy of its commit
with `dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release
-p:RunAOTCompilation=true`, .NET SDK 10.0.400, in the same headless Chrome as
before: the branch as it stands (`6e55f93`; what was committed after it changes
only tests and documents); `fb7a0fb`, the commit before the
video ULA, which draws nothing, published with task 9's bench host so it can
fill the dense page; and `85ce6c5`, the end of task 6b, before any chip after
the VIAs, for context. Sound and the dense page need the newer host, so the
dense page with all four channels sounding was timed on the branch alone.

**Waiting for quiet.** The machine is shared with other work, and on this
Sunday morning it was busy almost all the time: from 03:46 to 09:18 UTC the
one-minute load stood mostly between 3 and 30, and at 08:05 the CPUs were 93 to
97 per cent busy. Sets taken in that time are kept in the task's report and not
used here. Each set below was started only when the one-minute load was under 2
and at least 85 per cent of the CPU time had been idle over the five seconds
before it, and the load was read again when it ended. All but the last came
between 09:18 and 09:53 UTC.

```
MODE=7 ./alternate.sh browser 3 publish/aot-t15-final publish/aot-t15-fb7 publish/aot-t15-b85   # the order turned each set
MODE=1 ./alternate.sh browser 3 publish/aot-t15-final publish/aot-t15-fb7
SCREEN=dense ./alternate.sh browser 3 publish/aot-t15-final publish/aot-t15-fb7
SCREEN=dense SOUND=tone ./alternate.sh browser 3 publish/aot-t15-final
```

Each set is three launches of each build, five timed runs a launch. Multiples of
a 2 MHz BBC Micro (the median in MHz divided by two):

| Workload | Set, UTC | Load before, after | The branch | `fb7a0fb` | `85ce6c5` |
| --- | --- | --- | --- | --- | --- |
| Mode 7, the boot screen | 09:18 | 1.85, 3.24 | 13.61 | 17.36 | 17.01 |
| | 09:40 | 1.47, 1.68 | 13.85 | 18.25 | 18.80 |
| | 09:43 | 1.47, 1.68 | 13.62 | 17.76 | 18.73 |
| Mode 1 | 09:29 | 1.78, 3.19 | 13.40 | 15.38 | |
| | 09:41 | 1.34, 1.80 | 13.74 | 18.21 | |
| | 09:43 | 1.70, 1.63 | 13.97 | 16.89 | |
| Mode 7, the dense page | 09:40 | 1.64, 1.65 | 10.46 | 17.99 | |
| | 09:41 | 1.73, 2.86 | 11.27 | 17.95 | |
| | 09:52 | 1.30, 2.97 | 12.03 | 18.73 | |
| The dense page, all four channels sounding | 09:41 | 1.54, 1.58 | 11.56 | | |
| | 09:43 | 1.45, 1.47 | 10.53 | | |
| | 10:31 | 1.96, 2.48 | 9.47 | | |

The last set waited until 10:31 for a quiet moment, and the five-minute load
was still 4.00 when it started.

Every run of each build in those sets, taken together:

| Workload | Build | Runs | Median | Best | Slowest | Boot, median |
| --- | --- | --- | --- | --- | --- | --- |
| Mode 7, the boot screen | the branch | 45 | 13.62 | 16.26 | 10.46 | 377 ms |
| | `fb7a0fb` | 45 | 17.70 | 19.88 | 12.47 | 249 ms |
| | `85ce6c5` | 45 | 18.55 | 20.04 | 15.08 | 188 ms |
| Mode 1 | the branch | 45 | 13.55 | 15.60 | 8.17 | 376 ms |
| | `fb7a0fb` | 45 | 17.27 | 20.08 | 12.90 | 257 ms |
| Mode 7, the dense page | the branch | 45 | 11.43 | 13.26 | 8.27 | 422 ms |
| | `fb7a0fb` (draws nothing in mode 7) | 45 | 18.28 | 20.49 | 13.64 | 240 ms |
| The dense page with sound | the branch | 45 | 10.46 | 12.69 | 7.80 | 390 ms |

The boot is power on to the prompt, 6 million cycles, three seconds of machine
time, and includes the runtime warming up.

**What it says.** On a quiet machine the BBC Micro runs in the browser at a
median of about 13.6 times a real 2 MHz machine on the boot screen and in mode
1, and about 11.4 times on the worst mode 7 page, every cell drawn; with all four
sound channels sounding as well, about 10.5 times. **That last workload is at
the line, not clear of it:** one of its three sets had a median of 9.47 times,
and its slowest run was 7.80. Every other set's median is over ten. Single runs
are under ten on the busier pages: the slowest of mode 1 and of the dense page
were 8.17 and 8.27 times, inside sets that started quiet, so they are the host's
own jitter as much as load. A set at 9.47 times still runs the machine more
than nine times as fast as a real one, and the page runs it at its own 2 MHz, so
it only has to keep up. The next thing to try is the per-cell drawing in the
teletext path, which is what separates the dense page (11.4) from the boot
screen (13.6) here. The video ULA and the teletext chip cost
about a quarter of the speed on the boot screen (13.6 against 17.7 for the code
before them, at 0.77) and nearly two fifths on the dense page (0.63). The figures
of task 11's evening, 7.3 to 11.1 times for its code, were the host, as that
entry said: the branch, with the disc and the page added since, runs at 13.6
here.

## The branch, in summary

**What was built.** The BBC Micro Model B, on the same core as the KIM-1,
running the real operating system (MOS 1.20), BBC BASIC and Acorn DFS 1.20
from ROMs committed with their provenance and checked against pinned hashes
every time they are read. Its parts, each written from the datasheets and the
four fact sheets in `docs/bbc-micro/facts/`: the bus, with the memory map and
the 1 MHz stretch; the two 6522 VIAs, wired as the board wires them, and the
keyboard matrix; the 6845 CRTC; the video ULA, drawing modes 0 to 6; the
SAA5050 teletext chip for mode 7, on a CC0 glyph table checked against the
datasheet's figure; the SN76489 sound chip and its sample buffer; and the 8271
floppy controller with `.ssd` and `.dsd` images. The chips run lazily: the bus
looks at them only at the events they work out ahead, which is what makes the
machine fast enough for a browser. The KIM-1 and the BBC Micro now share one
browser host, and the BBC Micro has its page: the screen, the PC keyboard and
on-screen keys, sound, a disc drive, the parts left out, and a credited
photograph of an original machine. It counts on the home page.

**The proof, in layers.**

1. **Chip tests** from the fact sheets' worked examples and the datasheets'
   numbers, one chip at a time.
2. **Oracles.** Each lazy chip is compared with a plainly written model that
   does one cycle, character or clock at a time (the VIAs, the CRTC, the video
   ULA with the teletext chip, the sound chip, the 8271), and the whole bus with
   an oracle bus, access by access, under random programs and under the real OS.
3. **The real ROMs.** The machine boots to the prompt in all eight modes, read
   off the picture, not off memory; BASIC programs run; DFS saves, loads,
   catalogues and deletes on a disc image.
4. **The transistor-level model** for the core's change: an NMI held through an
   interrupt's vector read, found through the 8271, checked against runs of the
   Visual6502 model.
5. **The acceptance test** the registry names for the machine, and the browser
   check, which drives the built page in a real Chrome: the prompt, typing, the
   on-screen keys, sound on and off, a disc in and saved out, BREAK.
6. **Planted faults.** Each task's own table, and the pass above across the
   seams. One fault this pass planted cannot be seen by any test because it
   changes nothing (the bus's order of syncing, guarded twice); one could not be
   seen and now can (the order of the on-screen keys).

**What is knowingly not exact** is in `docs/known-differences.md`, each with why
and how the tests treat it. The largest: screen memory is read a line at a time
and register writes take effect from the next character, not the cycle; the
video ULA's pipeline is taken to have no delay; the VIA's shift register has
only the mode the DFS uses; the 8271's timing is a fixed delay and a fixed byte
rate, with no rotation; and where a source stops (power-on values, the 1 MHz
clock's phase, several teletext behaviours) the model names its guess. The
oracles pin the model, including its choices, not the hardware.

**What is not done.** The 6850 serial port, the analogue-to-digital converter
and joystick port, the Tube, the cassette interface, the printer port and the
1 MHz bus are not modelled, and the page says so, each with its issue
([#33](https://github.com/dbhq-uk/6502/issues/33) to
[#38](https://github.com/dbhq-uk/6502/issues/38)). The on-screen keys do not
yet suit a phone. The minors under "Not done, and why" above stand. The two 3D
models of the BBC Micro, outside and inside, are a second pull request
([#28](https://github.com/dbhq-uk/6502/issues/28)).

**Headline measurements**, 4 October 2026. The whole solution's tests,
`dotnet test -c Release` (07:25 to 07:41 UTC): the core 1,597 passed, the KIM-1
39, the BBC Micro 958, none failed. The site, `cd site && npm run build && npm
test` (05:23 UTC): 261 passed, none failed. The browser check, `npm run
browser-check`, passed for both machines. The speed in the browser, compiled
ahead of time, on a quiet machine (09:18 to 10:31 UTC, the section above): a
median of 13.6 times a 2 MHz BBC Micro on the boot screen and in mode 1, 11.4 on
a mode 7 page with every cell drawn, and 10.5 with all four sound channels
sounding as well, where one set of three fell to 9.47.
