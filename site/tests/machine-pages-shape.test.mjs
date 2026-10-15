import test from 'node:test';
import assert from 'node:assert/strict';
import { page, visibleText, pagePart, foldOf, modelPanel } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';
import { loadTryIt, runningMachines } from '../src/lib/machines.mjs';
import { modelsOf } from '../src/models/models.mjs';

// Every machine page has the same shape (Dan, 15 October 2026: "too much text
// where the app is; keep everything concise"). The machine, Try it and how to
// type at it come first and stay open. One short line under the machine says
// whose ROM or game it runs, and is never folded. Everything else is reference,
// in one section at the foot of the page, each part a closed <details> with a
// plain label: the facts, links and credits are still in the page, for the
// tests and for anyone who opens them, but they do not push the machine down.

const registry = loadRegistry();
const machines = runningMachines(registry);
const htmlOf = (m) => page(`/machines/${m.id}/`)?.html ?? '';
const main = (html) => /<main\b[\s\S]*<\/main>/.exec(html)?.[0] ?? '';
const closed = (html) => {
  let out = html;
  for (let before; before !== out;) { before = out; out = out.replace(/<details(?![^>]*\sopen\b)[^>]*>\s*<summary\b[^>]*>[\s\S]*?<\/summary>((?:(?!<details)[\s\S])*?)<\/details>/, ''); }
  return out;
};
const decode = (t) => t.replace(/&amp;/g, '&').replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>');
const h2s = (html) => [...html.matchAll(/<h2 id="([^"]+)">([^<]*)<\/h2>/g)].map((m) => m[1]);

test('every running machine has a page, and there are five or more to hold to one shape', () => {
  assert.ok(machines.length >= 5, `only ${machines.length} machines run`);
  for (const m of machines) assert.ok(htmlOf(m), `/machines/${m.id}/ was not built`);
});

test('every machine page has the same order: the machine, Try it, then the models and the reference at the foot', () => {
  for (const m of machines) {
    const ids = h2s(main(htmlOf(m))).filter((id) => id !== 'model');
    assert.equal(ids[0], 'run', `${m.id}: the machine is not the first section`);
    assert.equal(ids[1], 'try', `${m.id}: Try it does not follow the machine`);
    assert.equal(ids.at(-1), 'more', `${m.id}: the reference is not the last section`);
    assert.ok(main(htmlOf(m)).includes(`<h2 id="more">About the ${m.name}</h2>`), `${m.id}: the reference section is not headed for the machine`);
  }
});

test('the reference parts are closed details with a plain label, and each running machine has the same core three', () => {
  for (const m of machines) {
    const html = htmlOf(m);
    const more = html.slice(html.indexOf('<section class="machine-more"'));
    const folds = [...more.matchAll(/<details\b[^>]*\bid="([^"]+)"[^>]*>/g)].map((x) => x[1]);
    for (const id of ['left-out', 'photos', 'rom']) assert.ok(folds.includes(id), `${m.id}: the reference has no ${id}`);
    for (const id of folds) {
      const { summary, open } = foldOf(pagePart(html, id));
      assert.equal(open, false, `${m.id}: ${id} is open when the page loads`);
      assert.ok(summary.length > 8 && !summary.endsWith('.'), `${m.id}: ${id}'s summary "${summary}" is not a label`);
    }
  }
});

test('the head keeps only what the machine is, its acceptance count and its photograph with the credit beside it, never folded', () => {
  for (const m of machines) {
    const html = htmlOf(m);
    const head = html.slice(html.indexOf('<section class="machine-head"'), html.indexOf('<section class="machine-run"'));
    assert.doesNotMatch(head, /<details\b/, `${m.id}: something in the head is folded`);
    assert.match(head, /data-photo-credit/, `${m.id}: the main photograph's credit is not beside it`);
    assert.ok(visibleText(head).includes(`${m.year}, ${m.maker}, ${m.cpu}.`), `${m.id}: the head does not say what the machine is`);
  }
});

test('one short line under the machine says whose ROM or game it runs, and it is never folded', () => {
  for (const m of machines) {
    const html = htmlOf(m);
    const run = pagePart(html, 'run');
    const line = /<p class="rights-line" data-rights-line>([\s\S]*?)<\/p>/.exec(closed(run))?.[1];
    assert.ok(line, `${m.id}: no visible rights line under the machine`);
    const text = visibleText(line).replace(/\s+([.,:])/g, '$1');
    assert.ok(text.split(/\s+/).length <= 40, `${m.id}: the rights line is ${text.split(/\s+/).length} words, not short`);
    assert.match(line, /<a href="#rom">/, `${m.id}: the rights line does not lead to the full rights`);
    assert.match(text, /Full rights: (Whose (ROM|ROMs|game) (this is|these are))\.$/, `${m.id}: the rights line does not end by leading to the full rights`);
    const program = loadTryIt(m.id);
    if (m.rights.includes('no system ROM')) {
      // A console: the game in it, its author and its licence, and that commercial games are the visitor's own.
      for (const fact of [program.title, `by ${program.author}`, program.licence]) assert.ok(text.includes(fact), `${m.id}: the rights line does not give ${fact}`);
      // The licence is a link where the game's licence has an address, not only the words.
      if (program.licenceUrl) assert.ok(line.includes(`<a href="${program.licenceUrl}">`), `${m.id}: the rights line does not link the licence at ${program.licenceUrl}`);
      assert.match(text, /Commercial games are not bundled and never will be: load your own\./);
      assert.match(m.rights, /Commercial games are not bundled and never will be/);
    } else {
      // A computer: whose ROM it is, and the registry's own caveat, word for word.
      const holder = /MOS Technology|Acorn/.exec(m.rights)?.[0];
      assert.ok(holder && text.includes(holder), `${m.id}: the rights line does not name the holder the rights name`);
      assert.ok(/no licence or permission from any holder was found/i.test(text) && m.rights.includes('no licence or permission from any holder was found'), `${m.id}: the rights line and the rights do not both say no permission was found`);
      if (m.rights.includes('documented, not cleared')) assert.match(text, /documented, not cleared/, `${m.id}: the rights line drops "documented, not cleared"`);
      // The registry says who holds it now was not established; the KIM-1's short line says so as well.
      if (m.id === 'kim-1') assert.match(text, /who holds it now was not established/i, `${m.id}: the rights line drops that who holds the ROM now was not established`);
    }
  }
});

test('the Atari 2600\'s rights line links the game\'s licence, the MIT licence at the address its try-it file gives', () => {
  const program = loadTryIt('atari-2600');
  assert.ok(program.licenceUrl, 'the Atari 2600\'s game has no licence address to link');
  const line = /<p class="rights-line" data-rights-line>([\s\S]*?)<\/p>/.exec(htmlOf(machines.find((m) => m.id === 'atari-2600')))[1];
  assert.ok(line.includes(`<a href="${program.licenceUrl}">MIT licence</a>`), 'the licence is words without its link');
});

test('folding hides nothing from the page: every issue link and every credit is still in the built page, and the full rights in the registry\'s words', () => {
  for (const m of machines) {
    const html = htmlOf(m);
    assert.ok(decode(visibleText(pagePart(html, 'rom'))).includes(m.rights), `${m.id}: the full rights are not in the page`);
    for (const p of m.photos) assert.ok(pagePart(html, 'photos').includes(`href="${p.sourceUrl.replace(/&/g, '&amp;')}"`), `${m.id}: ${p.file}'s source is not linked`);
    // Every issue link in the reference is a link to this repository's issues.
    const issues = [...pagePart(html, 'left-out').matchAll(/<a href="([^"]+)">issue (\d+)<\/a>/g)];
    for (const [, url, n] of issues) assert.equal(url, `https://github.com/dbhq-uk/6502/issues/${n}`);
    if (m.id !== 'kim-1') assert.ok(issues.length > 0, `${m.id}: the parts left out link no issue`);
  }
});

// What the machine models is said once, in the open, in the head; what it does
// not model is the fold's, as the list of parts with their issues (or, for the
// KIM-1, which has no issues to link, the registry's own sentence).
const modelsSentence = (m) => m.notes.split(/(?<=\.)\s+(?=Not modelled)/)[0];
const lowerFirst = (t) => t[0].toLowerCase() + t.slice(1);

test('the head says in one sentence what the machine models, from the first sentence of the registry\'s notes', () => {
  for (const m of machines) {
    const html = htmlOf(m);
    const head = visibleText(html.slice(html.indexOf('<section class="machine-head"'), html.indexOf('<section class="machine-run"')));
    const sentence = modelsSentence(m);
    assert.ok(sentence.endsWith('.') && !sentence.includes('Not modelled'), `${m.id}: the notes' first sentence is not one sentence about what is modelled`);
    assert.ok(head.includes(`It models ${lowerFirst(sentence)}`), `${m.id}: the head does not say what the machine models`);
  }
});

test('the not-modelled part says the parts once: the list of issues where there is one, so no paragraph sits above a list of the same parts', () => {
  for (const m of machines) {
    const left = decode(visibleText(pagePart(htmlOf(m), 'left-out')));
    assert.ok(!left.includes(modelsSentence(m)), `${m.id}: the fold repeats what the head says is modelled`);
    const hasList = /<li>[^<]*<a href="[^"]+">issue \d+<\/a>/.test(pagePart(htmlOf(m), 'left-out'));
    const notModelled = m.notes.slice(m.notes.indexOf('Not modelled'));
    if (hasList) assert.ok(!left.includes('Not modelled:'), `${m.id}: the fold has a paragraph of the parts left out above a list of the same parts`);
    else assert.ok(left.includes(notModelled), `${m.id}: the parts left out are not in the fold, in the registry's words`);
  }
});

// The 3D models. Each model's note on how it was made is a fold, so the folds on one page must be told apart by their labels, and
// the credit for the photographs, drawings and scans it was traced from stays in the open, short, under the heading, with a link to the fold.
const modelFolds = (m) => modelsOf(m).flatMap(({ view, module }) => {
  const panel = modelPanel(htmlOf(m), module);
  return [...panel.matchAll(/<div class="model-made prose"[^>]*>([\s\S]*?)<\/details>\s*<\/div>/g)].map((x) => ({ view, module, html: x[1] }));
});
const esc = (u) => u.replace(/&/g, '&amp;');
const sourcesOf = (m) => [...(m.photos ?? []), ...(m.drawings ?? []), ...(m.references ?? [])];

test('the folds of the 3D models say which view they are about, so no two summaries on a page are the same', () => {
  for (const m of machines.filter((x) => modelsOf(x).length)) {
    const labels = new Map();
    for (const { view, html } of modelFolds(m)) {
      const summary = foldOf(/<details[\s\S]*$/.exec(html)[0] + '</details>').summary;
      assert.match(summary, /^What the (board|outside|inside) was measured from/, `${m.id}: "${summary}" does not name the view`);
      labels.set(view, summary);
    }
    assert.equal(new Set(labels.values()).size, labels.size, `${m.id}: two views have the same fold label: ${[...labels.values()].join(' | ')}`);
    if (modelsOf(m).length > 1) assert.ok(labels.size > 1);
  }
});

test('every 3D model credits its sources in the open, by author and licence, and links to the fold that says what was taken from each', () => {
  let checked = 0;
  for (const m of machines.filter((x) => modelsOf(x).length)) {
    for (const { view, html } of modelFolds(m)) {
      const outside = html.slice(0, html.indexOf('<details'));
      const byline = /<p class="model-byline" data-model-byline>([\s\S]*?)<\/p>/.exec(outside)?.[1];
      assert.ok(byline, `${m.id} ${view}: no credit line outside the fold`);
      const fold = /<details\b[^>]*\bid="([^"]+)"/.exec(html)?.[1];
      assert.ok(fold, `${m.id} ${view}: the fold has no id to link to`);
      assert.ok(byline.includes(`<a href="#${fold}">`), `${m.id} ${view}: the credit line does not link to the fold`);
      // Every source the fold lists is credited in the line, by author and licence.
      const text = decode(visibleText(byline)).replace(/\(\s+/g, '(').replace(/\s+\)/g, ')');
      const inFold = html.slice(html.indexOf('<details'));
      for (const s of sourcesOf(m).filter((x) => inFold.includes(`href="${esc(x.sourceUrl)}"`))) {
        assert.ok(text.includes(s.author), `${m.id} ${view}: the credit line does not name ${s.author}`);
        const name = s.licence === null ? 'no licence stated' : s.licence.replace(/\s*[(:,].*$/, '');
        assert.ok(text.includes(`(${name})`), `${m.id} ${view}: the credit line does not give the licence of ${s.author}'s work: ${name}`);
        checked += 1;
      }
    }
  }
  assert.ok(checked > 10, `only ${checked} credits were checked`);
});
