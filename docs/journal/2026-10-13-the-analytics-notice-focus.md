---
title: "Keyboard focus stays in the page after the analytics notice is answered"
date: 2026-10-13
summary: "Pressing OK or Opt out on the first visit hid the button that had focus, so the browser dropped focus to the top of the page. Focus now goes to the Cookie settings button in the footer, without scrolling the page. The status line is a live region. The notice moved from after the footer to just after the skip link, because it is a fixed panel and nothing in the layout or the tests depended on its place. Four tests fail without the change."
order: 40
---

# 13 October 2026: keyboard focus after the analytics notice

Issue #24. The analytics notice is a small panel with two buttons, OK and Opt out. When a visitor answered it for the first time, `hide()` in `site/public/consent.js` hid the panel and, because nothing had opened it, did not move focus. The focused button had just been hidden, so the browser dropped focus to the top of the page. A keyboard visitor had to start again from the skip link.

The notice also sat after the footer in the DOM, so a keyboard visitor reached it last, and the status line was a plain paragraph.

## What was done

**Focus.** `hide()` now always puts focus somewhere. If the Cookie settings button reopened the notice, focus goes back to that button, as before. If not (the first-visit case), focus goes to the first Cookie settings button on the page. That button is the notice's own way back, so it is the natural next stop. The call is `focus({ preventScroll: true })`. The button is in the footer, and without that option a click on OK would throw the page to the bottom. The cost is that a keyboard visitor's focus ring is off screen until the next Tab, which scrolls it into view. That was judged better than a jump on every mouse click.

**Status line.** It is now `role="status"`. One limit is worth recording: the line is `hidden` on a first visit and un-hidden on a reopen, and an element that appears already filled is not reliably announced. On a reopen focus moves to the notice itself, which has the label "Analytics notice", so the text is read anyway. The role is the right markup and costs nothing, but it should not be counted as the thing that makes the reopen audible.

**Position in the DOM.** Moved: `AnalyticsNotice` is now the second child of the body, straight after the skip link, and before the header. The notice is `position: fixed`, so its place in the source has no effect on the layout. No CSS rule or test depends on it coming last (`grep` for sibling selectors and for the footer found none; the design test keys on class names only). The first Tab goes to the skip link, the second to the notice while it is showing. Once answered it is `hidden` and leaves the tab order, so a returning visitor sees no change.

## What it was chosen against

- Focusing the page's `main` or the skip link instead. Both put a visitor back at the top, which is the fault being fixed. Cookie settings is the control for this notice, so it is the nearest relevant place.
- Focusing Cookie settings with scrolling. Rejected for the jump described above.
- Leaving the notice where it was and only fixing focus. Moving it is a one-line change in `Base.astro`, covered by a test, so there was no reason to leave a known problem. If the layout had stacked the notice in the flow, or a test had pinned its place, it would have been recorded as not done.

## How it was checked

`site/tests/analytics.test.mjs` runs `consent.js` in a sandbox with fake elements. The fake `focus()` now records its options. New or changed tests:

- After a first-visit OK or Opt out, Cookie settings has been focused once, with `preventScroll`, and the hidden notice was not focused.
- A reopened notice returns focus to Cookie settings on a choice and on Escape. The existing reopen test now also counts the first-visit focus.
- Every built page has `role="status"` on the status line.
- Every built page has the notice after the skip link and before the header, main and footer.

The tests were run against the fix reverted (`consent.js`, the component and the layout from `main`, with the new tests kept): four failed, the three new ones above and the changed reopen test. With the fix back, all 29 in the file pass. The reopen-returns-focus test passes either way, because that path already worked; it is there to keep it working.

No browser was run. Chrome is not installed on this host, so the real focus ring, the scroll behaviour and a screen reader were not tried. They should be before this is called finished for assistive technology.

## The rest of the estate

The issue says this matches the reference on terraken, so the fix belongs there too. The notice is the same pattern on every dbhq.uk site. This change fixes 6502.dbhq.uk only, and the same three changes (focus in `hide()`, `role="status"`, notice placed after the skip link) need to be made in the reference and from there carried to the other sites.
