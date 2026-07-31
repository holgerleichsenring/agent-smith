using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0312c: the PR diff reaches the master through {PrDiffSection}. Before this
/// it was rendered only by PrReviewSkillPromptStrategy, which died with the
/// SkillRound machinery — a pr-review master without this section would review
/// nothing while reporting a clean review.
/// </summary>
public sealed class PrDiffPromptSectionTests
{
    [Fact]
    public void Build_PipelineWithoutAPullRequest_IsEmpty()
    {
        // Every master binds the placeholder; only pr-review-master carries it.
        PrDiffPromptSection.Build(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void Build_WithDiff_CarriesCoordinatesAndTheRenderedDiff()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PrNumber, "42");
        pipeline.Set(ContextKeys.PrAuthor, "someone");
        pipeline.Set(ContextKeys.PrHead, "headsha");
        pipeline.Set(ContextKeys.PrBase, "basesha");
        pipeline.Set(ContextKeys.PrDiff, new PrDiffAnalysis("basesha", "headsha", []));

        var section = PrDiffPromptSection.Build(pipeline);

        section.Should().Contain("PR #42").And.Contain("someone")
            .And.Contain("headsha").And.Contain("basesha");
    }

    [Fact]
    public void Build_PrNumberButNoAnalysedDiff_SaysSoInsteadOfRenderingNothing()
    {
        // Silence would read to the model as "no changes", which is the one
        // wrong answer: it would report a clean review of an unread diff.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PrNumber, "7");

        var section = PrDiffPromptSection.Build(pipeline);

        section.Should().NotBeEmpty();
        section.Should().Contain("no structured diff available");
    }
}
