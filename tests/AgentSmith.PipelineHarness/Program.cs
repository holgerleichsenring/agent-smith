// p0199 console runner. Invoke without xUnit:
//   dotnet run --project tests/AgentSmith.PipelineHarness -- --preset fix-bug
//   dotnet run --project tests/AgentSmith.PipelineHarness -- --preset fix-bug --docker
//
// Without --docker: scripts a minimal LLM response per preset and runs
// through the stub-sandbox fast tier (same flow the xUnit fast-tier tests
// use). With --docker: spins up a per-test bare git remote + working copy,
// wires the production DockerSandboxFactory + real IConnectionMultiplexer,
// runs the named preset end-to-end against a clean container, then prints
// step results, container lifecycle, WIP-branch presence on the fake
// remote, and the final pipeline result. p0199c extends --docker to the
// full nine-preset matrix (init-project + autonomous land deferred until
// a real skill catalog is mounted into the sandbox).

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
    var docker = args.Length > 2 && args[2] == "--docker";
    return docker ? await DockerPresetRunner.RunAsync(args[1]) : await RunPresetAsync(args[1]);
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

    Console.WriteLine($"Running preset '{preset}' via RealCompositionHarness (stub sandbox)...");
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
    Console.WriteLine("  dotnet run --project tests/AgentSmith.PipelineHarness -- --preset <name> --docker");
    Console.WriteLine();
    Console.WriteLine("  --docker:  run the docker-tier flow end-to-end (real DockerSandbox + dotnet + git).");
    Console.WriteLine("             Supports all nine presets except init-project + autonomous (deferred,");
    Console.WriteLine("             see InitProjectDockerTests / AutonomousDockerTests for the gap detail).");
    Console.WriteLine("             Requires docker daemon + sandbox-agent image; uses REDIS_URL (default");
    Console.WriteLine("             localhost:6379). Sets AGENTSMITH_HARNESS_DOCKER=1 implicitly when invoked.");
}
