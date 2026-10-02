from scen import *
A=Disc(blank(40,b'DISC_A')); B=Disc(blank(40,b'DISC_B'))
m=Machine(A, timing=Timing(byte=128)); m.boot(); m.fdc.log=[]
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
m.oscli('SAVE ONLYA 2000 2006')
m.oscli('SAVE ONLYA2 2000 2006')
def reads(n0): return sum(1 for l in m.fdc.log[n0:] if l[1]=='X' and l[2]=='exec' and l[3]=='53')
def show(tag,cmd):
    n0=len(m.fdc.log)
    o=m.oscli(cmd)
    print('%-52s -> %-34r catalogue reads: %d'%(tag, fmt_out(o).replace('\n','')[:34], reads(n0)))
show('INFO * (disc A in drive)','INFO *')
m.fdc.drives[0].disc=B
show('INFO * right after swap to disc B, no idle','INFO *')
m.run(5_000_000)
print('   after 5M idle cycles: port23=%02X ready=%s'%(m.fdc.sp[0x23], m.fdc.ready(0)))
show('INFO * after 5M idle cycles (disc B)','INFO *')
m.fdc.drives[0].disc=A
show('INFO * swap to A again, no idle, ready never dropped','INFO *')
m.fdc.drives[0].disc=None; 
n0=len(m.fdc.log)
m.run(2_000_000)
m.fdc.drives[0].disc=B
m.fdc.drives[0].motor_since=None
show('swap via disc=None for 2M cycles, then B','INFO *')
