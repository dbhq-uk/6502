import test from 'node:test';
import assert from 'node:assert/strict';
import { parseTrx, summarise } from '../scripts/make-results.mjs';
import { validateResults, loadResults } from '../src/lib/results.mjs';

const trx = `<?xml version="1.0"?><TestRun><Results>
<UnitTestResult executionId="e1" testId="t1" testName="A.B.CpuTests.One" outcome="Passed" />
<UnitTestResult executionId="e2" testId="t2" testName="A.B.CpuTests.Two" outcome="Failed" />
<UnitTestResult executionId="e3" testId="t3" testName="A.B.Harte.Nmos6502Harte.Opcode(opcode: 1)" outcome="NotExecuted" />
</Results><TestDefinitions>
<UnitTest name="One" id="t1"><Execution id="e1" /><TestMethod codeBase="x" className="A.B.CpuTests" name="One" /></UnitTest>
<UnitTest name="Two" id="t2"><Execution id="e2" /><TestMethod codeBase="x" className="A.B.CpuTests" name="Two" /></UnitTest>
<UnitTest name="Opcode" id="t3"><Execution id="e3" /><TestMethod codeBase="x" className="A.B.Harte.Nmos6502Harte" name="Opcode" /></UnitTest>
</TestDefinitions></TestRun>`;

test('a TRX file becomes one result per test, keyed by its class', () => {
  assert.deepEqual(parseTrx(trx), [
    { suite: 'CpuTests', outcome: 'Passed' },
    { suite: 'CpuTests', outcome: 'Failed' },
    { suite: 'Nmos6502Harte', outcome: 'NotExecuted' },
  ]);
});

test('results are counted per suite and in total, with skipped kept apart from failed', () => {
  const s = summarise(parseTrx(trx), { commit: 'abc' });
  assert.deepEqual(s.total, { passed: 1, failed: 1, skipped: 1 });
  assert.deepEqual(s.suites.CpuTests, { passed: 1, failed: 1, skipped: 0 });
  assert.deepEqual(s.suites.Nmos6502Harte, { passed: 0, failed: 0, skipped: 1 });
  assert.equal(s.commit, 'abc');
});

test('a result with no known class is an error, not a silent gap', () => {
  assert.throws(() => parseTrx('<UnitTestResult testId="zz" outcome="Passed" />'), /no known class/);
});

test('results are publishable only if nothing failed or was skipped and Dormann ran', () => {
  const good = { total: { passed: 5, failed: 0, skipped: 0 }, suites: { DormannTests: { passed: 5, failed: 0, skipped: 0 } } };
  assert.deepEqual(validateResults(good), []);
  assert.match(validateResults({ ...good, total: { passed: 4, failed: 1, skipped: 0 } }).join(), /1 tests failed/);
  assert.match(validateResults({ ...good, total: { passed: 4, failed: 0, skipped: 1 } }).join(), /1 tests were skipped/);
  assert.match(validateResults({ total: good.total, suites: { CpuTests: { passed: 5, failed: 0, skipped: 0 } } }).join(), /Dormann tests did not run/);
  assert.match(validateResults({ total: good.total, suites: {} }).join(), /no test suites/);
});

test('the real results are publishable', () => {
  assert.deepEqual(validateResults(loadResults()), []);
});
