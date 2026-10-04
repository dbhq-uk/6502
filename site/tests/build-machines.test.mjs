import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { MACHINE_BUILDS, machineBuilds, readRom } from '../src/lib/machines.mjs';
import { bbcRoms, kim1Roms, pin } from '../src/lib/pins.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';

// scripts/build-machines.mjs publishes a machine by its registry id, with the
// ROMs its pins in Pins.cs name, each read from roms/ and checked before the
// slow publish. The publish itself takes minutes and is run by CI before the
// site is built; these checks are the parts that need no .NET.

const script = fs.readFileSync(path.join(process.cwd(), 'scripts', 'build-machines.mjs'), 'utf8');
const sha256 = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');

test('the builds are the KIM-1 and the BBC Micro, each from its own WebAssembly project, by its registry id', () => {
  assert.deepEqual(Object.keys(MACHINE_BUILDS), ['kim-1', 'bbc-micro']);
  for (const { project } of Object.values(MACHINE_BUILDS)) assert.ok(fs.existsSync(path.join(REPO_ROOT, 'src', project, `${project}.csproj`)), `no project ${project}`);
  const ids = JSON.parse(fs.readFileSync(path.join(REPO_ROOT, 'machines', 'registry.json'), 'utf8')).machines.map((m) => m.id);
  for (const id of Object.keys(MACHINE_BUILDS)) assert.ok(ids.includes(id), `${id} is not a registry id`);
  assert.equal(MACHINE_BUILDS['kim-1'].roms, kim1Roms);
  assert.equal(MACHINE_BUILDS['bbc-micro'].roms, bbcRoms);
});

test('the BBC Micro\'s ROMs are its three pins in Pins.cs, in the order BbcHost.Load takes them, and each file in roms/ matches its pin', () => {
  const roms = bbcRoms();
  assert.deepEqual(roms.map((r) => r.rom), ['os', 'basic', 'dfs']);
  assert.deepEqual(roms.map((r) => r.path), [pin('BbcOsPath'), pin('BbcBasicPath'), pin('BbcDfsPath')]);
  assert.deepEqual(roms.map((r) => r.sha256), [pin('BbcOsSha256'), pin('BbcBasicSha256'), pin('BbcDfsSha256')]);
  const host = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro.Wasm', 'Program.cs'), 'utf8');
  assert.match(host, /public static void Load\(byte\[\] os, byte\[\] basic, byte\[\] dfs, int mode, int sampleRate\)/);
  for (const r of roms) {
    assert.equal(r.file, path.basename(r.path));
    assert.equal(r.url, `https://github.com/dbhq-uk/6502/blob/main/${r.path}`);
    const bytes = readRom(r);
    assert.equal(bytes.length, 16 * 1024, `${r.file} is not 16 KB`);
    assert.equal(sha256(bytes), r.sha256);
  }
});

test('a ROM whose bytes are not its pin\'s, or that is missing, is refused', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'rom-pin-'));
  try {
    fs.mkdirSync(path.join(root, 'roms'));
    fs.writeFileSync(path.join(root, 'roms', 'a.rom'), Buffer.from([1, 2, 3]));
    const right = sha256(Buffer.from([1, 2, 3]));
    assert.deepEqual([...readRom({ path: 'roms/a.rom', sha256: right }, root)], [1, 2, 3]);
    assert.throws(() => readRom({ path: 'roms/a.rom', sha256: '0'.repeat(64) }, root), /roms\/a\.rom does not match its pinned hash 0{64}/);
    assert.throws(() => readRom({ path: 'roms/b.rom', sha256: right }, root), /ENOENT/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('the machines to build are named, each once, and a name with no build is refused', () => {
  assert.deepEqual(machineBuilds(['kim-1']).map((b) => b.id), ['kim-1']);
  assert.deepEqual(machineBuilds(['bbc-micro', 'kim-1', 'bbc-micro']).map((b) => b.id), ['bbc-micro', 'kim-1']);
  assert.throws(() => machineBuilds([]), /name the machines to build/);
  assert.throws(() => machineBuilds(['nes']), /no build for "nes"/);
});

test('the script stops before publishing anything when the machine, or an option, is not one it knows', () => {
  for (const args of [[], ['nes'], ['kim-1', '--aot']]) {
    const run = spawnSync(process.execPath, ['scripts/build-machines.mjs', ...args], { encoding: 'utf8' });
    assert.notEqual(run.status, 0, `build-machines.mjs ${args.join(' ')} succeeded`);
    assert.doesNotMatch(run.stdout, /dotnet publish/);
  }
});

test('every ROM of every machine named is read and checked before the first publish', () => {
  const firstRead = script.indexOf('readRom(r, repo)');
  const firstPublish = script.indexOf("['publish', project");
  assert.ok(firstRead > 0 && firstPublish > 0);
  assert.ok(firstRead < firstPublish, 'a ROM is read after the publish starts');
  // The ROMs are read in a loop of their own over every build, which ends before the publishing loop starts.
  assert.match(script, /for \(const build of builds\) \{\s*build\.roms = build\.roms\(\)\.map/);
});

test('CI and npm run machines publish both machines, each cached on its own inputs, so a change to one rebuilds that one', () => {
  const root = path.resolve(process.cwd(), '..');
  for (const name of ['validate.yml', 'deploy-site.yml']) {
    const text = fs.readFileSync(path.join(root, '.github', 'workflows', name), 'utf8');
    for (const [id, { project }] of Object.entries(MACHINE_BUILDS)) {
      // A cache of that machine's folder alone, keyed on its own projects, the core and what the build reads.
      const step = new RegExp(`id: ${id}\\n\\s+uses: actions/cache@\\S+ # v6\\n\\s+with:\\n\\s+path: site/public/machines/${id}\\n\\s+key: machine-${id}-[^\\n]*hashFiles\\(([^)]*)\\)`).exec(text);
      assert.ok(step, `${name} has no cache of its own for ${id}`);
      for (const input of [`src/${project}/**`, `src/${project.replace(/\.Wasm$/, '')}/**`, 'src/Dbhq.Cpu6502/**', 'Directory.Build.props', 'tests/Dbhq.Cpu6502.TestSupport/Pins.cs', 'site/scripts/build-machines.mjs', 'site/src/lib/pins.mjs', 'site/src/lib/machines.mjs']) {
        assert.ok(step[1].includes(`'${input}'`), `${name}: ${id}'s cache key does not hash ${input}`);
      }
      // A miss adds the machine to the build.
      assert.match(text, new RegExp(`\\|\\| IDS="\\$IDS ${id}"`), `${name} does not build ${id} when its cache misses`);
    }
    assert.match(text, /run: node scripts\/build-machines\.mjs \$IDS\n/, `${name} does not build the machines whose cache missed`);
  }
  const pkg = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8'));
  assert.equal(pkg.scripts.machines, `node scripts/build-machines.mjs ${Object.keys(MACHINE_BUILDS).join(' ')}`);
});
