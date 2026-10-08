#!/usr/bin/env python3
"""Shows that the NES differential can fail: plants one fault at a time in a scratch copy of the
repository, builds the differential there, runs it with --check against a baseline, counts the runs
that differ and which hashes, and puts the file back.

    python3 bench/nes-speed/differential/faults.py <scratch copy> <baseline file> [--threads n]
        [--tests <filter>] [--no-differential] [fault ...]

The scratch copy is a copy of the repository outside it, so the repository is never changed, for
example `rsync -a --exclude bin --exclude obj --exclude .git --exclude site --exclude node_modules
--exclude .testdata ./ /tmp/nes-faults/` and `ln -s "$PWD/.testdata" /tmp/nes-faults/.testdata`.
With fault names, only those run. Each fault is one exact replacement, or a list of them, each of
which must match once. --tests also runs the NES tests whose names match the filter (dotnet test's
--filter) with each fault in, and prints how many failed; --no-differential runs only those.
"""
import collections
import pathlib
import subprocess
import sys

# The batch faults' common part: whether the catch-up running is a batch of more than four dots.
BATCH = ('PpuCatchUp.cs', """    private long _logicalDots;
""", """    private bool _batch;
    private long _logicalDots;
""")
BATCH_SET = ('PpuCatchUp.cs', """        while (_caughtUpDots < _logicalDots)
        {
            _caughtUpDots++;
            RunDot();
        }""", """        _batch = _logicalDots - _caughtUpDots > 4;
        while (_caughtUpDots < _logicalDots)
        {
            _caughtUpDots++;
            RunDot();
        }""")
BATCH_SKIP = ('PpuState.cs', """        sink.Skip(nameof(_catchUpAt), "worked out from the state (ScheduleEvents)");""", """        sink.Skip(nameof(_catchUpAt), "worked out from the state (ScheduleEvents)");
        sink.Skip(nameof(_batch), "a planted fault's");""")


def skip(field):
    """A planted fault's own field, skipped in the PPU's report, so the reflection check passes and no hash changes."""
    return ('PpuState.cs', """        sink.Skip(nameof(_nextEventDot), "worked out from the state (ScheduleEvents)");""", f"""        sink.Skip(nameof({field}), "a planted fault's");
        sink.Skip(nameof(_nextEventDot), "worked out from the state (ScheduleEvents)");""")

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

    # Faults of the kind a fast scanline renderer could make (task 3a, before the renderer): each
    # acts only inside a catch-up of more than four dots, which the lazy build makes and the per-dot
    # reference never does (it catches up two dots at most at a time), so the scene tests, which
    # hold the lazy PPU to the per-dot one, can see them; and each leaves the state as the right
    # code leaves it once the catch-up goes on past the dot, so only a point inside the window
    # sees it.
    ('batch-pixel-one-off', [BATCH, BATCH_SET, BATCH_SKIP, ('Ppu.cs', """        // Column dot - 2 is decided from the shifters as they stand, then they shift.
        DrawPixel(dot - 2);
        Shift();
        switch (dot & 7)""", """        // Column dot - 2 is decided from the shifters as they stand, then they shift.
        if (_batch && dot == 130)
        {
            int at = (_line << 8) | 128;
            uint stale = _pixels[at];
            DrawPixel(128);
            _pixels[at + 1] = _pixels[at];
            _pixels[at] = stale;
            _held = true;
        }
        else if (_held && dot == 131)
        {
            int at = (_line << 8) | 128;
            _pixels[at] = _pixels[at + 1];
            _held = false;
            DrawPixel(129);
        }
        else
        {
            DrawPixel(dot - 2);
        }

        Shift();
        switch (dot & 7)"""), ('Ppu.cs', """    private const int VblankLine = 241;
""", """    private const int VblankLine = 241;
    private bool _held;
"""), skip('_held')], "in a batch, on dot 130 of a visible line, column 128's pixel written one pixel off, into column 129, and put right on dot 131, so the picture is the same once dot 131 has run"),
    ('batch-sprite0-hit-late-low', [BATCH, BATCH_SET, BATCH_SKIP, ('Ppu.cs', """    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawPixel(int x)
    {
        int pixel = 0;""", """    private bool _hitLate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawPixel(int x)
    {
        if (_hitLate)
        {
            _hitLate = false;
            _status |= StatusSprite0;
        }

        int pixel = 0;"""), ('Ppu.cs', """            if ((sprite & SpriteIsSprite0) != 0 && pixel != 0 && x != 255)
            {
                _status |= StatusSprite0;
            }""", """            if ((sprite & SpriteIsSprite0) != 0 && pixel != 0 && x != 255)
            {
                if (_batch && _oam[0] > 120 && (_status & StatusSprite0) == 0)
                {
                    _hitLate = true;
                }
                else
                {
                    _status |= StatusSprite0;
                }
            }"""), skip('_hitLate')], "in a batch, with sprite 0's y above 120, sprite 0 hit set a dot late, on the next pixel"),
    ('batch-pal-prerender-337-early', [BATCH, BATCH_SET, BATCH_SKIP, ('Ppu.cs', """            FetchBackground(dot);
        }
        else if (dot == 337)
        {
            Shift();
            Reload();
        }""", """            FetchBackground(dot);
            if (_batch && dot == 336 && !visible && _lines == 312)
            {
                Shift();
                Reload();
                _early = true;
            }
        }
        else if (dot == 337)
        {
            if (!_early)
            {
                Shift();
                Reload();
            }

            _early = false;
        }"""), ('Ppu.cs', """    private const int VblankLine = 241;
""", """    private const int VblankLine = 241;
    private bool _early;
"""), skip('_early')], "in a batch, on PAL's pre-render line, dot 337's shift and reload done at the end of dot 336, so they are a dot early and the same once dot 337 has run"),
    ('batch-pal-prerender-340-early', [BATCH, BATCH_SET, BATCH_SKIP, ('Ppu.cs', """        if (++_dot == Region.DotsPerLine)
        {""", """        if (++_dot == 340 && _batch && _line == _preRenderLine && _lines == 312)
        {
            _oddFrame = !_oddFrame;
            _toggled = true;
        }

        if (_dot == Region.DotsPerLine)
        {"""), ('Ppu.cs', """        _frame++;
        _oddFrame = !_oddFrame;""", """        _frame++;
        _oddFrame ^= !_toggled;
        _toggled = false;"""), ('Ppu.cs', """    private const int VblankLine = 241;
""", """    private const int VblankLine = 241;
    private bool _toggled;
"""), skip('_toggled')], "in a batch, on PAL's pre-render line, the odd frame flag turned as the PPU reaches dot 340 rather than with the frame's end after it"),
]

OLD = {'steps', 'cycles', 'trace', 'sound', 'dropped', 'memory', 'cpu', 'result'}
FORMAT_2 = OLD | {'points', 'ppu', 'apu', 'board', 'lines'}


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
    tests = None
    if '--tests' in args:
        i = args.index('--tests')
        tests = args[i + 1]
        del args[i:i + 2]
    differential = '--no-differential' not in args
    args = [a for a in args if a != '--no-differential']
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
            if tests is not None:
                run_tests(root, name, tests)
            if not differential:
                continue
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
            only_pixels = 0
            synthetic = 0
            for k in differ:
                changed = [f for f in base[k] if base[k][f] != got[k].get(f)]
                by.update(changed)
                only_new += not (set(changed) & OLD)
                only_pixels += not (set(changed) & FORMAT_2)
                synthetic += k.startswith('synthetic/')
            roms = sorted({k.rsplit(' ', 1)[0] for k in differ})
            crashed = sorted(k for k in differ if 'result' in got[k] and got[k]['result'].startswith('crashed'))
            print(f'{name} ({what}): {len(differ)} of {len(base)} runs, {len(roms)} ROMs, {synthetic} of the runs synthetic; '
                  f'only the new state hashes: {only_new}; only the pixels at the points: {only_pixels}; by hash: {dict(sorted(by.items()))}', flush=True)
            print('   e.g. ' + ', '.join(roms[:10]), flush=True)
            for k in crashed:
                print(f'   {k}: {got[k]["result"]}', flush=True)
        finally:
            for done, original in reversed(saved):
                done.write_text(original)


def run_tests(root, name, filter):
    """The NES tests matching the filter, with the fault in: how many ran and how many failed."""
    ran = subprocess.run(['dotnet', 'test', 'tests/Dbhq.Machines.Nes.Tests', '-c', 'Release', '--filter', filter],
                         cwd=root, capture_output=True, text=True)
    summary = [l.strip() for l in ran.stdout.splitlines() if 'Failed:' in l and 'Passed:' in l]
    if not summary:
        sys.exit(f'{name}: the tests did not run\n{ran.stdout[-3000:]}{ran.stderr[-2000:]}')
    failed = sorted({l.split('Failed ', 1)[1].split(' [', 1)[0] for l in ran.stdout.splitlines() if l.strip().startswith('Failed Dbhq')})
    print(f'{name}: unit tests {filter}: {summary[-1]}', flush=True)
    for test in failed:
        print(f'   failed: {test}', flush=True)


main()
