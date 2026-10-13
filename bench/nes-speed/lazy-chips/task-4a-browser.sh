#!/bin/bash
# Task 4a of the scanline renderer work: the browser AOT figure for Lan Master played by script (as task
# 3b), three builds alternated: main, the build before task 4a, and this one. A set is NTSC then PAL, four
# rounds each of one fresh launch of each build in turn, each the page's five timed runs and eight more
# timed in the CPU time of the page's main thread, of 5.37 million cycles after 6 million of boot; then the
# BBC Micro gauge. Each argument is a folder published with `dotnet publish src/Dbhq.Machines.Nes.Wasm -c
# Release -p:RunAOTCompilation=true -o <folder>`. The log's "round <n> build <name>" lines are what
# task-4a-medians.py reads.
#
#   bench/nes-speed/lazy-chips/task-4a-browser.sh <main published> <before published> <after published>
HERE=$(cd "$(dirname "$0")" && pwd)
H=$HERE/..
G=$HERE/../../bbc-micro-speed
export ROM=homebrew PLAY=1
for region in 0 1; do
  echo "set play region $region"; uptime; date -u
  for round in 1 2 3 4; do
    for build in main before after; do
      echo "round $round build $build"
      case $build in main) dir=$1;; before) dir=$2;; *) dir=$3;; esac
      (cd "$H" && THREAD_TIME=8 node run-in-browser.mjs "$dir" 1 5370000 6000000 5 "$region")
    done
  done
  echo "gauge"; uptime; date -u
  (cd "$G" && ROM= PLAY= THREAD_TIME=10 node run-in-browser.mjs publish/aot-gauge 1 2000000 6000000 1)
done
uptime; date -u
