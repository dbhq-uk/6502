from scen import *
d=bytearray(blank(40,b'FULL'))
for i in range(31):
    nm=('F%02d'%i).ljust(7).encode()+b'$'
    d[8+8*i:16+8*i]=nm
    st=32-i
    d[0x100+8+8*i:0x100+16+8*i]=bytes([0,0x20,0,0x20,1,0,0,st])
d[0x105]=31*8
m=Machine(Disc(d), timing=Timing(byte=128)); m.boot()
m.bus.ram[0x2000:0x2006]=bytes(6)
try:
    o=m.oscli('SAVE NEW 2000 2006'); print(m.error)
except Exception as e: print('EXC',e)
print('Cat full at', hex(0x8000+open(ROMS+'DFS-1.2.rom','rb').read().find(b'Cat full')))
