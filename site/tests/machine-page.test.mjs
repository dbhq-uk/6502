import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { page, visibleText, DIST } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';
import { KIM1_KEYS, TELETYPE_KEYS, loadTeletype, loadTryIt, parseKeys, parseTeletype, teletypeChars } from '../src/lib/machines.mjs';
import { loadResults } from '../src/lib/results.mjs';
import { Paper } from '../public/teletype-paper.js';
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
  assert.ok(names.includes('dotnet.js'), 'no dotnet.js, the loader machine-host.js imports');
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

test("the monitor ROM was copied into the site from roms/, and each half's SHA-256 is the pinned one", () => {
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
  // The runtime is loaded by the shared host, machine-host.js, which the driver
  // imports and hands the base and the assembly to (moved there in task 13 of
  // the BBC Micro plan; the same two checks, on the file that now does each).
  const host = fs.readFileSync(path.join(process.cwd(), 'public', 'machine-host.js'), 'utf8');
  assert.match(driver, /^import \{ startMachine \} from '\/machine-host\.js';$/m);
  assert.match(driver, /startMachine\(\{\s*panel,\s*base,/);
  assert.match(host, /import\(`\$\{base\}_framework\/dotnet\.js`\)/);
  for (const rom of kim1Roms()) assert.ok(driver.includes(`\${base}${rom.file}`), `kim-1.js does not load ${rom.file}`);
  assert.match(driver, /assembly: 'Dbhq\.Machines\.Kim1\.Wasm',\s*hostClass: 'Kim1Host',/);
  assert.match(host, /getAssemblyExports\(assembly\)/);
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
  // The page has a photograph of the board, but the machine itself is drawn, not a picture.
  const panel = html.slice(html.indexOf('data-kim1 '), html.indexOf('data-kim1-speed'));
  assert.ok(panel.length > 0, 'the panel was not found');
  assert.doesNotMatch(panel, /<img\b|<picture\b/, 'the machine must be drawn, not a picture');
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

// The teletype: the panel's part of it, and the session on the page.
const ttyPanel = (() => {
  const at = html.indexOf('data-kim1-tty');
  return at < 0 ? '' : html.slice(html.lastIndexOf('<div', at), html.indexOf('data-kim1-status', at));
})();

test('the teletype is on the panel: a labelled group with the TTY switch, the speed, the paper as a log, a labelled field and RUB OUT and LF, all disabled without JavaScript', () => {
  assert.ok(ttyPanel, 'the panel has no teletype');
  assert.match(ttyPanel, /role="group" aria-labelledby="kim1-tty-name"/);
  assert.match(ttyPanel, /id="kim1-tty-name">Teletype</);
  assert.match(ttyPanel, /<label class="kim1-tty-switch"><input type="checkbox" role="switch" data-kim1-mode aria-describedby="kim1-tty-about" disabled\/?> TTY switch<\/label>/);
  assert.match(ttyPanel, /<div class="kim1-paper" role="log" aria-label="The teletype's paper" tabindex="0" data-kim1-paper><\/div>/);
  assert.match(ttyPanel, /<label class="kim1-tty-input">Type to the KIM-1 <input type="text" data-kim1-type[^>]*disabled/);
  const options = [...ttyPanel.matchAll(/<option value="(\d+)"( selected)?>/g)];
  assert.deepEqual(options.map((o) => Number(o[1])), [110, 300, 1200]);
  assert.equal(options.find((o) => o[2])?.[1], '110', 'the terminal does not start at 110 baud, the Teletype Model 33\'s speed');
  assert.deepEqual([...ttyPanel.matchAll(/data-kim1-send="(\w+)"/g)].map((m) => m[1]), ['RUBOUT', 'LF']);
  for (const m of ttyPanel.matchAll(/<(button|select|input)\b[^>]*>/g)) assert.match(m[0], /\bdisabled\b/, `${m[0]} is enabled without JavaScript`);
});

test('the page says the teletype is modelled, and the registry says only the cassette and the expansion bus are not', () => {
  assert.doesNotMatch(kim.notes, /teletype interface, the cassette/);
  assert.match(kim.notes, /Not modelled: the cassette interface and the expansion bus\./);
  assert.match(kim.notes, /teletype/);
  const text = visibleText(html);
  assert.ok(text.includes(kim.notes), 'the page does not show the registry\'s notes');
  assert.doesNotMatch(text, /teletype[^.]*not modelled/i);
});

test('the teletype session on the page is Kim1TeletypeTests\'s, step by step, from the same file, with that suite\'s count from the results', () => {
  const { steps } = loadTeletype('kim-1');
  const tests = fs.readFileSync(path.join(root, 'tests', 'Dbhq.Machines.Kim1.Tests', 'Kim1TeletypeTests.cs'), 'utf8');
  assert.match(tests, /"machines", "kim-1", "teletype\.json"/, 'Kim1TeletypeTests no longer reads the page\'s session');
  const at = html.indexOf('<h2 id="teletype">');
  assert.ok(at > 0, 'the page has no teletype section');
  const section = html.slice(at, html.indexOf('</section>', at));
  const items = [...section.matchAll(/<li>([\s\S]*?)<\/li>/g)].map((m) => m[1]);
  assert.equal(items.length, steps.length);
  const names = { SPACE: 'SPACE', CR: 'RETURN', LF: 'LF', RUBOUT: 'RUB OUT' };
  const kbds = (p) => [...(p ?? '').matchAll(/<kbd>([^<]*)<\/kbd>/g)].map((m) => decode(m[1]).replace('&#43;', '+'));
  steps.forEach((step, i) => {
    const keypad = /<p class="tty-keys">On the keypad:([\s\S]*?)<\/p>/.exec(items[i])?.[1];
    const typed = /<p class="tty-keys">At the teletype:([\s\S]*?)<\/p>/.exec(items[i])?.[1];
    assert.deepEqual(kbds(keypad), step.keypad ? parseKeys(step.keypad) : [], `step ${i + 1}'s keypad keys`);
    assert.deepEqual(kbds(typed), step.type ? parseTeletype(step.type).map((p) => (p.key ? names[p.key] : p.text)) : [], `step ${i + 1}'s teletype keys`);
    assert.ok(visibleText(items[i]).includes(step.says), `step ${i + 1} does not say "${step.says}"`);
    assert.ok(items[i].includes(`<code>${step.prints.join('\n')}</code>`), `step ${i + 1} does not show what the paper prints`);
  });
  const passed = loadResults().suites.Kim1TeletypeTests?.passed;
  assert.ok(passed > 0, 'Kim1TeletypeTests is not in the results');
  assert.ok(visibleText(section).includes(`that suite has ${passed.toLocaleString('en-GB')} tests passing`), 'the page does not give Kim1TeletypeTests\'s count from the results');
});

test('teletype keys are read as Kim1Teletype.Parse reads them, with the same named keys', () => {
  const cs = fs.readFileSync(path.join(root, 'src', 'Dbhq.Machines.Kim1', 'Kim1Teletype.cs'), 'utf8');
  const named = Object.fromEntries([...cs.matchAll(/\["(\w+)"\] = (?:'(\\?.)'|\(char\)0x([0-9A-F]+))/g)].map((m) => [m[1], m[3] ? String.fromCharCode(parseInt(m[3], 16)) : JSON.parse(`"${m[2]}"`)]));
  assert.deepEqual(named, TELETYPE_KEYS);
  assert.deepEqual(parseTeletype('0200[SPACE]AD.'), [{ text: '0200' }, { key: 'SPACE' }, { text: 'AD.' }]);
  assert.equal(teletypeChars('[RUBOUT]0200[SPACE]G[CR][LF]'), '\u007f0200 G\r\n');
  for (const bad of ['0200 G', '[RUB OUT]', '[TAB]', '[SPACE', 'café']) assert.throws(() => parseTeletype(bad), bad);
});

test('the paper prints as a teletype does: CR to the start of the line, LF to a new one, nothing for NUL, RUB OUT or XOFF', () => {
  const paper = new Paper();
  const nuls = '\u0000'.repeat(6);
  paper.print(`\u007f\r\n${nuls}KIM\r\n${nuls}0000 00 0002 `);
  assert.deepEqual(paper.lines, ['', 'KIM', '0000 00 0002 ']);
  paper.print('\r\n0002 00 \u0013');
  assert.equal(paper.text(), '\nKIM\n0000 00 0002\n0002 00');
  // A carriage return without a line feed prints over the line.
  paper.print('\rX');
  assert.equal(paper.lines.at(-1), 'X002 00 ');
  // The oldest lines are torn off, and the change says how many.
  // A line feed alone moves the paper and leaves the head where it was.
  const feed = new Paper();
  feed.print('A\nB');
  assert.deepEqual(feed.lines, ['A', ' B']);
  const short = new Paper(3);
  assert.deepEqual(short.print('A\r\nB\r\nC\r\nD\r\nE'), { from: 0, torn: 2 });
  assert.deepEqual(short.lines, ['C', 'D', 'E']);
});

test('typing at the teletype is never also a press of the keypad, and the field leaves Tab alone', () => {
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'kim-1.js'), 'utf8');
  assert.match(driver, /tty\.contains\(event\.target\)\) return;/, 'the keypad\'s keyboard handler does not skip the teletype');
  const keys = /const key = \{([^}]*)\}\[event\.key\]/.exec(driver)?.[1] ?? '';
  assert.deepEqual([...keys.matchAll(/(\w+):/g)].map((m) => m[1]), ['Enter', 'Backspace', 'Delete']);
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

// The photograph of the original board, and its credit, from the registry.
const figure = (() => {
  const at = html.indexOf('<figure class="photo"');
  return at < 0 ? '' : html.slice(at, html.indexOf('</figure>', at) + '</figure>'.length);
})();
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"');

test('the page\'s head shows the registry\'s main photograph of the original, from this site, with its alt text and its size set', () => {
  assert.ok(kim.photos?.length >= 1, 'the KIM-1 has no photograph in the registry');
  const main = kim.photos[0];
  assert.ok(figure, 'the page has no photograph');
  const img = /<img\b[^>]*>/.exec(figure)?.[0] ?? '';
  const stem = main.file.replace(/\.[a-z]+$/, '');
  assert.match(img, new RegExp(`src="/_astro/${stem}\\.[\\w-]+\\.webp"`), 'the photograph is not served from this site');
  assert.match(figure, new RegExp(`<source srcset="/_astro/${stem}\\.[^"]+\\.avif 360w`), 'no AVIF copies in the srcset');
  assert.equal(decode(/\balt="([^"]*)"/.exec(img)?.[1] ?? ''), main.alt);
  // Its box is reserved before it loads: no layout shift.
  assert.match(img, /\bwidth="\d+"/);
  assert.match(img, /\bheight="\d+"/);
  assert.doesNotMatch(img, /data-generated/, 'a photograph is not a generated image');
  assert.doesNotMatch(figure, /src="https?:/, 'the photograph is hot-linked');
});

test('the photograph is credited on the page from the registry: its author, a link to its source and the licence as the source states it', () => {
  const main = kim.photos[0];
  const caption = /<figcaption class="figure-caption">([\s\S]*?)<\/figcaption>/.exec(figure)?.[1] ?? '';
  // visibleText puts a space where a tag was; a link before a full stop leaves one there.
  const text = visibleText(caption).replace(/\s+([.,])/g, '$1');
  assert.match(text, /^Photograph: /, 'the caption does not call it a photograph');
  assert.doesNotMatch(text, /illustration/i);
  assert.ok(text.includes(`By ${main.author}.`), 'the author is not credited');
  assert.ok(caption.includes(`href="${main.sourceUrl}"`), 'the source is not linked');
  assert.ok(text.includes(main.licence === null ? 'Licence: no licence stated.' : `Licence: ${main.licence}.`), 'the licence is not given as the source states it');
  if (main.licenceUrl) assert.ok(caption.includes(`href="${main.licenceUrl}"`), 'the licence is not linked');
  assert.match(text, /taken \d{1,2} \w+ \d{4}|taken \w+ \d{4}|taken \d{4}/, 'the caption does not say when it was taken');
});

// Every photograph the page uses, in the photographs section, each credited.
const gallery = (() => {
  const at = html.indexOf('<section class="photos"');
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
})();

test('the photographs section shows every photograph in the registry, main one first, each from this site, lazily, with its alt text and size set', () => {
  assert.ok(gallery, 'the page has no photographs section');
  assert.match(gallery, /<h2 id="photos">Photographs of the original<\/h2>/);
  const figures = [...gallery.matchAll(/<figure class="photo photo-item"[^>]*data-photo-file="([^"]+)"[^>]*>([\s\S]*?)<\/figure>/g)];
  assert.deepEqual(figures.map((f) => f[1]), kim.photos.map((p) => p.file), 'the section does not show the registry\'s photographs, in its order');
  for (const [, file, body] of figures) {
    const p = kim.photos.find((x) => x.file === file);
    const img = /<img\b[^>]*>/.exec(body)?.[0] ?? '';
    const stem = file.replace(/\.[a-z]+$/, '');
    assert.match(img, new RegExp(`src="/_astro/${stem}\\.[\\w-]+\\.webp"`), `${file} is not served from this site`);
    assert.match(img, /\bloading="lazy"/, `${file} is not loaded lazily`);
    assert.match(img, /\bwidth="\d+"/);
    assert.match(img, /\bheight="\d+"/);
    assert.equal(decode(/\balt="([^"]*)"/.exec(img)?.[1] ?? ''), p.alt);
  }
});

test('every photograph in the section is credited from the registry: its author, a link to its source, its licence as stated, and what the model took from it', () => {
  const figures = [...gallery.matchAll(/data-photo-file="([^"]+)"[^>]*>([\s\S]*?)<\/figure>/g)];
  assert.equal(figures.length, kim.photos.length);
  for (const [, file, body] of figures) {
    const p = kim.photos.find((x) => x.file === file);
    const caption = /<figcaption class="figure-caption">([\s\S]*?)<\/figcaption>/.exec(body)?.[1] ?? '';
    const text = visibleText(caption).replace(/\s+([.,:])/g, '$1');
    assert.match(text, /^Photograph: /, `${file} is not captioned as a photograph`);
    assert.ok(text.includes(`By ${p.author}.`), `${file}'s author is not credited`);
    assert.ok(caption.includes(`href="${p.sourceUrl}"`), `${file}'s source is not linked`);
    assert.ok(text.includes(p.licence === null ? 'Licence: no licence stated.' : `Licence: ${p.licence}.`), `${file}'s licence is not given as the source states it`);
    if (p.licenceUrl) assert.ok(caption.includes(`href="${p.licenceUrl}"`), `${file}'s licence is not linked`);
    assert.ok(text.includes(`Used for the model: ${p.used}.`), `${file} does not say what the model took from it`);
  }
  // A photograph with no licence stated says so in words, not by leaving the licence out.
  assert.ok(kim.photos.some((p) => p.licence === null), 'no photograph here tests the "no licence stated" case');
});
