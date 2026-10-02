// Loads a machine's 3D model, and three.js with it, only when the visitor gets
// near it: when the model's section comes within a screen's reach of being in
// view, or when they press "Load the 3D model". Until then the page carries
// none of it. An external module because the site's CSP allows no inline
// script.
//
// Without JavaScript none of this runs and the section stays hidden: the
// photograph and the page's own keypad are the machine without it.

const NEAR = '600px 0px';

for (const root of document.querySelectorAll('[data-model]')) setup(root);

function setup(root) {
  const button = root.querySelector('[data-model-load]');
  const status = root.querySelector('[data-model-status]');
  root.hidden = false;

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
