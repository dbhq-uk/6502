# Throwaway NMOS 6502 interpreter for tracing DFS 1.20. Documented opcodes only.
# Cycle counts are the documented ones (page-cross and branch penalties included).
# No 1 MHz bus stretch is modelled (see disc.md section 9).
class CPU:
    def __init__(s, bus):
        s.b = bus
        s.a = s.x = s.y = 0
        s.sp = 0xFD
        s.pc = 0
        s.c = s.z = s.i = s.d = s.v = s.n = 0
        s.cycles = 0
        s.nmi_prev = 0
        s.nmi_pending = False
        s.hook = None       # called with pc before each instruction; return True to skip
        s.halted = False

    # ---- flags
    def P(s, brk=0):
        return (s.c | s.z << 1 | s.i << 2 | s.d << 3 | brk << 4 | 0x20 | s.v << 6 | s.n << 7)

    def setP(s, p):
        s.c = p & 1; s.z = p >> 1 & 1; s.i = p >> 2 & 1; s.d = p >> 3 & 1
        s.v = p >> 6 & 1; s.n = p >> 7 & 1

    def nz(s, v):
        v &= 0xFF
        s.z = 1 if v == 0 else 0
        s.n = v >> 7
        return v

    def rd(s, a): return s.b.read(a & 0xFFFF)
    def wr(s, a, v): s.b.write(a & 0xFFFF, v & 0xFF)
    def push(s, v):
        s.wr(0x100 | s.sp, v); s.sp = (s.sp - 1) & 0xFF
    def pull(s):
        s.sp = (s.sp + 1) & 0xFF; return s.rd(0x100 | s.sp)
    def fetch(s):
        v = s.rd(s.pc); s.pc = (s.pc + 1) & 0xFFFF; return v
    def fetch16(s):
        lo = s.fetch(); return lo | s.fetch() << 8

    def interrupt(s, vec, brk=0):
        s.push(s.pc >> 8); s.push(s.pc & 0xFF); s.push(s.P(brk))
        s.i = 1
        s.pc = s.rd(vec) | s.rd(vec + 1) << 8

    # ---- ALU
    def adc(s, m):
        if s.d:
            al = (s.a & 0xF) + (m & 0xF) + s.c
            if al >= 0xA: al = ((al + 6) & 0xF) + 0x10
            r = (s.a & 0xF0) + (m & 0xF0) + al
            s.z = 1 if ((s.a + m + s.c) & 0xFF) == 0 else 0
            s.n = r >> 7 & 1
            s.v = 1 if (~(s.a ^ m) & (s.a ^ r) & 0x80) else 0
            if r >= 0xA0: r += 0x60
            s.c = 1 if r >= 0x100 else 0
            s.a = r & 0xFF
        else:
            s.adc_bin(m)

    def sbc(s, m):
        t = s.a - m - (1 - s.c)
        s.v = 1 if ((s.a ^ m) & (s.a ^ t) & 0x80) else 0
        s.c = 0 if t < 0 else 1
        s.nz(t)
        al = (s.a & 0xF) - (m & 0xF) + s.c - 1
        if al < 0: al = ((al - 6) & 0xF) - 0x10
        r = (s.a & 0xF0) - (m & 0xF0) + al
        if r < 0: r -= 0x60
        s.a = r & 0xFF

    def adc_bin(s, m):
        r = s.a + m + s.c
        s.v = 1 if (~(s.a ^ m) & (s.a ^ r) & 0x80) else 0
        s.c = r >> 8 & 1
        s.a = s.nz(r)

    def cmp(s, r, m):
        t = r - m
        s.c = 1 if t >= 0 else 0
        s.nz(t)

    # ---- addressing: each returns (addr, extra_cycle_if_page_crossed)
    def am(s, mode):
        if mode == 'imm': a = s.pc; s.pc += 1; return a, 0
        if mode == 'zp': return s.fetch(), 0
        if mode == 'zpx': return (s.fetch() + s.x) & 0xFF, 0
        if mode == 'zpy': return (s.fetch() + s.y) & 0xFF, 0
        if mode == 'abs': return s.fetch16(), 0
        if mode == 'abx':
            b = s.fetch16(); a = (b + s.x) & 0xFFFF; return a, (b ^ a) >> 8 & 1
        if mode == 'aby':
            b = s.fetch16(); a = (b + s.y) & 0xFFFF; return a, (b ^ a) >> 8 & 1
        if mode == 'inx':
            z = (s.fetch() + s.x) & 0xFF
            return s.rd(z) | s.rd((z + 1) & 0xFF) << 8, 0
        if mode == 'iny':
            z = s.fetch(); b = s.rd(z) | s.rd((z + 1) & 0xFF) << 8
            a = (b + s.y) & 0xFFFF; return a, (b ^ a) >> 8 & 1
        raise Exception(mode)

    def step(s):
        """Execute one instruction (or take an interrupt). Returns cycles used."""
        c0 = s.cycles
        # NMI edge
        nmi = s.b.nmi_line()
        if nmi and not s.nmi_prev:
            s.nmi_pending = True
        s.nmi_prev = nmi
        if s.nmi_pending:
            s.nmi_pending = False
            s.b.on_nmi_taken(s)
            s.interrupt(0xFFFA); s.cycles += 7
            return s.cycles - c0
        if not s.i and s.b.irq_line():
            s.interrupt(0xFFFE); s.cycles += 7
            return s.cycles - c0
        if s.hook and s.hook(s):
            return 1
        op = s.fetch()
        cyc = s.exec(op)
        s.cycles += cyc
        return s.cycles - c0

    def exec(s, op):
        t = TABLE.get(op)
        if t is None:
            raise Exception('bad opcode %02X at %04X' % (op, (s.pc - 1) & 0xFFFF))
        name, mode, cyc, pen = t
        f = getattr(s, 'op_' + name)
        if mode in ('imp', 'acc', 'rel'):
            return cyc + (f(mode) or 0)
        a, x = s.am(mode)
        return cyc + (x if pen else 0) + (f(a) or 0)

    # --- ops with an effective address argument
    def op_LDA(s, a): s.a = s.nz(s.rd(a))
    def op_LDX(s, a): s.x = s.nz(s.rd(a))
    def op_LDY(s, a): s.y = s.nz(s.rd(a))
    def op_STA(s, a): s.wr(a, s.a)
    def op_STX(s, a): s.wr(a, s.x)
    def op_STY(s, a): s.wr(a, s.y)
    def op_AND(s, a): s.a = s.nz(s.a & s.rd(a))
    def op_ORA(s, a): s.a = s.nz(s.a | s.rd(a))
    def op_EOR(s, a): s.a = s.nz(s.a ^ s.rd(a))
    def op_ADC(s, a): s.adc(s.rd(a))
    def op_SBC(s, a):
        m = s.rd(a)
        if s.d: s.sbc(m)
        else: s.adc_bin(m ^ 0xFF)
    def op_CMP(s, a): s.cmp(s.a, s.rd(a))
    def op_CPX(s, a): s.cmp(s.x, s.rd(a))
    def op_CPY(s, a): s.cmp(s.y, s.rd(a))
    def op_BIT(s, a):
        m = s.rd(a); s.z = 1 if (s.a & m) == 0 else 0; s.n = m >> 7; s.v = m >> 6 & 1
    def op_INC(s, a): v = s.nz(s.rd(a) + 1); s.wr(a, v)
    def op_DEC(s, a): v = s.nz(s.rd(a) - 1); s.wr(a, v)
    def op_ASL(s, a):
        if a == 'acc':
            s.c = s.a >> 7; s.a = s.nz(s.a << 1); return
        m = s.rd(a); s.c = m >> 7; s.wr(a, s.nz(m << 1))
    def op_LSR(s, a):
        if a == 'acc':
            s.c = s.a & 1; s.a = s.nz(s.a >> 1); return
        m = s.rd(a); s.c = m & 1; s.wr(a, s.nz(m >> 1))
    def op_ROL(s, a):
        if a == 'acc':
            c = s.c; s.c = s.a >> 7; s.a = s.nz(s.a << 1 | c); return
        m = s.rd(a); c = s.c; s.c = m >> 7; s.wr(a, s.nz(m << 1 | c))
    def op_ROR(s, a):
        if a == 'acc':
            c = s.c; s.c = s.a & 1; s.a = s.nz(s.a >> 1 | c << 7); return
        m = s.rd(a); c = s.c; s.c = m & 1; s.wr(a, s.nz(m >> 1 | c << 7))
    def op_JMP(s, a): s.pc = a
    def op_JMPI(s, a):
        s.pc = s.rd(a) | s.rd((a & 0xFF00) | ((a + 1) & 0xFF)) << 8
    def op_JSR(s, a):
        r = (s.pc - 1) & 0xFFFF; s.push(r >> 8); s.push(r & 0xFF); s.pc = a
    # --- implied
    def op_NOP(s, m): pass
    def op_TAX(s, m): s.x = s.nz(s.a)
    def op_TAY(s, m): s.y = s.nz(s.a)
    def op_TXA(s, m): s.a = s.nz(s.x)
    def op_TYA(s, m): s.a = s.nz(s.y)
    def op_TSX(s, m): s.x = s.nz(s.sp)
    def op_TXS(s, m): s.sp = s.x
    def op_INX(s, m): s.x = s.nz(s.x + 1)
    def op_INY(s, m): s.y = s.nz(s.y + 1)
    def op_DEX(s, m): s.x = s.nz(s.x - 1)
    def op_DEY(s, m): s.y = s.nz(s.y - 1)
    def op_CLC(s, m): s.c = 0
    def op_SEC(s, m): s.c = 1
    def op_CLI(s, m): s.i = 0
    def op_SEI(s, m): s.i = 1
    def op_CLD(s, m): s.d = 0
    def op_SED(s, m): s.d = 1
    def op_CLV(s, m): s.v = 0
    def op_PHA(s, m): s.push(s.a)
    def op_PHP(s, m): s.push(s.P(1))
    def op_PLA(s, m): s.a = s.nz(s.pull())
    def op_PLP(s, m): s.setP(s.pull())
    def op_RTS(s, m):
        lo = s.pull(); s.pc = ((lo | s.pull() << 8) + 1) & 0xFFFF
    def op_RTI(s, m):
        s.setP(s.pull()); lo = s.pull(); s.pc = lo | s.pull() << 8
    def op_BRK(s, m):
        s.pc = (s.pc + 1) & 0xFFFF; s.interrupt(0xFFFE, 1)
    def branch(s, cond):
        o = s.fetch()
        if cond:
            if o > 127: o -= 256
            n = (s.pc + o) & 0xFFFF
            pen = 2 if (n ^ s.pc) & 0xFF00 else 1
            s.pc = n; return pen
        return 0
    def op_BPL(s, m): return s.branch(not s.n)
    def op_BMI(s, m): return s.branch(s.n)
    def op_BVC(s, m): return s.branch(not s.v)
    def op_BVS(s, m): return s.branch(s.v)
    def op_BCC(s, m): return s.branch(not s.c)
    def op_BCS(s, m): return s.branch(s.c)
    def op_BNE(s, m): return s.branch(not s.z)
    def op_BEQ(s, m): return s.branch(s.z)

# name, mode, base cycles, page-cross penalty applies
T = {}
def A(name, d):
    for mode, (op, cyc, pen) in d.items(): T[op] = (name, mode, cyc, pen)
alu = lambda b: {'imm': (b + 8, 2, 0), 'zp': (b + 4, 3, 0), 'zpx': (b + 0x14, 4, 0), 'abs': (b + 0xC, 4, 0),
                 'abx': (b + 0x1C, 4, 1), 'aby': (b + 0x18, 4, 1), 'inx': (b, 6, 0), 'iny': (b + 0x10, 5, 1)}
for n, b in [('ORA', 0x01), ('AND', 0x21), ('EOR', 0x41), ('ADC', 0x61), ('CMP', 0xC1), ('SBC', 0xE1), ('LDA', 0xA1)]:
    A(n, alu(b))
A('STA', {'zp': (0x85, 3, 0), 'zpx': (0x95, 4, 0), 'abs': (0x8D, 4, 0), 'abx': (0x9D, 5, 0), 'aby': (0x99, 5, 0), 'inx': (0x81, 6, 0), 'iny': (0x91, 6, 0)})
A('LDX', {'imm': (0xA2, 2, 0), 'zp': (0xA6, 3, 0), 'zpy': (0xB6, 4, 0), 'abs': (0xAE, 4, 0), 'aby': (0xBE, 4, 1)})
A('LDY', {'imm': (0xA0, 2, 0), 'zp': (0xA4, 3, 0), 'zpx': (0xB4, 4, 0), 'abs': (0xAC, 4, 0), 'abx': (0xBC, 4, 1)})
A('STX', {'zp': (0x86, 3, 0), 'zpy': (0x96, 4, 0), 'abs': (0x8E, 4, 0)})
A('STY', {'zp': (0x84, 3, 0), 'zpx': (0x94, 4, 0), 'abs': (0x8C, 4, 0)})
A('CPX', {'imm': (0xE0, 2, 0), 'zp': (0xE4, 3, 0), 'abs': (0xEC, 4, 0)})
A('CPY', {'imm': (0xC0, 2, 0), 'zp': (0xC4, 3, 0), 'abs': (0xCC, 4, 0)})
A('BIT', {'zp': (0x24, 3, 0), 'abs': (0x2C, 4, 0)})
for n, b in [('INC', 0xE6), ('DEC', 0xC6), ('ASL', 0x06), ('LSR', 0x46), ('ROL', 0x26), ('ROR', 0x66)]:
    A(n, {'zp': (b, 5, 0), 'zpx': (b + 0x10, 6, 0), 'abs': (b + 8, 6, 0), 'abx': (b + 0x18, 7, 0)})
for n, b in [('ASL', 0x0A), ('LSR', 0x4A), ('ROL', 0x2A), ('ROR', 0x6A)]:
    T[b] = (n + '_A', 'acc', 2, 0)
A('JMP', {'abs': (0x4C, 3, 0)}); T[0x6C] = ('JMPI', 'abs', 5, 0)
A('JSR', {'abs': (0x20, 6, 0)})
for n, o in [('BPL', 0x10), ('BMI', 0x30), ('BVC', 0x50), ('BVS', 0x70), ('BCC', 0x90), ('BCS', 0xB0), ('BNE', 0xD0), ('BEQ', 0xF0)]:
    T[o] = (n, 'rel', 2, 0)
for n, o, c in [('BRK', 0, 7), ('PHP', 0x08, 3), ('CLC', 0x18, 2), ('PLP', 0x28, 4), ('SEC', 0x38, 2), ('RTI', 0x40, 6), ('PHA', 0x48, 3),
                ('CLI', 0x58, 2), ('RTS', 0x60, 6), ('PLA', 0x68, 4), ('SEI', 0x78, 2), ('DEY', 0x88, 2), ('TXA', 0x8A, 2), ('TYA', 0x98, 2),
                ('TXS', 0x9A, 2), ('TAY', 0xA8, 2), ('TAX', 0xAA, 2), ('CLV', 0xB8, 2), ('TSX', 0xBA, 2), ('INY', 0xC8, 2), ('DEX', 0xCA, 2),
                ('CLD', 0xD8, 2), ('INX', 0xE8, 2), ('NOP', 0xEA, 2), ('SED', 0xF8, 2)]:
    T[o] = (n, 'imp', c, 0)
TABLE = T

# accumulator-mode shifts call op_X with 'acc' as the "address"
for _n in ('ASL', 'LSR', 'ROL', 'ROR'):
    setattr(CPU, 'op_' + _n + '_A', (lambda nm: (lambda s, m: getattr(s, 'op_' + nm)('acc')))(_n))
