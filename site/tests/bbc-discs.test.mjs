import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { page, visibleText, DIST } from './helpers.mjs';
import { REPO_ROOT } from '../src/lib/registry.mjs';
import {
  COPYLEFT, DISCS_DIR, DISCS_FOLDER, DISC_KINDS, DISC_LICENCES, discFolderUrl, discLibrary, discLicenceUrl, discProblems, discSourceUrl,
  discTracks, licenceIds, loadDiscs, needsSource,
} from '../src/lib/bbc-discs.mjs';
import { downloadBytes } from '../src/lib/bbc-micro.mjs';

// The BBC Micro's preset discs (machines/bbc-micro/discs/): other people's
// software, bundled only with a written licence that allows it (AGENTS.md
// rule 4), each kept with its licence, a README and, where the licence asks
// for it, its source; listed in the manifest the page and the build read; and
// offered on the page beside the visitor's own file, which stays as it was.

const discs = loadDiscs();
const sha256 = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');
const html = page('/machines/bbc-micro/')?.html ?? '';
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');
const panel = (() => {
  const at = html.indexOf('<div class="bbc"');
  return at < 0 ? '' : html.slice(at, html.indexOf('</fieldset>', at));
})();
const section = (id) => {
  const at = html.indexOf(`aria-labelledby="${id}"`);
  return at < 0 ? '' : html.slice(at, html.indexOf('</section>', at));
};

// The titles docs/bbc-micro/facts/discs.md says to avoid: commercial, non-commercial,
// no rights-holder statement, someone else's content, or hosted only by a mirror.
const AVOID = [
  'Hyper Viper', 'Cross-Lib', 'Cross Chase', 'Nursery Rhyme', 'Nyan', 'Arcade Adventure Design Kit', 'Repton', 'Zap', 'Usborne', 'Acornsoft', 'Welcome',
  'Chuckie Egg', 'Exile', 'Galaforce', 'NICCC', 'Bad Apple', 'Karateka', 'Bomberman', 'Alan Partridge', 'BCP', 'Elite', 'Level 9',
  'Vertigo', 'Bird Strike', 'b-tracker', 'jBiplane', 'Wobble Colours', 'Free Fall', 'Reversi', 'Krystal Connection', 'Mountain Panic',
  'White Light', 'Beebout', 'Polymer Picker', 'Androidz', 'HEX survivors', 'one-liners',
];

test('the manifest is well formed: every disc says what it is, whose it is and under what licence, with exactly one default', () => {
  assert.ok(discs.length > 0, 'the manifest lists no discs');
  assert.deepEqual(discProblems(discs), []);
  assert.equal(discs.filter((d) => d.default === true).length, 1);
  for (const d of discs) {
    assert.ok(d.licenceStatement.trim().length > 20, `${d.slug}: the licence statement is too short to be the author's words`);
    assert.match(d.sourceUrl, /^https?:\/\//, `${d.slug}: no link to the author's page`);
    assert.ok(d.kind in DISC_KINDS, `${d.slug}: kind ${d.kind}`);
    assert.equal(d.run, 'autoboot');
  }
  // The page shows them in the manifest's order: the default first, then games, puzzles, demos and tools.
  assert.equal(discs[0].default, true, 'the default is not first');
  const order = Object.values(DISC_KINDS).filter((g, i, all) => all.indexOf(g) === i);
  const groups = discs.map((d) => order.indexOf(DISC_KINDS[d.kind]));
  assert.deepEqual(groups, [...groups].sort((a, b) => a - b), 'the discs are not grouped in the order the page shows them');
});

test('every disc\'s image is in its folder with the manifest\'s size and SHA-256, a whole number of sectors, on a 40 or 80 track disc', () => {
  for (const d of discs) {
    const bytes = fs.readFileSync(path.join(REPO_ROOT, DISCS_DIR, d.slug, `${d.slug}.ssd`));
    assert.equal(bytes.length, d.bytes, `${d.slug}: size`);
    assert.equal(sha256(bytes), d.sha256, `${d.slug}: SHA-256`);
    assert.equal(bytes.length % 256, 0, `${d.slug}: not whole sectors`);
    assert.ok(bytes.length <= d.tracks * 2560, `${d.slug}: larger than a ${d.tracks}-track side`);
    assert.ok([40, 80].includes(d.tracks) && discTracks(bytes) === d.tracks, `${d.slug}: tracks`);
    assert.equal(d.sides, 1);
  }
});

test('every disc is kept with its licence text and a README, and a copyleft one with its source', () => {
  for (const d of discs) {
    const folder = path.join(REPO_ROOT, DISCS_DIR, d.slug);
    const licence = fs.readFileSync(path.join(folder, 'LICENSE'), 'utf8');
    assert.ok(licence.length > 100, `${d.slug}: the LICENSE file is too short to be a licence`);
    assert.ok(licenceIds(d.licence).every((id) => DISC_LICENCES.includes(id)), `${d.slug}: ${d.licence}`);
    // The licence file is the licence the manifest names.
    const named = licenceIds(d.licence)[0];
    const expect = { MIT: /Permission is hereby granted, free of charge/, 'BSD-2-Clause': /Redistribution and use in source and binary forms/, 'GPL-3.0-only': /GNU GENERAL PUBLIC LICENSE\s+Version 3/, 'GPL-3.0-or-later': /GNU GENERAL PUBLIC LICENSE\s+Version 3/, 'LGPL-3.0-only': /GNU LESSER GENERAL PUBLIC LICENSE\s+Version 3/ }[named];
    assert.match(licence, expect, `${d.slug}: its LICENSE is not the ${named} text`);
    const readme = fs.readFileSync(path.join(folder, 'README.md'), 'utf8');
    for (const fact of [d.title, d.author, d.licence, d.sha256, d.imageFrom]) assert.ok(readme.includes(fact), `${d.slug}: the README does not give ${fact}`);
    assert.match(readme, /## What was changed/, `${d.slug}: the README does not say what was changed`);
    const source = path.join(folder, 'source');
    if (needsSource(d)) {
      assert.ok(fs.existsSync(source) && fs.readdirSync(source).length > 0, `${d.slug} is ${d.licence} and has no source`);
    }
  }
  // Every copyleft licence the manifest may carry asks for the source.
  assert.deepEqual(COPYLEFT.filter((id) => !DISC_LICENCES.includes(id)), []);
  assert.ok(!DISC_LICENCES.some((id) => /NC|ND|proprietary|unknown/i.test(id)), 'a non-free licence is allowed');
});

test('every file in a disc\'s folder is listed in its README with its SHA-256, so nothing is there unaccounted for', () => {
  for (const d of discs) {
    const folder = path.join(REPO_ROOT, DISCS_DIR, d.slug);
    const readme = fs.readFileSync(path.join(folder, 'README.md'), 'utf8');
    const files = fs.readdirSync(folder, { recursive: true }).map(String).filter((f) => fs.statSync(path.join(folder, f)).isFile() && f !== 'README.md');
    for (const f of files) {
      // A file taken out of a disc image has a .inf beside it, listed with the file.
      const listed = f.endsWith('.inf') ? f.slice(0, -4) : f;
      const row = readme.split('\n').find((line) => line.startsWith(`| \`${listed.replaceAll(path.sep, '/')}\` |`));
      assert.ok(row, `${d.slug}: ${f} is not in its README`);
      if (!f.endsWith('.inf')) assert.ok(row.includes(sha256(fs.readFileSync(path.join(folder, f)))), `${d.slug}: ${f}'s SHA-256 in the README is not the file's`);
    }
  }
  // CP/M-65 keeps the notice of every third-party part on its disc, lib6502's included.
  for (const f of ['LICENSE', 'LICENSE.altirra-basic', 'LICENSE.pascal-m', 'LICENSE.lib6502']) assert.ok(fs.existsSync(path.join(REPO_ROOT, DISCS_DIR, 'cpm65', f)), `cpm65 has no ${f}`);
  assert.match(fs.readFileSync(path.join(REPO_ROOT, DISCS_DIR, 'cpm65', 'LICENSE.lib6502'), 'utf8'), /Copyright \(c\) 2005 Ian Piumarta/);
  assert.match(fs.readFileSync(path.join(REPO_ROOT, DISCS_DIR, 'blinkenlights', 'LICENSE.intro-to-interrupts'), 'utf8'), /Copyright \(c\) 2020 Kieran Connell/);
});

test('a manifest that breaks a rule is refused, with a sentence for each problem', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'discs-'));
  try {
    const d = { ...discs[0] };
    fs.cpSync(path.join(REPO_ROOT, DISCS_DIR, d.slug), path.join(root, DISCS_DIR, d.slug), { recursive: true });
    assert.deepEqual(discProblems([d], root), []);
    const problems = (change) => discProblems([{ ...d, ...change }], root).join('\n');
    assert.match(problems({ licence: 'CC-BY-NC-SA-4.0' }), /is not one a bundled disc may carry/);
    assert.match(problems({ licence: 'GPL-3.0-only AND LicenseRef-Unknown' }), /is not one a bundled disc may carry/);
    assert.match(problems({ sha256: '0'.repeat(64) }), /does not match the manifest's SHA-256/);
    assert.match(problems({ bytes: d.bytes + 1 }), /the manifest says/);
    assert.match(problems({ licenceStatement: '' }), /licenceStatement must be given/);
    assert.match(problems({ sourceUrl: 'not a link' }), /sourceUrl must link/);
    assert.match(problems({ default: false }), /exactly one disc must be the default/);
    fs.rmSync(path.join(root, DISCS_DIR, d.slug, 'source'), { recursive: true, force: true });
    assert.match(problems({}), /its licence asks for the source/);
    fs.rmSync(path.join(root, DISCS_DIR, d.slug, 'LICENSE'));
    assert.match(problems({}), /no LICENSE in its folder/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('none of the titles the research said to avoid is bundled', () => {
  const facts = fs.readFileSync(path.join(REPO_ROOT, 'docs', 'bbc-micro', 'facts', 'discs.md'), 'utf8');
  for (const title of AVOID) {
    for (const d of discs) assert.ok(!`${d.title} ${d.slug}`.toLowerCase().includes(title.toLowerCase()), `${d.title} looks like ${title}, which is not to be bundled`);
  }
  // The avoid list here follows the fact sheet's.
  for (const title of ['Hyper Viper', 'Cross-Lib', 'Elite', 'Level 9', 'Repton', 'Chuckie Egg', 'Usborne']) assert.ok(facts.includes(title), `discs.md no longer names ${title}`);
});

// Hosting by an archive is not permission (AGENTS.md rule 4): the image and the licence must
// come from the author's own site or repository, never from a mirror alone.
const MIRRORS = /\b(bbcmicro\.co\.uk|stairwaytohell\.com|8bs\.com|archive\.org|mdfs\.net)\b/i;

test('no disc\'s image or licence was taken from a mirror', () => {
  for (const d of discs) {
    for (const field of ['imageFrom', 'licenceUrl', 'sourceUrl']) assert.doesNotMatch(d[field], MIRRORS, `${d.slug}: ${field} is a mirror, ${d[field]}`);
  }
  assert.match('https://www.bbcmicro.co.uk/game.php?id=1', MIRRORS);
  assert.match('http://www.stairwaytohell.com/x.ssd', MIRRORS);
  assert.match('https://8bs.com/catalogue/tbi.htm', MIRRORS);
});

test('the edge serves the disc images as plain bytes: a rule in _headers for /machines/*/discs/*', () => {
  const headers = fs.readFileSync(path.join(process.cwd(), 'public', '_headers'), 'utf8');
  assert.match(headers, /^\/machines\/\*\/discs\/\*\n  Content-Type: application\/octet-stream$/m);
  // The built site carries the same file, and the images are where the rule points.
  assert.equal(fs.readFileSync(path.join(DIST, '_headers'), 'utf8'), headers);
  for (const d of discs) assert.ok(fs.existsSync(path.join(DIST, 'machines', 'bbc-micro', DISCS_FOLDER, `${d.slug}.ssd`)));
});

test('no disc image is committed outside machines/bbc-micro/discs/', () => {
  const tracked = execFileSync('git', ['ls-files', '-z'], { cwd: REPO_ROOT, encoding: 'utf8' }).split('\0').filter(Boolean);
  const images = tracked.filter((f) => /\.(ssd|dsd)$/i.test(f));
  const outside = images.filter((f) => !f.startsWith('machines/bbc-micro/discs/'));
  assert.deepEqual(outside, []);
  // And every committed image there is in the manifest.
  for (const f of images) assert.ok(discs.some((d) => f === `machines/bbc-micro/discs/${d.slug}/${d.slug}.ssd`), `${f} is committed but not in the manifest`);
});

test('the build publishes each image under discs/ beside the machine, as the manifest gives it, and nothing else there', () => {
  const folder = path.join(DIST, 'machines', 'bbc-micro', DISCS_FOLDER);
  assert.ok(fs.existsSync(folder), 'no discs in the build: run `npm run machines` before the build');
  assert.deepEqual(fs.readdirSync(folder).sort(), discs.map((d) => `${d.slug}.ssd`).sort());
  for (const d of discs) assert.equal(sha256(fs.readFileSync(path.join(folder, `${d.slug}.ssd`))), d.sha256, `${d.slug}`);
  // Start's download does not count them: they are fetched one at a time, when inserted.
  // (The folder also holds the page itself, index.html, which is not the machine's.)
  let framework = 0;
  const walk = (dir) => { for (const e of fs.readdirSync(dir, { withFileTypes: true })) { const f = path.join(dir, e.name); if (e.isDirectory()) walk(f); else if (e.name !== 'index.html') framework += fs.statSync(f).size; } };
  walk(path.join(DIST, 'machines', 'bbc-micro'));
  const inDiscs = discs.reduce((n, d) => n + d.bytes, 0);
  assert.equal(downloadBytes(), framework - inDiscs, 'the Start button counts the discs');
});

test('the build script checks every disc before the slow publish, and the CI cache of the BBC Micro\'s build is keyed on the discs', () => {
  const script = fs.readFileSync(path.join(process.cwd(), 'scripts', 'build-machines.mjs'), 'utf8');
  assert.ok(script.indexOf('readDisc(') < script.indexOf("execFileSync('dotnet'"), 'the discs are read after the publish starts');
  for (const name of ['validate.yml', 'deploy-site.yml']) {
    const text = fs.readFileSync(path.join(REPO_ROOT, '.github', 'workflows', name), 'utf8');
    const key = /id: bbc-micro\n[\s\S]*?key: machine-bbc-micro-[^\n]*/.exec(text)?.[0] ?? '';
    for (const input of ['machines/bbc-micro/discs/**', 'site/src/lib/bbc-discs.mjs']) assert.ok(key.includes(`'${input}'`), `${name}: the BBC Micro's cache key does not hash ${input}`);
  }
});

test('the page offers the library in the disc drive: a labelled list grouped by kind, the default chosen, all disabled until the machine runs', () => {
  const { groups, default: chosen } = discLibrary();
  const select = /<select id="bbc-preset" data-bbc-preset aria-describedby="bbc-preset-about" disabled>([\s\S]*?)<\/select>/.exec(panel);
  assert.ok(select, 'no list of discs, disabled until the script runs');
  assert.match(panel, /<label class="bbc-preset-label" for="bbc-preset">A disc from the library<\/label>/);
  const found = [...select[1].matchAll(/<optgroup label="([^"]+)">([\s\S]*?)<\/optgroup>/g)].map((g) => ({
    name: g[1],
    options: [...g[2].matchAll(/<option value="([^"]+)"( selected)?>([^<]*)<\/option>/g)].map((o) => ({ slug: o[1], selected: Boolean(o[2]), title: decode(o[3]) })),
  }));
  assert.deepEqual(found.map((g) => g.name), groups.map((g) => g.name));
  assert.deepEqual(found.flatMap((g) => g.options.map((o) => o.slug)), discs.map((d) => d.slug));
  assert.deepEqual(found.flatMap((g) => g.options.map((o) => o.title)), discs.map((d) => d.title));
  assert.deepEqual(found.flatMap((g) => g.options.filter((o) => o.selected).map((o) => o.slug)), [chosen.slug]);
  assert.match(panel, /<button type="button" class="btn small" data-bbc-preset-run disabled>Insert and run<\/button>/);
  assert.match(panel, /<button type="button" class="btn small" data-bbc-preset-insert disabled>Insert in drive 0<\/button>/);
  // Not the lime: Start keeps the page's one lime fill.
  assert.doesNotMatch(panel, /class="btn pill"[^>]*data-bbc-preset/);
});

test('under the list, each disc\'s credit: author, year, description, keys, licence linked to its text, the author\'s page, its source when copyleft, and its note first', () => {
  const { default: chosen } = discLibrary();
  for (const d of discs) {
    const block = new RegExp(`<div class="bbc-preset-disc" data-bbc-preset-disc="${d.slug}"([^>]*)>([\\s\\S]*?)</div>`).exec(panel);
    assert.ok(block, `${d.slug} has no credit under the list`);
    assert.equal(/\bhidden\b/.test(block[1]), d.slug !== chosen.slug, `${d.slug}: only the default's credit shows before a choice`);
    const text = decode(visibleText(block[2]));
    for (const fact of [d.title, d.author, d.year, d.description, d.controls, d.licence]) assert.ok(text.includes(fact), `${d.slug}: the credit does not give ${fact}`);
    assert.ok(block[2].includes(`<a href="${discLicenceUrl(d)}">${d.licence}</a>`), `${d.slug}: the licence is not linked to its text`);
    assert.ok(decode(block[2]).includes(`<a href="${d.sourceUrl}">`), `${d.slug}: the author's page is not linked`);
    assert.equal(block[2].includes(`<a href="${discSourceUrl(d)}">`), needsSource(d), `${d.slug}: the source link`);
    if (d.note) assert.match(block[2], new RegExp(`^\\s*<p class="bbc-preset-note" role="note">`), `${d.slug}: the note is not first, as a note`);
    assert.ok(block[1].includes(`data-licence="${d.licence}"`) && decode(block[1]).includes(`data-title="${d.title}"`), `${d.slug}: the driver cannot name it`);
  }
  // The photosensitivity notes are there before anything loads.
  const flashing = discs.filter((d) => /Photosensitivity/.test(d.note ?? ''));
  assert.deepEqual(flashing.map((d) => d.slug).sort(), ['blinkenlights', 'headcase-hotel']);
});

test('the visitor\'s own disc stays as it was: the file input, drop, blank, save, eject and write-protect are all still there', () => {
  assert.match(panel, /<button type="button" class="btn small" data-bbc-insert disabled>Insert a disc<\/button>/);
  assert.match(panel, /<input type="file" accept="\.ssd,\.dsd" data-bbc-file hidden\/?>/);
  assert.match(panel, /<button type="button" class="btn small" data-bbc-blank disabled>Make a blank disc<\/button>/);
  assert.match(panel, /<button type="button" class="btn small" data-bbc-save disabled>Save disc<\/button>/);
  assert.match(panel, /<button type="button" class="btn small" data-bbc-eject disabled>Eject<\/button>/);
  assert.match(panel, /data-bbc-protect disabled/);
  assert.match(visibleText(panel), /drop a \.ssd or \.dsd disc image on the page, or press Insert a disc/);
  const driver = fs.readFileSync(path.join(process.cwd(), 'public', 'bbc-micro.js'), 'utf8');
  assert.match(driver, /document\.addEventListener\('drop'/);
  assert.match(driver, /panel\.putDisc = put;/);
  // A preset is fetched from this site, beside the machine, and started with SHIFT and BREAK by the host.
  assert.match(driver, /bytes\(`\$\{panel\.dataset\.base\}discs\/\$\{slug\}\.ssd`\)/);
  assert.match(driver, /bbc\.ShiftBreak\(\)/);
  const host = fs.readFileSync(path.join(REPO_ROOT, 'src', 'Dbhq.Machines.BbcMicro.Wasm', 'Program.cs'), 'utf8');
  assert.match(host, /\[JSExport\]\s+public static void ShiftBreak\(\) => Keys\.ShiftBreak\(\);/);
});

test('the page credits every disc without anything loaded, says they are not MIT, and that commercial games are load-your-own', () => {
  const credits = section('discs');
  assert.match(credits, /<h2 id="discs">Credits and licences for the discs<\/h2>/);
  const text = decode(visibleText(credits));
  assert.match(text, /not under this project's MIT licence/);
  assert.match(text, /Commercial games are not/);
  assert.match(text, /goes in with Insert a disc/);
  const items = [...credits.matchAll(/<li data-bbc-credit="([^"]+)">([\s\S]*?)<\/li>/g)];
  assert.deepEqual(items.map((m) => m[1]), discs.map((d) => d.slug));
  for (const [, slug, body] of items) {
    const d = discs.find((x) => x.slug === slug);
    const t = decode(visibleText(body));
    for (const fact of [d.title, d.author, d.year, d.licence]) assert.ok(t.includes(fact), `${slug}: the credit does not give ${fact}`);
    assert.ok(body.includes(`<a href="${discLicenceUrl(d)}">${d.licence}</a>`), `${slug}: licence link`);
    assert.ok(decode(body).includes(`<a href="${d.sourceUrl}">`), `${slug}: author's page`);
    assert.ok(body.includes(`<a href="${discFolderUrl(d)}">`), `${slug}: folder`);
    assert.equal(body.includes(`<a href="${discSourceUrl(d)}">`), needsSource(d), `${slug}: source link`);
  }
});
