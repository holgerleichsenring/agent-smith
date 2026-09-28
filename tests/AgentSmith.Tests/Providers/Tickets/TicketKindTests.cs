using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-27-481bf: what a tracker CALLS a ticket, in the tracker's own word.
/// <para>
/// Nothing inbound carried one: the only kind handling in this tree was outbound, on the two
/// creators that file tickets. The vocabulary is not ours — Azure DevOps process templates and
/// Jira projects both define their own types — so a fixed set of ours would either mislabel
/// something or silently show nothing.
/// </para>
/// </summary>
public sealed class TicketKindTests
{
    [Fact]
    public void TicketKind_AzureDevOps_ReadsTheWorkItemType()
    {
        var fields = new Dictionary<string, object>
        {
            ["System.Title"] = "Widget drops",
            ["System.State"] = "Active",
            ["System.WorkItemType"] = "Produktrückstandselement",
        };

        // A German process template's own word, which no set of ours would have contained.
        new AzureDevOpsFieldMapper().Map(new TicketId("412"), fields)
            .Kind.Should().Be("Produktrückstandselement");
    }

    [Fact]
    public void TicketKind_Jira_ReadsTheIssueType()
    {
        var issue = JsonDocument.Parse(
            """{"fields":{"summary":"Widget drops","issuetype":{"name":"Story"}}}""");

        new JiraFieldMapper().Map(new TicketId("DPG-1"), issue.RootElement)
            .Kind.Should().Be("Story");
    }

    [Fact]
    public void TicketKind_JiraWithoutTheField_IsAbsentRatherThanGuessed()
    {
        var issue = JsonDocument.Parse("""{"fields":{"summary":"Widget drops"}}""");

        new JiraFieldMapper().Map(new TicketId("DPG-1"), issue.RootElement).Kind.Should().BeNull();
    }

    [Fact]
    public void TicketKind_GitLab_ReadsWhicheverTypeFieldIsPresentAndNoneWhenNeitherIs()
    {
        // GitLab carries it under two names across versions, and a self-managed instance below the
        // version that added the uppercase enum returns neither.
        Kind("""{"title":"a","issue_type":"incident"}""").Should().Be("incident");
        // The enum token is spoken rather than shown raw: TEST_CASE is not a word.
        Kind("""{"title":"a","type":"TEST_CASE"}""").Should().Be("Test case");
        Kind("""{"title":"a"}""").Should().BeNull();
    }

    [Fact]
    public void TicketKind_ATicketRewrittenByTheFetchDoor_KeepsItsKind()
    {
        // WithDescription rewrites EVERY fetched ticket positionally, so a field left out of it is
        // one silently dropped everywhere.
        var ticket = new AgentSmith.Domain.Entities.Ticket(
            new TicketId("412"), "Widget drops", "before", null, "Open", "Jira", kind: "Bug");

        ticket.WithDescription("after").Kind.Should().Be("Bug");
    }

    private static string? Kind(string json) =>
        new GitLabFieldMapper()
            .Map(new TicketId("1"), JsonDocument.Parse(json).RootElement).Kind;
}
