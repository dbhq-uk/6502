// The BBC Micro Model B's case as a three.js scene: all of the drawing for its
// outside model, so that the model's page entry (task 9b of the BBC Micro
// models plan) is thin: the stage, this module, the machine's panel and the
// test hooks. It exports no mount(root), so no page bundles it before the BBC
// Micro claims its models (the plan's re-plan of 13 October 2026), and its
// tests build its scene graph in Node.
//
// It is our own model, built from the measurements in
// ./bbc-micro-case-layout.mjs (tools/bbc-micro-model/ is the measuring): the
// case at the Service Manual's size, the opening in its top with the panel the
// keys stand on, every keycap at its place from an open keyboard drawing put in
// the case by triangulating two photographs, the three lights, the badge
// strip's words, the rear's ports, vents and mains panel, and underneath, the
// connectors and the feet. Its words, the legends on the keys among them, are
// drawn by us, in the site's own type, into one canvas once the type has
// loaded: no image, no copy of anyone's lettering or artwork. It is labelled a
// model, not a photograph, everywhere it is shown.
//
// Every colour is a token the stylesheet defines, read through `token` (the
// stage's reader in the browser, one from tokens.css in the tests).

import {
  BufferGeometry, Float32BufferAttribute, BoxGeometry, PlaneGeometry, CylinderGeometry, ExtrudeGeometry, Shape, Path, Vector2, Vector3,
  Mesh, Group, MeshStandardMaterial, MeshBasicMaterial, CanvasTexture, SRGBColorSpace,
} from 'three';
import { CASE, RECESS, PROFILE, KEYS, LEDS, BADGE, GRILLE, REAR, UNDERSIDE, FEET, legendOf } from './bbc-micro-case-layout.mjs';

// The case frame (mm, from the left rear corner at table level, x right, y to
// the front, z up) to the scene's centimetres, centred on the case; y is up.
export const X = (x) => (x - CASE.width / 2) / 10;
export const Y = (z) => (z - CASE.height / 2) / 10;
export const Z = (y) => (y - CASE.depth / 2) / 10;
const S = (mm) => mm / 10;

// Typical, not measured (the note says so): how far a key goes down when
// pressed; a keycap's sides, drawn in by FOOT_INSET at its foot and TAPER at
// its flat top from the cell K1 gives it; the feet's diameter and how far in
// from the case's sides and ends they stand.
export const TRAVEL_MM = 3.5;
const FOOT_INSET = 0.6;
const TAPER = 2.5;
const FOOT = { d: 12, inX: 30, inY: 25 };
// How far a part drawn on a face stands off it, in mm; how deep the panel and
// the badge strip are drawn below the parts that cover them.
const SKIN = 0.3;
const UNDER = 0.5;
// The words: how many canvas pixels to a millimetre of the case, and the canvas's width.
const PX_PER_MM = 12;
const ATLAS_WIDTH = 2048;
// The height of a printed label on the rear and underneath, in mm.
const LABEL_H = 3.5;

/** Where the camera starts and the box its target is held in (createStage's options): the case with two centimetres to spare, all the way round, under it too. */
export function stageOptions() {
  const margin = 2;
  const bounds = [X(0) - margin, Y(0) - margin, Z(0) - margin, X(CASE.width) + margin, Y(CASE.height) + margin, Z(CASE.depth) + margin];
  return { view: [-34, 30, 46, 0, -1, 2], bounds, minDistance: 3, maxDistance: 150 };
}

/** A point of the case's plan as a shape's point: the shape is extruded up, then turned so its v runs to the rear. */
const plan = ([x, y]) => new Vector2(X(x), -Z(y));

/** The case's outline seen from above, its corners rounded to CASE.cornerRadius, as a shape. */
function outline() {
  const r = CASE.cornerRadius;
  const [x0, y0, x1, y1] = [0, 0, CASE.width, CASE.depth];
  const s = new Shape();
  const p = (x, y) => plan([x, y]);
  s.moveTo(p(x0 + r, y0).x, p(x0 + r, y0).y);
  for (const [[ax, ay], [cx, cy], [bx, by]] of [
    [[x1 - r, y0], [x1, y0], [x1, y0 + r]], [[x1, y1 - r], [x1, y1], [x1 - r, y1]],
    [[x0 + r, y1], [x0, y1], [x0, y1 - r]], [[x0, y0 + r], [x0, y0], [x0 + r, y0]],
  ]) {
    s.lineTo(p(ax, ay).x, p(ax, ay).y);
    s.quadraticCurveTo(p(cx, cy).x, p(cx, cy).y, p(bx, by).x, p(bx, by).y);
  }
  return s;
}

/** A shape (in plan) extruded from height z0 to z1 in the case frame. */
function slab(shape, z0, z1) {
  const g = new ExtrudeGeometry(shape, { depth: S(z1 - z0), bevelEnabled: false, curveSegments: 6 });
  g.rotateX(-Math.PI / 2);
  g.translate(0, Y(z0), 0);
  return g;
}

/** A closed polygon of case points as a shape or a path. */
const polygon = (pts, Kind = Shape) => new Kind(pts.map(plan));

/**
 * A keycap of the cell w by d (mm), h high: a block drawn in at its foot and
 * more at its flat top, standing on y = 0. Each face's triangles are turned to
 * face out.
 */
const caps = new Map();
function capGeometry(w, d, h) {
  const key = `${w} ${d} ${h}`;
  if (caps.has(key)) return caps.get(key);
  const [bx, bz, tx, tz] = [S(w / 2 - FOOT_INSET), S(d / 2 - FOOT_INSET), S(w / 2 - TAPER), S(d / 2 - TAPER)];
  const foot = [[-bx, 0, -bz], [bx, 0, -bz], [bx, 0, bz], [-bx, 0, bz]];
  const top = [[-tx, h, -tz], [tx, h, -tz], [tx, h, tz], [-tx, h, tz]];
  const quads = [top, ...[0, 1, 2, 3].map((i) => [foot[i], foot[(i + 1) % 4], top[(i + 1) % 4], top[i]])];
  const centre = new Vector3(0, h / 2, 0);
  const pos = [];
  for (const [a, b, c, e] of quads) {
    for (const tri of [[a, b, c], [a, c, e]]) {
      const [p, q, r] = tri.map((v) => new Vector3(...v));
      const n = q.clone().sub(p).cross(r.clone().sub(p));
      const out = p.clone().add(q).add(r).divideScalar(3).sub(centre);
      for (const v of n.dot(out) >= 0 ? [p, q, r] : [p, r, q]) pos.push(v.x, v.y, v.z);
    }
  }
  const g = new BufferGeometry();
  g.setAttribute('position', new Float32BufferAttribute(pos, 3));
  g.computeVertexNormals();
  caps.set(key, g);
  return g;
}

/** The size of a keycap's flat top, in mm, from its cell. */
const topOf = (k) => ({ w: k.w - 2 * TAPER, d: k.d - 2 * TAPER });

/**
 * Every word the case carries, for the atlas: each key's legend (none on the
 * space bar), the badge strip's words, the lights' names, each rear port's
 * label and the mains panel's words, and each underside connector's label. Each
 * has an `id`, its `text`, the `lines` it is set in, the size in mm of the box
 * it fills on the case, the token of its ink and its weight.
 */
export function atlasItems() {
  const items = [];
  const legend = 'model-bbc-legend';
  for (const k of KEYS.keys) {
    const text = legendOf(k.name);
    if (!text) continue;
    const t = topOf(k);
    items.push({ id: `key:${k.name}`, text, lines: text.split(' '), wMm: t.w - 1, hMm: t.d - 1, ink: legend, weight: 600 });
  }
  const [bx0, by0, bx1, by1] = BADGE.wordsBox;
  items.push({ id: 'badge', text: BADGE.words.join('\n'), lines: BADGE.words, wMm: bx1 - bx0, hMm: by1 - by0, ink: legend, weight: 500 });
  for (const l of LEDS.leds) items.push({ id: `led:${l.name}`, text: l.name, lines: l.name.split(' '), wMm: 14, hMm: 5, ink: legend, weight: 500 });
  const label = (text) => ({ text, lines: [text], wMm: text.length * 1.8 + 2, hMm: LABEL_H, weight: 500 });
  for (const p of REAR.ports) items.push({ id: `rear:${p.ref}`, ...label(p.label), ink: 'model-bbc-panel' });
  const m = REAR.mains;
  items.push({ id: 'mains', text: m.words.join('\n'), lines: m.words, wMm: m.x1 - m.x0 - 4, hMm: m.z1 - m.z0 - 4, ink: legend, weight: 500 });
  for (const u of UNDERSIDE.connectors) items.push({ id: `under:${u.ref}`, ...label(u.label), ink: legend });
  return items;
}

/**
 * Where each item goes in the atlas: shelves across a canvas `width` pixels
 * wide, the tallest first, `pxPerMm` pixels to a millimetre of the case.
 * Returns the canvas's size and, by item id, its box in pixels and in texture
 * coordinates (the canvas's first row is v = 1).
 */
export function layoutAtlas(items, { width = ATLAS_WIDTH, pxPerMm = PX_PER_MM } = {}) {
  const boxes = items.map((i) => ({ id: i.id, w: Math.ceil(i.wMm * pxPerMm) + 4, h: Math.ceil(i.hMm * pxPerMm) + 4 }));
  let x = 0, y = 0, row = 0;
  for (const b of [...boxes].sort((a, c) => c.h - a.h)) {
    if (x + b.w > width) { x = 0; y += row; row = 0; }
    Object.assign(b, { x, y });
    x += b.w;
    row = Math.max(row, b.h);
  }
  const height = y + row;
  return {
    width, height,
    boxes: new Map(boxes.map((b) => [b.id, { x: b.x, y: b.y, w: b.w, h: b.h, u0: b.x / width, u1: (b.x + b.w) / width, v0: 1 - (b.y + b.h) / height, v1: 1 - b.y / height }])),
  };
}

/**
 * In the browser: the atlas drawn, every item's lines in the site's own type
 * (`family`, the page's --font-body), centred in its box, each line no wider
 * than the box, once the type has loaded. Returns what buildCase takes: the
 * texture and each item's place in it.
 */
export async function drawAtlas(items, token, family) {
  await document.fonts?.load(`600 32px ${family}`).catch(() => null);
  await document.fonts?.load(`500 32px ${family}`).catch(() => null);
  const { width, height, boxes } = layoutAtlas(items);
  const canvas = Object.assign(document.createElement('canvas'), { width, height });
  const g = canvas.getContext('2d');
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  for (const item of items) {
    const b = boxes.get(item.id);
    const lineH = (b.h - 4) / item.lines.length;
    g.fillStyle = `#${token(item.ink).getHexString()}`;
    g.font = `${item.weight} ${Math.round(lineH * 0.72)}px ${family}`;
    item.lines.forEach((line, i) => {
      const fit = Math.min(1, (b.w - 6) / (g.measureText(line).width || 1));
      g.save();
      g.translate(b.x + b.w / 2, b.y + 2 + lineH * (i + 0.5));
      g.scale(fit, 1);
      g.fillText(line, 0, 0);
      g.restore();
    });
  }
  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  texture.anisotropy = 4;
  return { texture, place: boxes };
}

/**
 * Builds the case into `scene`: the shell with the opening cut through its
 * top, the panel in the opening and the badge strip at its rear, every
 * keycap, the lights, the rear's ports, vents and mains panel, the underside's
 * panel and connectors, and the feet. `atlas` (drawAtlas's, in the browser) puts
 * the words on; without it the case has none.
 *
 * Returns the handles the page's entry uses: `shell`, `feet`, `caps` (keycap
 * meshes by key name, each with userData.key), `leds` (by name, in LEDS
 * order), `ports` (a group each, rear and underneath, by reference, with
 * userData.port and .label), `vents`, `pickable` (what a pointer names or
 * clicks), `materials`, `setKeys(names)` (exactly those caps down),
 * `setLeds(names)` (exactly those lit) and `keyTop(name)` (the cap's top, in
 * the scene).
 */
export function buildCase(scene, token, { atlas = null } = {}) {
  const standard = (name, opts = {}) => new MeshStandardMaterial({ color: token(name), roughness: 0.6, metalness: 0.02, ...opts });
  const M = {
    case: standard('model-bbc-case', { roughness: 0.7 }),
    panel: standard('model-bbc-panel', { roughness: 0.75 }),
    badge: standard('model-bbc-badge', { roughness: 0.25 }),
    red: standard('model-bbc-key-red', { roughness: 0.45 }),
    light: standard('model-bbc-key-light', { roughness: 0.45 }),
    dark: standard('model-bbc-key-dark', { roughness: 0.45 }),
    metal: standard('model-tin', { roughness: 0.35, metalness: 0.4 }),
    hole: standard('void', { roughness: 0.9 }),
    ledOff: standard('model-led-off', { roughness: 0.3 }),
    ledOn: new MeshBasicMaterial({ color: token('model-led'), toneMapped: false }),
  };
  const ink = atlas ? new MeshBasicMaterial({ map: atlas.texture, transparent: true, depthWrite: false, toneMapped: false, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }) : null;
  const items = new Map(atlasItems().map((i) => [i.id, i]));
  /** A plane of the atlas for item `id`, facing +z before it is turned, or null with no atlas. */
  const words = (id) => {
    const item = items.get(id);
    if (!ink || !item) return null;
    const at = atlas.place.get(id);
    const geometry = new PlaneGeometry(S(item.wMm), S(item.hMm));
    const uv = geometry.attributes.uv;
    for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) ? at.u1 : at.u0, uv.getY(i) ? at.v1 : at.v0);
    const plane = new Mesh(geometry, ink);
    plane.userData = { item: id, words: item.text };
    return plane;
  };
  const turn = (deg) => -(deg * Math.PI) / 180;
  const box = (w, h, d, material) => new Mesh(new BoxGeometry(S(w), S(h), S(d)), material);

  // ---- The shell: the body, the rim round the opening, the panel in it and the badge strip ----
  const shell = new Group();
  const panelZ = RECESS.panel;
  shell.add(new Mesh(slab(outline(), FEET.height, panelZ - UNDER), M.case));
  const rim = outline();
  rim.holes.push(polygon(RECESS.opening, Path));
  shell.add(new Mesh(slab(rim, panelZ - UNDER, CASE.height), M.case));
  shell.add(new Mesh(slab(polygon(RECESS.opening), panelZ - UNDER, panelZ), M.panel));
  // The badge strip at the opening's rear, a little below the top, its words at their place on it.
  const stripTop = CASE.height - UNDER;
  shell.add(new Mesh(slab(polygon(BADGE.strip), panelZ - UNDER, stripTop), M.badge));
  const badge = words('badge');
  if (badge) {
    const [x0, y0, x1, y1] = BADGE.wordsBox;
    badge.rotation.x = -Math.PI / 2;
    badge.position.set(X((x0 + x1) / 2), Y(stripTop), Z((y0 + y1) / 2));
    shell.add(badge);
  }
  scene.add(shell);

  // ---- The keys: a cap each, standing on the panel, its top at the key plane, its legend on its top ----
  const capsByName = new Map();
  const h = S(PROFILE.keyPlane - panelZ);
  for (const k of KEYS.keys) {
    const cap = new Mesh(capGeometry(k.w, k.d, h), M[k.colour]);
    cap.position.set(X(k.x), Y(panelZ), Z(k.y));
    cap.rotation.y = turn(k.rotation);
    cap.userData = { key: k.name, rest: cap.position.y };
    const legend = words(`key:${k.name}`);
    if (legend) {
      legend.rotation.x = -Math.PI / 2;
      legend.position.y = h;
      cap.add(legend);
    }
    scene.add(cap);
    capsByName.set(k.name, cap);
  }

  // ---- The lights, on the panel, their names printed behind them ----
  const leds = new Map();
  for (const l of LEDS.leds) {
    const led = new Mesh(new CylinderGeometry(S(1.5), S(1.5), S(1.2), 16), M.ledOff);
    led.position.set(X(l.x), Y(panelZ) + S(0.6), Z(l.y));
    led.userData = { led: l.name };
    scene.add(led);
    leds.set(l.name, led);
    const name = words(`led:${l.name}`);
    if (name) {
      name.rotation.x = -Math.PI / 2;
      name.position.set(X(l.x), Y(panelZ), Z(l.y - 6));
      scene.add(name);
    }
  }

  // ---- The rear: each port with its label above it, the vents and the mains panel ----
  const ports = new Map();
  const rear = Z(0);
  const onRear = (mesh, x, z, proud) => { mesh.position.set(X(x), Y(z), rear - S(proud) / 2); return mesh; };
  const tube = (r, length, material) => { const m = new Mesh(new CylinderGeometry(S(r), S(r), S(length), 20), material); m.rotation.x = Math.PI / 2; return m; };
  for (const p of REAR.ports) {
    const g = new Group();
    g.userData = { port: p.ref, label: p.label };
    if (p.kind === 'phono' || p.kind === 'bnc') {
      g.add(onRear(tube(p.w / 2, p.kind === 'bnc' ? 10 : 6, M.metal), p.x, p.z, p.kind === 'bnc' ? 10 : 6));
      g.add(onRear(tube(p.w / 5, 0.4, M.hole), p.x, p.z, (p.kind === 'bnc' ? 10 : 6) * 2 + 0.4));
    } else if (p.kind === 'din') {
      g.add(onRear(tube(p.w / 2, 1.5, M.dark), p.x, p.z, 1.5));
      g.add(onRear(tube(p.w * 0.3, 0.4, M.hole), p.x, p.z, 3.4));
    } else {
      g.add(onRear(box(p.w, p.h, 2, M.metal), p.x, p.z, 2));
      g.add(onRear(box(p.w * 0.6, p.h * 0.6, 0.4, M.hole), p.x, p.z, 4.4));
    }
    scene.add(g);
    ports.set(p.ref, g);
    const label = words(`rear:${p.ref}`);
    if (label) {
      label.rotation.y = Math.PI;
      label.position.set(X(p.x), Y(p.z + p.h / 2 + 1.5 + LABEL_H / 2), rear - S(SKIN));
      scene.add(label);
    }
  }
  const vents = GRILLE.slots.map((v) => {
    const m = onRear(box(v.x1 - v.x0, v.z1 - v.z0, SKIN, M.hole), (v.x0 + v.x1) / 2, (v.z0 + v.z1) / 2, SKIN);
    scene.add(m);
    return m;
  });
  const m = REAR.mains;
  scene.add(onRear(box(m.x1 - m.x0, m.z1 - m.z0, SKIN, M.panel), (m.x0 + m.x1) / 2, (m.z0 + m.z1) / 2, SKIN));
  const mains = words('mains');
  if (mains) {
    mains.rotation.y = Math.PI;
    mains.position.set(X((m.x0 + m.x1) / 2), Y((m.z0 + m.z1) / 2), rear - S(SKIN) - S(0.05));
    scene.add(mains);
  }

  // ---- Underneath: the panel the connectors sit in, each connector with its label in front of it ----
  const base = FEET.height;
  const u = UNDERSIDE.panel;
  const under = box(u.x1 - u.x0, UNDER, u.y1 - u.y0, M.panel);
  under.position.set(X((u.x0 + u.x1) / 2), Y(base - UNDER / 2), Z((u.y0 + u.y1) / 2));
  scene.add(under);
  for (const c of UNDERSIDE.connectors) {
    const g = new Group();
    g.userData = { port: c.ref, label: c.label };
    const body = box(c.w, 1, c.d, M.dark);
    body.position.set(X(c.x), Y(base - UNDER - 0.5), Z(c.y));
    g.add(body);
    scene.add(g);
    ports.set(c.ref, g);
    const label = words(`under:${c.ref}`);
    if (label) {
      label.rotation.x = Math.PI / 2;
      label.position.set(X(c.x), Y(base - UNDER) - S(0.05), Z(c.y + c.d / 2 + 1 + LABEL_H / 2));
      scene.add(label);
    }
  }

  // ---- The feet, typical, near the corners ----
  const feet = new Group();
  for (const [x, y] of [[FOOT.inX, FOOT.inY], [CASE.width - FOOT.inX, FOOT.inY], [FOOT.inX, CASE.depth - FOOT.inY], [CASE.width - FOOT.inX, CASE.depth - FOOT.inY]]) {
    const foot = new Mesh(new CylinderGeometry(S(FOOT.d / 2), S(FOOT.d / 2), S(FEET.height), 24), M.dark);
    foot.position.set(X(x), Y(FEET.height / 2), Z(y));
    feet.add(foot);
  }
  scene.add(feet);

  const pickable = [...capsByName.values(), ...[...ports.values()].flatMap((g) => g.children)];
  return {
    shell, feet, caps: capsByName, leds, ports, vents, pickable, materials: M,
    /** Puts exactly the caps named down by TRAVEL_MM, and the rest up. */
    setKeys(names) { for (const [name, cap] of capsByName) cap.position.y = cap.userData.rest - (names.includes(name) ? S(TRAVEL_MM) : 0); },
    /** Lights exactly the lights named. */
    setLeds(names) { for (const [name, led] of leds) led.material = names.includes(name) ? M.ledOn : M.ledOff; },
    /** The middle of a cap's top, in the scene, where it is now. */
    keyTop(name) { const cap = capsByName.get(name); return cap ? cap.localToWorld(new Vector3(0, h, 0)) : null; },
  };
}
