import test from 'node:test';
import assert from 'node:assert/strict';
import { figures, fmt, fmt1 } from '../src/lib/figures.mjs';
import { loadRegistry, validateRegistry } from '../src/lib/registry.mjs';
import { loadResults } from '../src/lib/results.mjs';
import { loadMeasurements } from '../src/lib/measurements.mjs';

test('figures are computed from the registry, the results and the measurements', () => {
  const registry = { machines: [{ status: 'running' }, { status: 'planned' }, { status: 'out-of-scope' }], chips: [] };
  const results = { total: { passed: 30, failed: 0, skipped: 0 }, suites: { AHarte: { passed: 10 }, BHarte: { passed: 12 }, HarteRunnerTests: { passed: 3 }, DormannTests: { passed: 4 }, TransistorModelTests: { passed: 5 } } };
  const run = (mhz) => ({ runs: [{ mhz: mhz / 2 }, { mhz }] });
  const measurements = { modes: { native: run(100), interpreter: run(4), aot: run(50) } };
  const f = figures({ registry, results, measurements });
  assert.equal(f.machinesImplemented, 1);
  assert.equal(f.machinesInScope, 2);
  assert.equal(f.variants, 2);
  assert.equal(f.harteTests, 22);
  assert.equal(f.dormannBuilds, 4);
  assert.equal(f.interruptRuns, 5);
  assert.equal(f.speedAot, 25);
});

test('numbers are formatted the British way', () => {
  assert.equal(fmt(12780), '12,780');
  assert.equal(fmt1(25.48), '25.5');
});

test('the real figures can be computed', () => {
  const f = figures({ registry: loadRegistry(), results: loadResults(), measurements: loadMeasurements() });
  assert.ok(f.testsPassing > 0 && f.speedAot > 0);
});

test('the real registry is valid against the real test results', () => {
  assert.deepEqual(validateRegistry(loadRegistry(), loadResults()), []);
});
