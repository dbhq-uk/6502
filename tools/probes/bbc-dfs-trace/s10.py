from scen import *
d=Disc(blank(40,b'WORKED'))
# pre-existing big file FILL: sectors 2..338 (337 sectors = 86272 bytes = 0x15100), built by hand per B2
cat0=bytearray(256); cat1=bytearray(256)
cat0[0:6]=b'WORKED'
cat0[8:16]=b'FILL   $'.ljust(8)[:8]
ln=337*256; ld=0x1900
# entry: load 0x1900 exec 0x1900 length ln start 2 ; byte14 = exec_hi<<6 | len_hi<<4 | load_hi<<2 | start_hi
cat1[8:16]=bytes([ld&255,ld>>8, ld&255,ld>>8, ln&255,(ln>>8)&255, ((ln>>16)&3)<<4, 2])
cat1[5]=8; cat1[6]=0x01; cat1[7]=0x90
d.data[0:256]=cat0; d.data[256:512]=cat1
m=Machine(d, timing=Timing(byte=128)); m.boot()
print('hand-built catalogue FILL entry s0:',bytes(cat0[:16]).hex(' '),' s1:',bytes(cat1[:16]).hex(' '))
o=m.oscli('CAT'); print(fmt_out(o))
# OSFILE save of HELLO as BASIC would: load FFFF1900 exec FFFF8023, 0x123 bytes from 0x1900
r=m.bus.ram
name=b'HELLO\r'
r[0x0900:0x0900+len(name)]=name
blk=0x0A00
def w32(a,v): r[a:a+4]=v.to_bytes(4,'little')
r[blk]=0x00; r[blk+1]=0x09
w32(blk+2,0xFFFF1900); w32(blk+6,0xFFFF8023); w32(blk+10,0x1900); w32(blk+14,0x1900+0x123)
for i in range(0x123): r[0x1900+i]=(i*3)&255
m.fdc.log=[]
A,X,Y,C=m.call(0xFFDD,0,blk&255,blk>>8)
print('OSFILE save returned A=%02X error=%s'%(A,m.error))
print('s0 entries:'); print(' ',bytes(d.data[0:0x20]).hex(' ')); print(' ',bytes(d.data[8*1:8*1+8]).hex(' '))
print('s1:', bytes(d.data[0x100:0x120]).hex(' '))
o=m.oscli('INFO *'); print(fmt_out(o))
for l in collapse(cmds(m,0)): print('   ',l)
