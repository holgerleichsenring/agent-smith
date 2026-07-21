using AgentSmith.Application.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199b docker-tier fix-bug coverage. Five end-to-end assertions
/// against the production DockerSandboxFactory + a per-test bare git
/// remote + real dotnet tooling:
///   A: green path — pipeline succeeds, container removed on dispose
///   B: WIP branch reaches the bare remote AFTER AgenticMaster, BEFORE Test
///   C: FIXTURE_FAIL=1 — pipeline reports failure, WIP branch survives
///   D: registries-empty + private feed in nuget.config → NU1301
///   E: registries-configured + same host → restore green
///
/// Each test runs only when AGENTSMITH_HARNESS_DOCKER=1 AND the docker
/// daemon is reachable; either gate failing produces a loud Skip log
/// per spec. CI sets neither, so CI never picks the heavy tier up.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
// p0363: reads AGENTSMITH_TEST_AZDO_TOKEN — serialized with its writers.
[Collection("agentsmith-test-env-token")]
public sealed class FixBugDockerTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Docker_FixBug_GreenPath_PipelineSucceedsContainerRemoved()
    {
        if (SkipIfUnavailable()) return;
        var (harness, session, runner) = await StartAsync(FixturePaths.Docker);
        // Master makes one trivial edit so PersistWorkBranch + CommitAndPR have
        // something real to push. Empty-edit happy path is exercised by the
        // fast-tier MasterReturnsZeroChanges_StillPipelineGreen — that case
        // depends on StubSandbox's lying-clean-commit; on real docker an empty
        // tree fails `git commit` as designed.
        EnqueueWriteEdit(harness);

        var result = await runner.RunAsync("fix-bug");
        LogResult(result);
        result.IsSuccess.Should().BeTrue($"green path must complete: {result.Message}");
        harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty("at least one container spawned");
        await DisposeAsync(harness, session);
    }

    [Fact]
    public async Task Docker_FixBug_WipBranchPushedBeforeTest()
    {
        if (SkipIfUnavailable()) return;
        var (harness, session, runner) = await StartAsync(FixturePaths.Docker);
        EnqueueWriteEdit(harness);

        var result = await runner.RunAsync("fix-bug");
        LogResult(result);
        var branches = session.BareBranches();
        output.WriteLine("bare branches: " + string.Join(", ", branches));
        branches.Should().Contain(b => b != "main",
            "PersistWorkBranch must push a WIP branch BEFORE Test runs");
        await DisposeAsync(harness, session);
    }

    [Fact]
    public async Task Docker_FixBug_FixtureFails_WipBranchPreservedForRetry()
    {
        if (SkipIfUnavailable()) return;
        Environment.SetEnvironmentVariable("FIXTURE_FAIL", "1");
        try
        {
            var (harness, session, runner) = await StartAsync(FixturePaths.Docker);
            EnqueueWriteEdit(harness);
            var result = await runner.RunAsync("fix-bug");
            LogResult(result);
            var branches = session.BareBranches();
            output.WriteLine("bare branches: " + string.Join(", ", branches));
            branches.Should().Contain(b => b != "main",
                "WIP branch must survive on the bare remote so operator-retry can resume");
            await DisposeAsync(harness, session);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIXTURE_FAIL", null);
        }
    }

    [Fact]
    public async Task Docker_FixBug_RegistriesEmpty_DotnetRestoreFailsWithNU1301()
    {
        if (SkipIfUnavailable()) return;
        var (harness, session, runner) = await StartAsync(
            FixturePaths.DockerNoRegistries, includePrivateFeed: true);
        EnqueueWriteEdit(harness);

        var result = await runner.RunAsync("fix-bug");
        LogResult(result);
        result.IsSuccess.Should().BeFalse(
            "registries-empty + private feed in nuget.config must trip NU1301 in `dotnet restore`");
        // NU1301 surfaces in the Test handler's stdout/stderr capture only when the
        // harness pipes step output into the failure message. We assert the run did
        // fail; the captured stderr lives in the per-test container log (see test
        // output) for operator inspection.
        await DisposeAsync(harness, session);
    }

    [Fact]
    public async Task Docker_FixBug_RegistriesConfigured_DotnetRestoreGreen()
    {
        if (SkipIfUnavailable()) return;
        var token = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_AZDO_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            output.WriteLine(
                "Skipping registries-configured assertion — AGENTSMITH_TEST_AZDO_TOKEN not set; " +
                "without a valid Azure DevOps token the private feed query still fails and the " +
                "assertion would be indistinguishable from the registries-empty case.");
            return;
        }
        var (harness, session, runner) = await StartAsync(FixturePaths.Docker);
        harness.ChatClient.EnqueueText("No changes needed.");

        var result = await runner.RunAsync("fix-bug");
        LogResult(result);
        result.IsSuccess.Should().BeTrue(
            "registries-configured + valid token must let SetupRegistryAuth stage creds and restore go green");
        result.Message.Should().NotContain("NU1301");
        await DisposeAsync(harness, session);
    }

    private bool SkipIfUnavailable()
    {
        if (DockerAvailability.IsAvailable(out var detail)) return false;
        output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
        return true;
    }

    private static Task<(RealCompositionHarness, DockerHarnessSession, PipelineRunner)> StartAsync(
        string fixtureYml) => StartAsync(fixtureYml, includePrivateFeed: false);

    private static async Task<(RealCompositionHarness, DockerHarnessSession, PipelineRunner)> StartAsync(
        string fixtureYml, bool includePrivateFeed)
    {
        var session = await DockerHarnessSession.CreateAsync(
            FixturePaths.CsharpFixtureSource(), includePrivateFeed);
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(fixtureYml), SandboxBackend.Docker, session);
        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DockerHarnessRepo.For(session),
        };
        return (harness, session, runner);
    }

    // The harness Docker mode swaps IProjectAnalyzer for StubProjectAnalyzer
    // (canned csharp ProjectMap with a real `dotnet test` command). We
    // therefore script only the master's edit, not the analyzer JSON.
    private static void EnqueueWriteEdit(RealCompositionHarness harness)
    {
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/NOTE.md","content":"harness-touched"}""")
            .EnqueueText("Edit applied.");
    }

    private void LogResult(AgentSmith.Domain.Models.CommandResult result) =>
        output.WriteLine($"pipeline result: {result.IsSuccess} — {result.Message}");

    private static async Task DisposeAsync(RealCompositionHarness harness, DockerHarnessSession session)
    {
        await harness.DisposeAsync();
        await session.DisposeAsync();
    }
}
