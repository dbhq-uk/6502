namespace Dbhq.Machines.Nes;

/// <summary>
/// A cartridge file the machine cannot run: it is not a NES file, it is cut short, it asks for a
/// board that is not modelled, or it is for a console that is not. The message is one plain
/// sentence that says why, written for the page to show a visitor as it stands.
/// </summary>
public sealed class NesFormatException : Exception
{
    /// <summary>An exception with <paramref name="message"/>, one plain sentence naming the cause.</summary>
    public NesFormatException(string message)
        : base(message)
    {
    }
}
