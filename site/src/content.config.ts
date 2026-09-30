import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { z } from 'astro/zod';

// The journal is the repository's own docs/journal folder, read at build time.
// Every entry carries title, date and summary in its front matter.
const journal = defineCollection({
  loader: glob({ pattern: '2*.md', base: '../docs/journal' }),
  schema: z.object({
    title: z.string(),
    date: z.coerce.date(),
    summary: z.string(),
    // Position within a day, for entries that share a date. Higher is later.
    order: z.number().default(0),
  }),
});

export const collections = { journal };
