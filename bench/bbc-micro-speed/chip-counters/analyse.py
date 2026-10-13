"""Reads a launches file and applies the rule fixed before the first launch (rule.md).

python3 analyse.py <launches.txt>

Per launch: the median of its eight `thread` lines' mhz, if it printed its prompt (and, for
dense, its page). Per pair: after / baseline. Per case: the median of the ratios and a 90 per
cent percentile bootstrap interval of that median (10,000 resamples, seed 88).
"""
import random
import re
import statistics
import sys

CASES = (('mode7', 'mode 7'), ('dense', 'dense'))


def repeated(text):
    """The labels that head more than one launch, once each, in order. read() keeps the last
    launch under a label; a repeat means a pair was run again, and the earlier launch is not
    in the maths, so the run says so."""
    seen, again = set(), []
    for block in text.split('=== launch ')[1:]:
        label = block.split()[0]
        if label in seen and label not in again:
            again.append(label)
        seen.add(label)
    return again


def read(text):
    launches = {}
    for block in text.split('=== launch ')[1:]:
        label = block.split()[0]
        load = float(re.search(r'loadavg=([\d.]+)', block).group(1))
        end = re.search(r'--- exit=(\d+) end=\S+ loadavg=([\d.]+)', block)
        mhz = [float(x) for x in re.findall(r'launch 1 thread \d+ cycles=\d+ thread_ms=[\d.]+ mhz=([\d.]+)', block)]
        counts = ('launch 1 prompt yes' in block
                  and (not label.startswith('dense') or 'launch 1 screen dense' in block)
                  and len(mhz) == 8 and end is not None and end.group(1) == '0')
        launches[label] = dict(load=load, end_load=float(end.group(2)) if end else None,
                               counts=counts, median=statistics.median(mhz) if counts else None)
    return launches


def interval(ratios, n=10_000, seed=88):
    rng = random.Random(seed)
    medians = sorted(statistics.median(rng.choices(ratios, k=len(ratios))) for _ in range(n))
    return medians[int(0.05 * n)], medians[int(0.95 * n) - 1]


def verdict(median, low, high):
    if median >= 0.98 and low >= 0.96:
        return 'pass'
    if high < 0.98:
        return 'fail'
    return 'inconclusive'


def main(path):
    text = open(path).read()
    launches = read(text)
    for label in repeated(text):
        print(f'note: the label {label} heads more than one launch; the last one is used and the earlier is not in the maths')
    verdicts = []
    for case, name in CASES:
        pairs = sorted({int(m) for m in re.findall(rf'\b{case}-p(\d+)-', ' '.join(launches))})
        ratios = []
        print(f'{name}: pair, baseline median, after median, ratio, load before (base, after)')
        for p in pairs:
            b, a = launches.get(f'{case}-p{p}-base'), launches.get(f'{case}-p{p}-after')
            if not b or not a or not b['counts'] or not a['counts']:
                print(f'  {p}: does not count (run the pair again)')
                continue
            r = a['median'] / b['median']
            ratios.append(r)
            print(f"  {p}: {b['median']:.3f} {a['median']:.3f} {r:.4f}  ({b['load']:.2f}, {a['load']:.2f})")
        if not ratios:
            print('  no pair counts')
            continue
        median = statistics.median(ratios)
        low, high = interval(ratios)
        v = verdict(median, low, high)
        verdicts.append(v)
        print(f'  n={len(ratios)} median ratio {median:.4f}, 90% interval {low:.4f} to {high:.4f}; case verdict {v}')
    if len(verdicts) == len(CASES):
        print('VERDICT:', 'FAIL' if 'fail' in verdicts else 'PASS' if set(verdicts) == {'pass'} else 'INCONCLUSIVE')


if __name__ == '__main__':
    main(sys.argv[1])
