"""Prints, for rows FIRST to LAST of one of the suite's reference pictures, each run of pixels
that is not the background colour, as "x0-x1:rrggbb". tia.md section 4 reads the delays of the
VBLANK, GRP0 and playfield paths from midline-vblank, midline-grp and midline-pf this way.
Reads 8-bit RGB or RGBA PNGs with the standard library; the background is the bottom-right
pixel unless given as r,g,b.

    python3 -I tools/probes/atari-2600/png_rows.py roms/tia-timing/midline-vblank_ntsc.png 37 47 [r,g,b]
"""
import struct
import sys
import zlib


def paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    return a if pa <= pb and pa <= pc else b if pb <= pc else c


def read(path):
    data, i, idat = open(path, 'rb').read(), 8, b''
    while i < len(data):
        n = struct.unpack('>I', data[i:i + 4])[0]
        kind = data[i + 4:i + 8]
        if kind == b'IHDR':
            width, height, depth, colour = struct.unpack('>IIBB', data[i + 8:i + 18])
            if depth != 8 or colour not in (2, 6):
                sys.exit(f'{path}: only 8-bit RGB or RGBA is read')
        elif kind == b'IDAT':
            idat += data[i + 8:i + 8 + n]
        i += 12 + n
    bpp = 4 if colour == 6 else 3
    raw, stride, prev, rows = zlib.decompress(idat), width * bpp, bytearray(width * bpp), []
    for r in range(height):
        f, line = raw[r * (stride + 1)], bytearray(raw[r * (stride + 1) + 1:(r + 1) * (stride + 1)])
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            c = prev[x - bpp] if x >= bpp else 0
            line[x] = (line[x] + (0, a, prev[x], (a + prev[x]) // 2, paeth(a, prev[x], c))[f]) & 255
        rows.append([tuple(line[x:x + 3]) for x in range(0, stride, bpp)])
        prev = line
    return width, height, rows


width, height, rows = read(sys.argv[1])
first, last = int(sys.argv[2]), int(sys.argv[3])
background = tuple(int(v) for v in sys.argv[4].split(',')) if len(sys.argv) > 4 else rows[-1][-1]
print(f'{sys.argv[1]}: {width} by {height}, background {background}')
for y in range(first, min(last, height)):
    runs, x = [], 0
    while x < width:
        if rows[y][x] == background:
            x += 1
            continue
        start, colour = x, rows[y][x]
        while x < width and rows[y][x] == colour:
            x += 1
        runs.append(f'{start}-{x - 1}:{colour[0]:02x}{colour[1]:02x}{colour[2]:02x}')
    print(y, ' '.join(runs))
