import { dotnet } from './_framework/dotnet.js'

// The machine is given its two ROMs as bytes: the server (run-in-browser.mjs) has read each
// from roms/ and checked it against its pin in Pins.cs before serving it.
//
// ?boot=N    cycles of machine time to run before the prompt is checked (default 4 million, two seconds)
// ?cycles=N  cycles in each timed run (default 2 million, one second of machine time)
// ?runs=N    timed runs in each mode (default 5)
// ?modes=L   the ULA display modes to time, in order, comma separated (default 6,0): 6 is the boot
//            default and is never held up by the display, 0 is held up on every line of its 256
const params = new URLSearchParams(location.search);
const bootCycles = Number(params.get('boot') ?? 4_000_000);
const cycles = Number(params.get('cycles') ?? 2_000_000);
const runs = Number(params.get('runs') ?? 5);
const modes = (params.get('modes') ?? '6,0').split(',').map(Number);
const log = document.getElementById('log');

const fetchBytes = async (name) => {
  const response = await fetch(`roms/${name}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}: roms/${name}`);
  return new Uint8Array(await response.arrayBuffer());
};

const { getAssemblyExports, getConfig } = await dotnet.create();
const exports = await getAssemblyExports(getConfig().mainAssemblyName);
const electron = exports.ElectronHost;
const [os, basic] = await Promise.all(['os', 'basic'].map(fetchBytes));

// Let the page paint "running" before the runs block the main thread.
log.textContent = 'running...';
await new Promise(resolve => setTimeout(resolve, 50));

const line = (label, ran, ms) =>
  `${label} cycles=${ran} ms=${ms.toFixed(3)} cycles_per_second=${Math.round(ran / (ms / 1000))} mhz=${(ran / ms / 1000).toFixed(3)}`;
const lines = [];

// The boot: power on and run the real OS to the prompt. Timed, and the first thing the runtime
// executes, so it includes the runtime warming up.
electron.Load(os, basic, 44100);
let t = performance.now();
let before = electron.Cycles();
electron.Run(bootCycles);
let ms = performance.now() - t;
lines.push(line('boot', electron.Cycles() - before, ms));

// The prompt must be on the screen, or the timed runs would measure something else: row 1
// "Acorn Electron" and the bell glyph, row 3 "BASIC", row 5 ">" (BootTests, ula.md s10b).
const rows = [];
for (let r = 0; r < 8; r++) rows.push(electron.ScreenRow(r).trimEnd());
lines.push('screen ' + JSON.stringify(rows));
const prompt = electron.Mode() === 6 && rows[1].startsWith('Acorn Electron') && rows[3] === 'BASIC' && rows[5] === '>';
lines.push('prompt ' + (prompt ? 'yes' : 'NO'));

for (const mode of modes) {
  // The ULA's mode is written from here, as a program would write $FE07; the OS does not write it
  // again while it waits at the prompt, which the check after the runs confirms.
  electron.SetMode(mode);
  const set = electron.Mode() === mode;
  for (let i = 1; i <= runs; i++) {
    t = performance.now();
    before = electron.Cycles();
    electron.Run(cycles);
    ms = performance.now() - t;
    lines.push(line(`mode ${mode} timed ${i}`, electron.Cycles() - before, ms));
  }
  lines.push(`mode ${mode} held ${set && electron.Mode() === mode ? 'yes' : 'NO'}`);
}

for (const l of lines) console.log(l);
log.textContent = lines.join('\n');
document.body.dataset.done = 'true';
