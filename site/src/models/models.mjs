// Every 3D model, by module: the model's id, which is its machine's id or
// starts with it and a hyphen (the KIM-1's board is `kim-1`). The registry says
// which models a machine has (`models` in machines/registry.json, a list of
// { view, module }); this map says what each one is, and the two must agree both
// ways (tests/model.test.mjs). A machine has at most one model per view:
// `board` for a machine with no case, `outside` and `inside` for one with a case.
//
// To add a model (the NES's outside and the BBC Micro's two are next):
//   1. Write src/models/<module>.js, a browser module that exports
//      `mount(root)`. It builds the scene with createStage() from ./stage.mjs,
//      which every model shares (renderer, camera controls and their keyboard,
//      mouse and touch handling, the reset button, the lazy render loop), and
//      draws the machine itself. The
//      board or case is that machine's own code: do not generalise it.
//   2. Add an entry here, naming its `machine` and its `view`, with the text
//      alternative the page shows beside it and, if it was measured, `made`: a
//      function of its measurements (src/data/<module>-model.json, which every
//      claimed model must have) giving the note on how it was made. A model
//      that draws more than one console (the NES's, NTSC and PAL) lists them in
//      `regions` and keys `label`, `about` and `made` by region; read the words
//      through wordsFor(entry), never from the fields directly. A model may add
//      `help`, a sentence on what clicking it does, which the page puts with
//      its controls. It is here and
//      not in the registry because the regions are a fact about what the module
//      draws, and the registry's claim stays one module for one view.
//   3. Claim it in the machine's registry entry. src/lib/registry.mjs refuses a
//      claimed model whose module or results file is missing, and a machine
//      with a case that claims one of its two models without the other.
//   4. scripts/build-models.mjs bundles every entry into
//      public/models/<module>.js, and the machine page
//      (src/pages/machines/[id].astro) shows the model section for any machine
//      that claims a model. public/model-loader.js loads the bundle only when
//      that section nears the screen, or on a button press.
//
// A machine that claims no model gets no model section. Its photograph is a
// different matter: that is required of every running machine, in the registry.
import { describe as describeKim1, TRACKS as KIM1_TRACKS } from './kim-1-layout.mjs';
import { made as madeKim1 } from './kim-1-notes.mjs';
import { describe as describeBoard, chipLegend, LABELS as BOARD_LABELS, LEGEND_WORDS, REGIONS as BOARD_REGIONS, TRACKS as BOARD_TRACKS, TRACKS_HELP as BOARD_TRACKS_HELP } from './nes-famicom-board-layout.mjs';
import { made as madeBoard } from './nes-famicom-board-notes.mjs';
import { describe as describeCase, LABELS_FOR as CASE_LABELS, HELP as CASE_HELP, REGIONS as CASE_REGIONS } from './nes-famicom-case-layout.mjs';
import { made as madeCase } from './nes-famicom-case-notes.mjs';

/**
 * What the visitor is told about the controls, once, so the visible text under
 * the model and its aria-description cannot disagree. The wheel zooms only
 * while the model has focus or with Ctrl or Cmd held, so that it never traps
 * the page's scrolling (src/models/stage.mjs).
 */
export const CONTROLS = {
  /** On the canvas while the model has no focus, for a mouse. A label: no full stop. */
  hint: 'Click the model, then scroll to zoom',
  /** The same, for a touch screen. */
  hintTouch: 'Tap the model, then drag to turn and pinch to zoom',
  pointer: 'Drag to turn it, and right-drag or Shift-drag to pan. Click the model, then scroll to zoom, or hold Ctrl or Cmd and scroll. Double-click empty space to reset the view.',
  touch: 'On a touch screen, tap the model first: then one finger turns it, and two fingers pinch to zoom and pan.',
  keys: 'With the model focused, the arrow keys turn it, Shift and the arrow keys pan, plus and minus zoom, Home resets the view, and Escape lets go of it.',
  /** For a model with a track map: what its two buttons do. */
  tracks: 'Show tracks puts the copper tracks on the board or takes them off, and Show tracks only fades the parts out so the tracks can be followed.',
};

/** The two buttons of a model with a track map. Labels: no full stop. */
export const TRACK_BUTTONS = { tracks: 'Show tracks', only: 'Show tracks only' };

/** What a screen reader is told about the model's keys and wheel. */
export const CONTROLS_DESCRIPTION = `${CONTROLS.keys} The wheel zooms only while the model has focus, so the page still scrolls.`;

export const MODELS = {
  'kim-1': {
    /** The machine whose model this is, and which of its views: the registry's claim, word for word. */
    machine: 'kim-1',
    view: 'board',
    /** The model section's accessible name. */
    label: '3D model of the KIM-1 board. Click a key on the model to press it.',
    /** The text alternative, shown as the caption and read as the description. */
    about: describeKim1(),
    /** Where its track map is served from: scripts/build-models.mjs copies it there beside the bundle. */
    texture: KIM1_TRACKS.src,
    /** How it was made, from its measurements (src/data/kim-1-model.json): the note under it. */
    made: madeKim1,
  },
  'nes-famicom-case': {
    machine: 'nes',
    view: 'outside',
    /** It draws both consoles' cases, NTSC first; the page's region control names the one shown. */
    regions: [...CASE_REGIONS],
    label: { ntsc: CASE_LABELS.ntsc, pal: CASE_LABELS.pal },
    about: { ntsc: describeCase('ntsc'), pal: describeCase('pal') },
    /** What a click on its POWER and RESET does, said under it. */
    help: CASE_HELP,
    made: { ntsc: (f) => madeCase(f, 'ntsc'), pal: (f) => madeCase(f, 'pal') },
  },
  'nes-famicom-board': {
    machine: 'nes',
    view: 'inside',
    /** It draws both consoles' boards, NTSC first; the page's region control names the one shown. */
    regions: [...BOARD_REGIONS],
    label: { ntsc: BOARD_LABELS.ntsc, pal: BOARD_LABELS.pal },
    about: { ntsc: describeBoard('ntsc'), pal: describeBoard('pal') },
    texture: BOARD_TRACKS.src,
    /** What its track buttons do, in place of CONTROLS.tracks: its copper is traced to look at. */
    tracksHelp: BOARD_TRACKS_HELP,
    made: { ntsc: (f) => madeBoard(f, 'ntsc'), pal: (f) => madeBoard(f, 'pal') },
    /** The chip legend under the model: chipLegend(region) gives its rows, and these words follow it. */
    legend: chipLegend,
    legendWords: LEGEND_WORDS,
  },
};

/** Where a model's bundle is served from. */
export const modelSrc = (module) => `/models/${module}.js`;

/** The models a machine claims in the registry, in the order its page offers them: none when it claims none. */
export const modelsOf = (machine) => machine.models ?? [];

/**
 * A model's words for one console: its accessible name, its caption and its
 * note on how it was made. A model that draws more than one console (the NES's,
 * NTSC and PAL) lists them in `regions` and keys `label`, `about` and `made` by
 * region; the first region is the one shown until the page names another. A
 * model with no `regions` draws one console and has one of each.
 */
export function wordsFor(entry, region = null) {
  if (!entry.regions) return { label: entry.label, about: entry.about, made: entry.made };
  const r = region ?? entry.regions[0];
  if (!entry.regions.includes(r)) throw new Error(`this model draws ${entry.regions.join(' and ')}, not ${r}`);
  return { label: entry.label[r], about: entry.about[r], made: entry.made?.[r] };
}

/** What is wrong with a model's `regions`, as sentences: none for a model that has none. */
export function regionProblems(module, entry) {
  if (entry.regions === undefined) return [];
  const errors = [];
  if (!Array.isArray(entry.regions) || entry.regions.length === 0) return [`${module}: regions, when given, must be a non-empty list`];
  if (new Set(entry.regions).size !== entry.regions.length) errors.push(`${module}: a region is listed twice`);
  for (const r of entry.regions) {
    if (!/^[a-z]+$/.test(r)) errors.push(`${module}: region "${r}" must be a lower-case word`);
    for (const field of ['label', 'about']) {
      if (typeof entry[field]?.[r] !== 'string' || entry[field][r].length === 0) errors.push(`${module}: no ${field} for ${r}`);
    }
    if (entry.made !== undefined && typeof entry.made?.[r] !== 'function') errors.push(`${module}: made has no note for ${r}`);
  }
  for (const field of ['label', 'about', 'made']) {
    const extra = Object.keys(entry[field] ?? {}).filter((k) => !entry.regions.includes(k));
    if (typeof entry[field] === 'object' && extra.length) errors.push(`${module}: ${field} has ${extra.join(', ')}, which is not in regions`);
  }
  return errors;
}

/**
 * What the model section draws, one view for each console: its words, whether
 * the page hides it (all but the first region, which the visitor sees until
 * the model switches them), and the suffix that keeps its element ids apart.
 * A model with no `regions` is one view with no region and no suffix, so its
 * markup is what it always was.
 */
export function regionViews(entry) {
  if (!entry.regions) return [{ region: null, hidden: false, suffix: '', ...wordsFor(entry) }];
  return entry.regions.map((region, i) => ({ region, hidden: i > 0, suffix: `-${region}`, ...wordsFor(entry, region) }));
}

/**
 * Which console a model shows when the page's region is `region` (lower case),
 * and what it says about it (Review Focus 5 of the NES models plan): the
 * region's own console if the model draws it; otherwise its first console,
 * labelled as that console, and a sentence that there is no model of the
 * page's console, and why (the entry's `missing[region]`, or that none was
 * built). A model with no `regions` draws one console whatever the page says.
 * Both NES models draw both consoles, so on the NES's page the sentence is
 * never needed; it is here for a model that stops short of one.
 */
export function shownFor(entry, region) {
  if (!entry.regions) return { region: null, label: entry.label, missing: null };
  if (entry.regions.includes(region)) return { region, label: entry.label[region], missing: null };
  const first = entry.regions[0];
  const why = entry.missing?.[region] ?? 'none was built';
  return { region: first, label: entry.label[first], missing: `There is no model of the ${region.toUpperCase()} console, because ${why}, so this is the ${first.toUpperCase()} console's.` };
}
