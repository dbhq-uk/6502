import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { page, visibleText, DIST } from './helpers.mjs';
import { counts, loadRegistry, REPO_ROOT } from '../src/lib/registry.mjs';
import { loadTryIt, runningMachines, runningSentence } from '../src/lib/machines.mjs';
import { electronRoms } from '../src/lib/pins.mjs';
import { NOT_MODELLED, TAPE_TIMES, downloadBytes, issueUrl, machineSeconds, megabytes, symbolTable } from '../src/lib/electron.mjs';
import { ON_SCREEN_ROWS, STICKY, legend } from '../public/electron-keys.js';

// The Electron's page, as the site that deploys builds it: the registry says
// the machine runs, so it has a page, linked from the machines table, with its
// photograph credited, and the home page counts it. Until 13 October 2026 the
// page was built here from a preview of the registry, as the BBC Micro's was;
// the preview was deleted when the Electron was switched on.

const html = page('/machines/electron/')?.html ?? '';
const machine = loadRegistry().machines.find((m) => m.id === 'electron');
const section = (id) => {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
};
const panel = (() => {
  const at = html.indexOf('<div class="electron"');
  return at < 0 ? '' : html.slice(at, html.indexOf('</fieldset>', at) + '</fieldset>'.length);
})();
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');

test('the registry says the Electron runs, names its acceptance test, gives its clock, and has its photograph', () => {
  assert.equal(machine.status, 'running');
  assert.equal(machine.acceptance, 'ElectronAcceptanceTests');
  assert.equal(machine.cpu, '6502, 2 MHz');
  assert.equal(machine.photos?.[0]?.file, 'electron.webp');
});

test('the site has the Electron\'s page, linked from the machines table', () => {
  assert.ok(html, '/machines/electron/ was not built');
  assert.match(page('/machines/').html, /<a href="\/machines\/electron\/">Electron<\/a>/);
});

test('the home page\'s count is the registry\'s running count, computed and not typed, and it counts the Electron', () => {
  const registry = loadRegistry();
  const running = runningMachines(registry).map((m) => m.id);
  assert.ok(running.includes('electron'), `running: ${running.join(', ')}`);
  const { running: n, inScope } = counts(registry);
  assert.equal(n, running.length);
  const home = page('/').html;
  assert.match(home, new RegExp(`>${n} of ${inScope}<`), 'the machines-implemented card is not the registry\'s count');
  assert.ok(visibleText(home).includes(runningSentence(registry)), 'the home page does not say which machines run');
  assert.ok(runningSentence(registry).includes('Electron'), 'the running sentence does not name the Electron');
  const template = fs.readFileSync(path.join(process.cwd(), 'src', 'pages', 'index.astro'), 'utf8');
  assert.doesNotMatch(template, /\b\d+ of \d+\b/, 'a count is typed into the home page');
});

test('the photograph is credited on the page as its source gives it: the author, the source, the licence linked to its deed, and the change', () => {
  const photo = machine.photos[0];
  assert.equal(photo.licence, 'CC BY 2.0');
  const credit = /<figure class="photo" data-photo="electron"[\s\S]*?<\/figure>/.exec(html)?.[0] ?? '';
  assert.ok(credit, 'the page has no main photograph');
  const text = decode(visibleText(credit));
  assert.match(text, /Photograph: an original Acorn Electron, taken 18 February 2018\./);
  assert.ok(text.includes(`By ${photo.author}.`), 'the author is not credited');
  assert.ok(credit.includes(`<a href="${photo.sourceUrl}">commons.wikimedia.org</a>`), 'the source is not linked');
  assert.ok(credit.includes(`<a href="${photo.licenceUrl}">CC BY 2.0</a>`), 'the licence is not linked to its deed');
  assert.match(text, /Changes: resized and converted to WebP for this page\./);
  assert.match(credit, /<img [^>]*alt="An original Acorn Electron/);
});

test('the page loads its driver, and tells it where the machine and its two ROMs are, and the clock from the registry', () => {
  assert.match(html, /<script type="module" src="\/electron\.js"><\/script>/);
  assert.match(panel, /data-base="\/machines\/electron\/"/);
  assert.match(panel, new RegExp(`data-roms="${electronRoms().map((r) => r.file).join(' ').replace(/\./g, '\\.')}"`));
  const mhz = Number(/([\d.]+) MHz/.exec(machine.cpu)[1]);
  assert.equal(mhz, 2);
  assert.match(panel, new RegExp(`data-clock-mhz="${mhz}"`));
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'electron.js'), 'utf8');
  assert.match(driver, /^import \{ startMachine \} from '\/machine-host\.js';$/m);
  assert.match(driver, /assembly: 'Dbhq\.Machines\.Electron\.Wasm',\s*hostClass: 'ElectronHost',/);
  assert.match(driver, /clockMhz,\s*clockOf: "the Electron's",/);
  // The host's Load takes the OS, then BASIC, then the sample rate.
  const host = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.Electron.Wasm', 'Program.cs'), 'utf8');
  assert.match(host, /public static void Load\(byte\[\] os, byte\[\] basic, int sampleRate\)/);
  assert.deepEqual(electronRoms().map((r) => r.rom), ['os', 'basic']);
});

test('the WebAssembly and the ROMs are built into the site beside the page, each ROM matching its pin', () => {
  const files = path.join(DIST, 'machines', 'electron');
  assert.ok(fs.existsSync(path.join(files, '_framework', 'dotnet.js')), 'machines/electron/_framework is missing: run `npm run machines` before the build');
  const names = fs.readdirSync(path.join(files, '_framework'));
  for (const pattern of [/^dotnet\.native\.\w+\.wasm$/, /^Dbhq\.Machines\.Electron\.Wasm\.\w+\.wasm$/, /^Dbhq\.Machines\.Electron\.\w+\.wasm$/, /^Dbhq\.Cpu6502\.\w+\.wasm$/]) {
    assert.ok(names.some((n) => pattern.test(n)), `no file matching ${pattern}`);
  }
  assert.deepEqual(names.filter((n) => /\.(br|gz|pdb)$/.test(n)), []);
  for (const rom of electronRoms()) {
    const bytes = fs.readFileSync(path.join(files, rom.file));
    assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), rom.sha256, `${rom.file} does not match its pin`);
  }
});

test('Start says how much it downloads, read from the built files, never typed, and nothing is fetched before it is pressed', () => {
  const bytes = downloadBytes();
  assert.ok(bytes > 1e6, 'the machine was not built: run `npm run machines`');
  let total = 0;
  const walk = (d) => { for (const e of fs.readdirSync(d, { withFileTypes: true })) { const f = path.join(d, e.name); if (e.isDirectory()) walk(f); else total += fs.statSync(f).size; } };
  // The framework and the ROMs, as built; the page's own index.html sits in the same folder.
  walk(path.join(DIST, 'machines', 'electron', '_framework'));
  for (const r of electronRoms()) total += fs.statSync(path.join(DIST, 'machines', 'electron', r.file)).size;
  assert.equal(total, bytes);
  const button = /<button type="button" class="btn pill" data-electron-start disabled>([^<]*)<\/button>/.exec(panel);
  assert.ok(button, 'no Start button, disabled until the script runs');
  assert.equal(decode(button[1]), `Start the Electron, a download of up to ${megabytes(bytes)} MB`);
  assert.match(panel, new RegExp(`data-download="${bytes}"`));
  const component = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'ElectronPanel.astro'), 'utf8');
  assert.doesNotMatch(component, /\d+(\.\d+)? MB/, 'a size is typed into the panel');
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'electron.js'), 'utf8');
  assert.match(driver, /start\.addEventListener\('click', \(\) => run\(panel, say\), \{ once: true \}\)/);
  assert.equal((driver.match(/startMachine\(/g) ?? []).length, 1);
  assert.ok(driver.indexOf('startMachine(') > driver.indexOf('async function run('), 'the machine is started outside run(), which only Start calls');
});

test('the screen is a canvas in the machine\'s own size, shown four by three, with a name, and a line saying it models the picture and not a television', () => {
  assert.match(panel, /<div class="electron-screen" data-electron-screen role="application" aria-label="The Electron's screen" aria-describedby="electron-typing">\s*<canvas data-electron-canvas width="640" height="256"><\/canvas>/);
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8');
  assert.match(css, /\.electron-screen canvas \{[^}]*aspect-ratio: 4 \/ 3;/);
  const text = visibleText(panel);
  assert.match(text, /It models the picture, not a television/);
  assert.match(text, /Tab moves on, so the page never keeps the keyboard/);
  assert.doesNotMatch(panel, /<img\b/, 'the machine must be drawn, not a picture');
});

test('without JavaScript every control is disabled, and the status line says the machine needs it', () => {
  const controls = [...panel.matchAll(/<(button|input)\b[^>]*>/g)].map((m) => m[0]).filter((t) => !/type="file"/.test(t));
  assert.ok(controls.length >= 7 + ON_SCREEN_ROWS.flat().length, `only ${controls.length} controls were found`);
  for (const c of controls) assert.match(c, /\bdisabled\b/, `${c} is not disabled`);
  const status = /<p class="electron-status" role="status" data-electron-status>([^<]*)<\/p>/.exec(panel);
  assert.match(status?.[1] ?? '', /needs JavaScript/);
});

test('the sound is off until asked for, and the page says so', () => {
  assert.match(panel, /<button type="button" class="btn small" data-electron-sound disabled>Turn sound on<\/button>/);
  assert.match(panel, /<p class="electron-line" data-electron-sound-status aria-live="polite">Sound is off\.<\/p>/);
});

test('the cassette recorder: insert, blank, rewind and save, nothing uploaded, and how long a tape takes from the machine\'s own measurement', () => {
  for (const label of ['Insert a tape', 'Blank tape', 'Rewind', 'Save tape']) assert.ok(panel.includes(`>${label}</button>`), `no ${label} button`);
  assert.match(panel, /<input type="file" accept="\.uef" data-electron-file hidden\/?>/);
  assert.match(panel, /<p class="electron-line" data-electron-tape aria-live="polite">No tape in the recorder\.<\/p>/);
  const text = decode(visibleText(panel));
  assert.match(text, /is not uploaded anywhere/);
  assert.match(text, /A tape plays at the Electron's own pace, as a cassette does, and nothing is sped up\./);
  assert.ok(text.includes(`In the machine's own test on ${TAPE_TIMES.measured}, saving a one-line program took ${machineSeconds(TAPE_TIMES.saveCycles)} seconds of the Electron's time`), 'the SAVE time is not the machine\'s');
  assert.ok(text.includes(`and loading it back ${machineSeconds(TAPE_TIMES.loadCycles)} seconds`), 'the LOAD time is not the machine\'s');
  assert.ok(panel.includes(`<a href="${TAPE_TIMES.journal}">the journal</a>`), 'the measurement does not link the journal');
  assert.match(text, /A tape the machine cannot read is refused with the reason, and the tape that was in stays in\./);
  const component = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'ElectronPanel.astro'), 'utf8');
  assert.doesNotMatch(component, /\d+\.\d+ seconds/, 'a time is typed into the panel');
});

test('the page lists the four parts left out, each linked to its issue', () => {
  const list = section('left-out');
  assert.match(list, /<h2 id="left-out">What is not modelled<\/h2>/);
  const items = [...list.matchAll(/<li>([^<]*)<a href="([^"]+)">issue (\d+)<\/a><\/li>/g)];
  assert.deepEqual(items.map((m) => [decode(m[1]).replace(/: $/, '').toLowerCase(), m[2], Number(m[3])]), NOT_MODELLED.map((m) => [m.part.toLowerCase(), issueUrl(m.issue), m.issue]));
  // The four issues filed with the page on 5 October 2026: the Plus 1, the Plus 3, the ports, and 300 baud tapes.
  assert.deepEqual(items.map((m) => Number(m[3])), [57, 58, 59, 60]);
});

test('the page says whose ROMs it runs, in the registry\'s words: documented and not cleared, the elkjs licence not a licence for them, and removed if a holder asks', () => {
  const rom = section('rom');
  assert.match(rom, /<h2 id="rom">Whose ROMs these are<\/h2>/);
  const text = decode(visibleText(rom));
  assert.ok(text.includes(machine.rights), 'the rights text is not the registry\'s');
  assert.match(machine.rights, /Acorn/);
  assert.match(machine.rights, /elkjs is licensed GPL-2\.0 and says nothing about the ROMs, so that licence is not a licence for them/);
  assert.match(machine.rights, /documented, not cleared/);
  assert.match(machine.rights, /if a rights holder asks, the ROMs will be removed/);
  for (const r of electronRoms()) {
    assert.ok(rom.includes(`href="${r.url}"`), `${r.file}'s copy is not linked`);
    assert.ok(rom.includes(`<code>${r.sha256}</code>`), `${r.file}'s SHA-256 is not shown`);
    assert.ok(text.includes(r.name), `${r.name} is not named`);
  }
});

test('the try-it program on the page is the acceptance test\'s, from the same file, and the registry names that test', () => {
  const program = loadTryIt('electron');
  const tryIt = section('try');
  const blocks = [...tryIt.matchAll(/<pre class="bbc-program"><code>([\s\S]*?)<\/code><\/pre>/g)].map((m) => decode(m[1]));
  assert.deepEqual(blocks, [program.lines.join('\n'), program.shows.join('\n')]);
  const tests = fs.readFileSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.Electron.Tests', 'ElectronAcceptanceTests.cs'), 'utf8');
  assert.match(tests, /"machines", "electron", "try-it\.json"/, 'ElectronAcceptanceTests no longer reads the page\'s program');
  assert.match(tests, /public sealed class ElectronAcceptanceTests/);
  assert.equal(machine.acceptance, 'ElectronAcceptanceTests');
});

test('the keyboard section says where every symbol is, from the OS ROM and the key map, and how CAPS LK and FUNC share a key', () => {
  const keys = section('keyboard');
  const rows = [...keys.matchAll(/<tr><td><kbd>([^<]*)<\/kbd><\/td><td>([^<]*)<\/td><td>([^<]*)<\/td><\/tr>/g)].map((m) => m.slice(1).map(decode));
  assert.deepEqual(rows, symbolTable().map((r) => [r.character, r.electron, r.pc ?? 'not on a PC keyboard here']));
  const text = visibleText(keys);
  assert.match(text, /CAPS LK and FUNC are one key on the Electron/);
  assert.match(text, /Tap it twice instead and it is pressed alone, which turns CAPS LOCK on or off/);
  assert.match(text, /The Break button, or the Pause key, is BREAK/);
  assert.match(text, /no key is on the on-screen keys only/);
});

test('the on-screen keys: every key once, in the machine\'s rows, real buttons disabled until it runs, the latching keys as toggles, named in words where the legend is a symbol', () => {
  const group = /<div class="electron-keys" role="group" aria-label="The Electron's keys">([\s\S]*?)<\/div>\s*<fieldset/.exec(panel)?.[1] ?? '';
  const buttons = [...group.matchAll(/<button type="button" class="electron-key[^"]*" data-electron-key="([^"]+)"([^>]*)>([^<]*)<\/button>/g)];
  assert.deepEqual(buttons.map((b) => b[1]), ON_SCREEN_ROWS.flat());
  for (const [, key, attrs, label] of buttons) {
    assert.match(attrs, /\bdisabled\b/, `${key} is enabled before the machine runs`);
    assert.equal(decode(label), legend(key));
    assert.equal(/aria-pressed="false"/.test(attrs), STICKY.includes(key), `${key}'s aria-pressed`);
    if (!/^[\w ]+$/.test(legend(key))) assert.match(attrs, /aria-label="[a-z ]+"/, `${key} has a symbol and no name in words`);
  }
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8');
  assert.match(css, /\.electron-key \{[^}]*min-width: 44px; min-height: 44px;/, 'a key is under 44 pixels');
});
