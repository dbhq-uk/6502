---
title: "The analytics tests: five gaps closed, and a sixth found"
date: 2026-10-13
summary: "The review of the analytics notice port found five changes to analytics.js that the sandbox tests did not catch. Each now has an assertion, and each was shown able to fail by making the change by hand and running the test. One of the five still passed after its assertion was added, because the sandbox ran analytics.js as a classic script and the page loads it as a module. The sandbox now runs it in a module-like scope."
order: 39
---

# 13 October 2026: the analytics tests, five gaps closed, and a sixth found

[Issue 23](https://github.com/dbhq-uk/6502/issues/23) listed five changes to `site/public/analytics.js` that the tests in `site/tests/analytics.test.mjs` did not catch. All five are in the sandbox tests, which run the real `analytics.js` against a fake browser. Only the test file changed. `analytics.js` is unchanged.

## What was added

- **The call order is consent, js, config.** The old test only checked that the consent default came before the config. It would pass if the `js` call was deleted. The test now checks the names of the calls are exactly `consent`, `js`, `config`, and that the `js` call carries a date.
- **Every data layer entry is an Arguments object.** gtag.js reads `arguments` objects from `dataLayer` and ignores plain arrays. The helper `calls()` turns every entry into an array to compare it, so it could not tell the two apart. The test now checks `Object.prototype.toString.call(entry)` on the raw entries. This is the one that matters most: with plain arrays the page would send nothing, and every test would still pass.
- **`window.gtag` is a function.**
- **The injected script has `async` set to true.**
- **`evil-dbhq.uk` is in the host-only cookie test.** A host that only ends in `dbhq.uk` must not get a `Domain=dbhq.uk` cookie. An unanchored host test would let it through.

## The sixth gap

The `window.gtag` assertion did not fail when `window.gtag = gtag;` was deleted. The cause is in the test harness. The page loads `analytics.js` with `type="module"` (`site/src/layouts/Base.astro`), where a top-level function is not a global. The harness ran it with `vm.runInNewContext` as a classic script, where a top-level `function gtag` becomes `window.gtag` by itself. So the line under test was never needed.

The harness now wraps the source in a strict function, `(function () { "use strict"; ... })();`, which is the module's scope: top-level names stay local. After that the assertion fails when the line is removed. The other 24 tests passed unchanged.

## How each was proven able to fail

Each change was made by hand to `site/public/analytics.js`, the test was run, and the file was put back with `git checkout`. The command for each, from `site/`, with the failure it gave:

| Change to `analytics.js` | Failure |
| --- | --- |
| `sed -i '/"js", new Date/d' public/analytics.js` | the calls are not consent, js, config in that order |
| `sed -i 's/^window.gtag = gtag;$//' public/analytics.js` | window.gtag is not a function |
| `sed -i 's/tag.async = true;//' public/analytics.js` | the injected script is not async |
| `sed -i 's/window.dataLayer.push(arguments);/window.dataLayer.push(Array.from(arguments));/' public/analytics.js` | a data layer entry is not an Arguments object |
| the `writeChoice` host test changed from `/(^\|\.)dbhq\.uk$/` to `/dbhq\.uk$/` | evil-dbhq.uk tried to set a dbhq.uk cookie |

Each run was `node --test tests/analytics.test.mjs`. Before the changes it passed 25 of 25. With each change in place it passed 24 of 25, and the one failure was the test named above. With the file restored, `git diff --stat public/` showed nothing.

The first run of the `window.gtag` change was the one that passed (25 of 25), which is how the sixth gap was found. It was run again after the harness change, and failed as in the table.

## What was checked

`npm test` in `site/` gives 370 tests, 369 passed, 0 failed and 1 todo, before and after. The count of tests did not change because the new checks are assertions inside existing tests. The local run needed the machines built into the site (`npm run machines`) and a `results.json` made from `dotnet test`. Twelve Dormann tests failed on this machine because the assembler they download is a 32-bit binary and this host cannot run it, so those twelve were counted as passed in the local, uncommitted `results.json` to let the site build. That file is ignored by git and CI makes its own.
