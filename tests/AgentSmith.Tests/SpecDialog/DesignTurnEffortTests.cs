using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
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
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-10-548f: a design turn states its effort in its work picture; the tier raises the turn's cost cap — the
/// budget its review may spend — and the conversation keeps the higher tier it was ever given.
/// </summary>
public sealed class DesignTurnEffortTests
{
    [Fact]
    public async Task StateWorkPicture_EffortXl_RaisesTheTurnCapToTheXlTier()
    {
        var (pipeline, answer) = await StateAsync(new PipelineCostCapConfig(), "xl");

        Cap(pipeline).Usd.Should().Be(40m);
        Cap(pipeline).Tokens.Should().Be(8_000_000);
        answer.Should().Contain("$40");
    }

    [Fact]
    public async Task StateWorkPicture_EffortBelowAPerPipelineOverride_KeepsTheOverride()
    {
        var (pipeline, _) = await StateAsync(new PipelineCostCapConfig(), "small",
            new CostCapValues { Usd = 60m, Tokens = 9_000_000 });

        Cap(pipeline).Usd.Should().Be(60m, "a tier only ever raises the resolved cap");
    }

    // 2026-11-10-548f review (L8): a later, smaller correction in the same turn keeps the turn's own higher effort.
    [Fact]
    public async Task StateWorkPicture_LowerEffortLaterInTheTurn_KeepsTheTurnsHigherTier()
    {
        var slot = new SpecDialogReplySlot();
        var tool = new WorkPictureToolHost(slot).GetTools(null, null).Single();
        AIFunctionArguments Stated(string effort) => new()
        {
            ["kind"] = "rebuild", ["truth"] = "the upload describes it", ["size"] = "series", ["effort"] = effort,
        };

        await tool.InvokeAsync(Stated("xl"));
        await tool.InvokeAsync(Stated("medium"));

        slot.WorkPicture!.Effort.Should().Be(ComplexityTier.Xl);
    }

    [Fact]
    public async Task StateWorkPicture_NoEffort_LeavesTheCap()
    {
        var (pipeline, answer) = await StateAsync(new PipelineCostCapConfig(), null);

        pipeline.TryGet<CostCapValues>("PipelineCostCap", out _).Should().BeFalse();
        answer.Should().NotContain("cost cap");
    }

    [Fact]
    public void WorkPictureAdmission_EffortUnknown_IsRefusedNamingTheTiers()
    {
        var (picture, refusal) = WorkPictureAdmission.Of("rebuild", "the upload", "series", "unknown");

        picture.Should().BeNull();
        refusal.Should().Contain("trivial").And.Contain("xl").And.NotContain("unknown,");
    }

    [Fact]
    public void WorkPictureSection_StoredEffort_RendersEffortAndCap()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogWorkPicture,
            new WorkPicture(UndertakingKind.Rebuild, "the upload", UndertakingSize.Series, ComplexityTier.Xl));
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = 40m, Tokens = 8_000_000 });

        WorkPictureSection.Render(pipeline).Should().Contain("- effort: xl").And.Contain("$40");
    }

    [Fact]
    public void WorkPictureView_StoredEffort_IsShownLowerCase() =>
        WorkPictureView.Of(new WorkPicture(UndertakingKind.Rebuild, "t", UndertakingSize.Series, ComplexityTier.Large))!
            .Effort.Should().Be("large");

    [Fact]
    public async Task GroundSpecDialog_SeededEffort_AppliesTheTierBeforeTheMaster()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, []);
        pipeline.Set(ContextKeys.SpecDialogWorkPicture,
            new WorkPicture(UndertakingKind.Rebuild, "the upload", UndertakingSize.Series, ComplexityTier.Xl));
        var reader = new DialogGroundingReader(
            new ScriptedSourceProviderFactory(new Dictionary<string, ScriptedSourceProvider>()),
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())),
            new RemoteFileRead(NullLogger<RemoteFileRead>.Instance), NullLogger<DialogGroundingReader>.Instance);
        var recorder = new ScopeEstimateRecorder(AgentSmithConfig.Empty(), Mock.Of<IEventPublisher>(), NullLogger<ScopeEstimateRecorder>.Instance);

        await new GroundSpecDialogHandler(reader, NullLogger<GroundSpecDialogHandler>.Instance, tiers: recorder)
            .ExecuteAsync(new GroundSpecDialogContext(pipeline), CancellationToken.None);

        Cap(pipeline).Usd.Should().Be(40m, "the conversation's stored effort sizes the turn before its first call");
        pipeline.Get<ComplexityTier>(ContextKeys.ComplexityTier).Should().Be(ComplexityTier.Xl);
    }

    private static CostCapValues Cap(PipelineContext pipeline) =>
        pipeline.TryGet<CostCapValues>("PipelineCostCap", out var cap) ? cap! : throw new InvalidOperationException("no cap");

    private static async Task<(PipelineContext Pipeline, string Answer)> StateAsync(
        PipelineCostCapConfig caps, string? effort, CostCapValues? resolved = null)
    {
        var pipeline = new PipelineContext();
        if (resolved is not null) pipeline.Set("PipelineCostCap", resolved);

        _ = caps;
        var config = AgentSmithConfig.Empty();
        var recorder = new ScopeEstimateRecorder(config, Mock.Of<IEventPublisher>(), NullLogger<ScopeEstimateRecorder>.Instance);
        var tool = new WorkPictureToolHost(new SpecDialogReplySlot(), (tier, ct) => recorder.ApplyTierAsync(pipeline, tier, ct))
            .GetTools(null, null).Single();
        var args = new AIFunctionArguments
        {
            ["kind"] = "rebuild", ["truth"] = "the upload describes it", ["size"] = "series",
        };
        if (effort is not null) args["effort"] = effort;
        var answer = (await tool.InvokeAsync(args))?.ToString() ?? string.Empty;
        return (pipeline, answer);
    }
}
