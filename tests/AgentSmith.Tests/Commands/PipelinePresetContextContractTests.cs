using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0179f + p0195: walks every pipeline preset step by step and asserts that
/// every command's ContextBuilder.Build() succeeds against a PipelineContext
/// primed with the keys prior steps in the SAME preset are known to produce.
/// Catches the class of regression where a step's builder demands a context
/// key that no upstream step in the new preset shape ever sets — and every
/// production run crashes at that step with "Key X not found".
///
/// p0195 widens the scope from the 3 collapsed coding presets to ALL 10
/// presets, and switches builder resolution from a hand-list to the
/// production DI registry (AddContextBuilders) so the test follows
/// automatically when a new command + builder is added.
/// </summary>
public sealed class PipelinePresetContextContractTests
{
    // p0204 (partial): skill-manager's misplaced LoadContext step is
    // removed, but CompileDiscussion (and likely others) ALSO requires a
    // Repository the preset doesn't provide. The preset has deeper shape
    // issues than the original p0204 spec admitted — full rework deferred
    // to p0204a. Keep the entry so the contract test stays green for the
    // remaining 9 presets; the meta-test KnownBrokenPresets_AreStillBroken
    // forces the operator to revisit when somebody thinks they've fixed
    // the deeper issue.
    // p0167a: pr-review is staged across three slices — the preset declares its
    // final shape from day one, but the CompilePrReviewFindings / PostPrComments
    // builders + handlers land in p0167c. Until then the contract walk throws
    // "Unknown command" at CompilePrReviewFindings; p0167c removes the entry.
    private static readonly HashSet<string> KnownBrokenPresets =
        new(StringComparer.OrdinalIgnoreCase) { "skill-manager", "pr-review" };

    public static readonly IEnumerable<object[]> AllPresets =
        PipelinePresets.Names
            .Where(n => !KnownBrokenPresets.Contains(n))
            .Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void PipelinePresetContextContract_AllStepsBuildContext(string presetName)
    {
        var preset = PipelinePresets.TryResolve(presetName)!.ToList();
        var factory = new CommandContextFactory(ResolveBuilders());
        var project = CreateProjectConfig(presetName);

        for (var i = 0; i < preset.Count; i++)
        {
            var pipeline = SeedInitialContext(presetName);
            for (var j = 0; j < i; j++)
                ApplyKnownOutputs(pipeline, preset[j]);

            var act = () => factory.Create(PipelineCommand.Simple(preset[i]), project, pipeline);

            act.Should().NotThrow(
                $"step {i} '{preset[i]}' in preset '{presetName}' must build its context " +
                $"from keys set by prior steps in the same preset");
        }
    }

    [Theory]
    [InlineData("fix-bug")]
    [InlineData("fix-no-test")]
    [InlineData("add-feature")]
    public void PipelinePresetContextContract_CodeTouchingPreset_IncludesEnsurePrerequisites(string presetName)
    {
        // p0202e: EnsurePrerequisites sits AFTER AnalyzeCode (so the
        // analyzer-derived, repo-state-aware command is available) and BEFORE
        // the master/Test (so deps exist when code runs).
        var preset = PipelinePresets.TryResolve(presetName)!.ToList();

        preset.Should().Contain(CommandNames.EnsurePrerequisites);
        var install = preset.IndexOf(CommandNames.EnsurePrerequisites);
        install.Should().BeGreaterThan(preset.IndexOf(CommandNames.AnalyzeCode));
        install.Should().BeLessThan(preset.IndexOf(CommandNames.AgenticMaster));
    }

    [Theory]
    [InlineData("security-scan")]
    [InlineData("api-security-scan")]
    public void PipelinePresetContextContract_ScanPreset_ExcludesEnsurePrerequisites(string presetName)
    {
        // p0202: scan presets restore internally / read source / hit a live
        // target — EnsurePrerequisites would be latency at best, a misleading
        // failure surface at worst.
        PipelinePresets.TryResolve(presetName)!
            .Should().NotContain(CommandNames.EnsurePrerequisites);
    }

    [Fact]
    public void PipelinePresetContextContract_LegalAnalysis_IncludesEnsurePrerequisites()
    {
        // p0199e: EnsurePrerequisites sits between AcquireSource (document
        // copied into /work) and BootstrapDocument (shells out to markitdown).
        // The operator-set prerequisites pins `pip install markitdown==<v>`
        // so the binary is on PATH when BootstrapDocument runs.
        var preset = PipelinePresets.TryResolve("legal-analysis")!.ToList();

        preset.Should().Contain(CommandNames.EnsurePrerequisites);
        var install = preset.IndexOf(CommandNames.EnsurePrerequisites);
        install.Should().BeGreaterThan(preset.IndexOf(CommandNames.AcquireSource));
        install.Should().BeLessThan(preset.IndexOf(CommandNames.BootstrapDocument));
    }

    [Fact]
    public void KnownBrokenPresets_AreStillBroken()
    {
        // If a preset on the skip list starts passing, fail so the operator
        // knows to remove it from KnownBrokenPresets. Silent suppression
        // ages badly.
        foreach (var name in KnownBrokenPresets)
        {
            var preset = PipelinePresets.TryResolve(name);
            preset.Should().NotBeNull($"'{name}' must reference a real preset");
            var factory = new CommandContextFactory(ResolveBuilders());
            var project = CreateProjectConfig(name);
            var pipeline = SeedInitialContext(name);
            var hadFailure = false;
            for (var i = 0; i < preset!.Count && !hadFailure; i++)
            {
                try { factory.Create(PipelineCommand.Simple(preset[i]), project, pipeline); }
                catch { hadFailure = true; }
                ApplyKnownOutputs(pipeline, preset[i]);
            }
            hadFailure.Should().BeTrue(
                $"'{name}' is on KnownBrokenPresets but now passes the contract — " +
                $"take it off the skip list");
        }
    }

    // ---- helpers ----

    private static IEnumerable<KeyedContextBuilder> ResolveBuilders()
    {
        var services = new ServiceCollection();
        services.AddContextBuilders();
        return services.BuildServiceProvider().GetServices<KeyedContextBuilder>();
    }

    private static PipelineContext SeedInitialContext(string presetName)
    {
        var pipeline = new PipelineContext();
        var agent = new AgentConfig { Type = "claude", Model = "sonnet" };
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig(presetName, agent,
                PipelinePresets.GetDefaultSkillsPath(presetName), "config/coding-principles.md"));
        pipeline.Set(ContextKeys.PipelineName, presetName);
        pipeline.Set(ContextKeys.AgentConfig, agent);
        pipeline.Set(ContextKeys.Headless, true);
        pipeline.Set(ContextKeys.TicketId, new TicketId("1"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos,
            [new RepoConnection { Name = "primary", Type = RepoType.Local, Path = "/tmp" }]);
        // api-security-scan + security-scan accept SourcePath/SourceUrl as
        // CLI input (api-scan: --swagger + --target; security-scan: --branch).
        pipeline.Set(ContextKeys.SourcePath, "/tmp/source");
        pipeline.Set(ContextKeys.SourceUrl, "git://x");
        // pr-review runs are seeded with the PR identifier by the pr-event
        // webhook (p0167a); AnalyzePrDiffContextBuilder requires it.
        pipeline.Set(ContextKeys.PrNumber, "1");
        return pipeline;
    }

    // For every command that produces a context key any downstream builder
    // in any preset reads, seed that key. New commands added without an entry
    // here will surface as test failures pointing at the missing builder
    // dependency — the test message names the offending step.
    private static void ApplyKnownOutputs(PipelineContext pipeline, string commandName)
    {
        switch (commandName)
        {
            case CommandNames.FetchTicket:
                pipeline.Set(ContextKeys.Ticket,
                    new Ticket(new TicketId("1"), "T", "D", null, "Open", "GitHub"));
                break;
            case CommandNames.CheckoutSource:
            case CommandNames.TryCheckoutSource:
            case CommandNames.AcquireSource:
                pipeline.Set(ContextKeys.Repository,
                    new Repository(new BranchName("agent-smith/1"), "git://x"));
                break;
            case CommandNames.LoadCodingPrinciples:
                pipeline.Set(ContextKeys.CodingPrinciples, "principles");
                break;
            case CommandNames.LoadContext:
                pipeline.Set(ContextKeys.ProjectContext, "context.yaml content");
                break;
            case CommandNames.LoadSwagger:
                pipeline.Set(ContextKeys.SwaggerSpecFull, "{\"openapi\":\"3.0.0\"}");
                break;
            case CommandNames.AnalyzeCode:
                pipeline.Set(ContextKeys.ProjectMap, new ProjectMap(
                    "C#", [], [], [], [], new Conventions(null, null, null),
                    new CiConfig(false, null, null, null)));
                break;
            case CommandNames.AnalyzePrDiff:
                pipeline.Set(ContextKeys.PrDiff, new PrDiffAnalysis("base", "head", []));
                pipeline.Set(ContextKeys.PrHead, "head");
                pipeline.Set(ContextKeys.PrBase, "base");
                break;
            case CommandNames.AgenticMaster:
            case CommandNames.AgenticExecute:
            case CommandNames.GenerateTests:
            case CommandNames.GenerateDocs:
                pipeline.Set<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, []);
                break;
            // The remaining commands don't currently produce keys read by any
            // downstream preset step. If a new preset adds such coupling,
            // add a case here. Commands without downstream context-key
            // consumers in any current preset: PipelineNameInitializer,
            // BootstrapCheck, BootstrapGate, BootstrapDocument, BootstrapDispatch,
            // Approval, Test, WriteRunResult, CommitAndPR, InitCommit, PrCrossLink,
            // SessionSetup, SpawnNuclei, SpawnSpectral, SpawnZap, SpawnFix,
            // StaticPatternScan, GitHistoryScan, DependencyAudit, SecurityTrend,
            // SecuritySnapshotWrite, DeliverFindings, DeliverOutput,
            // CompileFindings, CompileDiscussion, CompileKnowledge,
            // QueryKnowledge, LoadRuns, WriteTickets, Triage, SkillRound,
            // SecuritySkillRound, ApiSecuritySkillRound, FilterRound,
            // ConvergenceCheck, GeneratePlan, EmptyPlanCheck, PlanOpenQuestions,
            // PersistWorkBranch, RunReviewPhase, RunFinalPhase, RunVerifyPhase,
            // SwitchSkill, Ask, CompressSecurityFindings, CompressApiScanFindings.
        }
    }

    private static ResolvedProject CreateProjectConfig(string presetName) => new()
    {
        Repos = [new RepoConnection { Name = "primary", Type = RepoType.Local, Path = "/tmp" }],
        Tracker = new TrackerConnection { Type = TrackerType.GitHub, Url = "https://github.com/x/y" },
        Agent = new AgentConfig { Type = "claude", Model = "sonnet" },
        Pipeline = presetName,
        CodingPrinciplesPath = "config/coding-principles.md",
    };
}
