#!/usr/bin/env python3
"""Plants one fault at a time in the BBC Micro's two 3D models (their data,
their modules, their tools, the page's wiring and the machine's counters),
runs the tests that should notice, and puts the files back. Prints one line a
fault: its id, what it does, and the tests that failed. A fault that no test
fails on is reported as SURVIVED.

    python3 tools/probes/bbc_models_mutations.py --root ~/mut-bbc-models          # every fault
    python3 tools/probes/bbc_models_mutations.py --root ~/mut-bbc-models M3 M16   # chosen faults
    python3 tools/probes/bbc_models_mutations.py --list
    python3 tools/probes/bbc_models_mutations.py --root ~/mut-bbc-models --check  # each fault applies, nothing run

Run it on a scratch worktree outside the repository (`git worktree add
~/mut-bbc-models --detach HEAD`), never on the one you work in, and do not edit
its files while it runs. The scratch copy needs, in site/, node_modules, the
built machines in public/machines/ and src/data/results.json, as `npm test`
does. Each fault says which suites it runs:

    site   `npm run build` (a build that fails counts as caught: CI's npm test
           builds first), then `node --test tests/*.test.mjs`; a failing test
           is reported by file and name, and a run with fewer tests than the
           unmutated run (a module that no longer loads drops the tests after
           it) as "N tests did not run", which the workflows' floors catch
    py     the tools' pytest suite (PYTHON, default ~/venv-models/bin/python)
    cs     the BBC Micro's C# test project (dotnet, DOTNET or ~/.dotnet/dotnet)
    run    tools/bbc-micro-model/board_register.py on the real inputs
           (BBC_MODEL_INPUTS), its exit code and last line, then site

A fault is one or more replacements, each of which must match exactly once
(or every time, where it says "all"), or a function that edits the files; the
script stops if one does not apply. Every file a fault or a run touches is put
back afterwards, and the worktree is checked clean with git. The faults are
task 10 of docs/superpowers/plans/2026-10-04-bbc-micro-models.md, mapped to
the code as it is, and more; the results from 14 October 2026 are in
docs/journal/2026-10-04-the-bbc-micro-models.md, "Task 10".
"""
import hashlib
import json
import os
import re
import subprocess
import sys

HOME = os.path.expanduser('~')
PYTHON = os.environ.get('PYTHON', os.path.join(HOME, 'venv-models', 'bin', 'python'))
DOTNET = os.environ.get('DOTNET', os.path.join(HOME, '.dotnet', 'dotnet'))

M = 'site/src/models/'
LAYOUT = M + 'bbc-micro-board-layout.mjs'
NOTES = M + 'bbc-micro-board-notes.mjs'
PARTS = M + 'bbc-micro-board-parts.mjs'
SCENE = M + 'bbc-micro-board-scene.mjs'
BOARD = M + 'bbc-micro-board.js'
CLAYOUT = M + 'bbc-micro-case-layout.mjs'
CNOTES = M + 'bbc-micro-case-notes.mjs'
CPARTS = M + 'bbc-micro-case-parts.mjs'
CSCENE = M + 'bbc-micro-case-scene.mjs'
CASE = M + 'bbc-micro-case.js'
ACCESS = M + 'access.mjs'
MODELS = M + 'models.mjs'
PANEL = 'site/public/bbc-micro.js'
REGISTRY = 'machines/registry.json'
REGISTRY_JS = 'site/src/lib/registry.mjs'
DEPLOY = '.github/workflows/deploy-site.yml'
CHECK = 'site/scripts/browser-check-bbc.mjs'
MODEL_TEST = 'site/tests/model.test.mjs'
BRES = 'site/src/data/bbc-micro-board-model.json'
CRES = 'site/src/data/bbc-micro-case-model.json'
T = 'tools/bbc-micro-model/'
D = T + 'data/'
FACTS = 'docs/bbc-micro/facts/models.md'
BUS = 'src/Dbhq.Machines.BbcMicro/BbcBus.cs'
CHIP = 'src/Dbhq.Machines.BbcMicro/BbcChip.cs'
MAP = 'site/src/assets/tracks/bbc-micro-board.webp'

CLAIM = ('      "case": true,\n      "models": [\n        {\n          "view": "outside",\n          "module": "bbc-micro-case"\n'
         '        },\n        {\n          "view": "inside",\n          "module": "bbc-micro-board"\n        }\n      ],')
I1_USED = ('"used": "the inside model\'s board: its outline, holes, pads and scale, the component side\'s copper and the printed legend, '
           'traced to look at, with their connections not verified')


def swap(path, a, b, every=False):
    """Three replacements that swap two texts, each found once (or every time), through a placeholder."""
    flag = ('all',) if every else ()
    return [(path, a, '\0SWAP\0', *flag), (path, b, a, *flag), (path, '\0SWAP\0', b, *flag)]


def in_entry(path, sha, old, new):
    """An edit inside the one registry entry whose SHA-256 begins with `sha`: `old` must be in that entry once."""
    def edit(root):
        full = os.path.join(root, path)
        text = open(full, encoding='utf-8').read()
        at = text.index(f'"sha256": "{sha}')
        start, end = text.rindex('{', 0, at), text.index('}', at)
        entry = text[start:end]
        if entry.count(old) != 1:
            sys.exit(f'in_entry: {old!r} is in the entry {entry.count(old)} times, not once')
        open(full, 'w', encoding='utf-8').write(text[:start] + entry.replace(old, new) + text[end:])
        return [path]
    return edit


def over_budget(root):
    """The track map padded to 600,001 bytes, with copper.json's size and SHA-256 made to match."""
    path = os.path.join(root, MAP)
    data = open(path, 'rb').read()
    data += b'\0' * (600_001 - len(data))
    open(path, 'wb').write(data)
    cj = os.path.join(root, D + 'copper.json')
    text = open(cj).read()
    m = json.loads(text)['map']
    for old, new in ((str(m['bytes']), '600001'), (m['sha256'], hashlib.sha256(data).hexdigest())):
        if text.count(old) != 1:
            sys.exit(f'over_budget: {old} is in copper.json {text.count(old)} times, not once')
        text = text.replace(old, new)
    open(cj, 'w').write(text)
    return [MAP, D + 'copper.json']


# (id, what it does, edits, suites). An edit is (file, old, new) or (file, old, new, 'all'), or a function of the
# root that edits files itself and returns the paths it changed.
FAULTS = [
    # ---- The plan's list (task 10, step 1), mapped to the code as it is ----
    ('M1a', 'x and y scales swapped in frame.json', [
        (D + 'frame.json', ' "pxPerMm": {\n  "x": 15.7524,\n  "y": 15.7256\n }', ' "pxPerMm": {\n  "x": 15.7256,\n  "y": 15.7524\n }')], ['site']),
    ('M1b', "board_frame.py's fit_scale returning y then x", [
        (T + 'board_frame.py', '    return float(r.x[0]), float(r.x[1])', '    return float(r.x[1]), float(r.x[0])')], ['py', 'site']),
    ('M2a', 'the solder side not mirrored: registration.json\'s transform says y is flipped, not x', [
        (D + 'registration.json', '"mirror": "x_m = widthPx - 1 - x; y unchanged"', '"mirror": "y_m = heightPx - 1 - y; x unchanged"')], ['site']),
    ('M2b', 'the solder side not mirrored: board_register.py reads I2 unflipped, run on the real scans', [
        (T + 'board_register.py', "i2m = common.read_rgb('I2', mirror=True)", "i2m = common.read_rgb('I2', mirror=False)")], ['py', 'run']),
    ('M3a', "one drill's component-side centre moved 0.5 mm, the drill kept at its faces' mean", [
        (D + 'registration.json', '{"bottom":[21.476,1.88],"bottomPx":[4636.176,78.965],"d":null,"filled":true,"spreadMm":0.121,"top":[21.59,1.841],"x":21.533,"y":1.861}',
         '{"bottom":[21.476,1.88],"bottomPx":[4636.176,78.965],"d":null,"filled":true,"spreadMm":0.121,"top":[22.09,1.841],"x":21.783,"y":1.861}')], ['site']),
    ('M3b', "one matched hole's held-out error 0.5 mm larger", [
        (D + 'registration.json', '[1.182,661,1.032,545,0.141,"blob",21.59,1.841]', '[1.182,661,1.032,545,0.641,"blob",21.59,1.841]')], ['site']),
    ('M4a', "one key centre moved 3 mm (Q's x), in case.json and the parts module", [
        (D + 'case.json', '   "name": "Q",\n   "rotation": -0.3484,\n   "row": 2,\n   "w": 19.05,\n   "x": 108.5068,',
         '   "name": "Q",\n   "rotation": -0.3484,\n   "row": 2,\n   "w": 19.05,\n   "x": 111.5068,'),
        (CPARTS, '"name":"Q","rotation":-0.348,"row":2,"w":19.05,"x":108.507,', '"name":"Q","rotation":-0.348,"row":2,"w":19.05,"x":111.507,')], ['site']),
    ('M4b', "one key centre moved 3 mm in keys.json (K1's centre for Q)", [
        (D + 'keys.json', '   "name": "Q",\n   "row": 2,\n   "units": 1.0,\n   "w": 19.05,\n   "x": 104.775,',
         '   "name": "Q",\n   "row": 2,\n   "units": 1.0,\n   "w": 19.05,\n   "x": 107.775,')], ['site']),
    ('M5a', "two keys' names swapped (Q and W) in keys.json, case.json and the parts module alike", [
        *swap(D + 'keys.json', '"name": "Q"', '"name": "W"'), *swap(D + 'case.json', '"name": "Q"', '"name": "W"'),
        *swap(CPARTS, '"name":"Q"', '"name":"W"')], ['site']),
    ('M5b', "two keys swapped in keys.py's map from K1's references (Q and W, with K1's values)", [
        (T + 'keys.py', "'SW2': ('Q', 'Q'), 'SW10': ('W', 'W')", "'SW2': ('W', 'W'), 'SW10': ('Q', 'Q')")], ['py', 'site']),
    ('M6', 'a legend not from LEGENDS: the layout prints the key\'s name', [
        (CLAYOUT, "return key === 'Space' ? '' : LEGENDS[key];", "return key === 'Space' ? '' : key.toUpperCase();")], ['site']),
    ('M7a', 'the case width 410 in the parts module', [
        (CPARTS, 'export const CASE = {"width":415.0,', 'export const CASE = {"width":410.0,')], ['site']),
    ('M7b', 'the case width 410 in case.json, the results file and the parts module together', [
        (D + 'case.json', '"widthMm": 415.0', '"widthMm": 410.0', 'all'), (CRES, '"widthMm": 415.0', '"widthMm": 410.0', 'all'),
        (CPARTS, 'export const CASE = {"width":415.0,', 'export const CASE = {"width":410.0,')], ['site']),
    ('M7c', 'the case width 410 typed into the scene', [
        (CSCENE, 'export const X = (x) => (x - CASE.width / 2) / 10;', 'export const X = (x) => (x - 410 / 2) / 10;')], ['site']),
    ('M8a', "the LED bits swapped in the page's decoding (CAPS LOCK bit 2, SHIFT LOCK bit 1)", [
        (PANEL, 'caps: (bits & 2) !== 0, shiftLock: (bits & 4) !== 0', 'caps: (bits & 4) !== 0, shiftLock: (bits & 2) !== 0')], ['site']),
    ('M8b', "the LED bits swapped in the machine's BbcLeds (CAPS LOCK 4, SHIFT LOCK 2)", [
        (CHIP, '    CapsLock = 2,', '    CapsLock = 4,'), (CHIP, '    ShiftLock = 4,', '    ShiftLock = 2,')], ['cs', 'site']),
    ('M8c', "the lights' names swapped in the outside's layout (motor and CAPS LOCK)", [
        (CLAYOUT, 'motor: LEDS.leds[0].name, caps: LEDS.leds[1].name', 'motor: LEDS.leds[1].name, caps: LEDS.leds[0].name')], ['site']),
    ('M9', 'the motor bit read from bit 6', [
        (BUS, '((SerialUlaControl & 0x80) != 0 ? BbcLeds.CassetteMotor', '((SerialUlaControl & 0x40) != 0 ? BbcLeds.CassetteMotor')], ['cs']),
    ('M10a', "a counter on the wrong chip: the ADC's reads counted as the ACIA's", [
        (BUS, '            case >= 0xC0 and <= 0xDF:\n                unchecked { _adcAccesses++; }\n                break;\n        }\n\n        return AbsentSheila',
         '            case >= 0xC0 and <= 0xDF:\n                unchecked { _aciaAccesses++; }\n                break;\n        }\n\n        return AbsentSheila')], ['cs']),
    ('M10b', "a counter on the wrong chip in the parts: IC3 counted as the user VIA and IC69 as the system VIA, in board_parts.py's SHEILA_OF", [
        (T + 'board_parts.py', "'IC3': 'systemVia',", "'IC3': 'userVia',"), (T + 'board_parts.py', "'IC69': 'userVia',", "'IC69': 'systemVia',")], ['py', 'site']),
    ('M10c', 'a counter on the wrong chip in the parts module (IC3 and IC69\'s counters swapped)', [
        *swap(PARTS, '"fitted":true,"sheila":"systemVia"', '"fitted":true,"sheila":"userVia"')], ['site']),
    ('M11', 'Peek counting: a peek of the system VIA adds to its counter', [
        (BUS, 'public byte Peek(ushort address) => Decode(address, sideEffects: false);',
         'public byte Peek(ushort address) { if (address >= 0xFE40 && address <= 0xFE5F) unchecked { _systemViaAccesses++; } return Decode(address, sideEffects: false); }')], ['cs']),
    ('M12a', 'IC3 and IC69 swapped in the parts module (each at the other\'s place)', [
        *swap(PARTS, '{"ref":"IC3","part":"6522","role":"the internal (system) VIA"', '{"ref":"IC69","part":"6522","role":"the external (user) VIA"')], ['site']),
    ('M12b', 'IC3 and IC69 swapped the same way in parts.json and the parts module together', [
        *swap(D + 'parts.json', '"IC3": {', '"IC69": {', every=True),
        *swap(PARTS, '{"ref":"IC3","part":"6522","role":"the internal (system) VIA"', '{"ref":"IC69","part":"6522","role":"the external (user) VIA"')], ['site']),
    ('M13a', 'IC78 marked not fitted in the parts module', [
        (PARTS, '"fitted":true,"sheila":"fdc8271"', '"fitted":false,"sheila":"fdc8271"')], ['site']),
    ('M13b', "IC78 not fitted everywhere: the parts module, parts.json's list and the fact sheet's table", [
        (PARTS, '"fitted":true,"sheila":"fdc8271"', '"fitted":false,"sheila":"fdc8271"'),
        (D + 'parts.json', ' "IC77", "IC78", "IC79",', ' "IC77", "IC79",'),
        (FACTS, '| IC78 | 8271 | yes |', '| IC78 | 8271 | no |')], ['site']),
    ('M14a', 'a module claimed twice: the BBC Micro claims bbc-micro-case for both views', [
        (REGISTRY, CLAIM, CLAIM.replace('"module": "bbc-micro-board"', '"module": "bbc-micro-case"'))], ['site']),
    ('M14b', "a module claimed by two machines: validateRegistry's check taken out", [
        (REGISTRY_JS, "      if (modules.has(model.module)) errors.push(`${where}: module ${model.module} is claimed twice in the registry`);\n", '')], ['site']),
    ('M15', 'the cased rule removed (a cased machine that claims a model claims both)', [
        (REGISTRY_JS, "  if (hasCase === true && !(views.includes('outside') && views.includes('inside'))) errors.push(", "  if (false) errors.push(")], ['site']),
    ('M16', 'the rule that a machine claiming a model states case removed', [
        (REGISTRY_JS, '  if (hasCase === undefined) errors.push(`${where}: a machine that claims a model must state case', '  if (false) errors.push(`${where}: a machine that claims a model must state case')], ['site']),
    ('M17', 'a running machine required to claim a model', [
        (REGISTRY_JS, "  if (machine.models === undefined) return errors;", "  if (machine.models === undefined) return machine.status === 'running' ? [...errors, `${where}: a running machine must claim a model`] : errors;")], ['site']),
    ('M18a', "the inside's track map missing from the deploy list", [
        (DEPLOY, ' models/bbc-micro-board-tracks.webp; do', '; do')], ['site']),
    ('M18b', "the outside's bundle missing from the deploy list", [
        (DEPLOY, ' models/bbc-micro-case.js models/bbc-micro-board.js', ' models/bbc-micro-board.js')], ['site']),
    ('M19a', "a colour literal in the outside's scene (the lit light)", [
        (CSCENE, "ledOn: new MeshBasicMaterial({ color: token('model-led'), toneMapped: false })", "ledOn: new MeshBasicMaterial({ color: '#ff3020', toneMapped: false })")], ['site']),
    ('M19b', "a colour literal in the inside's scene (the access tint)", [
        (SCENE, "active: new MeshBasicMaterial({ color: token('model-active'),", "active: new MeshBasicMaterial({ color: 0x40ff40,")], ['site']),
    ('M20', "the track map over budget (600,001 bytes, copper.json's size and hash made to match)", over_budget, ['site']),
    # ---- The re-plan's additions ----
    ('R1', 'the BBC Micro claiming only the inside, with "case": true', [
        (REGISTRY, CLAIM, '      "case": true,\n      "models": [\n        {\n          "view": "inside",\n          "module": "bbc-micro-board"\n        }\n      ],')], ['site']),
    ('R2', 'the BBC Micro with no case', [
        (REGISTRY, CLAIM, CLAIM.replace('      "case": true,\n', ''))], ['site']),
    ('R3', "access.mjs reading the counted list one place out", [
        (ACCESS, '[chip, { moved: d[i] > 0, perSecond: Math.round(d[i] / seconds) }]', '[chip, { moved: d[(i + 1) % d.length] > 0, perSecond: Math.round(d[(i + 1) % d.length] / seconds) }]')], ['site']),
    ('R3b', "the board layout's COUNTED one place out (the ACIA and the serial ULA swapped)", [
        (LAYOUT, "export const COUNTED = ['crtc', 'acia', 'serialUla',", "export const COUNTED = ['crtc', 'serialUla', 'acia',")], ['site']),
    ('R4', "a counter on the wrong field in the inline decode: a user VIA write counted on the system VIA's field", [
        (BUS, '                unchecked { _userViaAccesses++; }\n                UserVia.Write', '                unchecked { _systemViaAccesses++; }\n                UserVia.Write')], ['cs']),
    ('R5a', "the bbc:start event not sent after a new machine (the page)", [
        (PANEL, "  panel.dispatchEvent(new CustomEvent('bbc:start'));\n", '')], ['site']),
    ('R5b', "the inside not taking a fresh baseline on bbc:start", [
        (BOARD, '    status.textContent = STATUS.running;\n    restart();\n', '    status.textContent = STATUS.running;\n')], ['site']),
    ('R5c', 'the outside not letting its keys go on bbc:start', [
        (CASE, '    keys.reset();\n', '')], ['site']),
    ('R6', "the map's BUDGETS entry missing", [
        (MODEL_TEST, ", 'bbc-micro-board': 600_000 };", ' };')], ['site']),
    ('R7', 'a references entry without its sha256 (K1)', [
        (REGISTRY, '          "sha256": "35b34ac3ef0a04fb311ee1971eb9fc32cb1c323877377c4d1a74986c709b0929",\n', '')], ['site']),
    ('R8a', "task 4's nets result changed to pass in copper.json", [
        (D + 'copper.json', '  "i5Registration": "pass",\n  "nets": "fail"', '  "i5Registration": "pass",\n  "nets": "pass"')], ['site']),
    ('R8b', "task 4's nets result changed in the plan's words (the outcome says it passed)", [
        ('docs/superpowers/plans/2026-10-04-bbc-micro-models.md', '**The known nets FAILED**', '**The known nets passed**')], ['site']),
    # ---- The modules: scene, layout, notes, entries ----
    ('S1', 'the inside draws the unfitted ICs too', [
        (LAYOUT, 'export const FITTED = ICS.filter((ic) => ic.fitted);', 'export const FITTED = ICS;')], ['site']),
    ('S2', 'the paged slots swapped (15 the DFS, 14 BASIC)', [
        (LAYOUT, "export const PAGED_SLOTS = { 15: 'BASIC', 14: 'DFS' };", "export const PAGED_SLOTS = { 15: 'DFS', 14: 'BASIC' };")], ['site']),
    ('S3', 'the legend says the processor is marked like the others', [
        (LAYOUT, "  cpu: 'in use all the time: it is the processor',", "  cpu: 'marked when the processor reads or writes it',")], ['site']),
    ('S4', "the inside's sampler every 500 ms", [
        (BOARD, 'const SAMPLE_MS = 250;', 'const SAMPLE_MS = 500;')], ['site']),
    ('S5', "the inside's caption without \"traced to look at\"", [
        (LAYOUT, 'traced from flatbed scans of a bare board, to look at: the copper', 'traced from flatbed scans of a bare board: the copper')], ['site']),
    ('S6', "the inside's caption typing the chip count", [
        (LAYOUT, 'On it are the ${FITTED.length} chips fitted', 'On it are the 88 chips fitted')], ['site']),
    ('S7', "the board's note says the nets check passed", [
        (NOTES, "that the ground pins of ${nets.chips} chips join in one net and their +5V pins in another, failed:", "that the ground pins of ${nets.chips} chips join in one net and their +5V pins in another, passed:")], ['site']),
    ('S8', "the board's first limit without \"not as a netlist\"", [
        (NOTES, "'The copper is traced to look at, not as a netlist: its connections", "'The copper is traced to look at: its connections")], ['site']),
    ('S9', "the board's note says the parts checks passed", [
        (NOTES, 'return `The two checks on these parts failed.', 'return `The two checks on these parts passed.')], ['site']),
    ('S10', "the board's note types the I5 registration's median", [
        (NOTES, 'to ${mm(i5.heldOutMedianMm)} at the median', 'to 0.363 mm at the median')], ['site']),
    ('S11', "the case's size limit says the measures agree", [
        (CNOTES, 'not measured, and the photographs\' measures point different ways:', 'not measured, and the photographs\' measures agree with it:')], ['site']),
    ('S12', "the case's front edge limit says it matched", [
        (CNOTES, "`The front edge's check could not be measured: no camera height", "`The front edge's check matched the manual's width: no camera height")], ['site']),
    ('S13', "the case's note types the case top's scale", [
        (CNOTES, 'the keys read ${times(w.caseTopScale)} times', 'the keys read 1.039 times')], ['site']),
    ('S14', "the case's limits drop the owl", [
        (CNOTES, "    'The badge strip carries its words only: the BBC\\'s owl beside them is not drawn.',\n", '')], ['site']),
    ('S15', 'an owl in the case scene (a comment naming it)', [
        (CSCENE, "export const X = (x) => (x - CASE.width / 2) / 10;", "// the badge strip's owl\nexport const X = (x) => (x - CASE.width / 2) / 10;")], ['site']),
    ('S16', 'a tap held 1400 ms on the model', [
        (CLAYOUT, 'export const TAP_MS = 140;', 'export const TAP_MS = 1400;')], ['site']),
    ('S17', "the outside's lights never set (the entry)", [
        (CASE, '      model.setLeds(lit);\n', '')], ['site']),
    ('S18', "the outside's caption says the measures agree", [
        (CLAYOUT, "the Service Manual's size, though the phot", "the Service Manual's size, which the phot")], ['site']),
    ('S19', "the space bar given SPACE", [
        (CLAYOUT, "return key === 'Space' ? '' : LEGENDS[key];", 'return LEGENDS[key];')], ['site']),
    ('S20', 'SHIFT puts down only one keycap', [
        (CLAYOUT, 'return KEYS.keys.filter((k) => machineKey(k.name) === name).map((k) => k.name);', 'return KEYS.keys.filter((k) => k.name === name).map((k) => k.name);')], ['site']),
    ('S21', 'MODELS: the inside without its tracksHelp', [
        (MODELS, '    tracksHelp: BBC_TRACKS_HELP,\n', '')], ['site']),
    # ---- The credits ----
    ('C1', "I6's author without the byline Chris's Acorns asks for",
        in_entry(REGISTRY, 'bab4176a', '"author": "Chris Whytehead (Chris\'s Acorns)",', '"author": "Chris Whytehead",'), ['site']),
    ('C2', "I5's licence given as public domain",
        in_entry(REGISTRY, '7ae4b573', '"licence": null,', '"licence": "public domain",'), ['site']),
    ('C3', "the outside's results file without O8-I among its sources", [
        (CRES, '"id": "O8-I"', '"id": "O8-X"')], ['site']),
    ('C4', "I1's credit no longer says the copper is traced to look at", [
        (REGISTRY, I1_USED, I1_USED.replace(', traced to look at, with their connections not verified', ''))], ['site']),
    # ---- The tools: judges and generators ----
    ('T1', "board_trace.py's nets judge at 75 per cent", [
        (T + 'board_trace.py', 'NETS_MIN_SHARE = 0.90', 'NETS_MIN_SHARE = 0.75')], ['py', 'site']),
    ('T2', "board_trace.py's nets judge ignoring a GND pin and a +5V pin in one net", [
        (T + 'board_trace.py', "'nets': ok(n['chips'] >= NETS_MIN_CHIPS and not n['touching']", "'nets': ok(n['chips'] >= NETS_MIN_CHIPS")], ['py', 'site']),
    ('T3', "board_parts.py's manual check at 20 mm", [
        (T + 'board_parts.py', 'MANUAL_LIMIT_MM = 10.0', 'MANUAL_LIMIT_MM = 20.0')], ['py', 'site']),
    ('T4', "board_parts.py's identity rule letting a digit follow the part", [
        (T + 'board_parts.py', "return re.search(pat + r'(?!\\d)', _norm(marking)) is not None", "return re.search(pat, _norm(marking)) is not None")], ['py', 'site']),
    ('T5', "keys.py's judge_keys passing a median of 3 mm", [
        (T + 'keys.py', "KEYS_PASS = {'median': 1.0, 'p90': 2.0, 'max': 3.0}", "KEYS_PASS = {'median': 3.0, 'p90': 3.0, 'max': 9.0}")], ['py', 'site']),
    ('T6', "case_parts.py's rear check at 6 mm", [
        (T + 'case_parts.py', 'REAR_LIMIT_MM = 2.0', 'REAR_LIMIT_MM = 6.0')], ['py', 'site']),
    ('T7', "case_profile.py's check point at 5 mm", [
        (T + 'case_profile.py', 'CHECK_PASS_MM = 2.0', 'CHECK_PASS_MM = 5.0')], ['py', 'site']),
    ('T8', "results.py taking the x verdict as first written, not as revised", [
        (T + 'results.py', "rows = [v for v in frame['verdicts'] if 'as revised' in v['measure']]", "rows = [v for v in frame['verdicts'] if 'as first written' in v['measure']]")], ['py']),
    ('T9', "results.py's k1Mm from the smaller of the two K1 scales", [
        (T + 'results.py', "k1_mm = round(width / max(case['caseTop']['scale'], case['k1ToCase']['impliedScale']), 1)", "k1_mm = round(width / min(case['caseTop']['scale'], case['k1ToCase']['impliedScale']), 1)")], ['py']),
    ('T10', "case_parts.py's module without CASE's note", [
        (T + 'case_parts.py', "'cornerRadius': case['outline']['cornerRadiusMm'], 'note': f'D1\\'s 415 by 345 by 73 mm with the feet, {width}; the corners\\' radius typical'},",
         "'cornerRadius': case['outline']['cornerRadiusMm']},")], ['py', 'site']),
    ('T11', "board_parts.py's module without each IC's counter", [
        (T + 'board_parts.py', "'fitted': ic['fitted'], 'sheila': SHEILA_OF.get(r)}", "'fitted': ic['fitted'], 'sheila': None}")], ['py', 'site']),
    ('T12', 'the results file\'s board figure edited by hand (the y median)', [
        (BRES, '"yMedianMm": 0.0303', '"yMedianMm": 0.0313')], ['site']),
    # ---- The browser check, where node reaches it ----
    ('B1', 'the frames not cleared before *DUMP', [
        (CHECK, '  await page.evaluate(() => window.bbcInside.paged.clear());\n', '')], ['site']),
    ('B2', 'the *DUMP disc\'s file 1 KB, not 2', [
        (CHECK, 'export function dumpDisc() {', 'export function dumpDisc() { return testDisc(); }\nfunction unusedDumpDisc() {')], ['site']),
    ('B3', 'the DFS looked for in the slot BASIC is in', [
        (CHECK, '  const dfs = pagedRef(14);', '  const dfs = pagedRef(15);')], ['site']),
    ('B4', "the *CAT judged on IC78's row alone, not its mark on the model", [
        (CHECK, "  if (!cat.accessed.includes('IC78') || !cat.rows.includes('IC78')) problem(", "  if (!cat.rows.includes('IC78')) problem(")], ['site']),
    ('B5', 'the motor light judged on *MOTOR 1 only', [
        (CHECK, "  if (!motorOff) problem('*MOTOR 0 did not put the cassette motor light out');\n", '')], ['site']),
]


def run(command, cwd, env=None):
    p = subprocess.run(command, cwd=cwd, capture_output=True, text=True, env=env)
    return p.returncode, p.stdout + p.stderr


def site(root):
    code, text = run(['npm', 'run', 'build'], os.path.join(root, 'site'))
    if code != 0:
        lines = [l for l in text.splitlines() if 'rror' in l]
        return ['site: BUILD FAILED: ' + (lines[0].strip()[:160] if lines else 'exit %d' % code)], None
    code, text = run(['node', '--test', 'tests/*.test.mjs'], os.path.join(root, 'site'))
    failed = []
    tail = text.split('✖ failing tests:', 1)[1] if '✖ failing tests:' in text else ''
    for m in re.finditer(r'^test at (tests/[\w.-]+):\d+:\d+\n✖ (.*?) \(\d', tail, re.M):
        failed.append(f'site: {m.group(1)}: {m.group(2)}')
    n = re.search(r'^ℹ tests (\d+)', text, re.M)
    return sorted(set(failed)), int(n.group(1)) if n else 0


def py(root):
    # No bytecode: a fault and its restore can leave a source file with the same size and second, and a stale
    # .pyc would then run the fault again in the next fault's pytest.
    _, text = run([PYTHON, '-m', 'pytest', 'tools/bbc-micro-model/tests', '-q', '-p', 'no:cacheprovider'], root, dict(os.environ, PYTHONDONTWRITEBYTECODE='1'))
    return sorted(set('py: ' + m for m in re.findall(r'^FAILED (\S+)', text, re.M))) or (['py: ERROR ' + text.strip().splitlines()[-1]] if ' error' in text.splitlines()[-1] else [])


def cs(root):
    code, text = run([DOTNET, 'build', 'tests/Dbhq.Machines.BbcMicro.Tests', '-c', 'Release', '-v', 'q'], root)
    if code != 0 or 'error CS' in text:
        return ['cs: DID NOT BUILD']
    _, text = run([DOTNET, 'test', 'tests/Dbhq.Machines.BbcMicro.Tests', '-c', 'Release', '--no-build', '--logger', 'console;verbosity=normal'], root)
    # A theory's cases are one test here: the class and the method, not the arguments.
    return sorted(set('cs: ' + m.split('(')[0].split('.')[-2] + '.' + m.split('(')[0].split('.')[-1] for m in re.findall(r'^\s*Failed ([\w.]+(?:\(.*\))?)', text, re.M)))


def register(root):
    env = dict(os.environ, PYTHONDONTWRITEBYTECODE='1', BBC_MODEL_INPUTS=os.environ.get('BBC_MODEL_INPUTS', os.path.join(HOME, 'bbc-model-inputs')))
    code, text = run([PYTHON, 'board_register.py'], os.path.join(root, T), env)
    last = [l for l in text.strip().splitlines() if l.strip()][-3:]
    return [f'run: board_register.py exit {code}: ' + ' | '.join(l.strip()[:150] for l in last)]


def apply(root, fault):
    fid, what, edits = fault[:3]
    changed = {}
    if callable(edits):
        for p in edits(root):
            changed.setdefault(os.path.join(root, p), None)
        return changed
    for e in edits:
        path, old, new = e[:3]
        every = len(e) > 3 and e[3] == 'all'
        full = os.path.join(root, path)
        text = open(full, encoding='utf-8').read()
        n = text.count(old)
        if n == 0 or (n != 1 and not every):
            sys.exit(f'{fid}: the text to replace matches {n} times in {path}, not once')
        changed.setdefault(full, None)
        open(full, 'w', encoding='utf-8').write(text.replace(old, new))
    return changed


def restore(root):
    run(['git', 'checkout', '--', '.'], root)
    code, text = run(['git', 'status', '--porcelain', '--untracked-files=no'], root)
    if text.strip():
        sys.exit(f'the worktree is not clean after a fault:\n{text}')


def main(argv):
    if '--list' in argv:
        for f in FAULTS:
            print(f[0], '|', f[1], '|', ', '.join(f[3]))
        return 0
    root = os.path.abspath(argv[argv.index('--root') + 1]) if '--root' in argv else os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    if os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')) == root and '--here' not in argv:
        sys.exit('run it on a scratch worktree (--root), not on the repository it lives in (--here to insist)')
    check = '--check' in argv
    skip = {argv[i + 1] for i, a in enumerate(argv) if a == '--root'}
    chosen = [a for a in argv if not a.startswith('-') and a not in skip]
    restore(root)
    base = None
    if not check:
        failed, base = site(root)
        if failed:
            sys.exit(f'the unmutated site suite fails: {failed}')
        if py(root):
            sys.exit('the unmutated pytest suite fails')
        print(f'unmutated: site {base} tests, none failing; pytest passes', flush=True)
    survived = []
    for fault in FAULTS:
        fid, what, edits, suites = fault
        if chosen and fid not in chosen:
            continue
        try:
            apply(root, fault)
            if check:
                print(fid, 'applies', flush=True)
                continue
            caught = []
            if 'py' in suites:
                caught += py(root)
            if 'cs' in suites:
                caught += cs(root)
            if 'run' in suites:
                caught += register(root)
            if 'site' in suites or 'run' in suites:
                failed, n = site(root)
                caught += failed
                if n is not None and n < base:
                    caught.append(f'site: {base - n} tests did not run')
        finally:
            restore(root)
        if 'run' in suites:
            caught_by_tests = [c for c in caught if not c.startswith('run:')]
        else:
            caught_by_tests = caught
        if not caught_by_tests:
            survived.append(fid)
        print(f'{fid} | {what} | ' + ('; '.join(caught) if caught_by_tests else 'SURVIVED' + ('; ' + '; '.join(caught) if caught else '')), flush=True)
    if not check:
        print('survived:', ' '.join(survived) or 'none')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
