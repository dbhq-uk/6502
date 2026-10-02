// Every machine that has a 3D model, by machine id. This map is the one place to
// add the next one.
//
// To add a machine's model (the BBC Micro is next):
//   1. Write src/models/<id>.js, a browser module that exports
//      `mount(root)`. It builds the scene with createStage() from ./stage.mjs,
//      which every model shares (renderer, camera controls, the arrow keys, the
//      reset button, the lazy render loop), and draws the machine itself. The
//      board or case is that machine's own code: do not generalise it.
//   2. Add an entry here, with the text alternative the page shows beside it.
//   3. scripts/build-models.mjs bundles every entry into public/models/<id>.js,
//      and the machine page (src/pages/machines/[id].astro) shows the model
//      section for any machine in this map. public/model-loader.js loads the
//      bundle only when that section nears the screen, or on a button press.
//
// A machine with no entry here gets no model section. Its photograph is a
// different matter: that is required of every running machine, in the registry.
import { describe as describeKim1 } from './kim-1-layout.mjs';

export const MODELS = {
  'kim-1': {
    /** The model section's accessible name. */
    label: '3D model of the KIM-1 board. Drag or use the arrow keys to turn it, scroll or use plus and minus to zoom, and click a key on the model to press it.',
    /** The text alternative, shown as the caption and read as the description. */
    about: describeKim1(),
  },
};

/** Where a model's bundle is served from. */
export const modelSrc = (id) => `/models/${id}.js`;
