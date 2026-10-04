import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { page, visibleText, DIST } from './helpers.mjs';
import { counts, loadRegistry, REPO_ROOT } from '../src/lib/registry.mjs';
import { loadTryIt, runningMachines, runningSentence } from '../src/lib/machines.mjs';
import { bbcRoms } from '../src/lib/pins.mjs';
import { NOT_MODELLED, downloadBytes, issueUrl, megabytes, symbolTable } from '../src/lib/bbc-micro.mjs';
import { NOT_ON_A_PC, ON_SCREEN_ROWS, legend } from '../public/bbc-keys.js';

// The BBC Micro's page, as the site that deploys builds it: the registry says
// the machine runs, so it has a page, linked from the machines table, with its
// photograph credited, and the home page counts it.

const html = page('/machines/bbc-micro/')?.html ?? '';
const machine = loadRegistry().machines.find((m) => m.id === 'bbc-micro');
const section = (id) => {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
};
const panel = (() => {
  const at = html.indexOf('<div class="bbc"');
  return at < 0 ? '' : html.slice(at, html.indexOf('</fieldset>', at));
})();
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');

test('the registry says the BBC Micro runs, names its acceptance test, and has its photograph', () => {
  assert.equal(machine.status, 'running');
  assert.equal(machine.acceptance, 'BbcAcceptanceTests');
  assert.equal(machine.photos?.[0]?.file, 'bbc-micro.webp');
});

test('the site has the BBC Micro\'s page, linked from the machines table', () => {
  assert.ok(html, '/machines/bbc-micro/ was not built');
  assert.match(page('/machines/').html, /<a href="\/machines\/bbc-micro\/">BBC Micro<\/a>/);
});

test('the home page counts the BBC Micro with the KIM-1, from the registry, and names both', () => {
  const registry = loadRegistry();
  const running = runningMachines(registry).map((m) => m.id);
  assert.ok(running.includes('kim-1') && running.includes('bbc-micro'), `running: ${running.join(', ')}`);
  const home = page('/').html;
  const text = visibleText(home);
  const { running: n, inScope } = counts(registry);
  assert.match(home, new RegExp(`>${n} of ${inScope}<`), 'the machines-implemented card is not the registry\'s count');
  assert.ok(text.includes(runningSentence(registry)), 'the home page does not say which machines run');
  // Both names in the sentence, whatever else runs by then: not the sentence as it reads today.
  for (const name of ['KIM-1', 'BBC Micro']) {
    assert.ok(runningSentence(registry).includes(name), `the running sentence does not name the ${name}`);
  }
  assert.match(home, /<h3><a href="\/machines\/bbc-micro\/">BBC Micro<\/a><\/h3>/);
});

test('the photograph is credited on the page as its source gives it: the author, the source, and the licence linked to its deed', () => {
  const photo = machine.photos[0];
  assert.equal(photo.licence, 'CC BY 2.0');
  const credit = /<figure class="photo" data-photo="bbc-micro"[\s\S]*?<\/figure>/.exec(html)?.[0] ?? '';
  assert.ok(credit, 'the page has no main photograph');
  const text = visibleText(credit);
  assert.match(text, /Photograph: an original Acorn BBC Micro Model B, taken 18 February 2018\./);
  assert.ok(text.includes(`By ${photo.author}.`), 'the author is not credited');
  assert.ok(credit.includes(`<a href="${photo.sourceUrl}">commons.wikimedia.org</a>`), 'the source is not linked');
  assert.ok(credit.includes(`<a href="${photo.licenceUrl}">CC BY 2.0</a>`), 'the licence is not linked to its deed');
});

test('the page loads its driver, and tells it where the machine and its three ROMs are, and the clock from the registry', () => {
  assert.match(html, /<script type="module" src="\/bbc-micro\.js"><\/script>/);
  assert.match(panel, /data-base="\/machines\/bbc-micro\/"/);
  assert.match(panel, new RegExp(`data-roms="${bbcRoms().map((r) => r.file).join(' ').replace(/\./g, '\\.')}"`));
  const mhz = Number(/([\d.]+) MHz/.exec(machine.cpu)[1]);
  assert.match(panel, new RegExp(`data-clock-mhz="${mhz}"`));
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'bbc-micro.js'), 'utf8');
  assert.match(driver, /^import \{ startMachine \} from '\/machine-host\.js';$/m);
  assert.match(driver, /assembly: 'Dbhq\.Machines\.BbcMicro\.Wasm',\s*hostClass: 'BbcHost',/);
  assert.match(driver, /clockMhz,\s*clockOf: "the BBC Micro's",/);
});

test('the WebAssembly and the ROMs are built into the site beside the page, each ROM matching its pin', () => {
  const files = path.join(DIST, 'machines', 'bbc-micro');
  assert.ok(fs.existsSync(path.join(files, '_framework', 'dotnet.js')), 'dist/machines/bbc-micro/_framework is missing: run `npm run machines` before the build');
  const names = fs.readdirSync(path.join(files, '_framework'));
  for (const pattern of [/^dotnet\.native\.\w+\.wasm$/, /^Dbhq\.Machines\.BbcMicro\.Wasm\.\w+\.wasm$/, /^Dbhq\.Machines\.BbcMicro\.\w+\.wasm$/, /^Dbhq\.Cpu6502\.\w+\.wasm$/]) {
    assert.ok(names.some((n) => pattern.test(n)), `no file matching ${pattern}`);
  }
  assert.deepEqual(names.filter((n) => /\.(br|gz|pdb)$/.test(n)), []);
  for (const rom of bbcRoms()) {
    const bytes = fs.readFileSync(path.join(files, rom.file));
    assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), rom.sha256, `${rom.file} does not match its pin`);
  }
});

test('Start says how much it downloads, read from the built files, never typed, and nothing is fetched before it is pressed', () => {
  const bytes = downloadBytes();
  assert.ok(bytes > 1e6, 'the machine was not built: run `npm run machines`');
  // The figure on the button is the sum of the files in the build, as built.
  let total = 0;
  const walk = (d) => { for (const e of fs.readdirSync(d, { withFileTypes: true })) { const f = path.join(d, e.name); if (e.isDirectory()) walk(f); else total += fs.statSync(f).size; } };
  walk(path.join(DIST, 'machines', 'bbc-micro', '_framework'));
  for (const r of bbcRoms()) total += fs.statSync(path.join(DIST, 'machines', 'bbc-micro', r.file)).size;
  assert.equal(total, bytes);
  const button = /<button type="button" class="btn pill" data-bbc-start disabled>([^<]*)<\/button>/.exec(panel);
  assert.ok(button, 'no Start button, disabled until the script runs');
  assert.equal(decode(button[1]), `Start the BBC Micro, a download of up to ${megabytes(bytes)} MB`);
  assert.match(panel, new RegExp(`data-download="${bytes}"`));
  const component = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'BbcPanel.astro'), 'utf8');
  assert.doesNotMatch(component, /\d+(\.\d+)? MB/, 'a size is typed into the panel');
  // The driver starts the machine only from the button.
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'bbc-micro.js'), 'utf8');
  assert.match(driver, /start\.addEventListener\('click', \(\) => run\(panel, say\), \{ once: true \}\)/);
  assert.equal((driver.match(/startMachine\(/g) ?? []).length, 1);
  assert.ok(driver.indexOf('startMachine(') > driver.indexOf('async function run('), 'the machine is started outside run(), which only Start calls');
});

test('the screen is a canvas in the machine\'s own size, shown four by three, with a name, and a line saying it models the picture and not a television', () => {
  assert.match(panel, /<div class="bbc-screen" data-bbc-screen role="application" aria-label="The BBC Micro's screen" aria-describedby="bbc-typing">\s*<canvas data-bbc-canvas width="640" height="512"><\/canvas>/);
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8');
  assert.match(css, /\.bbc-screen canvas \{[^}]*aspect-ratio: 4 \/ 3;/);
  const text = visibleText(panel);
  assert.match(text, /It models the picture, not a television/);
  assert.match(text, /Tab moves on, so the page never keeps the keyboard/);
  assert.doesNotMatch(panel, /<img\b/, 'the machine must be drawn, not a picture');
});

test('without JavaScript every control is disabled, and the status line says the machine needs it', () => {
  const controls = [...panel.matchAll(/<(button|input)\b[^>]*>/g)].map((m) => m[0]).filter((t) => !/type="file"/.test(t));
  assert.ok(controls.length >= 7, `only ${controls.length} controls were found`);
  for (const c of controls) assert.match(c, /\bdisabled\b/, `${c} is not disabled`);
  const status = /<p class="bbc-status" role="status" data-bbc-status>([^<]*)<\/p>/.exec(panel);
  assert.match(status?.[1] ?? '', /needs JavaScript/);
});

test('the sound is off until asked for, and the page says so', () => {
  assert.match(panel, /<button type="button" class="btn small" data-bbc-sound disabled>Turn sound on<\/button>/);
  assert.match(panel, /<p class="bbc-line" data-bbc-sound-status aria-live="polite">Sound is off\.<\/p>/);
});

test('the disc drive: insert, blank, save, eject and write-protect, drive 0 only, nothing uploaded, and the catalogue DFS keeps', () => {
  for (const label of ['Insert a disc', 'Make a blank disc', 'Save disc', 'Eject']) assert.ok(panel.includes(`>${label}</button>`), `no ${label} button`);
  assert.match(panel, /<input type="file" accept="\.ssd,\.dsd" data-bbc-file hidden\/?>/);
  assert.match(panel, /<input type="checkbox" role="switch" data-bbc-protect disabled\/?> Write-protect<\/label>/);
  const text = visibleText(panel);
  assert.match(text, /is not uploaded anywhere/);
  assert.match(text, /The page has drive 0 only/);
  assert.match(text, /after changing discs, type \*CAT first/);
});

test('the page lists the six parts left out, each linked to its issue', () => {
  const list = section('left-out');
  assert.match(list, /<h2 id="left-out">What is not modelled<\/h2>/);
  const items = [...list.matchAll(/<li>([^<]*)<a href="([^"]+)">issue (\d+)<\/a><\/li>/g)];
  assert.deepEqual(items.map((m) => [decode(m[1]).replace(/: $/, '').toLowerCase(), m[2], Number(m[3])]), NOT_MODELLED.map((m) => [m.part.toLowerCase(), issueUrl(m.issue), m.issue]));
});

test('the page says whose ROMs it runs, in the registry\'s words: documented and not cleared, and removed if a holder asks', () => {
  const rom = section('rom');
  assert.match(rom, /<h2 id="rom">Whose ROMs these are<\/h2>/);
  const text = visibleText(rom);
  assert.ok(text.includes(machine.rights), 'the rights text is not the registry\'s');
  assert.match(machine.rights, /Acorn/);
  assert.match(machine.rights, /documented, not cleared/);
  assert.match(machine.rights, /if a rights holder asks, the ROMs will be removed/);
  for (const r of bbcRoms()) {
    assert.ok(rom.includes(`href="${r.url}"`), `${r.file}'s copy is not linked`);
    assert.ok(rom.includes(`<code>${r.sha256}</code>`), `${r.file}'s SHA-256 is not shown`);
    assert.ok(text.includes(r.name), `${r.name} is not named`);
  }
});

test('the try-it program on the page is the acceptance test\'s, from the same file, and the registry names that test', () => {
  const program = loadTryIt('bbc-micro');
  const tryIt = section('try');
  const blocks = [...tryIt.matchAll(/<pre class="bbc-program"><code>([\s\S]*?)<\/code><\/pre>/g)].map((m) => decode(m[1]));
  assert.deepEqual(blocks, [program.lines.join('\n'), program.shows.join('\n')]);
  const tests = fs.readFileSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.BbcMicro.Tests', 'BbcAcceptanceTests.cs'), 'utf8');
  assert.match(tests, /"machines", "bbc-micro", "try-it\.json"/, 'BbcAcceptanceTests no longer reads the page\'s program');
  assert.match(tests, /public sealed class BbcAcceptanceTests/);
  assert.equal(machine.acceptance, 'BbcAcceptanceTests');
});

test('the keyboard section says where every symbol is, from the OS ROM and the key map, and which keys a PC does not press', () => {
  const keys = section('keyboard');
  const rows = [...keys.matchAll(/<tr><td><kbd>([^<]*)<\/kbd><\/td><td>([^<]*)<\/td><td>([^<]*)<\/td><\/tr>/g)].map((m) => m.slice(1).map(decode));
  assert.deepEqual(rows, symbolTable().map((r) => [r.character, r.bbc, r.pc ?? 'not on a PC keyboard here']));
  const text = visibleText(keys);
  for (const why of new Set(Object.values(NOT_ON_A_PC))) assert.ok(text.includes(why), `the reason "${why}" is not given`);
  // The keys are named by their legends, as the machine prints them.
  for (const key of Object.keys(NOT_ON_A_PC)) assert.ok(decode(keys).includes(legend(key)), `${legend(key)} is not named`);
  assert.doesNotMatch(text, /ShiftLock|\bF0\b|\bTab,/, 'a key is named by its code, not its legend');
  assert.match(text, /which are on the on-screen keys only/);
  assert.match(text, /Tap SHIFT or CTRL and it stays down for the next key you tap/);
  assert.doesNotMatch(text, /is not here yet/);
});

test('the on-screen keys: every key once, in the machine\'s rows, real buttons disabled until it runs, SHIFT and CTRL as toggles, named in words where the legend is a symbol', () => {
  const group = /<div class="bbc-keys" role="group" aria-label="The BBC Micro's keys">([\s\S]*?)<\/div>\s*<fieldset/.exec(panel)?.[1] ?? '';
  const buttons = [...group.matchAll(/<button type="button" class="bbc-key[^"]*" data-bbc-key="([^"]+)"([^>]*)>([^<]*)<\/button>/g)];
  assert.deepEqual(buttons.map((b) => b[1]), ON_SCREEN_ROWS.flat());
  for (const [, key, attrs, label] of buttons) {
    assert.match(attrs, /\bdisabled\b/, `${key} is enabled before the machine runs`);
    assert.equal(decode(label), legend(key));
    assert.equal(/aria-pressed="false"/.test(attrs), key === 'Shift' || key === 'Ctrl', `${key}'s aria-pressed`);
    if (!/^[\w ]+$/.test(legend(key))) assert.match(attrs, /aria-label="[a-z ]+"/, `${key} has a symbol and no name in words`);
  }
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8');
  assert.match(css, /\.bbc-key \{[^}]*min-width: 44px; min-height: 44px;/, 'a key is under 44 pixels');
});
