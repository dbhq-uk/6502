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
tests/                      # the tests and the library they share
bench/                      # the speed benchmarks, run locally and not in CI (native, and the browser speed check)
tools/                      # scripts that make test data and check assumptions
  probes/                   # scripts that check assumptions against test data
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

**3. Nothing third-party is committed.** No ROM, no game, no test data and no
third-party test program. Tests download what they need from a pinned commit
and check it against a recorded hash. Some of it is GPL and this repository is
MIT.

**4. Nothing of Capcom's, ever.** No *Mega Man 2* ROM, graphics, music or code
in this repository or on the site, and the port itself lives in a separate
private repository. The project names the game and carries a non-affiliation
line; it never carries the game.

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
