import sys, json
import numpy as np, cv2
from PIL import Image
sys.path.insert(0, '.')
import common, kicad

Image.MAX_IMAGE_PIXELS = None
b = kicad.load('/tmp/kim/casino/kim-1.kicad_pcb')


def kraster(layer, R):
    M = common.to_frame_px(R)
    T = lambda x, y: tuple((M @ np.array([*common.kicad_to_board(x, y), 1]))[:2])
    img = kicad.draw_copper(b, layer, T, common.frame_size(R), R)
    return np.asarray(img).astype(np.float32) / 255


def feature(rgb):
    L, A, B = common.oklab(rgb)
    k = max(3, int(rgb.shape[1] / 300)) | 1
    bg = cv2.morphologyEx(L, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_RECT, (k, k)))
    f = np.clip((L - bg) / 0.15, 0, 1)
    return f.astype(np.float32)


photo, corners, layer = sys.argv[1], json.loads(sys.argv[2]), sys.argv[3]
rgb = np.asarray(Image.open(photo).convert('RGB'))
f = feature(rgb)
src = np.array(corners, dtype=np.float32)
dst = np.array([[0, 0], [200, 0], [200, 273], [0, 273]], dtype=np.float32)
H0 = cv2.getPerspectiveTransform(src, dst)  # photo px -> board mm
for R in (2, 4, 8):
    ref = cv2.GaussianBlur(kraster(layer, R), (0, 0), 1.5)
    W = common.to_frame_px(R) @ H0
    warped = cv2.warpPerspective(f, W, common.frame_size(R), flags=cv2.INTER_AREA)
    warped = cv2.GaussianBlur(warped, (0, 0), 1.5)
    E = np.eye(3, dtype=np.float32)
    try:
        cc, E = cv2.findTransformECC(ref, warped, E, cv2.MOTION_HOMOGRAPHY, (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 200, 1e-6), None, 5)
    except cv2.error as e:
        print('ECC failed', R, e); break
    # warped(E x) ~ ref(x): frame px -> warped px is E, so photo->frame = E^-1 W
    W2 = np.linalg.inv(E.astype(np.float64)) @ W
    H0 = np.linalg.inv(common.to_frame_px(R)) @ W2
    H0 /= H0[2, 2]
    print('R', R, 'cc', round(cc, 4))
np.save('/tmp/kim/H_' + sys.argv[4] + '.npy', H0)
R = 4
W = common.to_frame_px(R) @ H0
warped = cv2.warpPerspective(rgb, W, common.frame_size(R), flags=cv2.INTER_AREA)
ref = kraster(layer, R)
over = warped.copy()
over[ref > 0.5] = (0.5 * over[ref > 0.5] + [0, 0, 127]).astype(np.uint8)
Image.fromarray(over).save('/tmp/kim/reg_' + sys.argv[4] + '.png')
