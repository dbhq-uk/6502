// Loads a machine's 3D model, and three.js with it, only when the visitor gets
// near it: when the model comes within a screen's reach of being in view, or
// when they press "Load the 3D model". Until then the page carries none of it.
// An external module because the site's CSP allows no inline script.
//
// A machine with a case has two models, its views, as tabs (MachineModel.astro):
// the section is shown here, and each view's panel is a model of its own, loaded
// as above. A hidden panel never nears the screen, so a view loads only once its
// tab is chosen; switching away hides its panel, whose stage then stops drawing
// and keeps its camera for when the view comes back. The tabs are WAI-ARIA's:
// a click, Enter or Space selects one; Left and Right move to the next and
// select it, Home and End to the ends; only the selected tab is in the Tab
// order, and Tab goes on from it into the panel and out of the section.
//
// Without JavaScript none of this runs and the section stays hidden: the
// photograph and the page's own keypad are the machine without it.

const NEAR = '600px 0px';

for (const section of document.querySelectorAll('[data-model-section]')) section.hidden = false;
for (const root of document.querySelectorAll('[data-model]')) setup(root);
for (const list of document.querySelectorAll('[data-model-tabs]')) tabs(list);

function setup(root) {
  const button = root.querySelector('[data-model-load]');
  const status = root.querySelector('[data-model-status]');
  // A view's panel is shown by its tab, not here.
  if (root.getAttribute('role') !== 'tabpanel') root.hidden = false;

  let started = false;
  let observer = null;
  const load = async () => {
    if (started) return;
    started = true;
    observer?.disconnect();
    root.dataset.state = 'loading';
    button.disabled = true;
    status.textContent = 'Loading the 3D model.';
    try {
      const { mount } = await import(root.dataset.modelSrc);
      await mount(root);
      if (root.dataset.state === 'no-webgl') status.textContent = 'The 3D model needs WebGL, which this browser has not started. The photograph and the keypad above are the same machine.';
    } catch (error) {
      root.dataset.state = 'failed';
      status.textContent = `The 3D model could not load: ${error.message}`;
    }
  };

  button.addEventListener('click', load);
  if ('IntersectionObserver' in window) {
    observer = new IntersectionObserver((entries) => { if (entries.some((e) => e.isIntersecting)) load(); }, { rootMargin: NEAR });
    observer.observe(root);
  }
}

function tabs(list) {
  const all = [...list.querySelectorAll('[role="tab"]')];
  const select = (tab) => {
    for (const t of all) {
      const on = t === tab;
      t.setAttribute('aria-selected', String(on));
      t.setAttribute('tabindex', on ? '0' : '-1');
      document.getElementById(t.getAttribute('aria-controls')).hidden = !on;
    }
  };
  for (const tab of all) {
    tab.addEventListener('click', () => select(tab));
    tab.addEventListener('keydown', (e) => {
      const i = all.indexOf(tab);
      const to = { ArrowLeft: all[(i + all.length - 1) % all.length], ArrowRight: all[(i + 1) % all.length], Home: all[0], End: all.at(-1), Enter: tab, ' ': tab }[e.key];
      if (!to) return;
      e.preventDefault();
      select(to);
      to.focus();
    });
  }
}
