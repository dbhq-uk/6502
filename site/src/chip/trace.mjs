// Turns the core's recorded trace (src/data/chip-trace.json, written by
// tools/Dbhq.Cpu6502.ChipTrace) into one frame per bus cycle for the chip page.
// Pure, so the tests can check it in Node.

/** Instructions that compute a result in the ALU. Loads, stores, transfers, branches and flag changes do not count. */
export const ALU_MNEMONICS = new Set([
  'ADC', 'SBC', 'AND', 'ORA', 'EOR', 'CMP', 'CPX', 'CPY', 'BIT',
  'INC', 'DEC', 'INX', 'INY', 'DEX', 'DEY', 'ASL', 'LSR', 'ROL', 'ROR',
]);

const REGS = ['a', 'x', 'y', 's', 'p'];
const named = (r) => Object.fromEntries(REGS.map((k, i) => [k, r[i]]));

/**
 * One frame per bus cycle. A frame carries the cycle's bus activity, the
 * instruction it belongs to, and the registers as the core holds them: the
 * state left by the previous instruction, until the instruction's last cycle,
 * when they take its result. So a register changes on the cycle that finishes
 * the instruction, and `changed` names the ones that did.
 */
export function frames(trace) {
  const out = [];
  let regs = named(trace.start.regs);
  trace.instructions.forEach((ins, k) => {
    const after = named(ins.after);
    const changed = REGS.filter((r) => after[r] !== regs[r]);
    for (let c = 0; c < ins.cycles; c++) {
      const i = ins.first + c;
      const [address, data, write] = trace.cycles[i];
      const last = c === ins.cycles - 1;
      const now = last ? after : regs;
      out.push({
        cycle: i,
        address,
        data,
        write: write === 1,
        fetch: c === 0,
        last,
        instruction: k,
        pc: ins.pc,
        text: ins.text,
        mnemonic: ins.mnemonic,
        alu: ALU_MNEMONICS.has(ins.mnemonic),
        regs: { ...now, pch: ins.pc >> 8, pcl: ins.pc & 0xff, abh: address >> 8, abl: address & 0xff, dl: data },
        changed: last ? changed : [],
      });
    }
    regs = after;
  });
  return out;
}

/** The bits of a value, bit 0 first. */
export const bits = (value, width = 8) => Array.from({ length: width }, (_, b) => (value >> b) & 1);

/** Which of a 16-bit address's lines differ from the previous cycle's. */
export const toggled = (a, b, width) => bits(a ^ b, width).map(Boolean);

export const hex = (value, digits) => '$' + value.toString(16).toUpperCase().padStart(digits, '0');
