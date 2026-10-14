// THROWAWAY, never merged: runs only the BBC Micro models' part of the browser
// check (checkBbcModels in scripts/browser-check-bbc.mjs) N times on one build,
// optionally with Chrome's CPU throttled, and counts what each run reported,
// for the review of task 9b of the BBC Micro models plan (14 October 2026).
//
//   node scripts/stress-bbc-models.mjs --runs 25 --throttle 4
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';
import { checkBbcModels } from './browser-check-bbc.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const site = path.resolve(here, '..');
const dist = path.join(site, 'dist');
const option = (name, fallback) => { const i = process.argv.indexOf(name); return i >= 0 ? Number(process.argv[i + 1]) : fallback; };
const runs = option('--runs', 25);
const throttle = option('--throttle', 1);

function edgeHeaders() {
  const lines = fs.readFileSync(path.join(site, 'public', '_headers'), 'utf8').split('\n');
  const headers = {};
  let inAll = false;
  for (const line of lines) {
    if (/^\S/.test(line) && !line.startsWith('#')) inAll = line.trim() === '/*';
    else if (inAll && /^\s+\S/.test(line)) { const [name, ...value] = line.trim().split(':'); headers[name] = value.join(':').trim(); }
  }
  return headers;
}
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.bin': 'application/octet-stream', '.svg': 'image/svg+xml', '.webp': 'image/webp', '.avif': 'image/avif', '.woff2': 'font/woff2', '.woff': 'font/woff', '.xml': 'application/xml', '.txt': 'text/plain' };
const headers = edgeHeaders();
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  let file = path.join(dist, path.normalize(decodeURIComponent(url.pathname)));
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (!file.startsWith(dist) || !fs.existsSync(file)) { res.writeHead(404).end(); return; }
  res.writeHead(200, { ...headers, 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
  fs.createReadStream(file).pipe(res);
});
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH ?? '/usr/bin/google-chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
console.log(`browser ${browser.version()}; ${runs} runs; CPU throttled ${throttle} times`);
const throttled = async (p) => { if (throttle > 1) await (await p.context().newCDPSession(p)).send('Emulation.setCPUThrottlingRate', { rate: throttle }); return p; };
// The browser as checkBbcModels uses it, every page it opens throttled.
const wrapped = {
  newPage: async () => throttled(await browser.newPage()),
  newContext: async (o) => { const c = await browser.newContext(o); const np = c.newPage.bind(c); c.newPage = async () => throttled(await np()); return c; },
};
let sink = [];
const watch = async (p, label = '') => {
  p.on('console', (m) => { if (m.type() === 'error') sink.push(`${label}console error: ${m.text()}`); });
  p.on('pageerror', (e) => sink.push(`${label}page error: ${e.message}`));
  p.on('requestfailed', (r) => sink.push(`${label}request failed: ${r.url()} ${r.failure()?.errorText ?? ''}`));
  p.on('response', (r) => { if (r.status() >= 400) sink.push(`${label}HTTP ${r.status()}: ${r.url()}`); });
  await p.exposeFunction('reportCspViolation', (v) => sink.push(`${label}CSP violation: ${v}`));
  await p.addInitScript(() => { document.addEventListener('securitypolicyviolation', (e) => window.reportCspViolation(`${e.violatedDirective} blocked ${e.blockedURI || '(inline)'}`)); });
};
const tally = { runs: 0, clean: 0, rest: 0, framed: 0, other: 0 };
for (let i = 1; i <= runs; i++) {
  const problems = [];
  sink = problems;
  const t = Date.now();
  try {
    await checkBbcModels({ browser: wrapped, watch, problems, origin });
  } catch (error) {
    problems.push(`thrown: ${error.message}`);
  }
  tally.runs++;
  const rest = problems.filter((p) => /never came to rest/.test(p)).length;
  const framed = problems.filter((p) => /never framed/.test(p)).length;
  const other = problems.length - rest - framed;
  if (problems.length === 0) tally.clean++;
  if (rest) tally.rest++;
  if (framed) tally.framed++;
  if (other) tally.other++;
  console.log(`STRESS run ${i} of ${runs}, throttle ${throttle}: ${Math.round((Date.now() - t) / 1000)} s; ${problems.length === 0 ? 'clean' : problems.join(' | ')}`);
}
console.log(`STRESS TALLY throttle ${throttle}: ${JSON.stringify(tally)} (runs with a camera that never rested, runs where the DFS was never framed, runs with any other problem)`);
await browser.close();
server.close();
