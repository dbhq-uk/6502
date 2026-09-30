import fs from 'node:fs';
import path from 'node:path';

// The working directory is site/: see registry.mjs.
export const MEASUREMENTS_FILE = path.join(process.cwd(), 'src', 'data', 'measurements.json');

export function loadMeasurements(file = MEASUREMENTS_FILE) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

/** Best run in MHz. */
export const best = (mode) => Math.max(...mode.runs.map((r) => r.mhz));
