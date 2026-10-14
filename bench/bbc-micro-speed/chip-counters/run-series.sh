#!/usr/bin/env bash
# Runs the launches that rule.md fixes, one at a time, and writes them where analyse.py reads them.
# It applies the rule; it does not change it. The gate's limit, the launch line, the order of a
# pair and what counts are all rule.md's.
#
#   run-series.sh smoke <after-folder> <out-dir>
#       the one smoke launch of the after build in mode 7, kept in the log and not counted
#   run-series.sh pairs <base-folder> <after-folder> <out-dir> <first-pair> <last-pair>
#       pairs <first> to <last> of mode 7, then the same pairs of dense
#
# Run from bench/bbc-micro-speed (the harness, after `npm ci`). Folders are published builds.
# It writes, in <out-dir>:
#   launches.txt  every launch's whole output, between rule.md's `=== launch` and `--- exit` lines
#   watch.txt     the load average and the four busiest processes every 15 seconds
#   reruns.txt    every pair run again because a launch in it did not count
#
# The gate: before every launch the one-minute load average must be under 1.00; if not, it waits,
# polling every 15 seconds. GATE_MAX_SECONDS (default 1800) is how long one gate may wait before
# the series stops with an error: the rule says wait, and this only stops a wait that would never
# end. LOADAVG_FILE (default /proc/loadavg) is read for the load; it is there so the script can be
# tried without Chrome or a quiet host, and is never set for a real run.
set -euo pipefail

LOADAVG_FILE=${LOADAVG_FILE:-/proc/loadavg}
GATE_MAX_SECONDS=${GATE_MAX_SECONDS:-1800}
GATE_LIMIT=1.00
POLL_SECONDS=15
TRIES=3

now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
load() { cat "$LOADAVG_FILE"; }
busiest() { top -b -n1 -w 200 | awk 'NR > 7 && NR <= 11 { printf "%s %s;", $9, $12 }'; }

gate() {
  local waited=0 one
  while :; do
    one=$(cut -d' ' -f1 "$LOADAVG_FILE")
    if awk -v l="$one" -v limit="$GATE_LIMIT" 'BEGIN { exit !(l < limit) }'; then
      return 0
    fi
    if ((waited >= GATE_MAX_SECONDS)); then
      echo "gate: the one-minute load average was still $one after ${waited}s; the series stops" >&2
      return 1
    fi
    echo "gate: one-minute load average $one, not under $GATE_LIMIT; waiting" >&2
    sleep "$POLL_SECONDS"
    waited=$((waited + POLL_SECONDS))
  done
}

watch_start() {
  (
    while :; do
      echo "$(date -u +%H:%M:%SZ) $(cut -d' ' -f1-3 "$LOADAVG_FILE") | $(busiest)"
      sleep "$POLL_SECONDS"
    done
  ) >>"$out/watch.txt" 2>&1 &
  watcher=$!
  trap 'kill "$watcher" 2>/dev/null || true' EXIT
}

# One launch: gate, header, the whole output, footer. Returns 0 if it counts by rule.md.
launch() {
  local label=$1 folder=$2 screen=$3 output code threads
  # An explicit exit: launch is called on the left of ||, where set -e does not stop the shell.
  gate || exit 1
  output=$(mktemp)
  {
    echo "=== launch $label folder=$folder screen=$screen start=$(now) loadavg=$(load)"
    echo "--- top before: $(busiest)"
  } >>"$out/launches.txt"
  set +e
  THREAD_TIME=8 node run-in-browser.mjs "$folder" 1 2000000 6000000 5 7 "$screen" none >"$output" 2>&1
  code=$?
  set -e
  cat "$output" >>"$out/launches.txt"
  echo "--- exit=$code end=$(now) loadavg=$(load)" >>"$out/launches.txt"
  threads=$(grep -cE '^launch 1 thread [0-9]+ cycles=[0-9]+ thread_ms=[0-9.]+ mhz=[0-9.]+' "$output" || true)
  local counts=yes
  [[ $code == 0 ]] || counts=no
  grep -q '^launch 1 prompt yes' "$output" || counts=no
  [[ $screen != dense ]] || grep -q '^launch 1 screen dense$' "$output" || counts=no
  [[ $threads == 8 ]] || counts=no
  rm -f "$output"
  echo "$label exit=$code threads=$threads counts=$counts"
  [[ $counts == yes ]]
}

command=${1:-}
case $command in
  smoke)
    [[ $# == 3 ]] || { echo "usage: $0 smoke <after-folder> <out-dir>" >&2; exit 2; }
    after=$2 out=$3
    mkdir -p "$out"
    watch_start
    # The smoke launch checks the harness on this host. If it does not count, every launch of the
    # series would fail the same way, so the series does not start; launches.txt says why.
    launch smoke "$after" boot || { echo "the smoke launch did not count: the harness does not run on this host" >&2; exit 1; }
    ;;
  pairs)
    [[ $# == 6 ]] || { echo "usage: $0 pairs <base-folder> <after-folder> <out-dir> <first-pair> <last-pair>" >&2; exit 2; }
    base=$2 after=$3 out=$4 first=$5 last=$6
    mkdir -p "$out"
    watch_start
    for case in mode7 dense; do
      screen=boot
      [[ $case == mode7 ]] || screen=dense
      for ((p = first; p <= last; p++)); do
        # Odd pairs launch the baseline first and even pairs the after build, so drift in the
        # host cannot favour one build.
        if ((p % 2 == 1)); then order=(base after); else order=(after base); fi
        for ((try = 1; try <= TRIES; try++)); do
          ok=yes
          for side in "${order[@]}"; do
            folder=$base
            [[ $side == base ]] || folder=$after
            launch "$case-p$p-$side" "$folder" "$screen" || ok=no
          done
          [[ $ok == no ]] || break
          echo "$(now) $case pair $p, try $try: a launch did not count; the pair is run again" | tee -a "$out/reruns.txt"
        done
      done
    done
    ;;
  *)
    sed -n '2,22p' "$0" >&2
    exit 2
    ;;
esac
