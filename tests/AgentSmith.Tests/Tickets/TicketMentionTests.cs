using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// p0454: a parked run posts its question and moves the ticket — and until now
/// addressed it to nobody, so two live runs sat unnoticed for hours. Each platform
/// delivers a notification only for ITS OWN mention form; anything else renders as
/// text that looks like a ping and is not one.
/// </summary>
public sealed class TicketMentionTests
{
    private const string Guid = "3f7c1a2e-9b44-4d0e-8f21-6c5a0d9e1b73";

    [Fact]
    public void AzureDevOps_MentionsByIdentityGuid()
    {
        var line = TicketMention.WaitingLine(
            TrackerType.AzureDevOps, Ticket(Person("Jane Operator", Guid)));

        line.Should().Contain($"data-vss-mention=\"version:2.0,{Guid}\"");
        line.Should().Contain(">@Jane Operator</a>");
    }

    [Fact]
    public void Jira_MentionsByAccountId_NotTheRetiredUsernameForm()
    {
        var line = TicketMention.WaitingLine(
            TrackerType.Jira, Ticket(Person("Jane Operator", "5b10a2844c20165700ede21g")));

        line.Should().Contain("[~accountid:5b10a2844c20165700ede21g]");
        line.Should().NotContain("[~Jane");
    }

    [Fact]
    public void GitHub_MentionsByLogin()
    {
        var line = TicketMention.WaitingLine(
            TrackerType.GitHub, Ticket(Person("Jane Operator", "jane-operator")));

        line.Should().Contain("@jane-operator");
    }

    [Fact]
    public void GitLab_MentionsByUsername()
    {
        var line = TicketMention.WaitingLine(
            TrackerType.GitLab, Ticket(Person("Jane Operator", "jane.operator")));

        line.Should().Contain("@jane.operator");
    }

    [Fact]
    public void WithNoAssignee_TheCommentFallsBackToTheReporter()
    {
        var ticket = Ticket(assignee: null, reporter: Person("Sam Reporter", "sam"));

        var line = TicketMention.WaitingLine(TrackerType.GitHub, ticket);

        line.Should().Contain("@sam");
        line.Should().NotBe(TicketMention.NobodyToNotify);
    }

    [Fact]
    public void WithNobodyOnTheTicket_TheCommentSaysNobodyWasNotified()
    {
        var line = TicketMention.WaitingLine(TrackerType.AzureDevOps, Ticket(assignee: null));

        line.Should().Be(TicketMention.NobodyToNotify);
        line.Should().Contain("nobody was notified");
    }

    [Fact]
    public void AnIdentityWithoutItsProviderId_IsNobodyWeCanReach()
    {
        TicketPerson.From("Jane Operator", providerId: null).Should().BeNull();
        TicketPerson.From(displayName: "  ", providerId: "jane").Should().BeNull();
        TicketPerson.From(" Jane ", " jane ").Should().Be(new TicketPerson("Jane", "jane"));
    }

    private static TicketPerson Person(string displayName, string providerId) =>
        new(displayName, providerId);

    private static Ticket Ticket(TicketPerson? assignee, TicketPerson? reporter = null) =>
        new(new TicketId("19213"), "T", "D", null, "Active", "AzureDevOps", null, assignee, reporter);
}
