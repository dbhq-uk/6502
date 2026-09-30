import { dotnet } from './_framework/dotnet.js'

// ?cycles=N sets the measured run (default 100 million); ?warmup=N sets the
// warm-up run that is timed and printed but not counted (default 5 million).
const params = new URLSearchParams(location.search);
const cycles = Number(params.get('cycles') ?? 100_000_000);
const warmup = Number(params.get('warmup') ?? 5_000_000);
const log = document.getElementById('log');

const { getAssemblyExports, getConfig } = await dotnet.create();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);

// Let the page paint "running" before the run blocks the main thread.
log.textContent = 'running...';
await new Promise(resolve => setTimeout(resolve, 50));

const lines = [];
lines.push('warmup ' + exports.Bench.Run(warmup));
lines.push('measured ' + exports.Bench.Run(cycles));
for (const line of lines) console.log(line);
log.textContent = lines.join('\n');
document.body.dataset.done = 'true';
