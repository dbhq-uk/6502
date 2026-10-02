# bbc-dfs-trace

A throwaway Python BBC Micro that was written to find out what the DFS 1.20 ROM
needs from the Intel 8271 floppy controller, before any C# was written: a 6502
interpreter (`cpu.py`), the machine (`bbc.py`), an 8271 written from the Intel
datasheet (`fdc8271.py`), and the scenarios (`s1.py` to `s14.py`) that produced
the figures in [`docs/bbc-micro/facts/disc.md`](../../../docs/bbc-micro/facts/disc.md).

It is evidence, not a deliverable: nothing in `src/` is derived from it, and it
is not run in CI. It reads the three ROMs from `roms/bbc-micro/`, or from the
folder in `$BBC_ROMS`. Run a scenario from this folder, for example
`python3 s1.py`.

Written from the datasheet and the ROMs. Nothing was copied from another
emulator.
