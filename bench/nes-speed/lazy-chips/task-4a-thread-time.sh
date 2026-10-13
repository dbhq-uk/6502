#!/bin/bash
# Task 4a of the scanline renderer work: the thread-time bench (bench/thread-time) on the scripted play of
# Lan Master (play-ntsc and play-pal), three builds alternated in four rounds: main, the build before task 4a,
# and this one. Each argument is a folder the bench was built into with
# `dotnet build bench/thread-time -c Release -o <folder>`, inside its own repository (main is an export of
# 5640a57 with bench/thread-time, bench/nes-speed/play and bench/nes-speed/lan-master-play.json copied in).
#
#   bench/nes-speed/lazy-chips/task-4a-thread-time.sh <main folder> <before folder> <after folder>
uptime; date -u
for round in 1 2 3 4; do
  for workload in play-ntsc play-pal; do
    for build in main before after; do
      case $build in main) dir=$1;; before) dir=$2;; *) dir=$3;; esac
      echo -n "round $round $build "
      dotnet "$dir/Dbhq.Machines.ThreadTime.dll" "$workload" 5
    done
  done
  uptime
done
date -u; uptime
