# The NES's 3D models: handover

Written 13 October 2026, when the work moved from the dev machine to a new
agent on `dbhq-arm` (an idle 4-core ARM machine). Read this first, then
[the design](../specs/2026-10-05-nes-models-design.md) and
[the plan](2026-10-05-nes-models.md). The plan has every task's dated notes, and
the journal entry `docs/journal/2026-10-05-the-nes-models.md` has each task's
section, with the review findings and the mistakes. Nothing below replaces them.
It says where the work stands, what is left, how to set up an ARM machine, and
the rules that were settled along the way.

## Where it stands

- **Branch `feat/nes-models`** is pushed. It holds the whole of the work, with
  `main` merged in (last at `c886d96`). The tree was clean when this was written.
- **Pull request #76** (draft, `Closes #68`): the NES's two 3D models. CI was
  green on all four jobs (dashes, machines, test, site) on 8 October 2026,
  including the whole browser check.
- **Pull request #75** (draft, branch `fix/photo-credit-changes`): every
  photograph's credit says it is a resized copy (CC BY and CC BY-SA ask that a
  change is indicated). Independent of #76, in either order.
- **Nothing is merged, and merging is Dan's go, not yours.** Merging to `main`
  deploys the site. Both pull requests say "Not for merge until Dan says".
- **Tasks 0 to 10 of the plan are done**, each reviewed by a separate agent, and
  the whole branch had a final review that found no overclaim. After it, one fix
  wave and one last two-line fix were made and checked.
- **Issues for what is left:** #77 (the speed re-measure), #78 (the copper map),
  #79 (the rear connectors and the case limits), #80 (the KIM-1's phone step in
  the browser check), #81 (small notes from the reviews).

## What to do, in order

1. **#77: measure the chip counters' speed again, on the idle ARM machine.**
   This is the one open item that matters before a merge. The counters (four
   calls to `_accesses.Note(...)` in `src/Dbhq.Machines.Nes/NesBus.cs`) were
   measured on 7 October 2026 against the old `main`, before the lazy PPU
   (#70) made the machine about 1.3 times faster, so the same counter is now a
   larger share. The result then passed its rule narrowly, with wide intervals
   (`docs/journal/2026-10-07-the-nes-models-speed.md`).
   - Method: `bench/nes-speed/README.md`, with `THREAD_TIME=8` and the AOT
     build, builds alternated, at least eight launches a side in each region,
     one launch at a time on an idle host (load under 1, checked before every
     launch). Baseline: `main` as it was just before #76 (that is,
     `git merge-base origin/main HEAD`, or `origin/main` if #76 has not merged).
     After: the branch.
   - **Fix the pass rule before the first launch and write it in the report.**
     The earlier rule: in each region the median of the per-launch ratios (after
     over before) at least 0.98 and the lower end of a 90 per cent bootstrap
     interval at least 0.96 is a pass; an upper end below 0.98 is a fail; the
     rest is inconclusive, with one more batch and then stop. Keep every launch.
   - This machine is ARM. The figures are not comparable with the x64 figures in
     the bench README's table, but the ratio of the two builds is what is
     judged, and that carries over. Say it is ARM in the journal and the table.
   - Run the differential check afterwards (`cmp` the two outputs: they must be
     identical). `perf stat -e instructions:u` would give a load-free count for
     the native half: on this machine you have passwordless sudo, so
     `sudo sysctl kernel.perf_event_paranoid=1` is possible, but it is a host
     setting: say you changed it and put it back.
   - If the result is a slowdown over 2 per cent, profile `ChipAccesses.Note` in
     the AOT build first (`PROFILE=<n>`, see the bench README). Options if it is
     the counter: count only while a model is mounted, or count in the lazy
     catch-up. Tell Dan before changing the machine.
   - Record it as the plan, the journal and the bench README ask: a new dated
     journal file (`order` one above the highest in `docs/journal/`, no digits in
     the title or summary), a dated row in the bench README's table, a dated
     'Done' line in the plan's Deferred list, and a comment on #77.
2. **Keep #75 and #76 current.** Merge `main` into each branch as it moves
   (`git fetch`, `git merge origin/main`), resolve conflicts, run the checks
   below, push. Reply to review comments. Do not rebase a pushed branch.
3. **#80, the browser check's flaky step.** Only if it fails on a quiet runner
   again. The page moved 19 px in the KIM-1's phone step in two whole runs made
   under load above 18, and passed in the two made below 8. It is in code this
   branch does not change.
4. **#78, #79, #81 are Dan's to call.** The recommendation is to leave #78 and
   #79 open: the page states each uncertainty. #81 is small notes and can be done
   when someone is in those files.

## Setting up the ARM machine

`docs/journal/2026-10-09-building-on-arm.md` (on PR #84, branch
`feat/arm-as65-under-qemu`, not yet on `main`) records that everything in this
repository built and tested on `dbhq-arm`. What the machine has now: Ubuntu
24.04 aarch64, Node 24, Python 3.12, `gh` logged in as `dandcg`, and
passwordless sudo. What it lacks: the .NET SDK, the Chromium that the browser
checks use, `pdfimages` and `cwebp`. Install, as that entry says:

- **.NET 10.0.400** with Microsoft's `dotnet-install.sh`, then
  `dotnet workload install wasm-tools --version 10.0.401` (the `linux-arm64`
  packs exist).
- **Chromium**: `npx playwright-core install chromium`, then set `CHROME_PATH`
  to the browser it downloads. Google publishes no Linux ARM Chrome, and the
  benches and `node scripts/browser-check.mjs` default to `/usr/bin/google-chrome`.
- **Dormann's tests** (the core's 12) fail on ARM with `Exec format error`,
  because `as65` is a 32-bit x86 program. Either merge #84's branch into yours
  locally to test, or install `qemu-user`, `libc6-i386-cross` and
  `libstdc++6-i386-cross`, or filter them out (`--filter "FullyQualifiedName!~Dormann"`)
  and say you did. They are not what #77 measures.
- **Only for re-running the offline tools** (`tools/nes-model/`, which the build
  never runs): `sudo apt-get install poppler-utils webp`, and a venv with numpy,
  opencv-python-headless, pillow, scipy and pytest. The tools' inputs are not in
  the repository; `tools/nes-model/README.md` says how to fetch each by hand into
  a folder named by `NES_MODEL_INPUTS`, and `verify.py` checks them against their
  recorded SHA-256. OpenTendo is read from the `dbhq-uk/OpenTendo` fork at
  `3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009`. You need none of this for #77.
- Run **one build at a time**. The repository's rules about a shared host are in
  the bench README: leave the machine otherwise idle, and note `uptime` before and
  after.

## The rules that were settled (do not reopen them without Dan)

Read `AGENTS.md` first: six constraints. In particular: no figure describing the
project as it stands is typed by hand (every figure comes from a test or a
recorded measurement with its command, dated); document as you go in
`docs/journal/`; no em or en dashes (CI fails on them); British English; stage
files by name. Dan's preferences: simple technical English, short sentences; he
wants solutions with a recommendation, not lists of problems; fix things unless
the fix is destructive or his to call; ask him one question at a time in a
question box, with the evidence and your recommendation in a message before the
box; never hard-wrap markdown in pull request bodies, issue bodies or comments.
Do not push to a shared branch other than your own, and never merge, without his
go.

Decisions taken, each with what it was chosen over (the plan and the journal
have the reasons):

- **Both consoles, both views** (Dan): NTSC and PAL, outside and inside. One
  module per view, drawing both consoles, with `regions` in `MODELS`; the
  registry does not change. Chosen over four modules, and over NTSC only.
- **The NES builds the shared two-view tabs, loader and legend table itself**
  (Dan, 6 October), to the BBC Micro models design, because that work was not
  built. The BBC Micro can adopt the same section later.
- **State** (Dan): the power light, POWER and RESET outside; chip marks with live
  access rates inside. The machine change that supports it is `ChipAccesses`.
- **The case checks were revised twice after figures were seen**, the second
  time because Dan said stopping was the wrong trade. Depth and height are judged
  on the design patent's drawings against the published ratios, pass within 5 per
  cent, stop over 8. They only catch a gross scale error. The case size is the
  published 254 by 203.2 by 88.9 mm, said on the page to be not Nintendo's and
  good to about 3 per cent. Both revisions and the old results are kept in the
  plan, the journal and `tools/nes-model/data/spike.json`. Do not tune them.
- **The copper map is traced to look at; its connections are not verified.** The
  nets check failed and is pinned as failed in a test. Every page sentence about
  the copper says so. The re-trace is #78, with a new check fixed before it runs.
  Do not tune a second trace against the old check.
- **The rear connectors' check failed as measured and was accepted.** The model
  uses the photograph's places (1.3 mm against the patent's rear view); the page
  states the board's 5.2 mm miss. #79.
- **The registry:** the NES row states `case: true` and claims outside then
  inside; a machine that claims a model states `case` (a rule added here).
- **No Nintendo logo shapes**; the console as made in grey tokens; heights are
  typical, and the page says so.
- **Process that worked:** each task was built by one agent and reviewed by
  another, with fix rounds, and every figure recorded with its command. If you
  use subagents, keep the same shape, and never let an implementer review its own
  work.

## Checks to run before pushing a change

On this machine, one at a time: `dotnet build -c Release 6502.slnx` (warnings are
errors); `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`; `cd site`, then
`node scripts/build-machines.mjs nes bbc-micro kim-1` and `npm test` (the build
needs `src/data/results.json`, which is git-ignored: take it from the latest CI
run's `results` artifact with `gh run download <run> -n results`, and delete it
before committing); and `node scripts/browser-check.mjs` with `CHROME_PATH` set.
The site suite's floor is in both `.github/workflows/validate.yml` and
`deploy-site.yml` and must be the same number in both, raised (never lowered)
when tests are added.

## What was not measured, said plainly

The speed of the chip counters on the machine as it now is (#77). The look of the
models on screen: the shared stage light tints the greys a little green, the case
geometry is simplified (square corners, a flat rear window set in by about 4.4 mm
on the console, ribs as lines), and the PAL rear words are squeezed onto one line;
the page says each, and no test checks them. The models in any browser but Chrome
with software WebGL, or on a real phone. A screen reader on both views.
