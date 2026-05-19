using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0147b: round handlers route the SkillCallRuntime's typed
/// execution-limit / execution-error observations into the shared pipeline
/// observation list, the same way regular LLM observations flow — so silent
/// skill drops surface in the final operator summary regardless of which
/// outcome path the call took.
/// </summary>
public sealed class SkillRoundHandlerRuntimeObservationFlowTests
{
    private static readonly ISkillRoundBufferDispatcher Dispatcher = new SkillRoundBufferDispatcher();

    private static void Buffer(PipelineContext pipeline, string skillName, int round, SkillCallResult result)
    {
        if (result.RuntimeObservations.Count == 0) return;
        var buffer = new SkillRoundBuffer(
            skillName, round, result.RuntimeObservations.ToList(), null, null);
        Dispatcher.Dispatch(pipeline, buffer);
    }

    [Fact]
    public void BufferRuntimeObservations_AppendsToPipelineObservationList()
    {
        var pipeline = new PipelineContext();
        var result = MakeResultWithLimit(ExecutionLimitCategories.ExecutionLimitTokens);

        Buffer(pipeline, "skill-x", round: 1, result);

        var observations = pipeline.Get<List<SkillObservation>>(ContextKeys.SkillObservations);
        observations.Should().ContainSingle();
        observations[0].Category.Should().Be(ExecutionLimitCategories.ExecutionLimitTokens);
        observations[0].Severity.Should().Be(ObservationSeverity.Info);
    }

    [Fact]
    public void BufferRuntimeObservations_EmptyResult_DoesNotTouchPipeline()
    {
        var pipeline = new PipelineContext();
        var okResult = new SkillCallResult
        {
            Outcome = SkillCallOutcome.Ok,
            Output = "{}",
            Cost = MakeCost(),
            Trace = Array.Empty<LoopTraceEntry>()
        };

        Buffer(pipeline, "skill-x", round: 1, okResult);

        pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.SkillObservations, out var existing).Should().BeFalse();
    }

    [Fact]
    public void BufferRuntimeObservations_MultipleCalls_Accumulate()
    {
        var pipeline = new PipelineContext();

        Buffer(pipeline, "skill-a", 0,
            MakeResultWithLimit(ExecutionLimitCategories.ExecutionLimitTokens));
        Buffer(pipeline, "skill-b", 0,
            MakeResultWithLimit(ExecutionLimitCategories.ExecutionLimitWallClock));

        var observations = pipeline.Get<List<SkillObservation>>(ContextKeys.SkillObservations);
        observations.Should().HaveCount(2);
        observations.Select(o => o.Category).Should().BeEquivalentTo(new[]
        {
            ExecutionLimitCategories.ExecutionLimitTokens,
            ExecutionLimitCategories.ExecutionLimitWallClock
        });
    }

    private static SkillCallResult MakeResultWithLimit(string category) => new()
    {
        Outcome = SkillCallOutcome.Incomplete,
        Output = null,
        Cost = MakeCost(),
        Trace = Array.Empty<LoopTraceEntry>(),
        RuntimeObservations = new[]
        {
            new SkillObservation(
                Id: 0, Role: "runtime",
                Concern: ObservationConcern.Risk,
                Description: $"skill hit {category}",
                Suggestion: "raise budget",
                Blocking: false,
                Severity: ObservationSeverity.Info,
                Confidence: 100,
                EvidenceMode: EvidenceMode.Confirmed,
                Category: category)
        }
    };

    private static CallCostRecord MakeCost() => new()
    {
        SkillName = "skill-x",
        Role = "investigator",
        Phase = SkillExecutionPhase.Plan,
        StartedAt = DateTimeOffset.UtcNow
    };
}
