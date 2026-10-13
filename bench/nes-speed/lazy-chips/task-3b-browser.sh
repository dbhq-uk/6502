#!/bin/bash
# Task 3b of the scanline renderer work: the browser AOT figure for Lan Master played by script, a baseline
# build against this one, alternated. A set is NTSC then PAL, four rounds each of one fresh launch of the
# baseline then one of this build, each the page's five timed runs and eight more timed in the CPU time of
# the page's main thread, of 5.37 million cycles (3 seconds of machine time, about 180 frames) each after 6
# million cycles of boot, which is the title, the menu and the start of the first level; then the BBC Micro
# gauge. Each argument is a folder published with `dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release
# -p:RunAOTCompilation=true -o <folder>`; the gauge is bench/bbc-micro-speed's publish/aot-gauge. The log
# goes to task-3-browser-medians.py, which reads "set <name> region <n>" lines.
#
#   bench/nes-speed/lazy-chips/task-3b-browser.sh <base published> <after published>
HERE=$(cd "$(dirname "$0")" && pwd)
H=$HERE/..
G=$HERE/../../bbc-micro-speed
export ROM=homebrew PLAY=1
for region in 0 1; do
  echo "set play region $region"; uptime; date -u
  for round in 1 2 3 4; do
    for build in base after; do
      echo "round $round build $build"
      if [ "$build" = base ]; then dir=$1; else dir=$2; fi
      (cd "$H" && THREAD_TIME=8 node run-in-browser.mjs "$dir" 1 5370000 6000000 5 "$region")
    done
  done
  echo "gauge"; uptime; date -u
  (cd "$G" && ROM= PLAY= THREAD_TIME=10 node run-in-browser.mjs publish/aot-gauge 1 2000000 6000000 1)
done
uptime; date -u
