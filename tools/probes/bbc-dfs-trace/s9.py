from scen import *
def screen(b):
    # emulate a 40-column text screen well enough: NUL ignored, LF CR = newline (no wrapping needed here)
    lines=['']
    for c in b:
        if c==0: continue
        if c==10: lines.append(''); continue
        if c==13: continue
        lines[-1]+=chr(c)
    return lines
def show(title,b):
    print('##',title)
    print('stream:',repr(bytes(b)))
    for i,l in enumerate(screen(b)): print('  row',i,repr(l))
d=Disc(blank(40))                           # title empty
m=Machine(d, timing=Timing(byte=128)); m.boot()
show('*CAT empty 40-track, empty title', m.oscli('CAT'))
m.bus.ram[0x2000:0x2006]=bytes([0xA9,0x41,0x20,0xEE,0xFF,0x60])
m.oscli('SAVE TEST 2000 2006 2000')
show('*CAT after *SAVE TEST', m.oscli('CAT'))
show('*INFO TEST', m.oscli('INFO TEST'))
show('*INFO (no arg) hmm', m.oscli('INFO'))
m.oscli('SAVE ABCDEFG 2000 2006')
m.oscli('SAVE A.HELLO 2000 2006'); m.oscli('ACCESS A.HELLO L')
show('*CAT 3 files incl dir A locked', m.oscli('CAT'))
show('*INFO *.*', m.oscli('INFO *.*'))
m.oscli('TITLE HELLO WORLD')
show('title 11 chars', m.oscli('CAT'))
