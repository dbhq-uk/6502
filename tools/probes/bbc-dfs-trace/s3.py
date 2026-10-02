from scen import *
import sys
def mk(disc, **kw):
    m = Machine(disc, timing=Timing(byte=128, **kw)); m.boot(); m.fdc.log=[]; return m
def run(m, cmd, cap=30_000_000):
    mark=len(m.fdc.log); c0=m.cpu.cycles
    try:
        o=m.oscli(cmd, max_cycles=cap)
        err=m.error
    except Exception as e:
        o=bytes(m.out); err=('EXC',str(e))
    print('=== *%s   (%d cycles)  error=%s'%(cmd, m.cpu.cycles-c0, err)); print(fmt_out(o))
    for l in collapse(cmds(m,mark)): print('   ',l)
    return err
d = Disc(blank(40, b'MYTITLE'))
m=mk(d)
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
run(m,'SAVE PROG 2000 2006 2000')
run(m,'LOAD NOPE')
run(m,'INFO NOPE')
run(m,'RUN NOPE')
run(m,'NOSUCH')
run(m,'DELETE NOPE')
run(m,'ACCESS PROG L')
run(m,'SAVE PROG 2000 2006')
run(m,'CAT')
run(m,'INFO *')
print('--- unformatted (all zero) image')
m=mk(Disc(bytearray(102400)))
run(m,'CAT')
print('--- no disc in drive 0')
m=mk(None)
run(m,'CAT', cap=20_000_000)
print('--- drive 1 empty')
m=mk(d)
run(m,'CAT 1', cap=20_000_000)
print('--- short image (only 3 tracks) ')
m=mk(Disc(blank(40)[:3*2560]))
run(m,'CAT')
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
run(m,'SAVE X 2000 2006', cap=60_000_000)
print('--- write protected')
m=mk(Disc(blank(40), writeprot=True))
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
run(m,'SAVE X 2000 2006')
