"""Gathers the measurements the BBC Micro's page quotes into committed results
files, so the page never carries a figure typed by hand (AGENTS.md rule 5). The
board half, for the inside model, writes
site/src/data/bbc-micro-board-model.json (plan task 7); the case half, for the
outside model, comes with task 9a.

    ~/venv-models/bin/python tools/bbc-micro-model/results.py

Reads data/frame.json, data/registration.json, data/copper.json,
data/parts.json and data/sources.json, all written or kept by the other
scripts here, and writes only the figures the page quotes, each as the data
file holds it. It reads no input and fetches nothing.
"""
import json

import common

SITE_DATA = common.REPO / 'site' / 'src' / 'data'
BOARD_OUT = SITE_DATA / 'bbc-micro-board-model.json'


def load(name):
    with open(common.DATA / name, encoding='utf8') as f:
        return json.load(f)


def with_addresses(ids, sources):
    """Each source by id, with its address, its page, the SHA-256 of the
    original, its licence as stated, its author and the day it was fetched, from
    sources.json, so the page can credit each by its address."""
    by_id = {s['id']: s for s in sources}
    missing = [i for i in ids if i not in by_id]
    if missing:
        raise SystemExit(f'not in data/sources.json: {", ".join(missing)}')
    keys = ('id', 'url', 'page', 'sha256', 'kind', 'licence', 'author', 'fetched')
    return [{k: by_id[i][k] for k in keys} for i in ids]


def board_sources(parts, sources):
    """The inputs the board model rests on: the ones parts.json names as the
    parts module's sources (the bare scans I1 and I2, the populated board I5,
    and the Service Manual D1), then I6, the second populated board, whose
    markings task 5 read and recorded. The makers' data sheets behind the nets
    check's pinouts are facts for a check, listed in ic-table.json, and not
    sources of what is drawn."""
    ids = list(parts['model']['sources'])
    if 'I6' not in ids:
        ids.append('I6')
    return with_addresses(ids, sources)


def x_verdict(frame):
    """The x scale's verdict as the plan revised it (task 2): one row of
    frame.json's verdicts says so in its measure."""
    rows = [v for v in frame['verdicts'] if 'as revised' in v['measure']]
    if len(rows) != 1:
        raise SystemExit(f'frame.json has {len(rows)} verdicts "as revised", not one')
    return rows[0]['verdict']


def board_results(frame, registration, copper, parts, sources):
    """The board model's figures, each read from the data file named beside it."""
    solder = registration['solder']
    nets = copper['nets']
    i5 = copper['againstI5']
    manual = parts['manual']['check']
    model = parts['model']
    heights = [h for k, h in model['heights'].items() if k != 'board']
    fitted = [i for i in model['ics'] if i['fitted']]
    return {
        'about': 'Made by tools/bbc-micro-model/results.py from the measurements in tools/bbc-micro-model/data/. '
                 'The BBC Micro page quotes these for the board model; nothing on it is typed by hand.',
        # frame.json: the scan's size and scale
        'board': {'widthMm': frame['board']['widthMm'], 'depthMm': frame['board']['depthMm']},
        'scale': {
            'yMedianMm': frame['heldOut']['rowLengthErrMm']['median'],
            'yRows': frame['heldOut']['rowLengthErrMm']['n'],
            'xMedianMm': frame['heldOutX']['scaledErrMm']['median'],
            'xRows': frame['heldOutX']['scaledErrMm']['n'],
            'xLargestMm': frame['heldOutX']['largest']['scaledErrMm'],
            'xLargestRow': frame['heldOutX']['largest']['row'],
            'xyPct': frame['ratioPct'],
            'xVerdict': x_verdict(frame),
        },
        # registration.json: the solder side on the component side, the figure without exclusion (the plan's rule)
        'solder': {'holes': solder['holes'], 'heldOutMedianMm': solder['heldOutMm']['median'], 'heldOutP90Mm': solder['heldOutMm']['p90'],
                   'verdict': solder['verdict']},
        # copper.json: the copper, its checks and the map
        'copper': {
            'top': copper['coverage']['top'],
            'bottom': copper['coverage']['bottom'],
            'print': copper['coverage']['print'],
            'drills': copper['drills']['n'],
            'drillsTop': copper['drillsInCopper']['top'],
            'drillsBottom': copper['drillsInCopper']['bottom'],
            'mapPxPerMm': copper['mapPxPerMm'],
            'printMm2': copper['print']['areaMm2'],
            'recoveredMm2': copper['print']['recoveredMm2'],
            'nets': {
                'chips': nets['chips'],
                'gndPins': nets['gnd']['pins'],
                'gndInLargest': nets['gnd']['inLargest'],
                'vccPins': nets['vcc']['pins'],
                'vccInLargest': nets['vcc']['inLargest'],
                'touching': nets['touching'],
                'verdict': copper['verdicts']['nets'],
            },
            'againstI5': {'iou': i5['iou'], 'comparedMm2': i5['comparedMm2'], 'verdict': copper['verdicts']['againstI5']},
            'i5Registration': {'chips': i5['registration']['chips'], 'points': i5['registration']['points'],
                               'heldOutMedianMm': i5['registration']['heldOutMm']['median'], 'verdict': copper['verdicts']['i5Registration']},
        },
        # parts.json: the parts, and the two checks on them
        'parts': {
            'ics': len(model['ics']),
            'fitted': len(fitted),
            'socketed': sum(1 for i in fitted if i['socketed']),
            'connectors': len(model['connectors']),
            'passives': len(model['passives']),
            'axial': parts['passives']['byKind']['axial'],
            'radial': parts['passives']['byKind']['radial'],
            'manual': {
                'maxMm': manual['maxMm'],
                'medianMm': manual['medianMm'],
                'limitMm': manual['limitMm'],
                'over': sorted((r for r, p in manual['perPart'].items() if p['offMm'] > manual['limitMm']), key=lambda r: int(r[2:])),
                'verdict': parts['verdicts']['manual'],
            },
            'identities': {
                'read': parts['identities']['row']['read'],
                'populated': parts['identities']['row']['populated'],
                'disagree': len(parts['identities']['row']['disagree']),
                'verdict': parts['verdicts']['identities'],
            },
            'heightsTypical': sum(1 for h in heights if not h['measured']),
            'heightsMeasured': sum(1 for h in heights if h['measured']),
        },
        'sources': board_sources(parts, sources),
    }


def main():
    out = board_results(load('frame.json'), load('registration.json'), load('copper.json'), load('parts.json'), load('sources.json')['sources'])
    text = json.dumps(common.plain(out, None), indent=1, sort_keys=True, ensure_ascii=False)
    BOARD_OUT.write_text(text + '\n', encoding='utf8')
    print(f'wrote {BOARD_OUT.relative_to(common.REPO)}')


if __name__ == '__main__':
    main()
