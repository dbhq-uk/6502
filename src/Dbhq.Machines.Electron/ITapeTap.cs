namespace Dbhq.Machines.Electron;

/// <summary>
/// A listener on the ULA's cassette registers: told of every write to $FE04 to $FE07, and asked
/// for the byte a read of $FE04 gives. It is the seam the test-only tape probe used to find what
/// the OS does on tape (<c>tape.md</c> s7); the cassette itself replaces it. With no tap set the
/// ULA behaves as if the seam were not there.
/// </summary>
internal interface ITapeTap
{
    /// <summary>A write to register 4, 5, 6 or 7, at <paramref name="cycle"/>, after the ULA has taken it.</summary>
    void OnWrite(int register, byte value, long cycle);

    /// <summary>A read of $FE04 by the CPU at <paramref name="cycle"/>: the byte it gives.</summary>
    byte OnReadData(long cycle);
}
