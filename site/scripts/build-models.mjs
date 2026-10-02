// Bundles each machine's 3D model, three.js and camera-controls included, into
// public/models/<id>.js: one same-origin module per model, which is what the
// site's CSP allows. The list is src/models/models.mjs, so a model added there
// is built without an edit here. The bundles are build outputs and git ignores
// them; esbuild keeps the bundled libraries' licence comments at the end.
//
// No page loads a bundle when it opens: public/model-loader.js imports one only
// when its section nears the screen, or when the visitor asks for it.
//
// A model's track map, if it has one (src/assets/tracks/<id>.webp, committed,
// made by tools/kim1-model/ at the repository root), is copied beside its bundle as
// public/models/<id>-tracks.webp, so it is fetched with the model and not before.
import fs from 'node:fs';
import { build } from 'esbuild';
import { MODELS } from '../src/models/models.mjs';

for (const id of Object.keys(MODELS)) {
  await build({
    entryPoints: [`src/models/${id}.js`],
    outfile: `public/models/${id}.js`,
    bundle: true,
    format: 'esm',
    minify: true,
    target: 'es2022',
    legalComments: 'eof',
    logLevel: 'warning',
  });
  const tracks = `src/assets/tracks/${id}.webp`;
  if (MODELS[id].texture) {
    if (!fs.existsSync(tracks)) throw new Error(`${id}'s model names a track map, ${MODELS[id].texture}, but ${tracks} is missing`);
    fs.copyFileSync(tracks, `public${MODELS[id].texture}`);
  }
}
