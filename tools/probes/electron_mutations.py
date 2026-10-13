#!/usr/bin/env python3
"""Plants one fault at a time in the Electron's sources, runs the tests that
should notice, and puts the files back. Prints one line a fault: its id, what
it does, and the tests that failed. A fault that no test fails on is reported
as SURVIVED.

    python3 tools/probes/electron_mutations.py            # every fault
    python3 tools/probes/electron_mutations.py M3 M16b    # chosen faults
    python3 tools/probes/electron_mutations.py --list

Every fault runs the Electron test project (dotnet test, about a minute each).
A fault marked with the site also runs site/tests/electron.test.mjs, which
holds the page's copy of the key matrix against ElectronKey.cs. Needs the .NET
SDK and, for those, node and `npm ci` in site. A fault is one or more
replacements, each of which must match exactly once, or the script stops. The
working tree is restored after each fault; run it on a clean tree, best in a
scratch worktree outside the repository (`git worktree add ../scratch HEAD
--detach`), and do not edit the files while it runs. The faults are task 15 of
docs/superpowers/plans/2026-10-04-electron.md, mapped to the code as it is;
the results from 13 October 2026 are in
docs/journal/2026-10-13-the-electron-mutation-pass.md.
"""
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
E = 'src/Dbhq.Machines.Electron/'
TIMING = E + 'ElectronTiming.cs'
ULA = E + 'Ula.cs'
BUS = E + 'ElectronBus.cs'
KEY = E + 'ElectronKey.cs'
KEYBOARD = E + 'ElectronKeyboard.cs'
DISPLAY = E + 'UlaDisplay.cs'
SOUND = E + 'UlaSound.cs'
TAPE = E + 'UlaTape.cs'
CRC = E + 'Tape/TapeCrc.cs'
BLOCKS = E + 'Tape/TapeBlocks.cs'
READER = E + 'Tape/UefReader.cs'
WRITER = E + 'Tape/UefWriter.cs'
PAGE_KEYS = 'site/public/electron-keys.js'

# (id, what it does, [(file, old text, new text), ...], run the site's key test too)
FAULTS = [
    ('M1', 'the cost parity flipped: 3 cycles from an even start, 2 from an odd', [
        (TIMING, 'if ((boundary & 1) != 0)', 'if ((boundary & 1) == 0)')]),
    ('M2', 'the dummy read left out of RTS (its last cycle, the read at the pulled address)', [
        ('src/Dbhq.Cpu6502/Cpu.ControlFlow.cs',
         '        PC = (ushort)(lo | hi << 8);\n        Read(PC);\n        PC++;',
         '        PC = (ushort)(lo | hi << 8);\n        PC++;')]),
    ('M3', 'the window one boundary too narrow: 39 blocked boundaries, positions 2 to 78', [
        (TIMING, 'WindowLast = 80;', 'WindowLast = 78;')]),
    ('M4', 'the window one boundary too wide: 41 blocked boundaries, positions 2 to 82', [
        (TIMING, 'WindowLast = 80;', 'WindowLast = 82;')]),
    ('M5', "mode 3's two blank lines in each row contended", [
        (TIMING, '3 => line < 250 && line % 10 < 8,', '3 => line < 250,')]),
    ('M6', 'the RTC at 12,672 and 52,672, not 12,670 and 52,670', [
        (ULA, 'OddRtc = 12_670;', 'OddRtc = 12_672;'),
        (ULA, 'EvenRtc = 52_670;', 'EvenRtc = 52_672;')]),
    ('M6b', 'the odd field\'s RTC alone at 12,672', [
        (ULA, 'OddRtc = 12_670;', 'OddRtc = 12_672;')]),
    ('M7', 'the two display ends 40,000 apart (the even one at 72,736, not 72,672)', [
        (ULA, 'EvenDisplayEnd = 72_672;', 'EvenDisplayEnd = 72_736;')]),
    ('M8', 'a $FE05 write of $00 honoured while slot 8 to 11 is paged', [
        (BUS, 'if (!pagedIn8To11 || (value & 0x08) != 0)', 'if (!pagedIn8To11 || (value & 0x08) != 0 || value == 0)')]),
    ('M9', 'Caps Lock and Func split: a Func key of its own, at the free position column 0 bit 2, in the machine and the page', [
        (KEY, 'CapsLock = 0x1D,', 'CapsLock = 0x1D, Func = 0x20,'),
        (PAGE_KEYS, 'CapsLock: 0x1D,', 'CapsLock: 0x1D, Func: 0x20,')], True),
    ('M10', 'both Shifts separate: a right Shift at the free position column 2 bit 3, in the machine and the page, on the PC\'s right Shift', [
        (KEY, 'Shift = 0x3D,', 'Shift = 0x3D, ShiftRight = 0x32,'),
        (PAGE_KEYS, 'Shift: 0x3D,', 'Shift: 0x3D, ShiftRight: 0x32,'),
        (PAGE_KEYS, "ShiftRight: 'Shift',", "ShiftRight: 'ShiftRight',")], True),
    ('M11', 'the keyboard OR changed to AND across the selected columns', [
        (KEYBOARD, 'int value = 0;\n        for (int column', 'int value = 0x0F;\n        for (int column'),
        (KEYBOARD, 'value |= _columns[column];', 'value &= _columns[column];')]),
    ('M12', "the palette's negative logic dropped: a 1 turns a colour on", [
        (DISPLAY, 'if ((_palette[register] & mask) == 0)', 'if ((_palette[register] & mask) != 0)')]),
    ('M13', "mode 2's bit order swapped: bit 7 is the low bit of the logical colour, bit 1 the high", [
        (DISPLAY, '_ => (byte)((Bit(7 - k) << 3) | (Bit(5 - k) << 2) | (Bit(3 - k) << 1) | Bit(1 - k)),',
         '_ => (byte)(Bit(7 - k) | (Bit(5 - k) << 1) | (Bit(3 - k) << 2) | (Bit(1 - k) << 3)),')]),
    ('M14', "the sound's half period 16 x (S + 1), not 32 x (S + 1)", [
        (SOUND, 'CyclesPerCounterStep = 32;', 'CyclesPerCounterStep = 16;')]),
    ('M15', "the CRC's initial value $FFFF, not 0", [
        (CRC, 'int crc = 0;', 'int crc = 0xFFFF;')]),
    ('M16', 'the CRC stored low byte first, written and read so', [
        (BLOCKS, '        to.Add((byte)(crc >> 8));\n        to.Add((byte)crc);', '        to.Add((byte)crc);\n        to.Add((byte)(crc >> 8));'),
        (BLOCKS, '(ushort)((b[headerCrcAt] << 8) | b[headerCrcAt + 1])', '(ushort)(b[headerCrcAt] | (b[headerCrcAt + 1] << 8))'),
        (BLOCKS, '(ushort)((b[dataEnd] << 8) | b[dataEnd + 1])', '(ushort)(b[dataEnd] | (b[dataEnd + 1] << 8))')]),
    ('M16b', 'the CRC written low byte first and read high byte first', [
        (BLOCKS, '        to.Add((byte)(crc >> 8));\n        to.Add((byte)crc);', '        to.Add((byte)crc);\n        to.Add((byte)(crc >> 8));')]),
    ('M17', "the UEF's chunk length counted with its own 6-byte header, written and read so", [
        (READER, 'Chunk(id, bytes.Slice(dataAt, (int)length), at, events);\n            at = dataAt + (int)length;',
         'Chunk(id, bytes.Slice(dataAt, (int)length - 6), at, events);\n            at = at + (int)length;'),
        (WRITER, 'to.Write(BitConverter.GetBytes((uint)data.Length));', 'to.Write(BitConverter.GetBytes((uint)data.Length + 6));')]),
    ('M17b', "the UEF reader alone counting a chunk's length with its header", [
        (READER, 'Chunk(id, bytes.Slice(dataAt, (int)length), at, events);\n            at = dataAt + (int)length;',
         'Chunk(id, bytes.Slice(dataAt, (int)length - 6), at, events);\n            at = at + (int)length;')]),
    ('M18', 'a carrier count off by one: the writer puts one cycle more in each &0110 chunk', [
        (WRITER, '[(byte)take, (byte)(take >> 8)]', '[(byte)(take + 1), (byte)((take + 1) >> 8)]')]),
    ('M18b', 'a carrier count off by one: the reader takes one cycle more from each &0110 chunk', [
        (READER, 'Expect(id, at, data, 2);\n                AddCarrier(events, data[0] | (data[1] << 8));',
         'Expect(id, at, data, 2);\n                AddCarrier(events, (data[0] | (data[1] << 8)) + 1);')]),
    ('M18c', 'a carrier count off by one: the recorder records one cycle more of each idle stretch', [
        (TAPE, 'new Carrier((int)Math.Round(idle / (double)TapeTiming.CarrierCycleCpuCycles, MidpointRounding.AwayFromZero))',
         'new Carrier((int)Math.Round(idle / (double)TapeTiming.CarrierCycleCpuCycles, MidpointRounding.AwayFromZero) + 1)')]),
]


def run(command, cwd):
    p = subprocess.run(command, cwd=cwd, capture_output=True, text=True)
    return p.stdout + p.stderr


def dotnet():
    text = run(['dotnet', 'test', 'tests/Dbhq.Machines.Electron.Tests', '-c', 'Release', '--logger', 'console;verbosity=normal'], ROOT)
    if 'error CS' in text:
        return ['DID NOT BUILD']
    # A theory's cases are one test here: the class and the method, not the arguments.
    return sorted(set(m.split('.')[-2] + '.' + m.split('.')[-1] for m in re.findall(r'^\s*Failed ([\w.]+)', text, re.M)))


def site():
    text = run(['node', '--test', 'tests/electron.test.mjs'], os.path.join(ROOT, 'site'))
    return ['site: ' + t for t in sorted(set(re.findall(r'^✖ (.*?) \(\d', text, re.M)))]


def main(argv):
    if '--list' in argv:
        for f in FAULTS:
            print(f[0], f[1], ', '.join(sorted({r[0] for r in f[2]})))
        return 0
    chosen = [a for a in argv if not a.startswith('-')]
    for fault in FAULTS:
        fid, what, replacements = fault[:3]
        with_site = len(fault) > 3 and fault[3]
        if chosen and fid not in chosen:
            continue
        originals = {}
        try:
            for path, old, new in replacements:
                full = os.path.join(ROOT, path)
                if full not in originals:
                    originals[full] = open(full).read()
                text = open(full).read()
                if text.count(old) != 1:
                    sys.exit(f'{fid}: the text to replace matches {text.count(old)} times in {path}, not once')
                open(full, 'w').write(text.replace(old, new))
            failed = dotnet() + (site() if with_site else [])
        finally:
            for full, original in originals.items():
                open(full, 'w').write(original)
        print(fid, what, '->', ('SURVIVED' if not failed else f'{len(failed)} failed: ' + '; '.join(failed)), flush=True)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
