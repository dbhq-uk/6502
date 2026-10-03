# AGENTS.md

Guidance for AI agents (and people) working in this repository.

## What this is

A cycle-accurate 6502 core in C# on .NET 10, covering the NMOS 6502 and the
CMOS 65C02, and the machines built on it: the KIM-1 first, then the BBC Micro,
then the NES. Built in public, with the journey written up at 6502.dbhq.uk.

## Layout

```
6502.slnx                   # the solution
src/Dbhq.Cpu6502/           # the core: one library, no dependencies
src/Dbhq.Machines.Kim1/     # the KIM-1: its 6530s, keypad, display and bus, on the core
src/Dbhq.Machines.Kim1.Wasm/  # the KIM-1 as .NET WebAssembly, for its page on the site
src/Dbhq.Machines.BbcMicro/  # the BBC Micro Model B, on the core; its chips arrive task by task
src/Dbhq.Machines.BbcMicro.Wasm/  # the BBC Micro as .NET WebAssembly; the ROMs are given to it as bytes
machines/                   # registry.json, and per machine its "try it" program, which the page shows and the acceptance test runs
site/                       # 6502.dbhq.uk: the Astro site, its tests, and the scripts that build the machines into it
tests/                      # the tests and the library they share, one project per machine
bench/                      # the speed benchmarks (native, and the browser speed checks), run locally and never in CI; the .NET ones are in the solution, so CI builds them
tools/                      # scripts that make test data and check assumptions
  Dbhq.Cpu6502.ChipTrace/   # records the core's bus cycles for the site's chip page
  probes/                   # scripts that check assumptions against test data
  kim1-model/               # offline Python that measures the KIM-1's 3D model from photographs; its outputs are committed
roms/                       # system ROMs with their provenance and rights in roms/README.md: kim-1/, bbc-micro/
docs/bbc-micro/facts/       # the BBC Micro's fact sheets, written before its code
docs/superpowers/specs/     # the design; each stage gets its own spec here
docs/superpowers/plans/     # the plan for each spec
docs/journal/               # the record of how it was built; the site's source
docs/known-differences.md   # where the core knowingly differs from a reference
docs/the-6502-family.md     # every 6502-family chip and machine, and what runs
```

More arrives with the code. Keep this section true as it does.

## The constraints that must not be broken

Everything else here is a preference. These are not.

**1. Every bus access is one cycle.** The CPU does one `Read` or one `Write`
per cycle, and the machine advances its other chips inside each call. A cycle
that happens without a bus access, or a bus access that is not a cycle, is a
bug.

**2. The core knows no machine.** Nothing in the core refers to the KIM-1, the
BBC Micro, the NES or any other machine. Machine behaviour, including stalls
and slow devices, lives in that machine's bus.

**3. Nothing depends on a third party's server, and what is too big or too
restricted to commit is forked.** Dan, 2 October 2026: CI must not depend on
external sources. System ROMs are committed under `roms/`, with where each came
from and what is known of its rights in `roms/README.md` (rule 4). Test data and
programs that are large, or GPL (this repository is MIT), are not committed: the
tests download them from a pinned commit, check a recorded hash, and the source is
**a repository in the `dbhq-uk` organisation**, a fork of the original (Harte's
SingleStepTests, Dormann's tests, `nestest`, `perfect6502`). A new input is
committed or forked first, then pinned. `site/tests/mirrors.test.mjs` fails on any
fetch from another host. Games and commercial software are never committed. This
covers data inputs; package registries and GitHub Actions are a separate question,
pinned and locked as before.

**4. A machine's system ROM is used when its rights are documented.** Dan, 1 October 2026:
"if it's documented we do it", for every machine. The documentation is the
machine's `rights` field in `machines/registry.json` and a section in its
journal entry: who holds the ROM, where our copy comes from (the original URL and a
sha256), and what is known about permission, including when nothing could be
found. The ROM is committed under `roms/` and checked against its hash every time
it is read. **Commercial games and application
software are still not bundled; they are load-your-own.** The site says plainly,
on each machine's page, whose ROM it runs.

**5. No figure is typed by hand.** A pass count, a percentage or a speed that
describes the project as it stands comes from test output. A number that was
true when it was written and is false now is worse than no number. A journal
entry may quote a measurement from its own day, dated, with the command that
produced it, because that is a record of the day rather than a claim about
now.

**6. Document as you go.** Every decision, finding, surprise and mistake goes
into [`docs/journal/`](docs/journal/README.md) in the same pull request as the
work: what was decided and what it was chosen over, what was checked and how,
what was assumed and turned out wrong. The journal is the source for
6502.dbhq.uk, and the site cannot be written from anything that was not
written down at the time. A difference from a reference also goes in
[`docs/known-differences.md`](docs/known-differences.md).
