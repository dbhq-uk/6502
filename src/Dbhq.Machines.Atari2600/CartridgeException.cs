namespace Dbhq.Machines.Atari2600;

/// <summary>
/// A file the machine cannot run as a cartridge: its size is not one of the boards modelled. The
/// message is one plain sentence naming the size and the sizes that are modelled, written for the
/// page to show a visitor as it stands.
/// </summary>
public sealed class CartridgeException(string message) : Exception(message);
