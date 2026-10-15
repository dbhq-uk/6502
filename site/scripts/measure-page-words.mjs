// The visible words and the height of each machine page, as a visitor sees it
// when it has loaded and nothing is opened: the words of <main>'s innerText at
// 1440 pixels wide (a closed <details> and a hidden element add none), and the
// page's scroll height. The machines' WebAssembly is not loaded. It serves dist/
// on 127.0.0.1 and opens each page in headless Chrome; it prints a table.
//
//   npm run build && node scripts/measure-page-words.mjs
//
// The Chrome is CHROME_PATH, or /usr/bin/google-chrome as scripts/browser-check.mjs
// has it. Used for the journal entry 2026-10-15-the-machine-pages-made-short.md.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const dist = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'dist');
const pages = ['kim-1', 'bbc-micro', 'nes', 'electron', 'atari-2600'];
const server = http.createServer((req, res) => {
  let file = path.join(dist, path.normalize(decodeURIComponent(new URL(req.url, 'http://127.0.0.1').pathname)));
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (!file.startsWith(dist) || !fs.existsSync(file)) return void res.writeHead(404).end();
  const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.webp': 'image/webp', '.avif': 'image/avif', '.woff2': 'font/woff2' };
  res.writeHead(200, { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
});
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH ?? '/usr/bin/google-chrome', headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  console.log('| Page | Words | Page height (px) |\n|---|---|---|');
  for (const id of pages) {
    await page.goto(`${origin}/machines/${id}/`, { waitUntil: 'load' });
    await page.waitForTimeout(500);
    const { words, height } = await page.evaluate(() => ({ words: document.querySelector('main').innerText.split(/\s+/).filter(Boolean).length, height: document.documentElement.scrollHeight }));
    console.log(`| ${id} | ${words} | ${height} |`);
  }
} finally {
  await browser.close();
  server.close();
}
