"""The Atari 2600 plan's task 7 fault pass: plant one fault at a time in HMOVE and the motion clocks
(TiaMotion, its wiring in Tia, the reset's cut in TiaPositionCounter), in the screenshot runner's
partition rule and PNG reader, and in the state reports, in a scratch copy outside the repository,
build, run the whole Atari 2600 test project and print which test files failed, with the number of
failing cases, and which suite tests among them. A fault no test file catches shows as SURVIVED.
G1 replants task 6's C2, the latches stopped by the horizontal blank, to see whether the suite's
collision/blank-gating now proves latching in the blank. Standard library only.

    python3 -I tools/probes/atari-2600/tia-motion-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (H1, Z2, ...) pick some faults only.
The suite's files are copied from the repository's .testdata cache when it has them, so the scratch
copy need not fetch them again. Needs dotnet on PATH (or in ~/.dotnet)."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
D = "src/Dbhq.Machines.Atari2600/"
T = "tests/Dbhq.Machines.Atari2600.Tests/"
TIA, MOT, CTR = D + "Tia.cs", D + "TiaMotion.cs", D + "TiaPositionCounter.cs"
PART, PNG, RUN = T + "Partition.cs", T + "PngReader.cs", T + "ScreenshotRunner.cs"

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("H1 HMOVE taken a grid clock early", MOT, [("    private const int StartDelay = 4;", "    private const int StartDelay = 0;")]),
    ("H2 HMOVE taken a grid clock late", MOT, [("    private const int StartDelay = 4;", "    private const int StartDelay = 8;")]),
    ("H3 the comb left out", TIA, [("if (state == (_motion.Extended ? Lrhb : Rhb))", "if (state == Rhb)")]),
    ("H4 the HMOVE latch never cleared", TIA, [("            _motion.LineStart();\n", "")]),
    ("H5 the comparators read every bit inverted", MOT, [("if (((registers[Hmp0 + i] >> 4) ^ 0x07) == _ripple)", "if (((registers[Hmp0 + i] >> 4) ^ 0x0F) == _ripple)")]),
    ("H6 an HMOVE while counting starts the count again", MOT, [("            if (!_counting)\n", "            if (true)\n")]),
    ("H7 the ripple counter never comes to rest", MOT, [("            _ripple = Rest;\n            _counting = false;\n", "            _ripple = Rest;\n")]),
    ("H8 HMCLR clears nothing", TIA, [("                Array.Clear(_registers, Hmp0, Hmbl - Hmp0 + 1);\n", "")]),
    ("H9 HMCLR clears the latches too", TIA, [("                Array.Clear(_registers, Hmp0, Hmbl - Hmp0 + 1);\n", "                Array.Clear(_registers, Hmp0, Hmbl - Hmp0 + 1);\n                _motion.PowerOn();\n")]),
    ("H10 the grid on quarter 1", MOT, [("        bool grid = quarter == 2;", "        bool grid = quarter == 1;")]),
    ("H11 the extra clock acts after the pixel", TIA,
     [("        if (extra != 0)\n        {\n            ClockObjects(extra, motionClock: true);\n        }\n\n        int lit = 0;", "        int lit = 0;"),
      ("        ClockObjects(~extra & 0x1F, motionClock);\n", "        ClockObjects(~extra & 0x1F, motionClock);\n        ClockObjects(extra, motionClock: true);\n")]),
    ("H12 the extra clock and the motion clock both count", TIA, [("        ClockObjects(~extra & 0x1F, motionClock);\n", "        ClockObjects(0x1F, motionClock);\n")]),
    ("H13 the extra clocks skip the players", TIA, [("            ClockObjects(extra, motionClock: true);\n", "            ClockObjects(extra & ~0x03, motionClock: true);\n")]),
    ("R1 a reset cuts the start on the clock it lands", CTR, [("        _cutLate = true;\n", "        _start = false;\n        _startBefore = false;\n")]),
    ("G1 the latches stopped by the horizontal blank (task 6's C2)", TIA, [("        if (!_vblank)\n        {\n            _collisions", "        if (!_vblank && !_hblank)\n        {\n            _collisions")]),
    ("Z1 a partition tie to the higher number", PART, [("if (counts[number] > counts[best])", "if (counts[number] >= counts[best])")]),
    ("Z2 a shared match kept by the less frequent", PART, [("shared.OrderByDescending(colour => under[colour].Sum())", "shared.OrderBy(colour => under[colour].Sum())")]),
    ("Z3 a colour without a match counts nothing", PART, [("            if (ours[i] != match[reference[i]])", "            if (match[reference[i]] >= 0 && ours[i] != match[reference[i]])")]),
    ("Z4 the Paeth filter takes the up byte on a tie", PNG, [("return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);", "return (byte)(pa < pb && pa <= pc ? a : pb <= pc ? b : c);")]),
    ("Z5 the average filter rounds up", PNG, [("3 => (byte)((left + up) / 2),", "3 => (byte)((left + up + 1) / 2),")]),
    ("Z6 the rows aligned one apart", RUN, [("    public const int RowOffset = 0;", "    public const int RowOffset = 1;")]),
    ("Z7 the seventh frame compared", RUN, [("    public const int Frame = 8;", "    public const int Frame = 7;")]),
    ("Z8 the CRC not checked", PNG, [("            if (BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at + 8 + length)) != Crc32(typeAndData))", "            if (false)")]),
    ("S1 the TIA does not report its motion", TIA, [("        sink.Add(nameof(_motion), _motion);\n", "")]),
    ("S2 the motion does not report its latches", MOT, [("        sink.Add(nameof(_latches), _latches);\n", "")]),
    ("S3 the counter does not report its late cut", CTR, [("        sink.Add(nameof(_cutLate), _cutLate);\n", "")]),
]

def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def failed_files(stdout):
    names = re.findall(r"^\s*Failed Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.", stdout, re.M)
    return collections.Counter(names)


def failed_suite_tests(stdout):
    return sorted(set(re.findall(r"^\s*Failed Dbhq\.Machines\.Atari2600\.Tests\.SuiteSelfTests\.\w+\(test: \"([^\"]+)\"", stdout, re.M)))


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-tia-motion-faults-")
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
                suite = failed_suite_tests(r.stdout)
                out.write(f"{fid}: CAUGHT by " + ", ".join(f"{k} ({v})" for k, v in sorted(files.items()))
                          + (f"; suite: {', '.join(suite)}" if suite else "") + "\n")
            else:
                survived.append(fid)
                out.write(f"{fid}: SURVIVED\n")
        finally:
            open(full, "w").write(text)
            out.flush()
    out.write(f"\nSurvived: {', '.join(survived) if survived else 'none'}\nDONE\n")


main()
