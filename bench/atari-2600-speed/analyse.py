"""Reads a launches file and applies the rule fixed before the first launch (rule.md).

python3 analyse.py <launches.txt>
    each launch's five times_real, its best and its median, then the VERDICT line
python3 analyse.py --counts <output-file> <exit-code> <NTSC|PAL>
    exits 0 if one launch's output counts by rule.md, else 1 with the reason; run-series.sh
    asks this after each launch, so what counts is written once, here

Per timed run: times_real = (cycles / seconds) / the CPU clock the page printed from the machine
(Region.CpuHz). Per launch: the best and the median of the five. The verdict is set by the lower
of the two regions' AOT medians: at least 25 CONTINUE, at least 10 CONTINUE, HEADROOM, under 10
STOP; with a launch missing, NONE. The interpreter is measured and judged by nothing.
"""
import re
import statistics
import sys

LAUNCHES = (('aot-ntsc', 'NTSC'), ('aot-pal', 'PAL'), ('interpreter-ntsc', 'NTSC'), ('interpreter-pal', 'PAL'))
TIMED = re.compile(r'^launch 1 timed (\d+) cycles=(\d+) frames=(\d+) lines=(\d+) ms=([\d.]+) cycles_per_second=\d+$', re.M)
CLOCK = re.compile(r'^launch 1 clock cpu_hz=([\d.e+]+) nominal_lines=(\d+)$', re.M)


def judge(body, code, region):
    """The five times_real of one launch's output, or (None, why) if it does not count."""
    if code != 0:
        return None, f'exit {code}'
    if not re.search(rf'^launch 1 load .* in {region}\.$', body, re.M):
        return None, f'no load line for {region}'
    clock = CLOCK.search(body)
    if not clock:
        return None, 'no clock line'
    cpu_hz, nominal = float(clock.group(1)), int(clock.group(2))
    if not re.search(r'^launch 1 settle cycles=\d+ frames=60 ', body, re.M):
        return None, 'no settle line with 60 frames'
    runs = TIMED.findall(body)
    if [int(r[0]) for r in runs] != [1, 2, 3, 4, 5]:
        return None, f'{len(runs)} timed lines, not five'
    times = []
    for number, cycles, frames, lines, ms in runs:
        if int(frames) == 0:
            return None, f'timed {number} ended no frame'
        if int(lines) != nominal:
            return None, f'timed {number} ended on a frame of {lines} lines, not {nominal}'
        times.append(int(cycles) / (float(ms) / 1000) / cpu_hz)
    return times, None


def read(text):
    """The first launch under each label that counts, and a note for each that does not."""
    judged, notes = {}, []
    for block in text.split('=== launch ')[1:]:
        label = block.split()[0]
        end = re.search(r'^--- exit=(\d+) ', block, re.M)
        region = 'PAL' if label.endswith('-pal') else 'NTSC'
        times, why = judge(block, int(end.group(1)) if end else -1, region)
        if times is None:
            notes.append(f'note: a launch {label} does not count ({why}); it stays in the log')
        elif label not in judged:
            judged[label] = times
    return judged, notes


def verdict(lower):
    if lower >= 25:
        return 'CONTINUE'
    if lower >= 10:
        return 'CONTINUE, HEADROOM'
    return 'STOP'


def main(path):
    judged, notes = read(open(path).read())
    for note in notes:
        print(note)
    print('launch: times_real of the five timed runs; best; median')
    for label, _ in LAUNCHES:
        times = judged.get(label)
        if times is None:
            print(f'  {label}: no launch counts')
            continue
        print(f"  {label}: {' '.join(f'{t:.2f}' for t in times)}; best {max(times):.2f}; median {statistics.median(times):.2f}")
    medians = [statistics.median(judged[label]) for label in ('aot-ntsc', 'aot-pal') if label in judged]
    if len(medians) < 2 or not all(label in judged for label, _ in LAUNCHES):
        print('VERDICT: NONE')
        return
    lower = min(medians)
    print(f'the lower AOT median: {lower:.2f} times real time')
    print('VERDICT:', verdict(lower))


def counts(path, code, region):
    times, why = judge(open(path).read(), int(code), region)
    if times is None:
        print(f'does not count: {why}')
        sys.exit(1)


if __name__ == '__main__':
    if sys.argv[1] == '--counts':
        counts(*sys.argv[2:5])
    else:
        main(sys.argv[1])
