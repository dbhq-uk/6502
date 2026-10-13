"""Reads the variants' launches log and applies the rule fixed beforehand (issue 77, second run).

python3 analyse_variants.py <variants.txt>
Per launch: the median of its eight `thread` lines' times_real. Per round: each build over the
round's base launch. Per build and region: the median of the ratios and a 90 per cent percentile
bootstrap interval of it (10,000 resamples, seed 77).
"""
import random, re, statistics, sys

text = open(sys.argv[1]).read()
med = {}
for block in text.split('=== launch ')[1:]:
    label = block.split()[0]
    tr = [float(x) for x in re.findall(r'thread \d+ cycles=\d+ thread_ms=[\d.]+ mhz=[\d.]+ times_real=([\d.]+)', block)]
    med[label] = statistics.median(tr) if len(tr) == 8 else None
    if len(tr) != 8:
        print(f'{label}: {len(tr)} thread lines, not counted')

def boot(xs, n=10000, seed=77):
    rng = random.Random(seed)
    ms = sorted(statistics.median(rng.choices(xs, k=len(xs))) for _ in range(n))
    return ms[int(0.05 * n)], ms[int(0.95 * n) - 1]

verdict = {}
for region, name in (('0', 'NTSC'), ('1', 'PAL')):
    rounds = sorted({int(m) for m in re.findall(rf'r{region}-p(\d+)-', ' '.join(med))})
    print(f'{name}: round, base, cur, A, B (times real time)')
    for i in rounds:
        print(f'  {i}: ' + ' '.join(f"{med.get(f'r{region}-p{i}-{b}') or float('nan'):.3f}" for b in ('base', 'cur', 'A', 'B')))
    for b in ('cur', 'A', 'B'):
        rs = [med[f'r{region}-p{i}-{b}'] / med[f'r{region}-p{i}-base'] for i in rounds
              if med.get(f'r{region}-p{i}-{b}') and med.get(f'r{region}-p{i}-base')]
        if not rs:
            continue
        m = statistics.median(rs); lo, hi = boot(rs)
        v = 'pass' if m >= 0.98 and lo >= 0.96 else 'fail' if hi < 0.98 else 'inconclusive'
        verdict.setdefault(b, []).append(v)
        print(f'  {b}: n={len(rs)} median ratio {m:.4f}, 90% interval {lo:.4f} to {hi:.4f}, range {min(rs):.4f} to {max(rs):.4f}: {v}')
for b, vs in verdict.items():
    if len(vs) == 2:
        print(f'VERDICT {b}:', 'FAIL' if 'fail' in vs else 'PASS' if vs == ['pass', 'pass'] else 'INCONCLUSIVE')
