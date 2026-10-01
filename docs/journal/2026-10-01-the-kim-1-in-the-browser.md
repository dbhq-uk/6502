---
title: "The KIM-1 in the browser"
date: 2026-10-01
summary: "The KIM-1 gets its page: the machine compiled to WebAssembly ahead of time, MOS Technology's monitor ROM fetched at build time, a drawn keypad and display, and a headless browser that types the acceptance test's program in through the page and reads the answer off the digits. It is the first machine to count."
order: 8
---

# 1 October 2026: the KIM-1 in the browser

The KIM-1 passed its acceptance test earlier today but did not count, because a
machine counts only when it also runs in the browser. This is that half: a page
at `/machines/kim-1/` with the six digits, the keypad, the RS and ST keys and the
SST switch, driven by the same machine code the tests run. With it in place the
registry entry is `running`, and the "machines implemented" figure on Home and
Status, which is counted from the registry, now includes it.

## What was built

- **`src/Dbhq.Machines.Kim1.Wasm/`**, a plain .NET WebAssembly app (not Blazor),
  the way `bench/Dbhq.Cpu6502.WasmBench` proved works. It exports seven calls to
  JavaScript: load the two ROM halves, tap a key, how many taps are waiting, set
  the SST switch, run for a number of cycles, the segments of one digit, and the
  digits as text.
- **`Kim1Keystrokes`** in the machine library: key taps, queued and played into
  the machine at a person's pace, each held 40 ms and rested 40 ms of machine
  time. The tests' `Kim1Session` now presses keys through it too, so the page and
  the acceptance test press keys through the same code.
- **`machines/kim-1/try-it.json`**: the program the page asks a visitor to type
  in, step by step, with the display each step should leave. The page renders
  it, a new acceptance test (`TheProgramOnTheMachinesWebPageRunsAsThePageSays`)
  runs it on the machine and checks every display, and the browser check clicks
  it in through the page. Change the file and all three change.
- **`site/scripts/build-machines.mjs`**: publishes the WebAssembly, copies its
  `_framework` folder into `site/public/machines/kim-1/`, and fetches the two ROM
  halves from the pins in `tests/Dbhq.Cpu6502.TestSupport/Pins.cs`, checking each
  SHA-256 (or uses the copy the tests already fetched, when its hash matches).
  That folder is git-ignored, so neither the WebAssembly nor the ROM is committed.
- **The page**: `site/src/components/Kim1Panel.astro` draws the machine and
  `site/public/kim-1.js` runs it. The page also shows the program, and says whose
  ROM it is in the registry's own words, with the pinned URLs and hashes read from
  `Pins.cs`.
- **`site/scripts/browser-check.mjs`**: serves the built site on `127.0.0.1` with
  the site's own response headers, opens the page in headless Chrome, clicks the
  program in through the page's own keypad, and reads the digits after every step.
- **CI**: a `machines` job in both workflows builds the WebAssembly and fetches
  the ROM, and the site job puts it where the Astro build copies it into `dist`
  before `npm test`. Validate then runs the browser check.

## Decisions

- **Compiled ahead of time, not the interpreter.** Measured below. The
  interpreter ran the KIM-1 at about two to two and a half times real speed on
  this machine, and at a quarter of the CPU (Chrome's CPU throttling, 4 times) it
  fell to about half real speed. AOT's capacity stayed above the board's clock
  at a sixth of the CPU in every sample, though only just in the busiest batch. The price is size and build time: the AOT framework folder is 7,170,304
  bytes against 3,069,607 for the interpreter, and the publish takes minutes
  rather than under one. A visitor on a slow laptop is the case that matters, and
  a page that cannot keep up with a 1 MHz board would be a poor first machine.
- **Key taps are queued and timed in machine cycles, not in milliseconds.**
  Chosen over passing mouse-down and mouse-up straight to the key matrix. A click
  can be over in a few milliseconds, which the monitor would miss, and clicks can
  come faster than the monitor reads them. Every key is a tap, because nothing on
  the KIM-1 needs a key held: the monitor waits for a key to come up before it
  takes another. Timing in machine cycles also means a slow browser still plays
  every key correctly, just later.
- **The queue lives in the machine library, not in the page's JavaScript,** so
  it has tests of its own (`Kim1KeystrokesTests`) and the acceptance tests use it.
- **The page's program is a data file both sides read,** chosen over the site
  parsing the C# test, which would break on a reformat. The existing acceptance
  tests are unchanged; the new one runs the file.
- **ROM delivered as two files, `6530-002.bin` and `6530-003.bin`,** each exactly
  the pinned file, chosen over joining them into one `rom.bin`, so each served
  file can be checked against its own pin with no arithmetic between.
- **The ROM is fetched by the site's build script from the pins in `Pins.cs`,**
  read by a regular expression in `site/src/lib/pins.mjs`, rather than written a
  second time in JavaScript. The build script fetches before it publishes, so a
  ROM that fails its hash stops the build in seconds.
- **No culture data** (`InvariantGlobalization`): nothing here formats text by
  culture, and the ICU files would have been the biggest download.
- **The display is the machine's own persistence model.** The page only draws
  what `Kim1Display` reports at the end of each frame, and that already keeps a
  digit lit for 20 ms after the monitor last scanned it. So the digits are
  steady while the monitor scans, and dark while a program runs, exactly as the
  tests read them. A second persistence model in JavaScript was not needed.
- **Each animation frame runs as many cycles as real time has passed,** at the
  clock in the registry entry (`6502, 1 MHz`), up to a tenth of a second's worth,
  so after a stall or a hidden tab the machine pauses rather than racing.
- **Digits drawn in SVG in the site's pale green,** not the board's red LEDs. Red
  is not in the palette, and this is a drawing of what the machine shows, not a
  photograph. Unlit segments stay faintly visible. `site/DESIGN.md` has the rest.
- **Keypad in the board's layout,** six rows of four: GO, ST, RS and the SST
  switch; AD, DA, PC and +; C to F; 8 to B; 4 to 7; 0 to 3. The User Manual's text
  (section 4.1) gives "a total of 23 keys and one slide switch" but its figure did
  not survive in the transcription, so the positions were checked against the
  key positions in MAME's KIM-1 layout file (facts read, nothing copied).
- **Real buttons, and the screen reader hears a summary.** Every key is a
  `<button>`; the SST switch is a checkbox with `role="switch"`; the drawing is
  hidden from screen readers and a polite live region reads out the display once
  it has held still for 400 ms, so it does not announce every key on the way.
  With focus on the machine, 0 to 9 and A to F on a keyboard press the same keys.
- **Without JavaScript** every key is disabled and the status line says the
  machine needs JavaScript. The program and the rights text are still there.
- **The CSP gains `'wasm-unsafe-eval'` in `script-src`, site-wide,** and nothing
  else. A browser compiles WebAssembly only when the policy allows it; this
  keyword allows that and nothing more, not `eval()` of JavaScript. The `.wasm`
  and ROM files are fetched from this site, which `connect-src 'self'` already
  allowed. Chosen over a policy for `/machines/kim-1/` alone, because Cloudflare
  Pages joins the headers of every rule a path matches, and a second policy on
  one path would narrow the first rather than widen it. The site tests now expect
  exactly `'self' 'wasm-unsafe-eval'` and the Google Tag Manager origin, and
  assert there is no `'unsafe-eval'` and no `'unsafe-inline'`.
- **The home page's line about which machines run is computed from the
  registry**: "None runs in the browser yet." for none, "Running in the browser
  now: KIM-1." for one, and a list for more. Its test checks all three on made-up
  registries, and the built page against the real one.
- **A running machine with no panel stops the build.** "Running" means it runs
  in the browser, so `[id].astro` refuses to publish a page that could not.
- **The browser check runs in Validate,** on GitHub's Ubuntu runner, which ships
  Google Chrome, chosen over keeping it a local check only. It does not depend on
  how fast the runner is: keys are timed in machine cycles, and each step waits
  for the key queue to empty and the digits to settle.
- **The built machine is cached in CI** under a key made of everything that goes
  into it (`src/`, `Directory.Build.props`, `Pins.cs`, the build script, the pins
  reader, and the pinned SDK and workload versions), so a change that touches only
  the site does not compile the machine again.
- **The SDK is pinned in the machines job (10.0.400) and the workload with it
  (`wasm-tools`, workload version 10.0.401).** A workload version belongs to one
  SDK feature band, so leaving the SDK at `10.0.x` would break the pin the day a
  new band is released. These are the versions on this machine
  (`dotnet --version`, `dotnet workload --version`). The test job keeps `10.0.x`.

## How fast it runs in the browser

Measured on 1 October 2026 with `node scripts/browser-check.mjs --throttle N`
from `site/`, which, after typing the program in, reads the page's own speed
report once a second for five seconds. "Capacity" is cycles run divided by the
time spent inside the machine's run call: how fast this browser could run it
flat out. "Running" is cycles run divided by real time. Chrome 153.0.8010.47,
headless, on the same machine as the browser speed check of 30 September: a
KVM virtual machine, 8 cores, shared with other sessions. That sharing is the
biggest source of spread below, so the load average (`uptime`) is given for each
batch.

AOT, 21:06 to 21:08 UTC, load average 3.92 at the start. The "running" figures
in this batch are not to be trusted: they came from the first version of the
speed report, which could not show a slow browser (see the mistakes below). The
capacity figures are unaffected.

```
== AOT throttle 1
speed, once a second for 5 s: running 1.00 MHz, capacity 18.05 MHz; running 1.00 MHz, capacity 19.19 MHz; running 1.00 MHz, capacity 18.80 MHz; running 1.00 MHz, capacity 19.07 MHz; running 1.00 MHz, capacity 19.23 MHz
== AOT throttle 4
speed, once a second for 5 s: running 1.00 MHz, capacity 4.27 MHz; running 1.00 MHz, capacity 4.40 MHz; running 1.00 MHz, capacity 4.64 MHz; running 1.00 MHz, capacity 4.63 MHz; running 1.00 MHz, capacity 4.86 MHz
== AOT throttle 6
speed, once a second for 5 s: running 1.00 MHz, capacity 3.45 MHz; running 1.00 MHz, capacity 3.32 MHz; running 1.00 MHz, capacity 2.94 MHz; running 1.00 MHz, capacity 3.14 MHz; running 1.00 MHz, capacity 3.66 MHz
```

Interpreter, a clean build, 21:31 UTC:

```
== interpreter, clean build, throttle 1
speed, once a second for 5 s: running 1.00 MHz, capacity 2.22 MHz; running 1.00 MHz, capacity 2.50 MHz; running 1.00 MHz, capacity 2.19 MHz; running 1.00 MHz, capacity 2.51 MHz; running 1.00 MHz, capacity 2.41 MHz
== interpreter, clean build, throttle 4
speed, once a second for 5 s: running 0.51 MHz, capacity 0.54 MHz; running 0.49 MHz, capacity 0.47 MHz; running 0.43 MHz, capacity 0.50 MHz; running 0.50 MHz, capacity 0.55 MHz; running 0.48 MHz, capacity 0.51 MHz
```

AOT again, five runs (three unthrottled), 21:37 to 21:41 UTC, load average
15.66 at the end, while other sessions were compiling:

```
== AOT, clean build, throttle 1
capacity 4.21, 10.67, 4.58, 5.55, 11.64 MHz; running 1.00, 0.93, 1.00, 1.00, 1.00 MHz
== AOT, clean build, throttle 1
capacity 9.86, 10.71, 11.88, 3.89, 9.98 MHz; running 1.00 MHz throughout
== AOT, clean build, throttle 1
capacity 9.51, 7.01, 12.41, 7.98, 13.38 MHz; running 1.00 MHz throughout
== AOT, clean build, throttle 4
capacity 3.75, 2.03, 2.25, 4.18, 2.39 MHz; running 1.00, 0.89, 1.00, 1.00, 0.98 MHz
== AOT, clean build, throttle 6
capacity 1.89, 1.62, 2.19, 1.13, 1.45 MHz; running 0.95, 1.00, 1.00, 0.91, 0.92 MHz
```

(That batch is shortened here to the figures; the lines were in the same form
as the others.) The same five, 21:47 to 21:51 UTC, load average 18.88 at the
start and 6.72 at the end:

```
throttle 1: capacity 15.17, 15.34, 17.97, 16.18, 10.15 MHz; running 1.00, 1.00, 0.96, 1.00, 1.00 MHz
throttle 1: capacity 18.25, 17.12, 11.49, 20.37, 13.34 MHz; running 1.00 MHz throughout
throttle 1: capacity 15.71, 14.61, 16.08, 15.71, 8.91 MHz; running 1.00 MHz throughout
throttle 4: capacity 2.74, 2.93, 3.87, 2.42, 5.24 MHz; running 1.00 MHz throughout
throttle 6: capacity 2.87, 1.75, 1.39, 2.01, 1.52 MHz; running 1.00, 1.00, 0.95, 0.97, 0.97 MHz
```

What that says, and no more: on this machine, quiet, AOT runs the KIM-1 about
eighteen to nineteen times as fast as the board and the interpreter about two
times. With other work on the machine, AOT's figures fall and spread a long way,
and at a sixth of the CPU it dropped below real speed in some seconds (0.91 at
the lowest). Chrome's throttling is an imitation of a slower computer, not a
measurement of one, and no phone or real slow laptop was tried. A visitor's
page reports its own figure in its speed line, so the page never claims a speed
it is not getting.

Compared with the browser speed check of 30 September, which ran a synthetic
program on a plain array: the interpreter's best there was 4.688 MHz and AOT's
50.968 MHz. The KIM-1 here is slower in both, which is expected (every cycle
now also clocks two 6530s and the display), but the two measurements are of
different programs and are not comparable cycle for cycle.

Build sizes and times, from the same day: `ls`/`du` on the published
`_framework` folder gave 7,170,304 bytes in 11 files for AOT and 3,069,607 for
the interpreter (the build script prints the total). The precompressed copies the
publish also writes, which are not shipped, came to 1.7 MB (AOT) and 924 KB
(interpreter) by `du -ch *.br`. `time` on the publish: AOT 2 min 59.7 s the first
time and 1 min 40 s to 2 min 25 s afterwards; the interpreter 1 min 1.6 s the
first time and 40.2 s afterwards. The machine was shared throughout.

## What the browser check showed

The last run before committing, at 21:52 UTC, `node scripts/browser-check.mjs`
from `site/`, against the AOT build:

```
browser 153.0.8010.47
loaded in 3388 ms: Running. Press RS to start the monitor.
step 1 ok   keys "[RS]" expected "xxxx xx" shown "0000 00" announced "Display 0000 00" (699 ms)
step 2 ok   keys "[AD] 17FA [DA] 00 [+] 1C" expected "17FB 1C" shown "17FB 1C" announced "Display 17FB 1C" (2411 ms)
step 3 ok   keys "[AD] 0200 [DA] AD [+] 10 [+] 02 [+] 18 [+] 6D [+] 11 [+] 02 [+] 8D [+] 12 [+] 02 [+] 4C [+] 0A [+] 02" expected "020C 02" shown "020C 02" announced "Display 020C 02" (5909 ms)
step 4 ok   keys "[AD] 0210 [DA] 27 [+] 15" expected "0211 15" shown "0211 15" announced "Display 0211 15" (1679 ms)
step 5 ok   keys "[AD] 0200 [GO]" expected "       " shown "       " announced "Display dark" (1228 ms)
step 6 ok   keys "[ST]" expected "020A 4C" shown "020A 4C" announced "Display 020A 4C" (577 ms)
step 7 ok   keys "[AD] 0212" expected "0212 3C" shown "0212 3C" announced "Display 0212 3C" (1013 ms)
status line: "Running. Press RS to start the monitor.", then "Running. Type the program below, or your own."
speed, once a second for 5 s: running 1.00 MHz, capacity 12.13 MHz; running 1.00 MHz, capacity 22.48 MHz; running 1.00 MHz, capacity 16.29 MHz; running 1.00 MHz, capacity 15.37 MHz; running 0.93 MHz, capacity 22.51 MHz
page speed line: Running at the board's own 1 MHz. This browser could run it about 22 times as fast.
no console errors, no failed requests, no CSP violations
```

The check bites. With `'wasm-unsafe-eval'` taken out of `_headers`, the same
command failed: `CSP violation: script-src blocked wasm-eval`, then
`WebAssembly.compileStreaming(): Compiling or instantiating WebAssembly module
violates the following Content Security policy directive`, and `the machine did
not start (state failed)`.

## The tests

On 1 October 2026, `dotnet test --configuration Release --logger trx` at the
root: 39 passed in the KIM-1 tests (35 before; the new ones are the page's
program in `Kim1AcceptanceTests` and three in `Kim1KeystrokesTests`) and 1,480 in
the core's. `node scripts/make-results.mjs` on that run: 1,519 passed in 19
suites. `cd site && npm test`: 141 passed, 0 failed (125 before).

Then `main` moved: the `/inside/` page (pull request 17) landed while this was
being built, and the pull request could not merge. `main` was merged into the
branch; the conflicts were the test floor in both workflows, the list of scripts
the deploy checks (both `chip.js` and `kim-1.js` now) and the end of
`global.css` (both sets of rules kept). After the merge, the same commands gave
1,481 passed in the core's tests, 39 in the KIM-1's, 1,520 in 20 suites from
`make-results.mjs`, and 156 site tests passed with none failed (`main` had 140,
this branch adds 16), so the floor in both workflows is 156.

Two deliberate breaks, each in a scratch edit and put back: changing the last
step's expected display in `try-it.json` to `0212 3D` failed
`TheProgramOnTheMachinesWebPageRunsAsThePageSays` with `after [AD] 0212: the page
promises "0212 3D", the display shows "0212 3C"`; and taking out the rest between
taps (the next key going down as soon as the last came up) failed seven tests,
six of them acceptance tests. The monitor really does miss keys without it.

## Surprises and mistakes

- **A stale build looked like a runtime bug, twice.** The first interpreter build
  failed in the browser with `MONO interpreter: NIY encountered in method
  <Module>:.cctor ()`. I took it for a problem with `InvariantGlobalization`,
  because a build with culture data on worked, and measured that build instead.
  Then an AOT build that had worked an hour earlier failed with
  `aot-runtime.c:2146`. Both were the same mistake: interpreter, AOT and
  culture-data builds had all been published from the same `obj/` folder, and the
  publish reused output from a different kind of build. A clean interpreter build
  with `InvariantGlobalization` runs fine. `build-machines.mjs` now deletes the
  project's `obj/Release` and `bin/Release` before it publishes. CI always
  started clean, so it was never at risk; a local build was.
- **The page's first speed line could not report a slow browser.** It measured
  the running speed against the frame time after capping it at a tenth of a
  second, so a browser that could not keep up still read "running 1.00 MHz"
  (the interpreter at a quarter of the CPU showed capacity 0.58 MHz and running
  1.00 MHz). It measures against the real time now; the rerun showed running
  0.55 to 0.61 MHz.
- **"About 1 times as fast"** was the first wording at a sixth of the CPU. One is
  now worded as keeping up "with little to spare".
- **The status line said "Press RS"** after RS had been pressed. It changes once
  the monitor first lights the digits, and the browser check now fails if it
  does not.
- **The family document said the KIM-1 has "a 24-key keypad".** The manual says
  23 keys and one slide switch. Corrected while placing the keys.
- **`dotnet test` at the root does not build the WebAssembly project.** It builds
  test projects and what they reference, so the host is compiled only by its
  publish, in the machines job, where warnings are errors as everywhere else.

## Not done

- No phone or real slow laptop was tried; the slower-computer figures are
  Chrome's CPU throttling.
- The CI timings of the new `machines` job are not in this entry: they are in the
  pull request's checks, and the first run also downloads the core's test data
  again (the earlier commit changed `Pins.cs`).
- The ROM still has one download source, the Internet Archive. The CI cache
  holds the built machine, ROM included, between runs, so an Archive outage
  stops only a run that changes the machine.
- The shared browser host is still not extracted. The design says to do that
  when the second machine needs it.
