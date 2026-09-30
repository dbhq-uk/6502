using System.Text.Json;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Cpu6502.Tests.Harte;

public sealed record HarteState(ushort PC, byte S, byte A, byte X, byte Y, byte P, (ushort Address, byte Value)[] Ram);

public sealed record HarteCase(string Name, HarteState Initial, HarteState Final, BusAccess[] Cycles);

/// <summary>Reads one of Tom Harte's SingleStepTests files: 10,000 cases for one opcode.</summary>
public static class HarteFile
{
    public static IReadOnlyList<HarteCase> Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0)
        {
            return [];
        }

        using var document = JsonDocument.Parse(bytes);
        var cases = new List<HarteCase>(document.RootElement.GetArrayLength());
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            cases.Add(new HarteCase(
                element.GetProperty("name").GetString()!,
                State(element.GetProperty("initial")),
                State(element.GetProperty("final")),
                element.GetProperty("cycles").EnumerateArray()
                    .Select(c => new BusAccess((ushort)c[0].GetInt32(), (byte)c[1].GetInt32(), c[2].GetString() == "write"))
                    .ToArray()));
        }

        return cases;
    }

    private static HarteState State(JsonElement element) => new(
        (ushort)element.GetProperty("pc").GetInt32(),
        (byte)element.GetProperty("s").GetInt32(),
        (byte)element.GetProperty("a").GetInt32(),
        (byte)element.GetProperty("x").GetInt32(),
        (byte)element.GetProperty("y").GetInt32(),
        (byte)element.GetProperty("p").GetInt32(),
        element.GetProperty("ram").EnumerateArray()
            .Select(r => ((ushort)r[0].GetInt32(), (byte)r[1].GetInt32()))
            .ToArray());
}
