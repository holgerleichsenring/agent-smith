using System.Text.Json;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-25-9dbbf: a GitHub review is read page by page until every connection is exhausted.</summary>
public sealed class PrReviewPagingTests
{
    private const string PrUrl = "https://github.com/o/r/pull/4";
    private readonly GitHubGraphQlHost _host = new();

    [Fact]
    public async Task ListAsync_150Comments_ReturnsAll150()
    {
        var reader = Reader(query => query switch
        {
            _ when query == GitHubPrReviewQueries.First => Pr(Connection("reviewThreads", [], null), Connection("reviews", [], null),
                Connection("comments", Comments(0, 100), "c100")),
            _ when query == GitHubPrReviewQueries.Comments => Pr(Connection("comments", Comments(100, 50), null)),
            _ => throw new InvalidOperationException($"unexpected query {query}"),
        });

        var threads = await reader.ListAsync(PrUrl, CancellationToken.None);

        threads.Should().HaveCount(150);
        threads[^1].Notes.Single().Body.Should().Be("comment 149");
        _host.Requests[1].Variables.GetProperty("after").GetString().Should().Be("c100");
    }

    [Fact]
    public async Task ListAsync_ThreadWith120Comments_ReturnsAll120()
    {
        var thread = $$"""{ "id": "T1", "isResolved": false, "path": "a.cs", "line": 3, {{Connection("comments", Comments(0, 100), "t100")}} }""";
        var reader = Reader(query => query switch
        {
            _ when query == GitHubPrReviewQueries.First => Pr(Connection("reviewThreads", [thread], null),
                Connection("reviews", [], null), Connection("comments", [], null)),
            _ when query == GitHubPrReviewQueries.ThreadComments => $$"""{ "data": { "node": { {{Connection("comments", Comments(100, 20), null)}} } } }""",
            _ => throw new InvalidOperationException($"unexpected query {query}"),
        });

        var threads = await reader.ListAsync(PrUrl, CancellationToken.None);

        threads.Single().Notes.Should().HaveCount(120);
        _host.Requests[1].Variables.GetProperty("id").GetString().Should().Be("T1");
    }

    private GitHubPrReviewThreadReader Reader(Func<string, string> reply) =>
        new(Client(reply), GitHubRepoAddress.Parse("https://github.com/o/r"), NullLogger.Instance);

    private IGitHubClient Client(Func<string, string> reply) => _host.Client((query, _) => reply(query));

    private static string Pr(params string[] connections) =>
        $$"""{ "data": { "viewer": { "login": "agent" }, "repository": { "pullRequest": { {{string.Join(", ", connections)}} } } } }""";

    private static string Connection(string field, IEnumerable<string> nodes, string? next) =>
        $$"""
        "{{field}}": { "pageInfo": { "hasNextPage": {{(next is null ? "false" : "true")}}, "endCursor": {{(next is null ? "null" : $"\"{next}\"")}} },
          "nodes": [ {{string.Join(", ", nodes)}} ] }
        """;

    private static IEnumerable<string> Comments(int from, int count) =>
        Enumerable.Range(from, count).Select(i =>
            $$"""{ "id": "C{{i}}", "author": { "__typename": "User", "login": "alice" }, "body": "comment {{i}}", "createdAt": "2026-10-08T10:00:00Z" }""");
}
