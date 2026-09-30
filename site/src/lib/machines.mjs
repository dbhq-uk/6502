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
