---
title: "The analytics notice"
date: 2026-10-01
summary: "The site launched with an opt-in analytics prompt the day after every other DBHQ site dropped that pattern, so it moved to the estate's opt-out notice, and its analytics code is now tested by running it."
order: 6
---

# 1 October 2026: the analytics notice

## Why this changed the morning after launch

The site went live on 1 October 2026 with an opt-in prompt: a modal dialog, all four Consent Mode signals denied, and nothing loaded until a visitor pressed Accept. That was the estate's pattern when the site was designed.

On 30 September 2026 every other DBHQ site moved to a different one: analytics on by default, a notice that does not block the page, and a simple opt-out, under the PECR statistical-purposes exception. This site was built on the same day, from the old pattern, so it launched one step behind and out of line the next morning. Dan decided it should match.

The reason for the estate's change carries over unchanged. An opt-in gate drops everyone who declines or ignores the prompt, so below a large audience the figures describe only the people who pressed Accept. The exception allows analytics without prior consent when it is used only for statistics, the provider acts only as a processor, and visitors get clear information and a simple, free way to object.

## What changed

- **Analytics runs by default on the live host.** It does not run for a visitor who opted out, for a likely bot, or anywhere except `6502.dbhq.uk`. The three advertising signals are denied, and Google Signals and ad personalisation are switched off in the tag. That is the line the exception depends on, so a test holds each signal by name.
- **The choice is a cookie,** `dbhq_analytics=on|off`, on the parent domain `dbhq.uk`. Opting out here opts out on every DBHQ site, and the other way round. The old `localStorage` key could never do that, because storage belongs to one origin.
- **An answer left under the old prompt is kept.** On the first page load, a stored `denied` becomes an opt-out and a stored `granted` becomes on. The cookie is written and the old key removed.
- **The notice is not a modal.** It is a small panel at the foot of the page, shown until the visitor answers. OK and Opt out are the same size and the same button, with Opt out first. It says what is counted and that there is no advertising, and links the estate's privacy policy.
- **"Cookie settings" in the footer reopens it on every page,** with a line saying whether analytics is on or off and, when it is off, a first button reading "Turn back on". It ships hidden and the script reveals it, so a visitor without JavaScript, who gets no analytics either, is not shown a button that does nothing.
- **Opting out stops the page that is open, not only later pages.** Denied consent alone still lets the tag send cookieless pings, including one when the page is left. Setting Google's per-ID disable flag stops every hit from that page, and the `_ga` cookies are expired on the host and on every parent domain.
- **Unchanged:** the one measurement ID with no data stream of its own, the external `analytics.js` and `consent.js` (the Content-Security-Policy still allows no inline script), and the three Google origins in the policy, no more.

## Decisions

- **Match the estate, rather than keep the stricter prompt.** The stricter prompt was a choice nobody made for this site: it was the pattern of the day. Keeping it would have meant one site in the estate counting a different share of its visitors from the rest, and a second behaviour to maintain.
- **Keep this site's own look.** The behaviour is the estate's and the drawing is not. The notice uses the iron surface and a hairline border, with no shadow and no lime, because the lime is rationed to one fill a page and a cookie notice should not spend it. Both buttons are the plain `.btn`; Opt out is the transparent variant. On the notice's own surface the two read the same.
- **No new outline rule for the notice.** A first draft of the notice's styling added a lime focus ring for the panel. The test that names every use of the lime failed on it, correctly, and the ring was dropped: the notice takes the site's existing focus ring, which shows on a keyboard reopen and not on a mouse click.

## How it was tested

The old test file pinned the modal, so it was rewritten rather than edited. The change is that it now runs `analytics.js` and `consent.js` in a Node sandbox with a fake browser (a cookie jar that honours expiry, `localStorage`, `navigator`, a `<head>` that records what is appended, and a fake notice for `consent.js` to find), and checks what a browser would load, instead of searching the source for phrases.

- **Old code:** with the new test file copied into a checkout of the previous commit, `node --test tests/analytics.test.mjs` ran 25 tests, and 21 failed. The 4 that passed are the ones that are meant to hold across both patterns: the one measurement ID, script order, no other project named in the scripts, and the Content-Security-Policy.
- **New code:** the same command passed 25 of 25.
- **Deliberate breaks:** 20 one-line changes were made to the new code, one at a time, in a scratch copy, and the test file was run after each. All 20 were caught by the test that names the behaviour. They were: Signals on, ad storage granted, no disable flag on opt out, `_ga` not expired, no `Domain` on the cookie, the host check loosened, the old key ignored, an opted-out visitor still loaded, the bot function drifted, bots not excluded, the disable flag left set after turning back on, no "Turn back on" label, Escape dismissing a first-visit notice, "Cookie settings" never revealed, no notice on a first visit, `unsafe-inline` in `script-src`, an extra Google origin, the notice visible before the script decides, Opt out made a small button, and the notice claiming "no cookies".
- **Whole suite:** `cd site && npm test` passed 125 of 125. The test floor in both workflows moved from 112 to 125 in the same change.

Two things came out of writing the tests that were not in the plan. A deep-equality check failed on two arrays that looked identical, because arrays built inside the sandbox belong to a different realm from the test's own; the helper copies them with `Array.from`. And a first version of the "no old prompt left" test failed on the earlier journal entries, which describe the old prompt truthfully. They are the record of what was built, so that test skips the journal pages and still has to find the others.

## Differences from the estate's version

- **The wording says "This site counts visits"** where the product sites name themselves, because this site has no product name of its own to use.
- **The link carries no external-link arrow or hidden "(external site)" text.** This site's other external links, such as GitHub in the footer, do not have them either, and the notice follows the site.
- **`consent.js` is behaviour-tested.** The estate's other test suites read its text. Here it is run, so first-visit display, reopening, the status line, focus return and Escape are each checked by what happens.
- **The deploy workflow's comment about the two scripts was wrong for the new pattern.** It said a missing `consent.js` looks like a site with no analytics. With analytics on by default it is worse: the visitor is counted and the notice never shows. The comment now says so.

## Not done

- **Nothing is deployed.** The CI deploy is still off, so merging does not ship this. The live site is still on the opt-in prompt until a deploy is run.
- **Search Console is not set up** for the host. That is unchanged.
