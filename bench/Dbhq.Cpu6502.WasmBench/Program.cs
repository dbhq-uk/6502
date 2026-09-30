using System.Runtime.InteropServices.JavaScript;
using Dbhq.Cpu6502.Bench;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page calls Bench.Run once the runtime is up.
Console.WriteLine("Dbhq.Cpu6502.WasmBench ready");

public static partial class Bench
{
    /// <summary>Runs the shared workload and returns the one-line result.</summary>
    [JSExport]
    public static string Run(int cycles) => Workload.Run(cycles).ToString();
}
