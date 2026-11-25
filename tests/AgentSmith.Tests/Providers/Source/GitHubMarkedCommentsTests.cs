using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-7a92d: after a refresh pushed, the token's own marked comments on a GitHub pull request are hidden as RESOLVED
/// (minimizeComment, per docs.github.com) across every page; one already hidden, a stranger's and an unmarked one are left.
/// </summary>
public sealed class GitHubMarkedCommentsTests
{
    private const string Marker = "<!-- agent-smith:base-refresh -->";
    private readonly GitHubGraphQlHost _host = new();

    [Fact]
    public async Task MinimizeAsync_OnlyTheTokensOpenMarkedComments_AreHiddenAsResolved_OnEveryPage()
    {
        var client = _host.Client((query, variables) => query.StartsWith("mutation", StringComparison.Ordinal)
            ? """{"data":{"minimizeComment":{"minimizedComment":{"isMinimized":true}}}}"""
            : !variables.TryGetProperty("after", out var after) || after.ValueKind == System.Text.Json.JsonValueKind.Null
                ? Page(true, Comment("ours", true, false, Marker + " refused"), Comment("hidden", true, true, Marker + " old"))
                : Page(false, Comment("theirs", false, false, Marker + " pasted"), Comment("plain", true, false, "LGTM")));

        var hidden = await new GitHubMarkedComments(client, GitHubRepoAddress.Parse("https://github.com/example/sample-server"), NullLogger.Instance)
            .MinimizeAsync("7", Marker, CancellationToken.None);

        hidden.Should().Be(1);
        var mutations = _host.Requests.Where(r => r.Query.StartsWith("mutation", StringComparison.Ordinal)).ToList();
        mutations.Should().ContainSingle().Which.Variables.GetProperty("id").GetString().Should().Be("ours");
        mutations[0].Query.Should().Contain("classifier:RESOLVED");
        _host.Requests.Count(r => r.Query.StartsWith("query", StringComparison.Ordinal)).Should().Be(2, "the second page is read too");
    }

    [Fact]
    public void Create_GitHub_IsAMarkedThreadResolver() =>
        SourceProviderParityTests.Provider(RepoType.GitHub, "https://github.com/example/sample-server").Should().BeAssignableTo<IPrMarkedThreadResolver>();

    private static string Page(bool more, params string[] comments) =>
        "{\"data\":{\"repository\":{\"pullRequest\":{\"comments\":{\"pageInfo\":{\"hasNextPage\":" + (more ? "true" : "false")
        + ",\"endCursor\":\"c1\"},\"nodes\":[" + string.Join(",", comments) + "]}}}}}";

    private static string Comment(string id, bool mine, bool minimized, string body) =>
        $$"""{"id":"{{id}}","body":{{System.Text.Json.JsonSerializer.Serialize(body)}},"isMinimized":{{(minimized ? "true" : "false")}},"viewerDidAuthor":{{(mine ? "true" : "false")}}}""";
}
