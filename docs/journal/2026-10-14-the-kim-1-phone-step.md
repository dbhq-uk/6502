---
title: "The KIM-1's phone step, and a line that changed height"
date: 2026-10-14
summary: "The browser check's phone step for the KIM-1 failed now and then because the page moved under the pointer. The cause was the one-line speed sentence under each machine, which changes every second and sometimes wraps onto a second line on a phone. The sentence now sits in a box as tall as the tallest sentence, so it never changes the height of the page."
order: 56
---

# 14 October 2026: the KIM-1's phone step, and a line that changed height

The browser check has a step that runs the KIM-1's page at phone width and presses its keys. It failed now and then with the page having moved nineteen pixels. This is issue 80.

## What was found

The machines' pages show a sentence about headroom, such as how many times faster than the real board the browser could run it. The page rewrites it once a second. On a phone, one more digit, or a different kind of sentence, can wrap it onto one more line. One line at a phone's size is 18.75 px, which is the 12.5 px type at a line height of 1.5.

When the sentence grew by a line, the browser's scroll anchoring moved the page to keep something else in place. That is the nineteen pixels. It happens only when the sentence changes length at the wrong moment, which is why the step failed on some runs and not others, and more often when the runner was busy.

## What was decided

The sentence is now inside a `.speed-line`, over a hidden copy of the longest sentence of each kind. The line is always as tall as the tallest, so a change of text cannot change the height of the page. This was chosen over fixing the height in CSS, because the height of the longest sentence depends on the width of the screen and the font, and only the browser knows it.

It was chosen over making the browser check retry the step, which would hide a real page that moves under a finger.

## What was checked

The browser check now reads the height of the line for every kind of sentence at phone width and fails if they differ. The unit tests for `machine-host.js` cover the hidden room and the choice of the longest sentence.

A throwaway pull request (164, never merged) ran only the phone step 25 times with the runner's CPUs loaded, on the page as `main` had it and on the fixed page. On the old page the speed line's height varied between runs and within one, from 37.50 to 56.25 px. On the fixed page it was 56.25 px every time. The old page did not move by nineteen pixels in those 25 runs, so the run shows the cause (a line that changes height) and the fix (a line that does not), but it did not reproduce the failure itself. The failure is rare, and nothing here proves it cannot happen for another reason.
