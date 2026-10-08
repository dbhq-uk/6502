#!/bin/bash
# Task 3 of the scanline renderer work: the browser AOT figure, a baseline build against this one,
# alternated. A set is SNOW and the homebrew (ROM=homebrew), NTSC then PAL, four rounds each of
# one fresh launch of the baseline then one of this build, each the page's five timed runs and
# eight more timed in the CPU time of the page's main thread; then the BBC Micro gauge. Each
# argument is a folder published with `dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release
# -p:RunAOTCompilation=true -o <folder>`; the gauge is bench/bbc-micro-speed's publish/aot-gauge.
#
#   bench/nes-speed/lazy-chips/task-3-browser.sh <base published> <after published>
HERE=$(cd "$(dirname "$0")" && pwd)
H=$HERE/..
G=$HERE/../../bbc-micro-speed
for set in "snow 0" "snow 1" "homebrew 0" "homebrew 1"; do
  read rom region <<< "$set"
  echo "set $rom region $region"; uptime; date -u
  for round in 1 2 3 4; do
    for build in base after; do
      echo "round $round build $build"
      if [ "$build" = base ]; then dir=$1; else dir=$2; fi
      if [ "$rom" = homebrew ]; then export ROM=homebrew; else unset ROM; fi
      (cd "$H" && THREAD_TIME=8 node run-in-browser.mjs "$dir" 1 1790000 5000000 5 "$region")
    done
  done
  unset ROM
  echo "gauge"; uptime; date -u
  (cd "$G" && THREAD_TIME=10 node run-in-browser.mjs publish/aot-gauge 1 2000000 6000000 1)
done
uptime; date -u
