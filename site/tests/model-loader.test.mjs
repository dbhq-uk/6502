import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';

// public/model-loader.js, run on a made-up page with a small stand-in for the
// DOM: the views of a machine with a case, as MachineModel.astro writes them.
// What a real browser does with it is scripts/browser-check-nes.mjs's to check;
// this holds the loader's rules: which panel loads, and how the tabs move.

const SOURCE = fs.readFileSync(path.join(process.cwd(), 'public', 'model-loader.js'), 'utf8');

class El {
  constructor(attrs = {}, children = []) {
    this.attrs = { ...attrs };
    this.children = children;
    this.parent = null;
    for (const c of children) c.parent = this;
    this.listeners = {};
    this.dataset = {};
    for (const [k, v] of Object.entries(attrs)) if (k.startsWith('data-')) this.dataset[k.slice(5).replace(/-([a-z])/g, (_, c) => c.toUpperCase())] = v;
    this.disabled = false;
    this.textContent = '';
  }
  get hidden() { return 'hidden' in this.attrs; }
  set hidden(on) { if (on) this.attrs.hidden = ''; else delete this.attrs.hidden; }
  getAttribute(name) { return name in this.attrs ? this.attrs[name] : null; }
  setAttribute(name, value) { this.attrs[name] = String(value); }
  /** The one kind of selector the loader uses: [name] or [name="value"]. */
  matches(selector) {
    const m = /^\[([\w-]+)(?:="([^"]*)")?\]$/.exec(selector);
    if (!m) throw new Error(`the stand-in DOM does not know the selector ${selector}`);
    return m[1] in this.attrs && (m[2] === undefined || this.attrs[m[1]] === m[2]);
  }
  *all() { for (const c of this.children) { yield c; yield* c.all(); } }
  querySelectorAll(selector) { return [...this.all()].filter((e) => e.matches(selector)); }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  addEventListener(type, fn) { (this.listeners[type] ??= []).push(fn); }
  fire(type, init = {}) {
    const event = { type, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...init };
    for (const fn of this.listeners[type] ?? []) fn(event);
    return event;
  }
  focus() { page.focused = this; }
  /** Shown, with every parent shown: a hidden element is display none, and never nears the screen. */
  get shown() { for (let e = this; e; e = e.parent) if (e.hidden) return false; return true; }
}

let page;

/** A page with one section of views, each a panel with a stage, a Load button and a status line. */
function makePage(views = ['outside', 'inside']) {
  const tabs = views.map((v, i) => new El({ role: 'tab', id: `model-tab-${v}`, 'aria-selected': String(i === 0), 'aria-controls': `model-panel-${v}`, tabindex: i === 0 ? '0' : '-1' }));
  const panels = views.map((v, i) => new El(
    { role: 'tabpanel', id: `model-panel-${v}`, 'data-model': `m-${v}`, 'data-model-src': `/models/m-${v}.js`, 'data-state': 'idle', ...(i > 0 ? { hidden: '' } : {}) },
    [new El({ 'data-model-stage': '' }, [new El({ 'data-model-load': '' })]), new El({ 'data-model-status': '' })],
  ));
  const list = new El({ role: 'tablist', 'aria-label': 'Views of the model', 'data-model-tabs': '' }, tabs);
  const section = new El({ 'data-model-section': '', hidden: '' }, [list, ...panels]);
  const body = new El({}, [section]);
  const observed = [];
  const imported = [];
  page = { body, section, list, tabs, panels, observed, imported, focused: null };
  const document = {
    querySelectorAll: (s) => body.querySelectorAll(s),
    getElementById: (id) => [...body.all()].find((e) => e.attrs.id === id) ?? null,
  };
  class IntersectionObserver {
    constructor(callback) { this.callback = callback; this.targets = []; observed.push(this); }
    observe(el) { this.targets.push(el); }
    disconnect() { this.targets = []; }
  }
  // The loader's one dynamic import, made visible: the test records what would be fetched.
  const code = SOURCE.replace('await import(root.dataset.modelSrc)', 'await __import(root.dataset.modelSrc)');
  assert.notEqual(code, SOURCE, 'the loader no longer imports root.dataset.modelSrc');
  const __import = async (src) => { imported.push(src); return { mount: async (root) => { root.dataset.state = 'running'; } }; };
  vm.runInNewContext(code, { document, window: { IntersectionObserver }, IntersectionObserver, __import });
  return page;
}

/** The visitor scrolls the section into view: every observer hears whether its target is shown. */
async function scroll() {
  for (const o of page.observed) for (const t of o.targets) o.callback([{ target: t, isIntersecting: t.shown }]);
  await new Promise((r) => setImmediate(r));
}
const selected = () => page.tabs.map((t) => t.getAttribute('aria-selected') === 'true');
const tabOrder = () => page.tabs.map((t) => t.getAttribute('tabindex'));
const shownPanels = () => page.panels.map((p) => !p.hidden);
const key = (tab, k) => tab.fire('keydown', { key: k });

test('the loader shows the section, leaves the hidden panel hidden, and loads only the panel that is shown', async () => {
  makePage();
  assert.equal(page.section.hidden, false, 'the section stays hidden with JavaScript');
  assert.deepEqual(shownPanels(), [true, false], 'the loader showed a hidden panel');
  await scroll();
  assert.deepEqual(page.imported, ['/models/m-outside.js'], 'a hidden panel was loaded');
  assert.equal(page.panels[0].dataset.state, 'running');
  assert.equal(page.panels[1].dataset.state, 'idle');
  // Choosing Inside shows its panel, which then loads as it nears the screen; Outside is not loaded twice.
  page.tabs[1].fire('click');
  await scroll();
  assert.deepEqual(shownPanels(), [false, true]);
  assert.deepEqual(page.imported, ['/models/m-outside.js', '/models/m-inside.js']);
  page.tabs[0].fire('click');
  await scroll();
  assert.deepEqual(page.imported, ['/models/m-outside.js', '/models/m-inside.js'], 'a view was loaded twice');
});

test('a hidden panel loads when its Load button is pressed, and not before', async () => {
  makePage();
  await scroll();
  page.panels[1].querySelector('[data-model-load]').fire('click');
  await new Promise((r) => setImmediate(r));
  assert.deepEqual(page.imported, ['/models/m-outside.js', '/models/m-inside.js']);
});

test('the tabs: a click, Enter and Space select; Left and Right move and select, round the ends; Home and End go to the ends; only the selected tab is in the Tab order', () => {
  makePage(['outside', 'inside', 'third']);
  const [a, b, c] = page.tabs;
  assert.deepEqual(selected(), [true, false, false]);
  assert.deepEqual(tabOrder(), ['0', '-1', '-1']);
  let e = key(a, 'ArrowRight');
  assert.ok(e.defaultPrevented);
  assert.deepEqual(selected(), [false, true, false]);
  assert.deepEqual(tabOrder(), ['-1', '0', '-1']);
  assert.deepEqual(shownPanels(), [false, true, false]);
  assert.equal(page.focused, b, 'Right did not move the focus');
  key(b, 'ArrowRight');
  key(c, 'ArrowRight');
  assert.deepEqual(selected(), [true, false, false], 'Right does not go round from the last tab to the first');
  key(a, 'ArrowLeft');
  assert.deepEqual(selected(), [false, false, true], 'Left does not go round from the first tab to the last');
  assert.equal(page.focused, c);
  key(c, 'Home');
  assert.deepEqual(selected(), [true, false, false]);
  key(a, 'End');
  assert.deepEqual(selected(), [false, false, true]);
  assert.deepEqual(shownPanels(), [false, false, true]);
  // Enter and Space select the tab they are pressed on; a click too.
  b.fire('click');
  assert.deepEqual(selected(), [false, true, false]);
  key(a, 'Enter');
  assert.deepEqual(selected(), [true, false, false]);
  e = key(c, ' ');
  assert.ok(e.defaultPrevented, 'Space would scroll the page');
  assert.deepEqual(selected(), [false, false, true]);
  assert.deepEqual(tabOrder(), ['-1', '-1', '0']);
  // Any other key is the browser's: Tab moves on out of the tab list, and the loader does not stop it.
  e = key(c, 'Tab');
  assert.equal(e.defaultPrevented, false, 'the tab list traps Tab');
  assert.deepEqual(selected(), [false, false, true]);
});

test('the loader stays small, and imports nothing when the page opens', () => {
  assert.ok(Buffer.byteLength(SOURCE) < 4000, `the loader is ${Buffer.byteLength(SOURCE)} bytes`);
  assert.doesNotMatch(SOURCE, /^import\b/m);
  // No focus trap: the loader never takes Tab, and never moves the focus but on an arrow, Home or End.
  assert.doesNotMatch(SOURCE, /'Tab'|Escape/);
});

test('a tab key with Alt, Ctrl or Meta held is the browser\'s: Alt and Left goes back, and the tabs do not move', () => {
  makePage();
  const [a] = page.tabs;
  for (const mod of ['altKey', 'ctrlKey', 'metaKey']) {
    for (const k of ['ArrowRight', 'ArrowLeft', 'Home', 'End', 'Enter', ' ']) {
      const e = a.fire('keydown', { key: k, [mod]: true });
      assert.equal(e.defaultPrevented, false, `${mod} and ${k} was taken`);
      assert.deepEqual(selected(), [true, false], `${mod} and ${k} moved the tabs`);
    }
  }
});
