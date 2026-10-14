"""Makes tia.md section 19.6's palette examples and checks the properties the sheet quotes.

Computes each colour number's RGB by the formulas of tia.md sections 19.4 (NTSC: Eric Ball's
conversion with a 360/14-degree step) and 19.5 (PAL: the fitted hue angles), both choices the
sheet marks. Prints the worked examples, then checks: hue 0 is grey at every luminance; within
a hue no component falls and their sum rises as the luminance rises; bit 0 makes no difference;
on PAL, hues 0, 1, 14 and 15 are grey; on NTSC hue 15 is hue 1; the two regions differ; and
the count of different colours in each region. Standard library only.

    python3 -I tools/probes/atari-2600/tia_palette.py
"""
import math

SCALE = 1 / 0.63                # contrast: luminance 7 is white (section 19.4)
SATURATION = (20 / 92.5) * SCALE  # the carrier is the burst's 20 IRE of 92.5 (Ball 2003)


def uv(hue, region):
    if region == 'NTSC':
        if hue == 0:
            return 0.0, 0.0
        angle = 180 - (hue - 1) * 360 / 14
    else:
        if hue in (0, 1, 14, 15):
            return 0.0, 0.0
        angle = 165 - (hue - 2) // 2 * 30 if hue % 2 == 0 else -(165 - (hue - 3) // 2 * 30)
    t = math.radians(angle)
    return SATURATION * math.cos(t), SATURATION * math.sin(t)


def rgb(number, region):
    y = ((number >> 1) & 7) / 7
    u, v = uv(number >> 4, region)
    linear = (y + 1.140 * v, y - 0.3947 * u - 0.5808 * v, y + 2.033 * u)
    return y, u, v, tuple(round(255 * max(0.0, min(1.0, c))) for c in linear)


EXAMPLES = {'NTSC': [0x00, 0x0E, 0x1E, 0x44, 0xC8, 0xF8], 'PAL': [0x1E, 0x2E, 0x58, 0x64, 0x7E, 0xC8, 0xE8]}
for region, numbers in EXAMPLES.items():
    for n in numbers:
        y, u, v, c = rgb(n, region)
        print(f'{region} ${n:02X}: Y {y:.4f} U {u:+.4f} V {v:+.4f} RGB #{c[0]:02X}{c[1]:02X}{c[2]:02X}')

failures = []
for region in ('NTSC', 'PAL'):
    for hue in range(16):
        row = [rgb((hue << 4) | (lum << 1), region)[3] for lum in range(8)]
        if hue == 0 and any(len(set(c)) != 1 for c in row):
            failures.append(f'{region} hue 0 is not grey')
        if region == 'PAL' and hue in (0, 1, 14, 15) and any(len(set(c)) != 1 for c in row):
            failures.append(f'PAL hue {hue} is not grey')
        for a, b in zip(row, row[1:]):
            if any(y < x for x, y in zip(a, b)) or sum(b) <= sum(a):
                failures.append(f'{region} hue {hue}: luminance does not rise')
                break
    for n in range(0, 256, 2):
        if rgb(n, region)[3] != rgb(n + 1, region)[3]:
            failures.append(f'{region} ${n:02X}: bit 0 changes the colour')
if any(rgb((1 << 4) | (l << 1), 'NTSC')[3] != rgb((15 << 4) | (l << 1), 'NTSC')[3] for l in range(8)):
    failures.append('NTSC hue 15 is not hue 1')
if all(rgb(n, 'NTSC')[3] == rgb(n, 'PAL')[3] for n in range(256)):
    failures.append('NTSC and PAL are the same')
for region in ('NTSC', 'PAL'):
    print(f'{region}: {len({rgb(n, region)[3] for n in range(0, 256, 2)})} different colours')
print('all checks pass' if not failures else '\n'.join(failures))
