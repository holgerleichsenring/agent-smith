using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class SkillRoundBufferMergeTests
{
    [Fact]
    public void ApplyBufferToContext_AssignsSequentialObservationIds_FromOne()
    {
        var pipeline = new PipelineContext();
        var buffer = BufferWith("alpha", round: 1, observationCount: 3);

        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, buffer);

        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs);
        obs!.Select(o => o.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ApplyBufferToContext_TwoBuffersInOrder_IdsAreSequential()
    {
        var pipeline = new PipelineContext();

        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, BufferWith("alpha", 1, 2));
        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, BufferWith("beta", 1, 3));

        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs);
        obs!.Select(o => o.Id).Should().Equal(1, 2, 3, 4, 5);
        obs!.Where(o => o.Role == "alpha").Should().HaveCount(2);
        obs!.Where(o => o.Role == "beta").Should().HaveCount(3);
    }

    [Fact]
    public void ApplyBufferToContext_AppendsDiscussionEntryInCallOrder()
    {
        var pipeline = new PipelineContext();
        var entryA = new DiscussionEntry("alpha", "Alpha", "🅰", 1, "first");
        var entryB = new DiscussionEntry("beta", "Beta", "🅱", 1, "second");

        SkillRoundHandlerBase.ApplyBufferToContext(pipeline,
            new SkillRoundBuffer("alpha", 1, [], entryA, null));
        SkillRoundHandlerBase.ApplyBufferToContext(pipeline,
            new SkillRoundBuffer("beta", 1, [], entryB, null));

        pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var log);
        log!.Select(e => e.RoleName).Should().Equal("alpha", "beta");
    }

    [Fact]
    public void ApplyBufferToContext_StructuredOutput_IsStoredUnderSkillName()
    {
        var pipeline = new PipelineContext();
        var buffer = new SkillRoundBuffer("gate", 0, [], null, "gate-output");

        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, buffer);

        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SkillOutputs, out var outputs);
        outputs!["gate"].Should().Be("gate-output");
    }

    [Fact]
    public void ApplyBufferToContext_OnExistingObservations_ContinuesIdSequence()
    {
        var pipeline = new PipelineContext();
        var existing = new List<SkillObservation>
        {
            new(7, "earlier", ObservationConcern.Correctness, "x", "", false,
                ObservationSeverity.Info, 50, null)
        };
        pipeline.Set(ContextKeys.SkillObservations, existing);

        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, BufferWith("alpha", 1, 2));

        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs);
        obs!.Select(o => o.Id).Should().Equal(7, 8, 9);
    }

    private static SkillRoundBuffer BufferWith(string skill, int round, int observationCount)
    {
        var observations = Enumerable.Range(0, observationCount)
            .Select(_ => new SkillObservation(
                Id: 0,
                Role: skill,
                Concern: ObservationConcern.Correctness,
                Description: "test",
                Suggestion: "",
                Blocking: false,
                Severity: ObservationSeverity.Info,
                Confidence: 80,
                Rationale: null))
            .ToList();
        return new SkillRoundBuffer(skill, round, observations, null, null);
    }
}
