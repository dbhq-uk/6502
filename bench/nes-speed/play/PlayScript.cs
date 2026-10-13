using System.Text.Json;

namespace Dbhq.Bench;

/// <summary>
/// The scripted play of the bundled homebrew, Lan Master, from <c>bench/nes-speed/lan-master-play.json</c>
/// (made by make-script.py, task 3b of the scanline renderer work): the buttons to hold on controller 1 in
/// each PPU frame, the names of the phases, and the last frame of play in a region. The thread-time bench
/// and the line probe both compile this file, so the two read the one script.
/// </summary>
internal sealed class PlayScript
{
    private readonly byte[] _masks;
    private readonly (string Name, int First)[] _phases;

    private PlayScript(byte[] masks, (string Name, int First)[] phases, int limit)
    {
        _masks = masks;
        _phases = phases;
        Limit = limit;
    }

    /// <summary>The last frame of play in the region: past it the script holds nothing.</summary>
    public int Limit { get; }

    /// <summary>The phases' names, in order, each once.</summary>
    public IReadOnlyList<string> PhaseNames => _phases.Select(p => p.Name).Distinct().ToList();

    /// <summary>Reads the script for the region ("NTSC" or "PAL").</summary>
    public static PlayScript Load(string path, string region)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        int limit = root.GetProperty("limit").GetProperty(region).GetInt32();
        var masks = new byte[limit + 1];
        int mask = 0;
        int frame = 0;
        foreach (JsonElement step in root.GetProperty("steps").EnumerateArray())
        {
            int first = step[0].GetInt32();
            for (; frame < first && frame < masks.Length; frame++)
            {
                masks[frame] = (byte)mask;
            }

            mask = step[1].GetInt32();
        }

        for (; frame < masks.Length; frame++)
        {
            masks[frame] = (byte)mask;
        }

        (string, int)[] phases = root.GetProperty("phases").EnumerateArray().Select(p => (p[0].GetString()!, p[1].GetInt32())).ToArray();
        return new PlayScript(masks, phases, limit);
    }

    /// <summary>The buttons held on controller 1 while PPU frame <paramref name="frame"/> runs (frames from 0).</summary>
    public byte MaskAt(long frame) => frame < _masks.Length ? _masks[frame] : (byte)0;

    /// <summary>The name of the phase PPU frame <paramref name="frame"/> is in.</summary>
    public string PhaseAt(long frame)
    {
        string name = _phases[0].Name;
        foreach ((string Name, int First) phase in _phases)
        {
            if (frame >= phase.First)
            {
                name = phase.Name;
            }
        }

        return name;
    }
}
