# Task 3 of the scanline renderer work: the medians of a task-3-browser.sh log, by set (ROM and region) and build:
# the main thread CPU time runs (thread), the page's wall-clock runs, the median of each launch, and the BBC gauge.
#   python3 bench/nes-speed/lazy-chips/task-3-browser-medians.py <log>
import re,collections,statistics,sys
th=collections.defaultdict(list); wall=collections.defaultdict(list); launch=collections.defaultdict(list); gauge=[]
cur=None; build=None; per=[]
for l in open(sys.argv[1]):
    m=re.match(r'^set (\S+) region (\d)',l)
    if m: cur=(m.group(1),'NTSC' if m.group(2)=='0' else 'PAL'); continue
    m=re.match(r'^round \d+ build (\w+)',l)
    if m:
        if per: launch[(cur,build)].append(statistics.median(per))
        build=m.group(1); per=[]; continue
    if l.startswith('gauge'):
        if per: launch[(cur,build)].append(statistics.median(per)); per=[]
        build='gauge'; continue
    m=re.search(r'thread \d+ .* mhz=(\S+)( times_real=(\S+))?',l)
    if m:
        if build=='gauge': gauge.append(float(m.group(1)))
        else: th[(cur,build)].append(float(m.group(3))); per.append(float(m.group(3)))
        continue
    m=re.search(r'timed \d+ .* times_real=(\S+)',l)
    if m and build!='gauge': wall[(cur,build)].append(float(m.group(1)))
if per and build!='gauge': launch[(cur,build)].append(statistics.median(per))
for key in sorted({k[0] for k in th}):
    b=statistics.median(th[(key,'base')]); a=statistics.median(th[(key,'after')])
    wb=statistics.median(wall[(key,'base')]); wa=statistics.median(wall[(key,'after')])
    print(f"{key[0]} {key[1]}: thread median base {b:.2f} after {a:.2f} ratio {a/b:.3f} (n={len(th[(key,'base')])},{len(th[(key,'after')])}); wall base {wb:.2f} after {wa:.2f} ratio {wa/wb:.3f}; launch medians base {[round(x,2) for x in launch[(key,'base')]]} after {[round(x,2) for x in launch[(key,'after')]]}")
print('gauge medians of ten (MHz):', end=' ')
for i in range(0,len(gauge),10): print(round(statistics.median(gauge[i:i+10]),2), end=' ')
print()
