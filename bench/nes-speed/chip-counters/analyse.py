"""Reads a launches log and applies the rule fixed beforehand (issue 77).

python3 analyse.py <launches.txt>
Per launch: the median of its eight `thread` lines' times_real. Per pair: after / baseline.
Per region: the median of the ratios and a 90 per cent percentile bootstrap interval of it
(10,000 resamples, seed 77).
"""
import random, re, statistics, sys

text = open(sys.argv[1]).read()
launches = {}
for block in text.split('=== launch ')[1:]:
    label = block.split()[0]
    load = re.search(r'loadavg=([\d.]+)', block).group(1)
    end = re.search(r'--- exit=(\d+) end=\S+ loadavg=([\d.]+)', block)
    tr = [float(x) for x in re.findall(r'thread \d+ cycles=\d+ thread_ms=[\d.]+ mhz=[\d.]+ times_real=([\d.]+)', block)]
    launches[label] = dict(load=float(load), end_load=float(end.group(2)) if end else None,
                           exit=int(end.group(1)) if end else None, tr=tr,
                           med=statistics.median(tr) if len(tr) == 8 else None)

def boot(ratios, n=10000, seed=77):
    rng = random.Random(seed)
    meds = sorted(statistics.median(rng.choices(ratios, k=len(ratios))) for _ in range(n))
    return meds[int(0.05 * n)], meds[int(0.95 * n) - 1]

verdicts = []
for region, name in (('0', 'NTSC'), ('1', 'PAL')):
    pairs = sorted({int(m) for m in re.findall(rf'r{region}-p(\d+)-', ' '.join(launches))})
    ratios = []; done = []
    print(f'{name}: pair, baseline median, after median, ratio, load before (base, after), load at end (base, after)')
    for p in pairs:
        b, a = launches.get(f'r{region}-p{p}-base'), launches.get(f'r{region}-p{p}-after')
        if not b or not a or b['med'] is None or a['med'] is None:
            print(f'  {p}: incomplete'); continue
        r = a['med'] / b['med']; ratios.append(r); done.append(p)
        print(f"  {p}: {b['med']:.3f} {a['med']:.3f} {r:.4f}  ({b['load']:.2f}, {a['load']:.2f})  ({b['end_load']:.2f}, {a['end_load']:.2f})")
    if not ratios: continue
    med = statistics.median(ratios); lo, hi = boot(ratios)
    bm = statistics.median(launches[f'r{region}-p{p}-base']['med'] for p in done)
    am = statistics.median(launches[f'r{region}-p{p}-after']['med'] for p in done)
    v = 'pass' if med >= 0.98 and lo >= 0.96 else 'fail' if hi < 0.98 else 'inconclusive'
    verdicts.append(v)
    print(f'  n={len(ratios)} median ratio {med:.4f}, 90% interval {lo:.4f} to {hi:.4f}, range {min(ratios):.4f} to {max(ratios):.4f}')
    print(f'  median of launch medians: baseline {bm:.3f}, after {am:.3f} times real time; region verdict {v}')
if len(verdicts) == 2:
    print('VERDICT:', 'FAIL' if 'fail' in verdicts else 'PASS' if verdicts == ['pass', 'pass'] else 'INCONCLUSIVE')
