using Dbhq.Cpu6502.ChipTrace;

// Writes the chip page's trace. Run from the repository root:
//   dotnet run --project tools/Dbhq.Cpu6502.ChipTrace
// or pass a path to write somewhere else.
string path = args.Length > 0 ? args[0] : ChipTrace.RelativePath;
File.WriteAllText(path, ChipTrace.Build());
Console.WriteLine($"wrote {path}");
