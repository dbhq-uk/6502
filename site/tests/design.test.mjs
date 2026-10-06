import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

// sharp ships with Astro (its image service), so the texture can be measured without a new dependency.
const sharp = (await import('sharp')).default;

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

test('the three body text colours meet AA on the card, window and nav surfaces', () => {
  for (const surface of ['iron', 'card', 'veil']) {
    for (const name of ['white', 'moss-70', 'sage-60']) {
      const ratio = contrast(color(name), color(surface));
      assert.ok(ratio >= AA, `--${name} on --${surface} is ${ratio.toFixed(2)} to 1`);
    }
  }
});

// Every rule in global.css that sets a text colour, and the surface(s) it is set on.
// The test below fails if a colour rule is missing from this table, so a new rule
// has to say where it sits before it can ship. The nav bar is translucent: its
// colour is the CSS rgba blended over the black canvas, which is what a reader
// sees at the top of a page. (Scrolled over lighter content it can differ.)
const css = global.replace(/\/\*[\s\S]*?\*\//g, '').replace(/@media[^{]*\{/g, '');
const rules = [...css.matchAll(/([^{}]+)\{([^}]*)\}/g)].map((m) => ({ selector: m[1].trim().replace(/\s+/g, ' '), body: m[2] }));

const nav = (() => {
  const m = /\.top \{[^}]*background: rgba\((\d+), (\d+), (\d+), ([\d.]+)\)/.exec(css);
  assert.ok(m, 'the nav bar background is not an rgba the test can read');
  const alpha = Number(m[4]);
  const bg = color('void');
  return '#' + [1, 2, 3].map((i, k) => Math.round(Number(m[i]) * alpha + parseInt(bg.slice(1 + 2 * k, 3 + 2 * k), 16) * (1 - alpha)).toString(16).padStart(2, '0')).join('');
})();

// The traces texture is a picture, so its "colour" is the worst case: the
// brightest pixel of the image, under the black overlay the stylesheet puts
// over it. Text that passes on that pixel passes everywhere on the section.
const tex = await (async () => {
  const overlay = /\.tex::before \{[^}]*background: rgba\(0, 0, 0, ([\d.]+)\)/.exec(css);
  assert.ok(overlay, 'the .tex overlay is not an rgba the test can read');
  const { data, info } = await sharp(path.join(process.cwd(), 'src', 'assets', 'imagery', 'traces.webp')).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  let best = [0, 0, 0];
  for (let i = 0; i < data.length; i += info.channels) {
    const px = [data[i], data[i + 1], data[i + 2]];
    if (luminance('#' + px.map((v) => v.toString(16).padStart(2, '0')).join('')) > luminance('#' + best.map((v) => v.toString(16).padStart(2, '0')).join(''))) best = px;
  }
  return '#' + best.map((v) => Math.round(v * (1 - Number(overlay[1]))).toString(16).padStart(2, '0')).join('');
})();
const surface = (name) => (name === 'nav' ? nav : name === 'tex' ? tex : color(name));

const ON = {
  body: ['void'],
  a: ['void', 'iron', 'tex'],
  'a:hover': ['void', 'iron'],
  '.skip': ['white'],
  'h1, h2, h3': ['void', 'iron', 'card', 'tex'],
  '.eyebrow': ['void', 'tex'],
  '.brand': ['nav'],
  '.brand small': ['nav'],
  '.links a': ['nav'],
  '.links a:hover, .links a[aria-current="page"]': ['nav'],
  '.btn': ['iron'],
  '.btn:hover': ['veil'],
  '.btn.pill': ['lime'],
  '.btn.pill:hover': ['lime'],
  '.hero .lede': ['void'],
  '.figure-caption': ['void'],
  '.card b': ['card'],
  '.card h3 a': ['card'],
  '.card h3 a:hover': ['card'],
  '.chip': ['void', 'card'],
  '.chip.on': ['white'],
  '.note': ['void'],
  '.tex .note': ['tex'],
  '.win .bar span': ['iron'],
  '.win pre': ['iron'],
  '.win .k': ['iron'],
  '.entry time': ['void'],
  '.entry h2 a, .entry h3 a': ['void'],
  '.prose code': ['iron'],
  '.prose th': ['void'],
  '.prose blockquote': ['void'],
  '.meta': ['void'],
  '.filters label': ['void'],
  '.filters select': ['iron'],
  '.data th': ['iron'],
  '.status': ['void', 'card'],
  '.status.running': ['white'],
  '.count': ['void'],
  footer: ['void'],
  'footer a': ['void'],
  'footer .linkbtn': ['void'],
  'footer .linkbtn:hover': ['void'],
  '.die-label': ['iron'],
  '.die-tour-head': ['iron'],
  '.die-count b': ['iron'],
  '.die-n': ['iron'],
  '.die-t': ['iron'],
  '.die-stop:hover .die-t': ['iron'],
  '.die-stop[aria-current="true"] .die-t': ['iron'],
  '.die-text p': ['iron'],
  '.die-field': ['void'],
  '.die-field select': ['iron'],
  '.analytics-notice': ['iron'],
  '.analytics-notice .analytics-notice-status': ['iron'],
  // The KIM-1's page. A key sits on veil, on card when hovered, on the black
  // canvas while pressed, and on iron while it is disabled before the machine loads.
  '.kim1 .key': ['veil', 'card', 'void', 'iron'],
  '.kim1-status': ['iron'],
  '.kim1-speed': ['iron'],
  '.steps kbd, .bbc-symbols kbd': ['iron'],
  // The BBC Micro's page. Its lines sit on the iron panel; a disabled button is
  // iron too, before the machine loads. The symbol table's keys are on iron
  // over the black canvas, like the KIM-1 program's, and share its rule.
  '.bbc .btn:disabled': ['iron'],
  '.bbc-status': ['iron'],
  '.bbc-line': ['iron'],
  '.bbc-note': ['iron'],
  '.bbc-note kbd': ['iron'],
  '.bbc-speed': ['iron'],
  '.bbc-disc legend': ['iron'],
  '.bbc-protect': ['iron'],
  '.bbc-symbols caption': ['void'],
  // The on-screen keys: veil, card when hovered or latched, the black canvas
  // while pressed; disabled, before the machine runs, iron.
  '.bbc-key': ['veil', 'card', 'void'],
  '.bbc-key:disabled': ['iron'],
  // The NES's page, as the BBC Micro's: its lines on the iron panel, a disabled
  // button iron, the region's and the cartridge's legends and choices on iron.
  '.nes .btn:disabled': ['iron'],
  '.nes-status': ['iron'],
  '.nes-line': ['iron'],
  '.nes-note': ['iron'],
  '.nes-speed': ['iron'],
  '.nes-region legend, .nes-cartridge legend': ['iron'],
  '.nes-choice': ['iron'],
  // Controller 1's buttons: veil, card when hovered, the black canvas while
  // pressed; disabled, before the machine runs, iron.
  '.nes-button': ['veil', 'card', 'void'],
  '.nes-button:disabled': ['iron'],
  // The machine page's 3D model: its label sits on an iron tag over the canvas,
  // and its status line on the black canvas.
  '.model-tag': ['iron'],
  '.model-hint': ['iron'],
  '.model-status': ['void'],
  '.model-help': ['void'],
  // Show tracks and Show tracks only, pressed: white on veil, the same pair as a hovered button.
  '.model-toggle[aria-pressed="true"]': ['veil'],
};

test('every text colour rule is placed on a surface, and reaches AA on each one it is set on', () => {
  const unplaced = [];
  const failures = [];
  for (const { selector, body } of rules) {
    if (!/(?<![-\w])color:/.test(body)) continue;
    const m = /(?<![-\w])color:\s*var\(--([\w-]+)\)/.exec(body);
    if (!m) { failures.push(`${selector}: colour is not a token`); continue; }
    if (!ON[selector]) { unplaced.push(selector); continue; }
    for (const s of ON[selector]) {
      const ratio = contrast(color(m[1]), surface(s));
      if (ratio < AA) failures.push(`${selector}: --${m[1]} on ${s} is ${ratio.toFixed(2)} to 1`);
    }
  }
  assert.deepEqual(unplaced, [], 'colour rules with no surface in the table');
  assert.deepEqual(failures, []);
  const stale = Object.keys(ON).filter((k) => !rules.some((r) => r.selector === k && /(?<![-\w])color:/.test(r.body)));
  assert.deepEqual(stale, [], 'table entries for rules that no longer set a colour');
});

test('the two dimmest text colours are used only where they were measured to pass', () => {
  // sage-40 and deep pass on the black canvas alone. Naming the bar they fail on keeps the reason in the suite.
  for (const name of ['sage-40', 'deep']) {
    assert.ok(contrast(color(name), color('void')) >= AA, `--${name} must pass on black`);
    assert.ok(contrast(color(name), nav) < AA, `--${name} unexpectedly passes on the nav bar: widen its use or drop this guard`);
  }
  for (const { selector, body } of rules) {
    const m = /(?<![-\w])color:\s*var\(--(sage-40|deep)\)/.exec(body);
    if (m) assert.deepEqual(ON[selector], ['void'], `${selector} sets --${m[1]} on a surface other than the black canvas`);
  }
});

test('the navigation links are never hidden, at any width', () => {
  const hidden = rules.filter((r) => /(^|[\s,])\.(links|top|brand)\b/.test(r.selector) && /display:\s*none/.test(r.body));
  assert.deepEqual(hidden.map((r) => r.selector), []);
  const wrap = rules.find((r) => r.selector === '.top .wrap' && /flex-wrap:\s*wrap/.test(r.body));
  assert.ok(wrap, 'the header must be able to wrap onto a second row');
  assert.ok(rules.some((r) => r.selector === '.links' && /flex-wrap:\s*wrap/.test(r.body)), 'the links must be able to wrap');
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
