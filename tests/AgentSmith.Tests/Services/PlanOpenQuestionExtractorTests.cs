using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class PlanOpenQuestionExtractorTests
{
    private readonly IPlanOpenQuestionExtractor _sut =
        new PlanOpenQuestionExtractor(NullLogger<PlanOpenQuestionExtractor>.Instance);

    [Fact]
    public void Complete_NoQuestions_NoSideChannel()
    {
        var pipeline = new PipelineContext();
        var plan = MakePlan(PlanStatus.Complete, openQuestions: []);

        var count = _sut.PublishSideChannel(plan, "raw", pipeline);

        count.Should().Be(0);
        pipeline.TryGet<string>(ContextKeys.PlanJson, out _).Should().BeFalse();
    }

    [Fact]
    public void NeedsUserInput_NoQuestions_PublishesRawAnyway()
    {
        // Even when the plan declared NeedsUserInput but emitted no questions,
        // downstream PlanOpenQuestionsHandler is the one that detects this and
        // logs a warning — the extractor's job is just to surface the raw JSON.
        var pipeline = new PipelineContext();
        var plan = MakePlan(PlanStatus.NeedsUserInput, openQuestions: []);

        var count = _sut.PublishSideChannel(plan, "raw", pipeline);

        count.Should().Be(0);
        pipeline.TryGet<string>(ContextKeys.PlanJson, out var json).Should().BeTrue();
        json.Should().Be("raw");
    }

    [Fact]
    public void Complete_WithQuestions_PublishesAndReturnsCount()
    {
        var pipeline = new PipelineContext();
        var plan = MakePlan(PlanStatus.Complete, openQuestions:
        [
            new PlanOpenQuestion("q1", "what about X?", []),
            new PlanOpenQuestion("q2", "and Y?", []),
        ]);

        var count = _sut.PublishSideChannel(plan, "raw-json", pipeline);

        count.Should().Be(2);
        pipeline.TryGet<string>(ContextKeys.PlanJson, out var json).Should().BeTrue();
        json.Should().Be("raw-json");
    }

    private static Plan MakePlan(PlanStatus status, IReadOnlyList<PlanOpenQuestion> openQuestions) =>
        new("summary", Array.Empty<PlanStep>(), "{}")
        {
            Status = status,
            OpenQuestions = openQuestions,
        };
}
