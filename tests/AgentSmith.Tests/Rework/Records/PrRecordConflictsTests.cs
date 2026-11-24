using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewHostFakes;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-24-6811a: whether a host says an open pull request conflicts with its base — true, false, or not decided
/// yet — is read from each host and recorded; an undecided read keeps what the host said last.</summary>
public sealed class PrRecordConflictsTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Theory]
    [InlineData("\"CONFLICTING\"", true)]
    [InlineData("\"MERGEABLE\"", false)]
    [InlineData("\"UNKNOWN\"", null)]
    [InlineData("null", null)]
    public async Task GitHub_ReadsMergeable(string mergeable, bool? expected)
    {
        var reply = $$"""
            { "data": { "viewer": { "login": "agent" }, "repository": { "pullRequest": {
              "number": 4, "state": "OPEN", "isDraft": false, "mergeable": {{mergeable}}, "createdAt": "2026-10-01T08:00:00Z",
              "mergedAt": null, "closedAt": null, "author": { "login": "agent" }, "latestReviews": { "nodes": [] } } } } }
            """;
        var state = await new GitHubPrReviewStateReader(GitHubReplying(reply),
            AgentSmith.Infrastructure.Models.GitHubRepoAddress.Parse("https://github.com/o/r")).ReadAsync("https://github.com/o/r/pull/4", CancellationToken.None);

        state.HasConflicts.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{ "detailed_merge_status": "conflict", "has_conflicts": true }""", true)]
    [InlineData("""{ "detailed_merge_status": "mergeable", "has_conflicts": false }""", false)]
    [InlineData("""{ "detailed_merge_status": "checking", "has_conflicts": false }""", null)]
    [InlineData("""{ "merge_status": "unchecked", "has_conflicts": false }""", null)]
    [InlineData("""{ "has_conflicts": true }""", true)]
    [InlineData("""{ }""", null)]
    public void GitLab_ReadsHasConflicts_ButNotWhileChecking(string json, bool? expected)
    {
        using var doc = JsonDocument.Parse(json);

        GitLabMergeRequestRead.Conflicts(doc.RootElement).Should().Be(expected);
    }

    [Fact]
    public void Azure_ReadsTheMergeStatus()
    {
        var pr = AzurePr(PullRequestStatus.Active);
        pr.MergeStatus = PullRequestAsyncStatus.Conflicts;
        AzureReposVoteDecisions.Read(pr, [], "self", "u").HasConflicts.Should().BeTrue();
        pr.MergeStatus = PullRequestAsyncStatus.Succeeded;
        AzureReposVoteDecisions.Read(pr, [], "self", "u").HasConflicts.Should().BeFalse();
        pr.MergeStatus = PullRequestAsyncStatus.Queued;
        AzureReposVoteDecisions.Read(pr, [], "self", "u").HasConflicts.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_RecordsAConflict_KeepsItOnAnUndecidedRead_AndClearsItWhenClean()
    {
        await _h.RunAsync("r1", Pr1);
        State(true);
        await _h.RefreshAsync();
        var first = _h.Records().Single();
        first.HasConflicts.Should().BeTrue();
        first.ConflictsAtTicks.Should().NotBeNull();

        State(null);
        _h.Clock.Now = _h.Clock.Now.AddMinutes(10);
        await _h.RefreshAsync();
        _h.Records().Single().Should().Match<PrReviewRecord>(r => r.HasConflicts == true && r.ConflictsAtTicks == first.ConflictsAtTicks,
            "GitHub answers UNKNOWN while it recomputes, which says nothing new");

        State(false);
        _h.Clock.Now = _h.Clock.Now.AddMinutes(10);
        await _h.RefreshAsync();
        _h.Records().Single().Should().Match<PrReviewRecord>(r => r.HasConflicts == false && r.ConflictsAtTicks == null);
    }

    [Fact]
    public void Copy_AnEndedRecord_ConflictsWithNothing()
    {
        var stored = new PrReviewRecord { State = PrReviewStates.Open, HasConflicts = true, ConflictsAtTicks = 5 };

        PrReviewRecordCopy.Record(new PrReviewRecord { State = PrReviewStates.Merged, HasConflicts = null, CheckedAtTicks = 9 }, stored);

        (stored.HasConflicts, stored.ConflictsAtTicks).Should().Be(((bool?)null, (long?)null));
    }

    private void State(bool? conflicts) =>
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Pr1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("1", PrReviewStates.Open, T, []) { HasConflicts = conflicts });
}
