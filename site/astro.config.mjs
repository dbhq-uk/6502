import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import { unified } from '@astrojs/markdown-remark';
import { rehypeJournal } from './src/lib/rehype-journal.mjs';

export default defineConfig({
  site: 'https://6502.dbhq.uk',
  // One canonical form per page, matching the other DBHQ sites.
  trailingSlash: 'always',
  // Every stylesheet inlined: the pages are small and this removes a
  // render-blocking request from the critical path.
  build: { inlineStylesheets: 'always' },
  markdown: { processor: unified({ rehypePlugins: [rehypeJournal] }) },
  integrations: [sitemap()],
});
