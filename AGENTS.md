# AGENTS.md

Guidance for AI agents (and people) working in this repository.

## What this is

A cycle-accurate MOS 6502 core in C# on .NET 10, and the machines built on it:
the BBC Micro first, then the NES. Built in public, with the journey written
up at 6502.dbhq.uk.

## Layout

```
docs/superpowers/specs/   # the design; each stage gets its own spec here
docs/superpowers/plans/   # the plan for each spec
```

More arrives with the code. Keep this section true as it does.

## The constraints that must not be broken

Everything else here is a preference. These are not.

**1. Every bus access is one cycle.** The CPU does one `Read` or one `Write`
per cycle, and the machine advances its other chips inside each call. A cycle
that happens without a bus access, or a bus access that is not a cycle, is a
bug.

**2. The core knows no machine.** Nothing in the core refers to the BBC Micro,
the NES or any other machine. Machine behaviour, including stalls and slow
devices, lives in that machine's bus.

**3. Nothing third-party is committed.** No ROM, no game, no test data and no
third-party test program. Tests download what they need from a pinned commit
and check it against a recorded hash. Some of it is GPL and this repository is
MIT.

**4. Nothing of Capcom's, ever.** No *Mega Man 2* ROM, graphics, music or code
in this repository or on the site, and the port itself lives in a separate
private repository. The project names the game and carries a non-affiliation
line; it never carries the game.

**5. No figure is typed by hand.** A pass count, a percentage or a speed on the
site or in the docs comes from test output. A number that was true when it was
written and is false now is worse than no number.
