import test from 'node:test';
import assert from 'node:assert/strict';
import { HtmlValidate } from 'html-validate';
import { pages } from './helpers.mjs';

// The recommended rules, less the ones that object to how Astro writes markup
// (void-element style, inline style attributes on a few one-off margins).
const validator = new HtmlValidate({
  extends: ['html-validate:recommended'],
  rules: {
    'void-style': 'off',
    'no-inline-style': 'off',
    'no-trailing-whitespace': 'off',
    'attribute-boolean-style': 'off',
    'long-title': 'off',
    // HTML5 allows an id that starts with a digit, and a heading like "65C02 interrupt timing" makes one.
    'valid-id': ['error', { relaxed: true }],
    'no-unknown-elements': 'error',
  },
});

test('every built page is valid HTML', async () => {
  const problems = [];
  for (const p of pages()) {
    const report = await validator.validateString(p.html, p.url);
    for (const result of report.results) {
      for (const m of result.messages) problems.push(`${p.url} ${m.line}:${m.column} ${m.ruleId}: ${m.message}`);
    }
  }
  assert.deepEqual(problems, []);
});
