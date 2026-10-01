import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { page, visibleText, DIST } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';
import { KIM1_KEYS, loadTryIt, parseKeys } from '../src/lib/machines.mjs';
import { kim1Roms } from '../src/lib/pins.mjs';

// The KIM-1's own page, built against the real registry: the first page of a
// running machine, and the files it needs to run. Every check here reads the
// built site. If the machine's files are missing, run `npm run machines`
// (scripts/build-machines.mjs) before the build; CI does that before `npm test`.

const kim = loadRegistry().machines.find((m) => m.id === 'kim-1');
const built = page('/machines/kim-1/');
const html = built?.html ?? '';
const files = path.join(DIST, 'machines', 'kim-1');
const framework = path.join(files, '_framework');
const root = path.resolve(process.cwd(), '..');
const deploy = fs.readFileSync(path.join(root, '.github', 'workflows', 'deploy-site.yml'), 'utf8');
const validate = fs.readFileSync(path.join(root, '.github', 'workflows', 'validate.yml'), 'utf8');

test('the KIM-1 is running in the real registry, its page is built, and the machines table links it', () => {
  assert.equal(kim?.status, 'running');
  assert.ok(built, '/machines/kim-1/ was not built');
  assert.match(page('/machines/').html, /<a href="\/machines\/kim-1\/">KIM-1<\/a>/);
  assert.match(page('/').html, /<a href="\/machines\/kim-1\/">KIM-1<\/a>/, 'the home page card does not link the running machine');
});

test('the WebAssembly the page loads was built into the site: the loader, the runtime and the assemblies', () => {
  assert.ok(fs.existsSync(framework), 'dist/machines/kim-1/_framework is missing: run `npm run machines` before the build');
  const names = fs.readdirSync(framework);
  assert.ok(names.includes('dotnet.js'), 'no dotnet.js, the loader kim-1.js imports');
  for (const pattern of [/^dotnet\.native\.\w+\.wasm$/, /^dotnet\.native\.\w+\.js$/, /^dotnet\.runtime\.\w+\.js$/, /^Dbhq\.Machines\.Kim1\.Wasm\.\w+\.wasm$/, /^Dbhq\.Machines\.Kim1\.\w+\.wasm$/, /^Dbhq\.Cpu6502\.\w+\.wasm$/]) {
    assert.ok(names.some((n) => pattern.test(n)), `no file matching ${pattern}`);
  }
  // The compressed copies are left out (the edge compresses), and so are symbols.
  assert.deepEqual(names.filter((n) => /\.(br|gz|pdb)$/.test(n)), []);
  // Every .wasm really is WebAssembly: the four-byte magic number.
  for (const n of names.filter((x) => x.endsWith('.wasm'))) {
    assert.deepEqual([...fs.readFileSync(path.join(framework, n)).subarray(0, 4)], [0x00, 0x61, 0x73, 0x6d], `${n} is not WebAssembly`);
  }
});

test("the monitor ROM was fetched into the site, and each half's SHA-256 is the pinned one", () => {
  for (const rom of kim1Roms()) {
    const file = path.join(files, rom.file);
    assert.ok(fs.existsSync(file), `${rom.file} was not built into the site`);
    const bytes = fs.readFileSync(file);
    assert.equal(bytes.length, 1024, `${rom.file} is not 1 KB`);
    assert.equal(crypto.createHash('sha256').update(bytes).digest('hex'), rom.sha256, `${rom.file} does not match its pin in Pins.cs`);
  }
});

test('no ROM or published WebAssembly is committed: the folder they are built into is git-ignored', () => {
  assert.match(fs.readFileSync(path.join(process.cwd(), '.gitignore'), 'utf8'), /^public\/machines\/$/m);
});

test('the page loads its driver, which loads the machine from the folder the page names', () => {
  assert.match(html, /<script type="module" src="\/kim-1\.js"><\/script>/);
  assert.match(html, /data-base="\/machines\/kim-1\/"/);
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'kim-1.js'), 'utf8');
  assert.match(driver, /import\(`\$\{base\}_framework\/dotnet\.js`\)/);
  for (const rom of kim1Roms()) assert.ok(driver.includes(`\${base}${rom.file}`), `kim-1.js does not load ${rom.file}`);
  assert.match(driver, /getAssemblyExports\('Dbhq\.Machines\.Kim1\.Wasm'\)/);
});

test('the page runs the machine at the clock in its registry entry, never a typed one', () => {
  const mhz = Number(/([\d.]+) MHz/.exec(kim.cpu)[1]);
  assert.match(html, new RegExp(`data-clock-mhz="${mhz}"`));
});

test('the keypad has every KIM-1 key once, as real buttons, in the board\'s layout, and the SST switch', () => {
  const keys = [...html.matchAll(/<button type="button" class="key" data-key="([^"]+)"[^>]*>/g)];
  assert.deepEqual(keys.map((m) => m[1]).sort(), [...KIM1_KEYS].sort());
  // The board's rows, top to bottom: GO ST RS (and SST), AD DA PC +, then C D E F down to 0 1 2 3.
  assert.deepEqual(keys.map((m) => m[1]), ['GO', 'ST', 'RS', 'AD', 'DA', 'PC', '+', 'C', 'D', 'E', 'F', '8', '9', 'A', 'B', '4', '5', '6', '7', '0', '1', '2', '3']);
  assert.match(html, /<input type="checkbox" role="switch" data-kim1-sst disabled\/?> SST<\/label>/);
  assert.match(html, /role="group" aria-label="Keypad"/);
});

test('without JavaScript the keys are disabled and the page says the machine needs it', () => {
  for (const m of html.matchAll(/<button type="button" class="key"[^>]*>/g)) assert.match(m[0], /\bdisabled\b/);
  const status = /<p class="kim1-status" role="status" data-kim1-status>([^<]*)<\/p>/.exec(html);
  assert.ok(status, 'no status line');
  assert.match(status[1], /needs JavaScript/);
});

test('the digits are drawn, hidden from screen readers, and summarised in a polite live region instead', () => {
  const digits = [...html.matchAll(/<svg class="digit[^"]*" data-digit="(\d)"[^>]*>([\s\S]*?)<\/svg>/g)];
  assert.deepEqual(digits.map((m) => m[1]), ['0', '1', '2', '3', '4', '5']);
  for (const d of digits) assert.equal((d[2].match(/<polygon data-seg=/g) ?? []).length, 7, 'a digit needs seven segments');
  assert.match(html, /<div class="kim1-display" aria-hidden="true">/);
  assert.match(html, /<p class="sr-only" aria-live="polite" data-kim1-said><\/p>/);
  assert.doesNotMatch(html, /<img\b[^>]*kim/i, 'the machine must be drawn, not a picture');
});

test('the program on the page is the acceptance test\'s program, step by step, from the same file', () => {
  const { steps } = loadTryIt('kim-1');
  const tests = fs.readFileSync(path.join(root, 'tests', 'Dbhq.Machines.Kim1.Tests', 'Kim1AcceptanceTests.cs'), 'utf8');
  assert.match(tests, /"machines", "kim-1", "try-it\.json"/, 'Kim1AcceptanceTests no longer reads the page\'s program');
  const items = [...html.matchAll(/<li>\s*<p class="keys">([\s\S]*?)<\/p>([\s\S]*?)<\/li>/g)];
  assert.equal(items.length, steps.length);
  steps.forEach((step, i) => {
    const kbds = [...items[i][1].matchAll(/<kbd>([^<]*)<\/kbd>/g)].map((m) => m[1].replace('&#43;', '+'));
    assert.deepEqual(kbds, parseKeys(step.keys), `step ${i + 1}'s keys`);
    const text = visibleText(items[i][2]);
    assert.ok(text.includes(step.says), `step ${i + 1} does not say "${step.says}"`);
    // The display is in a <code>, which visibleText leaves out, so it is read from the markup.
    assert.ok(items[i][2].includes(`<code>${step.display.trim() === '' ? 'nothing: dark' : step.display}</code>`), `step ${i + 1} does not show ${step.display}`);
  });
});

test('every key named in the program is a key on the keypad', () => {
  for (const step of loadTryIt('kim-1').steps) for (const k of parseKeys(step.keys)) assert.ok(KIM1_KEYS.includes(k), k);
});

test('keys are read as the manual writes them, the same way Kim1Keystrokes.Parse reads them', () => {
  assert.deepEqual(parseKeys('[AD] 0002 [DA] 18'), ['AD', '0', '0', '0', '2', 'DA', '1', '8']);
  assert.deepEqual(parseKeys('[+] [GO] [PC] [ST] [RS]'), ['+', 'GO', 'PC', 'ST', 'RS']);
  assert.deepEqual(parseKeys('fa'), ['F', 'A']);
  assert.deepEqual(parseKeys('AD'), ['A', 'D']);
  assert.throws(() => parseKeys('[XY]'));
  assert.throws(() => parseKeys('G'));
});

test('the page says whose ROM it runs, in the registry\'s own words, and where the copy came from', () => {
  const text = visibleText(html);
  assert.ok(text.includes(kim.rights), 'the rights text is not the registry\'s');
  assert.match(text, /MOS Technology/);
  for (const rom of kim1Roms()) {
    assert.ok(html.includes(`href="${rom.url}"`), `${rom.chip}'s pinned URL is not linked`);
    assert.ok(html.includes(`<code>${rom.sha256}</code>`), `${rom.chip}'s SHA-256 is not shown`);
  }
});

test('both workflows build the machines before the site, with the wasm-tools workload at a pinned version', () => {
  for (const [name, text] of [['validate.yml', validate], ['deploy-site.yml', deploy]]) {
    assert.match(text, /dotnet workload install wasm-tools --version \d+\.\d+\.\d+/, `${name} does not pin the wasm-tools workload`);
    assert.match(text, /working-directory: site\n\s+run: node scripts\/build-machines\.mjs/, `${name} does not build the machines from site/`);
    assert.ok(text.indexOf('build-machines.mjs') < text.indexOf('npm test'), `${name} builds the machines after the site`);
  }
});

test('the deploy checks every file under machines/ is served, and .wasm as application/wasm', () => {
  assert.match(deploy, /find machines -type f/, 'the deploy does not derive the machine files from the build');
  assert.match(deploy, /application\/wasm/);
});
