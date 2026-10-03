#!/usr/bin/env bash
# Runs two or more builds of the BBC Micro speed check in turn, a launch of each at a time,
# so drift in the shared machine's load falls on all of them, then prints each build's median.
# Prints the load average and the time before and after, and every timed line.
#
#   ./alternate.sh browser <launches> <published-folder>...
#       one headless Chrome launch of five timed runs per build, in turn (run-in-browser.mjs)
#   ./alternate.sh native <launches> <native-build-folder>...
#       one launch of twelve timed runs per build, in turn, keeping the last ten
#
# Each launch also times its boot, power on to the prompt (6 million cycles), and the summary
# gives each build's median boot time beside its median speed.
#       (a folder holding a build of native/, Dbhq.Machines.BbcMicro.SpeedNative.dll)
#
# Run from this folder. Folders are relative to it; a native build must sit inside the
# repository, because the bench finds the ROMs by looking for 6502.slnx above itself.
# MODE=n in the environment boots every build in screen mode n (default 7); a build from
# before task 8 has no mode, so give it MODE only when it was published with one.
set -euo pipefail
cd "$(dirname "$0")"

mode=$1
screen=${MODE:-7}
launches=$2
shift 2
lines=$(mktemp)
trap 'rm -f "$lines"' EXIT

echo "load before: $(cut -d' ' -f1-3 /proc/loadavg)  $(date -u +%H:%M:%S) UTC  screen mode $screen"
for ((i = 1; i <= launches; i++)); do
  for build in "$@"; do
    if [[ $mode == browser ]]; then
      output=$(node run-in-browser.mjs "$build" 1 2000000 6000000 5 "$screen")
      grep -q '^launch 1 prompt yes' <<<"$output" || { echo "no prompt from $build" >&2; echo "$output" >&2; exit 1; }
      grep -E ' (timed|boot) ' <<<"$output" | sed "s|^|$build |" | tee -a "$lines"
    else
      output=$(dotnet "$build/Dbhq.Machines.BbcMicro.SpeedNative.dll" 12 2000000 "$screen")
      grep -q '^prompt yes' <<<"$output" || { echo "no prompt from $build" >&2; echo "$output" >&2; exit 1; }
      grep '^boot ' <<<"$output" | sed "s|^|$build |" | tee -a "$lines"
      grep '^timed ' <<<"$output" | tail -n 10 | sed "s|^|$build |" | tee -a "$lines"
    fi
  done
done
echo "load after: $(cut -d' ' -f1-3 /proc/loadavg)  $(date -u +%H:%M:%S) UTC"

python3 - "$lines" <<'PY'
import collections, re, statistics, sys
runs = collections.defaultdict(list)
boots = collections.defaultdict(list)
for line in open(sys.argv[1]):
    build = line.split()[0]
    if " boot " in line:
        boots[build].append(float(re.search(r"ms=([\d.]+)", line).group(1)))
    else:
        runs[build].append(float(re.search(r"mhz=([\d.]+)", line).group(1)))
for build, mhz in runs.items():
    mhz.sort()
    median = statistics.median(mhz)
    print(f"{build}: runs={len(mhz)} median={median:.3f} MHz ({median / 2:.2f} times 2 MHz) "
          f"best={mhz[-1]:.3f} slowest={mhz[0]:.3f}")
# The boot, power on to the prompt, is timed on its own: a slow start (task 8's review found
# one) would never show in the timed runs, which start at the prompt.
for build, ms in boots.items():
    ms.sort()
    print(f"{build}: boots={len(ms)} boot median={statistics.median(ms):.1f} ms fastest={ms[0]:.1f} slowest={ms[-1]:.1f}")
PY
