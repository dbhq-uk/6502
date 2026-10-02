# Throwaway Intel 8271 model, written from the datasheet (D1) for tracing DFS 1.20.
# Non-DMA mode only. Time unit: CPU cycles at 2 MHz (1 cycle = 500 ns).
import math

SEC = 256

class Disc:
    """Sector store. data is a bytearray of side-0-only (ssd) or track-interleaved (dsd) layout."""
    def __init__(self, data, sides=1, tracks=None, writeprot=False, formatted=True):
        self.formatted = formatted
        self.data = bytearray(data)
        self.sides = sides
        self.tracks = tracks if tracks is not None else math.ceil(len(self.data) / (SEC * 10 * sides))
        self.wp = writeprot
        self.dirty = False
    def off(self, side, track, sector):
        if not self.formatted: return None
        if side >= self.sides: return None
        if track >= self.tracks or sector >= 10: return None
        o = ((track * self.sides + side) * 10 + sector) * SEC
        return o

class Drive:
    def __init__(self):
        self.disc = None
        self.phys = 0          # physical head track
        self.motor_since = None
        self.max_track = 80

class Timing:
    """All delays in CPU cycles. 'real' values follow the datasheet and BBC hardware text; 'instant' is the minimal model."""
    def __init__(self, byte=128, seek_per_step=0, settle=0, headload=0, spinup=0, sector_gap=0, find=0, param=8, late=True, done=0):
        self.byte = byte; self.seek_per_step = seek_per_step; self.settle = settle
        self.headload = headload; self.spinup = spinup; self.sector_gap = sector_gap
        self.find = find; self.param = param; self.late = late; self.done = done

REAL = dict(byte=128, seek_per_step=8000, settle=32000, headload=0, spinup=800000, sector_gap=16*64, find=200000, param=8)

NPARAMS = {}
for c in (0x00, 0x04): NPARAMS[c] = 5
for c in (0x0A, 0x0E, 0x12, 0x16, 0x1E): NPARAMS[c] = 2
for c in (0x0B, 0x0F, 0x13, 0x17, 0x1F): NPARAMS[c] = 3
NPARAMS.update({0x1B: 3, 0x23: 5, 0x29: 1, 0x2C: 0, 0x35: 4, 0x3A: 2, 0x3D: 1})

class FDC8271:
    def __init__(self, timing=None, log=None):
        self.t = timing or Timing()
        self.lat = []
        self.inject = None
        self.activity = 0
        self.ndrq_since = 0
        self.drives = [Drive(), Drive()]
        self.now = 0
        self.log = log            # list or None
        self.reset_state()
        self.events = []          # (time, fn)
        self.proc = None          # running generator
        self.proc_wake = None
        self.late_check = None
        self.sp = {}              # special registers
        self.waiting_take = False

    def reset_state(self):
        self.cmd = None; self.params = []; self.nparams = 0
        self.busy = 0; self.cmdfull = 0; self.paramfull = 0
        self.ndrq = 0; self.int_ = 0; self.resfull = 0
        self.result = 0; self.data = 0
        self.proc = None; self.proc_wake = None; self.take_deadline = None; self.waiting_take = False
        self.sp = {0x06: 0, 0x10: 0xFF, 0x11: 0xFF, 0x12: 0xFF, 0x18: 0xFF, 0x19: 0xFF, 0x1A: 0xFF,
                   0x17: 0, 0x23: 0, 0x13: 0, 0x14: 0, 0x0D: 0, 0x0E: 0, 0x0F: 0}
        self.nr = [0, 0]          # latched not-ready per drive (zero-latching ready bits)
        self.events = []
        self.cmd_count = 0

    # ---------------- helpers
    def L(self, *a):
        if self.log is not None: self.log.append((self.now,) + a)

    def nmi_line(self): return 1 if self.int_ else 0

    def sel_drive(self, cmd):
        if cmd & 0x40: return 0
        if cmd & 0x80: return 1
        return None

    def ready(self, d):
        dr = self.drives[d]
        if dr.disc is None: return False
        if dr.motor_since is None: return False
        return self.now - dr.motor_since >= self.t.spinup

    def input_port(self, d):
        """Pin state as read by Read Drive Status / special register $22. Bits from D1 p8-129:
        D6 RDY1, D5 FAULT, D4 INDEX, D3 WRPROT, D2 RDY0, D1 TRK0, D0 CNT/OPI.
        On the BBC one ready circuit feeds both ready pins (S2 5.5.1), so both are set together."""
        dr = self.drives[d]
        v = 0
        if self.ready(d): v |= 0x44
        if dr.disc is not None and dr.disc.wp: v |= 0x08
        if dr.phys == 0: v |= 0x02
        return v

    # ---------------- CPU-side registers
    def read(self, off, now):
        self.now = now
        self.tick(now)
        off &= 3
        if off == 0:
            v = (self.busy << 7) | (self.cmdfull << 6) | (self.paramfull << 5) | (self.resfull << 4) | (self.int_ << 3) | (self.ndrq << 2)
            self.L('R', 'status', v)
            return v
        if off == 1:
            v = self.result
            self.resfull = 0
            if self.int_ and not self.ndrq: self.int_ = 0
            elif self.int_: self.int_ = 0
            self.L('R', 'result', v)
            return v
        return 0

    def read_data(self, now):
        self.now = now
        self.tick(now)
        if self.ndrq: self.lat.append(now - self.ndrq_since)
        self.ndrq = 0; self.int_ = 0
        if self.waiting_take: self.proc_wake = now; self.waiting_take = False
        self.L('R', 'data', self.data)
        return self.data

    def write(self, off, v, now):
        self.now = now
        self.tick(now)
        off &= 3
        if off == 0:
            self.L('W', 'cmd', v)
            if self.busy:
                self.L('!', 'command written while busy')
            self.cmd = v & 0xFF
            self.busy = 1; self.cmdfull = 1
            self.params = []; self.nparams = NPARAMS.get(v & 0x3F, None)
            if self.nparams is None:
                self.L('!', 'unknown command %02X' % v)
                self.nparams = 0
            self.int_ = 0 if self.int_ and False else self.int_
            if self.nparams == 0:
                self.start(now)
        elif off == 1:
            self.L('W', 'param', v)
            self.params.append(v)
            self.paramfull = 1
            self.at(now + self.t.param, lambda: setattr(self, 'paramfull', 0))
            if len(self.params) == self.nparams:
                self.start(now)
        elif off == 2:
            self.L('W', 'reset', v)
            if v & 1:
                self.reset_state()
                self.sp_keep = None

    def write_data(self, v, now):
        self.now = now
        self.tick(now)
        if self.ndrq: self.lat.append(now - self.ndrq_since)
        self.data = v; self.ndrq = 0; self.int_ = 0
        if self.waiting_take: self.proc_wake = now; self.waiting_take = False
        self.L('W', 'data', v)

    # ---------------- scheduler
    def at(self, t, fn):
        self.events.append((t, fn)); self.events.sort(key=lambda e: e[0])

    def tick(self, now):
        # run due events and the command process
        guard = 0
        while True:
            guard += 1
            if guard > 100000: raise Exception('fdc spin')
            if self.events and self.events[0][0] <= now:
                t, fn = self.events.pop(0); self.now = t; fn(); continue
            if self.proc is not None and self.proc_wake is not None and self.proc_wake <= now:
                self.now = self.proc_wake
                self.step_proc()
                continue
            break
        self.now = now

    def next_time(self):
        c = []
        if self.events: c.append(self.events[0][0])
        if self.proc is not None and self.proc_wake is not None: c.append(self.proc_wake)
        return min(c) if c else None

    def step_proc(self):
        try:
            y = self.proc.send(None)
        except StopIteration:
            self.proc = None; self.proc_wake = None; return
        kind, n = y
        if kind == 'sleep':
            self.proc_wake = self.now + n
        elif kind == 'take':           # wait n cycles, then caller checks the CPU took the byte
            self.proc_wake = self.now + n
        elif kind == 'wait':           # wait until the CPU touches the data register
            self.proc_wake = None; self.waiting_take = True

    # ---------------- commands
    def start(self, now):
        self.cmdfull = 0
        self.cmd_count += 1
        op = self.cmd & 0x3F
        p = self.params
        self.L('X', 'exec', '%02X' % self.cmd, list(p))
        if op == 0x2C:                  # read drive status: immediate result
            d = self.sel_drive(self.cmd)
            v = self.input_port(d) if d is not None else 0
            if d is not None:
                self.nr[d] = 0          # clears the latched not-ready
            self.finish_immediate(v)
            return
        if op == 0x3D:
            r = p[0]
            v = self.sp_read(r)
            self.finish_immediate(v); return
        if op == 0x3A:
            self.sp_write(p[0], p[1]); self.busy = 0; return
        if op == 0x35:
            self.specify(p); self.busy = 0; return
        want = self.cmd & 0xC0
        if (self.sp[0x23] & 0xC0) != want:
            self.sp[0x23] = (self.sp[0x23] & 0x20) | want   # D1 p8-124: new select bits clear WE/STEP/DIR/LOAD/LOWCUR
        self.proc = self.run_cmd(op, p)
        self.proc_wake = self.now
        self.step_proc()

    def sp_read(self, r):
        if r == 0x22:
            v = 0
            d = 0 if (self.sp[0x23] & 0x40) else 1
            if not (self.sp[0x23] & 0xC0): d = None
            return self.input_port(d) if d is not None else 0
        return self.sp.get(r, 0)

    def sp_write(self, r, v):
        self.sp[r] = v
        if r == 0x23: self.port_changed()
        self.L('S', 'special', r, v)

    def port_changed(self):
        v = self.sp[0x23]
        for d in (0, 1):
            sel = bool(v & (0x40 if d == 0 else 0x80))
            dr = self.drives[d]
            if sel and (v & 0x08):
                if dr.motor_since is None: dr.motor_since = self.now
            else:
                dr.motor_since = None

    def motor_keep(self, d): return False

    def specify(self, p):
        t = p[0]
        if t == 0x0D:
            self.sp[0x0D], self.sp[0x0E], self.sp[0x0F] = p[1], p[2], p[3]
        elif t == 0x10:
            self.sp[0x10], self.sp[0x11], self.sp[0x12] = p[1], p[2], p[3]
        elif t == 0x18:
            self.sp[0x18], self.sp[0x19], self.sp[0x1A] = p[1], p[2], p[3]

    def schedule_unload(self):
        self.activity = self.now
        revs = (self.sp.get(0x0F, 0) >> 4) & 0xF
        if revs == 15: return          # D1 p8-129: 15 = head stays loaded
        t0 = self.now
        def unload():
            if self.activity == t0 and not self.busy:
                self.sp[0x23] &= ~0x1F        # LOAD HEAD, WE, STEP, DIR, LOWCUR off; select bits cleared too per D1 p8-124
                self.sp[0x23] &= ~0xC0
                self.port_changed()
                self.L('U', 'head unloaded after idle')
        self.at(self.now + revs * 400000, unload)

    def finish_immediate(self, v):
        self.result = v; self.resfull = 1; self.busy = 0

    def finish(self, result):
        # (delay handled by callers via proc sleeps)
        self.schedule_unload()
        self.result = result; self.resfull = 1; self.busy = 0; self.int_ = 1; self.ndrq = 0
        self.L('X', 'done', '%02X' % result)

    # ---------------- generator processes
    def run_cmd(self, op, p):
        cmd = self.cmd
        d = self.sel_drive(cmd)
        if d is None:
            yield ('sleep', 100); self.finish(0x10); return
        dr = self.drives[d]
        side = 1 if (self.sp[0x23] & 0x20) else 0
        # drive ready check
        yield ('sleep', 1)
        # the 8271 loads the head itself (LOAD HEAD = motor) when a command needs it
        if dr.disc is not None and dr.motor_since is None:
            self.sp[0x23] |= 0x08; self.port_changed()
        yield ('sleep', self.t.headload)
        if not self.ready(d):
            if dr.disc is None:
                self.nr[d] = 1
                yield ('sleep', 100); self.finish(0x10); return
            w = dr.motor_since + self.t.spinup - self.now
            if w > 0: yield ('sleep', w)
        if self.nr[d]:
            self.finish(0x10); return
        trk_reg = 0x12 if d == 0 else 0x1A
        if op in (0x0A, 0x0B, 0x0E, 0x0F, 0x23) and dr.disc.wp:
            self.finish(0x12); return
        if op == 0x29:                     # seek
            r = yield from self.seek(d, p[0], trk_reg)
            self.finish(r); return
        if op == 0x1B:                     # read ID
            r = yield from self.seek(d, p[0], trk_reg)
            if r: self.finish(r); return
            cnt = p[2]
            for i in range(cnt):
                for b in (dr.phys, side, i % 10, 1):
                    r = yield from self.xfer_out(b)
                    if r: self.finish(r); return
            self.finish(0); return
        if op == 0x23:                     # format track
            r = yield from self.seek(d, p[0], trk_reg)
            if r: self.finish(r); return
            nsec = p[2] & 0x1F
            for i in range(nsec):
                ids = []
                for k in range(4):
                    r = yield from self.xfer_in()
                    if r: self.finish(r); return
                    ids.append(self.data)
                o = dr.disc.off(side, dr.phys, i)
                if o is not None:
                    dr.disc.data[o:o + SEC] = bytes([0xE5]) * SEC; dr.disc.dirty = True
            self.finish(0); return
        if self.inject is not None:
            r = self.inject(op, p)
            if r is not None:
                self.finish(r); return
        # read / write / verify data
        track, sector, sc = p[0], p[1], (p[2] if len(p) > 2 else 0x21)
        nsec = sc & 0x1F if len(p) > 2 else 1
        size = 128 << (sc >> 5) if len(p) > 2 else 128
        r = yield from self.seek(d, track, trk_reg)
        if r: self.finish(r); return
        yield ('sleep', self.t.find)
        for i in range(nsec):
            s = sector + i
            self.sp[0x06] = s
            if dr.phys != track:
                self.finish(0x18); return
            o = dr.disc.off(side, dr.phys, s)
            if o is None or o + SEC > len(dr.disc.data):
                self.finish(0x18); return
            if size != SEC:
                self.finish(0x18); return
            if i: yield ('sleep', self.t.sector_gap)
            if op in (0x12, 0x13, 0x16, 0x17):
                for k in range(size):
                    r = yield from self.xfer_out(dr.disc.data[o + k])
                    if r: self.finish(r); return
            elif op in (0x1E, 0x1F):
                yield ('sleep', size * self.t.byte)
            else:
                for k in range(size):
                    r = yield from self.xfer_in()
                    if r: self.finish(r); return
                    dr.disc.data[o + k] = self.data
                dr.disc.dirty = True
        yield ('sleep', self.t.done)
        self.finish(0)

    def seek(self, d, track, trk_reg):
        dr = self.drives[d]
        reg = self.sp[trk_reg]
        steps = 0
        if track == 0:
            steps = 255 if False else dr.phys
            dr.phys = 0
            yield ('sleep', steps * self.t.seek_per_step + self.t.settle)
            self.sp[trk_reg] = 0
            return 0
        steps = abs(track - reg) if reg != 0xFF else track
        nphys = dr.phys + (track - reg if reg != 0xFF else track)
        dr.phys = max(0, min(dr.disc.tracks + 0 if False else 255, nphys))
        self.sp[trk_reg] = track
        yield ('sleep', steps * self.t.seek_per_step + (self.t.settle if steps else 0))
        return 0

    def xfer_out(self, byte):
        """present a byte to the CPU. late=True: it must be taken within one byte time (real 8271).
        late=False: the 8271 waits for the CPU however long it takes (minimal model)."""
        self.data = byte; self.ndrq = 1; self.int_ = 1; self.ndrq_since = self.now
        r = yield from self.pace('read')
        return r

    def xfer_in(self):
        self.ndrq = 1; self.int_ = 1; self.ndrq_since = self.now
        r = yield from self.pace('write')
        return r

    def pace(self, what):
        t0 = self.now
        if self.t.late:
            yield ('take', self.t.byte)
            if self.ndrq:
                self.ndrq = 0; self.int_ = 0
                self.L('!', 'late DMA (%s)' % what)
                return 0x0A
            return 0
        # no late error: wait for the CPU, then pad to the byte interval
        if self.ndrq:
            yield ('wait', None)
        rest = t0 + self.t.byte - self.now
        if rest > 0: yield ('sleep', rest)
        return 0
