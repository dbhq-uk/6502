// The BBC Micro Model B's case as a 3D model: the outside view on the
// machine's page. Bundled by scripts/build-models.mjs into
// public/models/bbc-micro-case.js, and loaded by public/model-loader.js only
// when its view's panel nears the screen.
//
// A thin entry: all of the drawing is ./bbc-micro-case-scene.mjs, and every
// word and the keys' state is ./bbc-micro-case-layout.mjs. This module puts the
// scene on the shared stage, draws its words once the site's type has loaded,
// and ties it to the running machine and the page. The case is the Service
// Manual's size; the note under the model says what the photographs' measures
// make of it.
//
// It is tied to the running machine, not animated by hand. The page's panel
// (public/bbc-micro.js) announces every key pressed on it as `bbc:key`, from
// the PC keyboard, the on-screen keys or this model: the model keeps which keys
// are down (createKeyState, on the page's clock) and puts their keycaps down
// each frame, SHIFT both of its keycaps. Each frame it lights the three lights
// as `panel.bbc.leds()` says. A click on a keycap presses that key on the
// machine through `panel.bbc.tap`, exactly as the on-screen key does; before
// Start it presses nothing and the status line says why. On `bbc:start` a new
// machine is running, and every key is let go.
//
// What a test can read off the view's root: data-model-pressed (the key the
// last press was of), data-model-presses (how many presses the model has
// seen), data-model-down (the keycaps down, space between) and data-model-leds
// (the lit lights in words, or none). And root.modelKeyPoint(name), where a
// keycap's top is on the screen, and root.modelView(), from the stage.

import { Vector2, Raycaster } from 'three';
import { createStage } from './stage.mjs';
import { STATUS, createKeyState, litLeds, ledWords, press, pointWords } from './bbc-micro-case-layout.mjs';
import { buildCase, stageOptions, atlasItems, drawAtlas } from './bbc-micro-case-scene.mjs';

/** What the pointer or a click is on: a keycap's name or a port's reference, from the object or one it is part of. */
function partOf(object) {
  for (let o = object; o; o = o.parent) {
    if (o.userData?.key) return { key: o.userData.key };
    if (o.userData?.port) return { port: o.userData.port };
  }
  return null;
}

export async function mount(root) {
  const s = createStage(root, stageOptions());
  if (!s) return;
  const { scene, token, every } = s;
  const family = getComputedStyle(document.documentElement).getPropertyValue('--font-body').trim();
  const atlas = await drawAtlas(atlasItems(), token, family);
  const model = buildCase(scene, token, { atlas });
  const status = root.querySelector('[data-model-status]');

  // ---- The machine: the panel on the same page runs it ----
  const panel = document.querySelector('[data-bbc]');
  const bbc = () => panel?.bbc ?? null;
  const keys = createKeyState();
  root.dataset.modelPresses = '0';
  panel?.addEventListener('bbc:key', (e) => {
    if (!keys.on(e.detail, performance.now())) return;
    root.dataset.modelPresses = String(keys.presses);
    if (keys.last) root.dataset.modelPressed = keys.last;
  });
  panel?.addEventListener('bbc:start', () => {
    keys.reset();
    status.textContent = STATUS.running;
  });

  // The keys and the lights, each frame, on screen or not; the scene changes only when they do.
  let downWas = null;
  let litWas = null;
  every((dt, now) => {
    const down = keys.down(now);
    const d = down.join(' ');
    if (d !== downWas) {
      downWas = d;
      model.setKeys(down);
      root.dataset.modelDown = d;
    }
    const lit = litLeds(bbc()?.leds?.() ?? null);
    const words = ledWords(lit);
    if (words !== litWas) {
      litWas = words;
      model.setLeds(lit);
      root.dataset.modelLeds = words;
    }
  });

  // A click on a keycap presses it on the machine (press, in the layout); on a port, names it.
  s.onClick(() => model.pickable, (hit) => {
    for (let o = hit; o; o = o.parent) {
      if (o.userData?.key) { status.textContent = press(o.userData.key, panel); return; }
      if (o.userData?.port) { status.textContent = pointWords({ port: o.userData.port }); return; }
    }
  });

  // Pointing names a keycap, a port or a connector on the status line.
  const ray = new Raycaster();
  const ndc = new Vector2();
  const canvas = s.renderer.domElement;
  let pending = null;
  let pointed = null;
  canvas.addEventListener('pointermove', (e) => {
    if (e.buttons || e.pointerType !== 'mouse' || pending) return;
    pending = requestAnimationFrame(() => {
      pending = null;
      const rect = canvas.getBoundingClientRect();
      ndc.set(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
      ray.setFromCamera(ndc, s.camera);
      const hit = ray.intersectObjects(model.pickable, true)[0];
      const part = hit ? partOf(hit.object) : null;
      const words = part ? pointWords(part) : null;
      if (words && words !== pointed) status.textContent = words;
      pointed = words;
    });
  });

  /** Where a keycap's top is on the screen, where it is now. */
  root.modelKeyPoint = (name) => {
    const v = model.keyTop(name);
    if (!v) return null;
    v.project(s.camera);
    const rect = canvas.getBoundingClientRect();
    return { x: rect.left + ((v.x + 1) / 2) * rect.width, y: rect.top + ((1 - v.y) / 2) * rect.height };
  };

  status.textContent = bbc() ? STATUS.running : STATUS.idle;
  s.start();
}
