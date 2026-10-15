"""The Atari 2600 plan's task 9 fault pass: plant one fault at a time in the inputs (Controllers, the
TIA's fire buttons and latch and its paddle ports, the RIOT's port A against a closed switch, and the
state report), in a scratch copy outside the repository, build, run the whole Atari 2600 test project
and print which test files failed, with the number of failing cases, and which suite tests among
them. A fault no test file catches shows as SURVIVED. Standard library only.

    python3 -I tools/probes/atari-2600/input-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (I1, S2, ...) pick some faults only.
The suite's files are copied from the repository's .testdata cache when it has them, so the scratch
copy need not fetch them again. Needs dotnet on PATH (or in ~/.dotnet)."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
D = "src/Dbhq.Machines.Atari2600/"
TIA, CON, RIOT = D + "Tia.cs", D + "Controllers.cs", D + "Riot6532.cs"

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("I1 the fire latch ignoring VBLANK D6", TIA, [("        byte level = (_registers[Vblank] & 0x40) != 0 ? _fireLatches : _firePins;", "        byte level = _firePins;")]),
    ("I2 disabling the latch does not reset it", TIA, [("                    _fireLatches = FireBoth;\n                }\n                else", "                }\n                else")]),
    ("I3 enabling with a button held does not clear the latch", TIA, [("                else\n                {\n                    CatchFire();\n                }\n", "")]),
    ("I4 the latch catches presses while disabled", TIA, [("        if ((_registers[Vblank] & 0x40) != 0)\n        {\n            _fireLatches &= _firePins;", "        {\n            _fireLatches &= _firePins;")]),
    ("I5 INPT4 and INPT5 swapped", TIA, [("        return (byte)(((level >> (register - 0x0C)) & 1) << 7);", "        return (byte)(((level >> (0x0D - register)) & 1) << 7);")]),
    ("I6 the fire buttons active high", CON, [("(byte)(((left & Joystick.Fire) == 0 ? 0b01 : 0) | ((right & Joystick.Fire) == 0 ? 0b10 : 0));", "(byte)(((left & Joystick.Fire) != 0 ? 0b01 : 0) | ((right & Joystick.Fire) != 0 ? 0b10 : 0));")]),
    ("I7 the paddle ports read 1 with the dump off", TIA, [("        if (register is not (0x0C or 0x0D))\n        {\n            return 0x00;", "        if (register is not (0x0C or 0x0D))\n        {\n            return (_registers[Vblank] & 0x80) != 0 ? (byte)0x00 : (byte)0x80;")]),
    ("I8 the two sticks on each other's nibble", CON, [("(byte)~((Directions(left) << 4) | Directions(right));", "(byte)~((Directions(right) << 4) | Directions(left));")]),
    ("I9 up and down swapped on the port", CON, [("    private static int Directions(Joystick stick) => (int)stick & 0x0F;", "    private static int Directions(Joystick stick) => ((int)stick & 0x0C) | (((int)stick & 1) << 1) | (((int)stick >> 1) & 1);")]),
    ("I10 the difficulty switches swapped", CON, [("ConsoleSwitches.LeftDifficultyA) != 0 ? 0x40 : 0;", "ConsoleSwitches.LeftDifficultyA) != 0 ? 0x80 : 0;"), ("ConsoleSwitches.RightDifficultyA) != 0 ? 0x80 : 0;", "ConsoleSwitches.RightDifficultyA) != 0 ? 0x40 : 0;")]),
    ("I11 the colour switch active high", CON, [("ConsoleSwitches.BlackAndWhite) == 0 ? 0x08 : 0;", "ConsoleSwitches.BlackAndWhite) != 0 ? 0x08 : 0;")]),
    ("I12 the unconnected port B pins read 0", CON, [("    private const byte UnconnectedPortB = 0x34;", "    private const byte UnconnectedPortB = 0x00;")]),
    ("I13 opposite directions filtered out", CON, [("    private static int Directions(Joystick stick) => (int)stick & 0x0F;", "    private static int Directions(Joystick stick) => (int)stick & 0x0F & ((stick & (Joystick.Up | Joystick.Down)) == (Joystick.Up | Joystick.Down) ? 0x0C : 0x0F) & ((stick & (Joystick.Left | Joystick.Right)) == (Joystick.Left | Joystick.Right) ? 0x03 : 0x0F);")]),
    ("I14 a bit that names no input taken", CON, [("        if ((state & ~EveryStickInput) != 0)", "        if (false)")]),
    ("I15 the stick check lets -1 through", CON, [("        if (stick is not (0 or 1))", "        if (stick > 1)")]),
    ("I16 power on releases the fire buttons", TIA, [("        _fireLatches = FireBoth;\n    }\n\n    /// <inheritdoc />", "        _fireLatches = FireBoth;\n        _firePins = FireBoth;\n    }\n\n    /// <inheritdoc />")]),
    ("I17 a port A output at 1 holds its pin high against a closed switch", RIOT, [("    public byte PortAPins => (byte)(_portAInput & (_ora | ~_ddra));", "    public byte PortAPins => (byte)((_portAInput & ~_ddra) | (_ora & _ddra));")]),
    ("I18 the switches not applied until the first change", CON, [("        _tia = tia;\n        Apply();", "        _tia = tia;")]),
    ("S1 the TIA does not report its fire latches", TIA, [("        sink.Add(nameof(_fireLatches), _fireLatches);\n", "")]),
    ("S2 the TIA does not report its fire pins", TIA, [("        sink.Add(nameof(_firePins), _firePins);\n", "")]),
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
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-input-faults-")
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
