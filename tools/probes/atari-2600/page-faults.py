"""The Atari 2600 plan's task 14 fault pass: plant one fault at a time in the page and what it is
built from (the registry row, the page's template and library, the pins, the try-it check, the
photographs' README and the browser check's comparisons), in a scratch copy outside the repository,
build the site there, run the site's tests that read them with node, and print which test files
failed, with the number of failing cases. A fault no test file catches shows as SURVIVED. Standard
library only.

    python3 -I tools/probes/atari-2600/page-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (R1, P2, ...) pick some faults only.
The scratch copy is the repository without its build outputs, node_modules (linked, not copied)
and .testdata; it keeps site/public/machines/ and site/src/data/results.json, which this box makes
by hand (a machine build and a merged results file), so the page's tests have a built machine.
Each fault's text is replaced at its first place only. Needs node and npm on PATH."""
import os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
REG = "machines/registry.json"
PAGE = "site/src/pages/machines/[id].astro"
LIB = "site/src/lib/atari-2600.mjs"
TESTS = ["tests/atari-2600.test.mjs", "tests/atari-2600-page.test.mjs", "tests/browser-check-atari-2600.test.mjs", "tests/registry.test.mjs",
         "tests/build-machines.test.mjs", "tests/build.test.mjs", "tests/mirrors.test.mjs"]

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("R1 the row claims 3D models", REG, [('"case": true,\n      "photos": [\n        {\n          "file": "atari-2600.webp"', '"case": true,\n      "models": [{ "view": "outside", "module": "atari-2600-case" }, { "view": "inside", "module": "atari-2600-board" }],\n      "photos": [\n        {\n          "file": "atari-2600.webp"')]),
    ("R2 the main photograph's licence is not as Commons states it", REG, [('Atari-2600-Light-Sixer-FL.jpg",\n          "licence": "Public domain"', 'Atari-2600-Light-Sixer-FL.jpg",\n          "licence": "CC0"')]),
    ("R3 the clock typed, not Region.cs's", REG, [('"cpu": "6507 at 1.19 MHz (NTSC), 1.18 MHz (PAL)"', '"cpu": "6507 at 1.19 MHz (NTSC), 1.19 MHz (PAL)"')]),
    ("R4 the rights lose the second copyright line", REG, [('and its source carries the same licence with a second line, \\"Original 2048 game Copyright (c) 2014 Gabriele Cirulli\\". ', '')]),
    ("R5 the row is not running", REG, [('"status": "running",\n      "acceptance": "Atari2600AcceptanceTests"', '"status": "planned",\n      "acceptance": "Atari2600AcceptanceTests"')]),
    ("R6 the second photograph dropped", REG, [('\n        },\n        {\n          "file": "atari-2600-four-switch.webp"', '\n        },\n        {\n          "file": "atari-2600.webp"')]),
    ("P1 the page drops the one-console sentence", PAGE, [("So a trusted result is the behaviour of the one console the suite was checked on, not of every Atari 2600. ", "")]),
    ("P2 the credit does not link the licence", PAGE, [("<p data-vcs-credit>{program.credit} Its licence: <a href={program.licenceUrl}>{program.licence}</a>.</p>", "<p data-vcs-credit>{program.credit} Its licence: {program.licence}.</p>")]),
    ("P3 the game is said to be in roms/", PAGE, [("in a folder of its own, <a href={VCS_GAME_FOLDER_URL}><code>machines/atari-2600/cartridges/2048/</code></a>", "in <code>roms/</code>")]),
    ("P4 the boards not modelled are not listed", PAGE, [("{VCS_BOARDS.map((b) => <li>{b.board}: <a href={issueUrl(b.issue)}>issue {b.issue}</a></li>)}", "")]),
    ("P5 the speed issue not linked", PAGE, [("Making it faster is <a href={issueUrl(VCS_ISSUES.speed)}>issue {VCS_ISSUES.speed}</a>.", "Making it faster is planned.")]),
    ("P6 the panel is given no game", PAGE, [("  : vcs ? { rom: { file: roms[0].file, title: program.title }, download: machineDownloadBytes('atari-2600') }", "  : vcs ? { rom: null, download: machineDownloadBytes('atari-2600') }")]),
    ("L1 a left-out part's issue is wrong", LIB, [("{ part: 'the paddles', issue: 108 }", "{ part: 'the paddles', issue: 109 }")]),
    ("L2 one board dropped from the list", LIB, [("['E0', 131], ", "")]),
    ("L3 the right fire button is joystick 1's", LIB, [("'Right fire button': { stick: 1, inputs: ['Fire'] }", "'Right fire button': { stick: 0, inputs: ['Fire'] }")]),
    ("L4 the captures' issue is not 162", LIB, [("captures: 162", "captures: 161")]),
    ("M1 the game's pin is the NES's hash", "site/src/lib/pins.mjs", [("sha256: pin('Atari2600HomebrewSha256', source),", "sha256: pin('NesHomebrewSha256', source),")]),
    ("M2 the try-it check takes any control", "site/src/lib/machines.mjs", [("    if (!ATARI_2600_CONTROLS.includes(control.button)) throw new Error(`${where}: \"${control.button}\" is not one of the console's controls`);\n", "")]),
    ("M3 the try-it check reads no licence address", "site/src/lib/machines.mjs", [("  if (!/^https:\\/\\/\\S+$/.test(program.licenceUrl ?? '')) throw", "  if (false) throw")]),
    ("M4 the machine built from the NES's project", "site/src/lib/machines.mjs", [("'atari-2600': { project: 'Dbhq.Machines.Atari2600.Wasm'", "'atari-2600': { project: 'Dbhq.Machines.Nes.Wasm'")]),
    ("D1 the README's hash of the main photograph's copy is wrong", "site/src/assets/photos/README.md", [("SHA-256 `a4f54924feb7bf0a70fea1d42162336e4faaf86111b4c9c91124f8866ef807c7`", "SHA-256 `a4f54924feb7bf0a70fea1d42162336e4faaf86111b4c9c91124f8866ef807c8`")]),
    ("B1 the colour map is checked one way only", "site/scripts/browser-check-atari-2600.mjs", [("if ((forward.has(ca) && forward.get(ca) !== cb) || (back.has(cb) && back.get(cb) !== ca))", "if (forward.has(ca) && forward.get(ca) !== cb)")]),
    ("B2 the windows' rows matched from row 0 both", "site/scripts/browser-check-atari-2600.mjs", [("const j = ((fromB + y) * width + x) * 4;", "const j = (y * width + x) * 4;")]),
    ("B3 the test cartridge from another host", "site/scripts/browser-check-atari-2600.mjs", [("raw.githubusercontent.com/dbhq-uk/missingno/", "raw.githubusercontent.com/ajoneil/missingno/")]),
    ("B4 the test cartridge's hash not checked", "site/scripts/browser-check-atari-2600.mjs", [("    if (gitBlob(rom) !== entry[0]) throw new Error(`${url} does not match its git blob hash ${entry[0]}`);\n", "")]),
]

SKIP = shutil.ignore_patterns("node_modules", ".git", "bin", "obj", ".testdata", "dist", "TestResults", ".astro")


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-page-faults-")
    only = set(args)
    if os.path.abspath(dst).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    shutil.copytree(SRC, dst, ignore=SKIP)
    os.symlink(os.path.join(SRC, "site", "node_modules"), os.path.join(dst, "site", "node_modules"))
    site = os.path.join(dst, "site")
    out = sys.stdout

    def failures():
        b = run("npm run build", cwd=site)
        counts = {}
        if b.returncode:
            counts["the site build"] = 1
        for test in TESTS:
            r = run(f"timeout 300 node --test {test}", cwd=site)
            m = re.search(r"^ℹ fail (\d+)", r.stdout, re.M)
            counts[os.path.basename(test)] = int(m.group(1)) if m else (1 if r.returncode else 0)
        return counts

    base = failures()
    out.write(f"baseline: {sum(base.values())} failed {base}\n")
    out.flush()
    survived = []
    for fid, rel, change in FAULTS:
        if only and fid.split()[0] not in only:
            continue
        full = os.path.join(dst, rel)
        original = open(full, "rb").read()
        text = original.decode("utf-8")
        missing = [old for old, _ in change if text.count(old) < 1]
        if missing:
            out.write(f"{fid}: PATTERN NOT FOUND ONCE\n")
            out.flush()
            continue
        for old, new in change:
            text = text.replace(old, new, 1)
        open(full, "wb").write(text.encode("utf-8"))
        try:
            files = {k: v - base.get(k, 0) for k, v in failures().items() if v > base.get(k, 0)}
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
