using System.Text.Json;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-25-9dbbf: a GitHub review is read page by page until every connection is exhausted.</summary>
public sealed class PrReviewPagingTests
{
    private const string PrUrl = "https://github.com/o/r/pull/4";
    private readonly List<(string Query, JsonElement Variables)> _asked = [];

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
        _asked[1].Variables.GetProperty("after").GetString().Should().Be("c100");
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
        _asked[1].Variables.GetProperty("id").GetString().Should().Be("T1");
    }

    [Fact]
    public async Task GitHubGraphQlQuery_ErrorsArray_Throws()
    {
        var query = new GitHubGraphQlQuery(Client(_ => """{ "errors": [ { "message": "nope" } ] }"""), new Uri("https://api.github.com/graphql"));

        var run = () => query.RunAsync("query{viewer{login}}", new { }, "viewer");

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*viewer*nope*");
    }

    private GitHubPrReviewThreadReader Reader(Func<string, string> reply) =>
        new(Client(reply), GitHubRepoAddress.Parse("https://github.com/o/r"), NullLogger.Instance);

    private IGitHubClient Client(Func<string, string> reply)
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.Post<string>(It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns<Uri, object, string, string, IDictionary<string, string>, CancellationToken>((_, body, _, _, _, _) =>
            {
                var posted = JsonSerializer.SerializeToElement(body);
                var query = posted.GetProperty("query").GetString()!;
                _asked.Add((query, posted.GetProperty("variables")));
                var response = new Mock<IApiResponse<string>>();
                response.SetupGet(r => r.Body).Returns(reply(query));
                return Task.FromResult(response.Object);
            });
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Connection).Returns(connection.Object);
        return client.Object;
    }

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
