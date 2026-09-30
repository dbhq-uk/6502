import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { pages } from './helpers.mjs';

const read = (f) => fs.readFileSync(path.join(process.cwd(), 'public', f), 'utf8');
const analytics = read('analytics.js');
const consent = read('consent.js');
const headers = read('_headers');
const csp = /Content-Security-Policy: (.*)/.exec(headers)[1];
const directive = (name) => new RegExp(`(?:^|; )${name} ([^;]*)`).exec(csp)?.[1].split(' ') ?? [];

test('GA4 uses the estate\'s one measurement ID, and no data stream of its own', () => {
  assert.match(analytics, /const MEASUREMENT_ID = "G-3H3NFGSX85";/);
  assert.equal((analytics.match(/G-[A-Z0-9]{8,}/g) ?? []).filter((id) => id !== 'G-3H3NFGSX85').length, 0);
});

test('all four Consent Mode v2 signals are denied by default', () => {
  const block = /gtag\("consent", "default", \{([^}]*)\}/.exec(analytics)?.[1] ?? '';
  for (const signal of ['ad_storage', 'analytics_storage', 'ad_user_data', 'ad_personalization']) {
    assert.match(block, new RegExp(`${signal}: "denied"`), `${signal} is not denied by default`);
  }
});

test('nothing measures off the live host, and the tag loads only through the consent gate', () => {
  assert.match(analytics, /const PROD = location\.hostname === "6502\.dbhq\.uk";/);
  const enable = /window\.__dbhqEnableGA = function \(\) \{([\s\S]*?)\n\};/.exec(analytics)?.[1] ?? '';
  assert.match(enable, /if \(!PROD \|\| window\.__gaLoaded\) return;/);
  assert.match(enable, /googletagmanager\.com\/gtag\/js/);
  // The gtag script address appears once, inside the gate, and never at the top level.
  assert.equal((analytics.match(/googletagmanager\.com\/gtag\/js/g) ?? []).length, 1);
  assert.match(analytics, /localStorage\.getItem\("dbhq-consent"\) === "granted"/);
});

test('the prompt stores the estate\'s three states, and Decline is as easy as Accept', () => {
  assert.match(consent, /"dbhq-consent"/);
  assert.match(consent, /set\("granted"\)/);
  assert.match(consent, /set\("denied"\)/);
  const html = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'Consent.astro'), 'utf8');
  const decline = html.indexOf('data-consent-decline');
  const accept = html.indexOf('data-consent-accept');
  assert.ok(decline > 0 && accept > decline, 'Decline must come first');
  assert.match(html.slice(decline - 60, decline), /class="btn ghost"/);
});

test('every page loads analytics.js before consent.js, and none names the Google tag in its HTML', () => {
  for (const p of pages().filter((x) => x.url !== '/404.html')) {
    const a = p.html.indexOf('src="/analytics.js"');
    const c = p.html.indexOf('src="/consent.js"');
    assert.ok(a > 0 && c > a, `${p.url}: analytics.js must be loaded, before consent.js`);
    assert.ok(!/googletagmanager|gtag\(/.test(p.html), `${p.url} names the Google tag in its HTML`);
    assert.ok(p.html.includes('data-consent'), `${p.url} has no consent prompt`);
  }
});

test('the CSP has no unsafe-inline for scripts and allows exactly the Google Analytics origins', () => {
  assert.deepEqual(directive('script-src'), ["'self'", 'https://www.googletagmanager.com']);
  const origins = new Set(csp.match(/https:\/\/[^\s;]+/g));
  assert.deepEqual([...origins].sort(), ['https://*.analytics.google.com', 'https://*.google-analytics.com', 'https://www.google-analytics.com', 'https://www.googletagmanager.com']);
  assert.deepEqual(directive('font-src'), ["'self'"]);
  assert.match(csp, /frame-ancestors 'none'/);
  assert.match(headers, /Strict-Transport-Security: max-age=\d+; includeSubDomains/);
});
