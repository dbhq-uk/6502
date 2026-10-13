#!/bin/bash
# Task 3 of the scanline renderer work: the thread-time bench (bench/thread-time) alternated in
# four rounds, a baseline build against this one, and optionally a third build ("off": this tree
# with Ppu.WholeLines false by default). Each argument is a folder the bench was built into with
# `dotnet build bench/thread-time -c Release -o <folder>`, inside its own repository.
#
#   bench/nes-speed/lazy-chips/task-3-thread-time.sh <base folder> <after folder> [off folder]
BASE=$1
AFTER=$2
OFF=$3
uptime; date -u
for round in 1 2 3 4; do
  if [ -z "$OFF" ]; then
    set -- "base ntsc" "after ntsc" "after ntsc-oracle" "base pal" "after pal" "after pal-oracle" \
           "base lan-ntsc" "after lan-ntsc" "base lan-pal" "after lan-pal"
  else
    set -- "base ntsc" "off ntsc" "after ntsc" "base pal" "off pal" "after pal" "base lan-ntsc" "off lan-ntsc" "after lan-ntsc"
  fi
  for bw in "$@"; do
    read build workload <<< "$bw"
    case $build in base) dir=$BASE;; off) dir=$OFF;; *) dir=$AFTER;; esac
    echo -n "round $round $build "
    dotnet "$dir/Dbhq.Machines.ThreadTime.dll" "$workload" 5
  done
  uptime
done
date -u; uptime
