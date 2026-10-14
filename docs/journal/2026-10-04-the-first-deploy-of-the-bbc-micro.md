---
title: "The first deploy of the BBC Micro, and what it taught the deploy"
date: 2026-10-04
summary: "The merge that put the BBC Micro on the site failed its own check of the live pages, because the new pages were not at the edge yet. The site was fine a few seconds later. The check now waits for a page that is late, and the deploy now counts a build's machine files before it publishes anything, not only after."
order: 27
---

# 4 October 2026: the first deploy of the BBC Micro

## What happened

The pull request for the BBC Micro merged to `main` at 10:58 UTC and the deploy
workflow ran. Its build, the site's tests, the upload to Cloudflare Pages and
the cache purge all passed. The next step, "Every page must be serving", then
failed: every page that had not existed in the previous deployment answered 404,
thirty-odd journal and machine pages among them, while every page that already
existed answered 200. The four checks after it were skipped.

About thirty seconds later the whole site was live. The home page said "2 of 51",
`/machines/bbc-micro/` answered 200, and a run of the check's own commands by
hand against the live site found every page and every machine file correct,
with the right content types. Re-running the failed job passed every check,
including the 26 machine files and the headers at the edge.

## What it was

A race, not a fault in the site. The deploy command returns when the upload is
accepted, and the new version reaches the edge a little later. The check ran in
that gap. It had passed on every earlier deploy because those added one or two
pages, so the gap rarely showed. This one added more than thirty.

## What was changed, and what was chosen over

- **A late page is retried, and a missing page still fails.** Each page that does
  not answer 200 is tried again every five seconds for about ninety seconds
  before the check counts it as a failure. This was chosen over a single fixed
  sleep before the check, which would be too short on a bad day and wasted time
  on a good one, and over making the deploy wait on the Pages API, which would
  be a new dependency inside the step that holds the token.
- **Every try has its own cache-buster.** The check used one query string for
  every request, and the same URL could hand back the same cached 404 for the
  whole window, which would have made the retry useless. A page that never
  reaches 200 still fails, after eighteen tries.
- **The machine files are counted on the build, before the deploy.** The check
  that every machine file is serving, and that the floor of 21 is met, stays
  after the deploy, because only the edge shows content types. A new step before
  the upload now counts the build's machine files against the same floor, so a
  build that is missing a machine fails before anything is published and not
  after it has gone live.

## How it was checked

The workflow is not run by the pull request's own checks, so its new shell was
tested by hand on 4 October 2026. Each step was taken out of the workflow file
and run in a scratch folder:

- the count step on the real build printed 26 machine files and passed;
- the same step on a copy of the build with only the KIM-1's machine printed 13
  and failed;
- the page loop against a local server that answered 404 to the first three
  tries of each page passed, and printed each retry;
- the same loop with one page that never answers 200 failed after eighteen
  tries.

What those runs do not show is the step against the real edge. The next real
deploy showed it, and it showed a gap.

## The next deploy: the same race, in the step that was not hardened

The preset discs merged that evening and the deploy ran with the new checks. The
page check, now patient, passed, and so did the new count of the build's machine
files. Then "Every machine file must be serving" failed: two of the new
WebAssembly files and three of the new discs answered 404 at the moment of
checking, while the other thirty-eight files, which already existed or had
arrived, answered 200. A run of the same commands by hand against the live site
a few minutes later found all seventeen discs at the right size, both
WebAssembly files served as `application/wasm`, the disc images served as
`application/octet-stream`, and a browser check of the preset flow on the live
page passed with no console errors or failed requests. So the site was right and
the check was early again.

The first fix covered one step, the page check. The race belongs to every check
that runs after the deploy and looks for something new, so it was wrong to patch
the one that had failed. The machine-readable files, the machine files and the
response headers now wait the same ninety seconds, each try with its own
cache-buster, and a machine file is retried until it answers 200 **with the
right content type**, so a late file cannot pass on the wrong type. They were
tested the same way as the page check on 4 October 2026: a local server that
answered 404 to the first three tries of each file passed with each retry
printed; a file that never arrives failed after eighteen tries; and the headers
step passed once the server began sending its three headers on the fourth try.

## What was chosen over

- **A longer fixed wait after the deploy step** was rejected again: it costs the
  same time on every deploy and is still wrong on a bad day.
- **Waiting on Cloudflare's API for the deployment to be live** was rejected
  again: it adds a dependency inside a step that holds the token, to avoid
  sending a few more requests.

## Mistakes

- **The first deploy of a large change was treated as a pass for the site and a
  failure for the workflow, and it took a manual run of the check's commands to
  tell them apart.** A red run that skips the checks after it says less than it
  looks. The retry makes a late page a delay, so a red run now means a page did
  not arrive.
- **The first fix was one step wide.** It was written for the check that had
  failed, and the next deploy failed in the neighbouring one for the same reason.
  When a failure has a cause, the fix belongs to everything that shares the
  cause.
