#!/bin/bash
# Task 3b of the scanline renderer work: the thread-time bench (bench/thread-time) on the scripted play of
# Lan Master (play-ntsc and play-pal), alternated in four rounds, a baseline build against this one. Each
# argument is a folder the bench was built into with `dotnet build bench/thread-time -c Release -o <folder>`,
# inside its own repository (the baseline is an export of 5640a57 with bench/thread-time, bench/nes-speed/play
# and bench/nes-speed/lan-master-play.json copied in).
#
#   bench/nes-speed/lazy-chips/task-3b-thread-time.sh <base folder> <after folder>
BASE=$1
AFTER=$2
uptime; date -u
for round in 1 2 3 4; do
  for bw in "base play-ntsc" "after play-ntsc" "base play-pal" "after play-pal"; do
    read build workload <<< "$bw"
    if [ "$build" = base ]; then dir=$BASE; else dir=$AFTER; fi
    echo -n "round $round $build "
    dotnet "$dir/Dbhq.Machines.ThreadTime.dll" "$workload" 5
  done
  uptime
done
date -u; uptime
