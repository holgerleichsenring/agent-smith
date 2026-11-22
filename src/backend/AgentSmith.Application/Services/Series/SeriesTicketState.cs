using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the words a member ticket's facts read as in the table, and their kind. Landed wins; then the
/// newest attempt — running, parked on a question, red with its reason, green with its pull request open; a ticket
/// with no attempt waits on the prerequisites that have not landed, or has not started. 2026-11-22-6d85c: an attempt the
/// server's stop interrupted reads restarting, not red, while its one automatic restart is still to come.
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
        if (fact.StartsOnceMerged.Count > 0) return (StartsOnce(fact.StartsOnceMerged, all), Held); // 2026-11-22-6d85d
        var unmet = fact.Requires.Select(id => all.FirstOrDefault(f => f.Ticket.TicketId == id))
            .Where(f => f is { Landed: false }).Select(f => f!.Ticket.Key).ToList();
        return unmet.Count > 0 ? ($"waits on {string.Join(", ", unmet)}", Held) : ("not started", NotStarted);
    }

    // 2026-11-22-6d85d: "starts once SCRUM-44 and SCRUM-45 are merged" — a member by its key, another by its id.
    private static string StartsOnce(IReadOnlyList<string> ids, IReadOnlyList<SeriesTicketFact> all)
    {
        var names = ids.Select(id => all.FirstOrDefault(f => f.Ticket.TicketId == id)?.Ticket.Key ?? $"#{id}").ToList();
        return $"starts once {Claim.RequiresUnmetGate.MergesNamed(names)} {(names.Count == 1 ? "is" : "are")} merged";
    }

    // 2026-11-17-0207d: a green run's pull requests as the CI loop's records see them — the CI loop fixes a red one.
    private static (string State, string Kind) OfCi(SeriesTicketFact fact) => fact.CiState switch
    {
        CiStates.Red => ($"CI red · {fact.CiFailure ?? "a check failed"}", Red),
        CiStates.Pending => ("green · CI running", Green),
        CiStates.Green => ("green · CI green", Green),
        _ => (fact.OpenedPullRequest ? "green · PR open" : "green", Green),
    };

    private static (string State, string Kind) OfAttempt(SeriesTicketFact fact, string status)
    {
        if (status == RunStatuses.Queued) return ("queued", Running);
        if (!fact.LatestFinished && status != RunStatuses.WaitingForInput) return ("coding", Running);
        if (status == RunStatuses.WaitingForInput)
            return (fact.PendingQuestion is null ? "waiting for input" : "question", Waiting);
        if (status == RunStatuses.Success) return OfCi(fact);
        if (fact.Interrupted) // 2026-11-22-6d85c: the server stopped under it; restarted once by itself
            return fact.Restarting ? ("interrupted · restarting", Running) : ("red · interrupted, not restarted by itself", Red);
        return (fact.EndDetail is { Length: > 0 } why ? $"red · {why}" : $"red · {status}", Red);
    }
}
