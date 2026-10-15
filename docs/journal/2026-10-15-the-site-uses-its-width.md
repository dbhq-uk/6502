---
title: "The site uses its width"
date: 2026-10-15
summary: "Dan found the margins too big. Every reading section was a narrow strip centred in the page, which left wide empty margins on a desktop and squeezed the machine panels. Now each section takes the whole container, left-aligned with the header, and only running text is held to a readable line. The side padding and the space between sections are smaller, a machine's screen fits the window, and a phone no longer scrolls sideways on the NES page."
order: 65
---

# 15 October 2026: the site uses its width

This entry records the layout half of the site tidy pass. Dan's words: "the margin widths are too big". The copy half is a separate pull request; this one changes only the stylesheets, a test and the docs.

## What was wrong

Nearly every section of a machine page, and the about page, the status page and every journal entry, sat in `<div class="wrap narrow">`, and `.narrow` capped the container at 860 pixels, centred. `.prose` and the journal list did the same. On a 1440-pixel window that left 330 pixels of black either side of the text, and the machine's panel, its screen and its tables were squeezed into 780 pixels. The vertical spacing was large too: a section had 56 to 96 pixels above and below it.

The strip was also too wide for its own text. A paragraph of rendered markdown filled the 780 pixels, about 109 characters a line, which is longer than is comfortable to read.

## What was decided

- **One container, used to its edges.** `--max` is 1320 pixels, up from 1280, and `--pad` is 16 to 32 pixels a side, down from 20 to 40. `.narrow` no longer caps the width: a reading section takes the whole container, left-aligned with the brand in the header. The panel, the tables, the code and the photographs use the full width.
- **A measure for running text only.** `--measure` is 33em. Paragraphs, list items and the panels' notes are held to it, left-aligned. It is in em, so it follows the font size. The panels' notes were capped at 72ch, but Inter's characters are narrower than its zero, so 72ch held about 90 characters.
- **Less space between sections.** `--section` is 32 to 64 pixels. The home page's hero, the section heads and the footer lost some padding too.
- **The machine head** puts its text at the measure on the left and the photograph at the container's right edge, in line with the panel under it. The photograph's column is 360 pixels, up from 300, its existing largest size.
- **A machine's screen fits the window.** At full width the BBC Micro's screen would have been more than 850 pixels tall on a 900-pixel window. Each screen is now as wide as its panel but never taller than the window under the header, with room for the first row of controls (`--screen-fit`).
- **The NES and Atari 2600 pads keep a pad's width,** centred under the screen, so the d-pad and the fire buttons do not drift to the panel's far edges.
- **The journal list runs in two columns** where two fit at a readable width, and in one below that.

The alternative was to keep the centred column and make it wider. It was not taken because a centred column wide enough for the panel is too wide for text, and a centred column narrow enough for text squeezes the panel. Left-aligned text with a measure lets both have what they need.

The look did not change: no colour, type, radius, shadow or lime use was touched, and `tests/design.test.mjs` passes unchanged.

## What turned up

Taking the screenshots at 390 pixels showed that four of the seven pages photographed were already wider than a phone before this change, so a visitor could scroll them sideways:

- **The NES page.** The pad's narrow-screen layout needed about 394 pixels and the panel had 316. Select and Start now take two columns each under the pad, which fits with every button still 44 pixels or more.
- **The Atari 2600 page.** A long hash in the rights paragraph did not break.
- **The status page and a journal entry.** Long test names in `code` did not break.

Running text in `.prose` now breaks a long word rather than overflow, and `code` there may break anywhere. After the change no page of the built site, all 98 of them, is wider than the window at 390 or at 1024 pixels wide; a script outside the repository loaded each one and compared its scroll width with the window's.

## What was checked

Screenshots before and after, at 1440, 1024 and 390 pixels wide, of the Atari 2600, NES and BBC Micro pages, the home page, the status page, the about page and the newest journal entry, taken with Playwright's Chrome from a script outside the repository on the built site. The same script measured where the text starts, the width of the first paragraph and of the machine's panel, and the page's height. On 15 October 2026, on the site built from `main` at `9568268` and then from this branch:

| At 1440 pixels | Before | After |
| --- | --- | --- |
| Left edge of the text | 330 px | 92 px |
| Machine panel | 780 px | 1256 px |
| A line of a journal entry or the about page | about 109 characters | about 74 characters |
| A line of a machine page's head | about 60 characters | about 74 characters |

At 1024 pixels the panel went from 780 to 960 pixels and the text from 122 to 32 pixels in; at 390 the panel went from 350 to 358 pixels. Pages got shorter on a phone (the Atari 2600's from 10,860 to 10,404 pixels, the home page's from 6,172 to 5,849) and on a desktop the home page did too (4,462 to 3,956). Pages of long prose got taller on a desktop, because their lines got shorter: the BBC Micro's from 12,594 to 13,921 pixels and the status page's from 58,570 to 73,890.

`site/tests/layout.test.mjs` holds the numbers: the four tokens, the section padding, that no reading section caps its width again, that the paragraphs and notes use the measure, which few rules keep a cap in `ch` and why, that every screen is held to the window, and the pads. It converts `--measure` to characters with Inter's average character, 7.13 pixels at 16 pixels, measured in Chrome on the built Atari 2600 page, and fails outside 70 to 75. Against the old stylesheets all four tests failed; with only `--measure` raised to 40em, the measure test failed at about 90 characters; with only the 860-pixel cap put back on `.narrow`, the section test failed.

The whole site suite (`cd site && npm test`) and the browser check (`node scripts/browser-check.mjs`) passed on 15 October 2026. The BBC Micro's pinned screen on a phone still works: at 390 by 844 the screen ran from the header at 130 pixels to 377 with the last key at 598, and none of 148 Tab stops left the focused control behind the screen. The screen is 6 pixels taller than before because the side padding is 4 pixels smaller. No speed figure was recorded.

## What is left

The class is still called `.narrow`, though it no longer narrows anything. Renaming it means editing the machine page's markup, which the copy pull request is changing at the same time, so it is left for later.
