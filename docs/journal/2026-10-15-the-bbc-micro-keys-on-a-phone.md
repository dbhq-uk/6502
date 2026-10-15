---
title: "The BBC Micro's keys on a phone: the screen stays put"
date: 2026-10-15
summary: "On a phone the BBC Micro's on-screen keys stand taller than the screen, so a visitor could not see the picture and the keys together. The keys cannot be made shorter without breaking the touch size, so the picture is now pinned under the site header while the machine runs and the keys scroll past it. The browser check scrolls to the last key and fails if the screen is not still in view."
order: 61
---

# 15 October 2026: the BBC Micro's keys on a phone

At 390 pixels wide the BBC Micro page's on-screen keys wrap to about 710 pixels tall, so a phone visitor could not see the screen and the keys together. All the keys meet the 44 pixel minimum and nothing overflows, so the page worked, but it was awkward to play on. This is issue 43, found in the review of the page's task 14a.

## What was measured

On 15 October 2026 the BBC Micro part of the browser check (`cd site && node scripts/browser-check.mjs`, run for that part alone, on the built site in Chrome) printed `the key block is 708 px tall in 15 lines` at 390 px wide, before any change to the layout. In the same step the screen was 241 px tall and the sticky site header 130 px, because its links wrap onto a second line on a phone.

A key is at least 44 px wide, and the key block's container is 316 px wide there, so 6 keys fit on a line and not 7.

## What was decided

The issue offered three fixes: balanced half-rows, the keys before the screen, or a collapsible key panel. Counting the lines showed that the first two do nothing for the height. Splitting each row evenly gives the same 15 lines, because a row of 14 or 15 keys needs 3 lines whichever way it is split, and putting the keys first only moves the problem. A collapsible panel hides the keys when the visitor wants them.

What the visitor needs is the picture in view while they reach for a key. So the screen is pinned. On a phone-sized window, up to 480 px wide and at least 560 px tall, once the machine is running, the screen is `position: sticky` under the site header and the keys scroll past beneath it. The height of the header, which is two lines on a phone and one on a tablet, is read by the page's script and handed to the stylesheet as `--bbc-pin`, rather than typed into it.

Two details that were needed, and one that was weighed.

- **The panel is a column while the machine runs.** A sticky item moves only inside its own grid area, and the panel was a grid whose rows were each as tall as their content, so the screen had nowhere to move. A flex column gives it the whole panel.
- **Only while the machine runs.** Before Start the screen is blank, and pinning a black rectangle over the page's notes helps nobody.
- **The keys stay 44 px.** Shrinking them would have fitted more per screen, and was rejected: the 44 px floor is a decision of the site, held by a test.

The look is meant to stay as it was: the change adds no colour and no shadow, and the lime stays on the Start button and the screen's focus ring. The tests show that the key size, the pinned rule and the unchanged tokens hold; they do not compare pictures, and while the machine runs the panel is a flex column where it was a grid, so the spacing between its parts is the same only because both use the same 14 px gap, which no test holds.

## A focus problem the review found

The first version passed its own check and still hid the key that had the focus. The page's `scroll-padding-top` is 140 px, enough for the header and not for the screen below it, so Shift+Tab to a key near the top of the block scrolled it to just under the header, behind the pinned screen (WCAG 2.2, 2.4.11, focus not obscured). The review counted 45 of 70 Shift+Tab stops this way at 390 by 844.

The driver now also gives the stylesheet the screen's height as `--bbc-screen-height`, kept current by a `ResizeObserver` as `--bbc-pin` is, and the keys, Break and the sound button take `scroll-margin-top` of that height and 8 px while the machine runs. The padding already clears the header, so the margin only has to clear the screen, and it adds to the padding. This is a height and not the screen's bottom edge, which the review proposed, because the bottom edge would count the header twice. A browser without `ResizeObserver` reads both sizes once, and does not stop.

The browser check has a step for it. It presses Shift+Tab from the last key back to the screen, then Tab forward again, and fails if the focused control is wholly behind the screen or under the header at any stop. Before the change that step failed on 39 of 148 stops (the first was the V key, at 323 to 367 px while the screen covered 130 to 371); after it, none did. Both runs were the BBC part alone, on 15 October 2026.

## What was checked

A test in `site/tests/bbc-page.test.mjs` holds the rule: the phone rule exists, the panel is a column, the screen is sticky at the header's height, nothing pins the screen outside that rule, and the script sets `--bbc-pin` from the header and `--bbc-screen-height` from the screen, and keys that take the focus clear the screen. A test in `site/tests/bbc-micro.test.mjs` runs the page script in a fake browser to show both sizes are set, follow a resize, and are still set where there is no `ResizeObserver`. It failed before the change.

The browser check (`site/scripts/browser-check-bbc.mjs`) now resizes the page to 390 by 844 after Start, scrolls the last key into view, and fails unless the whole screen is still in view above it. Run on the BBC part alone on 15 October 2026, before the change the screen had scrolled to 904 px above the top of the window; after it, the screen sat from the header's bottom at 130 px to 371 px, with the last key at 470 px. No speed figure was recorded.

## What is left

The pinned screen and the header take 371 px of an 844 px window. The same step printed the last key at 594 px with the screen pinned, and 473 px is left below the screen, which is room for ten lines of keys at 47 px each (44 px and a 3 px gap). The analytics notice, when it is open, covers part of that, and I did not measure by how much. That is the cost of keeping the keys big enough to hit.

On a short window, about 360 by 560 or a phone whose browser toolbars collapse as the page scrolls, the window's height crosses the 560 px that the rule asks for, so the pin can switch on and off as the page scrolls. That is accepted: it fits the window it is in at each moment, and no height is better than a pin that is wrong. The Electron's page has the same layout and the same problem; it is not changed here, and it would be the same fix.
