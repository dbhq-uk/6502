import os
ROMS = os.environ.get("BBC_ROMS") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "roms", "bbc-micro") + os.sep
# Throwaway Model B just enough to boot MOS 1.20 + DFS 1.20 + BASIC and run OSCLI commands for tracing.
import sys
from cpu import CPU
from fdc8271 import FDC8271, Timing, Disc, REAL

class VIA:
    def __init__(s, name):
        s.name = name
        s.orb = s.ora = s.ddrb = s.ddra = 0
        s.t1l = s.t1c = 0xFFFF; s.t2l = s.t2c = 0xFFFF
        s.acr = s.pcr = 0; s.ifr = s.ier = 0
        s.t1run = False; s.t2run = False
        s.acc = 0
        s.latch = 0
        s.sr = 0
    def irq(s): return 1 if (s.ifr & s.ier & 0x7F) else 0
    def tick(s, cyc):
        s.acc += cyc
        n = s.acc >> 1; s.acc &= 1
        if not n: return
        if s.t1run:
            s.t1c -= n
            while s.t1c < 0:
                s.ifr |= 0x40
                if s.acr & 0x40: s.t1c += s.t1l + 2
                else: s.t1run = False; s.t1c &= 0xFFFF; break
        else:
            s.t1c = (s.t1c - n) & 0xFFFF
        if s.t2run:
            s.t2c -= n
            if s.t2c < 0:
                s.ifr |= 0x20; s.t2run = False; s.t2c &= 0xFFFF
        else:
            s.t2c = (s.t2c - n) & 0xFFFF
    def ifr_read(s): return (s.ifr & 0x7F) | (0x80 if (s.ifr & s.ier & 0x7F) else 0)
    def read(s, r):
        r &= 15
        if r == 0: s.ifr &= ~0x18; return (s.orb & s.ddrb) | (s.inb() & ~s.ddrb & 0xFF)
        if r in (1, 15):
            s.ifr &= ~0x03
            return (s.ora & s.ddra) | (s.ina() & ~s.ddra & 0xFF)
        if r == 2: return s.ddrb
        if r == 3: return s.ddra
        if r == 4: s.ifr &= ~0x40; return s.t1c & 0xFF
        if r == 5: return (s.t1c >> 8) & 0xFF
        if r == 6: return s.t1l & 0xFF
        if r == 7: return s.t1l >> 8
        if r == 8: s.ifr &= ~0x20; return s.t2c & 0xFF
        if r == 9: return (s.t2c >> 8) & 0xFF
        if r == 10: return s.sr
        if r == 11: return s.acr
        if r == 12: return s.pcr
        if r == 13: return s.ifr_read()
        if r == 14: return s.ier | 0x80
    def write(s, r, v):
        r &= 15
        if r == 0: s.orb = v; s.ifr &= ~0x18; s.portb(v)
        elif r in (1, 15): s.ora = v; s.ifr &= ~0x03
        elif r == 2: s.ddrb = v
        elif r == 3: s.ddra = v
        elif r == 4 or r == 6: s.t1l = (s.t1l & 0xFF00) | v
        elif r == 5:
            s.t1l = (s.t1l & 0xFF) | v << 8; s.t1c = s.t1l; s.ifr &= ~0x40; s.t1run = True
        elif r == 7: s.t1l = (s.t1l & 0xFF) | v << 8; s.ifr &= ~0x40
        elif r == 8: s.t2l = (s.t2l & 0xFF00) | v
        elif r == 9: s.t2c = s.t2l | v << 8; s.ifr &= ~0x20; s.t2run = True
        elif r == 10: s.sr = v
        elif r == 11: s.acr = v
        elif r == 12: s.pcr = v
        elif r == 13: s.ifr &= ~(v & 0x7F)
        elif r == 14:
            if v & 0x80: s.ier |= v & 0x7F
            else: s.ier &= ~(v & 0x7F)
    def inb(s): return 0xFF
    def ina(s): return 0
    def portb(s, v): pass

class SysVIA(VIA):
    def __init__(s, links=0x00):
        super().__init__('sys'); s.lat = 0; s.links = links
    def portb(s, v):
        bit = v & 7
        if v & 8: s.lat |= 1 << bit
        else: s.lat &= ~(1 << bit)
    def inb(s): return 0xF0 | (s.orb & s.ddrb)
    def ina(s):
        # keyboard: value written to ORA selects column (bits 0-3) and row (bits 4-6)
        k = s.ora & 0x7F
        col, row = k & 15, k >> 4
        pressed = False
        if row == 0 and 2 <= col <= 9:      # DIP links appear as keys on row 0, columns 2-9 (bus.md section 4b)
            pressed = bool(s.links >> (col - 2) & 1)
        return (k & s.ddra) | (0x80 if pressed else 0)

class Bus:
    def __init__(s, os_rom, roms, fdc, links=0x00):
        s.ram = bytearray(0x8000)
        s.os = os_rom
        s.roms = roms         # dict slot -> bytes
        s.romsel = 0
        s.sysvia = SysVia = SysVIA(links)
        s.usrvia = VIA('usr')
        s.fdc = fdc
        s.cpu = None
        s.vsync_acc = 0
        s.crtc = [0] * 32; s.crtcsel = 0
        s.fdc_trace = False
        s.nmi_log = []
        s.nmi_assert_time = None
        s.lat_log = []
        s.max_nmi_latency = 0
        s.trace_fdc_pc = []
    def cycles(s): return s.cpu.cycles
    def nmi_line(s):
        s.fdc.tick(s.cpu.cycles)
        v = s.fdc.nmi_line()
        if v and s.nmi_assert_time is None: s.nmi_assert_time = s.cpu.cycles
        if not v: s.nmi_assert_time = None
        return v
    def irq_line(s): return s.sysvia.irq() | s.usrvia.irq()
    def on_nmi_taken(s, cpu):
        s.nmi_log.append(cpu.cycles - (s.nmi_assert_time if s.nmi_assert_time is not None else cpu.cycles))
    def tick(s, cyc):
        s.sysvia.tick(cyc); s.usrvia.tick(cyc)
        s.vsync_acc += cyc
        if s.vsync_acc >= 40000:
            s.vsync_acc -= 40000
            s.sysvia.ifr |= 0x02
    def read(s, a):
        if a < 0x8000: return s.ram[a]
        if a < 0xC000:
            r = s.roms.get(s.romsel)
            return r[a - 0x8000] if r else 0xFF
        if a < 0xFC00 or a >= 0xFF00: return s.os[a - 0xC000]
        if a < 0xFE00: return 0xFF
        o = a & 0xFF
        if o < 0x08: return s.crtc[s.crtcsel] if (o & 1) and s.crtcsel in (14, 15, 16, 17) else 0
        if o < 0x10: return 0x02 if (o & 1) == 0 else 0
        if o < 0x18: return 0
        if o < 0x20: return 0
        if o < 0x30: return 0xFE
        if o < 0x40: return 0xFE
        if o < 0x60: return s.sysvia.read(o)
        if o < 0x80: return s.usrvia.read(o)
        if o < 0xA0:
            if o & 4: return s.fdc.read_data(s.cpu.cycles)
            v = s.fdc.read(o, s.cpu.cycles)
            if s.fdc_trace: s.trace_fdc_pc.append((s.cpu.cycles, s.cpu.pc, 'R', o & 3, v))
            return v
        if o < 0xC0: return 0xFE
        if o < 0xE0: return 0x40
        return 0xFE
    def write(s, a, v):
        if a < 0x8000: s.ram[a] = v; return
        if a < 0xFE00: return
        o = a & 0xFF
        if o < 0x08:
            if o & 1: s.crtc[s.crtcsel] = v
            else: s.crtcsel = v & 31
            return
        if 0x30 <= o < 0x40: s.romsel = v & 15; return
        if 0x40 <= o < 0x60: s.sysvia.write(o, v); return
        if 0x60 <= o < 0x80: s.usrvia.write(o, v); return
        if 0x80 <= o < 0xA0:
            if o & 4: s.fdc.write_data(v, s.cpu.cycles)
            else:
                s.fdc.write(o, v, s.cpu.cycles)
                if s.fdc_trace: s.trace_fdc_pc.append((s.cpu.cycles, s.cpu.pc, 'W', o & 3, v))
            return

class Machine:
    def __init__(s, disc=None, disc2=None, timing=None, links=0x00, log=None, basic=True):
        d = ROMS
        s.fdc = FDC8271(timing or Timing(**REAL), log)
        s.fdc.drives[0].disc = disc
        s.fdc.drives[1].disc = disc2
        roms = {14: open(d + 'DFS-1.2.rom', 'rb').read()}
        if basic: roms[15] = open(d + 'BASIC.ROM', 'rb').read()
        s.bus = Bus(open(d + 'os.rom', 'rb').read(), roms, s.fdc, links)
        s.cpu = CPU(s.bus); s.bus.cpu = s.cpu
        s.out = bytearray()
        s.cpu.hook = s.hook
        s.cpu.pc = s.bus.read(0xFFFC) | s.bus.read(0xFFFD) << 8
        s.cpu.i = 1
        s.trap = None
        s.stop_at_wait = False
        s.keybuf = None
        s.stopped = None
        s.pcs = {}
        s.catch = False
        s.error = None
    def hook(s, cpu):
        pc = cpu.pc
        if pc == 0xFFEE:                      # OSWRCH: capture, do not run the VDU drivers
            s.out.append(cpu.a)
            lo = cpu.pull(); cpu.pc = (lo | cpu.pull() << 8) + 1
            return True
        if pc == s.trap:
            s.stopped = 'trap'; return True
        if pc == 0xBFFE and s.catch:      # BRKV redirected here during call(): record the error
            r = s.bus.ram
            p = (r[0xFD] | r[0xFE] << 8)
            num = s.bus.read(p) if p >= 0x8000 else r[p]
            msg = bytearray(); q = p + 1
            while True:
                b = s.bus.read(q) if q >= 0x8000 else r[q]
                if b == 0: break
                msg.append(b); q += 1
            s.error = (num, bytes(msg))
            s.stopped = 'error'; return True
        return False
    def run(s, max_cycles, until=None):
        cpu = s.cpu; bus = s.bus
        end = cpu.cycles + max_cycles
        while cpu.cycles < end:
            c = cpu.step()
            bus.tick(c)
            if until and until(s): return True
            if s.stopped: return True
        return False
    def call(s, addr, a=0, x=0, y=0, max_cycles=200_000_000):
        """JSR addr with registers, run until it returns. Returns (A,X,Y,carry)."""
        cpu = s.cpu
        save = (cpu.pc, cpu.a, cpu.x, cpu.y, cpu.sp, cpu.P())
        TR = 0xFFFF   # an address in OS ROM that is never executed ($FFFF is the last vector byte)
        s.trap = 0xBFFD    # inside the paged-ROM window: never executed, so no RAM is touched
        cpu.push((s.trap - 1) >> 8); cpu.push((s.trap - 1) & 0xFF)
        cpu.pc = addr; cpu.a, cpu.x, cpu.y = a, x, y
        s.stopped = None
        s.error = None
        oldbrk = (s.bus.ram[0x202], s.bus.ram[0x203])
        s.catch = True
        s.bus.ram[0x202] = 0xFE; s.bus.ram[0x203] = 0xBF
        ok = s.run(max_cycles)
        s.bus.ram[0x202], s.bus.ram[0x203] = oldbrk
        s.catch = False
        res = (cpu.a, cpu.x, cpu.y, cpu.c)
        if not ok: raise Exception('call did not return at pc=%04X' % cpu.pc)
        cpu.pc, cpu.a, cpu.x, cpu.y, cpu.sp = save[:5]; cpu.setP(save[5])
        s.stopped = None; s.trap = None
        return res
    def boot(s, max_cycles=60_000_000):
        """Run until BASIC reads its first line (OSWORD 0 via OSWORD entry $FFF1 with A=0)."""
        def u(m):
            return m.cpu.pc == 0xFFF1 and m.cpu.a == 0
        if not s.run(max_cycles, u): raise Exception('boot did not reach BASIC prompt; pc=%04X' % s.cpu.pc)
    def oscli(s, text, **kw):
        base = 0x0E00 + 0x1F0 if False else 0x0900
        data = text.encode() + b'\r'
        for i, b in enumerate(data): s.bus.ram[0x0900 + i] = b
        s.out.clear()
        # make sure the BASIC ROM is not paged in while OSCLI runs: the OS handles it
        r = s.call(0xFFF7, 0, 0x00, 0x09, **kw)
        return bytes(s.out)

def fmt_out(b):
    """printable form of captured OSWRCH bytes"""
    o = []
    for c in b:
        if c == 13: o.append('\\r')
        elif c == 10: o.append('\\n\n')
        elif 32 <= c < 127: o.append(chr(c))
        else: o.append('<%02X>' % c)
    return ''.join(o)
