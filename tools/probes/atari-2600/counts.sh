#!/bin/sh
# Count the suite's tests by folder and kind. A test with a _ntsc.png beside it
# is a screenshot test; every other .asm is a self-test. Run from the suite clone.
printf '%-12s %6s %10s %6s\n' folder self-test screenshot total
for d in roms/*/; do
  n=$(ls "$d"*.asm | wc -l)
  s=$(ls "$d"*_ntsc.png 2>/dev/null | wc -l)
  printf '%-12s %6s %10s %6s\n' "$(basename "$d")" $((n - s)) "$s" "$n"
done
printf '%-12s %6s %10s %6s\n' all $(( $(ls roms/*/*.asm | wc -l) - $(ls roms/*/*_ntsc.png | wc -l) )) $(ls roms/*/*_ntsc.png | wc -l) $(ls roms/*/*.asm | wc -l)
