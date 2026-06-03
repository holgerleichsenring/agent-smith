using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199d docker-tier init-project coverage. Re-uses the FixBugDocker pattern
/// (per-test bare git remote + working copy + DockerSandboxFactory) but opts
/// into SkillsBackend.Fixture so BootstrapDispatch matches csharp-bootstrap
/// from the checked-in fixture catalog. StubProjectAnalyzer keeps the
/// scripted LLM queue from being drained by the analyzer's LLM pass — the
/// scripted write_file response is what BootstrapRound consumes for its
/// "0 changes" guard.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class InitProjectDockerTests(ITestOutputHelper output)
{
    private readonly DockerPresetHarness _harness = new(output);

    [Fact]
    public async Task Docker_InitProject_BootstrapDispatchPopulatesAvailableRoles_PipelineGreen()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync(
            "init-project", SkillsBackend.Fixture, HarnessProjectAnalyzerStub.Register);

        var result = await run.Runner.RunAsync("init-project");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"init-project must complete end-to-end in docker with the fixture skill catalog: {result.Message}");
        run.Harness.DockerSandboxFactory!.Spawned.Should().NotBeEmpty(
            "at least one sandbox container must have spawned");
    }

    [Fact]
    public async Task Docker_InitProject_BootstrapRoundWritesContextYaml_OnBareRemote()
    {
        if (_harness.SkipIfUnavailable()) return;
        await using var run = await _harness.StartAsync(
            "init-project", SkillsBackend.Fixture, HarnessProjectAnalyzerStub.Register);

        var result = await run.Runner.RunAsync("init-project");
        _harness.LogResult(result);

        result.IsSuccess.Should().BeTrue(
            $"init-project must complete end-to-end in docker: {result.Message}");
        var branches = run.Session.BareBranches();
        output.WriteLine("bare branches: " + string.Join(", ", branches));
        branches.Should().Contain(b => b != "main",
            "InitCommit must push the bootstrap-touched branch to the bare remote");
    }
}
