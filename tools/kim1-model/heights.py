"""Measures how high the KIM-1's parts stand, from two photographs of the same
board taken from different places (bolo-oblique, from a low corner, and
bolo-end, from above the other end), by triangulation.

    python heights.py

Both photographs are already registered to the board (register.py), which
gives, for each, the homography G from the board's plane (z = 0, the top face)
to the image. With the camera's focal length f, in pixels, and its principal
point (neither photograph is cropped: both are the camera's full 5616 by
3744), G is K [r1 r2 t], so each camera's pose follows,
and a point marked in both photographs is the place where the two rays from
the cameras through it come nearest each other. Its z is its height above the
board's top face.

f is not taken on trust. The EXIF says a 100 mm lens, 15,154 pixels, but a
lens focused close has a longer effective focal length. So one camera is
fitted to both registrations at once (the same body and lens, a minute
apart): its focal length, its principal point and a pose per photograph,
by least squares (fit_cameras). Every height is given with a range: the same
point with the camera refitted with its principal point held at the frame's
centre, and with each mark moved by 3 pixels in eight directions.

The marks are in data/height-marks.json, in the originals' pixels, marked by
hand on crops of each photograph with a pixel scale; each says what the point
is. Writes data/heights.json.
"""
import json
import os

import numpy as np

import common

W, H = 5616, 3744
VIEWS = ('bolo-oblique', 'bolo-end')


def fit_cameras(reg, free_centre=True):
    """One pinhole camera for both photographs (the same body and lens, a
    minute apart): focal length f, and the principal point when free_centre
    (else the frame's centre), with a pose for each photograph, fitted so that
    the board's plane projects as each registration says it does, over a grid
    of 180 points on the body. Returns the two 3 by 4 projections, f, the
    principal point and the fit's RMS error in pixels."""
    from scipy.optimize import least_squares
    from scipy.spatial.transform import Rotation as Rot
    xs, ys = np.meshgrid(np.linspace(5, 195, 12), np.linspace(5, 268, 15))
    pts = np.stack([xs.ravel(), ys.ravel()], 1)
    plane = np.c_[pts, np.zeros(len(pts))]
    targets = []
    for v in VIEWS:
        G = np.linalg.inv(np.array(reg[v]['H']))
        p = G @ np.c_[pts, np.ones(len(pts))].T
        targets.append((p[:2] / p[2]).T)

    def unpack(q):
        f, cx, cy = (q[0], q[1], q[2]) if free_centre else (q[0], W / 2, H / 2)
        o = 3 if free_centre else 1
        return f, cx, cy, [(Rot.from_rotvec(q[o + 6 * i:o + 3 + 6 * i]).as_matrix(), q[o + 3 + 6 * i:o + 6 + 6 * i]) for i in range(len(VIEWS))]

    def residuals(q):
        f, cx, cy, cams = unpack(q)
        out = []
        for (R, t), tg in zip(cams, targets):
            c = plane @ R.T + t
            out.append((np.stack([f * c[:, 0] / c[:, 2] + cx, f * c[:, 1] / c[:, 2] + cy], 1) - tg).ravel())
        return np.concatenate(out)

    f0 = 15154.4  # the EXIF's 100 mm, as the start
    q0 = [f0, W / 2, H / 2] if free_centre else [f0]
    for v in VIEWS:
        K = np.array([[f0, 0, W / 2], [0, f0, H / 2], [0, 0, 1.0]])
        M = np.linalg.inv(K) @ np.linalg.inv(np.array(reg[v]['H']))
        M /= (np.linalg.norm(M[:, 0]) + np.linalg.norm(M[:, 1])) / 2
        if M[2, 2] < 0:
            M = -M
        U, _, Vt = np.linalg.svd(np.stack([M[:, 0], M[:, 1], np.cross(M[:, 0], M[:, 1])], 1))
        q0 += list(Rot.from_matrix(U @ Vt).as_rotvec()) + list(M[:, 2])
    r = least_squares(residuals, q0, x_scale='jac')
    f, cx, cy, cams = unpack(r.x)
    K = np.array([[f, 0, cx], [0, f, cy], [0, 0, 1.0]])
    return [K @ np.column_stack([R, t]) for R, t in cams], float(f), (float(cx), float(cy)), float(np.sqrt(np.mean(r.fun ** 2)))


def triangulate(Ps, uvs):
    A = []
    for P, (u, v) in zip(Ps, uvs):
        A.append(u * P[2] - P[0])
        A.append(v * P[2] - P[1])
    _, _, Vt = np.linalg.svd(np.array(A))
    X = Vt[-1]
    X = X[:3] / X[3]
    err = []
    for P, (u, v) in zip(Ps, uvs):
        p = P @ np.append(X, 1)
        err.append(np.hypot(p[0] / p[2] - u, p[1] / p[2] - v))
    return X, max(err)


def main():
    reg = json.load(open(os.path.join(common.DATA, 'registration.json'), encoding='utf8'))
    marks = json.load(open(os.path.join(common.DATA, 'height-marks.json'), encoding='utf8'))['marks']
    models = {'free': fit_cameras(reg, True), 'centred': fit_cameras(reg, False)}
    for name, (_, f, c, rms) in models.items():
        print(f'camera, principal point {name}: f {f:.0f} pixels ({f * 25.4 / 3849.2:.1f} mm), principal point {c[0]:.0f}, {c[1]:.0f}; the plane fits to {rms:.2f} pixels RMS')
    out = []
    for m in marks:
        uvs = [m[v] for v in VIEWS]
        heights = []
        for name, (Ps, *_rest) in models.items():
            X, err = triangulate(Ps, uvs)
            heights.append(-X[2])
            if name == 'free':
                best, best_err = X, err
                for dx, dy in [(3, 0), (-3, 0), (0, 3), (0, -3), (2, 2), (-2, -2), (2, -2), (-2, 2)]:
                    for k in range(len(VIEWS)):
                        moved = [list(p) for p in uvs]
                        moved[k] = [moved[k][0] + dx, moved[k][1] + dy]
                        heights.append(-triangulate(Ps, moved)[0][2])
        z = -best[2]
        entry = {'name': m['name'], 'part': m.get('part'), 'xy': [round(float(best[0]), 2), round(float(best[1]), 2)], 'height': round(float(z), 2), 'range': [round(float(min(heights)), 2), round(float(max(heights)), 2)], 'raysMissPx': round(float(best_err), 1)}
        out.append(entry)
        print(f"{m['name']}: at {best[0]:.1f}, {best[1]:.1f} mm, {z:.2f} mm above the board (from {min(heights):.2f} to {max(heights):.2f}); the rays miss by {best_err:.1f} px")
    free = models['free']
    common.save_json(os.path.join(common.DATA, 'heights.json'), {
        'about': 'Heights above the board\'s top face, in mm, by triangulation between bolo-oblique and bolo-end. The range covers the camera fitted with its principal point free and at the frame\'s centre, and every mark moved by 3 pixels. Made by heights.py from height-marks.json.',
        'camera': {'focalPx': round(free[1]), 'principalPoint': [round(v) for v in free[2]], 'planeRmsPx': round(free[3], 2), 'centredFocalPx': round(models['centred'][1]), 'centredPlaneRmsPx': round(models['centred'][3], 2)},
        'points': out,
    })


if __name__ == '__main__':
    main()
