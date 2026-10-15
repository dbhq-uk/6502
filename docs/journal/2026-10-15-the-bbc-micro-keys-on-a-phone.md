---
title: "The BBC Micro's keys on a phone: the screen stays put"
date: 2026-10-15
summary: "On a phone the BBC Micro's on-screen keys stand taller than the screen, so a visitor could not see the picture and the keys together. The keys cannot be made shorter without breaking the touch size, so the picture is now pinned under the site header while the machine runs and the keys scroll past it. The browser check scrolls to the last key and fails if the screen is not still in view."
order: 61
---

# 15 October 2026: the BBC Micro's keys on a phone

At 390 pixels wide the BBC Micro page's on-screen keys wrap to about 710 pixels tall, so a phone visitor could not see the screen and the keys together. All the keys meet the 44 pixel minimum and nothing overflows, so the page worked, but it was awkward to play on. This is issue 43, found in the review of the page's task 14a.

## What was measured

On 15 October 2026, with a Playwright script that loads `/machines/bbc-micro/` from the built site and reads each row's wrapped lines, the key block was 708 px tall at 390 px wide, in 15 lines of at most 6 keys. The screen was 241 px tall and the sticky site header 130 px, because its links wrap onto a second line on a phone.

A key is at least 44 px wide, and the container is 316 px wide, so 6 keys fit on a line and not 7.

## What was decided

The issue offered three fixes: balanced half-rows, the keys before the screen, or a collapsible key panel. Counting the lines showed that the first two do nothing for the height. Splitting each row evenly gives the same 15 lines, because a row of 14 or 15 keys needs 3 lines whichever way it is split, and putting the keys first only moves the problem. A collapsible panel hides the keys when the visitor wants them.

What the visitor needs is the picture in view while they reach for a key. So the screen is pinned. On a phone-sized window, up to 480 px wide and at least 560 px tall, once the machine is running, the screen is `position: sticky` under the site header and the keys scroll past beneath it. The height of the header, which is two lines on a phone and one on a tablet, is read by the page's script and handed to the stylesheet as `--bbc-pin`, rather than typed into it.

Two details that were needed, and one that was weighed.

- **The panel is a column while the machine runs.** A sticky item moves only inside its own grid area, and the panel was a grid whose rows were each as tall as their content, so the screen had nowhere to move. A flex column gives it the whole panel.
- **Only while the machine runs.** Before Start the screen is blank, and pinning a black rectangle over the page's notes helps nobody.
- **The keys stay 44 px.** Shrinking them would have fitted more per screen, and was rejected: the 44 px floor is a decision of the site, held by a test.

Nothing about the look changed: no new colour, no shadow, and the lime stays on the Start button and the screen's focus ring.

## What was checked

A test in `site/tests/bbc-page.test.mjs` holds the rule: the phone rule exists, the panel is a column, the screen is sticky at the header's height, nothing pins the screen outside that rule, and the script sets `--bbc-pin` from the header. It failed before the change.

The browser check (`site/scripts/browser-check-bbc.mjs`) now resizes the page to 390 by 844 after Start, scrolls the last key into view, and fails unless the whole screen is still in view above it. Run on the BBC part alone on 15 October 2026, before the change the screen had scrolled to 904 px above the top of the window; after it, the screen sat from the header's bottom at 130 px to 371 px, with the last key at 470 px. No speed figure was recorded.

## What is left

The pinned screen takes about a third of a phone's height, so about seven lines of keys are in view at once. That is the cost of keeping the keys big enough to hit. The Electron's page has the same layout and the same problem; it is not changed here, and it would be the same fix.
