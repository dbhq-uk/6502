// Every machine that has a 3D model, by machine id. This map is the one place to
// add the next one.
//
// To add a machine's model (the BBC Micro is next):
//   1. Write src/models/<id>.js, a browser module that exports
//      `mount(root)`. It builds the scene with createStage() from ./stage.mjs,
//      which every model shares (renderer, camera controls and their keyboard,
//      mouse and touch handling, the reset button, the lazy render loop), and
//      draws the machine itself. The
//      board or case is that machine's own code: do not generalise it.
//   2. Add an entry here, with the text alternative the page shows beside it,
//      and, if it was measured, `made`: a function of its measurements
//      (src/data/<id>-model.json) giving the note on how it was made.
//   3. scripts/build-models.mjs bundles every entry into public/models/<id>.js,
//      and the machine page (src/pages/machines/[id].astro) shows the model
//      section for any machine in this map. public/model-loader.js loads the
//      bundle only when that section nears the screen, or on a button press.
//
// A machine with no entry here gets no model section. Its photograph is a
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
export const modelSrc = (id) => `/models/${id}.js`;
