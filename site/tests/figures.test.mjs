import test from 'node:test';
import assert from 'node:assert/strict';
import { figures, fmt, fmt1, targetVerdict, SPEED_TARGET } from '../src/lib/figures.mjs';
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
  assert.equal(f.dormannTestsPassed, 4);
  assert.equal(f.transistorModelTestsPassed, 5);
  assert.equal(f.speedAot, 25);
});

test('numbers are formatted the British way', () => {
  assert.equal(fmt(12780), '12,780');
  assert.equal(fmt1(25.48), '25.5');
});

test('the real figures can be computed', () => {
  const f = figures({ registry: loadRegistry(), results: loadResults(), measurements: loadMeasurements() });
  for (const key of ['testsPassing', 'variants', 'harteTests', 'dormannTestsPassed', 'transistorModelTestsPassed', 'speedAot']) {
    assert.ok(f[key] > 0, `${key} must be positive, got ${f[key]}`);
  }
  assert.equal(typeof f.machinesInProgress, 'number');
  assert.ok(Number.isInteger(f.machinesInProgress) && f.machinesInProgress >= 0);
});

test('a missing suite stops the build and names the suite, rather than publishing 0', () => {
  const registry = { machines: [], chips: [] };
  const measurements = { modes: { native: { runs: [{ mhz: 1 }] }, interpreter: { runs: [{ mhz: 1 }] }, aot: { runs: [{ mhz: 1 }] } } };
  const suite = { passed: 1, failed: 0, skipped: 0 };
  const all = { XHarte: suite, DormannTests: suite, TransistorModelTests: suite };
  const compute = (suites) => figures({ registry, results: { total: { passed: 3 }, suites }, measurements });
  assert.doesNotThrow(() => compute(all));
  const { DormannTests, ...withoutDormann } = all;
  assert.throws(() => compute(withoutDormann), /"DormannTests"[\s\S]*figures\.mjs/);
  const { TransistorModelTests, ...withoutModel } = all;
  assert.throws(() => compute(withoutModel), /"TransistorModelTests"/);
  // A suite that is present but has run nothing is a real 0 and is still published.
  assert.equal(compute({ ...all, DormannTests: { passed: 0, failed: 0, skipped: 0 } }).dormannTestsPassed, 0);
});

test('the real registry is valid against the real test results', () => {
  assert.deepEqual(validateRegistry(loadRegistry(), loadResults()), []);
});

test('the target verdict says met only at or above the target, and never rounds up', () => {
  assert.equal(targetVerdict(SPEED_TARGET), 'met');
  assert.equal(targetVerdict(SPEED_TARGET + 0.1), 'met');
  assert.equal(targetVerdict(SPEED_TARGET - 0.001), 'not yet met');
  assert.equal(fmt1(24.425), '24.4');
});
