using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Metrics;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0140e: EmptyPlanCheckHandler short-circuits the pipeline when GeneratePlanHandler
/// produced a Plan with zero steps. Sets <see cref="ContextKeys.EmptyPlanSkipped"/>
/// and emits agent_smith_pipeline_skipped_as_irrelevant_total with project / pipeline
/// / reason labels. Plans with steps and missing Plan keys are no-ops.
/// </summary>
[Collection(MeterCollection.Name)]
public sealed class EmptyPlanCheckHandlerTests
{
    private const string SkipCounterName = "agent_smith_pipeline_skipped_as_irrelevant_total";

    private readonly EmptyPlanCheckHandler _handler = new(
        NullLogger<EmptyPlanCheckHandler>.Instance);

    [Fact]
    public async Task Handle_PlanWithSteps_DoesNotSkip_NoCounter()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, PlanWith(
            new PlanStep(1, "do the thing", targetFile: null, changeType: "modify")));
        var context = new EmptyPlanCheckContext(pipeline);

        using var capture = MeterCapture.ForCounter(SkipCounterName);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Has(ContextKeys.EmptyPlanSkipped).Should().BeFalse();
        capture.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PlanWithZeroSteps_SetsSkipFlag_EmitsCounter()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, PlanWith(/* no steps */));
        var context = new EmptyPlanCheckContext(pipeline);

        using var capture = MeterCapture.ForCounter(SkipCounterName);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Has(ContextKeys.EmptyPlanSkipped).Should().BeTrue();
        pipeline.Get<bool>(ContextKeys.EmptyPlanSkipped).Should().BeTrue();

        capture.Measurements.Should().HaveCount(1);
        var m = capture.Measurements[0];
        m.Value.Should().Be(1);
        TagValue(m.Tags, "reason").Should().Be("empty_plan");
    }

    [Fact]
    public async Task Handle_NoPlanInContext_NoOp()
    {
        var pipeline = new PipelineContext();
        var context = new EmptyPlanCheckContext(pipeline);

        using var capture = MeterCapture.ForCounter(SkipCounterName);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Has(ContextKeys.EmptyPlanSkipped).Should().BeFalse();
        capture.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PlanWithZeroSteps_CounterLabelsIncludeProjectAndPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, PlanWith(/* no steps */));
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            PipelineName: "fix-bug",
            Agent: new AgentConfig { Type = "claude", Model = "test" },
            SkillsPath: "skills/coding",
            CodingPrinciplesPath: null));
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            new TicketId("T-1"),
            title: "title",
            description: "desc",
            acceptanceCriteria: null,
            status: "open",
            source: "github/acme/app"));
        var context = new EmptyPlanCheckContext(pipeline);

        using var capture = MeterCapture.ForCounter(SkipCounterName);
        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capture.Measurements.Should().HaveCount(1);

        var tags = capture.Measurements[0].Tags;
        TagValue(tags, "pipeline").Should().Be("fix-bug");
        TagValue(tags, "project").Should().Be("github/acme/app");
        TagValue(tags, "reason").Should().Be("empty_plan");
    }

    private static Plan PlanWith(params PlanStep[] steps) =>
        new(summary: "test", steps: steps, rawResponse: "{}");

    private static string? TagValue(KeyValuePair<string, object?>[] tags, string key) =>
        tags.FirstOrDefault(t => t.Key == key).Value as string;
}
