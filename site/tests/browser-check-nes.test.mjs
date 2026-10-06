import test from 'node:test';
import assert from 'node:assert/strict';
import { settled } from '../scripts/browser-check-nes.mjs';

// The NES browser check's wait for a view's camera to come to rest (scripts/browser-check-nes.mjs), on a made-up
// page: the real one needs Chrome. Added in the final fix wave, 6 Oct 2026, after the review found the wait
// swallowed its timeout, so a camera still moving was compared as if it had stopped.

function fakePage({ rests }) {
  const calls = { evaluate: 0 };
  return {
    calls,
    waitForFunction: () => (rests ? Promise.resolve(true) : Promise.reject(new Error('Timeout 30000ms exceeded'))),
    waitForTimeout: () => Promise.resolve(),
    locator: () => ({ evaluate: () => { calls.evaluate++; return Promise.resolve({ polar: 1, azimuth: 0.5, distance: 30, moving: false }); } }),
  };
}

test('a camera that comes to rest gives its view, and no problem', async () => {
  const problems = [];
  const page = fakePage({ rests: true });
  assert.deepEqual(await settled(page, '#model-panel-inside', problems), { polar: 1, azimuth: 0.5, distance: 30, moving: false });
  assert.deepEqual(problems, []);
});

test('a camera that never comes to rest is a problem, named by its view, and gives nothing to compare', async () => {
  const problems = [];
  const page = fakePage({ rests: false });
  assert.equal(await settled(page, '#model-panel-outside', problems), null);
  assert.deepEqual(problems, ['nes: the camera never came to rest in #model-panel-outside']);
  assert.equal(page.calls.evaluate, 0, 'the view was read after the wait timed out');
});
