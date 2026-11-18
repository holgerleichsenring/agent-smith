using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the words a member ticket's facts read as in the table, and their kind. Landed wins; then the
/// newest attempt — running, parked on a question, red with its reason, green with its pull request open; a ticket
/// with no attempt waits on the prerequisites that have not landed, or has not started.
/// </summary>
public static class SeriesTicketState
{
    public const string Landed = "landed", Running = "running", Red = "red", Waiting = "waiting", Held = "held",
        NotStarted = "not started", Green = "green";

    public static (string State, string Kind) Of(SeriesTicketFact fact, IReadOnlyList<SeriesTicketFact> all)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.Landed) return ("landed", Landed);
        if (fact.LatestStatus is { } status) return OfAttempt(fact, status);
        var unmet = fact.Requires.Select(id => all.FirstOrDefault(f => f.Ticket.TicketId == id))
            .Where(f => f is { Landed: false }).Select(f => f!.Ticket.Key).ToList();
        return unmet.Count > 0 ? ($"waits on {string.Join(", ", unmet)}", Held) : ("not started", NotStarted);
    }

    private static (string State, string Kind) OfAttempt(SeriesTicketFact fact, string status)
    {
        if (status == RunStatuses.Queued) return ("queued", Running);
        if (!fact.LatestFinished && status != RunStatuses.WaitingForInput) return ("coding", Running);
        if (status == RunStatuses.WaitingForInput)
            return (fact.PendingQuestion is null ? "waiting for input" : "question", Waiting);
        if (status == RunStatuses.Success) return (fact.OpenedPullRequest ? "green · PR open" : "green", Green);
        return (fact.EndDetail is { Length: > 0 } why ? $"red · {why}" : $"red · {status}", Red);
    }
}
