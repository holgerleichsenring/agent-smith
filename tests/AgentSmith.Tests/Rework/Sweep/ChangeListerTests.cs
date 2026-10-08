using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Rework.Sweep;

/// <summary>2026-10-08-9e6e: the listers page within their budget and in the shape each host answers.</summary>
public sealed class ChangeListerTests
{
    private static readonly DateTimeOffset Since = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sweep_JiraDataCenter_PagesByStartAt()
    {
        var bodies = new List<string>();
        var handler = new Routed((request, body) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/myself")) return """{ "timeZone": "UTC" }""";
            bodies.Add(body);
            var start = body.Contains("\"startAt\":100") ? 100 : 0;
            var count = start == 0 ? 100 : 50;
            var issues = string.Join(",", Enumerable.Range(start, count).Select(i => $$"""{ "key": "A-{{i}}", "fields": { "updated": "2026-10-08T10:00:30.000+0000" } }"""));
            return $$"""{ "startAt": {{start}}, "total": 150, "issues": [ {{issues}} ] }""";
        });
        var lister = new JiraChangedTickets(TicketProviderHttpClient.WithBasicAuth(new HttpClient(handler), "e", "t"),
            "https://jira.example", new JiraEndpoints { Search = "/rest/api/2/search" }, "A");

        var page = await lister.ChangedSinceAsync(Since, 10, CancellationToken.None);

        page.Items.Should().HaveCount(150);
        page.Cut.Should().BeFalse();
        bodies.Should().HaveCount(2);
        bodies[0].Should().Contain("2026/10/08 09:59", "the cursor is floored to the minute and re-reads one").And.Contain("ORDER BY updated ASC");
    }

    [Fact]
    public async Task Sweep_OpenPrsBeyondFirstPage_Seen()
    {
        var replies = new Queue<string>([Page("p1", hasNext: true, "c1"), Page("p2", hasNext: false, null)]);
        var response = new Mock<IApiResponse<string>>();
        response.SetupGet(r => r.Body).Returns(() => replies.Dequeue());
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.Post<string>(It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Connection).Returns(connection.Object);

        var page = await new GitHubChangedPullRequests(client.Object, "o", "r").ChangedSinceAsync(Since, null, 10, CancellationToken.None);

        page.Items.Select(i => i.PrUrl).Should().Equal("https://github.com/o/r/pull/p1", "https://github.com/o/r/pull/p2");
    }

    private static string Page(string id, bool hasNext, string? cursor) => $$"""
        { "data": { "repository": { "pullRequests": { "pageInfo": { "hasNextPage": {{(hasNext ? "true" : "false")}}, "endCursor": {{(cursor is null ? "null" : $"\"{cursor}\"")}} },
          "nodes": [ { "url": "https://github.com/o/r/pull/{{id}}", "headRefName": "agent-smith/1", "isCrossRepository": false,
            "latestReviews": { "nodes": [ { "state": "CHANGES_REQUESTED", "submittedAt": "2026-10-08T11:00:00Z" } ] } } ] } } } }
        """;

    private sealed class Routed(Func<HttpRequestMessage, string, string> answer) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer(request, body), Encoding.UTF8, "application/json") };
        }
    }
}
