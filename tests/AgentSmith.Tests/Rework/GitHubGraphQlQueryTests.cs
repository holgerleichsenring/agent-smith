using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-25-684b: the reply GitHub sent reaches the reader through Octokit's real connection.</summary>
public sealed class GitHubGraphQlQueryTests
{
    private static readonly Uri Endpoint = new("https://ghe.example/api/graphql");

    [Fact]
    public async Task GitHubGraphQlQuery_RealOctokitConnection_ParsesReplyData()
    {
        var host = new GitHubGraphQlHost();
        var query = new GitHubGraphQlQuery(host.Client((_, _) => """{ "data": { "viewer": { "login": "agent" } } }"""), Endpoint);

        var data = await query.RunAsync("query{viewer{login}}", new { owner = "o" }, "viewer");

        data.GetProperty("viewer").GetProperty("login").GetString().Should().Be("agent");
        host.Requests.Single().Url.Should().Be(Endpoint);
        host.Requests.Single().Variables.GetProperty("owner").GetString().Should().Be("o");
    }

    [Fact]
    public async Task GitHubGraphQlQuery_ErrorsArray_Throws()
    {
        var query = new GitHubGraphQlQuery(GitHubGraphQlHost.Replying("""{ "errors": [ { "message": "nope" } ] }"""), Endpoint);

        var run = () => query.RunAsync("query{viewer{login}}", new { }, "viewer");

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*viewer*nope*");
    }
}
