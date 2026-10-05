#!/usr/bin/env bash
# Runs two or more builds of the Electron speed check in turn, a launch of each at a time, so
# drift in the shared machine's load falls on all of them, then prints each build's median in
# each mode. The shape of ../bbc-micro-speed/alternate.sh, for this bench's page and modes.
# Prints the load average and the time before and after, and every timed line.
#
#   ./alternate.sh <launches> <published-folder>...
#
# Each launch is one headless Chrome launch of a build (run-in-browser.mjs): power on, four
# million cycles to the prompt, then five timed runs of two million cycles in each mode of
# MODES (default 6,0). Run from this folder; folders are relative to it or absolute.
set -euo pipefail
cd "$(dirname "$0")"

modes=${MODES:-6,0}
launches=$1
shift
lines=$(mktemp)
trap 'rm -f "$lines"' EXIT

echo "load before: $(cut -d' ' -f1-3 /proc/loadavg)  $(date -u +%H:%M:%S) UTC  modes $modes"
for ((i = 1; i <= launches; i++)); do
  for build in "$@"; do
    output=$(node run-in-browser.mjs "$build" 1 2000000 4000000 5 "$modes")
    grep -q '^launch 1 prompt yes' <<<"$output" || { echo "no prompt from $build" >&2; echo "$output" >&2; exit 1; }
    if grep -q ' held NO' <<<"$output"; then echo "$build did not hold its mode" >&2; exit 1; fi
    grep -E ' timed ' <<<"$output" | sed "s|^|$build |" | tee -a "$lines"
  done
done
echo "load after: $(cut -d' ' -f1-3 /proc/loadavg)  $(date -u +%H:%M:%S) UTC"

python3 - "$lines" <<'PY'
import collections, re, statistics, sys
runs = collections.defaultdict(list)
for line in open(sys.argv[1]):
    build = line.split()[0]
    mode = re.search(r" mode (\d) timed ", line).group(1)
    runs[(build, mode)].append(float(re.search(r"mhz=([\d.]+)", line).group(1)))
for (build, mode), mhz in runs.items():
    mhz.sort()
    median = statistics.median(mhz)
    print(f"{build} mode {mode}: runs={len(mhz)} median={median:.3f} MHz ({median / 2:.2f} times 2 MHz) "
          f"best={mhz[-1]:.3f} slowest={mhz[0]:.3f}")
PY
