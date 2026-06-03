using System.Diagnostics;
using AgentSmith.PipelineHarness.Presets;
using StackExchange.Redis;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b operator-facing entry point for the docker-tier flow. p0199c
/// extended it from the original fix-bug-only gate to the full nine-preset
/// matrix; p0199f closes out api-security-scan via the passive-mode default
/// (no source checkout, Kestrel mini-server target). Prints readable
/// single-line summaries: container lifecycle, pipeline result, WIP-branch
/// presence on the fake remote.
///
/// Startup validation pre-checks the three env-driven boundaries (host
/// Redis, sandbox-side Redis URL reachable from inside the chosen docker
/// network, and the docker network itself) so a misconfigured run fails
/// fast with an actionable message instead of timing out at CheckoutSource
/// after 330s.
/// </summary>
internal static class DockerPresetRunner
{
    private static readonly Dictionary<string, string> DeferredPresets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["skill-manager"] =
                "fast-tier-only by design (p0204): preset spawns no sandbox; docker-tier would be ceremony without coverage gain. Run `dotnet test tests/AgentSmith.PipelineHarness --filter SkillManagerTests` for fast-tier validation.",
        };

    public static async Task<int> RunAsync(string preset)
    {
        if (!IsKnownPreset(preset))
        {
            Console.Error.WriteLine($"Unknown preset '{preset}'. See --list.");
            return 2;
        }
        if (DeferredPresets.TryGetValue(preset, out var deferReason))
        {
            Console.Error.WriteLine(
                $"--docker for '{preset}' is deferred — {deferReason} " +
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
        var layout = DockerPresetLayout.For(preset);
        await using var session = await DockerHarnessSession.CreateAsync(layout.FixtureSourceDir);
        await using var apiTarget = await TryStartApiTargetAsync(layout);
        Console.WriteLine($"bare repo : {session.BareRepoPath}");
        Console.WriteLine($"working   : {session.WorkingCopyPath}");
        if (apiTarget is not null)
            Console.WriteLine($"api target: {apiTarget.BaseUrl}");

        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(layout.ConfigYml), SandboxBackend.Docker, session,
            ResolveSkillsBackend(preset),
            PresetDeferrals.ComposeOverrides(preset));
        DockerPresetScripts.Seed(preset, harness.ChatClient);

        var runner = BuildRunner(harness, session, layout, apiTarget);
        var result = await runner.RunAsync(preset);

        Console.WriteLine($"spawned   : {harness.DockerSandboxFactory!.Spawned.Count} sandbox container(s)");
        Console.WriteLine($"branches  : {string.Join(", ", session.BareBranches())}");
        Console.WriteLine($"result    : {(result.IsSuccess ? "SUCCESS" : "FAIL")} — {result.Message}");
        return result.IsSuccess ? 0 : 1;
    }

    // p0199f: passive-mode api-security-scan needs a real HTTP target +
    // openapi URL even when scanners are stubbed (keeps ApiTarget honest).
    // Other layouts skip the Kestrel boot — the host is only relevant when
    // SpawnNuclei/Spectral/ZAP run for real (env-gated, p0199f).
    private static Task<StubApiTargetHost?> TryStartApiTargetAsync(DockerPresetLayout layout) =>
        layout.SourceMode == DockerPresetSourceMode.Passive
            ? StartApiTargetAsync()
            : Task.FromResult<StubApiTargetHost?>(null);

    private static async Task<StubApiTargetHost?> StartApiTargetAsync() =>
        await StubApiTargetHost.StartAsync(FixturePaths.StubApiTargetOpenApi());

    private static PipelineRunner BuildRunner(
        RealCompositionHarness harness, DockerHarnessSession session,
        DockerPresetLayout layout, StubApiTargetHost? apiTarget) => new(harness.Services)
        {
            RepoOverride = DockerHarnessRepo.For(session),
            // Source-mode presets point SourcePath at the per-test working
            // copy so TryCheckoutSource's CLI-override branch takes over and
            // publishes Repository. Passive mode (p0199f) leaves SourcePath
            // unset so TryCheckoutSource fail-softs and BootstrapGate's
            // p0130a-conditional skip on api-scan kicks in; Repository is
            // pre-seeded at the working-copy scratch so AgenticMaster +
            // FilesystemToolHost still get a real LocalPath.
            SourcePathOverride = layout.SourceMode == DockerPresetSourceMode.Source
                ? session.WorkingCopyPath : null,
            PassiveRepositoryLocalPath = layout.SourceMode == DockerPresetSourceMode.Passive
                ? session.WorkingCopyPath : null,
            SourceFilePathOverride = layout.SourceFilePath,
            ApiTargetOverride = apiTarget?.BaseUrl,
            SwaggerPathOverride = apiTarget?.OpenApiUrl,
        };

    // p0199d: init-project + autonomous need the checked-in fixture catalog
    // so BootstrapDispatch / Triage see populated AvailableRoles. All other
    // presets stay on the empty-catalog stub (handler-shape only).
    private static SkillsBackend ResolveSkillsBackend(string preset) =>
        string.Equals(preset, "init-project", StringComparison.OrdinalIgnoreCase)
        || string.Equals(preset, "autonomous", StringComparison.OrdinalIgnoreCase)
            ? SkillsBackend.Fixture
            : SkillsBackend.Stub;

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
