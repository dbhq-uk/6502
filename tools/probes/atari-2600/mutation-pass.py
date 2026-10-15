"""The Atari 2600 plan's task 15 mutation pass: the plan's list of twenty mutations, each planted
alone in a scratch copy of the committed head outside the repository (git archive), the whole Atari
2600 test project built and run each time, and the failing tests printed by file with the first
few names. A mutation no test catches shows as SURVIVED. Each line also gives the file and the line
the mutation changes, counted in the unchanged file. Standard library only.

    python3 -I tools/probes/atari-2600/mutation-pass.py [SCRATCH_DIR] [MUTATION_ID ...]

SCRATCH_DIR defaults to a new temporary directory; MUTATION_IDs (M1, M9a, ...) pick some only.
Most patterns are the earlier fault passes' (bus-faults.py, riot-faults.py, board-faults.py,
tia-line-faults.py, tia-object-faults.py, tia-motion-faults.py, sound-faults.py, input-faults.py),
named in each comment; M4, M5, M13, M16 and M17 are new here. The suite's files are copied from
the repository's .testdata cache, which git archive leaves out. Needs dotnet on PATH (or in
~/.dotnet)."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
D = "src/Dbhq.Machines.Atari2600/"
BUS, TIA, RIOT, REG = D + "Atari2600Bus.cs", D + "Tia.cs", D + "Riot6532.cs", D + "Region.cs"
PF, CTR, MS, MOT, SND = D + "TiaPlayfield.cs", D + "TiaPositionCounter.cs", D + "TiaMissile.cs", D + "TiaMotion.cs", D + "TiaSound.cs"
BANKED, FA, SC = D + "Boards/BankedBoard.cs", D + "Boards/Fa.cs", D + "Boards/Superchip.cs"

MUTATIONS = [
    # (id and name, file, [(old, new), ...]); each old must occur exactly once.
    ("M1 the TIA mirror at $0100 dropped", BUS,  # bus-faults B2
     [("        if ((address & 0x0080) == 0)\n        {\n            return Chip.Tia;",
       "        if ((address & 0x0180) == 0)\n        {\n            return Chip.Tia;")]),
    ("M2 A12 decoded as A13", BUS,  # bus-faults B1
     [("        if ((address & 0x1000) != 0)\n        {\n            return Chip.Cartridge;",
       "        if ((address & 0x2000) != 0)\n        {\n            return Chip.Cartridge;")]),
    ("M3 the floating bus driving all eight bits", BUS,  # bus-faults B4
     [("                    value = (byte)((_tia.Read(register) & driven) | (_dataBus & ~driven));",
       "                    value = _tia.Read(register);")]),
    ("M4 the WSYNC halt releasing one colour clock late", TIA,  # new; half of tia-object-faults X2
     [("            if (_counter == 0)\n            {\n                _rdyLow = false;\n            }\n\n            _playfield.Show();\n        }\n        else if (_phase == 2)",
       "            _playfield.Show();\n        }\n        else if (_phase == 1 && _counter == 0)\n        {\n            _rdyLow = false;\n        }\n        else if (_phase == 2)")]),
    # M5's field is reported and powered on as the bus's others are, so the state report's own
    # tests do not catch the new field instead of the lost write.
    ("M5 DEC WSYNC writing once: the second write lost", BUS,  # new
     [("    private long _cycles;\n", "    private long _cycles;\n    private long _lastWsyncWrite = -2;\n"),
      ("        _colourClocks = 0;\n        _dataBus = 0;\n", "        _colourClocks = 0;\n        _dataBus = 0;\n        _lastWsyncWrite = -2;\n"),
      ("        sink.Add(nameof(_dataBus), _dataBus);\n", "        sink.Add(nameof(_dataBus), _dataBus);\n        sink.Add(nameof(_lastWsyncWrite), _lastWsyncWrite);\n"),
      ("                    _tia.Write(line & 0x3F, value);\n",
       "                    if ((line & 0x3F) != 0x02 || _lastWsyncWrite != _cycles - 1) { _tia.Write(line & 0x3F, value); }\n"
       "                    if ((line & 0x3F) == 0x02) { _lastWsyncWrite = _cycles; }\n")]),
    ("M6 the split inside a cycle moved by one clock", BUS,  # tia-line-faults B1
     [("internal const int ClocksBeforeAccess = 3;", "internal const int ClocksBeforeAccess = 2;")]),
    ("M7 the timer's count after zero held", RIOT,  # riot-faults T3
     [("        if (_freeRunning)\n        {\n            // In the free run",
       "        if (_freeRunning)\n        {\n            _untilDecrement = _divider;\n            // In the free run")]),
    ("M8 the PA7 flag cleared by the wrong read: INTIM, not TIMINT", RIOT,  # riot-faults E2 and E3 together
     [("            byte flags = Flags;\n            _edgeFlag = false;", "            byte flags = Flags;"),
      ("        _timerInterruptEnabled = (address & 0x08) != 0;\n\n        // \"The reading",
       "        _timerInterruptEnabled = (address & 0x08) != 0;\n        _edgeFlag = false;\n\n        // \"The reading")]),
    ("M9a a hotspot read on F8, F6 or F4 answered from the bank before its own access", BANKED,  # board-faults B1
     [("        if ((uint)hotspot < (uint)_banks)\n        {\n            Select(hotspot);\n        }\n\n        return write ? dataBus : _image[_base + offset];",
       "        byte old = _image[_base + offset];\n        if ((uint)hotspot < (uint)_banks)\n        {\n            Select(hotspot);\n        }\n\n        return write ? dataBus : old;")]),
    ("M9b an FA hotspot read answered from the bank its own access selects", FA,  # board-faults F6
     [("            Select(hotspot);\n        }\n\n        return data;",
       "            Select(hotspot);\n            return write ? dataBus : _image[_base + offset];\n        }\n\n        return data;")]),
    ("M10 the Superchip's dummy read not writing", SC,  # board-faults S1
     [("        if (offset < ReadPort)\n        {\n            _ram[offset - WritePort] = dataBus;",
       "        if (offset < ReadPort)\n        {\n            if (write) _ram[offset - WritePort] = dataBus;")]),
    ("M11 FA's D0 gate inverted", FA,  # board-faults F1
     [("(data & 0x01) != 0", "(data & 0x01) == 0")]),
    ("M12 the playfield's PF0 bit order reversed", PF,  # tia-line-faults P1
     [("_bits = (_bits & ~0x0000F) | (value >> 4);", "_bits = (_bits & ~0x0000F) | (Reverse(value) & 0xF);")]),
    ("M13 a NUSIZ copy spacing off by eight: the close copy at 24, not 16", CTR,  # new; D1 moved by one count
     [("        3 => Close,", "        5 => Close,")]),
    ("M14 RESMP not hiding the missile", MS,  # tia-object-faults L3
     [("if ((enam & 0x02) == 0 || (resmp & 0x02) != 0)", "if ((enam & 0x02) == 0)")]),
    ("M15 the HMOVE comb left out", TIA,  # tia-motion-faults H3
     [("if (state == (_motion.Extended ? Lrhb : Rhb))", "if (state == Rhb)")]),
    ("M16 one motion value's sign flipped: +1 read as -1", MOT,  # new
     [("if (((registers[Hmp0 + i] >> 4) ^ 0x07) == _ripple)",
       "if ((((registers[Hmp0 + i] >> 4) == 0x1 ? 0xF : (registers[Hmp0 + i] >> 4)) ^ 0x07) == _ripple)")]),
    ("M17 a collision latch wired to the wrong pair: P0-PF's latch set by P0-M1", TIA,  # new
     [("(P0Bit, PfBit, 5)", "(P0Bit, M1Bit, 5)")]),
    ("M18 a polynomial counter's tap moved: the five-bit counter's from bit 2 to bit 3", SND,  # sound-faults N1
     [("new0 = (((bits >> 2) ^ (bits >> 4)) & 1)", "new0 = (((bits >> 3) ^ (bits >> 4)) & 1)")]),
    ("M19 the PAL palette used on NTSC", REG,  # tia-line-faults C1
     [("hueAngle: TiaPalette.NtscHueAngle);", "hueAngle: TiaPalette.PalHueAngle);")]),
    ("M20 the fire latch ignoring VBLANK bit 6", TIA,  # input-faults I1
     [("        byte level = (_registers[Vblank] & 0x40) != 0 ? _fireLatches : _firePins;", "        byte level = _firePins;")]),
]


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def failed_tests(stdout):
    found = re.findall(r"^\s*Failed Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.(\w+)", stdout, re.M)
    by_file = collections.OrderedDict()
    for cls, name in found:
        by_file.setdefault(cls, []).append(name)
    return by_file


def describe(by_file):
    parts = []
    for cls, names in sorted(by_file.items()):
        unique = list(dict.fromkeys(names))
        shown = ", ".join(unique[:3]) + (", ..." if len(unique) > 3 else "")
        parts.append(f"{cls} ({len(names)}: {shown})")
    return "; ".join(parts)


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"M\d+[ab]?", args[0]) else tempfile.mkdtemp(prefix="atari-mutation-pass-")
    only = set(args)
    if os.path.abspath(dst).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    os.makedirs(dst)
    head = run("git rev-parse --short HEAD", cwd=SRC).stdout.strip()
    r = run(f"git archive HEAD | tar -x -C '{dst}'", cwd=SRC)
    if r.returncode != 0:
        sys.exit("git archive failed: " + r.stderr)
    cache = os.path.join(SRC, ".testdata", "atari-2600")
    if os.path.isdir(cache):
        shutil.copytree(cache, os.path.join(dst, ".testdata", "atari-2600"))
    env = dict(os.environ, PATH=os.path.expanduser("~/.dotnet") + ":" + os.environ["PATH"])
    proj = "tests/Dbhq.Machines.Atari2600.Tests"
    test = f"timeout 1200 dotnet test -c Release --no-build {proj} --logger 'console;verbosity=normal'"
    out = sys.stdout
    out.write(f"head {head}, scratch copy {dst}\n")
    r = run(f"dotnet build -c Release {proj}", cwd=dst, env=env)
    out.write(f"baseline build rc={r.returncode}\n")
    r = run(test, cwd=dst, env=env)
    last = r.stdout.strip().splitlines()[-1] if r.stdout.strip() else ""
    out.write(f"baseline: {sum(len(v) for v in failed_tests(r.stdout).values())} failed ({last})\n")
    out.flush()
    survived = []
    for mid, rel, pairs in MUTATIONS:
        if only and mid.split()[0] not in only:
            continue
        full = os.path.join(dst, rel)
        text = open(full).read()
        missing = [o for o, _ in pairs if text.count(o) != 1]
        if missing:
            out.write(f"{mid}: PATTERN NOT FOUND ONCE ({', '.join(str(text.count(o)) for o in missing)})\n")
            out.flush()
            continue
        line = ",".join(str(text[:text.index(o)].count("\n") + 1) for o, _ in pairs)
        planted = text
        for o, n in pairs:
            planted = planted.replace(o, n)
        open(full, "w").write(planted)
        try:
            r = run(f"dotnet build -c Release {proj}", cwd=dst, env=env)
            if r.returncode != 0:
                errs = sorted(set(re.findall(r"error \w+", r.stdout)))
                out.write(f"{mid} [{rel}:{line}]: BUILD FAILED {errs}\n")
                continue
            r = run(test, cwd=dst, env=env)
            by_file = failed_tests(r.stdout)
            if r.returncode == 124:
                by_file["(timed out)"] = ["run"]
            if by_file:
                out.write(f"{mid} [{rel}:{line}]: CAUGHT by {describe(by_file)}\n")
            else:
                survived.append(mid.split()[0])
                out.write(f"{mid} [{rel}:{line}]: SURVIVED\n")
        finally:
            open(full, "w").write(text)
            out.flush()
    out.write(f"\nSurvived: {', '.join(survived) if survived else 'none'}\nDONE\n")


main()
