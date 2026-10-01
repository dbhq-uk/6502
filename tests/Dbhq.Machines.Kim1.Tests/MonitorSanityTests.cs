using Dbhq.Cpu6502;
using Xunit;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// A long run of the monitor with the keypad in use: every instruction it
/// fetches is a documented opcode in its own ROM, and the CPU never locks up.
/// A wrong byte in the ROM image, or a decode that put RAM or open bus where
/// the ROM should be, sends the CPU off into data, and this fails.
/// </summary>
public sealed class MonitorSanityTests
{
    [Fact]
    public void TheMonitorRunsFiveMillionCyclesOfKeypadUseOnDocumentedOpcodesInItsOwnRom()
    {
        const long cycles = 5_000_000;
        var kim = new Kim1Session();
        kim.Machine.PressReset();

        // A fixed walk over every key but GO, which would run whatever RAM holds.
        Kim1Key[] walk = [.. Enum.GetValues<Kim1Key>().Where(k => k != Kim1Key.Go)];
        long nextChange = Kim1Session.HoldCycles;
        int press = 0;
        long instructions = 0;
        while (kim.Machine.Cycles < cycles)
        {
            if (kim.Machine.Cycles >= nextChange)
            {
                kim.Machine.Keypad.ReleaseAll();
                if (press % 2 == 0)
                {
                    kim.Machine.Keypad.Press(walk[press / 2 * 7 % walk.Length]);
                }

                press++;
                nextChange += Kim1Session.HoldCycles;
            }

            ushort pc = kim.Machine.Cpu.PC;
            Assert.True(pc is >= 0x1800 and <= 0x1FFF, $"the CPU left the monitor ROM: PC ${pc:X4} after {instructions:N0} instructions");
            OpcodeInfo opcode = OpcodeTable.Get(CpuVariant.Nmos6502, kim.Machine.Bus.Peek(pc));
            Assert.False(opcode.Undocumented, $"undocumented opcode {opcode.Mnemonic} at ${pc:X4}");
            kim.Machine.Step();
            Assert.False(kim.Machine.Cpu.IsJammed);
            instructions++;
        }

        // Every key was pressed at least once.
        Assert.True(press / 2 >= walk.Length, $"only {press / 2} presses in {cycles:N0} cycles");
    }
}
