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

/**
 * The views a machine's model can be: `board` for a machine with no case (the
 * board is the whole machine), `outside` and `inside` for a machine with one.
 */
export const VIEWS = ['board', 'outside', 'inside'];
/** Where a model's browser module is (`<module>.js`) and where its results file is (`<module>-model.json`). */
export const MODELS_DIR = path.join(SITE_ROOT, 'src', 'models');
export const DATA_DIR = path.join(SITE_ROOT, 'src', 'data');
/** What is on disk for one model: its module, whether that exports mount(root), and its results file. */
function modelOnDisk(module) {
  const file = path.join(MODELS_DIR, `${module}.js`);
  const there = fs.existsSync(file);
  return {
    module: there,
    exportsMount: there && /export async function mount\(root\)/.test(fs.readFileSync(file, 'utf8')),
    results: fs.existsSync(path.join(DATA_DIR, `${module}-model.json`)),
  };
}

const REGISTRY_FILE = path.join(REPO_ROOT, 'machines', 'registry.json');

/** The registry: machines/registry.json, or another file in its shape (the tests use made-up ones). */
export function loadRegistry(file = REGISTRY_FILE) {
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
 * What is wrong with one of a model's references: a source it was measured
 * from that is not committed (a flatbed scan, or a photograph whose source
 * states no licence). It is credited like a drawing, and names the SHA-256 of
 * the original that was measured, so the credit says which file it was.
 */
export function referenceProblems(reference, where) {
  if (typeof reference !== 'object' || reference === null) return [`${where}: a reference must be an object`];
  const errors = text(reference.title) ? [] : [`${where}: must say what it is (title)`];
  errors.push(...creditProblems(reference, where));
  if (!/^[0-9a-f]{64}$/.test(String(reference.sha256 ?? ''))) errors.push(`${where}: sha256 must be the SHA-256 of the original, 64 hex digits in lower case`);
  return errors;
}

/**
 * What is wrong with a machine's `case` and `models`, as sentences (design,
 * "The registry's models field"). Both are optional: a running machine is never
 * required to claim a model (Dan, 2 October 2026), but a model it claims must
 * be built, and a machine with a case that claims a model claims both the
 * outside and the inside. `modelFiles(module)` says what is on disk for a
 * module; the tests replace it to try made-up registries. That a module is
 * claimed once in the whole registry is checked by validateRegistry, which sees
 * every machine.
 */
export function modelProblems(machine, where, { modelFiles = modelOnDisk } = {}) {
  const errors = [];
  const hasCase = machine.case;
  if (hasCase !== undefined && typeof hasCase !== 'boolean') errors.push(`${where}: case, when given, must be true or false`);
  if (machine.models === undefined) return errors;
  if (!Array.isArray(machine.models) || machine.models.length === 0) return [...errors, `${where}: models, when given, must be a non-empty list of { view, module }`];
  const views = [];
  machine.models.forEach((model, i) => {
    const at = `${where}: models[${i}]`;
    if (typeof model !== 'object' || model === null) { errors.push(`${at}: a model must be an object, { view, module }`); return; }
    const { view, module } = model;
    if (!VIEWS.includes(view)) errors.push(`${at}: view "${view}" is not one of ${VIEWS.join(', ')}`);
    else if (views.includes(view)) errors.push(`${where}: the view ${view} is claimed twice`);
    views.push(view);
    if (hasCase === false && (view === 'outside' || view === 'inside')) errors.push(`${at}: the machine has no case, so it cannot claim the ${view} view (its model is the board)`);
    if (hasCase === true && view === 'board') errors.push(`${at}: the machine has a case, so it claims outside and inside, not board`);
    // Named for its machine, so the name alone says whose it is. A name that is not is never looked for on disk.
    const named = typeof module === 'string' && /^[a-z0-9]+(-[a-z0-9]+)*$/.test(module) && (module === machine.id || module.startsWith(`${machine.id}-`));
    if (!named) { errors.push(`${at}: module "${module}" must be the machine's id, ${machine.id}, or start with ${machine.id}- (lower-case words joined by hyphens)`); return; }
    const files = modelFiles(module);
    if (!files.module) errors.push(`${at}: site/src/models/${module}.js is missing`);
    else if (!files.exportsMount) errors.push(`${at}: site/src/models/${module}.js does not export mount(root)`);
    if (!files.results) errors.push(`${at}: site/src/data/${module}-model.json is missing: a model's results file says how it was made`);
  });
  if (hasCase === true && !(views.includes('outside') && views.includes('inside'))) errors.push(`${where}: the machine has a case and claims a model, so it must claim both outside and inside`);
  return errors;
}

/**
 * Everything wrong with a registry, as a list of sentences. An empty list is a
 * valid registry. `results` is optional: when given, a running machine's
 * acceptance test must be in it, and passing. `photoExists` says whether a
 * photograph's file is on disk, and `modelFiles` what is on disk for a model's
 * module; the tests replace them to try made-up registries.
 */
export function validateRegistry(registry, results = null, { photoExists = photoOnDisk, modelFiles = modelOnDisk } = {}) {
  const errors = [];
  const ids = new Set();
  const modules = new Set();
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
    if (m.references !== undefined && !Array.isArray(m.references)) errors.push(`${where}: references must be a list`);
    if (Array.isArray(m.references)) m.references.forEach((r, i) => errors.push(...referenceProblems(r, `${where}: references[${i}]`)));
    errors.push(...modelProblems(m, where, { modelFiles }));
    for (const model of Array.isArray(m.models) ? m.models : []) {
      if (typeof model?.module !== 'string') continue;
      if (modules.has(model.module)) errors.push(`${where}: module ${model.module} is claimed twice in the registry`);
      modules.add(model.module);
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
