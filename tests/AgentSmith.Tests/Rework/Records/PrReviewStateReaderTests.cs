using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using static AgentSmith.Tests.Rework.Records.PrReviewHostFakes;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-10-13-9a59b: a pull request's state and standing decisions, read from each host.</summary>
public sealed class PrReviewStateReaderTests
{
    private const string GitLabMr = "https://gitlab.example/o/r/-/merge_requests/3";

    private static string GitHubReply(string state, string reviews) => $$"""
        { "data": { "viewer": { "login": "agent" }, "repository": { "pullRequest": {
          "number": 4, "state": "{{state}}", "createdAt": "2026-10-01T08:00:00Z", "mergedAt": {{(state == "MERGED" ? "\"2026-10-09T08:00:00Z\"" : "null")}}, "closedAt": null,
          "author": { "login": "agent" },
          "latestReviews": { "nodes": [ {{reviews}} ] } } } } }
        """;

    private static string Review(string id, string state, string login, string type = "User") =>
        $$"""{ "id": "{{id}}", "state": "{{state}}", "body": "", "author": { "__typename": "{{type}}", "login": "{{login}}" }, "authorAssociation": "MEMBER", "submittedAt": "2026-10-08T10:00:00Z" }""";

    private static Task<PrReviewState> ReadGitHub(string reply) =>
        new GitHubPrReviewStateReader(GitHubReplying(reply), GitHubRepoAddress.Parse("https://github.com/o/r")).ReadAsync("https://github.com/o/r/pull/4", CancellationToken.None);

    private static Task<PrReviewState> ReadGitLab(string notes) =>
        new GitLabMrStateReader("https://gitlab.example", "o%2Fr", "t", "https://gitlab.example/o/r", GitLabAnswering(path =>
                path.EndsWith("/api/v4/user") ? """{ "id": 1 }"""
                : path.Contains("/users/") ? """{ "bot": false }"""
                : path.EndsWith("/notes") ? notes
                : """{ "iid": 3, "state": "opened", "created_at": "2026-10-01T08:00:00Z", "author": { "id": 2 } }"""), NullLogger.Instance)
            .ReadAsync(GitLabMr, CancellationToken.None);

    private static string Note(long id, long author, string body, string at) =>
        $$"""{ "id": {{id}}, "body": "{{body}}", "system": true, "created_at": "{{at}}", "author": { "id": {{author}}, "username": "u{{author}}" } }""";

    [Fact]
    public async Task PrReviewState_GitHub_ApprovalAndMergeRead()
    {
        var state = await ReadGitHub(GitHubReply("MERGED",
            $"{Review("R1", "APPROVED", "alice")}, {Review("R2", "COMMENTED", "bob")}, {Review("R3", "APPROVED", "renovate", "Bot")}"));

        state.State.Should().Be(PrReviewStates.Merged);
        state.StateAt.Should().Be(new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero));
        state.Number.Should().Be("4");
        state.Decisions.Should().ContainSingle().Which.Should().Match<PrReviewDecision>(d =>
            d.HostId == "R1" && d.Decision == PrReviewDecisions.Approved && d.Author!.AuthorLogin == "alice");
    }

    [Fact]
    public void PrReviewState_AzureDevOps_MapsTenFiveMinusFiveMinusTen()
    {
        var at = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
        var pr = AzurePr(PullRequestStatus.Active,
            new() { Id = "a", Vote = 10 }, new() { Id = "b", Vote = 5 }, new() { Id = "c", Vote = -5 }, new() { Id = "d", Vote = -10 });

        var state = AzureReposVoteDecisions.Read(pr, [VoteThread(7, "a", 10, at), VoteThread(8, "c", -5, at)], "self", "u");

        state.State.Should().Be(PrReviewStates.Open);
        state.Decisions.Select(d => (d.Author!.AuthorId, d.Decision, d.HostId)).Should().Equal(
            ("a", PrReviewDecisions.Approved, "7/1"), ("b", PrReviewDecisions.ApprovedWithSuggestions, "vote:b"),
            ("c", PrReviewDecisions.ChangesRequested, "8/1"), ("d", PrReviewDecisions.Rejected, "vote:d"));
    }

    [Fact]
    public void PrReviewState_AzureDevOps_CreatorAutoCompleteVote_IsNoApproval()
    {
        var at = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
        var pr = AzurePr(PullRequestStatus.Completed, new() { Id = "creator", Vote = 10 }, new() { Id = "self", Vote = 10 });

        var state = AzureReposVoteDecisions.Read(pr, [VoteThread(7, "creator", 10, at)], "self", "u");

        state.State.Should().Be(PrReviewStates.Merged);
        state.Decisions.Should().BeEmpty("the auto-complete approves as the creator, and the token's own vote is ours");
    }

    [Fact]
    public void PrReviewState_AzureDevOps_CreatorWaitForAuthor_IsChangesRequested()
    {
        var at = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
        var pr = AzurePr(PullRequestStatus.Active, new() { Id = "creator", Vote = -5 }, new() { Id = "self", Vote = 10 });

        var state = AzureReposVoteDecisions.Read(pr, [VoteThread(8, "creator", -5, at)], "self", "u");

        state.Decisions.Select(d => (d.Author!.AuthorId, d.Decision, d.HostId)).Should().Equal(
            ("creator", PrReviewDecisions.ChangesRequested, "8/1"));
    }

    [Fact]
    public async Task PrReviewState_GitLab_TokenUserApproval_IsExcluded()
    {
        var state = await ReadGitLab($"[{Note(5, 1, "approved this merge request", "2026-10-08T12:00:00Z")}, {Note(4, 9, "approved this merge request", "2026-10-08T11:00:00Z")}]");

        state.State.Should().Be(PrReviewStates.Open);
        state.Decisions.Should().ContainSingle().Which.Should().Match<PrReviewDecision>(d =>
            d.HostId == "4" && d.Author!.AuthorId == "9" && d.Decision == PrReviewDecisions.Approved);
    }

    [Fact]
    public async Task PrReviewState_WithdrawnDecision_ReadsNone()
    {
        var dismissed = await ReadGitHub(GitHubReply("OPEN", Review("R1", "DISMISSED", "alice")));
        var reset = AzureReposVoteDecisions.Read(AzurePr(PullRequestStatus.Active, new IdentityRefWithVote { Id = "a", Vote = 0 }),
            [VoteThread(7, "a", 10, new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc))], "self", "u");
        var unapproved = await ReadGitLab($"[{Note(5, 9, "unapproved this merge request", "2026-10-08T12:00:00Z")}, {Note(4, 9, "approved this merge request", "2026-10-08T11:00:00Z")}]");

        dismissed.Decisions.Should().BeEmpty("GitHub: a dismissed review is no decision");
        reset.Decisions.Should().BeEmpty("Azure DevOps: a vote reset to 0 decides nothing");
        unapproved.Decisions.Should().BeEmpty("GitLab: the newest note withdrew the approval");
    }
}
