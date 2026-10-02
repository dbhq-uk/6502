// The stage every machine's 3D model shares: the renderer, the camera and its
// controls (camera-controls, as /inside/ uses), the keyboard, the reset button
// and a render loop that draws only while the model is on screen. The machine
// itself, its board or its case, is drawn by that machine's own module.
//
// The controls are the usual ones, and they never trap the page:
//   mouse     left-drag turns, right-drag or shift-left-drag pans, and the wheel
//             zooms to the cursor only while the model has focus (click it, or
//             tab to it) or with ctrl or cmd held. Otherwise the wheel scrolls
//             the page.
//   touch     one finger scrolls the page until a tap focuses the model. Then
//             one finger turns, two fingers pinch-zoom and pan. The canvas is
//             touch-action none only while focused (data-active on the stage).
//   keyboard  arrows turn, shift and arrows pan, plus and minus zoom, Home
//             resets, Escape lets go of the model.
//   either    a double click or double tap on empty space resets the view.
// The target is held inside `bounds`, so the board cannot be lost off screen,
// and the camera may go all the way round, under the board as well as over it.
//
// Colours are the site's tokens, read from the stylesheet when the model
// starts, so the scene holds no colour of its own (tests/model.test.mjs).
//
// three.js is imported by name, not as a namespace, so the bundler keeps only
// the parts a model uses; camera-controls is given just the classes it needs.

import {
  WebGLRenderer, Scene, PerspectiveCamera, AmbientLight, DirectionalLight, HemisphereLight, Color, SRGBColorSpace, ACESFilmicToneMapping,
  Vector2, Vector3, Vector4, Quaternion, Matrix4, Spherical, Box3, Sphere, Raycaster, MathUtils,
} from 'three';
import CameraControls from 'camera-controls';

CameraControls.install({ THREE: { Vector2, Vector3, Vector4, Quaternion, Matrix4, Spherical, Box3, Sphere, Raycaster } });

/**
 * Builds the stage inside `root` (the model section). `view` is the starting
 * camera as [x, y, z, targetX, targetY, targetZ]; `bounds` is the box the
 * camera's target is held in, as [minX, minY, minZ, maxX, maxY, maxZ]. Returns null when WebGL
 * cannot start, having marked the section so the page can say so.
 */
export function createStage(root, { view, bounds, minDistance = 3, maxDistance = 150, maxPolarAngle = Math.PI }) {
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
  // The camera may go under the board, so the underside is lit too: a
  // hemisphere light whose ground colour falls on faces that look down, and a
  // second fill from below, from the opposite side to the key.
  scene.add(new HemisphereLight(token('white'), token('moss-70'), 0.5));
  const under = new DirectionalLight(token('white'), 0.9);
  under.position.set(12, -28, -14);
  scene.add(under);

  const camera = new PerspectiveCamera(36, 1, 0.1, 400);
  const canvas = renderer.domElement;
  const controls = new CameraControls(camera, canvas);
  controls.minDistance = minDistance;
  controls.maxDistance = maxDistance;
  controls.maxPolarAngle = maxPolarAngle;
  if (bounds) controls.setBoundary(new Box3(new Vector3(...bounds.slice(0, 3)), new Vector3(...bounds.slice(3))));
  // The wheel is ours, not the library's: it must not zoom while the page is
  // meant to scroll, and it zooms to the cursor (see `zoomAt` below).
  controls.mouseButtons.wheel = CameraControls.ACTION.NONE;
  // No auto-rotation anywhere: the model moves only when the visitor moves it.
  // With reduced motion, moves land at once rather than easing.
  controls.smoothTime = reduced ? 0 : 0.35;
  controls.draggingSmoothTime = reduced ? 0 : 0.1;

  const reset = (smooth = !reduced) => controls.setLookAt(...view, smooth);
  reset(false);
  root.querySelector('[data-model-reset]')?.addEventListener('click', () => reset());

  // Focus decides whether the model takes the wheel and the touch. Without it
  // the wheel scrolls the page and one finger scrolls the page; with it the
  // wheel zooms and one finger turns the model, two fingers pinch and pan.
  // camera-controls sets touch-action none on its canvas itself, so the
  // canvas's own style is set here, after it, and kept in step with focus.
  const { ACTION } = CameraControls;
  const touchesWhenFocused = { one: ACTION.TOUCH_ROTATE, two: ACTION.TOUCH_DOLLY_TRUCK, three: ACTION.TOUCH_TRUCK };
  const setActive = (active) => {
    stage.toggleAttribute('data-active', active);
    Object.assign(controls.touches, active ? touchesWhenFocused : { one: ACTION.NONE, two: ACTION.NONE, three: ACTION.NONE });
    canvas.style.touchAction = active ? 'none' : 'pan-y pinch-zoom';
  };
  setActive(false);
  stage.addEventListener('focus', () => setActive(true));
  stage.addEventListener('blur', () => setActive(false));

  // The wheel, zooming to the point under the cursor: the camera and its
  // target are scaled about the point where the cursor's ray meets the board's
  // plane, so that point stays under the cursor. Distance limits and the
  // target's bounds still hold.
  const ray = new Raycaster();
  const ndc = new Vector2();
  const aim = new Vector3();
  const spherical = new Spherical();
  const zoomAt = (e) => {
    const lines = e.deltaMode === 1 ? 16 : e.deltaMode === 2 ? innerHeight : 1;
    // A trackpad pinch arrives as ctrl and the wheel in small steps, and needs a larger gain than a wheel notch.
    const gain = e.ctrlKey && Math.abs(e.deltaY * lines) < 40 ? 0.01 : 0.0012;
    const factor = Math.exp(e.deltaY * lines * gain);
    const was = controls.getSpherical(spherical, true).radius;
    const next = MathUtils.clamp(was * factor, controls.minDistance, controls.maxDistance);
    const rect = canvas.getBoundingClientRect();
    ndc.set(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    const { origin, direction } = ray.ray;
    const t = Math.abs(direction.y) > 1e-4 ? -origin.y / direction.y : -1;
    controls.getTarget(aim, true);
    if (t > 0) {
      const hit = origin.clone().addScaledVector(direction, t);
      aim.sub(hit).multiplyScalar(next / was).add(hit);
    }
    controls.moveTo(aim.x, aim.y, aim.z, !reduced);
    controls.dollyTo(next, !reduced);
  };
  canvas.addEventListener('wheel', (e) => {
    if (document.activeElement !== stage && !e.ctrlKey && !e.metaKey) return;
    e.preventDefault();
    zoomAt(e);
  }, { passive: false });

  // The mouse takes the focus when it goes down on the model. A finger does
  // not: it takes it on a tap (below), so a finger that starts a scroll stays a scroll.
  canvas.addEventListener('pointerdown', (e) => {
    if (e.pointerType !== 'mouse') return;
    stage.focus({ preventScroll: true });
    // Shift and the left button pan, for a mouse with no right button.
    controls.mouseButtons.left = e.shiftKey ? ACTION.TRUCK : ACTION.ROTATE;
  }, true);

  // The keyboard, with focus on the model: arrows turn it, shift and arrows
  // pan, plus and minus zoom, Home resets the view, Escape lets go.
  const STEP = MathUtils.degToRad(15);
  stage.addEventListener('keydown', (e) => {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    const smooth = !reduced;
    // From where the camera is heading, not where it is, so keys held down keep adding up.
    const heading = controls.getSpherical(spherical, true).radius;
    const pan = heading * 0.08;
    const moves = {
      ArrowLeft: () => (e.shiftKey ? controls.truck(-pan, 0, smooth) : controls.rotate(-STEP, 0, smooth)),
      ArrowRight: () => (e.shiftKey ? controls.truck(pan, 0, smooth) : controls.rotate(STEP, 0, smooth)),
      ArrowUp: () => (e.shiftKey ? controls.truck(0, -pan, smooth) : controls.rotate(0, -STEP, smooth)),
      ArrowDown: () => (e.shiftKey ? controls.truck(0, pan, smooth) : controls.rotate(0, STEP, smooth)),
      '+': () => controls.dolly(heading * 0.15, smooth),
      '=': () => controls.dolly(heading * 0.15, smooth),
      '-': () => controls.dolly(-heading * 0.15, smooth),
      Home: () => reset(),
      Escape: () => stage.blur(),
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
  // A ray hits hidden objects too (three.js does not check), so the first hit
  // that is shown, with every parent shown, is the one that counts: a model
  // may hide some of its parts.
  const raycaster = new Raycaster();
  const pointer = new Vector2();
  const shown = (o) => { for (; o; o = o.parent) if (!o.visible) return false; return true; };
  const pick = (event, objects) => {
    const rect = canvas.getBoundingClientRect();
    pointer.set(((event.clientX - rect.left) / rect.width) * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1);
    raycaster.setFromCamera(pointer, camera);
    return raycaster.intersectObjects(objects, true).find((hit) => shown(hit.object))?.object ?? null;
  };
  const onClick = (objects, handler) => {
    let down = null;
    canvas.addEventListener('pointerdown', (e) => { down = [e.clientX, e.clientY]; });
    canvas.addEventListener('pointerup', (e) => {
      if (!down || Math.hypot(e.clientX - down[0], e.clientY - down[1]) > 5) return;
      down = null;
      const hit = pick(e, objects());
      if (hit) handler(hit);
    });
    canvas.addEventListener('pointermove', (e) => {
      if (e.buttons) return;
      canvas.style.cursor = pick(e, objects()) ? 'pointer' : '';
    });
  };

  // A tap or click that goes down and up in the same place. A finger's tap
  // focuses the model. Two in a row on empty space, which is the black
  // around the board and not a part of the model, reset the view.
  let tapStart = null;
  let lastTap = null;
  canvas.addEventListener('pointerdown', (e) => {
    tapStart = e.isPrimary && e.button === 0 ? { x: e.clientX, y: e.clientY, at: performance.now() } : null;
  });
  canvas.addEventListener('pointerup', (e) => {
    const tap = tapStart && Math.hypot(e.clientX - tapStart.x, e.clientY - tapStart.y) <= 5 && performance.now() - tapStart.at < 500;
    tapStart = null;
    if (!tap) { lastTap = null; return; }
    if (e.pointerType !== 'mouse') stage.focus({ preventScroll: true });
    if (pick(e, scene.children)) { lastTap = null; return; }
    const now = performance.now();
    if (lastTap && now - lastTap.at < 350 && Math.hypot(e.clientX - lastTap.x, e.clientY - lastTap.y) < 30) {
      lastTap = null;
      reset();
    } else {
      lastTap = { x: e.clientX, y: e.clientY, at: now };
    }
  });

  // What a test can read: where the camera is heading (the values it eases
  // towards, which are exact), whether it is still easing there (to within a
  // fiftieth of a centimetre or a radian), and the box the
  // target is held in.
  const there = new Vector3();
  const here = new Vector3();
  const goal = new Spherical();
  const now = new Spherical();
  root.modelView = () => {
    controls.getSpherical(goal, true);
    controls.getSpherical(now, false);
    controls.getTarget(there, true);
    controls.getTarget(here, false);
    const moving = Math.abs(goal.radius - now.radius) + Math.abs(goal.phi - now.phi) + Math.abs(goal.theta - now.theta) + here.distanceTo(there) > 0.02;
    return {
      polar: goal.phi, azimuth: goal.theta, distance: goal.radius, target: there.toArray(),
      moving, bounds, focused: document.activeElement === stage,
    };
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
