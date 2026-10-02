from scen import *
d=Disc(blank(40))
m=Machine(d, timing=Timing(byte=128)); m.boot()
for n in (0,1,2,3):
    m.oscli('OPT 4,%d'%n)
    o=m.oscli('CAT'); l=fmt_out(o).split('\n')[1]
    print(n, repr(bytes(o).split(b'\n')[1]))
