"""Fuses the traced photographs into the model's track map, and measures how
well the map agrees with what it can be checked against.

    python fuse.py

Reads out/trace-<id>.png (trace.py) and data/kicad-copper-*.png, and writes:

- site/src/assets/tracks/kim-1.webp: the map, lossless WebP, covering the
  board's body edge to edge at 8 pixels to the millimetre. Three channels,
  each white for copper, kept apart because they come from different places
  under different licences:
    red    the top face, traced from the photographs;
    green  the top face where no photograph shows the board (under the chips,
           the display and the keypad, which sit in the same place on every
           board), from the KiCad replica;
    blue   the underside, traced from its photograph.
- data/agreement.json: the before and after figures.
- out/fused-*.png and out/overlay-*.png for looking at.

The top face, pixel by pixel, over the three photographs of it:
- seen by two or three: the majority; a tie (one of two) goes to the
  better source, ranked by native resolution over held-out registration
  error (register.py), and every pixel where the sources disagree is counted;
- seen by one: that one;
- seen by none: the replica, in the green channel.
Before the vote, copper wider than 2.5 mm both ways in one photograph counts
only where another photograph has copper as well: that is a pour, or a light
part that passed for one.
"""
import json
import os

import cv2
import numpy as np
from PIL import Image

import common

R = 12          # the traces' pixels per mm
MAP_R = 8       # the map's
TOP = ['bolo-top', 'bolo-end', 'rev-b-front']
BOTTOM = 'rev-b-back'
# The map the model had before this work, read from git at the commit that last
# changed it, so the before figure is measured on the same ground as the after.
OLD_MAP_COMMIT = 'e0fd715'
OLD_MAP = 'site/src/assets/tracks/kim-1.webp'
OLD_MAP_SHA = 'bfec20d30c433f4b54d817f5cec2d2b0138fae5682e82f6a1415880141180e54'

# Six zones of the body, for the per-region figures: [x0, y0, x1, y1] in mm.
ZONES = {
    'top left: the bus and the three 40-pin chips': (0, 0, 100, 91),
    'top right: memory, crystal and name': (100, 0, 200, 91),
    'middle left: the bus between the connectors': (0, 91, 100, 182),
    'middle right: display drivers and display': (100, 91, 200, 182),
    'bottom left: cassette, teletype and timer': (0, 182, 100, 273),
    'bottom right: the keypad': (100, 182, 200, 273),
}


def load_trace(sid):
    t = np.asarray(Image.open(os.path.join(common.OUT, f'trace-{sid}.png')).convert('RGB'))
    return t[..., 0] > 127, t[..., 1] > 127


def replica(layer, R_):
    full = np.asarray(Image.open(os.path.join(common.DATA, f'kicad-copper-{layer}.png')).convert('L')).astype(np.float32) / 255
    return cv2.resize(full, common.frame_size(R_), interpolation=cv2.INTER_LINEAR) > 0.5


def body_slice(R_):
    x0, y0, _, _ = common.FRAME
    c0, r0 = int(round(-x0 * R_)), int(round(-y0 * R_))
    return slice(r0, r0 + int(round(common.BODY[1] * R_))), slice(c0, c0 + int(round(common.BODY[0] * R_)))


def clean(mask):
    """Specks under 0.3 square mm dropped, holes under 0.6 filled."""
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), connectivity=8)
    keep = stats[:, cv2.CC_STAT_AREA] >= 0.3 * R * R
    keep[0] = False
    out = keep[labels]
    n, labels, stats, _ = cv2.connectedComponentsWithStats((~out).astype(np.uint8), connectivity=4)
    small = stats[:, cv2.CC_STAT_AREA] < 0.6 * R * R
    small[0] = False
    return out | small[labels]


def rank(reg, sid):
    s = common.sources()[sid]
    H = np.array(reg[sid]['H'])
    # Native resolution: the length of the body's top edge in the original's pixels, over 200 mm.
    Hi = np.linalg.inv(H)
    p = [Hi @ np.array([x, 0, 1.0]) for x in (0, common.BODY[0])]
    px = np.hypot(*(p[1][:2] / p[1][2] - p[0][:2] / p[0][2])) / common.BODY[0]
    held = reg[sid]['residualMm']['correctedHeldOut']['median']
    return {'pxPerMm': round(float(px), 2), 'heldOutMedianMm': held, 'score': round(float(px / held), 1)}


def fuse(ids, traces, order):
    copper = np.stack([traces[i][0] for i in ids])
    shows = np.stack([traces[i][1] for i in ids])
    # Copper wider than 2.5 mm both ways is a pour, or a light part taken for
    # one (a cream capacitor passes the colour tests in one photograph). It is
    # kept only where another photograph has copper too.
    k = np.ones((int(2.5 * R), int(2.5 * R)), np.uint8)
    for i in range(len(ids)):
        wide = cv2.morphologyEx(copper[i].astype(np.uint8), cv2.MORPH_OPEN, k) > 0
        others = (np.delete(copper & shows, i, axis=0)).any(0)
        copper[i] &= ~(wide & ~others)
    n = shows.sum(0)
    v = (copper & shows).sum(0)
    out = v * 2 > n
    tie = (n > 0) & (v * 2 == n)
    best = np.zeros(n.shape, bool)
    decided = np.zeros(n.shape, bool)
    for i in sorted(range(len(ids)), key=lambda k: order.index(ids[k])):
        take = tie & shows[i] & ~decided
        best[take] = copper[i][take]
        decided |= take
    out = np.where(tie, best, out)
    disagree = (n >= 2) & (v > 0) & (v < n)
    return out, n, disagree


def iou(a, b, where):
    a, b = a & where, b & where
    union = (a | b).sum()
    return None if union == 0 else round(float((a & b).sum() / union), 4)


def zones_iou(a, b, where, R_):
    rs, cs = body_slice(R_)
    out = {}
    for name, (x0, y0, x1, y1) in ZONES.items():
        z = np.zeros(where.shape, bool)
        z[rs.start + int(y0 * R_):rs.start + int(y1 * R_), cs.start + int(x0 * R_):cs.start + int(x1 * R_)] = True
        out[name] = iou(a, b, where & z)
    return out


def old_map_in_frame(R_):
    """The map the model had before, placed as that model placed it: stretched
    over the board's body, whatever the body's size."""
    import hashlib, io, subprocess
    data = subprocess.run(['git', 'show', f'{OLD_MAP_COMMIT}:{OLD_MAP}'], cwd=common.REPO, capture_output=True, check=True).stdout
    if hashlib.sha256(data).hexdigest() != OLD_MAP_SHA:
        raise SystemExit(f'{OLD_MAP} at {OLD_MAP_COMMIT} is not the map from before (SHA-256 {OLD_MAP_SHA})')
    old = np.asarray(Image.open(io.BytesIO(data)).convert('L'))
    out = np.zeros(common.frame_size(R_)[::-1], bool)
    rs, cs = body_slice(R_)
    out[rs, cs] = cv2.resize(old, (cs.stop - cs.start, rs.stop - rs.start), interpolation=cv2.INTER_LINEAR) > 127
    return out


def to_map(mask):
    rs, cs = body_slice(R)
    body = mask[rs, cs].astype(np.float32)
    w, h = int(common.BODY[0] * MAP_R), int(common.BODY[1] * MAP_R)
    return np.clip(cv2.resize(body, (w, h), interpolation=cv2.INTER_AREA) * 255 + 0.5, 0, 255).astype(np.uint8)


def main():
    reg = json.load(open(os.path.join(common.DATA, 'registration.json'), encoding='utf8'))
    ranks = {sid: rank(reg, sid) for sid in TOP + [BOTTOM]}
    order = sorted(TOP, key=lambda s: -ranks[s]['score'])
    traces = {sid: load_trace(sid) for sid in TOP + [BOTTOM]}
    rs, cs = body_slice(R)
    body = np.zeros(traces[TOP[0]][0].shape, bool)
    body[rs, cs] = True
    edge = np.zeros_like(body)
    e = int(1.2 * R)
    edge[rs.start + e:rs.stop - e, cs.start + e:cs.stop - e] = True

    top, n, disagree = fuse(TOP, traces, order)
    top = clean(top) & edge
    seen = (n > 0) & body
    fill = replica('top', R) & ~seen & edge
    under_copper, under_shows = traces[BOTTOM]
    under = clean(under_copper & under_shows) & edge

    # The map.
    rgb = np.stack([to_map(top), to_map(fill), to_map(under)], -1)
    out = os.path.join(common.SITE, 'src', 'assets', 'tracks', 'kim-1.webp')
    measuring_old = old_map_in_frame(8)
    Image.fromarray(rgb).save(out, lossless=True, quality=100, method=6)
    print(f'wrote {os.path.relpath(out, common.REPO)}: {rgb.shape[1]} x {rgb.shape[0]}, {os.path.getsize(out)} bytes')

    # The figures, at the replica's 8 pixels per mm.
    down = lambda m: cv2.resize(m.astype(np.uint8) * 255, common.frame_size(8), interpolation=cv2.INTER_AREA) > 127
    ref_top, ref_bottom = replica('top', 8), replica('bottom', 8)
    seen8, top8, fill8, under8 = down(seen), down(top), down(fill), down(under)
    shows_under8 = down(under_shows & body)
    agreement = {
        'about': 'Intersection over union of copper pixels, at 8 pixels to the millimetre. "replica" is Eduardo Casino\'s KiCad replica of the Rev D board; the photographs are of Rev B boards, so the two differ in places and 1.0 is not reachable. Measured only where at least one photograph shows the board. Made by fuse.py.',
        'sources': ranks,
        'order': order,
        'top': {
            'seenShare': round(float(seen[rs, cs].mean()), 4),
            'fromReplicaShare': round(float(fill[rs, cs].mean()), 4),
            'disagreeShareOfSeenByTwo': round(float(disagree[(n >= 2) & body].mean()), 4),
            'after': {'replica': iou(top8, ref_top, seen8), 'zones': zones_iou(top8, ref_top, seen8, 8)},
        },
        'bottom': {
            'seenShare': round(float(under_shows[rs, cs].mean()), 4),
            'after': {'replica': iou(under8, ref_bottom, shows_under8), 'zones': zones_iou(under8, ref_bottom, shows_under8, 8)},
            'before': {'replica': 0.0, 'note': 'the underside had no tracks'},
        },
        'leaveOneOut': {},
    }
    old_path = os.path.join(common.DATA, 'agreement.json')
    old8 = measuring_old
    agreement['top']['before'] = {'replica': iou(old8, ref_top, seen8), 'zones': zones_iou(old8, ref_top, seen8, 8)}
    # Each photograph against the other two fused: independent of it.
    for sid in TOP:
        others = [s for s in TOP if s != sid]
        f, n2, _ = fuse(others, traces, order)
        f = clean(f) & edge
        c, s = traces[sid]
        where = down(s & (n2 > 0) & body)
        entry = {'before': iou(measuring_old, down(c & s), where), 'after': iou(down(f), down(c & s), where)}
        agreement['leaveOneOut'][sid] = entry
    common.save_json(old_path, agreement)
    t = agreement['top']
    print(f"top: seen by a photograph {t['seenShare'] * 100:.1f}% of the body, from the replica {t['fromReplicaShare'] * 100:.1f}%; sources disagree on {t['disagreeShareOfSeenByTwo'] * 100:.1f}% of what two or more see")
    print(f"top against the replica: before {t.get('before', {}).get('replica')}, after {t['after']['replica']}")
    for name in ZONES:
        print(f"   {name}: before {t.get('before', {}).get('zones', {}).get(name)}, after {t['after']['zones'][name]}")
    print(f"underside against the replica: {agreement['bottom']['after']['replica']}")
    for name in ZONES:
        print(f"   {name}: {agreement['bottom']['after']['zones'][name]}")
    for sid, e in agreement['leaveOneOut'].items():
        print(f"{sid} against the other two: before {e.get('before')}, after {e['after']}")

    # For looking at.
    for name, m in (('top', top), ('fill', fill), ('under', under)):
        Image.fromarray((m * 255).astype(np.uint8)).save(os.path.join(common.OUT, f'fused-{name}.png'))


if __name__ == '__main__':
    main()
