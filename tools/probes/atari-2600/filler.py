"""For each .a26 path given, print its size and, for each 4K bank, whether
the first 256 bytes are one repeated byte (the Superchip filler rule)."""
import sys
for p in sys.argv[1:]:
    b = open(p, 'rb').read()
    banks = [b[i:i + 4096] for i in range(0, len(b), 4096)] if len(b) >= 4096 else [b]
    flags = []
    for n, k in enumerate(banks):
        head = k[:256]
        one = len(set(head)) == 1
        flags.append(f"bank{n}:{'one $%02X' % head[0] if one else 'mixed'}")
    allone = all('one' in f for f in flags)
    print(f"{p.rsplit('/', 1)[-1]} size={len(b)} all-banks-filler={allone} " + ' '.join(flags))
