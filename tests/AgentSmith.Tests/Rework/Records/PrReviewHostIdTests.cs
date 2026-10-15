using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using static AgentSmith.Tests.Rework.Records.PrReviewHostFakes;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-10-13-9a59b: every note, thread and act carries the host's own id, and a decision's id is the
/// id of the note the thread reader returns for the same object.
/// </summary>
public sealed class PrReviewHostIdTests
{
    private const string GitHubThreads = """
        { "data": { "viewer": { "login": "agent" }, "repository": { "pullRequest": {
          "reviewThreads": { "pageInfo": { "hasNextPage": false }, "nodes": [
            { "id": "T1", "isResolved": false, "path": "a.cs", "line": 3, "originalLine": 3,
              "comments": { "nodes": [ { "id": "C1", "author": { "__typename": "User", "login": "alice" }, "authorAssociation": "MEMBER",
                "body": "rename", "createdAt": "2026-10-08T10:00:00Z", "publishedAt": "2026-10-08T10:00:00Z" } ] } } ] },
          "reviews": { "nodes": [ { "id": "R1", "author": { "__typename": "User", "login": "alice" }, "authorAssociation": "MEMBER",
            "body": "Please fix", "submittedAt": "2026-10-08T10:05:00Z" } ] },
          "comments": { "nodes": [ { "id": "I1", "author": { "__typename": "User", "login": "bob" }, "authorAssociation": "MEMBER",
            "body": "looks fine", "createdAt": "2026-10-08T10:06:00Z" } ] } } } } }
        """;

    private const string GitLabDiscussions = """
        [ { "id": "d-1", "notes": [ { "id": 11, "body": "rename", "system": false, "resolvable": true, "resolved": false,
            "created_at": "2026-10-08T10:00:00Z", "author": { "id": 8, "username": "alice" } } ] } ]
        """;

    [Fact]
    public async Task PrReviewThreads_EveryHost_CarryHostIds()
    {
        var github = await new GitHubPrReviewThreadReader(GitHubReplying(GitHubThreads), "o", "r", "https://github.com/o/r", NullLogger.Instance)
            .ListAsync("https://github.com/o/r/pull/4", CancellationToken.None);
        var gitlab = await new GitLabPrReviewThreadReader("https://gitlab.example", "o%2Fr", "t", "https://gitlab.example/o/r",
                GitLabAnswering(p => p.EndsWith("/api/v4/user") ? """{ "id": 1 }""" : p.Contains("/users/") ? """{ "bot": false }""" : GitLabDiscussions),
                NullLogger.Instance)
            .ListAsync("https://gitlab.example/o/r/-/merge_requests/3", CancellationToken.None);
        var azure = AzureReposReviewMapping.Map([new GitPullRequestCommentThread
        {
            Id = 7, Status = CommentThreadStatus.Active,
            Comments = [new Comment { Id = 2, Content = "x", Author = new IdentityRef { Id = "u" }, PublishedDate = DateTime.UtcNow }],
        }], "u", "repo", "proj", null);

        github.Select(t => (t.HostId, t.FromReview, t.Notes.Single().HostId)).Should().Equal(
            ("T1", false, "C1"), ("R1", true, "R1"), ("I1", false, "I1"));
        gitlab.Single().HostId.Should().Be("d-1");
        gitlab.Single().Notes.Single().HostId.Should().Be("11");
        azure.Single().HostId.Should().Be("7");
        azure.Single().Notes.Single().HostId.Should().Be("7/2");
    }

    [Fact]
    public async Task PrReviewActs_EveryHost_CarryHostIds()
    {
        var github = await new GitHubPrReviewActReader(GitHubReplying("""
                { "data": { "repository": { "pullRequest": { "author": { "login": "agent" }, "latestReviews": { "nodes": [
                  { "id": "R9", "state": "CHANGES_REQUESTED", "author": { "__typename": "User", "login": "alice" },
                    "authorAssociation": "MEMBER", "submittedAt": "2026-10-08T10:00:00Z" } ] } } } } }
                """), "o", "r", "https://github.com/o/r")
            .ChangesRequestedAsync("https://github.com/o/r/pull/4", CancellationToken.None);
        var gitlab = await new GitLabMrActReader("https://gitlab.example", "o%2Fr", "t", "https://gitlab.example/o/r",
                GitLabAnswering(p => p.EndsWith("/api/v4/user") ? """{ "id": 1 }""" : p.Contains("/users/") ? """{ "bot": false }"""
                    : p.EndsWith("/notes") ? """[ { "id": 44, "body": "requested changes", "system": true, "created_at": "2026-10-08T10:00:00Z", "author": { "id": 9, "username": "u9" } } ]"""
                    : """{ "iid": 3, "author": { "id": 2 } }"""), NullLogger.Instance)
            .ChangesRequestedAsync("https://gitlab.example/o/r/-/merge_requests/3", CancellationToken.None);
        var azure = AzureReposVoteActs.Standing([VoteThread(8, "v", -5, new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc))],
            [new IdentityRefWithVote { Id = "v", Vote = -5 }], "u", "r", "p");

        github.Single().HostId.Should().Be("R9", "the act's id is the review's node id, the id its body note carries");
        gitlab.Single().HostId.Should().Be("44", "the act's id is the system note's id");
        azure.Single().HostId.Should().Be("8/1", "the act's id is the vote-thread comment's id");
    }
}
