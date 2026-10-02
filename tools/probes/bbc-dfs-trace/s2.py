from scen import *
d = Disc(blank(40, b'MYTITLE'))
def mk():
    m = Machine(d, timing=Timing(byte=128)); m.boot(); m.fdc.log=[]; return m
def run(m, cmd):
    mark=len(m.fdc.log); c0=m.cpu.cycles
    o=m.oscli(cmd, max_cycles=60_000_000)
    print('=== *%s   (%d cycles)'%(cmd, m.cpu.cycles-c0)); print(fmt_out(o))
    for l in collapse(cmds(m,mark)): print('   ',l)
m=mk()
prog=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
m.bus.ram[0x2000:0x2006]=prog
big=bytes((i*13+5)&0xFF for i in range(0x3000))
m.bus.ram[0x3000:0x6000]=big
run(m,'SAVE PROG 2000 2006 2000')
run(m,'SAVE BIG 3000 6000')
run(m,'CAT')
print('cat0', bytes(d.data[:0x30]).hex(' ')); print('cat1', bytes(d.data[0x100:0x130]).hex(' '))
# fresh machine
m=mk()
run(m,'RUN PROG')
run(m,'LOAD BIG 4000')
ok = bytes(m.bus.ram[0x4000:0x7000])==big
print('BIG round trip equal:', ok)
run(m,'INFO *')
run(m,'LOAD NOPE')
run(m,'NOSUCH')
