"""The Atari 2600 plan's task 6 fault pass: plant one fault at a time in the TIA's objects, their
position counters, priority, score mode, the collision latches and their state reports, in a
scratch copy outside the repository, build, run the whole Atari 2600 test project and print which
test files failed, with the number of failing cases. A fault no test file catches shows as
SURVIVED. X1 and X2 replant task 5's RSYNC faults, to see what the suite's rsync test makes of them.
Standard library only.

    python3 -I tools/probes/atari-2600/tia-object-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (R1, C4, ...) pick some faults only.
The suite's files are copied from the repository's .testdata cache when it has them, so the scratch
copy need not fetch them again. Needs dotnet on PATH (or in ~/.dotnet).

A record of its day. Its copy list predates the tests that read machines/ and docs/, so on later
code the scratch copy's baseline fails in Atari2600AcceptanceTests, BootTests, PagePathTests,
SuiteRecordedTests and SuiteSelfTests (162 cases on 15 October 2026), and a fault is read by the
files beyond those. R4 stopped matching when task 6's review changed Reset; it was ported on
15 October 2026 (the final review's fix wave) and, run alone, TiaMissileTests and TiaPlayerTests
caught it."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
D = "src/Dbhq.Machines.Atari2600/"
TIA, PF, CTR, PL, MS, BL = D + "Tia.cs", D + "TiaPlayfield.cs", D + "TiaPositionCounter.cs", D + "TiaPlayer.cs", D + "TiaMissile.cs", D + "TiaBall.cs"

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("R1 a reset holds the counter three clocks", CTR, [("    private const int ResetClocks = 4;", "    private const int ResetClocks = 3;")]),
    ("R2 a reset is colour clocks only", CTR, [("    private const int ResetMotionClocks = 2;", "    private const int ResetMotionClocks = 0;")]),
    ("R3 a reset is MOTCK clocks only", CTR, [("    private const int ResetMotionClocks = 2;", "    private const int ResetMotionClocks = 4;")]),
    ("R4 a held decode is dropped by a reset", CTR, [("        _cutLate = true;\n        if (sendsStart)", "        _cutLate = true;\n        _held = None;\n        if (sendsStart)")]),
    ("R5 RESBL sends no start", BL, [("_counter.Reset(sendsStart: true);", "_counter.Reset(sendsStart: false);")]),
    ("R6 RESP0 and RESP1 send a start", PL, [("_counter.Reset(sendsStart: false);", "_counter.Reset(sendsStart: true);")]),
    ("D1 the close copy decoded a count late", CTR, [("        3 => Close,", "        4 => Close,")]),
    ("D2 the far copy decoded a count late", CTR, [("        15 => Far,", "        16 => Far,")]),
    ("D3 NUSIZ %110 without its far copy", CTR, [("        0b110 => 0b110,", "        0b110 => 0b010,")]),
    ("D4 NUSIZ %101 with a close copy", CTR, [("        0b110 => 0b110,\n", "        0b110 => 0b110,\n        0b101 => 0b001,\n")]),
    ("D5 the main copy decoded a count early", CTR, [("        Counts - 1 => Main,", "        Counts - 2 => Main,")]),
    ("D6 NUSIZ read at the decode, not at the boundary", CTR,
     [("        _start = held == Main || (held != None && (copies & (1 << (held - Close))) != 0);", "        _start = held != None;"),
      ("        _held = Decode(_count);\n    }", "        _held = Decode(_count);\n        if (_held > Main && (copies & (1 << (_held - Close))) == 0) { _held = None; }\n    }")]),
    ("P1 double width gated on quarters 0 and 2", PL, [("        0b101 => (phase & 1) != 0,", "        0b101 => (phase & 1) == 0,")]),
    ("P2 quad width gated on quarter 0", PL, [("        0b111 => phase == 1,", "        0b111 => phase == 0,")]),
    ("P3 REFP ignored", PL, [("int bit = (refp & 0x08) != 0 ? _scan : 7 - _scan;", "int bit = 7 - _scan;")]),
    ("P4 VDELP ignored", PL, [("byte graphics = (vdelp & 0x01) != 0 ? _seenOld : _seenNew;", "byte graphics = _seenNew;")]),
    ("P5 GRP seen on the clock it lands", PL, [("byte graphics = (vdelp & 0x01) != 0 ? _seenOld : _seenNew;", "byte graphics = (vdelp & 0x01) != 0 ? _old : _new;")]),
    ("P6 GRP0 copies into player 0's own old register", TIA, [("                _player0.WriteGraphics(value);\n                _player1.CopyNewToOld();", "                _player0.WriteGraphics(value);\n                _player0.CopyNewToOld();")]),
    ("P7 GRP1 does not copy the ball's enable", TIA, [("                _ball.CopyNewToOld();\n", "")]),
    ("P8 the scan counter stops at the line's end", PL, [("        bool lockPoint = false;\n", "        bool lockPoint = false;\n        if (!motionClock) { _scanning = false; }\n")]),
    ("P9 a new start does not restart the scan", PL, [("            if (_counter.TakeStart())\n", "            if (_counter.TakeStart() && !_scanning)\n")]),
    ("L1 RESMP locks at scan %100", PL, [("else if (_scan == 2 && _scanMain)", "else if (_scan == 4 && _scanMain)")]),
    ("L2 RESMP locks on every copy", PL, [("else if (_scan == 2 && _scanMain)", "else if (_scan == 2)")]),
    ("L3 RESMP does not hide the missile", MS, [("if ((enam & 0x02) == 0 || (resmp & 0x02) != 0)", "if ((enam & 0x02) == 0)")]),
    ("W1 width 2 draws one pixel", MS, [("        1 => counter.Start && counter.Phase < 2,", "        1 => counter.Start && counter.Phase < 1,")]),
    ("W2 width 8 draws four", MS, [("        _ => counter.Start || counter.StartBefore,", "        _ => counter.Start,")]),
    ("W3 the ball's width from NUSIZ0", TIA, [("        if (_ball.Output(r[Vdelbl], r[Ctrlpf]))", "        if (_ball.Output(r[Vdelbl], r[Nusiz0]))")]),
    ("C1 two latches of CXM0P swapped", TIA, [("(M0Bit, P1Bit, 1), (M0Bit, P0Bit, 0),", "(M0Bit, P1Bit, 0), (M0Bit, P0Bit, 1),")]),
    ("C2 the latches stopped by the horizontal blank", TIA, [("        if (!_vblank)\n        {\n            _collisions", "        if (!_vblank && !_hblank)\n        {\n            _collisions")]),
    ("C3 VBLANK does not stop the latches", TIA, [("        if (!_vblank)\n        {\n            _collisions", "        if (_vblank || !_vblank)\n        {\n            _collisions")]),
    ("C4 CXCLR clears nothing", TIA, [("            case Cxclr:\n                _collisions = 0;\n", "            case Cxclr:\n")]),
    ("C5 no BL-PF latch", TIA, [("            (BlBit, PfBit, 13),\n", "")]),
    ("C6 the latches miss what player 0 and missile 0 hide", TIA, [("            _collisions |= CollisionsOf[lit];", "            _collisions |= CollisionsOf[(lit & (P0Bit | M0Bit)) != 0 ? lit & ~(P1Bit | M1Bit) : lit];")]),
    ("Q1 CTRLPF D2 ignored", TIA, [("        if ((ctrlpf & 0x04) != 0)", "        if ((ctrlpf & 0x40) != 0)")]),
    ("Q2 score mode ignored", TIA, [("        else if (pf && (ctrlpf & 0x02) != 0)", "        else if (pf && (ctrlpf & 0x00) != 0)")]),
    ("Q3 score mode's halves swapped", TIA, [("rightHalf ? P1Bit : P0Bit", "rightHalf ? P0Bit : P1Bit")]),
    ("Q4 player 1 in front of player 0", TIA, [("        if ((lit & (P0Bit | M0Bit)) != 0)\n        {\n            return Colup0;", "        if ((lit & (P0Bit | M0Bit)) != 0 && (lit & (P1Bit | M1Bit)) == 0)\n        {\n            return Colup0;")]),
    ("Q5 score mode's half by pixel 79", PF, [("        _nextRight = count >= RightStart;", "        _nextRight = count >= RightStart - 1;")]),
    ("M1 the objects clocked in the blank", TIA, [("        bool motionClock = !_hblank;", "        bool motionClock = true;")]),
    ("X1 RSYNC restarts the line four clocks after the write", TIA, [("                _phase = 1;\n", "                _phase = 0;\n")]),
    ("X2 RSYNC at four clocks and WSYNC released a clock later", TIA,
     [("                _phase = 1;\n", "                _phase = 0;\n"),
      ("            if (_counter == 0)\n            {\n                _rdyLow = false;\n            }\n\n            _playfield.Show();\n        }\n        else if (_phase == 2)",
       "            _playfield.Show();\n        }\n        else if (_phase == 1 && _counter == 0)\n        {\n            _rdyLow = false;\n        }\n        else if (_phase == 2)")]),
    ("X3 RSYNC's line does not release WSYNC", TIA,
     [("            if (_counter == 0)\n            {\n                _rdyLow = false;\n            }\n", "            if (_counter == 0 && !_rsyncLine)\n            {\n                _rdyLow = false;\n            }\n\n            _rsyncLine = false;\n"),
      ("            _rsync = false;\n            _counter = 0;\n", "            _rsyncLine = _rsync;\n            _rsync = false;\n            _counter = 0;\n"),
      ("    private bool _rsync;\n", "    private bool _rsync;\n    private bool _rsyncLine;\n"),
      ("        sink.Add(nameof(_rsync), _rsync);\n", "        sink.Add(nameof(_rsync), _rsync);\n        sink.Add(nameof(_rsyncLine), _rsyncLine);\n")]),
    ("S1 the player does not report its scan", PL, [("        sink.Add(nameof(_scan), _scan);\n", "")]),
    ("S2 the counter does not report its held decode", CTR, [("        sink.Add(nameof(_held), _held);\n", "")]),
    ("S3 the playfield does not report its half", PF, [("        sink.Add(nameof(_right), _right);\n", "")]),
    ("S4 the TIA does not report the ball", TIA, [("        sink.Add(nameof(_ball), _ball);\n", "")]),
]

def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def failed_files(stdout):
    names = re.findall(r"^\s*Failed Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.", stdout, re.M)
    return collections.Counter(names)


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-tia-object-faults-")
    only = set(args)
    if os.path.abspath(dst).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    os.makedirs(dst)
    for item in ["6502.slnx", "Directory.Build.props", ".editorconfig", "src/Dbhq.Cpu6502", "src/Dbhq.Machines.Atari2600",
                 "tests/Dbhq.Cpu6502.TestSupport", "tests/Dbhq.Machines.Atari2600.Tests", ".testdata/atari-2600"]:
        s, d = os.path.join(SRC, item), os.path.join(dst, item)
        if not os.path.exists(s):
            continue
        if os.path.isdir(s):
            shutil.copytree(s, d, ignore=shutil.ignore_patterns("bin", "obj"))
        else:
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(s, d)
    env = dict(os.environ, PATH=os.path.expanduser("~/.dotnet") + ":" + os.environ["PATH"])
    proj = "tests/Dbhq.Machines.Atari2600.Tests"
    test = f"timeout 1200 dotnet test -c Release --no-build {proj} --logger 'console;verbosity=normal'"
    out = sys.stdout
    r = run(f"dotnet build -c Release {proj}", cwd=dst, env=env)
    out.write(f"baseline build rc={r.returncode}\n")
    r = run(test, cwd=dst, env=env)
    out.write(f"baseline: {sum(failed_files(r.stdout).values())} failed ({r.stdout.strip().splitlines()[-1] if r.stdout.strip() else ''})\n")
    out.flush()
    survived = []
    for fid, rel, pairs in FAULTS:
        if only and fid.split()[0] not in only:
            continue
        full = os.path.join(dst, rel)
        text = open(full).read()
        planted = text
        missing = [old for old, _ in pairs if planted.count(old) != 1]
        if missing:
            out.write(f"{fid}: PATTERN NOT FOUND ONCE\n")
            out.flush()
            continue
        for old, new in pairs:
            planted = planted.replace(old, new)
        open(full, "w").write(planted)
        try:
            r = run(f"dotnet build -c Release {proj}", cwd=dst, env=env)
            if r.returncode != 0:
                errs = sorted(set(re.findall(r"error \w+", r.stdout)))
                out.write(f"{fid}: BUILD FAILED {errs}\n")
                continue
            r = run(test, cwd=dst, env=env)
            files = failed_files(r.stdout)
            if r.returncode == 124:
                files["(timed out)"] += 1
            if files:
                out.write(f"{fid}: CAUGHT by " + ", ".join(f"{k} ({v})" for k, v in sorted(files.items())) + "\n")
            else:
                survived.append(fid)
                out.write(f"{fid}: SURVIVED\n")
        finally:
            open(full, "w").write(text)
            out.flush()
    out.write(f"\nSurvived: {', '.join(survived) if survived else 'none'}\nDONE\n")


main()
