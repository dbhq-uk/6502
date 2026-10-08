// Groups the self time of profiles written by run-in-browser.mjs (PROFILE_OUT=<path>) by what the
// function is, and prints each group's share of all the samples, per file and the median over
// the files. The names are the AOT build's, kept by publishing with -p:WasmNativeStrip=false.
//
//   node lazy-chips/profile-groups.mjs [--top <n>] <profile.json>...
//
// Self time only: a method the compiler inlined into its caller is in the caller's row.
import fs from 'node:fs';

const args = process.argv.slice(2);
let top = 0;
if (args[0] === '--top') { top = Number(args[1]); args.splice(0, 2); }

const strip = (name) => name.replace(/^Dbhq_(Machines_Nes|Cpu6502)_Dbhq_(Machines_Nes|Cpu6502)_/, '$1.').replace(/_(bool|int|long|byte|uint16|double).*$/, '');
const groups = [
  ['PPU RunDot (the baseline build calls it Tick), own code and what the compiler inlined', /^Machines_Nes\.Ppu_(RunDot|Tick)$/],
  ['PPU RenderDot (pixel output)', /^Machines_Nes\.Ppu_RenderDot/],
  ['PPU sprite evaluation (EvaluationStep, Evaluate)', /^Machines_Nes\.Ppu_(EvaluationStep|Evaluate)/],
  ['PPU sprite fetch and laying (FetchSprite, LaySprite)', /^Machines_Nes\.Ppu_(FetchSprite|LaySprite)/],
  ['PPU background fetch (FetchBackground)', /^Machines_Nes\.Ppu_FetchBackground/],
  ['PPU CatchUp (the loop that calls RunDot)', /^Machines_Nes\.Ppu_CatchUp/],
  ['PPU registers and the rest (WriteRegister, DecayedLatch, increments, ...)', /^Machines_Nes\.Ppu_/],
  ['NesBus.Cycle', /^Machines_Nes\.NesBus_Cycle/],
  ['NesBus reads and writes (ReadAccess, WriteAccess, Read, Write, DMA)', /^Machines_Nes\.NesBus_/],
  ['Nes.Run and Nes.Step (the machine loop)', /^Machines_Nes\.Nes_(Run|Step)/],
  ['APU Tick', /^Machines_Nes\.Apu_Tick/],
  ['APU rest (Remix, Write, channels)', /^Machines_Nes\.(Apu_|[A-Za-z]*Channel)/],
  ['SampleBuffer (Step, Impulse, Emit, Add, kernel)', /^Machines_Nes\.SampleBuffer/],
  ['Mapper (Board, Nrom and the others)', /^Machines_Nes\.(Nrom|Board|Mapper|Mmc|Uxrom|Cnrom|Axrom)/i],
  ['Other NES code', /^Machines_Nes\./],
  ['CPU core (Cpu.*)', /^Cpu6502\./],
  ['(program): the browser, outside JS and WebAssembly', /^\(program\)$/],
  ['(idle)', /^\(idle\)$/],
  ['(garbage collector)', /^\(garbage collector\)$/],
  ['JS glue and page (js-to-wasm, wasm-to-js, dotnet.js, main.js, anonymous)', null],
];

function load(file) {
  const profile = JSON.parse(fs.readFileSync(file, 'utf8'));
  const byId = new Map(profile.nodes.map(n => [n.id, n]));
  const self = new Map();
  let total = 0;
  profile.samples.forEach((id, i) => {
    const dt = profile.timeDeltas[i] ?? 0;
    const f = byId.get(id).callFrame;
    const name = f.functionName || `(anonymous ${f.url.split('/').pop()})`;
    self.set(name, (self.get(name) ?? 0) + dt);
    total += dt;
  });
  return { self, total };
}

const mono = /^(mono_|dlmalloc|dlfree|emscripten_|corlib_|System_|sin$|cos$|__|inflate_|memcpy|memset)/;
function classify(raw) {
  const name = strip(raw);
  for (let i = 0; i < groups.length - 1; i++) if (groups[i][1].test(name)) return i;
  if (mono.test(raw)) return groups.length; // the .NET runtime and libc inside the wasm
  return groups.length - 1;
}
const labels = [...groups.map(g => g[0]), 'The .NET runtime and libc in the WebAssembly (mono_*, memset, corlib_*, ...)'];

const files = args;
const rows = files.map(file => {
  const { self, total } = load(file);
  const sums = new Array(labels.length).fill(0);
  for (const [name, t] of self) sums[classify(name)] += t;
  return { file, total, sums, self };
});

const median = (xs) => { const s = [...xs].sort((a, b) => a - b); const m = s.length >> 1; return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2; };
console.log(['group', ...files.map((f, i) => `#${i + 1}`), 'median'].join('\t'));
const line = (label, pick) => {
  const shares = rows.map(r => 100 * pick(r.sums) / r.total);
  console.log([label, ...shares.map(s => s.toFixed(1)), median(shares).toFixed(1)].join('\t'));
};
labels.forEach((label, g) => line(label, sums => sums[g]));
console.log('subtotals');
const add = (sums, ...gs) => gs.reduce((a, g) => a + sums[g], 0);
const at = (re) => labels.findIndex(l => re.test(l));
const ppu = [0, 1, 2, 3, 4, 5, 6];
line('PPU, all (the first seven groups)', sums => add(sums, ...ppu));
line('PPU, per-dot work a scanline loop would replace (all but registers)', sums => add(sums, 0, 1, 2, 3, 4, 5));
line('NesBus and the machine loop (Cycle, reads and writes, Run and Step)', sums => add(sums, 7, 8, 9));
line('APU and SampleBuffer (the four groups, excluding what Cycle inlined)', sums => add(sums, 10, 11, 12));
line('CPU core', sums => sums[at(/^CPU core/)]);
line('Browser, page, runtime and idle (program, idle, GC, JS glue, .NET runtime)', sums => add(sums, at(/^\(program\)/), at(/^\(idle\)/), at(/^\(garbage/), at(/^JS glue/), labels.length - 1));
console.log(['samples (ms)', ...rows.map(r => (r.total / 1000).toFixed(0))].join('\t'));
if (top > 0) {
  const all = new Map();
  for (const r of rows) for (const [name, t] of r.self) all.set(name, (all.get(name) ?? 0) + 100 * t / r.total / rows.length);
  console.log('\nmean share by function:');
  for (const [name, s] of [...all].sort((a, b) => b[1] - a[1]).slice(0, top)) console.log(`${s.toFixed(2)}\t${strip(name)}\t[${labels[classify(name)].split(' ')[0]}]`);
}
