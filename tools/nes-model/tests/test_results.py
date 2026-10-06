"""The board half of results.py, on made-up data files.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q
"""
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import results  # noqa: E402


def made_up():
    frame = {
        'board': {'widthMm': 195.9, 'depthMm': 119.4}, 'statedDpi': 300,
        'heldOutX': {'scaledErrMm': {'median': 0.01, 'max': 0.03}, 'n': 6},
        'heldOutY': {'errPct': {'median': 0.4, 'max': 0.9}, 'footprints': [{}, {}, {}]},
        'ratioPct': 0.17, 'verdicts': [{'check': 'scale x', 'verdict': 'pass'}, {'check': 'scale y', 'verdict': 'pass'}],
    }
    registration = {'solder': {'holes': 500, 'heldOutMm': {'median': 0.09, 'p90': 0.32, 'max': 0.9}, 'verdict': 'pass'}}
    copper = {
        'coverage': {'top': 0.45, 'bottom': 0.31, 'print': 0.09}, 'drillsInCopper': {'top': 0.99, 'bottom': 0.98},
        'drills': {'n': 480}, 'mapPxPerMm': 10,
        'nets': {'chips': 10, 'gnd': {'pins': 10, 'inLargest': 1}, 'vcc': {'pins': 10, 'inLargest': 2}, 'touching': True},
        'verdicts': {'coverage': 'pass', 'drillsInCopper': 'pass', 'nets': 'fail'},
    }
    parts = {
        'model': {'ics': [{}, {}], 'passives': [{}, {}, {}], 'sources': ['I1-front', 'I4'],
                  'heights': {'board': 1.6, 'dip': {'value': 4.0, 'measured': False}, 'P1': {'value': 14.0, 'measured': True}}},
        'kicad': {'maxMm': 0.4, 'medianMm': 0.3},
        'sitsOn': {'limitMm': 2.0, 'ics': {'U1': {'I3': 0.3, 'I4': 0.1, 'I5': 0.5}, 'U2': {'I3': 0.6, 'I4': 0.2, 'I5': 0.0}}},
    }
    sources = [
        {'id': i, 'url': f'address-of-{i}', 'page': f'page-of-{i}', 'sha256': i * 2, 'kind': 'scan'}
        for i in ['I1-front', 'I2', 'I4', 'O1']
    ]
    return frame, registration, copper, parts, sources


def test_every_figure_is_the_data_files_own():
    out = results.board_results(*made_up())
    assert out['board'] == {'widthMm': 195.9, 'depthMm': 119.4, 'dpi': 300}
    assert out['scale'] == {'xMedianMm': 0.01, 'xRows': 6, 'yMedianPct': 0.4, 'yFootprints': 3, 'xyPct': 0.17, 'verdicts': ['pass', 'pass']}
    assert out['solder'] == {'holes': 500, 'heldOutMedianMm': 0.09, 'heldOutP90Mm': 0.32, 'verdict': 'pass'}
    assert out['copper']['nets'] == {'chips': 10, 'gndPins': 10, 'gndInLargest': 1, 'vccPins': 10, 'vccInLargest': 2, 'touching': True, 'verdict': 'fail'}
    assert (out['copper']['top'], out['copper']['bottom'], out['copper']['drills']) == (0.45, 0.31, 480)
    # the worst of the CPU-07's two photographs for NTSC, the CPU-11's for PAL
    assert (out['parts']['ntscWorstMm'], out['parts']['palWorstMm']) == (0.5, 0.6)
    assert (out['parts']['ics'], out['parts']['passives'], out['parts']['heightsTypical'], out['parts']['heightsMeasured']) == (2, 3, 1, 1)


def test_the_sources_are_the_parts_modules_and_the_redrawing_with_their_addresses():
    out = results.board_results(*made_up())
    assert [s['id'] for s in out['sources']] == ['I1-front', 'I4', 'I2']
    assert out['sources'][0] == {'id': 'I1-front', 'url': 'address-of-I1-front', 'page': 'page-of-I1-front', 'sha256': 'I1-frontI1-front', 'kind': 'scan'}


def test_a_source_missing_from_sources_json_stops_it():
    frame, registration, copper, parts, sources = made_up()
    parts['model']['sources'].append('I9')
    with pytest.raises(SystemExit, match='I9'):
        results.board_results(frame, registration, copper, parts, sources)


# --- the case half (plan task 9) ---------------------------------------------------

def made_up_case():
    case = {
        'footprint': {'widthMm': 254.0, 'depthMm': 203.2, 'notNintendos': True, 'widthSource': 'published, not Nintendo\'s; good to about 3 per cent'},
        'heightMm': 88.9, 'heightFrom': 'published',
        'model': {'CASE': {'feet': 3.96}, 'PROFILE': {'points': [{'inset': 0.0, 'z': 92.86}, {'inset': 16.0, 'z': 3.96}]}},
        'spike': {'caseDepth': 'pass', 'caseHeight': 'pass', 'palFront': 'between pass and stop'},
        'widthCrossCheck': {'againstPublishedMidpointPct': -1.96},
        'profileCheck': {'endsMm': {'min': 12.86, 'max': 17.71}, 'errMm': 0.71, 'limitMm': 2.0, 'passes': True, 'fallbackUsed': False, 'endsWhat': 'the average, good to about 2.5 mm. The rest'},
        'rear': {
            'ntsc': [{'face': 'rear', 'checked': True}, {'face': 'right', 'checked': False}],
            'pal': [{'face': 'rear', 'checked': False}],
        },
        'rearCheck': {'checked': 3, 'within': 1, 'limitMm': 2.0, 'worstMm': 5.16, 'uncertaintyMm': {'placesUsed': 1.3, 'boardMiss': 5.16}, 'words': 'the check failed as measured'},
        'boardInCase': {'uncertaintyMm': 2.0, 'from': 'O9; the shells the moulding [inferring]'},
        'rearWindow': {'setBackReadMm': 4.39},
        'buttons': [{'travelMm': 3.0, 'travelFrom': 'typical'}, {'travelMm': 3.0, 'travelFrom': 'typical'}],
        'powerLatch': {'seen': False},
        'palDifferences': ['a', 'b'],
        'sources': ['O1', 'O4'],
    }
    sources = [
        {'id': 'O1', 'url': 'address-of-O1', 'page': 'page-of-O1', 'sha256': 'aa', 'kind': 'drawing'},
        {'id': 'O4', 'url': 'address-of-O4', 'page': 'page-of-O4', 'sha256': 'bb', 'kind': 'photograph', 'focalLengthMm': 20},
        {'id': 'O10', 'url': 'address-of-O10', 'page': 'page-of-O10', 'sha256': 'cc', 'kind': 'photograph', 'focalLengthMm': 20},
    ]
    return case, sources


def test_the_case_figures_are_case_jsons_own():
    out = results.case_results(*made_up_case())
    assert out['size'] == {'widthMm': 254.0, 'depthMm': 203.2, 'heightMm': 88.9, 'from': 'published', 'notNintendos': True, 'goodToPct': 3.0, 'feetMm': 3.96}
    assert out['profile'] == {'insetMm': 16.0, 'endsMm': {'min': 12.86, 'max': 17.71}, 'heldOutMm': 0.71, 'limitMm': 2.0, 'passes': True, 'fallbackUsed': False, 'goodToMm': 2.5}
    assert out['rear'] == {'checked': 3, 'within': 1, 'limitMm': 2.0, 'worstMm': 5.16, 'placesUsedMm': 1.3, 'boardMissMm': 5.16, 'words': 'the check failed as measured', 'avChecked': False, 'palChecked': False, 'windowSetBackMm': 4.39}
    assert out['boardInCase'] == {'uncertaintyMm': 2.0, 'sharedMoulding': True}
    assert out['buttons'] == {'travelMm': 3.0, 'travelFrom': 'typical'}
    assert (out['powerLatchSeen'], out['palLensMm'], out['palDifferences']) == (False, 20, 2)
    assert [s['id'] for s in out['sources']] == ['O1', 'O4']
    assert out['sources'][1] == {'id': 'O4', 'url': 'address-of-O4', 'page': 'page-of-O4', 'sha256': 'bb', 'kind': 'photograph'}


def test_the_case_half_stops_on_words_it_cannot_read_or_a_lens_it_does_not_have():
    case, sources = made_up_case()
    case['profileCheck']['endsWhat'] = 'the average'
    with pytest.raises(SystemExit, match='good to about'):
        results.case_results(case, sources)
    case, sources = made_up_case()
    del sources[2]['focalLengthMm']
    with pytest.raises(SystemExit, match='focal length'):
        results.case_results(case, sources)
    case, sources = made_up_case()
    case['sources'].append('O9')
    with pytest.raises(SystemExit, match='O9'):
        results.case_results(case, sources)


def test_the_shared_moulding_is_read_from_its_own_tag_and_nothing_else():
    assert results.shared_moulding('O9; the PAL and NTSC boards share the layout (task 0) and the shells the moulding [inferring]') is True
    assert results.shared_moulding('O9, through the camera its XMP records') is False
    with pytest.raises(SystemExit, match='moulding'):
        results.shared_moulding('O9; the shells do not share the moulding')
