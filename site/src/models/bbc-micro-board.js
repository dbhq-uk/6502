// The BBC Micro Model B's main board, Issue 7, as a 3D model: the inside view
// on the machine's page. Bundled by scripts/build-models.mjs into
// public/models/bbc-micro-board.js, and loaded by public/model-loader.js only
// when its view's panel is shown and nears the screen.
//
// A thin entry: all of the drawing is ./bbc-micro-board-scene.mjs, and every
// word is ./bbc-micro-board-layout.mjs. This module puts the scene on the
// shared stage and ties it to the running machine and the page.
//
// The copper on both faces comes from one track map, served beside the bundle,
// traced to look at: its connections are not verified, and the checks that
// would have shown them failed. Two buttons under the model take the tracks
// off, and fade the parts out to leave the tracks.
//
// It is tied to the running machine, not animated by hand. The page's panel
// (public/bbc-micro.js) carries `panel.bbc` once the machine runs: every
// quarter second the model reads `panel.bbc.accessCounts()`, the processor's
// reads and writes of each chip it reaches by address, marks each chip whose
// count moved (its top tinted, its legend row data-accessed) and writes its rate
// into its row; and it reads `panel.bbc.romSlot()` and frames the paged ROM,
// BASIC or the DFS. On `bbc:start` the machine's counters start from zero, so
// the model takes a fresh baseline and shows no rate until the next sample.
// Pointing at a chip names it on the status line and marks its legend row.
//
// What a test can read off the view's root: data-model-accessed (the chips
// marked, by reference, space between), data-model-paged (the framed ROM's
// reference, or empty), data-model-rates (the last rates, as JSON, or null),
// data-model-tracks (on or off) and data-model-parts (shown or hidden). And
// root.modelChipPoint(ref), where a chip's top is on the screen;
// root.modelBoardPoint(x, y, below), the same for a point on the board in mm,
// on its top face or its underside; root.modelLayers(), what the scene is
// showing; and root.modelView(), from the stage.

import { Vector2, Vector3, Raycaster } from 'three';
import { createStage } from './stage.mjs';
import { createSampler } from './access.mjs';
import { BOARD, COUNTED, TRACKS, STATUS, marksFrom, pagedRef, pointWords } from './bbc-micro-board-layout.mjs';
import { X, Z, buildBoard, stageOptions, trackMaps, trackMaterials, createLayers, canvasPrinter } from './bbc-micro-board-scene.mjs';

// How often the counters are read.
const SAMPLE_MS = 250;

export async function mount(root) {
  const s = createStage(root, stageOptions());
  if (!s) return;
  const { scene, token, reduced, every } = s;
  const mono = getComputedStyle(document.documentElement).getPropertyValue('--font-mono').trim();
  const board = buildBoard(scene, token, { printer: canvasPrinter(token, mono) });

  const status = root.querySelector('[data-model-status]');
  const rows = new Map([...root.querySelectorAll('[data-legend-ref]')].map((r) => [r.dataset.legendRef, r]));
  const rateCells = new Map([...root.querySelectorAll('[data-legend-rate]')].map((c) => [c.dataset.legendRate, c]));

  // ---- The machine: the panel on the same page runs it ----
  const panel = document.querySelector('[data-bbc]');
  const bbc = () => panel?.bbc ?? null;
  let running = Boolean(bbc());
  const sampler = createSampler({ counted: COUNTED, counts: () => bbc()?.accessCounts?.() ?? null, now: () => performance.now() });
  let marked = [];
  let paged = null;
  const show = (rates) => {
    root.dataset.modelRates = JSON.stringify(rates);
    for (const chip of COUNTED) {
      const cell = rateCells.get(chip);
      if (cell) cell.textContent = !running ? 'not running' : rates ? rates[chip].perSecond.toLocaleString('en-GB') : 'measuring';
    }
    marked = marksFrom(rates);
    board.mark(marked);
    for (const [ref, row] of rows) row.toggleAttribute('data-accessed', marked.includes(ref));
    root.dataset.modelAccessed = marked.join(' ');
    paged = running ? pagedRef(bbc()?.romSlot?.()) : null;
    board.page(paged);
    root.dataset.modelPaged = paged ?? '';
  };
  const restart = () => {
    sampler.reset();
    show(null);
  };
  panel?.addEventListener('bbc:start', () => {
    running = true;
    status.textContent = STATUS.running;
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
    const words = ref ? pointWords(ref, { marked, paged }) : null;
    if (words) status.textContent = words;
  };
  s.onClick(() => board.pickable, (hit) => point(hit.userData.ref));
  const ray = new Raycaster();
  const ndc = new Vector2();
  const canvas = s.renderer.domElement;
  let pending = null;
  canvas.addEventListener('pointermove', (e) => {
    if (e.buttons || e.pointerType !== 'mouse' || pending) return;
    pending = requestAnimationFrame(() => {
      pending = null;
      const rect = canvas.getBoundingClientRect();
      ndc.set(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
      ray.setFromCamera(ndc, s.camera);
      const hit = board.parts.visible ? ray.intersectObjects(board.pickable, false)[0] : null;
      if (hit) point(hit.object.userData.ref);
      else if (pointed) point(null);
    });
  });

  // ---- The tracks: loaded with the model, put on both faces, and switched by the two buttons ----
  // If the map cannot load, the board stays plain, the buttons are disabled and
  // the status line says why; the rest of the model runs.
  let faces = { top: null, bottom: null };
  let tracksError = null;
  try {
    faces = trackMaterials(await trackMaps(TRACKS.src, token), s.renderer.capabilities.getMaxAnisotropy());
  } catch (error) {
    tracksError = error;
  }
  const layers = createLayers(board, faces);
  const tracksButton = root.querySelector('[data-toggle-tracks]');
  const onlyButton = root.querySelector('[data-toggle-tracks-only]');
  const shown = () => {
    const { tracks, partsShown } = layers.state();
    tracksButton?.setAttribute('aria-pressed', String(tracks));
    onlyButton?.setAttribute('aria-pressed', String(!partsShown));
    root.dataset.modelTracks = tracks ? 'on' : 'off';
    root.dataset.modelParts = partsShown ? 'shown' : 'hidden';
  };
  if (layers.loaded) {
    tracksButton?.addEventListener('click', () => {
      layers.toggleTracks();
      shown();
      status.textContent = layers.state().tracks ? STATUS.tracksOn : STATUS.tracksOff;
    });
    onlyButton?.addEventListener('click', () => {
      layers.toggleOnly();
      shown();
      status.textContent = layers.state().partsShown ? STATUS.partsShown : STATUS.partsHidden;
    });
  } else {
    for (const b of [tracksButton, onlyButton]) if (b) { b.disabled = true; b.setAttribute('aria-pressed', 'false'); }
  }
  shown();
  every((dt) => layers.step(dt, reduced));

  const T = BOARD.thickness / 10;
  root.modelLayers = () => ({
    tracksLoaded: layers.loaded,
    tracks: layers.loaded && board.board.material[0] === faces.top,
    underside: layers.loaded && board.board.material[1] === faces.bottom,
    partsVisible: board.parts.visible,
    partsLevel: layers.state().partsLevel,
    running,
  });
  const onScreen = (v) => {
    const rect = canvas.getBoundingClientRect();
    v.project(s.camera);
    return { x: rect.left + ((v.x + 1) / 2) * rect.width, y: rect.top + ((1 - v.y) / 2) * rect.height };
  };
  /** Where a chip's top is on the screen. */
  root.modelChipPoint = (ref) => {
    const c = board.chips.get(ref);
    return c ? onScreen(new Vector3(X(c.ic.x), c.top, Z(c.ic.y))) : null;
  };
  /** Where a point on the board, in mm as the layout has it, is on the screen: on the top face, or the underside when `below`. */
  root.modelBoardPoint = (x, y, below = false) => onScreen(new Vector3(X(x), below ? -T : 0, Z(y)));

  status.textContent = tracksError
    ? `The model is running, without its tracks, which could not load: ${tracksError.message}`
    : running ? STATUS.running : STATUS.idle;
  s.start();
}
