using System.Net;
using System.Text;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Moq;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: a GitHub API host faked at the HTTP layer under a REAL Octokit client, so a test sees the bytes
/// Octokit puts on the wire — the typed tree body included — and Octokit's own error mapping (404 NotFound, 422
/// validation) answers a refusal. Routes match method and path suffix; the first registered match answers; anything
/// unrouted answers 404.
/// </summary>
internal sealed class GitHubHostFake : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string Suffix, HttpStatusCode Status, string Body)> _routes = [];

    public List<(HttpMethod Method, string PathAndQuery, string? Body)> Requests { get; } = [];

    public GitHubHostFake On(HttpMethod method, string suffix, HttpStatusCode status, string body)
    {
        _routes.Add((method, suffix, status, body));
        return this;
    }

    public string? BodyOf(HttpMethod method, string pathPart) =>
        Requests.LastOrDefault(r => r.Method == method && r.PathAndQuery.Contains(pathPart, StringComparison.Ordinal)).Body;

    public IGitHubClientFactory Factory()
    {
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<GitHubRepoAddress>(), It.IsAny<string>())).Returns(() => new GitHubClient(
            new Connection(new ProductHeaderValue("agent-smith-tests"), new Uri("https://api.github.com/"),
                new InMemoryCredentialStore(Octokit.Credentials.Anonymous), new HttpClientAdapter(() => this),
                new SimpleJsonSerializer())));
        return factory.Object;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var pathAndQuery = request.RequestUri!.PathAndQuery;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request.Method, pathAndQuery, body));
        var route = _routes.FirstOrDefault(r => r.Method == request.Method
            && Uri.UnescapeDataString(pathAndQuery).EndsWith(r.Suffix, StringComparison.Ordinal));
        return route.Suffix is null
            ? Json(HttpStatusCode.NotFound, """{"message":"Not Found"}""")
            : Json(route.Status, route.Body);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
