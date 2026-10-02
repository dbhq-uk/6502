from scen import *
import sys
def mk(disc, disc2=None, **kw):
    m = Machine(disc, disc2=disc2, timing=Timing(byte=128, **kw)); m.boot(); m.fdc.log=[]; return m
def run(m, cmd, cap=30_000_000, esc_after=None):
    mark=len(m.fdc.log); c0=m.cpu.cycles
    if esc_after:
        orig=m.cpu.hook
        def h(cpu):
            if cpu.cycles>c0+esc_after: m.bus.ram[0xFF]=0x80
            return orig(cpu)
        m.cpu.hook=h
    try:
        o=m.oscli(cmd, max_cycles=cap)
        err=m.error
    except Exception as e:
        o=bytes(m.out); err=('EXC',str(e))
    if esc_after: m.cpu.hook=orig; m.bus.ram[0xFF]=0
    print('=== *%s   (%d cycles)  error=%s'%(cmd, m.cpu.cycles-c0, err)); print(fmt_out(o))
    for l in collapse(cmds(m,mark)): print('   ',l[:100])
    return err
print('--- unformatted')
m=mk(Disc(bytearray(102400), formatted=False))
run(m,'CAT')
print('--- no disc, Escape after 3M cycles')
m=mk(None)
run(m,'CAT', esc_after=3_000_000, cap=10_000_000)
print('--- bad name')
m=mk(Disc(blank(40)))
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
run(m,'SAVE TOOLONGNAME 2000 2006')
run(m,'SAVE A.B.C 2000 2006')
print('--- disc full')
d=Disc(blank(40)); d.data[0x106]=0; d.data[0x107]=4   # 4 sectors: catalogue + 2 free
m=mk(d)
m.bus.ram[0x3000:0x3800]=bytes(0x800)
run(m,'SAVE BIG 3000 3800')
print('--- dir/lock listing')
m=mk(Disc(blank(40,b'LISTING')))
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
for n in ['ZED','A.HELLO','B.SUMS','!BOOT','TABLE']:
    run(m,'SAVE %s 2000 2006'%n)
run(m,'ACCESS A.HELLO L')
run(m,'TITLE MYDISC')
run(m,'OPT 4,2')
run(m,'CAT')
run(m,'CAT')
run(m,'INFO *.*')
run(m,'DELETE ZED')
run(m,'CAT')
