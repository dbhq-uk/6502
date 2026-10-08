# Thread CPU time a cycle

How many nanoseconds of its own thread's CPU time one emulated cycle costs: the core alone, and
each machine. Written for task 17, the core's speed; the figures it gave are in the journal entry
[`docs/journal/2026-10-06-the-core-speed.md`](../../docs/journal/2026-10-06-the-core-speed.md).
This file is how to run it again.

The machine this project is measured on is shared. The wall clock counts the time other work
held the processor, so on a busy day it can say a build is half as fast as it is. The thread's
CPU time (`CLOCK_THREAD_CPUTIME_ID`) leaves that out. It does not leave out the slowdown from
sharing a core's caches with other work, so only figures taken in the same minutes, with the
builds run in turn, can be compared.

## The workloads

| Workload | What runs | Cycles a run |
| --- | --- | --- |
| `core` | Dormann's functional test on a flat 64 KB bus, NMOS, from its start to its trap | the whole test |
| `bbc` | the BBC Micro, mode 7, at the prompt after 6 million cycles | 4 million |
| `kim` | the KIM-1 monitor after RS and 2 million cycles | 10 million |
| `ntsc`, `pal` | the NES running SNOW after 5 million cycles | 1.79 million |
| `ntsc-oracle`, `pal-oracle` | the same with `NesOptions.PerDotReference`: the PPU caught up every cycle, as the per-dot build ran it (the lazy chips, task 2); it stops with a message on a commit from before the option | 1.79 million |
| `lan-ntsc`, `lan-pal` | the NES running the bundled homebrew, Lan Master, from `roms/nes/`, at its title after 5 million cycles | 1.79 million |

Dormann's test and SNOW come from the pinned forks and are checked against their hashes; the
ROMs of the BBC Micro and the KIM-1, and Lan Master, are the committed ones, Lan Master checked
against its hash. One untimed run comes first. The
line printed is:

```
<workload> ns/cycle best=<x> median=<x> all=<every run>
```

## How to run it

From the repository root, on Linux:

```sh
dotnet run -c Release --project bench/thread-time -- core 5      # workload, timed runs
```

To compare with an older commit, export it with `git archive` into a folder of its own, copy
this folder into the same place in the export, link `.testdata/` there, and build both:

```sh
dotnet build bench/thread-time -c Release -o <before>/out          # in the export
dotnet build bench/thread-time -c Release -o <after>/out           # here
for round in 1 2 3 4; do
  for build in <before>/out <after>/out; do
    dotnet $build/Dbhq.Machines.ThreadTime.dll core 5
  done
done
```

Task 17 took the median of each build's four medians. The output folder must sit inside its
repository, because the program finds the ROMs by looking for `6502.slnx` above itself.

Benchmarks are run locally. They are not run in CI.
