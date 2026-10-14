// The test floor: the one number under the site's passing tests, and the check
// that both workflows read it from there and hold no number of their own.
// Not a test file itself; build.test.mjs runs it on the real workflows and on
// broken copies, to show that it can fail.
import fs from 'node:fs';
import path from 'node:path';

export const FLOOR_FILE = path.join(process.cwd(), 'tests', 'floor.json');

// The floor from the file. Throws unless it is a positive whole number.
export function readFloor(file = FLOOR_FILE) {
  const { floor } = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (!Number.isInteger(floor) || floor <= 0) throw new Error(`${file} must hold a floor that is a positive whole number, not ${JSON.stringify(floor)}`);
  return floor;
}

// What is wrong with one workflow's floor check, as a list of sentences. Empty means right.
export function floorProblems(name, text) {
  const problems = [];
  const code = text.split('\n').filter((l) => !/^\s*#/.test(l));
  const reads = code.filter((l) => /^\s*FLOOR=/.test(l));
  if (reads.length !== 1 || !/^\s*FLOOR=\$\(jq -r \.floor tests\/floor\.json\)\s*$/.test(reads[0])) {
    problems.push(`${name} must set FLOOR once, from tests/floor.json with jq, and not otherwise`);
  }
  const compares = code.filter((l) => /\$\{PASS:-0\}/.test(l));
  if (compares.length !== 1 || !/^\s*\[ "\$\{PASS:-0\}" -ge "\$FLOOR" \]\s*$/.test(compares[0])) {
    problems.push(`${name} must compare the passing count with "$FLOOR" once, and with nothing else`);
  }
  for (const l of code.filter((l) => /\bPASS\b|\bFLOOR\b/.test(l))) {
    if (/(^|[^A-Za-z0-9_.-])\d{2,}([^A-Za-z0-9_.-]|$)/.test(l.replace(/\$\{PASS:-0\}/, ''))) {
      problems.push(`${name} has a number of its own on a line that handles the count: ${l.trim()}`);
    }
  }
  return problems;
}
