using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Runs;
using H = AgentSmith.Application.Services.Holds.TicketHoldCases;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: a member ticket's state, its kind and its words. 2026-11-25-9992a: read from the ONE hold the ticket
/// list and page read, never from a reading of its own — landed first; then the hold's CASE: the series' next merge, a
/// question or plan gap, a failure or stop a person decides, a run at work; then a merge others start on, a pull request
/// that waits, the rest waits. An escalated wait (<see cref="TicketHold.Escalation"/>) keeps its case's kind, its words say
/// it is overdue. A ticket nothing holds reads from its newest attempt: none waits, a green one waits, a live one runs, a
/// parked one asks, any other end failed. 2026-11-26-5201f: a pull request waiting for a person's approval is that person's step.
/// </summary>
public static class SeriesTicketState
{
    public static string KindOf(SeriesTicketFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.Landed) return SeriesTicketKinds.Merged;
        if (fact.Hold is not { } hold) return Unheld(fact);
        if (hold.Case == H.MergeNext) return SeriesTicketKinds.Now;
        if (hold.Case is H.Question or H.QuestionPlan or H.QuestionEnvironment || hold.Addressee == TicketHoldAddressees.Plan)
            return SeriesTicketKinds.Question;
        if (hold.Case == H.ApprovalNeeded) return SeriesTicketKinds.Approval; // 2026-11-26-5201f: a person's step, not a failure
        if (hold.Case == H.PrDraft) return SeriesTicketKinds.Draft;
        if (hold.Escalation is null && hold.Addressee is TicketHoldAddressees.Decide or TicketHoldAddressees.Admin)
            return SeriesTicketKinds.Failed;
        if (hold.Case is H.Working or H.ModelWait or H.Queued or H.Resuming or H.Restarting or H.Refreshing) return SeriesTicketKinds.Running;
        return Waits(fact);
    }

    public static string WordsOf(SeriesTicketFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.Landed) return "merged";
        if (fact.Hold is not { } hold) return UnheldWords(fact);
        return SeriesTicketWords.Of(hold, fact)
            + (hold.Escalation is { } late ? $" · overdue past {late.AfterMinutes} min ({late.Key})" : string.Empty);
    }

    private static string Waits(SeriesTicketFact fact) =>
        fact.NeededBy.Count > 0 ? SeriesTicketKinds.Needed : fact.Waiting.Count > 0 ? SeriesTicketKinds.Pr : SeriesTicketKinds.Waiting;

    private static string Unheld(SeriesTicketFact fact) => fact.LatestStatus switch
    {
        null or RunStatuses.Success => Waits(fact),
        RunStatuses.WaitingForInput => fact.PendingQuestion is null ? SeriesTicketKinds.Waiting : SeriesTicketKinds.Question,
        RunStatuses.Running or RunStatuses.Queued => SeriesTicketKinds.Running,
        _ => SeriesTicketKinds.Failed,
    };

    private static string UnheldWords(SeriesTicketFact fact) => fact.LatestStatus switch
    {
        null => "not started",
        RunStatuses.Success => fact.Waiting.Count > 0 ? "green · PR open" : "green",
        RunStatuses.WaitingForInput => fact.PendingQuestion is null ? "waiting for input" : "question",
        RunStatuses.Queued => "queued",
        RunStatuses.Running => "coding",
        var status => fact.EndDetail is { Length: > 0 } why ? $"red · {why}" : $"red · {status}",
    };
}

/// <summary>2026-11-25-9992a: the kinds a series ticket reads as — the Overview's colours, the master's table and the
/// panel's alike.</summary>
public static class SeriesTicketKinds
{
    public const string Merged = "merged", Now = "now", Needed = "needed", Question = "question", Failed = "failed",
        Running = "running", Pr = "pr", Waiting = "waiting";

    /// <summary>2026-11-26-5201f: the host still needs a person's approval on its pull request, or its pull request is a draft.</summary>
    public const string Approval = "approval", Draft = "draft";

    /// <summary>The text is one of the kinds.</summary>
    public static bool Is(string? text) => text is Merged or Now or Needed or Question or Approval or Draft or Failed or Running or Pr or Waiting;
}
