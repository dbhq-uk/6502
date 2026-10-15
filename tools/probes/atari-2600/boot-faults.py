"""The Atari 2600 plan's task 12 fault pass: plant one fault at a time in the bundled game's files
(its image, its folder, its notice, its pins), in the recorded frames
(machines/atari-2600/expected-frames.json changed by hand), in the boot check's fixed switches,
and in the machine where the boot check should see it, in a scratch copy outside the repository,
build, run the whole Atari 2600 test project and print which test files failed, with the number of
failing cases. A fault no test file catches shows as SURVIVED. Standard library only.

    python3 -I tools/probes/atari-2600/boot-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (G1, F2, ...) pick some faults only.
The suite's files are copied from the repository's .testdata cache when it has them, so the
scratch copy need not fetch them again. Each text fault's text is replaced at its first place
only; a byte fault flips one byte of a binary file. Needs dotnet on PATH (or in ~/.dotnet)."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
T = "tests/Dbhq.Machines.Atari2600.Tests/"
GAME = "machines/atari-2600/cartridges/2048/"
FRAMES = "machines/atari-2600/expected-frames.json"
OUTCOMES = "machines/atari-2600/suite-outcomes.json"
M = "src/Dbhq.Machines.Atari2600/"

FAULTS = [
    # (id and name, file, [(old, new), ...]) for a text fault, or (id and name, file, offset) to flip a byte.
    ("G1 one byte of the game changed", GAME + "2048.bin", 0x100),
    ("G2 the game's pinned hash changed", "tests/Dbhq.Cpu6502.TestSupport/Pins.cs", [('Atari2600HomebrewSha256 = "c85d', 'Atari2600HomebrewSha256 = "d85d')]),
    ("G3 the licence's copyright line changed", GAME + "LICENSE", [("(Chester)", "(chesterbr)")]),
    ("G4 the art sentence dropped from the folder's README", GAME + "README.md", [("## The cartridge art is not in the ROM", "## The cartridge art")]),
    ("G5 the folder's manifest names another licence", GAME + "manifest.json", [('"licence": "MIT"', '"licence": "MIT-0"')]),
    ("G6 the list names another hash", "machines/atari-2600/cartridges/manifest.json", [('"sha256": "c85d', '"sha256": "d85d')]),
    ("G7 the notice loses the second copyright line", "NOTICE.md", [("Original 2048 game Copyright (c) 2014 Gabriele Cirulli", "Original 2048 game by Gabriele Cirulli")]),
    ("G8 roms/README.md loses its pointer", "roms/README.md", [("[`machines/atari-2600/cartridges/2048/`](../machines/atari-2600/cartridges/2048/)", "the cartridge folder")]),
    ("G9 the try-it names another file", "machines/atari-2600/try-it.json", [('"rom": "machines/atari-2600/cartridges/2048/2048.bin"', '"rom": "machines/atari-2600/cartridges/2048/2048.a26"')]),
    ("F1 the NTSC window's hash changed by hand", FRAMES, [('"rgba": {\n    "NTSC": "8', '"rgba": {\n    "NTSC": "9')]),
    ("F2 the PAL colours' hash changed by hand", FRAMES, [('"PAL": "4eda', '"PAL": "5eda')]),
    ("F3 the NTSC frame count changed by hand", FRAMES, [('"NTSC": 120', '"NTSC": 121')]),
    ("F4 the PAL switch changed by hand", FRAMES, [('"PAL": "black and white"', '"PAL": "colour"')]),
    ("S1 the check runs PAL with the switch at colour", T + "BootCheck.cs", [("region == Region.Pal ? ConsoleSwitches.BlackAndWhite : ConsoleSwitches.None", "ConsoleSwitches.None")]),
    ("S2 the check runs NTSC for 119 frames", T + "BootCheck.cs", [("region == Region.Pal ? 100 : 120", "region == Region.Pal ? 100 : 119")]),
    ("M1 the machine draws every colour with bit 1 set", M + "FrameBuffer.cs", [("internal void Put(int x, byte colour) => _drawing[(_row * Width) + x] = colour;", "internal void Put(int x, byte colour) => _drawing[(_row * Width) + x] = (byte)(colour | 2);")]),
    ("M2 the machine draws nothing", M + "FrameBuffer.cs", [("internal void Put(int x, byte colour) => _drawing[(_row * Width) + x] = colour;", "internal void Put(int x, byte colour) => _drawing[(_row * Width) + x] = 0;")]),
    ("M3 the sound makes half its samples", M + "SampleBuffer.cs", [("_clocksPerSample = 3 * value / SampleRate;", "_clocksPerSample = 6 * value / SampleRate;")]),
    ("A1 a recorded outcome changed by hand", OUTCOMES, [('"code": "$04",\n        "observed": "$53"', '"code": "$05",\n        "observed": "$53"')]),
    ("A2 a trusted test missing from Trusted", T + "SuiteSelfTests.cs", [('        "riot/timer",\n', '')]),
]


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def failed_files(stdout):
    names = re.findall(r"^\s*Failed Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.", stdout, re.M)
    return collections.Counter(names)


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-boot-faults-")
    only = set(args)
    if os.path.abspath(dst).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    os.makedirs(dst)
    for item in ["6502.slnx", "Directory.Build.props", ".editorconfig", "NOTICE.md", "roms/README.md", "docs/known-differences.md", "src/Dbhq.Cpu6502", "src/Dbhq.Machines.Atari2600",
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
    for fid, rel, change in FAULTS:
        if only and fid.split()[0] not in only:
            continue
        full = os.path.join(dst, rel)
        original = open(full, "rb").read()
        if isinstance(change, int):
            planted = bytearray(original)
            planted[change] ^= 0xFF
            planted = bytes(planted)
        else:
            text = original.decode("utf-8")
            missing = [old for old, _ in change if text.count(old) < 1]
            if missing:
                out.write(f"{fid}: PATTERN NOT FOUND ONCE\n")
                out.flush()
                continue
            for old, new in change:
                text = text.replace(old, new, 1)
            planted = text.encode("utf-8")
        open(full, "wb").write(planted)
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
            open(full, "wb").write(original)
            out.flush()
    out.write(f"\nSurvived: {', '.join(survived) if survived else 'none'}\nDONE\n")


main()
