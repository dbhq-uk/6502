import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { parseRuns, parseBrowser, describeMachine, detectVirtualisation } from '../../bench/collect-measurements.mjs';
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

test('a machine is called physical only when the detection tool ran and said none', () => {
  assert.equal(describeMachine({ virtualisation: null, model: 'X', cores: 4 }), 'a machine of unknown type, X, 4 cores');
  assert.equal(describeMachine({ virtualisation: undefined, model: 'X', cores: 4 }), 'a machine of unknown type, X, 4 cores');
  assert.equal(describeMachine({ virtualisation: '', model: 'X', cores: 4 }), 'a machine of unknown type, X, 4 cores');
});

// These run real child processes through execFileSync, the runner the collector
// uses, so the error shapes are the real ones and not guesses.
const real = (script) => () => execFileSync(process.execPath, ['-e', script], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });

test('detection: a virtual machine, a physical machine, a missing tool and a failing tool are four different answers', () => {
  assert.equal(detectVirtualisation(real('console.log("kvm")')), 'kvm');
  // systemd-detect-virt prints "none" and exits 1 on a physical machine.
  assert.equal(detectVirtualisation(real('console.log("none"); process.exit(1)')), 'none');
  // A tool that is not installed says nothing at all.
  const missing = () => execFileSync('this-tool-does-not-exist-6502', [], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
  assert.equal(detectVirtualisation(missing), null);
  // A tool that failed without saying "none" is not a physical machine either.
  assert.equal(detectVirtualisation(real('process.exit(1)')), null);
  assert.equal(detectVirtualisation(real('process.exit(127)')), null);
});

test('detection and description together: a missing tool never publishes "physical"', () => {
  const missing = () => execFileSync('this-tool-does-not-exist-6502', [], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
  const said = (v) => describeMachine({ virtualisation: detectVirtualisation(v), model: 'X', cores: 2 });
  assert.equal(said(missing), 'a machine of unknown type, X, 2 cores');
  assert.equal(said(real('console.log("none"); process.exit(1)')), 'a physical machine, X, 2 cores');
});
