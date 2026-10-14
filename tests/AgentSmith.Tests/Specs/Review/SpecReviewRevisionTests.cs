using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762g: the one mid-run revision publisher — one revision naming its cause, the placed
/// repository and the approval kept, a hand-back carried, and a revision the branch did not take failing.</summary>
public sealed class SpecReviewRevisionTests
{
    private const string Cause = $"spec review round 2 of {First}: build-with-cuts";

    [Fact]
    public async Task Revision_ChangedRound_PublishesOneRevisionNamingTheRound()
    {
        var rig = new ReviewLoopRig();

        var result = await PublishAsync(rig);

        result.IsSuccess.Should().BeTrue();
        var published = rig.Published.Should().ContainSingle().Subject;
        published.Revisions.Select(r => (r.Number, r.Cause)).Should().Equal((1, "initial derivation"), (2, Cause));
        published.Specs[0].Draft.Yaml.Should().Contain("one dated file");
        published.Specs[1].Should().Be(rig.Published[0].Specs[1], "a spec no replacement names is left as it was");
    }

    [Fact]
    public async Task Revision_SpecYamlWithoutRepos_KeepsItsPlacedRepository()
    {
        var rig = new ReviewLoopRig();

        await PublishAsync(rig);

        rig.Published.Single().Specs[0].Draft.Repos.Should().Equal("shop");
    }

    [Fact]
    public async Task Revision_WriteNotMade_FailsTheStep()
    {
        var rig = new ReviewLoopRig { WritesToBranch = false };

        var result = await PublishAsync(rig);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("was not written to the ticket branch").And.Contain(Cause);
    }

    [Fact]
    public async Task Revision_WithHandback_PublishesTheSetCarryingIt()
    {
        var rig = new ReviewLoopRig();
        var handback = new SpecHandback(SpecHandbackCase.Question, "Which file format does accounting read?");

        await new SpecReviewRevision(rig.Publisher.Object).PublishAsync(rig.Pipeline, [], Cause, CancellationToken.None, handback);

        rig.Published.Single().Handback.Should().Be(handback);
    }

    [Fact]
    public async Task Revision_ApprovedSet_KeepsItsApproval()
    {
        var rig = new ReviewLoopRig();
        var approval = new SpecApproval(DateTimeOffset.UtcNow, "session-1", "carol");
        rig.Pipeline.Set(ContextKeys.SpecSet, rig.Set with { Approval = approval });

        await PublishAsync(rig);

        rig.Published.Single().Approval.Should().Be(approval);
    }

    private static Task<AgentSmith.Domain.Models.CommandResult> PublishAsync(ReviewLoopRig rig)
    {
        var revised = CutRevisionFixtures.Draft(CutRevisionFixtures.With("into one file", "into one dated file"));
        return new SpecReviewRevision(rig.Publisher.Object).PublishAsync(rig.Pipeline, [revised], Cause, CancellationToken.None);
    }
}
