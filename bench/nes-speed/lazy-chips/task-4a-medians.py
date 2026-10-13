# Task 4a of the scanline renderer work: the medians of task-4a-browser.sh and task-4a-thread-time.sh logs.
# Browser: times_real of the runs in the main thread's CPU time, by region and build, and the ratios this
# build over main and over the build before, overall and by log (set). Native: ns a cycle, the median of each
# build's round medians, as times real (1e9 / (ns * CpuHz)), and the same ratios.
#   python3 bench/nes-speed/lazy-chips/task-4a-medians.py browser <log> [<log> ...]
#   python3 bench/nes-speed/lazy-chips/task-4a-medians.py native <log> [<log> ...]
#   python3 bench/nes-speed/lazy-chips/task-4a-medians.py wall <log> [<log> ...]
# 'wall' is the browser's wall-clock figure: times_real of the page's five timed runs a launch, by region and build.
# 'native' and 'browser' also print the ns a cycle each figure is (1e9 / (times_real * CpuHz)).
# A log is one set, or several split at '## Set' lines (as task-4a-browser.txt and task-4a-thread-time.txt are).
import collections, os, re, statistics, sys

# The CPU clocks, read from Region.cs (cpuHz: ...), never typed here.
_region = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'src', 'Dbhq.Machines.Nes', 'Region.cs')).read()
CPU_HZ = {}
for name, expr in re.findall(r'name: "(NTSC|PAL)",[\s\S]*?cpuHz: ([0-9_. /]+),', _region):
    CPU_HZ[name] = eval(expr.replace('_', ''), {'__builtins__': {}})
mode, logs = sys.argv[1], sys.argv[2:]
pooled = collections.defaultdict(list)
per_set = []
gauge = []
def sets():
    # Each log is a set, or several, split at its '## Set' lines.
    for log in logs:
        lines = []
        for line in open(log):
            if line.startswith('## Set') and lines:
                yield lines
                lines = []
            lines.append(line)
        yield lines

for lines in sets():
    runs = collections.defaultdict(list)
    region = build = None
    in_gauge = False
    for line in lines:
        if mode in ('browser', 'wall'):
            m = re.match(r'^set play region (\d)', line)
            if m:
                region = 'NTSC' if m.group(1) == '0' else 'PAL'
                in_gauge = False
                continue
            m = re.match(r'^round \d+ build (\w+)', line)
            if m:
                build = m.group(1)
                continue
            if line.startswith('gauge'):
                in_gauge = True
                continue
            if mode == 'wall':
                m = re.search(r'timed \d+ .* times_real=(\S+)', line)
                if m and not in_gauge:
                    runs[(region, build)].append(float(m.group(1)))
                continue
            m = re.search(r'thread \d+ .* mhz=(\S+)( times_real=(\S+))?', line)
            if m:
                if in_gauge:
                    gauge.append(float(m.group(1)))
                elif m.group(3):
                    runs[(region, build)].append(float(m.group(3)))
        else:
            m = re.match(r'round \d+ (\w+) play-(ntsc|pal) ns/cycle best=\S+ median=(\S+)', line)
            if m:
                region = m.group(2).upper()
                runs[(region, m.group(1))].append(1e9 / (float(m.group(3)) * CPU_HZ[region]))
    if runs:
        per_set.append(runs)
    for k, v in runs.items():
        pooled[k] += v

def med(v):
    return statistics.median(v) if v else float('nan')

for region in ('NTSC', 'PAL'):
    main, before, after = (med(pooled[(region, b)]) for b in ('main', 'before', 'after'))
    sets_main = ', '.join(f"{med(s[(region, 'after')]) / med(s[(region, 'main')]):.2f}" for s in per_set)
    sets_before = ', '.join(f"{med(s[(region, 'after')]) / med(s[(region, 'before')]):.2f}" for s in per_set)
    ns = ' '.join(f'{b} {1e9 / (v * CPU_HZ[region]):.1f}' for b, v in (('main', main), ('before', before), ('after', after)))
    print(f"{region}: ns a cycle {ns}; times real main {main:.2f} before {before:.2f} after {after:.2f}; after/main {after / main:.2f} (sets {sets_main}); after/before {after / before:.2f} (sets {sets_before}); n={len(pooled[(region, 'after')])}")
if gauge:
    print('gauge medians of ten (MHz):', ' '.join(f'{med(gauge[i:i + 10]):.1f}' for i in range(0, len(gauge), 10)))
