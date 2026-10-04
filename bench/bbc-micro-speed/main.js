import { dotnet } from './_framework/dotnet.js'

// The machine is given its three ROMs as bytes: the server (run.mjs) has read each
// from roms/bbc-micro/ and checked it against its pin in Pins.cs before serving it.
//
// ?boot=N   cycles of machine time to run before the prompt is checked (default 6 million, three seconds)
// ?cycles=N cycles in each timed run (default 2 million, one second of machine time)
// ?runs=N   timed runs (default 5)
// ?mode=N   the screen mode the start-up links select (default 7, the teletext screen)
// ?screen=S boot (default), or after the prompt check fill mode 7 screen memory with a page
//           that makes every cell drawn: dense (random bytes) or text (random printable characters)
// ?sound=S  none (default, the sound buffer never read), or after the prompt check read it every
//           field as a page does: silent (nothing playing) or tone (all four channels sounding)
const params = new URLSearchParams(location.search);
const bootCycles = Number(params.get('boot') ?? 6_000_000);
const cycles = Number(params.get('cycles') ?? 2_000_000);
const runs = Number(params.get('runs') ?? 5);
const mode = Number(params.get('mode') ?? 7);
const screen = params.get('screen') ?? 'boot';
const sound = params.get('sound') ?? 'none';
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
if (mode === 7) {
  // The mode and the sample rate, the machine's defaults; an older build that takes three arguments ignores the rest.
  bbc.Load(os, basic, dfs, 7, 48000);
} else {
  bbc.LoadInMode(os, basic, dfs, mode);
}
let t = performance.now();
let before = bbc.Cycles();
bbc.Run(bootCycles);
let ms = performance.now() - t;
lines.push(line('boot', bbc.Cycles() - before, ms));

// The prompt must be on the screen, or the timed runs would measure something else.
// With the 8271 (task 12) the screen is: blank, BBC Computer 32K, blank, Acorn DFS, blank,
// BASIC, blank, >. A build from before task 12 has no DFS line, so BASIC is on row 3 and the
// prompt on row 5; both are accepted, so old and new builds can be timed with this page.
// In mode 7 that is read as text from screen memory. In another mode the screen is pixels, so
// the check is the OS's own record: the mode at &0355, and the text cursor at &0318 and &0319
// one column right of the > on the prompt's row.
let prompt;
if (mode === 7) {
  const rows = [];
  for (let r = 0; r < 8; r++) rows.push(bbc.ScreenRow(r).trimEnd());
  lines.push('screen ' + JSON.stringify(rows));
  const dfs = rows[3] === 'Acorn DFS';
  const base = dfs ? 2 : 0;
  prompt = rows[1] === 'BBC Computer 32K' && rows[3 + base] === 'BASIC' && rows[5 + base] === '>';
} else {
  const os = { mode: bbc.Peek(0x355), x: bbc.Peek(0x318), y: bbc.Peek(0x319) };
  lines.push('os ' + JSON.stringify(os));
  prompt = os.mode === mode && os.x === 1 && (os.y === 5 || os.y === 7);
}
lines.push('prompt ' + (prompt ? 'yes' : 'NO'));
if (screen !== 'boot') {
  bbc.FillScreen(screen);
  lines.push('screen ' + screen);
}
if (sound !== 'none') {
  bbc.SoundOn(sound);
  lines.push('sound ' + sound);
}

for (let i = 1; i <= runs; i++) {
  t = performance.now();
  before = bbc.Cycles();
  if (sound === 'none') {
    bbc.Run(cycles);
  } else {
    bbc.RunWithSound(cycles);
  }
  ms = performance.now() - t;
  lines.push(line(`timed ${i}`, bbc.Cycles() - before, ms));
}

for (const l of lines) console.log(l);
log.textContent = lines.join('\n');
document.body.dataset.done = 'true';
