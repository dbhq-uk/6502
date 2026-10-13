// The part of a machine page that every machine shares: it loads the machine
// (.NET WebAssembly), runs it in step with real time, and says how fast this
// browser runs it. Each machine's own file (kim-1.js, bbc-micro.js) draws the
// machine, takes its keys and calls startMachine. An external module because the site's CSP
// allows no inline script.
//
// THE CONTRACT
//
//   const started = await startMachine({ panel, base, name, assembly, hostClass,
//     load, clockMhz, clockOf, onFrame, onPause, onResume, say, speedEl });
//
//   panel      The machine's element. The host writes two figures on it, once a
//              second: panel.dataset.capacityMhz, how fast this browser runs the
//              machine flat out (cycles over the time spent inside Run), and
//              panel.dataset.actualMhz, how fast it is running it (cycles over
//              the real time that passed), each in MHz to two places.
//   base       Where the machine's files are, ending in a slash. The runtime is
//              base + '_framework/dotnet.js', as the publish lays it out.
//   name       The machine's name, for the sentence when it cannot start.
//   assembly   The WebAssembly assembly to ask for, for example
//              'Dbhq.Machines.Kim1.Wasm'.
//   hostClass  The [JSExport] class in it that is the machine, for example
//              'Kim1Host'. It must have Cycles(), the cycles it has run, and
//              Run(cycles), which runs at least that many (cycles is an int)
//              and returns Cycles(). The host takes each frame's cycles as the
//              difference between two readings, so the count must never go
//              back: a machine whose power cycle sets its bus's count to 0
//              keeps a running total across it, as NesHost.Cycles does. For
//              the KIM-1 and the BBC Micro it is the cycles since power on.
//   load       Called as load(host, runtime) once the runtime is up, to give the
//              machine its ROMs and switch it on: host is that class, and
//              runtime the .NET runtime's API, for a machine that hands the
//              page its picture or its sound through functions the page
//              registers with runtime.setModuleImports (the BBC Micro's do).
//              It may be async; its errors are the host's failed load. The
//              machine does not run until it returns.
//   clockMhz   The machine's CPU clock, which every budget is counted in: 1 for
//              the KIM-1, 2 for the BBC Micro. Or a function that returns it,
//              read every frame and every report, for a machine whose clock
//              changes as it runs (the NES's, with its region).
//   clockOf    Whose clock the sentences name; "the board's" unless given.
//   onFrame    Called after every frame's run as onFrame(host, cycles, now):
//              cycles is what the machine really ran this frame (it can pass
//              the budget by part of an instruction), so a machine can drain its
//              picture and its sound for exactly that much time; now is the
//              frame's time stamp. Optional.
//   onPause    Called when the loop stops running the machine: the page was
//              hidden, or stop() was called. onResume is called when a hidden
//              page is shown again and the loop carries on. Neither is called
//              when the machine first starts. A machine with sound stops and
//              starts it here, so a hidden tab falls silent. Both optional.
//   say        say(text, state): the page's status line. The host calls it only
//              when the machine cannot start: "The <name> could not start: <the
//              reason>", with state "failed".
//   speedEl    Where the headroom sentence goes, once a second.
//
//   It resolves to { host, stop, hold }, host being the class above, or to null
//   when the machine could not start. It resolves before the first frame runs,
//   so whatever the caller does after its await (wiring keys, saying it is
//   running) is done before onFrame is first called. stop() ends the loop for
//   good. hold(true) stops running the machine until hold(false), as a hidden
//   page does (onPause and onResume are called), and a hidden page shown again
//   while held stays held: the NES's test hook holds the loop while it runs the
//   machine to an exact frame.
//
// TIMING. Each animation frame runs as many machine cycles as real time has
// passed since the last one, at clockMhz, up to MAX_FRAME_MS of machine time:
// after a stall the machine loses the time rather than racing to catch up. At
// the BBC Micro's 2 MHz that cap is 200,000 cycles, well inside the int that
// Run takes. A hidden page is paused outright: its frame is cancelled, and when
// the page is shown again the machine carries on from then and the speed
// figures start a fresh second, so the time away is neither run nor counted
// against the browser.
//
// THE SENTENCES, for a clock of N MHz and a browser that runs the machine M
// times as fast as that, M rounded down:
//   M of 2 or more: "Running at the board's own N MHz. This browser could run it about M times as fast."
//   M of 1:         "Running at the board's own N MHz, with little to spare in this browser."
//   M of 0:         "Running at A MHz, slower than the board's N MHz: this browser cannot keep up."
// where A is the actual speed. "the board's" is clockOf.

export const MAX_FRAME_MS = 100;

export async function startMachine({ panel, base, name, assembly, hostClass, load, clockMhz, clockOf = "the board's", onFrame, onPause, onResume, say, speedEl }) {
  const clock = typeof clockMhz === 'function' ? clockMhz : () => clockMhz;
  let host;
  try {
    const { dotnet } = await import(`${base}_framework/dotnet.js`);
    const runtime = await dotnet.create();
    const exports = await runtime.getAssemblyExports(assembly);
    host = exports?.[hostClass];
    if (!host) throw new Error(`${assembly} exports no ${hostClass}`);
    await load(host, runtime);
  } catch (error) {
    say(`The ${name} could not start: ${error.message}`, 'failed');
    return null;
  }

  let last = 0;
  let cycles = host.Cycles();
  let busyMs = 0;
  let busyCycles = 0;
  let wallMs = 0;
  let wallCycles = 0;
  let pending = null;
  let stopped = false;
  // True once the loop has stopped running the machine, so onResume follows only an onPause.
  let paused = false;
  // True while the page holds the machine (hold), so showing a hidden page does not run it.
  let held = false;

  const frame = (now) => {
    pending = null;
    const real = now - last;
    const elapsed = Math.min(real, MAX_FRAME_MS);
    last = now;
    const want = Math.max(1, Math.round(elapsed * clock() * 1000));
    const before = performance.now();
    const after = host.Run(want);
    busyMs += performance.now() - before;
    const ran = after - cycles;
    busyCycles += ran;
    wallMs += real;
    wallCycles += ran;
    cycles = after;

    onFrame?.(host, ran, now);

    if (wallMs >= 1000) {
      report(busyCycles / busyMs / 1000, wallCycles / wallMs / 1000);
      busyMs = busyCycles = wallMs = wallCycles = 0;
    }
    if (!stopped && !held && !document.hidden) pending = requestAnimationFrame(frame);
  };

  // capacity: how fast this browser runs the machine flat out, in MHz, from
  // the time spent inside Run. actual: how fast it is running it, against the
  // real time that passed, so a browser that cannot keep up reports less than
  // the machine's clock.
  const report = (capacity, actual) => {
    const clockMhz = clock();
    panel.dataset.capacityMhz = capacity.toFixed(2);
    panel.dataset.actualMhz = actual.toFixed(2);
    const times = Math.floor(capacity / clockMhz);
    speedEl.textContent = times >= 2
      ? `Running at ${clockOf} own ${fmt(clockMhz)} MHz. This browser could run it about ${fmt(times)} times as fast.`
      : times === 1
        ? `Running at ${clockOf} own ${fmt(clockMhz)} MHz, with little to spare in this browser.`
        : `Running at ${actual.toFixed(2)} MHz, slower than ${clockOf} ${fmt(clockMhz)} MHz: this browser cannot keep up.`;
  };

  // From now: the time a hidden page was away is not run, and not counted.
  const resume = () => {
    last = performance.now();
    // The machine may have run outside the loop while it was paused (the NES's
    // test hook runs it to an exact frame while held): those cycles are not
    // the next frame's, nor counted in the speed.
    cycles = host.Cycles();
    busyMs = busyCycles = wallMs = wallCycles = 0;
    if (pending === null) pending = requestAnimationFrame(frame);
    if (paused) {
      paused = false;
      onResume?.();
    }
  };
  const pause = () => {
    if (pending !== null) cancelAnimationFrame(pending);
    pending = null;
    if (!paused) {
      paused = true;
      onPause?.();
    }
  };
  const visibility = () => {
    if (stopped) return;
    if (document.hidden) pause();
    else if (!held) resume();
  };
  const hold = (on) => {
    if (stopped) return;
    held = on;
    if (on) pause();
    else if (!document.hidden) resume();
  };
  document.addEventListener('visibilitychange', visibility);

  const stop = () => {
    stopped = true;
    pause();
    document.removeEventListener('visibilitychange', visibility);
  };

  if (!document.hidden) resume();
  return { host, stop, hold };
}

const fmt = (n) => n.toLocaleString('en-GB');
