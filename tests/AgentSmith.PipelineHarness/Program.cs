// p0199 console runner. Invoke without xUnit:
//   dotnet run --project tests/AgentSmith.PipelineHarness -- --preset fix-bug
//
// Builds the same RealCompositionHarness the xUnit tests use, scripts a
// minimal LLM response per preset (matching the fast-tier coverage that
// passes in CI), then runs the preset end-to-end through IPipelineExecutor
// and prints the result. Deferred presets (init-project, autonomous) print
// the same Skip rationale the xUnit suite emits.

using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

if (args[0] == "--list")
{
    foreach (var name in AgentSmith.Contracts.Commands.PipelinePresets.Names)
        Console.WriteLine(name);
    return 0;
}

if (args[0] == "--preset")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("--preset requires a preset name. See --list.");
        return 2;
    }
    return await RunPresetAsync(args[1]);
}

Console.Error.WriteLine($"Unknown argument: {args[0]}. See --help.");
return 2;

static async Task<int> RunPresetAsync(string preset)
{
    if (PresetDeferrals.IsDeferred(preset, out var reason))
    {
        Console.WriteLine($"Preset '{preset}' is deferred: {reason}");
        return 0;
    }

    Console.WriteLine($"Running preset '{preset}' via RealCompositionHarness...");
    var configPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "agentsmith.yml");
    await using var harness = RealCompositionHarness.Build(
        configPath, PresetDeferrals.RegisterScannerStubsIfNeeded(preset));
    PresetDeferrals.SeedDefaultScript(preset, harness.ChatClient);

    var runner = new PipelineRunner(harness.Services);
    var result = await runner.RunAsync(preset);
    Console.WriteLine($"  result: {(result.IsSuccess ? "SUCCESS" : "FAIL")} — {result.Message}");
    return result.IsSuccess ? 0 : 1;
}

static void PrintUsage()
{
    Console.WriteLine("agent-smith pipeline harness (p0199)");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project tests/AgentSmith.PipelineHarness -- --list");
    Console.WriteLine("  dotnet run --project tests/AgentSmith.PipelineHarness -- --preset <name>");
}
