import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { page, visibleText, DIST } from './helpers.mjs';
import { loadRegistry, REPO_ROOT } from '../src/lib/registry.mjs';
import { loadTryIt } from '../src/lib/machines.mjs';
import { bbcRoms } from '../src/lib/pins.mjs';
import { NOT_MODELLED, downloadBytes, issueUrl, megabytes, symbolTable } from '../src/lib/bbc-micro.mjs';
import { NOT_ON_A_PC, ON_SCREEN_ROWS, legend } from '../public/bbc-keys.js';

// The BBC Micro's page. The registry still lists the BBC Micro as planned, so
// the site that deploys has no page for it, and that is checked first. The page
// itself is checked on a second build, made here into a temporary folder with
// REGISTRY_PREVIEW set to tests/fixtures/bbc-micro-running.json, which is the
// registry as it will be once the machine is switched on (with a stand-in
// photograph). Then the whole of the site's suite runs against that build, so
// every rule the site makes about every page holds for this one too, and for
// the home page and the machines table as they will be.
//
// When the registry itself says running, the preview changes nothing and this
// file can read dist/ instead: drop the build below and the fixture with it.

const PREVIEW = 'tests/fixtures/bbc-micro-running.json';
const root = path.resolve(process.cwd(), '..');
const out = fs.mkdtempSync(path.join(os.tmpdir(), 'bbc-preview-'));
test.after(() => fs.rmSync(out, { recursive: true, force: true }));
const env = { ...process.env, REGISTRY_PREVIEW: PREVIEW, SITE_DIST: out };
const build = spawnSync(process.execPath, [path.join('node_modules', 'astro', 'bin', 'astro.mjs'), 'build', '--outDir', out], { env, encoding: 'utf8', timeout: 300_000 });

const read = (url) => {
  const file = path.join(out, url, 'index.html');
  return fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '';
};
const html = read('/machines/bbc-micro/');
const machine = loadRegistry(undefined, PREVIEW).machines.find((m) => m.id === 'bbc-micro');
const section = (id) => {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
};
const panel = (() => {
  const at = html.indexOf('<div class="bbc"');
  return at < 0 ? '' : html.slice(at, html.indexOf('</fieldset>', at));
})();
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');

test('the deployed site has no BBC Micro page and links none: the registry still says planned', () => {
  assert.equal(loadRegistry(undefined, '').machines.find((m) => m.id === 'bbc-micro').status, 'planned');
  assert.equal(page('/machines/bbc-micro/'), undefined, 'dist has a BBC Micro page while the registry says planned');
  for (const url of ['/', '/machines/']) assert.doesNotMatch(page(url).html, /href="\/machines\/bbc-micro\//, `${url} links the BBC Micro`);
});

test('no workflow builds or deploys with a preview: only this test and the browser check use one', () => {
  for (const name of ['validate.yml', 'deploy-site.yml']) {
    assert.doesNotMatch(fs.readFileSync(path.join(root, '.github', 'workflows', name), 'utf8'), /REGISTRY_PREVIEW|fixtures\/bbc-micro-running/, `${name} sets a preview`);
  }
  assert.doesNotMatch(fs.readFileSync(path.join(process.cwd(), 'package.json'), 'utf8'), /REGISTRY_PREVIEW/);
});

test('the preview builds, and has the BBC Micro\'s page, linked from the machines table', () => {
  assert.equal(build.status, 0, `the preview build failed:\n${build.stdout?.slice(-2000)}\n${build.stderr?.slice(-2000)}`);
  assert.ok(html, '/machines/bbc-micro/ was not built in the preview');
  assert.match(read('/machines/'), /<a href="\/machines\/bbc-micro\/">BBC Micro<\/a>/);
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
  walk(path.join(out, 'machines', 'bbc-micro', '_framework'));
  for (const r of bbcRoms()) total += fs.statSync(path.join(out, 'machines', 'bbc-micro', r.file)).size;
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

test('the KIM-1\'s page is the same, byte for byte, with the BBC Micro switched on', () => {
  assert.equal(read('/machines/kim-1/'), page('/machines/kim-1/').html);
});

test('the whole site suite passes on the build with the BBC Micro switched on', () => {
  const files = fs.readdirSync('tests').filter((f) => f.endsWith('.test.mjs') && f !== 'bbc-page.test.mjs').map((f) => path.join('tests', f));
  // Without NODE_TEST_CONTEXT, which this runner sets for the files it runs: with it, the
  // inner runner reports to this one instead of running as a suite of its own.
  const { NODE_TEST_CONTEXT, ...outer } = env;
  const run = spawnSync(process.execPath, ['--test', ...files], { env: outer, encoding: 'utf8', timeout: 600_000 });
  // A piped run reports in TAP: "# pass 250".
  const count = (name) => Number(new RegExp(`^(?:#|ℹ) ${name} (\\d+)$`, 'm').exec(run.stdout)?.[1]);
  const failures = [...run.stdout.matchAll(/^not ok \d+ - (.*)$/gm)].map((m) => m[1]);
  assert.deepEqual(failures, [], 'tests fail on the preview build');
  assert.equal(run.status, 0, run.stdout.slice(-3000));
  assert.ok(count('pass') > 200, `only ${count('pass')} tests ran`);
  assert.equal(count('fail'), 0);
});
