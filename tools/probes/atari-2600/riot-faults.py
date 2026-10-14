"""The Atari 2600 plan's task 3 fault pass: plant one fault at a time in the RIOT, in a scratch copy
outside the repository, build, run Riot6532Tests and print which test cases failed. Then list every
test case and the faults that made it fail, so a test that no fault reached shows as SURVIVED.
Standard library only.

    python3 -I tools/probes/atari-2600/riot-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (T1, P4, ...) pick some faults only.
Needs dotnet on PATH (or in ~/.dotnet)."""
import os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
R = "src/Dbhq.Machines.Atari2600/Riot6532.cs"

FAULTS = [
    # (id and name, old, new)
    ("T1 first fall a whole divider after the write",
     "        _untilDecrement = 1;\n        _freeRunning = false;\n        _timerFlag = false;\n    }",
     "        _untilDecrement = _divider;\n        _freeRunning = false;\n        _timerFlag = false;\n    }"),
    ("T2 flag set on reaching zero, one cycle early (the 1981 figure)",
     "        if (_count == 0xFF)\n        {\n            _freeRunning = true;",
     "        if (_count == 0x00)\n        {\n            _count = 0xFF;\n            _freeRunning = true;"),
    ("T3 count after zero held at the divider",
     "        if (_freeRunning)\n        {\n            // In the free run",
     "        if (_freeRunning)\n        {\n            _untilDecrement = _divider;\n            // In the free run"),
    ("T4 a read on the flag's cycle clears it",
     "        if (!_timerFlagSetThisCycle)", "        if (!_timerFlagSetThisCycle || _count == 0xFF)"),
    ("T5 TIMINT read clears the timer flag",
     "            byte flags = Flags;\n            _edgeFlag = false;",
     "            byte flags = Flags;\n            _edgeFlag = false;\n            _timerFlag = false;"),
    ("T6 INTIM read leaves the timer flag",
     "            _timerFlag = false;\n            if (_freeRunning)", "            if (_freeRunning)"),
    ("T7 a read after zero leaves the free run",
     "                _freeRunning = false;\n                _untilDecrement = _divider;", ""),
    ("T8 the re-armed divider one cycle sooner",
     "                _untilDecrement = _divider;\n            }", "                _untilDecrement = _divider - 1;\n            }"),
    ("T9 dividers 8 and 64 swapped", "            1 => 8,\n            2 => 64,", "            1 => 64,\n            2 => 8,"),
    ("T10 a timer write leaves the flag",
     "        _freeRunning = false;\n        _timerFlag = false;\n    }", "        _freeRunning = false;\n    }"),
    ("T11 the free run's wrap protects a read",
     "            // [guessing - verify]: no source or suite test reaches it.\n            return;",
     "            // [guessing - verify]: no source or suite test reaches it.\n            _timerFlagSetThisCycle = _count == 0xFF;\n            return;"),
    ("D1 A4 not decoded on a write: edge control starts the timer",
     "        if ((address & 0x10) == 0)", "        if ((address & 0x10) == 0x20)"),
    ("D2 the port registers decode A3",
     "    private byte ReadPort(ushort address) => (address & 0x03) switch",
     "    private byte ReadPort(ushort address) => (address & 0x0B) switch"),
    ("D3 TIMINT drives bits 5 to 0 from the count",
     "    private byte Flags => (byte)((_timerFlag ? 0x80 : 0) | (_edgeFlag ? 0x40 : 0));",
     "    private byte Flags => (byte)((_timerFlag ? 0x80 : 0) | (_edgeFlag ? 0x40 : 0) | (_count & 0x3F));"),
    ("D4 RAM of 64 bytes on write",
     "public void WriteRam(int offset, byte value) => _ram[offset & (RamSize - 1)] = value;",
     "public void WriteRam(int offset, byte value) => _ram[offset & 0x3F] = value;"),
    ("I1 an INTIM read ignores A3",
     "        _timerInterruptEnabled = (address & 0x08) != 0;\n\n        // \"The reading", "\n        // \"The reading"),
    ("I2 a timer write ignores A3",
     "        _timerInterruptEnabled = (address & 0x08) != 0;\n        _count = value;", "        _count = value;"),
    ("I3 the IRQ pin ignores the enables",
     "public bool Irq => (_timerFlag && _timerInterruptEnabled) || (_edgeFlag && _edgeInterruptEnabled);",
     "public bool Irq => _timerFlag || _edgeFlag;"),
    ("E1 the edge polarity inverted", "        if (level == _edgePositive)", "        if (level != _edgePositive)"),
    ("E2 TIMINT read leaves the edge flag",
     "            byte flags = Flags;\n            _edgeFlag = false;", "            byte flags = Flags;"),
    ("E3 an INTIM read clears the edge flag",
     "        _timerInterruptEnabled = (address & 0x08) != 0;\n\n        // \"The reading",
     "        _timerInterruptEnabled = (address & 0x08) != 0;\n        _edgeFlag = false;\n\n        // \"The reading"),
    ("E4 arming sets the flag when the level matches",
     "            _edgeInterruptEnabled = (address & 0x02) != 0;\n            return;",
     "            _edgeInterruptEnabled = (address & 0x02) != 0;\n            if (_pa7 == _edgePositive) _edgeFlag = true;\n            return;"),
    ("E5 the detector ignores DDRA",
     "                    _ddra = value;\n                    WatchPa7();", "                    _ddra = value;"),
    ("E6 the detector ignores the joystick",
     "            _portAInput = value;\n            WatchPa7();", "            _portAInput = value;"),
    ("E7 the edge interrupt enable from A0",
     "            _edgeInterruptEnabled = (address & 0x02) != 0;", "            _edgeInterruptEnabled = (address & 0x01) != 0;"),
    ("P1 port A output reads ORA, not the pin",
     "public byte PortAPins => (byte)(_portAInput & (_ora | ~_ddra));",
     "public byte PortAPins => (byte)((_ora & _ddra) | (_portAInput & ~_ddra));"),
    ("P2 port B output reads the pin",
     "public byte PortBPins => (byte)((_orb & _ddrb) | (PortBInput & ~_ddrb));",
     "public byte PortBPins => (byte)(PortBInput & (_orb | ~_ddrb));"),
    ("P3 an ORA write kept only for output bits", "                    _ora = value;", "                    _ora = (byte)(value & _ddra);"),
    ("P4 port B reads its output register for every bit",
     "public byte PortBPins => (byte)((_orb & _ddrb) | (PortBInput & ~_ddrb));",
     "public byte PortBPins => _orb;"),
    ("O1 power on keeps the RAM", "        Array.Clear(_ram);\n", ""),
    ("O2 power on keeps ORA and DDRA", "        _ora = 0;\n        _ddra = 0;\n", ""),
    ("O3 power on keeps the polarity", "        _edgePositive = false;\n\n", "\n"),
    ("O4 power on keeps the interrupt enables",
     "        _timerInterruptEnabled = false;\n        _edgeInterruptEnabled = false;\n", ""),
    ("O5 the power-on timer at divide by 1024",
     "        _divider = 1;\n        _untilDecrement = 1;", "        _divider = 1024;\n        _untilDecrement = 1024;"),
    ("O6 a new chip not powered on", "    public Riot6532() => PowerOn();", "    public Riot6532() { }"),
    ("O7 power on keeps the flags", "        _timerFlag = false;\n        _timerFlagSetThisCycle = false;\n        _edgeFlag = false;\n", ""),
    ("K1 Peek of TIMINT clears the edge flag",
     "        return (address & 0x01) != 0 ? Flags : _count;",
     "        if ((address & 0x01) != 0) { byte f = Flags; _edgeFlag = false; return f; }\n        return _count;"),
    ("K2 Peek of INTIM clears the timer flag",
     "        return (address & 0x01) != 0 ? Flags : _count;",
     "        if (!_timerFlagSetThisCycle) _timerFlag = false;\n        return (address & 0x01) != 0 ? Flags : _count;"),
    ("K3 Peek of INTIM ends the free run",
     "        return (address & 0x01) != 0 ? Flags : _count;",
     "        if ((address & 0x01) == 0 && _freeRunning && !_timerFlagSetThisCycle) { _freeRunning = false; _untilDecrement = _divider; }\n        return (address & 0x01) != 0 ? Flags : _count;"),
]


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def cases(stdout, outcome):
    return set(re.findall(rf"^\s*{outcome} Dbhq\.Machines\.Atari2600\.Tests\.Riot6532Tests\.(\w+(?:\([^)]*\))?)", stdout, re.M))


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-riot-faults-")
    only = set(args)
    if os.path.abspath(dst).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    os.makedirs(dst)
    for item in ["6502.slnx", "Directory.Build.props", ".editorconfig", "src/Dbhq.Cpu6502", "src/Dbhq.Machines.Atari2600",
                 "tests/Dbhq.Cpu6502.TestSupport", "tests/Dbhq.Machines.Atari2600.Tests"]:
        s, d = os.path.join(SRC, item), os.path.join(dst, item)
        if os.path.isdir(s):
            shutil.copytree(s, d, ignore=shutil.ignore_patterns("bin", "obj"))
        else:
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(s, d)
    env = dict(os.environ, PATH=os.path.expanduser("~/.dotnet") + ":" + os.environ["PATH"])
    proj = "tests/Dbhq.Machines.Atari2600.Tests"
    test = f"dotnet test -c Release --no-build {proj} --filter 'FullyQualifiedName~Riot6532Tests' --logger 'console;verbosity=normal'"
    out = sys.stdout
    r = run(f"dotnet build -c Release {proj}", cwd=dst, env=env)
    out.write(f"baseline build rc={r.returncode}\n")
    r = run(test, cwd=dst, env=env)
    every = cases(r.stdout, "Passed")
    out.write(f"baseline: {len(every)} passed, {len(cases(r.stdout, 'Failed'))} failed\n")
    out.flush()
    caught_by = {c: [] for c in every}
    full = os.path.join(dst, R)
    text = open(full).read()
    for fid, old, new in FAULTS:
        if only and fid.split()[0] not in only:
            continue
        if text.count(old) != 1:
            out.write(f"{fid}: PATTERN NOT FOUND ONCE ({text.count(old)})\n")
            out.flush()
            continue
        open(full, "w").write(text.replace(old, new))
        try:
            r = run(f"dotnet build -c Release {proj}", cwd=dst, env=env)
            if r.returncode != 0:
                errs = sorted(set(re.findall(r"error \w+", r.stdout)))
                out.write(f"{fid}: BUILD FAILED {errs}\n")
                continue
            r = run(test, cwd=dst, env=env)
            failed = sorted(cases(r.stdout, "Failed"))
            for c in failed:
                caught_by.setdefault(c, []).append(fid.split()[0])
            out.write(f"{fid}: {'CAUGHT by ' + str(len(failed)) + ': ' + ', '.join(failed) if failed else 'SURVIVED'}\n")
        finally:
            open(full, "w").write(text)
            out.flush()
    out.write("\nEach test case and the faults that made it fail:\n")
    for c in sorted(caught_by):
        out.write(f"{c}: {' '.join(caught_by[c]) if caught_by[c] else 'SURVIVED'}\n")
    out.write("DONE\n")


main()
