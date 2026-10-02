from scen import *
import sys, json
from fdc8271 import REAL
data=bytes((i*31+7)&0xFF for i in range(512))
def scenario(**tkw):
    d=Disc(blank(40,b'T'))
    res={}
    try:
        m=Machine(d, timing=Timing(**tkw)); m.boot(); m.fdc.log=[]
        m.bus.ram[0x3000:0x3200]=data
        o1=m.oscli('SAVE FILE 3000 3200 3000', max_cycles=40_000_000)
        e1=m.error
        c1=m.cpu.cycles
        m2=Machine(d, timing=Timing(**tkw)); m2.boot(); m2.fdc.log=[]
        o2=m2.oscli('LOAD FILE 4000', max_cycles=40_000_000)
        ok = bytes(m2.bus.ram[0x4000:0x4200])==data and m.error is None and m2.error is None
        late=[l for l in m.fdc.log+m2.fdc.log if l[1]=='!']
        lat=m.fdc.lat+m2.fdc.lat
        return dict(ok=ok, err=(m.error,m2.error), late=len(late), save_cycles=c1, maxlat=max(lat) if lat else None, nlat=len(lat))
    except Exception as e:
        return dict(ok=False, exc=str(e)[:80])
variants={
 'real-ish (byte128, late, instant seek)': dict(byte=128),
 'byte=100': dict(byte=100),
 'byte=90': dict(byte=90),
 'byte=80': dict(byte=80),
 'byte=76': dict(byte=76),
 'byte=70': dict(byte=70),
 'byte=64 (my first, wrong, value)': dict(byte=64),
 'byte=300': dict(byte=300),
 'byte=2000': dict(byte=2000),
 'no late error, paced 128': dict(byte=128, late=False),
 'no late error, byte=0 (as fast as CPU takes)': dict(byte=0, late=False),
 'param delay 0': dict(byte=128, param=0),
 'param delay 200': dict(byte=128, param=200),
 'real timings (step 8000/track, settle 32000, find 200000, spinup 800000)': dict(REAL),
}
if __name__!="__main__": raise SystemExit
sel=sys.argv[1:] 
for k,v in variants.items():
    if sel and not any(s in k for s in sel): continue
    r=scenario(**v); print(k, '->', r, flush=True)
