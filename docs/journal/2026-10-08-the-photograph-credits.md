---
title: "Every photograph's credit now says it is a resized copy"
date: 2026-10-08
summary: "Creative Commons attribution licences ask that a change to a work is indicated. Every photograph on the site is a resized copy, converted to WebP, but the credit line under each one never said so; only the photographs' README did. The credit now ends with a sentence that says it, on every photograph, and a test fails a credit without it."
order: 36
---

# 8 October 2026: the photograph credits say they are resized copies

The final review of the NES's 3D models (6 and 7 October 2026) noticed that the
page's credit under a photograph, `By <author>. Source: <host>. Licence: <licence>.`,
never says that the picture shown is a resized copy of the original. That is
true of every photograph here: the originals are not committed, and each copy in
`site/src/assets/photos/` is made with `cwebp -q 82 -resize 1600 0 -metadata none`
(the photographs' README says so beside each hash). The site then resizes it
again for the page. Creative Commons BY and BY-SA licences ask that the user
"indicate if changes were made". That affects the KIM-1's CC BY-SA photographs,
the BBC Micro's CC BY photograph and the NES's CC BY 4.0 ones.

## What changed

`site/src/components/MachinePhoto.astro` now ends every credit with
"Changes: resized and converted to WebP for this page." `site/tests/site.test.mjs`,
in the test that every photograph is credited wherever it is shown, now also
requires that sentence.

## What it was chosen over

- **Only for the Creative Commons photographs**, which would need the component
  to decide from the licence string whether a licence asks for it. A string
  match can be wrong (a new licence, a typo), and a wrong guess fails silently.
  Saying it on every photograph is true and costs one short sentence for the
  public-domain ones.
- **Only in the README**, where it already was. The licence asks the user of the
  work to be told, and a visitor to the page never sees the README.

## How it was checked

On 8 October 2026, in a worktree on `origin/main` (`5e48505`):

- `cd site && npm run build` built the site; every credit on the three machine
  pages carries the sentence (counted in `dist/`: 6 on the KIM-1's page, 2 on the
  BBC Micro's, 2 on the NES's, each equal to the number of credits on that page).
- `node --test tests/site.test.mjs tests/nes-page.test.mjs tests/registry.test.mjs`
  passed, 59 of 59.
- With the component's change taken out and the test left in, the test
  "every photograph is captioned as a photograph ... and credited with its source,
  wherever it is shown" failed, so the new assertion can fail.

The NES models' branch (`feat/nes-models`) adds more photographs; they get the
same sentence from the same component when this change reaches it.
