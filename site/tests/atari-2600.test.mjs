import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { REPO_ROOT, loadRegistry, validateRegistry } from '../src/lib/registry.mjs';
import { ATARI_2600_CONTROLS, MACHINE_BUILDS, loadTryIt } from '../src/lib/machines.mjs';
import { atari2600Roms, pin } from '../src/lib/pins.mjs';
import { BOARDS, CONTROL_INPUTS, ELSEWHERE, NOT_MODELLED, controlKeys } from '../src/lib/atari-2600.mjs';
import { issueUrl } from '../src/lib/bbc-micro.mjs';
import { STICK_KEYS, SWITCH_KEYS, KEY_NAMES, SWITCHES, STICK } from '../public/atari-2600-keys.js';

// The Atari 2600's registry record and the data its page is built from,
// without a build: the record against the design, its rights against the
// bundled game's own record (its folder's README and manifest, and its try-it
// file), its clock against the machine's, its boards against Cartridge.cs, and
// the try-it file read the way the page reads it.
// tests/atari-2600-page.test.mjs checks the built page; tests/atari-2600-panel.test.mjs the panel's script.

const registry = loadRegistry();
const vcs = registry.machines.find((m) => m.id === 'atari-2600');
const tryIt = loadTryIt('atari-2600');
const [game] = atari2600Roms();
const folder = path.join(REPO_ROOT, 'machines', 'atari-2600', 'cartridges', '2048');
const gameReadme = fs.readFileSync(path.join(folder, 'README.md'), 'utf8').replace(/\s+/g, ' ');
const gameManifest = JSON.parse(fs.readFileSync(path.join(folder, 'manifest.json'), 'utf8'));
const notice = fs.readFileSync(path.join(REPO_ROOT, 'NOTICE.md'), 'utf8');

test('the Atari 2600 is one record, by the id atari-2600, and the registry validates', () => {
  assert.ok(vcs, 'the registry has no record with the id atari-2600');
  assert.equal(registry.machines.filter((m) => /2600/.test(m.id)).length, 1);
  assert.deepEqual(validateRegistry(registry), []);
});

test('the record mirrors the NES\'s, field for field, with the values the design gives, and claims no 3D model', () => {
  const nes = registry.machines.find((m) => m.id === 'nes');
  // The NES has a priority and its models' references; the Atari 2600 has neither, and no models (issue 129).
  for (const field of Object.keys(nes).filter((f) => !['priority', 'models', 'references'].includes(f))) assert.ok(field in vcs, `the Atari 2600 has no ${field}, which the NES has`);
  assert.equal(vcs.name, 'Atari 2600');
  assert.equal(vcs.year, 1977);
  assert.equal(vcs.maker, 'Atari');
  assert.equal(vcs.category, 'console');
  assert.equal(vcs.core, 'nmos');
  assert.equal(vcs.status, 'running');
  assert.equal(vcs.acceptance, 'Atari2600AcceptanceTests');
  assert.equal(vcs.case, true);
  // The design: no models field until both models exist (issue 129), so the registry's rule that a cased
  // machine claims both views never stops it counting.
  assert.equal('models' in vcs, false);
  assert.equal(ELSEWHERE.models, 129);
  const tests = path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.Atari2600.Tests', 'Atari2600AcceptanceTests.cs');
  assert.match(fs.readFileSync(tests, 'utf8'), /public sealed class Atari2600AcceptanceTests\b/);
});

/** A region's CPU clock in MHz, from its colourClockHz in Region.cs, over three, as Region.CpuHz works it out. */
const clockOf = (region) => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Atari2600', 'Region.cs'), 'utf8');
  assert.match(source, /public double CpuHz => ColourClockHz \/ 3;/);
  const block = source.slice(source.indexOf(`public static Region ${region} `));
  const expression = /colourClockHz: ([\d_.]+(?: [*/] [\d_.]+)*),/.exec(block)[1];
  const [first, ...rest] = expression.split(' ');
  let value = Number(first.replaceAll('_', ''));
  for (let i = 0; i < rest.length; i += 2) value = rest[i] === '*' ? value * Number(rest[i + 1].replaceAll('_', '')) : value / Number(rest[i + 1].replaceAll('_', ''));
  return value / 3 / 1e6;
};

test('the cpu field names the 6507 with each region\'s clock from Region.cs, NTSC first, so the page reads the NTSC clock', () => {
  const ntsc = clockOf('Ntsc').toFixed(2);
  const pal = clockOf('Pal').toFixed(2);
  assert.equal(vcs.cpu, `6507 at ${ntsc} MHz (NTSC), ${pal} MHz (PAL)`);
  assert.equal(/([\d.]+) MHz/.exec(vcs.cpu)[1], ntsc);
});

test('the rights say there is no system ROM, that commercial games are the visitor\'s own, and whose the bundled game is, as its folder records it', () => {
  assert.match(vcs.rights, /no system ROM/);
  assert.match(vcs.rights, /Commercial games are not bundled/);
  assert.match(vcs.rights, /load your own, which is read in this tab and not uploaded/);
  assert.ok(vcs.rights.includes(tryIt.title), `the rights do not name ${tryIt.title}`);
  assert.ok(vcs.rights.includes(`by ${tryIt.author}`), `the rights do not name ${tryIt.author}`);
  assert.ok(vcs.rights.includes(`${tryIt.licence} licence`), 'the rights do not give the licence');
  assert.equal(gameManifest.licence, tryIt.licence);
  // Both copyright lines, as the licence file and the source's notice carry them.
  for (const line of ['Copyright (c) 2014 Carlos Duarte do Nascimento (Chester)', 'Original 2048 game Copyright (c) 2014 Gabriele Cirulli']) {
    assert.ok(vcs.rights.includes(line), `the rights do not quote "${line}"`);
    assert.ok(notice.includes(line), `NOTICE.md does not carry "${line}"`);
  }
  // Where our copy came from, and its hash, as the folder records them and the pins hold them.
  for (const fact of [gameManifest.imageFrom, gameManifest.sha256, gameManifest.commit, pin('Atari2600HomebrewCommit'), game.sha256]) {
    assert.ok(vcs.rights.includes(fact), `the rights do not give ${fact}`);
    assert.ok(gameReadme.includes(fact), `the game's README does not give ${fact}`);
  }
  assert.match(vcs.rights, /The cartridge art, by Jenn Harrison, is not in the ROM/);
  assert.ok(gameReadme.includes('None of it is in the ROM'));
  assert.ok(vcs.rights.includes('machines/atari-2600/cartridges/2048/'));
});

test('the notes name every board Cartridge.cs models and every part left out, and the page lists each left-out part with its own issue', () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Atari2600', 'Cartridge.cs'), 'utf8');
  const boards = [...source.matchAll(/\(\d+, "(\w+)", Family\.\w+\)/g)].map((m) => m[1]);
  assert.ok(boards.length > 3, 'Cartridge.cs no longer lists its boards as (size, "name", family)');
  const modelled = vcs.notes.slice(0, vcs.notes.indexOf('Not modelled:'));
  for (const board of boards) assert.match(modelled, new RegExp(`\\b${board}\\b`), `the notes do not name ${board}`);
  assert.ok(modelled.includes('F8, F6 and F4 with the Superchip'));
  for (const part of ['NTSC and PAL', 'two joysticks and the console switches', 'two sound channels']) assert.ok(modelled.includes(part), `the notes do not say ${part}`);
  const notOf = vcs.notes.slice(vcs.notes.indexOf('Not modelled:'));
  for (const part of ['SECAM', 'paddles', 'keypads', 'driving controller', 'peripherals', 'analogue', 'board']) {
    assert.ok(notOf.includes(part), `the notes do not leave out ${part}`);
    assert.ok(NOT_MODELLED.some((m) => m.part.includes(part)), `the page's list has no ${part}`);
  }
});

test('the page links every issue the plan names: the parts left out, each board not modelled, the speed, the models and the captures', () => {
  // The plan's Global Constraints: SECAM 106, the boards 107 and 131 to 155, the paddles 108, the keypads 124,
  // the driving controller 125, board detection 126, other peripherals 127, the analogue picture 128.
  assert.deepEqual(NOT_MODELLED.map((m) => m.issue).sort((a, b) => a - b), [106, 107, 108, 124, 125, 126, 127, 128]);
  assert.deepEqual(BOARDS.map((b) => b.issue), Array.from({ length: 155 - 131 + 1 }, (_, i) => 131 + i));
  assert.equal(new Set(BOARDS.map((b) => b.board)).size, BOARDS.length);
  assert.deepEqual(ELSEWHERE, { speed: 130, models: 129, captures: 162 });
  assert.equal(issueUrl(131), 'https://github.com/dbhq-uk/6502/issues/131');
});

test('the machine is built for the site from its own WebAssembly project, with the game its page puts in', () => {
  assert.equal(MACHINE_BUILDS['atari-2600'].project, 'Dbhq.Machines.Atari2600.Wasm');
  assert.ok(fs.existsSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Atari2600.Wasm', 'Dbhq.Machines.Atari2600.Wasm.csproj')));
  assert.deepEqual(MACHINE_BUILDS['atari-2600'].roms().map((r) => [r.path, r.file, r.sha256]), [[pin('Atari2600HomebrewPath'), '2048.bin', pin('Atari2600HomebrewSha256')]]);
});

test('the try-it file is the bundled game, its steps, its controls and its credit, and the game is the pinned file', () => {
  assert.equal(tryIt.rom, pin('Atari2600HomebrewPath'));
  assert.equal(tryIt.title, '2048 2600');
  assert.equal(tryIt.author, gameManifest.author);
  assert.equal(tryIt.licenceUrl, gameManifest.licenceUrl);
  assert.ok(tryIt.credit.includes(tryIt.author) && tryIt.credit.includes(tryIt.licence));
  assert.ok(tryIt.steps.length > 0 && tryIt.controls.length > 0);
});

test('every control a try-it file may name has its keys on the page\'s map, and the keys are the map\'s', () => {
  assert.deepEqual(Object.keys(CONTROL_INPUTS).sort(), [...ATARI_2600_CONTROLS].sort());
  for (const [button, control] of Object.entries(CONTROL_INPUTS)) {
    if (control.switch) assert.ok(control.switch in SWITCHES, `${button}: ${control.switch} is not a console switch`);
    else for (const input of control.inputs) assert.ok(input in STICK, `${button}: ${input} is not a joystick input`);
    assert.ok(controlKeys(button).length > 0, `${button} has no key`);
  }
  assert.equal(controlKeys('Joystick'), ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].map((c) => KEY_NAMES[c]).join(', ').replace(/, ([^,]*)$/, ' and $1'));
  assert.equal(controlKeys('Left fire button'), KEY_NAMES[Object.keys(STICK_KEYS).find((c) => STICK_KEYS[c].stick === 0 && STICK_KEYS[c].input === 'Fire')]);
  assert.equal(controlKeys('Right fire button'), KEY_NAMES[Object.keys(STICK_KEYS).find((c) => STICK_KEYS[c].stick === 1 && STICK_KEYS[c].input === 'Fire')]);
  assert.equal(controlKeys('GAME SELECT'), KEY_NAMES[Object.keys(SWITCH_KEYS).find((c) => SWITCH_KEYS[c] === 'Select')]);
  assert.throws(() => controlKeys('Turbo'), /not one of the Atari 2600's controls/);
  for (const control of tryIt.controls) assert.ok(ATARI_2600_CONTROLS.includes(control.button), control.button);
});

test('a try-it file in the wrong shape, or naming any game but the pinned one, is refused', () => {
  const good = JSON.parse(fs.readFileSync(path.join(REPO_ROOT, 'machines', 'atari-2600', 'try-it.json'), 'utf8'));
  const cases = [
    [{ ...good, steps: [] }, /steps must be a list of \{ do, says \}/],
    [{ ...good, steps: [{ do: 'Press Start.' }] }, /step 1 needs do and says/],
    [{ ...good, steps: [{ keys: '[GO]', says: 'x' }] }, /step 1 needs do and says/],
    [{ ...good, controls: [] }, /controls must be a list of \{ button, does \}/],
    [{ ...good, controls: [{ button: 'Turbo', does: 'fires' }] }, /"Turbo" is not one of the console's controls/],
    [{ ...good, controls: [{ button: 'Joystick' }] }, /control 1 needs button and does/],
    [{ ...good, title: '' }, /needs its title/],
    [{ ...good, licence: undefined }, /needs its licence/],
    [{ ...good, credit: ' ' }, /needs its credit/],
    [{ ...good, licenceUrl: 'http://example.com/LICENSE' }, /needs its licenceUrl/],
    [{ ...good, rom: 'machines/atari-2600/cartridges/other/other.bin' }, /names machines\/atari-2600\/cartridges\/other\/other\.bin, not the pinned machines\/atari-2600\/cartridges\/2048\/2048\.bin/],
  ];
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'atari-2600-try-'));
  try {
    fs.mkdirSync(path.join(root, 'machines', 'atari-2600', 'cartridges', '2048'), { recursive: true });
    for (const [file, why] of cases) {
      fs.writeFileSync(path.join(root, 'machines', 'atari-2600', 'try-it.json'), JSON.stringify(file));
      assert.throws(() => loadTryIt('atari-2600', root), why, JSON.stringify(file).slice(0, 120));
    }
    // The pinned name, but not the pinned bytes: the game is read and its hash checked.
    fs.writeFileSync(path.join(root, 'machines', 'atari-2600', 'try-it.json'), JSON.stringify(good));
    assert.throws(() => loadTryIt('atari-2600', root), /ENOENT/);
    fs.writeFileSync(path.join(root, good.rom), Buffer.alloc(2048));
    assert.throws(() => loadTryIt('atari-2600', root), /does not match its pinned hash/);
    fs.copyFileSync(path.join(REPO_ROOT, good.rom), path.join(root, good.rom));
    assert.deepEqual(loadTryIt('atari-2600', root), good);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
