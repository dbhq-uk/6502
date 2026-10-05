import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { REPO_ROOT } from './registry.mjs';
import { kim1Roms, bbcRoms, electronRoms } from './pins.mjs';

// The rules about which machines get a page, kept out of the templates so a
// test can run them on a made-up registry.

/** The machines that run in the browser and pass their acceptance test. Only these have a page. */
export const runningMachines = (registry) => registry.machines.filter((m) => m.status === 'running');

/** Where a machine's own page is, or null: only a running machine has one, so only a running machine is linked. */
export const machineHref = (machine) => (machine.status === 'running' ? `/machines/${machine.id}/` : null);

/** The sentence on a machine's page about its acceptance test. The count is read from the results, never typed. */
export function acceptanceSummary(machine, results, format = String) {
  const suite = results.suites[machine.acceptance];
  if (!suite) throw new Error(`machine ${machine.id}: acceptance test "${machine.acceptance}" is not in the test results`);
  return `Acceptance test ${machine.acceptance}: ${format(suite.passed)} passing.`;
}

/** Every key name the KIM-1's keypad has, as the User Manual names them. The same list as Kim1Keystrokes.Names. */
export const KIM1_KEYS = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'AD', 'DA', '+', 'GO', 'PC', 'ST', 'RS'];

/**
 * Splits keys written as the User Manual writes them into key names, the way
 * Kim1Keystrokes.Parse does: a bracketed word is one key, and a run of hex
 * digits is one key per character. "[AD] 0002 [DA] 18" is eight keys.
 */
export function parseKeys(keys) {
  const names = [];
  for (const word of keys.split(' ').filter(Boolean)) {
    const bracketed = /^\[(.+)\]$/.exec(word);
    if (bracketed) {
      if (!KIM1_KEYS.includes(bracketed[1])) throw new Error(`"${word}" is not a bracketed KIM-1 key`);
      names.push(bracketed[1]);
    } else {
      if (!/^[0-9a-f]+$/i.test(word)) throw new Error(`"${word}" is not a run of hex keys; write the other keys in brackets`);
      names.push(...word.toUpperCase());
    }
  }
  return names;
}

/** The machines whose "try it" program is BASIC lines rather than keypad steps. */
export const BASIC_TRY_IT = ['bbc-micro', 'electron'];

/**
 * A machine's "try it" program, from machines/<id>/try-it.json, or null when it
 * has none. The KIM-1's is steps of keypad keys, each checked here as
 * Kim1Keystrokes.Parse reads them. The BBC Micro's and the Electron's are BASIC
 * (BASIC_TRY_IT): `lines` to type, each followed by RETURN, and `shows`, what the
 * screen shows after them, each line plain printable text.
 */
export function loadTryIt(id, root = REPO_ROOT) {
  const file = path.join(root, 'machines', id, 'try-it.json');
  if (!fs.existsSync(file)) return null;
  const program = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (BASIC_TRY_IT.includes(id)) {
    for (const name of ['lines', 'shows']) {
      const list = program[name];
      if (!Array.isArray(list) || list.length === 0) throw new Error(`machines/${id}/try-it.json: ${name} must be a list of lines`);
      for (const line of list) if (typeof line !== 'string' || !/^[\x20-\x7e]+$/.test(line)) throw new Error(`machines/${id}/try-it.json: "${line}" in ${name} is not a line of plain printable text`);
    }
    return program;
  }
  for (const step of program.steps) parseKeys(step.keys);
  return program;
}

/**
 * The home page's sentence about which machines run, true for none, one or
 * many: "None runs in the browser yet." or "Running in the browser now: KIM-1."
 * The names come from the registry, so the sentence cannot claim more than it holds.
 */
export function runningSentence(registry) {
  const names = runningMachines(registry).map((m) => m.name);
  if (names.length === 0) return 'None runs in the browser yet.';
  const list = names.length === 1 ? names[0] : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`;
  return `Running in the browser now: ${list}.`;
}

/**
 * What scripts/build-machines.mjs publishes for each machine it knows, by the
 * machine's registry id: the WebAssembly project under src/, and the ROMs, in
 * the order the host's Load takes them, as their pins in Pins.cs give them.
 * `discs` is true for a machine with preset discs: the BBC Micro's, listed in
 * machines/bbc-micro/discs/manifest.json (src/lib/bbc-micro.mjs). The
 * Electron's BASIC is the BBC Micro's file, so both publish a copy of it.
 */
export const MACHINE_BUILDS = {
  'kim-1': { project: 'Dbhq.Machines.Kim1.Wasm', roms: kim1Roms, discs: false },
  'bbc-micro': { project: 'Dbhq.Machines.BbcMicro.Wasm', roms: bbcRoms, discs: true },
  electron: { project: 'Dbhq.Machines.Electron.Wasm', roms: electronRoms, discs: false },
};

/** The build for each id named, in the order named. Throws on an id with no build, or on none at all. */
export function machineBuilds(ids) {
  const known = Object.keys(MACHINE_BUILDS);
  const last = (word) => `${known.slice(0, -1).join(', ')} ${word} ${known.at(-1)}`;
  if (ids.length === 0) throw new Error(`name the machines to build: ${last('or')}, or several`);
  for (const id of ids) if (!MACHINE_BUILDS[id]) throw new Error(`no build for "${id}": the machines are ${last('and')}`);
  return [...new Set(ids)].map((id) => ({ id, ...MACHINE_BUILDS[id] }));
}

/**
 * A ROM's bytes, read from the repository at the path its pin gives and
 * checked against its pinned SHA-256 (AGENTS.md rules 3 and 4). Throws when the
 * file is missing or its hash is not the pin.
 */
export function readRom({ path: relative, sha256: want }, root = REPO_ROOT) {
  const bytes = fs.readFileSync(path.join(root, relative));
  const got = crypto.createHash('sha256').update(bytes).digest('hex');
  if (got !== want) throw new Error(`${relative} does not match its pinned hash ${want}`);
  return bytes;
}
