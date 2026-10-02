from scen import *
import collections
data=bytes((i*31+7)&0xFF for i in range(512))
d=Disc(blank(40,b'T'))
m=Machine(d, timing=Timing(byte=128)); m.boot()
st={'t':None}; res=collections.defaultdict(list)
orig=m.cpu.hook
last_take=[None]
def h(cpu):
    if 0x0D00<=cpu.pc<0x0D50 and m.bus.read(cpu.pc)==0x40:
        res['rti_at'].append((cpu.pc, cpu.cycles+6-last_take[0]))
    return orig(cpu)
m.cpu.hook=h
oldtaken=m.bus.on_nmi_taken
def taken(cpu):
    last_take[0]=cpu.cycles
    oldtaken(cpu)
m.bus.on_nmi_taken=taken
m.bus.ram[0x3000:0x3200]=data
m.oscli('SAVE FILE 3000 3200 3000', max_cycles=40_000_000)
print('write path (X=0 handler): RTI cycle offsets from NMI taken (incl 7 cycle NMI sequence is before; counted from the cycle NMI was taken):')
c=collections.Counter(res['rti_at'])
for k,v in sorted(c.items()): print(' pc %04X  total %d cycles after NMI taken  x%d'%(k[0],k[1],v))
res.clear()
m2=Machine(d, timing=Timing(byte=128)); m2.boot()
orig=m2.cpu.hook
last_take=[None]
def h2(cpu):
    if 0x0D00<=cpu.pc<0x0D50 and m2.bus.read(cpu.pc)==0x40:
        res['rti_at'].append((cpu.pc, cpu.cycles+6-last_take[0]))
    return orig(cpu)
m2.cpu.hook=h2
old2=m2.bus.on_nmi_taken
def taken2(cpu):
    last_take[0]=cpu.cycles; old2(cpu)
m2.bus.on_nmi_taken=taken2
m2.oscli('LOAD FILE 4000', max_cycles=40_000_000)
print('read path (X=1 handler):')
c=collections.Counter(res['rti_at'])
for k,v in sorted(c.items()): print(' pc %04X  total %d cycles after NMI taken  x%d'%(k[0],k[1],v))
import statistics
print('assert->NDRQ taken latency (cycles): max',max(m.fdc.lat+m2.fdc.lat),'min',min(m.fdc.lat+m2.fdc.lat))
print('NMI assert -> NMI taken: ', collections.Counter(m.bus.nmi_log+m2.bus.nmi_log).most_common(8))
