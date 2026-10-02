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

/** Where a machine's photographs live: each `photos[].file` in the registry names a file in this folder. */
export const PHOTOS_DIR = path.join(SITE_ROOT, 'src', 'assets', 'photos');
const photoOnDisk = (file) => fs.existsSync(path.join(PHOTOS_DIR, file));

export function loadRegistry(file = path.join(REPO_ROOT, 'machines', 'registry.json')) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

const text = (v) => typeof v === 'string' && v.trim() !== '';
// A source may be http: one of the KIM-1's (Hans Otten's site) serves no https.
const webLink = (v) => text(v) && /^https?:\/\/\S+$/.test(v);
const day = (v) => /^\d{4}-\d{2}-\d{2}$/.test(String(v ?? ''));

/** The credit every photograph and drawing carries: who, where from, the licence as stated, when fetched, and what it was used for. */
function creditProblems(item, where) {
  const errors = [];
  if (!text(item.author)) errors.push(`${where}: must name its author (author)`);
  if (!webLink(item.sourceUrl)) errors.push(`${where}: must link its source (sourceUrl, an http or https link)`);
  if (!('licence' in item) || (item.licence !== null && !text(item.licence))) errors.push(`${where}: licence must be the licence as the source states it, or null when it states none`);
  if (item.licenceUrl !== undefined && (!text(item.licenceUrl) || !/^https:\/\/\S+$/.test(item.licenceUrl))) errors.push(`${where}: licenceUrl must be an https link`);
  if (item.licenceUrl !== undefined && item.licence === null) errors.push(`${where}: licenceUrl is set but no licence is named`);
  if (!day(item.fetched)) errors.push(`${where}: fetched must be the day it was downloaded, as YYYY-MM-DD`);
  if (!text(item.used) || item.used.trim().length < 12) errors.push(`${where}: used must say what was traced or measured from it`);
  return errors;
}

/**
 * What is wrong with one of a machine's photographs, as sentences. A
 * photograph is of the original device and is credited on the machine's page,
 * so it must say who took it, where it came from, when it was fetched and what
 * the model took from it. `licence` is the licence as the source states it, or
 * null when the source states none; the page then says "no licence stated".
 * Rights are not a gate (Dan, 2 October 2026), the credit is.
 */
export function photoProblems(photo, where, exists = photoOnDisk) {
  if (typeof photo !== 'object' || photo === null) return [`${where}: a photograph must be an object`];
  const errors = [];
  if (!text(photo.file) || !/^[\w.-]+\.(webp|jpe?g|png|avif)$/.test(photo.file)) errors.push(`${where}: file must be an image file name in site/src/assets/photos/`);
  else if (!exists(photo.file)) errors.push(`${where}: file ${photo.file} is not in site/src/assets/photos/`);
  errors.push(...creditProblems(photo, where));
  if (!/^\d{4}(-\d{2}(-\d{2})?)?$/.test(String(photo.date ?? ''))) errors.push(`${where}: date must be when it was taken, as YYYY, YYYY-MM or YYYY-MM-DD`);
  if (!text(photo.alt) || photo.alt.trim().length < 30) errors.push(`${where}: alt must describe the photograph, in a sentence`);
  else if (/^illustration/i.test(photo.alt)) errors.push(`${where}: alt calls the photograph an illustration`);
  if (photo.caption !== undefined && !text(photo.caption)) errors.push(`${where}: caption, when given, must not be empty`);
  return errors;
}

/** What is wrong with one of the drawings a machine's model was measured from (a replica's layout, a schematic). */
export function drawingProblems(drawing, where) {
  if (typeof drawing !== 'object' || drawing === null) return [`${where}: a drawing must be an object`];
  return [...(text(drawing.title) ? [] : [`${where}: must say what it is (title)`]), ...creditProblems(drawing, where)];
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
      if (!Array.isArray(m.photos) || m.photos.length === 0) errors.push(`${where}: a running machine must have at least one photograph of the original (photos), with its author and source`);
    }
    if (m.photo !== undefined) errors.push(`${where}: photo is now photos, a list with the main photograph first`);
    if (m.photos !== undefined && !Array.isArray(m.photos)) errors.push(`${where}: photos must be a list`);
    if (Array.isArray(m.photos)) {
      m.photos.forEach((p, i) => errors.push(...photoProblems(p, `${where}: photos[${i}]`, photoExists)));
      const files = m.photos.map((p) => p?.file);
      if (new Set(files).size !== files.length) errors.push(`${where}: a photograph is listed twice`);
    }
    if (m.drawings !== undefined && !Array.isArray(m.drawings)) errors.push(`${where}: drawings must be a list`);
    if (Array.isArray(m.drawings)) m.drawings.forEach((d, i) => errors.push(...drawingProblems(d, `${where}: drawings[${i}]`)));
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
