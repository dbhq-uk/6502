"""Gathers the measurements the NES's page quotes into committed results files,
so the page never carries a figure typed by hand (AGENTS.md rule 5). This is
the board half, for the inside model: site/src/data/nes-famicom-board-model.json.
The case half, for the outside model, comes with it (plan task 9).

    /tmp/nesvenv/bin/python tools/nes-model/results.py

Reads data/frame.json, data/registration.json, data/copper.json,
data/parts.json and data/sources.json, all written or kept by the other
scripts here, and writes only the figures the page quotes, each as the data
file holds it. It reads no input and fetches nothing.
"""
import json
import sys

import common

SITE_DATA = common.REPO / 'site' / 'src' / 'data'
BOARD_OUT = SITE_DATA / 'nes-famicom-board-model.json'


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


def main():
    out = board_results(load('frame.json'), load('registration.json'), load('copper.json'), load('parts.json'), common.sources())
    text = json.dumps(common.plain(out), indent=1, sort_keys=True, ensure_ascii=False)
    BOARD_OUT.write_text(text + '\n', encoding='utf8')
    print(f'wrote {BOARD_OUT.relative_to(common.REPO)}')


if __name__ == '__main__':
    sys.exit(main())
