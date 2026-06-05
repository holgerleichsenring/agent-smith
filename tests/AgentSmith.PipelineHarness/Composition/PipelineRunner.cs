using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
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

    /// <summary>The RunId seeded into the last built context — lets a test read
    /// the run's cached result.md/plan.md back out of the artifact store.</summary>
    public string? LastRunId { get; private set; }

    /// <summary>
    /// p0199c: lets a docker-tier test point ContextKeys.SourcePath at a
    /// real host-side directory (typically the per-test working copy). The
    /// api-security-scan preset's TryCheckoutSource then takes the CLI-
    /// override branch instead of trying to host-clone the bind-mounted
    /// file:///bare-remotes/... URL (which only exists inside the sandbox).
    /// </summary>
    public string? SourcePathOverride { get; set; }

    /// <summary>
    /// p0199e: lets a docker-tier legal-analysis test point ContextKeys.
    /// SourceFilePath at a real fixture document (LegalFixture/inbox/...).
    /// Default seeds a throwaway txt blob in the temp dir, which is fine
    /// for the fast tier but masks shape mismatches the docker tier wants
    /// to surface (markitdown reads a real file, BootstrapDocument emits
    /// non-stub markdown).
    /// </summary>
    public string? SourceFilePathOverride { get; set; }

    /// <summary>
    /// p0199f: lets the docker-tier api-security-scan passive-mode test
    /// pre-seed ContextKeys.Repository with a host-side scratch directory.
    /// TryCheckoutSource WarnPassive does NOT clear Repository (only
    /// updates source_available), so the seed survives the fail-soft and
    /// AgenticMaster + FilesystemToolHost get a real LocalPath to scope
    /// writes inside. Production passive runs would need the same shape;
    /// closing that systemically is out of p0199f scope (see decisions/
    /// p0199f.yaml).
    /// </summary>
    public string? PassiveRepositoryLocalPath { get; set; }

    /// <summary>
    /// p0199f: lets the docker-tier api-security-scan passive-mode test
    /// point ContextKeys.ApiTarget at a real live HTTP endpoint (the
    /// per-test StubApiTargetHost Kestrel server). Default keeps the fast-
    /// tier stub URL so other presets are unaffected.
    /// </summary>
    public string? ApiTargetOverride { get; set; }

    /// <summary>
    /// p0199f: parallel override for ContextKeys.SwaggerPath. LoadSwagger
    /// resolves URLs via the stubbed ISwaggerProvider in the harness, so
    /// the test still parses the synthetic spec; the override keeps the
    /// context value honest (a real URL, not a stub.test placeholder).
    /// </summary>
    public string? SwaggerPathOverride { get; set; }

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
            // p0199c: leave CodingPrinciplesPath unset so LoadCodingPrinciplesHandler
            // resolves the default `.agentsmith/coding-principles.md` AND keeps the
            // nested per-context fallback (`.agentsmith/contexts/<name>/coding-
            // principles.md`) active. A non-default path disables the fallback,
            // which surfaced as "DomainRules not found" on the docker tier where
            // the fixture only ships the nested variant.
        };
    }

    // Url present so SandboxLanguageResolver runs the REAL remote-discovery
    // path (reads the stub context.yaml via StubSourceProvider) instead of
    // short-circuiting to the synthetic default. Without it the fast tier
    // never exercises discovery / per-context handlers (Install, Test) — the
    // gap that let the p0202 EnsurePrerequisites no-op slip through. Type stays
    // Local so CheckoutSource still trusts the bind mount (keys on Type).
    private static RepoConnection BuildRepo() =>
        new() { Name = "primary", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/primary" };

    private PipelineContext BuildContext(string presetName, ResolvedProject project)
    {
        var pipeline = new PipelineContext();
        var conceptValue = PipelineNameConceptMap.ToConceptValue(presetName);
        // p0199c: CodingPrinciplesPath left null so LoadCodingPrinciplesHandler
        // resolves the default (.agentsmith/coding-principles.md) AND keeps the
        // nested per-context fallback active for fixtures that ship principles
        // only under .agentsmith/contexts/<name>/. A non-default value disables
        // the fallback path — that's how add-feature failed on the docker tier
        // until this seed was relaxed.
        var resolved = new ResolvedPipelineConfig(
            conceptValue, project.Agent,
            PipelinePresets.GetDefaultSkillsPath(presetName),
            CodingPrinciplesPath: null);

        SeedRequired(pipeline, project, resolved, conceptValue);
        SeedPresetSpecific(pipeline, presetName);
        return pipeline;
    }

    private void SeedRequired(
        PipelineContext pipeline, ResolvedProject project,
        ResolvedPipelineConfig resolved, string conceptValue)
    {
        pipeline.Set(ContextKeys.ResolvedPipeline, resolved);
        pipeline.Set(ContextKeys.PipelineName, conceptValue);
        pipeline.Set(ContextKeys.AgentConfig, project.Agent);
        pipeline.Set(ContextKeys.Headless, true);
        pipeline.Set(ContextKeys.TicketId, new TicketId("1"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, project.Repos);
        SeedSourceAndRepository(pipeline);
        pipeline.Set(ContextKeys.SourceUrl, "git://stub");
        LastRunId = "harness-" + Guid.NewGuid().ToString("N")[..8];
        pipeline.Set(ContextKeys.RunId, LastRunId);
        pipeline.Set(ContextKeys.ConceptVocabulary, RunStateConceptsTestFactory.FallbackMinimal);
        // p0205: mirror the binding ExecutePipelineUseCase sets after the catalog
        // resolver runs, so the visible LoadCatalog first step exercises its real
        // path (the resolver/network bootstrap itself stays out of the harness).
        pipeline.Set(ContextKeys.CatalogResolution, new Contracts.Models.CatalogResolution(
            "/catalog", "harness", SkillsSourceMode.Default, "https://stub.test/catalog", FromCache: true));
    }

    // p0199f: passive mode pre-seeds Repository (with a real scratch dir
    // FilesystemToolHost can write into) and leaves SourcePath unset so
    // TryCheckoutSource's WarnPassive path runs and source_available stays
    // false — BootstrapGate's conditional skip on api-scan is the
    // falsifiability anchor. Source mode seeds SourcePath at the per-test
    // working copy and lets TryCheckoutSource publish Repository itself.
    private void SeedSourceAndRepository(PipelineContext pipeline)
    {
        if (PassiveRepositoryLocalPath is not null)
        {
            pipeline.Set(ContextKeys.Repository,
                new Repository(new BranchName("(passive)"), PassiveRepositoryLocalPath));
            return;
        }
        pipeline.Set(ContextKeys.SourcePath, SourcePathOverride ?? "/tmp/source");
    }

    private void SeedPresetSpecific(PipelineContext pipeline, string presetName)
    {
        pipeline.Set(ContextKeys.SourceFilePath, SourceFilePathOverride ?? CreateLegalStubFile());
        pipeline.Set(ContextKeys.SwaggerPath, SwaggerPathOverride ?? "https://stub.test/swagger.json");
        pipeline.Set(ContextKeys.ApiTarget, ApiTargetOverride ?? "https://stub.test");
        HarnessTicketSeed.SeedIfPlanProducing(pipeline, presetName);
    }

    private static string CreateLegalStubFile()
    {
        var legalTempPath = Path.Combine(
            Path.GetTempPath(), $"agentsmith-harness-legal-{Guid.NewGuid():N}.txt");
        File.WriteAllText(legalTempPath, "Stub legal document content.");
        return legalTempPath;
    }
}
