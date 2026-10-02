from scen import *
d = Disc(blank(40, b'MYTITLE'))
m = Machine(d, timing=Timing(byte=128))
m.boot(); m.fdc.log=[]
for k in range(0x2000,0x2100): m.bus.ram[k]=(k*7)&0xFF
def run(cmd, note=''):
    mark=len(m.fdc.log); c0=m.cpu.cycles
    o=m.oscli(cmd, max_cycles=40_000_000)
    print('=== *%s   (%d cycles)'%(cmd, m.cpu.cycles-c0)); print(fmt_out(o))
    for l in collapse(cmds(m,mark)): print('   ',l)
run('CAT')
run('SAVE TEST 2000 2100 2000 2000')
run('CAT')
run('INFO TEST')
run('INFO *')
print('cat sector0:', bytes(d.data[0:0x20]).hex(' '))
print('cat sector1:', bytes(d.data[0x100:0x120]).hex(' '))
