#!/usr/bin/env python3
"""Disassemble the KIM-1 monitor's teletype routines from the ROM in roms/.

Reads roms/kim-1/6530-002.bin, checks it against its pinned SHA-256, and
prints each routine the teletype uses, labelled with the name the KIM-1 User
Manual's listing gives it. The teletype model (src/Dbhq.Machines.Kim1/
Kim1Teletype.cs) was written from this listing and the manual, not from
memory.

    python3 tools/probes/kim1_serial_routines.py
"""
import hashlib
import os
import sys

ROM = os.path.join(os.path.dirname(__file__), "..", "..", "roms", "kim-1", "6530-002.bin")
SHA256 = "e9e5245854603cdbc0208235310db1bf6e6a75904960385baaddd38fe60ef750"
BASE = 0x1C00

# The routines, in the order the monitor uses them: (first, last + 1, name, what it does).
ROUTINES = [
    (0x1C22, 0x1C4F, "RST", "reset: SAD bit 0 low (the TTY/KB switch) means teletype; then wait for PA7 to fall and count the start bit of RUB OUT into CNTH30/CNTL30 ($17F3/$17F2)"),
    (0x1C4F, 0x1C64, "START", "the switch again; in teletype mode CR LF, then KIM, then the open cell"),
    (0x1C64, 0x1C77, "READ", "GETCH, and anything that is not a command is taken as a hex digit"),
    (0x1DA9, 0x1E1E, "commands", "SPACE, RUB OUT, CR, LF, '.', G, Q and L"),
    (0x1E2F, 0x1E5A, "CRLF, PRTST, PRTBYT", "print CR LF and six NULs, a string from the table at $1FD5, a byte as two hex digits"),
    (0x1E5A, 0x1E88, "GETCH", "wait for a start bit on PA7, a bit and a half, then eight samples a bit apart, least significant first; bit 7 dropped"),
    (0x1E88, 0x1E9E, "INITS, INIT1", "PA all inputs, PB0 to PB5 outputs, SBD = $07: PB0 high (the printer loop marking), PB5 low (PA7 follows the keyboard), the 74145 on output 3"),
    (0x1E9E, 0x1ED4, "OUTSP, OUTCH", "a bit of mark, then the start bit and eight bits on PB0, least significant first, then a stop bit"),
    (0x1ED4, 0x1EFE, "DELAY, DEHALF", "count CNTL30/CNTH30 down 14 cycles a step: one bit, or half of one"),
]
tbl = """
00 BRK imp;01 ORA izx;05 ORA zp;06 ASL zp;08 PHP imp;09 ORA imm;0A ASL acc;0D ORA abs;0E ASL abs;10 BPL rel;11 ORA izy;15 ORA zpx;16 ASL zpx;18 CLC imp;19 ORA aby;1D ORA abx;1E ASL abx;
20 JSR abs;21 AND izx;24 BIT zp;25 AND zp;26 ROL zp;28 PLP imp;29 AND imm;2A ROL acc;2C BIT abs;2D AND abs;2E ROL abs;30 BMI rel;31 AND izy;35 AND zpx;36 ROL zpx;38 SEC imp;39 AND aby;3D AND abx;3E ROL abx;
40 RTI imp;41 EOR izx;45 EOR zp;46 LSR zp;48 PHA imp;49 EOR imm;4A LSR acc;4C JMP abs;4D EOR abs;4E LSR abs;50 BVC rel;51 EOR izy;55 EOR zpx;56 LSR zpx;58 CLI imp;59 EOR aby;5D EOR abx;5E LSR abx;
60 RTS imp;61 ADC izx;65 ADC zp;66 ROR zp;68 PLA imp;69 ADC imm;6A ROR acc;6C JMP ind;6D ADC abs;6E ROR abs;70 BVS rel;71 ADC izy;75 ADC zpx;76 ROR zpx;78 SEI imp;79 ADC aby;7D ADC abx;7E ROR abx;
81 STA izx;84 STY zp;85 STA zp;86 STX zp;88 DEY imp;8A TXA imp;8C STY abs;8D STA abs;8E STX abs;90 BCC rel;91 STA izy;94 STY zpx;95 STA zpx;96 STX zpy;98 TYA imp;99 STA aby;9A TXS imp;9D STA abx;
A0 LDY imm;A1 LDA izx;A2 LDX imm;A4 LDY zp;A5 LDA zp;A6 LDX zp;A8 TAY imp;A9 LDA imm;AA TAX imp;AC LDY abs;AD LDA abs;AE LDX abs;B0 BCS rel;B1 LDA izy;B4 LDY zpx;B5 LDA zpx;B6 LDX zpy;B8 CLV imp;B9 LDA aby;BA TSX imp;BC LDY abx;BD LDA abx;BE LDX aby;
C0 CPY imm;C1 CMP izx;C4 CPY zp;C5 CMP zp;C6 DEC zp;C8 INY imp;C9 CMP imm;CA DEX imp;CC CPY abs;CD CMP abs;CE DEC abs;D0 BNE rel;D1 CMP izy;D5 CMP zpx;D6 DEC zpx;D8 CLD imp;D9 CMP aby;DD CMP abx;DE DEC abx;
E0 CPX imm;E1 SBC izx;E4 CPX zp;E5 SBC zp;E6 INC zp;E8 INX imp;E9 SBC imm;EA NOP imp;EC CPX abs;ED SBC abs;EE INC abs;F0 BEQ rel;F1 SBC izy;F5 SBC zpx;F6 INC zpx;F8 SED imp;F9 SBC aby;FD SBC abx;FE INC abx
"""
size = {"imp": 1, "acc": 1, "imm": 2, "zp": 2, "zpx": 2, "zpy": 2, "izx": 2, "izy": 2, "rel": 2, "abs": 3, "abx": 3, "aby": 3, "ind": 3}
ops = {}
for entry in tbl.replace("\n", "").split(";"):
    entry = entry.strip()
    if entry:
        code, mnemonic, mode = entry.split()
        ops[int(code, 16)] = (mnemonic, mode)


def disassemble(rom, start, end):
    pc = start
    while pc < end:
        op = rom[pc - BASE]
        if op not in ops:
            print(f"{pc:04X}  {op:02X}        .byte")
            pc += 1
            continue
        mnemonic, mode = ops[op]
        n = size[mode]
        b = rom[pc - BASE:pc - BASE + n]
        if mode == "rel":
            arg = f"${pc + 2 + ((b[1] ^ 0x80) - 0x80):04X}"
        elif n == 2:
            arg = {"imm": "#${:02X}", "zp": "${:02X}", "zpx": "${:02X},X", "zpy": "${:02X},Y", "izx": "(${:02X},X)", "izy": "(${:02X}),Y"}[mode].format(b[1])
        elif n == 3:
            arg = {"abs": "${:04X}", "abx": "${:04X},X", "aby": "${:04X},Y", "ind": "(${:04X})"}[mode].format(b[1] | b[2] << 8)
        else:
            arg = "A" if mode == "acc" else ""
        print(f"{pc:04X}  {' '.join(f'{x:02X}' for x in b):<9} {mnemonic} {arg}".rstrip())
        pc += n


rom = open(ROM, "rb").read()
digest = hashlib.sha256(rom).hexdigest()
if digest != SHA256:
    sys.exit(f"{ROM} does not match its pin: {digest}")
print(f"6530-002, SHA-256 {digest}")
for start, end, name, what in ROUTINES:
    print(f"\n; {name}: {what}")
    disassemble(rom, start, end)
table = rom[0x1FD5 - BASE:0x1FE7 - BASE]
print("\n; the table at $1FD5, printed from its end down: " + " ".join(f"{x:02X}" for x in table))
