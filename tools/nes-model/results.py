"""Gathers the measurements the NES's page quotes into committed results files,
so the page never carries a figure typed by hand (AGENTS.md rule 5). The board
half, for the inside model, writes site/src/data/nes-famicom-board-model.json
(plan task 7); the case half, for the outside model, writes
site/src/data/nes-famicom-case-model.json (plan task 9).

    /tmp/nesvenv/bin/python tools/nes-model/results.py

Reads data/frame.json, data/registration.json, data/copper.json,
data/parts.json, data/case.json and data/sources.json, all written or kept by
the other scripts here, and writes only the figures the page quotes, each as the data
file holds it. It reads no input and fetches nothing.
"""
import json
import re
import sys

import common

SITE_DATA = common.REPO / 'site' / 'src' / 'data'
BOARD_OUT = SITE_DATA / 'nes-famicom-board-model.json'
CASE_OUT = SITE_DATA / 'nes-famicom-case-model.json'


def load(name):
    with open(common.DATA / name, encoding='utf8') as f:
        return json.load(f)


def board_sources(parts, sources):
    """The inputs the board model rests on: the ones parts.json names as the
    parts module's sources (the scans, and the photographs the parts were read
    from), then the KiCad redrawing, I2, which was compared with the scan and
    never drawn from. Each with its address, its page and the SHA-256 of the
    original, from sources.json, so the page can credit each by its address."""
    ids = list(parts['model']['sources'])
    if 'I2' not in ids:
        ids.append('I2')
    by_id = {s['id']: s for s in sources}
    missing = [i for i in ids if i not in by_id]
    if missing:
        raise SystemExit(f'not in data/sources.json: {", ".join(missing)}')
    return [{'id': i, 'url': by_id[i]['url'], 'page': by_id[i]['page'], 'sha256': by_id[i]['sha256'], 'kind': by_id[i]['kind']} for i in ids]


def board_results(frame, registration, copper, parts, sources):
    """The board model's figures, each read from the data file named beside it."""
    solder = registration['solder']
    nets = copper['nets']
    sits = parts['sitsOn']['ics']
    heights = [h for k, h in parts['model']['heights'].items() if k != 'board']
    return {
        'about': 'Made by tools/nes-model/results.py from the measurements in tools/nes-model/data/. The NES page quotes these for the board model; nothing on it is typed by hand.',
        # frame.json: the scan's size and scale
        'board': {'widthMm': frame['board']['widthMm'], 'depthMm': frame['board']['depthMm'], 'dpi': frame['statedDpi']},
        'scale': {
            'xMedianMm': frame['heldOutX']['scaledErrMm']['median'],
            'xRows': frame['heldOutX']['n'],
            'yMedianPct': frame['heldOutY']['errPct']['median'],
            'yFootprints': len(frame['heldOutY']['footprints']),
            'xyPct': frame['ratioPct'],
            'verdicts': [v['verdict'] for v in frame['verdicts']],
        },
        # registration.json: the solder side on the component side
        'solder': {'holes': solder['holes'], 'heldOutMedianMm': solder['heldOutMm']['median'], 'heldOutP90Mm': solder['heldOutMm']['p90'], 'verdict': solder['verdict']},
        # copper.json: the copper, its checks and the map
        'copper': {
            'top': copper['coverage']['top'],
            'bottom': copper['coverage']['bottom'],
            'drillsTop': copper['drillsInCopper']['top'],
            'drillsBottom': copper['drillsInCopper']['bottom'],
            'drills': copper['drills']['n'],
            'mapPxPerMm': copper['mapPxPerMm'],
            'nets': {
                'chips': nets['chips'],
                'gndPins': nets['gnd']['pins'],
                'gndInLargest': nets['gnd']['inLargest'],
                'vccPins': nets['vcc']['pins'],
                'vccInLargest': nets['vcc']['inLargest'],
                'touching': nets['touching'],
                'verdict': copper['verdicts']['nets'],
            },
        },
        # parts.json: the parts, both consoles
        'parts': {
            'ics': len(parts['model']['ics']),
            'passives': len(parts['model']['passives']),
            'kicadMaxMm': parts['kicad']['maxMm'],
            'kicadMedianMm': parts['kicad']['medianMm'],
            # the NTSC parts, read on Evan-Amos's NES-CPU-07 (I4 top, I5 solder side), on the CPU-10's footprints
            'ntscWorstMm': max(max(s['I4'], s['I5']) for s in sits.values()),
            # the PAL parts, read on the NES-CPU-11 photograph (I3), on the same footprints
            'palWorstMm': max(s['I3'] for s in sits.values()),
            'sitsOnLimitMm': parts['sitsOn']['limitMm'],
            'heightsTypical': sum(1 for h in heights if not h['measured']),
            'heightsMeasured': sum(1 for h in heights if h['measured']),
        },
        'sources': board_sources(parts, sources),
    }


def with_addresses(ids, sources):
    """Each source by id, with its address, its page and the SHA-256 of the
    original, from sources.json, so the page can credit each by its address."""
    by_id = {s['id']: s for s in sources}
    missing = [i for i in ids if i not in by_id]
    if missing:
        raise SystemExit(f'not in data/sources.json: {", ".join(missing)}')
    return [{'id': i, 'url': by_id[i]['url'], 'page': by_id[i]['page'], 'sha256': by_id[i]['sha256'], 'kind': by_id[i]['kind']} for i in ids]


def about(text, unit):
    """The figure a sentence of case.json's gives as "good to about N <unit>":
    the words were written by case_measure.py beside the figures they sum up,
    so the page quotes them from there rather than typing them again."""
    m = re.search(r'good to about ([0-9.]+) ' + re.escape(unit), text)
    if not m:
        raise SystemExit(f'case.json no longer says "good to about ... {unit}" in: {text}')
    return float(m.group(1))


def case_results(case, sources):
    """The case model's figures, each read from case.json (or, for the PAL
    photographs' lens, from sources.json, which records it from each file's XMP)."""
    rear = case['rear']
    check = case['rearCheck']
    profile = case['profileCheck']
    board = case['boardInCase']
    pal_lens = {s['id']: s['focalLengthMm'] for s in sources if s['id'] in ('O4', 'O10') and 'focalLengthMm' in s}
    if sorted(pal_lens) != ['O10', 'O4'] or len(set(pal_lens.values())) != 1:
        raise SystemExit('sources.json does not give O4 and O10 one focal length')
    buttons = case['buttons']
    if len({b['travelMm'] for b in buttons}) != 1 or len({b['travelFrom'] for b in buttons}) != 1:
        raise SystemExit('the buttons do not share one travel')
    return {
        'about': 'Made by tools/nes-model/results.py from tools/nes-model/data/case.json. The NES page quotes these for the case model; nothing on it is typed by hand.',
        # the size: published, not Nintendo's
        'size': {
            'widthMm': case['footprint']['widthMm'], 'depthMm': case['footprint']['depthMm'], 'heightMm': case['heightMm'],
            'from': case['heightFrom'], 'notNintendos': case['footprint']['notNintendos'],
            'goodToPct': about(case['footprint']['widthSource'], 'per cent'),
            'feetMm': case['model']['CASE']['feet'],
        },
        # task 0's checks of the proportions on the patent's views, and the width cross-check on O9
        'spike': {'depth': case['spike']['caseDepth'], 'height': case['spike']['caseHeight'], 'palFront': case['spike']['palFront']},
        'widthCrossCheckPct': case['widthCrossCheck']['againstPublishedMidpointPct'],
        # the side profile: the bottom shell's ends, leaning in
        'profile': {
            'insetMm': case['model']['PROFILE']['points'][-1]['inset'],
            'endsMm': profile['endsMm'], 'heldOutMm': profile['errMm'], 'limitMm': profile['limitMm'],
            'passes': profile['passes'], 'fallbackUsed': profile['fallbackUsed'],
            'goodToMm': about(profile['endsWhat'], 'mm'),
        },
        # the rear connectors: placed from O2-BR; the board's places, the check, failed as measured
        'rear': {
            'checked': check['checked'], 'within': check['within'], 'limitMm': check['limitMm'], 'worstMm': check['worstMm'],
            'placesUsedMm': check['uncertaintyMm']['placesUsed'], 'boardMissMm': check['uncertaintyMm']['boardMiss'],
            'words': check['words'],
            'avChecked': any(r['checked'] for r in rear['ntsc'] if r['face'] != 'rear'),
            'palChecked': any(r['checked'] for r in rear['pal']),
        },
        # the board's place in the case, from O9
        'boardInCase': {'uncertaintyMm': board['uncertaintyMm'], 'sharedMoulding': 'moulding' in board['from']},
        'buttons': {'travelMm': buttons[0]['travelMm'], 'travelFrom': buttons[0]['travelFrom']},
        'powerLatchSeen': case['powerLatch']['seen'],
        'palLensMm': next(iter(pal_lens.values())),
        'palDifferences': len(case['palDifferences']),
        'sources': with_addresses(list(case['sources']), sources),
    }


def write(path, out):
    text = json.dumps(common.plain(out), indent=1, sort_keys=True, ensure_ascii=False)
    path.write_text(text + '\n', encoding='utf8')
    print(f'wrote {path.relative_to(common.REPO)}')


def main():
    write(BOARD_OUT, board_results(load('frame.json'), load('registration.json'), load('copper.json'), load('parts.json'), common.sources()))
    write(CASE_OUT, case_results(load('case.json'), common.sources()))


if __name__ == '__main__':
    sys.exit(main())
