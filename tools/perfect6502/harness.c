/*
 * Drives perfect6502, a simulation of the 6502's own transistors, through
 * short interrupt scenarios and prints every bus cycle. The output is the
 * expected data for tests/Dbhq.Cpu6502.Tests/Interrupts: the answer comes
 * from the chip's circuit, not from a reading of a document.
 *
 * Built and run by generate.sh against a pinned perfect6502 commit.
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "types.h"
#include "netlist_sim.h"
#include "perfect6502.h"

/* Node numbers of the IRQ and NMI pins, from netlist_6502.h at the pinned commit. */
#define NODE_IRQ 103
#define NODE_NMI 1297

#define START 0x0400

static void load(const unsigned char *code, int length)
{
    memset(memory, 0xEA, sizeof memory);
    memcpy(&memory[START], code, length);
    memory[0x0500] = 0x40; /* IRQ and BRK handler: RTI */
    memory[0x0600] = 0x40; /* NMI handler: RTI */
    memory[0xFFFA] = 0x00; memory[0xFFFB] = 0x06;
    memory[0xFFFC] = 0x00; memory[0xFFFD] = 0x04;
    memory[0xFFFE] = 0x00; memory[0xFFFF] = 0x05;
}

/*
 * Cycle 0 is the first opcode fetch from START after reset. A line change
 * scheduled for cycle k is made before that cycle's two half-steps. A line
 * whose off cycle is -1 is held for the rest of the run. A second NMI pulse,
 * nmi2On to nmi2Off, is printed as an "nmi2" line only when there is one.
 */
static void run2(const char *family, int k, const unsigned char *code, int length,
                 int irqOn, int irqOff, int nmiOn, int nmiOff, int nmi2On, int nmi2Off, int cycles)
{
    load(code, length);
    void *state = initAndResetChip();
    int started = 0;
    int index = 0;
    for (int guard = 0; index < cycles; guard++) {
        if (!started && guard > 64) {
            fprintf(stderr, "%s k=%d: no fetch from $%04X after reset\n", family, k, START);
            exit(1);
        }
        if (started) {
            if (index == irqOn) setNode(state, NODE_IRQ, 0);
            if (index == irqOff) setNode(state, NODE_IRQ, 1);
            if (index == nmiOn) setNode(state, NODE_NMI, 0);
            if (index == nmiOff) setNode(state, NODE_NMI, 1);
            if (index == nmi2On) setNode(state, NODE_NMI, 0);
            if (index == nmi2Off) setNode(state, NODE_NMI, 1);
        }
        step(state);
        unsigned short address = readAddressBus(state);
        int read = readRW(state);
        if (!started && address == START && read) {
            started = 1;
            printf("## %s k=%d\n", family, k);
            printf("code");
            for (int i = 0; i < length; i++) printf(" %02X", code[i]);
            printf("\nirq %d %d\nnmi %d %d\n", irqOn, irqOff, nmiOn, nmiOff);
            if (nmi2On >= 0) printf("nmi2 %d %d\n", nmi2On, nmi2Off);
            printf("state A=%02X X=%02X Y=%02X S=%02X P=%02X\n",
                   readA(state), readX(state), readY(state), readSP(state), readP(state));
        }
        step(state);
        if (started) {
            printf("%d %04X %02X %c\n", index, address, readDataBus(state), read ? 'R' : 'W');
            index++;
        }
    }
    destroyChip(state);
}

static void run(const char *family, int k, const unsigned char *code, int length,
                int irqOn, int irqOff, int nmiOn, int nmiOff, int cycles)
{
    run2(family, k, code, length, irqOn, irqOff, nmiOn, nmiOff, -1, -1, cycles);
}

int main(void)
{
    /* CLI, then a loop of 2-, 4- and 5-cycle instructions for the IRQ to land in. */
    static const unsigned char loop[] = { 0x58, 0xEA, 0xA9, 0x01, 0x8D, 0x00, 0x02, 0xE6, 0x10, 0x4C, 0x01, 0x04 };
    /* CLI; LDX #0; BEQ +0 (taken, same page); NOP; NOP; JMP back. */
    static const unsigned char branch[] = { 0x58, 0xA2, 0x00, 0xF0, 0x00, 0xEA, 0xEA, 0x4C, 0x01, 0x04 };
    /* LDA #0; BRK; padding; NOP; JMP to the start. */
    static const unsigned char brk[] = { 0xA9, 0x00, 0x00, 0xEA, 0xEA, 0x4C, 0x00, 0x04 };
    /* SEI; CLI with the IRQ already held: one more instruction runs first. */
    static const unsigned char cli[] = { 0x78, 0x58, 0xEA, 0xEA, 0xEA, 0x4C, 0x02, 0x04 };
    /* CLI; NOP; SEI with an IRQ arriving around it. */
    static const unsigned char sei[] = { 0x58, 0xEA, 0x78, 0xEA, 0xEA, 0x4C, 0x02, 0x04 };
    /* PLP setting the interrupt-disable flag, then clearing it. */
    static const unsigned char plp[] = { 0x58, 0xA9, 0x04, 0x48, 0x28, 0xA9, 0x00, 0x48, 0x28, 0xEA, 0x4C, 0x01, 0x04 };

    /* CLI; LDX #0; JMP $04FC, where BEQ +$10 is taken across a page to $050E. */
    static unsigned char crossing[0x113];
    memset(crossing, 0xEA, sizeof crossing);
    crossing[0x00] = 0x58; crossing[0x01] = 0xA2; crossing[0x02] = 0x00;
    crossing[0x03] = 0x4C; crossing[0x04] = 0xFC; crossing[0x05] = 0x04;
    crossing[0xFC] = 0xF0; crossing[0xFD] = 0x10;
    crossing[0x110] = 0x4C; crossing[0x111] = 0x01; crossing[0x112] = 0x04;

    for (int k = 1; k <= 24; k++) run("irq", k, loop, sizeof loop, k, k + 12, -1, -1, 48);
    for (int k = 1; k <= 24; k++) run("nmi", k, loop, sizeof loop, -1, -1, k, k + 2, 48);
    for (int k = 1; k <= 16; k++) run("branch", k, branch, sizeof branch, k, k + 12, -1, -1, 40);
    for (int k = 1; k <= 16; k++) run("branch-nmi", k, branch, sizeof branch, -1, -1, k, k + 2, 40);
    for (int k = 4; k <= 16; k++) run("crossing", k, crossing, sizeof crossing, k, k + 12, -1, -1, 40);
    for (int k = 1; k <= 12; k++) run("brk", k, brk, sizeof brk, -1, -1, k, k + 2, 40);
    for (int k = 1; k <= 16; k++) run("irq-nmi", k, loop, sizeof loop, 1, 40, k, k + 2, 40);
    run("cli", 1, cli, sizeof cli, 1, 40, -1, -1, 40);
    for (int k = 1; k <= 12; k++) run("sei", k, sei, sizeof sei, k, k + 12, -1, -1, 40);
    for (int k = 1; k <= 16; k++) run("plp", k, plp, sizeof plp, k, k + 12, -1, -1, 48);

    /*
     * Added in task 12b (4 Oct 2026): the runs above release NMI two cycles
     * after it goes active, and they alone said an NMI arriving on a vector
     * read is lost. These hold the line, or pulse it for 1 to 4 cycles, around
     * the vector reads of an IRQ (low byte at cycle 9 with the IRQ at 1), a
     * BRK (low byte at 7) and an NMI (an NMI at 1, then a second one).
     */
    char name[32];
    for (int k = 5; k <= 14; k++) run("irq-nmi-held", k, loop, sizeof loop, 1, 40, k, -1, 48);
    for (int length = 1; length <= 4; length++) {
        snprintf(name, sizeof name, "irq-nmi-pulse%d", length);
        for (int k = 7; k <= 12; k++) run(name, k, loop, sizeof loop, 1, 40, k, k + length, 48);
    }
    for (int k = 4; k <= 11; k++) run("brk-held", k, brk, sizeof brk, -1, -1, k, -1, 40);
    for (int length = 1; length <= 4; length++) {
        snprintf(name, sizeof name, "brk-pulse%d", length);
        for (int k = 5; k <= 10; k++) run(name, k, brk, sizeof brk, -1, -1, k, k + length, 40);
    }
    for (int k = 5; k <= 14; k++) run2("nmi-nmi-held", k, loop, sizeof loop, -1, -1, 1, 3, k, -1, 48);
    for (int length = 1; length <= 4; length++) {
        snprintf(name, sizeof name, "nmi-nmi-pulse%d", length);
        for (int k = 5; k <= 14; k++) run2(name, k, loop, sizeof loop, -1, -1, 1, 3, k, k + length, 48);
    }
    return 0;
}
