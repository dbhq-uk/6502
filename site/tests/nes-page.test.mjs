import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { page, visibleText, DIST } from './helpers.mjs';
import { counts, loadRegistry, validateRegistry, REPO_ROOT } from '../src/lib/registry.mjs';
import { loadTryIt, machineDownloadBytes, runningMachines, runningSentence } from '../src/lib/machines.mjs';
import { nesRoms } from '../src/lib/pins.mjs';
import { issueUrl, megabytes } from '../src/lib/bbc-micro.mjs';
import { NOT_MODELLED, ELSEWHERE } from '../src/lib/nes.mjs';
import { KEYS, KEY_NAMES } from '../public/nes-keys.js';

// The NES's page, as the site that deploys builds it: the registry says the
// machine runs, so it has a page, linked from the machines table, with its
// photograph credited, and the home page counts it. The page's parts are read
// from the built HTML; scripts/browser-check.mjs runs it in a real browser.

const html = page('/machines/nes/')?.html ?? '';
const registry = loadRegistry();
const machine = registry.machines.find((m) => m.id === 'nes');
const results = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src', 'data', 'results.json'), 'utf8'));
const program = loadTryIt('nes');
const [game] = nesRoms();
const section = (id) => {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
};
const panel = (() => {
  const at = html.indexOf('<div class="nes"');
  return at < 0 ? '' : html.slice(at, html.lastIndexOf('</fieldset>') + '</fieldset>'.length);
})();
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');
const PUBLIC = path.join(process.cwd(), 'public');
const driver = fs.readFileSync(path.join(PUBLIC, 'nes.js'), 'utf8');

test('the registry says the NES runs, names its acceptance test, which passes, and has its photograph, and it validates', () => {
  assert.equal(machine.status, 'running');
  assert.equal(machine.acceptance, 'NesAcceptanceTests');
  assert.equal(machine.photos?.[0]?.file, 'nes.webp');
  assert.deepEqual(validateRegistry(registry, results), []);
});

test('the site has the NES\'s page, linked from the machines table', () => {
  assert.ok(html, '/machines/nes/ was not built');
  assert.match(page('/machines/').html, /<a href="\/machines\/nes\/">NES<\/a>/);
});

test('the home page counts the NES, from the registry: one more than with the NES not running, and never typed', () => {
  const before = { ...registry, machines: registry.machines.map((m) => (m.id === 'nes' ? { ...m, status: 'planned' } : m)) };
  const { running: n, inScope } = counts(registry);
  assert.equal(n, counts(before).running + 1);
  assert.equal(inScope, counts(before).inScope);
  const home = page('/').html;
  assert.match(home, new RegExp(`>${n} of ${inScope}<`), 'the machines-implemented card is not the registry\'s count');
  assert.ok(visibleText(home).includes(runningSentence(registry)), 'the home page does not say which machines run');
  assert.ok(runningSentence(registry).includes('NES'));
  assert.ok(!runningSentence(before).includes('NES'));
  assert.ok(runningMachines(registry).some((m) => m.id === 'nes'));
  assert.match(home, /<h3><a href="\/machines\/nes\/">NES<\/a><\/h3>/);
});

test('the page states its acceptance test and the count from the results', () => {
  const text = visibleText(html);
  const passed = results.suites.NesAcceptanceTests.passed;
  assert.ok(text.includes(`Acceptance test NesAcceptanceTests: ${passed.toLocaleString('en-GB')} passing.`));
});

test('the photograph is credited on the page as its source gives it: the author, the source, and the licence as stated', () => {
  const photo = machine.photos[0];
  assert.equal(photo.licence, 'Public domain');
  const credit = /<figure class="photo" data-photo="nes"[\s\S]*?<\/figure>/.exec(html)?.[0] ?? '';
  assert.ok(credit, 'the page has no main photograph');
  const text = visibleText(credit).replace(/\s+([.,])/g, '$1');
  assert.match(text, /Photograph: an original Nintendo Entertainment System, the North American console, taken 27 July 2016\./);
  assert.ok(text.includes(`By ${photo.author}.`), 'the author is not credited');
  assert.ok(credit.includes(`<a href="${photo.sourceUrl}">commons.wikimedia.org</a>`), 'the source is not linked');
  assert.ok(text.includes('Licence: Public domain.'), 'the licence is not given as the source states it');
  const img = /<img\b[^>]*>/.exec(credit)?.[0] ?? '';
  assert.match(img, /src="\/_astro\/nes\.[\w-]+\.webp"/, 'the photograph is not served from this site');
  assert.equal(decode(/\balt="([^"]*)"/.exec(img)?.[1] ?? ''), photo.alt);
});

test('the page loads its driver, and tells it where the machine and its game are, and the driver takes the clock from the machine', () => {
  assert.match(html, /<script type="module" src="\/nes\.js"><\/script>/);
  assert.match(panel, /data-base="\/machines\/nes\/"/);
  assert.match(panel, new RegExp(`data-rom="${game.file.replace(/\./g, '\\.')}"`));
  assert.match(panel, new RegExp(`data-rom-title="${program.title}"`));
  assert.match(driver, /^import \{ startMachine \} from '\/machine-host\.js';$/m);
  assert.match(driver, /assembly: 'Dbhq\.Machines\.Nes\.Wasm',\s*hostClass: 'NesHost',/);
  // The region's own clock, read from the machine after every load: no rate is typed.
  assert.match(driver, /clockMhz = host\.CpuHz\(\) \/ 1e6;/);
  assert.match(driver, /clockMhz: \(\) => clockMhz,\s*clockOf: "the NES's",/);
  assert.doesNotMatch(panel, /data-clock-mhz/);
});

test('the WebAssembly and the game are built into the site beside the page, the game matching its pin', () => {
  const files = path.join(DIST, 'machines', 'nes');
  assert.ok(fs.existsSync(path.join(files, '_framework', 'dotnet.js')), 'dist/machines/nes/_framework is missing: run `npm run machines` before the build');
  const names = fs.readdirSync(path.join(files, '_framework'));
  for (const pattern of [/^dotnet\.native\.\w+\.wasm$/, /^Dbhq\.Machines\.Nes\.Wasm\.\w+\.wasm$/, /^Dbhq\.Machines\.Nes\.\w+\.wasm$/, /^Dbhq\.Cpu6502\.\w+\.wasm$/]) {
    assert.ok(names.some((n) => pattern.test(n)), `no file matching ${pattern}`);
  }
  assert.deepEqual(names.filter((n) => /\.(br|gz|pdb)$/.test(n)), []);
  const bytes = fs.readFileSync(path.join(files, game.file));
  assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), game.sha256, `${game.file} does not match its pin`);
});

test('Start says how much it downloads, read from the built files, never typed, and the first load fetches nothing of the machine', () => {
  const bytes = machineDownloadBytes('nes');
  assert.ok(bytes > 1e6, 'the machine was not built: run `npm run machines`');
  let total = 0;
  const walk = (d) => { for (const e of fs.readdirSync(d, { withFileTypes: true })) { const f = path.join(d, e.name); if (e.isDirectory()) walk(f); else total += fs.statSync(f).size; } };
  // The machine's files in the build, not the page, which is built into the same folder as index.html.
  walk(path.join(DIST, 'machines', 'nes', '_framework'));
  total += fs.statSync(path.join(DIST, 'machines', 'nes', game.file)).size;
  assert.equal(total, bytes);
  const button = /<button type="button" class="btn pill" data-nes-start disabled>([^<]*)<\/button>/.exec(panel);
  assert.ok(button, 'no Start button, disabled until the script runs');
  assert.equal(decode(button[1]), `Start the NES, a download of up to ${megabytes(bytes)} MB`);
  assert.match(panel, new RegExp(`data-download="${bytes}"`));
  const component = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'NesPanel.astro'), 'utf8');
  assert.doesNotMatch(component, /\d+(\.\d+)? MB/, 'a size is typed into the panel');
  // Nothing under /machines/nes/ is named by the page itself: no preload, no script, no image.
  for (const m of html.matchAll(/\b(?:src|href|srcset)="([^"]*)"/g)) assert.ok(!m[1].startsWith('/machines/nes/'), `the page loads ${m[1]} on its first load`);
  // The driver fetches the machine and the game only from run(), which only Start calls.
  assert.match(driver, /start\.addEventListener\('click', \(\) => run\(panel, say\), \{ once: true \}\)/);
  assert.equal((driver.match(/startMachine\(/g) ?? []).length, 1);
  assert.ok(driver.indexOf('startMachine(') > driver.indexOf('async function run('), 'the machine is started outside run()');
  assert.ok(driver.indexOf('bytes(`${base}${bundledFile}`)') > driver.indexOf('async function run('), 'the game is fetched outside run()');
});

test('the screen is a canvas in the machine\'s own size, with a name, and a line saying it models the picture and not a television', () => {
  assert.match(panel, /<div class="nes-screen" data-nes-screen role="application" aria-label="The NES's screen" aria-describedby="nes-keys">\s*<canvas data-nes-canvas width="256" height="240"><\/canvas>/);
  assert.match(visibleText(panel), /It models the picture, not a television/);
  assert.doesNotMatch(panel, /<img\b/, 'the machine must be drawn, not a picture');
});

test('the region control offers NTSC and PAL, and the page says what each is and how a cartridge chooses', () => {
  const radios = [...panel.matchAll(/<input type="radio" name="nes-region" value="(\w+)" data-nes-region[^>]*>/g)].map((m) => m[1]);
  assert.deepEqual(radios, ['NTSC', 'PAL']);
  const text = visibleText(section('run'));
  assert.match(text, /NTSC, the console of North America and Japan/);
  assert.match(text, /PAL, the console of Europe and Australia/);
  assert.match(text, /Each cartridge starts in the region its file names, or NTSC when the file does not say/);
});

test('the cartridge picker takes a .nes file, read in the tab and not uploaded', () => {
  assert.match(panel, /<button type="button" class="btn small" data-nes-choose disabled>Load a \.nes file<\/button>/);
  assert.match(panel, /<input type="file" accept="\.nes" data-nes-file hidden\/?>/);
  assert.match(visibleText(panel), /is not uploaded anywhere/);
});

test('without JavaScript every control is disabled, and the status line says the machine needs it', () => {
  const controls = [...panel.matchAll(/<(button|input)\b[^>]*>/g)].map((m) => m[0]).filter((t) => !/type="file"/.test(t));
  assert.ok(controls.length >= 15, `only ${controls.length} controls were found`);
  for (const c of controls) assert.match(c, /\bdisabled\b/, `${c} is not disabled`);
  const status = /<p class="nes-status" role="status" data-nes-status>([^<]*)<\/p>/.exec(panel);
  assert.match(status?.[1] ?? '', /needs JavaScript/);
});

test('the page says plainly that saving is not built, so a reload loses battery RAM', () => {
  const sentence = 'Saving is not built, so a page reload loses battery RAM';
  assert.ok(visibleText(panel).includes(sentence), 'the panel does not say it');
  assert.ok(visibleText(section('left-out')).includes(sentence), 'the list of what is left out does not say it');
});

test('the page says the NES needs a fast computer, gives no speed of its own, and links the speed issue', () => {
  const text = visibleText(section('run'));
  assert.match(text, /it needs a fast computer to run at full speed/);
  assert.match(text, /The line under the screen says how fast this browser runs it, measured while it runs/);
  assert.ok(section('run').includes(`<a href="${issueUrl(ELSEWHERE.speed)}">issue ${ELSEWHERE.speed}</a>`));
  // The live headroom line is the only speed: nothing on the page is a typed multiple or rate of its own.
  assert.doesNotMatch(visibleText(html), /\d+(\.\d+)? times|times as fast|\d+(\.\d+)? MHz(?! \((NTSC|PAL)\))/);
});

test('the page lists the parts left out, each linked to its issue, and the 3D models\' issue', () => {
  const list = section('left-out');
  assert.match(list, /<h2 id="left-out">What is not modelled<\/h2>/);
  const items = [...list.matchAll(/<li>([^<]*)<a href="([^"]+)">issue (\d+)<\/a><\/li>/g)];
  assert.deepEqual(items.map((m) => [decode(m[1]).replace(/: $/, '').toLowerCase(), m[2], Number(m[3])]), NOT_MODELLED.map((m) => [m.part.toLowerCase(), issueUrl(m.issue), m.issue]));
  assert.ok(list.includes(`<a href="${issueUrl(ELSEWHERE.models)}">issue ${ELSEWHERE.models}</a>`), 'the 3D models\' issue is not linked');
});

test('the page says whose game it runs, in the registry\'s words, and where the copy is', () => {
  const rom = section('rom');
  assert.match(rom, /<h2 id="rom">Whose game this is<\/h2>/);
  const text = decode(visibleText(rom));
  assert.ok(text.includes(machine.rights), 'the rights text is not the registry\'s');
  assert.ok(rom.includes(`href="${game.url}"`), 'the copy in the repository is not linked');
  assert.ok(rom.includes(`<code>${game.sha256}</code>`), 'the SHA-256 is not shown');
  assert.ok(text.includes(`${program.title}, by ${program.author}`));
});

test('the try-it steps and the controls on the page are the file the acceptance test\'s boot check reads, with the keys from the page\'s own map', () => {
  const tryIt = section('try');
  const steps = [...tryIt.matchAll(/<li>\s*<p>([^<]*)<\/p>\s*<p>([^<]*)<\/p>\s*<\/li>/g)].map((m) => [decode(m[1]), decode(m[2])]);
  assert.deepEqual(steps, program.steps.map((s) => [s.do, s.says]));
  const rows = [...tryIt.matchAll(/<tr><td>([^<]*)<\/td><td>([^<]*)<\/td><td>([^<]*)<\/td><\/tr>/g)].map((m) => m.slice(1).map(decode));
  assert.deepEqual(rows.map((r) => r[0]), program.controls.map((c) => c.button));
  assert.deepEqual(rows.map((r) => r[1].toLowerCase()), program.controls.map((c) => c.does.toLowerCase()));
  for (const [button, , keys] of rows) {
    const wanted = (button === 'D-pad' ? ['Up', 'Down', 'Left', 'Right'] : [button]).flatMap((b) => Object.entries(KEYS).filter(([, to]) => to === b).map(([code]) => KEY_NAMES[code]));
    for (const k of wanted) assert.ok(keys.includes(k), `${button}: ${k} is not named`);
  }
  const tests = fs.readFileSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.Nes.Tests', 'BootTests.cs'), 'utf8');
  assert.match(tests, /"machines", "nes", "try-it\.json"/, 'the boot tests no longer read the page\'s file');
  assert.match(fs.readFileSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.Nes.Tests', 'NesAcceptanceTests.cs'), 'utf8'), /public sealed class NesAcceptanceTests/);
});
