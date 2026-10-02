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

/** Where a machine's photograph lives: its registry `photo.file` names a file in this folder. */
export const PHOTOS_DIR = path.join(SITE_ROOT, 'src', 'assets', 'photos');
const photoOnDisk = (file) => fs.existsSync(path.join(PHOTOS_DIR, file));

export function loadRegistry(file = path.join(REPO_ROOT, 'machines', 'registry.json')) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

/**
 * What is wrong with one machine's photograph, as sentences. A photograph is of
 * the original device and is credited on the machine's page, so it must say who
 * took it and where it came from. `licence` is the licence as the source states
 * it, or null when the source states none; the page then says "no licence
 * stated". Rights are not a gate (Dan, 2 October 2026), the credit is.
 */
export function photoProblems(photo, where, exists = photoOnDisk) {
  const errors = [];
  if (typeof photo !== 'object' || photo === null) return [`${where}: photo must be an object`];
  const text = (v) => typeof v === 'string' && v.trim() !== '';
  if (!text(photo.file) || !/^[\w.-]+\.(webp|jpe?g|png|avif)$/.test(photo.file)) errors.push(`${where}: photo.file must be an image file name in site/src/assets/photos/`);
  else if (!exists(photo.file)) errors.push(`${where}: photo.file ${photo.file} is not in site/src/assets/photos/`);
  if (!text(photo.author)) errors.push(`${where}: a photograph must name its author (photo.author)`);
  if (!text(photo.sourceUrl) || !/^https:\/\/\S+$/.test(photo.sourceUrl)) errors.push(`${where}: a photograph must link its source (photo.sourceUrl, https)`);
  if (!('licence' in photo) || (photo.licence !== null && !text(photo.licence))) errors.push(`${where}: photo.licence must be the licence as the source states it, or null when it states none`);
  if (photo.licenceUrl !== undefined && (!text(photo.licenceUrl) || !/^https:\/\/\S+$/.test(photo.licenceUrl))) errors.push(`${where}: photo.licenceUrl must be an https link`);
  if (photo.licenceUrl !== undefined && photo.licence === null) errors.push(`${where}: photo.licenceUrl is set but no licence is named`);
  if (!/^\d{4}(-\d{2}(-\d{2})?)?$/.test(String(photo.date ?? ''))) errors.push(`${where}: photo.date must be when it was taken, as YYYY, YYYY-MM or YYYY-MM-DD`);
  if (!text(photo.alt) || photo.alt.trim().length < 30) errors.push(`${where}: photo.alt must describe the photograph, in a sentence`);
  else if (/^illustration/i.test(photo.alt)) errors.push(`${where}: photo.alt calls the photograph an illustration`);
  if (photo.caption !== undefined && !text(photo.caption)) errors.push(`${where}: photo.caption, when given, must not be empty`);
  return errors;
}

/**
 * Everything wrong with a registry, as a list of sentences. An empty list is a
 * valid registry. `results` is optional: when given, a running machine's
 * acceptance test must be in it, and passing. `photoExists` says whether a
 * photograph's file is on disk; the tests replace it to try made-up registries.
 */
export function validateRegistry(registry, results = null, { photoExists = photoOnDisk } = {}) {
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
      // Every machine page shows a photograph of the original, always: there is no "no photo" fallback (issue 28).
      if (m.photo === undefined || m.photo === null) errors.push(`${where}: a running machine must have a photograph of the original (photo), with its author and source`);
    }
    if (m.photo !== undefined && m.photo !== null) errors.push(...photoProblems(m.photo, where, photoExists));
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
