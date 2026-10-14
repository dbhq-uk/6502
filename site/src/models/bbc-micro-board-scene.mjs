// The BBC Micro Model B's main board, Issue 7, as a three.js scene: all of
// the drawing for its inside model, so that the model's page entry (task 9b of
// the BBC Micro models plan) is thin: the stage, this module, the machine's
// panel and the test hooks. It exports no mount(root), so no page bundles it
// before the BBC Micro claims its models (the plan's re-plan of 13 October
// 2026), and its tests build its scene graph in Node.
//
// It is our own model, built from the measurements in
// ./bbc-micro-board-layout.mjs (tools/bbc-micro-model/ is the measuring): the
// board's outline and holes, its copper on both faces and its printed legend
// from flatbed scans of a bare board, and every fitted part on the scan's
// footprints, named from the Service Manual. It is labelled a model, not a
// photograph, everywhere it is shown.
//
// Both faces carry the board's copper, from one map: its red channel is the
// component side's copper, its green the solder side's and its blue the
// printed legend, traced to look at: the copper's connections are not
// verified, and the checks that would have shown them failed. trackMaps()
// turns the map into each face's colour, shine and relief, from tokens.
//
// Every colour is a token the stylesheet defines, read through `token` (the
// stage's reader in the browser, one from tokens.css in the tests).

import {
  BufferGeometry, Float32BufferAttribute, BoxGeometry, PlaneGeometry, CylinderGeometry, EdgesGeometry, LineSegments, LineBasicMaterial,
  Mesh, InstancedMesh, Object3D, Group, MeshStandardMaterial, MeshBasicMaterial, CanvasTexture, SRGBColorSpace, DoubleSide, Vector2,
  ShapeUtils,
} from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { BOARD, FITTED, CONNECTORS, PASSIVES, OTHERS, HEIGHTS, TRACKS } from './bbc-micro-board-layout.mjs';

// Millimetres on the board to the scene's centimetres, centred on the board; y is up.
export const X = (x) => (x - BOARD.width / 2) / 10;
export const Z = (y) => (y - BOARD.depth / 2) / 10;
const S = (mm) => mm / 10;
const T = S(BOARD.thickness);

// The lacquer, the copper and the print: how rough and how metallic each is.
const SURFACE = { mask: { roughness: 0.55, metalness: 0.1 }, copper: { roughness: 0.35, metalness: 0.25 }, print: { roughness: 0.8, metalness: 0 } };
// How far a track stands proud, as three.js's bump scale; how long the parts take to fade; a DIP's body above its legs' seat.
const RELIEF = 1.5;
const FADE_S = 0.3;
const STANDOFF = 0.8;

/** A box's four corners in board millimetres: x, y its centre, l along its rotation, w across. */
function corners({ x, y, l, w, rotation }) {
  const t = (rotation * Math.PI) / 180;
  const u = [(Math.cos(t) * l) / 2, (Math.sin(t) * l) / 2], v = [(-Math.sin(t) * w) / 2, (Math.cos(t) * w) / 2];
  return [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([a, b]) => [x + a * u[0] + b * v[0], y + a * u[1] + b * v[1]]);
}

/**
 * Where the camera starts and the box its target is held in (createStage's
 * options): the board, the connectors that run past its rear and front edges
 * and the modulator, with two centimetres to spare; it may go as close as a
 * chip filling the view, as far as the whole board small, and all the way
 * round, under it too.
 */
export function stageOptions() {
  const margin = 2;
  const pts = [[0, 0], [BOARD.width, BOARD.depth], ...[...CONNECTORS, ...OTHERS].flatMap(corners)];
  const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
  const bounds = [X(Math.min(...xs)) - margin, -2, Z(Math.min(...ys)) - margin, X(Math.max(...xs)) + margin, 2, Z(Math.max(...ys)) + margin];
  return { view: [0, 32, 29, 0, -0.5, 1.5], bounds, minDistance: 3, maxDistance: 150 };
}

/** Where a board point (mm) falls on the track map, as a texture coordinate: the map's first row is v = 1. */
const uvOf = (x, y) => [((x - TRACKS.originMm[0]) * TRACKS.pxPerMm) / TRACKS.width, 1 - ((y - TRACKS.originMm[1]) * TRACKS.pxPerMm) / TRACKS.height];

/**
 * The board as one geometry from its measured outline, with its holes cut
 * through: group 0 the component side (y = 0, the map's coordinates), group 1
 * the solder side (y = -T), group 2 the edges and the holes' walls.
 */
function boardGeometry() {
  const ring = (pts) => {
    const p = pts.map(([x, y]) => new Vector2(x, y));
    if (p.length > 1 && p[0].equals(p[p.length - 1])) p.pop();
    return p;
  };
  const contour = ring(BOARD.outline);
  if (ShapeUtils.isClockWise(contour)) contour.reverse();
  const holes = BOARD.holes.map((h) => {
    const p = Array.from({ length: 28 }, (_, i) => new Vector2(h.x + (h.d / 2) * Math.cos((i / 28) * 2 * Math.PI), h.y + (h.d / 2) * Math.sin((i / 28) * 2 * Math.PI)));
    return ShapeUtils.isClockWise(p) ? p : p.reverse();
  });
  const all = [...contour, ...holes.flat()];
  const faces = ShapeUtils.triangulateShape(contour.slice(), holes.map((h) => h.slice()));
  const pos = [], uv = [];
  const vertex = (p, y) => { pos.push(X(p.x), y, Z(p.y)); uv.push(...uvOf(p.x, p.y)); };
  // A face's triangles turned so that their normal points up (top) or down (underside).
  const face = (y, up) => {
    for (const [a, b, c] of faces) {
      const [pa, pb, pc] = [all[a], all[b], all[c]];
      const ny = (pb.y - pa.y) * (pc.x - pa.x) - (pb.x - pa.x) * (pc.y - pa.y);
      const tri = (ny > 0) === up ? [pa, pb, pc] : [pa, pc, pb];
      for (const p of tri) vertex(p, y);
    }
  };
  const geometry = new BufferGeometry();
  face(0, true);
  const topCount = pos.length / 3;
  face(-T, false);
  const bottomCount = pos.length / 3 - topCount;
  for (const loop of [contour, ...holes]) {
    for (let i = 0; i < loop.length; i++) {
      const p = loop[i], q = loop[(i + 1) % loop.length];
      for (const [v, y] of [[p, 0], [q, 0], [q, -T], [p, 0], [q, -T], [p, -T]]) { pos.push(X(v.x), y, Z(v.y)); uv.push(0, 0); }
    }
  }
  geometry.setAttribute('position', new Float32BufferAttribute(pos, 3));
  geometry.setAttribute('uv', new Float32BufferAttribute(uv, 2));
  geometry.addGroup(0, topCount, 0);
  geometry.addGroup(topCount, bottomCount, 1);
  geometry.addGroup(topCount + bottomCount, pos.length / 3 - topCount - bottomCount, 2);
  geometry.computeVertexNormals();
  return geometry;
}

/**
 * Builds the board into `scene`: the board itself, and in a group of its own
 * (so that Show tracks only can fade them out together) every fitted chip, on
 * its socket where the parts file has one, the connectors, the two-lead parts
 * and the modulator, crystal and relay. `printer`, if given, prints each chip's
 * part on its top (canvasPrinter below, in the browser); without it the chips
 * are plain, as in the tests.
 *
 * Returns the handles the page's entry uses: `board` (its materials: [top,
 * underside, edges]), `parts`, `plain` (the faces' materials with no tracks),
 * `chips` (by reference: the IC, its group, body, socket, tint for the access
 * mark, frame for the paged ROM and label, and its top's height in cm),
 * `pickable` (the meshes a pointer names a chip by), `mark(refs)` and
 * `page(ref)`.
 */
export function buildBoard(scene, token, { printer = null } = {}) {
  const standard = (name, opts = {}) => new MeshStandardMaterial({ color: token(name), roughness: 0.6, metalness: 0.1, ...opts });
  const M = {
    board: standard('model-bbc-pcb', { roughness: 0.55 }),
    under: standard('model-bbc-pcb-under', { roughness: 0.55 }),
    edge: standard('model-bbc-pcb', { roughness: 0.7, side: DoubleSide }),
    socket: standard('model-bbc-socket', { roughness: 0.7 }),
    tin: standard('model-tin', { roughness: 0.4, metalness: 0.3 }),
    chip: standard('iron', { roughness: 0.45 }),
    plastic: standard('veil', { roughness: 0.6 }),
    capacitor: standard('model-capacitor', { roughness: 0.5 }),
    resistor: standard('model-resistor', { roughness: 0.6 }),
    active: new MeshBasicMaterial({ color: token('model-active'), transparent: true, opacity: 0.85, toneMapped: false, depthWrite: false }),
  };
  const outline = (mesh, colour) => mesh.add(new LineSegments(new EdgesGeometry(mesh.geometry), new LineBasicMaterial({ color: token(colour) })));
  const turn = (deg) => -(deg * Math.PI) / 180;
  const tag = (o, ref, kind) => { o.userData.ref = ref; o.userData.kind = kind; return o; };

  // The board, with a hairline round its edge.
  const geometry = boardGeometry();
  const board = new Mesh(geometry, [M.board, M.under, M.edge]);
  board.add(new LineSegments(new EdgesGeometry(geometry, 30), new LineBasicMaterial({ color: token('circuit') })));
  scene.add(board);
  const parts = new Group();
  scene.add(parts);

  // The chips: a black body on its legs, or on a socket where the parts file
  // has one, its top at its typical height; a tint over its top for the access
  // mark, and round a paged ROM's top a frame for the paged mark.
  const socketH = S(HEIGHTS.socket.value);
  const place = new Object3D();
  const legCount = FITTED.filter((ic) => !ic.socketed).reduce((n, ic) => n + ic.pins, 0);
  const legs = new InstancedMesh(new BoxGeometry(S(0.5), 1, S(0.5)), M.tin, legCount);
  let leg = 0;
  const chips = new Map();
  const pickable = [];
  for (const ic of FITTED) {
    const top = S(ic.height);
    const seat = ic.socketed ? socketH : 0;
    const bottom = seat + S(STANDOFF);
    const group = new Group();
    group.rotation.y = turn(ic.rotation);
    group.position.set(X(ic.x), 0, Z(ic.y));
    parts.add(group);
    const body = tag(new Mesh(new BoxGeometry(S(ic.l), top - bottom, S(ic.w)), M.chip), ic.ref, 'body');
    body.position.y = (top + bottom) / 2;
    outline(body, 'pine');
    group.add(body);
    let socket = null;
    if (ic.socketed) {
      socket = tag(new Mesh(new BoxGeometry(S(ic.l + 1.5), socketH, S(ic.w + 1.5)), M.socket), ic.ref, 'socket');
      socket.position.y = socketH / 2;
      group.add(socket);
    }
    const tint = tag(new Mesh(new PlaneGeometry(S(ic.l) * 0.98, S(ic.w) * 0.94), M.active), ic.ref, 'tint');
    tint.rotation.x = -Math.PI / 2;
    tint.position.y = top + 0.003;
    tint.visible = false;
    group.add(tint);
    let frame = null;
    if (ic.paged) {
      // Four bars round the top, outside the body: a shape as well as a colour, apart from the tint.
      frame = tag(new Group(), ic.ref, 'frame');
      const bar = S(0.8), L = S(ic.l) + 2 * bar, W = S(ic.w) + 2 * bar;
      for (const [w, d, x, z] of [[L, bar, 0, (W - bar) / 2], [L, bar, 0, -(W - bar) / 2], [bar, W, (L - bar) / 2, 0], [bar, W, -(L - bar) / 2, 0]]) {
        const b = new Mesh(new BoxGeometry(w, S(0.4), d), M.active);
        b.position.set(x, top - S(0.2), z);
        frame.add(b);
      }
      frame.visible = false;
      group.add(frame);
    }
    let label = null;
    if (printer) {
      label = tag(printer(S(ic.l) * 0.8, S(ic.w) * 0.5), ic.ref, 'label');
      label.position.y = top + 0.006;
      label.print(ic.part);
      group.add(label);
    }
    for (const m of [body, tint, label]) if (m) pickable.push(m);
    chips.set(ic.ref, { ic, group, body, socket, tint, frame, label, top });
    if (ic.socketed) continue;
    // The legs, in two rows across the body, 2.54 mm apart along it.
    const rows = ic.w > 10 ? 15.24 : 7.62;
    const perSide = ic.pins / 2;
    const t = (ic.rotation * Math.PI) / 180;
    for (let i = 0; i < perSide; i++) {
      const along = (i - (perSide - 1) / 2) * 2.54;
      for (const side of [-1, 1]) {
        const across = (side * rows) / 2;
        place.position.set(X(ic.x + along * Math.cos(t) - across * Math.sin(t)), bottom / 2, Z(ic.y + along * Math.sin(t) + across * Math.cos(t)));
        place.scale.set(1, bottom, 1);
        place.rotation.set(0, turn(ic.rotation), 0);
        place.updateMatrix();
        legs.setMatrixAt(leg++, place.matrix);
      }
    }
  }
  parts.add(legs);

  // The connectors, as plain boxes: the rear ones and the headers on the
  // component side, the ribbon connectors on the solder side, under the board.
  const connectorMaterial = { phono: M.tin, din: M.plastic, dType: M.tin, idc: M.chip, header: M.chip };
  for (const c of CONNECTORS) {
    const h = S(c.h);
    const box = tag(new Mesh(new BoxGeometry(S(c.l), h, S(c.w)), connectorMaterial[c.kind] ?? M.chip), c.ref, 'connector');
    box.position.set(X(c.x), c.edge === 'underside' ? -T - h / 2 : h / 2, Z(c.y));
    box.rotation.y = turn(c.rotation);
    outline(box, 'circuit');
    parts.add(box);
  }

  // The modulator's can, the crystal and the cassette motor's relay.
  for (const o of OTHERS) {
    const h = S(o.h);
    const geometry = o.kind === 'crystal' ? new RoundedBoxGeometry(S(o.l), h, S(o.w), 2, 0.12) : new BoxGeometry(S(o.l), h, S(o.w));
    const mesh = tag(new Mesh(geometry, o.kind === 'relay' ? M.plastic : M.tin), o.ref, 'other');
    mesh.position.set(X(o.x), h / 2, Z(o.y));
    mesh.rotation.y = turn(o.rotation);
    if (o.kind !== 'crystal') outline(mesh, 'circuit');
    parts.add(mesh);
  }

  // Resistors and capacitors: hundreds, so each kind is one instanced mesh.
  // An axial part lies along its pads; a radial one, a dipped capacitor,
  // stands on its leads as a flat box.
  const lists = { axial: [], radial: [] };
  const pivot = new Object3D();
  const piece = new Object3D();
  pivot.add(piece);
  for (const p of PASSIVES) {
    pivot.position.set(X(p.x), 0, Z(p.y));
    pivot.rotation.set(0, turn(p.rotation), 0);
    if (p.kind === 'axial') {
      const r = S(p.diameter / 2);
      piece.position.set(0, r, 0);
      piece.rotation.set(0, 0, Math.PI / 2);
      piece.scale.set(r, S(p.length), r);
    } else {
      const h = S(HEIGHTS.radial.value);
      piece.position.set(0, h / 2, 0);
      piece.rotation.set(0, 0, 0);
      piece.scale.set(S(p.length), h, S(p.diameter));
    }
    pivot.updateMatrixWorld(true);
    lists[p.kind].push(piece.matrixWorld.clone());
  }
  const kinds = { axial: [new CylinderGeometry(1, 1, 1, 14), M.resistor], radial: [new BoxGeometry(1, 1, 1), M.capacitor] };
  for (const [kind, list] of Object.entries(lists)) {
    if (list.length === 0) continue;
    const mesh = new InstancedMesh(kinds[kind][0], kinds[kind][1], list.length);
    list.forEach((m, i) => mesh.setMatrixAt(i, m));
    mesh.userData.passives = kind;
    parts.add(mesh);
  }

  return {
    board, parts, chips, pickable, materials: M,
    plain: { top: M.board, bottom: M.under },
    /** Tints exactly the chips named, by reference. */
    mark(refs) { for (const [ref, c] of chips) c.tint.visible = refs.includes(ref); },
    /** Frames the paged ROM's chip, by reference, or none. */
    page(ref) { for (const [r, c] of chips) if (c.frame) c.frame.visible = r === ref; },
  };
}

/** The track map's channels, by face: the component side's copper is red and its print blue; the solder side's copper is green, with no print. */
export const CHANNELS = { top: { copper: 0, print: 2 }, bottom: { copper: 1, print: null } };

/** How much copper and print the map's pixel at byte `i` of `rgb` (RGBA) gives `face`, each 0 to 1. */
export function texel(rgb, i, face) {
  const c = CHANNELS[face];
  return { copper: rgb[i + c.copper] / 255, print: c.print === null ? 0 : rgb[i + c.print] / 255 };
}

/** A face's three colours, from the tokens, as sRGB bytes: its bare lacquer, its copper and its print. */
export function faceColours(face, token) {
  const bytes = (c) => { const o = { r: 0, g: 0, b: 0 }; c.getRGB(o, SRGBColorSpace); return [o.r, o.g, o.b].map((v) => Math.round(v * 255)); };
  return { mask: bytes(token(face === 'top' ? 'model-bbc-pcb' : 'model-bbc-pcb-under')), copper: bytes(token('model-copper')), print: bytes(token('model-bbc-print')) };
}

/**
 * One pixel of a face: its colour (the lacquer to the copper, then the print
 * over it), its roughness and metalness, and its relief (the copper, and the
 * print a little).
 */
export function shade({ copper: t, print: q }, { mask, copper, print }) {
  const colour = [0, 1, 2].map((c) => { const base = mask[c] + (copper[c] - mask[c]) * t; return Math.round(base + (print[c] - base) * q); });
  const rough = SURFACE.mask.roughness + (SURFACE.copper.roughness - SURFACE.mask.roughness) * t;
  const metal = SURFACE.mask.metalness + (SURFACE.copper.metalness - SURFACE.mask.metalness) * t;
  return { colour, roughness: rough + (SURFACE.print.roughness - rough) * q, metalness: metal + (SURFACE.print.metalness - metal) * q, relief: Math.max(t, q * 0.4) };
}

/**
 * In the browser: each face's three maps from the track map at `src`, as
 * canvases: colour (in sRGB), surface (roughness in green and metalness in
 * blue, as three.js reads them) and relief (softened, so a track's edge is a
 * slope rather than a cliff). The map is in the board's own frame and the
 * board's faces take their texture coordinates from it, so neither face is
 * turned here.
 */
export async function trackMaps(src, token) {
  const img = new Image();
  img.src = src;
  await img.decode();
  const { naturalWidth: w, naturalHeight: h } = img;
  const sheet = () => Object.assign(document.createElement('canvas'), { width: w, height: h });
  const read = sheet().getContext('2d', { willReadFrequently: true });
  read.drawImage(img, 0, 0);
  const rgb = read.getImageData(0, 0, w, h).data;
  const out = {};
  for (const face of Object.keys(CHANNELS)) {
    const colours = faceColours(face, token);
    const colour = sheet(), surface = sheet(), relief = sheet();
    const cg = colour.getContext('2d'), sg = surface.getContext('2d'), rg = relief.getContext('2d');
    const C = cg.createImageData(w, h), F = sg.createImageData(w, h), B = rg.createImageData(w, h);
    for (let i = 0; i < w * h * 4; i += 4) {
      const s = shade(texel(rgb, i, face), colours);
      C.data[i] = s.colour[0]; C.data[i + 1] = s.colour[1]; C.data[i + 2] = s.colour[2]; C.data[i + 3] = 255;
      F.data[i] = 255; F.data[i + 1] = 255 * s.roughness; F.data[i + 2] = 255 * s.metalness; F.data[i + 3] = 255;
      B.data[i] = B.data[i + 1] = B.data[i + 2] = 255 * s.relief; B.data[i + 3] = 255;
    }
    cg.putImageData(C, 0, 0);
    sg.putImageData(F, 0, 0);
    const soft = sheet();
    soft.getContext('2d').putImageData(B, 0, 0);
    rg.filter = 'blur(1.5px)';
    rg.drawImage(soft, 0, 0);
    out[face] = { colour, surface, relief };
  }
  return out;
}

/** Each face's material from trackMaps(): its colour, shine and relief, with no glow. */
export function trackMaterials(maps, anisotropy = 1) {
  const texture = (c, srgb) => {
    const t = new CanvasTexture(c);
    if (srgb) t.colorSpace = SRGBColorSpace;
    t.anisotropy = anisotropy;
    return t;
  };
  const face = (m) => {
    const surface = texture(m.surface, false);
    return new MeshStandardMaterial({ map: texture(m.colour, true), roughnessMap: surface, metalnessMap: surface, roughness: 1, metalness: 1, bumpMap: texture(m.relief, false), bumpScale: RELIEF });
  };
  return { top: face(maps.top), bottom: face(maps.bottom) };
}

/**
 * Show tracks and Show tracks only, on the board `buildBoard` gave, with the
 * faces' track materials (`top` and `bottom` null when the map could not load:
 * the board stays plain and the buttons change nothing). Tracks only, with no
 * tracks, would be an empty board, so turning the tracks off brings the parts
 * back, and tracks only puts the tracks on. `step(dt, reduced)`, each frame,
 * fades the parts to where they should be: at once with reduced motion.
 */
export function createLayers({ board, parts, plain }, { top, bottom }) {
  const loaded = Boolean(top && bottom);
  let tracks = loaded;
  let partsShown = true;
  let partsLevel = 1;
  const apply = () => {
    board.material[0] = tracks ? top : plain.top;
    board.material[1] = tracks ? bottom : plain.bottom;
  };
  // Fading the parts: every material in the group, each back to its own opacity and transparency when shown.
  const faded = new Map();
  parts.traverse((o) => [o.material].flat().forEach((m) => { if (m && !faded.has(m)) faded.set(m, { opacity: m.opacity, transparent: m.transparent }); }));
  const fade = (level) => {
    parts.visible = level > 0;
    for (const [m, was] of faded) {
      m.opacity = was.opacity * level;
      const transparent = level < 1 || was.transparent;
      if (m.transparent !== transparent) { m.transparent = transparent; m.needsUpdate = true; }
    }
  };
  apply();
  return {
    loaded,
    toggleTracks() {
      if (!loaded) return;
      tracks = !tracks;
      if (!tracks) partsShown = true;
      apply();
    },
    toggleOnly() {
      if (!loaded) return;
      partsShown = !partsShown;
      if (!partsShown) tracks = true;
      apply();
    },
    step(dt, reduced) {
      const target = partsShown ? 1 : 0;
      if (partsLevel === target) return;
      partsLevel = reduced ? target : partsLevel + Math.sign(target - partsLevel) * Math.min(Math.abs(target - partsLevel), dt / FADE_S);
      fade(partsLevel);
    },
    state: () => ({ tracks, partsShown, partsLevel }),
  };
}

/**
 * In the browser: printed text, a transparent canvas with the words on a
 * plane just above a part, in the token `colour` and the page's mono face.
 * Returns the printer buildBoard takes: (width, depth) to a plane with
 * `print(text)`.
 */
export function canvasPrinter(token, mono, { colour = 'moss-70', size = 0.5, weight = 500 } = {}) {
  const hex = `#${token(colour).getHexString()}`;
  return (width, depth) => {
    const canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = Math.max(32, Math.round((512 * depth) / width));
    const texture = new CanvasTexture(canvas);
    texture.colorSpace = SRGBColorSpace;
    texture.anisotropy = 4;
    const plane = new Mesh(new PlaneGeometry(width, depth), new MeshBasicMaterial({ map: texture, transparent: true, depthWrite: false }));
    plane.rotation.x = -Math.PI / 2;
    plane.print = (text) => {
      const g = canvas.getContext('2d');
      g.clearRect(0, 0, canvas.width, canvas.height);
      g.fillStyle = hex;
      g.font = `${weight} ${Math.round(canvas.height * size)}px ${mono}`;
      g.textAlign = 'center';
      g.textBaseline = 'middle';
      g.fillText(text, canvas.width / 2, canvas.height / 2 + 1, canvas.width * 0.94);
      texture.needsUpdate = true;
    };
    return plane;
  };
}
