namespace Dbhq.Machines.Kim1;

/// <summary>
/// Where the TTY/KB switch is: the switch, or a jumper, between pins A-21 and
/// A-V of the application connector (KIM-1 User Manual, section 2.6).
/// </summary>
public enum Kim1Mode
{
    /// <summary>Open: the monitor uses the keypad and the six digits.</summary>
    Keypad,

    /// <summary>Closed: the monitor uses the teletype, once RS and then RUB OUT have been pressed.</summary>
    Teletype,
}
