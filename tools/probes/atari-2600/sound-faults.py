"""The Atari 2600 plan's task 10 fault pass: plant one fault at a time in the sound (TiaSound's
divider and noise-tone generator, the TIA's audio clock, volume writes and mix, the sample buffer's
count, ring and steps, the machine's catch-up and the state reports), in a scratch copy outside the
repository, build, run the whole Atari 2600 test project and print which test files failed, with
the number of failing cases. A fault no test file catches shows as SURVIVED. Standard library only.

    python3 -I tools/probes/atari-2600/sound-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (N1, B2, S1, ...) pick some faults
only. The suite's files are copied from the repository's .testdata cache when it has them, so the
scratch copy need not fetch them again. Needs dotnet on PATH (or in ~/.dotnet)."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
D = "src/Dbhq.Machines.Atari2600/"
SND, TIA, BUF, BUS, MAC = D + "TiaSound.cs", D + "Tia.cs", D + "SampleBuffer.cs", D + "Atari2600Bus.cs", D + "Atari2600.cs"

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("N1 the five-bit counter's tap moved from bit 2 to bit 3", SND, [("new0 = (((bits >> 2) ^ (bits >> 4)) & 1)", "new0 = (((bits >> 3) ^ (bits >> 4)) & 1)")]),
    ("N2 the four-bit counter's tap moved from bit 7 to bit 6", SND, [("0 => (((bits >> 7) ^ (bits >> 8)) & 1)", "0 => (((bits >> 6) ^ (bits >> 8)) & 1)")]),
    ("N3 the nine-bit chain's tap moved from bit 4 to bit 3", SND, [("new0 = (((bits >> 8) ^ (bits >> 4)) & 1)", "new0 = (((bits >> 8) ^ (bits >> 3)) & 1)")]),
    ("N4 the divide by 2 not toggling", SND, [("1 => ~(bits >> 5) & 1,", "1 => (bits >> 5) & 1,")]),
    ("N5 the divide by 6 leaving the wrong unused state", SND, [("(((bits >> 5) & 7) == 5 ? 1 : 0)", "(((bits >> 5) & 7) == 2 ? 1 : 0)")]),
    ("N6 the four-bit counter clocked at state 8 only", SND, [("2 => poly5 is 8 or 24,", "2 => poly5 is 8,")]),
    ("N7 the four-bit counter clocked by bit 3 of the five-bit counter", SND, [("_ => ((bits >> 4) & 1) != 0,", "_ => ((bits >> 3) & 1) != 0,")]),
    ("N8 the divider restarting at 1, dividing by AUDF", SND, [("            _divider0 = 0;\n            _bits0 = Pulse", "            _divider0 = 1;\n            _bits0 = Pulse")]),
    ("N9 the divider counting modulo 31", SND, [("_divider0 = (_divider0 + 1) & 0x1F;", "_divider0 = (_divider0 + 1) % 31;")]),
    ("N10 AUDF's top three bits used", SND, [("if (_divider0 == (audf0 & 0x1F))", "if (_divider0 == audf0)")]),
    ("N11 AUDC's top four bits used", SND, [("_bits0 = Pulse(_bits0, audc0 & 0x0F);", "_bits0 = Pulse(_bits0, audc0);")]),
    ("N12 power on with all nine bits 0", SND, [("        _bits0 = PowerOnBits;\n", "        _bits0 = 0;\n")]),
    ("N13 AUDV's top four bits used", SND, [("(bit1 * (audv1 & 0x0F))", "(bit1 * audv1)")]),
    ("N14 the level over 15, not 30", SND, [("mix / (double)LoudestMix", "mix / 15.0")]),
    ("T1 channel 1 stepped with channel 0's AUDC", TIA, [("_audio.Step(r[Audc0], r[Audf0], r[Audc1], r[Audf1]);", "_audio.Step(r[Audc0], r[Audf0], r[Audc0], r[Audf1]);")]),
    ("T2 the channels stepped once a line", TIA, [("if (state is Rhs or Cnt)", "if (state is Cnt)")]),
    ("T3 the first step at LRHB, not RHS", TIA, [("if (state is Rhs or Cnt)", "if (state is Lrhb or Cnt)")]),
    ("T4 a volume write waits for the next step", TIA, [("                if (mix != _mix)\n", "                if (mix != _mix && _clock < 0)\n")]),
    ("T5 the clocks at a line's start lost", TIA, [("            _soundCarry += _clock - _soundMark;\n", "")]),
    ("T6 power on keeps the level", TIA, [("        _audio.PowerOn();\n        _mix = 0;\n", "        _audio.PowerOn();\n")]),
    ("B1 the sample period's clocks a thousandth out", BUF, [("_clocksPerSample = 3 * value / SampleRate;", "_clocksPerSample = 3.003 * value / SampleRate;")]),
    ("B2 an instant emitted one late", BUF, [("while (InstantTime(_instant + 1) <= _clock)", "while (InstantTime(_instant + 2) <= _clock)")]),
    ("B3 a full ring keeps its oldest place", BUF, [("            _read = _read + 1 == Capacity ? 0 : _read + 1;\n            _dropped++;", "            _dropped++;")]),
    ("B4 dropped samples not counted", BUF, [("            _read = _read + 1 == Capacity ? 0 : _read + 1;\n            _dropped++;", "            _read = _read + 1 == Capacity ? 0 : _read + 1;")]),
    ("B5 a step's edge placed at the wrong end of its sample", BUF, [("int phase = Math.Clamp((int)(fraction * Phases), 0, Phases - 1);", "int phase = Math.Clamp((int)((1 - fraction) * Phases), 0, Phases - 1);")]),
    ("B6 a kernel two samples wide", BUF, [("private const int HalfWidth = 16;", "private const int HalfWidth = 2;")]),
    ("B7 the clock changed with no re-base", BUF, [("            if (_clocksPerSample > 0)\n", "            if (_clocksPerSample < 0)\n")]),
    ("B8 clear keeps the level", BUF, [("        _level = 0;\n        _output = 0;\n", "        _output = 0;\n")]),
    ("B9 clear keeps the steps under way", BUF, [("        Array.Clear(_pending);\n", "")]),
    ("M1 no catch-up at the end of Run", MAC, [("            Step();\n        }\n\n        Bus.CatchUpSound();\n    }\n\n    /// <summary>\n    /// Runs whole instructions until <paramref name=\"frames\"/>", "            Step();\n        }\n    }\n\n    /// <summary>\n    /// Runs whole instructions until <paramref name=\"frames\"/>")]),
    ("M2 the region switch not passed to the sound", BUS, [("            _sound.CpuHz = value.CpuHz;\n", "")]),
    ("M3 power on keeps the samples", BUS, [("        _sound.Clear();\n        _tia.PowerOn();", "        _tia.PowerOn();")]),
    ("M4 the sample buffer a tenth of a second", BUS, [("Math.Max(1, sampleRate / 4)", "Math.Max(1, sampleRate / 10)")]),
    ("S1 the TIA does not report its mix", TIA, [("        sink.Add(nameof(_mix), _mix);\n", "")]),
    ("S2 the TIA does not report its channels", TIA, [("        sink.Add(nameof(_audio), _audio);\n", "")]),
    ("S3 the channels do not report a divider", SND, [("        sink.Add(nameof(_divider1), _divider1);\n", "")]),
    ("S4 the sample buffer does not report its level", BUF, [("        sink.Add(nameof(_level), _level);\n", "")]),
    ("S5 the bus does not report the sample buffer", BUS, [("        sink.Add(nameof(_sound), _sound);\n", "")]),
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
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-sound-faults-")
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
