"""Gathers the measurements the BBC Micro's page quotes into committed results
files, so the page never carries a figure typed by hand (AGENTS.md rule 5). The
board half, for the inside model, writes
site/src/data/bbc-micro-board-model.json (plan task 7); the case half, for the
outside model, writes site/src/data/bbc-micro-case-model.json (plan task 9a).

    ~/venv-models/bin/python tools/bbc-micro-model/results.py

Reads data/frame.json, data/registration.json, data/copper.json,
data/parts.json, data/keys.json, data/case.json and data/sources.json, all
written or kept by the other scripts here, and writes only the figures the page
quotes, each as the data file holds it. It reads no input and fetches nothing.
"""
import json

import common

SITE_DATA = common.REPO / 'site' / 'src' / 'data'
BOARD_OUT = SITE_DATA / 'bbc-micro-board-model.json'
CASE_OUT = SITE_DATA / 'bbc-micro-case-model.json'


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


def case_results(keys, case, sources):
    """The case model's figures, each read from keys.json or case.json as
    named beside it. The width's three measures are case_parts.py's
    width_words(): the keys' scale on O1's case top, the scale the opening's
    two front corners imply, and the case's width on O2's D shell scale against
    the Service Manual's; they disagree with the manual, and nothing here
    resolves that."""
    width = case['footprint']['widthMm']
    rear_ratio = round(case['widthOnRear']['mm'] / width, 4)
    ratios = [case['caseTop']['scale'], case['k1ToCase']['impliedScale'], rear_ratio]
    o1, o3 = keys['o1'], keys['o3']
    rear = case['rearCheck']
    worst = max(rear['errMm'], key=lambda ref: abs(rear['errMm'][ref]))
    profile = case['profileCheck']
    # The typical values the model draws. keyAboveTop and panelBelowKeys are
    # case_parts.py's fallback, drawn only if the side profile's check failed.
    fallback_only = {'keyAboveTop', 'panelBelowKeys'}
    typical = sorted(k for k, t in case['typical'].items() if not t['measured'] and (profile['fallback'] or k not in fallback_only))
    colours = [k['colour'] for k in keys['keys']]
    front = case['frontEdge']
    return {
        'about': 'Made by tools/bbc-micro-model/results.py from tools/bbc-micro-model/data/keys.json and case.json. '
                 'The BBC Micro page quotes these for the case model; nothing on it is typed by hand.',
        # case.json: the Service Manual's size, and the three measures that disagree with it
        'size': {'widthMm': width, 'depthMm': case['footprint']['depthMm'], 'heightMm': case['heightMm'], 'from': case['footprint']['source']},
        'width': {'caseTopScale': case['caseTop']['scale'], 'cornersScale': case['k1ToCase']['impliedScale'],
                  'rearMm': case['widthOnRear']['mm'], 'rearRatio': rear_ratio, 'low': min(ratios), 'high': max(ratios), 'resolved': False},
        # keys.json: the keycaps, their mapping and their colours as read on O1
        'keys': {'keycaps': keys['mapping']['keys'], 'machineKeys': keys['mapping']['machineKeys'], 'mapping': keys['verdicts']['mapping'],
                 'red': colours.count('red'), 'light': colours.count('light'), 'dark': colours.count('dark'), 'coloursAgree': keys['colours']['agree']},
        'o1': {'n': o1['n'], 'medianMm': o1['heldOutMm']['median'], 'p90Mm': o1['heldOutMm']['p90'], 'maxMm': o1['heldOutMm']['max'], 'verdict': keys['verdicts']['o1']},
        'o3': {'n': len(o3['legends']), 'medianMm': o3['heldOutMm']['median'], 'p90Mm': o3['heldOutMm']['p90'], 'maxMm': o3['heldOutMm']['max'], 'verdict': keys['verdicts']['o3']},
        # case.json: the front edge's check, not measurable, and d
        'frontEdge': {'rawMm': front['rawMm'], 'verdict': case['verdicts']['frontEdge'], 'hFound': front['h']['mm'] is not None,
                      'dMm': front['d']['mm'], 'dSeMm': front['d']['seMm']},
        'profileCheck': {'errMm': profile['errMm'], 'limitMm': profile['limitMm'], 'verdict': case['verdicts']['profileCheck'], 'fallback': profile['fallback']},
        'rear': {'n': rear['n'], 'within': rear['within'], 'worstMm': rear['worstMm'], 'worstRef': worst, 'limitMm': rear['limitMm'],
                 'verdict': case['verdicts']['rear'], 'boardOffsetMm': case['boardInCase']['x']},
        'typical': typical,
        'sources': with_addresses(case['sources'], sources),
    }


def write(path, out):
    text = json.dumps(common.plain(out, None), indent=1, sort_keys=True, ensure_ascii=False)
    path.write_text(text + '\n', encoding='utf8')
    print(f'wrote {path.relative_to(common.REPO)}')


def main():
    sources = load('sources.json')['sources']
    write(BOARD_OUT, board_results(load('frame.json'), load('registration.json'), load('copper.json'), load('parts.json'), sources))
    write(CASE_OUT, case_results(load('keys.json'), load('case.json'), sources))


if __name__ == '__main__':
    main()
