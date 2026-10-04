import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { REPO_ROOT } from './registry.mjs';
import { readRom } from './machines.mjs';

// The BBC Micro's preset discs, read at build time by the page and by
// scripts/build-machines.mjs, which publishes their images.
//
// machines/bbc-micro/discs/ holds the discs the page offers, each in a folder
// of its own with its licence, a README saying where it came from and, where
// its licence asks for it, its source. manifest.json lists them in the order
// the page shows them. They are other people's software under their own
// licences (AGENTS.md rule 4): the manifest carries each one's licence as its
// author states it, and the page credits every one.

/** The folder the build publishes the disc images to, under the machine's own: /machines/bbc-micro/discs/. */
export const DISCS_FOLDER = 'discs';

/** Where the discs live in the repository. */
export const DISCS_DIR = path.join('machines', 'bbc-micro', 'discs');

/** The licences a bundled disc may carry, as SPDX ids: free licences only, none that forbids commercial use. */
export const DISC_LICENCES = ['MIT', 'BSD-2-Clause', 'FSFAP', 'GPL-3.0-only', 'GPL-3.0-or-later', 'LGPL-3.0-only'];

/** The licences whose terms ask for the source to go with the disc. */
export const COPYLEFT = ['GPL-3.0-only', 'GPL-3.0-or-later', 'LGPL-3.0-only'];

/** The kinds of disc, in the order the page groups them, each with its group's name. */
export const DISC_KINDS = { game: 'Games', adventure: 'Games', basic: 'Games', puzzle: 'Puzzles', demo: 'Demos', tool: 'Tools' };

/** The SPDX ids in a licence, which may be one id or several joined by AND. */
export const licenceIds = (licence) => String(licence ?? '').split(' AND ');

/** True when a disc's licence asks for its source to go with it. */
export const needsSource = (disc) => licenceIds(disc.licence).some((id) => COPYLEFT.includes(id));

const REPO_URL = 'https://github.com/dbhq-uk/6502';
const text = (v) => typeof v === 'string' && v.trim() !== '';
const link = (v) => text(v) && /^https?:\/\/\S+$/.test(v);

/** The disc's image in the repository, relative to the root. */
export const discPath = (disc) => path.posix.join('machines', 'bbc-micro', 'discs', disc.slug, `${disc.slug}.ssd`);

/** Where the page fetches the disc's image: beside the machine's files, on this site. */
export const discImageUrl = (base, disc) => `${base}${DISCS_FOLDER}/${disc.slug}.ssd`;

/** The disc's licence file in the repository, on GitHub. */
export const discLicenceUrl = (disc) => `${REPO_URL}/blob/main/machines/bbc-micro/discs/${disc.slug}/LICENSE`;

/** The disc's folder in the repository, on GitHub: its README, licence and source. */
export const discFolderUrl = (disc) => `${REPO_URL}/tree/main/machines/bbc-micro/discs/${disc.slug}`;

/** The disc's source in the repository, on GitHub. */
export const discSourceUrl = (disc) => `${discFolderUrl(disc)}/source`;

/** Tracks a side, as DiscImage.FromBytes decides it: 40 unless the image is longer, or its catalogue says 800 sectors. */
export function discTracks(bytes) {
  const tracks = bytes.length <= 40 * 2560 ? 40 : bytes.length <= 80 * 2560 ? 80 : null;
  if (tracks === 40 && bytes.length >= 264 && (((bytes[262] & 3) << 8) | bytes[263]) === 800) return 80;
  return tracks;
}

/** The manifest, machines/bbc-micro/discs/manifest.json, or another file in its shape (the tests use made-up ones). */
export function loadDiscs(root = REPO_ROOT) {
  return JSON.parse(fs.readFileSync(path.join(root, DISCS_DIR, 'manifest.json'), 'utf8')).discs;
}

/**
 * Everything wrong with a manifest's discs, as sentences: an empty list is a
 * good manifest. Each disc must say what it is, whose it is and under what
 * licence, verbatim, with links to both; its file must be in its folder with
 * the size and SHA-256 the manifest gives; its licence must be one of
 * DISC_LICENCES; its folder must hold its licence text and a README, and its
 * source when its licence asks for it; and exactly one disc is the default.
 */
export function discProblems(discs, root = REPO_ROOT) {
  const errors = [];
  if (!Array.isArray(discs) || discs.length === 0) return ['the manifest lists no discs'];
  const slugs = new Set();
  for (const d of discs) {
    const where = `disc ${d.slug ?? d.title}`;
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(d.slug ?? '')) errors.push(`${where}: slug must be lower-case words joined by hyphens`);
    if (slugs.has(d.slug)) errors.push(`${where}: listed twice`);
    slugs.add(d.slug);
    for (const field of ['title', 'author', 'year', 'description', 'controls', 'licenceStatement', 'imageFrom']) if (!text(d[field])) errors.push(`${where}: ${field} must be given`);
    if (!(d.kind in DISC_KINDS)) errors.push(`${where}: kind "${d.kind}" is not one of ${Object.keys(DISC_KINDS).join(', ')}`);
    if (!link(d.sourceUrl)) errors.push(`${where}: sourceUrl must link the author's page`);
    if (!link(d.licenceUrl)) errors.push(`${where}: licenceUrl must link where the licence was read`);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(d.fetched ?? '')) errors.push(`${where}: fetched must be the day it was downloaded, as YYYY-MM-DD`);
    if (!licenceIds(d.licence).every((id) => DISC_LICENCES.includes(id))) errors.push(`${where}: licence "${d.licence}" is not one a bundled disc may carry (${DISC_LICENCES.join(', ')})`);
    if (d.run !== 'autoboot') errors.push(`${where}: run must be "autoboot", which is how the page starts it`);
    if (d.sides !== 1) errors.push(`${where}: only single-sided discs (.ssd) are offered`);
    if (d.note !== undefined && !text(d.note)) errors.push(`${where}: note, when given, must not be empty`);
    if (typeof d.default !== 'boolean') errors.push(`${where}: default must be true or false`);
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(d.slug ?? '')) continue;
    const folder = path.join(root, DISCS_DIR, d.slug ?? '');
    const image = path.join(folder, `${d.slug}.ssd`);
    if (!fs.existsSync(image)) {
      errors.push(`${where}: its image is not at ${path.relative(root, image)}`);
      continue;
    }
    const bytes = fs.readFileSync(image);
    if (bytes.length !== d.bytes) errors.push(`${where}: the image is ${bytes.length} bytes, the manifest says ${d.bytes}`);
    if (crypto.createHash('sha256').update(bytes).digest('hex') !== d.sha256) errors.push(`${where}: the image does not match the manifest's SHA-256`);
    if (bytes.length % 256 !== 0) errors.push(`${where}: the image is not a whole number of 256-byte sectors`);
    if (discTracks(bytes) !== d.tracks) errors.push(`${where}: the image makes a ${discTracks(bytes)}-track disc, the manifest says ${d.tracks}`);
    for (const file of ['LICENSE', 'README.md']) if (!fs.existsSync(path.join(folder, file))) errors.push(`${where}: no ${file} in its folder`);
    const source = path.join(folder, 'source');
    if (needsSource(d) && !(fs.existsSync(source) && fs.readdirSync(source).length > 0)) errors.push(`${where}: its licence asks for the source, and its folder has no source/`);
  }
  if (discs.filter((d) => d.default === true).length !== 1) errors.push('exactly one disc must be the default');
  return errors;
}

/** A disc's image, read from the repository and checked against the manifest, for the build to publish. */
export const readDisc = (disc, root = REPO_ROOT) => readRom({ path: discPath(disc), sha256: disc.sha256 }, root);

/**
 * The discs for the page: the manifest checked (the build stops on any
 * problem, as it does for the registry), grouped by kind in the order the
 * manifest gives them, each group named.
 */
export function discLibrary(root = REPO_ROOT) {
  const discs = loadDiscs(root);
  const problems = discProblems(discs, root);
  if (problems.length > 0) throw new Error('The BBC Micro\'s discs cannot be published:\n- ' + problems.join('\n- '));
  const groups = new Map();
  for (const d of discs) groups.set(DISC_KINDS[d.kind], [...(groups.get(DISC_KINDS[d.kind]) ?? []), d]);
  return { discs, groups: [...groups].map(([name, list]) => ({ name, discs: list })), default: discs.find((d) => d.default) };
}
