// The KIM-1 board as a 3D model, on the machine's page. Bundled by
// scripts/build-models.mjs into public/models/kim-1.js, and loaded by
// public/model-loader.js only when its section nears the screen.
//
// It is our own model, built here from boxes, measured from the photograph on
// the same page (kim-1-layout.mjs says how). It is labelled a model, not a
// photograph, everywhere it is shown.
//
// It is tied to the running machine, not animated by hand. The six digits
// light with the segments the page's drawn display shows, read from the same
// machine through the panel (public/kim-1.js puts that on the panel as
// `panel.kim1`). A click on a key here presses that key on the machine, and a
// key pressed anywhere, here, on the page's keypad or on a keyboard, goes down
// here, because the panel announces every tap with a `kim1:key` event.
//
// The board's top face carries the real board's copper tracks, traced from the
// photograph into a greyscale map (scripts/make-board-tracks.mjs): white is
// copper. The map is loaded with the model, as a same-origin image, and turned
// here into the face's colour (the mask's green and the copper's, both tokens),
// its shine (the copper smoother and a little metallic, the mask matte) and its
// relief (a bump map, so a track stands proud of the mask). Two buttons under
// the model switch the tracks off, and fade the parts out to leave the tracks.
// The underside has no tracks: the photograph does not show it.
//
// What a test can read off the section: data-model-segments and
// data-model-display (what the model's digits show), data-model-pressed and
// data-model-presses (the last key that went down, and how many have),
// data-model-tracks (on or off) and data-model-parts (shown or hidden). And
// root.modelKeyPoint(name), where a key's top is on the screen, so the browser
// check can click one; root.modelBoardPoint(x, y), the same for a point on the
// board in mm; and root.modelLayers(), what the scene is showing.

import {
  BoxGeometry, PlaneGeometry, CylinderGeometry, EdgesGeometry, LineSegments, LineBasicMaterial,
  Mesh, InstancedMesh, Object3D, Group, MeshStandardMaterial, MeshBasicMaterial, CanvasTexture, SRGBColorSpace, Vector3,
} from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { createStage } from './stage.mjs';
import { BOARD, TABS, CONTACTS_PER_TAB, PITCH, CHIPS, RAM, LOGIC, CRYSTAL, NAME, HOLES, DISPLAY, KEYPAD, KEYS, SST, SEGMENTS, TRACKS, decode } from './kim-1-layout.mjs';

// Millimetres on the photograph to the scene's centimetres, centred on the board.
const X = (x) => (x - BOARD.width / 2) / 10;
const Z = (y) => (y - BOARD.depth / 2) / 10;
const S = (mm) => mm / 10;

// Typical heights, in centimetres: a photograph taken from above has none.
const H = { board: S(BOARD.thickness), dip: 0.38, standoff: 0.08, display: 0.45, keypad: 0.55, key: 0.4, travel: 0.16 };

// The mask and the copper, on the top face: how rough and how metallic each is.
// The mask's are the plain board's, so with the tracks off nothing else changes.
const SURFACE = { mask: { roughness: 0.55, metalness: 0.1 }, copper: { roughness: 0.35, metalness: 0.25 } };
// How far a track stands proud, as three.js's bump scale, and how long the parts take to fade.
const RELIEF = 1.5;
const FADE_S = 0.3;

/**
 * The top face's three maps from the track map: colour (mask to copper, in
 * sRGB, from the two tokens), surface (roughness in green and metalness in
 * blue, as three.js reads them) and relief (the map itself, softened so a
 * track's edge is a slope rather than a cliff).
 */
async function trackMaps(src, mask, copper) {
  const img = new Image();
  img.src = src;
  await img.decode();
  const { naturalWidth: w, naturalHeight: h } = img;
  const sheet = () => Object.assign(document.createElement('canvas'), { width: w, height: h });
  const relief = sheet();
  const rg = relief.getContext('2d', { willReadFrequently: true });
  rg.drawImage(img, 0, 0);
  const cover = rg.getImageData(0, 0, w, h).data;
  rg.filter = 'blur(1.5px)';
  rg.drawImage(img, 0, 0);
  const colour = sheet(), surface = sheet();
  const cg = colour.getContext('2d'), sg = surface.getContext('2d');
  const C = cg.createImageData(w, h), F = sg.createImageData(w, h);
  const channels = (c) => { const o = { r: 0, g: 0, b: 0 }; c.getRGB(o, SRGBColorSpace); return [o.r, o.g, o.b].map((v) => Math.round(v * 255)); };
  const m = channels(mask), k = channels(copper);
  const { mask: sm, copper: sc } = SURFACE;
  for (let i = 0; i < cover.length; i += 4) {
    const t = cover[i] / 255;
    for (let c = 0; c < 3; c++) C.data[i + c] = m[c] + (k[c] - m[c]) * t;
    C.data[i + 3] = 255;
    F.data[i] = 255;
    F.data[i + 1] = 255 * (sm.roughness + (sc.roughness - sm.roughness) * t);
    F.data[i + 2] = 255 * (sm.metalness + (sc.metalness - sm.metalness) * t);
    F.data[i + 3] = 255;
  }
  cg.putImageData(C, 0, 0);
  sg.putImageData(F, 0, 0);
  return { colour, surface, relief };
}

export async function mount(root) {
  // The camera's target is held over the board and its tabs, with two
  // centimetres to spare, and a little above and below it: pan as far as the
  // edge, never off it. It may go as close as a chip filling the view, and as
  // far as the whole board small.
  const margin = 2;
  const bounds = [X(-Math.max(...TABS.map((t) => t.out))) - margin, -2, Z(0) - margin, X(BOARD.width) + margin, 2, Z(BOARD.depth) + margin];
  const s = createStage(root, { view: [0, 26, 25, 0, -1, 1.5], bounds, minDistance: 3, maxDistance: 150 });
  if (!s) return;
  const { scene, token, reduced, every } = s;
  const css = getComputedStyle(document.documentElement);
  const hex = (name) => `#${token(name).getHexString()}`;
  const mono = css.getPropertyValue('--font-mono').trim();

  const standard = (name, opts = {}) => new MeshStandardMaterial({ color: token(name), roughness: 0.6, metalness: 0.1, ...opts });
  const M = {
    board: standard('model-pcb', { roughness: 0.55 }),
    under: standard('model-pcb-under', { roughness: 0.55 }),
    gold: standard('model-gold', { roughness: 0.35, metalness: 0.35 }),
    tin: standard('model-tin', { roughness: 0.4, metalness: 0.3 }),
    chip: standard('iron', { roughness: 0.45 }),
    hole: new MeshBasicMaterial({ color: token('void') }),
    window: standard('void', { roughness: 0.15, metalness: 0.3 }),
    keypad: standard('iron', { roughness: 0.7 }),
    key: standard('void', { roughness: 0.25, metalness: 0.2 }),
    knob: standard('moss-70'),
    off: standard('model-led-off', { roughness: 0.4 }),
    on: new MeshBasicMaterial({ color: token('model-led'), toneMapped: false }),
  };

  // The parts stand on the board in a group of their own, so that Show tracks
  // only can fade them out together. The board, its tabs, contacts, holes and
  // the pads underneath are not parts.
  const parts = new Group();
  scene.add(parts);
  const add = (mesh, x, y, z, parent = parts) => { mesh.position.set(x, y, z); parent.add(mesh); return mesh; };
  const fixed = (mesh, x, y, z) => add(mesh, x, y, z, scene);

  // Printed text: a transparent canvas with the words, on a plane just above a part.
  const printed = (text, width, depth, { colour = 'white', size = 0.62, weight = 500 } = {}) => {
    const canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = Math.max(32, Math.round((512 * depth) / width));
    const g = canvas.getContext('2d');
    g.fillStyle = hex(colour);
    g.font = `${weight} ${Math.round(canvas.height * size)}px ${mono}`;
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.fillText(text, canvas.width / 2, canvas.height / 2 + 1);
    const texture = new CanvasTexture(canvas);
    texture.colorSpace = SRGBColorSpace;
    texture.anisotropy = 4;
    const plane = new Mesh(new PlaneGeometry(width, depth), new MeshBasicMaterial({ map: texture, transparent: true, depthWrite: false }));
    plane.rotation.x = -Math.PI / 2;
    return plane;
  };

  // The board and its two tabs, with a hairline round the edge.
  // A box has six faces, in the order +x, -x, +y, -y, +z, -z: the third is the
  // top, which takes the tracks once they have loaded, and the fourth is the
  // underside, which the camera can see, so it has its own material. Each tab
  // has its own copy of the list, so the tracks go on the board alone.
  const faces = [M.board, M.board, M.board, M.under, M.board, M.board];
  const board = fixed(new Mesh(new BoxGeometry(S(BOARD.width), H.board, S(BOARD.depth)), faces), 0, -H.board / 2, 0);
  board.add(new LineSegments(new EdgesGeometry(board.geometry), new LineBasicMaterial({ color: token('circuit') })));
  for (const t of TABS) {
    const tab = fixed(new Mesh(new BoxGeometry(S(t.out), H.board, S(t.y1 - t.y0)), [...faces]), X(-t.out / 2), -H.board / 2, Z((t.y0 + t.y1) / 2));
    tab.add(new LineSegments(new EdgesGeometry(tab.geometry), new LineBasicMaterial({ color: token('circuit') })));
  }
  for (const [x, y] of HOLES) fixed(new Mesh(new CylinderGeometry(S(1.6), S(1.6), H.board + 0.01, 20), M.hole), X(x), -H.board / 2, Z(y));

  // The gold contacts, 22 on each tab, at the connector's pitch, on both faces of the tab.
  const contacts = new InstancedMesh(new BoxGeometry(S(7.5), 0.012, S(2.4)), M.gold, TABS.length * CONTACTS_PER_TAB * 2);
  const place = new Object3D();
  let n = 0;
  for (const t of TABS) {
    for (let i = 0; i < CONTACTS_PER_TAB; i++) {
      for (const y of [0.006, -H.board - 0.006]) {
        place.position.set(X(-t.out + 3.75), y, Z(t.first + i * PITCH));
        place.updateMatrix();
        contacts.setMatrixAt(n++, place.matrix);
      }
    }
  }
  scene.add(contacts);

  // Chips: a dual in-line package of `pins` legs, its long side along x or y.
  const DIP = { 40: { length: 52, body: 13.7, rows: 15.24 }, 16: { length: 19.3, body: 6.4, rows: 7.62 }, 14: { length: 19, body: 6.4, rows: 7.62 }, 8: { length: 9.6, body: 6.4, rows: 7.62 } };
  const all = [...CHIPS.map((c) => ({ ...c, along: 'x' })), ...RAM.map((c) => ({ ...c, along: 'x' })), ...LOGIC.map((c) => ({ ...c, along: 'y' }))];
  const legs = new InstancedMesh(new BoxGeometry(S(0.5), H.dip * 0.75, S(0.5)), M.tin, all.reduce((sum, c) => sum + c.pins, 0));
  // Each leg comes through the board to a tinned pad on the underside. The pads
  // are the board's, not the parts', so they have their own material and stay.
  const pads = new InstancedMesh(new BoxGeometry(S(1.7), 0.012, S(1.7)), standard('model-tin', { roughness: 0.4, metalness: 0.3 }), legs.count);
  let leg = 0;
  for (const c of all) {
    const d = DIP[c.pins];
    const alongX = c.along === 'x';
    const body = add(new Mesh(new BoxGeometry(S(alongX ? d.length : d.body), H.dip, S(alongX ? d.body : d.length)), M.chip), X(c.x), H.standoff + H.dip / 2, Z(c.y));
    body.add(new LineSegments(new EdgesGeometry(body.geometry), new LineBasicMaterial({ color: token('pine') })));
    const perSide = c.pins / 2;
    for (let i = 0; i < perSide; i++) {
      const along = (i - (perSide - 1) / 2) * 2.54;
      for (const side of [-1, 1]) {
        const across = (side * d.rows) / 2;
        place.position.set(X(c.x + (alongX ? along : across)), (H.dip * 0.75) / 2, Z(c.y + (alongX ? across : along)));
        place.updateMatrix();
        legs.setMatrixAt(leg, place.matrix);
        place.position.y = -H.board - 0.006;
        place.updateMatrix();
        pads.setMatrixAt(leg++, place.matrix);
      }
    }
    if (c.label) {
      const label = printed(`MOS ${c.label}`, S(26), S(5), { colour: 'moss-70' });
      add(label, X(c.x), H.standoff + H.dip + 0.003, Z(c.y));
    }
  }
  parts.add(legs);
  scene.add(pads);

  add(new Mesh(new BoxGeometry(S(CRYSTAL.width), 0.5, S(CRYSTAL.depth)), M.tin), X(CRYSTAL.x), 0.25, Z(CRYSTAL.y));
  add(printed(NAME.text, S(NAME.width), S(NAME.depth), { size: 0.86 }), X(NAME.x), 0.004, Z(NAME.y));

  // The display: a dark window with six digits of seven segments in it.
  add(new Mesh(new BoxGeometry(S(DISPLAY.width), H.display, S(DISPLAY.depth)), M.window), X(DISPLAY.x), H.display / 2, Z(DISPLAY.y));
  const digits = DISPLAY.digits.map((dx) => SEGMENTS.map(([u, v, len, across]) => {
    const w = S(DISPLAY.digitWidth), d = S(DISPLAY.digitDepth), t = S(0.9);
    const geometry = across ? new BoxGeometry(w * len, 0.02, t) : new BoxGeometry(t, 0.02, d * len);
    return add(new Mesh(geometry, M.off), X(dx) + (u - 0.5) * w, H.display + 0.011, Z(DISPLAY.y) + (v - 0.5) * d);
  }));

  // The keypad: a block with 23 keys and the SST slide switch on it.
  add(new Mesh(new RoundedBoxGeometry(S(KEYPAD.width), H.keypad, S(KEYPAD.depth), 2, 0.08), M.keypad), X(KEYPAD.x), H.keypad / 2, Z(KEYPAD.y));
  const keys = new Map();
  const pickable = [];
  const keyTop = H.keypad + H.key / 2;
  for (const k of KEYS) {
    const group = new Group();
    const cap = new Mesh(new RoundedBoxGeometry(S(KEYPAD.key), H.key, S(KEYPAD.key), 3, 0.06), M.key);
    const legend = printed(k.name, S(KEYPAD.key) * 0.8, S(KEYPAD.key) * 0.8, { size: k.name.length > 1 ? 0.42 : 0.6 });
    legend.position.y = H.key / 2 + 0.003;
    group.add(cap, legend);
    cap.userData.key = legend.userData.key = k.name;
    pickable.push(cap, legend);
    add(group, X(k.x), keyTop, Z(k.y));
    keys.set(k.name, { group, level: 0, target: 0, until: 0 });
  }
  const slot = add(new Mesh(new BoxGeometry(S(7), 0.05, S(2.6)), M.window), X(SST.x), H.keypad + 0.025, Z(SST.y));
  const knob = add(new Mesh(new BoxGeometry(S(2.6), 0.18, S(2.2)), M.knob), X(SST.x), H.keypad + 0.09, Z(SST.y));
  knob.userData.sst = slot.userData.sst = true;
  pickable.push(knob, slot);
  add(printed('SST', S(8), S(2.6), { colour: 'moss-70', size: 0.7 }), X(SST.x), H.keypad + 0.003, Z(SST.y - 4));

  // The machine: the panel on the same page runs it.
  const panel = document.querySelector('[data-kim1]');
  const sst = panel?.querySelector('[data-kim1-sst]');
  const status = root.querySelector('[data-model-status]');
  let machine = panel?.kim1 ?? null;
  panel?.addEventListener('kim1:ready', () => { machine = panel.kim1; });

  let presses = 0;
  const press = (name) => {
    const k = keys.get(name);
    if (!k) return;
    k.target = 1;
    k.until = performance.now() + 140;
    if (reduced) k.level = 1;
    root.dataset.modelPressed = name;
    root.dataset.modelPresses = String(++presses);
  };
  panel?.addEventListener('kim1:key', (e) => press(e.detail.key));

  s.onClick(() => pickable, (hit) => {
    if (!machine) {
      status.textContent = 'The machine has not started yet, so the model\'s keys do nothing until it has.';
      return;
    }
    if (hit.userData.sst) { if (sst && !sst.disabled) sst.click(); return; }
    if (hit.userData.key) machine.tap(hit.userData.key);
  });

  const shown = digits.map(() => 0);
  every((dt, now) => {
    let changed = false;
    for (let d = 0; d < digits.length; d++) {
      const segments = machine ? Math.max(0, machine.segments(d)) : 0;
      if (segments === shown[d]) continue;
      shown[d] = segments;
      changed = true;
      digits[d].forEach((mesh, b) => { mesh.material = segments & (1 << b) ? M.on : M.off; });
    }
    if (changed || root.dataset.modelSegments === undefined) {
      root.dataset.modelSegments = shown.join(',');
      root.dataset.modelDisplay = shown.map(decode).join('');
    }
    const ease = reduced ? 1 : 1 - Math.exp(-28 * dt);
    for (const k of keys.values()) {
      if (k.target && now > k.until) k.target = 0;
      k.level += (k.target - k.level) * ease;
      k.group.position.y = keyTop - H.travel * k.level;
    }
    const on = sst?.checked ? 1 : -1;
    knob.position.x += (X(SST.x) + on * S(2) - knob.position.x) * ease;
  });

  // The tracks: loaded with the model, put on the top face, and switched by
  // the two buttons. If the map cannot load, the board stays plain, the buttons
  // are disabled and the status line says why; the rest of the model runs.
  const tracksButton = root.querySelector('[data-toggle-tracks]');
  const onlyButton = root.querySelector('[data-toggle-tracks-only]');
  let top = null;
  let tracksError = null;
  try {
    const maps = await trackMaps(TRACKS.src, token('model-pcb'), token('model-copper'));
    const texture = (canvas, srgb) => {
      const t = new CanvasTexture(canvas);
      if (srgb) t.colorSpace = SRGBColorSpace;
      t.anisotropy = s.renderer.capabilities.getMaxAnisotropy();
      return t;
    };
    const surface = texture(maps.surface, false);
    top = new MeshStandardMaterial({ map: texture(maps.colour, true), roughnessMap: surface, metalnessMap: surface, roughness: 1, metalness: 1, bumpMap: texture(maps.relief, false), bumpScale: RELIEF });
  } catch (error) {
    tracksError = error;
  }

  let tracksOn = Boolean(top);
  let partsShown = true;
  let partsLevel = 1;
  const show = () => {
    board.material[2] = tracksOn ? top : M.board;
    tracksButton?.setAttribute('aria-pressed', String(tracksOn));
    onlyButton?.setAttribute('aria-pressed', String(!partsShown));
    root.dataset.modelTracks = tracksOn ? 'on' : 'off';
    root.dataset.modelParts = partsShown ? 'shown' : 'hidden';
  };
  if (top) {
    tracksButton?.addEventListener('click', () => {
      tracksOn = !tracksOn;
      // Tracks only, with no tracks, would be an empty board: the parts come back.
      if (!tracksOn) partsShown = true;
      show();
      status.textContent = tracksOn ? 'The copper tracks are on the board.' : 'The tracks are off: the board is plain.';
    });
    onlyButton?.addEventListener('click', () => {
      partsShown = !partsShown;
      if (!partsShown) tracksOn = true;
      show();
      status.textContent = partsShown ? 'The parts are back on the board.' : 'The parts are hidden, so only the board and its tracks show.';
    });
  } else {
    for (const b of [tracksButton, onlyButton]) if (b) { b.disabled = true; b.setAttribute('aria-pressed', 'false'); }
  }
  show();

  // Fading the parts: every material in the group, each back to its own
  // opacity and transparency when shown. The lit and unlit segments swap, so
  // both are in the list whichever a digit is showing.
  const faded = new Map();
  const collect = (m) => { if (m && !faded.has(m)) faded.set(m, { opacity: m.opacity, transparent: m.transparent }); };
  parts.traverse((o) => [o.material].flat().forEach(collect));
  [M.on, M.off].forEach(collect);
  const fade = (level) => {
    parts.visible = level > 0;
    for (const [m, was] of faded) {
      m.opacity = was.opacity * level;
      const transparent = level < 1 || was.transparent;
      if (m.transparent !== transparent) { m.transparent = transparent; m.needsUpdate = true; }
    }
  };
  every((dt) => {
    const target = partsShown ? 1 : 0;
    if (partsLevel === target) return;
    partsLevel = reduced ? target : partsLevel + Math.sign(target - partsLevel) * Math.min(Math.abs(target - partsLevel), dt / FADE_S);
    fade(partsLevel);
  });

  root.modelLayers = () => ({ tracksLoaded: Boolean(top), tracks: board.material[2] === top && Boolean(top), partsVisible: parts.visible, partsLevel });

  const onScreen = (v) => {
    const rect = s.renderer.domElement.getBoundingClientRect();
    v.project(s.camera);
    return { x: rect.left + ((v.x + 1) / 2) * rect.width, y: rect.top + ((1 - v.y) / 2) * rect.height };
  };
  root.modelKeyPoint = (name) => {
    const k = keys.get(name);
    return onScreen(new Vector3(k.group.position.x, k.group.position.y + H.key / 2, k.group.position.z));
  };
  /** Where a point on the board's top face, in mm as the layout has it, is on the screen. */
  root.modelBoardPoint = (x, y) => onScreen(new Vector3(X(x), 0, Z(y)));

  status.textContent = tracksError
    ? `The model is running, without its tracks, which could not load: ${tracksError.message}`
    : 'The model is running. Its digits show the machine\'s display.';
  s.start();
}
