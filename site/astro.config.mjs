import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

export default defineConfig({
  site: 'https://6502.dbhq.uk',
  // One canonical form per page, matching the other DBHQ sites.
  trailingSlash: 'always',
  // Every stylesheet inlined: the pages are small and this removes a
  // render-blocking request from the critical path.
  build: { inlineStylesheets: 'always' },
  integrations: [sitemap()],
});
