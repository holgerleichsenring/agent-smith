using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199: runs one pipeline preset end-to-end through <see cref="IPipelineExecutor"/>,
/// resolved from the real composition. Mirrors the context seeding
/// <c>ExecutePipelineUseCase</c> performs without going through the catalog
/// resolver / network bootstrap — handler chain is the same, the outer use
/// case wiring is not what this harness asserts on.
/// </summary>
public sealed class PipelineRunner(IServiceProvider services)
{
    public RepoConnection? RepoOverride { get; set; }

    public Task<CommandResult> RunAsync(string presetName, CancellationToken ct = default)
    {
        var executor = services.GetRequiredService<IPipelineExecutor>();
        var preset = PipelinePresets.TryResolve(presetName)
            ?? throw new InvalidOperationException($"Unknown preset '{presetName}'");

        var project = BuildProject(presetName);
        var context = BuildContext(presetName, project);
        return executor.ExecuteAsync(preset, project, context, ct);
    }

    private ResolvedProject BuildProject(string presetName)
    {
        var agent = new AgentConfig { Type = "claude", Model = "sonnet" };
        return new ResolvedProject
        {
            Repos = [RepoOverride ?? BuildRepo()],
            Tracker = new TrackerConnection { Type = TrackerType.GitHub, Url = "https://stub.test" },
            Agent = agent,
            Pipeline = presetName,
            CodingPrinciplesPath = "config/coding-principles.md",
        };
    }

    private static RepoConnection BuildRepo() =>
        new() { Name = "primary", Type = RepoType.Local, Path = "/tmp" };

    private PipelineContext BuildContext(string presetName, ResolvedProject project)
    {
        var pipeline = new PipelineContext();
        var conceptValue = PipelineNameConceptMap.ToConceptValue(presetName);
        var resolved = new ResolvedPipelineConfig(
            conceptValue, project.Agent,
            PipelinePresets.GetDefaultSkillsPath(presetName),
            "config/coding-principles.md");

        SeedRequired(pipeline, project, resolved, conceptValue);
        SeedPresetSpecific(pipeline, presetName);
        return pipeline;
    }

    private static void SeedRequired(
        PipelineContext pipeline, ResolvedProject project,
        ResolvedPipelineConfig resolved, string conceptValue)
    {
        pipeline.Set(ContextKeys.ResolvedPipeline, resolved);
        pipeline.Set(ContextKeys.PipelineName, conceptValue);
        pipeline.Set(ContextKeys.AgentConfig, project.Agent);
        pipeline.Set(ContextKeys.Headless, true);
        pipeline.Set(ContextKeys.TicketId, new TicketId("1"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, project.Repos);
        pipeline.Set(ContextKeys.SourcePath, "/tmp/source");
        pipeline.Set(ContextKeys.SourceUrl, "git://stub");
        pipeline.Set(ContextKeys.RunId, "harness-" + Guid.NewGuid().ToString("N")[..8]);
        pipeline.Set(ContextKeys.ConceptVocabulary, RunStateConceptsTestFactory.FallbackMinimal);
    }

    private static void SeedPresetSpecific(PipelineContext pipeline, string presetName)
    {
        var legalTempPath = Path.Combine(
            Path.GetTempPath(), $"agentsmith-harness-legal-{Guid.NewGuid():N}.txt");
        File.WriteAllText(legalTempPath, "Stub legal document content.");
        pipeline.Set(ContextKeys.SourceFilePath, legalTempPath);
        pipeline.Set(ContextKeys.SwaggerPath, "https://stub.test/swagger.json");
        pipeline.Set(ContextKeys.ApiTarget, "https://stub.test");
    }
}
