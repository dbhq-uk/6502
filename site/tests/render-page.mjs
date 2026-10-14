// Renders src/pages/machines/[id].astro for machine records the registry does
// not hold, through Astro's container and Vite's module loader, in Node: so a
// test can see the built page of a machine as it would be, such as a running
// machine with no model, which the real registry no longer has. Not a test
// file itself (no .test.mjs): tests/model.test.mjs imports it.
import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { getViteConfig } from 'astro/config';
import { createServer } from 'vite';

/** Each machine record's page, as HTML, in order. */
export async function renderMachinePage(machines) {
  const config = await getViteConfig({ server: { middlewareMode: true, hmr: false, ws: false }, appType: 'custom', logLevel: 'silent', optimizeDeps: { noDiscovery: true, include: [] } })({ command: 'serve', mode: 'test' });
  const vite = await createServer(config);
  try {
    const page = await vite.ssrLoadModule('/src/pages/machines/[id].astro');
    const container = await AstroContainer.create();
    const out = [];
    for (const machine of machines) out.push(await container.renderToString(page.default, { props: { machine }, params: { id: machine.id } }));
    return out;
  } finally {
    await vite.close();
  }
}
