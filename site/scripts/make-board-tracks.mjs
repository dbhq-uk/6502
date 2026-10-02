// Traces the KIM-1's copper tracks off its photograph, for the top face of the
// 3D model. The output is committed (src/assets/tracks/kim-1.webp), so neither
// the build nor the tests run this; run it again only to remake the map.
//
//   node scripts/make-board-tracks.mjs <photograph> [--debug <folder>]
//
// <photograph> is either the full-size source (3792 by 4675, the one the
// committed map was made from; where to fetch it and its SHA-256 are in
// src/assets/photos/README.md, and a full-size file with any other hash is
// refused) or the committed copy, src/assets/photos/kim-1.webp, which works but
// at 0.42 of the resolution, so thin tracks that run close together merge.
// --debug writes the stages as PNGs: the board cut out of the photograph,
// the strong and weak candidates, the pours, the pads, what was cleared, and
// the result.
//
// How, in order (the thresholds are THRESHOLDS below, and the journal for
// 2 October 2026 says how they were measured and tuned):
//
// 1. Cut the board's body out of the photograph with the box in
//    src/models/kim-1-layout.mjs (PHOTO.body), at the photograph's own
//    resolution. The board is square to the camera to within a pixel or two,
//    so a box, not a perspective warp (the journal has the edge fits).
// 2. Convert every pixel to OKLab, a perceptual colour space: L is lightness,
//    and a and b are the green-red and blue-yellow axes. The tracks are
//    tinned copper under the green mask, which the photograph shows as a
//    lighter, yellower green than the mask round them.
// 3. Estimate the mask's own colour round each pixel: an opening (a minimum
//    then a maximum over a square a little wider than a track), which removes
//    the tracks, then a box blur. A track is lighter and yellower than that.
// 4. Strong copper: well above the local mask in L and b, saturated, with a
//    hue in the tracks' band (yellow-green, 97 to 135 degrees). The band is
//    what leaves most parts out: the cream capacitors sit at 85 to 89
//    degrees, the resistors and other brown parts at 53 to 68, the ceramic
//    chips' gold at about 74 and the orange capacitor at 35, and the black
//    chips and the white ceramic have too little colour to have a hue. The
//    saturation leaves out the silkscreen. Copper pours, wider than the
//    opening, are taken on their absolute colour instead.
// 5. Weak copper: the same tests, looser. A weak region is kept only if it
//    touches a strong one (hysteresis), which keeps the near-white highlight
//    along a track without letting in loose specks.
// 6. Close one-pixel holes, then add the tinned pads: silver, round blobs of
//    pad size that touch a track.
// 7. Clear what is not copper on the board: the bodies of the parts the model
//    draws itself (the parts hide the board there anyway), and the photographed
//    parts and labels the model does not draw whose colour still passed
//    (HIDDEN below). Then drop any region smaller than a speck, fill the small
//    holes, and clear a strip round the edge.
// 8. Shrink to the map's size with a smooth kernel, so the edges are soft, and
//    write it as lossless WebP.
//
// The image helpers are exported so that a probe can reuse them; nothing in
// the site imports this file except tests/model.test.mjs, for HIDDEN.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';
import { BOARD, PHOTO, TRACKS, CHIPS, RAM, LOGIC, CRYSTAL, DISPLAY, KEYPAD } from '../src/models/kim-1-layout.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(here, '..', 'src', 'assets', 'tracks', 'kim-1.webp');

export const THRESHOLDS = {
  /** The opening's half width, and the blur after it, in mm: a track is up to about 0.8 mm wide. */
  openMm: 0.8,
  blurMm: 1.6,
  /** Hue band of the tracks, in degrees of OKLab hue, and the least chroma that has a hue. */
  hue: [97, 135],
  chroma: 0.03,
  /**
   * Strong copper: this far above the local mask in b and in L, and this
   * saturated (chroma over lightness). The saturation is what tells a track
   * from the white silkscreen: both are lighter than the mask, but the
   * silkscreen's colour is mostly glare, 0.05 to 0.09, where a track's flanks
   * are 0.12 to 0.20.
   */
  strong: { b: 0.02, L: 0.07, saturation: 0.13 },
  /** Weak copper, kept only where it touches strong. Its hue band is a little wider. */
  weak: { b: 0.012, L: 0.05, saturation: 0.09, hue: [92, 140] },
  /** A copper pour: yellow and light outright, over an area wider than this many mm. */
  pour: { b: 0.062, L: 0.42, wideMm: 1.2 },
  /** Holes up to this wide inside a track are closed, in mm. */
  closeMm: 0.07,
  /** A tinned pad: silver (little chroma), light, and lighter than the mask by this much. */
  pad: { chroma: 0.05, L: 0.42, aboveMask: 0.12, areaMm2: [0.5, 6], sideMm: [0.6, 3.2], fill: 0.45, touchMm: 0.4 },
  /** Regions smaller than this are specks, in mm squared. */
  speckMm2: 0.35,
  /** Holes smaller than this inside copper are filled, in mm squared: a pad's drill hole, a reflection. */
  holeMm2: 1.0,
  /** A strip round the board's edge, in mm, is cleared: the edge's bevel catches the light. */
  edgeMm: 0.8,
};

/**
 * Photographed parts that the model does not draw and whose colour still
 * passes as copper in places: their outlines, in mm on the board as
 * kim-1-layout.mjs measures it, [left, top, right, bottom], read off a grid
 * laid over the photograph. The tracks under them cannot be seen, so they are
 * not drawn: the map is mask there.
 */
export const HIDDEN = [
  [21, 6, 35, 12], // the "Rev. B" silkscreen, top left
  [155, 44, 172, 50.5], // the "EC-715" silkscreen under the name
  [176, 0, 199.7, 92], // the right-hand strip: the MOS logo's edge, the owner's label and the glare along the board's edge, where there are no tracks
  [121, 134.5, 192, 138.7], // the U18 to U23 silkscreen along the top of the display
  [60, 207.5, 82, 217], // a cream capacitor whose highlights pass as copper
  [110, 186, 121, 212], // another, beside the keypad
  [43, 215, 50, 219], // "U27"
  [65, 218.5, 71, 222], // "U28"
  [46.5, 168.5, 52.5, 173], // "U14"
  [91, 165.5, 99, 170], // "U24"
];

/** The bodies of the parts the model draws itself, as [left, top, right, bottom] in mm. */
export function modelled() {
  const DIP = { 40: [52, 13.7], 16: [19.3, 6.4], 14: [19, 6.4], 8: [9.6, 6.4] };
  const box = (x, y, w, d) => [x - w / 2, y - d / 2, x + w / 2, y + d / 2];
  return [
    ...CHIPS.map((c) => box(c.x, c.y, ...DIP[c.pins])),
    ...RAM.map((c) => box(c.x, c.y, ...DIP[c.pins])),
    ...LOGIC.map((c) => box(c.x, c.y, DIP[c.pins][1], DIP[c.pins][0])),
    box(CRYSTAL.x, CRYSTAL.y, CRYSTAL.width, CRYSTAL.depth),
    box(DISPLAY.x, DISPLAY.y, DISPLAY.width, DISPLAY.depth),
    box(KEYPAD.x, KEYPAD.y, KEYPAD.width, KEYPAD.depth),
  ];
}

// ---- image helpers on flat arrays, W by H ----

/** A running minimum or maximum over 2r+1 pixels, along rows then columns (a square). */
export function extremum(src, W, H, r, max) {
  const better = max ? (a, b) => a >= b : (a, b) => a <= b;
  const pass = (from, to, n, count, at) => {
    const q = new Int32Array(n);
    for (let line = 0; line < count; line++) {
      let head = 0, tail = 0;
      for (let i = 0; i < n + r; i++) {
        if (i < n) {
          const v = from[at(line, i)];
          while (tail > head && better(v, from[at(line, q[tail - 1])])) tail--;
          q[tail++] = i;
        }
        const o = i - r;
        if (o < 0) continue;
        while (q[head] < o - r) head++;
        to[at(line, o)] = from[at(line, q[head])];
      }
    }
  };
  const tmp = new Float32Array(W * H), out = new Float32Array(W * H);
  pass(src, tmp, W, H, (y, x) => y * W + x);
  pass(tmp, out, H, W, (x, y) => y * W + x);
  return out;
}

/** A box blur over 2r+1 pixels, rows then columns. */
export function blur(src, W, H, r) {
  const pass = (from, to, n, count, at) => {
    for (let line = 0; line < count; line++) {
      let sum = 0, k = 0;
      for (let i = 0; i < Math.min(r, n); i++) { sum += from[at(line, i)]; k++; }
      for (let i = 0; i < n; i++) {
        if (i + r < n) { sum += from[at(line, i + r)]; k++; }
        if (i - r - 1 >= 0) { sum -= from[at(line, i - r - 1)]; k--; }
        to[at(line, i)] = sum / k;
      }
    }
  };
  const tmp = new Float32Array(W * H), out = new Float32Array(W * H);
  pass(src, tmp, W, H, (y, x) => y * W + x);
  pass(tmp, out, H, W, (x, y) => y * W + x);
  return out;
}

const dilate = (mask, W, H, r) => extremum(Float32Array.from(mask), W, H, r, true).map((v) => (v > 0 ? 1 : 0));
const erode = (mask, W, H, r) => extremum(Float32Array.from(mask), W, H, r, false).map((v) => (v > 0 ? 1 : 0));

/** Labels the 4-connected regions of a 0/1 mask. Returns the labels and each region's area and box. */
export function regions(mask, W, H) {
  const label = new Int32Array(W * H);
  const stats = [null];
  const stack = new Int32Array(W * H);
  for (let s = 0; s < W * H; s++) {
    if (!mask[s] || label[s]) continue;
    const id = stats.length;
    const r = { area: 0, x0: W, y0: H, x1: 0, y1: 0 };
    let top = 0;
    stack[top++] = s;
    label[s] = id;
    while (top) {
      const p = stack[--top];
      const x = p % W, y = (p - x) / W;
      r.area++;
      if (x < r.x0) r.x0 = x;
      if (x > r.x1) r.x1 = x;
      if (y < r.y0) r.y0 = y;
      if (y > r.y1) r.y1 = y;
      for (const q of [x > 0 ? p - 1 : -1, x < W - 1 ? p + 1 : -1, y > 0 ? p - W : -1, y < H - 1 ? p + W : -1]) {
        if (q >= 0 && mask[q] && !label[q]) { label[q] = id; stack[top++] = q; }
      }
    }
    stats.push(r);
  }
  return { label, stats };
}

const toLinear = (c) => { c /= 255; return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4; };

async function main() {
  const args = process.argv.slice(2);
  const debugAt = args.indexOf('--debug');
  const debug = debugAt >= 0 ? args.splice(debugAt, 2)[1] : null;
  const file = args[0];
  if (!file) {
    console.error('usage: node scripts/make-board-tracks.mjs <the photograph> [--debug <folder>]');
    process.exit(2);
  }
  const bytes = fs.readFileSync(file);
  const sha = crypto.createHash('sha256').update(bytes).digest('hex');
  const meta = await sharp(bytes).metadata();
  const scale = meta.width / PHOTO.width;
  if (Math.abs(meta.height / PHOTO.height - scale) > 0.002) throw new Error(`${file} is ${meta.width} by ${meta.height}, not the photograph's shape`);
  if (meta.width === PHOTO.width && sha !== PHOTO.sha256) throw new Error(`${file} is full size but its SHA-256 is ${sha}, not the photograph's`);
  const [bx0, by0, bx1, by1] = PHOTO.body.map((v) => Math.round(v * scale));
  const W = bx1 - bx0, H = by1 - by0, N = W * H;
  const pxmm = W / BOARD.width;
  console.log(`photograph ${meta.width} x ${meta.height}, SHA-256 ${sha}`);
  console.log(`board body x ${bx0} to ${bx1}, y ${by0} to ${by1}: ${W} x ${H} pixels, ${pxmm.toFixed(2)} pixels to the millimetre`);
  const mm = (v) => Math.max(1, Math.round(v * pxmm));
  const T = THRESHOLDS;

  const { data } = await sharp(bytes).extract({ left: bx0, top: by0, width: W, height: H }).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const L = new Float32Array(N), A = new Float32Array(N), B = new Float32Array(N);
  for (let i = 0; i < N; i++) {
    const r = toLinear(data[i * 3]), g = toLinear(data[i * 3 + 1]), b = toLinear(data[i * 3 + 2]);
    const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
    const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
    const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
    L[i] = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s;
    A[i] = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
    B[i] = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
  }

  // The mask's own colour round each pixel.
  const open = (ch) => blur(extremum(extremum(ch, W, H, mm(T.openMm), false), W, H, mm(T.openMm), true), W, H, mm(T.blurMm));
  const bgL = open(L), bgB = open(B);

  // A pour is copper too wide for the opening to see, so it is found on its
  // own colour, and only where that colour fills a patch wider than a track.
  let pour = new Uint8Array(N);
  for (let i = 0; i < N; i++) pour[i] = B[i] > T.pour.b && L[i] > T.pour.L ? 1 : 0;
  const wide = mm(T.pour.wideMm / 2);
  pour = dilate(erode(pour, W, H, wide), W, H, wide);

  const strong = new Uint8Array(N), weak = new Uint8Array(N), silver = new Uint8Array(N);
  for (let i = 0; i < N; i++) {
    const C = Math.hypot(A[i], B[i]);
    const h = (Math.atan2(B[i], A[i]) * 180) / Math.PI;
    const hue = (h + 360) % 360;
    const dB = B[i] - bgB[i], dL = L[i] - bgL[i];
    const sat = C / Math.max(L[i], 0.01);
    const coloured = C > T.chroma;
    const S = T.strong, Wk = T.weak;
    if (coloured && hue > T.hue[0] && hue < T.hue[1] && ((dB > S.b && dL > S.L && sat > S.saturation) || pour[i])) strong[i] = 1;
    if (coloured && hue > Wk.hue[0] && hue < Wk.hue[1] && ((dB > Wk.b && dL > Wk.L && sat > Wk.saturation) || pour[i])) weak[i] = 1;
    if (C < T.pad.chroma && L[i] > T.pad.L && dL > T.pad.aboveMask) silver[i] = 1;
  }
  for (let i = 0; i < N; i++) if (strong[i]) weak[i] = 1;

  // Hysteresis: a weak region stays only if it holds a strong pixel.
  const weakRegions = regions(weak, W, H);
  const anchored = new Uint8Array(weakRegions.stats.length);
  for (let i = 0; i < N; i++) if (strong[i]) anchored[weakRegions.label[i]] = 1;
  let copper = new Uint8Array(N);
  for (let i = 0; i < N; i++) copper[i] = anchored[weakRegions.label[i]];

  const kept = count(copper);

  // Close the specular line along a track, where the highlight is too white to pass.
  const c = mm(T.closeMm);
  copper = erode(dilate(copper, W, H, c), W, H, c);

  // The pads: silver, round, pad-sized, touching copper.
  const silverRegions = regions(dilate(silver, W, H, 1), W, H);
  const near = dilate(copper, W, H, mm(T.pad.touchMm));
  const P = T.pad;
  const isPad = silverRegions.stats.map((r) => {
    if (!r) return false;
    const w = (r.x1 - r.x0 + 1) / pxmm, d = (r.y1 - r.y0 + 1) / pxmm, area = r.area / pxmm ** 2;
    return area >= P.areaMm2[0] && area <= P.areaMm2[1] && w >= P.sideMm[0] && w <= P.sideMm[1] && d >= P.sideMm[0] && d <= P.sideMm[1] && w / d < 1.8 && d / w < 1.8 && r.area / ((r.x1 - r.x0 + 1) * (r.y1 - r.y0 + 1)) >= P.fill;
  });
  const touching = new Uint8Array(isPad.length);
  for (let i = 0; i < N; i++) if (near[i] && isPad[silverRegions.label[i]]) touching[silverRegions.label[i]] = 1;
  const pads = new Uint8Array(N);
  let padCount = 0;
  for (let k = 1; k < touching.length; k++) padCount += touching[k];
  for (let i = 0; i < N; i++) if (touching[silverRegions.label[i]]) pads[i] = 1;
  const padsFilled = erode(dilate(pads, W, H, 2), W, H, 2);
  for (let i = 0; i < N; i++) if (padsFilled[i]) copper[i] = 1;

  // Clear the parts: the model's own, and the photographed ones it does not draw.
  const cleared = new Uint8Array(N);
  const clear = ([x0, y0, x1, y1]) => {
    for (let y = Math.max(0, Math.floor(y0 * pxmm)); y < Math.min(H, Math.ceil(y1 * pxmm)); y++) {
      for (let x = Math.max(0, Math.floor(x0 * pxmm)); x < Math.min(W, Math.ceil(x1 * pxmm)); x++) cleared[y * W + x] = 1;
    }
  };
  modelled().forEach(clear);
  HIDDEN.forEach(clear);
  const edge = mm(T.edgeMm);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (x < edge || y < edge || x >= W - edge || y >= H - edge) cleared[y * W + x] = 1;
  for (let i = 0; i < N; i++) if (cleared[i]) copper[i] = 0;

  // Drop the specks.
  const final = regions(copper, W, H);
  const speck = T.speckMm2 * pxmm ** 2;
  let specks = 0;
  const keep = final.stats.map((r) => r && r.area >= speck);
  for (let k = 1; k < keep.length; k++) if (!keep[k]) specks++;
  const out = Buffer.alloc(N);
  for (let i = 0; i < N; i++) if (copper[i] && keep[final.label[i]]) out[i] = 1;
  // Fill the small holes: a region of mask that does not reach the edge and is smaller than a hole.
  const gaps = regions(out.map((v) => 1 - v), W, H);
  const hole = T.holeMm2 * pxmm ** 2;
  const fill = gaps.stats.map((r) => r && r.area < hole && r.x0 > 0 && r.y0 > 0 && r.x1 < W - 1 && r.y1 < H - 1);
  let holes = 0;
  for (let k = 1; k < fill.length; k++) holes += fill[k] ? 1 : 0;
  let on = 0;
  for (let i = 0; i < N; i++) {
    if (!out[i] && fill[gaps.label[i]] && !cleared[i]) out[i] = 1;
    if (out[i]) { out[i] = 255; on++; }
  }
  console.log(`pixels: strong ${count(strong)}, weak ${count(weak)}, kept by hysteresis ${kept}; pads ${padCount}; cleared ${count(cleared)} pixels; specks dropped ${specks}; holes filled ${holes}`);
  console.log(`copper covers ${((on / N) * 100).toFixed(1)}% of the board's top face`);

  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  await sharp(out, { raw: { width: W, height: H, channels: 1 } })
    .resize(TRACKS.width, TRACKS.height, { fit: 'fill', kernel: 'mitchell' })
    .webp({ lossless: true, effort: 6 })
    .toFile(OUT);
  const written = await sharp(OUT).metadata();
  console.log(`wrote ${path.relative(process.cwd(), OUT)}: ${written.width} x ${written.height}, ${fs.statSync(OUT).size} bytes`);

  if (debug) {
    fs.mkdirSync(debug, { recursive: true });
    const grey = (m, name) => sharp(Buffer.from(m.map((v) => (v ? 255 : 0))), { raw: { width: W, height: H, channels: 1 } }).png().toFile(path.join(debug, `${name}.png`));
    await sharp(data, { raw: { width: W, height: H, channels: 3 } }).png().toFile(path.join(debug, 'board.png'));
    await grey(strong, 'strong');
    await grey(weak, 'weak');
    await grey(pads, 'pads');
    await grey(pour, 'pour');
    await grey(cleared, 'cleared');
    await sharp(out, { raw: { width: W, height: H, channels: 1 } }).png().toFile(path.join(debug, 'tracks.png'));
    console.log(`debug images in ${debug}`);
  }
}

const count = (m) => m.reduce((a, b) => a + b, 0);

if (process.argv[1] === fileURLToPath(import.meta.url)) await main();
