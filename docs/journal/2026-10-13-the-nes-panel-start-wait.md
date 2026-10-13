---
title: "The NES panel tests waited for a count of turns, not for the page"
date: 2026-10-13
summary: "Two NES panel tests failed once each in CI with the panel still loading. The helper that waits for Start spun a fixed number of event-loop turns, which is not a wait when the page's start-up does real work. It now waits for the state, with a time limit, and a slowed fetch shows the old loop fails and the new wait does not."
order: 43
---

# 13 October 2026: the NES panel tests waited for a count of turns, not for the page

Issue 91 records two failures in the site job of CI on 13 October 2026, on pull requests 89 and 93, in two different tests of `site/tests/nes-panel.test.mjs`. Both said `the page did not start: Loading the NES`: the state was `loading` where `running` was expected. Neither pull request touched the NES page or its script, and re-running the failed job passed.

## What was wrong

The helper `start()` pressed Start and then looped 1000 times on `setImmediate`, stopping early if the state became `running`. One pass of that loop takes very little time, so the loop is a wait for a number of turns of the event loop, not for a length of time. The page's start-up awaits real work (a dynamic import, a file read), and how long that takes depends on how busy the runner is. When it took longer than 1000 quick turns, the assertion ran with the panel still loading. A second loop, in the test for a new cartridge, waited 100 turns for two events in the same way.

## What changed

A helper `until(done, ms)` polls every 2 ms of real time and gives up after 10 seconds. Both loops use it. The assertions are unchanged, so a page that never starts still fails, now with the status line in the message after 10 seconds instead of after a moment.

## How it was checked

The failure cannot be made to happen on demand, so it was made likely. The fake `fetch` in `nesPage()` was slowed by 100 ms, on a scratch copy of the test file.

- Old loop, fetch slowed by 100 ms: 28 of 33 tests passed and 5 failed with `the page did not start: Loading the NES: the emulator and its cartridge.`
- New wait, fetch slowed by 100 ms: 33 of 33 passed.
- New wait, fetch as committed: 33 of 33 passed (`node --test tests/nes-panel.test.mjs`).

The slowed fetch is not committed. The site suite's floor is unchanged, because no test was added.

## Not claimed

This is the cause that fits the message and the code, shown by reproduction with a slowed fetch. It is not shown that CI's two failures were this and nothing else. If the failure comes back with the new wait in place, issue 91 stays open and the 10 second limit is the first thing to question.
