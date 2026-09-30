import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { z } from 'astro/zod';

// The journal is the repository's own docs/journal folder, read at build time.
// Every entry carries title, date, summary and order in its front matter.
const journal = defineCollection({
  loader: glob({ pattern: '2*.md', base: '../docs/journal' }),
  schema: z.object({
    title: z.string(),
    date: z.coerce.date(),
    summary: z.string(),
    // Position within a day, for entries that share a date. Higher is later. Required, so no entry silently sorts as 0.
    order: z.number(),
  }),
});

// Two documents rendered as pages, unaltered.
const docs = defineCollection({
  loader: glob({ pattern: ['the-6502-family.md', 'known-differences.md'], base: '../docs' }),
  schema: z.object({ title: z.string().optional() }),
});

export const collections = { journal, docs };
