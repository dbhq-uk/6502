"""The Atari 2600 plan's task 5, step 3: the order inside a cycle by the plan's "Fixed before
running" item 8. In a scratch copy outside the repository, builds the machine with each of the
four splits (0, 1, 2 or 3 of a cycle's colour clocks before its access, the rest after), runs the
suite's trusted self-tests through SuiteSelfTests and the bus's CycleOrderTests, and prints for
each split which trusted tia-timing/ self-tests passed, how many other trusted rows passed, and
which CycleOrderTests cases passed. Item 8 keeps the split that passes the most trusted
tia-timing/ self-tests; a tie goes to the split the sources state (tia.md section 4: all three
before the access). Standard library only.

    python3 -I tools/probes/atari-2600/cycle-split.py [SCRATCH_DIR]

The suite's files are copied from the repository's .testdata cache when it has them. Needs dotnet
on PATH (or in ~/.dotnet)."""
import os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
BUS = "src/Dbhq.Machines.Atari2600/Atari2600Bus.cs"
CONSTANT = "internal const int ClocksBeforeAccess = 3;"
ROW = re.compile(r"^\s*(Passed|Failed) Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.(\w+)(\(.*?\))?\s*\[", re.M)


def run(cmd, cwd, env):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, cwd=cwd, env=env)


def main():
    dst = sys.argv[1] if len(sys.argv) > 1 else tempfile.mkdtemp(prefix="atari-cycle-split-")
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
    bus = os.path.join(dst, BUS)
    original = open(bus).read()
    if original.count(CONSTANT) != 1:
        sys.exit("the split constant was not found once in the bus")
    for split in range(4):
        open(bus, "w").write(original.replace(CONSTANT, CONSTANT.replace("= 3;", f"= {split};")))
        r = run(f"dotnet build -c Release {proj}", dst, env)
        if r.returncode != 0:
            print(f"split {split}: BUILD FAILED")
            continue
        r = run(f"timeout 3000 dotnet test -c Release --no-build {proj} "
                "--filter \"FullyQualifiedName~SuiteSelfTests.Trusted_self_test_passes|FullyQualifiedName~CycleOrderTests\" "
                "--logger 'console;verbosity=normal'", dst, env)
        rows = ROW.findall(r.stdout)
        timing = sorted((args, verdict) for verdict, cls, name, args in rows if cls == "SuiteSelfTests" and "tia-timing/" in args)
        others = [verdict for verdict, cls, name, args in rows if cls == "SuiteSelfTests" and "tia-timing/" not in args]
        order = sorted((name + args, verdict) for verdict, cls, name, args in rows if cls == "CycleOrderTests")
        passed = sum(1 for _, verdict in timing if verdict == "Passed")
        print(f"split {split} ({split} before the access, {3 - split} after): "
              f"trusted tia-timing/ self-tests passed {passed} of {len(timing)}; "
              f"other trusted rows passed {others.count('Passed')} of {len(others)}")
        for args, verdict in timing:
            print(f"    {verdict:6} {args}")
        for name, verdict in order:
            print(f"    {verdict:6} CycleOrderTests.{name}")
        sys.stdout.flush()
    open(bus, "w").write(original)
    print("DONE")


main()
