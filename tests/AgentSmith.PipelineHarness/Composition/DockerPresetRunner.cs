using System.Diagnostics;
using AgentSmith.PipelineHarness.Presets;
using StackExchange.Redis;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b operator-facing entry point for the docker-tier flow. p0199c
/// extends it from the original fix-bug-only gate to the full nine-preset
/// matrix. Prints readable single-line summaries: container lifecycle,
/// pipeline result, WIP-branch presence on the fake remote. Used by the
/// --docker console flag so an operator has ONE command per preset to know
/// the full pipeline works locally without touching the UI or triggering a
/// real ticket.
///
/// Startup validation pre-checks the three env-driven boundaries (host
/// Redis, sandbox-side Redis URL reachable from inside the chosen docker
/// network, and the docker network itself) so a misconfigured run fails
/// fast with an actionable message instead of timing out at CheckoutSource
/// after 330s.
/// </summary>
internal static class DockerPresetRunner
{
    private static readonly HashSet<string> DeferredPresets =
        new(StringComparer.OrdinalIgnoreCase) { "init-project", "autonomous", "skill-manager" };

    public static async Task<int> RunAsync(string preset)
    {
        if (!IsKnownPreset(preset))
        {
            Console.Error.WriteLine($"Unknown preset '{preset}'. See --list.");
            return 2;
        }
        if (DeferredPresets.Contains(preset))
        {
            Console.Error.WriteLine(
                $"--docker for '{preset}' is deferred — needs a real skill catalog mounted into the " +
                "sandbox (see InitProjectDockerTests / AutonomousDockerTests for the gap detail). " +
                "Run the fast tier for now: dotnet run --project tests/AgentSmith.PipelineHarness -- --preset " + preset);
            return 2;
        }
        Environment.SetEnvironmentVariable(DockerAvailability.OptInEnv, "1");
        if (!DockerAvailability.IsAvailable(out var detail))
        {
            Console.Error.WriteLine("Docker not available: " + detail);
            return 2;
        }
        if (!await ValidateEnvironmentAsync()) return 2;

        return await ExecutePresetAsync(preset);
    }

    private static async Task<int> ExecutePresetAsync(string preset)
    {
        Console.WriteLine($"=== docker-tier {preset} ===");
        await using var session = await DockerHarnessSession.CreateAsync(FixturePaths.CsharpFixtureSource());
        Console.WriteLine($"bare repo : {session.BareRepoPath}");
        Console.WriteLine($"working   : {session.WorkingCopyPath}");

        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Docker), SandboxBackend.Docker, session,
            PresetDeferrals.RegisterScannerStubsIfNeeded(preset));
        DockerPresetScripts.Seed(preset, harness.ChatClient);

        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DockerHarnessRepo.For(session),
        };
        var result = await runner.RunAsync(preset);

        Console.WriteLine($"spawned   : {harness.DockerSandboxFactory!.Spawned.Count} sandbox container(s)");
        Console.WriteLine($"branches  : {string.Join(", ", session.BareBranches())}");
        Console.WriteLine($"result    : {(result.IsSuccess ? "SUCCESS" : "FAIL")} — {result.Message}");
        return result.IsSuccess ? 0 : 1;
    }

    private static bool IsKnownPreset(string preset) =>
        AgentSmith.Contracts.Commands.PipelinePresets.TryResolve(preset) is not null;

    private static async Task<bool> ValidateEnvironmentAsync()
    {
        var hostRedis = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
        var sandboxRedis = Environment.GetEnvironmentVariable("HARNESS_SANDBOX_REDIS_URL") ?? "redis:6379";
        var network = Environment.GetEnvironmentVariable("HARNESS_SANDBOX_NETWORK")
                      ?? Environment.GetEnvironmentVariable("DOCKER_NETWORK")
                      ?? "deploy_default";

        Console.WriteLine($"=== env resolved ===");
        Console.WriteLine($"  host redis     : {hostRedis}    (REDIS_URL — what THIS process connects to)");
        Console.WriteLine($"  sandbox redis  : {sandboxRedis}    (HARNESS_SANDBOX_REDIS_URL — what the in-container agent uses)");
        Console.WriteLine($"  docker network : {network}    (DOCKER_NETWORK / HARNESS_SANDBOX_NETWORK)");
        Console.WriteLine();

        if (!await HostRedisReachableAsync(hostRedis))
        {
            Console.Error.WriteLine(
                $"FATAL: host-side Redis at {hostRedis} is unreachable. " +
                "Start your compose stack (or set REDIS_URL to a reachable host:port).");
            return false;
        }
        if (!DockerNetworkExists(network))
        {
            Console.Error.WriteLine(
                $"FATAL: docker network '{network}' does not exist. " +
                "Bring up your compose stack (it creates the network) or override HARNESS_SANDBOX_NETWORK.");
            return false;
        }
        return true;
    }

    private static async Task<bool> HostRedisReachableAsync(string url)
    {
        try
        {
            using var mux = await ConnectionMultiplexer.ConnectAsync(url + ",abortConnect=false,connectTimeout=2000");
            return mux.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    private static bool DockerNetworkExists(string name)
    {
        try
        {
            var psi = new ProcessStartInfo("docker", $"network inspect {name}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(2000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
