using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// What the review is allowed to DO with what it found: replace a criterion by the
/// observation that decides it, publish that as a revision of the artifact on the branch,
/// and hand everything else to the author. The closure is the safety argument — a critic
/// free to reshape the contract makes it satisfiable instead of making it true.
/// </summary>
public sealed class SpecReviewCorrectionTests
{
    private const string Shape = "the package manifest carries the upgraded versions";
    private const string Outcome = "an audit reports zero high-severity findings";
    private const string Yaml = """
        phase: p1
        goal: "Upgrade the vulnerable dependencies"
        steps:
          - id: upgrade
            action: "Upgrade the flagged packages"
        done:
          - "the package manifest carries the upgraded versions"
          - "the lint command exits 0"
        """;

    private static SpecPhase Phase() =>
        new(new PhaseDraft("p1", "Upgrade the vulnerable dependencies", Yaml, [])
        {
            Done = [Shape, "the lint command exits 0"],
        }, "upgrade-the-vulnerable-dependencies", "# markdown", [1]);

    private static CriterionReview Finding(string? replacement) =>
        new(Shape, SpecReviewDisposition.PrescribesShape,
            "search_branch for the flagged packages in package.json",
            "none of them is a direct dependency", Replacement: replacement);

    private sealed class StubReviewer(SpecReview review) : ISpecReviewer
    {
        public Task<SpecReview> ReviewAsync(
            SpecPhase phase, AgentConfig agent, BranchSearch? search,
            PipelineCostTracker costTracker, CancellationToken cancellationToken) =>
            Task.FromResult(review);
    }

    private static SpecSet Set(SpecPhase phase, params string[] executed) =>
        new("ticket-1", [phase], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            SpecSource.Derived, ExecutedPhaseIds: executed);

    private static Task<SpecReviewOutcome> Run(SpecReview review, SpecSet set) =>
        new SpecReviewPass(new StubReviewer(review), NullLogger<SpecReviewPass>.Instance)
            .RunAsync(set, new AgentConfig(), null, PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

    [Fact]
    public void SpecReview_ACorrection_ReplacesTheCriterionInTheDoneListOnly()
    {
        var (phase, unapplied) = SpecReviewCorrection.Apply(Phase(), [Finding(Outcome)]);

        unapplied.Should().BeEmpty();
        phase.Draft.Done.Should().Equal(Outcome, "the lint command exits 0");
        phase.Draft.Yaml.Should().Contain($"- \"{Outcome}\"");
        // The goal says "Upgrade the vulnerable dependencies" and is untouched: the swap
        // happens after the done: marker so no other part of the spec can be edited.
        phase.Draft.Yaml.Should().Contain("goal: \"Upgrade the vulnerable dependencies\"");
    }

    [Fact]
    public void SpecReview_ACriterionTheYamlDoesNotCarryVerbatim_IsNotCorrected()
    {
        var invented = new CriterionReview(
            "a criterion this spec never stated", SpecReviewDisposition.PrescribesShape,
            "search_branch", "no match", Replacement: Outcome);

        var (phase, unapplied) = SpecReviewCorrection.Apply(Phase(), [invented]);

        unapplied.Should().ContainSingle();
        phase.Draft.Yaml.Should().Be(Yaml);
    }

    [Fact]
    public async Task SpecReview_ARequestOutsideTheThreeShapes_ReachesTheHandbackInstead()
    {
        // A finding that names no replacement: objecting is not knowing what should stand
        // instead, and the review may not invent the difference.
        var outcome = await Run(new SpecReview("p1", [Finding(replacement: null)]), Set(Phase()));

        outcome.ParksTheRun.Should().BeTrue();
        outcome.ChangedTheContract.Should().BeFalse();
        outcome.ForTheAuthor.Should().ContainSingle();
        outcome.ParkedPhaseId.Should().Be("p1");
    }

    [Fact]
    public async Task SpecReview_ACorrectableFinding_ChangesTheContractAndDoesNotPark()
    {
        var outcome = await Run(new SpecReview("p1", [Finding(Outcome)]), Set(Phase()));

        outcome.ParksTheRun.Should().BeFalse();
        outcome.ChangedTheContract.Should().BeTrue();
        outcome.Set.Phases[0].Draft.Done.Should().Contain(Outcome);
    }

    [Fact]
    public async Task SpecReview_APhaseThatAlreadyRan_IsNotReviewed()
    {
        // Correcting the contract a finished phase was judged by would rewrite the record of
        // work that already sits in the branch history.
        var outcome = await Run(new SpecReview("p1", [Finding(Outcome)]), Set(Phase(), "p1"));

        outcome.Reviews.Should().BeEmpty();
        outcome.ParksTheRun.Should().BeFalse();
        outcome.ChangedTheContract.Should().BeFalse();
    }

    [Fact]
    public void SpecReview_ACorrectedSpec_IsPublishedAsARevisionCarryingTheFinding()
    {
        var at = DateTimeOffset.UtcNow;

        var revised = SpecReviewRevision.Of(Set(Phase()), [Finding(Outcome)], at);

        revised.Revisions.Should().HaveCount(2);
        revised.Current.Number.Should().Be(2);
        revised.Current.At.Should().Be(at);
        revised.Current.Cause.Should().Contain("spec review");
        revised.Current.Cause.Should().Contain(Shape);
    }

    [Fact]
    public async Task SpecReview_RoundsZero_SkipsTheReviewEntirely()
    {
        var handler = new ReviewSpecHandler(
            null!, null!,
            new LoopLimitsConfig { MaxSpecReviewRounds = 0 },
            NullLogger<ReviewSpecHandler>.Instance);

        var result = await handler.ExecuteAsync(
            new ReviewSpecContext(null, [], new AgentConfig(), new PipelineContext()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Spec review is off");
    }
}
