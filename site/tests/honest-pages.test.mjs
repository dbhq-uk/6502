import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { pages, page, visibleText } from './helpers.mjs';

const styles = path.join(process.cwd(), 'src', 'styles');
const read = (name) => fs.readFileSync(path.join(styles, name), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');

test('the hero image is above the fold, so it is not lazy: eager, with high fetch priority', () => {
  const html = page('/').html;
  const hero = [...html.matchAll(/<img\b[^>]*>/g)].map((m) => m[0]).find((t) => /data-generated/.test(t));
  assert.ok(hero, 'the home page has no generated image');
  assert.match(hero, /\bloading="eager"/);
  assert.match(hero, /\bfetchpriority="high"/);
  assert.doesNotMatch(hero, /loading="lazy"/);
});

// A generated image used as a CSS background has no <img> for the caption test
// in site.test.mjs to find. So: every custom property in imagery.css that holds
// an image is traced to the class that uses it, and every page with that class
// must carry a visible caption calling the backgrounds illustrations.
test('every generated image used as a background is captioned as an illustration on the pages that show it', () => {
  const vars = [...read('imagery.css').matchAll(/--([\w-]+):\s*url\(/g)].map((m) => m[1]);
  assert.ok(vars.length > 0, 'imagery.css holds no generated image');
  const rules = [...read('global.css').matchAll(/([^{}]+)\{([^}]*)\}/g)].map((m) => ({ selector: m[1].trim(), body: m[2] }));
  for (const name of vars) {
    const users = rules.filter((r) => r.body.includes(`var(--${name})`));
    assert.ok(users.length > 0, `--${name} is defined but no rule uses it: remove it or use it`);
    for (const rule of users) {
      const cls = /\.([\w-]+)/.exec(rule.selector.replace(/^@media[^{]*/, ''))?.[1];
      assert.ok(cls, `cannot find the class in "${rule.selector}"`);
      const shown = pages().filter((p) => new RegExp(`class="[^"]*\\b${cls}\\b`).test(p.html));
      assert.ok(shown.length > 0, `no built page uses .${cls}, which shows --${name}`);
      for (const p of shown) {
        const captions = [...p.html.matchAll(/<(?:p|figcaption)\b[^>]*class="figure-caption"[^>]*>([\s\S]*?)<\/(?:p|figcaption)>/g)].map((m) => visibleText(m[1]));
        assert.ok(captions.some((c) => /^Illustration/.test(c) && /background/i.test(c)), `${p.url} shows the generated --${name} but has no caption calling the backgrounds an illustration`);
      }
    }
  }
});

test('the home page does not read as though a machine already runs', () => {
  const text = visibleText(page('/').html);
  assert.ok(text.includes('A machine counts only once it runs in the browser and passes an automated test.'));
  assert.ok(!text.includes('Each runs in the browser'));
  assert.ok(!text.includes('One core, real machines'));
  assert.ok(!/machines built on it/.test(text));
});

// Tom Harte's SingleStepTests are a published record of every opcode's bus
// activity. Nothing in this repository says they were captured from hardware, and
// the known-differences document says one feature of them looks like a property of
// the program that generated them. So no page may claim the data is from a real
// chip. (A chip's own behaviour, such as unstable opcodes varying between parts, is
// a different claim and is not matched here.)
const HARDWARE_PROVENANCE = /(?:from|against|by) (?:the )?real (?:chip|hardware|silicon|6502)|recorded (?:data )?from (?:the |a )?(?:real|actual)|captured from|recorded from hardware|from (?:the )?(?:real )?hardware|one real chip's answer/i;

test('no page claims the reference data was recorded from a real chip', () => {
  for (const p of pages()) {
    const m = HARDWARE_PROVENANCE.exec(visibleText(p.html));
    assert.equal(m, null, `${p.url} says "${m?.[0]}", which nothing in the repository supports`);
  }
});

test('the proof claims on the home and about pages name the data they rest on', () => {
  for (const url of ['/', '/about/']) {
    const text = visibleText(page(url).html);
    assert.match(text, /Tom Harte's SingleStepTests, a published record of every opcode's bus activity, cycle by cycle/, `${url} does not say what the core is checked against`);
  }
});

test('the family page does not say that a machine runs on the core', () => {
  const text = visibleText(page('/family/').html);
  assert.doesNotMatch(text, /runs? on (?:this repository's|the) core|all run on the core|does too/i);
  assert.match(text, /Uses a CPU the core implements/);
});

test('no page asserts a test cadence the site cannot verify', () => {
  for (const p of pages()) assert.ok(!/on every push/i.test(visibleText(p.html)), `${p.url} claims tests run on every push`);
});
