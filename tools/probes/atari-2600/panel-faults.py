"""The Atari 2600 plan's task 13 fault pass: plant one fault at a time in the page's parts (the
console parts moved out of the NES's files, the Atari 2600's page script, its tables, its panel and
its WebAssembly host's source), in a scratch copy outside the repository, run the NES's and the
Atari 2600's panel tests with node, and print which test files failed, with the number of failing
cases. A fault no test file catches shows as SURVIVED. Standard library only.

    python3 -I tools/probes/atari-2600/panel-faults.py [SCRATCH_DIR] [FAULT_ID ...]

SCRATCH_DIR defaults to a new temporary directory; FAULT_IDs (S1, A2, ...) pick some faults only.
The scratch copy holds site/ (its node_modules linked, not copied) and the sources the tests read.
Each fault's text is replaced at its first place only. Needs node on PATH."""
import collections, os, re, shutil, subprocess, sys, tempfile

SRC = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
P = "site/public/"
TESTS = ["tests/nes-panel.test.mjs", "tests/atari-2600-panel.test.mjs"]

FAULTS = [
    # (id and name, file, [(old, new), ...])
    ("S1 the ring drops down to its most, not its prime", P + "sample-worklet.js", [("return { drop: total - prime, queued: prime };", "return { drop: total - most, queued: most };")]),
    ("S2 the ring registers no processor of its own", P + "sample-worklet.js", [("registerRing('sample-ring');", "")]),
    ("S3 a file at the limit is refused", P + "cartridge-picker.js", [("if (file.size <= limit) return null;", "if (file.size < limit) return null;")]),
    ("S4 a file over the limit is read before it is refused", P + "cartridge-picker.js", [("  const refusal = sizeRefusal(file, limit);\n  if (refusal) return { ok: false, line: refusal };\n  try {\n    return { ok: true, bytes: new Uint8Array(await file.arrayBuffer()) };", "  const read = new Uint8Array(await file.arrayBuffer());\n  const refusal = sizeRefusal(file, limit);\n  if (refusal) return { ok: false, line: refusal };\n  try {\n    return { ok: true, bytes: read };")]),
    ("S5 the picker opens before the bundled cartridge is tried", P + "cartridge-picker.js", [("  const data = await bundled;\n  const loaded = Boolean(data) && use(data);\n  openPicker();", "  openPicker();\n  const data = await bundled;\n  const loaded = Boolean(data) && use(data);")]),
    ("S6 a gamepad's buttons all read as its first", P + "gamepads.js", [("if (gamepad?.buttons?.[index]?.pressed)", "if (gamepad?.buttons?.[0]?.pressed)")]),
    ("S7 the left stick moves at a quarter of the way", P + "gamepads.js", [("export function stickDirections(gamepad, share = 0.5)", "export function stickDirections(gamepad, share = 0.25)")]),
    ("S8 a held button is not let go when its finger lifts", P + "touch-pad.js", [("    b.addEventListener('pointerup', release);\n", "")]),
    ("S9 a refused region change leaves the radios on the refused region", P + "region-line.js", [("if (change(r.value) === false) checkRegion(radios, current());", "change(r.value);")]),
    ("S10 the sound is posted without its transform", P + "sound-output.js", [("      transform?.(samples);\n", "")]),
    ("A1 Space is joystick 2's fire", P + "atari-2600-keys.js", [("Space: { stick: 0, input: 'Fire' }", "Space: { stick: 1, input: 'Fire' }")]),
    ("A2 the PAL window one line short", P + "atari-2600-keys.js", [("PAL: { first: 27, lines: 264,", "PAL: { first: 27, lines: 263,")]),
    ("A3 Reset flips and stays", P + "atari-2600.js", [("if (!(name in SWITCHES) || HELD_SWITCHES.includes(name)) return toggled;", "if (!(name in SWITCHES)) return toggled;")]),
    ("A4 the PAL suggestion takes the mean", P + "atari-2600.js", [("const m = median(lines.slice(-SUGGEST_FRAMES));", "const last = lines.slice(-SUGGEST_FRAMES); const m = last.reduce((s, v) => s + v, 0) / last.length;")]),
    ("A5 the PAL suggestion shows on PAL too", P + "atari-2600.js", [("if (region !== 'NTSC' || lines.length < SUGGEST_FRAMES) return false;", "if (lines.length < SUGGEST_FRAMES) return false;")]),
    ("A6 up and down together are filtered", P + "atari-2600.js", [("    if (key) sticks[key.stick] |= STICK[key.input];", "    if (key) sticks[key.stick] |= STICK[key.input];\n    for (const s of [0, 1]) if ((sticks[s] & 3) === 3) sticks[s] &= ~2;")]),
    ("A7 a toggle key flips on its repeats", P + "atari-2600.js", [("    if (event.repeat || held.has(event.code)) return;\n    held.add(event.code);\n    const name = SWITCH_KEYS[event.code];\n    if (name && !HELD_SWITCHES.includes(name)) onToggle(name);", "    const name = SWITCH_KEYS[event.code];\n    if (name && !HELD_SWITCHES.includes(name)) onToggle(name);\n    if (event.repeat || held.has(event.code)) return;\n    held.add(event.code);")]),
    ("A8 a region change restarts the machine", P + "atari-2600.js", [("    host.SetRegion(value);\n", "    host.SetRegion(value);\n    host.Load(rom, value, SAMPLE_RATE);\n")]),
    ("A9 the canvas keeps NTSC's window on PAL", P + "atari-2600.js", [("  canvas.height = height;\n", "")]),
    ("A10 stepTo does not power off and on", P + "atari-2600.js", [("      send([0, 0], input.toggled);\n      host.PowerCycle();\n", "      send([0, 0], input.toggled);\n")]),
    ("A11 stepTo lets go of the switches that stay", P + "atari-2600.js", [("      send([0, 0], input.toggled);", "      send([0, 0], 0);")]),
    ("A16 stepTo sends the inputs as held, not let go (task 13's review)", P + "atari-2600.js", [("      send([0, 0], input.toggled);", "      sendInputs();")]),
    ("S11 fresh() does not start the transform again (task 13's review)", P + "sound-output.js", [("      transform?.reset?.();\n", "")]),
    ("A17 the high-pass is not seeded by its first sample (task 13's review)", P + "atari-2600.js", [("      if (!seeded) {", "      if (false) {")]),
    ("A12 the offset is kept: the corner at 0 Hz", P + "atari-2600.js", [("export const DC_CORNER_HZ = 20;", "export const DC_CORNER_HZ = 0;")]),
    ("A13 the page types the size limit", P + "atari-2600.js", [("const read = await readRom(file, host.MaxRomBytes());", "const read = await readRom(file, 32768);")]),
    ("A14 a refused file is said to leave no machine running", P + "atari-2600.js", [("      cartLine.textContent = badFileLine(name, result, running || rom !== null);\n      return false;", "      cartLine.textContent = badFileLine(name, result, false);\n      return false;")]),
    ("A15 the gamepad's Reset is on button 8", P + "atari-2600-keys.js", [("export const GAMEPAD_SWITCHES = { 8: 'Select', 9: 'Reset' };", "export const GAMEPAD_SWITCHES = { 8: 'Reset', 9: 'Select' };")]),
    ("P1 the panel has no line count", "site/src/components/Atari2600Panel.astro", [("    <p class=\"vcs-line\" data-vcs-lines></p>\n", "")]),
    ("P2 a switch button enabled before the script runs", "site/src/components/Atari2600Panel.astro", [("data-vcs-toggle=\"BlackAndWhite\" aria-pressed=\"false\" disabled>", "data-vcs-toggle=\"BlackAndWhite\" aria-pressed=\"false\">")]),
    ("P3 the panel no longer says it is not a CRT", "site/src/components/Atari2600Panel.astro", [("It models the picture and is not a CRT", "It models the picture")]),
    ("H1 the host types the size limit", "src/Dbhq.Machines.Atari2600.Wasm/Program.cs", [("public static int MaxRomBytes() => Cartridge.MaxFileSize;", "public static int MaxRomBytes() => 32768;")]),
    ("H2 the host hands the picture to another module", "src/Dbhq.Machines.Atari2600.Wasm/Program.cs", [('[JSImport("picture", "atari2600")]', '[JSImport("picture", "vcs")]')]),
    ("H3 the host exports no SetRegion", "src/Dbhq.Machines.Atari2600.Wasm/Program.cs", [("    [JSExport]\n    public static void SetRegion(", "    public static void SetRegion(")]),
]


def run(cmd, **kw):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True, **kw)


def failed_files(stdout):
    """The failing cases per test file, from node's spec reporter run on each file in turn."""
    return collections.Counter({k: v for k, v in stdout.items() if v})


def main():
    args = sys.argv[1:]
    dst = args.pop(0) if args and not re.fullmatch(r"[A-Z]\d+", args[0]) else tempfile.mkdtemp(prefix="atari-panel-faults-")
    only = set(args)
    if os.path.abspath(dst).startswith(SRC + os.sep):
        sys.exit("the scratch copy must be outside the repository")
    if os.path.exists(dst):
        shutil.rmtree(dst)
    os.makedirs(dst)
    for item in ["site", "src/Dbhq.Machines.Atari2600", "src/Dbhq.Machines.Atari2600.Wasm", "src/Dbhq.Machines.Nes/Controller.cs", "src/Dbhq.Machines.Nes.Wasm/Program.cs",
                 "docs/atari-2600/facts/region.md", "machines/registry.json"]:
        s, d = os.path.join(SRC, item), os.path.join(dst, item)
        if os.path.isdir(s):
            shutil.copytree(s, d, ignore=shutil.ignore_patterns("bin", "obj", "node_modules", "dist", ".astro"))
        else:
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(s, d)
    os.symlink(os.path.join(SRC, "site", "node_modules"), os.path.join(dst, "site", "node_modules"))
    site = os.path.join(dst, "site")
    out = sys.stdout

    def failures():
        counts = {}
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
            files = failed_files(failures())
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
