using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-11-205fc: a design turn's effort is bounded both ways — never below the resolved cap, never above a
/// per-pipeline override — in the turn and at the next turn's start; the published tier is the highest stated.
/// </summary>
public sealed class DesignEffortCapTests
{
    private static readonly CostCapValues Ten = new() { Usd = 10m, Tokens = 1_000_000 };

    [Fact]
    public async Task StateWorkPicture_EffortXlUnderTenDollarOverride_CapStaysTen()
    {
        var (efforts, pipeline, events) = Seeded(withOverride: true);
        var tool = Tool(new SpecDialogReplySlot(), efforts, pipeline);

        var answer = (await tool.InvokeAsync(Stated("xl")))?.ToString();

        Cap(pipeline).Usd.Should().Be(10m, "the operator's override is the ceiling the effort may not cross");
        answer.Should().Contain("$10");
        events.Should().ContainSingle().Which.Tier.Should().Be("xl", "the tier stated is the tier published");
    }

    [Fact]
    public async Task GroundSpecDialog_StoredXlUnderTenDollarOverride_NextTurnCapStaysTen()
    {
        var (efforts, pipeline, _) = Seeded(withOverride: true);
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, []);
        pipeline.Set(ContextKeys.SpecDialogWorkPicture,
            new WorkPicture(UndertakingKind.Rebuild, "the upload", UndertakingSize.Series, ComplexityTier.Xl));
        var reader = new DialogGroundingReader(
            new ScriptedSourceProviderFactory(new Dictionary<string, ScriptedSourceProvider>()),
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())),
            new RemoteFileRead(NullLogger<RemoteFileRead>.Instance), NullLogger<DialogGroundingReader>.Instance);

        await new GroundSpecDialogHandler(reader, NullLogger<GroundSpecDialogHandler>.Instance, efforts: efforts)
            .ExecuteAsync(new GroundSpecDialogContext(pipeline), CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(10m, "the stored xl re-applied at turn start is bounded the same way");
    }

    [Fact]
    public async Task StateWorkPicture_LowerEffortLater_PublishesTheHigherTier()
    {
        var (efforts, pipeline, events) = Seeded(withOverride: false);
        var tool = Tool(new SpecDialogReplySlot(), efforts, pipeline);

        await tool.InvokeAsync(Stated("xl"));
        await tool.InvokeAsync(Stated("small"));

        pipeline.Get<ComplexityTier>(ContextKeys.ComplexityTier).Should().Be(ComplexityTier.Xl);
        events.Select(e => e.Tier).Should().Equal("xl", "xl");
        Cap(pipeline).Usd.Should().Be(40m);
    }

    [Fact]
    public async Task StateWorkPicture_XlThenSmallInTheNextTurn_KeepsXlAndFortyDollars()
    {
        var (efforts, pipeline, events) = Seeded(withOverride: false);
        await Tool(new SpecDialogReplySlot(), efforts, pipeline).InvokeAsync(Stated("xl"));
        pipeline.Set(ContextKeys.SpecDialogWorkPicture, // what the keeper stored after turn one
            new WorkPicture(UndertakingKind.Rebuild, "the upload", UndertakingSize.Series, ComplexityTier.Xl));

        pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot()); // turn two's fresh slot
        var master = new AgenticMasterContext("design-partner-master", new Repository(new BranchName("main"), "/tmp"),
            string.Empty, new AgentConfig { Type = "claude" }, pipeline);
        var nextTurn = new TicketActToolsFactory(Handlers.MasterHandlerFixture.Verification(),
                Handlers.MasterHandlerFixture.ProposalWrites(), efforts)
            .Tools(master).OfType<AIFunction>().Single(t => t.Name == WorkPictureToolHost.Name);
        await nextTurn.InvokeAsync(Stated("small"));

        pipeline.Get<ComplexityTier>(ContextKeys.ComplexityTier).Should().Be(ComplexityTier.Xl);
        events.Select(e => e.Tier).Should().Equal("xl", "xl");
        Cap(pipeline).Usd.Should().Be(40m);
    }

    [Fact]
    public void WorkPictureAdmission_SeriesWithoutEffort_IsRefusedNamingTheTiers()
    {
        var (picture, refusal) = WorkPictureAdmission.Of("rebuild", "the upload", "series");

        picture.Should().BeNull();
        refusal.Should().Contain("series").And.Contain("trivial").And.Contain("xl");
        WorkPictureAdmission.Of("fix", "repo-a", "one-spec").Picture.Should().NotBeNull("one spec needs no effort");
    }

    [Fact]
    public void WorkPictureSection_NoPicture_RendersTheStartingCap()
    {
        var pipeline = new PipelineContext();
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = 5m, Tokens = 500_000 });

        WorkPictureSection.Render(pipeline).Should()
            .StartWith(WorkPictureSection.NoneStored).And.Contain("This turn's cost cap: $5 / 500,000 tokens.");
    }

    [Fact]
    public async Task ScopeEstimate_CodingRunLargeOverOverride_StillRaises()
    {
        var config = AgentSmithConfig.Empty();
        config.PipelineCostCap.PerPipeline["code"] = Ten;
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, "code");
        pipeline.Set("PipelineCostCap", Ten);
        var recorder = new ScopeEstimateRecorder(config, Mock.Of<IEventPublisher>(), NullLogger<ScopeEstimateRecorder>.Instance);

        await recorder.ApplyTierAsync(pipeline, ComplexityTier.Large, CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(25m, "p0413a: a coding run's estimate only raises");
    }

    [Fact]
    public void CostCapValues_LoweredTo_TakesTheSmallerPerDimension() =>
        new CostCapValues { Usd = 40m, Tokens = 500_000 }.LoweredTo(Ten).Should()
            .BeEquivalentTo(new CostCapValues { Usd = 10m, Tokens = 500_000 });

    private static (DesignEffortCap Efforts, PipelineContext Pipeline, List<RunBudgetResolvedEvent> Events) Seeded(
        bool withOverride)
    {
        var config = AgentSmithConfig.Empty();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set("PipelineCostCap", withOverride ? Ten : config.PipelineCostCap.Default);
        if (withOverride) config.PipelineCostCap.PerPipeline[PipelinePresets.SpecDialogName] = Ten;
        var events = new List<RunBudgetResolvedEvent>();
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => { if (e is RunBudgetResolvedEvent b) events.Add(b); })
            .Returns(Task.CompletedTask);
        var recorder = new ScopeEstimateRecorder(config, publisher.Object, NullLogger<ScopeEstimateRecorder>.Instance);
        return (new DesignEffortCap(config, recorder), pipeline, events);
    }

    private static AIFunction Tool(SpecDialogReplySlot slot, DesignEffortCap efforts, PipelineContext pipeline) =>
        new WorkPictureToolHost(slot, (tier, ct) => efforts.ApplyAsync(pipeline, tier, ct)).GetTools(null, null).Single();

    private static AIFunctionArguments Stated(string effort) => new()
    {
        ["kind"] = "rebuild", ["truth"] = "the upload describes it", ["size"] = "series", ["effort"] = effort,
    };

    private static CostCapValues Cap(PipelineContext pipeline) =>
        pipeline.TryGet<CostCapValues>("PipelineCostCap", out var cap) ? cap! : throw new InvalidOperationException("no cap");
}
