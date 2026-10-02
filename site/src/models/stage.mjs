// The stage every machine's 3D model shares: the renderer, the camera and its
// controls (camera-controls, as /inside/ uses), the keyboard, the reset button
// and a render loop that draws only while the model is on screen. The machine
// itself, its board or its case, is drawn by that machine's own module.
//
// Colours are the site's tokens, read from the stylesheet when the model
// starts, so the scene holds no colour of its own (tests/model.test.mjs).
//
// three.js is imported by name, not as a namespace, so the bundler keeps only
// the parts a model uses; camera-controls is given just the classes it needs.

import {
  WebGLRenderer, Scene, PerspectiveCamera, AmbientLight, DirectionalLight, Color, SRGBColorSpace, ACESFilmicToneMapping,
  Vector2, Vector3, Vector4, Quaternion, Matrix4, Spherical, Box3, Sphere, Raycaster, MathUtils,
} from 'three';
import CameraControls from 'camera-controls';

CameraControls.install({ THREE: { Vector2, Vector3, Vector4, Quaternion, Matrix4, Spherical, Box3, Sphere, Raycaster } });

/**
 * Builds the stage inside `root` (the model section). `view` is the starting
 * camera as [x, y, z, targetX, targetY, targetZ]. Returns null when WebGL
 * cannot start, having marked the section so the page can say so.
 */
export function createStage(root, { view, minDistance = 10, maxDistance = 120, maxPolarAngle = Math.PI * 0.48 }) {
  const stage = root.querySelector('[data-model-stage]');
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  let renderer;
  try {
    renderer = new WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
  } catch {
    root.dataset.state = 'no-webgl';
    return null;
  }

  const css = getComputedStyle(document.documentElement);
  const token = (name) => {
    const value = css.getPropertyValue(`--${name}`).trim();
    if (!value) throw new Error(`the stylesheet has no --${name}`);
    return new Color(value);
  };

  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.outputColorSpace = SRGBColorSpace;
  renderer.toneMapping = ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.0;
  renderer.domElement.setAttribute('aria-hidden', 'true');
  renderer.domElement.dataset.modelCanvas = '';
  stage.prepend(renderer.domElement);

  const scene = new Scene();
  scene.background = token('void');
  scene.add(new AmbientLight(token('white'), 0.7));
  const key = new DirectionalLight(token('white'), 1.6);
  key.position.set(-14, 30, 18);
  scene.add(key);
  const fill = new DirectionalLight(token('mint'), 0.35);
  fill.position.set(20, 12, -16);
  scene.add(fill);

  const camera = new PerspectiveCamera(36, 1, 0.1, 400);
  const controls = new CameraControls(camera, renderer.domElement);
  controls.minDistance = minDistance;
  controls.maxDistance = maxDistance;
  controls.maxPolarAngle = maxPolarAngle;
  // No auto-rotation anywhere: the model moves only when the visitor moves it.
  // With reduced motion, moves land at once rather than easing.
  controls.smoothTime = reduced ? 0 : 0.35;
  controls.draggingSmoothTime = reduced ? 0 : 0.1;

  const reset = (smooth = !reduced) => controls.setLookAt(...view, smooth);
  reset(false);
  root.querySelector('[data-model-reset]')?.addEventListener('click', () => reset());

  // The keyboard, with focus on the model: arrows turn it, plus and minus zoom.
  const STEP = MathUtils.degToRad(15);
  stage.addEventListener('keydown', (e) => {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    const smooth = !reduced;
    const moves = {
      ArrowLeft: () => controls.rotate(-STEP, 0, smooth),
      ArrowRight: () => controls.rotate(STEP, 0, smooth),
      ArrowUp: () => controls.rotate(0, -STEP, smooth),
      ArrowDown: () => controls.rotate(0, STEP, smooth),
      '+': () => controls.dolly(controls.distance * 0.15, smooth),
      '=': () => controls.dolly(controls.distance * 0.15, smooth),
      '-': () => controls.dolly(-controls.distance * 0.15, smooth),
    };
    if (!moves[e.key]) return;
    moves[e.key]();
    e.preventDefault();
  });

  const resize = () => {
    const { clientWidth: w, clientHeight: h } = stage;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  };
  new ResizeObserver(resize).observe(stage);
  resize();

  // A click on the model, told apart from a drag: the pointer moved less than
  // a few pixels between going down and coming up.
  const raycaster = new Raycaster();
  const pointer = new Vector2();
  const pick = (event, objects) => {
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.set(((event.clientX - rect.left) / rect.width) * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1);
    raycaster.setFromCamera(pointer, camera);
    return raycaster.intersectObjects(objects, true)[0]?.object ?? null;
  };
  const onClick = (objects, handler) => {
    let down = null;
    renderer.domElement.addEventListener('pointerdown', (e) => { down = [e.clientX, e.clientY]; });
    renderer.domElement.addEventListener('pointerup', (e) => {
      if (!down || Math.hypot(e.clientX - down[0], e.clientY - down[1]) > 5) return;
      down = null;
      const hit = pick(e, objects());
      if (hit) handler(hit);
    });
    renderer.domElement.addEventListener('pointermove', (e) => {
      if (e.buttons) return;
      renderer.domElement.style.cursor = pick(e, objects()) ? 'pointer' : '';
    });
  };

  // The loop runs every frame, so what the model shows keeps up with the
  // machine, but it draws only while the model is on screen.
  let visible = true;
  new IntersectionObserver(([e]) => { visible = e.isIntersecting; }).observe(stage);
  const hooks = [];
  let last = performance.now();
  const tick = () => {
    requestAnimationFrame(tick);
    const now = performance.now();
    const dt = Math.min((now - last) / 1000, 0.1);
    last = now;
    for (const hook of hooks) hook(dt, now);
    if (!visible || document.hidden) return;
    controls.update(dt);
    renderer.render(scene, camera);
  };

  return {
    root, stage, scene, camera, renderer, controls, token, reduced, onClick,
    /** Run `fn(dt, now)` every frame, on screen or not. */
    every: (fn) => hooks.push(fn),
    start: () => { root.dataset.state = 'running'; tick(); },
  };
}
