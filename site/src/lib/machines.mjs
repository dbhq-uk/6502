import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { REPO_ROOT, SITE_ROOT } from './registry.mjs';
import { kim1Roms, bbcRoms, nesRoms, electronRoms, atari2600Roms } from './pins.mjs';

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
 * The teletype's keys with no printed character of their own, as the User
 * Manual names them in brackets, and the character each sends. The same table
 * as Kim1Teletype.Named.
 */
export const TELETYPE_KEYS = { SPACE: ' ', CR: '\r', LF: '\n', RUBOUT: '\u007f' };

/**
 * Splits keys typed at the KIM-1's teletype, written as the User Manual writes
 * them, into parts: { key } for a bracketed key ([SPACE], [CR], [LF] or
 * [RUBOUT]) and { text } for a run of other printable characters, the way
 * Kim1Teletype.Parse reads them. "0200[SPACE]G" is three parts. A bare space
 * is refused, because on a page it cannot be told from nothing.
 */
export function parseTeletype(keys) {
  const parts = [];
  for (const m of keys.matchAll(/\[([^\]]*)\]?|[^[]+/g)) {
    const word = m[0];
    if (word.startsWith('[')) {
      if (!word.endsWith(']') || !Object.hasOwn(TELETYPE_KEYS, m[1])) throw new Error(`"${word}" is not a bracketed teletype key`);
      parts.push({ key: m[1] });
    } else {
      if (!/^[!-~]+$/.test(word)) throw new Error(`"${word}" has a character that must be written as a bracketed key`);
      parts.push({ text: word });
    }
  }
  return parts;
}

/** The characters keys written as parseTeletype reads them send, in order. */
export const teletypeChars = (keys) => parseTeletype(keys).map((p) => (p.key ? TELETYPE_KEYS[p.key] : p.text)).join('');

/**
 * The session typed at a machine's teletype, from machines/<id>/teletype.json,
 * or null when it has none: steps, each with keys pressed on the keypad first
 * (`keypad`, as parseKeys reads them), keys typed at the teletype (`type`, as
 * parseTeletype reads them), or both; the last lines the paper `prints` after
 * it; and what it `says`. Kim1TeletypeTests runs the same file.
 */
export function loadTeletype(id, root = REPO_ROOT) {
  const file = path.join(root, 'machines', id, 'teletype.json');
  if (!fs.existsSync(file)) return null;
  const session = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (!Array.isArray(session.steps) || session.steps.length === 0) throw new Error(`machines/${id}/teletype.json has no steps`);
  for (const [i, step] of session.steps.entries()) {
    const where = `machines/${id}/teletype.json, step ${i + 1}`;
    if (!step.keypad && !step.type) throw new Error(`${where} presses no key and types nothing`);
    if (step.keypad) parseKeys(step.keypad);
    if (step.type) parseTeletype(step.type);
    if (!Array.isArray(step.prints) || step.prints.length === 0 || !step.prints.every((l) => typeof l === 'string' && /^[ -~]*$/.test(l) && l === l.trimEnd())) {
      throw new Error(`${where}: prints must be lines of plain printable text with no spaces at their ends`);
    }
    if (!words(step.says)) throw new Error(`${where} says nothing`);
  }
  return session;
}

/**
 * A machine's "try it" program, from machines/<id>/try-it.json, or null when it
 * has none. The KIM-1's is steps of keypad keys, each checked here as
 * Kim1Keystrokes.Parse reads them. The BBC Micro's and the Electron's are BASIC
 * (BASIC_TRY_IT): `lines` to type, each followed by RETURN, and `shows`, what the
 * screen shows after them, each line plain printable text. The NES's is a game to
 * play: see checkNesTryIt. The Atari 2600's is a game too: see checkAtari2600TryIt.
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
  if (id === 'nes') return checkNesTryIt(program, root);
  if (id === 'atari-2600') return checkAtari2600TryIt(program, root);
  for (const step of program.steps) parseKeys(step.keys);
  return program;
}

const words = (v) => typeof v === 'string' && v.trim() !== '';

/**
 * The buttons a try-it file may name for the NES pad: the D-pad as one, and the
 * other four. Typed here rather than read from public/nes-keys.js, because the
 * build script imports this file and reads nothing under public/;
 * tests/nes.test.mjs checks the two agree.
 */
export const NES_PAD = ['D-pad', 'A', 'B', 'Select', 'Start'];

/**
 * The NES's try-it file: the game Start puts in (`rom`, the pinned file, read
 * and checked against its hash, with its `title`, `author` and `licence`),
 * `steps` of what to do and what the page then shows, and `controls`, what each
 * of the pad's buttons does in the game (one of NES_PAD).
 */
function checkNesTryIt(program, root) {
  const where = 'machines/nes/try-it.json';
  for (const name of ['title', 'author', 'licence']) if (!words(program[name])) throw new Error(`${where}: needs its ${name}, the bundled game's`);
  if (!Array.isArray(program.steps) || program.steps.length === 0) throw new Error(`${where}: steps must be a list of { do, says }`);
  program.steps.forEach((step, i) => {
    if (!words(step?.do) || !words(step?.says)) throw new Error(`${where}: step ${i + 1} needs do and says, each a sentence`);
  });
  if (!Array.isArray(program.controls) || program.controls.length === 0) throw new Error(`${where}: controls must be a list of { button, does }`);
  program.controls.forEach((control, i) => {
    if (!words(control?.button) || !words(control?.does)) throw new Error(`${where}: control ${i + 1} needs button and does`);
    if (!NES_PAD.includes(control.button)) throw new Error(`${where}: "${control.button}" is not a button on the NES pad`);
  });
  // Last, as it reads the file: the game named is the pinned one, and its bytes are the pin's.
  const [game] = nesRoms();
  if (program.rom !== game.path) throw new Error(`${where}: names ${program.rom}, not the pinned ${game.path}`);
  readRom(game, root);
  return program;
}

/**
 * The controls a try-it file may name for the Atari 2600: the console's own,
 * a joystick and its two players' fire buttons, and the switches on its front.
 * Typed here rather than read from public/atari-2600-keys.js, because the build
 * script imports this file and reads nothing under public/;
 * tests/atari-2600.test.mjs checks each has its key on the page's map.
 */
export const ATARI_2600_CONTROLS = ['Joystick', 'Left fire button', 'Right fire button', 'GAME RESET', 'GAME SELECT', 'Colour switch', 'Left difficulty switch', 'Right difficulty switch'];

/**
 * The Atari 2600's try-it file, in the NES's shape: the game Start puts in
 * (`rom`, the pinned file, read and checked against its hash, with its `title`,
 * `author`, `licence` and `licenceUrl`, the address of the licence as its author
 * published it, and the `credit` the page gives), `steps` of what to do and
 * what the page then shows, and `controls`, what each of the console's controls
 * does in the game (one of ATARI_2600_CONTROLS).
 */
function checkAtari2600TryIt(program, root) {
  const where = 'machines/atari-2600/try-it.json';
  for (const name of ['title', 'author', 'licence', 'credit']) if (!words(program[name])) throw new Error(`${where}: needs its ${name}, the bundled game's`);
  if (!/^https:\/\/\S+$/.test(program.licenceUrl ?? '')) throw new Error(`${where}: needs its licenceUrl, an https link to the licence as its author published it`);
  if (!Array.isArray(program.steps) || program.steps.length === 0) throw new Error(`${where}: steps must be a list of { do, says }`);
  program.steps.forEach((step, i) => {
    if (!words(step?.do) || !words(step?.says)) throw new Error(`${where}: step ${i + 1} needs do and says, each a sentence`);
  });
  if (!Array.isArray(program.controls) || program.controls.length === 0) throw new Error(`${where}: controls must be a list of { button, does }`);
  program.controls.forEach((control, i) => {
    if (!words(control?.button) || !words(control?.does)) throw new Error(`${where}: control ${i + 1} needs button and does`);
    if (!ATARI_2600_CONTROLS.includes(control.button)) throw new Error(`${where}: "${control.button}" is not one of the console's controls`);
  });
  // Last, as it reads the file: the game named is the pinned one, and its bytes are the pin's.
  const [game] = atari2600Roms();
  if (program.rom !== game.path) throw new Error(`${where}: names ${program.rom}, not the pinned ${game.path}`);
  readRom(game, root);
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
 * Electron's BASIC is the BBC Micro's file, so both publish a copy of it. The
 * Atari 2600, like the NES, has no system ROM: it publishes the game its page
 * puts in, from machines/atari-2600/cartridges/.
 */
export const MACHINE_BUILDS = {
  'kim-1': { project: 'Dbhq.Machines.Kim1.Wasm', roms: kim1Roms, discs: false },
  'bbc-micro': { project: 'Dbhq.Machines.BbcMicro.Wasm', roms: bbcRoms, discs: true },
  nes: { project: 'Dbhq.Machines.Nes.Wasm', roms: nesRoms, discs: false },
  electron: { project: 'Dbhq.Machines.Electron.Wasm', roms: electronRoms, discs: false },
  'atari-2600': { project: 'Dbhq.Machines.Atari2600.Wasm', roms: atari2600Roms, discs: false },
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

/**
 * How many bytes a machine's Start button downloads: every file the build put
 * in public/machines/<id>/, the WebAssembly and the ROMs, as built (the edge may
 * compress them on the way, so this is the most it can be), but for the
 * folders named in `skip`, which Start does not fetch (the BBC Micro's preset
 * discs). Null when the machine was not built into this copy of the site,
 * which the page says.
 */
export function machineDownloadBytes(id, root = SITE_ROOT, skip = []) {
  const dir = path.join(root, 'public', 'machines', id);
  if (!fs.existsSync(path.join(dir, '_framework'))) return null;
  let total = 0;
  const walk = (d) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      if (d === dir && skip.includes(e.name)) continue;
      const full = path.join(d, e.name);
      if (e.isDirectory()) walk(full);
      else total += fs.statSync(full).size;
    }
  };
  walk(dir);
  return total;
}
