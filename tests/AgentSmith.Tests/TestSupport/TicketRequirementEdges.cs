using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-14-dbcef: requirement edges between tickets of one sample tracker connection.</summary>
internal static class TicketRequirementEdges
{
    public const string Tracker = "sample-tracker";
    public const string Project = "sample";

    public static TicketRequirementEdge Edge(
        string ticket, string required, bool startsWhenMet = false, string tracker = Tracker, string project = Project) =>
        new(tracker, $"jira-{ticket}", ticket.ToUpperInvariant(), project, $"jira-{required}", required.ToUpperInvariant(),
            startsWhenMet, default, "conversation-1");
}
