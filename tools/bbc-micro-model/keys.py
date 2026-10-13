"""Task 8: the BBC Micro's keys, from K1 (Dominic Beesley's MIT KiCad keyboard,
read from the dbhq-uk fork at its pinned commit), and the photographs of the
keyboard registered to it; writes data/keys.json.

    BBC_MODEL_INPUTS=<folder> python keys.py           # the run
    BBC_MODEL_INPUTS=<folder> python keys.py --look    # the legends found, drawn in out/; no held-out figure
    python keys.py --hashes                            # the SHA-256 of the method's files (case_parts.py prints the same)

1. The keys. Every switch footprint in K1 is one keycap, its centre the
   switch's 4 mm locating hole (common.switch_centres). Each is mapped to the
   machine's key by its reference (K1_KEYS), and the value K1 prints on it is
   checked against the one the table expects, so a mistyped row fails. Every
   name in BBC_KEYS (site/public/bbc-keys.js, read as text) must have exactly
   one keycap, BREAK one, and SHIFT a second (Shift2: O1 shows a SHIFT at
   each end of the bottom letter row); a machine key K1 lacks is a STOP. A
   keycap's size is its footprint's (1.00u, 1.50u, 2.00u, 6.50u) times the
   19.05 mm pitch across, by 19.05 mm: the cell it stands in, not the cap,
   which is smaller by a gap no source here gives. Its row is its place in
   K1, 0 for the function keys to 5 for the space bar. Its legend is not
   recorded: the outside model draws LEGENDS[name] from bbc-keys.js, so the
   legends cannot drift from the on-screen keys.

2. O1 to K1: task 0's 50 keys marked by hand on O1 and its homography, each
   key held out in turn (spike.keys, unchanged), judged on the plan's row:
   median at most 1.0 mm, 90th percentile at most 2.0, largest at most 3.0
   to pass; STOP if the median is over 1.5 mm or more than five keys are over
   3.0. The same marks and the same fit as task 0, so not a new check. The
   case's raw front edge on that registration, and the camera from the key
   fit alone, are task 0's, recorded.

3. O3 to K1: the keyboard out of its case. Its points are the legends of the
   26 letter keys and the ten function keys, found in code (legend_centre),
   not marked by hand: a legend is printed in the middle of its keycap's top,
   so the middle of its bounding box is the top's middle, give or take the
   printing. A homography from O3 to K1, each key held out in turn, on the
   same row as O1, recorded and not a stop.

4. Colours, by class. Each keycap's colour on O1 is the median OKLab of a
   disc a quarter of the pitch across round its centre (the legend's light
   pixels lie in it, and the median stands over them), where its centre is
   K1's mapped into O1 through task 0's fit. The medians are put in three
   clusters (k-means, from fixed starts); the cluster with the most red is
   "red", of the other two the lighter is "light" and the other "dark".
   Recorded as classes, not colours: the model's colours are tokens.
"""
import argparse
import hashlib
import json
import re
import sys

import numpy as np

import common

PITCH = 19.05
K1_COMMIT = '5ec53df566c77424caa6be827d0831da3916ecfa'
BBC_KEYS_JS = common.REPO / 'site' / 'public' / 'bbc-keys.js'

# K1's reference -> (the machine's key, the value K1 prints on the footprint). Read off K1's footprints on
# 13 October 2026 and checked against O1, key by key: the keys O1 shows in each place.
K1_KEYS = {
    'SW3': ('F0', 'F0'), 'SW8': ('F1', 'f1'), 'SW15': ('F2', 'f2'), 'SW22': ('F3', 'f3'), 'SW30': ('F4', 'F4'),
    'SW29': ('F5', 'f5'), 'SW36': ('F6', 'f6'), 'SW44': ('F7', 'f7'), 'SW43': ('F8', 'f8'), 'SW50': ('F9', 'f9'),
    'SW75': ('Break', 'break'),
    'SW1': ('Escape', 'escape'), 'SW4': ('D1', '1'), 'SW11': ('D2', '2'), 'SW9': ('D3', '3'), 'SW16': ('D4', '4'),
    'SW23': ('D5', '5'), 'SW32': ('D6', '6'), 'SW31': ('D7', '7'), 'SW37': ('D8', '8'), 'SW38': ('D9', '9'),
    'SW52': ('D0', '0'), 'SW51': ('Minus', '=-'), 'SW58': ('Caret', '~^'), 'SW57': ('Backslash', '\\\\|'),
    'SW65': ('Left', 'left'), 'SW64': ('Right', 'right'),
    'SW7': ('Tab', 'tab'), 'SW2': ('Q', 'Q'), 'SW10': ('W', 'W'), 'SW17': ('E', 'E'), 'SW25': ('R', 'R'),
    'SW24': ('T', 'T'), 'SW33': ('Y', 'Y'), 'SW39': ('U', 'U'), 'SW45': ('I', 'I'), 'SW46': ('O', 'O'),
    'SW53': ('P', 'P'), 'SW54': ('At', '@'), 'SW60': ('LeftBracket', '{['), 'SW59': ('Underscore', '_£'),
    'SW67': ('Up', 'up'), 'SW66': ('Down', 'down'),
    'SW5': ('CapsLock', 'caps lk'), 'SW73': ('Ctrl', 'ctrl'), 'SW12': ('A', 'A'), 'SW13': ('S', 'S'), 'SW18': ('D', 'D'),
    'SW26': ('F', 'F'), 'SW27': ('G', 'G'), 'SW34': ('H', 'H'), 'SW40': ('J', 'J'), 'SW47': ('K', 'K'), 'SW48': ('L', 'L'),
    'SW55': ('Semicolon', ';+'), 'SW61': ('Colon', '*:'), 'SW62': ('RightBracket', '}]'), 'SW68': ('Return', 'return'),
    'SW6': ('ShiftLock', 'shift lk'), 'SW72': ('Shift', 'shift'), 'SW14': ('Z', 'Z'), 'SW19': ('X', 'X'), 'SW20': ('C', 'C'),
    'SW28': ('V', 'V'), 'SW35': ('B', 'B'), 'SW41': ('N', 'N'), 'SW42': ('M', 'M'), 'SW49': ('Comma', '<,'),
    'SW56': ('FullStop', '>.'), 'SW63': ('Slash', '?/'), 'SW71': ('Shift2', 'shift'), 'SW69': ('Delete', 'delete'),
    'SW70': ('Copy', 'copy'),
    'SW21': ('Space', 'space'),
}
EXTRA = ('Break', 'Shift2')      # keycaps that are not in BBC_KEYS: BREAK (the reset line) and the second SHIFT
LEGEND_KEYS = tuple('QWERTYUIOPASDFGHJKLZXCVBNM') + tuple(f'F{i}' for i in range(10))

# --- the plan's rows, fixed before the run (the thresholds table; task 8's note) ---------------------
KEYS_PASS = {'median': 1.0, 'p90': 2.0, 'max': 3.0}
KEYS_STOP = {'median': 1.5, 'over3': 5}
KEYS_MIN = 30
THRESHOLDS = {
    'mapping': 'every name in BBC_KEYS has exactly one K1 keycap, BREAK one and the second SHIFT one, and no keycap is left over; a machine key K1 lacks is a STOP',
    'o1': 'O1 to K1, each key held out: median at most 1.0 mm, 90th percentile at most 2.0, largest at most 3.0 to pass; STOP if the median is over 1.5 or more than five keys are over 3.0; at least 30 keys',
    'o3': 'O3 to K1, each key held out, as O1: recorded, not a stop',
}


def bbc_keys():
    """The names in BBC_KEYS, read from site/public/bbc-keys.js as text."""
    text = BBC_KEYS_JS.read_text(encoding='utf8')
    block = text.split('export const BBC_KEYS = {')[1].split('};')[0]
    return re.findall(r'(\w+): 0x[0-9a-f]{2}', block)


def k1_keys():
    """Every K1 keycap with its machine key, centre, size and row, in K1's millimetres (x to the right, y towards the
    front, from above). Refuses a reference K1_KEYS lacks, and a value K1 prints that is not the one expected."""
    sw = common.switch_centres(common.original('K1'))
    missing = sorted(set(sw) - set(K1_KEYS))
    if missing:
        raise ValueError(f'K1 has keycaps K1_KEYS does not map: {missing}')
    rows = sorted({round(v['centre'][1], 2) for v in sw.values()})
    out = []
    for ref, (name, value) in K1_KEYS.items():
        if ref not in sw:
            continue
        v = sw[ref]
        if v['value'] != value:
            raise ValueError(f'{ref}: K1 prints {v["value"]!r}, the table expects {value!r}')
        units = float(re.search(r'([0-9.]+)u$', v['footprint']).group(1))
        out.append({'name': name, 'k1': ref, 'k1Value': value, 'x': v['centre'][0], 'y': v['centre'][1],
                    'units': units, 'w': units * PITCH, 'd': PITCH, 'row': rows.index(round(v['centre'][1], 2))})
    return out


def mapping_check(keys, names):
    """The mapping row: every BBC_KEYS name once, BREAK once, Shift2 once, nothing else. A machine key K1 lacks is a STOP."""
    have = [k['name'] for k in keys]
    lacks = [n for n in names + list(EXTRA) if n not in have]
    twice = sorted({n for n in have if have.count(n) > 1})
    other = sorted(set(have) - set(names) - set(EXTRA))
    verdict = 'STOP' if lacks else ('pass' if not twice and not other else 'fail')
    return {'keys': len(have), 'machineKeys': len(names), 'lacks': lacks, 'twice': twice, 'other': other, 'verdict': verdict}


# --- legends found in a photograph ----------------------------------------------------------

def legend_centre(rgb, x, y, r):
    """The middle of the bounding box of a keycap's legend near (x, y), within r pixels: the legend's pixels are the
    light, nearly grey ones (OKLab L over the midpoint of the window's median and its 99.5th percentile, chroma under
    0.12), in pieces of at least 4 pixels whose middles lie within 0.6 r of the window's centre. None if there are none."""
    from scipy import ndimage
    x0, y0 = int(round(x - r)), int(round(y - r))
    win = rgb[max(y0, 0):y0 + 2 * r + 1, max(x0, 0):x0 + 2 * r + 1]
    L, A, B = common.oklab(win)
    C = np.hypot(A, B)
    t = (np.median(L) + np.percentile(L, 99.5)) / 2
    lbl, n = ndimage.label((L > t) & (C < 0.12))
    keep = np.zeros(L.shape, bool)
    cx, cy = x - max(x0, 0), y - max(y0, 0)
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lbl == i)
        if len(xs) >= 4 and np.hypot(xs.mean() - cx, ys.mean() - cy) <= 0.6 * r:
            keep[lbl == i] = True
    ys, xs = np.nonzero(keep)
    if len(xs) == 0:
        return None
    return float(max(x0, 0) + (xs.min() + xs.max()) / 2), float(max(y0, 0) + (ys.min() + ys.max()) / 2)


def find_legends(source_id, seeds, k1, names=LEGEND_KEYS, skip=()):
    """Every named key's legend in a photograph: predicted from K1 by a homography fitted to the rough seeds, found,
    then predicted again from a homography fitted to what was found, and found again. Returns {name: (x, y)} in the
    photograph's pixels. `skip` keys are found but not used in the second fit."""
    rgb = common.read_rgb(source_id)
    at = {k['name']: (k['x'], k['y']) for k in k1}
    H = common._fit('homography', np.array([at[n] for n in seeds], float), np.array([seeds[n] for n in seeds], float))['H']
    found = {}
    for _ in range(2):
        pred = {n: common.transform('homography', {'H': H}, [at[n]])[0] for n in names}
        # the search radius: 0.38 of the local pitch, from the predicted distance to a key one pitch to the right
        found = {}
        for n in names:
            step = common.transform('homography', {'H': H}, [(at[n][0] + PITCH, at[n][1])])[0]
            r = int(round(0.38 * np.hypot(*(step - pred[n]))))
            c = legend_centre(rgb, pred[n][0], pred[n][1], r)
            if c is not None:
                found[n] = c
        use = [n for n in found if n not in skip]
        H = common._fit('homography', np.array([at[n] for n in use], float), np.array([found[n] for n in use], float))['H']
    return found


def register(found, k1):
    """A photograph's legends to K1: the homography photograph -> K1, each key held out in turn (common.fit_held_out)."""
    at = {k['name']: (k['x'], k['y']) for k in k1}
    names = sorted(found)
    r = common.fit_held_out({n: found[n] for n in names}, {n: at[n] for n in names}, 'homography', 'leave-one-out')
    held = dict(r['heldOutEach'])
    return {'n': r['n'], 'model': 'homography', 'fitMm': r['fitMm'], 'heldOutMm': r['heldOutMm'],
            'over3mm': sum(1 for v in held.values() if v > 3.0), 'worst': r['worst'][:10], 'params': r['params']}


def judge_keys(k):
    ok = (k['heldOutMm']['median'] <= KEYS_PASS['median'] and k['heldOutMm']['p90'] <= KEYS_PASS['p90']
          and k['heldOutMm']['max'] <= KEYS_PASS['max'] and k['n'] >= KEYS_MIN)
    stop = k['heldOutMm']['median'] > KEYS_STOP['median'] or k['over3mm'] > KEYS_STOP['over3']
    return 'STOP' if stop else ('pass' if ok else 'between pass and stop')


# --- colours ----------------------------------------------------------------------------------

def colour_classes(keys, H_k1_to_o1):
    """Each keycap's median OKLab on O1 in a disc a quarter of the pitch across round its centre, put in three
    clusters by k-means from fixed starts (the reddest, the lightest and the darkest median); named red, light, dark."""
    rgb = common.read_rgb('O1')
    L, A, B = common.oklab(rgb)
    med = []
    for k in keys:
        c = common.transform('homography', {'H': H_k1_to_o1}, [(k['x'], k['y'])])[0]
        e = common.transform('homography', {'H': H_k1_to_o1}, [(k['x'] + PITCH, k['y'])])[0]
        r = 0.125 * np.hypot(*(e - c))
        yy, xx = np.ogrid[:L.shape[0], :L.shape[1]]
        m = (xx - c[0]) ** 2 + (yy - c[1]) ** 2 <= r * r
        med.append([float(np.median(L[m])), float(np.median(A[m])), float(np.median(B[m]))])
    X = np.array(med)
    starts = [int(np.argmax(X[:, 1])), int(np.argmax(X[:, 0])), int(np.argmin(X[:, 0]))]
    centres = X[starts].copy()
    for _ in range(50):
        lab = np.argmin(((X[:, None, :] - centres[None]) ** 2).sum(-1), 1)
        new = np.array([X[lab == i].mean(0) if np.any(lab == i) else centres[i] for i in range(3)])
        if np.allclose(new, centres):
            break
        centres = new
    chroma = np.hypot(centres[:, 1], centres[:, 2])
    red = int(np.argmax(centres[:, 1]))
    rest = [i for i in range(3) if i != red]
    light = max(rest, key=lambda i: centres[i, 0])
    names = {red: 'red', light: 'light', [i for i in rest if i != light][0]: 'dark'}
    return [names[int(i)] for i in lab], {names[i]: {'oklab': centres[i].tolist(), 'chroma': float(chroma[i]),
                                                       'keys': int((lab == i).sum())} for i in range(3)}


def method_files():
    return ['tools/bbc-micro-model/keys.py', 'tools/bbc-micro-model/case_register.py', 'tools/bbc-micro-model/case_profile.py',
            'tools/bbc-micro-model/case_parts.py', 'tools/bbc-micro-model/common.py', 'tools/bbc-micro-model/spike.py',
            'tools/bbc-micro-model/data/case-marks.json', 'tools/bbc-micro-model/data/marks.json',
            'tools/bbc-micro-model/data/parts.json']


def method_hashes():
    return {f: hashlib.sha256((common.REPO / f).read_bytes()).hexdigest() for f in method_files()}


def case_marks():
    return json.loads((common.DATA / 'case-marks.json').read_text(encoding='utf8'))


def measure(look=False):
    import spike
    names = bbc_keys()
    keys = k1_keys()
    mapping = mapping_check(keys, names)
    print(f"mapping: {mapping['keys']} keycaps for {mapping['machineKeys']} machine keys and {', '.join(EXTRA)}; "
          f"lacks {mapping['lacks']}, twice {mapping['twice']}, other {mapping['other']}: {mapping['verdict']}")
    if mapping['verdict'] == 'STOP':
        return None
    seeds = case_marks()['seeds']
    o3 = find_legends('O3', seeds['O3'], keys)
    if look:
        draw(o3, 'O3')
        return None
    o1, r1 = spike.keys()
    o1v = judge_keys(o1)
    print(f"O1 to K1 (task 0's marks), each key held out: median {o1['heldOutMm']['median']:.3f}, p90 {o1['heldOutMm']['p90']:.3f}, "
          f"max {o1['heldOutMm']['max']:.3f} mm over {o1['n']} keys, {o1['over3mm']} over 3 mm: {o1v}")
    reg3 = register(o3, keys)
    o3v = judge_keys(reg3)
    print(f"O3 to K1 (legends), each key held out: median {reg3['heldOutMm']['median']:.3f}, p90 {reg3['heldOutMm']['p90']:.3f}, "
          f"max {reg3['heldOutMm']['max']:.3f} mm over {reg3['n']} keys, {reg3['over3mm']} over 3 mm: {o3v} (recorded, not a stop)")
    H_o1_to_k1 = np.array(r1['params']['H'])
    classes, clusters = colour_classes(keys, np.linalg.inv(H_o1_to_k1))
    for k, c in zip(keys, classes):
        k['colour'] = c
    print('colours: ' + ', '.join(f"{n} {v['keys']}" for n, v in clusters.items()))
    return {
        'about': ('Task 8 of the BBC Micro models plan: every keycap from K1 (Dominic Beesley\'s MIT KiCad keyboard, from the dbhq-uk '
                  'fork at its pinned commit) mapped to the machine\'s keys, and O1 and O3 registered to it. Written by keys.py; every '
                  'figure is a measurement made on the date of the journal entry that quotes it. Coordinates are K1\'s millimetres '
                  '(x to the right, y towards the front, from above); case_parts.py places them in the case.'),
        'k1': {'commit': K1_COMMIT, 'switches': len(keys), 'sha256': common.source('K1')['sha256']},
        'keys': keys,
        'mapping': mapping,
        'o1': {k: o1[k] for k in ('n', 'model', 'fitMm', 'heldOutMm', 'over3mm', 'worst')} | {'from': 'task 0\'s 50 marks (marks.json keys), spike.keys', 'verdict': o1v},
        'o3': {k: reg3[k] for k in ('n', 'model', 'fitMm', 'heldOutMm', 'over3mm', 'worst')} | {
            'from': 'the legends of the 26 letter keys and the ten function keys, found by legend_centre', 'verdict': o3v,
            'legends': {n: list(v) for n, v in sorted(o3.items())}},
        'frontEdgeMm': o1['frontEdgeMm'],
        'frontEdgeRepeatMm': o1['frontEdgeRepeatMm'],
        'parallax': o1['parallax'],
        'colours': clusters,
        'thresholds': THRESHOLDS,
        'verdicts': {'mapping': mapping['verdict'], 'o1': o1v, 'o3': o3v},
    }


def draw(found, source_id):
    import cv2
    from PIL import Image
    common.OUT.mkdir(exist_ok=True)
    img = common.read_rgb(source_id).copy()
    for n, (x, y) in found.items():
        cv2.drawMarker(img, (int(round(x)), int(round(y))), (0, 255, 0), cv2.MARKER_CROSS, 14, 1)
        cv2.putText(img, n, (int(x) + 6, int(y) - 6), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 1)
    Image.fromarray(img).save(common.OUT / f'keys-{source_id.lower()}.png')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--hashes', action='store_true', help='print the SHA-256 of the method\'s files, and stop')
    ap.add_argument('--look', action='store_true', help='find O3\'s legends and draw them in out/; no held-out figure')
    args = ap.parse_args()
    if args.hashes:
        for k, v in method_hashes().items():
            print(v, k)
        return 0
    data = measure(look=args.look)
    if args.look:
        return 0
    if data is None:
        return 3
    data['methodFixed'] = method_hashes()
    common.write_data('keys.json', data)
    return 3 if 'STOP' in data['verdicts'].values() else 0


if __name__ == '__main__':
    sys.exit(main())
