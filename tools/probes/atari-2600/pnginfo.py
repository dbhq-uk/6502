"""Decode 8-bit non-interlaced PNGs (colour types 2 and 6) given as arguments and
print size, colour type, the filter types used, the alpha values present and
the count of distinct colours. Standard library only."""
import struct, sys, zlib
def paeth(a, b, c):
    p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    return a if pa <= pb and pa <= pc else (b if pb <= pc else c)
for path in sys.argv[1:]:
    b = open(path, 'rb').read(); i = 8; idat = b''
    while i < len(b):
        n = struct.unpack('>I', b[i:i + 4])[0]; t = b[i + 4:i + 8]
        if t == b'IHDR': w, h, bd, ct, cm, fm, il = struct.unpack('>IIBBBBB', b[i + 8:i + 21])
        if t == b'IDAT': idat += b[i + 8:i + 8 + n]
        i += 12 + n
    bpp = 4 if ct == 6 else 3
    raw = zlib.decompress(idat); stride = w * bpp; prev = bytearray(stride); rows = []; filters = set()
    for r in range(h):
        f = raw[r * (stride + 1)]; line = bytearray(raw[r * (stride + 1) + 1:(r + 1) * (stride + 1)]); filters.add(f)
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0; up = prev[x]; c = prev[x - bpp] if x >= bpp else 0
            line[x] = (line[x] + (0, a, up, (a + up) // 2, paeth(a, up, c))[f]) & 255
        rows.append(line); prev = line
    alphas = sorted({row[x + 3] for row in rows for x in range(0, stride, 4)}) if ct == 6 else 'none'
    colours = {bytes(row[x:x + bpp]) for row in rows for x in range(0, stride, bpp)}
    print(f"{path} {w}x{h} ct={ct} il={il} filters={sorted(filters)} alpha={alphas} colours={len(colours)}")
