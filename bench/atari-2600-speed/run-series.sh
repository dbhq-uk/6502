#!/usr/bin/env bash
# Runs the four launches that rule.md fixes, one at a time, and writes them where analyse.py reads
# them. It applies the rule; it does not change it. The gate's limit, the launch line, the order
# and what counts are all rule.md's.
#
#   run-series.sh <aot-folder> <interpreter-folder> <out-dir>
#
# Run from bench/atari-2600-speed (the harness, after `npm ci`). The folders are published builds.
# It writes, in <out-dir>:
#   launches.txt  every launch's whole output, between rule.md's `=== launch` and `--- exit` lines
#   watch.txt     the load average and the four busiest processes every 15 seconds
#   reruns.txt    every launch run again because it did not count
#
# The gate: before every launch the one-minute load average must be under 1.00; if not, it waits,
# polling every 15 seconds. GATE_MAX_SECONDS (default 1800) is how long one gate may wait before
# the series stops with an error. LOADAVG_FILE (default /proc/loadavg) is read for the load; it is
# there so the script can be tried without a quiet host, and is never set for a run that counts.
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

# One launch: gate, header, the whole output, footer. Returns 0 if it counts by rule.md, which
# analyse.py --counts decides, so what counts is written in one place.
launch() {
  local label=$1 folder=$2 region=$3 output code
  # An explicit exit: launch is called on the left of ||, where set -e does not stop the shell.
  gate || exit 1
  output=$(mktemp)
  {
    echo "=== launch $label folder=$folder start=$(now) loadavg=$(load)"
    echo "--- top before: $(busiest)"
  } >>"$out/launches.txt"
  set +e
  node run-in-browser.mjs "$folder" 1 "$region" 5 60 >"$output" 2>&1
  code=$?
  set -e
  cat "$output" >>"$out/launches.txt"
  echo "--- exit=$code end=$(now) loadavg=$(load)" >>"$out/launches.txt"
  local counts=yes why
  why=$(python3 analyse.py --counts "$output" "$code" "$region") || counts=no
  rm -f "$output"
  echo "$label exit=$code counts=$counts${why:+ ($why)}"
  [[ $counts == yes ]]
}

[[ $# == 3 ]] || { sed -n '2,17p' "$0" >&2; exit 2; }
aot=$1 interpreter=$2 out=$3
mkdir -p "$out"
(
  while :; do
    echo "$(date -u +%H:%M:%SZ) $(cut -d' ' -f1-3 "$LOADAVG_FILE") | $(busiest)"
    sleep "$POLL_SECONDS"
  done
) >>"$out/watch.txt" 2>&1 &
watcher=$!
trap 'kill "$watcher" 2>/dev/null || true' EXIT

for build in aot interpreter; do
  folder=$aot
  [[ $build == aot ]] || folder=$interpreter
  for region in NTSC PAL; do
    label="$build-${region,,}"
    for ((try = 1; try <= TRIES; try++)); do
      if launch "$label" "$folder" "$region"; then
        break
      fi
      echo "$(now) $label, try $try: the launch did not count" | tee -a "$out/reruns.txt"
    done
  done
done
