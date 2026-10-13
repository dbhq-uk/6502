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
                'kind': 'scan', 'licence': None, 'fetched': '2026-10-13'} for i in ('I1', 'D1', 'I6', 'O1')]
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
                                 'kind': 'scan', 'licence': None, 'fetched': '2026-10-13'}


def test_a_source_missing_from_sources_json_stops_the_run():
    frame, registration, copper, parts, sources = data()
    with pytest.raises(SystemExit, match='I6'):
        results.board_results(frame, registration, copper, parts, [s for s in sources if s['id'] != 'I6'])


def test_the_x_scale_verdict_must_be_found_once():
    frame, *rest = data()
    frame['verdicts'] = [v for v in frame['verdicts'] if 'as revised' not in v['measure']]
    with pytest.raises(SystemExit, match='as revised'):
        results.board_results(frame, *rest)
