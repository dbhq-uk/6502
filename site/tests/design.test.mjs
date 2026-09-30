import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

const styles = path.join(process.cwd(), 'src', 'styles');
const tokens = fs.readFileSync(path.join(styles, 'tokens.css'), 'utf8');
const global = fs.readFileSync(path.join(styles, 'global.css'), 'utf8');
const color = (name) => /#[0-9a-fA-F]{6}/.exec(new RegExp(`--${name}:\\s*(#[0-9a-fA-F]{6})`).exec(tokens)?.[0] ?? '')?.[0];

const luminance = (hex) => {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const contrast = (a, b) => {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
};

// WCAG 2.2 AA: 4.5 to 1 for text.
const AA = 4.5;
const TEXT = ['white', 'mint', 'moss-80', 'moss-70', 'sage-60', 'sage-40', 'fern', 'deep'];

test('every text colour meets AA on the black canvas', () => {
  for (const name of TEXT) {
    const ratio = contrast(color(name), color('void'));
    assert.ok(ratio >= AA, `--${name} on black is ${ratio.toFixed(2)} to 1`);
  }
});

test('text meets AA on the card, window and nav surfaces it is actually set on', () => {
  for (const surface of ['iron', 'card', 'veil']) {
    for (const name of ['white', 'moss-70', 'sage-60']) {
      const ratio = contrast(color(name), color(surface));
      assert.ok(ratio >= AA, `--${name} on --${surface} is ${ratio.toFixed(2)} to 1`);
    }
  }
});

test('the lime accent works as a fill: dark text on it, and it stands out from the canvas', () => {
  assert.ok(contrast(color('iron'), color('lime')) >= AA);
  assert.ok(contrast(color('lime'), color('void')) >= 3, 'a non-text component needs 3 to 1');
});

test('a button outline and a focus ring are visible against the canvas', () => {
  assert.ok(contrast(color('white'), color('void')) >= 3);
  assert.ok(contrast(color('lime'), color('void')) >= 3);
});

test('only the navigation bar has a shadow', () => {
  const stripped = global.replace(/\/\*[\s\S]*?\*\//g, '');
  const shadows = [...stripped.matchAll(/([^{}]+)\{[^}]*box-shadow:/g)].map((m) => m[1].trim());
  assert.deepEqual(shadows, ['.top']);
});

test('the design tokens are used by name, never as a raw hex in the stylesheet', () => {
  const raw = global.match(/#[0-9a-fA-F]{3,8}\b/g) ?? [];
  // A few translucent and near-black values belong to one component each (the window's own hairline, the nav's blur).
  const allowed = new Set(['#000', '#2a2f2a', '#3a3f3a']);
  assert.deepEqual(raw.filter((h) => !allowed.has(h)), []);
});
