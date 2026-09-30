import fs from 'node:fs';
import path from 'node:path';

// The working directory is site/: see registry.mjs.
export const RESULTS_FILE = path.join(process.cwd(), 'src', 'data', 'results.json');

export function loadResults(file = RESULTS_FILE) {
  if (!fs.existsSync(file)) {
    throw new Error(
      `${file} is missing. It is generated from a test run and never committed: ` +
        'run `npm run results` in site/ (it runs the whole test suite), or download the results artifact from CI.',
    );
  }
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

/**
 * What must be true of a test run before its figures may be published: it ran
 * something, nothing failed, nothing was skipped, and the Dormann tests, which
 * are skipped rather than failed where they cannot run, really ran.
 */
export function validateResults(results) {
  const errors = [];
  if (!results.suites || Object.keys(results.suites).length === 0) errors.push('no test suites in the results');
  if (results.total.failed > 0) errors.push(`${results.total.failed} tests failed`);
  if (results.total.skipped > 0) errors.push(`${results.total.skipped} tests were skipped`);
  const dormann = results.suites?.DormannTests;
  if (!dormann || dormann.passed < 1) errors.push('the Dormann tests did not run');
  return errors;
}
