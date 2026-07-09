using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers;

/// <summary>
/// p0167c: per-provider PostReviewBatchAsync shape. GitHub submits the whole
/// inline batch as ONE review-create call (line/side anchors) + one summary
/// issue comment; GitLab posts one positioned MR discussion per inline + a
/// summary note; AzDO opens one right-file-anchored thread per inline + a
/// context-free summary thread. Marker delete is covered on the GitLab REST
/// path (paginated notes; only marker-prefixed bodies deleted).
/// </summary>
public sealed class PrCommentProviderBatchTests
{
    private static readonly PrReviewSummary Review = new(
        "Summary body",
        [
            new PrReviewInlineComment("src/A.cs", 3, 4, "High", "correctness", "multi-line finding"),
            new PrReviewInlineComment("src/A.cs", 9, 9, "Medium", "style", "single-line finding"),
            new PrReviewInlineComment("src/B.cs", 7, 7, "Low", "test-coverage", "another finding"),
        ]);

    // ---- GitHub ----

    [Fact]
    public async Task GitHubPrCommentProvider_PostReviewBatch_OneApiCallPerBatch()
    {
        var (provider, github, connection) = CreateGitHubSut();
        object? payload = null;
        connection.Setup(c => c.Post(
                It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Uri, object, string, CancellationToken>((_, body, _, _) => payload = body)
            .ReturnsAsync(HttpStatusCode.OK);
        github.Setup(c => c.Issue.Comment.Create("org", "my-api", 42, It.IsAny<string>()))
            .ReturnsAsync((IssueComment)null!);

        await provider.PostReviewBatchAsync("42", Review, CancellationToken.None);

        connection.Verify(c => c.Post(
            new Uri("repos/org/my-api/pulls/42/reviews", UriKind.Relative),
            It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        github.Verify(c => c.Issue.Comment.Create("org", "my-api", 42, "Summary body"), Times.Once);

        var review = payload.Should().BeOfType<Dictionary<string, object>>().Subject;
        review["event"].Should().Be("COMMENT");
        var comments = review["comments"].Should()
            .BeAssignableTo<IReadOnlyList<Dictionary<string, object>>>().Subject;
        comments.Should().HaveCount(3);
        comments[0].Should().Contain("path", "src/A.cs").And.Contain("line", 4)
            .And.Contain("side", "RIGHT").And.Contain("start_line", 3).And.Contain("start_side", "RIGHT");
        comments[1].Should().Contain("line", 9).And.NotContainKey("start_line");
    }

    [Fact]
    public async Task GitHubPrCommentProvider_PostReviewBatch_NoInline_PostsOnlySummaryComment()
    {
        var (provider, github, connection) = CreateGitHubSut();
        github.Setup(c => c.Issue.Comment.Create("org", "my-api", 42, It.IsAny<string>()))
            .ReturnsAsync((IssueComment)null!);

        await provider.PostReviewBatchAsync("42", new PrReviewSummary("Clean", []), CancellationToken.None);

        connection.Verify(c => c.Post(
            It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        github.Verify(c => c.Issue.Comment.Create("org", "my-api", 42, "Clean"), Times.Once);
    }

    private static (GitHubSourceProvider Provider, Mock<IGitHubClient> Client, Mock<IConnection> Connection)
        CreateGitHubSut()
    {
        var connection = new Mock<IConnection>();
        var github = new Mock<IGitHubClient>();
        github.SetupGet(c => c.Connection).Returns(connection.Object);
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create("token")).Returns(github.Object);
        var provider = new GitHubSourceProvider(
            new GitHubSourceConnection("https://github.com/org/my-api", "token"),
            factory.Object, NullLogger<GitHubSourceProvider>.Instance);
        return (provider, github, connection);
    }

    // ---- GitLab ----

    [Fact]
    public async Task GitLabMrCommentProvider_PostReviewBatch_PositionedNotePerInlinePlusSummary()
    {
        var handler = new RecordingHandler(request => request switch
        {
            { Method.Method: "GET" } => Json(HttpStatusCode.OK,
                """{"diff_refs":{"base_sha":"b1","start_sha":"s1","head_sha":"h1"}}"""),
            _ => Json(HttpStatusCode.Created, """{"id":1}"""),
        });
        var provider = CreateGitLabSut(handler);

        await provider.PostReviewBatchAsync("5", Review, CancellationToken.None);

        handler.Requests.Should().HaveCount(5); // 1 diff_refs + 3 discussions + 1 summary note
        handler.Requests[0].Uri.Should().EndWith("/merge_requests/5");
        foreach (var request in handler.Requests.Skip(1).Take(3))
        {
            request.Uri.Should().EndWith("/merge_requests/5/discussions");
            request.Body.Should().Contain("\"position_type\":\"text\"").And.Contain("\"base_sha\":\"b1\"")
                .And.Contain("\"start_sha\":\"s1\"").And.Contain("\"head_sha\":\"h1\"");
        }
        handler.Requests[1].Body.Should().Contain("\"new_path\":\"src/A.cs\"").And.Contain("\"new_line\":4");
        handler.Requests[4].Uri.Should().EndWith("/merge_requests/5/notes");
        handler.Requests[4].Body.Should().Contain("Summary body");
    }

    [Fact]
    public async Task GitLabMrCommentProvider_DeleteCommentsByMarker_DeletesOnlyMarkedNotes()
    {
        var handler = new RecordingHandler(request => request.Method.Method switch
        {
            "GET" => Json(HttpStatusCode.OK, """
                [{"id":11,"body":"<!-- agentsmith:pr-review:src/A.cs:3 -->\nold finding"},
                 {"id":12,"body":"human comment"}]
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent),
        });
        var provider = CreateGitLabSut(handler);

        var deleted = await provider.DeleteCommentsByMarkerAsync(
            "5", "<!-- agentsmith:pr-review:", CancellationToken.None);

        deleted.Should().Be(1);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Method.Should().Be("DELETE");
        handler.Requests[1].Uri.Should().EndWith("/merge_requests/5/notes/11");
    }

    private static GitLabSourceProvider CreateGitLabSut(HttpMessageHandler handler) =>
        new(new GitLabSourceConnection(
                "https://gitlab.example.com", "group%2Frepo",
                "https://gitlab.example.com/group/repo.git", "glpat-test", "main"),
            new HttpClient(handler), NullLogger<GitLabSourceProvider>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<(string Method, string Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri!.GetLeftPart(UriPartial.Path), body));
            return respond(request);
        }
    }

    // ---- Azure DevOps ----

    [Fact]
    public async Task AzureDevOpsPrCommentProvider_PostReviewBatch_ThreadPerInlineWithRightFileAnchor()
    {
        var (provider, gitClient) = CreateAzDoSut();
        var threads = new List<GitPullRequestCommentThread>();
        gitClient.Setup(c => c.CreateThreadAsync(
                It.IsAny<GitPullRequestCommentThread>(), "demo", "repo", 7,
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<GitPullRequestCommentThread, string, string, int, object, CancellationToken>(
                (thread, _, _, _, _, _) => threads.Add(thread))
            .ReturnsAsync(new GitPullRequestCommentThread());

        await provider.PostReviewBatchAsync("7", Review, CancellationToken.None);

        threads.Should().HaveCount(4); // 3 inline + 1 summary
        threads[0].ThreadContext.FilePath.Should().Be("/src/A.cs");
        threads[0].ThreadContext.RightFileStart.Line.Should().Be(3);
        threads[0].ThreadContext.RightFileEnd.Line.Should().Be(4);
        threads[0].Comments[0].Content.Should().Be("multi-line finding");
        threads[3].ThreadContext.Should().BeNull();
        threads[3].Comments[0].Content.Should().Be("Summary body");
    }

    private static (AzureReposSourceProvider Provider, Mock<GitHttpClient> GitClient) CreateAzDoSut()
    {
        var gitClient = new Mock<GitHttpClient>(
            new Uri("https://localhost/fake"),
            new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gitClient.Object);
        var provider = new AzureReposSourceProvider(
            new AzureReposSourceConnection("https://dev.azure.com/example", "demo", "repo", "pat"),
            factory.Object, NullLogger<AzureReposSourceProvider>.Instance);
        return (provider, gitClient);
    }
}
