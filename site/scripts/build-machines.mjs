// Builds what the machine pages load and puts it in public/machines/<id>/,
// where the Astro build copies it into dist. Nothing it writes is committed:
// public/machines/ is git-ignored.
//
//   node scripts/build-machines.mjs [--interpreter]
//
// For the KIM-1 that is two things:
//
//   1. The machine as .NET WebAssembly: src/Dbhq.Machines.Kim1.Wasm,
//      published and its _framework folder copied across. It is compiled
//      ahead of time (AOT), which needs the wasm-tools workload; --interpreter
//      publishes it without AOT instead, for comparison. The journal entry
//      "The KIM-1 in the browser" says why AOT.
//   2. MOS Technology's monitor ROM, the two 1 KB halves, read from roms/ in
//      this repository and checked against the SHA-256 pinned in
//      tests/Dbhq.Cpu6502.TestSupport/Pins.cs (AGENTS.md rules 3 and 4).
//
// Needs the .NET 10 SDK and Node 22.22 or later.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { kim1Roms } from '../src/lib/pins.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const site = path.resolve(here, '..');
const repo = path.resolve(site, '..');
const out = path.join(site, 'public', 'machines', 'kim-1');
const aot = !process.argv.includes('--interpreter');

const sha256 = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');

async function rom({ file, path: relative, sha256: want }) {
  const bytes = fs.readFileSync(path.join(repo, relative));
  if (sha256(bytes) !== want) throw new Error(`${relative} does not match its pinned hash ${want}`);
  console.log(`${file}: read from ${relative}, hash checked`);
  return bytes;
}

// The site's libraries find the repository from the working directory, as
// every npm script and CI step runs in site/. Run from anywhere else, they
// would look for Pins.cs in the wrong place.
if (process.cwd() !== site) throw new Error(`run this from ${path.relative(process.cwd(), site) || '.'}: cd site && node scripts/build-machines.mjs`);

// Read first: a ROM that fails its hash should stop the build before the
// slow publish, not after it.
const roms = await Promise.all(kim1Roms().map(async (r) => ({ ...r, bytes: await rom(r) })));

// A clean build every time. The WebAssembly publish keeps its AOT output in
// obj/, and an AOT build after an interpreter build (or after a change to an
// assembly it compiles) reused stale output once here: the published runtime
// then refused to start ("aot-runtime.c:2146"). CI starts clean anyway.
const project = path.join(repo, 'src', 'Dbhq.Machines.Kim1.Wasm');
for (const dir of ['obj', 'bin']) fs.rmSync(path.join(project, dir, 'Release'), { recursive: true, force: true });

const publish = fs.mkdtempSync(path.join(os.tmpdir(), 'kim1-wasm-'));
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
  const bytes = files.reduce((sum, f) => sum + fs.statSync(path.join(out, '_framework', f)).size, 0);
  console.log(`${path.relative(repo, out)}: ${files.length} framework files, ${bytes} bytes, ${aot ? 'compiled ahead of time' : 'interpreter'}; ${roms.map((r) => r.file).join(' and ')}`);
} finally {
  fs.rmSync(publish, { recursive: true, force: true });
}
