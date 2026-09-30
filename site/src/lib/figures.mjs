import { counts } from './registry.mjs';
import { best } from './measurements.mjs';

/** The design's speed target: "at least 25 times real speed, for the core alone". A constant of the design, not a result. Whether a run meets it depends on REFERENCE_MHZ, which is the site's own choice. */
export const SPEED_TARGET = 25;
/** The clock of the reference machine the speed is compared with, in MHz: the BBC Micro's. This site's own choice, not the design's. */
export const REFERENCE_MHZ = 2;

/** Whether a speed, as a multiple of the reference machine, meets the design's target. Worded here so no page types the verdict. */
export const targetVerdict = (multiple) => (multiple >= SPEED_TARGET ? 'met' : 'not yet met');

export const fmt = (n) => n.toLocaleString('en-GB');
export const fmt1 = (n) => n.toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/**
 * Every figure the site shows, computed from the registry, the test results and
 * the dated measurements. Nothing here is a typed number except the two
 * constants above, which belong to the design.
 */
export function figures({ registry, results, measurements }) {
  const c = counts(registry);
  const suites = results.suites;
  const names = Object.keys(suites);
  const harte = names.filter((n) => /Harte$/.test(n));
  const speed = (mode) => best(measurements.modes[mode]) / REFERENCE_MHZ;
  return {
    machinesImplemented: c.running,
    machinesInScope: c.inScope,
    machinesInProgress: c.inProgress,
    variants: harte.length,
    testsPassing: results.total.passed,
    harteTests: harte.reduce((sum, n) => sum + suites[n].passed, 0),
    dormannBuilds: suites.DormannTests?.passed ?? 0,
    interruptRuns: suites.TransistorModelTests?.passed ?? 0,
    speedTarget: SPEED_TARGET,
    speedAot: speed('aot'),
    speedInterpreter: speed('interpreter'),
    speedNative: speed('native'),
  };
}
