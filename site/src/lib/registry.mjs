import fs from 'node:fs';
import path from 'node:path';

/**
 * The repository root: the folder above site/. It is taken from the working
 * directory, which is site/ for every npm script and every CI step, because the
 * build bundles the pages into dist/ and a path taken from a module's own
 * location would point into the bundle.
 */
export const SITE_ROOT = process.cwd();
export const REPO_ROOT = path.resolve(SITE_ROOT, '..');

export const STATUSES = ['running', 'in-progress', 'planned', 'out-of-scope'];
export const CATEGORIES = ['single-board', 'computer', 'console', 'arcade', 'peripheral', 'modern'];
export const CORES = ['nmos', '2a03', '65c02', 'none'];

export function loadRegistry(file = path.join(REPO_ROOT, 'machines', 'registry.json')) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

/**
 * Everything wrong with a registry, as a list of sentences. An empty list is a
 * valid registry. `results` is optional: when given, a running machine's
 * acceptance test must be in it, and passing.
 */
export function validateRegistry(registry, results = null) {
  const errors = [];
  const ids = new Set();
  for (const m of registry.machines) {
    const where = `machine ${m.id ?? m.name}`;
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(m.id ?? '')) errors.push(`${where}: id must be lower-case words joined by hyphens`);
    if (ids.has(m.id)) errors.push(`${where}: duplicate id`);
    ids.add(m.id);
    if (!m.name) errors.push(`${where}: no name`);
    if (!STATUSES.includes(m.status)) errors.push(`${where}: status "${m.status}" is not one of ${STATUSES.join(', ')}`);
    if (!CATEGORIES.includes(m.category)) errors.push(`${where}: category "${m.category}" is not one of ${CATEGORIES.join(', ')}`);
    if (!CORES.includes(m.core)) errors.push(`${where}: core "${m.core}" is not one of ${CORES.join(', ')}`);
    if ((m.core === 'none') !== (m.status === 'out-of-scope')) errors.push(`${where}: a machine is out of scope exactly when no core variant runs it`);
    if (m.status === 'running') {
      if (!m.acceptance) errors.push(`${where}: a running machine must name its acceptance test`);
      else if (results) {
        const suite = results.suites[m.acceptance];
        if (!suite) errors.push(`${where}: acceptance test "${m.acceptance}" is not in the test results`);
        else if (suite.passed < 1 || suite.failed > 0 || suite.skipped > 0) errors.push(`${where}: acceptance test "${m.acceptance}" must pass with nothing failed or skipped`);
      }
    }
  }
  for (const c of registry.chips) {
    if (!c.name) errors.push('a chip has no name');
    if (!CORES.includes(c.core)) errors.push(`chip ${c.name}: core "${c.core}" is not one of ${CORES.join(', ')}`);
  }
  return errors;
}

export function counts(registry) {
  const by = (status) => registry.machines.filter((m) => m.status === status).length;
  const outOfScope = by('out-of-scope');
  return {
    running: by('running'),
    inProgress: by('in-progress'),
    planned: by('planned'),
    outOfScope,
    inScope: registry.machines.length - outOfScope,
  };
}
