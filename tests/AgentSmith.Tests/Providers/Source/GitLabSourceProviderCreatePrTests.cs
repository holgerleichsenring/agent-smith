using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0298: CreatePullRequestAsync is idempotent — a re-run whose branch already has an
/// open MR (GitLab 409) reuses it instead of failing at the PR step.
/// </summary>
public sealed class GitLabSourceProviderCreatePrTests
{
    private const string BaseUrl = "https://gitlab.example.com";
    private const string ProjectPath = "group%2Frepo";
    private const string CloneUrl = "https://gitlab.example.com/group/repo.git";
    private const string Token = "glpat-test";
    private const string DefaultBranch = "main";

    [Fact]
    public async Task CreatePullRequestAsync_NoExistingMr_CreatesAndReturnsUrl()
    {
        var handler = new RoutingHandler((method, _) =>
        {
            method.Should().Be(HttpMethod.Post);
            return Json(HttpStatusCode.Created,
                """{"web_url":"https://gitlab.example.com/group/repo/-/merge_requests/1"}""");
        });
        var sut = CreateSut(handler);

        var url = await sut.CreatePullRequestAsync(Repo("feature/x"), "t", "d", CancellationToken.None);

        url.Should().Be("https://gitlab.example.com/group/repo/-/merge_requests/1");
    }

    [Fact]
    public async Task CreatePullRequestAsync_ExistingMr_ReturnsExistingUrl_NoThrow()
    {
        var handler = new RoutingHandler((method, uri) =>
        {
            if (method == HttpMethod.Post)
                return Json(HttpStatusCode.Conflict,
                    """{"message":["Another open merge request already exists for this source branch"]}""");

            // GET /merge_requests?source_branch=...&state=opened
            uri.Query.Should().Contain("state=opened");
            return Json(HttpStatusCode.OK,
                """[{"web_url":"https://gitlab.example.com/group/repo/-/merge_requests/7"}]""");
        });
        var sut = CreateSut(handler);

        var url = await sut.CreatePullRequestAsync(Repo("feature/x"), "t", "d", CancellationToken.None);

        url.Should().Be("https://gitlab.example.com/group/repo/-/merge_requests/7");
    }

    private static Repository Repo(string branch) => new(new BranchName(branch), CloneUrl);

    private static GitLabSourceProvider CreateSut(HttpMessageHandler handler) =>
        new(new GitLabSourceConnection(BaseUrl, ProjectPath, CloneUrl, Token, DefaultBranch),
            new HttpClient(handler), NullLogger<GitLabSourceProvider>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RoutingHandler(Func<HttpMethod, Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request.Method, request.RequestUri!));
    }
}
