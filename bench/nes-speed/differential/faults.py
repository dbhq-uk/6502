#!/usr/bin/env python3
"""Shows that the NES differential can fail: plants one fault at a time in a scratch copy of the
repository, builds the differential there, runs it with --check against a baseline, counts the runs
that differ and which hashes, and puts the file back.

    python3 bench/nes-speed/differential/faults.py <scratch copy> <baseline file> [--threads n] [fault ...]

The scratch copy is a copy of the repository outside it, so the repository is never changed, for
example `rsync -a --exclude bin --exclude obj --exclude .git --exclude site --exclude node_modules
--exclude .testdata ./ /tmp/nes-faults/` and `ln -s "$PWD/.testdata" /tmp/nes-faults/.testdata`.
With fault names, only those run. Each fault is one exact replacement, or a list of them, each of
which must match once.
"""
import collections
import pathlib
import subprocess
import sys

# Name, file under src/Dbhq.Machines.Nes, the text, its replacement, and what it does; or name, a
# list of (file, text, replacement), and what it does.
FAULTS = [
    ('bg-nametable-late', 'Ppu.cs', """            case 2:
                EvaluateEvenDot(dot);
                FetchNametableByte();
                break;

            case 3:
                EvaluateOddDot(dot);
                break;
""", """            case 2:
                EvaluateEvenDot(dot);
                break;

            case 3:
                EvaluateOddDot(dot);
                FetchNametableByte();
                break;
""", "the background's nametable byte fetched a dot late, on the third dot of each 8, not the second"),
    ('bg-reload-late', 'Ppu.cs', """            case 1:
                Reload();
                EvaluateOddDot(dot);
                break;

            case 2:
                EvaluateEvenDot(dot);
""", """            case 1:
                EvaluateOddDot(dot);
                break;

            case 2:
                Reload();
                EvaluateEvenDot(dot);
""", "the background's shifters reloaded a dot late"),
    ('sprite-eval-late', 'PpuSprites.cs', """        _oamLatch = dot <= 64 ? (byte)0xFF : _oam[(_evaluationN << 2) | _evaluationM];""",
     """        _oamLatch = dot <= 65 ? (byte)0xFF : _oam[(_evaluationN << 2) | _evaluationM];""",
     "sprite evaluation's first OAM read a dot late"),
    ('sprite0-hit-next-dot', 'Ppu.cs', """            if ((sprite & SpriteIsSprite0) != 0 && pixel != 0 && x != 255)""",
     """            if ((sprite & SpriteIsSprite0) != 0 && x >= _backgroundFrom && ((int)(_backgroundPixels >> ((14 - _x) << 2)) & 0xF) != 0 && x != 255)""",
     "sprite 0 hit tested against the next column's background pixel"),
    ('vblank-late', 'Ppu.cs', """        if (_dot == 1)
        {
            if (_line == VblankLine)""", """        if (_dot == (_line == VblankLine ? 2 : 1))
        {
            if (_line == VblankLine)""", "the VBlank flag set on dot 2 of line 241, not dot 1"),
    ('ppu-access-dot-late', 'NesBus.cs', """        int before = Math.Min(DotsBeforeAccess, dots);""",
     """        int before = Math.Min((uint)(address - 0x2000) < 0x2000 ? DotsBeforeAccess + 1 : DotsBeforeAccess, dots);""",
     "a CPU access to the PPU made after three of its cycle's dots, not two"),
    ('frame-counter-late', 'Apu.cs', """        else if (++_frameCycle == _nextStep)""", """        else if (++_frameCycle == _nextStep + 1)""",
     "every step of the frame counter a cycle late"),
    ('dmc-reload-late', 'ApuChannels.cs', """        _fetchFrom = cycle + 2;""", """        _fetchFrom = cycle + 3;""",
     "a DMC reload's fetch allowed to halt the CPU a cycle later"),
    ('mmc3-a12-late', 'PpuSprites.cs', """                Fetching(_spriteAddress);
                break;
            }

            case 5:
                _fetchLow = ReadPattern(_spriteAddress);
                break;
""", """                break;
            }

            case 5:
                Fetching(_spriteAddress);
                _fetchLow = ReadPattern(_spriteAddress);
                break;
""", "the sprite fetch's pattern address put on the PPU's bus a dot late, so MMC3 sees A12 rise a dot later"),
    ('mmc1-second-write-taken', 'Mappers/Mmc1.cs', """        bool consecutive = _sinceWrite == 1;""", """        bool consecutive = false;""",
     "MMC1 taking the write on the cycle straight after a write, which the chip ignores"),

    # The lazy PPU's own machinery (task 2 of the lazy chips plan).
    ('no-catch-up-before-cartridge-write', 'NesBus.cs', """|| (write && address >= 0x4020))""", """/* no cartridge writes */)""",
     "the PPU not caught up before a CPU write to the cartridge, $4020 to $FFFF (Ruling T)"),
    ('no-catch-up-before-2002-read', [
        ('NesBus.cs', """|| (uint)(address - 0x2000) < 0x2000u ||""", """|| ((uint)(address - 0x2000) < 0x2000u && (write || (address & 7) != 2)) ||"""),
        ('Ppu.cs', """    public byte ReadRegister(int register)
    {
        CatchUp();""", """    public byte ReadRegister(int register)
    {
        if ((register & 7) != 2)
        {
            CatchUp();
        }
"""),
    ], "the PPU not caught up before a $2002 read, by the bus or by the register read itself"),
    ('next-event-one-dot-late', 'PpuCatchUp.cs', """            next = _caughtUpDots + (target >= index ? target - index + 1 : toEnd + target + 1);""",
     """            next = _caughtUpDots + (target >= index ? target - index + 1 : toEnd + target + 1) + 1;""",
     "NextEventDot one dot late, so the bus may catch up for the NMI output a cycle late"),
    ('no-frame-end-event', 'PpuCatchUp.cs', """        _catchUpAt = Math.Min(next, _frameEndDot);""", """        _catchUpAt = next;""",
     "the frame end not an event, so a frame ends at the next catch-up rather than in its own cycle"),
]

OLD = {'steps', 'cycles', 'trace', 'sound', 'dropped', 'memory', 'cpu', 'result'}


def fields(line):
    key, _, value = line.partition(': ')
    if not value.startswith('steps='):
        return key, {'result': value}
    return key, dict(p.split('=', 1) for p in value.split(' '))


def main():
    args = sys.argv[1:]
    threads = '4'
    if '--threads' in args:
        i = args.index('--threads')
        threads = args[i + 1]
        del args[i:i + 2]
    root, baseline, only = pathlib.Path(args[0]).resolve(), pathlib.Path(args[1]).resolve(), args[2:]
    src = root / 'src/Dbhq.Machines.Nes'
    dll = root / 'bench/nes-speed/differential/bin/Release/net10.0/Dbhq.Machines.Nes.Differential.dll'
    base = dict(fields(l) for l in baseline.read_text().splitlines()[1:])
    for fault in FAULTS:
        name, what = fault[0], fault[-1]
        edits = fault[1] if isinstance(fault[1], list) else [fault[1:4]]
        if only and name not in only:
            continue
        saved = []
        for file, old, new in edits:
            path = src / file
            text = path.read_text()
            if text.count(old) != 1:
                matches = text.count(old)
                for done, original in reversed(saved):
                    done.write_text(original)
                sys.exit(f'{name}: the text matches {matches} times in {file}, not once')
            saved.append((path, text))
            path.write_text(text.replace(old, new))
        try:
            built = subprocess.run(['dotnet', 'build', 'bench/nes-speed/differential', '-c', 'Release'], cwd=root, capture_output=True, text=True)
            if built.returncode != 0:
                sys.exit(f'{name}: the build failed\n{built.stdout[-3000:]}')
            out = root / f'faults-{name}.txt'
            if out.exists():
                out.unlink()
            ran = subprocess.run(['dotnet', str(dll), '--check', str(baseline), '--out', str(out), '--threads', threads], cwd=root, capture_output=True, text=True)
            if not out.exists():
                sys.exit(f'{name}: the differential wrote nothing\n{ran.stdout[-2000:]}{ran.stderr[-3000:]}')
            got = dict(fields(l) for l in out.read_text().splitlines()[1:])
            differ = [k for k in base if base[k] != got.get(k)]
            by = collections.Counter()
            only_new = 0
            synthetic = 0
            for k in differ:
                changed = [f for f in base[k] if base[k][f] != got[k].get(f)]
                by.update(changed)
                only_new += not (set(changed) & OLD)
                synthetic += k.startswith('synthetic/')
            roms = sorted({k.rsplit(' ', 1)[0] for k in differ})
            crashed = sorted(k for k in differ if 'result' in got[k] and got[k]['result'].startswith('crashed'))
            print(f'{name} ({what}): {len(differ)} of {len(base)} runs, {len(roms)} ROMs, {synthetic} of the runs synthetic; '
                  f'only the new state hashes: {only_new}; by hash: {dict(sorted(by.items()))}', flush=True)
            print('   e.g. ' + ', '.join(roms[:10]), flush=True)
            for k in crashed:
                print(f'   {k}: {got[k]["result"]}', flush=True)
        finally:
            for done, original in reversed(saved):
                done.write_text(original)


main()
