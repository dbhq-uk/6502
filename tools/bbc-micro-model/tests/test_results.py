"""results.py: the board half gathers the figures the page quotes from the data
files, as each holds them, and lists the sources the board used."""
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import results  # noqa: E402


def data():
    frame = {
        'board': {'widthMm': 310.0, 'depthMm': 229.4},
        'heldOut': {'rowLengthErrMm': {'median': 0.03, 'n': 7}},
        'heldOutX': {'scaledErrMm': {'median': 0.136, 'n': 7}, 'largest': {'row': 'PL11', 'scaledErrMm': 0.502}},
        'ratioPct': 0.17,
        'verdicts': [{'measure': 'scale y', 'verdict': 'pass'}, {'measure': 'scale x (as revised)', 'verdict': 'between pass and stop'},
                     {'measure': 'scale x (as first written; recorded)', 'verdict': 'STOP'}],
    }
    registration = {'solder': {'holes': 2367, 'heldOutMm': {'median': 0.114, 'p90': 0.258}, 'verdict': 'pass'}}
    copper = {
        'coverage': {'top': 0.32, 'bottom': 0.35, 'print': 0.12}, 'drills': {'n': 2797}, 'drillsInCopper': {'top': 1.0, 'bottom': 1.0},
        'mapPxPerMm': 8, 'print': {'areaMm2': 8624.2, 'recoveredMm2': 700.2},
        'nets': {'chips': 51, 'gnd': {'pins': 51, 'inLargest': 39}, 'vcc': {'pins': 51, 'inLargest': 30}, 'touching': True},
        'againstI5': {'iou': 0.411, 'comparedMm2': 17127.4, 'registration': {'chips': 14, 'points': 56, 'heldOutMm': {'median': 0.363}}},
        'verdicts': {'nets': 'fail', 'againstI5': 'fail', 'i5Registration': 'pass'},
    }
    parts = {
        'model': {
            'ics': [{'ref': 'IC1', 'fitted': True, 'socketed': False}, {'ref': 'IC52', 'fitted': True, 'socketed': True},
                    {'ref': 'IC100', 'fitted': False, 'socketed': True}],
            'connectors': [{'ref': 'SK1'}], 'passives': [{}, {}, {}],
            'heights': {'board': {'measured': False}, 'dip': {'measured': False}, 'socket': {'measured': True}},
            'sources': ['I1', 'D1'],
        },
        'passives': {'byKind': {'axial': 1, 'radial': 2}},
        'manual': {'check': {'maxMm': 97.2, 'medianMm': 1.4, 'limitMm': 10.0,
                             'perPart': {'IC94': {'offMm': 97.2}, 'IC1': {'offMm': 2.4}, 'IC39': {'offMm': 10.5}}}},
        'identities': {'row': {'read': 80, 'populated': 83, 'disagree': ['IC5', 'IC31']}},
        'verdicts': {'manual': 'fail', 'identities': 'fail'},
    }
    sources = [{'id': i, 'url': f'address-of-{i}', 'page': f'page-of-{i}', 'sha256': i * 4,
                'kind': 'scan', 'licence': None, 'author': f'author-of-{i}', 'fetched': '2026-10-13'}
               for i in ('I1', 'D1', 'I6', 'O1')]
    return frame, registration, copper, parts, sources


def test_the_board_figures_are_the_data_files_own():
    out = results.board_results(*data())
    assert out['board'] == {'widthMm': 310.0, 'depthMm': 229.4}
    assert out['scale'] == {'yMedianMm': 0.03, 'yRows': 7, 'xMedianMm': 0.136, 'xRows': 7, 'xLargestMm': 0.502, 'xLargestRow': 'PL11',
                            'xyPct': 0.17, 'xVerdict': 'between pass and stop'}
    assert out['solder'] == {'holes': 2367, 'heldOutMedianMm': 0.114, 'heldOutP90Mm': 0.258, 'verdict': 'pass'}
    assert out['copper']['nets'] == {'chips': 51, 'gndPins': 51, 'gndInLargest': 39, 'vccPins': 51, 'vccInLargest': 30, 'touching': True, 'verdict': 'fail'}
    assert out['copper']['againstI5'] == {'iou': 0.411, 'comparedMm2': 17127.4, 'verdict': 'fail'}
    assert out['copper']['i5Registration'] == {'chips': 14, 'points': 56, 'heldOutMedianMm': 0.363, 'verdict': 'pass'}
    p = out['parts']
    assert (p['ics'], p['fitted'], p['socketed'], p['connectors'], p['passives'], p['axial'], p['radial']) == (3, 2, 1, 1, 3, 1, 2)
    assert p['manual'] == {'maxMm': 97.2, 'medianMm': 1.4, 'limitMm': 10.0, 'over': ['IC39', 'IC94'], 'verdict': 'fail'}
    assert p['identities'] == {'read': 80, 'populated': 83, 'disagree': 2, 'verdict': 'fail'}
    assert (p['heightsTypical'], p['heightsMeasured']) == (1, 1)


def test_the_sources_are_the_parts_modules_then_i6_with_their_addresses():
    out = results.board_results(*data())
    assert [s['id'] for s in out['sources']] == ['I1', 'D1', 'I6']
    assert out['sources'][0] == {'id': 'I1', 'url': 'address-of-I1', 'page': 'page-of-I1', 'sha256': 'I1I1I1I1',
                                 'kind': 'scan', 'licence': None, 'author': 'author-of-I1', 'fetched': '2026-10-13'}


def test_a_source_missing_from_sources_json_stops_the_run():
    frame, registration, copper, parts, sources = data()
    with pytest.raises(SystemExit, match='I6'):
        results.board_results(frame, registration, copper, parts, [s for s in sources if s['id'] != 'I6'])


def test_the_x_scale_verdict_must_be_found_once():
    frame, *rest = data()
    frame['verdicts'] = [v for v in frame['verdicts'] if 'as revised' not in v['measure']]
    with pytest.raises(SystemExit, match='as revised'):
        results.board_results(frame, *rest)


# The case half (plan task 9a): the outside model's figures, from keys.json and
# case.json as each holds them, and the sources case.json names.

def case_data():
    keys = {
        'mapping': {'keys': 74, 'machineKeys': 72, 'verdict': 'pass'},
        'o1': {'n': 50, 'heldOutMm': {'median': 0.395, 'p90': 0.632, 'max': 0.989}, 'over3mm': 0, 'verdict': 'pass'},
        'o3': {'heldOutMm': {'median': 0.205, 'p90': 0.329, 'max': 0.588}, 'legends': {'A': [1, 2], 'F0': [3, 4], 'B': [5, 6]}},
        'verdicts': {'mapping': 'pass', 'o1': 'pass', 'o3': 'pass'},
        'colours': {'agree': 63},
        'keys': [{'name': 'F0', 'colour': 'red'}, {'name': 'Up', 'colour': 'light'}, {'name': 'A', 'colour': 'dark'}, {'name': 'B', 'colour': 'dark'}],
    }
    case = {
        'footprint': {'widthMm': 415.0, 'depthMm': 345.0, 'source': 'D1'},
        'heightMm': 73.0,
        'caseTop': {'scale': 1.0387},
        'k1ToCase': {'impliedScale': 1.0373},
        'widthOnRear': {'mm': 425.0986},
        'frontEdge': {'rawMm': 403.4661, 'rawSeMm': 0.8475, 'verdict': 'not measurable', 'correctedMm': None,
                      'h': {'mm': None, 'seMm': None, 'from': 'none found'}, 'd': {'mm': 6.1848, 'seMm': 2.8027, 'from': 'O4 and O5'}},
        'profileCheck': {'errMm': 0.4496, 'limitMm': 2.0, 'verdict': 'pass', 'fallback': False},
        'rearCheck': {'errMm': {'SK1': 0.7773, 'SK3': -0.3165}, 'limitMm': 2.0, 'n': 2, 'within': 2, 'worstMm': 0.7773, 'verdict': 'pass'},
        'boardInCase': {'x': 104.6405},
        'verdicts': {'frontEdge': 'not measurable', 'profileCheck': 'pass', 'rear': 'pass'},
        'typical': {'feet': {'measured': False}, 'wall': {'measured': False}, 'keyAboveTop': {'measured': False, 'what': 'typical, used only if the side profile\'s check fails'}},
        'sources': ['D1', 'O1'],
    }
    sources = [{'id': i, 'url': f'address-of-{i}', 'page': f'page-of-{i}', 'sha256': i * 4,
                'kind': 'photograph', 'licence': 'Public domain', 'author': f'author-of-{i}', 'fetched': '2026-10-13'}
               for i in ('D1', 'O1', 'O2')]
    return keys, case, sources


def test_the_case_figures_are_the_data_files_own():
    out = results.case_results(*case_data())
    assert out['size'] == {'widthMm': 415.0, 'depthMm': 345.0, 'heightMm': 73.0, 'from': 'D1'}
    # corrected after task 9a's review: the two K1 scales make the case narrower in K1's units, the rear wider
    assert out['width'] == {'caseTopScale': 1.0387, 'cornersScale': 1.0373, 'k1Mm': 399.5, 'rearMm': 425.0986, 'rearRatio': 1.0243,
                            'resolved': False}
    assert out['keys'] == {'keycaps': 74, 'machineKeys': 72, 'mapping': 'pass', 'red': 1, 'light': 1, 'dark': 2, 'coloursAgree': 63}
    assert out['o1'] == {'n': 50, 'medianMm': 0.395, 'p90Mm': 0.632, 'maxMm': 0.989, 'verdict': 'pass'}
    assert out['o3'] == {'n': 3, 'medianMm': 0.205, 'p90Mm': 0.329, 'maxMm': 0.588, 'verdict': 'pass'}
    assert out['frontEdge'] == {'rawMm': 403.4661, 'verdict': 'not measurable', 'hFound': False, 'dMm': 6.1848, 'dSeMm': 2.8027}
    assert out['profileCheck'] == {'errMm': 0.4496, 'limitMm': 2.0, 'verdict': 'pass', 'fallback': False}
    assert out['rear'] == {'n': 2, 'within': 2, 'worstMm': 0.7773, 'worstRef': 'SK1', 'limitMm': 2.0, 'verdict': 'pass', 'boardOffsetMm': 104.6405}
    # the typical values the model draws, not the ones kept for a fallback that did not happen
    assert out['typical'] == ['feet', 'wall']


def test_the_case_sources_are_case_json_s_with_their_authors():
    out = results.case_results(*case_data())
    assert [s['id'] for s in out['sources']] == ['D1', 'O1']
    assert out['sources'][1] == {'id': 'O1', 'url': 'address-of-O1', 'page': 'page-of-O1', 'sha256': 'O1O1O1O1',
                                 'kind': 'photograph', 'licence': 'Public domain', 'author': 'author-of-O1', 'fetched': '2026-10-13',
                                 'used': results.CASE_USED['O1']}
    # every source says what the case took from it; one from Chris's Acorns carries the site's byline
    keys, case, sources = case_data()
    case['sources'] = ['O8-I']
    sources.append({**sources[0], 'id': 'O8-I', 'url': 'a-chrisacorns-address'})
    o8 = results.case_results(keys, case, sources)['sources'][0]
    assert o8['byline'] == "Chris's Acorns" and 'connector' in o8['used'] and 'names' in o8['used']
    sources.append({**sources[0], 'id': 'O9'})
    case['sources'] = ['O9']
    with pytest.raises(SystemExit, match='CASE_USED has nothing for O9'):
        results.case_results(keys, case, sources)


def test_a_fallback_profile_lists_its_typical_heights_and_a_missing_source_stops_the_run():
    keys, case, sources = case_data()
    case['profileCheck'] = {**case['profileCheck'], 'verdict': 'fail', 'fallback': True}
    out = results.case_results(keys, case, sources)
    assert out['profileCheck']['fallback'] is True
    # with the fallback in use, the typical heights it draws are listed too
    assert out['typical'] == ['feet', 'keyAboveTop', 'wall']
    case['sources'] = ['D1', 'O9']
    with pytest.raises(SystemExit, match='O9'):
        results.case_results(keys, case, sources)
