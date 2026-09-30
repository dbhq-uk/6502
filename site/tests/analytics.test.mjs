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
  for (const p of pages()) {
    const a = p.html.indexOf('src="/analytics.js"');
    const c = p.html.indexOf('src="/consent.js"');
    assert.ok(a > 0 && c > a, `${p.url}: analytics.js must be loaded, before consent.js`);
    assert.ok(!/googletagmanager|gtag\(/.test(p.html), `${p.url} names the Google tag in its HTML`);
    assert.ok(p.html.includes('data-consent'), `${p.url} has no consent prompt`);
  }
});

const consentAstro = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'Consent.astro'), 'utf8');
const footerAstro = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'Footer.astro'), 'utf8');

test('the prompt does not start with Accept focused, so a bare Enter cannot grant consent', () => {
  assert.doesNotMatch(consentAstro, /data-consent-accept[^>]*autofocus/);
  assert.doesNotMatch(consentAstro, /autofocus[^>]*data-consent-accept/);
  assert.match(consentAstro, /<h2 id="consent-title" tabindex="-1" autofocus>/);
});

test('every page has a real Cookie choice button in the footer', () => {
  assert.match(footerAstro, /<button type="button" class="linkbtn" data-consent-reopen>Cookie choice<\/button>/);
  for (const p of pages()) {
    assert.match(p.html, /<footer[\s\S]*<button type="button" class="linkbtn" data-consent-reopen>Cookie choice<\/button>[\s\S]*<\/footer>/, `${p.url} has no Cookie choice button`);
  }
});

test('the Cookie choice button forgets the answer, revokes all four signals and reopens the prompt', () => {
  const wiring = /querySelector\("\[data-consent-reopen\]"\)[\s\S]*?\n  \}\n/.exec(consent)?.[0] ?? '';
  assert.ok(wiring, 'consent.js does not wire [data-consent-reopen]');
  assert.match(wiring, /addEventListener\("click"/);
  assert.match(wiring, /localStorage\.removeItem\("dbhq-consent"\)/);
  assert.match(wiring, /window\.__dbhqRevokeGA\(\)/);
  assert.match(wiring, /dlg\.showModal\(\)/);
  const revoke = /window\.__dbhqRevokeGA = function \(\) \{([\s\S]*?)\n\};/.exec(analytics)?.[1] ?? '';
  assert.match(revoke, /gtag\("consent", "update"/);
  for (const signal of ['ad_storage', 'analytics_storage', 'ad_user_data', 'ad_personalization']) {
    assert.match(revoke, new RegExp(`${signal}: "denied"`), `${signal} is not denied on withdrawal`);
  }
});

test('accepting again after a withdrawal, in the same page load, grants consent again', () => {
  const enable = /window\.__dbhqEnableGA = function \(\) \{([\s\S]*?)\n\};/.exec(analytics)?.[1] ?? '';
  const regrant = enable.indexOf('if (PROD && window.__gaLoaded) gtag("consent", "update", { analytics_storage: "granted" });');
  assert.ok(regrant >= 0, 'the loaded-tag path never sends the granted update');
  assert.ok(regrant < enable.indexOf('if (!PROD || window.__gaLoaded) return;'), 'the granted update must come before the early return');
});

test('the comments in the shipped scripts say what is true, and name nothing internal', () => {
  assert.doesNotMatch(consent, /next visit rather than/);
  assert.match(consent, /next page load/);
  // analytics.js is copied to the site as it is, so it must not name other projects, the property or another repository's path.
  assert.doesNotMatch(analytics, /\b(bbs|modem|terraken)\b/i);
  assert.doesNotMatch(analytics, /\b544327698\b/);
  assert.doesNotMatch(analytics, /\.md\b|\bdocs\/|\bdbhq\//);
});

test('the CSP has no unsafe-inline for scripts and allows exactly the Google Analytics origins', () => {
  assert.deepEqual(directive('script-src'), ["'self'", 'https://www.googletagmanager.com']);
  const origins = new Set(csp.match(/https:\/\/[^\s;]+/g));
  assert.deepEqual([...origins].sort(), ['https://*.analytics.google.com', 'https://*.google-analytics.com', 'https://www.google-analytics.com', 'https://www.googletagmanager.com']);
  assert.deepEqual(directive('font-src'), ["'self'"]);
  assert.match(csp, /frame-ancestors 'none'/);
  assert.match(headers, /Strict-Transport-Security: max-age=\d+; includeSubDomains/);
});

test('the headers comment names both relaxations: unsafe-inline styles and data: images', () => {
  const comment = headers.split('\n').filter((l) => l.startsWith('#')).join('\n');
  assert.match(comment, /style-src needs 'unsafe-inline'/);
  assert.match(comment, /img-src allows data:/);
  assert.doesNotMatch(comment, /the only other relaxation/);
  assert.match(csp, /img-src [^;]*\bdata:/);
});
