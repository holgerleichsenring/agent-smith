using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0454: GitHub mentions by @login, and an account's Name is optional — so the login
/// carries the identity and doubles as the display name when nobody filled one in.
/// The provider news up its own Octokit client, so the mapper carries the proof
/// (GitHubCommentMapperTests precedent).
/// </summary>
public sealed class GitHubFieldMapperTests
{
    private static readonly GitHubFieldMapper Sut = new();

    [Fact]
    public void AnAssignedIssue_CarriesItsAssigneeAndReporter()
    {
        var issue = Deserialize("""
            {
              "number": 7, "title": "T", "body": "D", "state": "open",
              "assignee": { "login": "jane-operator", "name": "Jane Operator" },
              "user": { "login": "sam-reporter" }
            }
            """);

        var ticket = Sut.Map(new TicketId("7"), issue);

        ticket.Assignee.Should().Be(new TicketPerson("Jane Operator", "jane-operator"));
        ticket.Reporter.Should().Be(new TicketPerson("sam-reporter", "sam-reporter"));
    }

    [Fact]
    public void AnUnassignedIssue_NamesNobody()
    {
        var issue = Deserialize("""{ "number": 7, "title": "T", "state": "open" }""");

        Sut.Map(new TicketId("7"), issue).Assignee.Should().BeNull();
    }

    private static Issue Deserialize(string json) =>
        new SimpleJsonSerializer().Deserialize<Issue>(json);
}
