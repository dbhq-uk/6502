import { dotnet } from './_framework/dotnet.js'

// The machine is given its three ROMs as bytes: the server (run.mjs) has read each
// from roms/bbc-micro/ and checked it against its pin in Pins.cs before serving it.
//
// ?boot=N   cycles of machine time to run before the prompt is checked (default 6 million, three seconds)
// ?cycles=N cycles in each timed run (default 2 million, one second of machine time)
// ?runs=N   timed runs (default 5)
const params = new URLSearchParams(location.search);
const bootCycles = Number(params.get('boot') ?? 6_000_000);
const cycles = Number(params.get('cycles') ?? 2_000_000);
const runs = Number(params.get('runs') ?? 5);
const log = document.getElementById('log');

const fetchBytes = async (name) => {
  const response = await fetch(`roms/${name}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}: roms/${name}`);
  return new Uint8Array(await response.arrayBuffer());
};

const { getAssemblyExports, getConfig } = await dotnet.create();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);
const bbc = exports.BbcHost;
const [os, basic, dfs] = await Promise.all(['os', 'basic', 'dfs'].map(fetchBytes));

// Let the page paint "running" before the runs block the main thread.
log.textContent = 'running...';
await new Promise(resolve => setTimeout(resolve, 50));

const line = (label, ran, ms) =>
  `${label} cycles=${ran} ms=${ms.toFixed(3)} cycles_per_second=${Math.round(ran / (ms / 1000))} mhz=${(ran / ms / 1000).toFixed(3)}`;
const lines = [];

// The boot: power on and run the real OS to the prompt. Timed, and the first
// thing the runtime executes, so it includes the runtime warming up.
bbc.Load(os, basic, dfs);
let t = performance.now();
let before = bbc.Cycles();
bbc.Run(bootCycles);
let ms = performance.now() - t;
lines.push(line('boot', bbc.Cycles() - before, ms));

// The prompt must be on the screen, or the timed runs would measure something else.
// Without the 8271 (task 12) the screen is: blank, BBC Computer 32K, blank, BASIC, blank, >.
const rows = [];
for (let r = 0; r < 8; r++) rows.push(bbc.ScreenRow(r).trimEnd());
lines.push('screen ' + JSON.stringify(rows));
const prompt = rows[5] === '>' && rows[1] === 'BBC Computer 32K' && rows[3] === 'BASIC';
lines.push('prompt ' + (prompt ? 'yes' : 'NO'));

for (let i = 1; i <= runs; i++) {
  t = performance.now();
  before = bbc.Cycles();
  bbc.Run(cycles);
  ms = performance.now() - t;
  lines.push(line(`timed ${i}`, bbc.Cycles() - before, ms));
}

for (const l of lines) console.log(l);
log.textContent = lines.join('\n');
document.body.dataset.done = 'true';
