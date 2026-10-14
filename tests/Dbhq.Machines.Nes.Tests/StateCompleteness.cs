using System.Reflection;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// Checks that a machine's state reports (<see cref="IReportsState"/>) name every field: from a
/// root, each part's report is read for names, and the names are compared with the instance
/// fields its class and the classes above it declare, by reflection. A field that is neither
/// reported nor skipped is a hole in the differential, and a name that is no field is a report out
/// of date. Shared by <c>StateReportTests</c> and the differential in
/// <c>bench/nes-speed/differential</c>, which runs it before it runs anything else.
/// </summary>
public static class StateCompleteness
{
    /// <summary>The problems found under <paramref name="root"/>, one line each, and the classes it looked at; no problems is an empty list.</summary>
    internal static (List<string> Problems, List<Type> Checked) Check(IReportsState root)
    {
        var problems = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var checkedTypes = new List<Type>();
        var queue = new Queue<IReportsState>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            IReportsState part = queue.Dequeue();
            if (!seen.Add(part))
            {
                continue;
            }

            var names = new NameSink();
            part.ReportState(names);
            foreach (IReportsState child in names.Parts)
            {
                queue.Enqueue(child);
            }

            Type type = part.GetType();
            if (!checkedTypes.Contains(type))
            {
                checkedTypes.Add(type);
            }

            var fields = Fields(type).ToHashSet(StringComparer.Ordinal);
            foreach (string field in fields.Where(f => !names.Names.Contains(f)).Order(StringComparer.Ordinal))
            {
                problems.Add($"{type.Name}.{field} is neither reported nor skipped");
            }

            foreach (string name in names.Names.Where(n => !fields.Contains(n)).Order(StringComparer.Ordinal))
            {
                problems.Add($"{type.Name} reports {name}, which is not one of its fields");
            }

            foreach (string name in names.Repeated.Order(StringComparer.Ordinal))
            {
                problems.Add($"{type.Name} reports {name} more than once");
            }
        }

        return (problems, checkedTypes);
    }

    // The instance fields of the type and the classes above it, a property's hidden field named
    // as the property.
    private static IEnumerable<string> Fields(Type type)
    {
        for (Type? t = type; t is not null && t != typeof(object); t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string name = field.Name;
                yield return name.StartsWith('<') && name.EndsWith(">k__BackingField", StringComparison.Ordinal) ? name[1..name.IndexOf('>', StringComparison.Ordinal)] : name;
            }
        }
    }

    // Collects the names a report gives, the field name being the part before a space or a
    // bracket ("_controllers[0]" is the field _controllers), and the parts it holds.
    private sealed class NameSink : IStateSink
    {
        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Repeated { get; } = new(StringComparer.Ordinal);

        public List<IReportsState> Parts { get; } = [];

        private readonly HashSet<string> _full = new(StringComparer.Ordinal);

        public void Add(string name, long value) => Note(name);

        public void Add(string name, ulong value) => Note(name);

        public void Add(string name, bool value) => Note(name);

        public void Add(string name, double value) => Note(name);

        public void Add(string name, ReadOnlySpan<byte> values) => Note(name);

        public void Add(string name, ReadOnlySpan<int> values) => Note(name);

        public void Add(string name, ReadOnlySpan<uint> values) => Note(name);

        public void Add(string name, ReadOnlySpan<long> values) => Note(name);

        public void Add(string name, ReadOnlySpan<float> values) => Note(name);

        public void Add(string name, ReadOnlySpan<double> values) => Note(name);

        public void Add(string name, IReportsState part)
        {
            Note(name);
            Parts.Add(part);
        }

        public void Skip(string name, string why) => Note(name);

        private void Note(string name)
        {
            if (!_full.Add(name))
            {
                Repeated.Add(name);
            }

            int end = name.IndexOfAny([' ', '[']);
            Names.Add(end < 0 ? name : name[..end]);
        }
    }
}
