using System.Net;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9d: the three hosts' review, mapped from what each returns.</summary>
public sealed class PrReviewReaderTests
{
    private static GitHubPrReviewThreadReader GitHub(string reply)
    {
        var response = new Mock<IApiResponse<string>>();
        response.SetupGet(r => r.Body).Returns(reply);
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.Post<string>(It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Connection).Returns(connection.Object);
        return new GitHubPrReviewThreadReader(client.Object, "o", "r", "https://github.com/o/r", NullLogger.Instance);
    }

    private const string GitHubReply = """
        { "data": { "repository": { "pullRequest": {
          "reviewThreads": { "pageInfo": { "hasNextPage": false }, "nodes": [
            { "isResolved": false, "path": "a.cs", "line": null, "originalLine": 7,
              "comments": { "nodes": [ { "author": { "__typename": "User", "login": "alice" }, "authorAssociation": "MEMBER",
                "body": "rename", "createdAt": "2026-10-08T10:00:00Z", "publishedAt": "2026-10-08T10:00:00Z" } ] } } ] },
          "reviews": { "nodes": [
            { "author": { "__typename": "User", "login": "alice" }, "authorAssociation": "MEMBER", "body": "Please fix the naming", "submittedAt": "2026-10-08T10:05:00Z" },
            { "author": { "__typename": "Bot", "login": "agent[bot]" }, "authorAssociation": "NONE", "body": "", "submittedAt": "2026-10-08T10:06:00Z" } ] },
          "comments": { "nodes": [] } } } } }
        """;

    [Fact]
    public async Task GitHubListAsync_ChangesRequestedReviewBody_IsNote()
    {
        var threads = await GitHub(GitHubReply).ListAsync("https://github.com/o/r/pull/4", CancellationToken.None);

        threads.Should().Contain(t => t.File == null && t.Notes.Single().Body == "Please fix the naming");
        threads.Should().Contain(t => t.File == "a.cs" && t.Line == 7 && t.Resolved == false);
    }

    [Fact]
    public async Task GitHubListAsync_EmptyBodiedReview_Skipped()
    {
        var threads = await GitHub(GitHubReply).ListAsync("https://github.com/o/r/pull/4", CancellationToken.None);

        threads.Should().HaveCount(2);
    }

    [Fact]
    public async Task GitHubListAsync_GraphQlErrors_Throws()
    {
        var act = () => GitHub("""{ "errors": [ { "message": "nope" } ] }""").ListAsync("https://github.com/o/r/pull/4", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GitLabListAsync_Discussions_MapsResolvedAndSystem()
    {
        var handler = new ScriptedGitLab();
        var reader = new GitLabPrReviewThreadReader("https://gitlab.example", "o%2Fr", "t", "https://gitlab.example/o/r",
            new HttpClient(handler), NullLogger.Instance);

        var threads = await reader.ListAsync("https://gitlab.example/o/r/-/merge_requests/3", CancellationToken.None);

        threads.Should().HaveCount(2);
        threads[0].Should().Match<AgentSmith.Contracts.Reviews.PrReviewThread>(t => t.Resolved == true && t.File == "a.cs" && t.Line == 5);
        threads[1].Resolved.Should().BeNull();
        threads[1].Notes.Single().IsSystem.Should().BeTrue();
        threads[0].Notes.Single().IsBot.Should().BeTrue();
    }

    [Fact]
    public void AzureDevOpsListAsync_SoftDeletedComment_Skipped()
    {
        var thread = new GitPullRequestCommentThread
        {
            Status = CommentThreadStatus.Fixed,
            Comments =
            [
                new Comment { Content = "kept", Author = new IdentityRef { Id = "1", UniqueName = "a@x" }, PublishedDate = DateTime.UtcNow },
                new Comment { Content = "gone", IsDeleted = true, Author = new IdentityRef { Id = "2" }, PublishedDate = DateTime.UtcNow },
            ],
        };

        var mapped = AzureReposReviewMapping.Map([thread], "u", "repo", "proj", null);

        mapped.Single().Resolved.Should().BeTrue();
        mapped.Single().Notes.Select(n => n.Body).Should().Equal("kept");
    }

    private sealed class ScriptedGitLab : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var body = request.RequestUri.AbsolutePath.EndsWith("/api/v4/user") ? """{ "id": 99 }"""
                : url.Contains("/users/7") ? """{ "id": 7, "bot": true }"""
                : url.Contains("/users/") ? """{ "id": 8, "bot": false }"""
                : """
                  [ { "notes": [ { "id": 1, "body": "rename", "system": false, "resolvable": true, "resolved": true,
                       "created_at": "2026-10-08T10:00:00Z", "author": { "id": 7, "username": "renovate" },
                       "position": { "new_path": "a.cs", "new_line": 5 } } ] },
                    { "notes": [ { "id": 2, "body": "changed the description", "system": true, "resolvable": false,
                       "created_at": "2026-10-08T10:01:00Z", "author": { "id": 8, "username": "alice" } } ] } ]
                  """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
