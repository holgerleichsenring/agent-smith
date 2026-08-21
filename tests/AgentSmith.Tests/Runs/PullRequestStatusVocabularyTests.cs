using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Runs;

/// <summary>
/// p0501: three outcomes of finishing a pull request, and the two questions the
/// surfaces ask about them. Merged is done, refused needs a human, and ARMED is
/// neither — the platform will merge it itself when its policy passes. Collapsing
/// armed into either neighbour reports a lie: as merged it claims work that has not
/// happened, as refused it sends the operator to look at something already finishing
/// itself.
/// </summary>
public sealed class PullRequestStatusVocabularyTests
{
    [Theory]
    [InlineData(PullRequestStatuses.Opened)]
    [InlineData(PullRequestStatuses.Completed)]
    [InlineData(PullRequestStatuses.CompletionArmed)]
    [InlineData(PullRequestStatuses.CompletionRefused)]
    public void PullRequestStatuses_EveryStatusThatProducedOne_HasAPullRequest(string status) =>
        PullRequestStatuses.HasPullRequest(status).Should().BeTrue();

    [Theory]
    [InlineData(PullRequestStatuses.NoChanges)]
    [InlineData(PullRequestStatuses.Failed)]
    [InlineData(null)]
    public void PullRequestStatuses_NoPullRequestWasOpened_HasNone(string? status) =>
        PullRequestStatuses.HasPullRequest(status).Should().BeFalse();

    [Fact]
    public void PullRequestStatuses_Armed_HasAPullRequest_ButNeedsNoHuman()
    {
        PullRequestStatuses.HasPullRequest(PullRequestStatuses.CompletionArmed).Should().BeTrue(
            "it exists and links to something");
        PullRequestStatuses.NeedsAHuman(PullRequestStatuses.CompletionArmed).Should().BeFalse(
            "it is waiting on a build, not on a person");
    }

    [Theory]
    [InlineData(PullRequestStatuses.Opened)]
    [InlineData(PullRequestStatuses.CompletionRefused)]
    public void PullRequestStatuses_StillOpen_NeedsAHuman(string status) =>
        PullRequestStatuses.NeedsAHuman(status).Should().BeTrue();

    [Fact]
    public void PullRequestStatuses_Merged_NeedsNoHuman() =>
        PullRequestStatuses.NeedsAHuman(PullRequestStatuses.Completed).Should().BeFalse();
}
