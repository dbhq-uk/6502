// The NES's main board as a 3D model, the inside view on the machine's page.
// Bundled by scripts/build-models.mjs into public/models/nes-famicom-board.js,
// and loaded by public/model-loader.js only when its section nears the screen.
//
// It is our own model, built here from the measurements in
// ./nes-famicom-board-layout.mjs (tools/nes-model/ is the measuring): the
// NES-CPU-10's outline and holes, its copper on both faces and its printed
// legend from flatbed scans of a bare board, and every part on the scan's
// footprints. It draws both consoles, the NTSC NES-001's parts and the PAL
// NESE-001's, on the same board, and shows the one the page's region names. It
// is labelled a model, not a photograph, everywhere it is shown.
//
// It is tied to the running machine, not animated by hand. The page's panel
// (public/nes.js) carries `panel.nes` from the moment the page is ready: the
// model reads its region when it mounts, and once the machine runs it reads
// `panel.nes.accessCounts()`, the processor's reads and writes of each chip it
// can tell apart by address, every quarter second. A chip whose count moved is
// marked (its top tinted, its legend row data-accessed) and its rate written
// into its row. On `nes:start` (Start, Power, a new cartridge, a change of
// region) and `nes:region` the machine's counters start again from zero, so
// the model takes a fresh baseline and shows no rate until the next sample. On
// `nes:region` it swaps the parts, the crystal and the modulator, the caption,
// the label, the note and the legend's parts, with nothing downloaded.
//
// Both faces of the board carry the board's copper, from one map
// (scripts/build-models.mjs serves it beside the bundle): its red channel is the
// component side's copper, its green the solder side's and its blue the
// printed legend, traced to look at: the copper's connections are not
// verified. The map is loaded with the model, as a same-origin image, and turned
// here into each face's colour (the lacquer, the copper and the print, all
// tokens), its shine and its relief. Two buttons under the model switch the
// tracks off, and fade the parts out to leave the tracks.
//
// What a test can read off the section: data-model-region (the console drawn),
// data-model-accessed (the chips marked, by reference, space between),
// data-model-rates (the last rates, as JSON, or null), data-model-tracks (on or
// off) and data-model-parts (shown or hidden). And root.modelChipPoint(ref),
// where a chip's top is on the screen; root.modelBoardPoint(x, y, below), the
// same for a point on the board in mm, on its top face or its underside;
// root.modelLayers(), what the scene is showing; and root.modelView(), from the
// stage.

import {
  BufferGeometry, Float32BufferAttribute, BoxGeometry, PlaneGeometry, CylinderGeometry, EdgesGeometry, LineSegments, LineBasicMaterial,
  Mesh, InstancedMesh, Object3D, Group, MeshStandardMaterial, MeshBasicMaterial, CanvasTexture, SRGBColorSpace, DoubleSide, Vector2, Vector3,
  ShapeUtils, Raycaster,
} from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { createStage } from './stage.mjs';
import { BOARD, ICS, CONNECTORS, PASSIVES, OTHERS, HEIGHTS, REGIONS, CONSOLES, TRACKS } from './nes-famicom-board-layout.mjs';
import { COUNTED, createSampler } from './nes-famicom-access.mjs';

// Millimetres on the board to the scene's centimetres, centred on the board.
const X = (x) => (x - BOARD.width / 2) / 10;
const Z = (y) => (y - BOARD.depth / 2) / 10;
const S = (mm) => mm / 10;
const T = S(BOARD.thickness);

// The lacquer, the copper and the print: how rough and how metallic each is.
const SURFACE = { mask: { roughness: 0.55, metalness: 0.1 }, copper: { roughness: 0.35, metalness: 0.25 }, print: { roughness: 0.8, metalness: 0 } };
// How far a track stands proud, as three.js's bump scale; how long the parts take to fade; how often the counters are read.
const RELIEF = 1.5;
const FADE_S = 0.3;
const SAMPLE_MS = 250;

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
  return { geometry, outline: contour };
}

/**
 * Each face's three maps from the track map: colour (lacquer to copper, then
 * the print over it, in sRGB, from the tokens), surface (roughness in green and
 * metalness in blue, as three.js reads them) and relief (the copper, softened so
 * a track's edge is a slope rather than a cliff). The component side's copper
 * is the map's red channel and its print the blue; the solder side's copper is
 * the green. The map is in the board's own frame and the board's faces take
 * their texture coordinates from it, so neither face is turned here.
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
  const { mask: sm, copper: sc, print: sp } = SURFACE;
  const out = {};
  for (const [name, { copperOf, printOf, mask, copper, print }] of Object.entries(faces)) {
    const colour = sheet(), surface = sheet(), relief = sheet();
    const cg = colour.getContext('2d'), sg = surface.getContext('2d'), rg = relief.getContext('2d');
    const C = cg.createImageData(w, h), F = sg.createImageData(w, h), B = rg.createImageData(w, h);
    const m = channels(mask), k = channels(copper), p = channels(print);
    for (let i = 0; i < w * h * 4; i += 4) {
      const t = copperOf(rgb, i) / 255;
      const q = printOf ? printOf(rgb, i) / 255 : 0;
      for (let c = 0; c < 3; c++) {
        const base = m[c] + (k[c] - m[c]) * t;
        C.data[i + c] = base + (p[c] - base) * q;
      }
      C.data[i + 3] = 255;
      const rough = sm.roughness + (sc.roughness - sm.roughness) * t;
      const metal = sm.metalness + (sc.metalness - sm.metalness) * t;
      F.data[i] = 255;
      F.data[i + 1] = 255 * (rough + (sp.roughness - rough) * q);
      F.data[i + 2] = 255 * (metal + (sp.metalness - metal) * q);
      F.data[i + 3] = 255;
      B.data[i] = B.data[i + 1] = B.data[i + 2] = 255 * Math.max(t, q * 0.4);
      B.data[i + 3] = 255;
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
  // The modulator stands off the board's lower right edge, and the cartridge
  // connector off its lower edge: the camera's target is held over all of it,
  // with two centimetres to spare. It may go as close as a chip filling the
  // view, as far as the whole board small, and all the way round, under it too.
  const margin = 2;
  const far = Math.max(BOARD.depth, ...REGIONS.flatMap((r) => OTHERS[r].map((o) => o.y + o.l / 2)), ...CONNECTORS.map((c) => c.y + c.w / 2));
  const bounds = [X(0) - margin, -2, Z(0) - margin, X(BOARD.width) + margin, 2, Z(far) + margin];
  const s = createStage(root, { view: [0, 21, 21, 0, -0.5, 1.2], bounds, minDistance: 3, maxDistance: 150 });
  if (!s) return;
  const { scene, token, reduced, every } = s;
  const css = getComputedStyle(document.documentElement);
  const hex = (name) => `#${token(name).getHexString()}`;
  const mono = css.getPropertyValue('--font-mono').trim();

  const standard = (name, opts = {}) => new MeshStandardMaterial({ color: token(name), roughness: 0.6, metalness: 0.1, ...opts });
  const M = {
    board: standard('model-nes-pcb', { roughness: 0.55 }),
    under: standard('model-nes-pcb-under', { roughness: 0.55 }),
    edge: standard('model-nes-pcb', { roughness: 0.7, side: DoubleSide }),
    tin: standard('model-tin', { roughness: 0.4, metalness: 0.3 }),
    chip: standard('iron', { roughness: 0.45 }),
    plastic: standard('veil', { roughness: 0.6 }),
    header: standard('model-ceramic', { roughness: 0.6 }),
    capacitor: standard('model-capacitor', { roughness: 0.5 }),
    resistor: standard('model-resistor', { roughness: 0.6 }),
    active: new MeshBasicMaterial({ color: token('model-active'), transparent: true, opacity: 0.85, toneMapped: false, depthWrite: false }),
  };

  // The parts stand on the board in a group of their own, so that Show tracks
  // only can fade them out together. The board is not a part.
  const parts = new Group();
  scene.add(parts);
  const add = (mesh, x, y, z, parent = parts) => { mesh.position.set(x, y, z); parent.add(mesh); return mesh; };
  const outline = (mesh, colour) => mesh.add(new LineSegments(new EdgesGeometry(mesh.geometry), new LineBasicMaterial({ color: token(colour) })));
  const turn = (deg) => -(deg * Math.PI) / 180;

  // Printed text: a transparent canvas with the words, on a plane just above a
  // part. It can be printed again, for the other console's part.
  const printed = (width, depth, { colour = 'moss-70', size = 0.5, weight = 500 } = {}) => {
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
      g.fillStyle = hex(colour);
      g.font = `${weight} ${Math.round(canvas.height * size)}px ${mono}`;
      g.textAlign = 'center';
      g.textBaseline = 'middle';
      g.fillText(text, canvas.width / 2, canvas.height / 2 + 1, canvas.width * 0.94);
      texture.needsUpdate = true;
    };
    return plane;
  };

  // The board, with a hairline round its edge.
  const { geometry } = boardGeometry();
  const board = new Mesh(geometry, [M.board, M.under, M.edge]);
  scene.add(board);
  board.add(new LineSegments(new EdgesGeometry(geometry, 30), new LineBasicMaterial({ color: token('circuit') })));

  // The chips: every one is soldered in, so each is a black body on its legs,
  // its top at the typical height, the part this console has printed on it,
  // and a tint over its top for the mark.
  const place = new Object3D();
  const legs = new InstancedMesh(new BoxGeometry(S(0.5), 1, S(0.5)), M.tin, ICS.reduce((n, ic) => n + ic.pins, 0));
  let leg = 0;
  const chips = new Map();
  const pickable = [];
  const STANDOFF = 0.8;
  for (const ic of ICS) {
    const top = S(ic.height);
    const thick = top - S(STANDOFF);
    const group = new Group();
    group.rotation.y = turn(ic.rotation);
    add(group, X(ic.x), 0, Z(ic.y));
    const body = new Mesh(new BoxGeometry(S(ic.l), thick, S(ic.w)), M.chip);
    body.position.y = top - thick / 2;
    outline(body, 'pine');
    const tint = new Mesh(new PlaneGeometry(S(ic.l) * 0.98, S(ic.w) * 0.94), M.active);
    tint.rotation.x = -Math.PI / 2;
    tint.position.y = top + 0.003;
    tint.visible = false;
    const label = printed(S(ic.l) * 0.8, S(ic.w) * 0.5);
    label.position.y = top + 0.006;
    group.add(body, tint, label);
    for (const m of [body, tint, label]) { m.userData.ref = ic.ref; pickable.push(m); }
    chips.set(ic.ref, { ic, group, body, tint, label, top });
    // The legs, in two rows across the body, 2.54 mm apart along it.
    const rows = ic.w > 10 ? 15.24 : 7.62;
    const perSide = ic.pins / 2;
    const legTop = top - thick / 2;
    const t = (ic.rotation * Math.PI) / 180;
    for (let i = 0; i < perSide; i++) {
      const along = (i - (perSide - 1) / 2) * 2.54;
      for (const side of [-1, 1]) {
        const across = (side * rows) / 2;
        const x = ic.x + along * Math.cos(t) - across * Math.sin(t), y = ic.y + along * Math.sin(t) + across * Math.cos(t);
        place.position.set(X(x), legTop / 2, Z(y));
        place.scale.set(1, legTop, 1);
        place.rotation.set(0, turn(ic.rotation), 0);
        place.updateMatrix();
        legs.setMatrixAt(leg++, place.matrix);
      }
    }
  }
  parts.add(legs);

  // The connectors, as plain boxes: the cartridge connector clamps the edge
  // fingers, so it stands over both faces of the board at its edge; the others
  // sit on the component side.
  for (const c of CONNECTORS) {
    const h = S(c.h);
    const material = c.kind === 'controller' ? M.header : c.kind === 'cartridge' ? M.plastic : M.chip;
    const box = add(new Mesh(new BoxGeometry(S(c.l), h, S(c.w)), material), X(c.x), c.kind === 'cartridge' ? -T / 2 : h / 2, Z(c.y));
    box.rotation.y = turn(c.rotation);
    outline(box, 'circuit');
  }

  // Resistors, capacitors and the rest: dozens, so each kind is one instanced
  // mesh. An axial part lies along its pads, a radial one stands on its leads,
  // and the others (arrays, transistors, trimmers) are boxes.
  const unit = new CylinderGeometry(1, 1, 1, 14);
  const lists = { axial: [], radial: [], other: [] };
  const pivot = new Object3D();
  const piece = new Object3D();
  pivot.add(piece);
  for (const p of PASSIVES) {
    pivot.position.set(X(p.x), 0, Z(p.y));
    pivot.rotation.set(0, turn(p.rotation), 0);
    const r = S(p.diameter / 2);
    if (p.kind === 'axial') {
      piece.position.set(0, r, 0);
      piece.rotation.set(0, 0, Math.PI / 2);
      piece.scale.set(r, S(p.length), r);
    } else if (p.kind === 'radial') {
      const h = S(HEIGHTS.radial.value);
      piece.position.set(0, h / 2, 0);
      piece.rotation.set(0, 0, 0);
      piece.scale.set(r, h, r);
    } else {
      const h = S(HEIGHTS.other.value);
      piece.position.set(0, h / 2, 0);
      piece.rotation.set(0, 0, 0);
      piece.scale.set(S(p.length), h, S(p.diameter));
    }
    pivot.updateMatrixWorld(true);
    lists[p.kind].push(piece.matrixWorld.clone());
  }
  const kinds = { axial: [unit, M.resistor], radial: [unit, M.capacitor], other: [new BoxGeometry(1, 1, 1), M.chip] };
  for (const [kind, list] of Object.entries(lists)) {
    if (list.length === 0) continue;
    const mesh = new InstancedMesh(kinds[kind][0], kinds[kind][1], list.length);
    list.forEach((m, i) => mesh.setMatrixAt(i, m));
    parts.add(mesh);
  }

  // Each console's own crystal and modulator, both built now, one shown: a
  // change of region swaps them with nothing downloaded.
  const consoles = {};
  for (const region of REGIONS) {
    const group = new Group();
    for (const o of OTHERS[region]) {
      const h = S(o.h);
      if (o.kind === 'crystal') {
        const can = new Mesh(new RoundedBoxGeometry(S(o.l), h, S(o.w), 2, 0.12), M.tin);
        can.position.set(X(o.x), h / 2, Z(o.y));
        can.rotation.y = turn(o.rotation);
        const words = printed(S(o.l) * 0.85, S(o.w) * 0.5, { colour: 'iron' });
        words.print(o.part);
        words.position.set(X(o.x), h + 0.004, Z(o.y));
        group.add(can, words);
      } else {
        const box = new Mesh(new BoxGeometry(S(o.l), h, S(o.w)), M.tin);
        box.position.set(X(o.x), h / 2, Z(o.y));
        box.rotation.y = turn(o.rotation);
        outline(box, 'circuit');
        group.add(box);
      }
    }
    group.visible = false;
    parts.add(group);
    consoles[region] = group;
  }

  // ---- The console, and the page's words for it ----
  const stage = root.querySelector('[data-model-stage]');
  const status = root.querySelector('[data-model-status]');
  const rows = new Map([...root.querySelectorAll('[data-legend-ref]')].map((r) => [r.dataset.legendRef, r]));
  const rateCells = new Map([...root.querySelectorAll('[data-legend-rate]')].map((c) => [c.dataset.legendRate, c]));
  let region = null;
  const setRegion = (next) => {
    if (!REGIONS.includes(next) || next === region) return;
    region = next;
    root.dataset.modelRegion = region;
    for (const r of REGIONS) consoles[r].visible = r === region;
    for (const { ic, label } of chips.values()) label.print(ic.parts[region]);
    // The caption, the note and the legend's parts: every element marked for a
    // console, but the accessible names, which stay hidden.
    for (const el of root.querySelectorAll('[data-model-region]')) if (!el.hasAttribute('data-model-label')) el.hidden = el.dataset.modelRegion !== region;
    const name = root.querySelector(`[data-model-label][data-model-region="${region}"]`)?.textContent;
    if (name) stage.setAttribute('aria-label', name);
    if (root.querySelector(`#model-about-${region}`)) stage.setAttribute('aria-describedby', `model-about-${region}`);
  };

  // ---- The machine: the panel on the same page runs it ----
  const panel = document.querySelector('[data-nes]');
  const nes = () => panel?.nes ?? null;
  const regionNow = () => nes()?.region?.()?.toLowerCase() ?? REGIONS[0];
  setRegion(REGIONS.includes(regionNow()) ? regionNow() : REGIONS[0]);
  let running = Boolean(nes()?.running?.());
  const sampler = createSampler({ counts: () => nes()?.accessCounts?.() ?? null, now: () => performance.now() });
  const words = {
    idle: 'The model is running. The machine above is not running, so no chip is marked: press Start, and the chips the processor reads and writes are marked here.',
    running: 'The model is running, and so is the machine: a marked chip was read or written by the processor in the last quarter second.',
  };
  let marked = [];
  const mark = (refs) => {
    marked = refs;
    for (const [ref, c] of chips) c.tint.visible = refs.includes(ref);
    for (const [ref, row] of rows) row.toggleAttribute('data-accessed', refs.includes(ref));
    root.dataset.modelAccessed = refs.join(' ');
  };
  const show = (rates) => {
    root.dataset.modelRates = JSON.stringify(rates);
    for (const chip of COUNTED) {
      const cell = rateCells.get(chip);
      if (cell) cell.textContent = !running ? 'not running' : rates ? rates[chip].perSecond.toLocaleString('en-GB') : 'measuring';
    }
    mark(rates ? ICS.filter((ic) => ic.chip && rates[ic.chip].moved).map((ic) => ic.ref) : []);
  };
  const restart = () => {
    sampler.reset();
    show(null);
  };
  panel?.addEventListener('nes:start', () => {
    running = true;
    setRegion(regionNow());
    restart();
    status.textContent = words.running;
  });
  panel?.addEventListener('nes:region', (e) => {
    setRegion(e.detail?.region);
    restart();
  });
  show(null);
  setInterval(() => {
    if (!running) return;
    show(sampler.sample());
  }, SAMPLE_MS);

  // Pointing at a chip names it on the status line and marks its legend row; a
  // click or a tap does the same, for a screen with no pointer to hover.
  let pointed = null;
  const point = (ref) => {
    if (ref === pointed) return;
    pointed = ref;
    for (const [r, row] of rows) row.toggleAttribute('data-pointed', r === ref);
    if (!ref) return;
    const { ic } = chips.get(ref);
    const otherRegion = REGIONS.find((r) => r !== region);
    const counted = ic.chip ? (marked.includes(ref) ? ' Marked: the processor read or wrote it in the last quarter second.' : '') : ' It is never marked: its row in the legend says why.';
    status.textContent = `${ref}, ${ic.role}: ${ic.parts[region]} in this console, ${ic.parts[otherRegion]} in ${CONSOLES[otherRegion].a}.${counted}`;
  };
  s.onClick(() => pickable, (hit) => point(hit.userData.ref));
  const ray = new Raycaster();
  const ndc = new Vector2();
  const canvas = s.renderer.domElement;
  let pending = null;
  canvas.addEventListener('pointermove', (e) => {
    if (e.buttons || e.pointerType !== 'mouse') return;
    if (pending) return;
    pending = requestAnimationFrame(() => {
      pending = null;
      const rect = canvas.getBoundingClientRect();
      ndc.set(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
      ray.setFromCamera(ndc, s.camera);
      const hit = parts.visible ? ray.intersectObjects(pickable, false)[0] : null;
      if (hit) point(hit.object.userData.ref);
      else if (pointed) { for (const row of rows.values()) row.removeAttribute('data-pointed'); pointed = null; }
    });
  });

  // ---- The tracks: loaded with the model, put on both faces, and switched by the two buttons ----
  // If the map cannot load, the board stays plain, the buttons are disabled
  // and the status line says why; the rest of the model runs.
  const tracksButton = root.querySelector('[data-toggle-tracks]');
  const onlyButton = root.querySelector('[data-toggle-tracks-only]');
  let top_ = null, bottom_ = null;
  let tracksError = null;
  try {
    const maps = await trackMaps(TRACKS.src, {
      top: { copperOf: (d, i) => d[i], printOf: (d, i) => d[i + 2], mask: token('model-nes-pcb'), copper: token('model-copper'), print: token('model-nes-print') },
      bottom: { copperOf: (d, i) => d[i + 1], printOf: null, mask: token('model-nes-pcb-under'), copper: token('model-copper'), print: token('model-nes-print') },
    });
    const texture = (c, srgb) => {
      const t = new CanvasTexture(c);
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
  const layers = () => {
    board.material[0] = tracksOn ? top_ : M.board;
    board.material[1] = tracksOn ? bottom_ : M.under;
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
      layers();
      status.textContent = tracksOn ? 'The copper tracks are on the board, traced to look at: their connections are not verified.' : 'The tracks are off: the board is plain.';
    });
    onlyButton?.addEventListener('click', () => {
      partsShown = !partsShown;
      if (!partsShown) tracksOn = true;
      layers();
      status.textContent = partsShown ? 'The parts are back on the board.' : 'The parts are hidden, so only the board and its tracks show, traced to look at: their connections are not verified.';
    });
  } else {
    for (const b of [tracksButton, onlyButton]) if (b) { b.disabled = true; b.setAttribute('aria-pressed', 'false'); }
  }
  layers();

  // Fading the parts: every material in the group, each back to its own
  // opacity and transparency when shown.
  const faded = new Map();
  const collect = (m) => { if (m && !faded.has(m)) faded.set(m, { opacity: m.opacity, transparent: m.transparent }); };
  parts.traverse((o) => [o.material].flat().forEach(collect));
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

  root.modelLayers = () => ({ tracksLoaded: Boolean(top_), tracks: board.material[0] === top_ && Boolean(top_), underside: board.material[1] === bottom_ && Boolean(bottom_), partsVisible: parts.visible, partsLevel, region, running });

  const onScreen = (v) => {
    const rect = canvas.getBoundingClientRect();
    v.project(s.camera);
    return { x: rect.left + ((v.x + 1) / 2) * rect.width, y: rect.top + ((1 - v.y) / 2) * rect.height };
  };
  /** Where a chip's top is on the screen. */
  root.modelChipPoint = (ref) => {
    const c = chips.get(ref);
    if (!c) return null;
    return onScreen(new Vector3(X(c.ic.x), c.top, Z(c.ic.y)));
  };
  /** Where a point on the board, in mm as the layout has it, is on the screen: on the top face, or the underside when `below`. */
  root.modelBoardPoint = (x, y, below = false) => onScreen(new Vector3(X(x), below ? -T : 0, Z(y)));

  status.textContent = tracksError
    ? `The model is running, without its tracks, which could not load: ${tracksError.message}`
    : running ? words.running : words.idle;
  s.start();
}
