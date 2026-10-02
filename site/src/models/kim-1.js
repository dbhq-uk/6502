// The KIM-1 board as a 3D model, on the machine's page. Bundled by
// scripts/build-models.mjs into public/models/kim-1.js, and loaded by
// public/model-loader.js only when its section nears the screen.
//
// It is our own model, built here from boxes and cylinders, measured from five
// photographs of real boards and a replica of the board's layout
// (kim-1-layout.mjs says how, and tools/kim1-model/ is the measuring). It is
// labelled a model, not a photograph, everywhere it is shown.
//
// It is tied to the running machine, not animated by hand. The six digits
// light with the segments the page's drawn display shows, read from the same
// machine through the panel (public/kim-1.js puts that on the panel as
// `panel.kim1`). A click on a key here presses that key on the machine, and a
// key pressed anywhere, here, on the page's keypad or on a keyboard, goes down
// here, because the panel announces every tap with a `kim1:key` event.
//
// Both faces of the board carry the real board's copper, from one map
// (scripts/build-models.mjs serves it beside the bundle): its red channel is
// the top face traced from three photographs, its green the top face under the
// parts, where no photograph shows the board, from the replica, and its blue
// the underside traced from its photograph. The map is loaded with the model,
// as a same-origin image, and turned here into each face's colour (the
// lacquer's green and the copper's, all tokens), its shine (the copper
// smoother and a little metallic) and its relief (a bump map, so a track
// stands proud). Two buttons under the model switch the tracks off, and fade
// the parts out to leave the tracks.
//
// What a test can read off the section: data-model-segments and
// data-model-display (what the model's digits show), data-model-pressed and
// data-model-presses (the last key that went down, and how many have),
// data-model-tracks (on or off) and data-model-parts (shown or hidden). And
// root.modelKeyPoint(name), where a key's top is on the screen, so the browser
// check can click one; root.modelBoardPoint(x, y, below), the same for a point
// on the board in mm, on its top face or its underside; and root.modelLayers(),
// what the scene is showing.

import {
  BoxGeometry, PlaneGeometry, CylinderGeometry, TubeGeometry, CatmullRomCurve3, EdgesGeometry, LineSegments, LineBasicMaterial,
  Mesh, InstancedMesh, Object3D, Group, MeshStandardMaterial, MeshBasicMaterial, CanvasTexture, SRGBColorSpace, Vector3,
} from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { createStage } from './stage.mjs';
import {
  BOARD, TABS, CONTACTS_PER_TAB, PITCH, CHIPS, AXIAL, TRANSISTORS, TRIMMER, DISC, CRYSTAL, NAME, HOLES, KEYPAD_HOLES, WIRE,
  DISPLAY, KEYPAD, KEYS, SST, HEIGHTS, SEGMENTS, TRACKS, decode,
} from './kim-1-layout.mjs';

// Millimetres on the board to the scene's centimetres, centred on the board.
const X = (x) => (x - BOARD.width / 2) / 10;
const Z = (y) => (y - BOARD.depth / 2) / 10;
const S = (mm) => mm / 10;

// Heights in centimetres, from the layout: the tops of the parts as measured
// (HEIGHTS, the keypad, the display and the crystal), and the few typical ones.
const H = {
  board: S(BOARD.thickness),
  travel: 0.16,
  // A package's body is about this thick; it stands on its legs below that.
  body: { ceramic: S(2.0), socketed: S(4.0), memory: S(2.2), plastic: S(3.3) },
  top: { ceramic: S(HEIGHTS.ceramic), socketed: S(HEIGHTS.socketed), memory: S(HEIGHTS.memory), plastic: S(HEIGHTS.plastic) },
  socket: S(4.2),
};

// The lacquer and the copper: how rough and how metallic each is. The
// lacquer's are the plain board's, so with the tracks off nothing else changes.
const SURFACE = { mask: { roughness: 0.55, metalness: 0.1 }, copper: { roughness: 0.35, metalness: 0.25 } };
// How far a track stands proud, as three.js's bump scale, and how long the parts take to fade.
const RELIEF = 1.5;
const FADE_S = 0.3;

/**
 * Each face's three maps from the track map: colour (lacquer to copper, in
 * sRGB, from the tokens), surface (roughness in green and metalness in blue,
 * as three.js reads them) and relief (the copper, softened so a track's edge
 * is a slope rather than a cliff). The top face's copper is the map's red or
 * green channel, the underside's its blue. The underside's sheets are turned
 * upside down, because three.js lays a texture on a box's bottom face with
 * its first row at the board's far edge.
 */
async function trackMaps(src, faces) {
  const img = new Image();
  img.src = src;
  await img.decode();
  const { naturalWidth: w, naturalHeight: h } = img;
  const sheet = () => Object.assign(document.createElement('canvas'), { width: w, height: h });
  const read = sheet().getContext('2d', { willReadFrequently: true });
  read.drawImage(img, 0, 0);
  const rgb = read.getImageData(0, 0, w, h).data;
  const channels = (c) => { const o = { r: 0, g: 0, b: 0 }; c.getRGB(o, SRGBColorSpace); return [o.r, o.g, o.b].map((v) => Math.round(v * 255)); };
  const { mask: sm, copper: sc } = SURFACE;
  const out = {};
  for (const [name, { cover, mask, copper, flip }] of Object.entries(faces)) {
    const colour = sheet(), surface = sheet(), relief = sheet();
    const cg = colour.getContext('2d'), sg = surface.getContext('2d'), rg = relief.getContext('2d');
    const C = cg.createImageData(w, h), F = sg.createImageData(w, h), B = rg.createImageData(w, h);
    const m = channels(mask), k = channels(copper);
    for (let y = 0; y < h; y++) {
      const from = flip ? h - 1 - y : y;
      for (let x = 0; x < w; x++) {
        const i = (y * w + x) * 4;
        const t = cover(rgb, (from * w + x) * 4) / 255;
        for (let c = 0; c < 3; c++) C.data[i + c] = m[c] + (k[c] - m[c]) * t;
        C.data[i + 3] = 255;
        F.data[i] = 255;
        F.data[i + 1] = 255 * (sm.roughness + (sc.roughness - sm.roughness) * t);
        F.data[i + 2] = 255 * (sm.metalness + (sc.metalness - sm.metalness) * t);
        F.data[i + 3] = 255;
        B.data[i] = B.data[i + 1] = B.data[i + 2] = 255 * t;
        B.data[i + 3] = 255;
      }
    }
    cg.putImageData(C, 0, 0);
    sg.putImageData(F, 0, 0);
    const soft = sheet();
    soft.getContext('2d').putImageData(B, 0, 0);
    rg.filter = 'blur(1.5px)';
    rg.drawImage(soft, 0, 0);
    out[name] = { colour, surface, relief };
  }
  return out;
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
    ceramic: standard('model-ceramic', { roughness: 0.5 }),
    capacitor: standard('model-capacitor', { roughness: 0.5 }),
    resistor: standard('model-resistor', { roughness: 0.6 }),
    wire: standard('model-wire', { roughness: 0.5 }),
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
  const outline = (mesh, colour) => mesh.add(new LineSegments(new EdgesGeometry(mesh.geometry), new LineBasicMaterial({ color: token(colour) })));

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
  // top and the fourth the underside, and each takes its tracks once they have
  // loaded. Each tab has its own copy of the list, so the tracks go on the board alone.
  const faces = [M.board, M.board, M.board, M.under, M.board, M.board];
  const board = fixed(new Mesh(new BoxGeometry(S(BOARD.width), H.board, S(BOARD.depth)), faces), 0, -H.board / 2, 0);
  outline(board, 'circuit');
  for (const t of TABS) {
    const tab = fixed(new Mesh(new BoxGeometry(S(t.out), H.board, S(t.y1 - t.y0)), [...faces]), X(-t.out / 2), -H.board / 2, Z((t.y0 + t.y1) / 2));
    outline(tab, 'circuit');
  }
  for (const [x, y] of [...HOLES, ...KEYPAD_HOLES]) fixed(new Mesh(new CylinderGeometry(S(1.6), S(1.6), H.board + 0.01, 20), M.hole), X(x), -H.board / 2, Z(y));

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

  // Chips: a dual in-line package of `pins` legs, its long side along x or y,
  // as the Musée Bolo's board has it: the 6502 and one 6530 in white ceramic
  // with a gold lid, the other 6530 black and raised in a socket, the memory
  // in low black packages, the logic in taller plastic ones.
  const DIP = { 40: { length: 52, body: 13.7, rows: 15.24 }, 16: { length: 19.3, body: 6.4, rows: 7.62 }, 14: { length: 19, body: 6.4, rows: 7.62 }, 8: { length: 9.6, body: 6.4, rows: 7.62 } };
  const legs = new InstancedMesh(new BoxGeometry(S(0.5), 1, S(0.5)), M.tin, CHIPS.reduce((sum, c) => sum + c.pins, 0));
  // Each leg comes through the board to a tinned pad on the underside. The pads
  // are the board's, not the parts', so they have their own material and stay.
  const pads = new InstancedMesh(new BoxGeometry(S(1.7), 0.012, S(1.7)), standard('model-tin', { roughness: 0.4, metalness: 0.3 }), legs.count + AXIAL.length * 2);
  let leg = 0, pad = 0;
  const underPad = (x, y) => {
    place.position.set(X(x), -H.board - 0.006, Z(y));
    place.scale.set(1, 1, 1);
    place.updateMatrix();
    pads.setMatrixAt(pad++, place.matrix);
  };
  for (const c of CHIPS) {
    const d = DIP[c.pins];
    const alongX = c.alongX;
    const top = H.top[c.kind];
    const thick = H.body[c.kind];
    const w = S(alongX ? d.length : d.body), dd = S(alongX ? d.body : d.length);
    if (c.kind === 'socketed') {
      const socket = add(new Mesh(new BoxGeometry(w + S(1.5), H.socket, dd + S(1.8)), M.chip), X(c.x), H.socket / 2, Z(c.y));
      outline(socket, 'pine');
    }
    const body = add(new Mesh(new BoxGeometry(w, thick, dd), c.kind === 'ceramic' ? M.ceramic : M.chip), X(c.x), top - thick / 2, Z(c.y));
    outline(body, c.kind === 'ceramic' ? 'moss-70' : 'pine');
    if (c.kind === 'ceramic') add(new Mesh(new BoxGeometry(S(12), 0.006, S(11)), M.gold), X(c.x), top + 0.003, Z(c.y));
    const legTop = c.kind === 'socketed' ? H.socket : top - thick / 2;
    const perSide = c.pins / 2;
    for (let i = 0; i < perSide; i++) {
      const along = (i - (perSide - 1) / 2) * 2.54;
      for (const side of [-1, 1]) {
        const across = (side * d.rows) / 2;
        const x = c.x + (alongX ? along : across), y = c.y + (alongX ? across : along);
        place.position.set(X(x), legTop / 2, Z(y));
        place.scale.set(1, legTop, 1);
        place.updateMatrix();
        legs.setMatrixAt(leg++, place.matrix);
        underPad(x, y);
      }
    }
    if (c.label) add(printed(`MOS ${c.label}`, S(26), S(5), { colour: c.kind === 'ceramic' ? 'iron' : 'moss-70' }), X(c.x), top + 0.007, Z(c.y + (c.kind === 'ceramic' ? 4.5 : 0)));
  }
  parts.add(legs);

  // Resistors, capacitors and diodes: a cylinder lying between its two pads,
  // with a tinned lead from each end down through the board. There are dozens,
  // so each kind is one instanced mesh, and every lead is in one more: a few
  // draws instead of hundreds, which keeps software WebGL and a phone smooth.
  // A unit cylinder (radius 1, height 1, along y) is scaled and turned into
  // place for each; `pivot` stands at the part's centre and turns it to lie
  // along its pads, and `piece` is one body or lead within it.
  const unit = new CylinderGeometry(1, 1, 1, 16);
  const thin = new CylinderGeometry(1, 1, 1, 6);
  const pivot = new Object3D();
  const piece = new Object3D();
  pivot.add(piece);
  const lists = { capacitor: [], resistor: [], diode: [], transistor: [], lead: [] };
  const put = (list, [x, y, z], [rx, rz], [sx, sy, sz]) => {
    piece.position.set(x, y, z);
    piece.rotation.set(rx, 0, rz);
    piece.scale.set(sx, sy, sz);
    pivot.updateMatrixWorld(true);
    lists[list].push(piece.matrixWorld.clone());
  };
  const LEAD = S(0.3);
  for (const a of AXIAL) {
    const len = Math.hypot(a.x1 - a.x0, a.y1 - a.y0);
    const body = Math.min(a.length, len - 2);
    const r = S(a.diameter / 2);
    pivot.position.set(X((a.x0 + a.x1) / 2), r, Z((a.y0 + a.y1) / 2));
    pivot.rotation.set(0, -Math.atan2(a.y1 - a.y0, a.x1 - a.x0), 0);
    put(a.kind, [0, 0, 0], [0, Math.PI / 2], [r, S(body), r]);
    // The leads: along the axis from the body's ends to above the pads, then down.
    const reach = (len - body) / 2;
    for (const end of [-1, 1]) {
      put('lead', [end * S(body / 2 + reach / 2), 0, 0], [0, Math.PI / 2], [LEAD, S(reach), LEAD]);
      put('lead', [end * S(len / 2), -r / 2, 0], [0, 0], [LEAD, r, LEAD]);
    }
    underPad(a.x0, a.y0);
    underPad(a.x1, a.y1);
  }
  pads.count = pad;
  scene.add(pads);

  // Transistors: a TO-92, a black body 4.8 by 3.7 mm (drawn as a squashed
  // cylinder, not the real half-round), on three legs, its top at the height measured.
  const tall = S(4.5), top92 = S(HEIGHTS.transistor);
  pivot.rotation.set(0, 0, 0);
  for (const t of TRANSISTORS) {
    pivot.position.set(X(t.x), 0, Z(t.y));
    put('transistor', [0, top92 - tall / 2, 0], [0, 0], [S(2.4), tall, S(2.4) * (3.7 / 4.8)]);
    for (const dx of [-1.27, 0, 1.27]) put('lead', [S(dx), (top92 - tall) / 2, 0], [0, 0], [LEAD, top92 - tall, LEAD]);
  }
  const material = { capacitor: M.capacitor, resistor: M.resistor, diode: M.chip, transistor: M.chip, lead: M.tin };
  for (const [name, list] of Object.entries(lists)) {
    if (list.length === 0) continue;
    const mesh = new InstancedMesh(name === 'lead' ? thin : unit, material[name], list.length);
    list.forEach((m, i) => mesh.setMatrixAt(i, m));
    parts.add(mesh);
  }
  // The trimmer, a tinned disc with a rotor, and the one disc capacitor, standing up.
  if (TRIMMER) add(new Mesh(new CylinderGeometry(S(5), S(5), S(HEIGHTS.trimmer), 24), M.tin), X(TRIMMER.x), S(HEIGHTS.trimmer) / 2, Z(TRIMMER.y));
  if (DISC) {
    const disc = add(new Mesh(new CylinderGeometry(S(DISC.width / 2), S(DISC.width / 2), S(DISC.thickness), 20), M.resistor), X(DISC.x), S(DISC.width / 2 + 2), Z(DISC.y));
    disc.rotation.x = Math.PI / 2;
    disc.rotation.z = DISC.alongX ? 0 : Math.PI / 2;
  }

  // The crystal: a tinned can lying flat, at the height measured.
  const can = add(new Mesh(new RoundedBoxGeometry(S(CRYSTAL.width), S(CRYSTAL.height), S(CRYSTAL.depth), 2, 0.12), M.tin), X(CRYSTAL.x), S(CRYSTAL.height) / 2, Z(CRYSTAL.y));
  can.userData.part = 'crystal';
  add(printed(NAME.text, S(NAME.width), S(NAME.depth), { size: 0.86 }), X(NAME.x), 0.004, Z(NAME.y));

  // The loose red wire this board carries, lying on it.
  if (WIRE.length > 1) {
    const curve = new CatmullRomCurve3(WIRE.map(([x, y]) => new Vector3(X(x), S(1), Z(y))));
    add(new Mesh(new TubeGeometry(curve, WIRE.length * 6, S(0.6), 6), M.wire), 0, 0, 0);
  }

  // The display: a dark window with six digits of seven segments in it.
  const HD = S(DISPLAY.height);
  add(new Mesh(new BoxGeometry(S(DISPLAY.width), HD, S(DISPLAY.depth)), M.window), X(DISPLAY.x), HD / 2, Z(DISPLAY.y));
  const digits = DISPLAY.digits.map((dx) => SEGMENTS.map(([u, v, len, across]) => {
    const w = S(DISPLAY.digitWidth), d = S(DISPLAY.digitDepth), t = S(0.8);
    const geometry = across ? new BoxGeometry(w * len, 0.02, t) : new BoxGeometry(t, 0.02, d * len);
    return add(new Mesh(geometry, M.off), X(dx) + (u - 0.5) * w, HD + 0.011, Z(DISPLAY.digitY) + (v - 0.5) * d);
  }));

  // The keypad: its own black circuit board, a bezel on it with a well for
  // each key (built as a frame and the bars between the wells), the 23 keys
  // down in their wells, and the SST slide switch on the bezel's top right.
  const KB = KEYPAD.board, HB = S(KEYPAD.boardHeight), HZ = S(KEYPAD.bezelHeight);
  add(new Mesh(new BoxGeometry(S(KB.width), S(1.6), S(KB.depth)), M.keypad), X(KB.x), HB - S(0.8), Z(KB.y));
  // The walls are one instanced unit box, scaled into place: one draw.
  const walls = [];
  const wall = (x0, y0, x1, y1) => {
    place.position.set(X((x0 + x1) / 2), (HZ + HB) / 2, Z((y0 + y1) / 2));
    place.scale.set(S(x1 - x0), HZ - HB, S(y1 - y0));
    place.updateMatrix();
    walls.push(place.matrix.clone());
  };
  const half = 5.1; // a well's half width: a 9.1 mm key with half a millimetre round it
  const { columns: cols, rows } = KEYPAD;
  const left = KEYPAD.x - KEYPAD.width / 2, right = KEYPAD.x + KEYPAD.width / 2, top = KEYPAD.y - KEYPAD.depth / 2, bottom = KEYPAD.y + KEYPAD.depth / 2;
  const xs = [left, ...cols.flatMap((c) => [c - half, c + half]), right];
  const ys = [top, ...rows.flatMap((r) => [r - half, r + half]), bottom];
  for (let i = 0; i < xs.length; i += 2) wall(xs[i], top, xs[i + 1], bottom);
  for (let j = 0; j < ys.length; j += 2) for (let i = 1; i < xs.length - 1; i += 2) wall(xs[i], ys[j], xs[i + 1], ys[j + 1]);
  // SST's cell is not a well: the switch sits on the bezel there.
  wall(cols[3] - half, rows[0] - half, cols[3] + half, rows[0] + half);
  const bezel = new InstancedMesh(new BoxGeometry(1, 1, 1), M.keypad, walls.length);
  walls.forEach((m, i) => bezel.setMatrixAt(i, m));
  place.scale.set(1, 1, 1);
  parts.add(bezel);
  const keys = new Map();
  const pickable = [];
  const keyH = S(2.5);
  const keyTop = S(KEYPAD.keyHeight) - keyH / 2;
  for (const k of KEYS) {
    const group = new Group();
    const cap = new Mesh(new RoundedBoxGeometry(S(KEYPAD.key), keyH, S(KEYPAD.key), 3, 0.06), M.key);
    const legend = printed(k.name, S(KEYPAD.key) * 0.8, S(KEYPAD.key) * 0.8, { size: k.name.length > 1 ? 0.42 : 0.6 });
    legend.position.y = keyH / 2 + 0.003;
    group.add(cap, legend);
    cap.userData.key = legend.userData.key = k.name;
    pickable.push(cap, legend);
    add(group, X(k.x), keyTop, Z(k.y));
    keys.set(k.name, { group, level: 0, target: 0, until: 0 });
  }
  const slot = add(new Mesh(new BoxGeometry(S(7), 0.05, S(2.6)), M.window), X(SST.x), HZ + 0.025, Z(SST.y));
  const knob = add(new Mesh(new BoxGeometry(S(2.6), 0.18, S(2.2)), M.knob), X(SST.x), HZ + 0.09, Z(SST.y));
  knob.userData.sst = slot.userData.sst = true;
  pickable.push(knob, slot);
  add(printed('SST', S(8), S(2.6), { colour: 'moss-70', size: 0.7 }), X(SST.x), HZ + 0.003, Z(SST.y - 4));

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

  // The tracks: loaded with the model, put on both faces, and switched by
  // the two buttons. If the map cannot load, the board stays plain, the buttons
  // are disabled and the status line says why; the rest of the model runs.
  const tracksButton = root.querySelector('[data-toggle-tracks]');
  const onlyButton = root.querySelector('[data-toggle-tracks-only]');
  let top_ = null, bottom_ = null;
  let tracksError = null;
  try {
    const maps = await trackMaps(TRACKS.src, {
      top: { cover: (d, i) => Math.max(d[i], d[i + 1]), mask: token('model-pcb'), copper: token('model-copper'), flip: false },
      bottom: { cover: (d, i) => d[i + 2], mask: token('model-pcb-under'), copper: token('model-copper'), flip: true },
    });
    const texture = (canvas, srgb) => {
      const t = new CanvasTexture(canvas);
      if (srgb) t.colorSpace = SRGBColorSpace;
      t.anisotropy = s.renderer.capabilities.getMaxAnisotropy();
      return t;
    };
    const face = (m) => {
      const surface = texture(m.surface, false);
      return new MeshStandardMaterial({ map: texture(m.colour, true), roughnessMap: surface, metalnessMap: surface, roughness: 1, metalness: 1, bumpMap: texture(m.relief, false), bumpScale: RELIEF });
    };
    top_ = face(maps.top);
    bottom_ = face(maps.bottom);
  } catch (error) {
    tracksError = error;
  }

  let tracksOn = Boolean(top_);
  let partsShown = true;
  let partsLevel = 1;
  const show = () => {
    board.material[2] = tracksOn ? top_ : M.board;
    board.material[3] = tracksOn ? bottom_ : M.under;
    tracksButton?.setAttribute('aria-pressed', String(tracksOn));
    onlyButton?.setAttribute('aria-pressed', String(!partsShown));
    root.dataset.modelTracks = tracksOn ? 'on' : 'off';
    root.dataset.modelParts = partsShown ? 'shown' : 'hidden';
  };
  if (top_) {
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

  root.modelLayers = () => ({ tracksLoaded: Boolean(top_), tracks: board.material[2] === top_ && Boolean(top_), underside: board.material[3] === bottom_ && Boolean(bottom_), partsVisible: parts.visible, partsLevel });

  const onScreen = (v) => {
    const rect = s.renderer.domElement.getBoundingClientRect();
    v.project(s.camera);
    return { x: rect.left + ((v.x + 1) / 2) * rect.width, y: rect.top + ((1 - v.y) / 2) * rect.height };
  };
  root.modelKeyPoint = (name) => {
    const k = keys.get(name);
    return onScreen(new Vector3(k.group.position.x, k.group.position.y + keyH / 2, k.group.position.z));
  };
  /** Where a point on the board, in mm as the layout has it, is on the screen: on the top face, or the underside when `below`. */
  root.modelBoardPoint = (x, y, below = false) => onScreen(new Vector3(X(x), below ? -H.board : 0, Z(y)));

  status.textContent = tracksError
    ? `The model is running, without its tracks, which could not load: ${tracksError.message}`
    : 'The model is running. Its digits show the machine\'s display.';
  s.start();
}
