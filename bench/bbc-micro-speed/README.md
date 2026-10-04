# BBC Micro browser speed check

How many 2 MHz cycles per second does the BBC Micro run in a browser, with the
chips built so far? Measured in .NET WebAssembly's default interpreter mode and
compiled ahead of time (AOT). It is the same method as
[`../Dbhq.Cpu6502.WasmBench/`](../Dbhq.Cpu6502.WasmBench/README.md), with the
whole machine in place of a synthetic loop. The figures, and what they were run
on, are in the journal entry
[`docs/journal/2026-10-02-the-bbc-micro-speed.md`](../../docs/journal/2026-10-02-the-bbc-micro-speed.md).
This file is how to run it again.

## What is here

| Path | What it is |
| --- | --- |
| `../../src/Dbhq.Machines.BbcMicro.Wasm/` | The machine as a WebAssembly app: `Load`, `LoadInMode`, `Run`, `Cycles`, `ScreenRow`, `Peek`. It holds no ROMs. |
| `index.html`, `main.js` | The page. It fetches the three ROMs, boots the OS, checks the prompt is on the screen, then times the runs. |
| `run-in-browser.mjs`, `package.json` | Reads the three ROMs from `roms/bbc-micro/`, checks each against its SHA-256 in `Pins.cs`, serves them with the page and a published copy of the app on `127.0.0.1`, and runs it in a headless Chrome, a fresh launch each time. |
| `native/` | The same workload as a console program, in the solution so CI builds it. `--fingerprint` runs a scripted session (boot, a typed BASIC program that drives the user VIA, BREAK) and hashes every instruction and, every 100,000 cycles, all of RAM and every VIA register, so two builds can be shown to do the same thing. `--profile` compares the machine with the bare CPU on a flat copy of its memory. |
| `alternate.sh` | Runs two or more builds in turn, a launch of each at a time, in the browser or natively, so the shared machine's load falls on all of them, and prints each build's median. |

## The workload

Power on, run the real MOS 1.20 and BBC BASIC for 6 million cycles (three seconds
of machine time) to the prompt, check the screen reads `BBC Computer 32K`,
`Acorn DFS` (since task 12 fitted the 8271), `BASIC` and `>`, then time `Run(2_000_000)` five times. Each timed run is one
second of machine time, so the multiple of a real machine is the figure in MHz
divided by two. The machine sits at the prompt, waiting for a key, while it is
timed: that is the OS's idle loop and its 100 Hz interrupts, not a program.

The default is mode 7, the start-up links' default. Since task 8 the video ULA
draws modes 0 to 6, and since task 9 mode 7 through the teletext chip, so every
mode pays for its drawing. To time another mode give it with `?mode=N` on the
page, the sixth argument of `run-in-browser.mjs`, the third of the native
program, or `MODE=N` for `alternate.sh`.

The boot screen is mostly blank rows, so it is mode 7's easy case. The worst case
is a page where every cell is drawn: after the prompt check, `screen=dense` fills
the 1,000 bytes of mode 7 screen memory with random bytes (every control code,
double height, hold and flash included) and `screen=text` with random printable
characters, both from a fixed seed so every build times the same page. Give it
with `?screen=dense` on the page, the seventh argument of `run-in-browser.mjs`,
the fourth of the native program, or `SCREEN=dense` for `alternate.sh`, which
stops if a build does not confirm it filled the page (a build from before task
9's review round would time the boot screen instead). To compare against older
code, publish it with this bench's two `Program.cs` files, as task 9's review
round did for `76c2b2c`. Outside mode 7 the screen is pixels, so
the prompt check reads the OS's own record instead: the mode at `&0355` and the
text cursor at `&0318` and `&0319`, one column right of the `>` on row 7 (row 5 for
a build from before task 12, which has no DFS line; the page accepts either, the
native program only its own build's). The drive is empty, so the 8271 sits idle. A
build from before task 8 has no `LoadInMode`; to compare in another mode,
publish the old code with this page's `Program.cs` and `native/Program.cs`.

Since task 11 the machine has a sound chip, which makes its samples when its
buffer is read. The workload above never reads it, so the chip costs only the
writes the OS makes. A page reads it every frame: `sound=silent` reads it every
40,000 cycles (a field) through the timed runs with nothing playing, and
`sound=tone` does the same with all four channels sounding, three tones and
white noise written straight to the chip after the prompt check. Give it with
`?sound=tone` on the page, the eighth argument of `run-in-browser.mjs`, the
fifth of the native program, or `SOUND=tone` for `alternate.sh`. A build from
before task 11 has no sound and can only be timed with the default, `none`.

## How to run it

From the repository root. Needs the .NET 10 SDK with the `wasm-tools` workload,
Node, and Chrome.

```sh
dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -o bench/bbc-micro-speed/publish/interpreter
dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -p:RunAOTCompilation=true -o bench/bbc-micro-speed/publish/aot

cd bench/bbc-micro-speed
npm ci
node run-in-browser.mjs publish/interpreter 3   # folder, launches, timed cycles, boot cycles, timed runs
node run-in-browser.mjs publish/aot 3
```

`publish/` is git-ignored. `run-in-browser.mjs` uses `/usr/bin/google-chrome`;
set `CHROME_PATH` to use another. Run one build at a time, leave the machine
otherwise idle, and note `uptime` before and after. If an AOT publish follows an
interpreter publish and the runtime refuses to start, delete
`src/Dbhq.Machines.BbcMicro.Wasm/obj/Release` and publish again.

To compare two builds, publish each to its own folder under `publish/` and
alternate them; for the native bench, copy a build of `native/` into `publish/`
(it finds the ROMs by looking for `6502.slnx` above itself):

```sh
dotnet run -c Release --project native -- --fingerprint   # the same lines from two builds: the same behaviour
./alternate.sh browser 3 publish/aot-before publish/aot
./alternate.sh native 5 publish/native-before publish/native-after
```

Benchmarks are run locally. They are not run in CI.

## Measurements so far

Dated, with the command that made them; each journal entry has the full output.
Since task 8's review, `alternate.sh` prints each build's median boot time
beside its median speed: the boot, power on to the prompt, is where a slow start
shows, and the timed runs, which start at the prompt, cannot show it.

| Date | Code | AOT median | Interpreter median | Where |
| --- | --- | --- | --- | --- |
| 2 October 2026 | `2876852`, the bus and VIAs ticking every cycle | 7.13 times 2 MHz | 0.64 times | [the speed entry](../../docs/journal/2026-10-02-the-bbc-micro-speed.md) |
| 3 October 2026 | `98fe9d5`, the VIAs lazy, the bus looking only at events | 16.98 times 2 MHz (the old code 6.81 in the same session) | 2.11 times (the old code 0.60) | [the bus speed entry](../../docs/journal/2026-10-03-the-bbc-micro-bus-speed.md) |
| 3 October 2026 | task 7, the CRTC added, lazily | 17.95 and 18.83 times 2 MHz in two sets (the old code 17.64 and 18.55 in the same sets) | not measured | [the CRTC entry](../../docs/journal/2026-10-03-the-bbc-micro-crtc.md) |
| 3 October 2026 | task 8, the video ULA drawing modes 0 to 6 (`3522066`) | mode 7: 14.56 and 14.51 times 2 MHz (the old code 18.52 and 17.51); mode 1: 12.58 and 13.37 (15.53 and 16.89); mode 4: 12.53 (16.86), in sets under a load of up to 10. Two sets fell under ten times during load spikes, mode 4 at 7.66 (the old code 9.00) and mode 1 at 9.58 (15.59). Its boot, not then measured, was about 10 seconds: see the next row | not measured | [the video ULA entry](../../docs/journal/2026-10-03-the-bbc-micro-video-ula.md) |
| 3 October 2026 | task 8's review fix, the boot made cheap again | boot, power on to the prompt: natively 426 ms in mode 7 and 415 in mode 1 (the old code 400 and 432; `3522066` 10,793 and 12,492 in an earlier set); in the browser 719 and 1,004 ms (510 and 526). Timed runs as `3522066`'s within the noise of a set at a load of up to 50 | not measured | [the video ULA entry](../../docs/journal/2026-10-03-the-bbc-micro-video-ula.md), "The review round" |
| 3 October 2026 | task 9, the teletext chip drawing mode 7 | mode 7: 12.39 and 13.87 times 2 MHz (the old code 14.33 and 14.58); mode 1: 12.20 (11.92), at a load of about 5 | not measured | [the teletext entry](../../docs/journal/2026-10-03-the-bbc-micro-teletext.md) |
| 3 October 2026 | task 9's review round, the worst case: every cell of a mode 7 page drawn | dense page (random bytes): 11.86 and 10.88 times 2 MHz (the old code 14.77 and 15.17, which draws nothing in mode 7); text page: 12.02 (15.13); at a load of about 5 with the CPUs half busy | not measured | [the teletext entry](../../docs/journal/2026-10-03-the-bbc-micro-teletext.md), "The review round" |
| 3 October 2026 | task 11, the sound chip, never ticked (the standard workload never reads its buffer) | at an evening hour when the virtual machine ran at about half its morning speed (the bare CPU 15.5 to 24.5 ns a cycle against 10.5 to 11.0): 6.20 to 11.32 times 2 MHz for the old code and 7.32 to 11.11 for the new in nine alternating sets, level within the noise, the new ahead in some sets and behind in others; the ten-times check wants a quiet hour | not measured | [the sound entry](../../docs/journal/2026-10-03-the-bbc-micro-sound.md) |

Both were taken on a shared virtual machine with other work running; the
entries give the load average for each set. The 3 October interpreter set ran
while the load rose to 4.65, and its old-code median includes one launch that
load slowed to a third.
