import { dotnet } from './_framework/dotnet.js'

// The machine is given the cartridge as bytes: the server (run-in-browser.mjs) has fetched it from
// the pinned fork and checked it against its pin in Pins.cs before serving it as /rom.
//
// ?region=N region: 0 NTSC (default) or 1 PAL, given to Load as "NTSC" or "PAL"
// ?boot=N   cycles of machine time to run before the timed runs (default 5 million)
// ?cycles=N cycles in each timed run (default 1.79 million, about one second of machine time on NTSC)
// ?runs=N   timed runs (default 5)
const params = new URLSearchParams(location.search);
const region = Number(params.get('region') ?? 0);
if (region !== 0 && region !== 1) throw new Error(`region ${region}: 0 is NTSC and 1 is PAL`);
const regionName = region === 1 ? 'PAL' : 'NTSC';
const bootCycles = Number(params.get('boot') ?? 5_000_000);
const cycles = Number(params.get('cycles') ?? 1_790_000);
const runs = Number(params.get('runs') ?? 5);
const log = document.getElementById('log');

const response = await fetch('rom');
if (!response.ok) throw new Error(`HTTP ${response.status}: rom`);
const rom = new Uint8Array(await response.arrayBuffer());

const { getAssemblyExports, getConfig } = await dotnet.create();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);
const nes = exports.NesHost;

// Let the page paint "running" before the runs block the main thread.
log.textContent = 'running...';
await new Promise(resolve => setTimeout(resolve, 50));

const line = (label, ran, frames, ms) => {
  const perSecond = ran / (ms / 1000);
  const times = ` times_real=${(perSecond / cpuHz).toFixed(2)}`;
  return `${label} cycles=${ran} frames=${frames} ms=${ms.toFixed(3)} cycles_per_second=${Math.round(perSecond)} mhz=${(perSecond / 1e6).toFixed(3)}${times}`;
};
const lines = [`region ${region} ${regionName}`];

// The boot: power on and run the ROM. Timed, and the first thing the runtime executes, so it
// includes the runtime warming up.
// Load takes the region by name and says which it chose; the bench always names one.
lines.push(nes.Load(rom, regionName, 48000));
// The multiple of real time uses the region's own clock, from Region.CpuHz, never a rate typed here.
const cpuHz = nes.CpuHz();
let t = performance.now();
let before = nes.Cycles();
let framesBefore = nes.Frames();
nes.Run(bootCycles);
let ms = performance.now() - t;
lines.push(line('boot', nes.Cycles() - before, nes.Frames() - framesBefore, ms));

for (let i = 1; i <= runs; i++) {
  t = performance.now();
  before = nes.Cycles();
  framesBefore = nes.Frames();
  nes.Run(cycles);
  ms = performance.now() - t;
  lines.push(line(`timed ${i}`, nes.Cycles() - before, nes.Frames() - framesBefore, ms));
}

for (const l of lines) console.log(l);
log.textContent = lines.join('\n');
document.body.dataset.done = 'true';
