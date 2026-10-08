using System.Runtime.InteropServices;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>What an access in a dot scene does.</summary>
internal enum DotAccessKind
{
    /// <summary>A write of a register (<see cref="Ppu.WriteRegister"/>).</summary>
    Write,

    /// <summary>A read of a register, with its side effects (<see cref="Ppu.ReadRegister"/>).</summary>
    Read,

    /// <summary>A read of the picture (<see cref="Ppu.Screen"/>): a catch-up with no register access, as the page's and the tools' reads are.</summary>
    Picture,
}

/// <summary>
/// One access of a dot scene, made once the scene's start has been moved on by
/// <paramref name="Offset"/> dots: the PPU's next dot is the one <paramref name="Offset"/> dots
/// after the start, so the access comes before that dot runs. With <paramref name="Line"/> and
/// <paramref name="Dot"/> given (<see cref="At"/>), the driver checks that the offset puts the
/// PPU there, so a scene whose offsets are wrong (across an odd frame's dropped dot, say) fails
/// rather than testing another dot than it names.
/// </summary>
internal readonly record struct DotAccess(int Offset, DotAccessKind Kind, int Register = 0, byte Value = 0, int Line = -1, int Dot = -1)
{
    /// <summary>The same access, with the position the PPU must be at when it is made.</summary>
    public DotAccess At(int line, int dot) => this with { Line = line, Dot = dot };

    public static DotAccess Write(int offset, int register, byte value) => new(offset, DotAccessKind.Write, register, value);

    public static DotAccess Read(int offset, int register) => new(offset, DotAccessKind.Read, register);

    public static DotAccess Picture(int offset) => new(offset, DotAccessKind.Picture);
}

/// <summary>
/// A dot scene: where it starts (a line and dot of an odd or an even frame, with the flags of
/// <paramref name="Status"/>, set by <see cref="Ppu.MoveTo"/>), its accesses in order, and how
/// many dots it runs in all. <paramref name="Name"/> is what a failure names.
/// </summary>
internal sealed record DotScene(string Name, int Line, int Dot, bool OddFrame, byte Status, IReadOnlyList<DotAccess> Accesses, int Length);

/// <summary>
/// Two PPUs built the same way and run through the same dot scenes: the per-dot reference, which
/// runs each dot as it is delivered (<see cref="Ppu.Tick"/>), and the lazy one, which is delivered
/// the dots up to each access at once (<see cref="Ppu.Deliver"/>) and runs them in the catch-up
/// the access makes, as the bus's lazy build does. After each access and at each scene's end the
/// two must agree on everything: what a read gave, the whole state report with the picture, the
/// logical position, the dots delivered and run, the next event, and the picture at every frame
/// end on the way. The first difference is named, field by field, and a picture's by its row
/// and column.
/// </summary>
/// <remarks>
/// <para>
/// Scenes run one after another on the same pair, each from where it moves the PPUs to, so a
/// table of thousands costs one setup. The state a scene leaves is the same in both, which the
/// check at its end shows, so the next starts from equal states too.
/// </para>
/// <para>
/// The catch-ups the lazy PPU makes are the scene's: from its start to its first access, between
/// accesses, and from the last to its end, each as many dots, and so as many whole lines, as the
/// scene says. A second implementation of the line inside <see cref="Ppu.CatchUp"/>, such as a
/// fast scanline renderer, is held to the reference's dot-by-dot path by these scenes at every dot
/// an access is placed on.
/// </para>
/// </remarks>
internal sealed class DotScenePair
{
    /// <summary>
    /// What every pixel is set to as each scene starts: no colour the PPU draws (its alpha is 0),
    /// so a pixel the scene should draw and does not is seen, and none is left right by the scene
    /// before.
    /// </summary>
    public const uint Unpainted = 0x00FF00FF;

    private readonly StateBytes _reference = new();
    private readonly StateBytes _lazy = new();
    private readonly FrameEnds _referenceFrames;
    private readonly FrameEnds _lazyFrames;

    public DotScenePair(Func<Ppu> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        Reference = build();
        Lazy = build();
        _referenceFrames = new FrameEnds(Reference);
        _lazyFrames = new FrameEnds(Lazy);
        Reference.Observer = _referenceFrames;
        Lazy.Observer = _lazyFrames;
        Check(null, null, Reference.Line, Reference.Dot);
    }

    /// <summary>The per-dot reference.</summary>
    public Ppu Reference { get; }

    /// <summary>The lazy PPU.</summary>
    public Ppu Lazy { get; }

    /// <summary>How many accesses and scene ends were checked.</summary>
    public int Checks { get; private set; }

    /// <summary>Called with the reference after each dot it runs, for a test that watches a scene from inside; null for none.</summary>
    public Action<Ppu>? EachDot { get; set; }

    /// <summary>Runs <paramref name="scene"/> on both and checks them after each access and at its end.</summary>
    public void Run(DotScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Reference.MoveTo(scene.Line, scene.Dot, scene.OddFrame, scene.Status);
        Lazy.MoveTo(scene.Line, scene.Dot, scene.OddFrame, scene.Status);
        Array.Fill(Reference.PixelsAsRun, Unpainted);
        Array.Fill(Lazy.PixelsAsRun, Unpainted);
        int at = 0;
        foreach (DotAccess access in scene.Accesses)
        {
            if (access.Offset < at || access.Offset > scene.Length)
            {
                throw new ArgumentException($"{scene.Name}: an access at {access.Offset} dots, after one at {at} or past the scene's {scene.Length}");
            }

            MoveOn(access.Offset - at);
            at = access.Offset;
            int line = Reference.Line;
            int dot = Reference.Dot;
            if (access.Line >= 0 && (line, dot) != (access.Line, access.Dot))
            {
                Assert.Fail($"{scene.Name}: the {access.Kind} at {access.Offset} dots was meant for line {access.Line} dot {access.Dot}, and the PPU is at line {line} dot {dot}");
            }

            if ((line, dot) != (Lazy.Line, Lazy.Dot))
            {
                Assert.Fail($"{scene.Name}: before the {access.Kind} at {access.Offset} dots the reference is at line {line} dot {dot} and the lazy PPU at line {Lazy.Line} dot {Lazy.Dot}");
            }

            int a = Do(Reference, access);
            int b = Do(Lazy, access);
            if (a != b)
            {
                Assert.Fail($"{scene.Name}: the {access.Kind} of register {access.Register} at line {line} dot {dot} gave ${a:X2} on the reference and ${b:X2} on the lazy PPU");
            }

            Check(scene, access, line, dot);
        }

        MoveOn(scene.Length - at);
        Lazy.CatchUp();
        Check(scene, null, Reference.Line, Reference.Dot);
    }

    private void MoveOn(int dots)
    {
        Action<Ppu>? each = EachDot;
        for (int i = 0; i < dots; i++)
        {
            Reference.Tick();
            each?.Invoke(Reference);
        }

        Lazy.Deliver(dots);
    }

    private static int Do(Ppu ppu, DotAccess access)
    {
        switch (access.Kind)
        {
            case DotAccessKind.Write:
                ppu.WriteRegister(access.Register, access.Value);
                return -1;
            case DotAccessKind.Read:
                return ppu.ReadRegister(access.Register);
            default:
                _ = ppu.Screen;
                return -1;
        }
    }

    // Both the same, or a failure that names the scene, the access (none at a scene's end) and
    // the position, and the first thing that differs.
    private void Check(DotScene? scene, DotAccess? access, int line, int dot)
    {
        Checks++;
        string? difference = null;
        if (Reference.LogicalDots != Lazy.LogicalDots || Reference.CaughtUpDots != Lazy.CaughtUpDots)
        {
            difference = $"the dots: delivered {Reference.LogicalDots} and run {Reference.CaughtUpDots} on the reference, {Lazy.LogicalDots} and {Lazy.CaughtUpDots} on the lazy PPU";
        }
        else if (Reference.NextEventDot != Lazy.NextEventDot)
        {
            difference = $"the next event: {Reference.NextEventDot} on the reference, {Lazy.NextEventDot} on the lazy PPU";
        }
        else if ((Reference.Line, Reference.Dot, Reference.Frame) != (Lazy.Line, Lazy.Dot, Lazy.Frame))
        {
            difference = $"the position: line {Reference.Line} dot {Reference.Dot} frame {Reference.Frame} on the reference, line {Lazy.Line} dot {Lazy.Dot} frame {Lazy.Frame} on the lazy PPU";
        }
        else if (_referenceFrames.Count != _lazyFrames.Count)
        {
            difference = $"the frame ends: {_referenceFrames.Count} on the reference and {_lazyFrames.Count} on the lazy PPU since the last check";
        }
        else
        {
            for (int i = 0; i < _referenceFrames.Count && difference is null; i++)
            {
                difference = StateBytes.FirstDifference(_referenceFrames.Taken[i], _lazyFrames.Taken[i]);
                difference = difference is null ? null : $"at the frame end since the last check, {difference}";
            }

            difference ??= StateBytes.FirstDifference(_reference.Of(Reference), _lazy.Of(Lazy));
        }

        _referenceFrames.Count = 0;
        _lazyFrames.Count = 0;

        if (difference is not null)
        {
            string when = access is { } made ? $"after the {made.Kind} of register {made.Register} at line {line} dot {dot}" : $"at the end, line {line} dot {dot}";
            Assert.Fail($"{scene?.Name ?? "the setup"}, {when}: {difference}");
        }
    }

    // The whole state at each frame end since the last check, the picture with it, kept as bytes
    // to be compared at the next check.
    private sealed class FrameEnds(Ppu ppu) : INesObserver
    {
        public List<StateBytes> Taken { get; } = [];

        public int Count { get; set; }

        public void Accessed(ushort address, bool write, byte value)
        {
        }

        public void ChipsReset(bool power)
        {
        }

        public void CycleEnded(bool nmi, bool irq)
        {
        }

        public void DmcFetched()
        {
        }

        public void FrameEnded()
        {
            if (Count == Taken.Count)
            {
                Taken.Add(new StateBytes());
            }

            Taken[Count++].Of(ppu);
        }
    }
}

/// <summary>
/// A part's whole state report as bytes, each field kept apart under its name (a part's fields
/// under the part's name), so two can be compared at memory speed and the first field that
/// differs named. It reads only, so it catches nothing up. One is made once and filled again
/// each time (<see cref="Of"/>).
/// </summary>
internal sealed class StateBytes : IStateSink
{
    private readonly List<(string Name, int Start, int Length, int Size)> _fields = [];
    private byte[] _bytes = new byte[1 << 19];
    private int _length;
    private string _prefix = "";

    /// <summary>Fills this with <paramref name="part"/>'s report and returns it.</summary>
    public StateBytes Of(IReportsState part)
    {
        ArgumentNullException.ThrowIfNull(part);
        _fields.Clear();
        _length = 0;
        _prefix = "";
        part.ReportState(this);
        return this;
    }

    /// <summary>The first field in which the two differ, with the values or, for an array, the first element that differs; null when they are the same.</summary>
    public static string? FirstDifference(StateBytes a, StateBytes b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a._length == b._length && a._fields.Count == b._fields.Count && a._bytes.AsSpan(0, a._length).SequenceEqual(b._bytes.AsSpan(0, b._length)))
        {
            return null;
        }

        for (int i = 0; i < Math.Min(a._fields.Count, b._fields.Count); i++)
        {
            var (name, start, length, size) = a._fields[i];
            var other = b._fields[i];
            ReadOnlySpan<byte> x = a._bytes.AsSpan(start, length);
            ReadOnlySpan<byte> y = b._bytes.AsSpan(other.Start, other.Length);
            if (name != other.Name || x.SequenceEqual(y))
            {
                if (name != other.Name)
                {
                    return $"the reports differ in shape at {name} and {other.Name}";
                }

                continue;
            }

            if (x.Length != y.Length || size == 0)
            {
                return $"{name}: {Convert.ToHexString(x)} on the reference, {Convert.ToHexString(y)} on the lazy PPU";
            }

            int at = 0;
            while (x.Slice(at, size).SequenceEqual(y.Slice(at, size)))
            {
                at += size;
            }

            int index = at / size;
            string element = name.EndsWith("Pixels", StringComparison.Ordinal)
                ? $"pixel {index % FrameBuffer.Width} of row {index / FrameBuffer.Width}"
                : $"element {index}";
            return $"{name}, {element}: {Convert.ToHexString(x.Slice(at, size))} on the reference, {Convert.ToHexString(y.Slice(at, size))} on the lazy PPU";
        }

        return $"the reports differ in length: {a._fields.Count} fields and {b._fields.Count}";
    }

    public void Add(string name, long value) => Put(name, MemoryMarshal.AsBytes(new ReadOnlySpan<long>(ref value)), 0);

    public void Add(string name, ulong value) => Put(name, MemoryMarshal.AsBytes(new ReadOnlySpan<ulong>(ref value)), 0);

    public void Add(string name, bool value) => Put(name, [value ? (byte)1 : (byte)0], 0);

    public void Add(string name, double value) => Put(name, MemoryMarshal.AsBytes(new ReadOnlySpan<double>(ref value)), 0);

    public void Add(string name, ReadOnlySpan<byte> values) => Put(name, values, 1);

    public void Add(string name, ReadOnlySpan<int> values) => Put(name, MemoryMarshal.AsBytes(values), sizeof(int));

    public void Add(string name, ReadOnlySpan<uint> values) => Put(name, MemoryMarshal.AsBytes(values), sizeof(uint));

    public void Add(string name, ReadOnlySpan<long> values) => Put(name, MemoryMarshal.AsBytes(values), sizeof(long));

    public void Add(string name, ReadOnlySpan<float> values) => Put(name, MemoryMarshal.AsBytes(values), sizeof(float));

    public void Add(string name, ReadOnlySpan<double> values) => Put(name, MemoryMarshal.AsBytes(values), sizeof(double));

    public void Add(string name, IReportsState part)
    {
        ArgumentNullException.ThrowIfNull(part);
        string saved = _prefix;
        _prefix = saved + name + ".";
        part.ReportState(this);
        _prefix = saved;
    }

    public void Skip(string name, string why)
    {
    }

    // A field's bytes, with the size of one element of it (0 for a single value).
    private void Put(string name, ReadOnlySpan<byte> bytes, int size)
    {
        if (_length + bytes.Length > _bytes.Length)
        {
            Array.Resize(ref _bytes, Math.Max(_bytes.Length * 2, _length + bytes.Length));
        }

        bytes.CopyTo(_bytes.AsSpan(_length));
        _fields.Add((_prefix + name, _length, bytes.Length, size));
        _length += bytes.Length;
    }
}
