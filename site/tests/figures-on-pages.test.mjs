import test from 'node:test';
import assert from 'node:assert/strict';
import { page, visibleText } from './helpers.mjs';
import { loadRegistry } from '../src/lib/registry.mjs';
import { loadResults } from '../src/lib/results.mjs';
import { loadMeasurements } from '../src/lib/measurements.mjs';
import { figures, fmt, fmt1 } from '../src/lib/figures.mjs';

// The site's claim: no figure on the home or status page is typed by hand. The
// numbers a reader sees there are the generated figures, plus a short list of
// literals that name things (a chip, the reference machine's clock).
test('every number on the home and status pages is a generated figure or a named literal', () => {
  const f = figures({ registry: loadRegistry(), results: loadResults(), measurements: loadMeasurements() });
  const results = loadResults();
  const measurements = loadMeasurements();
  const allowed = new Set([
    // literals that name things
    '6502', '2',
    // the counts and speeds the pages format
    fmt(f.machinesImplemented), fmt(f.machinesInScope), fmt(f.machinesInProgress), fmt(f.variants), fmt(f.testsPassing), fmt(f.harteTests),
    fmt(f.dormannBuilds), fmt(f.interruptRuns), fmt(f.speedTarget), fmt1(f.speedAot), fmt1(f.speedInterpreter), fmt1(f.speedNative),
    fmt(results.total.passed), fmt(results.total.failed), fmt(results.total.skipped),
    ...Object.values(results.suites).flatMap((s) => [fmt(s.passed), fmt(s.failed), fmt(s.skipped)]),
    ...Object.values(measurements.modes).flatMap((m) => m.runs.map((r) => fmt1(r.mhz))),
    ...Object.values(measurements.modes).flatMap((m) => [fmt1(Math.max(...m.runs.map((r) => r.mhz))), fmt1(Math.max(...m.runs.map((r) => r.mhz)) / 2)]),
    fmt(loadRegistry().machines.length),
    // facts about a machine, read from the registry: its year and any numbers in its CPU
    ...loadRegistry().machines.flatMap((m) => [String(m.year), ...(m.cpu.match(/(?<![A-Za-z\d-])\d[\d.]*(?![A-Za-z\d])/g) ?? [])]),
  ]);
  for (const url of ['/', '/status/']) {
    let html = page(url).html;
    // dates, the commit hash, and the machine and run description are generated text, not figures
    html = html.replace(/<time\b[\s\S]*?<\/time>/g, ' ').replace(/<p class="note">[\s\S]*?<\/p>/g, ' ');
    const status = url === '/status/' ? html : html;
    let text = visibleText(status);
    if (url === '/status/') text = text.replace(/Speed How many[\s\S]*?One machine, one day: the figures are a record, not a promise\./, ' ').replace(/Where the core knowingly differs[\s\S]*$/, ' ');
    const numbers = [...text.matchAll(/(?<![A-Za-z\d-])\d(?:[\d,]*\d)?(?:\.\d+)?(?![A-Za-z\d])/g)].map((m) => m[0]);
    const stray = numbers.filter((n) => !allowed.has(n));
    assert.ok(stray.length === 0, `${url} shows numbers that are not generated figures: ${stray.join(', ')}`);
  }
});

test('the status page tells the reader which commit its figures came from', () => {
  const results = loadResults();
  if (results.commit) assert.ok(visibleText(page('/status/').html).includes(results.commit.slice(0, 7)));
  assert.match(visibleText(page('/status/').html), /Test run on commit/);
});

test('the home page shows the count of machines implemented and where the results came from', () => {
  const f = figures({ registry: loadRegistry(), results: loadResults(), measurements: loadMeasurements() });
  const text = visibleText(page('/').html);
  assert.ok(text.includes(`${fmt(f.machinesImplemented)} of ${fmt(f.machinesInScope)}`), 'the machines implemented figure is missing');
  assert.match(text, /Test run on commit/);
});
