#!/usr/bin/env python3
"""Plants one fault at a time in the KIM-1 teletype's sources, runs the tests
that should notice, and puts the file back. Prints one line a fault: its id,
what it does, and the tests that failed. A fault that no test fails on is
reported as SURVIVED.

    python3 tools/probes/kim1_teletype_mutations.py            # every fault
    python3 tools/probes/kim1_teletype_mutations.py M5 S2      # chosen faults
    python3 tools/probes/kim1_teletype_mutations.py --list

M faults are in the machine and run the Kim1 test project (dotnet test, about
a minute each). S faults are in the page and run site/tests/machine-page.test.mjs
and html.test.mjs, after `npm run build` where the fault is in the panel's
markup. Needs the .NET SDK and, for S, node and `npm ci` in site. Each
replacement must match exactly once, or the script stops. The working tree is
restored after each fault; run it on a clean tree, and do not edit the files
while it runs. The results from 13 October 2026 are in
docs/journal/2026-10-08-the-kim-1-teletype.md.
"""
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
T = 'src/Dbhq.Machines.Kim1/Kim1Teletype.cs'

# (id, what it does, file, old text, new text, rebuild the site first)
FAULTS = [
    ('M1', 'the printer samples at the start of each bit, not the middle', T,
     '_receiveStart + ((2L * n + 1) * 1_000_000 / (2L * Baud))', '_receiveStart + (n * 1_000_000L / Baud)'),
    ('M2', "the keyboard's eighth bit is a space, not a mark", T,
     '((((_sending & 0x7F) | 0x80) >> (_sendBit - 1)) & 1) != 0', '((((_sending & 0x7F)) >> (_sendBit - 1)) & 1) != 0'),
    ('M3', 'the keyboard sends one stop bit, not two', T,
     'public const int SendBits = 11;', 'public const int SendBits = 10;'),
    ('M4', 'the printer keeps a character whose stop bit is a space', T,
     '            if (mark)\n            {\n                char c', '            if (mark || _receiveByte >= 0)\n            {\n                char c'),
    ('M5', 'the keyboard does not wait for a quiet line', T,
     'public const int DefaultQuietBits = 4;', 'public const int DefaultQuietBits = 0;'),
    ('M6', 'PB5 high no longer holds PA7 at a mark', T,
     'public bool Pa7 => _pb5 || KeyboardMark;', 'public bool Pa7 => KeyboardMark;'),
    ('M7', 'PB0 low no longer holds the printer at a space', T,
     'bool mark = _pb0 && Pa7;', 'bool mark = Pa7;'),
    ('M8', 'a short space is taken as a start bit', T,
     '            if (mark)\n            {\n                // Back to a mark by the middle', '            if (mark && _receiveByte < 0)\n            {\n                // Back to a mark by the middle'),
    ('M9', 'a key queued between cycles can start in the past (the guard in NextStart removed)', T,
     '_receiving || _queuedAt == NextCycle)', '_receiving)'),
    ('M10', 'reading the paper stamps a queued key with the last cycle run (CatchUp back to Advance)', 'src/Dbhq.Machines.Kim1/Kim1Machine.cs',
     'Bus.Teletype.CatchUp(Bus.Cycles);', 'Bus.Teletype.Advance(Bus.Cycles);'),
    ('M11', 'teletype keys are not held back for the keypad taps before them', 'src/Dbhq.Machines.Kim1/Kim1Keystrokes.cs',
     'if (_held.Length > 0 && _down is null && _pending.Count == 0 && now >= _until)', 'if (_held.Length > 0)'),
    ('M12', 'the TTY switch is read on the wrong decoder output', 'src/Dbhq.Machines.Kim1/Kim1Bus.cs',
     'Mode == Kim1Mode.Teletype && row == 3', 'Mode == Kim1Mode.Teletype && row == 4'),
    ('M13', 'the quiet timer ignores edges on the printer loop', T,
     '        _lastOutputEdge = cycle;\n', ''),
    ('M14', "the quiet timer ignores where the printer's receiver finished", T,
     '        _receiverIdleSince = cycle;\n', ''),
    ('M15', 'replacing the terminal loses what the old one had printed', 'src/Dbhq.Machines.Kim1/Kim1Machine.cs',
     '            _carried = ReadTeletype();\n', ''),
    ('S1', 'parseTeletype checks the key table with in, not Object.hasOwn', 'site/src/lib/machines.mjs',
     '!Object.hasOwn(TELETYPE_KEYS, m[1])', '!(m[1] in TELETYPE_KEYS)', False),
    ('S2', 'the paper gives out its open line as if it were complete', 'site/public/teletype-paper.js',
     'this.lines.length - 1).map((l) => l.trimEnd())', 'this.lines.length).map((l) => l.trimEnd())', False),
    ('S3', 'the paper is left live (aria-live off removed)', 'site/public/kim-1.js',
     "  paperEl.setAttribute('aria-live', 'off');\n", '', False),
    ('S4', 'the page does not push the switch and speed into the machine at start', 'site/public/kim-1.js',
     "  kim.SetTeletypeBaud(Number(baud.value));\n  kim.SetTeletypeMode(mode.checked);\n", '', False),
    ('S5', 'the speed lets the browser restore it (autocomplete removed)', 'site/src/components/Kim1Panel.astro',
     '<select data-kim1-baud autocomplete="off" disabled>', '<select data-kim1-baud disabled>', True),
    ('S6', 'the polite region for completed lines is missing', 'site/src/components/Kim1Panel.astro',
     '    <p class="sr-only" aria-live="polite" data-kim1-line></p>\n', '', True),
    ('S7', 'a repeated line is written without clearing the region first', 'site/public/teletype-paper.js',
     '    if (this.shown) {\n      this.write(\'\');\n      this.shown = false;\n    } else if (this.waiting.length > 0) {',
     '    if (this.waiting.length > 0) {', False),
]


def run(command, cwd):
    p = subprocess.run(command, cwd=cwd, capture_output=True, text=True)
    return p.stdout + p.stderr


def main(argv):
    if '--list' in argv:
        for f in FAULTS:
            print(f[0], f[1], f[2])
        return 0
    chosen = [a for a in argv if not a.startswith('-')]
    for fault in FAULTS:
        fid, what, path, old, new = fault[:5]
        rebuild = fault[5] if len(fault) > 5 else False
        if chosen and fid not in chosen:
            continue
        full = os.path.join(ROOT, path)
        original = open(full).read()
        if original.count(old) != 1:
            sys.exit(f'{fid}: the text to replace matches {original.count(old)} times in {path}, not once')
        open(full, 'w').write(original.replace(old, new))
        try:
            if fid.startswith('M'):
                text = run(['dotnet', 'test', 'tests/Dbhq.Machines.Kim1.Tests', '-c', 'Release', '--logger', 'console;verbosity=normal'], ROOT)
                failed = sorted(set(re.findall(r'^\s*Failed (\S+)', text, re.M)))
                if 'error CS' in text:
                    failed = ['DID NOT BUILD']
            else:
                site = os.path.join(ROOT, 'site')
                if rebuild:
                    run(['npm', 'run', 'build'], site)
                text = run(['node', '--test', 'tests/machine-page.test.mjs', 'tests/html.test.mjs'], site)
                failed = sorted(set(re.findall(r'^✖ (.*?) \(\d', text, re.M)))
        finally:
            open(full, 'w').write(original)
        print(fid, what, '->', ('SURVIVED' if not failed else f'{len(failed)} failed: ' + '; '.join(failed)), flush=True)
    if any(len(f) > 5 and f[5] for f in FAULTS):
        run(['npm', 'run', 'build'], os.path.join(ROOT, 'site'))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
