// p0199 console runner. Invoke without xUnit:
//   dotnet run --project tests/AgentSmith.PipelineHarness -- --preset fix-bug
//
// Prints the available presets, exits 0 — preset-driven execution lands
// once per-preset runners are written. Today the harness's coverage is
// exercised via `dotnet test --project tests/AgentSmith.PipelineHarness`;
// the console runner is the scaffold the operator asked for.

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("agent-smith pipeline harness (p0199)");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project tests/AgentSmith.PipelineHarness -- --list");
    Console.WriteLine("  dotnet run --project tests/AgentSmith.PipelineHarness -- --preset <name>");
    Console.WriteLine();
    Console.WriteLine("Preset runners are stubs today — coverage lives in the xUnit suite.");
    return 0;
}

if (args[0] == "--list")
{
    var presets = AgentSmith.Contracts.Commands.PipelinePresets.Names;
    foreach (var name in presets) Console.WriteLine(name);
    return 0;
}

if (args[0] == "--preset")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("--preset requires a preset name. See --list.");
        return 2;
    }
    var preset = args[1];
    Console.WriteLine($"Preset '{preset}' standalone-runner is not implemented yet.");
    Console.WriteLine("Use `dotnet test --filter Category=PipelineHarness` for now.");
    return 0;
}

Console.Error.WriteLine($"Unknown argument: {args[0]}. See --help.");
return 2;
