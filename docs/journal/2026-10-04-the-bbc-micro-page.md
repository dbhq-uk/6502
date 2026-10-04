---
title: "The BBC Micro's page"
date: 2026-10-04
summary: "The BBC Micro gets its page: a screen drawn from the machine's own picture, the keyboard by position, sound once asked for, a disc drive, and a Start button that says how much it downloads. It runs in a real browser and types BASIC through the page's own keys, but stays unlisted until its photograph is chosen and the registry says it runs."
order: 24
---

# 4 October 2026: the BBC Micro's page

Task 14a of the BBC Micro plan: the page, its host, its keyboard, sound and
disc drive, the browser check and CI. Task 14b follows with the photograph of a
real Model B, the registry switched to running and the home page's count going
up. Until then the registry still says planned, so the site that deploys has no
page for the BBC Micro and links none. What changes in production from this
task alone is that the BBC Micro's WebAssembly, ROMs and three scripts are
published, unlinked, and every page's inlined stylesheet carries the panel's
rules (measured below).

## What was built

- **The host,** `src/Dbhq.Machines.BbcMicro.Wasm/Program.cs`, grown from the
  speed bench's into the page's: `Load(os, basic, dfs, mode, sampleRate)`,
  `Run` and `Cycles` for the shared host, `KeyDown` and `KeyUp` by the key's
  internal number, `Break`, `Frames` and `Picture`, `Sound` and `DropSound`, and
  the disc drive: `InsertDisc`, `BlankDisc`, `SaveDisc`, `EjectDisc` and
  `SetDiscReadOnly`. The bench's own exports stay, and its `Load` call passes
  the two new arguments, which an older build ignores.
- **`BbcKeyPresses`** in the machine library: the page's keys, queued and played
  into the matrix at a pace the OS can read (below).
- **`BbcAcceptanceTests`**: the program the page shows, from
  `machines/bbc-micro/try-it.json`, typed in and read back off the picture,
  once through the keyboard matrix as the other BASIC tests type, and once
  through `BbcKeyPresses` with every key reported down and up at once, as a
  browser can. A third test types `PRINT 6*7` the same way, which is what the
  browser check types. The expected output, `HELLO 1` to `HELLO 5`, is worked out
  from BASIC's rule that a number after a semicolon is printed with no padding.
- **The page:** `site/src/components/BbcPanel.astro`, `site/public/bbc-micro.js`
  on the shared host, `site/public/bbc-keys.js` (the key table, data only),
  `site/public/bbc-audio.js` (the sound worklet), and sections in
  `site/src/pages/machines/[id].astro` for the program, the keyboard, what is not
  modelled and whose ROMs these are. The KIM-1's page is unchanged: built before
  and after, it is byte for byte the same apart from the inlined stylesheet.
- **The host's two new hooks,** asked for by the task 13 review:
  `machine-host.js` now calls `onPause` when the page is hidden (or `stop()` is
  called) and `onResume` when it comes back, never at the first start, and hands
  `load` the runtime as well as the machine class. Both have tests. The task 13
  entry said the host tells the page nothing when it pauses; it now does, and the
  BBC Micro's page suspends its sound there.

## Decisions, and what they were chosen over

**The picture is handed over, not returned.** A field is 640 by 512 RGBA, 1.3
MB, fifty times a second. Returning it from a `[JSExport]` as an array copies it
twice, into a new .NET array and then a new JavaScript one, and leaves 65 MB a
second of garbage. A pointer into the WebAssembly heap would copy once but needs
the framebuffer's private array pinned and unsafe code in the host. A
SharedArrayBuffer needs the page served cross-origin isolated (COOP and COEP
headers), which the site is not. Chosen: `Picture()` calls a JavaScript function
the page registers (`[JSImport("picture", "bbc-micro")]`) with a `Span<byte>`
marshalled as a MemoryView over the framebuffer itself, and the page copies it
once, straight into its canvas's `ImageData`, then puts it on the canvas. The
pixels are `0xAABBGGRR`, which in memory is R, G, B, A, the order `ImageData`
keeps, so nothing is converted. It is called only when `Frames` has moved.
Measured in headless Chrome: 0.1 ms a handover (50 in a row, timed in the page).

**The sound goes to an AudioWorklet in small posted chunks.** Each frame,
`Sound()` reads what the machine made into a scratch array and hands it over the
same way, as bytes, because a MemoryView can be bytes, ints or doubles but not
floats; the page copies it into a new `Float32Array` and posts it to the worklet,
transferring the buffer rather than copying it. Chosen over a SharedArrayBuffer
ring (the same isolation headers) and over a ScriptProcessorNode (deprecated,
and it runs on the main thread the machine runs on). The worklet waits for 40 ms
of sound before it plays, cuts a queue that grows past 0.15 s back to the newest
40 ms, and when it runs dry holds the last level, which its filter turns into
silence without a click. The machine's samples run from 0 to 1, so the filter, a
one-pole high-pass, takes the steady level off, as the coupling capacitor on the
real output does. The page asks the browser for an `AudioContext` at 48,000
samples a second, the rate the machine makes them at, so nothing is resampled
twice. Sound is off until the visitor clicks Turn sound on, because browsers do
not let a page start sound by itself; turning it on, or coming back to a hidden
page, throws away what was made meanwhile, so nothing plays late.

**Keys by place, through a queue.** `event.code` names a key by where it is, so
the BBC Micro's key in the same place goes down, and the symbols are where the
BBC has them: `=` is SHIFT and `-`, `*` is SHIFT and the key where a PC has `'`.
Chosen over mapping by `event.key`, which types what the PC's legend says but
needs a table of every layout, cannot press SHIFT the way the BBC's keyboard
does, and breaks the moment a program reads the keyboard directly. Where the
keyboards differ in shape, a PC key stands in by its job: Backspace for DELETE,
Enter for RETURN, End for COPY, Esc for ESCAPE, the arrows for the cursor keys.
The BBC's spare keys go where the PC has room: `_` on the key left of 1, `]` on
the key the browser calls Backslash, `\` on the extra key left of Z. The page's
table of where every symbol is comes from the OS ROM's own key table and this
map, so it cannot disagree with either.

Then the queue. A browser can report a key down and up in one frame, before the
machine runs a cycle, and the OS, reading the keyboard on its 100 Hz tick,
would never see it; a test pressing keys does exactly that. `BbcKeyPresses` holds
every key at least 40 ms and rests the same key 40 ms before it goes down again,
in machine time, the figures the tests' typing already used. It keeps the order,
so SHIFT pressed around a key is held across that key's whole hold. Chosen over
resting between every key, as the tests' typing does, which halves the speed of
a fast typist, and over timing the keys in JavaScript, which a slow frame
defeats. A mutation that took the pacing out failed five of the nine new tests.

**Tab leaves; Esc is ESCAPE.** The plan asked for Tab and Escape to behave as on
the KIM-1's page and its 3D model, where Escape lets go. Here Esc is the key that
stops a BASIC program, and a visitor reaches for it first, so it goes to the
machine. The page never keeps the keyboard because Tab is never taken: it moves
on, as on the KIM-1's page, and the BBC's own TAB key is left without a PC key.
The function keys are left to the browser too (F5 reloads), so f0 to f9 have no
PC key yet. With Ctrl the machine gets the key but the browser keeps its
shortcut, so Ctrl and R still reloads; with Alt or the Command key the machine
gets nothing. Focus leaving the screen lets go of every key held. Start gives the
screen focus, because pressing it was the visitor asking to use the machine.

**The disc: drive 0, and a note about the catalogue.** A `.ssd` or `.dsd`
dropped on the page or chosen with Insert a disc goes in drive 0; Make a blank
disc puts in an empty 80-track one; Save disc downloads the disc as it stands,
through a Blob URL and a link with `download`, which is a download and not a
fetch, so the CSP needs nothing for it. Nothing is uploaded. Task 12 found that
DFS keeps the last catalogue it read until the drive's head unloads, so a disc
changed sooner shows the old one. The plan allowed making the drive report not
ready on an insert only if the library already could; it cannot (the 8271 has a
not-ready latch, but only a command on an empty drive sets it), and adding a
drive door to the 8271 is a change to the chip whose effect on DFS nothing tests.
So the page says what happens, as the real machine does it: type `*CAT` after
changing discs. Drive 1 is in the model and not on the page, which says so.

**No CSP change.** `audioWorklet.addModule` loads a module script, and worklets
are governed by `script-src`, not `worker-src` (which covers workers), so `'self'`
already allows `/bbc-audio.js`. WebAssembly was already allowed for the KIM-1.
The browser check, with the site's real headers, saw no violation.

**A preview of the registry, for building the page before it counts.** The page
must be built and tested now, but the registry must not say running until the
photograph is chosen, or the deployed site would gain a half-finished page and
a count of two. Chosen: `loadRegistry` lays the fields of a file named by
`REGISTRY_PREVIEW` over the registry's records (`site/tests/fixtures/bbc-micro-running.json`,
which holds the record as task 14b will write it, with the KIM-1's photograph as
a stand-in that is never published). `tests/bbc-page.test.mjs` builds the site
that way into a temporary folder, checks the page, and then runs the whole site
suite against that build, so the home page, the machines table and every
site-wide rule are tested as they will be. Chosen over a build flag in the Astro
config (a second way to change what the site says) and over setting the record
running now. No workflow sets the variable, and a test fails if one does.

**Start says how much it downloads, from the build.** The page reads the size
of every file in `public/machines/bbc-micro/` at build time and the button says
"a download of up to" that, in megabytes; up to, because the edge may compress
them on the way. Nothing is fetched before the click, which the browser check
confirms.

**CI: a cache for each machine.** The one machines cache keyed on all of
`src/` became two, each keyed on the core, the machine's own two projects and
what the build reads, so a change to the BBC Micro rebuilds the BBC Micro alone,
and the build is given only the machines whose cache missed.

## What was measured

All on 4 October 2026, on this machine, with both machines built ahead of time.

- `node scripts/build-machines.mjs bbc-micro`: 112 s, and the rebuild's files are
  byte for byte the first build's (`sha256sum` before and after). The BBC Micro
  publishes 10 framework files, 7,385,829 bytes, and its three 16 KB ROMs. The
  KIM-1's, rebuilt in the same run, are the same 11 files and 7,170,402 bytes as
  task 13's.
- What Start downloads, from the browser check: 13 files, 7,434,981 bytes, every
  file in the folder. Compressed for comparison (Node's zlib, every file): 2,407,956
  bytes at gzip level 9 and 1,743,362 with brotli, against the KIM-1's 2,307,443
  and 1,661,441.
- `node scripts/page-weight.mjs /machines/bbc-micro/` on the preview build, before
  Start: 169,293 bytes, 114,036 gzipped, of which 87,100 is the stand-in
  photograph; the scripts are 39,938 bytes.
- Every page's inlined stylesheet grew by the panel's rules: the home page from
  33,658 to 35,358 bytes (8,924 to 9,169 gzipped), the KIM-1's from 57,544 to
  59,244 (13,270 to 13,513).
- `node scripts/browser-check.mjs`, both sections: passes, no console errors, no
  failed requests, no CSP violations, in 204 s. The BBC Micro was running 732 ms
  after Start and at the prompt 479 ms later; the page said "Running at the BBC
  Micro's own 2 MHz. This browser could run it about 9 times as fast." (capacity
  18.22 MHz). `PRINT 6*7` typed through the page showed 42; sound turned on and
  off; a disc put in through the file input listed its title and file with
  `*CAT`; Save disc downloaded the same 102,400 bytes; Break restarted it. The BBC
  section alone took 14 s, its preview build included.
- `node --test tests/*.test.mjs` in `site/`: 257 tests in 9 s, against 215 in 4 s
  before this task (the nested run of the whole suite on the preview build is most
  of the difference).
- `dotnet test -c Release`: Kim1 39, BbcMicro 941, Cpu6502 1597, all passing.

**What CI pays.** On a push that changes the BBC Micro, the machines job builds
it ahead of time, about two minutes here; that job runs beside the tests, which
take longer, so it should not lengthen the run. A push that changes neither
machine restores both from cache. The site job gains the preview build and its
nested suite, seconds, and the browser check gains its BBC section, about 15 s
here.

## Deferred

- **An on-screen keyboard,** for a phone or a tablet, which can start the machine
  but not type into it; it would also give TAB, SHIFT LOCK and the function keys
  a way in. The page says it is not there yet.
- **Drive 1.**
- **The screen for a screen reader.** The canvas is a picture; in mode 7 the text
  could be read from screen memory into a live region, as the KIM-1's digits are
  summarised.
- **Task 14b:** the photograph, the registry record (copied from the fixture, less
  its stand-in photograph), the count, and then the preview can go.
- Not done from the task 13 review: the untested meter reset in `resume`, the
  build script tests spawning with a fake `dotnet`, and the floor comment style,
  which this task's floor change follows.

## Mistakes

- The queue tests were written straight after the queue rather than before it.
  They were then checked against a queue with its pacing taken out: five of the
  nine new tests failed, so they do test it.
- My first wait in the browser check built its test with `new Function` inside
  the page. The page's CSP rightly refuses that, and the watcher would have
  reported it as a violation; it polls from Node instead.
- The check's first count of what loads before Start included the page itself,
  which lives at the machine folder's own address, and failed. The size test's
  first sum counted the built page's `index.html` in the same folder.
- The nested run of the suite came back in a tenth of a second with no count:
  `node --test` sets `NODE_TEST_CONTEXT` for the files it runs, and an inner
  `node --test` that inherits it reports to its parent instead of running. The
  test now drops it.
