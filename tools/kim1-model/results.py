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

import common


def load(name):
    with open(os.path.join(common.DATA, name), encoding='utf8') as f:
        return json.load(f)


def main():
    reg, agree, heights, layout = load('registration.json'), load('agreement.json'), load('heights.json'), load('layout.json')
    sources = common.sources()
    registration = {sid: {'heldOutMedianMm': r['residualMm']['correctedHeldOut']['median'], 'heldOutP90Mm': r['residualMm']['correctedHeldOut']['p90'], 'blocks': r['blocks'], 'file': sources[sid]['file']} for sid, r in reg.items()}
    points = {p['part']: p for p in heights['points']}
    check = points['check']
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
        },
        'heights': {
            'checkMm': check['height'],
            'checkRange': check['range'],
            'points': [{'name': p['name'], 'part': p['part'], 'height': p['height'], 'range': p['range']} for p in heights['points'] if p['part'] != 'check'],
            'camera': heights['camera'],
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
