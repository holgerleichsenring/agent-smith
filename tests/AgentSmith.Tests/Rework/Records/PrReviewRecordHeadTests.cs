using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewHostFakes;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-10-15-114fc: the head commit each host's state read returns, and the refresh's stamps — a head move when a
/// successful read sees another sha than the stored one, a reopen when it sees an ended record open — timed by the
/// read's check time; a read without a sha keeps the stored one, a failed read stamps nothing.
/// </summary>
public sealed class PrReviewRecordHeadTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();

    public void Dispose() => _h.Dispose();

    private void Read(string state, string? sha) =>
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Pr1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("1", state, T, [], sha));

    private async Task<PrReviewRecord> RefreshAsync(TimeSpan later)
    {
        _h.Clock.Now += later;
        await _h.RefreshAsync();
        return _h.Records().Single();
    }

    [Fact]
    public async Task GitHubPrReviewStateReader_ReadsTheHeadSha()
    {
        const string reply = """
            { "data": { "viewer": { "login": "agent" }, "repository": { "pullRequest": {
              "number": 4, "state": "OPEN", "createdAt": "2026-10-01T08:00:00Z", "mergedAt": null, "closedAt": null,
              "headRefOid": "abc123", "author": { "login": "agent" }, "latestReviews": { "nodes": [] } } } } }
            """;

        var state = await new GitHubPrReviewStateReader(GitHubReplying(reply), "o", "r", "https://github.com/o/r")
            .ReadAsync("https://github.com/o/r/pull/4", CancellationToken.None);

        state.HeadSha.Should().Be("abc123");
    }

    [Fact]
    public async Task GitLabMrStateReader_ReadsTheHeadSha()
    {
        var state = await new GitLabMrStateReader("https://gitlab.example", "o%2Fr", "t", "https://gitlab.example/o/r", GitLabAnswering(path =>
                path.EndsWith("/api/v4/user") ? """{ "id": 1 }"""
                : path.EndsWith("/notes") ? "[]"
                : """{ "iid": 3, "state": "opened", "sha": "def456", "created_at": "2026-10-01T08:00:00Z", "author": { "id": 2 } }"""),
                NullLogger.Instance)
            .ReadAsync("https://gitlab.example/o/r/-/merge_requests/3", CancellationToken.None);

        state.HeadSha.Should().Be("def456");
    }

    [Fact]
    public void AzureReposVoteDecisions_ReadsTheLastMergeSourceCommit()
    {
        var pr = AzurePr(PullRequestStatus.Active);
        pr.LastMergeSourceCommit = new GitCommitRef { CommitId = "fed789" };

        AzureReposVoteDecisions.Read(pr, [], "self", "u").HeadSha.Should().Be("fed789");
    }

    [Fact]
    public async Task PrReviewRecordRefresh_FirstReadStoresTheShaWithoutAMove()
    {
        await _h.RunAsync("run-1", Pr1);
        Read(PrReviewStates.Open, "h1");

        var record = await RefreshAsync(TimeSpan.Zero);

        (record.HeadSha, record.HeadMovedAtTicks, record.ReopenedAtTicks).Should().Be(("h1", (long?)null, (long?)null));
    }

    [Fact]
    public async Task PrReviewRecordRefresh_ShaChanged_StampsTheMoveWithTheReadsCheckTime()
    {
        await _h.RunAsync("run-1", Pr1);
        Read(PrReviewStates.Open, null);
        await RefreshAsync(TimeSpan.Zero);
        Read(PrReviewStates.Open, "h1");
        (await RefreshAsync(TimeSpan.FromMinutes(1))).HeadMovedAtTicks.Should().BeNull("a record that had no sha yet stores one without a move");
        Read(PrReviewStates.Open, "h2");

        var record = await RefreshAsync(TimeSpan.FromMinutes(1));

        (record.HeadSha, record.HeadMovedAtTicks).Should().Be(("h2", (long?)record.CheckedAtTicks));
        record.CheckedAtTicks.Should().Be(_h.Clock.Now.UtcTicks);
    }

    [Fact]
    public async Task PrReviewRecordRefresh_ShaUnchanged_KeepsTheMove()
    {
        await _h.RunAsync("run-1", Pr1);
        Read(PrReviewStates.Open, "h1");
        await RefreshAsync(TimeSpan.Zero);
        Read(PrReviewStates.Open, "h2");
        var moved = (await RefreshAsync(TimeSpan.FromMinutes(1))).HeadMovedAtTicks;

        var record = await RefreshAsync(TimeSpan.FromMinutes(1));

        (record.HeadSha, record.HeadMovedAtTicks).Should().Be(("h2", moved));
    }

    [Fact]
    public async Task PrReviewRecordRefresh_ReadWithoutASha_KeepsTheStoredSha()
    {
        await _h.RunAsync("run-1", Pr1);
        Read(PrReviewStates.Open, "h1");
        await RefreshAsync(TimeSpan.Zero);
        Read(PrReviewStates.Open, null);

        var record = await RefreshAsync(TimeSpan.FromMinutes(1));

        (record.HeadSha, record.HeadMovedAtTicks).Should().Be(("h1", (long?)null));
    }

    [Fact]
    public async Task PrReviewRecordRefresh_ClosedRecordReadOpen_StampsTheReopen()
    {
        await _h.RunAsync("run-1", Pr1);
        Read(PrReviewStates.Closed, "h1");
        (await RefreshAsync(TimeSpan.Zero)).ReopenedAtTicks.Should().BeNull();
        Read(PrReviewStates.Open, "h1");

        var record = await RefreshAsync(TimeSpan.FromMinutes(1));

        (record.State, record.ReopenedAtTicks).Should().Be((PrReviewStates.Open, (long?)_h.Clock.Now.UtcTicks));
    }

    [Fact]
    public async Task PrReviewRecordRefresh_ReadFailsAfterAClose_StampsNoReopen()
    {
        await _h.RunAsync("run-1", Pr1);
        Read(PrReviewStates.Closed, "h1");
        await RefreshAsync(TimeSpan.Zero);
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Pr1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("host down"));

        var record = await RefreshAsync(TimeSpan.FromMinutes(1));

        (record.State, record.HeadSha, record.HeadMovedAtTicks, record.ReopenedAtTicks)
            .Should().Be((PrReviewStates.Closed, "h1", (long?)null, (long?)null));
    }
}
