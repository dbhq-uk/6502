---
title: "The journal's orders are unique within a day"
date: 2026-10-14
summary: "Four pairs of journal entries shared a date and an order, so the site listed them by file name, not by the order the work happened in. Each pair now has distinct orders that follow the work, the rule is written down in the journal's README, and a test fails on a new tie."
order: 51
---

# 14 October 2026: the journal's orders are unique within a day

The journal lists entries newest first by date, then by `order`, then by file name. Four pairs of entries shared a date and an order, so within those days the reading order followed the alphabet. This change gives each pair distinct orders and adds a test so that a new tie fails the build. It closes issue 115.

## What was found

A script grouped every entry by date and order. It found the three pairs the issue named and one more, made later the same day:

- 1 October: `inside-the-chip` and `the-kim-1`.
- 5 October: `the-nes-models-design` and `the-nes-plan`, and `the-nes-models` and `the-nes-speed`.
- 14 October: `the-atari-2600-plan` and `the-test-floor-in-one-file`. The merge train of that day had already renumbered entries by hand, and this tie came in after it.

## What was decided

The orders follow the order the entries were first committed, which each entry's pull request shows. On 1 October the analytics notice came first, then the chip page, then the KIM-1 (pull request 18, which also holds the KIM-1 in the browser). On 5 October the design came first, then the plan, then the models' inputs, then the speed check. On 14 October the floor entry was committed at 12:11 and the Atari plan at 12:22.

The later entry of each pair moved up, and the entries after it on that day moved up with it, so the sequence stays right. That is six front matter edits and no change to any text. On 1 October the KIM-1 is now 8 and the KIM-1 in the browser 9. On 5 October the plan is 30, the models' inputs 31 and the speed check 32. On 14 October the Atari plan is 50. Orders are only compared within a date, so the numbers on neighbouring days did not need to change.

This was chosen over accepting ties and writing the file-name rule down. A tie is a silent choice by the alphabet, and the entries are a record of sequence.

Fractions such as 7.5 were not used. The schema allows them, but the existing test and the README treat `order` as a whole number.

## What was checked

Nothing in the repository links to an order number. The mentions in the Electron entries are prose about the numbers, and they are left as they were written, because they describe the state of that day.

The new test in `site/tests/journal.test.mjs` reads every entry's date and order and fails on a tie, naming the files. It was run on `main` as it was, where it failed on the four pairs above, then after the renumbering, where it passed. It also shows that it can fail: it puts one entry's order on another entry of the same date and expects the tie to be found, and expects the same order on a different date not to be one.

The test adds one to the suite, so the floor in `site/tests/floor.json` goes from 641 to 642. That figure is the expected one; CI's count is the measurement.

## The rule

`docs/journal/README.md` now says that no two entries on a date share an order, how to number a new entry, and what to do about a tie.
