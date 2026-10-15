"""The Atari 2600 plan's task 4 fault pass: plant one fault at a time in the bus, the machine, the
TIA's first version and the state reports, in a scratch copy outside the repository, build, run the
whole Atari 2600 test project and print which test files failed, with the number of failing cases.
A fault no test file catches shows as SURVIVED. Standard library only.

    python3 -I tools/probes/atari-2600/bus-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (B1, T4, ...) pick some faults only.
The suite's files are copied from the repository's .testdata cache when it has them, so the scratch
copy need not fetch them again. Needs dotnet on PATH (or in ~/.dotnet).

A record of its day. Its copy list predates the tests that read machines/ and docs/, so on later
code the scratch copy's baseline fails in Atari2600AcceptanceTests, BootTests, PagePathTests,
SuiteRecordedTests and SuiteSelfTests (162 cases on 15 October 2026), and a fault is read by the
files beyond those. T7 stopped matching when task 9 gave the TIA its fire buttons; it was ported on
15 October 2026 (the final review's fix wave) and, run alone, Atari2600BusTests, ControllersTests and
TiaInputTests caught it."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
D = "src/Dbhq.Machines.Atari2600/"
BUS, TIA, MACHINE = D + "Atari2600Bus.cs", D + "Tia.cs", D + "Atari2600.cs"

FAULTS = [
    # (id and name, file, old, new); old may instead be a list of (old, new) pairs, with new None.
    # Ported on 14 October 2026 to the code task 5 rewrote: B6, B8, B9, B10, H3, H5, H6, S2, T1,
    # T4, T9 and M3 now name the same faults in the new code, and T2, whose latch never reset now
    # halts for ever, is a release a count early instead (task 5's review).
    ("B1 A13 decoded as the cartridge's select", BUS,
     "        if ((address & 0x1000) != 0)\n        {\n            return Chip.Cartridge;",
     "        if ((address & 0x2000) != 0)\n        {\n            return Chip.Cartridge;"),
    ("B2 the TIA mirror at $0100 dropped", BUS,
     "        if ((address & 0x0080) == 0)\n        {\n            return Chip.Tia;",
     "        if ((address & 0x0180) == 0)\n        {\n            return Chip.Tia;"),
    ("B3 RAM or I/O chosen by A8, not A9", BUS,
     "return (address & 0x0200) == 0 ? Chip.RiotRam : Chip.RiotIo;",
     "return (address & 0x0100) == 0 ? Chip.RiotRam : Chip.RiotIo;"),
    ("B4 a TIA read drives all eight lines", BUS,
     "                    value = (byte)((_tia.Read(register) & driven) | (_dataBus & ~driven));",
     "                    value = _tia.Read(register);"),
    ("B5 a write leaves the residue", BUS,
     "        if (write)\n        {\n            _dataBus = value;\n", "        if (write)\n        {\n"),
    ("B6 the halt releases one cycle late", BUS,
     "            if (!halted)\n            {\n                return value;\n            }\n",
     "            if (!halted)\n            {\n                if (held) { Cycle(false, address, 0, false, out _); }\n                return value;\n            }\n"),
    ("B7 RDY holds a write too", BUS,
     "    public override void Write(ushort address, byte value)\n    {\n",
     "    public override void Write(ushort address, byte value)\n    {\n        if (_tia.RdyLow) { Halt(address); }\n"),
    ("B8 the halted cycles are not cycles", BUS,
     "        bool held = !_tia.HaltReleaseDue;\n        while (true)\n",
     "        bool held = !_tia.HaltReleaseDue;\n        while (!_tia.HaltReleaseDue) { _tia.Tick(); _tia.Tick(); _tia.Tick(); }\n        while (true)\n"),
    ("B9 the RIOT ticked after the access", BUS,
     [("        _riot.Tick();\n        halted = ", "        halted = "),
      ("        for (int i = ClocksBeforeAccess; i < 3; i++)", "        _riot.Tick();\n        for (int i = ClocksBeforeAccess; i < 3; i++)")], None),
    ("B10 two colour clocks a cycle", BUS,
     "        for (int i = 0; i < ClocksBeforeAccess; i++)", "        for (int i = 1; i < ClocksBeforeAccess; i++)"),
    ("B11 a TIA write decodes A0 to A4", BUS, "_tia.Write(line & 0x3F, value);", "_tia.Write(line & 0x1F, value);"),
    ("B12 a TIA read decodes A0 to A4", BUS,
     "                    int register = line & 0x0F;\n                    byte driven = Tia.DrivenLines(register);\n                    value",
     "                    int register = line & 0x1F;\n                    byte driven = Tia.DrivenLines(register);\n                    value"),
    ("B13 Peek reads the RIOT's I/O with its side effects", BUS,
     "                return _riot.PeekRegister(line);", "                return _riot.ReadRegister(line);"),
    ("B14 Peek of the cartridge is an access", BUS,
     "                return _board.Peek(line);", "                return _board.Access(line, write: false, _dataBus);"),
    ("B15 power on keeps the data bus", BUS, "        _colourClocks = 0;\n        _dataBus = 0;\n", "        _colourClocks = 0;\n"),
    ("B16 power on keeps the board's bank", BUS, "        _board.PowerOn();\n        _cycles = 0;", "        _cycles = 0;"),
    ("H1 the observer is told of every access", BUS,
     "        if (chip is Chip.Tia or Chip.RiotIo || (chip == Chip.Cartridge && _board.Bank != bank))",
     "        if (chip != Chip.RiotRam)"),
    ("H2 the observer is not told of a bank switch", BUS,
     "        if (chip is Chip.Tia or Chip.RiotIo || (chip == Chip.Cartridge && _board.Bank != bank))",
     "        if (chip is Chip.Tia or Chip.RiotIo)"),
    ("H3 the halt's end is not told", BUS,
     "            _observer.HaltReleased(_cycles - 1);", "            _ = _cycles;"),
    ("H4 a frame's end is not told", BUS, "            _observer!.FrameEnded(_cycles);", "            _ = _cycles;"),
    ("H5 observing clears the timer flag", BUS,
     "        if (_tia.Frame.Frames != frames)\n", "        _riot.ReadRegister(0x0284);\n        if (_tia.Frame.Frames != frames)\n"),
    ("H6 the reference option adds a colour clock", BUS,
     "        _riot.Tick();\n        halted = ", "        _riot.Tick();\n        if (_perColourClockReference) { _tia.Tick(); }\n        halted = "),
    ("S1 the bus does not report the data bus", BUS, "        sink.Add(nameof(_dataBus), _dataBus);\n", ""),
    ("S2 the TIA does not report its counter", TIA, "        sink.Add(nameof(_counter), _counter);\n", ""),
    ("S3 the RIOT does not report PA7's level", D + "Riot6532.cs", "        sink.Add(nameof(_pa7), _pa7);\n", ""),
    ("S4 the Superchip does not report its board", D + "Boards/Superchip.cs", "        sink.Add(nameof(_board), _board);\n", ""),
    ("T1 WSYNC released a count late", TIA, "            if (_counter == 0)\n            {\n                _rdyLow = false;", "            if (_counter == 0b100000)\n            {\n                _rdyLow = false;"),
    ("T2 WSYNC released a count early", TIA, "            if (_counter == 0)\n            {\n                _rdyLow = false;", "            if (_counter == 0b010100)\n            {\n                _rdyLow = false;"),
    ("T3 a frame ends at the start of VSYNC", TIA,
     "                if ((old & 0x02) != 0 && (value & 0x02) == 0)", "                if ((old & 0x02) == 0 && (value & 0x02) != 0)"),
    ("T4 the line limit one line late", D + "FrameBuffer.cs", "        if (++_row == MaxLines)\n", "        if (++_row == MaxLines + 1)\n"),
    ("T5 CXCLR clears nothing", TIA, "            case Cxclr:\n                _collisions = 0;\n", "            case Cxclr:\n"),
    ("T6 CXBLPF drives D6", TIA, "        0x06 => 0x80,\n", ""),
    ("T7 a fire button reads pressed", TIA, "        return (byte)(((level >> (register - 0x0C)) & 1) << 7);", "        return 0x00;"),
    ("T8 a collision register's D7 and D6 swapped", TIA,
     "            return (byte)(((_collisions >> (2 * register)) & 0x03) << 6);",
     "            int pair = (_collisions >> (2 * register)) & 0x03;\n            return (byte)(((pair >> 1) | ((pair & 1) << 1)) << 6);"),
    ("T9 power on leaves the counter", TIA, "        _counter = 0;\n        _phase = 0;\n        _clock = 0;\n", ""),
    ("M1 power on without the CPU's reset", MACHINE, "        Cpu.Cycles = 0;\n        Cpu.Reset();", "        Cpu.Cycles = 0;"),
    ("M2 the CPU a 65C02", MACHINE, "new Cpu(Bus, CpuVariant.Nmos6502)", "new Cpu(Bus, CpuVariant.Wdc65C02)"),
    ("M3 RunFrames one frame long", MACHINE, "        while (Bus.Tia.Frame.Frames < end)", "        while (Bus.Tia.Frame.Frames <= end)"),
]


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def failed_files(stdout):
    names = re.findall(r"^\s*Failed Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.", stdout, re.M)
    return collections.Counter(names)


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-bus-faults-")
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
    for fid, rel, old, new in FAULTS:
        if only and fid.split()[0] not in only:
            continue
        full = os.path.join(dst, rel)
        text = open(full).read()
        pairs = old if isinstance(old, list) else [(old, new)]
        missing = [o for o, _ in pairs if text.count(o) != 1]
        if missing:
            out.write(f"{fid}: PATTERN NOT FOUND ONCE ({', '.join(str(text.count(o)) for o in missing)})\n")
            out.flush()
            continue
        planted = text
        for o, n in pairs:
            planted = planted.replace(o, n)
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
