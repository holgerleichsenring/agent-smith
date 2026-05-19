using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0147f: JiraFieldMapper unit tests against the Jira Cloud REST v3 issue
/// JSON shape. Covers the nested fields envelope, ADF description
/// extraction, and the search-response array shape.
/// </summary>
public sealed class JiraFieldMapperTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static readonly JiraFieldMapper Sut = new();

    [Fact]
    public void Map_NestedFieldsEnvelope_PopulatesTicket()
    {
        var issue = Parse(
            """
            {
              "key": "AS-1",
              "fields": {
                "summary": "T",
                "description": {
                  "type": "doc", "version": 1,
                  "content": [{ "type": "paragraph", "content": [{ "type": "text", "text": "D" }] }]
                },
                "status": { "name": "In Progress" },
                "labels": ["bug", "agent-smith:pending"]
              }
            }
            """);

        var ticket = Sut.Map(new TicketId("AS-1"), issue);

        ticket.Title.Should().Be("T");
        ticket.Description.Should().Be("D");
        ticket.Status.Should().Be("In Progress");
        ticket.Source.Should().Be("Jira");
        ticket.Labels.Should().BeEquivalentTo(["bug", "agent-smith:pending"]);
    }

    [Fact]
    public void Map_NullDescription_BecomesEmptyString()
    {
        var issue = Parse(
            """{ "fields": { "summary": "T", "description": null, "status": { "name": "Open" } } }""");

        var ticket = Sut.Map(new TicketId("1"), issue);

        ticket.Description.Should().Be("");
    }

    [Fact]
    public void MapSearchResponse_IteratesIssuesAndSkipsWithoutKey()
    {
        var root = Parse(
            """
            {
              "issues": [
                { "key": "AS-1", "fields": { "summary": "first",  "status": { "name": "Open" } } },
                { "fields": { "summary": "no-key", "status": { "name": "Open" } } },
                { "key": "AS-2", "fields": { "summary": "second", "status": { "name": "Done" } } }
              ]
            }
            """);

        var tickets = Sut.MapSearchResponse(root);

        tickets.Should().HaveCount(2);
        tickets[0].Id.Value.Should().Be("AS-1");
        tickets[1].Id.Value.Should().Be("AS-2");
    }

    [Fact]
    public void MapSearchResponse_MissingIssuesArray_ReturnsEmpty()
    {
        var root = Parse("""{ "total": 0 }""");

        Sut.MapSearchResponse(root).Should().BeEmpty();
    }
}
