// The chip page: a 3D diagram of the 6502, lit by a trace of this project's
// core. Bundled by scripts/build-chip.mjs into public/chip.js, a same-origin
// module, because the site's CSP allows no inline script and no other origin.
//
// Every value the scene shows comes from src/data/chip-trace.json. Nothing is
// animated by hand: a pad lights because the core put a 1 on that line, a
// register flashes because the instruction changed it.

import * as THREE from 'three';
import CameraControls from 'camera-controls';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { CSS2DRenderer, CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import trace from '../data/chip-trace.json';
import { DIE, PADS, COLUMNS, BLOCKS, WIRES, STOPS, FLAGS, ROWS, rowZ, padLabel } from './layout.mjs';
import { frames, bits, hex } from './trace.mjs';

CameraControls.install({ THREE });

const root = document.querySelector('[data-chip]');
if (root) start(root);

function start(root) {
  const stage = root.querySelector('[data-stage]');
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  let renderer;
  try {
    renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
  } catch {
    root.dataset.state = 'no-webgl';
    return;
  }
  root.dataset.state = 'running';

  // Colours are the site's own tokens, read from the stylesheet, so the scene
  // and the page cannot drift apart.
  const css = getComputedStyle(document.documentElement);
  const token = (name) => new THREE.Color(css.getPropertyValue(`--${name}`).trim());
  const C = {
    void: token('void'), iron: token('iron'), veil: token('veil'), card: token('card'),
    circuit: token('circuit'), pine: token('pine'), lime: token('lime'),
    white: token('white'), moss: token('moss-70'), sage: token('sage-60'),
  };

  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 0.92;
  stage.appendChild(renderer.domElement);
  renderer.domElement.setAttribute('aria-hidden', 'true');

  const labels = new CSS2DRenderer();
  labels.domElement.className = 'die-labels';
  stage.appendChild(labels.domElement);

  const scene = new THREE.Scene();
  scene.background = C.void;
  scene.fog = new THREE.FogExp2(C.void, 0.0105);
  scene.add(new THREE.AmbientLight(C.white, 0.35));
  const key = new THREE.DirectionalLight(C.white, 1.1);
  key.position.set(-20, 40, 25);
  scene.add(key);

  const camera = new THREE.PerspectiveCamera(42, 1, 0.1, 400);
  const controls = new CameraControls(camera, renderer.domElement);
  controls.minDistance = 5;
  controls.maxDistance = 95;
  controls.maxPolarAngle = Math.PI * 0.46;
  controls.smoothTime = reduced ? 0 : 0.6;
  controls.draggingSmoothTime = 0.12;

  // Glowing parts ease towards their target brightness each frame, so a
  // value that changes for one cycle still reads as a pulse.
  const glows = [];
  const glow = (material, base, lit, decay = 6) => {
    const g = { material, base, lit: lit.clone().multiplyScalar(0.8), level: 0, target: 0, decay };
    material.emissive = base.clone();
    glows.push(g);
    return g;
  };

  // Every part glows faintly at its edges where it turns away from the camera
  // (a Fresnel rim), which is most of what makes the parts read as solid and lit.
  const rimColor = { value: C.moss };
  const withRim = (material) => {
    material.onBeforeCompile = (shader) => {
      shader.uniforms.rimColor = rimColor;
      shader.fragmentShader = 'uniform vec3 rimColor;\n' + shader.fragmentShader.replace(
        '#include <emissivemap_fragment>',
        `#include <emissivemap_fragment>
        float rimF = pow(1.0 - saturate(dot(normalize(vNormal), normalize(vViewPosition))), 3.0);
        totalEmissiveRadiance += rimColor * rimF * 0.55;`,
      );
    };
    return material;
  };

  const box = (w, h, d, color, opts = {}) =>
    new THREE.Mesh(
      new RoundedBoxGeometry(w, h, d, 2, Math.max(0.01, Math.min(0.16, Math.min(w, h, d) / 2 - 0.005))),
      withRim(new THREE.MeshStandardMaterial({ color, roughness: 0.5, metalness: 0.4, ...opts })),
    );

  // Outlines are drawn on the plain box, so the rounded corners add no lines.
  const outline = (mesh, color, opacity = 0.9) => {
    const { width, height, depth } = mesh.geometry.parameters;
    const lines = new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.BoxGeometry(width, height, depth)), new THREE.LineBasicMaterial({ color, transparent: true, opacity }));
    mesh.add(lines);
    return lines;
  };

  const label = (text, at, kind = 'block') => {
    const el = document.createElement('span');
    el.className = `die-label die-label-${kind}`;
    el.textContent = text;
    const obj = new CSS2DObject(el);
    obj.position.set(...at);
    obj.userData.kind = kind;
    scene.add(obj);
    return obj;
  };

  // The die.
  const die = box(DIE.width, DIE.thickness, DIE.depth, C.iron, { roughness: 0.8, metalness: 0.1 });
  die.position.y = -DIE.thickness / 2;
  scene.add(die);
  outline(die, C.circuit, 0.8);
  const grid = new THREE.GridHelper(DIE.depth, 44, C.pine, C.pine);
  grid.scale.x = DIE.width / DIE.depth;
  grid.position.y = 0.01;
  grid.material.transparent = true;
  grid.material.opacity = 0.18;
  scene.add(grid);

  // The floor: a fine grid that fades out with distance, and a ring that
  // spreads outward from the chip every few seconds.
  const floorUniforms = { uTime: { value: 0 }, uColor: { value: C.pine } };
  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(220, 220),
    new THREE.ShaderMaterial({
      uniforms: floorUniforms,
      transparent: true,
      depthWrite: false,
      vertexShader: 'varying vec2 vP; void main() { vP = position.xy; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
      fragmentShader: `
        varying vec2 vP; uniform float uTime; uniform vec3 uColor;
        void main() {
          float d = length(vP) / 90.0;
          vec2 g = abs(fract(vP / 4.0 - 0.5) - 0.5) / fwidth(vP / 4.0);
          float line = 1.0 - min(min(g.x, g.y), 1.0);
          float ring = exp(-pow((d - fract(uTime * 0.06)) * 22.0, 2.0)) * (1.0 - fract(uTime * 0.06));
          float fade = smoothstep(1.0, 0.1, d);
          gl_FragColor = vec4(uColor * (0.9 + ring * 3.0), (line * 0.28 + ring * 0.4) * fade);
        }`,
    }),
  );
  floor.rotation.x = -Math.PI / 2;
  floor.position.y = -DIE.thickness - 0.05;
  scene.add(floor);

  // Motes of light drifting up through the scene.
  const MOTES = 420;
  const motePositions = new Float32Array(MOTES * 3);
  const moteSpeed = new Float32Array(MOTES);
  for (let i = 0; i < MOTES; i++) {
    motePositions.set([(Math.random() - 0.5) * 110, Math.random() * 34, (Math.random() - 0.5) * 110], i * 3);
    moteSpeed[i] = 0.4 + Math.random() * 1.2;
  }
  const moteGeometry = new THREE.BufferGeometry();
  moteGeometry.setAttribute('position', new THREE.BufferAttribute(motePositions, 3));
  const motes = new THREE.Points(moteGeometry, new THREE.PointsMaterial({ color: C.moss, size: 0.2, transparent: true, opacity: 0.55, depthWrite: false, blending: THREE.AdditiveBlending }));
  scene.add(motes);

  // Focus brackets: four corners that slide to frame the part being visited.
  const bracket = { x: 0, z: 0, w: 42, d: 46, tx: 0, tz: 0, tw: 42, td: 46 };
  const bracketGeometry = new THREE.BufferGeometry();
  bracketGeometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(8 * 2 * 3), 3));
  const bracketLines = new THREE.LineSegments(bracketGeometry, new THREE.LineBasicMaterial({ color: C.white, transparent: true, opacity: 0.9 }));
  bracketLines.frustumCulled = false;
  scene.add(bracketLines);
  const placeBrackets = () => {
    const a = bracketGeometry.attributes.position;
    const hw = bracket.w / 2 + 0.6, hd = bracket.d / 2 + 0.6;
    const L = Math.min(4, bracket.w * 0.22, bracket.d * 0.22);
    let k = 0;
    for (const [sx, sz] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) {
      const cx = bracket.x + sx * hw, cz = bracket.z + sz * hd;
      a.setXYZ(k++, cx, 0.12, cz); a.setXYZ(k++, cx - sx * L, 0.12, cz);
      a.setXYZ(k++, cx, 0.12, cz); a.setXYZ(k++, cx, 0.12, cz - sz * L);
    }
    a.needsUpdate = true;
  };
  placeBrackets();

  // Pads.
  const padGlow = new Map();
  for (const p of PADS) {
    const m = box(2, 0.3, 2, C.card, { metalness: 0.7, roughness: 0.3 });
    m.position.set(p.x, 0.15, p.z);
    scene.add(m);
    outline(m, C.circuit);
    padGlow.set(p.name + p.pin, glow(m.material, C.void, C.white));
    label(padLabel(p.name), [p.x, 0.9, p.z], 'pad');
  }
  const padsNamed = (name) => PADS.filter((p) => p.name === name).map((p) => padGlow.get(p.name + p.pin));

  // Wires: one mesh per segment, one material per wire so each lights alone.
  const wires = new Map();
  for (const w of WIRES) {
    const material = new THREE.MeshStandardMaterial({ color: C.pine, roughness: 0.4, metalness: 0.6 });
    const g = glow(material, C.void, w.bus === 'data' ? C.white : C.moss, 8);
    const path = w.points.map(([x, z]) => new THREE.Vector3(x, 0.06, z));
    for (let i = 1; i < path.length; i++) {
      const a = path[i - 1], b = path[i];
      const len = a.distanceTo(b);
      if (len < 1e-3) continue;
      const seg = new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.05, len + 0.09), material);
      seg.position.copy(a).add(b).multiplyScalar(0.5);
      seg.lookAt(b.x, seg.position.y, b.z);
      scene.add(seg);
    }
    const curve = new THREE.CurvePath();
    for (let i = 1; i < path.length; i++) if (path[i - 1].distanceTo(path[i]) > 1e-3) curve.add(new THREE.LineCurve3(path[i - 1], path[i]));
    wires.set(w.id, { glow: g, curve });
  }

  // Blocks above the datapath.
  const blocks = new Map();
  for (const b of BLOCKS) {
    const m = box(b.width, b.height, b.depth, C.veil, { transparent: true, opacity: 0.92 });
    m.position.set(b.x, b.height / 2, b.z);
    scene.add(m);
    const edges = outline(m, C.circuit);
    blocks.set(b.id, { glow: glow(m.material, C.void, C.moss, 4), edges });
    label(b.label, [b.x, b.height + 0.6, b.z - b.depth / 2 + 0.8]);
  }

  // The status register's cells, in the flags block.
  const flagBlock = BLOCKS.find((b) => b.id === 'flags');
  const flagCells = FLAGS.map((name, i) => {
    const m = box(0.55, 0.3, 2.2, C.card);
    m.position.set(flagBlock.x - flagBlock.width / 2 + 0.45 + i * 0.67, flagBlock.height + 0.15, flagBlock.z + 0.9);
    scene.add(m);
    label(name, [m.position.x, m.position.y + 0.5, m.position.z + 1.6], 'flag');
    return glow(m.material, C.void, C.white, 10);
  });

  // The datapath: a column per register, a cell per bit.
  const columns = new Map();
  for (const col of COLUMNS) {
    const top = rowZ(7) - 1, bottom = rowZ(0) + 1;
    const frame = box(col.width + 0.3, 0.2, bottom - top + 0.3, C.veil);
    frame.position.set(col.x, 0.1, (top + bottom) / 2);
    scene.add(frame);
    const edges = outline(frame, C.circuit);
    const flash = glow(edges.material.clone(), C.circuit, C.lime, 3);
    edges.material = flash.material;
    edges.material.color = C.circuit.clone();
    const entry = { flash, cells: [], edges };
    if (col.reg) {
      for (let b = 0; b < ROWS; b++) {
        const m = box(col.width - 0.3, 0.55, 1.5, C.card);
        m.position.set(col.x, 0.48, rowZ(b));
        scene.add(m);
        entry.cells.push(glow(m.material, C.void, C.white, 14));
      }
    } else {
      const m = box(col.width - 0.2, 1.4, bottom - top - 0.6, C.card, { metalness: 0.5 });
      m.position.set(col.x, 0.8, (top + bottom) / 2);
      scene.add(m);
      outline(m, C.circuit);
      entry.alu = glow(m.material, C.void, C.moss, 5);
    }
    columns.set(col.id, entry);
    label(col.label, [col.x, 1.4, top - 0.4], 'column');
  }

  // Pulses: small bright beads that run along a wire for one cycle.
  const beads = [];
  const beadGeometry = new THREE.SphereGeometry(0.16, 12, 8);
  const beadMaterial = new THREE.MeshBasicMaterial({ color: C.lime.clone().multiplyScalar(2.2) });
  const pulse = (wireId, outward, seconds) => {
    if (reduced) return;
    const w = wires.get(wireId);
    if (!w) return;
    let bead = beads.find((x) => !x.mesh.visible);
    if (!bead) {
      if (beads.length >= 64) return;
      bead = { mesh: new THREE.Mesh(beadGeometry, beadMaterial) };
      scene.add(bead.mesh);
      beads.push(bead);
    }
    Object.assign(bead, { curve: w.curve, t: 0, outward, seconds: Math.max(0.12, Math.min(seconds, 0.7)) });
    bead.mesh.visible = true;
  };

  // The trace.
  const all = frames(trace);
  let index = 0;
  let previous = null;

  const hud = {
    cycle: root.querySelector('[data-hud-cycle]'),
    text: root.querySelector('[data-hud-text]'),
    bus: root.querySelector('[data-hud-bus]'),
    regs: root.querySelector('[data-hud-regs]'),
  };
  const scrub = root.querySelector('[data-scrub]');
  scrub.max = String(all.length - 1);

  const lit = (g, on) => { g.target = on ? 1 : 0; };
  const kick = (g) => { g.level = 1; g.target = 0; };

  function show(i, animate) {
    const f = all[i];
    const prev = previous;
    const secs = 1 / speed;

    bits(f.address, 16).forEach((v, b) => {
      for (const g of padsNamed(`A${b}`)) lit(g, v);
      lit(wires.get(`A${b}`).glow, v);
      if (animate && prev && ((prev.address >> b) & 1) !== v) pulse(`A${b}`, true, secs);
    });
    bits(f.data).forEach((v, b) => {
      for (const g of padsNamed(`D${b}`)) lit(g, v);
      lit(wires.get(`D${b}`).glow, v);
      if (animate && v) pulse(`D${b}`, f.write, secs);
    });
    for (const g of padsNamed('RW')) lit(g, !f.write); // R/W is high for a read.
    lit(wires.get('RW').glow, !f.write);
    for (const g of padsNamed('SYNC')) lit(g, f.fetch);
    lit(wires.get('SYNC').glow, f.fetch);
    for (const g of padsNamed('PHI0')) kick(g);
    kick(wires.get('PHI0').glow);

    lit(blocks.get('decoder').glow, f.fetch);
    kick(blocks.get('control').glow);
    kick(blocks.get('timing').glow);
    columns.get('ALU').alu.target = f.alu ? 0.85 : 0;

    for (const col of COLUMNS) {
      const entry = columns.get(col.id);
      if (!col.reg) continue;
      bits(f.regs[col.reg]).forEach((v, b) => lit(entry.cells[b], v));
    }
    for (const r of f.changed) {
      const id = { a: 'A', x: 'X', y: 'Y', s: 'S' }[r];
      if (id) kick(columns.get(id).flash);
      if (r === 'p') kick(blocks.get('flags').glow);
    }
    const p = bits(f.regs.p);
    FLAGS.forEach((_, i2) => lit(flagCells[i2], p[7 - i2]));

    hud.cycle.textContent = `cycle ${i + 1} of ${all.length}`;
    hud.text.textContent = `${hex(f.pc, 4)}  ${f.text}`;
    hud.bus.textContent = `${f.write ? 'write' : 'read'} ${hex(f.address, 4)} ${f.write ? '<-' : '->'} ${hex(f.data, 2)}${f.fetch ? '  opcode fetch' : ''}`;
    const r = f.regs;
    hud.regs.textContent = `A ${hex(r.a, 2)}  X ${hex(r.x, 2)}  Y ${hex(r.y, 2)}  S ${hex(r.s, 2)}  P ${hex(r.p, 2)}`;
    scrub.value = String(i);
    previous = f;
  }

  // Playback.
  const playButton = root.querySelector('[data-play]');
  const speedSelect = root.querySelector('[data-speed]');
  let speed = Number(speedSelect.value);
  let playing = false;
  let carry = 0;
  const setPlaying = (on) => {
    playing = on;
    playButton.textContent = on ? 'Pause' : 'Play';
    playButton.setAttribute('aria-pressed', String(on));
  };
  const go = (i, animate = false) => {
    index = (i + all.length) % all.length;
    show(index, animate);
  };
  playButton.addEventListener('click', () => setPlaying(!playing));
  root.querySelector('[data-step]').addEventListener('click', () => { setPlaying(false); go(index + 1, true); });
  root.querySelector('[data-back]').addEventListener('click', () => { setPlaying(false); go(index - 1); });
  speedSelect.addEventListener('change', () => { speed = Number(speedSelect.value); });
  scrub.addEventListener('input', () => { setPlaying(false); go(Number(scrub.value)); });
  stage.addEventListener('keydown', (e) => {
    if (e.key === 'ArrowRight') { setPlaying(false); go(index + 1, true); e.preventDefault(); }
    if (e.key === 'ArrowLeft') { setPlaying(false); go(index - 1); e.preventDefault(); }
    if (e.key === ' ') { setPlaying(!playing); e.preventDefault(); }
  });

  // The tour.
  const stopButtons = [...root.querySelectorAll('[data-stop]')];
  const stopTexts = [...root.querySelectorAll('[data-stop-text]')];
  let lastVisit = 0;
  let lastInput = 0;
  let dragging = false;
  controls.addEventListener('controlstart', () => { dragging = true; });
  controls.addEventListener('controlend', () => { dragging = false; lastInput = performance.now(); });
  const items = [...root.querySelectorAll('[data-item]')];
  const number = root.querySelector('[data-stop-number]');
  const visit = (id, smooth = !reduced) => {
    const at = Math.max(0, STOPS.findIndex((x) => x.id === id));
    const s = STOPS[at];
    controls.setLookAt(...s.camera, ...s.target, smooth);
    [bracket.tx, bracket.tz, bracket.tw, bracket.td] = s.focus;
    lastVisit = performance.now();
    if (!smooth) Object.assign(bracket, { x: bracket.tx, z: bracket.tz, w: bracket.tw, d: bracket.td });
    for (const b of stopButtons) b.setAttribute('aria-current', String(b.dataset.stop === s.id));
    for (const t of stopTexts) t.hidden = t.dataset.stopText !== s.id;
    items.forEach((li, i) => { li.dataset.state = i < at ? 'done' : i === at ? 'current' : 'todo'; });
    number.textContent = String(at + 1);
    current = s.id;
  };
  let current = STOPS[0].id;
  for (const b of stopButtons) b.addEventListener('click', () => visit(b.dataset.stop));
  const move = (d) => {
    const i = STOPS.findIndex((s) => s.id === current);
    visit(STOPS[(i + d + STOPS.length) % STOPS.length].id);
  };
  root.querySelector('[data-next]').addEventListener('click', () => move(1));
  root.querySelector('[data-prev]').addEventListener('click', () => move(-1));

  // Rendering.
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));
  const bloom = new UnrealBloomPass(new THREE.Vector2(1, 1), 0.65, 0.4, 0.78);
  composer.addPass(bloom);
  // The last pass: a vignette, a little colour fringing towards the corners
  // and fine grain, so the picture feels like a lens and not a diagram.
  const look = new ShaderPass({
    uniforms: { tDiffuse: { value: null }, uTime: { value: 0 } },
    vertexShader: 'varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: `
      uniform sampler2D tDiffuse; uniform float uTime; varying vec2 vUv;
      void main() {
        vec2 c = vUv - 0.5; float r = dot(c, c);
        vec2 off = c * r * 0.005;
        vec3 col = vec3(texture2D(tDiffuse, vUv + off).r, texture2D(tDiffuse, vUv).g, texture2D(tDiffuse, vUv - off).b);
        col *= 1.0 - smoothstep(0.12, 0.7, r) * 0.75;
        float n = fract(sin(dot(vUv * vec2(1920.0, 1080.0) + uTime, vec2(12.9898, 78.233))) * 43758.5453);
        col += (n - 0.5) * 0.022 * smoothstep(0.0, 0.25, max(col.r, max(col.g, col.b)));
        gl_FragColor = vec4(col, 1.0);
      }`,
  });
  composer.addPass(look);
  composer.addPass(new OutputPass());

  const resize = () => {
    const { clientWidth: w, clientHeight: h } = stage;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    composer.setSize(w, h);
    labels.setSize(w, h);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  };
  new ResizeObserver(resize).observe(stage);
  resize();

  let visible = true;
  new IntersectionObserver(([e]) => { visible = e.isIntersecting; }).observe(stage);

  let last = performance.now();
  const scratch = new THREE.Vector3();
  const tick = () => {
    requestAnimationFrame(tick);
    const now = performance.now();
    const dt = Math.min((now - last) / 1000, 0.1);
    last = now;
    if (!visible || document.hidden) return;

    if (playing) {
      carry += dt * speed;
      while (carry >= 1) { carry -= 1; go(index + 1, true); }
    }

    for (const g of glows) {
      const k = 1 - Math.exp(-g.decay * dt);
      g.level += (g.target - g.level) * k;
      g.material.emissive.copy(g.base).lerp(g.lit, g.level);
      if (g.material.isLineBasicMaterial) g.material.color.copy(g.base).lerp(g.lit, g.level);
    }
    for (const b of beads) {
      if (!b.mesh.visible) continue;
      b.t += dt / b.seconds;
      if (b.t >= 1) { b.mesh.visible = false; continue; }
      b.curve.getPointAt(b.outward ? b.t : 1 - b.t, scratch);
      b.mesh.position.copy(scratch).setY(0.18);
    }

    // Time-driven parts: the floor's ring, the grain, the drifting motes, the
    // brackets sliding to their target and breathing.
    floorUniforms.uTime.value += dt;
    look.uniforms.uTime.value = (now / 1000) % 100;
    if (!reduced) {
      const m = moteGeometry.attributes.position;
      for (let i = 0; i < MOTES; i++) {
        let y = m.getY(i) + moteSpeed[i] * dt;
        if (y > 34) y = 0;
        m.setY(i, y);
      }
      m.needsUpdate = true;
    }
    const slide = 1 - Math.exp(-7 * dt);
    for (const k of ['x', 'z', 'w', 'd']) bracket[k] += (bracket['t' + k] - bracket[k]) * slide;
    placeBrackets();
    bracketLines.material.opacity = reduced ? 0.8 : 0.65 + 0.3 * Math.sin(now / 420);

    // Left alone on the overview, the camera drifts slowly round the chip.
    if (!reduced && current === 'overview' && !dragging && now - lastInput > 5000 && now - lastVisit > 4000) controls.rotate(dt * 0.07, 0, false);

    controls.update(dt);
    // Pad and flag labels only when close enough to read.
    const near = controls.distance < 60;
    scene.traverse((o) => { if (o.isCSS2DObject && o.userData.kind !== 'block' && o.userData.kind !== 'column') o.visible = near; });
    composer.render();
    labels.render(scene, camera);
  };

  visit(STOPS[0].id, false);
  go(0);
  if (!reduced) setPlaying(true);
  tick();
}
