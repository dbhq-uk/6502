from scen import *
for r in (0x0A,0x0C,0x0E,0x10,0x14,0x16,0x08,0x18,0x1E):
    d=Disc(blank(40,b'T'))
    m=Machine(d, timing=Timing(byte=128)); m.boot(); m.fdc.log=[]
    m.fdc.inject=lambda op,p,r=r: r if op in (0x13,) else None
    try:
        o=m.oscli('CAT', max_cycles=20_000_000); err=m.error
    except Exception as e: err=('EXC',str(e))
    n=sum(1 for l in m.fdc.log if l[1]=='X' and l[2]=='exec' and l[3]=='53')
    print('result %02X for read data -> error %s ; read attempts %d'%(r, err, n))
