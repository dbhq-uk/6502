"""Gathers the measurements the KIM-1's page quotes into one committed file,
site/src/data/kim-1-model.json, so the page never carries a figure typed by
hand (AGENTS.md rule 5): the registration error, the agreement of the tracks
before and after, the share of the board each source covers, and the heights.

    python results.py

Reads data/registration.json, data/agreement.json, data/heights.json and
data/layout.json, all written by the other scripts here.
"""
import json
import os
import re
import subprocess

import common
from fuse import MAP_R

# The model as it was before this work, for the before figures: its heights
# were typical ones, written into its code, and are read from there.
OLD_COMMIT = 'e0fd715'


def old_heights():
    """The tops of the parts in the model before, in mm above the board, read
    from site/src/models/kim-1.js at OLD_COMMIT (its H table is in cm)."""
    src = subprocess.run(['git', 'show', f'{OLD_COMMIT}:site/src/models/kim-1.js'], cwd=common.REPO, capture_output=True, check=True, text=True).stdout
    m = re.search(r"const H = \{ board: S\(BOARD\.thickness\), dip: ([\d.]+), standoff: ([\d.]+), display: ([\d.]+), keypad: ([\d.]+), key: ([\d.]+),", src)
    can = re.search(r"BoxGeometry\(S\(CRYSTAL\.width\), ([\d.]+), S\(CRYSTAL\.depth\)\)", src)
    if not m or not can:
        raise SystemExit(f'the heights were not found in site/src/models/kim-1.js at {OLD_COMMIT}')
    dip, standoff, display, keypad, key = (float(v) * 10 for v in m.groups())
    chip = round(standoff + dip, 2)
    # The measured part each old height stands for.
    return {'dip40-ceramic': chip, 'dip40-plastic': chip, 'dip16': chip, 'dip14': chip, 'display': round(display, 2), 'keypad-bezel': round(keypad, 2), 'key': round(keypad + key, 2), 'crystal': round(float(can.group(1)) * 10, 2)}


def load(name):
    with open(os.path.join(common.DATA, name), encoding='utf8') as f:
        return json.load(f)


def main():
    reg, agree, heights, layout = load('registration.json'), load('agreement.json'), load('heights.json'), load('layout.json')
    sources = common.sources()
    registration = {sid: {'heldOutMedianMm': r['residualMm']['correctedHeldOut']['median'], 'heldOutP90Mm': r['residualMm']['correctedHeldOut']['p90'], 'blocks': r['blocks'], 'file': sources[sid]['file']} for sid, r in reg.items()}
    points = {p['part']: p for p in heights['points']}
    check = points['check']
    # The model's heights before, against the triangulated ones: a part with two
    # marks (the memory chip) is taken at their mean.
    old = old_heights()
    measured = {}
    for p in heights['points']:
        measured.setdefault(p['part'], []).append(p['height'])
    compared = [{'part': k, 'before': v, 'measured': round(sum(measured[k]) / len(measured[k]), 2), 'off': round(abs(v - sum(measured[k]) / len(measured[k])), 2)} for k, v in old.items()]
    worst = max(compared, key=lambda c: c['off'])
    out = {
        'about': 'Made by tools/kim1-model/results.py from the measurements in tools/kim1-model/data/. The KIM-1 page quotes these; nothing on it is typed by hand.',
        'registration': registration,
        'tracks': {
            'top': {
                'seenShare': agree['top']['seenShare'],
                'fromReplicaShare': agree['top']['fromReplicaShare'],
                'disagreeShare': agree['top']['disagreeShareOfSeenByTwo'],
                'before': agree['top']['before']['replica'],
                'after': agree['top']['after']['replica'],
                'zones': {z: {'before': agree['top']['before']['zones'][z], 'after': v} for z, v in agree['top']['after']['zones'].items()},
            },
            'leaveOneOut': {sources[sid]['file']: v for sid, v in agree['leaveOneOut'].items()},
            'bottom': {'seenShare': agree['bottom']['seenShare'], 'after': agree['bottom']['after']['replica']},
            'order': [sources[s]['file'] for s in agree['order']],
            'mapPxPerMm': MAP_R,
        },
        'heights': {
            'checkMm': check['height'],
            'checkRange': check['range'],
            'points': [{'name': p['name'], 'part': p['part'], 'height': p['height'], 'range': p['range']} for p in heights['points'] if p['part'] != 'check'],
            'camera': heights['camera'],
            'before': {'commit': OLD_COMMIT, 'parts': compared, 'meanOffMm': round(sum(c['off'] for c in compared) / len(compared), 2), 'worst': worst},
        },
        'layout': {
            'chipBodyOffsetMm': layout['dipResidualMm'],
            'keyGridResidualMm': layout['keys']['gridResidualMm'],
        },
    }
    common.save_json(os.path.join(common.SITE, 'src', 'data', 'kim-1-model.json'), out)
    print('wrote site/src/data/kim-1-model.json')


if __name__ == '__main__':
    main()
