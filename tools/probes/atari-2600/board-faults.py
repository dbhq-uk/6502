"""The Atari 2600 plan's task 2 fault pass: plant one fault at a time in the region, the cartridge
and the boards, in a scratch copy outside the repository, build, run the Atari tests (without the
suite's pin checks) and print which test classes failed. Standard library only.

    python3 -I tools/probes/atari-2600/board-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (R1, K4, ...) pick some faults only.
Needs dotnet on PATH (or in ~/.dotnet).

A record of its day. Its copy list predates the tests that read machines/ and docs/, so on later
code the scratch copy's baseline fails in Atari2600AcceptanceTests, BootTests, PagePathTests,
SuiteRecordedTests and SuiteSelfTests (162 cases on 15 October 2026), and a fault is read by the
files beyond those. R4 stopped matching when Region gained its hue angle (task 5); it was ported on 15 October 2026
(the final review's fix wave) and, run alone, RegionTests caught it."""
import os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
B = "src/Dbhq.Machines.Atari2600/"

FAULTS = [
    # (id, file, old, new)
    ("R1 PAL clock typed as Korth's", B + "Region.cs", "colourClockHz: 4_433_618.75 * 4 / 5,", "colourClockHz: 3_546_894.0,"),
    ("R2 CPU clock a quarter", B + "Region.cs", "public double CpuHz => ColourClockHz / 3;", "public double CpuHz => ColourClockHz / 4;"),
    ("R3 NTSC window a line late", B + "Region.cs", "firstShownLine: 19,", "firstShownLine: 20,"),
    ("R4 PAL pixel 2:1", B + "Region.cs", "pixelWidth: 52,\n        pixelHeight: 25,", "pixelWidth: 2,\n        pixelHeight: 1,"),
    ("R5 PAL nominal lines 313", B + "Region.cs", "nominalLines: 312,", "nominalLines: 313,"),
    ("C1 filler checked in bank 0 only", B + "Cartridge.cs", "for (int bank = 0; bank < file.Length; bank += BankSize)", "for (int bank = 0; bank < BankSize; bank += BankSize)"),
    ("C2 filler 128 bytes", B + "Cartridge.cs", "private const int FillerSize = 256;", "private const int FillerSize = 128;"),
    ("C3 superchip on FA too", B + "Cartridge.cs", "(family is Family.F8 or Family.F6 or Family.F4) && HasFiller(file)", "(family is Family.F8 or Family.F6 or Family.F4 or Family.Fa) && HasFiller(file)"),
    ("C4 no copy kept", B + "Cartridge.cs", "new Cartridge((byte[])file.Clone(), name, family, superchip)", "new Cartridge(file, name, family, superchip)"),
    ("C5 copied before the size check", B + "Cartridge.cs", "ArgumentNullException.ThrowIfNull(file);\n", "ArgumentNullException.ThrowIfNull(file);\n        _ = (byte[])file.Clone();\n"),
    ("C6 message without the size", B + "Cartridge.cs", "This file is {length.ToString(\"N0\", invariant)} {unit}, and", "This file is {unit}, and"),
    ("C7 F6 and F4 sizes swapped", B + "Cartridge.cs", "(16384, \"F6\", Family.F6),\n        (32768, \"F4\", Family.F4),", "(16384, \"F4\", Family.F4),\n        (32768, \"F6\", Family.F6),"),
    ("P1 2K not mirrored", B + "Boards/Plain.cs", "_mask = image.Length - 1;", "_mask = 0xFFF;"),
    ("P2 a write changes the image", B + "Boards/Plain.cs", "public byte Access(ushort address, bool write, byte dataBus) => write ? dataBus : _image[address & _mask];", "public byte Access(ushort address, bool write, byte dataBus) => write ? _image[address & _mask] = dataBus : _image[address & _mask];"),
    ("B1 hotspot read returns the old bank's byte", B + "Boards/BankedBoard.cs", "        if ((uint)hotspot < (uint)_banks)\n        {\n            Select(hotspot);\n        }\n\n        return write ? dataBus : _image[_base + offset];", "        byte old = _image[_base + offset];\n        if ((uint)hotspot < (uint)_banks)\n        {\n            Select(hotspot);\n        }\n\n        return write ? dataBus : old;"),
    ("B2 hotspot switches on read only", B + "Boards/BankedBoard.cs", "        if ((uint)hotspot < (uint)_banks)\n        {\n            Select(hotspot);", "        if (!write && (uint)hotspot < (uint)_banks)\n        {\n            Select(hotspot);"),
    ("B3 F6 hotspots one late", B + "Boards/F6.cs", "firstHotspot: 0xFF6", "firstHotspot: 0xFF7"),
    ("B4 one hotspot too many", B + "Boards/BankedBoard.cs", "        if ((uint)hotspot < (uint)_banks)\n        {\n            Select(hotspot);", "        if ((uint)hotspot <= (uint)_banks)\n        {\n            Select(Math.Min(hotspot, _banks - 1));"),
    ("F1 D0 gate inverted", B + "Boards/Fa.cs", "(data & 0x01) != 0", "(data & 0x01) == 0"),
    ("F2 no D0 gate", B + "Boards/Fa.cs", "(uint)hotspot < Banks && (data & 0x01) != 0", "(uint)hotspot < Banks"),
    ("F3 read gates on the bus", B + "Boards/Fa.cs", "(uint)hotspot < Banks && (data & 0x01) != 0", "(uint)hotspot < Banks && (dataBus & 0x01) != 0"),
    ("F4 FA read of the write port writes nothing", B + "Boards/Fa.cs", "            if (offset < ReadPort)\n            {\n                _ram[offset - WritePort] = dataBus;", "            if (offset < ReadPort)\n            {\n                if (write) _ram[offset - WritePort] = dataBus;"),
    ("F5 FA read port at $1080", B + "Boards/Fa.cs", "private const int ReadPort = 0x100;", "private const int ReadPort = 0x080;"),
    ("F6 FA hotspot read returns the new bank's byte", B + "Boards/Fa.cs", "            Select(hotspot);\n        }\n\n        return data;", "            Select(hotspot);\n            return write ? dataBus : _image[_base + offset];\n        }\n\n        return data;"),
    ("S1 Superchip dummy read writes nothing", B + "Boards/Superchip.cs", "        if (offset < ReadPort)\n        {\n            _ram[offset - WritePort] = dataBus;", "        if (offset < ReadPort)\n        {\n            if (write) _ram[offset - WritePort] = dataBus;"),
    ("S2 Superchip write to the read port stores", B + "Boards/Superchip.cs", "return write ? dataBus : _ram[offset - ReadPort];", "if (write) { _ram[offset - ReadPort] = dataBus; return dataBus; } return _ram[offset - ReadPort];"),
    ("S3 Superchip read port shows the ROM", B + "Boards/Superchip.cs", "return write ? dataBus : _ram[offset - ReadPort];", "return _board.Access(address, write, dataBus);"),
    ("S4 Superchip RAM cleared on a bank switch", B + "Boards/Superchip.cs", "            return _board.Access(address, write, dataBus);\n        }\n\n        if (offset < ReadPort)", "            int before = _board.Bank;\n            byte b = _board.Access(address, write, dataBus);\n            if (_board.Bank != before) Array.Clear(_ram);\n            return b;\n        }\n\n        if (offset < ReadPort)"),
    ("K1 Peek switches the bank", B + "Boards/BankedBoard.cs", "        int bank = (uint)hotspot < (uint)_banks ? hotspot : Bank;", "        if ((uint)hotspot < (uint)_banks) Select(hotspot);\n        int bank = Bank;"),
    ("K2 Peek of the write port clears the cell", B + "Boards/Superchip.cs", "        return _ram[offset < ReadPort ? offset - WritePort : offset - ReadPort];", "        if (offset < ReadPort) _ram[offset] = 0x00;\n        return _ram[offset < ReadPort ? offset - WritePort : offset - ReadPort];"),
    ("K3 power on in bank 0", B + "Boards/BankedBoard.cs", "public void PowerOn() => Select(_banks - 1);", "public void PowerOn() => Select(0);"),
    ("K4 Superchip power on keeps the RAM", B + "Boards/Superchip.cs", "        _board.PowerOn();\n        Array.Clear(_ram);", "        _board.PowerOn();"),
    ("K5 Peek of a hotspot shows the bank showing", B + "Boards/BankedBoard.cs", "        int bank = (uint)hotspot < (uint)_banks ? hotspot : Bank;", "        int bank = Bank;"),
    ("K7 FA power on keeps the RAM", B + "Boards/Fa.cs", "        Select(Banks - 1);\n        Array.Clear(_ram);", "        Select(Banks - 1);"),
    ("K6 FA power on in bank 0", B + "Boards/Fa.cs", "        Select(Banks - 1);", "        Select(0);"),
]


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def main():
    args = sys.argv[1:]
    DST = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-board-faults-")
    only = set(args)
    if os.path.abspath(DST).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(DST):
        shutil.rmtree(DST)
    os.makedirs(DST)
    for item in ["6502.slnx", "Directory.Build.props", ".editorconfig", "src/Dbhq.Cpu6502", "src/Dbhq.Machines.Atari2600",
                 "tests/Dbhq.Cpu6502.TestSupport", "tests/Dbhq.Machines.Atari2600.Tests", ".testdata/atari-2600"]:
        s, d = os.path.join(SRC, item), os.path.join(DST, item)
        if os.path.isdir(s):
            shutil.copytree(s, d, ignore=shutil.ignore_patterns("bin", "obj"))
        else:
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(s, d)
    env = dict(os.environ, PATH=os.path.expanduser("~/.dotnet") + ":" + os.environ["PATH"])
    proj = "tests/Dbhq.Machines.Atari2600.Tests"
    filt = "FullyQualifiedName!~SuiteManifestTests"
    out = sys.stdout
    if True:
        r = run(f"dotnet build -c Release {proj}", cwd=DST, env=env)
        out.write(f"baseline build rc={r.returncode}\n")
        r = run(f"dotnet test -c Release --no-build {proj} --filter '{filt}'", cwd=DST, env=env)
        out.write("baseline: " + (re.findall(r"(Passed!|Failed!).*", r.stdout) or ["?"])[0] + "\n")
        out.flush()
        for fid, path, old, new in FAULTS:
            if only and fid.split()[0] not in only:
                continue
            full = os.path.join(DST, path)
            text = open(full).read()
            if text.count(old) != 1:
                out.write(f"{fid}: PATTERN NOT FOUND ONCE ({text.count(old)})\n")
                out.flush()
                continue
            open(full, "w").write(text.replace(old, new))
            try:
                r = run(f"dotnet build -c Release {proj}", cwd=DST, env=env)
                if r.returncode != 0:
                    errs = sorted(set(re.findall(r"error \w+", r.stdout)))
                    out.write(f"{fid}: BUILD FAILED {errs}\n")
                    continue
                r = run(f"dotnet test -c Release --no-build {proj} --filter '{filt}'", cwd=DST, env=env)
                failed = sorted(set(re.findall(r"Failed Dbhq\.Machines\.Atari2600\.Tests\.(\w+)\.(\w+)", r.stdout)))
                classes = {}
                for cls, test in failed:
                    classes.setdefault(cls, []).append(test)
                summary = "; ".join(f"{c} ({len(t)}: {', '.join(sorted(t)[:3])})" for c, t in sorted(classes.items()))
                out.write(f"{fid}: {'CAUGHT by ' + summary if classes else 'SURVIVED'}\n")
            finally:
                open(full, "w").write(text)
                out.flush()
        out.write("DONE\n")


main()
