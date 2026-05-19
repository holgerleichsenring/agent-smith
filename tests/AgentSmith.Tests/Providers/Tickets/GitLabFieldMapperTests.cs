using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0147f: GitLabFieldMapper unit tests against the GitLab REST v4 issue
/// JSON shape. Focused on the corner cases the previous inline mapping had
/// to handle: null description, missing labels, non-string label entries.
/// </summary>
public sealed class GitLabFieldMapperTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static readonly GitLabFieldMapper Sut = new();

    [Fact]
    public void Map_PopulatesAllFieldsFromIssueJson()
    {
        var element = Parse(
            """
            { "title": "T", "description": "D", "state": "opened",
              "labels": ["bug", "agent-smith:pending"] }
            """);

        var ticket = Sut.Map(new TicketId("42"), element);

        ticket.Title.Should().Be("T");
        ticket.Description.Should().Be("D");
        ticket.Status.Should().Be("opened");
        ticket.Source.Should().Be("GitLab");
        ticket.Labels.Should().BeEquivalentTo(["bug", "agent-smith:pending"]);
    }

    [Fact]
    public void Map_NullDescription_BecomesEmptyString()
    {
        var element = Parse("""{ "title": "T", "description": null, "state": "opened" }""");

        var ticket = Sut.Map(new TicketId("1"), element);

        ticket.Description.Should().Be("");
    }

    [Fact]
    public void Map_MissingLabels_BecomesEmptyList()
    {
        var element = Parse("""{ "title": "T", "description": "D", "state": "opened" }""");

        var ticket = Sut.Map(new TicketId("1"), element);

        ticket.Labels.Should().BeEmpty();
    }

    [Fact]
    public void MapMany_SkipsIssuesWithoutIid()
    {
        var array = Parse(
            """
            [
              { "iid": 17, "title": "first", "description": "d1", "state": "opened" },
              { "title": "no-iid",   "description": "d2", "state": "opened" },
              { "iid": 18, "title": "second", "description": null, "state": "closed" }
            ]
            """);

        var tickets = Sut.MapMany(array);

        tickets.Should().HaveCount(2);
        tickets[0].Id.Value.Should().Be("17");
        tickets[1].Id.Value.Should().Be("18");
        tickets[1].Description.Should().Be("");
    }

    [Fact]
    public void MapMany_NonArray_ReturnsEmpty()
    {
        var notAnArray = Parse("""{ "issues": [] }""");

        Sut.MapMany(notAnArray).Should().BeEmpty();
    }
}
