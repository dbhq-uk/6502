// Measures the KIM-1 board on its photograph, for the 3D model's scale. Run it
// on the full-size source, which is not committed (it is over 1 MB); where to
// fetch it, and its hash, are in src/assets/photos/README.md.
//
//   node scripts/measure-kim1-photo.mjs path/to/MOS_KIM-1_IMG_4211_cropped.jpg
//
// The board is where the photograph stops being black (the sum of a pixel's
// three channels over 90). The gold contacts are counted down a strip of the
// left tabs, where red is well above blue. Their pitch, against the 0.156 inch
// of the KIM-1's connectors, gives the scale; the board's size follows. The
// numbers in src/models/kim-1-layout.mjs are this script's output, rounded.
import sharp from 'sharp';

const file = process.argv[2];
if (!file) {
  console.error('usage: node scripts/measure-kim1-photo.mjs <the full-size photograph>');
  process.exit(2);
}
const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
const { width: W, height: H, channels: C } = info;
const px = (x, y) => { const i = (y * W + x) * C; return [data[i], data[i + 1], data[i + 2]]; };
const lit = (x, y) => px(x, y).reduce((a, b) => a + b, 0) > 90;
console.log(`image ${W} x ${H} pixels`);

// Rows and columns where at least 30% of the pixels are lit.
const rowShare = (y) => { let n = 0; for (let x = 0; x < W; x++) n += lit(x, y); return n / W; };
const colShare = (x) => { let n = 0; for (let y = 0; y < H; y++) n += lit(x, y); return n / H; };
let top = 0; while (rowShare(top) <= 0.3) top++;
let bottom = H - 1; while (rowShare(bottom) <= 0.3) bottom--;
let right = W - 1; while (colShare(right) <= 0.3) right--;
// The body's left edge: the first lit pixel on a row between the two tabs.
const firstLit = (y) => { let x = 0; while (x < W && !lit(x, y)) x++; return x; };
const middle = Math.round((top + bottom) / 2);
const left = firstLit(middle);
const tabLeft = Math.min(...Array.from({ length: 20 }, (_, k) => firstLit(top + Math.round(((bottom - top) * (k + 0.5)) / 20))));
console.log(`board body: x ${left} to ${right}, y ${top} to ${bottom}: ${right - left} x ${bottom - top} pixels; the tabs reach x ${tabLeft}`);

// The tabs: runs of lit pixels down a column inside them.
const tabX = tabLeft + 30;
const runs = [];
for (let y = 0, start = -1; y <= H; y++) {
  const on = y < H && lit(tabX, y);
  if (on && start < 0) start = y;
  if (!on && start >= 0) { if (y - start > 200) runs.push([start, y - 1]); start = -1; }
}
console.log(`tabs at x ${tabX}: ${runs.map(([a, b]) => `y ${a} to ${b}`).join(', ')}`);

// The contacts: runs where the strip's mean red is 60 over its mean blue.
const strip = [tabLeft + 8, tabLeft + 88];
const gold = [];
for (let y = 0, start = -1; y <= H; y++) {
  let on = false;
  if (y < H) {
    let r = 0, b = 0;
    for (let x = strip[0]; x < strip[1]; x++) { const p = px(x, y); r += p[0]; b += p[2]; }
    on = (r - b) / (strip[1] - strip[0]) > 60;
  }
  if (on && start < 0) start = y;
  if (!on && start >= 0) { if (y - start > 15) gold.push((start + y - 1) / 2); start = -1; }
}
const perTab = runs.map(([a, b]) => gold.filter((c) => c >= a && c <= b));
const pitches = perTab.map((cs) => (cs.at(-1) - cs[0]) / (cs.length - 1));
console.log(`contacts: ${perTab.map((cs) => cs.length).join(' and ')}; pitch ${pitches.map((p) => p.toFixed(2)).join(' and ')} pixels`);
const pxPerMm = pitches.reduce((a, b) => a + b, 0) / pitches.length / (0.156 * 25.4);
const mm = (p) => (p / pxPerMm).toFixed(1);
console.log(`scale ${pxPerMm.toFixed(2)} pixels to the millimetre, from 0.156 inch contacts`);
console.log(`board ${mm(right - left)} x ${mm(bottom - top)} mm; tabs stand ${mm(left - tabLeft)} mm proud, from y ${runs.map(([a, b]) => `${mm(a - top)} to ${mm(b - top)}`).join(' and ')} mm; first contacts at y ${perTab.map((cs) => mm(cs[0] - top)).join(' and ')} mm`);
