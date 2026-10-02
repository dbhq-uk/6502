from scen import *
import sys
def mk(disc, disc2=None, **kw):
    m = Machine(disc, disc2=disc2, timing=Timing(byte=128, **kw)); m.boot(); m.fdc.log=[]; return m
def run(m, cmd, cap=30_000_000):
    mark=len(m.fdc.log); c0=m.cpu.cycles
    try:
        o=m.oscli(cmd, max_cycles=cap); err=m.error
    except Exception as e:
        o=bytes(m.out); err=('EXC',str(e))
    print('=== *%s   (%d cycles)  error=%s'%(cmd, m.cpu.cycles-c0, err)); print(fmt_out(o))
    for l in collapse(cmds(m,mark)): print('   ',l[:100])
    return err
print('--- DSD (2 sides, track interleaved), side 1 has its own catalogue and a title')
def dsd(tracks=40):
    n=tracks*10
    d=bytearray(tracks*2*2560)
    for side,title in ((0,b'SIDE0'),(1,b'SIDE1')):
        o=side*2560   # track 0 of this side
        d[o:o+len(title)]=title
        d[o+0x106]=n>>8; d[o+0x107]=n&255
    return Disc(d, sides=2)
d=dsd()
m=mk(d)
run(m,'CAT')
run(m,'CAT 2')
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
run(m,'SAVE :2.PROG 2000 2006')
run(m,'CAT 2')
print('side1 track0 sector0 at file offset 2560:', bytes(d.data[2560:2560+0x10]).hex(' '))
print('side0 catalogue unchanged? s0:', bytes(d.data[0:0x10]).hex(' '))
print('--- 80-track blank')
m=mk(Disc(blank(80,b'EIGHTY')))
run(m,'CAT')
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
run(m,'SAVE P80 2000 2006')
print('cat1 of 80-track after save:', bytes(m.fdc.drives[0].disc.data[0x100:0x110]).hex(' '))
print('--- short image: 3 tracks, file spills past track 2')
d=Disc(blank(40)[:3*2560])
m=mk(d)
m.bus.ram[0x3000:0x5000]=bytes(0x2000)
run(m,'SAVE SPILL 3000 5000')
print('--- CAT in a drive holding an image of 40 tracks whose catalogue says 800 sectors')
x=blank(40); x[0x106]=3; x[0x107]=0x20
m=mk(Disc(x)); run(m,'CAT')
