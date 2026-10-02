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
// What a test can read off the section: data-model-segments and
// data-model-display (what the model's digits show), data-model-pressed and
// data-model-presses (the last key that went down, and how many have). And
// root.modelKeyPoint(name), where a key's top is on the screen, so the browser
// check can click one.

import {
  BoxGeometry, PlaneGeometry, CylinderGeometry, EdgesGeometry, LineSegments, LineBasicMaterial,
  Mesh, InstancedMesh, Object3D, Group, MeshStandardMaterial, MeshBasicMaterial, CanvasTexture, SRGBColorSpace, Vector3,
} from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { createStage } from './stage.mjs';
import { BOARD, TABS, CONTACTS_PER_TAB, PITCH, CHIPS, RAM, LOGIC, CRYSTAL, NAME, HOLES, DISPLAY, KEYPAD, KEYS, SST, SEGMENTS, decode } from './kim-1-layout.mjs';

// Millimetres on the photograph to the scene's centimetres, centred on the board.
const X = (x) => (x - BOARD.width / 2) / 10;
const Z = (y) => (y - BOARD.depth / 2) / 10;
const S = (mm) => mm / 10;

// Typical heights, in centimetres: a photograph taken from above has none.
const H = { board: S(BOARD.thickness), dip: 0.38, standoff: 0.08, display: 0.45, keypad: 0.55, key: 0.4, travel: 0.16 };

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

  const add = (mesh, x, y, z) => { mesh.position.set(x, y, z); scene.add(mesh); return mesh; };

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
  // A box has six faces, in the order +x, -x, +y, -y, +z, -z: the fourth is the
  // underside, which the camera can now see, so it has its own material.
  const faces = [M.board, M.board, M.board, M.under, M.board, M.board];
  const board = add(new Mesh(new BoxGeometry(S(BOARD.width), H.board, S(BOARD.depth)), faces), 0, -H.board / 2, 0);
  board.add(new LineSegments(new EdgesGeometry(board.geometry), new LineBasicMaterial({ color: token('circuit') })));
  for (const t of TABS) {
    const tab = add(new Mesh(new BoxGeometry(S(t.out), H.board, S(t.y1 - t.y0)), faces), X(-t.out / 2), -H.board / 2, Z((t.y0 + t.y1) / 2));
    tab.add(new LineSegments(new EdgesGeometry(tab.geometry), new LineBasicMaterial({ color: token('circuit') })));
  }
  for (const [x, y] of HOLES) add(new Mesh(new CylinderGeometry(S(1.6), S(1.6), H.board + 0.01, 20), M.hole), X(x), -H.board / 2, Z(y));

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
  // Each leg comes through the board to a tinned pad on the underside.
  const pads = new InstancedMesh(new BoxGeometry(S(1.7), 0.012, S(1.7)), M.tin, legs.count);
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
  scene.add(legs, pads);

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

  root.modelKeyPoint = (name) => {
    const k = keys.get(name);
    const rect = s.renderer.domElement.getBoundingClientRect();
    const v = new Vector3(k.group.position.x, k.group.position.y + H.key / 2, k.group.position.z).project(s.camera);
    return { x: rect.left + ((v.x + 1) / 2) * rect.width, y: rect.top + ((1 - v.y) / 2) * rect.height };
  };

  status.textContent = 'The model is running. Its digits show the machine\'s display.';
  s.start();
}
