# 6502

A cycle-accurate MOS 6502 core in C#, and the machines built on it: the KIM-1
first, then the BBC Micro, then the NES.

This is built in public. The design, the plan and every step of the work are
in this repository, and the journey is written up at
[6502.dbhq.uk](https://6502.dbhq.uk/), which is not live yet.

## Where this is going

The end goal is a verified port of *Mega Man 2*, checked against the original
by running both side by side. That needs an NES accurate enough to trust, which
needs a cycle-accurate 6502 under it. The design is in
[`docs/superpowers/specs/2026-09-29-6502-design.md`](docs/superpowers/specs/2026-09-29-6502-design.md).

*Mega Man* is a trademark of Capcom Co., Ltd. This project is not affiliated
with, endorsed by or sponsored by Capcom. Nothing of Capcom's is in this
repository.

## Where it stands

Design. There is no code yet.

## What "cycle-accurate" means here

The 6502 does exactly one read or one write on its bus every clock cycle. The
core reproduces every one of them, in order, including the reads whose result
the chip throws away. The other chips in a machine run in step with it, one
cycle at a time.

That is checked, not claimed: the core is tested against Tom Harte's
SingleStepTests, which record the bus activity of every opcode cycle by cycle.

## Licence

MIT. See [`LICENSE`](LICENSE). No ROM, no game and no third-party test program
is committed to this repository; the tests download what they need, pinned by
hash.
