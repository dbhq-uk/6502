// Bundles the chip page's script, three.js and camera-controls included, into
// public/chip.js: one same-origin module, which is what the site's CSP allows.
// The bundle is a build output and git ignores it; the third-party code in it
// comes from node_modules at the versions package-lock.json pins, and
// esbuild keeps their licence comments at the end of the file.
import { build } from 'esbuild';

await build({
  entryPoints: ['src/chip/main.js'],
  outfile: 'public/chip.js',
  bundle: true,
  format: 'esm',
  minify: true,
  target: 'es2022',
  legalComments: 'eof',
  logLevel: 'warning',
});
