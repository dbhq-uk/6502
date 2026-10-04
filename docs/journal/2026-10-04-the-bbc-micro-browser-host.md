---
title: "One browser host for every machine page"
date: 2026-10-04
summary: "The part of the KIM-1's page that loads the machine, keeps it in step with real time and says how fast the browser runs it moves into a host every machine page shares, ready for the BBC Micro. The KIM-1 now runs on it, and the real-browser check passes as it did before. The build script publishes a machine by name, and the KIM-1's published files are byte for byte the same."
order: 23
---

# 4 October 2026: one browser host for every machine page

Task 13 of the BBC Micro plan. Nothing new runs yet: this is the KIM-1's page
taken apart, so that the BBC Micro's page (task 14) can be built on the half
that is not the KIM-1's.

## What moved, and what stayed

`site/public/kim-1.js` did two jobs. One was the KIM-1's own: the keypad, the
six digits, the keyboard, the SST switch, the screen-reader summary, and the
hooks the 3D model uses (`panel.kim1`, the `kim1:key` and `kim1:ready`
events). The other was any machine's: load the .NET runtime, ask it for the
machine, run the machine each animation frame for the real time since the last
frame, at most a tenth of a second's worth, and once a second write the
browser's capacity and actual speed on the panel and the headroom sentence
under it.

The second job is now `site/public/machine-host.js`, one exported function:

```js
startMachine({ panel, base, name, assembly, hostClass, load, clockMhz, clockOf, onFrame, say, speedEl })
```

It imports `base + '_framework/dotnet.js'`, creates the runtime, asks for
`assembly`, takes the class `hostClass` from it, calls `load` with that class,
and then runs the loop, calling `onFrame(host, cycles, now)` after each frame's
run. It resolves to `{ host, stop }`, or to `null` after saying that the
machine could not start, and why ("The KIM-1 could not start: ..."). The contract is written out at the top of the
file. The loop's arithmetic, the cap, the two speed figures and the three
sentences were moved, not rewritten: the lines are the KIM-1's, with the
machine's names taken out.

`kim-1.js` keeps everything that is the KIM-1's, and its draw-the-digits code
became its `onFrame`. It imports the host as `/machine-host.js`, the way
`machines-table.js` imports `/table-sort.js`.

## What the BBC Micro will need from it, and where the brief's interface grew

The plan gave the interface as `{ panel, base, assembly, hostClass, load,
clockMhz, onFrame, say, speedEl }`. Three things were added or settled:

- **`name`.** The failed-load sentence names the machine ("The KIM-1 could not
  start: ..."), and nothing else in the list carries the name. Chosen over
  building the sentence in each machine's file, which would have left the
  failure path outside the host and outside its test.
- **`clockOf`**, whose clock the sentences name, "the board's" unless given.
  The KIM-1's sentences had to stay word for word ("Running at the board's own
  1 MHz"), and the BBC Micro may want "the BBC Micro's". Chosen over a sentence
  template per machine, which would let two pages drift apart in wording.
- **What `onFrame` is given.** The BBC Micro must drain its picture and its
  sound for exactly the machine time that passed. That is the cycles the
  machine really ran, `Run`'s return less the last count, not the budget: `Run`
  finishes its last instruction, so it can pass the budget by a few cycles. The
  host passes the real figure, with the frame's time stamp as a third argument,
  which the KIM-1's screen-reader summary uses to wait for the digits to settle.

The budget is counted in the machine's own CPU clock. At the BBC Micro's 2 MHz
the cap is 200,000 cycles a frame, and `Run` takes an `int`, so there is no
overflow to worry about: the unit test checks the 2 MHz cap and that it is a
whole number.

`load` may be async. The KIM-1 used to fetch its two ROM halves in the same
`Promise.all` as the runtime; to keep that, `kim-1.js` starts the fetch before
calling the host and `load` awaits it. A `.catch(() => {})` on the fetch only
marks it handled for the case where the runtime fails first and the ROM is
never awaited, so the browser reports no unhandled rejection; `load` still sees
the failure. One small difference: when both the runtime and the ROM fail, the
message now names the runtime's failure, where before it named whichever failed
first.

## One deliberate change to the KIM-1: a hidden page is paused outright

The brief asks for the loop to pause when the tab is hidden. The KIM-1's page
paused already, but only because browsers stop sending animation frames to a
hidden tab. What it did not do was forget the time away: the first frame back
was capped at a tenth of a second of machine time, correctly, but the real time
away went into the once-a-second speed reading, so the first reading after a
minute in another tab put the actual speed far under 1 MHz. Only the
`data-actual-mhz` figure showed it; the sentence uses the actual speed only
when the browser cannot keep up.

The host now listens for `visibilitychange`: hidden cancels the waiting frame;
shown starts a fresh frame and a fresh one-second reading from that moment.
Chosen over leaving the KIM-1's behaviour exactly as it was, because the BBC
Micro will be making sound, and a page that only stops because the browser
stops calling it is a page whose sound buffer runs dry without being told.
Chosen over leaving out the frames longer than the cap from the reading
instead, because that would also leave out a browser that genuinely cannot
keep up, which is the one thing the reading is for. A page that starts hidden
(opened in a background tab) does not start the loop until it is shown.

## How it was shown not to have changed the KIM-1

Every check in this section was run on 4 October 2026, on this machine (8
cores, shared with other work).

**The real-browser check, before and after.** Before any file under
`site/public/` changed, the KIM-1 was published ahead of time with the old
script, the site built, and `node scripts/browser-check.mjs` run. Then the same
after the change. Both ended "no console errors, no failed requests, no CSP
violations", with every step of the try-it program ok and the same status
lines. With the timings taken out, the two logs differ only in:

- the speed: capacity 18.32 to 20.88 MHz before and 17.76 to 20.37 MHz after,
  so the page said "about 20 times as fast" before and "about 18 times" after.
  Run to run noise on a loaded machine, and both read "running 1.00 MHz" every
  second;
- pixel-level drag and render figures in the 3D model's checks (a right-drag
  ended at 11.817 before and 11.821 after, a variance of 1602.5 against
  1600.4), noise from software WebGL;
- the digits after the page's 1 key: "2121 34" before, "2121 02" after. In
  address mode the right two digits show the byte at `$2121`, where the KIM-1
  has no memory, so they show whatever was last on the bus. The journal entry
  for 2 October records "2121 00" and "2121 02" from runs on the same page, so
  this was varying before the change.

**The machine page's tests, and one test that followed the move.**
`site/tests/machine-page.test.mjs` checked, by regex on `kim-1.js`, that it
imports `` `${base}_framework/dotnet.js` `` and calls
`getAssemblyExports('Dbhq.Machines.Kim1.Wasm')`. Both lines moved, by design:
the import is the host's now, with the base handed over, and the assembly name
is handed over as `assembly: 'Dbhq.Machines.Kim1.Wasm'`. The test now checks
the same two things in the files that do them: that `kim-1.js` imports the host
and passes it `panel` and `base`, that the host imports
`` `${base}_framework/dotnet.js` ``, that `kim-1.js` names the assembly and
`Kim1Host`, and that the host asks for `getAssemblyExports(assembly)`. The ROM
check is unchanged and still reads `kim-1.js`. The message on its WebAssembly
test, "the loader kim-1.js imports", now says `machine-host.js`. No assertion
was dropped or loosened. `browser-check.mjs` was not touched, and neither was
`model.test.mjs`, whose checks of `panel.kim1`, `kim1:key`, `kim1:ready` and
`kim.Tap` read `kim-1.js` and still pass on it.

**The new unit test**, `site/tests/machine-host.test.mjs`, runs the host
against a fake runtime module (written to a temporary folder as
`_framework/dotnet.js`) and a fake machine that runs at a set speed, in a fake
browser whose clock and frames the test turns by hand. The first version of the
slow-browser test failed, and the test was wrong: it read the first second,
which includes the climb from a standing start to the cap, and so saw 0.6 MHz
for a browser that runs the machine at 0.5. It now reads the second second,
when every frame is at the cap.

## The build script publishes a machine by name

`node scripts/build-machines.mjs kim-1`, or `bbc-micro`, or both. The machines
it knows, their WebAssembly projects and their ROMs are `MACHINE_BUILDS` in
`site/src/lib/machines.mjs`, beside a `readRom` that reads a ROM from `roms/`
and checks it against its pin. `site/src/lib/pins.mjs` gained `bbcRoms()`,
beside `kim1Roms()`: the three files `BbcOsPath`, `BbcBasicPath` and
`BbcDfsPath` name in `Pins.cs`, in the order `BbcHost.Load` takes them, with
their pinned hashes. Every ROM of every machine named is read and checked
before the first publish, as the KIM-1's was.

The id is now required, rather than defaulting to the KIM-1, chosen so that
nothing quietly builds one machine when two exist. CI, `npm run machines` and
the README pass `kim-1`.

**The KIM-1's published files are byte for byte the same.** The old script's
output and the new script's, each a clean ahead-of-time build:

```
node scripts/build-machines.mjs        # before: 2 min 18 s
node scripts/build-machines.mjs kim-1  # after:  2 min 31 s
find public/machines -type f -exec sha256sum {} +
```

Thirteen files each (eleven in `_framework`, two ROM halves), the same names,
sizes and SHA-256 hashes. Both printed "11 framework files, 7170402 bytes,
compiled ahead of time; 6530-002.bin and 6530-003.bin".

The BBC Micro publishes too. `node scripts/build-machines.mjs bbc-micro` took
2 min 15 s ahead of time and wrote eleven files to `_framework` and the three
16 KB ROMs, `os.rom`, `BASIC.ROM` and `DFS-1.2.rom`, whose SHA-256 hashes are
the three pins. That output was then deleted again, so that the site built and
tested here is the one CI builds. No page loads it yet.

## What it means for CI

`validate.yml` and `deploy-site.yml` now run `node scripts/build-machines.mjs
kim-1`, and publish exactly what they did: the KIM-1 alone. The BBC Micro's
publish is added to CI in task 14, with its page. The cache of published
machines is keyed on `site/src/lib/machines.mjs` as well now, since the build
table lives there. The deploy's list of scripts that must be serving gains
`machine-host.js`: `site/tests/build.test.mjs` derives that list from the
built pages and the scripts they import, so it would fail a workflow that left
the host out. A 404 on the host and the KIM-1 never starts.

The site's test floor in both workflows goes up to the new total. On 4 October
2026, `cd site && npm run build && npm test` reported 215 tests, 215 passing:
the 193 of 2 October, fifteen in `machine-host.test.mjs` and seven in
`build-machines.test.mjs`. Both floors are 215.
