import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { REPO_ROOT, loadRegistry, validateRegistry } from '../src/lib/registry.mjs';
import { NES_PAD, loadTryIt } from '../src/lib/machines.mjs';
import { BUTTONS } from '../public/nes-keys.js';
import { pin } from '../src/lib/pins.mjs';
import { NOT_MODELLED, ELSEWHERE } from '../src/lib/nes.mjs';
import { issueUrl } from '../src/lib/bbc-micro.mjs';

// The NES's registry record and the data its page is built from, without a
// build: the record against the design, its rights against the bundled game's
// own record (roms/README.md and machines/nes/try-it.json), its clock against
// the machine's, and the try-it file read the way the page reads it.
// tests/nes-page.test.mjs checks the built page; tests/nes-panel.test.mjs the
// panel's script.

const registry = loadRegistry();
const nes = registry.machines.find((m) => m.id === 'nes');
const tryIt = loadTryIt('nes');
const romsReadme = fs.readFileSync(path.join(REPO_ROOT, 'roms', 'README.md'), 'utf8').replace(/\s+/g, ' ');

test('the NES is one record, by the id nes: the planned nes-famicom row it replaced is gone', () => {
  assert.ok(nes, 'the registry has no record with the id nes');
  assert.equal(registry.machines.filter((m) => /nes|famicom/i.test(m.id) && !/snes|super-famicom/.test(m.id)).length, 1);
  assert.equal(registry.machines.find((m) => m.id === 'nes-famicom'), undefined);
  assert.deepEqual(validateRegistry(registry), []);
});

test('the record mirrors the BBC Micro\'s, field for field, with the values the design gives', () => {
  const bbc = registry.machines.find((m) => m.id === 'bbc-micro');
  // The photograph comes with the count, as the BBC Micro's did: the site shows
  // a photograph only on a running machine's page (tests/nes-page.test.mjs).
  for (const field of Object.keys(bbc).filter((f) => f !== 'photos' || nes.status === 'running')) assert.ok(field in nes, `the NES has no ${field}, which the BBC Micro has`);
  assert.equal(nes.name, 'NES');
  assert.equal(nes.year, 1983);
  assert.equal(nes.maker, 'Nintendo');
  assert.equal(nes.category, 'console');
  assert.equal(nes.core, '2a03');
  assert.equal(nes.acceptance, 'NesAcceptanceTests');
  // A cased machine that claims no model yet: the models are their own pull request (issue 68).
  assert.equal(nes.case, true);
  assert.equal('models' in nes, false);
});

/** A region's CPU clock in MHz, from its `cpuHz:` in Region.cs, worked out as the C# does. */
const clockOf = (region) => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Nes', 'Region.cs'), 'utf8');
  const block = source.slice(source.indexOf(`public static Region ${region} `));
  const expression = /cpuHz: ([\d_.]+(?: \/ [\d_.]+)*),/.exec(block)[1];
  const [first, ...divisors] = expression.split(' / ').map((n) => Number(n.replaceAll('_', '')));
  return divisors.reduce((value, d) => value / d, first) / 1e6;
};

test('the cpu field names both chips with the machine\'s own clocks, NTSC first, so the page reads the NTSC clock', () => {
  const ntsc = clockOf('Ntsc').toFixed(2);
  const pal = clockOf('Pal').toFixed(2);
  assert.equal(ntsc, '1.79');
  assert.match(nes.cpu, new RegExp(`2A03 at ${ntsc.replace('.', '\\.')} MHz \\(NTSC\\)`));
  assert.match(nes.cpu, new RegExp(`2A07 at ${pal.replace('.', '\\.')} MHz \\(PAL\\)`));
  // The page takes the first clock it finds (src/pages/machines/[id].astro).
  assert.equal(/([\d.]+) MHz/.exec(nes.cpu)[1], ntsc);
});

test('the rights say there is no system ROM, that commercial games are the visitor\'s own, and whose the bundled game is, as the game\'s own record says', () => {
  assert.match(nes.rights, /no system ROM/);
  assert.match(nes.rights, /Commercial games are not bundled/);
  assert.match(nes.rights, /load your own/);
  // The bundled title, its author and its licence, as try-it.json gives them.
  assert.ok(nes.rights.includes(tryIt.title), `the rights do not name ${tryIt.title}`);
  assert.ok(nes.rights.includes(`by ${tryIt.author}`), `the rights do not name ${tryIt.author}`);
  assert.equal(tryIt.licence, 'public domain');
  assert.ok(nes.rights.includes(`into the ${tryIt.licence}`), 'the rights do not give the licence try-it.json gives');
  // The author's own words, and where our copy came from, as roms/README.md records them.
  const quote = 'It is free of charge, released into Public Domain, and provided "as is", without warranty or responsibility of any kind.';
  assert.ok(nes.rights.includes(quote), 'the rights do not quote the manual');
  assert.ok(romsReadme.includes(quote), 'roms/README.md no longer quotes the manual as the rights do');
  const archive = 'https://web.archive.org/web/20160327132502id_/http://shiru.untergrund.net/files/nes/lan_master.zip';
  const zip = '79410cc133f1100d2fe5409fd49297a62afc748aeeb5faccb8fe69c68c16e6aa';
  for (const fact of [archive, zip]) {
    assert.ok(nes.rights.includes(fact), `the rights do not give ${fact}`);
    assert.ok(romsReadme.includes(fact), `roms/README.md does not give ${fact}`);
  }
});

test('the notes say what is modelled and every part left out, and the page lists each left-out part with its own issue', () => {
  for (const part of ['NTSC and PAL', 'NROM, MMC1, UxROM, CNROM, AxROM and MMC3']) assert.ok(nes.notes.includes(part), `the notes do not say ${part}`);
  const notOf = nes.notes.slice(nes.notes.indexOf('Not modelled:'));
  for (const part of ['Dendy', 'Famicom Disk System', 'expansion audio', 'Zapper', 'unlicensed', 'analogue', 'battery']) {
    assert.ok(notOf.includes(part), `the notes do not leave out ${part}`);
    assert.ok(NOT_MODELLED.some((m) => m.part.includes(part)), `the page's list has no ${part}`);
  }
  assert.deepEqual(NOT_MODELLED.map((m) => m.issue), [61, 62, 63, 64, 65, 69, 66]);
  assert.deepEqual(Object.values(ELSEWHERE), [67, 68]);
  assert.equal(issueUrl(61), 'https://github.com/dbhq-uk/6502/issues/61');
});

test('every place that says how many cartridge boards are modelled says as many as Cartridge.SupportedMappers holds, and the notes name each', () => {
  const source = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Nes', 'Cartridge.cs'), 'utf8');
  const list = /SupportedMappers \{ get; \} = \[([\d,\s]+)\];/.exec(source);
  assert.ok(list, 'Cartridge.cs no longer declares SupportedMappers as a list of numbers');
  const n = list[1].split(',').map((x) => x.trim()).filter(Boolean).length;
  const word = ['no', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten', 'eleven', 'twelve'][n];
  // The registry's notes: "<n> cartridge boards: A, B, ... and Z."
  const named = new RegExp(`${word} cartridge boards: ([^.]+)\\.`).exec(nes.notes);
  assert.ok(named, `the notes do not say "${word} cartridge boards"`);
  assert.equal(named[1].split(/, | and /).length, n, `the notes name ${named[1]}, not ${n} boards`);
  const sources = {
    'README.md': [fs.readFileSync(path.join(REPO_ROOT, 'README.md'), 'utf8').replace(/\s+/g, ' '), `${word} cartridge boards`],
    'src/pages/machines/[id].astro': [fs.readFileSync(path.join(process.cwd(), 'src', 'pages', 'machines', '[id].astro'), 'utf8'), `on the ${word} boards`],
    'src/lib/nes.mjs': [fs.readFileSync(path.join(process.cwd(), 'src', 'lib', 'nes.mjs'), 'utf8'), `beyond these ${word}`],
  };
  for (const [file, [text, phrase]] of Object.entries(sources)) assert.ok(text.includes(phrase), `${file} does not say "${phrase}"`);
});

test('the try-it file is the bundled game, its steps and its controls, and the game is the pinned file', () => {
  assert.equal(tryIt.rom, pin('NesHomebrewPath'));
  assert.equal(tryIt.title, 'Lan Master');
  assert.equal(tryIt.author, 'Shiru');
  assert.ok(tryIt.steps.length > 0 && tryIt.controls.length > 0);
  for (const step of tryIt.steps) assert.ok(step.do && step.says);
  for (const control of tryIt.controls) assert.ok(control.button && control.does);
});

test('the buttons a try-it file may name are the pad\'s: the D-pad for its four directions, and the other four by name', () => {
  assert.deepEqual([...NES_PAD.filter((b) => b !== 'D-pad'), 'Up', 'Down', 'Left', 'Right'].sort(), Object.keys(BUTTONS).sort());
  for (const control of tryIt.controls) assert.ok(NES_PAD.includes(control.button), control.button);
});

test('a try-it file in the wrong shape, or naming any game but the pinned one, is refused', () => {
  const good = JSON.parse(fs.readFileSync(path.join(REPO_ROOT, 'machines', 'nes', 'try-it.json'), 'utf8'));
  const cases = [
    [{ ...good, steps: [] }, /steps must be a list of \{ do, says \}/],
    [{ ...good, steps: [{ do: 'Press Start.' }] }, /step 1 needs do and says/],
    [{ ...good, steps: [{ keys: '[GO]', says: 'x' }] }, /step 1 needs do and says/],
    [{ ...good, controls: [] }, /controls must be a list of \{ button, does \}/],
    [{ ...good, controls: [{ button: 'Turbo', does: 'fires' }] }, /"Turbo" is not a button on the NES pad/],
    [{ ...good, controls: [{ button: 'A' }] }, /control 1 needs button and does/],
    [{ ...good, title: '' }, /needs its title/],
    [{ ...good, licence: undefined }, /needs its licence/],
    [{ ...good, rom: 'roms/nes/Another.nes' }, /names roms\/nes\/Another\.nes, not the pinned roms\/nes\/Lan_Master\.nes/],
  ];
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'nes-try-'));
  try {
    fs.mkdirSync(path.join(root, 'machines', 'nes'), { recursive: true });
    fs.mkdirSync(path.join(root, 'roms', 'nes'), { recursive: true });
    for (const [file, why] of cases) {
      fs.writeFileSync(path.join(root, 'machines', 'nes', 'try-it.json'), JSON.stringify(file));
      assert.throws(() => loadTryIt('nes', root), why, JSON.stringify(file).slice(0, 120));
    }
    // The pinned name, but not the pinned bytes: the game is read and its hash checked.
    fs.writeFileSync(path.join(root, 'machines', 'nes', 'try-it.json'), JSON.stringify(good));
    assert.throws(() => loadTryIt('nes', root), /ENOENT/);
    fs.writeFileSync(path.join(root, good.rom), Buffer.from('NES\x1a'));
    assert.throws(() => loadTryIt('nes', root), /does not match its pinned hash/);
    fs.copyFileSync(path.join(REPO_ROOT, good.rom), path.join(root, good.rom));
    assert.deepEqual(loadTryIt('nes', root), good);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
