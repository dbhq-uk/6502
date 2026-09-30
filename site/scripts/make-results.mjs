// Turns the .trx files dotnet test writes into site/src/data/results.json:
// passed, failed and skipped, per suite (a test class) and in total, with the
// commit, run id and date. Every figure on the site is read from that file.
//
//   node scripts/make-results.mjs [trx-folder] [output-file]
//
// With no arguments it runs the whole test suite first, into ../TestResults.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');

/** Reads one .trx document into a list of { suite, outcome }. */
export function parseTrx(xml) {
  const classOf = new Map();
  for (const m of xml.matchAll(/<UnitTest\b[^>]*?\bid="([^"]+)"[\s\S]*?<TestMethod\b[^>]*?\bclassName="([^"]+)"/g)) {
    classOf.set(m[1], m[2].split('.').pop());
  }
  const results = [];
  for (const m of xml.matchAll(/<UnitTestResult\b([^>]*)>/g)) {
    const testId = /\btestId="([^"]+)"/.exec(m[1])?.[1];
    const outcome = /\boutcome="([^"]+)"/.exec(m[1])?.[1];
    const suite = classOf.get(testId);
    if (!suite || !outcome) throw new Error(`a test result has no known class or outcome: ${m[0].slice(0, 120)}`);
    results.push({ suite, outcome });
  }
  return results;
}

/** Folds parsed results into the results.json shape. */
export function summarise(results, meta) {
  const suites = {};
  const total = { passed: 0, failed: 0, skipped: 0 };
  for (const { suite, outcome } of results) {
    const key = outcome === 'Passed' ? 'passed' : outcome === 'NotExecuted' ? 'skipped' : 'failed';
    suites[suite] ??= { passed: 0, failed: 0, skipped: 0 };
    suites[suite][key]++;
    total[key]++;
  }
  return { ...meta, total, suites: Object.fromEntries(Object.entries(suites).sort(([a], [b]) => a.localeCompare(b))) };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const [folderArg, outArg] = process.argv.slice(2);
  const folder = path.resolve(folderArg ?? path.join(repo, 'TestResults'));
  if (!folderArg) {
    execFileSync('dotnet', ['test', '--configuration', 'Release', '--logger', 'trx;LogFileName=results.trx', '--results-directory', folder], { cwd: repo, stdio: 'inherit' });
  }
  const files = fs.readdirSync(folder).filter((f) => f.endsWith('.trx'));
  if (files.length === 0) throw new Error(`no .trx files in ${folder}`);
  const results = files.flatMap((f) => parseTrx(fs.readFileSync(path.join(folder, f), 'utf8')));
  const git = (...args) => { try { return execFileSync('git', args, { cwd: repo, stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim(); } catch { return null; } };
  const summary = summarise(results, {
    generated: new Date().toISOString(),
    commit: process.env.GITHUB_SHA ?? git('rev-parse', 'HEAD'),
    runId: process.env.GITHUB_RUN_ID ?? null,
  });
  const out = path.resolve(outArg ?? path.join(here, '..', 'src', 'data', 'results.json'));
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, JSON.stringify(summary, null, 2) + '\n');
  console.log(`${summary.total.passed} passed, ${summary.total.failed} failed, ${summary.total.skipped} skipped, in ${Object.keys(summary.suites).length} suites -> ${out}`);
}
