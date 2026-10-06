# The interrupt differential

A check that a change to the core changed nothing it does, aimed at the interrupt lines. It runs
random memory as code on every CPU variant, toggles the IRQ and NMI lines at random inside bus
accesses and between steps, resets now and then, and hashes every bus access and the CPU's state
after every step. Two builds that behave the same print the same 61 lines.

It was written in the review of task 17 (the journal entry
[`docs/journal/2026-10-06-the-core-speed.md`](../../docs/journal/2026-10-06-the-core-speed.md)
has its results). Task 17 takes the poll snapshot once an instruction, which relies on a rule:
an instruction that changes the I flag after its last bus access must call `FreezePoll` first,
as CLI, SEI and PLP do. The interrupt tests cover those three; this differential would also
catch a future instruction that breaks the rule.

## How to run it

From the repository root:

```sh
dotnet run -c Release --project bench/interrupt-differential -- 3000000 > after.txt   # steps per configuration
```

Each of the 60 configurations runs that many steps. 3 million (180 million steps in all) is what
the review ran; 1 million is enough to see a change.

To compare with an older commit, export it with `git archive` into a folder of its own, copy this
folder into the same place in the export, and run it there the same way:

```sh
mkdir ../before && git archive <commit> | tar -x -C ../before
cp -r bench/interrupt-differential ../before/bench/
(cd ../before && dotnet run -c Release --project bench/interrupt-differential -- 3000000 > ../before.txt)
cmp ../before.txt after.txt
```

The files must be identical. It needs nothing downloaded. It is a local check, not run in CI.
