// The NES's case as a 3D model, the outside view on the machine's page.
// Bundled by scripts/build-models.mjs into public/models/nes-famicom-case.js,
// and loaded by public/model-loader.js only when its view's panel nears the
// screen.
//
// It is our own model, built here from the measurements in
// ./nes-famicom-case-layout.mjs (tools/nes-model/ is the measuring): the
// published size, which is not Nintendo's; the bottom shell's ends leaning in
// below a break; the cartridge door, the vents, the POWER and RESET buttons,
// the power light, the two controller ports, the connectors on the rear and the
// right side, and the underside with its expansion cover and feet, as the
// photographs and the design patent's drawings show them. It draws both
// consoles, the NTSC NES-001 and the PAL NESE-001, which differ in their words,
// and shows the one the page's region names. Its words are drawn by us, in the
// site's own type, into one canvas once the type has loaded: no image, no
// outline of anyone's lettering. It is labelled a model, not a photograph,
// everywhere it is shown. The case is drawn as made, in the site's greys, not
// yellowed as the PAL console photographed is.
//
// It is tied to the running machine, not animated by hand. The page's panel
// (public/nes.js) carries `panel.nes` from the moment the page is ready: the
// power light is lit and POWER is in while `panel.nes.running()`, read every
// frame; RESET goes down for PRESS_MS on `nes:reset`, however the reset came. A
// click on POWER presses the page's own Start button, so it does exactly what
// Start does; once the machine runs, it changes nothing, and the status line
// says the page has no power-off. A click on RESET calls `panel.nes.reset()`
// while the machine runs, which announces `nes:reset`; before Start it does
// nothing to any machine, and the status line says why. The model reads the
// region when it mounts, so a view loaded after a change of region draws the
// region the page has then, and on `nes:region` it swaps the words, the rear
// and the underside, with nothing downloaded.
//
// What a test can read off the view's root: data-model-region (the console
// drawn), data-model-led (lit or dark), data-model-power (in or out),
// data-model-presses (how many times RESET has gone down) and
// data-model-pressed (RESET while it is down). And root.modelButtonPoint(name),
// where POWER's or RESET's face is on the screen, and root.modelView(), from
// the stage.

import {
  BoxGeometry, PlaneGeometry, CylinderGeometry, ExtrudeGeometry, Shape, EdgesGeometry, LineSegments, LineBasicMaterial, BufferGeometry, Float32BufferAttribute,
  Mesh, Group, MeshStandardMaterial, MeshBasicMaterial, CanvasTexture, SRGBColorSpace, Vector3,
} from 'three';
import { createStage } from './stage.mjs';
import { followRegion } from './regions.mjs';
import { CASE, PROFILE, DOOR, VENTS, BUTTONS, LED, PORTS, REAR, LABELS, UNDERSIDE, FEET, REGIONS, PRESS_MS, STATUS, press } from './nes-famicom-case-layout.mjs';

// The case frame (mm, from the left rear corner at table level, x right, y to
// the front, z up) to the scene's centimetres, centred on the case.
const TOP = CASE.height + CASE.feet;
const X = (x) => (x - CASE.width / 2) / 10;
const Y = (z) => (z - TOP / 2) / 10;
const Z = (y) => (y - CASE.depth / 2) / 10;
const S = (mm) => mm / 10;
// How far a part drawn on a face stands off it, in mm, so the two never fight for the same pixels.
const SKIN = 0.4;
// The words: how many canvas pixels to a millimetre of the case.
const PX_PER_MM = 12;

/** How far the end of the case is set in at height z, from the measured profile. */
const points = [...PROFILE.points].sort((a, b) => a.z - b.z);
function insetAt(z) {
  if (z <= points[0].z) return points[0].inset;
  for (let i = 1; i < points.length; i++) {
    const a = points[i - 1], b = points[i];
    if (z <= b.z) return a.inset + ((b.inset - a.inset) * (z - a.z)) / (b.z - a.z);
  }
  return points.at(-1).inset;
}
/** The height of the case's lower surface at x: the base, or the leaning end where the base does not reach. */
function bottomAt(x) {
  const base = insetAt(CASE.feet);
  const from = Math.min(x, CASE.width - x);
  if (from >= base) return CASE.feet;
  // Up the leaning end until its inset is as far in as x is.
  let lo = CASE.feet, hi = CASE.seamZ;
  for (let i = 0; i < 30; i++) {
    const mid = (lo + hi) / 2;
    if (insetAt(mid) > from) lo = mid; else hi = mid;
  }
  return hi;
}

/** One shell, from height z0 to z1, as the end seen from the front, run the case's depth. */
function shell(z0, z1) {
  const zs = [...new Set([z0, ...points.map((p) => p.z).filter((z) => z > z0 && z < z1), z1])].sort((a, b) => a - b);
  const shape = new Shape();
  const left = [...zs].reverse().map((z) => [insetAt(z), z]);
  const right = zs.map((z) => [CASE.width - insetAt(z), z]);
  [...left, ...right].forEach(([x, z], i) => (i === 0 ? shape.moveTo(X(x), Y(z)) : shape.lineTo(X(x), Y(z))));
  shape.closePath();
  const geometry = new ExtrudeGeometry(shape, { depth: S(CASE.depth), bevelEnabled: false });
  geometry.translate(0, 0, Z(0));
  return geometry;
}

/**
 * Every word on the case, both consoles', drawn once into one canvas in the
 * site's own type after it has loaded, each in a box of its own; returns the
 * texture and, for each label, where its box is in it. A label is stretched
 * across the width it was measured to take on the case; one of several lines
 * is set left, at its natural width or narrower.
 */
async function wordsAtlas(labels, fonts) {
  await Promise.all(fonts.map((f) => document.fonts?.load(f).catch(() => null)));
  const W = 2048;
  const boxes = labels.map((l) => ({ l, w: Math.ceil(l.wMm * PX_PER_MM) + 4, h: Math.ceil(l.hMm * PX_PER_MM) + 4 }));
  let x = 0, y = 0, row = 0;
  for (const b of [...boxes].sort((a, c) => c.h - a.h)) {
    if (x + b.w > W) { x = 0; y += row; row = 0; }
    Object.assign(b, { x, y });
    x += b.w;
    row = Math.max(row, b.h);
  }
  const canvas = Object.assign(document.createElement('canvas'), { width: W, height: y + row });
  const g = canvas.getContext('2d');
  for (const b of boxes) {
    const lines = b.l.words.split('\n');
    const lineH = (b.h - 4) / lines.length;
    g.fillStyle = b.l.colour;
    g.textBaseline = 'middle';
    g.font = `${b.l.weight} ${Math.round(lineH * 0.82)}px ${b.l.family}`;
    lines.forEach((line, i) => {
      const width = g.measureText(line).width || 1;
      const fit = (b.w - 4) / width;
      g.save();
      g.translate(b.x + 2, b.y + 2 + lineH * (i + 0.5));
      g.scale(lines.length === 1 ? fit : Math.min(1, fit), 1);
      g.fillText(line, 0, 0);
      g.restore();
    });
  }
  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  texture.anisotropy = 4;
  return { texture, place: new Map(boxes.map((b) => [b.l, { u0: b.x / W, u1: (b.x + b.w) / W, v0: 1 - (b.y + b.h) / canvas.height, v1: 1 - b.y / canvas.height }])) };
}

/** A plane w by h (cm) showing one box of the atlas. */
function wordsPlane(w, h, material, at) {
  const plane = new PlaneGeometry(w, h);
  const uv = plane.attributes.uv;
  for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) ? at.u1 : at.u0, uv.getY(i) ? at.v1 : at.v0);
  return new Mesh(plane, material);
}

export async function mount(root) {
  // The camera's target is held over the case with two centimetres to spare;
  // it may go all the way round, under the case too. The start view is three
  // quarters from the front, from the left and a little above.
  const margin = 2;
  const bounds = [X(0) - margin, Y(0) - margin, Z(0) - margin, X(CASE.width) + margin, Y(TOP) + margin, Z(CASE.depth) + margin];
  const s = createStage(root, { view: [-27, 20, 36, 0.5, -1, 0.5], bounds, minDistance: 3, maxDistance: 150 });
  if (!s) return;
  const { scene, token, every } = s;
  const css = getComputedStyle(document.documentElement);
  const hex = (name) => `#${token(name).getHexString()}`;

  const standard = (name, opts = {}) => new MeshStandardMaterial({ color: token(name), roughness: 0.62, metalness: 0.02, ...opts });
  const M = {
    upper: standard('model-nes-case'),
    lower: standard('model-nes-case-dark'),
    band: standard('model-nes-band', { roughness: 0.5 }),
    button: standard('model-nes-button', { roughness: 0.55 }),
    hole: standard('void', { roughness: 0.9 }),
    sticker: standard('model-nes-sticker', { roughness: 0.8 }),
    metal: standard('model-tin', { roughness: 0.35, metalness: 0.4 }),
    ledOff: standard('model-led-off', { roughness: 0.3 }),
    ledOn: new MeshBasicMaterial({ color: token('model-led'), toneMapped: false }),
  };
  const line = (colour) => new LineBasicMaterial({ color: token(colour) });
  const seam = line('model-nes-band');
  const outline = (mesh, material = seam) => { mesh.add(new LineSegments(new EdgesGeometry(mesh.geometry), material)); return mesh; };
  const box = (w, h, d, material, x, y, z) => { const m = new Mesh(new BoxGeometry(w, h, d), material); m.position.set(x, y, z); scene.add(m); return m; };

  // ---- The two shells: the lower dark, the upper light, split at the seam ----
  scene.add(outline(new Mesh(shell(CASE.feet, CASE.seamZ), M.lower), line('model-nes-band')));
  scene.add(outline(new Mesh(shell(CASE.seamZ, TOP), M.upper), line('model-nes-case-dark')));

  // A part on a face, SKIN proud of it: front (y = depth), rear (y = 0) or top (z = TOP).
  const onFront = (x, z, w, h, material, proud = SKIN) => box(S(w), S(h), S(proud), material, X(x + w / 2), Y(z + h / 2), Z(CASE.depth) + S(proud) / 2);
  const onRear = (x, z, w, h, material, proud = SKIN) => box(S(w), S(h), S(proud), material, X(x + w / 2), Y(z + h / 2), Z(0) - S(proud) / 2);
  const onTop = (x, y, w, d, material, proud = SKIN) => box(S(w), S(proud), S(d), material, X(x + w / 2), Y(TOP) + S(proud) / 2, Z(y + d / 2));

  // ---- The black band over the right of the case: down the front, over the top's two ends, down the rear ----
  const band = CASE.band;
  onFront(band.front.x, CASE.feet, band.front.w, TOP - CASE.feet, M.band);
  onTop(band.top.x, 0, band.top.w, band.blackEnds.rearD, M.band);
  onTop(band.top.x, band.blackEnds.frontY, band.top.w, CASE.depth - band.blackEnds.frontY, M.band);
  const win = CASE.rearWindow;
  onRear(band.rear.x, win.z + win.h, band.rear.w, TOP - win.z - win.h, M.band);
  // The rear's window, where the connectors are, and the notch at the foot of the rear.
  onRear(win.x, win.z, win.w, win.h, M.band);
  onRear(CASE.notch.x, CASE.notch.z, CASE.notch.w, CASE.notch.h, M.hole);

  // ---- The vents: slats across the top between the band's black ends, and slots underneath ----
  for (const v of VENTS.list) {
    for (let i = 0; i < v.slots; i++) {
      const pitch = v.d / v.slots;
      const y = v.y + pitch * (i + 0.5);
      if (v.face === 'top') onTop(v.x + 1, y - pitch * 0.2, v.w - 2, pitch * 0.4, M.lower, 0.2);
      else box(S(v.w), S(0.4), S(pitch * 0.45), M.hole, X(v.x + v.w / 2), Y(bottomAt(v.x + v.w / 2)) - S(0.2), Z(y));
    }
  }

  // ---- The cartridge door, over the front's top edge, and its lip ----
  const door = new Group();
  door.add(outline(onFront(DOOR.front.x, DOOR.front.z, DOOR.front.w, DOOR.front.h, M.upper), line('model-nes-case-dark')));
  door.add(outline(onTop(DOOR.top.x, DOOR.top.y, DOOR.top.w, DOOR.top.d, M.upper), line('model-nes-case-dark')));
  scene.add(door);
  onFront(DOOR.lip.x, DOOR.lip.z, DOOR.lip.w, DOOR.lip.h, M.sticker, 0.8);

  // ---- The panel the buttons sit in, as a line round it ----
  {
    const p = CASE.panel;
    const g = new BufferGeometry();
    const f = Z(CASE.depth) + S(0.05);
    const c = [[p.x, p.z], [p.x + p.w, p.z], [p.x + p.w, p.z + p.h], [p.x, p.z + p.h]];
    g.setAttribute('position', new Float32BufferAttribute(c.flatMap((a, i) => { const b = c[(i + 1) % 4]; return [X(a[0]), Y(a[1]), f, X(b[0]), Y(b[1]), f]; }), 3));
    scene.add(new LineSegments(g, line('model-nes-band')));
  }

  // ---- The power light and the two buttons ----
  const led = onFront(LED.x, LED.z, LED.w, LED.h, M.ledOff, 1);
  const buttons = new Map();
  for (const b of BUTTONS.list) {
    const mesh = new Mesh(new BoxGeometry(S(b.w), S(b.h), S(b.d)), M.button);
    outline(mesh, line('model-nes-case-dark'));
    const out = Z(CASE.depth) + S(b.proudMm) - S(b.d) / 2;
    mesh.position.set(X(b.x + b.w / 2), Y(b.z + b.h / 2), out);
    mesh.userData = { button: b.name, out, in: out - S(b.travelMm) };
    scene.add(mesh);
    buttons.set(b.name, mesh);
  }

  // ---- The controller ports: a black frame with the socket's dark hole ----
  for (const p of PORTS.list) {
    onFront(p.x, p.z, p.w, p.h, M.band, 0.8);
    onFront(p.x + p.w * 0.2, p.z + p.h * 0.18, p.w * 0.6, p.h * 0.64, M.hole, 1.0);
  }

  // ---- Each console's rear and underside: built now, one shown ----
  const consoles = {};
  for (const region of REGIONS) {
    const group = new Group();
    group.visible = false;
    for (const r of REAR[region]) {
      if (r.face === 'rear') {
        const at = (m) => { m.position.set(X(r.centre.x), Y(r.centre.z), Z(0)); group.add(m); return m; };
        if (r.kind === 'RF jack') {
          const jack = at(new Mesh(new CylinderGeometry(S(r.w / 2), S(r.w / 2), S(9), 20), M.metal));
          jack.rotation.x = Math.PI / 2;
          jack.position.z -= S(4.5);
        } else if (r.kind === 'channel switch') {
          at(new Mesh(new BoxGeometry(S(r.w), S(r.h), S(1.5)), M.lower)).position.z -= S(0.75);
          at(new Mesh(new BoxGeometry(S(r.w * 0.35), S(r.h * 0.8), S(3)), M.band)).position.set(X(r.centre.x - r.w * 0.2), Y(r.centre.z), Z(0) - S(1.5));
        } else {
          at(new Mesh(new BoxGeometry(S(r.w), S(r.h), S(2)), M.band)).position.z -= S(1);
          const hole = at(new Mesh(new CylinderGeometry(S(r.w * 0.3), S(r.w * 0.3), S(0.4), 16), M.hole));
          hole.rotation.x = Math.PI / 2;
          hole.position.z -= S(2.1);
        }
      } else {
        // On the right end, which leans in below its break: the jack stands out from it, square to the case's side.
        const jack = new Mesh(new CylinderGeometry(S(r.d / 2), S(r.d / 2), S(8), 20), M.metal);
        jack.rotation.z = Math.PI / 2;
        jack.position.set(X(r.x) + S(4), Y(r.z), Z(r.y));
        const hole = new Mesh(new CylinderGeometry(S(r.d * 0.18), S(r.d * 0.18), S(8.2), 12), M.hole);
        hole.rotation.z = Math.PI / 2;
        hole.position.copy(jack.position);
        group.add(jack, hole);
      }
    }
    const u = UNDERSIDE[region];
    const under = (x, y) => new Vector3(X(x), Y(bottomAt(x)) - S(0.15), Z(y));
    const rect = (r) => {
      const c = [[r.x, r.y], [r.x + r.w, r.y], [r.x + r.w, r.y + r.d], [r.x, r.y + r.d]].map(([x, y]) => under(x, y));
      const g = new BufferGeometry();
      g.setAttribute('position', new Float32BufferAttribute(c.flatMap((a, i) => [...a.toArray(), ...c[(i + 1) % 4].toArray()]), 3));
      return new LineSegments(g, line('model-nes-band'));
    };
    // The expansion cover, a panel of its own; the moulded ribs and panels, as lines.
    const cover = new Mesh(new BoxGeometry(S(u.cover.w), S(0.6), S(u.cover.d)), M.lower);
    cover.position.copy(under(u.cover.x + u.cover.w / 2, u.cover.y + u.cover.d / 2)).y -= S(0.3);
    outline(cover);
    group.add(cover, rect(u.coverInner), ...u.straps.map(rect), ...u.panels.map(rect), ...Object.values(u.bumps).map(rect));
    for (const sc of u.screws) {
      const hole = new Mesh(new CylinderGeometry(S(sc.d / 2), S(sc.d / 2), S(0.4), 16), M.hole);
      hole.position.copy(under(sc.x, sc.y));
      group.add(hole);
    }
    scene.add(group);
    consoles[region] = group;
  }
  // The feet, the same on both.
  for (const f of FEET.list) {
    const foot = new Mesh(new CylinderGeometry(S(f.d / 2), S(f.d / 2), S(f.h), 24), M.band);
    foot.position.set(X(f.x), Y(f.h / 2), Z(f.y));
    scene.add(foot);
  }

  // ---- The words: both consoles', in the site's own type, in one canvas ----
  const head = css.getPropertyValue('--font-head').trim();
  const body = css.getPropertyValue('--font-body').trim();
  const ink = hex('model-nes-ink');
  const kinds = {
    front: { colour: ink, weight: 700, family: head },
    button: { colour: ink, weight: 600, family: body },
    rear: { colour: hex('model-nes-case'), weight: 600, family: body },
    bottom: { colour: hex('iron'), weight: 500, family: body },
  };
  const all = REGIONS.flatMap((region) => LABELS[region].map((l) => {
    const kind = l.face.startsWith('button:') ? 'button' : l.face;
    const [, , w, h] = l.box;
    return { region, label: l, words: l.words, wMm: w, hMm: h, kind, ...kinds[kind] };
  }));
  const words = { ntsc: new Group(), pal: new Group() };
  for (const g of Object.values(words)) { g.visible = false; scene.add(g); }
  const { texture, place } = await wordsAtlas(all, [`700 32px ${head}`, `600 32px ${body}`, `500 32px ${body}`]);
  const ink_ = new MeshBasicMaterial({ map: texture, transparent: true, depthWrite: false, toneMapped: false });
  for (const a of all) {
    const [bx, bz, w, h] = a.label.box;
    const plane = wordsPlane(S(w), S(h), ink_, place.get(a));
    if (a.kind === 'front') {
      plane.position.set(X(bx + w / 2), Y(bz + h / 2), Z(CASE.depth) + S(SKIN + 0.1));
      words[a.region].add(plane);
    } else if (a.kind === 'rear') {
      plane.rotation.y = Math.PI;
      plane.position.set(X(bx + w / 2), Y(bz + h / 2), Z(0) - S(SKIN + 0.1));
      words[a.region].add(plane);
    } else if (a.kind === 'button') {
      // On the button's face, so it goes in with it: one plane a console, each shown with its words.
      const button = buttons.get(a.label.face.slice('button:'.length));
      const b = BUTTONS.list.find((x) => x.name === button.userData.button);
      plane.position.set(S(bx + w / 2 - (b.x + b.w / 2)), S(bz + h / 2 - (b.z + b.h / 2)), S(b.d) / 2 + S(0.05));
      plane.userData.region = a.region;
      button.add(plane);
    } else {
      // A label stuck on the underside: its paper, then its words, facing down, the front at the top.
      const [, by] = a.label.box;
      const paper = new Mesh(new PlaneGeometry(S(w), S(h)), M.sticker);
      for (const m of [paper, plane]) m.rotation.x = Math.PI / 2;
      const at = under0(bx + w / 2, by + h / 2);
      paper.position.copy(at);
      plane.position.copy(at).y -= S(0.05);
      words[a.region].add(paper, plane);
    }
  }
  function under0(x, y) { return new Vector3(X(x), Y(bottomAt(x)) - S(0.5), Z(y)); }

  // ---- The console, and the page's words for it ----
  const status = root.querySelector('[data-model-status]');
  let region = null;
  // The caption, the note, the stage's name and description follow the page's region (followRegion, in ./regions.mjs);
  // a region this model does not draw keeps the first console and says so on the status line. Then the scene.
  const setRegion = (next) => {
    const was = region;
    region = followRegion(root, REGIONS, next, region);
    if (region === was) return;
    for (const r of REGIONS) consoles[r].visible = words[r].visible = r === region;
    for (const button of buttons.values()) for (const child of button.children) if (child.userData.region) child.visible = child.userData.region === region;
  };

  // ---- The machine: the panel on the same page runs it ----
  const panel = document.querySelector('[data-nes]');
  const nes = () => panel?.nes ?? null;
  const running = () => nes()?.running?.() === true;
  const regionNow = () => nes()?.region?.()?.toLowerCase() ?? REGIONS[0];
  setRegion(regionNow());

  // The light and POWER follow the machine, read every frame.
  let on = null;
  const show = (now) => {
    if (now === on) return;
    on = now;
    led.material = on ? M.ledOn : M.ledOff;
    const power = buttons.get('POWER');
    power.position.z = on ? power.userData.in : power.userData.out;
    root.dataset.modelLed = on ? 'lit' : 'dark';
    root.dataset.modelPower = on ? 'in' : 'out';
  };
  show(running());
  every(() => show(running()));
  panel?.addEventListener('nes:start', () => { show(running()); status.textContent = STATUS.running; setRegion(regionNow()); });
  panel?.addEventListener('nes:region', (e) => setRegion(e.detail?.region));

  // RESET goes down for PRESS_MS whenever the machine is reset, by the page's
  // button, by panel.nes.reset() or by a click on the model. There is no easing
  // either way, so reduced motion changes nothing here.
  let presses = 0;
  let up = null;
  root.dataset.modelPresses = '0';
  panel?.addEventListener('nes:reset', () => {
    const reset = buttons.get('RESET');
    reset.position.z = reset.userData.in;
    root.dataset.modelPresses = String(++presses);
    root.dataset.modelPressed = 'RESET';
    clearTimeout(up);
    up = setTimeout(() => { reset.position.z = reset.userData.out; delete root.dataset.modelPressed; }, PRESS_MS);
  });

  // A click on a button: POWER presses the page's Start; RESET resets a running machine (press, in the layout).
  s.onClick(() => [...buttons.values()], (hit) => {
    for (let o = hit; o; o = o.parent) if (o.userData?.button) { status.textContent = press(o.userData.button, panel); return; }
  });

  const canvas = s.renderer.domElement;
  /** Where a button's face is on the screen. */
  root.modelButtonPoint = (name) => {
    const b = buttons.get(name);
    if (!b) return null;
    const v = new Vector3(0, 0, S(BUTTONS.list.find((x) => x.name === name).d) / 2);
    b.localToWorld(v);
    v.project(s.camera);
    const rect = canvas.getBoundingClientRect();
    return { x: rect.left + ((v.x + 1) / 2) * rect.width, y: rect.top + ((1 - v.y) / 2) * rect.height };
  };

  if (!root.dataset.modelMissing) status.textContent = running() ? STATUS.running : STATUS.idle;
  s.start();
}
