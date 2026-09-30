// Runs the speed benchmarks and writes site/src/data/measurements.json, the
// dated record the site's Status page reads. It takes about ten minutes, most of
// it the ahead-of-time compilation, and wants an otherwise idle machine.
//
//   node bench/collect-measurements.mjs [runs]
//
// Needs the .NET 10 SDK with the wasm-tools workload, Node, and Chrome (set
// CHROME_PATH if it is not /usr/bin/google-chrome). See
// bench/Dbhq.Cpu6502.WasmBench/README.md for what each tool measures.
//
// The figures are one machine's, on one day. The file says which machine and
// which day, and the site prints both beside the figures.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');

/** Every "measured" line the tools print: the measured run, not the warm-up. */
export function parseRuns(text) {
  const runs = [];
  for (const m of text.matchAll(/\bmeasured cycles=(\d+) ms=([\d.]+) cycles_per_second=(\d+) mhz=([\d.]+)/g)) {
    runs.push({ cycles: Number(m[1]), ms: Number(m[2]), mhz: Number(m[4]) });
  }
  return runs;
}

/** The browser line the runner prints once, for example "browser 153.0.8010.47". */
export function parseBrowser(text) {
  const m = /^browser (\S+)/m.exec(text);
  return m ? `Chrome ${m[1]}` : null;
}

export function describeMachine({ virtualisation, model, cores }) {
  const kind = virtualisation && virtualisation !== 'none' ? `a ${virtualisation.toUpperCase()} virtual machine` : 'a physical machine';
  return `${kind}, ${model}, ${cores} cores`;
}

export const WORKLOAD =
  'A synthetic 6502 program of our own, run on a plain 64 KB array with no logging: 100 million cycles per run after a 5 million cycle warm-up.';

const run = (cmd, args, options = {}) => execFileSync(cmd, args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'], maxBuffer: 1 << 26, ...options });
const tryRun = (cmd, args) => { try { return run(cmd, args).trim(); } catch { return null; } };

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const runs = Number(process.argv[2] ?? 5);
  const wasm = path.join(repo, 'bench', 'Dbhq.Cpu6502.WasmBench');
  const native = path.join(repo, 'bench', 'Dbhq.Cpu6502.SpeedNative');

  console.log('native');
  run('dotnet', ['build', '-c', 'Release', native], { stdio: 'inherit' });
  const dll = path.join(native, 'bin', 'Release', 'net10.0', 'Dbhq.Cpu6502.SpeedNative.dll');
  const nativeRuns = Array.from({ length: runs }, () => parseRuns(run('dotnet', [dll])).at(-1));

  console.log('browser: interpreter');
  run('dotnet', ['publish', wasm, '-c', 'Release', '-o', path.join(wasm, 'publish', 'interpreter')], { stdio: 'inherit' });
  console.log('browser: ahead of time (this takes a few minutes)');
  run('dotnet', ['publish', wasm, '-c', 'Release', '-p:RunAOTCompilation=true', '-o', path.join(wasm, 'publish', 'aot')], { stdio: 'inherit' });
  run('npm', ['ci'], { cwd: wasm, stdio: 'inherit' });
  const browserRun = (folder) => run('node', ['run-in-browser.mjs', path.join('publish', folder), String(runs)], { cwd: wasm });
  const interpreterText = browserRun('interpreter');
  const aotText = browserRun('aot');

  const modes = {
    native: { label: 'Native', runs: nativeRuns },
    interpreter: { label: 'Browser, interpreter', runs: parseRuns(interpreterText) },
    aot: { label: 'Browser, ahead of time', runs: parseRuns(aotText) },
  };
  for (const [name, mode] of Object.entries(modes)) {
    if (mode.runs.length !== runs || mode.runs.some((r) => !r)) throw new Error(`${name}: expected ${runs} measured runs, got ${mode.runs.filter(Boolean).length}`);
  }

  const cpu = fs.existsSync('/proc/cpuinfo') ? /model name\s*:\s*(.+)/.exec(fs.readFileSync('/proc/cpuinfo', 'utf8'))?.[1] : null;
  const record = {
    collected: new Date().toISOString().slice(0, 10),
    machine: {
      description: describeMachine({ virtualisation: tryRun('systemd-detect-virt', []), model: cpu ?? os.cpus()[0].model, cores: os.cpus().length }),
      browser: parseBrowser(interpreterText) ?? parseBrowser(aotText),
      dotnet: run('dotnet', ['--version']).trim(),
    },
    workload: WORKLOAD,
    modes,
  };
  const out = path.join(repo, 'site', 'src', 'data', 'measurements.json');
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, JSON.stringify(record, null, 2) + '\n');
  console.log(`written ${out}`);
}
