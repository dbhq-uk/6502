"""Task 8: O1, Daniel Beardsmore's photograph of the machine from above,
registered on the case top's own four corners to the Service Manual's 415 by
345 mm, and what is read off the top through it.

    BBC_MODEL_INPUTS=<folder> python case_register.py    # prints what it reads (case_parts.py writes case.json)

The case frame: millimetres from the case's left rear corner, x to the right,
y towards the front, as seen from above (z, up from the table, is
case_parts.py's).

1. The case top. A homography from O1's pixels to the case frame on the
   top's four corners (case-marks.json "o1.caseTop": the front two are task
   0's marks, the rear two read for this task), taken to be (0, 0), (415, 0),
   (0, 345) and (415, 345), D1's width and depth. Four points fix a
   homography exactly, so nothing about it is held out: it is right for
   points on the plane of the top, and nowhere else.

2. The plan's recorded row "the case top's plan size from published
   dimensions against O1 registered on the case top's four corners": task
   0's 50 key marks read through that registration and a similarity fitted
   from K1's centres to them. Its scale is how big the keyboard reads on the
   plane of the top (1 if the key tops were in that plane and the case were
   415 by 345 mm); recorded, not judged, with the fit's residuals. The key
   tops are not in that plane, which is why it is not judged.

3. Read through the registration, so on the top's plane: the keyboard's
   opening (its four corners), the badge strip (the opening's rear edge and
   the strip's front edge at each end) and the box of its words. The strip
   and its words lie a little below the top, so they are placed to a few
   millimetres. The three lights, read the same way, are recorded only: they
   lie on the panel, below the top, and case_parts.py places them by
   triangulation (case_profile.py).
"""
import sys

import numpy as np

import common
import keys as keyslib
import spike

CASE_W, CASE_D, CASE_H = 415.0, 345.0, 73.0      # D1, PDF page 109, "3.11 Dimensions"
THRESHOLDS = {'caseTop': 'O1 registered on the case top\'s four corners to 415 by 345 mm; the keys\' scale on that plane recorded, not judged'}


def case_top():
    m = keyslib.case_marks()['o1']['caseTop']
    src = np.array([m['rearLeft'], m['rearRight'], m['frontLeft'], m['frontRight']], float)
    dst = np.array([[0, 0], [CASE_W, 0], [0, CASE_D], [CASE_W, CASE_D]], float)
    return np.array(common._fit('homography', src, dst)['H'])


def to_case(H, pts):
    return common.transform('homography', {'H': H}, pts)


def keys_on_top(H):
    """Task 0's key marks on O1 through the case top's registration, and the similarity from K1 to them."""
    k1 = common.switch_centres(common.original('K1'))
    m = spike.marks()['keys']
    names = [k['key'] for k in m['marks']]
    on_top = to_case(H, [k['at'] for k in m['marks']])
    at = np.array([k1[k['k1']]['centre'] for k in m['marks']], float)
    sim = common._fit('similarity', at, on_top)
    res = np.hypot(*(common.transform('similarity', sim, at) - on_top).T)
    return {'n': len(names), 'scale': sim['scale'], 'rotationDeg': sim['rotationDeg'], 'matrix': sim['matrix'],
            'residualMm': common.stats(res),
            'what': ('Task 0\'s 50 key marks on O1 read through the case top\'s registration, and the similarity from K1\'s key centres to '
                     'them: its scale is how big the keyboard reads on the plane of the top, 1 if the key tops were in that plane and the '
                     'case were 415 by 345 mm. Recorded, not judged: the key tops are not in that plane')}


def read_top(H):
    o1 = keyslib.case_marks()['o1']
    op = o1['opening']
    opening = to_case(H, [op['rearLeft'], op['rearRight'], op['frontRight'], op['frontLeft']])
    b = o1['badge']
    strip = to_case(H, [op['rearLeft'], op['rearRight'], b['frontEdgeRight'], b['frontEdgeLeft']])
    w = b['wordsBox']
    words = to_case(H, [[w[0], w[1]], [w[2], w[3]]])
    leds = to_case(H, o1['leds']['points'])
    return {
        'opening': opening.tolist(),
        'badgeStrip': strip.tolist(),
        'badgeWords': {'box': [*words[0], *words[1]], 'words': b['words']},
        'ledsOnTop': {'order': o1['leds']['order'], 'points': leds.tolist(),
                      'what': 'the three lights read through the case top\'s registration: recorded only, being below the top'},
    }


def measure():
    H = case_top()
    kt = keys_on_top(H)
    top = read_top(H)
    print(f"case top: O1 on its four corners to {CASE_W:g} by {CASE_D:g} mm; the keys on that plane: scale {kt['scale']:.4f}, "
          f"turn {kt['rotationDeg']:.2f} deg, residual median {kt['residualMm']['median']:.2f} mm (recorded)")
    xs = [p[0] for p in top['opening']]
    ys = [p[1] for p in top['opening']]
    print(f"opening: x {min(xs):.1f} to {max(xs):.1f}, y {min(ys):.1f} to {max(ys):.1f} mm")
    return {'H': H.tolist(), 'keysOnTop': kt, **top, 'thresholds': THRESHOLDS}


if __name__ == '__main__':
    measure()
    sys.exit(0)
