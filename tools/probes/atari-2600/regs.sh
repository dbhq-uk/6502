#!/bin/sh
# For each suite .asm given, count code lines (comments stripped) that name
# each group of registers, to check which machine parts a test touches.
for f in "$@"; do
  code=$(sed 's/;.*//' "$f")
  c() { echo "$code" | grep -ciwE "$1"; }
  echo "$f hmove=$(c 'HMOVE') hm=$(c 'HMP0|HMP1|HMM0|HMM1|HMBL|HMCLR') rsync=$(c RSYNC) vdel=$(c 'VDELP0|VDELP1|VDELBL') inpt=$(c 'INPT[0-5]') riotio=$(c 'SWCHA|SWCHB|SWACNT|SWBCNT') timer=$(c 'TIM1T|TIM8T|TIM64T|T1024T|INTIM|TIMINT') cx=$(c 'CX[A-Z0-9]+') res=$(c 'RESP0|RESP1|RESM0|RESM1|RESBL|RESMP0|RESMP1') pf=$(c 'PF0|PF1|PF2') clean_start=$(c CLEAN_START)"
done
