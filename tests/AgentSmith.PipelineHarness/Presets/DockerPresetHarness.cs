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

    public async Task<DockerPresetRun> StartAsync(
        string preset, Action<IServiceCollection>? overrides = null)
    {
        var session = await DockerHarnessSession.CreateAsync(FixturePaths.CsharpFixtureSource());
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Docker),
            SandboxBackend.Docker, session, overrides);
        DockerPresetScripts.Seed(preset, harness.ChatClient);
        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DockerHarnessRepo.For(session),
        };
        return new DockerPresetRun(harness, session, runner);
    }

    public void LogResult(CommandResult result) =>
        output.WriteLine($"pipeline result: {result.IsSuccess} — {result.Message}");
}
