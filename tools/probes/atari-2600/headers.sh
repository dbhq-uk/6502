#!/bin/sh
# Print the leading comment block (header) of each suite .asm file given.
for f in "$@"; do
  echo "=== $f"
  awk 'BEGIN{h=1} h && /^[ \t]*;/ {print; next} h && /^[ \t]*$/ {print ""; next} {h=0}' "$f"
done
