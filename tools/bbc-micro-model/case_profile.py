"""Task 8: one camera fitted to O4 and O5, Stuart Brady's two photographs of the
same BBC Micro taken 80 seconds apart with one camera (an Olympus C760UZ at
6.3 mm, by their Exif), and what it triangulates: the key plane, the top of
the case at the front of the keyboard's opening, and the three lights.

    BBC_MODEL_INPUTS=<folder> python case_profile.py           # prints the fit and the check (case_parts.py writes case.json)
    BBC_MODEL_INPUTS=<folder> python case_profile.py --look    # the fit and the legends drawn in out/; the check point is not computed

The frame is K1's (keys.py): millimetres, x to the right and y towards the
front as seen from above, and z up from the plane of the key tops (the
legends), so the key plane is z = 0.

1. The points. In each photograph, the legends of the 26 letter keys and the
   ten function keys are found as keys.py finds them on O3 (the key A's too,
   but A is the check, below, and nothing else uses it): each is a known
   point, K1's place at z = 0. Points marked by hand in both photographs
   (case-marks.json "o4o5": the opening's two front corners and the three
   lights) are unknowns. O4 and O5 have had their backgrounds painted white,
   and the paint cuts across some of the case's corners, so no point on the
   case's outline is used.

2. The camera. Both photographs are crops of the camera's 2048 by 1536
   frame (the Exif's size), each by an amount no record gives, so the
   principal point is free in each; the focal length, in pixels, is one for
   both (the same lens at the same 6.3 mm, and neither photograph resized,
   which is assumed: if one were, the check below would find it). A pose for
   each. Fitted by least squares on the reprojection of every point, with a
   soft L1 loss of scale 2 px (LOSS_SCALE_PX) so that a legend found wrongly
   in glare does not pull the fit, starting from a homography's pose with
   F0_PX, which only starts it. No lens distortion is modelled.

3. The check (the plan's row "Side profile, the check point": within 2.0 mm
   of where it is measured), fixed before any camera was fitted: the key A's
   legend, left out of the fit, triangulated with the two cameras (the point
   whose reprojection in both is least), against K1's place for A on the key
   plane. Pass if the distance is at most 2.0 mm. If it fails, the profile
   falls back to the typical (case_parts.py), as the plan says.

4. The figures. Each unknown point's place, with a standard error from a
   jackknife over the known points (each legend left out in turn, the whole
   fit redone). The top of the case at the front of the opening is the mean
   height of its two front corners; the case's front edge is taken to be at
   that height (the top is flat from the opening to the front edge, which
   the photographs show and nothing here measures), so d, the front edge's
   depth below the key plane, is minus that mean, its standard error the
   jackknife's and half the two corners' difference in quadrature.
"""
import argparse
import math
import sys

import numpy as np

import common
import keys as keyslib

F0_PX = 2240.0            # where the fit starts: 6.3 mm on a 1/2.5 inch sensor 2048 pixels across; the fit sets f
LOSS_SCALE_PX = 2.0
CHECK_PASS_MM = 2.0
VIEWS = ('O4', 'O5')
POINTS = ('openingFrontLeft', 'openingFrontRight', 'ledCassetteMotor', 'ledCapsLock', 'ledShiftLock')
THRESHOLDS = {
    'checkPoint': 'the key A\'s legend, left out of the camera fit and triangulated from O4 and O5, within 2.0 mm of K1\'s place for A on the key plane; else the profile is typical',
}


def rot(v):
    from scipy.spatial.transform import Rotation
    return Rotation.from_rotvec(v).as_matrix()


def project(f, c, R, t, X):
    """Pinhole projection of world points X (N, 3), z up, through a camera x_cam = R X + t."""
    P = X @ R.T + t
    return np.c_[f * P[:, 0] / P[:, 2] + c[0], f * P[:, 1] / P[:, 2] + c[1]]


def pose_from_homography(H_img_to_plane, f, c):
    """A camera pose from a plane's homography (plane z = 0 to the picture) and the camera's f and principal point."""
    G = np.linalg.inv(np.array(H_img_to_plane, float))
    K = np.array([[f, 0, c[0]], [0, f, c[1]], [0, 0, 1.0]])
    M = np.linalg.inv(K) @ G
    lam = 1 / math.sqrt(np.linalg.norm(M[:, 0]) * np.linalg.norm(M[:, 1]))
    if M[2, 2] * lam < 0:
        lam = -lam
    r1, r2, t = M[:, 0] * lam, M[:, 1] * lam, M[:, 2] * lam
    U, _, Vt = np.linalg.svd(np.c_[r1, r2, np.cross(r1, r2)])
    R = U @ Vt
    from scipy.spatial.transform import Rotation
    return Rotation.from_matrix(R).as_rotvec(), t


def triangulate(cams, pts):
    """The world point whose reprojection error in every camera is least, from a linear start.
    cams: [(f, c, R, t)], pts: [(x, y)] one per camera."""
    from scipy.optimize import least_squares
    A = []
    for (f, c, R, t), (x, y) in zip(cams, pts):
        K = np.array([[f, 0, c[0]], [0, f, c[1]], [0, 0, 1.0]])
        P = K @ np.c_[R, t]
        A += [x * P[2] - P[0], y * P[2] - P[1]]
    X = np.linalg.svd(np.array(A))[2][-1]
    X = X[:3] / X[3]
    r = least_squares(lambda X: np.concatenate([project(f, c, R, t, X[None])[0] - p for (f, c, R, t), p in zip(cams, pts)]), X)
    return r.x


class Bundle:
    """One focal length, a principal point and a pose per view, and the unknown points."""

    def __init__(self, known, unknown, sizes):
        self.known = known          # {view: (world (N, 3), picture (N, 2))}
        self.unknown = unknown      # {name: {view: (x, y)}}
        self.sizes = sizes          # {view: (width, height)}
        self.names = list(unknown)

    def unpack(self, q):
        f = q[0]
        cams = {}
        for i, v in enumerate(VIEWS):
            o = 1 + 8 * i
            cams[v] = (f, q[o:o + 2], rot(q[o + 2:o + 5]), q[o + 5:o + 8])
        X = q[1 + 8 * len(VIEWS):].reshape(-1, 3)
        return cams, X

    def residuals(self, q):
        cams, X = self.unpack(q)
        out = []
        for v in VIEWS:
            W, p = self.known[v]
            out.append((project(*cams[v], W) - p).ravel())
            for j, n in enumerate(self.names):
                out.append((project(*cams[v], X[j:j + 1])[0] - np.array(self.unknown[n][v])))
        return np.concatenate(out)

    def start(self):
        q = [F0_PX]
        cams = {}
        for v in VIEWS:
            W, p = self.known[v]
            c = np.array(self.sizes[v], float) / 2
            H = common._fit('homography', p, W[:, :2])['H']
            rv, t = pose_from_homography(H, F0_PX, c)
            q += list(c) + list(rv) + list(t)
            cams[v] = (F0_PX, c, rot(rv), t)
        for n in self.names:
            q += list(triangulate([cams[v] for v in VIEWS], [self.unknown[n][v] for v in VIEWS]))
        return np.array(q, float)

    def fit(self, q0=None):
        from scipy.optimize import least_squares
        q0 = self.start() if q0 is None else q0
        r = least_squares(self.residuals, q0, loss='soft_l1', f_scale=LOSS_SCALE_PX, x_scale='jac', max_nfev=20000)
        return r.x


def inputs():
    """The known points (legends, A left out) and the unknown points (marked by hand), for both views."""
    k1 = keyslib.k1_keys()
    at = {k['name']: (k['x'], k['y']) for k in k1}
    marks = keyslib.case_marks()
    check = marks['checkKey']['key']
    names = [n for n in keyslib.LEGEND_KEYS]
    found = {v: keyslib.find_legends(v, marks['seeds'][v], k1, names=names, skip=(check,)) for v in VIEWS}
    known = {}
    for v in VIEWS:
        use = [n for n in names if n in found[v] and n != check]
        known[v] = (np.array([[*at[n], 0.0] for n in use], float), np.array([found[v][n] for n in use], float), use)
    unknown = {n: {v: tuple(marks['o4o5']['points'][n][v]) for v in VIEWS} for n in POINTS}
    sizes = {v: (common.source(v)['width'], common.source(v)['height']) for v in VIEWS}
    return k1, at, check, found, known, unknown, sizes


def solve(known, unknown, sizes, drop=None):
    """The bundle, with one legend (name) left out of both views if `drop` is given."""
    kn = {}
    for v in VIEWS:
        W, p, use = known[v]
        keep = [i for i, n in enumerate(use) if n != drop]
        kn[v] = (W[keep], p[keep])
    b = Bundle(kn, unknown, sizes)
    q = b.fit()
    return b, q


def measure(look=False):
    k1, at, check, found, known, unknown, sizes = inputs()
    b, q = solve(known, unknown, sizes)
    cams, X = b.unpack(q)
    res = b.residuals(q).reshape(-1, 2)
    err = np.hypot(res[:, 0], res[:, 1])
    nk = {v: len(known[v][2]) for v in VIEWS}
    keyErr = np.concatenate([err[:nk['O4']], err[nk['O4'] + len(POINTS):nk['O4'] + len(POINTS) + nk['O5']]])
    ptErr = np.concatenate([err[nk['O4']:nk['O4'] + len(POINTS)], err[nk['O4'] + len(POINTS) + nk['O5']:]])
    fit = {'fPx': q[0], 'principalPoint': {v: cams[v][1].tolist() for v in VIEWS},
           'legends': nk, 'reprojectionPx': {'legends': common.stats(keyErr), 'marked': common.stats(ptErr)},
           'cameraHeightMm': {v: float((cams[v][2].T @ cams[v][3])[2]) for v in VIEWS}}
    print(f"cameras: f {q[0]:.1f} px; principal points O4 {cams['O4'][1].round(1)}, O5 {cams['O5'][1].round(1)}; "
          f"legends {nk}; reprojection legends median {np.median(keyErr):.2f} px (max {keyErr.max():.2f}), marked points median {np.median(ptErr):.2f} px (max {ptErr.max():.2f})")
    points = {n: X[j] for j, n in enumerate(b.names)}
    if look:
        draw(found, cams, points)
        for n, p in points.items():
            print(f'{n}: {(p * np.array([1, 1, -1])).round(2)} (z up)')
        return None
    # jackknife over the legends (each in turn, from both views)
    every = sorted(set(known['O4'][2]) | set(known['O5'][2]))
    jack = []
    for n in every:
        bj, qj = solve(known, unknown, sizes, drop=n)
        jack.append(bj.unpack(qj)[1])
    jack = np.array(jack)
    m = len(jack)
    se = np.sqrt((m - 1) / m * ((jack - jack.mean(0)) ** 2).sum(0))
    # the fit's third axis points down, into the key plane (a homography's r1 x r2 with x to the right and y towards the
    # front); every height written is up, so z is negated here
    pts = {n: {'x': X[j, 0], 'y': X[j, 1], 'z': -X[j, 2], 'se': se[j].tolist()} for j, n in enumerate(b.names)}
    zf = [-X[b.names.index('openingFrontLeft'), 2], -X[b.names.index('openingFrontRight'), 2]]
    zse = [se[b.names.index('openingFrontLeft'), 2], se[b.names.index('openingFrontRight'), 2]]
    dmm = -float(np.mean(zf))
    dse = math.sqrt((math.sqrt(zse[0] ** 2 + zse[1] ** 2) / 2) ** 2 + (abs(zf[0] - zf[1]) / 2) ** 2)
    # the check: A, left out of everything above
    A = triangulate([cams[v] for v in VIEWS], [found[v][check] for v in VIEWS])
    err = float(np.linalg.norm(A - np.array([*at[check], 0.0])))
    verdict = 'pass' if err <= CHECK_PASS_MM else 'fail'
    A = A * np.array([1, 1, -1])
    print(f"check point {check}: triangulated {A.round(2)}, K1 {np.round(at[check], 2)} at z 0: {err:.2f} mm (limit {CHECK_PASS_MM}): {verdict}")
    print(f"the top at the opening's front: z {np.round(zf, 2)} mm (se {np.round(zse, 2)}); d {dmm:.2f} mm, se {dse:.2f}")
    for n in POINTS:
        print(f"{n}: {(X[b.names.index(n)] * np.array([1, 1, -1])).round(2)} se {np.round(se[b.names.index(n)], 2)}")
    return {
        'fit': fit,
        'points': pts,
        'check': {'key': check, 'triangulated': A.tolist(), 'k1': [*at[check], 0.0], 'errMm': err, 'limitMm': CHECK_PASS_MM, 'verdict': verdict},
        'topAtOpening': {'z': zf, 'se': zse},
        'd': {'mm': dmm, 'seMm': dse, 'from': 'O4 and O5 triangulated: minus the mean height of the opening\'s two front corners above the key plane, the case\'s front edge taken to be at that height'},
        'legends': {v: {n: list(found[v][n]) for n in sorted(found[v])} for v in VIEWS},
        'jackknife': m,
        'thresholds': THRESHOLDS,
    }


def draw(found, cams, points):
    import cv2
    from PIL import Image
    common.OUT.mkdir(exist_ok=True)
    for v in VIEWS:
        img = common.read_rgb(v).copy()
        for n, (x, y) in found[v].items():
            cv2.drawMarker(img, (int(round(x)), int(round(y))), (0, 255, 0), cv2.MARKER_CROSS, 12, 1)
        for n, p in points.items():
            x, y = project(*cams[v], p[None])[0]
            cv2.drawMarker(img, (int(round(x)), int(round(y))), (255, 0, 255), cv2.MARKER_TILTED_CROSS, 16, 2)
        Image.fromarray(img).save(common.OUT / f'case-{v.lower()}.png')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--look', action='store_true', help='fit the cameras and draw the legends and points in out/; the check point is not computed')
    args = ap.parse_args()
    out = measure(look=args.look)
    return 0 if out is None or out['check']['verdict'] == 'pass' else 3


if __name__ == '__main__':
    sys.exit(main())
