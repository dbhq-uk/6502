// Serves a published copy of this app on 127.0.0.1 and runs it in a real
// headless Chrome, once per run, each in a fresh browser so every run starts
// from a cold runtime. Prints the lines the page prints, and the browser version.
//
//   node run-in-browser.mjs <published-folder> [runs] [cycles] [warmup-cycles]
//
// Local measurement only: the server binds to loopback and serves one folder.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright-core';

const [dir, runsArg = '5', cyclesArg = '100000000', warmupArg = '5000000'] = process.argv.slice(2);
if (!dir) {
  console.error('usage: node run-in-browser.mjs <published-folder> [runs] [cycles] [warmup-cycles]');
  process.exit(2);
}
const root = path.resolve(dir, fs.existsSync(path.join(dir, 'wwwroot')) ? 'wwwroot' : '.');
const chrome = process.env.CHROME_PATH ?? '/usr/bin/google-chrome';

const types = {
  '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript',
  '.json': 'application/json', '.wasm': 'application/wasm', '.dll': 'application/octet-stream',
  '.dat': 'application/octet-stream', '.pdb': 'application/octet-stream', '.blat': 'application/octet-stream',
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  const file = path.join(root, path.normalize(decodeURIComponent(url.pathname)).replace(/^(\.\.[/\\])+/, ''));
  const target = fs.existsSync(file) && fs.statSync(file).isDirectory() ? path.join(file, 'index.html') : file;
  if (!target.startsWith(root) || !fs.existsSync(target)) {
    res.writeHead(404).end();
    return;
  }
  res.writeHead(200, { 'Content-Type': types[path.extname(target)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
  fs.createReadStream(target).pipe(res);
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const base = `http://127.0.0.1:${server.address().port}/`;

try {
  for (let run = 1; run <= Number(runsArg); run++) {
    const browser = await chromium.launch({ executablePath: chrome, headless: true });
    if (run === 1) console.log('browser ' + browser.version());
    const page = await browser.newPage();
    page.on('pageerror', error => console.error('page error: ' + error.message));
    await page.goto(`${base}?cycles=${cyclesArg}&warmup=${warmupArg}`);
    await page.waitForFunction(() => document.body.dataset.done === 'true', null, { timeout: 30 * 60 * 1000 });
    const text = await page.locator('#log').innerText();
    for (const line of text.split('\n')) console.log(`run ${run} ${line}`);
    await browser.close();
  }
} finally {
  server.close();
}
