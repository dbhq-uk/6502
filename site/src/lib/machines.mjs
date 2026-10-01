import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from './registry.mjs';

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

/** A machine's "try it" program, from machines/<id>/try-it.json, or null when it has none. */
export function loadTryIt(id, root = REPO_ROOT) {
  const file = path.join(root, 'machines', id, 'try-it.json');
  if (!fs.existsSync(file)) return null;
  const program = JSON.parse(fs.readFileSync(file, 'utf8'));
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
