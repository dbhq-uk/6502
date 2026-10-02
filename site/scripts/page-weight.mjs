// What a built page loads when it opens, and what it loads only later: run
// after a build.
//
//   node scripts/page-weight.mjs /machines/kim-1/
//
// "When it opens" is the HTML (stylesheets are inlined into it), every script
// it names and every same-origin script those import, and the image each <img>
// names as its src. A browser picks a smaller copy from a srcset when it can, so
// the image figure is an upper bound. Fonts, the machine's WebAssembly and its
// ROM are fetched by the page's scripts and stylesheet and are not counted here.
// "Later" is each 3D model bundle the page names in data-model-src, which
// public/model-loader.js imports only when the visitor reaches the model.
// Sizes are bytes, as built and gzipped at level 9.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const dist = 'dist';
const url = process.argv[2] ?? '/machines/kim-1/';
const read = (p) => fs.readFileSync(path.join(dist, p));
const html = read(path.join(url, 'index.html'));
const text = html.toString();

const opens = new Map([[`${url} (html)`, html]]);
const queue = [...text.matchAll(/<script\b[^>]*\bsrc="(\/[^"]+)"/g)].map((m) => m[1]);
while (queue.length > 0) {
  const s = queue.shift();
  if (opens.has(s)) continue;
  const b = read(s);
  opens.set(s, b);
  for (const m of b.toString().matchAll(/^import\b[^;]*from\s+["'](\/[^"']+\.js)["']/gm)) queue.push(m[1]);
}
for (const m of text.matchAll(/<img\b[^>]*\bsrc="(\/[^"]+)"/g)) opens.set(m[1], read(m[1]));
const later = new Map([...text.matchAll(/data-model-src="(\/[^"]+)"/g)].map((m) => [m[1], read(m[1])]));

const report = (title, files) => {
  let raw = 0;
  let gz = 0;
  console.log(title);
  for (const [name, bytes] of files) {
    const g = zlib.gzipSync(bytes, { level: 9 }).length;
    raw += bytes.length;
    gz += g;
    console.log(`  ${name}\t${bytes.length}\t${g}`);
  }
  console.log(`  total\t${raw}\t${gz}`);
};
report(`${url} when it opens: file, bytes, gzipped`, opens);
if (later.size > 0) report('loaded later, when the visitor reaches the model:', later);
