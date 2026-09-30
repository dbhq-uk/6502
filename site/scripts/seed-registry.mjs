// One-off: builds machines/registry.json from the tables in docs/the-6502-family.md.
//
//   node site/scripts/seed-registry.mjs
//
// Run once to create the registry, commit the result, and never again: after
// that the registry is the source of truth and a site test keeps the family
// document in step with it. Re-running this would overwrite hand-kept fields
// (status, acceptance, rights), so it refuses if the file already exists.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const source = fs.readFileSync(path.join(root, 'docs', 'the-6502-family.md'), 'utf8');
const target = path.join(root, 'machines', 'registry.json');
if (fs.existsSync(target)) {
  console.error(`${target} exists; the registry is now hand-kept.`);
  process.exit(1);
}

const CORE = { 'Core': 'nmos', 'Core, no decimal': '2a03', '65C02': '65c02', 'Other': 'none' };
const PRIORITY = { 'KIM-1': 1, 'BBC Micro': 2, 'NES, Famicom': 3 };

const clean = (text) => text.replace(/\*\*/g, '').replace(/\*/g, '').trim();
const slug = (text) => clean(text).replace(/\s*\([^)]*\)/g, '').replace(/['\u2019]/g, '').replace(/\+/g, ' plus ').toLowerCase().replace(/&/g, 'and').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const cells = (line) => line.trim().replace(/^\||\|$/g, '').split('|').map((c) => c.trim());

// Every table under a "##" and "###" heading, as { h2, h3, header, rows }.
function tables(markdown) {
  const out = [];
  let h2 = '';
  let h3 = '';
  const lines = markdown.split('\n');
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].startsWith('## ')) { h2 = lines[i].slice(3).trim(); h3 = ''; }
    else if (lines[i].startsWith('### ')) { h3 = lines[i].slice(4).trim(); }
    else if (lines[i].startsWith('|') && lines[i + 1]?.startsWith('|---')) {
      const header = cells(lines[i]);
      const rows = [];
      let j = i + 2;
      while (j < lines.length && lines[j].startsWith('|')) { rows.push(cells(lines[j])); j++; }
      out.push({ h2, h3, header, rows });
      i = j - 1;
    }
  }
  return out;
}

const machines = [];
const chips = [];
const add = (m) => machines.push({
  id: slug(m.name), name: clean(m.name), year: m.year ?? null, maker: m.maker ?? null,
  category: m.category, cpu: m.cpu, core: m.core,
  status: m.core === 'none' ? 'out-of-scope' : 'planned',
  ...(PRIORITY[clean(m.name)] ? { priority: PRIORITY[clean(m.name)] } : {}),
  acceptance: null, rights: null, notes: m.notes ?? null,
});

for (const t of tables(source)) {
  if (t.h2 === 'The chips') {
    for (const [name, year, difference, usedIn, core] of t.rows) {
      chips.push({ name: clean(name), year: year ? Number(year) : null, difference, usedIn, core: CORE[core] });
    }
  } else if (t.h2 === 'Computers') {
    const single = t.h3.startsWith('Single-board');
    for (const [name, year, cpu, core] of t.rows) {
      add({ name, year: Number(year), maker: single || t.h3 === 'Others' ? null : t.h3.replace('Atari 8-bit', 'Atari'), category: single ? 'single-board' : 'computer', cpu, core: CORE[core] });
    }
  } else if (t.h2 === 'Consoles and handhelds') {
    for (const [name, year, cpu, core] of t.rows) add({ name, year: Number(year), category: 'console', cpu, core: CORE[core] });
  } else if (t.h2 === 'Arcade') {
    for (const [name, year, notes] of t.rows) add({ name, year: Number(year), maker: 'Atari', category: 'arcade', cpu: '6502', core: 'nmos', notes });
  } else if (t.h2 === 'Built today') {
    for (const [name, cpu, notes] of t.rows) {
      const year = /\((\d{4})\)/.exec(name)?.[1];
      add({ name: name.replace(/\s*\(\d{4}\)/, ''), year: year ? Number(year) : null, category: 'modern', cpu, core: /45GS02/.test(cpu) ? 'none' : '65c02', notes });
    }
  }
}

const ids = new Set();
for (const m of machines) {
  if (ids.has(m.id)) throw new Error(`duplicate id ${m.id}`);
  ids.add(m.id);
}
fs.writeFileSync(target, JSON.stringify({ machines, chips }, null, 2) + '\n');
console.log(`${machines.length} machines and ${chips.length} chips written`);
