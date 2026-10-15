import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { page, visibleText, DIST } from './helpers.mjs';
import { counts, loadRegistry, validateRegistry, REPO_ROOT } from '../src/lib/registry.mjs';
import { loadTryIt, machineDownloadBytes, runningMachines, runningSentence } from '../src/lib/machines.mjs';
import { atari2600Roms } from '../src/lib/pins.mjs';
import { issueUrl, megabytes } from '../src/lib/bbc-micro.mjs';
import { BOARDS, ELSEWHERE, NOT_MODELLED, controlKeys } from '../src/lib/atari-2600.mjs';

// The Atari 2600's page, as the site that deploys builds it: the registry says
// the machine runs, so it has a page, linked from the machines table, with its
// two photographs credited, and the home page counts it. The page's parts are
// read from the built HTML; scripts/browser-check-atari-2600.mjs runs it in a
// real browser, and tests/atari-2600-panel.test.mjs checks the panel's script.

const html = page('/machines/atari-2600/')?.html ?? '';
const registry = loadRegistry();
const machine = registry.machines.find((m) => m.id === 'atari-2600');
const results = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src', 'data', 'results.json'), 'utf8'));
const program = loadTryIt('atari-2600');
const [game] = atari2600Roms();
const section = (id) => {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
};
const panel = (() => {
  const at = html.indexOf('<div class="vcs"');
  return at < 0 ? '' : html.slice(at, html.lastIndexOf('</fieldset>') + '</fieldset>'.length);
})();
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');
const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'atari-2600.js'), 'utf8');

test('the registry says the Atari 2600 runs, names its acceptance test, which passes, and has its photographs, and it validates', () => {
  assert.equal(machine.status, 'running');
  assert.equal(machine.acceptance, 'Atari2600AcceptanceTests');
  assert.deepEqual(machine.photos.map((p) => p.file), ['atari-2600.webp', 'atari-2600-four-switch.webp']);
  assert.deepEqual(validateRegistry(registry, results), []);
});

test('the site has the Atari 2600\'s page, linked from the machines table', () => {
  assert.ok(html, '/machines/atari-2600/ was not built');
  assert.match(page('/machines/').html, /<a href="\/machines\/atari-2600\/">Atari 2600<\/a>/);
});

test('the home page counts the Atari 2600, from the registry: one more than with it not running, and never typed', () => {
  const before = { ...registry, machines: registry.machines.map((m) => (m.id === 'atari-2600' ? { ...m, status: 'planned' } : m)) };
  const { running: n, inScope } = counts(registry);
  assert.equal(n, counts(before).running + 1);
  assert.equal(inScope, counts(before).inScope);
  assert.equal(n, runningMachines(registry).length);
  const home = page('/').html;
  assert.match(home, new RegExp(`>${n} of ${inScope}<`), 'the machines-implemented card is not the registry\'s count');
  assert.ok(visibleText(home).includes(runningSentence(registry)), 'the home page does not say which machines run');
  assert.ok(runningSentence(registry).includes('Atari 2600'));
  assert.ok(!runningSentence(before).includes('Atari 2600'));
  const template = fs.readFileSync(path.join(process.cwd(), 'src', 'pages', 'index.astro'), 'utf8');
  assert.doesNotMatch(template, /\b\d+ of \d+\b/, 'a count is typed into the home page');
});

test('the page states its acceptance test and the count from the results', () => {
  const passed = results.suites.Atari2600AcceptanceTests.passed;
  assert.ok(visibleText(html).includes(`Acceptance test Atari2600AcceptanceTests: ${passed.toLocaleString('en-GB')} passing.`));
});

test('both photographs are credited on the page as their source gives them: the author, the source, the licence as stated, and the change', () => {
  const [main, second] = machine.photos;
  for (const photo of [main, second]) {
    assert.equal(photo.author, 'Evan-Amos');
    assert.equal(photo.licence, 'Public domain');
  }
  const head = /<figure class="photo" data-photo="atari-2600"[\s\S]*?<\/figure>/.exec(html)?.[0] ?? '';
  assert.ok(head, 'the page has no main photograph');
  const headText = visibleText(head).replace(/\s+([.,])/g, '$1');
  assert.ok(headText.includes(`Photograph: ${main.caption}, taken 22 November 2014.`), headText);
  const grid = section('photos');
  const figures = [...grid.matchAll(/<figure class="photo photo-item" data-photo-file="([^"]+)"[\s\S]*?<\/figure>/g)];
  assert.deepEqual(figures.map((f) => f[1]), ['atari-2600.webp', 'atari-2600-four-switch.webp']);
  const secondText = visibleText(figures[1][0]).replace(/\s+([.,])/g, '$1');
  assert.ok(secondText.includes(`Photograph: ${second.caption}, taken 28 February 2011.`), secondText);
  for (const [photo, figure] of [[main, head], [second, figures[1][0]]]) {
    const text = visibleText(figure).replace(/\s+([.,])/g, '$1');
    assert.ok(text.includes(`By ${photo.author}.`), `${photo.file}: the author is not credited`);
    assert.ok(figure.includes(`<a href="${photo.sourceUrl}">commons.wikimedia.org</a>`), `${photo.file}: the source is not linked`);
    assert.ok(text.includes('Licence: Public domain.'), `${photo.file}: the licence is not given as the source states it`);
    assert.ok(text.includes('Changes: resized and converted to WebP for this page.'));
    const img = /<img\b[^>]*>/.exec(figure)?.[0] ?? '';
    assert.match(img, new RegExp(`src="/_astro/${photo.file.replace('.webp', '')}\\.[\\w-]+\\.webp"`), `${photo.file} is not served from this site`);
    assert.equal(decode(/\balt="([^"]*)"/.exec(img)?.[1] ?? ''), photo.alt);
  }
  // No model, so no photograph says what a model took from it.
  assert.doesNotMatch(grid, /data-photo-used/);
});

test('the page loads its driver, and tells it where the machine and its game are, and the driver takes the clock from the machine', () => {
  assert.match(html, /<script type="module" src="\/atari-2600\.js"><\/script>/);
  assert.match(panel, /data-base="\/machines\/atari-2600\/"/);
  assert.match(panel, new RegExp(`data-rom="${game.file.replace(/\./g, '\\.')}"`));
  assert.match(panel, new RegExp(`data-rom-title="${program.title}"`));
  assert.match(driver, /^import \{ startMachine \} from '\/machine-host\.js';$/m);
  assert.match(driver, /assembly: 'Dbhq\.Machines\.Atari2600\.Wasm',\s*hostClass: 'Atari2600Host',/);
  assert.match(driver, /clockMhz = host\.CpuHz\(\) \/ 1e6;/);
  assert.match(driver, /clockMhz: \(\) => clockMhz,\s*clockOf: "the Atari 2600's",/);
  assert.doesNotMatch(panel, /data-clock-mhz/);
});

test('the WebAssembly and the game are built into the site beside the page, the game matching its pin', () => {
  const files = path.join(DIST, 'machines', 'atari-2600');
  assert.ok(fs.existsSync(path.join(files, '_framework', 'dotnet.js')), 'dist/machines/atari-2600/_framework is missing: run `npm run machines` before the build');
  const names = fs.readdirSync(path.join(files, '_framework'));
  for (const pattern of [/^dotnet\.native\.\w+\.wasm$/, /^Dbhq\.Machines\.Atari2600\.Wasm\.\w+\.wasm$/, /^Dbhq\.Machines\.Atari2600\.\w+\.wasm$/, /^Dbhq\.Cpu6502\.\w+\.wasm$/]) {
    assert.ok(names.some((n) => pattern.test(n)), `no file matching ${pattern}`);
  }
  assert.deepEqual(names.filter((n) => /\.(br|gz|pdb)$/.test(n)), []);
  const bytes = fs.readFileSync(path.join(files, game.file));
  assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), game.sha256, `${game.file} does not match its pin`);
});

test('Start says how much it downloads, read from the built files, never typed, and the first load fetches nothing under /machines/atari-2600/', () => {
  const bytes = machineDownloadBytes('atari-2600');
  assert.ok(bytes > 1e6, 'the machine was not built: run `npm run machines`');
  let total = 0;
  const walk = (d) => { for (const e of fs.readdirSync(d, { withFileTypes: true })) { const f = path.join(d, e.name); if (e.isDirectory()) walk(f); else total += fs.statSync(f).size; } };
  walk(path.join(DIST, 'machines', 'atari-2600', '_framework'));
  total += fs.statSync(path.join(DIST, 'machines', 'atari-2600', game.file)).size;
  assert.equal(total, bytes);
  const button = /<button type="button" class="btn pill" data-vcs-start disabled>([^<]*)<\/button>/.exec(panel);
  assert.ok(button, 'no Start button, disabled until the script runs');
  assert.equal(decode(button[1]), `Start the Atari 2600, a download of up to ${megabytes(bytes)} MB`);
  assert.match(panel, new RegExp(`data-download="${bytes}"`));
  const component = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'Atari2600Panel.astro'), 'utf8');
  assert.doesNotMatch(component, /\d+(\.\d+)? MB/, 'a size is typed into the panel');
  // Nothing under /machines/atari-2600/ is named by the page itself: no preload, no script, no image.
  for (const m of html.matchAll(/\b(?:src|href|srcset)="([^"]*)"/g)) assert.ok(!m[1].startsWith('/machines/atari-2600/'), `the page loads ${m[1]} on its first load`);
  // The driver fetches the machine and the game only from run(), which only Start calls.
  assert.match(driver, /start\.addEventListener\('click', \(\) => run\(panel, say\), \{ once: true \}\)/);
  assert.equal((driver.match(/startMachine\(/g) ?? []).length, 1);
  assert.ok(driver.indexOf('startMachine(') > driver.indexOf('async function run('), 'the machine is started outside run()');
  assert.ok(driver.indexOf('bytes(`${base}${bundledFile}`)') > driver.indexOf('async function run('), 'the game is fetched outside run()');
});

test('the panel has the canvas, Start, the region control, the console switches and the cartridge picker, each disabled until the script runs', () => {
  assert.match(panel, /<div class="vcs-screen" data-vcs-screen role="application" aria-label="The Atari 2600's screen" aria-describedby="vcs-keys">\s*<canvas data-vcs-canvas width="160" height="\d+"><\/canvas>/);
  assert.match(visibleText(panel), /It models the picture and is not a CRT/);
  assert.doesNotMatch(panel, /<img\b/, 'the machine must be drawn, not a picture');
  const radios = [...panel.matchAll(/<input type="radio" name="vcs-region" value="(\w+)" data-vcs-region[^>]*>/g)].map((m) => m[1]);
  assert.deepEqual(radios, ['NTSC', 'PAL']);
  assert.deepEqual([...panel.matchAll(/data-vcs-hold="(\w+)"/g)].map((m) => m[1]), ['Reset', 'Select']);
  assert.deepEqual([...panel.matchAll(/data-vcs-toggle="(\w+)"/g)].map((m) => m[1]), ['BlackAndWhite', 'LeftDifficultyA', 'RightDifficultyA']);
  assert.match(panel, /<button type="button" class="btn small" data-vcs-choose disabled>Load a cartridge file<\/button>/);
  assert.match(panel, /<input type="file" accept="\.a26,\.bin,\.rom" data-vcs-file hidden\/?>/);
  assert.match(visibleText(panel), /is not uploaded anywhere/);
  const controls = [...panel.matchAll(/<(button|input)\b[^>]*>/g)].map((m) => m[0]).filter((t) => !/type="file"/.test(t));
  assert.ok(controls.length >= 15, `only ${controls.length} controls were found`);
  for (const c of controls) assert.match(c, /\bdisabled\b/, `${c} is not disabled`);
  const status = /<p class="vcs-status" role="status" data-vcs-status>([^<]*)<\/p>/.exec(panel);
  assert.match(status?.[1] ?? '', /needs JavaScript/);
});

test('the page says what the two regions are, that the page never changes the region by itself, and that the Atari 2600 needs a fast computer, with no speed typed', () => {
  const text = visibleText(section('run'));
  assert.match(text, /NTSC, the console of North America and Japan, and PAL, the console of Europe and Australia/);
  assert.match(text, /the page never changes the region by itself/);
  assert.match(text, /Changing it changes the colours and the clock at once, with no restart/);
  assert.match(text, /That takes a fast computer to run at full speed/);
  assert.match(text, /The line under the screen says how fast this browser runs it, measured while it runs/);
  assert.ok(section('run').includes(`<a href="${issueUrl(ELSEWHERE.speed)}">issue ${ELSEWHERE.speed}</a>`));
  // The live headroom line is the only speed: nothing on the page is a typed multiple or rate of its own.
  assert.doesNotMatch(visibleText(html), /\d+(\.\d+)? times|times as fast|\d+(\.\d+)? MHz(?! \((NTSC|PAL)\))/);
});

test('the page says how it is checked, and that a trusted result is the behaviour of the one console the suite was checked on', () => {
  const checked = section('checked');
  assert.match(checked, /<h2 id="checked">How it is checked<\/h2>/);
  const text = decode(visibleText(checked));
  assert.ok(text.includes('So a trusted result is the behaviour of the one console the suite was checked on, not of every Atari 2600.'));
  assert.match(text, /The suite's notes say its tests were checked on one console, a PAL console from Australia/);
  assert.match(text, /a change fails the build/);
  assert.ok(checked.includes('href="https://github.com/dbhq-uk/missingno-vcs-tests"'), 'the suite is not linked to this project\'s fork');
  assert.ok(checked.includes(`<a href="${issueUrl(ELSEWHERE.captures)}">issue ${ELSEWHERE.captures}</a>`), 'the captures\' issue is not linked');
  assert.ok(checked.includes('href="https://github.com/dbhq-uk/6502/blob/main/docs/known-differences.md"'), 'the known differences are not linked');
  // What the page says the suite's notes say, the list of known differences records from the suite's README.
  const known = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'known-differences.md'), 'utf8');
  assert.match(known, /\*\*A trusted result is the behaviour of one console\.\*\* The suite's README says its tests were checked on one Australian PAL console/);
});

test('the page lists the parts left out, each linked to its issue, every board not modelled with its own, and the 3D models\' issue', () => {
  const list = section('left-out');
  assert.match(list, /<h2 id="left-out">What is not modelled<\/h2>/);
  const lists = [...list.matchAll(/<ul\b[^>]*>([\s\S]*?)<\/ul>/g)].map((m) => [...m[1].matchAll(/<li>([^<]*)<a href="([^"]+)">issue (\d+)<\/a><\/li>/g)]);
  assert.equal(lists.length, 2);
  assert.deepEqual(lists[0].map((m) => [decode(m[1]).replace(/: $/, '').toLowerCase(), m[2], Number(m[3])]), NOT_MODELLED.map((m) => [m.part.toLowerCase(), issueUrl(m.issue), m.issue]));
  assert.deepEqual(lists[1].map((m) => [decode(m[1]).replace(/: $/, ''), m[2], Number(m[3])]), BOARDS.map((b) => [b.board, issueUrl(b.issue), b.issue]));
  assert.ok(list.includes(`<a href="${issueUrl(ELSEWHERE.models)}">issue ${ELSEWHERE.models}</a>`), 'the 3D models\' issue is not linked');
});

test('the page says whose game it runs, in the registry\'s words, where the copy is, and links its licence', () => {
  const rom = section('rom');
  assert.match(rom, /<h2 id="rom">Whose game this is<\/h2>/);
  const text = decode(visibleText(rom));
  assert.ok(text.includes(machine.rights), 'the rights text is not the registry\'s');
  assert.ok(rom.includes(`href="${game.url}"`), 'the copy in the repository is not linked');
  assert.ok(rom.includes(`<code>${game.sha256}</code>`), 'the SHA-256 is not shown');
  assert.ok(text.includes(`${program.title}, by ${program.author}`));
  assert.ok(rom.includes(`<a href="${program.licenceUrl}">the ${program.licence} licence</a>`), 'the licence is not linked');
  assert.ok(rom.includes('href="https://github.com/dbhq-uk/6502/tree/main/machines/atari-2600/cartridges/2048"'), 'the game\'s folder is not linked');
  assert.doesNotMatch(text, /in roms\//, 'the game is not in roms/');
});

test('the try-it steps, the controls and the game\'s credit on the page are the file the boot check reads, with the keys from the page\'s own map', () => {
  const tryIt = section('try');
  const steps = [...tryIt.matchAll(/<li>\s*<p>([^<]*)<\/p>\s*<p>([^<]*)<\/p>\s*<\/li>/g)].map((m) => [decode(m[1]), decode(m[2])]);
  assert.deepEqual(steps, program.steps.map((s) => [s.do, s.says]));
  const rows = [...tryIt.matchAll(/<tr><td>([^<]*)<\/td><td>([^<]*)<\/td><td>([^<]*)<\/td><\/tr>/g)].map((m) => m.slice(1).map(decode));
  assert.deepEqual(rows.map((r) => r[0]), program.controls.map((c) => c.button));
  assert.deepEqual(rows.map((r) => r[1].toLowerCase()), program.controls.map((c) => c.does.toLowerCase()));
  assert.deepEqual(rows.map((r) => r[2]), program.controls.map((c) => controlKeys(c.button)));
  const credit = /<p data-vcs-credit>([\s\S]*?)<\/p>/.exec(tryIt)?.[1] ?? '';
  assert.ok(decode(visibleText(credit)).includes(program.credit), 'the game\'s credit is not on the page');
  assert.ok(credit.includes(`<a href="${program.licenceUrl}">${program.licence}</a>`), 'the credit does not link the licence');
  const tests = fs.readFileSync(path.join(REPO_ROOT, 'tests', 'Dbhq.Machines.Atari2600.Tests', 'BootTests.cs'), 'utf8');
  assert.match(tests, /try-it\.json/, 'the boot tests no longer read the page\'s file');
});
