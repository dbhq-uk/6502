import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { page, visibleText, DIST } from './helpers.mjs';
import { PADS, WIRES, COLUMNS, STOPS, padLabel } from '../src/chip/layout.mjs';
import { frames, bits, ALU_MNEMONICS } from '../src/chip/trace.mjs';

const trace = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src', 'data', 'chip-trace.json'), 'utf8'));
const dist = (...p) => path.join(DIST, ...p);

test('the package has 40 pins, numbered 1 to 40 once each, and every bus line has a pad', () => {
  assert.deepEqual(PADS.map((p) => p.pin), Array.from({ length: 40 }, (_, i) => i + 1));
  for (let b = 0; b < 16; b++) assert.ok(PADS.some((p) => p.name === `A${b}`), `no pad for A${b}`);
  for (let b = 0; b < 8; b++) assert.ok(PADS.some((p) => p.name === `D${b}`), `no pad for D${b}`);
  for (const n of ['RW', 'SYNC', 'PHI0', 'IRQ', 'NMI', 'RES']) assert.ok(PADS.some((p) => p.name === n), `no pad for ${n}`);
  assert.equal(padLabel('RW'), 'R/W');
});

test('every bus wire ends at its own pad, and every address and data line has one', () => {
  for (const w of WIRES.filter((x) => x.bus !== 'control')) {
    const pad = PADS.find((p) => p.name === w.id);
    const [x, z] = w.points.at(-1);
    assert.ok(Math.hypot(x - pad.x, z - pad.z) <= 1.5, `${w.id} ends ${Math.hypot(x - pad.x, z - pad.z).toFixed(2)} from its pad`);
  }
  assert.equal(WIRES.filter((w) => w.bus === 'address').length, 16);
  assert.equal(WIRES.filter((w) => w.bus === 'data').length, 8);
});

test('the registers drawn are the ones the core has, plus the ALU', () => {
  assert.deepEqual(COLUMNS.filter((c) => c.reg).map((c) => c.reg).sort(), ['a', 'abh', 'abl', 'dl', 'pch', 'pcl', 's', 'x', 'y']);
  assert.equal(COLUMNS.filter((c) => !c.reg).length, 1);
});

test('the trace is one frame per bus cycle, and each frame is exactly what the core recorded', () => {
  const f = frames(trace);
  assert.equal(f.length, trace.cycles.length);
  f.forEach((x, i) => {
    assert.deepEqual([x.address, x.data, x.write ? 1 : 0], trace.cycles[i]);
  });
  assert.equal(f.filter((x) => x.fetch).length, trace.instructions.length, 'one opcode fetch per instruction');
});

test('registers change on an instruction\'s last cycle and show the core\'s own result there', () => {
  const f = frames(trace);
  trace.instructions.forEach((ins, k) => {
    const mine = f.filter((x) => x.instruction === k);
    assert.equal(mine.length, ins.cycles);
    const last = mine.at(-1);
    assert.deepEqual([last.regs.a, last.regs.x, last.regs.y, last.regs.s, last.regs.p], ins.after);
    for (const x of mine.slice(0, -1)) assert.deepEqual(x.changed, [], 'a register changed before the instruction finished');
  });
});

test('the address and data registers show what is on the pins that cycle', () => {
  for (const x of frames(trace)) {
    assert.equal((x.regs.abh << 8) | x.regs.abl, x.address);
    assert.equal(x.regs.dl, x.data);
  }
  assert.deepEqual(bits(0b101, 3), [1, 0, 1]);
});

test('the ALU lights only for instructions that compute, and the loop has some of each', () => {
  const f = frames(trace);
  assert.ok(f.some((x) => x.alu) && f.some((x) => !x.alu));
  for (const x of f) assert.equal(x.alu, ALU_MNEMONICS.has(x.mnemonic));
});

test('the trace says where it came from, and was recorded on the NMOS 6502', () => {
  assert.equal(trace.variant, 'Nmos6502');
  assert.match(trace.about, /tools\/Dbhq\.Cpu6502\.ChipTrace/);
  assert.match(trace.about, /Do not edit by hand/);
});

test('the page has a tour stop button and text for every stop, and the stops are the ones in the layout', () => {
  const html = page('/inside/').html;
  for (const s of STOPS) {
    assert.match(html, new RegExp(`data-stop="${s.id}"`), `no button for ${s.id}`);
    assert.match(html, new RegExp(`data-stop-text="${s.id}"`), `no text for ${s.id}`);
  }
  assert.equal(new Set(STOPS.map((s) => s.id)).size, STOPS.length);
});

test('the page says the view is a diagram, not the die, and its figures are the trace\'s own', () => {
  const text = visibleText(page('/inside/').html);
  assert.match(text, /Diagram, not the die\./);
  assert.match(text, /nothing is traced from a photograph of the chip/);
  assert.ok(text.includes(`${trace.cycles.length.toLocaleString('en-GB')} bus cycles`));
  assert.ok(text.includes(`${trace.instructions.length.toLocaleString('en-GB')} instructions`));
  assert.ok(text.includes(`${trace.laps} laps`));
});

test('the page does not claim the view is the real die or a measurement of it', () => {
  const text = visibleText(page('/inside/').html);
  assert.doesNotMatch(text, /real die|actual die|photograph of the die|traced from the die|the chip's real layout/i);
});

test('the page works without WebGL: the tour reads, and the controls and view are hidden until the script has started', () => {
  const css = fs.readFileSync(path.join(process.cwd(), 'src', 'styles', 'global.css'), 'utf8');
  assert.match(css, /\[data-chip\]:not\(\[data-state="running"\]\) \.die-stage/);
  assert.match(css, /\[data-chip\]\[data-state="no-webgl"\] \.die-fallback/);
});

test('the scene takes its colours from the site\'s tokens and holds no colour of its own', () => {
  const src = fs.readFileSync(path.join(process.cwd(), 'src', 'chip', 'main.js'), 'utf8');
  assert.deepEqual(src.match(/#[0-9a-fA-F]{3,8}\b|0x[0-9a-fA-F]{6}\b/g) ?? [], []);
});

test('the bundle is built, loaded by the page as a same-origin module, and carries the licences of what it bundles', () => {
  const html = page('/inside/').html;
  assert.match(html, /<script[^>]*type="module"[^>]*src="\/chip\.js"/);
  assert.ok(fs.existsSync(dist('chip.js')), 'dist/chip.js was not built');
  const js = fs.readFileSync(dist('chip.js'), 'utf8');
  assert.match(js, /Copyright 2010-\d{4} Three\.js Authors/);
  assert.match(js, /SPDX-License-Identifier: MIT/);
});
