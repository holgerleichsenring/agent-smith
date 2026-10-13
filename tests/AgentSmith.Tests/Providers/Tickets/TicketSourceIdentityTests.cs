using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Octokit;
using Octokit.Internal;
using Comment = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Comment;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-10-13-9a59d: a comment carries the tracker's own id — what a keyword-comment cause stores —
/// and a fetched ticket carries where it opens in its tracker, on all four trackers.
/// </summary>
public sealed class TicketSourceIdentityTests
{
    [Fact]
    public void CommentMappers_EveryTracker_FillTheCommentId()
    {
        var github = new SimpleJsonSerializer().Deserialize<IssueComment>(
            """{ "id": 1001, "body": "b", "created_at": "2026-07-01T10:15:00Z" }""");
        var gitlab = JsonDocument.Parse("""[{ "id": 2002, "body": "b", "created_at": "2026-07-01T10:15:00Z" }]""").RootElement;
        var jira = JsonDocument.Parse("""{ "comments": [{ "id": "3003", "created": "2026-07-01T10:15:00.000+0000" }] }""").RootElement;

        new GitHubCommentMapper().MapMany([github]).Single().Id.Should().Be("1001");
        new GitLabCommentMapper().MapMany(gitlab).Single().Id.Should().Be("2002");
        new JiraCommentMapper().MapMany(jira).Single().Id.Should().Be("3003");
        new AzureDevOpsCommentMapper().MapMany([new Comment { Id = 4004, Text = "b" }]).Single().Id.Should().Be("4004");
    }

    [Fact]
    public async Task TicketProviders_EveryTracker_FillTheWebUrl()
    {
        var issue = new SimpleJsonSerializer().Deserialize<Issue>(
            """{ "number": 42, "title": "t", "state": "open", "html_url": "https://github.com/o/r/issues/42" }""");
        var gitlab = JsonDocument.Parse("""{ "iid": 7, "title": "t", "web_url": "https://gitlab.example.com/g/r/-/issues/7" }""").RootElement;

        new GitHubFieldMapper().Map(new TicketId("42"), issue).WebUrl.Should().Be("https://github.com/o/r/issues/42");
        new GitLabFieldMapper().Map(new TicketId("7"), gitlab).WebUrl.Should().Be("https://gitlab.example.com/g/r/-/issues/7");
        (await Jira().GetTicketAsync(new TicketId("PROJ-42"), default)).WebUrl
            .Should().Be("https://jira.example.com/browse/PROJ-42");
        // The Azure DevOps provider reads through an SDK client no test can substitute; the address
        // is the creator's helper, which GetTicketAsync appends to the mapped ticket.
        new AzureDevOpsTicketCreator("https://dev.azure.com/org", "My Project", (_, _, _) => Task.FromResult(new WorkItem()),
                NullLogger.Instance)
            .WebUrlFor(5).Should().Be("https://dev.azure.com/org/My%20Project/_workitems/edit/5");
    }

    [Fact]
    public void Ticket_With_CarriesEveryProperty()
    {
        var ticket = new Ticket(new TicketId("42"), "Title", "before", "criteria", "Open", "Jira", ["l"],
            TicketPerson.From("Ann", "ann"), TicketPerson.From("Bo", "bo"), "Bug", "https://jira.example.com/browse/42");

        var copy = ticket.With(description: "after");

        copy.Should().BeEquivalentTo(ticket, o => o.Excluding(t => t.Description));
        copy.Description.Should().Be("after");
        ticket.With(webUrl: "https://other").WebUrl.Should().Be("https://other");
        typeof(Ticket).GetProperties().Should().HaveCount(11,
            "a property added to Ticket must be added to With and to this test");
    }

    private static JiraTicketProvider Jira() =>
        new(new JiraTicketConnection("https://jira.example.com", "user@example.com", "token", "PROJ"),
            new HttpClient(new IssueHandler()), new JiraFieldMapper(), NullLogger<JiraTicketProvider>.Instance);

    private sealed class IssueHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "key": "PROJ-42", "fields": { "summary": "t", "status": { "name": "To Do" } } }""",
                    System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
