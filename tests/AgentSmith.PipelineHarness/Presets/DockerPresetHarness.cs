using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c: shared per-preset docker-tier scaffolding. Centralises gate
/// probe, harness construction, runner wiring, log emission, and dispose
/// so each preset's test class stays focused on its own assertion. Same
/// pattern as FixBugDockerTests' private helpers, extracted so the eight
/// new preset classes don't each re-implement it.
/// </summary>
internal sealed class DockerPresetHarness(ITestOutputHelper output)
{
    public bool SkipIfUnavailable()
    {
        if (DockerAvailability.IsAvailable(out var detail)) return false;
        output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
        return true;
    }

    public Task<DockerPresetRun> StartAsync(
        string preset, Action<IServiceCollection>? overrides = null)
        => StartAsync(preset, SkillsBackend.Stub, overrides);

    public async Task<DockerPresetRun> StartAsync(
        string preset, SkillsBackend skillsBackend,
        Action<IServiceCollection>? overrides = null)
    {
        var session = await DockerHarnessSession.CreateAsync(FixturePaths.CsharpFixtureSource());
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Docker),
            SandboxBackend.Docker, session, skillsBackend, overrides);
        DockerPresetScripts.Seed(preset, harness.ChatClient);
        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DockerHarnessRepo.For(session),
            // p0199c: api-security-scan's TryCheckoutSource clones on the
            // HOST (not in the sandbox); the bind-mounted bare-repo URL is
            // only reachable from inside the container. Point SourcePath at
            // the per-test working copy so the CLI-override branch takes
            // over and publishes Repository for downstream LoadContext.
            SourcePathOverride = NeedsHostSourcePath(preset) ? session.WorkingCopyPath : null,
        };
        return new DockerPresetRun(harness, session, runner);
    }

    private static bool NeedsHostSourcePath(string preset) =>
        string.Equals(preset, "api-security-scan", StringComparison.OrdinalIgnoreCase);

    public void LogResult(CommandResult result) =>
        output.WriteLine($"pipeline result: {result.IsSuccess} — {result.Message}");
}
