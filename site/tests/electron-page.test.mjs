import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { page, visibleText } from './helpers.mjs';
import { counts, loadRegistry, REPO_ROOT } from '../src/lib/registry.mjs';
import { loadTryIt, runningSentence } from '../src/lib/machines.mjs';
import { electronRoms } from '../src/lib/pins.mjs';
import { NOT_MODELLED, TAPE_TIMES, downloadBytes, issueUrl, machineSeconds, megabytes, symbolTable } from '../src/lib/electron.mjs';
import { ON_SCREEN_ROWS, STICKY, legend } from '../public/electron-keys.js';

// The Electron's page. The registry still lists the Electron as planned, so the
// site that deploys has no page for it, and that is checked first, with the home
// page's count, which is the registry's and does not move. The page itself is
// checked on a second build, made here into a temporary folder with
// REGISTRY_PREVIEW set to tests/fixtures/electron-running.json, the registry as
// it will be once the machine is switched on (with a stand-in photograph). Then
// the whole of the site's suite runs against that build, so every rule the site
// makes about every page holds for this one too, and for the home page and the
// machines table as they will be. The BBC Micro's page was checked this way
// before it counted.
//
// When the registry itself says running, the preview changes nothing and this
// file can read dist/ instead: drop the build below and the fixture with it.

const PREVIEW = 'tests/fixtures/electron-running.json';
const root = path.resolve(process.cwd(), '..');
const out = fs.mkdtempSync(path.join(os.tmpdir(), 'electron-preview-'));
test.after(() => fs.rmSync(out, { recursive: true, force: true }));
const env = { ...process.env, REGISTRY_PREVIEW: PREVIEW, SITE_DIST: out };
const build = spawnSync(process.execPath, [path.join('node_modules', 'astro', 'bin', 'astro.mjs'), 'build', '--outDir', out], { env, encoding: 'utf8', timeout: 300_000 });

const read = (url) => {
  const file = path.join(out, url, 'index.html');
  return fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '';
};
const html = read('/machines/electron/');
const machine = loadRegistry(undefined, PREVIEW).machines.find((m) => m.id === 'electron');
const section = (id) => {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
};
const panel = (() => {
  const at = html.indexOf('<div class="electron"');
  return at < 0 ? '' : html.slice(at, html.indexOf('</fieldset>', at) + '</fieldset>'.length);
})();
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');

test('the deployed site has no Electron page and links none: the registry still says planned', () => {
  assert.equal(loadRegistry(undefined, '').machines.find((m) => m.id === 'electron').status, 'planned');
  assert.equal(page('/machines/electron/'), undefined, 'dist has an Electron page while the registry says planned');
  for (const url of ['/', '/machines/']) assert.doesNotMatch(page(url).html, /href="\/machines\/electron\//, `${url} links the Electron`);
});

test('the home page\'s count is the registry\'s running count, computed and not typed, and this page does not move it', () => {
  const registry = loadRegistry(undefined, '');
  const { running, inScope } = counts(registry);
  const home = page('/').html;
  assert.match(home, new RegExp(`>${running} of ${inScope}<`), 'the machines-implemented card is not the registry\'s count');
  assert.ok(visibleText(home).includes(runningSentence(registry)));
  assert.doesNotMatch(runningSentence(registry), /Electron/);
  const template = fs.readFileSync(path.join(process.cwd(), 'src', 'pages', 'index.astro'), 'utf8');
  assert.doesNotMatch(template, /\b\d+ of \d+\b/, 'a count is typed into the home page');
  // In the preview the count is the preview registry's: one more, the same rule.
  const previewed = counts(loadRegistry(undefined, PREVIEW));
  assert.equal(previewed.running, running + 1);
  assert.match(read('/'), new RegExp(`>${previewed.running} of ${previewed.inScope}<`));
});

test('no workflow builds or deploys with a preview: only this test and the browser check use one', () => {
  for (const name of ['validate.yml', 'deploy-site.yml']) {
    assert.doesNotMatch(fs.readFileSync(path.join(root, '.github', 'workflows', name), 'utf8'), /REGISTRY_PREVIEW|fixtures\/electron-running/, `${name} sets a preview`);
  }
  assert.doesNotMatch(fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8'), /REGISTRY_PREVIEW/);
});

test('the preview builds, and has the Electron\'s page, linked from the machines table', () => {
  assert.equal(build.status, 0, `the preview build failed:\n${build.stdout?.slice(-2000)}\n${build.stderr?.slice(-2000)}`);
  assert.ok(html, '/machines/electron/ was not built in the preview');
  assert.match(read('/machines/'), /<a href="\/machines\/electron\/">Electron<\/a>/);
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
  const files = path.join(out, 'machines', 'electron');
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
  walk(path.join(out, 'machines', 'electron', '_framework'));
  for (const r of electronRoms()) total += fs.statSync(path.join(out, 'machines', 'electron', r.file)).size;
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

test('the KIM-1\'s and the BBC Micro\'s pages are the same, byte for byte, with the Electron switched on', () => {
  for (const url of ['/machines/kim-1/', '/machines/bbc-micro/']) assert.equal(read(url), page(url).html, `${url} changed`);
});

test('the whole site suite passes on the build with the Electron switched on', () => {
  const files = fs.readdirSync('tests').filter((f) => f.endsWith('.test.mjs') && f !== 'electron-page.test.mjs').map((f) => path.join('tests', f));
  // Without NODE_TEST_CONTEXT, which this runner sets for the files it runs: with it, the
  // inner runner reports to this one instead of running as a suite of its own.
  const { NODE_TEST_CONTEXT, ...outer } = env;
  const run = spawnSync(process.execPath, ['--test', ...files], { env: outer, encoding: 'utf8', timeout: 600_000 });
  const count = (name) => Number(new RegExp(`^(?:#|ℹ) ${name} (\\d+)$`, 'm').exec(run.stdout)?.[1]);
  const failures = [...run.stdout.matchAll(/^not ok \d+ - (.*)$/gm)].map((m) => m[1]);
  assert.deepEqual(failures, [], 'tests fail on the preview build');
  assert.equal(run.status, 0, run.stdout.slice(-3000));
  assert.ok(count('pass') > 250, `only ${count('pass')} tests ran`);
  assert.equal(count('fail'), 0);
});
