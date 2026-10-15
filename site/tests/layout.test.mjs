import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

// The layout's numbers (15 October 2026, after "the margin widths are too big"):
// the container, its padding, the space round a section, and the measure that
// holds running text to a readable line. A change to any of them fails here, so
// it is made on purpose and DESIGN.md is changed with it.

const styles = path.join(process.cwd(), 'src', 'styles');
const tokens = fs.readFileSync(path.join(styles, 'tokens.css'), 'utf8');
const global = fs.readFileSync(path.join(styles, 'global.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
const token = (name) => new RegExp(`--${name}:\\s*([^;]+);`).exec(tokens)?.[1].trim();
const rules = [...global.replace(/@media[^{]*\{/g, '').matchAll(/([^{}]+)\{([^}]*)\}/g)].map((m) => ({ selector: m[1].trim().replace(/\s+/g, ' '), body: m[2] }));
const rule = (selector) => rules.filter((r) => r.selector === selector).map((r) => r.body).join(';');

test('the container, its padding and the space round a section are the agreed ones', () => {
  assert.equal(token('max'), '1320px');
  assert.equal(token('pad'), 'clamp(16px, 4vw, 32px)');
  assert.equal(token('section'), 'clamp(32px, 4.5vw, 64px)');
  assert.match(rule('.wrap'), /max-width: var\(--max\);[^;]*;\s*padding: 0 var\(--pad\)/);
  assert.match(rule('section'), /padding: var\(--section\) 0/);
});

// Inter's average character in running English, at the body's -0.022em
// letter-spacing, measured in Chrome on 15 October 2026 on the built Atari 2600
// page: a 528-pixel line of 16-pixel text held 74 characters, 7.13 pixels each.
const CHARACTER_EM = 7.13 / 16;

test('the measure holds a line of running text to about 70 to 75 characters', () => {
  const m = /^([\d.]+)em$/.exec(token('measure') ?? '');
  assert.ok(m, `--measure is ${token('measure')}, not a length in em, so it would not follow the font size`);
  const characters = Number(m[1]) / CHARACTER_EM;
  assert.ok(characters >= 70 && characters <= 75, `--measure is about ${characters.toFixed(0)} characters a line`);
});

test('a reading section takes the whole container and only its text is held to the measure', () => {
  // No section is a narrow centred strip: nothing caps a block at a fixed width
  // in pixels but the things that are pictures or a pad (a screen, a photograph,
  // a pad, the notice, a model's stage).
  for (const selector of ['.narrow', '.prose', '.entries']) {
    assert.doesNotMatch(rule(selector), /max-width/, `${selector} caps its own width again`);
  }
  assert.doesNotMatch(global, /max-width: 860px/);
  // The text is held to the measure: a reading section's own paragraphs and lists,
  // rendered markdown at any depth, the machine head, the journal list and every
  // panel's notes.
  for (const selector of ['.narrow > p, .narrow > ul, .narrow > ol', '.prose :is(p, li, blockquote, dd, h2, h3)', '.entry p', '.head p', '.hero .lede', '.two p', '.kim1-tty-about', '.bbc-note', '.bbc-preset-about', '.electron-note', '.nes-note', '.vcs-note']) {
    assert.match(rule(selector), /max-width: var\(--measure\)/, `${selector} is not held to the measure`);
  }
  assert.match(rule('.machine-intro'), /grid-template-columns: minmax\(0, var\(--measure\)\) 360px; justify-content: space-between;/);
  // A cap in ch is wider than it looks in Inter (72ch is about 90 characters), so
  // one is kept only where the text is mono, where a ch is a character, for the
  // home page's heading, or for a line that is short anyway.
  const inCh = rules.filter((r) => /max-width: \d+ch/.test(r.body)).map((r) => r.selector).sort();
  assert.deepEqual(inCh, ['.bbc-speed', '.electron-speed', '.hero h1', '.kim1-speed', '.kim1-status', '.nes-speed', '.vcs-speed', 'footer p']);
});

test('a machine screen fits the window, and the pads keep the width of a real pad', () => {
  assert.equal(token('screen-fit'), 'max(240px, 100svh - 180px)');
  for (const screen of ['.bbc-screen canvas', '.electron-screen canvas', '.nes-screen canvas', '.vcs-screen canvas']) {
    assert.match(rule(screen), /max-width: [^;]*var\(--screen-fit\)/, `${screen} can grow taller than the window`);
  }
  assert.match(rule('.nes-pad'), /width: min\(100%, 640px\); justify-self: center;/);
  assert.match(rule('.vcs-pad'), /width: min\(100%, 420px\); justify-self: center;/);
  // On a phone the NES pad fits a 390-pixel screen: Select and Start take two columns each under the pad.
  assert.match(global, /@media \(max-width: 560px\) \{ \.nes-pad \{ grid-template-columns: repeat\(3, 44px\) repeat\(2, minmax\(0, 1fr\)\) repeat\(2, 48px\); grid-template-areas: "\. up \. \. \. \. \." "left \. right \. \. b a" "\. down \. select select start start"; gap: 4px; \} \}/);
});
