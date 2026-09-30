// Everything the pages read, loaded once: the registry, the test results, the
// dated measurements, and the figures computed from them. A page that needs a
// number takes it from here.
import { loadRegistry, validateRegistry } from './registry.mjs';
import { loadResults, validateResults } from './results.mjs';
import { loadMeasurements } from './measurements.mjs';
import { figures } from './figures.mjs';

const registry = loadRegistry();
const results = loadResults();
const measurements = loadMeasurements();

// The build refuses to publish figures it cannot stand behind.
const problems = [...validateRegistry(registry, results), ...validateResults(results)];
if (problems.length > 0) throw new Error('The site cannot be built:\n- ' + problems.join('\n- '));

export { registry, results, measurements };
export const fig = figures({ registry, results, measurements });
