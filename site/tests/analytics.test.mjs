import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { pages } from './helpers.mjs';

// Analytics: on by default, a notice, and a simple opt-out.
//
// The site launched on 1 Oct 2026 with an opt-in modal, then moved to the
// estate's pattern the same day: GA4 runs by default under the PECR
// statistical-purposes exception. The exception holds only while four things
// stay true, and each is held separately below:
//   1. the measurement is statistics and nothing else (ad signals denied,
//      Google Signals and ad personalisation off);
//   2. a visitor who opts out loads nothing and sends nothing more;
//   3. the visitor is told, on a notice that does not block the page;
//   4. objecting is as easy as carrying on, and one click from every page.
//
// Most tests run analytics.js and consent.js themselves, in a sandbox with a
// fake browser, rather than reading their text. A regex over the source proves
// a phrase is there; it does not prove the phrase is reached.

const read = (f) => fs.readFileSync(path.join(process.cwd(), 'public', f), 'utf8');
const analyticsSrc = read('analytics.js');
const consentSrc = read('consent.js');
const headers = read('_headers');
const csp = /Content-Security-Policy: (.*)/.exec(headers)[1];
const directives = new Map(csp.split(';').map((d) => d.trim()).filter(Boolean).map((d) => { const [n, ...v] = d.split(/\s+/); return [n, v]; }));
const component = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'AnalyticsNotice.astro'), 'utf8');
const footerAstro = fs.readFileSync(path.join(process.cwd(), 'src', 'components', 'Footer.astro'), 'utf8');
const all = pages();

const MEASUREMENT_ID = 'G-3H3NFGSX85';
const HOST = '6502.dbhq.uk';
const MODERN_CHROME = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36';
const TAG = `https://www.googletagmanager.com/gtag/js?id=${MEASUREMENT_ID}`;

/** A fake element: enough of the DOM for consent.js. */
function element(attrs = {}) {
  const listeners = {};
  const el = {
    hidden: false,
    textContent: '',
    focused: 0,
    ...attrs,
    addEventListener: (type, fn) => (listeners[type] ??= []).push(fn),
    fire: (type, event = {}) => (listeners[type] ?? []).forEach((fn) => fn(event)),
    focus() { el.focused += 1; },
  };
  return el;
}

/**
 * A browser just big enough for both scripts: a cookie jar that honours expiry,
 * localStorage, navigator, location, a <head> that records what is appended and,
 * when asked, the notice and a footer button for consent.js to find.
 */
function browser({
  hostname = HOST,
  ua = MODERN_CHROME,
  webdriver = false,
  cookies = {},
  storage = {},
  // likelyBot's staleness line moves a month at a time, so the clock is pinned
  // or this suite would start calling Chrome 140 a bot in 2028.
  now = Date.UTC(2026, 9, 1),
  withNotice = false,
  storageThrows = false,
} = {}) {
  const jar = new Map(Object.entries(cookies));
  const writes = [];
  const appended = [];
  const store = new Map(Object.entries(storage));
  const notice = withNotice
    ? { box: element({ hidden: true }), status: element({ hidden: true }), on: element({ textContent: 'OK' }), off: element({ textContent: 'Opt out' }), settings: element({ hidden: true }) }
    : null;
  const document = {
    get cookie() { return [...jar].map(([k, v]) => `${k}=${v}`).join('; '); },
    set cookie(raw) {
      writes.push(raw);
      const [pair, ...attrs] = raw.split(';').map((s) => s.trim());
      const eq = pair.indexOf('=');
      const expired = attrs.some((a) => /^expires=.*1970/i.test(a) || /^max-age=0$/i.test(a));
      if (expired) jar.delete(pair.slice(0, eq));
      else jar.set(pair.slice(0, eq), pair.slice(eq + 1));
    },
    createElement: (tag) => ({ tag }),
    head: { appendChild: (el) => appended.push(el) },
    querySelector: (sel) => (notice && sel === '[data-analytics-notice]' ? Object.assign(notice.box, { querySelector: (s) => ({ '[data-analytics-status]': notice.status, '[data-analytics-on]': notice.on, '[data-analytics-off]': notice.off })[s] }) : null),
    querySelectorAll: (sel) => (notice && sel === '[data-analytics-settings]' ? [notice.settings] : []),
  };
  const window = {
    document,
    location: { hostname },
    navigator: { userAgent: ua, webdriver },
    localStorage: storageThrows
      ? { getItem() { throw new Error('blocked'); }, setItem() { throw new Error('blocked'); }, removeItem() { throw new Error('blocked'); } }
      : { getItem: (k) => (store.has(k) ? store.get(k) : null), setItem: (k, v) => store.set(k, String(v)), removeItem: (k) => store.delete(k) },
    Date: class extends Date { static now() { return now; } },
  };
  window.window = window;
  vm.runInNewContext(analyticsSrc, window);
  if (withNotice) vm.runInNewContext(consentSrc, window);
  // gtag pushes Arguments objects; turn each into a plain array to compare.
  // Array.from, so the arrays belong to this realm and deepEqual can compare them.
  const calls = () => Array.from(window.dataLayer ?? [], (a) => Array.from(a));
  return { window, jar, writes, appended, store, calls, notice, api: window.dbhqAnalytics };
}

const tagLoaded = (b) => b.appended.some((el) => el.tag === 'script' && el.src === TAG);

test('GA4 uses the estate\'s one measurement ID, in the scripts and on every page, and no data stream of its own', () => {
  assert.ok(all.length > 0);
  const ids = [analyticsSrc, consentSrc, ...all.map((p) => p.html)].flatMap((t) => t.match(/G-[A-Z0-9]{8,}/g) ?? []);
  assert.deepEqual([...new Set(ids)], [MEASUREMENT_ID]);
  assert.match(analyticsSrc, /const MEASUREMENT_ID = "G-3H3NFGSX85";/);
});

test('a new visitor on the live host is measured, configured for analytics only', () => {
  const b = browser();
  assert.ok(tagLoaded(b), 'the tag did not load for a new visitor');
  assert.equal(b.appended.length, 1, 'more than one script was injected');
  const calls = b.calls();
  const def = calls.find((c) => c[0] === 'consent' && c[1] === 'default');
  assert.ok(def, 'no consent default was set');
  // Each signal on its own: granting an ad signal takes the site outside the
  // exception, and it must fail by name.
  assert.equal(def[2].analytics_storage, 'granted');
  for (const signal of ['ad_storage', 'ad_user_data', 'ad_personalization']) assert.equal(def[2][signal], 'denied', `${signal} is not denied`);
  const config = calls.find((c) => c[0] === 'config');
  assert.ok(config, 'no config call');
  assert.equal(config[1], MEASUREMENT_ID);
  assert.equal(config[2]?.allow_google_signals, false, 'Google Signals is not off in the config');
  assert.equal(config[2]?.allow_ad_personalization_signals, false, 'ad personalisation is not off in the config');
  assert.ok(calls.indexOf(def) < calls.indexOf(config), 'the consent default must come before the config');
  assert.equal(b.writes.length, 0, 'a cookie was written before the visitor answered');
  assert.equal(b.api.choice(), null);
});

test('nothing is measured off the live host', () => {
  assert.ok(tagLoaded(browser({ hostname: HOST })), 'the live host is not measured, so this test proves nothing');
  // 6502.pages.dev serves the same bytes and is not the site. The lookalikes
  // are there because an unanchored host test would let them through.
  for (const hostname of ['6502.pages.dev', 'localhost', '100.115.72.85', `www.${HOST}`, `${HOST}.example.com`, 'dbhq.uk']) {
    assert.equal(tagLoaded(browser({ hostname })), false, `the tag loaded on ${hostname}`);
  }
  assert.match(analyticsSrc, /const PROD = location\.hostname === "6502\.dbhq\.uk";/);
});

test('a likely bot is not measured, and a real phone with an old browser is', () => {
  assert.equal(tagLoaded(browser({ webdriver: true })), false, 'the tag loaded under navigator.webdriver');
  assert.equal(tagLoaded(browser({ ua: 'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)' })), false, 'the tag loaded for a crawler');
  assert.equal(tagLoaded(browser({ ua: MODERN_CHROME.replace('Chrome/140', 'HeadlessChrome/140') })), false, 'the tag loaded for headless Chrome');
  // Desktop Chrome and Firefox about two years stale are how rotating scrapers present.
  assert.equal(tagLoaded(browser({ ua: MODERN_CHROME.replace('Chrome/140', 'Chrome/118') })), false, 'the tag loaded for Chrome 118 on Windows');
  assert.equal(tagLoaded(browser({ ua: 'Mozilla/5.0 (X11; Linux x86_64; rv:100.0) Gecko/20100101 Firefox/100.0' })), false, 'the tag loaded for Firefox 100 on Linux');
  // Mobile, and Firefox ESR, are exempt: phones and managed desktops keep old browsers for real.
  const oldAndroid = 'Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.36';
  assert.ok(tagLoaded(browser({ ua: oldAndroid })), 'an old mobile Chrome was treated as a bot');
  const esr = 'Mozilla/5.0 (X11; Linux x86_64; rv:128.0) Gecko/20100101 Firefox/128.0';
  assert.ok(tagLoaded(browser({ ua: esr })), 'Firefox ESR was treated as a bot');
});

test('likelyBot is the estate\'s function, word for word', () => {
  // The same function runs on every dbhq.uk site and in the shop, so one bot
  // rule applies to one GA4 property. A local "improvement" here would count
  // the same visitor differently on different hosts. Compared with whitespace
  // collapsed, so formatting is free and logic is not.
  const CANONICAL = `function likelyBot() {
    try {
      if (navigator.webdriver) return true;
      var ua = navigator.userAgent || "";
      if (/bot|crawl|spider|headless/i.test(ua)) return true;
      if (/Android|Mobile|CrOS/.test(ua) || !/Windows NT 10\\.0|Macintosh|X11/.test(ua)) return false;
      var n = Math.max(0, Math.floor((Date.now() - Date.UTC(2025, 8, 2)) / 2592e6));
      var c = /Chrome\\/(\\d+)\\./.exec(ua);
      if (c) return +c[1] < 140 + n - 24;
      var f = /Firefox\\/(\\d+)\\./.exec(ua);
      if (f) return [115, 128, 140, 153].indexOf(+f[1]) < 0 && +f[1] < 142 + n - 24;
    } catch (e) {}
    return false;
  }`;
  const m = analyticsSrc.match(/function likelyBot\(\) \{[\s\S]*?\n\}/);
  assert.ok(m, 'analytics.js has no likelyBot function');
  const squash = (s) => s.replace(/\s+/g, ' ').trim();
  assert.equal(squash(m[0]), squash(CANONICAL), 'likelyBot has drifted from the estate\'s copy');
});

test('the tag is referenced once, inside the load gate, and nowhere else', () => {
  const refs = analyticsSrc.match(/googletagmanager\.com\/gtag\/js/g) ?? [];
  assert.equal(refs.length, 1, 'the tag address appears more than once');
  const gate = analyticsSrc.match(/function load\(\) \{([\s\S]*?)\n\}/);
  assert.ok(gate, 'analytics.js has no load() gate');
  assert.ok(gate[1].includes('googletagmanager.com/gtag/js'), 'the tag is referenced outside load()');
  assert.match(gate[1], /if \(!PROD \|\| bot \|\| loaded\) return;/, 'load() lost a guard');
  assert.ok(!/googletagmanager/.test(consentSrc), 'consent.js loads the tag itself');
  for (const p of all) assert.ok(!/googletagmanager|gtag\(/.test(p.html), `${p.url} names the Google tag in its HTML`);
});

test('opting out stores off on .dbhq.uk, stops every further hit and removes _ga', () => {
  const b = browser({ cookies: { _ga: 'GA1.1.1', _ga_3H3NFGSX85: 'GS2.1.s1', unrelated: 'keep' } });
  assert.ok(tagLoaded(b));
  b.api.optOut();
  // Every attribute is pinned: Domain=dbhq.uk is what makes one opt-out hold on every dbhq.uk site.
  const choice = b.writes.find((w) => w.startsWith('dbhq_analytics='));
  assert.ok(choice, 'no choice cookie was written');
  const attrs = choice.split(';').map((s) => s.trim());
  assert.equal(attrs[0], 'dbhq_analytics=off');
  for (const a of ['Max-Age=31536000', 'Path=/', 'SameSite=Lax', 'Secure', 'Domain=dbhq.uk']) assert.ok(attrs.includes(a), `the choice cookie lacks ${a}: ${choice}`);
  assert.equal(b.api.choice(), 'off');
  // Denied consent alone still sends cookieless pings, the page-leave hit
  // included. Google's per-ID disable flag is what stops them.
  assert.equal(b.window[`ga-disable-${MEASUREMENT_ID}`], true, 'ga-disable was not set');
  const update = b.calls().find((c) => c[0] === 'consent' && c[1] === 'update');
  assert.equal(update?.[2]?.analytics_storage, 'denied', 'consent was not updated to denied');
  // _ga lives on .dbhq.uk, so it is expired there and not only on the host.
  assert.equal(b.jar.has('_ga'), false, '_ga survived opting out');
  assert.equal(b.jar.has('_ga_3H3NFGSX85'), false, '_ga_<id> survived opting out');
  assert.ok(b.writes.some((w) => w.startsWith('_ga=;') && /domain=\.dbhq\.uk/.test(w)), '_ga was not expired on .dbhq.uk');
  assert.equal(b.jar.get('unrelated'), 'keep', 'an unrelated cookie was removed');
});

test('a visitor who opted out loads nothing on the next page, and can turn analytics back on', () => {
  const b = browser({ cookies: { dbhq_analytics: 'off' } });
  assert.equal(tagLoaded(b), false, 'the tag loaded for a visitor who opted out');
  assert.equal(b.calls().length, 0, 'gtag was called for a visitor who opted out');
  assert.equal(b.writes.length, 0, 'an opted-out visitor had a cookie written on arrival');
  b.api.keepOn();
  assert.ok(tagLoaded(b), 'turning back on did not load the tag');
  assert.equal(b.window[`ga-disable-${MEASUREMENT_ID}`], false, 'ga-disable was left set after turning back on');
  assert.ok(b.writes.some((w) => w.startsWith('dbhq_analytics=on;') && w.includes('Domain=dbhq.uk')), 'turning back on did not store on for .dbhq.uk');
  assert.equal(b.api.choice(), 'on');
});

test('opting out and back on in one page does not load the tag twice', () => {
  const b = browser();
  b.api.optOut();
  assert.equal(b.window[`ga-disable-${MEASUREMENT_ID}`], true);
  b.api.keepOn();
  assert.equal(b.window[`ga-disable-${MEASUREMENT_ID}`], false);
  assert.equal(b.appended.length, 1, 'the tag was injected a second time');
  const updates = b.calls().filter((c) => c[0] === 'consent' && c[1] === 'update').map((c) => c[2].analytics_storage);
  assert.deepEqual(updates, ['denied', 'granted'], 'consent did not go denied then granted');
});

test('a choice left under the old opt-in prompt is honoured and carried over', () => {
  // The old prompt kept its answer in localStorage, per origin. "denied" was a
  // refusal and stays one: nothing loads, the cookie takes over, the old key goes.
  const denied = browser({ storage: { 'dbhq-consent': 'denied' } });
  assert.equal(tagLoaded(denied), false, 'an old refusal loaded the tag');
  assert.equal(denied.calls().length, 0);
  assert.equal(denied.jar.get('dbhq_analytics'), 'off', 'an old refusal was not moved to the cookie');
  assert.ok(denied.writes[0].includes('Domain=dbhq.uk'), 'the carried-over cookie is not on .dbhq.uk');
  assert.equal(denied.store.has('dbhq-consent'), false, 'the old key was left behind');
  assert.equal(denied.api.choice(), 'off');

  const granted = browser({ storage: { 'dbhq-consent': 'granted' } });
  assert.ok(tagLoaded(granted));
  assert.equal(granted.jar.get('dbhq_analytics'), 'on');
  assert.equal(granted.store.has('dbhq-consent'), false);

  // The cookie wins over a leftover key, whichever way round they disagree.
  assert.ok(tagLoaded(browser({ cookies: { dbhq_analytics: 'on' }, storage: { 'dbhq-consent': 'denied' } })), 'a leftover old key overrode the cookie');
  assert.equal(tagLoaded(browser({ cookies: { dbhq_analytics: 'off' }, storage: { 'dbhq-consent': 'granted' } })), false, 'a leftover old key overrode the cookie');
});

test('a cookie value that is neither on nor off is ignored, and a visitor with blocked storage is still measured', () => {
  assert.equal(browser({ cookies: { dbhq_analytics: 'maybe' } }).api.choice(), null);
  assert.ok(tagLoaded(browser({ cookies: { dbhq_analytics: 'maybe' } })));
  // localStorage throws in some privacy modes. No readable choice is no choice.
  const blocked = browser({ storageThrows: true });
  assert.ok(tagLoaded(blocked), 'blocked storage stopped the page loading its analytics');
  assert.equal(blocked.api.choice(), null);
  // And it must not stop an opt-out being honoured.
  blocked.api.optOut();
  assert.equal(blocked.jar.get('dbhq_analytics'), 'off');
});

test('off the estate the choice cookie is host-only', () => {
  const b = browser({ hostname: '6502.pages.dev' });
  b.api.optOut();
  const choice = b.writes.find((w) => w.startsWith('dbhq_analytics='));
  assert.ok(choice, 'no choice cookie was written');
  assert.ok(!/Domain=/i.test(choice), 'a pages.dev preview tried to set a dbhq.uk cookie');
  // And on the estate it is not: the positive control for the line above.
  const live = browser();
  live.api.optOut();
  assert.ok(live.writes.find((w) => w.startsWith('dbhq_analytics=')).includes('Domain=dbhq.uk'));
});

// consent.js, run against a fake notice.

test('the notice shows on a first visit, says nothing about state, and OK keeps analytics on', () => {
  const b = browser({ withNotice: true });
  const { box, status, on, off, settings } = b.notice;
  assert.equal(box.hidden, false, 'the notice did not show to a visitor who has not chosen');
  assert.equal(status.hidden, true, 'a first-visit notice shows an on/off status line');
  assert.equal(on.textContent, 'OK');
  assert.equal(off.textContent, 'Opt out');
  assert.equal(box.focused, 0, 'a first-visit notice took focus from the page');
  assert.equal(settings.hidden, false, 'Cookie settings stayed hidden although JavaScript is running');
  on.fire('click');
  assert.equal(box.hidden, true, 'OK did not close the notice');
  assert.equal(b.jar.get('dbhq_analytics'), 'on');
  assert.ok(tagLoaded(b));
});

test('the notice does not show to a visitor who has chosen, either way', () => {
  for (const choice of ['on', 'off']) {
    const b = browser({ withNotice: true, cookies: { dbhq_analytics: choice } });
    assert.equal(b.notice.box.hidden, true, `the notice showed to a visitor who chose ${choice}`);
    assert.equal(b.notice.settings.hidden, false, 'Cookie settings must be there once a choice is made');
  }
  // A choice carried over from the old prompt counts as a choice.
  assert.equal(browser({ withNotice: true, storage: { 'dbhq-consent': 'denied' } }).notice.box.hidden, true);
});

test('Opt out in the notice stops analytics, and Cookie settings reopens it to turn it back on', () => {
  const b = browser({ withNotice: true });
  const { box, status, on, off, settings } = b.notice;
  off.fire('click');
  assert.equal(box.hidden, true, 'Opt out did not close the notice');
  assert.equal(b.jar.get('dbhq_analytics'), 'off');
  assert.equal(b.window[`ga-disable-${MEASUREMENT_ID}`], true);

  settings.fire('click');
  assert.equal(box.hidden, false, 'Cookie settings did not reopen the notice');
  assert.equal(status.hidden, false);
  assert.equal(status.textContent, 'Analytics is off in this browser.');
  assert.equal(on.textContent, 'Turn back on', 'a reopened notice cannot turn analytics back on');
  assert.equal(box.focused, 1, 'a reopened notice did not take focus');

  on.fire('click');
  assert.equal(box.hidden, true);
  assert.equal(settings.focused, 1, 'focus did not return to Cookie settings');
  assert.equal(b.jar.get('dbhq_analytics'), 'on');
  assert.equal(b.window[`ga-disable-${MEASUREMENT_ID}`], false);

  settings.fire('click');
  assert.equal(status.textContent, 'Analytics is on in this browser.');
  assert.equal(on.textContent, 'OK');
});

test('Escape closes a reopened notice without changing the choice, and never dismisses a first-visit notice', () => {
  const first = browser({ withNotice: true });
  first.notice.box.fire('keydown', { key: 'Escape' });
  assert.equal(first.notice.box.hidden, false, 'Escape dismissed a notice that had not been answered');
  assert.equal(first.writes.length, 0, 'Escape stored a choice');

  const later = browser({ withNotice: true, cookies: { dbhq_analytics: 'off' } });
  later.notice.settings.fire('click');
  assert.equal(later.notice.box.hidden, false);
  later.notice.box.fire('keydown', { key: 'a' });
  assert.equal(later.notice.box.hidden, false, 'a stray key closed the notice');
  later.notice.box.fire('keydown', { key: 'Escape' });
  assert.equal(later.notice.box.hidden, true);
  assert.equal(later.jar.get('dbhq_analytics'), 'off', 'Escape changed the choice');
  assert.equal(later.writes.length, 0);
});

// The markup and the wiring on the built pages.

const noticeOn = (html) => html.match(/<aside\b[^>]*data-analytics-notice[^>]*>[\s\S]*?<\/aside>/)?.[0];

test('every page ships the notice hidden, with OK and Opt out, and it is not a modal', () => {
  assert.ok(all.length > 0);
  for (const p of all) {
    const notice = noticeOn(p.html);
    assert.ok(notice, `${p.url}: no analytics notice`);
    assert.match(notice, /^<aside\b[^>]*\shidden(?=[\s>=])/, `${p.url}: the notice is visible before the script decides`);
    assert.match(notice, /\sdata-analytics-on(?=[\s>=])/, `${p.url}: the notice has no OK`);
    assert.match(notice, /\sdata-analytics-off(?=[\s>=])/, `${p.url}: the notice has no Opt out`);
    assert.match(notice, /data-analytics-off[^>]*>Opt out</, `${p.url}: the opt-out button does not say Opt out`);
    assert.match(notice, /data-analytics-on[^>]*>OK</, `${p.url}: the other button does not say OK`);
    assert.ok(!/<dialog\b|aria-modal/.test(p.html), `${p.url}: something modal shipped`);
  }
  assert.ok(!/\.showModal\s*\(/.test(consentSrc), 'consent.js opens the notice as a modal');
  assert.ok(!/::backdrop|\binert\b/.test(component), 'the notice dims or disables the page');
});

test('Opt out is as easy as OK: the same size, side by side, and Opt out first', () => {
  const html = all.find((p) => p.url === '/').html;
  const tag = (attr) => html.match(new RegExp(`<button\\b[^>]*\\s${attr}(?=[\\s>=])[^>]*>`))[0];
  const classes = (attr) => tag(attr).match(/class="([^"]*)"/)[1].split(/\s+/).filter((c) => !c.startsWith('astro-')).sort();
  const onClasses = classes('data-analytics-on');
  const offClasses = classes('data-analytics-off');
  assert.ok(onClasses.includes('btn'), 'OK is not a .btn');
  // The only difference allowed is the site's own ghost pairing, which changes the fill and not the size.
  assert.deepEqual(offClasses.filter((c) => c !== 'ghost'), onClasses.filter((c) => c !== 'ghost'), 'Opt out is sized or styled differently from OK');
  assert.ok(!offClasses.includes('pill') && !onClasses.includes('pill'), 'one button took the lime fill');
  assert.ok(html.indexOf('data-analytics-off') < html.indexOf('data-analytics-on'), 'Opt out must come before OK');
});

test('every page loads analytics.js before consent.js, so Opt out has something to call', () => {
  for (const p of all) {
    const a = p.html.indexOf('src="/analytics.js"');
    const c = p.html.indexOf('src="/consent.js"');
    assert.ok(a > 0 && c > a, `${p.url}: analytics.js must load, and before consent.js`);
  }
});

test('Cookie settings is in the footer of every page, and appears only with JavaScript', () => {
  assert.match(footerAstro, /data-analytics-settings/);
  for (const p of all) {
    const foot = p.html.match(/<footer[\s\S]*?<\/footer>/);
    assert.ok(foot, `${p.url}: no footer`);
    const btn = foot[0].match(/<button\b[^>]*data-analytics-settings[^>]*>([^<]*)<\/button>/);
    assert.ok(btn, `${p.url}: no Cookie settings control in the footer`);
    assert.equal(btn[1].trim(), 'Cookie settings', `${p.url}: the control is not labelled Cookie settings`);
    assert.match(btn[0], /\shidden(?=[\s>=])/, `${p.url}: Cookie settings shows without JavaScript, where it does nothing`);
    assert.ok(noticeOn(p.html), `${p.url}: Cookie settings with no notice to open`);
  }
});

test('the notice says what is counted and that there is no advertising, links the policy, and claims no more', () => {
  for (const p of all) {
    const notice = noticeOn(p.html);
    const text = notice.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ');
    assert.match(text, /counts visits with Google Analytics/, `${p.url}: the notice does not say what is counted, or by whom`);
    assert.match(text, /No advertising/, `${p.url}: the notice does not say there is no advertising`);
    assert.ok(notice.includes('href="https://dbhq.uk/privacy/#analytics"'), `${p.url}: the notice does not link the analytics section of the policy`);
    // Google Analytics sets a cookie and the figures are not anonymous, so the notice must not say otherwise.
    assert.ok(!/anonymous|no cookies|cookie-free|cookieless|privacy-friendly|no tracking|never tracked|consent/i.test(text), `${p.url}: the notice claims more than is true`);
    // DBHQ is one person, and is never "we".
    assert.ok(!/\bwe\b|\bour\b/i.test(text), `${p.url}: the notice speaks as "we"`);
  }
});

test('no page, source file or script still describes the opt-in prompt', () => {
  const OLD = /Count this visit|denied by default|Cookie choice|data-consent|consent-accept|consent-decline|__dbhqEnableGA|__dbhqRevokeGA|remembered in this browser|for this site only/;
  // The journal is the record of what was built and says so about the old prompt; every other page is the site as it is.
  const live = all.filter((p) => !p.url.startsWith('/journal/'));
  assert.ok(live.length > 0 && live.length < all.length, 'the journal pages were not told apart from the rest');
  for (const p of live) assert.ok(!OLD.test(p.html), `${p.url} still carries the opt-in prompt`);
  for (const [name, text] of [['analytics.js', analyticsSrc], ['consent.js', consentSrc], ['AnalyticsNotice.astro', component], ['Footer.astro', footerAstro]]) {
    assert.ok(!OLD.test(text), `${name} still carries the opt-in prompt`);
  }
  // The old key is only ever read and removed, never written: nothing stores a choice there any more.
  assert.ok(!/setItem/.test(analyticsSrc + consentSrc), 'a script writes to localStorage');
  assert.ok(!/localStorage/.test(consentSrc), 'consent.js touches localStorage itself instead of asking analytics.js');
  assert.ok(!fs.existsSync(path.join(process.cwd(), 'src', 'components', 'Consent.astro')), 'the old Consent component is still there');
});

test('the shipped scripts name no other project, property or private path', () => {
  // analytics.js is copied to the site as it is, so it must not name other
  // projects, the property or another repository's path.
  for (const [name, text] of [['analytics.js', analyticsSrc], ['consent.js', consentSrc]]) {
    assert.doesNotMatch(text, /\b(bbs|modem|terraken)\b/i, `${name} names another project`);
    assert.doesNotMatch(text, /\b544327698\b/, `${name} names an analytics account or property`);
    assert.doesNotMatch(text, /\.md\b|\bdocs\/|\bdbhq\//, `${name} points at a file in another repository`);
  }
});

test('the CSP has no unsafe-inline for scripts and allows exactly the Google Analytics origins', () => {
  const GTM = 'https://www.googletagmanager.com';
  // 'wasm-unsafe-eval' lets the KIM-1's page compile its WebAssembly, and nothing else: no eval of JavaScript.
  assert.deepEqual(directives.get('script-src'), ["'self'", "'wasm-unsafe-eval'", GTM]);
  assert.ok(!directives.get('script-src').includes("'unsafe-eval'"), 'script-src allows eval');
  assert.ok(!directives.get('script-src').includes("'unsafe-inline'"), 'script-src allows inline script');
  assert.deepEqual(directives.get('img-src'), ["'self'", 'data:', GTM, 'https://www.google-analytics.com', 'https://*.google-analytics.com']);
  assert.deepEqual(directives.get('connect-src'), ["'self'", GTM, 'https://www.google-analytics.com', 'https://*.google-analytics.com', 'https://*.analytics.google.com']);
  assert.deepEqual([...new Set(csp.match(/https:\/\/[^\s;]+/g))].sort(), ['https://*.analytics.google.com', 'https://*.google-analytics.com', GTM, 'https://www.google-analytics.com'].sort());
  assert.deepEqual(directives.get('default-src'), ["'self'"]);
  assert.deepEqual(directives.get('font-src'), ["'self'"]);
  assert.deepEqual(directives.get('frame-ancestors'), ["'none'"]);
  assert.deepEqual(directives.get('object-src'), ["'none'"]);
  assert.match(headers, /Strict-Transport-Security: max-age=\d+; includeSubDomains/);
});

test('the headers comment says the site runs analytics by default and names both relaxations', () => {
  const comment = headers.split('\n').filter((l) => l.startsWith('#')).join('\n');
  assert.match(comment, /GA RUNS BY DEFAULT/);
  assert.match(comment, /style-src needs 'unsafe-inline'/);
  assert.match(comment, /img-src allows data:/);
  assert.match(comment, /'wasm-unsafe-eval' because the KIM-1's page runs the\n# machine as WebAssembly/);
  assert.doesNotMatch(comment, /CONSENT-GATED|nothing loads until a visitor accepts/i);
  assert.match(csp, /img-src [^;]*\bdata:/);
});
