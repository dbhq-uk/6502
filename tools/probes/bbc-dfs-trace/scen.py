import sys, time
from bbc import *
from fdc8271 import Disc, Timing, REAL

def blank(tracks=40, title=b'', sides=1):
    d = bytearray(tracks*2560*sides)
    n = tracks*10
    d[0x100+6] = n>>8; d[0x100+7] = n&255
    d[0:len(title[:8])] = title[:8]; d[0x100:0x100+len(title[8:12])] = title[8:12]
    return d

def cmds(m, mark=0):
    """summarise FDC commands since log index mark"""
    out=[]
    for l in m.fdc.log[mark:]:
        if l[1]=='X' and l[2]=='exec': out.append('%02X %s'%(int(l[3],16), ' '.join('%02X'%x for x in l[4])))
        if l[1]=='X' and l[2]=='done': out.append('  -> result %s'%l[3])
        if l[1]=='!': out.append('  !! '+l[2])
    return out

def collapse(lines):
    res=[]; i=0
    while i<len(lines):
        j=i
        while j+1<len(lines) and lines[j+1]==lines[i] and not lines[i].startswith('  '): j+=1
        res.append(lines[i]+(' (x%d)'%(j-i+1) if j>i else ''))
        i=j+1
    return res
