"""The Atari 2600 plan's task 5 fault pass: plant one fault at a time in the TIA's line, the
playfield, the palette, the frame, the order inside a cycle and the state reports, in a scratch
copy outside the repository, build, run the whole Atari 2600 test project and print which test
files failed, with the number of failing cases. A fault no test file catches shows as SURVIVED.
Standard library only.

    python3 -I tools/probes/atari-2600/tia-line-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (L1, P4, ...) pick some faults only.
The suite's files are copied from the repository's .testdata cache when it has them, so the scratch
copy need not fetch them again. Needs dotnet on PATH (or in ~/.dotnet)."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
D = "src/Dbhq.Machines.Atari2600/"
BUS, TIA, PF, PAL, FB, REG = D + "Atari2600Bus.cs", D + "Tia.cs", D + "TiaPlayfield.cs", D + "TiaPalette.cs", D + "FrameBuffer.cs", D + "Region.cs"

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("L1 the blank ends on LRHB, at 76", TIA, [("        if (state == Rhb)\n", "        if (state == 0b010111)\n")]),
    ("L2 the line ends a count early", TIA, [("    private const int Shb = 0b010100;", "    private const int Shb = 0b101001;")]),
    ("L3 RSYNC restarts the line four clocks after the write", TIA, [("                _phase = 1;\n", "                _phase = 0;\n")]),
    ("L4 RSYNC does nothing", TIA, [("                _rsync = true;\n", "")]),
    ("L5 WSYNC released as the counter wraps, before a write landing at 228", TIA,
     [("            if (_counter == 0)\n            {\n                _rdyLow = false;\n            }\n\n", ""),
      ("            _rsync = false;\n            _counter = 0;\n", "            _rsync = false;\n            _rdyLow = false;\n            _counter = 0;\n")]),
    ("L6 VBLANK's gate with no clock of delay", TIA, [("byte colour = _hblank || _vblank ?", "byte colour = _hblank || (_registers[Vblank] & 0x02) != 0 ?")]),
    ("L7 VBLANK blanks nothing", TIA, [("byte colour = _hblank || _vblank ?", "byte colour = _hblank ?")]),
    ("L8 bit 0 kept in the frame", TIA, [("_frame.Put((int)x, (byte)(colour & 0xFE));", "_frame.Put((int)x, colour);")]),
    ("L9 a frame ends at the start of VSYNC", TIA, [("if ((old & 0x02) != 0 && (value & 0x02) == 0)", "if ((old & 0x02) == 0 && (value & 0x02) != 0)")]),
    ("P1 PF0's bit order reversed", PF, [("_bits = (_bits & ~0x0000F) | (value >> 4);", "_bits = (_bits & ~0x0000F) | (Reverse(value) & 0xF);")]),
    ("P2 PF1 not reversed", PF, [("(Reverse(value) << 4)", "(value << 4)")]),
    ("P3 PF2 reversed", PF, [("(value << 12)", "(Reverse(value) << 12)")]),
    ("P4 reflect ignored", PF, [("cell = _reflect ? Cells - 1 - (count - RightStart) : count - RightStart;", "cell = count - RightStart;")]),
    ("P5 a cell's bit read a clock earlier", TIA, [("        else if (_phase == 2)\n", "        else if (_phase == 1)\n")]),
    ("P6 the right half starts on CNT itself", PF, [("    private const int RightStart = 37;", "    private const int RightStart = 36;")]),
    ("F1 a new line's row not cleared", FB, [("        _drawing.AsSpan(_row * Width, Width).Clear();\n", "")]),
    ("F2 the line limit at 399", FB, [("        if (++_row == MaxLines)\n", "        if (++_row == MaxLines - 1)\n")]),
    ("F3 the window one line low", FB, [("int source = region.FirstShownLine + row;", "int source = region.FirstShownLine + row + 1;")]),
    ("F4 rows below the frame not black", FB, [("            if (source >= _lines)\n", "            if (source >= MaxLines)\n")]),
    ("F5 the line VSYNC ends on left in the old frame", FB, [("        _lines = _row;\n", "        _lines = carry ? _row + 1 : _row;\n")]),
    ("C1 the PAL signal on NTSC", REG, [("hueAngle: TiaPalette.NtscHueAngle);", "hueAngle: TiaPalette.PalHueAngle);")]),
    ("C2 the NTSC step 24 degrees", PAL, [("(hue - 1) * 360.0 / 14)", "(hue - 1) * 24.0)")]),
    ("C3 the saturation without the contrast scale", PAL, [("20.0 / 92.5 / 0.63;", "20.0 / 92.5;")]),
    ("C4 red and blue swapped", PAL, [("(b << 16) | (g << 8) | r;", "(r << 16) | (g << 8) | b;")]),
    ("C5 bit 0 read as luminance", PAL, [("_colours[colourNumber >> 1];", "_colours[colourNumber & 0x7F];")]),
    ("B1 two colour clocks before the access", BUS, [("internal const int ClocksBeforeAccess = 3;", "internal const int ClocksBeforeAccess = 2;")]),
    ("B2 RDY sampled as the cycle starts", BUS,
     [("        long frames = _tia.Frame.Frames;\n", "        long frames = _tia.Frame.Frames;\n        bool early = _tia.RdyLow;\n"),
      ("halted = !write && !_tia.HaltReleaseDue;", "halted = !write && early;")]),
    ("B3 a held read not repeated", BUS, [("            if (!halted)\n            {\n                return value;\n            }\n", "            return value;\n")]),
    ("S1 the frame does not report its row", FB, [("        sink.Add(nameof(_row), _row);\n", "")]),
    ("S2 the TIA does not report its clock", TIA, [("        sink.Add(nameof(_clock), _clock);\n", "")]),
    ("S3 the playfield does not report its fetched bit", PF, [("        sink.Add(nameof(_next), _next);\n", "")]),
]


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def failed_files(stdout):
    names = re.findall(r"^\s*Failed Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.", stdout, re.M)
    return collections.Counter(names)


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-tia-line-faults-")
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
