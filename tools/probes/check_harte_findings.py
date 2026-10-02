#!/usr/bin/env python3
"""Check what the core's plan assumes against Tom Harte's SingleStepTests.

Each check states one behaviour, runs it over every case in the relevant
test files, and prints PASS or FAIL with the number of cases looked at.
The files are downloaded from a pinned commit into .testdata/harte/ the
first time and reused after that.

    python3 tools/probes/check_harte_findings.py
"""
import collections
import json
import os
import sys
import urllib.request

COMMIT = "2f6980a2d95757486c7bee24355c360e40e2a224"
BASE = f"https://raw.githubusercontent.com/dbhq-uk/65x02/{COMMIT}"
ROOT = os.path.join(os.path.dirname(__file__), "..", "..", ".testdata", "harte")
CMOS = ("wdc65c02", "rockwell65c02", "synertek65c02")
failures = 0


def load(variant, opcode):
    path = os.path.join(ROOT, variant, "v1", f"{opcode}.json")
    if not os.path.exists(path) or os.path.getsize(path) == 0:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        urllib.request.urlretrieve(f"{BASE}/{variant}/v1/{opcode}.json", path)
    with open(path, "rb") as f:
        data = f.read()
    return json.loads(data) if data else []


def ram(case):
    return dict((a, v) for a, v in case["initial"]["ram"])


def operand(case, offset=1):
    return ram(case)[(case["initial"]["pc"] + offset) & 0xFFFF]


def report(label, bad, total):
    global failures
    status = "PASS" if bad == 0 and total > 0 else "FAIL"
    if status == "FAIL":
        failures += 1
    print(f"{status}  {label}  ({total - bad} of {total} cases)")


# Unstable NMOS opcodes: the magic constant.
for op, name, fn in (
    ("8b", "ANE", lambda i, m, k: (i["a"] | k) & i["x"] & m),
    ("ab", "LXA", lambda i, m, k: (i["a"] | k) & m),
):
    cases = load("6502", op)
    bad = sum(1 for c in cases if fn(c["initial"], operand(c), 0xEE) != c["final"]["a"])
    report(f"NMOS {name} (${op.upper()}) uses the magic constant $EE", bad, len(cases))


# SHA, SHX, SHY, TAS: value = register & (high byte of base + 1); on a page
# cross the high byte of the address is replaced by the value.
def store_high_and(op, name, reg, index, indirect):
    cases = load("6502", op)
    bad = 0
    for c in cases:
        i = c["initial"]
        r = ram(c)
        pc = i["pc"]
        if indirect:
            zp = r[(pc + 1) & 0xFFFF]
            base = r[zp] | (r[(zp + 1) & 0xFF] << 8)
        else:
            base = r[(pc + 1) & 0xFFFF] | (r[(pc + 2) & 0xFFFF] << 8)
        address = (base + i[index]) & 0xFFFF
        value = reg(i) & (((base >> 8) + 1) & 0xFF)
        if (base & 0xFF00) != (address & 0xFF00):
            address = (value << 8) | (address & 0xFF)
        write = [x for x in c["cycles"] if x[2] == "write"][-1]
        bad += (write[0], write[1]) != (address, value)
    report(f"NMOS {name} (${op.upper()}) stores register & (H+1), H replaced on a page cross", bad, len(cases))


store_high_and("9c", "SHY", lambda i: i["y"], "x", False)
store_high_and("9e", "SHX", lambda i: i["x"], "y", False)
store_high_and("9f", "SHA abs,Y", lambda i: i["a"] & i["x"], "y", False)
store_high_and("93", "SHA (zp),Y", lambda i: i["a"] & i["x"], "y", True)
store_high_and("9b", "TAS", lambda i: i["a"] & i["x"], "y", False)
cases = load("6502", "9b")
report("NMOS TAS sets S to A & X", sum(1 for c in cases if c["final"]["s"] != c["initial"]["a"] & c["initial"]["x"]), len(cases))

cases = load("6502", "bb")
bad = 0
for c in cases:
    i = c["initial"]
    r = ram(c)
    base = r[(i["pc"] + 1) & 0xFFFF] | (r[(i["pc"] + 2) & 0xFFFF] << 8)
    m = r.get((base + i["y"]) & 0xFFFF)
    v = m & i["s"]
    bad += not (c["final"]["a"] == c["final"]["x"] == c["final"]["s"] == v)
report("NMOS LAS sets A, X and S to memory & S", bad, len(cases))


# Decimal mode, from Bruce Clark's description of the NMOS and CMOS parts.
def adc_decimal(a, m, c, cmos):
    lo = (a & 0x0F) + (m & 0x0F) + c
    if lo >= 0x0A:
        lo = ((lo + 0x06) & 0x0F) + 0x10
    s = (a & 0xF0) + (m & 0xF0) + lo
    signed = ((a & 0xF0) - (256 if a & 0x80 else 0)) + ((m & 0xF0) - (256 if m & 0x80 else 0)) + lo
    n = bool(s & 0x80)
    v = signed < -128 or signed > 127
    if s >= 0xA0:
        s += 0x60
    result = s & 0xFF
    z = ((a + m + c) & 0xFF) == 0
    if cmos:
        n, z = bool(result & 0x80), result == 0
    return result, s >= 0x100, n, v, z


def sbc_decimal(a, m, c, cmos):
    binary = a - m + c - 1
    rb = binary & 0xFF
    v = bool((a ^ m) & (a ^ rb) & 0x80)
    lo = (a & 0x0F) - (m & 0x0F) + c - 1
    if cmos:
        s = binary
        if s < 0:
            s -= 0x60
        if lo < 0:
            s -= 0x06
        result = s & 0xFF
        return result, binary >= 0, bool(result & 0x80), v, result == 0
    if lo < 0:
        lo = ((lo - 0x06) & 0x0F) - 0x10
    s = (a & 0xF0) - (m & 0xF0) + lo
    if s < 0:
        s -= 0x60
    return s & 0xFF, binary >= 0, bool(rb & 0x80), v, rb == 0


def check_decimal(variant, op, fn, cmos):
    cases = [c for c in load(variant, op) if c["initial"]["p"] & 0x08]
    bad = 0
    for c in cases:
        i = c["initial"]
        r, carry, n, v, z = fn(i["a"], operand(c), i["p"] & 1, cmos)
        p = c["final"]["p"]
        bad += (r, carry, n, v, z) != (c["final"]["a"], bool(p & 1), bool(p & 0x80), bool(p & 0x40), bool(p & 2))
    name = "ADC" if op == "69" else "SBC"
    report(f"{variant} decimal {name} #imm: result and all four flags", bad, len(cases))


check_decimal("6502", "69", adc_decimal, False)
check_decimal("6502", "e9", sbc_decimal, False)
for v in CMOS:
    check_decimal(v, "69", adc_decimal, True)
    check_decimal(v, "e9", sbc_decimal, True)
cases = [c for c in load("nes6502", "69") if c["initial"]["p"] & 0x08]
bad = sum(1 for c in cases if c["final"]["a"] != (c["initial"]["a"] + operand(c) + (c["initial"]["p"] & 1)) & 0xFF)
report("2A03 ignores the decimal flag in ADC", bad, len(cases))


# The rest of the undocumented arithmetic.
def arr(a, m, p, decimal):
    c = p & 1
    t = a & m
    r = (c << 7) | (t >> 1)
    n, z, v = bool(c), r == 0, bool((t ^ r) & 0x40)
    if decimal:
        hi, lo = t >> 4, t & 0x0F
        if lo + (lo & 1) > 5:
            r = (r & 0xF0) | ((r + 6) & 0x0F)
        carry = hi + (hi & 1) > 5
        if carry:
            r = (r + 0x60) & 0xFF
    else:
        carry = bool(r & 0x40)
        v = bool(((r >> 6) ^ (r >> 5)) & 1)
    return r, carry, n, v, z


for variant in ("6502", "nes6502"):
    decimal_enabled = variant == "6502"
    checks = {
        "6b": ("ARR", lambda i, m: arr(i["a"], m, i["p"], decimal_enabled and bool(i["p"] & 8))),
        "cb": ("SBX", lambda i, m: (i["a"], ((i["a"] & i["x"]) - m) >= 0, bool(((i["a"] & i["x"]) - m) & 0x80), bool(i["p"] & 0x40), ((i["a"] & i["x"]) - m) & 0xFF == 0)),
        "0b": ("ANC", lambda i, m: (i["a"] & m, bool(i["a"] & m & 0x80), bool(i["a"] & m & 0x80), bool(i["p"] & 0x40), (i["a"] & m) == 0)),
        "4b": ("ALR", lambda i, m: ((i["a"] & m) >> 1, bool(i["a"] & m & 1), False, bool(i["p"] & 0x40), ((i["a"] & m) >> 1) == 0)),
    }
    for op, (name, fn) in checks.items():
        cases = load(variant, op)
        bad = 0
        for c in cases:
            r, carry, n, v, z = fn(c["initial"], operand(c))
            p = c["final"]["p"]
            bad += (r, carry, n, v, z) != (c["final"]["a"], bool(p & 1), bool(p & 0x80), bool(p & 0x40), bool(p & 2))
        report(f"{variant} {name} result and flags", bad, len(cases))


# Status register bits 4 and 5, which have no flip-flop behind them.
for op, name in (("28", "PLP"), ("40", "RTI")):
    cases = load("6502", op)
    bad = 0
    for c in cases:
        pulled = ram(c)[0x100 | ((c["initial"]["s"] + 1) & 0xFF)]
        bad += c["final"]["p"] != ((pulled & ~0x10) | 0x20)
    report(f"{name} loads P with bit 4 cleared and bit 5 set", bad, len(cases))
cases = load("6502", "08")
report("PHP pushes P with bits 4 and 5 set", sum(1 for c in cases if [x[1] for x in c["cycles"] if x[2] == "write"][0] != c["initial"]["p"] | 0x30), len(cases))


# JAM: the bus pattern Harte records for one step.
cases = load("6502", "02")
bad = 0
for c in cases:
    pc = c["initial"]["pc"]
    expected = [pc, (pc + 1) & 0xFFFF, 0xFFFF, 0xFFFE, 0xFFFE] + [0xFFFF] * 6
    bad += [x[0] for x in c["cycles"]] != expected or c["final"]["pc"] != (pc + 1) & 0xFFFF
report("NMOS JAM reads PC, PC+1, $FFFF, $FFFE, $FFFE, then $FFFF six times", bad, len(cases))


# 65C02 bus patterns. Addresses are named relative to the instruction.
def pattern(c, names):
    return tuple(names.get(a, "?") + ("r" if k == "read" else "w") for a, _, k in c["cycles"])


def abs_indexed_names(c, index):
    i = c["initial"]
    r = ram(c)
    pc = i["pc"]
    base = r[(pc + 1) & 0xFFFF] | (r[(pc + 2) & 0xFFFF] << 8)
    eff = (base + i[index]) & 0xFFFF
    names = {eff: "EFF", pc: "PC", (pc + 1) & 0xFFFF: "PC+1", (pc + 2) & 0xFFFF: "PC+2"}
    return names, (base & 0xFF00) != (eff & 0xFF00)


def check_abs_x(op, label, no_cross, cross):
    for v in CMOS:
        cases = load(v, op)
        bad = 0
        for c in cases:
            names, crossed = abs_indexed_names(c, "x")
            if len(set(names)) < 4:
                continue
            bad += pattern(c, names) != (cross if crossed else no_cross)
        report(f"{v} {label}", bad, len(cases))


check_abs_x("bd", "LDA abs,X re-reads PC+2 (the last operand byte) on a page cross",
            ("PCr", "PC+1r", "PC+2r", "EFFr"), ("PCr", "PC+1r", "PC+2r", "PC+2r", "EFFr"))
check_abs_x("1e", "ASL abs,X is read, read, write, with PC+2 re-read only on a page cross",
            ("PCr", "PC+1r", "PC+2r", "EFFr", "EFFr", "EFFw"), ("PCr", "PC+1r", "PC+2r", "PC+2r", "EFFr", "EFFr", "EFFw"))
check_abs_x("fe", "INC abs,X always re-reads PC+2, crossed or not",
            ("PCr", "PC+1r", "PC+2r", "PC+2r", "EFFr", "EFFr", "EFFw"), ("PCr", "PC+1r", "PC+2r", "PC+2r", "EFFr", "EFFr", "EFFw"))

for v in CMOS:
    for op, label in (("69", "ADC"), ("e9", "SBC")):
        cases = [c for c in load(v, op) if c["initial"]["p"] & 0x08]
        where = collections.Counter(c["cycles"][2][0] for c in cases if len(c["cycles"]) == 3)
        fixed = len(where) == 1 and len(cases) == sum(where.values())
        address = f"${next(iter(where)):04X}" if fixed else "varies"
        print(f"NOTE  {v} decimal {label} #imm: third cycle always reads {address}  ({len(cases)} cases)")

for v in CMOS:
    lengths = {}
    for op in ("02", "03", "07", "0f", "44", "54", "5c", "dc", "fc", "cb", "db"):
        cases = load(v, op)
        if not cases:
            lengths[op] = "no data"
            continue
        seen = collections.Counter(((c["final"]["pc"] - c["initial"]["pc"]) & 0xFFFF, len(c["cycles"])) for c in cases)
        top = seen.most_common(1)[0][0]
        lengths[op] = f"{top[0]}B/{top[1]}c" if len(seen) == 1 else "branches"
    print(f"NOTE  {v} undefined and variant opcodes (bytes/cycles): " + ", ".join(f"{k.upper()}={val}" for k, val in lengths.items()))

sys.exit(1 if failures else 0)
