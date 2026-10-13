# Task 3b of the scanline renderer work: the medians of task-3b-thread-time.sh logs. For each workload and build,
# the median of the rounds' medians (ns a cycle) over all the logs given, the ratio of the builds (base over after:
# above 1 means this build is faster), and each log's own.
#   python3 bench/nes-speed/lazy-chips/task-3b-thread-time-medians.py <log>...
import re, statistics, sys, collections
per = collections.defaultdict(list)
perlog = collections.defaultdict(lambda: collections.defaultdict(list))
for path in sys.argv[1:]:
    for line in open(path):
        m = re.match(r'round \d+ (base|after) (\S+) ns/cycle best=\S+ median=(\S+)', line)
        if m:
            per[(m.group(2), m.group(1))].append(float(m.group(3)))
            perlog[path][(m.group(2), m.group(1))].append(float(m.group(3)))
for workload in sorted({k[0] for k in per}):
    b = statistics.median(per[(workload, 'base')]); a = statistics.median(per[(workload, 'after')])
    own = []
    for path in sys.argv[1:]:
        pb = statistics.median(perlog[path][(workload, 'base')]); pa = statistics.median(perlog[path][(workload, 'after')])
        own.append(f'{pb:.1f}/{pa:.1f}={pb / pa:.2f}')
    print(f'{workload}: base {b:.1f} ns/cycle, this build {a:.1f}, base over this {b / a:.3f} (n={len(per[(workload, "base")])},{len(per[(workload, "after")])}); each log base/after=ratio: {" ".join(own)}')
