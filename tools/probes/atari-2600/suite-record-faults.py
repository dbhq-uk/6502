"""The Atari 2600 plan's task 11 fault pass: plant one fault at a time in the recorded outcomes
(machines/atari-2600/suite-outcomes.json changed by hand, as the plan's step 4 asks) and in the
suite's tables (SuiteOutcomes, SuiteSelfTests), in a scratch copy outside the repository, build,
run the whole Atari 2600 test project and print which test files failed, with the number of
failing cases. A fault no test file catches shows as SURVIVED. Standard library only.

    python3 -I tools/probes/atari-2600/suite-record-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (O1, T2, ...) pick some faults only.
The suite's files are copied from the repository's .testdata cache when it has them, so the
scratch copy need not fetch them again. Each fault's text is replaced at its first place only.
Needs dotnet on PATH (or in ~/.dotnet)."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
T = "tests/Dbhq.Machines.Atari2600.Tests/"
OUT, SELF, REC = "machines/atari-2600/suite-outcomes.json", T + "SuiteSelfTests.cs", T + "SuiteOutcomes.cs"

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("O1 a screenshot count changed by hand: PAL calibration one fewer", OUT, [('"PAL": 407', '"PAL": 406')]),
    ("O2 a self-test outcome changed by hand: the power-on bank read as bank 0", OUT, [('"observed": "$B1"', '"observed": "$A0"')]),
    ("O3 a recorded pass changed by hand to a fail", OUT, [('"NTSC": "pass"', '"NTSC": {\n        "outcome": "fail",\n        "code": "$0C",\n        "observed": "$00",\n        "expected": "$01"\n      }')]),
    ("O4 a recorded test left out of the file", OUT, [('    },\n    "cartridge/ram-superchip": {\n      "NTSC": "pass",\n      "PAL": "pass"\n    }\n', '    }\n')]),
    ("O5 the pins in the file changed", OUT, [('"suiteCommit": "891c', '"suiteCommit": "991c')]),
    ("R1 a pass recorded without the OBSERVED its header reports", REC, [('public static readonly string[] ObservedOnPass = ["cartridge/power-on-bank"];', 'public static readonly string[] ObservedOnPass = [];')]),
    ("R2 a no verdict recorded as a pass", REC, [(': new JsonObject { ["outcome"] = "no verdict", ["result"] = Hex(run.Result) };', ': JsonValue.Create("pass");')]),
    ("T1 a not-run test missing from NotRun", SELF, [('        ("cartridge/bank-e0", 131),\n', '')]),
    ("T2 a trusted test missing from Trusted", SELF, [('        "riot/timer",\n', '')]),
    ("T3 a recorded cartridge test listed as not run too", SELF, [('        ("cartridge/bank-e0", 131),\n', '        ("cartridge/bank-e0", 131),\n        ("cartridge/bank-fa", 107),\n')]),
    ("T4 a not-yet test listed as not run instead", SELF, [('        ("collision/merge-delivery-train", 11, 0x04, 0x47, 0x7F),\n', ''), ('        ("cartridge/bank-e0", 131),\n', '        ("cartridge/bank-e0", 131),\n        ("collision/merge-delivery-train", 107),\n')]),
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
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-record-faults-")
    only = set(args)
    if os.path.abspath(dst).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    os.makedirs(dst)
    for item in ["6502.slnx", "Directory.Build.props", ".editorconfig", "src/Dbhq.Cpu6502", "src/Dbhq.Machines.Atari2600",
                 "tests/Dbhq.Cpu6502.TestSupport", "tests/Dbhq.Machines.Atari2600.Tests", ".testdata/atari-2600", "machines/atari-2600"]:
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
        missing = [old for old, _ in pairs if planted.count(old) < 1]
        if missing:
            out.write(f"{fid}: PATTERN NOT FOUND ONCE\n")
            out.flush()
            continue
        for old, new in pairs:
            planted = planted.replace(old, new, 1)
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
