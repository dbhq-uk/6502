import { dotnet } from './_framework/dotnet.js'

// The machine is given the cartridge as bytes: the server (run-in-browser.mjs) has fetched the
// region's build of harness/calibration from the fork dbhq-uk/missingno at its pinned commit and
// checked its git blob hash against the suite's manifest before serving it as /rom.
//
// ?region=R  NTSC (default) or PAL, given to Load by name
// ?settle=N  frames to run before the timed runs (default 60, rule.md)
// ?runs=N    timed runs (default 5, rule.md)
// ?cycles=N  cycles in each timed run (default: one second of machine time, the region's
//            Region.CpuHz read from the machine and rounded, never a rate typed here)
const params = new URLSearchParams(location.search);
const region = params.get('region') ?? 'NTSC';
if (region !== 'NTSC' && region !== 'PAL') throw new Error(`region ${region}: NTSC or PAL`);
const settle = Number(params.get('settle') ?? 60);
const runs = Number(params.get('runs') ?? 5);
const log = document.getElementById('log');

const response = await fetch('rom');
if (!response.ok) throw new Error(`HTTP ${response.status}: rom`);
const rom = new Uint8Array(await response.arrayBuffer());

const { getAssemblyExports, getConfig } = await dotnet.create();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);
const vcs = exports.Atari2600Host;

// Let the page paint "running" before the runs block the main thread.
log.textContent = 'running...';
await new Promise(resolve => setTimeout(resolve, 50));

const lines = [`region ${region}`];
lines.push('load ' + vcs.Load(rom, region, 48000));
const cpuHz = vcs.CpuHz();
const cycles = Number(params.get('cycles') ?? Math.round(cpuHz));
lines.push(`clock cpu_hz=${cpuHz} nominal_lines=${vcs.NominalLines()}`);

const line = (label, ran, frames, ms) =>
  `${label} cycles=${ran} frames=${frames} lines=${vcs.Lines()} ms=${ms.toFixed(3)} cycles_per_second=${Math.round(ran / (ms / 1000))}`;

// The settle: power on and run the frames. Timed, and the first thing the runtime executes, so it
// includes the runtime warming up; rule.md does not judge it.
let t = performance.now();
let before = vcs.Cycles();
let framesBefore = vcs.Frames();
vcs.RunFrames(settle);
let ms = performance.now() - t;
lines.push(line('settle', vcs.Cycles() - before, vcs.Frames() - framesBefore, ms));

for (let i = 1; i <= runs; i++) {
  t = performance.now();
  before = vcs.Cycles();
  framesBefore = vcs.Frames();
  vcs.Run(cycles);
  ms = performance.now() - t;
  lines.push(line(`timed ${i}`, vcs.Cycles() - before, vcs.Frames() - framesBefore, ms));
}

for (const l of lines) console.log(l);
log.textContent = lines.join('\n');
document.body.dataset.done = 'true';
