using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c: shared per-preset docker-tier scaffolding. Centralises gate
/// probe, harness construction, runner wiring, log emission, and dispose
/// so each preset's test class stays focused on its own assertion. p0199f
/// extends it with optional StubApiTargetHost lifecycle for passive-mode
/// api-security-scan.
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

    public Task<DockerPresetRun> StartAsync(
        string preset, SkillsBackend skillsBackend,
        Action<IServiceCollection>? overrides = null)
        => StartAsync(preset, skillsBackend, DockerPresetLayout.For(preset), overrides);

    public async Task<DockerPresetRun> StartAsync(
        string preset, SkillsBackend skillsBackend, DockerPresetLayout layout,
        Action<IServiceCollection>? overrides = null)
    {
        var session = await DockerHarnessSession.CreateAsync(layout.FixtureSourceDir);
        var apiTarget = await TryStartApiTargetAsync(layout);
        var harness = RealCompositionHarness.Build(
            FixturePaths.For(layout.ConfigYml),
            SandboxBackend.Docker, session, skillsBackend, overrides);
        DockerPresetScripts.Seed(preset, harness.ChatClient);
        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DockerHarnessRepo.For(session),
            // Source-mode points SourcePath at the per-test working copy so
            // TryCheckoutSource's CLI-override branch takes over and publishes
            // Repository for downstream LoadContext. Passive mode (p0199f)
            // leaves SourcePath unset so TryCheckoutSource fail-softs and
            // BootstrapGate's p0130a-conditional skip on api-scan kicks in;
            // Repository is pre-seeded at the working-copy scratch so
            // AgenticMaster + FilesystemToolHost still have a real LocalPath.
            SourcePathOverride = layout.SourceMode == DockerPresetSourceMode.Source
                ? session.WorkingCopyPath : null,
            PassiveRepositoryLocalPath = layout.SourceMode == DockerPresetSourceMode.Passive
                ? session.WorkingCopyPath : null,
            SourceFilePathOverride = layout.SourceFilePath,
            ApiTargetOverride = apiTarget?.BaseUrl,
            SwaggerPathOverride = apiTarget?.OpenApiUrl,
        };
        return new DockerPresetRun(harness, session, runner, apiTarget);
    }

    private static Task<StubApiTargetHost?> TryStartApiTargetAsync(DockerPresetLayout layout) =>
        layout.SourceMode == DockerPresetSourceMode.Passive
            ? StartApiTargetAsync()
            : Task.FromResult<StubApiTargetHost?>(null);

    private static async Task<StubApiTargetHost?> StartApiTargetAsync() =>
        await StubApiTargetHost.StartAsync(FixturePaths.StubApiTargetOpenApi());

    public void LogResult(CommandResult result) =>
        output.WriteLine($"pipeline result: {result.IsSuccess} — {result.Message}");
}
