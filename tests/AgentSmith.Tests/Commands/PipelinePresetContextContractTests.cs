using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0179f: walks each collapsed coding preset (fix-bug, add-feature, fix-no-test)
/// step by step and asserts that every command's ContextBuilder.Build() succeeds
/// against a PipelineContext primed with ONLY the keys that prior steps in the
/// SAME preset are known to produce. Catches the class of regression where
/// p0179b retired GeneratePlan but left Approval — Approval's builder demanded a
/// Plan key that no upstream step in the new preset shape ever set, and every
/// production run crashed at step 9.
///
/// Modeling scope is intentionally the 3 collapsed coding presets only.
/// Extending to scan / mad / skill-manager / api-scan etc. requires modeling
/// outputs of Triage / SkillRound / Convergence / CompileDiscussion families
/// and is deferred until a similar regression surfaces on those presets.
/// </summary>
public sealed class PipelinePresetContextContractTests
{
    public static readonly IEnumerable<object[]> CollapsedCodingPresets =
    [
        ["fix-bug"],
        ["add-feature"],
        ["fix-no-test"],
    ];

    [Theory]
    [MemberData(nameof(CollapsedCodingPresets))]
    public void PipelinePresetContextContract_AllStepsBuildContext(string presetName)
    {
        var preset = PipelinePresets.TryResolve(presetName)!.ToList();
        var factory = new CommandContextFactory(AllBuilders());
        var project = CreateProjectConfig(presetName);

        for (var i = 0; i < preset.Count; i++)
        {
            var pipeline = SeedInitialContext(presetName);
            for (var j = 0; j < i; j++)
                ApplyKnownOutputs(pipeline, preset[j]);

            var act = () => factory.Create(PipelineCommand.Simple(preset[i]), project, pipeline);

            act.Should().NotThrow(
                $"step {i} '{preset[i]}' in preset '{presetName}' must build its context from keys set by prior steps in the same preset");
        }
    }

    // ---- helpers ----

    private static PipelineContext SeedInitialContext(string presetName)
    {
        var pipeline = new PipelineContext();
        var agent = new AgentConfig { Type = "claude", Model = "sonnet" };
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig(presetName, agent, "skills/coding", "config/coding-principles.md"));
        pipeline.Set(ContextKeys.PipelineName, presetName);
        pipeline.Set(ContextKeys.AgentConfig, agent);
        pipeline.Set(ContextKeys.Headless, true);
        pipeline.Set(ContextKeys.TicketId, new TicketId("1"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos,
            [new RepoConnection { Name = "primary", Type = RepoType.Local, Path = "/tmp" }]);
        return pipeline;
    }

    private static void ApplyKnownOutputs(PipelineContext pipeline, string commandName)
    {
        switch (commandName)
        {
            case CommandNames.FetchTicket:
                pipeline.Set(ContextKeys.Ticket,
                    new Ticket(new TicketId("1"), "T", "D", null, "Open", "GitHub"));
                break;
            case CommandNames.CheckoutSource:
                pipeline.Set(ContextKeys.Repository,
                    new Repository(new BranchName("agent-smith/1"), "git://x"));
                break;
            case CommandNames.LoadCodingPrinciples:
                pipeline.Set(ContextKeys.CodingPrinciples, "principles");
                break;
            case CommandNames.LoadContext:
                pipeline.Set(ContextKeys.ProjectContext, "context.yaml content");
                break;
            case CommandNames.AnalyzeCode:
                pipeline.Set(ContextKeys.ProjectMap, new ProjectMap(
                    "C#", [], [], [], [], new Conventions(null, null, null),
                    new CiConfig(false, null, null, null)));
                break;
            case CommandNames.AgenticMaster:
            case CommandNames.AgenticExecute:
            case CommandNames.GenerateTests:
            case CommandNames.GenerateDocs:
                pipeline.Set<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, []);
                break;
            // Commands without builder-relevant outputs for downstream coding-preset
            // steps: PipelineNameInitializer, BootstrapCheck, BootstrapGate,
            // Approval, Test, WriteRunResult, CommitAndPR, PrCrossLink.
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

    private static KeyedContextBuilder[] AllBuilders() =>
    [
        new(CommandNames.PipelineNameInitializer, new PipelineNameInitializerContextBuilder()),
        new(CommandNames.FetchTicket, new FetchTicketContextBuilder()),
        new(CommandNames.CheckoutSource, new CheckoutSourceContextBuilder()),
        new(CommandNames.BootstrapCheck, new BootstrapCheckContextBuilder()),
        new(CommandNames.BootstrapGate, new BootstrapGateContextBuilder()),
        new(CommandNames.LoadCodingPrinciples, new LoadCodingPrinciplesContextBuilder()),
        new(CommandNames.LoadContext, new LoadContextContextBuilder()),
        new(CommandNames.AnalyzeCode, new AnalyzeCodeContextBuilder()),
        new(CommandNames.Approval, new ApprovalContextBuilder()),
        new(CommandNames.AgenticMaster, new AgenticMasterContextBuilder()),
        new(CommandNames.AgenticExecute, new AgenticExecuteContextBuilder()),
        new(CommandNames.Test, new TestContextBuilder()),
        new(CommandNames.GenerateTests, new GenerateTestsContextBuilder()),
        new(CommandNames.GenerateDocs, new GenerateDocsContextBuilder()),
        new(CommandNames.WriteRunResult, new WriteRunResultContextBuilder()),
        new(CommandNames.CommitAndPR, new CommitAndPRContextBuilder()),
        new(CommandNames.PrCrossLink, new PrCrossLinkContextBuilder()),
    ];
}
