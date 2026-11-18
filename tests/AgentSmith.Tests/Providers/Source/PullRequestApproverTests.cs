using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-17-3435b: a merge-green run's approval — only where the host requires a review. GitHub reads the review
/// decision and submits an APPROVE review; GitLab reads approvals_left and approves; Azure DevOps casts the +10 vote as
/// the token's identity. A host's refusal comes back with its words.
/// </summary>
public sealed class PullRequestApproverTests
{
    [Fact]
    public async Task GitHub_ReviewRequired_SubmitsAnApproveReview()
    {
        var posted = new List<string>();
        var approval = await GitHub("REVIEW_REQUIRED", posted).ApproveIfRequiredAsync("https://github.com/o/r/pull/7", default);

        approval.Outcome.Should().Be(PullRequestApprovalOutcome.Approved);
        posted.Should().ContainSingle(b => b.Contains("\"event\":\"APPROVE\""));
    }

    [Fact]
    public async Task GitHub_NoReviewRequired_IsNotRequired()
    {
        var posted = new List<string>();
        (await GitHub(null, posted).ApproveIfRequiredAsync("https://github.com/o/r/pull/7", default))
            .Outcome.Should().Be(PullRequestApprovalOutcome.NotRequired);
        posted.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, HttpStatusCode.Created, PullRequestApprovalOutcome.Approved)]
    [InlineData(0, HttpStatusCode.Created, PullRequestApprovalOutcome.NotRequired)]
    [InlineData(1, HttpStatusCode.Unauthorized, PullRequestApprovalOutcome.Refused)]
    public async Task GitLab_ApprovesWhileApprovalsAreLeft(int left, HttpStatusCode answer, PullRequestApprovalOutcome expected)
    {
        var http = new HttpClient(new Handler((method, url, _) => method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, $$"""{"approvals_left":{{left}}}""")
            : Json(answer, """{"message":"401 Unauthorized"}""")));

        var approval = await new GitLabApprover("https://gl", "g%2Fr", "t", http).ApproveIfRequiredAsync("https://gl/g/r/-/merge_requests/7", default);

        approval.Outcome.Should().Be(expected);
    }

    [Fact]
    public async Task Azure_CastsThePlusTenVote_AsTheTokensIdentity()
    {
        var git = new Mock<GitHttpClient>(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        git.Setup(c => c.CreatePullRequestReviewerAsync(It.IsAny<IdentityRefWithVote>(), "demo", "sample-server", 7, "id-9",
                It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IdentityRefWithVote());
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClientAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(git.Object);
        factory.Setup(f => f.AuthorizedIdentityIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("id-9");
        var sut = new AzureReposSourceProvider(new AzureReposSourceConnection("https://dev.azure.com/example", "demo", "sample-server", "pat", "main"),
            factory.Object, NullLogger<AzureReposSourceProvider>.Instance);

        var approval = await sut.ApproveIfRequiredAsync("https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7", default);

        approval.Outcome.Should().Be(PullRequestApprovalOutcome.Approved);
        git.Verify(c => c.CreatePullRequestReviewerAsync(It.Is<IdentityRefWithVote>(v => v.Vote == 10), "demo", "sample-server", 7, "id-9",
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GitHubApprover GitHub(string? decision, List<string> posted)
    {
        var handler = new Handler((_, url, body) =>
        {
            if (url.EndsWith("/graphql", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, """{"data":{"repository":{"pullRequest":{"reviewDecision":"""
                    + (decision is null ? "null" : $"\"{decision}\"") + "}}}}");
            posted.Add(body);
            return Json(HttpStatusCode.OK, """{"id":1,"state":"APPROVED"}""");
        });
        var client = new GitHubClient(new Connection(new ProductHeaderValue("test"), GitHubClient.GitHubApiUrl,
            new InMemoryCredentialStore(new Octokit.Credentials("token")), new HttpClientAdapter(() => handler), new SimpleJsonSerializer()));
        return new GitHubApprover(client, GitHubRepoAddress.Parse("https://github.com/o/r"));
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpMethod, string, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            respond(request.Method, request.RequestUri!.ToString(), request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
    }
}
