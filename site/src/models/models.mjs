// Every 3D model, by module: the model's id, which is its machine's id or
// starts with it and a hyphen (the KIM-1's board is `kim-1`). The registry says
// which models a machine has (`models` in machines/registry.json, a list of
// { view, module }); this map says what each one is, and the two must agree both
// ways (tests/model.test.mjs). A machine has at most one model per view:
// `board` for a machine with no case, `outside` and `inside` for one with a case.
//
// To add a model (the BBC Micro's two are next):
//   1. Write src/models/<module>.js, a browser module that exports
//      `mount(root)`. It builds the scene with createStage() from ./stage.mjs,
//      which every model shares (renderer, camera controls and their keyboard,
//      mouse and touch handling, the reset button, the lazy render loop), and
//      draws the machine itself. The
//      board or case is that machine's own code: do not generalise it.
//   2. Add an entry here, naming its `machine` and its `view`, with the text
//      alternative the page shows beside it and, if it was measured, `made`: a
//      function of its measurements (src/data/<module>-model.json, which every
//      claimed model must have) giving the note on how it was made.
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
};

/** Where a model's bundle is served from. */
export const modelSrc = (module) => `/models/${module}.js`;

/** The models a machine claims in the registry, in the order its page offers them: none when it claims none. */
export const modelsOf = (machine) => machine.models ?? [];
