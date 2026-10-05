// Builds what the machine pages load and puts it in public/machines/<id>/,
// where the Astro build copies it into dist. Nothing it writes is committed:
// public/machines/ is git-ignored.
//
//   node scripts/build-machines.mjs <id>... [--interpreter]
//
// One machine or several, by registry id: kim-1, bbc-micro, electron. The
// machines it knows, their projects and their ROMs are MACHINE_BUILDS in
// src/lib/machines.mjs. For each one named that is two things:
//
//   1. The machine as .NET WebAssembly (src/Dbhq.Machines.Kim1.Wasm for the
//      KIM-1, src/Dbhq.Machines.BbcMicro.Wasm for the BBC Micro,
//      src/Dbhq.Machines.Electron.Wasm for the Electron), published
//      and its _framework folder copied across. It is compiled ahead of time
//      (AOT), which needs the wasm-tools workload; --interpreter publishes it
//      without AOT instead, for comparison. The journal entry "The KIM-1 in the
//      browser" says why AOT.
//   2. Its ROMs, read from roms/ in this repository and checked against the
//      SHA-256 pinned in tests/Dbhq.Cpu6502.TestSupport/Pins.cs (AGENTS.md
//      rules 3 and 4): the KIM-1's monitor ROM, the two 1 KB halves; the BBC
//      Micro's operating system, BASIC and DFS, 16 KB each; the Electron's
//      operating system from roms/electron/ and BASIC, the BBC Micro's file.
//   3. For the BBC Micro, its preset discs: each image listed in
//      machines/bbc-micro/discs/manifest.json, read from its folder, checked
//      against the manifest's size and SHA-256, and written to discs/<slug>.ssd.
//      Nothing else of the discs' folders is published: their licences and
//      source are for the repository, and the page links to them there.
//
// Every ROM and disc of every machine named is read and checked before
// anything is published, so a file that fails its hash stops the build before
// the slow part.
//
// Needs the .NET 10 SDK and Node 22.22 or later.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { machineBuilds, readRom } from '../src/lib/machines.mjs';
import { DISCS_FOLDER, discProblems, loadDiscs, readDisc } from '../src/lib/bbc-discs.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const site = path.resolve(here, '..');
const repo = path.resolve(site, '..');
const options = process.argv.slice(2).filter((a) => a.startsWith('--'));
for (const o of options) if (o !== '--interpreter') throw new Error(`unknown option ${o}: the only one is --interpreter`);
const aot = !options.includes('--interpreter');
const builds = machineBuilds(process.argv.slice(2).filter((a) => !a.startsWith('--')));

// "a and b", "a, b and c".
const list = (items) => (items.length < 2 ? items.join('') : `${items.slice(0, -1).join(', ')} and ${items.at(-1)}`);

// The site's libraries find the repository from the working directory, as
// every npm script and CI step runs in site/. Run from anywhere else, they
// would look for Pins.cs in the wrong place.
if (process.cwd() !== site) throw new Error(`run this from ${path.relative(process.cwd(), site) || '.'}: cd site && node scripts/build-machines.mjs <id>`);

// Read first: a ROM or a disc that fails its hash should stop the build before
// the slow publish, not after it.
for (const build of builds) {
  build.roms = build.roms().map((r) => {
    const bytes = readRom(r, repo);
    console.log(`${build.id} ${r.file}: read from ${r.path}, hash checked`);
    return { ...r, bytes };
  });
  const hasDiscs = build.discs;
  build.discs = [];
  if (hasDiscs) {
    const discs = loadDiscs(repo);
    const problems = discProblems(discs, repo);
    if (problems.length > 0) throw new Error(`${build.id}'s discs cannot be published:\n- ${problems.join('\n- ')}`);
    build.discs = discs.map((d) => ({ slug: d.slug, bytes: readDisc(d, repo) }));
    console.log(`${build.id} discs: ${build.discs.length} read from machines/${build.id}/discs/, hash checked`);
  }
}

for (const { id, project: name, roms, discs } of builds) {
  const out = path.join(site, 'public', 'machines', id);

  // A clean build every time. The WebAssembly publish keeps its AOT output in
  // obj/, and an AOT build after an interpreter build (or after a change to an
  // assembly it compiles) reused stale output once here: the published runtime
  // then refused to start ("aot-runtime.c:2146"). CI starts clean anyway.
  const project = path.join(repo, 'src', name);
  for (const dir of ['obj', 'bin']) fs.rmSync(path.join(project, dir, 'Release'), { recursive: true, force: true });

  const publish = fs.mkdtempSync(path.join(os.tmpdir(), `${id}-wasm-`));
  try {
    const args = ['publish', project, '--configuration', 'Release', '--output', publish];
    if (aot) args.push('-p:RunAOTCompilation=true');
    console.log(`dotnet ${args.join(' ')}`);
    execFileSync('dotnet', args, { cwd: repo, stdio: 'inherit' });

    fs.rmSync(out, { recursive: true, force: true });
    const framework = path.join(publish, 'wwwroot', '_framework');
    fs.mkdirSync(path.join(out, '_framework'), { recursive: true });
    // The precompressed .br and .gz copies are left behind: Cloudflare Pages
    // compresses on the way out and would serve them only as downloads.
    const files = fs.readdirSync(framework).filter((f) => !/\.(br|gz)$/.test(f));
    for (const f of files) fs.copyFileSync(path.join(framework, f), path.join(out, '_framework', f));
    for (const r of roms) fs.writeFileSync(path.join(out, r.file), r.bytes);
    if (discs.length > 0) fs.mkdirSync(path.join(out, DISCS_FOLDER));
    for (const d of discs) fs.writeFileSync(path.join(out, DISCS_FOLDER, `${d.slug}.ssd`), d.bytes);
    const bytes = files.reduce((sum, f) => sum + fs.statSync(path.join(out, '_framework', f)).size, 0);
    console.log(`${path.relative(repo, out)}: ${files.length} framework files, ${bytes} bytes, ${aot ? 'compiled ahead of time' : 'interpreter'}; ${list(roms.map((r) => r.file))}${discs.length > 0 ? `; ${discs.length} discs in ${DISCS_FOLDER}/` : ''}`);
  } finally {
    fs.rmSync(publish, { recursive: true, force: true });
  }
}
