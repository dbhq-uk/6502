import test from 'node:test';
import assert from 'node:assert/strict';
import { page, visibleText } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';
import { loadResults } from '../src/lib/results.mjs';
import { loadMeasurements, best } from '../src/lib/measurements.mjs';
import { figures, fmt, fmt1, REFERENCE_MHZ, targetVerdict } from '../src/lib/figures.mjs';

const registry = loadRegistry();
const results = loadResults();
const measurements = loadMeasurements();
const f = figures({ registry, results, measurements });

// Everything a reader can read on a page, including the terminal card and the
// notes, but not scripts or styles. Nothing on the page is exempt from the
// number check except the two rendered documents below.
const readable = (html) =>
  html
    .replace(/<(script|style)\b[\s\S]*?<\/\1>/g, ' ')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n)))
    .replace(/\s+/g, ' ')
    .trim();

const NUMBER = /(?<![A-Za-z\d-])\d(?:[\d,]*\d)?(?:\.\d+)?(?![A-Za-z\d])/g;
const numbersIn = (text) => [...text.matchAll(NUMBER)].map((m) => m[0]);
const dateParts = (d) => [String(d.getUTCDate()), String(d.getUTCFullYear())];

// The figures a page may show, each computed from the data. Two literals name
// things: the chip ("6502") and the reference machine's clock, which is a
// constant of the design. Nothing else is allowed, so a typed number fails.
function allowedNumbers() {
  const modes = Object.values(measurements.modes);
  const generated = new Date(results.generated);
  return new Set([
    '6502',
    String(REFERENCE_MHZ),
    fmt(f.machinesImplemented), fmt(f.machinesInScope), fmt(f.machinesInProgress), fmt(f.variants), fmt(f.testsPassing), fmt(f.harteTests),
    fmt(f.dormannBuilds), fmt(f.interruptRuns), fmt(f.speedTarget), fmt1(f.speedAot), fmt1(f.speedInterpreter), fmt1(f.speedNative),
    fmt(results.total.passed), fmt(results.total.failed), fmt(results.total.skipped),
    ...Object.values(results.suites).flatMap((s) => [fmt(s.passed), fmt(s.failed), fmt(s.skipped)]),
    ...modes.flatMap((m) => [...m.runs.map((r) => fmt1(r.mhz)), fmt1(best(m)), fmt1(best(m) / REFERENCE_MHZ), fmt(m.runs.length)]),
    fmt(registry.machines.length),
    // a machine's year and any numbers in its CPU field, shown on the home page's first three machines
    ...registry.machines.flatMap((m) => [String(m.year), ...numbersIn(m.cpu ?? ''), ...numbersIn(m.notes ?? '')]),
    // the description of the measurement, written in the data file
    ...numbersIn(measurements.machine.description), ...numbersIn(measurements.machine.browser), ...numbersIn(measurements.workload),
    // dates and the commit are generated text
    ...dateParts(new Date(measurements.collected)), ...dateParts(generated),
    ...numbersIn(results.commit ?? ''),
  ]);
}

// The status page ends with a rendered document, which is prose about
// cycles and opcodes, not a figure about the project.
const withoutDocument = (url, text) => (url === '/status/' ? text.replace(/Where the core knowingly differs from a reference[\s\S]*$/, ' ') : text);

test('every number on the home and status pages is a generated figure or a named literal', () => {
  const allowed = allowedNumbers();
  for (const url of ['/', '/status/']) {
    let html = page(url).html;
    // journal entries on the home page carry their own dates and summaries; the entry dates are generated from the entries
    html = html.replace(/<time\b[\s\S]*?<\/time>/g, ' ');
    const text = withoutDocument(url, readable(html));
    const stray = numbersIn(text).filter((n) => !allowed.has(n));
    assert.deepEqual(stray, [], `${url} shows numbers that are not generated figures`);
  }
});

const cardValues = (html) => [...html.matchAll(/<div class="card">\s*<b>([^<]*)<\/b>/g)].map((m) => m[1].trim());

test('each headline figure is exactly the value computed from the data', () => {
  assert.deepEqual(cardValues(page('/').html), [
    `${fmt(f.machinesImplemented)} of ${fmt(f.machinesInScope)}`,
    fmt(f.variants),
    fmt(f.testsPassing),
    `${fmt1(f.speedAot)} times`,
  ]);
  assert.deepEqual(cardValues(page('/status/').html), [
    `${fmt(f.machinesImplemented)} of ${fmt(f.machinesInScope)}`,
    fmt(f.variants),
    fmt(f.testsPassing),
    fmt(f.harteTests),
    fmt(f.dormannBuilds),
    fmt(f.interruptRuns),
  ]);
});

test('every row of the speed table is exactly the value computed from the measurements', () => {
  const html = page('/status/').html;
  for (const mode of Object.values(measurements.modes)) {
    const row = [...html.matchAll(/<tr>([\s\S]*?)<\/tr>/g)].map((m) => [...m[1].matchAll(/<td[^>]*>([\s\S]*?)<\/td>/g)].map((c) => c[1].replace(/<[^>]+>/g, '').trim())).find((cells) => cells[0] === mode.label);
    assert.ok(row, `no speed row for ${mode.label}`);
    assert.equal(row[1], fmt1(best(mode)), `${mode.label}: best MHz`);
    assert.equal(row[2], fmt1(best(mode) / REFERENCE_MHZ), `${mode.label}: multiple of a ${REFERENCE_MHZ} MHz machine`);
  }
});

// The design's target is 25 times. It is written once on each page, inside the
// sentence that says whether it is met, and nowhere else.
test('the design target appears once per page, inside a sentence that says whether it is met', () => {
  for (const url of ['/', '/status/']) {
    const text = withoutDocument(url, readable(page(url).html.replace(/<time\b[\s\S]*?<\/time>/g, ' ')));
    const at = [...text.matchAll(/(?<![A-Za-z\d.,-])25(?![A-Za-z\d,]|\.\d)/g)];
    assert.equal(at.length, 1, `${url} should show ${fmt(f.speedTarget)} exactly once, found ${at.length}`);
    const around = text.slice(Math.max(0, at[0].index - 120), at[0].index + 260);
    assert.match(around, /\b(?:not yet met|met)\b/, `${url}: the target is not next to a verdict`);
  }
});

test('the pages say "not yet met" when the browser build is below the target, and never claim the target was reached', () => {
  const verdictAot = targetVerdict(f.speedAot);
  for (const url of ['/', '/status/']) {
    const text = withoutDocument(url, readable(page(url).html));
    assert.ok(text.includes(verdictAot), `${url} does not say the ahead-of-time build is ${verdictAot}`);
    if (f.speedAot < f.speedTarget) assert.ok(text.includes('not yet met'), `${url} does not say "not yet met"`);
    if (f.speedAot < f.speedTarget) {
      for (const m of text.matchAll(/target/gi)) {
        const near = text.slice(Math.max(0, m.index - 100), m.index + 160);
        assert.doesNotMatch(near, /\b(reached|reaches|exceeds|exceeded|achieved|achieves|hit|surpass\w*)\b/i, `${url} claims the target was reached: "${near}"`);
      }
    }
  }
  // The status page names the verdict next to both figures it applies to.
  const status = readable(page('/status/').html);
  assert.ok(status.includes(`${fmt1(f.speedNative)} times: ${targetVerdict(f.speedNative)}`));
  assert.ok(status.includes(`${fmt1(f.speedAot)} times: ${verdictAot}`));
});

test('the status page tells the reader which commit its figures came from', () => {
  if (results.commit) assert.ok(visibleText(page('/status/').html).includes(results.commit.slice(0, 7)));
  assert.match(visibleText(page('/status/').html), /Test run on commit/);
});

test('the home page shows the count of machines implemented and where the results came from', () => {
  const text = visibleText(page('/').html);
  assert.ok(text.includes(`${fmt(f.machinesImplemented)} of ${fmt(f.machinesInScope)}`), 'the machines implemented figure is missing');
  assert.match(text, /Test run on commit/);
});
