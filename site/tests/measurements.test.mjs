import test from 'node:test';
import assert from 'node:assert/strict';
import { parseRuns, parseBrowser, describeMachine } from '../../bench/collect-measurements.mjs';
import { best } from '../src/lib/measurements.mjs';

const sample = `browser 153.0.8010.47
run 1 warmup cycles=5000002 ms=127.900 cycles_per_second=39093057 mhz=39.093
run 1 measured cycles=100000001 ms=2034.000 cycles_per_second=49164209 mhz=49.164
run 2 measured cycles=100000001 ms=1990.400 cycles_per_second=50241158 mhz=50.241`;

test('only the measured runs are read, never the warm-up', () => {
  assert.deepEqual(parseRuns(sample), [
    { cycles: 100000001, ms: 2034, mhz: 49.164 },
    { cycles: 100000001, ms: 1990.4, mhz: 50.241 },
  ]);
});

test('the browser line and the machine are described in words', () => {
  assert.equal(parseBrowser(sample), 'Chrome 153.0.8010.47');
  assert.equal(parseBrowser('nothing'), null);
  assert.equal(describeMachine({ virtualisation: 'kvm', model: 'X', cores: 8 }), 'a KVM virtual machine, X, 8 cores');
  assert.equal(describeMachine({ virtualisation: 'none', model: 'X', cores: 4 }), 'a physical machine, X, 4 cores');
});

test('the best run is the highest', () => {
  assert.equal(best({ runs: parseRuns(sample) }), 50.241);
});
